using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;
using MtgCore.Cards.Builders;
using MtgSimulator;

namespace MtgSimulator.Tests;

/// <summary>
/// WithDiscard turns every looter into a ChoiceAction that pauses mid-resolution, so a discard
/// deck now makes the AI resolve choices constantly — including the awkward ones: an empty hand,
/// a hand holding only the spell itself, a choice as the very first step of a pipeline.
///
/// These run whole games through the same strategy the Godot client uses. GameRunner catches
/// exceptions into the result rather than rethrowing, so a crash shows up as
/// GameEndReason.UnhandledException with the message attached.
/// </summary>
[TestFixture]
public class DiscardChoiceStabilityTests
{
	/// <summary>Draw 2, then discard 1 — the Tide of Whispers shape, at one mana.</summary>
	private static Card Looter(int ownerId) =>
		CardFactory.Spell("Looter", manaCost: 1).WithDraw(2).WithDiscard().Build() with
		{
			OwnerId = ownerId,
			ControllerId = ownerId,
		};

	/// <summary>Discard 1, then draw 2 — discard first, so the choice is the pipeline's head.</summary>
	private static Card Rummager(int ownerId) =>
		CardFactory.Spell("Rummager", manaCost: 1).WithDiscard().WithDraw(2).Build() with
		{
			OwnerId = ownerId,
			ControllerId = ownerId,
		};

	private static Card Bear(int ownerId) =>
		new()
		{
			Name = "Bear",
			ManaCost = 2,
			OwnerId = ownerId,
			ControllerId = ownerId,
			Components = ImmutableArray.Create<GameComponent>(
				new PermanentComponent(),
				new CreatureComponent { Power = 2, Toughness = 2 }
			),
		};

	private static Card Plains(int ownerId) =>
		new()
		{
			Name = "Plains",
			ManaCost = 0,
			OwnerId = ownerId,
			ControllerId = ownerId,
			Subtypes = ImmutableHashSet.Create("Land"),
		};

	[Test]
	public void DiscardHeavyDeck_PlaysFullGames_WithoutCrashingTheAi()
	{
		var failures = new List<string>();

		for (var seed = 1; seed <= 12; seed++)
		{
			var (state, ids) = MtgGameFactory.Create();
			state = FillLibrary(state, ids.Player1Id, MtgObjectKeys.Player1Library);
			state = FillLibrary(state, ids.Player2Id, MtgObjectKeys.Player2Library);

			var cardNames = state
				.IdToGameObjectMap.Values.OfType<Card>()
				.ToDictionary(c => c.Id, c => c.Name);

			// Same strategy and budget the Godot client runs with.
			var runner = new GameRunner(
				new MultiTurnBeamSearchAiStrategy(
					ids,
					currentTurnDepth: 3,
					lookaheadTurns: 2,
					captureDecisions: true,
					moveTimeBudget: TimeSpan.FromSeconds(1)
				),
				new MultiTurnBeamSearchAiStrategy(
					ids,
					currentTurnDepth: 3,
					lookaheadTurns: 2,
					moveTimeBudget: TimeSpan.FromSeconds(1)
				)
			);

			var (result, _) = runner.Run(
				state,
				ids,
				cardNames,
				shuffleSeed: seed,
				gameRngSeed: seed
			);

			if (result.EndReason == GameEndReason.UnhandledException)
				failures.Add(
					$"seed {seed}: {result.ExceptionMessage}\n{result.ExceptionStackTrace}"
				);
		}

		Assert.That(failures, Is.Empty, string.Join("\n\n", failures));
	}

	/// <summary>
	/// The corner the interactive client hit first: resolving a discard with nothing to discard.
	/// The choice still pauses the pipeline, and every consumer has to cope with zero options.
	/// </summary>
	[Test]
	public void DiscardWithEmptyHand_ResolvesWithoutThrowing()
	{
		var (state, ids) = MtgGameFactory.CreateForTesting();
		var (withSpell, spell) = state.AddObject(Rummager(ids.Player1Id), ids.Player1HandId);

		var (atChoice, _) = withSpell
			.AddAction(new CastSpellAction { CardId = spell.Id, CastingPlayerId = ids.Player1Id })
			.ProcessAllActions();

		Assert.That(atChoice.IsWaitingForChoice, Is.True, "Discard should still pause");

		var choice = atChoice.GetPendingChoice()!;
		Assert.That(choice.Options, Is.Empty, "Hand is empty — nothing to discard");

		var ai = new MultiTurnBeamSearchAiStrategy(ids);
		var selection = ai.ResolveChoice(atChoice, choice, ids.Player1Id);

		Assert.DoesNotThrow(
			() => atChoice.ResolveChoice(selection),
			"An empty-hand discard must resolve, not throw"
		);

		var (final, _) = atChoice.ResolveChoice(selection);
		Assert.That(final.IsWaitingForChoice, Is.False);
		Assert.That(final.HasPendingActions, Is.False, "Resolution must complete");
	}

	private static GameState FillLibrary(GameState state, int playerId, string libraryKey)
	{
		var libraryId = state.GetWellKnownId(libraryKey);
		for (var i = 0; i < 15; i++)
		{
			state = state.AddObject(Plains(playerId), libraryId).GameState;
			state = state.AddObject(Looter(playerId), libraryId).GameState;
			state = state.AddObject(Rummager(playerId), libraryId).GameState;
			state = state.AddObject(Bear(playerId), libraryId).GameState;
		}
		return state;
	}
}
