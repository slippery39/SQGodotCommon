using System.Collections.Immutable;
using MtgCore;
using MtgCore.Cards.Builders;

namespace MtgSimulator;

/// <param name="Damage">
/// Starting life minus the opponent's life at the cap. **Unbounded below on purpose** — the
/// opponent cannot lose, so this keeps accumulating instead of saturating at zero, which is what
/// lets one deck be twice as fast as another rather than both reading "won".
/// </param>
public sealed record DeckOutput(int Damage, int Permanents, int CardsDrawn, int Turns);

/// <param name="OverAverage">
/// <paramref name="Raw"/> minus the median across every candidate measured in the same shell — how
/// much better than the average card competing for this slot.
/// </param>
public sealed record CardOutput(string Name, double Raw, double OverAverage);

/// <summary>
/// **Solitaire, with an opponent that cannot die, measuring total OUTPUT rather than a position
/// score.**
///
/// Built because two earlier substrates each failed for a reason worth keeping:
///
/// 1. **`StateEvaluator` cannot see the cards this exists to rank.** It counts `MaxMana` only
///    (temporary mana excluded) and permanent power only (`UntilEndOfTurn` excluded) — both
///    deliberate, both correct for why they were added. Together they make *"Exhaust: add mana
///    equal to the number of Elves you control"* and a tap-to-pump elf score identically to doing
///    nothing: measured, three cards read an identical −0.70, which is just the card leaving hand.
/// 2. **A longer rollout made it worse, not better.** Swept over 4/8/12/20 half-turns in
///    `CardValueSandbox.MeasureInContext`, every card converged to +0.0 — the fixture resolves
///    itself, both arms reach the same terminal, and the difference cancels. Even a genuine +20
///    board card evaporated.
///
/// Damage dealt is an **outcome, not a position score**, so the evaluator's exclusions do not apply
/// to it: if a mana elf buys an extra creature that attacks, the damage appears whether or not
/// anything scores the mana. And an opponent that cannot lose means the game never resolves, so a
/// longer horizon accumulates instead of erasing.
///
/// **This measures theoretical output, never whether the deck wins.** Nothing here may become a
/// fitness function. `Goldfish` SPEED was measured to be the wrong fitness — dismantling Storm made
/// it goldfish faster — and that finding was about comparing DECKS; this compares CARDS inside one
/// fixed shell, which is a different question the confound does not transfer to cleanly. A weaker
/// version still does: at a short horizon a card adding immediate damage beats one adding long-term
/// mana, which is why <see cref="DefaultTurns"/> is 8 rather than 4 and why the turn is a parameter
/// rather than a hidden constant.
/// </summary>
public static class OutputProbe
{
	/// <summary>
	/// Actions a simulated own-turn may take, shared by every pilot in a deckbuilding run.
	///
	/// **An environment variable rather than a console prompt**, matching `MTG_MIN_LANDS`: the mode 6
	/// prompt sequence has been documented wrongly twice, and a piped command that silently answers
	/// the wrong question is worse than a knob nobody finds.
	///
	/// **It must reach the PROBE and not just the field games.** `OutputProbe` is where a card's
	/// context value is measured, so measuring Wirewood Conduit with a pilot that cannot cast it
	/// re-runs the original experiment with the original blind spot.
	/// </summary>
	public static int SelfActionsPerTurn =>
		int.TryParse(Environment.GetEnvironmentVariable("MTG_SELF_ACTIONS"), out var n) && n > 0
			? n
			: 1;

	/// <summary>
	/// Turns simulated. Long enough that a mana engine has bought something and attacked with it,
	/// short enough to run hundreds of times — and deliberately not a constant hidden inside the
	/// measurement, because where it sits decides whether ramp or aggression wins.
	/// </summary>
	public const int DefaultTurns = 8;

	private const string ImmortalName = "__anvil";

	/// <summary>
	/// A permanent on the inert seat carrying <see cref="CannotLoseComponent"/>.
	///
	/// **The life clause only** — `CheckStateBasedEffectsAction.LifeLoss` consults it and decking
	/// still kills, which is why the inert deck is all lands and the cap is well short of a
	/// 60-card library. It suppresses the OUTCOME, not the cause, so life still falls and the
	/// damage number keeps counting.
	/// </summary>
	private static Card Immortal(int ownerId) =>
		CardFactory
			.Enchantment(ImmortalName, manaCost: 0)
			.WithComponent(new CannotLoseComponent())
			.Build() with
		{
			OwnerId = ownerId,
			ControllerId = ownerId,
		};

	public static DeckOutput Play(
		Decklist deck,
		IReadOnlyDictionary<string, Card> pool,
		int seed,
		int turns = DefaultTurns,
		int aiDepth = 2
	)
	{
		// All lands: the inert seat never casts, and a land-only library is also what keeps the
		// decking clause (which CannotLose does NOT cover) away from the cap.
		var inert = Decklist.Empty("Inert") with
		{
			Lands = Decklist.DeckSize,
		};

		var (state, ids, cardNames) = GameSetup.FromDecks(
			owner => deck.Materialize(owner, pool),
			owner => inert.Materialize(owner, pool)
		);

		(state, _) = state.AddObject(Immortal(ids.Player2Id), parentId: ids.Player2BattlefieldId);

		var runner = new GameRunner(
			new MultiTurnBeamSearchAiStrategy(
				ids,
				aiDepth,
				rng: new Random(seed + 1),
				cardValues: AiCardValues.Current,
				selfActionsPerTurn: SelfActionsPerTurn
			),
			new Goldfish.InertStrategy(),
			maxTurns: turns
		);

		var (result, final) = runner.Run(state, ids, cardNames, seed + 2, seed + 3);

		var them = final.GetPlayer(ids.Player2Id);
		return new DeckOutput(
			them.StartingLife - them.Life,
			final.GetCardsInZone(ids.Player1BattlefieldId).Count(),
			result.Player1DrawnCards.Count,
			result.TurnCount
		);
	}

	/// <summary>
	/// Each candidate played as <paramref name="copies"/> copies in the SAME shell, averaged over
	/// <paramref name="seeds"/> shuffles, reported against the median.
	///
	/// Everything but the one slot is held fixed, which is what makes the comparison mean anything —
	/// and it is why this can be far cheaper than a win-rate A/B, which has to resolve whole games
	/// against a field before a 1-3pp difference clears its own noise.
	/// </summary>
	public static IReadOnlyList<CardOutput> Compare(
		IReadOnlyDictionary<string, int> shellSpells,
		int lands,
		IReadOnlyList<string> candidates,
		IReadOnlyDictionary<string, Card> pool,
		int copies = 4,
		int seeds = 3,
		int turns = DefaultTurns,
		int aiDepth = 2
	)
	{
		var raw = new List<(string Name, double Value)>(candidates.Count);

		foreach (var name in candidates)
		{
			// **The candidate is SET to `copies`, never added to what the shell already holds.**
			// A core's candidate list and the deck built from it overlap heavily — an elf shell
			// contains Wirewood Herald, which is also a candidate — so adding produced 8 copies of
			// a 4-of and the list was rejected. It failed for EVERY candidate identically, which is
			// the worst shape a bug can take: a whole run's worth of the feature was inert and only
			// the caller's fallback warning made it visible.
			var spells = shellSpells
				.Where(kv => !string.Equals(kv.Key, name, StringComparison.Ordinal))
				.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);

			// Resize the REST of the shell to leave exactly room for the candidate, so every
			// candidate is measured in a deck of the same size rather than the caller having to
			// pre-size one per candidate.
			//
			// **Both directions, and the grow half is the one that was missing.** Removing a
			// candidate the shell already held leaves it short by `copies`, and a trim-only resize
			// produced a 56-card list — rejected for every candidate identically, which is the same
			// uniform-failure shape as the overlap bug it was introduced to fix.
			var room = Decklist.DeckSize - lands - copies;
			var order = spells.Keys.Order(StringComparer.Ordinal).ToList();

			while (spells.Values.Sum() > room && spells.Count > 0)
			{
				var last = order.LastOrDefault(spells.ContainsKey);
				if (last is null)
					break;
				if (--spells[last] <= 0)
					spells.Remove(last);
			}

			for (var i = 0; spells.Values.Sum() < room; i = (i + 1) % Math.Max(1, order.Count))
			{
				if (order.Count == 0)
					throw new ArgumentException($"no shell to measure {name} in");
				var card = order[i];
				if (spells.GetValueOrDefault(card) >= Decklist.MaxCopies)
					continue;
				spells[card] = spells.GetValueOrDefault(card) + 1;
			}

			spells[name] = copies;

			var deck = new Decklist(
				name,
				spells.ToImmutableSortedDictionary(StringComparer.Ordinal),
				lands
			);

			// A shell that does not add up is a measurement of the wrong deck, and every candidate
			// would fail the same way — silent and uniform, which is the worst shape for a bug.
			if (deck.Validate() is { } invalid)
				throw new ArgumentException($"shell + {copies}x {name} is illegal: {invalid}");

			var total = 0.0;
			for (var s = 0; s < seeds; s++)
				total += Score(Play(deck, pool, 60_000 + s * 101, turns, aiDepth));

			raw.Add((name, total / seeds));
		}

		var sorted = raw.Select(r => r.Value).Order().ToList();
		var median =
			sorted.Count % 2 == 1
				? sorted[sorted.Count / 2]
				: (sorted[sorted.Count / 2 - 1] + sorted[sorted.Count / 2]) / 2.0;

		return [.. raw.Select(r => new CardOutput(r.Name, r.Value, r.Value - median))];
	}

	/// <summary>
	/// Damage is the headline; permanents and cards are counted at a fraction so a pure draw or
	/// ramp engine is not scored as zero for dealing none itself.
	///
	/// **The weights are the subjective part and are deliberately crude.** Nothing here is tuned,
	/// because a tuned aggregate is how a proxy quietly becomes a fitness function. The three
	/// components stay on <see cref="DeckOutput"/> so a caller can see which one is carrying a
	/// number rather than trusting this sum.
	/// </summary>
	public static double Score(DeckOutput o) => o.Damage + 0.5 * o.Permanents + 0.25 * o.CardsDrawn;
}
