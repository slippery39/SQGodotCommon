using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;
using MtgCore.Cards.Builders;
using NUnit.Framework;

namespace MtgCore.Tests;

/// <summary>
/// Indestructible, Exhaust, Exalted and subtype Protection — the combat keywords added for the
/// Core Set Cube. Cards are defined inline so card balance changes cannot break these.
/// </summary>
[TestFixture]
public class CombatKeywordTests
{
	private GameState _state;
	private MtgGameIds _ids;

	[SetUp]
	public void Setup() => (_state, _ids) = MtgGameFactory.CreateForTesting();

	// ===== INDESTRUCTIBLE =====

	[Test]
	public void Indestructible_SurvivesLethalCombatDamage()
	{
		var (s1, attacker) = AddCreature(_state, "Bear", 2, 2, _ids.Player1Id);
		var (s2, defender) = AddCreature(s1, "Wall", 0, 1, _ids.Player2Id, indestructible: true);

		var (final, _) = s2.AddAction(Attack(attacker.Id, defender.Id)).ProcessAllActions();

		Assert.That(
			final.GetCardZone(defender.Id).ZoneType,
			Is.EqualTo(ZoneType.Battlefield),
			"2 damage on a 0/1 indestructible creature is not lethal"
		);
		Assert.That(
			((Card)final.GetObject(defender.Id)).GetComponent<CreatureComponent>()!.Damage,
			Is.EqualTo(2),
			"Damage is still marked — it just isn't lethal"
		);
	}

	[Test]
	public void Indestructible_SurvivesDestroyEffect()
	{
		var (s1, victim) = AddCreature(_state, "Wall", 2, 2, _ids.Player1Id, indestructible: true);

		var (final, _) = s1.AddAction(
				new DestroyCreatureAction { TargetIds = ImmutableList.Create(victim.Id) }
			)
			.ProcessAllActions();

		Assert.That(final.GetCardZone(victim.Id).ZoneType, Is.EqualTo(ZoneType.Battlefield));
	}

	[Test]
	public void Indestructible_SurvivesDeathtouch()
	{
		// Deathtouch does not beat indestructible in real MTG either — both route through
		// IsLethalDamage, where indestructible is checked first.
		var (s1, attacker) = AddCreature(_state, "Snake", 1, 1, _ids.Player1Id, deathtouch: true);
		var (s2, defender) = AddCreature(s1, "Wall", 0, 4, _ids.Player2Id, indestructible: true);

		var (final, _) = s2.AddAction(Attack(attacker.Id, defender.Id)).ProcessAllActions();

		Assert.That(final.GetCardZone(defender.Id).ZoneType, Is.EqualTo(ZoneType.Battlefield));
	}

	[Test]
	public void Indestructible_StillDiesToZeroToughness()
	{
		// The real rule: indestructible answers damage and destruction, not a -X/-X shrink.
		var (s1, victim) = AddCreature(_state, "Wall", 2, 2, _ids.Player1Id, indestructible: true);

		var shrunk = s1.AddAction(
			new AddModifierAction
			{
				PowerBonus = 0,
				ToughnessBonus = -2,
				Duration = ModifierDuration.Permanent,
				TargetIds = ImmutableList.Create(victim.Id),
			}
		);

		var (final, _) = shrunk.ProcessAllActions();

		Assert.That(
			final.GetCardZone(victim.Id).ZoneType,
			Is.EqualTo(ZoneType.Graveyard),
			"Zero effective toughness kills even an indestructible creature"
		);
	}

	// ===== EXHAUST =====

	[Test]
	public void Exhausted_CreatureCannotAttack()
	{
		var (s1, creature) = AddCreature(_state, "Bear", 2, 2, _ids.Player1Id);

		var (exhausted, _) = s1.AddAction(
				new ExhaustCreatureAction { TargetIds = ImmutableList.Create(creature.Id) }
			)
			.ProcessAllActions();

		Assert.That(
			((Card)exhausted.GetObject(creature.Id)).GetComponent<CreatureComponent>()!.IsExhausted,
			Is.True
		);

		var (_, success) = exhausted.TryAddAction(Attack(creature.Id, _ids.Player2Id));
		Assert.That(success, Is.False, "An exhausted creature must not be able to attack");
	}

	[Test]
	public void Exhaust_EmitsEventIntoTriggerFeed()
	{
		// The event has to reach PendingGameEvents, not just the Events log, or Gideon's Avenger
		// silently never fires.
		var (s1, creature) = AddCreature(_state, "Bear", 2, 2, _ids.Player1Id);

		var (_, events) = s1.AddAction(
				new ExhaustCreatureAction { TargetIds = ImmutableList.Create(creature.Id) }
			)
			.ProcessAllActions();

		Assert.That(
			events.OfType<CreatureExhaustedEvent>().Any(e => e.CreatureId == creature.Id),
			Is.True
		);
	}

	[Test]
	public void Exhaust_AlreadyExhausted_DoesNotEmitSecondEvent()
	{
		// Otherwise an exhaust payoff double-counts.
		var (s1, creature) = AddCreature(_state, "Bear", 2, 2, _ids.Player1Id);
		var exhaust = new ExhaustCreatureAction { TargetIds = ImmutableList.Create(creature.Id) };

		var (once, _) = s1.AddAction(exhaust).ProcessAllActions();
		var (_, events) = once.AddAction(exhaust).ProcessAllActions();

		Assert.That(events.OfType<CreatureExhaustedEvent>().Count(), Is.EqualTo(0));
	}

	[Test]
	public void Exhaust_ClearsAtTheExhaustedCreaturesControllersTurnStart()
	{
		// This is the untap step. Exhausting Player 2's creature during Player 1's turn must cost
		// Player 2 exactly one attack: it stays exhausted through the rest of P1's turn and clears
		// when P2's turn begins.
		var (s1, victim) = AddCreature(_state, "Bear", 2, 2, _ids.Player2Id);

		var (exhausted, _) = s1.AddAction(
				new ExhaustCreatureAction { TargetIds = ImmutableList.Create(victim.Id) }
			)
			.ProcessAllActions();

		var p2Battlefield = exhausted.GetPlayerZoneId(_ids.Player2Id, ZoneType.Battlefield);

		// Player 1's turn starting again must NOT clear Player 2's creature.
		var (afterP1Turn, _) = exhausted
			.AddAction(
				new StartTurnAction
				{
					ActivePlayerId = _ids.Player1Id,
					BattlefieldId = exhausted.GetPlayerZoneId(_ids.Player1Id, ZoneType.Battlefield),
					SkipDraw = true,
				}
			)
			.ProcessAllActions();

		Assert.That(
			((Card)afterP1Turn.GetObject(victim.Id)).GetComponent<CreatureComponent>()!.IsExhausted,
			Is.True,
			"The exhauster's turn start must not untap the victim"
		);

		var (afterP2Turn, _) = afterP1Turn
			.AddAction(
				new StartTurnAction
				{
					ActivePlayerId = _ids.Player2Id,
					BattlefieldId = p2Battlefield,
					SkipDraw = true,
				}
			)
			.ProcessAllActions();

		Assert.That(
			((Card)afterP2Turn.GetObject(victim.Id)).GetComponent<CreatureComponent>()!.IsExhausted,
			Is.False,
			"The victim's own turn start clears it"
		);
	}

	[Test]
	public void RequiresTapAbility_ExhaustsTheCreature_AndCannotBeReused()
	{
		// Before IsExhausted existed, RequiresTap only blocked activation under summoning
		// sickness — the ability was effectively repeatable within the turn.
		var tapper = CardFactory
			.Creature("Tapper", manaCost: 1, power: 1, toughness: 1)
			.WithActivatedAbility(
				"Tap",
				manaCost: 0,
				effect: eb => eb.WithExhaust(),
				requiresTap: true,
				maxPerTurn: 0
			)
			.Build();

		var battlefieldId = _state.GetPlayerZoneId(_ids.Player1Id, ZoneType.Battlefield);
		var ready = (Card)
			tapper.WithComponentReplaced(
				tapper.GetComponent<CreatureComponent>()! with
				{
					HasSummoningSickness = false,
				}
			);
		var (s1, source) = _state.AddObject(
			ready with
			{
				OwnerId = _ids.Player1Id,
				ControllerId = _ids.Player1Id,
			},
			parentId: battlefieldId
		);
		var (s2, victim) = AddCreature(s1, "Bear", 2, 2, _ids.Player2Id);

		var (after, _) = s2.AddAction(
				new ActivateAbilityAction
				{
					CardId = source.Id,
					ActivatingPlayerId = _ids.Player1Id,
					AbilityIndex = 0,
					TargetIds = ImmutableList.Create(victim.Id),
				}
			)
			.ProcessAllActions();

		Assert.That(
			((Card)after.GetObject(victim.Id)).GetComponent<CreatureComponent>()!.IsExhausted,
			Is.True,
			"The ability should exhaust its target"
		);
		Assert.That(
			((Card)after.GetObject(source.Id)).GetComponent<CreatureComponent>()!.IsExhausted,
			Is.True,
			"A tap cost should exhaust the source itself"
		);

		var (_, success) = after.TryAddAction(
			new ActivateAbilityAction
			{
				CardId = source.Id,
				ActivatingPlayerId = _ids.Player1Id,
				AbilityIndex = 0,
				TargetIds = ImmutableList.Create(victim.Id),
			}
		);
		Assert.That(success, Is.False, "An exhausted creature cannot use its tap ability again");
	}

	// ===== EXALTED =====

	[Test]
	public void Exalted_AttackingAlone_GivesBonus()
	{
		var (s1, attacker) = AddCreature(_state, "Knight", 2, 2, _ids.Player1Id, exalted: 1);

		var (final, _) = s1.AddAction(Attack(attacker.Id, _ids.Player2Id)).ProcessAllActions();

		Assert.That(
			final.GetPlayer(_ids.Player2Id).Life,
			Is.EqualTo(17),
			"A lone attacker with exalted should hit for 3, not 2"
		);
	}

	[Test]
	public void Exalted_SecondAttacker_GetsNoBonus()
	{
		var (s1, first) = AddCreature(_state, "Knight", 2, 2, _ids.Player1Id, exalted: 1);
		var (s2, second) = AddCreature(s1, "Squire", 1, 1, _ids.Player1Id);

		var (afterFirst, _) = s2.AddAction(Attack(first.Id, _ids.Player2Id)).ProcessAllActions();
		var (final, _) = afterFirst
			.AddAction(Attack(second.Id, _ids.Player2Id))
			.ProcessAllActions();

		// First attacker was alone: 3. Second is not alone: 1. Total 4.
		Assert.That(
			final.GetPlayer(_ids.Player2Id).Life,
			Is.EqualTo(16),
			"Only the first, lone attacker gets the exalted bonus"
		);
	}

	[Test]
	public void Exalted_InstancesStackAcrossYourCreatures()
	{
		// Sublime Archangel's pattern: instances on other creatures still count.
		var (s1, attacker) = AddCreature(_state, "Knight", 2, 2, _ids.Player1Id, exalted: 1);
		var (s2, _) = AddCreature(s1, "Archangel", 4, 3, _ids.Player1Id, exalted: 2);

		var (final, _) = s2.AddAction(Attack(attacker.Id, _ids.Player2Id)).ProcessAllActions();

		Assert.That(
			final.GetPlayer(_ids.Player2Id).Life,
			Is.EqualTo(15),
			"Three total exalted instances should give the lone attacker +3/+3"
		);
	}

	[Test]
	public void Exalted_GrantedInstance_IsCounted()
	{
		var (s1, attacker) = AddCreature(_state, "Vanilla", 2, 2, _ids.Player1Id);

		var card = (Card)s1.GetObject(attacker.Id);
		var granted = s1.UpdateObject(
			attacker.Id,
			card with
			{
				Components = card.Components.Add(
					new AppliedKeywordComponent { GrantsExalted = true }
				),
			}
		);

		var (final, _) = granted.AddAction(Attack(attacker.Id, _ids.Player2Id)).ProcessAllActions();

		Assert.That(final.GetPlayer(_ids.Player2Id).Life, Is.EqualTo(17));
	}

	[Test]
	public void Exalted_DoesNotStackWithItselfOnRepeatAttacks()
	{
		// One attack per turn already, but the bonus must be UntilEndOfTurn so it does not
		// accumulate permanently.
		var (s1, attacker) = AddCreature(_state, "Knight", 2, 2, _ids.Player1Id, exalted: 1);

		var (final, _) = s1.AddAction(Attack(attacker.Id, _ids.Player2Id)).ProcessAllActions();

		var modifiers = ((Card)final.GetObject(attacker.Id))
			.GetComponents<PowerToughnessModifier>()
			.ToList();

		Assert.That(modifiers, Has.Count.EqualTo(1));
		Assert.That(modifiers[0].Duration, Is.EqualTo(ModifierDuration.UntilEndOfTurn));
	}

	// ===== PROTECTION FROM SUBTYPE =====

	[Test]
	public void Protection_PreventsDamageFromThatSubtype()
	{
		var (s1, attacker) = AddCreature(_state, "Demon Lord", 5, 5, _ids.Player1Id, "Demon");
		var (s2, defender) = AddCreature(
			s1,
			"Baneslayer",
			5,
			5,
			_ids.Player2Id,
			protectedFrom: ["Demon"]
		);

		var (final, _) = s2.AddAction(Attack(attacker.Id, defender.Id)).ProcessAllActions();

		Assert.That(
			((Card)final.GetObject(defender.Id)).GetComponent<CreatureComponent>()!.Damage,
			Is.EqualTo(0),
			"A protected creature takes no damage from that subtype"
		);
		Assert.That(
			final.GetCardZone(attacker.Id).ZoneType,
			Is.EqualTo(ZoneType.Graveyard),
			"Protection is one-way — the Demon still dies"
		);
	}

	[Test]
	public void Protection_DoesNotApplyToOtherSubtypes()
	{
		var (s1, attacker) = AddCreature(_state, "Bear", 5, 5, _ids.Player1Id, "Beast");
		var (s2, defender) = AddCreature(
			s1,
			"Baneslayer",
			5,
			5,
			_ids.Player2Id,
			protectedFrom: ["Demon"]
		);

		var (final, _) = s2.AddAction(Attack(attacker.Id, defender.Id)).ProcessAllActions();

		Assert.That(final.GetCardZone(defender.Id).ZoneType, Is.EqualTo(ZoneType.Graveyard));
	}

	[Test]
	public void Protection_PreventsTargetingFromThatSubtype()
	{
		var (s1, source) = AddCreature(_state, "Dragon", 4, 4, _ids.Player1Id, "Dragon");
		var (s2, defender) = AddCreature(
			s1,
			"Baneslayer",
			5,
			5,
			_ids.Player2Id,
			protectedFrom: ["Dragon"]
		);

		var context = new TargetingContext
		{
			GameState = s2,
			SourceCardId = source.Id,
			CastingPlayerId = _ids.Player1Id,
		};

		Assert.That(new IsCreatureSpecification().IsSatisfiedBy(defender.Id, context), Is.False);
	}

	// ===== HELPERS =====

	private AttackAction Attack(int attackerId, int targetId) =>
		new()
		{
			AttackerId = attackerId,
			TargetId = targetId,
			AttackingPlayerId = _ids.Player1Id,
		};

	private static (GameState, Card) AddCreature(
		GameState state,
		string name,
		int power,
		int toughness,
		int ownerId,
		string? subtype = null,
		bool indestructible = false,
		bool deathtouch = false,
		int exalted = 0,
		string[]? protectedFrom = null
	)
	{
		var components = ImmutableArray.Create<GameComponent>(
			new PermanentComponent(),
			new CreatureComponent
			{
				Power = power,
				Toughness = toughness,
				HasSummoningSickness = false,
				HasIndestructible = indestructible,
				HasDeathtouch = deathtouch,
			}
		);

		if (exalted > 0)
			components = components.Add(new ExaltedComponent { Count = exalted });

		if (protectedFrom != null)
			components = components.Add(
				new ProtectionFromSubtypeComponent
				{
					Subtypes = protectedFrom.ToImmutableHashSet(StringComparer.OrdinalIgnoreCase),
				}
			);

		var card = new Card
		{
			Name = name,
			ManaCost = 2,
			OwnerId = ownerId,
			ControllerId = ownerId,
			Subtypes =
				subtype == null
					? ImmutableHashSet.Create<string>(StringComparer.OrdinalIgnoreCase)
					: ImmutableHashSet.Create(StringComparer.OrdinalIgnoreCase, subtype),
			Components = components,
		};

		return state.AddObject(
			card,
			parentId: state.GetPlayerZoneId(ownerId, ZoneType.Battlefield)
		);
	}
}
