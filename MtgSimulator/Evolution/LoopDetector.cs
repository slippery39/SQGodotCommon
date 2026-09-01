using System.Collections.Immutable;
using System.Reflection;
using ImmutableGameObjects;
using MtgCore;

namespace MtgSimulator;

/// <summary>
/// Everything about a position that a repeatable line could change, reduced to a comparable value.
///
/// **This is deliberately NOT a `GameState` key, and that distinction is the whole feature.** Two
/// iterations of a loop hold different card INSTANCE ids — every token is a new object — so state
/// equality never fires and a detector built on it reports zero forever while looking correct. What
/// repeats across a loop is the *shape and the resources*, not the identities.
///
/// <see cref="Permanents"/> is keyed by name AND tapped state on purpose. A line that leaves a
/// creature tapped where it started untapped has spent something, so the later position does not
/// dominate the earlier one and the sequence is not repeatable.
///
/// ponytail: counters are not in the key. A loop that only adds counters therefore looks like a
/// repeat of an identical board, which makes domination EASIER to claim — a false positive, which
/// a human reads and dismisses, rather than a false negative, which is silent. Add counters here if
/// the report starts carrying noise.
/// </summary>
public sealed record ResourceFingerprint(
	ImmutableSortedDictionary<string, int> Permanents,
	int Mana,
	int Life,
	int OpponentLife,
	int Hand,
	int Graveyard,
	int StormCount
)
{
	/// <summary>
	/// Quantities that must not go DOWN for a line to be repeatable, opponent life negated so that
	/// "lower is better" reads as "higher is better" like everything else.
	/// </summary>
	private (int, int, int, int, int) Resources => (Mana, Life, -OpponentLife, Hand, StormCount);

	/// <summary>
	/// **Could the line that reached this position be run again, and did it gain anything?**
	///
	/// Dominance rather than equality, because the interesting loops GROW: a token engine ends each
	/// iteration with strictly more permanents than it started, so an equality test would miss every
	/// one of them. The test is: this position has at least everything <paramref name="earlier"/>
	/// had, and strictly more of something.
	///
	/// The graveyard is checked for non-decrease but cannot be the thing that increased — every
	/// spell cast grows it, so counting it as a gain would make any two casts look like an engine.
	/// </summary>
	public bool Dominates(ResourceFingerprint earlier)
	{
		foreach (var (key, count) in earlier.Permanents)
			if (Permanents.GetValueOrDefault(key) < count)
				return false;

		if (Graveyard < earlier.Graveyard)
			return false;

		var (a, b, c, d, e) = Resources;
		var (a0, b0, c0, d0, e0) = earlier.Resources;
		if (a < a0 || b < b0 || c < c0 || d < d0 || e < e0)
			return false;

		var grewPermanents =
			Permanents.Sum(kv => kv.Value) > earlier.Permanents.Sum(kv => kv.Value);
		return grewPermanents || a > a0 || b > b0 || c > c0 || d > d0 || e > e0;
	}

	/// Human-readable account of what a repeat of this line gains. For the report.
	public string GainOver(ResourceFingerprint earlier)
	{
		var parts = new List<string>();
		void Add(string label, int now, int before)
		{
			if (now != before)
				parts.Add($"{label} {before}->{now}");
		}

		Add("permanents", Permanents.Sum(kv => kv.Value), earlier.Permanents.Sum(kv => kv.Value));
		Add("mana", Mana, earlier.Mana);
		Add("life", Life, earlier.Life);
		Add("opp life", OpponentLife, earlier.OpponentLife);
		Add("hand", Hand, earlier.Hand);
		Add("storm", StormCount, earlier.StormCount);
		return parts.Count == 0 ? "nothing" : string.Join(", ", parts);
	}

	public static ResourceFingerprint Of(GameState state, MtgGameIds ids, int playerId)
	{
		var mine = playerId == ids.Player1Id;
		var battlefield = mine ? ids.Player1BattlefieldId : ids.Player2BattlefieldId;
		var hand = mine ? ids.Player1HandId : ids.Player2HandId;
		var graveyard = mine ? ids.Player1GraveyardId : ids.Player2GraveyardId;
		var opponentId = mine ? ids.Player2Id : ids.Player1Id;

		var permanents = ImmutableSortedDictionary.CreateBuilder<string, int>(
			StringComparer.Ordinal
		);
		foreach (var card in state.GetCardsInZone(battlefield))
		{
			// **`HasAttacked` belongs here as much as `IsExhausted` does.** A creature that has
			// already attacked this turn is spent for the purpose of repeating a line, and leaving
			// it out let a board of two vanilla creatures read as a loop.
			var creature = card.GetComponent<CreatureComponent>();
			var spent =
				(creature?.IsExhausted ?? false) ? 'T'
				: (creature?.HasAttacked ?? false) ? 'A'
				: 'U';
			var key = $"{card.Name}|{spent}";
			permanents[key] = permanents.GetValueOrDefault(key) + 1;
		}

		var player = state.GetPlayer(playerId);
		var opponent = state.GetPlayer(opponentId);

		return new ResourceFingerprint(
			permanents.ToImmutable(),
			player?.CurrentMana ?? 0,
			player?.Life ?? 0,
			opponent?.Life ?? 0,
			state.GetChildrenIds(hand).Count(),
			graveyard == 0 ? 0 : state.GetChildrenIds(graveyard).Count(),
			state.TryGetGame()?.SpellsCastThisTurn ?? 0
		);
	}
}

/// One loop found: the actions that close it, and what a repeat gains.
public sealed record LoopFound(IReadOnlyList<string> Line, string Gain, int Iterations);

/// <summary>
/// **Does this position contain a repeatable line that gains something every time?**
///
/// Built as a BALANCE instrument first and a deckbuilding one second. "Did this set change
/// accidentally print an unbounded loop?" is worth asking on every set edit, and the answer "no"
/// is as valuable as the answer "yes" — which is exactly why the detector must be shown to fire on
/// a planted combo before any zero it reports is believed.
///
/// **It reads real game state, so it needs no vocabulary.** The demand-graph approach was tried and
/// closed: `PoolFeatures` harvests membership predicates and trigger events, with no OPERATIONS in
/// it, so a cycle there can only ever mean "these two cards are in the same tribe". This walks the
/// engine instead, and therefore sees a mechanic the day it ships rather than the day someone
/// teaches the harvester about it.
///
/// **The engine's existing guard is `ActivatedAbilityComponent.MaxActivationsPerTurn`** (default 1).
/// The realistic thing this catches is a card where that cap is missing or set high enough not to
/// bind.
/// </summary>
public static class LoopDetector
{
	public const int DefaultDepth = 6;
	public const int DefaultBranching = 8;
	public const int DefaultNodeCap = 4000;
	private const int ChoiceDrainCap = 50;

	/// <summary>
	/// Depth-first over the controller's legal actions, checking every reached position against
	/// every ancestor on the current path.
	///
	/// **Against ancestors on the PATH, not against everything seen.** A loop is a line you can run
	/// again from where it ended; two unrelated branches reaching comparable positions is not one.
	///
	/// `EndTurnAction` is excluded. A turn boundary refills mana and untaps the board, so a
	/// "gain" measured across it is the turn structure rather than a line, and every deck would
	/// report a loop.
	/// </summary>
	public static LoopFound? Find(
		GameState state,
		MtgGameIds ids,
		int playerId,
		int maxDepth = DefaultDepth,
		int maxBranching = DefaultBranching,
		int nodeCap = DefaultNodeCap
	)
	{
		var budget = nodeCap;
		return Search(
			state,
			ids,
			playerId,
			[],
			[ResourceFingerprint.Of(state, ids, playerId)],
			maxDepth,
			maxBranching,
			ref budget
		);
	}

	private static LoopFound? Search(
		GameState state,
		MtgGameIds ids,
		int playerId,
		List<string> line,
		List<ResourceFingerprint> path,
		int depth,
		int maxBranching,
		ref int budget
	)
	{
		if (depth <= 0 || budget <= 0)
			return null;

		IReadOnlyList<GameAction> legal;
		try
		{
			legal = MtgActionGenerator
				.GetLegalActions(state, ids, playerId)
				.Where(a => a is not EndTurnAction)
				.Take(maxBranching)
				.ToList();
		}
		catch
		{
			return null;
		}

		foreach (var action in legal)
		{
			if (budget <= 0)
				return null;
			budget--;

			GameState next;
			try
			{
				next = Apply(state, action, playerId);
			}
			catch
			{
				continue;
			}

			var here = ResourceFingerprint.Of(next, ids, playerId);
			line.Add(Describe(action));

			for (var i = 0; i < path.Count; i++)
			{
				if (!here.Dominates(path[i]))
					continue;

				// **Dominance alone is NOT a loop, and this is the trap the whole detector turns
				// on.** Any one-shot gain produces a position that dominates the one before it — a
				// single "gain 1 life" activation looks identical to an unbounded one after one
				// step. What makes it a loop is that the SAME LINE runs again from where it ended,
				// which is exactly what `MaxActivationsPerTurn` stops it doing. Verified by replay
				// rather than assumed.
				var cycle = line.Skip(i).ToList();
				if (!Repeats(next, ids, playerId, cycle))
					continue;

				return new LoopFound([.. cycle], here.GainOver(path[i]), cycle.Count);
			}

			path.Add(here);
			var found = Search(
				next,
				ids,
				playerId,
				line,
				path,
				depth - 1,
				maxBranching,
				ref budget
			);
			path.RemoveAt(path.Count - 1);
			line.RemoveAt(line.Count - 1);

			if (found is not null)
				return found;
		}

		return null;
	}

	/// <summary>
	/// **Can this exact line be run AGAIN from where it ended, and still gain?** That is the
	/// definition of a loop, and it is what separates an unbounded engine from a one-shot effect.
	///
	/// Steps are re-found by description in a freshly generated legal-action list rather than
	/// replayed as objects, because the objects carry state that has moved on. The matcher is
	/// deliberately strict — same card, same ability index — which makes it CONSERVATIVE: a line
	/// whose steps cannot be identified again fails to confirm and is reported as no loop. For a
	/// balance instrument that is the wrong direction to be wrong in, so treat an unconfirmed
	/// dominance as a gap in this matcher rather than as a clean pool. Same class of problem as
	/// `FindCommittedAction` / `ActionsMatch`, where matching a cast on card id alone silently
	/// re-found X=0.
	/// </summary>
	private static bool Repeats(
		GameState state,
		MtgGameIds ids,
		int playerId,
		IReadOnlyList<string> cycle
	)
	{
		var start = ResourceFingerprint.Of(state, ids, playerId);
		var current = state;

		foreach (var step in cycle)
		{
			GameAction? match;
			try
			{
				match = MtgActionGenerator
					.GetLegalActions(current, ids, playerId)
					.FirstOrDefault(a => Describe(a) == step);
			}
			catch
			{
				return false;
			}

			if (match is null)
				return false;

			try
			{
				current = Apply(current, match, playerId);
			}
			catch
			{
				return false;
			}
		}

		return ResourceFingerprint.Of(current, ids, playerId).Dominates(start);
	}

	/// <summary>
	/// Applies an action and drains the choices it raises.
	///
	/// `ProcessAllActions` deliberately STOPS at a `ChoiceAction` — the AI resolves choices, not the
	/// action loop — so without this every card raising a scry, discard or mode freezes mid-pipeline
	/// and measures as inert. That has cost this project three times; see `CardValueSandbox.Apply`.
	/// Choices are taken by lowest id rather than by an AI, because what is being asked here is
	/// whether a loop EXISTS, not whether an AI would find it.
	/// </summary>
	private static GameState Apply(GameState state, GameAction action, int playerId)
	{
		var (next, ok) = state.TryAddAction(action);
		if (!ok)
			return state;

		(next, _) = next.ProcessAllActions();

		for (var i = 0; i < ChoiceDrainCap && next.IsWaitingForChoice; i++)
		{
			var choice = next.GetPendingChoice();
			if (choice is null)
				break;
			var picks = choice
				.GetOptions(next, null)
				.Where(o => o.IsEnabled)
				.Take(Math.Max(0, choice.MinChoices))
				.Select(o => o.Id)
				.ToImmutableList();
			(next, _) = next.ResolveChoice(picks);
		}

		return next;
	}

	/// <summary>
	/// The identity a step is re-found by in <see cref="Repeats"/>.
	///
	/// **Anything that distinguishes two legal actions must be in here.** The first version returned
	/// a bare type name for anything it did not special-case, so two creatures attacking both read
	/// `"AttackAction"` — and a plain board of two vanilla bears reported a confirmed loop, because
	/// the replay of "attack" matched the OTHER bear. That is the same defect as `ActionsMatch`
	/// re-finding an X-cost cast as X=0, and it produced an equally plausible-looking result.
	///
	/// `CardId` is read by reflection on the property name, the same positional rule
	/// <see cref="EngineProbe"/> and <see cref="PoolFeatures"/> use, so an action type added
	/// tomorrow is distinguished the day it exists rather than the day someone adds a case here.
	/// </summary>
	private static string Describe(GameAction action)
	{
		var type = action.GetType();
		var id =
			type.GetProperty("CardId", BindingFlags.Public | BindingFlags.Instance)
				?.GetValue(action) as int?;
		var index =
			type.GetProperty("AbilityIndex", BindingFlags.Public | BindingFlags.Instance)
				?.GetValue(action) as int?;
		var x =
			type.GetProperty("XValue", BindingFlags.Public | BindingFlags.Instance)
				?.GetValue(action) as int?;

		return $"{type.Name}#{id?.ToString() ?? "-"}/{index?.ToString() ?? "-"}/{x?.ToString() ?? "-"}";
	}
}
