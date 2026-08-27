using ImmutableGameObjects;
using MtgCore;

namespace MtgSimulator;

/// <summary>
/// Sandbox card values, used to score the HAND a choice leaves behind.
///
/// Consumed only by <c>MultiTurnBeamSearchAiStrategy.ResolveChoice</c> — discard, scry, tutor,
/// impulse. It is deliberately not an evaluator term: the evaluator scores every position the
/// search considers, so a hand term there is consulted about land drops and attacks too, and its
/// value moves with <c>MaxMana</c> for reasons that have nothing to do with the hand. Inside one
/// choice the mana is fixed across every option, so that cannot happen.
///
/// **Score the resulting HAND, never the option's card.** Direction then falls out of the state:
/// discarding a bomb leaves a worse hand and scores lower, tutoring one leaves a better hand and
/// scores higher, with nothing anywhere having to know which kind of choice it is looking at.
/// </summary>
public sealed class CardValueTable
{
	private readonly IReadOnlyDictionary<string, float> _values;

	/// <summary>
	/// Scales the whole term against rollout scores. The rollout returns board scores in the tens;
	/// sandbox values run to ~80 for a bomb, so this needs to be well under 1 to inform a choice
	/// rather than overrule the rollout.
	/// </summary>
	public float Weight { get; }

	/// <summary>
	/// Per-turn decay on a card you cannot cast yet, assuming roughly a land a turn. This is what
	/// stops an eight-drop being kept over a playable two-drop on turn one: at two mana it is worth
	/// <c>0.75^6</c> ≈ 18% of its cast value.
	/// </summary>
	public float ReachDiscount { get; }

	public CardValueTable(
		IReadOnlyDictionary<string, float> values,
		float weight = 0.2f,
		float reachDiscount = 0.75f
	)
	{
		_values = values;
		Weight = weight;
		ReachDiscount = reachDiscount;
	}

	/// <summary>Builds from raw JSON — the Godot path, which reads its asset via FileAccess.</summary>
	public static CardValueTable? FromJson(string json, float weight = 0.5f)
	{
		var values = CardValueSandbox.Lookup(CardValueSandbox.FromJson(json), underPressure: true);
		return values.Count == 0 ? null : new CardValueTable(values, weight);
	}

	public static CardValueTable? TryLoad(string setCode, float weight = 0.5f)
	{
		var path = CardValueSandbox.PathFor(setCode);
		if (!File.Exists(path))
			return null;
		var values = CardValueSandbox.Lookup(CardValueSandbox.Load(path), underPressure: true);
		return values.Count == 0 ? null : new CardValueTable(values, weight);
	}

	/// <summary>
	/// How many cards off the top of the library are counted as "about to be yours".
	///
	/// **Without this, scry is uncovered.** A scry moves cards between the top and the bottom of
	/// the library and never touches the hand, so a hand-only valuation is identical across every
	/// option and contributes nothing — measured at 0/6 correct on `ChoiceAccuracyTests` before
	/// this existed, against 6/6 for discard and tutor.
	/// </summary>
	public const int LibraryTopCounted = 3;

	/// <summary>Per-card-down decay on library-top value. You will draw the top card long before the third.</summary>
	public float DrawDiscount { get; init; } = 0.6f;

	/// <summary>
	/// Extra decay per turn of mana you do NOT already hold the land for.
	///
	/// **This is what gives a land in hand its value, and the value is contextual rather than a
	/// constant.** `ReachDiscount` alone prices a four-drop at three lands as "one turn away"
	/// whether or not you are holding the land that gets you there — but those are very different
	/// positions. A land in hand makes next turn's mana certain; without one you have to topdeck it.
	///
	/// So a land is never given a number of its own. Its worth is entirely the difference it makes
	/// to everything else, which falls out with the right sign in every case:
	///
	/// | Position | Land in hand is worth |
	/// |---|---|
	/// | 3 lands, four-drops stuck in hand | a lot — it unlocks all of them |
	/// | 6 lands, nothing costs more than 4 | ~nothing — correctly the first pitch |
	/// | hand full of one-drops | ~nothing — flood |
	///
	/// Before this, a land scored exactly 0 and every spell scored positive, so **pitching the land
	/// always maximised hand value**. The rollout out-argued it at the shipped weight but the margin
	/// halved, and at weight 1.5 the AI started throwing away land drops — see
	/// `LandKeepingChoiceTests`.
	/// </summary>
	public float TopdeckDiscount { get; init; } = 0.6f;

	/// <summary>
	/// Total discounted value of the cards this player is holding or about to draw.
	///
	/// A card absent from the table contributes 0 — exactly average — so an incomplete table
	/// degrades gracefully rather than pricing unknown cards as worthless. Same rule as
	/// <c>DraftPickers.Trained</c>. Lands are skipped for the same reason
	/// <c>CardsInHandWeight</c> skips them: a land held is a resource not yet deployed.
	/// </summary>
	public float HandValue(GameState state, int playerId)
	{
		var maxMana = state.GetPlayer(playerId).MaxMana;
		var hand = state.GetCardsInZone(state.GetPlayerZoneId(playerId, ZoneType.Hand)).ToList();

		// Counted first, because it changes what every OTHER card in the hand is worth. One land
		// drop per turn, so N lands held is N turns of guaranteed mana growth.
		var landsInHand = hand.Count(c => c.HasSubtype("Land"));

		var total = 0f;
		foreach (var card in hand)
			total += Discounted(card, maxMana, landsInHand);

		// The top of the library, decayed by how far down it is. This is what makes scry and other
		// library manipulation visible: those choices reorder the library and leave the hand alone.
		//
		// Top of library is the FIRST card in the zone — DrawCardsAction takes
		// GetChildrenIds(libraryId).FirstOrDefault(). Reading it from the other end silently values
		// the three cards you will draw LAST, which is a scry heuristic that is exactly backwards.
		var depth = 0;
		foreach (
			var card in state
				.GetCardsInZone(state.GetPlayerZoneId(playerId, ZoneType.Library))
				.Take(LibraryTopCounted)
		)
		{
			depth++;
			total += Discounted(card, maxMana, landsInHand) * MathF.Pow(DrawDiscount, depth);
		}

		return total * Weight;
	}

	/// <summary>
	/// A card's value decayed by how far away casting it is, splitting that distance into mana you
	/// already hold the lands for and mana you still have to draw.
	///
	/// A land returns 0 here and that is deliberate — a land is not a threat, it is an enabler, and
	/// its worth is already expressed through the <paramref name="landsInHand"/> every other card
	/// is discounted against. Giving it a number of its own as well would count it twice.
	/// </summary>
	private float Discounted(Card card, int maxMana, int landsInHand)
	{
		if (card.HasSubtype("Land") || !_values.TryGetValue(card.Name, out var value))
			return 0f;

		var turnsAway = Math.Max(0, card.ManaCost - maxMana);
		if (turnsAway == 0)
			return value;

		// Turns covered by a land already in hand are merely LATER; the rest are also UNCERTAIN.
		var speculative = Math.Max(0, turnsAway - landsInHand);
		return value
			* MathF.Pow(ReachDiscount, turnsAway)
			* MathF.Pow(TopdeckDiscount, speculative);
	}
}
