using ImmutableGameObjects;
using MtgCore;
using MtgSimulator;

namespace MtgSimulator.Tests;

/// <summary>
/// Which cards make ENDING YOUR TURN cost score?
///
/// Those are the cards that induce a horizon effect: if passing the turn is priced as a loss, the
/// AI prefers any free action that defers it, and with a repeatable free action available it will
/// defer forever. Swiftfoot Boots plus Avaricious Dragon is the case that was already found the
/// hard way, via action-limit draws.
///
/// The cost is invisible in win rates, which is what makes a sweep worth it: the card is not weak,
/// the AI just mishandles the shape of its cost.
/// </summary>
[TestFixture]
[Explicit("Diagnostic sweep over the whole set")]
public class EndTurnCostSweep
{
	[Test]
	public void WhichCardsMakeEndingYourTurnExpensive()
	{
		var results = new List<(float Delta, string Name)>();

		foreach (var card in CoresetCube.Set.Cards)
		{
			var (state, ids) = MtgGameFactory.Create();
			state = state.WithoutDeckingLoss();

			// Library filler: rollouts draw, and a decking loss would swamp the signal.
			for (var i = 0; i < 30; i++)
				foreach (
					var (pid, lib) in new[]
					{
						(ids.Player1Id, ids.Player1LibraryId),
						(ids.Player2Id, ids.Player2LibraryId),
					}
				)
					(state, _) = state.AddObject(
						new Card
						{
							Name = "F",
							ManaCost = 99,
							OwnerId = pid,
							ControllerId = pid,
						},
						parentId: lib
					);

			var subject = card;
			var cc = subject.GetComponent<CreatureComponent>();
			if (cc != null)
				subject = (Card)
					subject.WithComponentReplaced(cc with { HasSummoningSickness = false });

			// Permanents only — a card in hand cannot have an end-of-turn trigger fire.
			if (!subject.HasComponent<PermanentComponent>())
				continue;

			(state, _) = state.AddObject(
				subject with
				{
					OwnerId = ids.Player1Id,
					ControllerId = ids.Player1Id,
				},
				parentId: ids.Player1BattlefieldId
			);

			// A hand, or discard/rummage triggers cannot fire and the sweep silently misses the
			// entire family it was written to find. Non-land so CardsInHandWeight counts them.
			for (var i = 0; i < 4; i++)
				(state, _) = state.AddObject(
					new Card
					{
						Name = "H",
						ManaCost = 99,
						OwnerId = ids.Player1Id,
						ControllerId = ids.Player1Id,
					},
					parentId: ids.Player1HandId
				);

			var before = StateEvaluator.Evaluate(state, ids, ids.Player1Id);

			GameState after;
			try
			{
				var endTurn = MtgActionGenerator
					.GetLegalActions(state, ids, ids.Player1Id)
					.OfType<EndTurnAction>()
					.FirstOrDefault();
				if (endTurn == null)
					continue;
				(after, _) = state.AddAction(endTurn).ProcessAllActions();
			}
			catch
			{
				continue;
			}

			var delta = StateEvaluator.Evaluate(after, ids, ids.Player1Id) - before;
			results.Add((delta, card.Name));
		}

		results.Sort();
		var median = results[results.Count / 2].Delta;

		// The universal part is the opponent's draw step, which every board pays and which cancels
		// between two lines that both end the turn. Card-specific EXCESS over that is the signal.
		TestContext.Out.WriteLine($"scanned {results.Count} permanents, median {median:F2}");
		TestContext.Out.WriteLine("--- excess cost over the universal baseline ---");
		foreach (var (d, n) in results.Where(r => r.Delta < median - 0.01f).Take(20))
			TestContext.Out.WriteLine($"  {d - median, 8:F2}   (raw {d, 7:F2})  {n}");
		Assert.Pass();
	}
}
