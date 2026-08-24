using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;
using MtgSimulator;
using MtgSimulator.Scenarios;

namespace MtgSimulator.Tests;

/// <summary>
/// A scenario is only useful if it loads back into a state the AI can actually be asked to move
/// in. GameStateSnapshot renders a position for a human and cannot be loaded; these tests are the
/// guarantee that StateJson is the other thing.
///
/// The strongest assertion here is not field-by-field equality — it is that the SAME EVALUATOR
/// SCORE and the SAME CHOSEN ACTION come out the other side. Those are what the viewer exists to
/// show, so they are what "round-tripped correctly" has to mean. A state that differs in some
/// field the AI never reads is fine; a state that scores differently is a broken scenario.
/// </summary>
[TestFixture]
public class StateJsonTests
{
	/// <summary>
	/// A real board several turns in — cards spread across zones, components attached, mana spent.
	/// The trivial opening position would not exercise the polymorphic converters at all, which is
	/// exactly the part most likely to be wrong.
	///
	/// Actions are picked randomly rather than by a strategy: this fixture is about serialization,
	/// and a beam search here would make every test pay for a full move search per step.
	/// </summary>
	private static (GameState State, MtgGameIds Ids) PlayedGame(int seed, int actions)
	{
		var (state, ids) = MtgGameFactory.Create();
		var rng = new Random(seed);

		foreach (
			var (playerId, libraryId, deckSeed) in new[]
			{
				(ids.Player1Id, ids.Player1LibraryId, seed),
				(ids.Player2Id, ids.Player2LibraryId, seed + 1),
			}
		)
		foreach (
			var card in CardPool.BuildRandomDeck(
				playerId,
				CardLibrary.All,
				rng: new Random(deckSeed)
			)
		)
			(state, _) = state.AddObject(card, parentId: libraryId);

		(state, _) = state.BeginGame(ids.GameId, ids.Player1Id, ids.Player2Id);

		for (var i = 0; i < actions; i++)
		{
			var game = state.TryGetGame();
			if (game == null || state.GetPlayer(ids.Player1Id).HasLost)
				break;
			if (state.GetPlayer(ids.Player2Id).HasLost)
				break;

			if (state.IsWaitingForChoice)
			{
				var choice = state.GetPendingChoice();
				if (choice == null || choice.Options.Count == 0)
					break;
				var picked = choice
					.Options.Take(Math.Max(1, choice.MinChoices))
					.Select(o => o.Id)
					.ToImmutableList();
				(state, _) = state.ResolveChoice(picked);
				continue;
			}

			var legal = MtgActionGenerator.GetLegalActions(state, ids, game.ActivePlayerId);
			if (legal.Count == 0)
				break;
			(state, _) = state.AddAction(legal[rng.Next(legal.Count)]).ProcessAllActions();
		}

		return (state, ids);
	}

	[Test]
	public void APlayedGameState_RoundTrips()
	{
		var (state, _) = PlayedGame(seed: 7, actions: 25);

		var json = StateJson.Serialize(state);
		var loaded = StateJson.Deserialize(json);

		Assert.Multiple(() =>
		{
			Assert.That(loaded.NextId, Is.EqualTo(state.NextId));
			Assert.That(loaded.RngSeed, Is.EqualTo(state.RngSeed));
			Assert.That(
				loaded.IdToGameObjectMap.Count,
				Is.EqualTo(state.IdToGameObjectMap.Count),
				"every object must survive"
			);
			Assert.That(
				loaded.ParentToChildren.Count,
				Is.EqualTo(state.ParentToChildren.Count),
				"zone membership is what makes it a board"
			);
			Assert.That(loaded.ChildToParent.Count, Is.EqualTo(state.ChildToParent.Count));
		});
	}

	[Test]
	public void ARoundTrippedState_ScoresIdentically()
	{
		var (state, ids) = PlayedGame(seed: 11, actions: 30);

		var loaded = StateJson.Deserialize(StateJson.Serialize(state));

		// The whole point. If the score moves, the scenario is not the position it was saved from
		// and every number the viewer shows is about a board that never existed.
		Assert.Multiple(() =>
		{
			Assert.That(
				StateEvaluator.Evaluate(loaded, ids, ids.Player1Id),
				Is.EqualTo(StateEvaluator.Evaluate(state, ids, ids.Player1Id)),
				"player 1 score must be identical"
			);
			Assert.That(
				StateEvaluator.Evaluate(loaded, ids, ids.Player2Id),
				Is.EqualTo(StateEvaluator.Evaluate(state, ids, ids.Player2Id)),
				"player 2 score must be identical"
			);
		});
	}

	[Test]
	public void ARoundTrippedState_OffersTheSameLegalActions()
	{
		var (state, ids) = PlayedGame(seed: 3, actions: 20);
		var active = state.TryGetGame()!.ActivePlayerId;

		var loaded = StateJson.Deserialize(StateJson.Serialize(state));

		var before = MtgActionGenerator.GetLegalActions(state, ids, active);
		var after = MtgActionGenerator.GetLegalActions(loaded, ids, active);

		Assert.That(
			after.Select(a => a.GetType().Name).OrderBy(n => n),
			Is.EqualTo(before.Select(a => a.GetType().Name).OrderBy(n => n)),
			"a scenario the AI has different moves in is a different scenario"
		);
	}

	[Test]
	public void ARoundTrippedState_ProducesTheSameAiDecision()
	{
		var (state, ids) = PlayedGame(seed: 5, actions: 22);
		var active = state.TryGetGame()!.ActivePlayerId;

		var loaded = StateJson.Deserialize(StateJson.Serialize(state));

		// Fresh strategies with equal seeds, so any difference is the state and not the RNG.
		var before = new MultiTurnBeamSearchAiStrategy(ids, rng: new Random(99));
		var after = new MultiTurnBeamSearchAiStrategy(ids, rng: new Random(99));

		Assert.That(
			ActionDescriber.Describe(after.SelectAction(loaded, ids, active), loaded),
			Is.EqualTo(ActionDescriber.Describe(before.SelectAction(state, ids, active), state)),
			"the viewer's whole job is showing what the AI does here; it must do the same thing"
		);
	}

	[Test]
	public void ComponentsSurvive_WithTheirConcreteTypes()
	{
		var (state, ids) = PlayedGame(seed: 13, actions: 30);

		var loaded = StateJson.Deserialize(StateJson.Serialize(state));

		var originalComponents = state
			.IdToGameObjectMap.Values.SelectMany(o => o.Components)
			.Select(c => c.GetType().Name)
			.OrderBy(n => n)
			.ToList();
		var loadedComponents = loaded
			.IdToGameObjectMap.Values.SelectMany(o => o.Components)
			.Select(c => c.GetType().Name)
			.OrderBy(n => n)
			.ToList();

		Assert.Multiple(() =>
		{
			Assert.That(
				originalComponents,
				Is.Not.Empty,
				"the fixture must actually exercise this"
			);
			Assert.That(loadedComponents, Is.EqualTo(originalComponents));
		});
	}

	[Test]
	public void TheActionStack_KeepsItsOrder()
	{
		// Order decides what resolves next, and ImmutableStack enumerates top-first — so a
		// converter that pushes in read order silently inverts it.
		var state = new GameState
		{
			ActionStack = ImmutableStack<GameAction>
				.Empty.Push(new EndTurnAction { GameId = 1 })
				.Push(new EndTurnAction { GameId = 2 })
				.Push(new EndTurnAction { GameId = 3 }),
		};

		var loaded = StateJson.Deserialize(StateJson.Serialize(state));

		Assert.That(
			loaded.ActionStack.Cast<EndTurnAction>().Select(a => a.GameId),
			Is.EqualTo(new[] { 3, 2, 1 })
		);
	}

	[Test]
	public void MetadataValues_KeepTheirTypes()
	{
		// GetMeta<T> casts, so an int that comes back as a boxed JsonElement throws at some
		// unrelated call far from the load.
		var (state, added) = new GameState().AddObject(
			new Zone
			{
				Name = "Tagged",
				Metadata = ImmutableDictionary<string, object>
					.Empty.Add("count", 7)
					.Add("label", "hello")
					.Add("flag", true),
			}
		);

		var loaded = StateJson.Deserialize(StateJson.Serialize(state));
		var obj = loaded.GetObject(added.Id)!;

		Assert.Multiple(() =>
		{
			Assert.That(obj.GetMeta<int>("count"), Is.EqualTo(7));
			Assert.That(obj.GetMeta<string>("label"), Is.EqualTo("hello"));
			Assert.That(obj.GetMeta<bool>("flag"), Is.True);
		});
	}
}
