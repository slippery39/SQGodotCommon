using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;
using NUnit.Framework;

namespace MtgCore.Tests;

[TestFixture]
public class GoblinTests
{
	private GameState _state;
	private MtgGameIds _ids;

	[SetUp]
	public void Setup()
	{
		(_state, _ids) = MtgGameFactory.CreateForTesting();
	}

	// ===== HASTE =====

	[Test]
	public void HasteCreature_CanAttackImmediately_AfterEnteringBattlefield()
	{
		var guide = TestCardLibrary.GoblinGuide() with
		{
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
		};
		var (state, addedGuide) = _state.AddObject(guide, _ids.Player1BattlefieldId);

		// PutIntoBattlefieldAction would set HasSummoningSickness = !HasHaste.
		// Direct AddObject skips that, so simulate the ETB flag stamping.
		var creature = addedGuide.GetComponent<CreatureComponent>()!;
		state = state.UpdateObject(
			addedGuide.Id,
			addedGuide.WithComponentReplaced(
				creature with
				{
					HasSummoningSickness = !creature.HasHaste,
				}
			)
		);

		var actions = MtgActionGenerator.GetLegalActions(state, _ids, _ids.Player1Id);
		var attackActions = actions
			.OfType<AttackAction>()
			.Where(a => a.AttackerId == addedGuide.Id);

		Assert.That(attackActions.Any(), Is.True, "Haste creature should be available to attack");
	}

	[Test]
	public void PutIntoBattlefield_HasteCreature_NoSummoningSickness()
	{
		var guide = TestCardLibrary.GoblinGuide() with
		{
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
		};
		var (stateWithGuide, guideCard) = _state.AddObject(guide, _ids.Player1HandId);

		var (finalState, _) = stateWithGuide
			.AddAction(
				new PutIntoBattlefieldAction { TargetIds = ImmutableList.Create(guideCard.Id) }
			)
			.ProcessAllActions();

		var placed = (Card)finalState.GetObject(guideCard.Id);
		var creature = placed.GetComponent<CreatureComponent>()!;

		Assert.That(creature.HasSummoningSickness, Is.False);
	}

	[Test]
	public void NonHasteCreature_HasSummoningSickness_AfterPutIntoBattlefield()
	{
		var lackey = TestCardLibrary.GoblinLackey() with
		{
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
		};
		var (stateWithLackey, lackeyCard) = _state.AddObject(lackey, _ids.Player1HandId);

		var (finalState, _) = stateWithLackey
			.AddAction(
				new PutIntoBattlefieldAction { TargetIds = ImmutableList.Create(lackeyCard.Id) }
			)
			.ProcessAllActions();

		var placed = (Card)finalState.GetObject(lackeyCard.Id);
		var creature = placed.GetComponent<CreatureComponent>()!;

		Assert.That(creature.HasSummoningSickness, Is.True);
	}

	// ===== COMBAT DAMAGE TRIGGER (GOBLIN LACKEY) =====

	[Test]
	public void GoblinLackey_CombatDamage_EmitsCombatDamageEvent()
	{
		var (state, lackey) = AddCreatureToBattlefield(
			_state,
			TestCardLibrary.GoblinLackey(),
			_ids.Player1Id,
			hasSummoningSickness: false
		);

		var (_, events) = state
			.AddAction(
				new AttackAction
				{
					AttackerId = lackey.Id,
					TargetId = _ids.Player2Id,
					AttackingPlayerId = _ids.Player1Id,
				}
			)
			.ProcessAllActions();

		Assert.That(
			events.OfType<CombatDamageDealtToPlayerEvent>().Any(e => e.AttackerId == lackey.Id),
			Is.True
		);
	}

	[Test]
	public void GoblinLackey_Trigger_PutsGoblinFromHandOntoBattlefield()
	{
		var (stateWithLackey, lackey) = AddCreatureToBattlefield(
			_state,
			TestCardLibrary.GoblinLackey(),
			_ids.Player1Id,
			hasSummoningSickness: false
		);
		var goblinInHand = MakeGoblin(_ids.Player1Id);
		var (stateWithGoblin, handGoblin) = stateWithLackey.AddObject(
			goblinInHand,
			_ids.Player1HandId
		);

		var (finalState, _) = stateWithGoblin
			.AddAction(
				new AttackAction
				{
					AttackerId = lackey.Id,
					TargetId = _ids.Player2Id,
					AttackingPlayerId = _ids.Player1Id,
				}
			)
			.ProcessAllActions();

		Assert.That(
			finalState.GetCardZone(handGoblin.Id).ZoneType,
			Is.EqualTo(ZoneType.Battlefield),
			"Goblin from hand should be on battlefield after Lackey trigger"
		);
	}

	[Test]
	public void GoblinLackey_Trigger_DoesNotFire_WhenNoGoblinInHand()
	{
		var (stateWithLackey, lackey) = AddCreatureToBattlefield(
			_state,
			TestCardLibrary.GoblinLackey(),
			_ids.Player1Id,
			hasSummoningSickness: false
		);

		var creatureCountBefore = stateWithLackey.GetCardsInZone(_ids.Player1BattlefieldId).Count();

		var (finalState, _) = stateWithLackey
			.AddAction(
				new AttackAction
				{
					AttackerId = lackey.Id,
					TargetId = _ids.Player2Id,
					AttackingPlayerId = _ids.Player1Id,
				}
			)
			.ProcessAllActions();

		var creatureCountAfter = finalState.GetCardsInZone(_ids.Player1BattlefieldId).Count();

		Assert.That(
			creatureCountAfter,
			Is.EqualTo(creatureCountBefore),
			"No Goblin in hand — battlefield count should not change"
		);
	}

	// ===== DOUBLE STRIKE (WARREN INSTIGATOR) =====

	[Test]
	public void DoubleStrike_DealsDamageTwiceToPlayer()
	{
		var (state, instigator) = AddCreatureToBattlefield(
			_state,
			TestCardLibrary.WarrenInstigator(),
			_ids.Player1Id,
			hasSummoningSickness: false
		);
		var lifeBefore = state.GetPlayer(_ids.Player2Id).Life;

		var (finalState, _) = state
			.AddAction(
				new AttackAction
				{
					AttackerId = instigator.Id,
					TargetId = _ids.Player2Id,
					AttackingPlayerId = _ids.Player1Id,
				}
			)
			.ProcessAllActions();

		Assert.That(
			finalState.GetPlayer(_ids.Player2Id).Life,
			Is.EqualTo(lifeBefore - 2),
			"Warren Instigator (1/1 double strike) should deal 2 total damage"
		);
	}

	[Test]
	public void DoubleStrike_EmitsTwoCombatDamageEvents()
	{
		var (state, instigator) = AddCreatureToBattlefield(
			_state,
			TestCardLibrary.WarrenInstigator(),
			_ids.Player1Id,
			hasSummoningSickness: false
		);

		var (_, events) = state
			.AddAction(
				new AttackAction
				{
					AttackerId = instigator.Id,
					TargetId = _ids.Player2Id,
					AttackingPlayerId = _ids.Player1Id,
				}
			)
			.ProcessAllActions();

		var combatEvents = events
			.OfType<CombatDamageDealtToPlayerEvent>()
			.Where(e => e.AttackerId == instigator.Id)
			.ToList();

		Assert.That(
			combatEvents.Count,
			Is.EqualTo(2),
			"Double strike should emit two combat damage events"
		);
	}

	[Test]
	public void WarrenInstigator_Trigger_FiresTwice_PuttingTwoGoblinsOnBattlefield()
	{
		var (stateWithInstigator, instigator) = AddCreatureToBattlefield(
			_state,
			TestCardLibrary.WarrenInstigator(),
			_ids.Player1Id,
			hasSummoningSickness: false
		);
		// Add two Goblins to hand — each trigger picks one
		var (s2, goblin1) = stateWithInstigator.AddObject(
			MakeGoblin(_ids.Player1Id),
			_ids.Player1HandId
		);
		var (s3, goblin2) = s2.AddObject(MakeGoblin(_ids.Player1Id), _ids.Player1HandId);

		var (finalState, _) = s3.AddAction(
				new AttackAction
				{
					AttackerId = instigator.Id,
					TargetId = _ids.Player2Id,
					AttackingPlayerId = _ids.Player1Id,
				}
			)
			.ProcessAllActions();

		var battlefieldGoblins = finalState
			.GetCardsInZone(_ids.Player1BattlefieldId)
			.Where(c => c.HasSubtype("Goblin") && c.Id != instigator.Id)
			.Count();

		Assert.That(
			battlefieldGoblins,
			Is.EqualTo(2),
			"Double strike should trigger twice, putting 2 Goblins from hand onto battlefield"
		);
	}

	// ===== TOKEN CREATION =====

	[Test]
	public void CreateCardAction_CreatesTokensOnBattlefield()
	{
		var (finalState, _) = _state
			.AddAction(
				new CreateCardAction
				{
					CardTemplate = TestCardLibrary.GoblinToken(),
					ControllerId = _ids.Player1Id,
					Count = 3,
				}
			)
			.ProcessAllActions();

		var tokensOnBattlefield = finalState
			.GetCardsInZone(_ids.Player1BattlefieldId)
			.Where(c => c.HasSubtype("Goblin"))
			.Count();

		Assert.That(tokensOnBattlefield, Is.EqualTo(3));
	}

	[Test]
	public void CreateCardAction_TokensHaveSummoningSickness()
	{
		var (finalState, _) = _state
			.AddAction(
				new CreateCardAction
				{
					CardTemplate = TestCardLibrary.GoblinToken(),
					ControllerId = _ids.Player1Id,
					Count = 1,
				}
			)
			.ProcessAllActions();

		var token = finalState.GetCardsInZone(_ids.Player1BattlefieldId).Single();
		Assert.That(token.GetComponent<CreatureComponent>()!.HasSummoningSickness, Is.True);
	}

	[Test]
	public void CreateCardAction_EmitsCreatureEnteredBattlefieldEvent_PerToken()
	{
		var (_, events) = _state
			.AddAction(
				new CreateCardAction
				{
					CardTemplate = TestCardLibrary.GoblinToken(),
					ControllerId = _ids.Player1Id,
					Count = 3,
				}
			)
			.ProcessAllActions();

		Assert.That(events.OfType<CreatureEnteredBattlefieldEvent>().Count(), Is.EqualTo(3));
	}

	// ===== SIEGE-GANG COMMANDER =====

	[Test]
	public void SiegeGangCommander_ETB_CreatesThreeGoblinTokens()
	{
		var sgc = TestCardLibrary.SiegeGangCommander() with
		{
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
		};
		var (stateWithSgc, sgcCard) = _state.AddObject(sgc, _ids.Player1HandId);

		var (finalState, _) = stateWithSgc
			.AddAction(
				new CastCreatureAction { CardId = sgcCard.Id, CastingPlayerId = _ids.Player1Id }
			)
			.ProcessAllActions();

		var tokens = finalState
			.GetCardsInZone(_ids.Player1BattlefieldId)
			.Where(c => c.HasSubtype("Goblin") && c.Id != sgcCard.Id)
			.Count();

		Assert.That(tokens, Is.EqualTo(3), "Siege-Gang ETB should create 3 Goblin tokens");
	}

	[Test]
	public void SiegeGangCommander_ActivatedAbility_SacrificesGoblinAndDeals2Damage()
	{
		var sgc = TestCardLibrary.SiegeGangCommander() with
		{
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
		};

		var sgcCreature = sgc.GetComponent<CreatureComponent>()!;
		sgcCreature = sgcCreature with { HasHaste = true }; // Bypass summoning sickness for testing the activated ability
		sgc = sgc.WithComponentReplaced(sgcCreature) as Card;

		var (s1, sgcCard) = _state.AddObject(sgc, _ids.Player1BattlefieldId);
		var goblin = MakeGoblin(_ids.Player1Id);

		var (s2, goblinCard) = s1.AddObject(goblin, _ids.Player1BattlefieldId);

		var lifeBefore = s2.GetPlayer(_ids.Player2Id).Life;

		var (finalState, _) = s2.AddAction(
				new ActivateAbilityAction
				{
					CardId = sgcCard.Id,
					ActivatingPlayerId = _ids.Player1Id,
					AbilityIndex = 0,
					TargetIds = ImmutableList.Create(_ids.Player2Id),
					AdditionalCostPayments = ImmutableDictionary<int, ImmutableList<int>>.Empty.Add(
						0,
						ImmutableList.Create(goblinCard.Id)
					),
				}
			)
			.ProcessAllActions();

		Assert.That(finalState.GetPlayer(_ids.Player2Id).Life, Is.EqualTo(lifeBefore - 2));
		Assert.That(
			finalState.GetCardZone(goblinCard.Id).ZoneType,
			Is.EqualTo(ZoneType.Graveyard),
			"Sacrificed Goblin should be in graveyard"
		);
	}

	// ===== KRENKO, MOB BOSS =====

	[Test]
	public void Krenko_ActivatedAbility_CreatesTokensEqualToGoblinCount()
	{
		var krenko = TestCardLibrary.KrenkoMobBoss() with
		{
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
		};

		//Give Krenko haste to bypass summoning sickness and test the token creation without needing to wait a turn

		var creature = krenko.GetComponent<CreatureComponent>();

		creature = creature with { HasHaste = true };

		krenko = krenko.WithComponentReplaced(creature) as Card;

		var (s1, krenkoBf) = _state.AddObject(krenko, _ids.Player1BattlefieldId);
		// 2 extra Goblins on battlefield alongside Krenko = 3 total
		var (s2, _) = s1.AddObject(MakeGoblin(_ids.Player1Id), _ids.Player1BattlefieldId);
		var (s3, _) = s2.AddObject(MakeGoblin(_ids.Player1Id), _ids.Player1BattlefieldId);

		var (finalState, _) = s3.AddAction(
				new ActivateAbilityAction
				{
					CardId = krenkoBf.Id,
					ActivatingPlayerId = _ids.Player1Id,
					AbilityIndex = 0,
				}
			)
			.ProcessAllActions();

		var tokensCreated = finalState
			.GetCardsInZone(_ids.Player1BattlefieldId)
			.Where(c => c.HasSubtype("Goblin") && c.Id != krenkoBf.Id)
			.Count();

		// Started with 2 Goblins + Krenko (3 total Goblins) → creates 3 new tokens
		// After activation: 2 original + 3 tokens = 5 non-Krenko Goblins
		Assert.That(tokensCreated, Is.EqualTo(5));
	}

	// ===== HELPERS =====

	private static (GameState, Card) AddCreatureToBattlefield(
		GameState state,
		Card template,
		int playerId,
		bool hasSummoningSickness
	)
	{
		var battlefieldId = state.GetPlayerZoneId(playerId, ZoneType.Battlefield);
		var card = template with { OwnerId = playerId, ControllerId = playerId };
		var (newState, added) = state.AddObject(card, battlefieldId);

		// Stamp the summoning sickness flag directly (bypasses PutIntoBattlefieldAction)
		var creature = added.GetComponent<CreatureComponent>()!;
		newState = newState.UpdateObject(
			added.Id,
			added.WithComponentReplaced(
				creature with
				{
					HasSummoningSickness = hasSummoningSickness,
				}
			)
		);

		return (newState, (Card)newState.GetObject(added.Id));
	}

	private static Card MakeGoblin(int ownerId) =>
		new()
		{
			Name = "Goblin Token",
			OwnerId = ownerId,
			ControllerId = ownerId,
			Subtypes = ImmutableList.Create("Goblin"),
			Components = ImmutableList.Create<GameComponent>(
				new CreatureComponent { Power = 1, Toughness = 1 }
			),
		};
}
