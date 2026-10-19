using System.Collections.Immutable;
using ImmutableGameObjects;
using MagicCore.Cards;
using MagicCore.Effects;

namespace MagicCore;

/// <summary>
/// The ONE place a permanent's power, toughness and keywords are known (CR 613): its own —
/// printed, counters, until-end-of-turn effects (Card.OwnPower / HasOwn) — plus every static
/// ability on the battlefield that applies to it ("creatures you control have haste"). Every rule
/// reads these; reading OwnPower in a rule silently ignores every anthem on the board.
///
/// P/T statics and keyword statics are separate passes, so a keyword static may filter on real
/// power (Tetsuko: "power or toughness 1 or less") without power ever asking about keywords.
///
/// ponytail: no layer dependency ordering (CR 613.8) — statics add up — and a card that has left
/// keeps only its OWN characteristics in last-known information, not what statics gave it then.
/// </summary>
public static class Characteristics
{
	public static int Power(this GameState s, Card card) =>
		card.OwnPower + Statics(s, BoardOf(s), card, ptOnly: true).Sum(a => a.Power);

	public static int Toughness(this GameState s, Card card) =>
		card.OwnToughness + Statics(s, BoardOf(s), card, ptOnly: true).Sum(a => a.Toughness);

	public static bool Has(this GameState s, Card card, string keyword)
	{
		var board = BoardOf(s);
		return (
				card.HasOwn(keyword)
				|| Statics(s, board, card, ptOnly: false).Any(a => a.Keyword == keyword)
			) && !Lost(board, card); // asked last: it's rarely true and this sits under the AI
	}

	/// <summary>
	/// THE read of a permanent's own abilities of one kind — triggered, activated, static, mana,
	/// replacement — empty once it has lost them all. Every rule asks here, never GetComponents, or
	/// "loses all abilities" misses that rule.
	/// </summary>
	public static IEnumerable<T> AbilitiesOf<T>(this GameState s, Card card)
		where T : GameComponent =>
		// The card's own components first: most permanents have none of a kind, and then nothing to lose.
		!card.HasComponent<T>() || s.HasLostAllAbilities(card)
			? []
			: card.GetComponents<T>();

	public static bool HasAbility<T>(this GameState s, Card card)
		where T : GameComponent => s.AbilitiesOf<T>(card).Any();

	/// <summary>
	/// "Loses all abilities" — until end of turn (Flourishing Grapple) or while an Aura says so
	/// (Infinite Coursework). ponytail: no timestamps (CR 613.7) — it loses every ability, even one
	/// granted later, and a P/T it sets for itself (layer 7a) with them. Order them if a card cares.
	/// </summary>
	public static bool HasLostAllAbilities(this GameState s, Card card) => Lost(BoardOf(s), card);

	static bool Lost(Board board, Card card)
	{
		if (board.Stripped.Count > 0 && board.Stripped.Contains((card.Id, card.Incarnation)))
			return true;
		foreach (var component in card.Components) // a loop, not LINQ: every ability read asks
			if (component is UntilEndOfTurn { LosesAllAbilities: true })
				return true;
		return false;
	}

	/// <summary>
	/// "Destroy" and lethal damage don't destroy it (CR 702.12b) — 0 toughness still does. The ONE
	/// answer: the Destroy effect and the state-based check both ask here, so they can't disagree
	/// (MtgCore keeps its two destroy actions in step by the same rule).
	/// </summary>
	public static bool IsIndestructible(this GameState s, Card card) =>
		s.Has(card, "Indestructible");

	record Applied(int Power, int Toughness, string? Keyword);

	/// <summary>
	/// What the battlefield says, found once per (immutable) state — this sits under the AI's
	/// simulations: every static that applies (from a permanent that still has its abilities), and
	/// the permanents an Aura strips of theirs. Never mutated once built.
	/// </summary>
	sealed record Board(
		(Card Source, StaticEffectComponent Effect)[] Statics,
		HashSet<(int, int)> Stripped
	);

	static readonly HashSet<(int, int)> NoneStripped = [];

	// The last board each thread built: the questions about one state come together (the state-based
	// check asks every creature's toughness), so one entry finds nearly all of them. A global
	// ConditionalWeakTable did this before, and cost more than it saved: its lock contended across
	// the search's threads, and every collection scanned a weak handle per state (story AI-7).
	// ponytail: one entry — a caller asking about two states in turn rebuilds the board every time
	// (keep two entries if one ever must), and each thread keeps its last state alive until the next.
	[ThreadStatic]
	static (GameState? State, Board? Board) lastBoard;

	static Board BoardOf(GameState s)
	{
		if (!ReferenceEquals(lastBoard.State, s))
			lastBoard = (s, BuildBoard(s));
		return lastBoard.Board!;
	}

	static Board BuildBoard(GameState state)
	{
		// One pass over the battlefield: it runs for every new state the AI simulates.
		List<(Card, StaticEffectComponent)> statics = [];
		HashSet<(int, int)>? stripped = null;
		foreach (var p in new[] { state.Player1Id(), state.Player2Id() })
		foreach (
			var c in state
				.CardsIn(p, ZoneType.Battlefield)
				.Concat(state.CardsIn(p, ZoneType.Command))
		) // emblems too
		foreach (var component in c.Components)
			if (component is StaticEffectComponent effect)
				statics.Add((c, effect));
			else if (component is EnchantedLosesAllAbilities)
				(stripped ??= []).Add((c.AttachedToId, c.AttachedToIncarnation));
		var lost = new Board([], stripped ?? NoneStripped);
		return lost with
		{
			// A permanent that lost its abilities has no statics either.
			Statics = [.. statics.Where(x => !Lost(lost, x.Item1))],
		};
	}

	/// <summary>Statics apply to permanents on the battlefield — not to a card in hand or a dead card's last-known copy.</summary>
	static IEnumerable<Applied> Statics(GameState s, Board board, Card card, bool ptOnly)
	{
		var all = board.Statics;
		if (
			all.Length == 0
			|| !s.IsOnBattlefield(card.Id)
			|| s.GetCard(card.Id).Incarnation != card.Incarnation
		)
			yield break;
		foreach (var (source, effect) in all)
		{
			// A P/T pass reads only statics that change P/T; a keyword pass only those that grant one.
			var relevant = ptOnly ? effect.ChangesPowerOrToughness : effect.Keyword is not null;
			if (!relevant)
				continue;
			if (!AppliesTo(s, source, effect, card))
				continue;
			var context = ImmutableDictionary<string, object>
				.Empty.Add(ContextKeys.Controller, source.ControllerId)
				.Add(ContextKeys.Source, source.Id);
			yield return new(
				effect.Power.Value(s, context),
				effect.Toughness.Value(s, context),
				effect.Keyword
			);
		}
	}

	/// <summary>
	/// A keyword static's scope may read real power and toughness (CR 613) — Tetsuko — because the
	/// P/T pass never asks about keywords; StaticEffectComponent refuses the scopes that would loop.
	/// `If` must not read keywords (nor, on a P/T static, P/T) — threshold and "this turn" counts only.
	/// </summary>
	static bool AppliesTo(GameState s, Card source, StaticEffectComponent effect, Card card) =>
		effect.Affects.Matches(s, card, new(source.ControllerId, source.Id))
		&& (effect.If is not { } condition || condition.Holds(s, Conditions.Of(source)));
}
