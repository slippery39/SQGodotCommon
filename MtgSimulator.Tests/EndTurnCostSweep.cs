using ImmutableGameObjects;
using MtgCore;
using MtgCore.Cards.Builders;
using MtgSimulator;

namespace MtgSimulator.Tests;

/// <summary>
/// Which cards make it cost score to PASS THROUGH a point in the turn?
///
/// Those are the cards that induce a horizon effect: if a moment the AI must eventually reach is
/// priced as a loss, it prefers any free action that defers arriving there, and with a repeatable
/// free action available it will defer forever. Swiftfoot Boots plus Avaricious Dragon is the case
/// that was already found the hard way, via action-limit draws.
///
/// The cost is invisible in win rates, which is what makes a sweep worth it: the card is not weak,
/// the AI just mishandles the shape of its cost.
///
/// Two moments are swept, because they are different halves of the turn and a card sits on one or
/// the other: <b>ending your turn</b> (end-step triggers) and <b>a full turn cycle</b> (upkeep
/// triggers and other recurring costs).
///
/// **The end-turn sweep shipped broken and its old per-card output must not be cited.** Two
/// defects, both now fixed, and both of a kind this project keeps rediscovering:
///
/// 1. **It never resolved choices.** It ended the turn with a bare <c>ProcessAllActions</c>, which
///    STOPS at a pending <c>ChoiceAction</c> — the same half-applied-state bug fixed in the engine
///    itself (see <c>HalfAppliedActionTests</c>). Any card whose trigger asks a question had its
///    cost measured as zero, because the cost had not happened yet. **That is why it missed
///    Avaricious Dragon, the one card it was written to find.**
/// 2. **It injected permanents straight onto the battlefield.** A card that enters as a 0/0 and
///    then gets counters or copies something — Hangarback Walker, Primordial Hydra, Wildwood
///    Scourge, Clone, Phantasmal Image — arrived as a literal 0/0 and was destroyed on arrival by
///    the zero-toughness rule. Those five were the whole top of its outlier list, and all five
///    were artifacts of the harness.
///
/// Cards are now CAST from hand, so the real ETB ceremony runs, and every action is fully drained
/// before anything is scored.
/// </summary>
[TestFixture]
[Explicit("Diagnostic sweep over the whole set")]
public class EndTurnCostSweep
{
	/// <summary>
	/// How much mana above a card's cost is left over for {X}. Arbitrary, but declared rather than
	/// implicit: the generator enumerates X ASCENDING, so taking the first cast action gives X = 0
	/// and puts the Hydras back into the battlefield as 0/0s — which is the exact artifact this
	/// rewrite exists to remove. See the XValue note in MtgSimulator/CLAUDE.md.
	/// </summary>
	private const int XBudget = 3;

	private const int ChoiceDrainCap = 100;

	/// <summary>
	/// Vanilla creatures given to EACH player before the subject card is cast.
	///
	/// **Without a standing board a sacrifice cost prices at zero**, because there is nothing to
	/// sacrifice — which would have made the upkeep sweep blind to exactly the family it exists to
	/// find. Symmetric on purpose: creature count, total power and race pressure are all scored as
	/// differences, so an equal board on both sides cancels completely and the baseline is the same
	/// as it would be on an empty one.
	/// </summary>
	private const int BoardCreatures = 3;

	/// <summary>
	/// Mana cost of the filler cards stuffed into libraries and hands. Roughly a cube's average.
	///
	/// **It was 99, and that is a trap rather than a harmless placeholder.** Filler is inert
	/// because it carries no components, not because it is unaffordable — so the only thing the
	/// cost does is feed cards that READ a mana value. Dark Tutelage ("lose life equal to the
	/// revealed card's mana value") drained 99 and the cycle sweep reported it at **−10205**,
	/// i.e. `LossScore`, as the worst card in the cube by four orders of magnitude.
	///
	/// That is the same class of defect this file was rewritten to remove: a number chosen for the
	/// harness's convenience, silently becoming the finding. Any filler value a card can read has
	/// to be plausible, not merely out of the way.
	/// </summary>
	private const int FillerManaCost = 3;

	/// <summary>Advances the game past the moment being priced. Null means "cannot, skip".</summary>
	private delegate GameState? Advance(GameState state, MtgGameIds ids, IAiStrategy ai);

	/// <summary>
	/// Ending your turn. Catches end-step triggers — Avaricious Dragon's discard is the type case.
	/// </summary>
	[Test]
	public void WhichCardsMakeEndingYourTurnExpensive() =>
		Sweep(
			"ending your turn",
			(state, ids, ai) =>
			{
				var end = EndTurnFor(state, ids, ids.Player1Id);
				return end == null ? null : Apply(state, end, ai, ids.Player1Id);
			}
		);

	/// <summary>
	/// A full turn cycle — your end step, the opponent's whole turn, and back to your own upkeep.
	///
	/// This is the sibling sweep, and it exists because **the end-turn sweep cannot see an upkeep
	/// cost at all**: they are different triggers on different halves of the turn. Call to the
	/// Grave sacrifices each upkeep and would never appear in the other table, which was claimed in
	/// session and was wrong.
	///
	/// Both upkeeps fire inside one cycle, ours and theirs. That is deliberate rather than
	/// sloppy — a symmetric enchantment really does tax both players, and a sweep that only counted
	/// our half would report a symmetric card as a one-sided cost.
	/// </summary>
	[Test]
	public void WhichCardsMakeATurnCycleExpensive() =>
		Sweep(
			"a full turn cycle",
			(state, ids, ai) =>
			{
				var ours = EndTurnFor(state, ids, ids.Player1Id);
				if (ours == null)
					return null;
				state = Apply(state, ours, ai, ids.Player1Id);

				// Ending THEIR turn spawns our StartTurnAction, which is what fires our upkeep.
				var theirs = EndTurnFor(state, ids, ids.Player2Id);
				return theirs == null ? null : Apply(state, theirs, ai, ids.Player2Id);
			}
		);

	private static void Sweep(string moment, Advance advance)
	{
		var results = new List<(float Delta, string Name)>();
		var skipped = new Dictionary<string, int>();

		void Skip(string reason) =>
			skipped[reason] = skipped.TryGetValue(reason, out var n) ? n + 1 : 1;

		foreach (var card in CoresetCube.Set.Cards)
		{
			// A card in hand cannot have a trigger fire.
			if (!card.HasComponent<PermanentComponent>())
			{
				Skip("not a permanent");
				continue;
			}

			var (state, ids) = Table();

			// The subject goes to HAND and is cast, rather than being placed on the battlefield.
			// Casting is what runs PutIntoBattlefieldAction's ETB ceremony: entry counters, copy
			// effects, ETB triggers. Injection skips all of it.
			Card subject;
			(state, subject) = state.AddObject(
				card with
				{
					OwnerId = ids.Player1Id,
					ControllerId = ids.Player1Id,
				},
				parentId: ids.Player1HandId
			);
			var subjectId = subject.Id;

			var ai = new MultiTurnBeamSearchAiStrategy(ids, rng: new Random(7));

			GameState cast;
			try
			{
				var castAction = MtgActionGenerator
					.GetLegalActions(state, ids, ids.Player1Id)
					.Where(a => CardIdOf(a) == subjectId)
					// Highest X within budget. FirstOrDefault would take X = 0 — see XBudget.
					.Where(a => XOf(a) <= XBudget)
					.OrderByDescending(XOf)
					.FirstOrDefault();

				if (castAction == null)
				{
					Skip("no legal cast action");
					continue;
				}

				cast = Apply(state, castAction, ai, ids.Player1Id);
			}
			catch
			{
				Skip("threw while casting");
				continue;
			}

			// It may have been countered, fizzled, or died on arrival for a reason that is about the
			// card rather than about the harness. Either way there is nothing on the board to
			// measure, and scoring it anyway is how the old version reported artifacts as findings.
			if (!cast.GetCardsInZone(ids.Player1BattlefieldId).Any(c => c.Id == subjectId))
			{
				Skip("did not reach the battlefield");
				continue;
			}

			var before = StateEvaluator.Evaluate(cast, ids, ids.Player1Id);

			GameState? after;
			try
			{
				after = advance(cast, ids, ai);
			}
			catch
			{
				Skip("threw while advancing");
				continue;
			}

			if (after == null)
			{
				Skip("could not advance");
				continue;
			}

			results.Add((StateEvaluator.Evaluate(after, ids, ids.Player1Id) - before, card.Name));
		}

		results.Sort();
		var median = results[results.Count / 2].Delta;

		// The universal part is what every board pays and what cancels between two lines that both
		// reach this moment. Card-specific EXCESS over that is the signal.
		TestContext.Out.WriteLine(
			$"=== {moment}: scanned {results.Count} permanents, median {median:F2} ==="
		);
		TestContext.Out.WriteLine("--- excess cost over the universal baseline ---");
		foreach (var (d, n) in results.Where(r => r.Delta < median - 0.01f).Take(20))
			TestContext.Out.WriteLine($"  {d - median, 8:F2}   (raw {d, 7:F2})  {n}");

		// Coverage is reported, not buried. The old version continued past three separate failure
		// paths in silence, so a run that measured forty cards and a run that measured four hundred
		// produced output of exactly the same shape.
		TestContext.Out.WriteLine("--- not measured ---");
		foreach (var (reason, n) in skipped.OrderByDescending(kv => kv.Value))
			TestContext.Out.WriteLine($"  {n, 4}  {reason}");

		// Coverage as a ratio, not a card count, so growing the set cannot break this — but a change
		// that makes a chunk of the set uncastable still fails instead of quietly narrowing the
		// sweep to whatever still works. Measured at 93% (282 of 303) when this was written.
		var permanents =
			results.Count + skipped.Where(kv => kv.Key != "not a permanent").Sum(kv => kv.Value);
		Assert.That(
			100.0 * results.Count / Math.Max(1, permanents),
			Is.GreaterThan(85.0),
			$"only {results.Count} of {permanents} permanents were measured — a sweep that silently "
				+ "narrows to the cards that still happen to work is how the previous version "
				+ "reported five artifacts as findings"
		);
	}

	/// <summary>
	/// A symmetric starting position: stocked libraries, a hand, and an equal board each side.
	/// </summary>
	private static (GameState State, MtgGameIds Ids) Table()
	{
		// 99 mana both sides. It is constant across the before/after pair so it cancels out of the
		// delta, and it decouples affordability from X — X is chosen explicitly by the caller.
		var (state, ids) = MtgGameFactory.CreateForTesting();
		state = state.WithoutDeckingLoss();

		var seats = new[]
		{
			(ids.Player1Id, ids.Player1LibraryId, ids.Player1BattlefieldId),
			(ids.Player2Id, ids.Player2LibraryId, ids.Player2BattlefieldId),
		};

		foreach (var (pid, lib, battlefield) in seats)
		{
			// Library filler: turns draw, and a decking loss would swamp the signal.
			for (var i = 0; i < 30; i++)
				(state, _) = state.AddObject(
					new Card
					{
						Name = "F",
						ManaCost = FillerManaCost,
						OwnerId = pid,
						ControllerId = pid,
					},
					parentId: lib
				);

			// Sacrifice fodder and attack targets. See BoardCreatures.
			for (var i = 0; i < BoardCreatures; i++)
			{
				var bear = CardFactory
					.Creature("Bear", manaCost: 2, power: 2, toughness: 2)
					.Build();
				var cc = bear.GetComponent<CreatureComponent>()!;
				bear = (Card)bear.WithComponentReplaced(cc with { HasSummoningSickness = false });
				(state, _) = state.AddObject(
					bear with
					{
						OwnerId = pid,
						ControllerId = pid,
					},
					parentId: battlefield
				);
			}
		}

		// A hand, or discard/rummage triggers cannot fire and the sweep silently misses the entire
		// family it was written to find. Non-land so CardsInHandWeight counts them.
		for (var i = 0; i < 4; i++)
			(state, _) = state.AddObject(
				new Card
				{
					Name = "H",
					ManaCost = FillerManaCost,
					OwnerId = ids.Player1Id,
					ControllerId = ids.Player1Id,
				},
				parentId: ids.Player1HandId
			);

		return (state, ids);
	}

	private static EndTurnAction? EndTurnFor(GameState state, MtgGameIds ids, int playerId) =>
		MtgActionGenerator
			.GetLegalActions(state, ids, playerId)
			.OfType<EndTurnAction>()
			.FirstOrDefault();

	/// <summary>
	/// Applies an action and drains every choice it raises, so what gets scored is a position
	/// rather than a half-finished resolution.
	///
	/// <c>ProcessAllActions</c> deliberately stops at a <c>ChoiceAction</c> — the AI resolves
	/// choices, not the action loop. A diagnostic that calls it alone leaves any card containing a
	/// discard, a scry or a mode frozen mid-pipeline, and every such card then measures as inert.
	/// That has now cost this project twice: once in <c>GreenLowWinRateAuditTests</c>, and once
	/// here, where it hid the single card the sweep was written to detect.
	/// </summary>
	private static GameState Apply(GameState state, GameAction action, IAiStrategy ai, int playerId)
	{
		var (next, ok) = state.TryAddAction(action);
		if (!ok)
			return state;

		(next, _) = next.ProcessAllActions();

		for (var i = 0; i < ChoiceDrainCap && next.IsWaitingForChoice; i++)
		{
			var choice = next.GetPendingChoice();
			if (choice == null)
				break;
			// AS ITS OWNER. Resolving our own end-step trigger from the opponent's perspective is a
			// real bug this project has already shipped once — see ResolveAllChoices.
			var owner = next.GetPendingChoiceDecidingPlayerId();
			(next, _) = next.ResolveChoice(
				ai.ResolveChoice(next, choice, owner == 0 ? playerId : owner)
			);
		}

		return next;
	}

	private static int CardIdOf(GameAction action) =>
		action switch
		{
			CastCreatureAction c => c.CardId,
			CastPermanentAction p => p.CardId,
			_ => -1,
		};

	// Only creature spells carry X among the permanent casts; CastPermanentAction has no XValue.
	private static int XOf(GameAction action) =>
		action switch
		{
			CastCreatureAction c => c.XValue,
			_ => 0,
		};
}
