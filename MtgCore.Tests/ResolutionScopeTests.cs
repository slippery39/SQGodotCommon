using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;
using NUnit.Framework;

namespace MtgCore.Tests;

/// <summary>
/// Tests that SuppressPostProcessor correctly defers state-based effect checks
/// until a spell or ability has fully resolved.
///
/// The key invariant: nothing the PostActionProcessor does (creature death,
/// win/loss checks, etc.) should take effect mid-resolution. It must wait
/// until EndResolutionScopeAction clears SuppressPostProcessor.
/// </summary>
[TestFixture]
public class ResolutionScopeTests
{
	private GameState _state;
	private MtgGameIds _ids;

	[SetUp]
	public void Setup()
	{
		(_state, _ids) = MtgGameFactory.CreateForTesting();
	}

	// ===== SuppressPostProcessor FLAG =====

	[Test]
	public void CastingSpell_SetsSuppressPostProcessor_DuringResolution()
	{
		// We step through actions manually so we can inspect the state mid-resolution.
		// CastSpellAction runs first (moves card to stack, spawns ResolveSpellAction).
		// ResolveSpellAction sets SuppressPostProcessor = true and spawns effects.
		// We confirm the flag is true before effects and EndResolutionScopeAction run.

		var (stateWithCreatures, _, _) = SetupTwoCreaturesOnOpponentBattlefield();
		var (stateWithCard, card) = stateWithCreatures.AddObject(
			MakePyroclasm(_ids.Player1Id),
			parentId: _ids.Player1HandId
		);

		var state = stateWithCard.AddAction(
			TestCardFactory.MakeCastAction(card.Id, _ids.Player1Id)
		);

		// CastSpellAction
		(state, _) = state.ProcessNextAction();
		// ResolveSpellAction â€” sets SuppressPostProcessor, spawns DealDamageActions + EndScope
		(state, _) = state.ProcessNextAction();

		Assert.That(
			state.SuppressPostProcessor,
			Is.True,
			"SuppressPostProcessor should be true while spell effects are pending"
		);
	}

	[Test]
	public void AfterSpellFullyResolves_SuppressPostProcessor_IsFalse()
	{
		var (stateWithCreatures, _, _) = SetupTwoCreaturesOnOpponentBattlefield();
		var (stateWithCard, card) = stateWithCreatures.AddObject(
			MakePyroclasm(_ids.Player1Id),
			parentId: _ids.Player1HandId
		);

		var (finalState, _) = stateWithCard
			.AddAction(TestCardFactory.MakeCastAction(card.Id, _ids.Player1Id))
			.ProcessAllActions();

		Assert.That(
			finalState.SuppressPostProcessor,
			Is.False,
			"SuppressPostProcessor should be false after full resolution"
		);
	}

	// ===== SBE DEFERRED DURING RESOLUTION =====

	[Test]
	public void CreatureWithLethalDamage_DoesNotDie_MidSpellResolution()
	{
		// Pyroclasm deals 2 damage to all creatures.
		// With two 1/1s on the battlefield, the first takes lethal damage.
		// SBE is deferred, so it should NOT move to the graveyard until
		// after the spell fully resolves (i.e. after EndResolutionScopeAction runs).

		var (stateWithCreatures, creature1Id, _) = SetupTwoCreaturesOnOpponentBattlefield(
			power: 1,
			toughness: 1
		);
		var (stateWithCard, card) = stateWithCreatures.AddObject(
			MakePyroclasm(_ids.Player1Id),
			parentId: _ids.Player1HandId
		);

		var state = stateWithCard.AddAction(
			TestCardFactory.MakeCastAction(card.Id, _ids.Player1Id)
		);

		// CastSpellAction
		(state, _) = state.ProcessNextAction();
		// ResolveSpellAction â€” sets SuppressPostProcessor, spawns 2x DealDamageAction + EndScope
		(state, _) = state.ProcessNextAction();
		// First DealDamageAction â€” creature 1 takes lethal damage
		(state, _) = state.ProcessNextAction();

		Assert.That(
			state.SuppressPostProcessor,
			Is.True,
			"PostProcessor should still be suppressed after first damage action"
		);
		Assert.That(
			state.HasObject(creature1Id),
			Is.True,
			"Creature with lethal damage should still exist mid-resolution â€” SBE is deferred"
		);
	}

	[Test]
	public void BothCreatures_DieAfterSpellFullyResolves()
	{
		var (stateWithCreatures, _, _) = SetupTwoCreaturesOnOpponentBattlefield(
			power: 1,
			toughness: 1
		);
		var (stateWithCard, card) = stateWithCreatures.AddObject(
			MakePyroclasm(_ids.Player1Id),
			parentId: _ids.Player1HandId
		);

		var (finalState, _) = stateWithCard
			.AddAction(TestCardFactory.MakeCastAction(card.Id, _ids.Player1Id))
			.ProcessAllActions();

		Assert.That(
			finalState.GetCardsInZone(_ids.Player2BattlefieldId).Count(),
			Is.EqualTo(0),
			"Both creatures should be gone from the battlefield after spell resolves"
		);
		Assert.That(
			finalState.GetCardsInZone(_ids.Player2GraveyardId).Count(),
			Is.EqualTo(2),
			"Both creatures should be in the graveyard"
		);
	}

	// ===== WIN CONDITION DEFERRED =====

	[Test]
	public void PlayerAtZeroLife_MidResolution_DoesNotLose_UntilSpellFullyResolves()
	{
		// A spell with two separate DealDamage effects targeting the opponent.
		// Opponent starts at 2 life and drops to 0 after the first effect.
		// HasLost should NOT be set until after the spell fully resolves.

		var player2 = _state.GetPlayer(_ids.Player2Id);
		var stateWithLowLife = _state.UpdateObject(_ids.Player2Id, player2 with { Life = 2 });

		var (stateWithCard, card) = stateWithLowLife.AddObject(
			MakeTwoHitSpell(_ids.Player1Id),
			parentId: _ids.Player1HandId
		);

		// Two-hit spell targets the opponent player directly â€” no user targeting needed
		// because we use TargetingStrategy.AllValid with IsPlayerSpecification filtered
		// to the opponent. We use a cast with no explicit targets since it's AllValid.
		var state = stateWithCard.AddAction(
			TestCardFactory.MakeCastAction(card.Id, _ids.Player1Id)
		);

		// CastSpellAction
		(state, _) = state.ProcessNextAction();
		// ResolveSpellAction
		(state, _) = state.ProcessNextAction();
		// First DealDamageAction â€” opponent drops to 0
		(state, _) = state.ProcessNextAction();

		Assert.That(
			state.GetPlayer(_ids.Player2Id).HasLost,
			Is.False,
			"Player should not be marked as lost mid-resolution â€” SBE is deferred"
		);

		var (finalState, events) = state.ProcessAllActions();

		Assert.That(
			finalState.GetPlayer(_ids.Player2Id).HasLost,
			Is.True,
			"Player should be marked as lost after full resolution"
		);
		Assert.That(events.OfType<PlayerLostEvent>().Any(), Is.True);
	}

	// ===== ACTIVATED ABILITIES =====

	[Test]
	public void ActivatedAbility_SetsSuppressPostProcessor_DuringResolution()
	{
		var (stateWithCard, cardId) = AddProdigalSorcererToBattlefield(_state);

		var state = stateWithCard.AddAction(
			new ActivateAbilityAction
			{
				CardId = cardId,
				ActivatingPlayerId = _ids.Player1Id,
				AbilityIndex = 0,
				TargetIds = ImmutableList.Create(_ids.Player2Id),
			}
		);

		// ActivateAbilityAction â€” sets SuppressPostProcessor, spawns effect + EndScope
		(state, _) = state.ProcessNextAction();

		Assert.That(
			state.SuppressPostProcessor,
			Is.True,
			"SuppressPostProcessor should be true while ability effects are pending"
		);
	}

	[Test]
	public void ActivatedAbility_ClearsSuppressPostProcessor_AfterFullResolution()
	{
		var (stateWithCard, cardId) = AddProdigalSorcererToBattlefield(_state);

		var (finalState, _) = stateWithCard
			.AddAction(
				new ActivateAbilityAction
				{
					CardId = cardId,
					ActivatingPlayerId = _ids.Player1Id,
					AbilityIndex = 0,
					TargetIds = ImmutableList.Create(_ids.Player2Id),
				}
			)
			.ProcessAllActions();

		Assert.That(
			finalState.SuppressPostProcessor,
			Is.False,
			"SuppressPostProcessor should be false after ability fully resolves"
		);
	}

	// ===== HELPERS =====

	/// <summary>
	/// A Pyroclasm-style spell: deals 2 damage to all creatures automatically (AllValid).
	/// No user target selection required.
	/// </summary>
	private Card MakePyroclasm(int ownerId) =>
		TestCardFactory.MakeSpellCard(
			"Pyroclasm",
			ownerId,
			new CardEffect
			{
				TargetingStrategy = TargetingStrategy.AllValid(new IsCreatureSpecification()),
				ActionTemplate = new DealDamageAction { Amount = 2 },
			},
			manaCost: 0
		);

	/// <summary>
	/// A spell with two separate effects, each dealing 2 damage to all opponent players.
	/// Used to test that win conditions are not checked between the two hits.
	/// </summary>
	private Card MakeTwoHitSpell(int ownerId)
	{
		var effect = new CardEffect
		{
			TargetingStrategy = TargetingStrategy.AllValid(
				new IsPlayerSpecification().And(new IsControlledByOpponentSpecification())
			),
			ActionTemplate = new DealDamageAction { Amount = 2 },
		};

		return TestCardFactory.MakeSpellCard(
			"Two Hit",
			ownerId,
			ImmutableList.Create(effect, effect),
			manaCost: 0
		);
	}

	/// <summary>
	/// Places two creatures on Player 2's battlefield and returns their IDs.
	/// </summary>
	private (
		GameState state,
		int creature1Id,
		int creature2Id
	) SetupTwoCreaturesOnOpponentBattlefield(int power = 2, int toughness = 2)
	{
		var c1 = TestCardFactory.MakeCreatureCard("Creature A", _ids.Player2Id, power, toughness);
		var c2 = TestCardFactory.MakeCreatureCard("Creature B", _ids.Player2Id, power, toughness);

		var (s1, added1) = _state.AddObject(c1, parentId: _ids.Player2BattlefieldId);
		var (s2, added2) = s1.AddObject(c2, parentId: _ids.Player2BattlefieldId);

		return (s2, added1.Id, added2.Id);
	}

	/// <summary>
	/// Places a Prodigal Sorcerer (from CardLibrary) on Player 1's battlefield.
	/// Its ping ability targets any player or creature for 1 damage.
	/// </summary>
	private (GameState state, int cardId) AddProdigalSorcererToBattlefield(GameState state)
	{
		var sorcerer = TestCardLibrary.ProdigalSorcerer() with
		{
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
		};

		var (newState, added) = state.AddObject(sorcerer, parentId: _ids.Player1BattlefieldId);

		// Clear summoning sickness so the ability can be used immediately
		var card = (Card)newState.GetObject(added.Id);
		var creature = card.GetComponent<CreatureComponent>()!;
		newState = newState.UpdateObject(
			added.Id,
			card.WithComponentReplaced(creature with { HasSummoningSickness = false })
		);

		return (newState, added.Id);
	}
}
