using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;
using MtgCore.Cards.Builders;
using NUnit.Framework;
using static MtgCore.Cards.Builders.TargetBuilder;

namespace MtgCore.Tests;

/// <summary>
/// Replacement effects, activation conditions, cast restrictions, trigger caps, life-gain
/// triggers and the conditional P/T modifiers. Cards are defined inline so card balance changes
/// cannot break these.
/// </summary>
[TestFixture]
public class CardMechanicsTests
{
	private GameState _state;
	private MtgGameIds _ids;

	[SetUp]
	public void Setup() => (_state, _ids) = MtgGameFactory.CreateForTesting();

	// ===== LIFE GAIN TRIGGER FEED (regression) =====

	[Test]
	public void GainLife_EmitsEventIntoTriggerFeed()
	{
		// Regression: PlayerGainedLifeEvent used to be put only on ActionResult.Events, never on
		// PendingGameEvents, so NO "whenever you gain life" trigger had ever fired.
		var warden = CardFactory
			.Creature("Warden", manaCost: 1, power: 1, toughness: 1)
			.WithTriggeredAbility(
				"Grow",
				TriggerConditions.OnGainLife(),
				eb =>
					eb.WithAction(
						new AddModifierAction
						{
							PowerBonus = 1,
							ToughnessBonus = 1,
							Duration = ModifierDuration.Permanent,
							TargetContextKey = ContextKeys.SourceCardId,
						},
						TargetingStrategy.NoTarget()
					)
			)
			.Build();

		var (s1, card) = PutOnBattlefield(_state, warden, _ids.Player1Id);

		var (final, _) = s1.AddAction(
				new GainLifeAction { Amount = 3, TargetIds = ImmutableList.Create(_ids.Player1Id) }
			)
			.ProcessAllActions();

		Assert.That(final.GetPlayer(_ids.Player1Id).Life, Is.EqualTo(23));
		Assert.That(
			final.GetEffectivePower(card.Id),
			Is.EqualTo(2),
			"The life-gain trigger must actually fire"
		);
	}

	[Test]
	public void GainLife_OpponentsGain_DoesNotFireYourTrigger()
	{
		var warden = CardFactory
			.Creature("Warden", manaCost: 1, power: 1, toughness: 1)
			.WithTriggeredAbility(
				"Grow",
				TriggerConditions.OnGainLife(),
				eb =>
					eb.WithAction(
						new AddModifierAction
						{
							PowerBonus = 1,
							ToughnessBonus = 1,
							Duration = ModifierDuration.Permanent,
							TargetContextKey = ContextKeys.SourceCardId,
						},
						TargetingStrategy.NoTarget()
					)
			)
			.Build();

		var (s1, card) = PutOnBattlefield(_state, warden, _ids.Player1Id);

		var (final, _) = s1.AddAction(
				new GainLifeAction { Amount = 3, TargetIds = ImmutableList.Create(_ids.Player2Id) }
			)
			.ProcessAllActions();

		Assert.That(final.GetEffectivePower(card.Id), Is.EqualTo(1));
	}

	// ===== REPLACEMENT EFFECTS =====

	[Test]
	public void LifeGainBonus_AddsToTheGain()
	{
		var angel = CardFactory
			.Creature("Angel of Vitality", manaCost: 3, power: 2, toughness: 2)
			.WithLifeGainBonus(1)
			.Build();

		var (s1, _) = PutOnBattlefield(_state, angel, _ids.Player1Id);

		var (final, _) = s1.AddAction(
				new GainLifeAction { Amount = 3, TargetIds = ImmutableList.Create(_ids.Player1Id) }
			)
			.ProcessAllActions();

		Assert.That(final.GetPlayer(_ids.Player1Id).Life, Is.EqualTo(24), "3 + 1 = 4 life gained");
	}

	[Test]
	public void LifeGainBonus_EventCarriesTheModifiedAmount()
	{
		// A trigger reading the event must see the real number, and there must be exactly ONE
		// event — a second would mean the bonus had become a trigger, which loops forever.
		var angel = CardFactory
			.Creature("Angel of Vitality", manaCost: 3, power: 2, toughness: 2)
			.WithLifeGainBonus(1)
			.Build();

		var (s1, _) = PutOnBattlefield(_state, angel, _ids.Player1Id);

		var (_, events) = s1.AddAction(
				new GainLifeAction { Amount = 3, TargetIds = ImmutableList.Create(_ids.Player1Id) }
			)
			.ProcessAllActions();

		var gained = events.OfType<PlayerGainedLifeEvent>().ToList();
		Assert.That(gained, Has.Count.EqualTo(1), "Exactly one life-gain event, not a cascade");
		Assert.That(gained[0].Amount, Is.EqualTo(4));
	}

	[Test]
	public void LifeGainBonus_TwoSources_Stack()
	{
		var angel = CardFactory
			.Creature("Angel of Vitality", manaCost: 3, power: 2, toughness: 2)
			.WithLifeGainBonus(1)
			.Build();

		var (s1, _) = PutOnBattlefield(_state, angel, _ids.Player1Id);
		var (s2, _) = PutOnBattlefield(s1, angel, _ids.Player1Id);

		var (final, _) = s2.AddAction(
				new GainLifeAction { Amount = 3, TargetIds = ImmutableList.Create(_ids.Player1Id) }
			)
			.ProcessAllActions();

		Assert.That(final.GetPlayer(_ids.Player1Id).Life, Is.EqualTo(25), "3 + 1 + 1 = 5");
	}

	[Test]
	public void LifeGainBonus_OnlyAppliesToItsController()
	{
		var angel = CardFactory
			.Creature("Angel of Vitality", manaCost: 3, power: 2, toughness: 2)
			.WithLifeGainBonus(1)
			.Build();

		var (s1, _) = PutOnBattlefield(_state, angel, _ids.Player1Id);

		var (final, _) = s1.AddAction(
				new GainLifeAction { Amount = 3, TargetIds = ImmutableList.Create(_ids.Player2Id) }
			)
			.ProcessAllActions();

		Assert.That(final.GetPlayer(_ids.Player2Id).Life, Is.EqualTo(23));
	}

	[Test]
	public void Replacement_MultipliersApplyBeforeAdditions()
	{
		// The documented ordering rule: a doubler must not also double someone else's flat bonus.
		// (2 * 3) + 1 = 7, not 2 * (3 + 1) = 8.
		var doubler = CardFactory
			.Creature("Doubler", manaCost: 4, power: 2, toughness: 2)
			.WithComponent(new TestLifeGainDoubler())
			.Build();
		var angel = CardFactory
			.Creature("Angel of Vitality", manaCost: 3, power: 2, toughness: 2)
			.WithLifeGainBonus(1)
			.Build();

		var (s1, _) = PutOnBattlefield(_state, doubler, _ids.Player1Id);
		var (s2, _) = PutOnBattlefield(s1, angel, _ids.Player1Id);

		var (final, _) = s2.AddAction(
				new GainLifeAction { Amount = 3, TargetIds = ImmutableList.Create(_ids.Player1Id) }
			)
			.ProcessAllActions();

		Assert.That(final.GetPlayer(_ids.Player1Id).Life, Is.EqualTo(27), "20 + (3*2 + 1)");
	}

	/// <summary>Test-only doubler; no real card needs one yet.</summary>
	private record TestLifeGainDoubler : ReplacementModifierComponent
	{
		public override ReplaceableEvent Event => ReplaceableEvent.LifeGain;
		public override int Multiplier => 2;
	}

	// ===== ACTIVATION CONDITIONS =====

	[Test]
	public void ActivationCondition_BlocksBelowThreshold()
	{
		var speaker = MakeGatedTokenMaker();
		var (s1, card) = PutOnBattlefield(_state, speaker, _ids.Player1Id);

		// Life is 20, starting life is 20 — needs 27.
		var (_, success) = s1.TryAddAction(
			new ActivateAbilityAction
			{
				CardId = card.Id,
				ActivatingPlayerId = _ids.Player1Id,
				AbilityIndex = 0,
			}
		);

		Assert.That(success, Is.False);
	}

	[Test]
	public void ActivationCondition_AllowsAtThreshold()
	{
		var speaker = MakeGatedTokenMaker();
		var (s1, card) = PutOnBattlefield(_state, speaker, _ids.Player1Id);

		var player = s1.GetPlayer(_ids.Player1Id);
		var s2 = s1.UpdateObject(_ids.Player1Id, player with { Life = 27 });

		var (_, success) = s2.TryAddAction(
			new ActivateAbilityAction
			{
				CardId = card.Id,
				ActivatingPlayerId = _ids.Player1Id,
				AbilityIndex = 0,
			}
		);

		Assert.That(success, Is.True);
	}

	[Test]
	public void ActivationCondition_NotOfferedByActionGenerator_WhenUnsatisfied()
	{
		// The AI and the UI must never see a button that does nothing.
		var speaker = MakeGatedTokenMaker();
		var (s1, card) = PutOnBattlefield(_state, speaker, _ids.Player1Id);

		var actions = MtgActionGenerator.GetLegalActions(s1, _ids, _ids.Player1Id);

		Assert.That(
			actions.OfType<ActivateAbilityAction>().Any(a => a.CardId == card.Id),
			Is.False
		);
	}

	[Test]
	public void ActivationCondition_ReadsStartingLifeNotAHardcoded20()
	{
		var speaker = MakeGatedTokenMaker();
		var (s1, card) = PutOnBattlefield(_state, speaker, _ids.Player1Id);

		// Starting life 30 means the gate is 37, so 27 life is no longer enough.
		var player = s1.GetPlayer(_ids.Player1Id);
		var s2 = s1.UpdateObject(_ids.Player1Id, player with { Life = 27, StartingLife = 30 });

		var (_, success) = s2.TryAddAction(
			new ActivateAbilityAction
			{
				CardId = card.Id,
				ActivatingPlayerId = _ids.Player1Id,
				AbilityIndex = 0,
			}
		);

		Assert.That(success, Is.False);
	}

	private static Card MakeGatedTokenMaker() =>
		CardFactory
			.Creature("Speaker", manaCost: 1, power: 1, toughness: 1)
			.WithActivatedAbility(
				"Make Angel",
				manaCost: 3,
				effect: eb =>
					eb.WithCreateTokens(
						CardFactory.Creature("Angel", 0, 4, 4).WithFlying().Build()
					),
				condition: new LifeAboveStartingCondition { Amount = 7 }
			)
			.Build();

	// ===== CAST RESTRICTIONS =====

	[Test]
	public void CastRestriction_BlocksBeforeMinimumRound()
	{
		var avenger = CardFactory
			.Creature("Serra Avenger", manaCost: 2, power: 3, toughness: 3)
			.WithFlying()
			.WithCastRestriction(new MinimumRoundCastRestriction { MinimumRound = 2 })
			.Build();

		var handId = _state.GetPlayerZoneId(_ids.Player1Id, ZoneType.Hand);
		var (s1, card) = _state.AddObject(
			avenger with
			{
				OwnerId = _ids.Player1Id,
				ControllerId = _ids.Player1Id,
			},
			parentId: handId
		);

		// TurnNumber starts at 1.
		var (_, success) = s1.TryAddAction(
			new CastCreatureAction { CardId = card.Id, CastingPlayerId = _ids.Player1Id }
		);

		Assert.That(success, Is.False);
	}

	[Test]
	public void CastRestriction_AllowsFromMinimumRound()
	{
		var avenger = CardFactory
			.Creature("Serra Avenger", manaCost: 2, power: 3, toughness: 3)
			.WithFlying()
			.WithCastRestriction(new MinimumRoundCastRestriction { MinimumRound = 2 })
			.Build();

		var handId = _state.GetPlayerZoneId(_ids.Player1Id, ZoneType.Hand);
		var (s1, card) = _state.AddObject(
			avenger with
			{
				OwnerId = _ids.Player1Id,
				ControllerId = _ids.Player1Id,
			},
			parentId: handId
		);

		var game = s1.TryGetGame()!;
		var s2 = s1.UpdateObject(game.Id, game with { TurnNumber = 2 });

		var (_, success) = s2.TryAddAction(
			new CastCreatureAction { CardId = card.Id, CastingPlayerId = _ids.Player1Id }
		);

		Assert.That(success, Is.True);
	}

	// ===== TRIGGER CAPS =====

	[Test]
	public void MaxTriggers_FiresOnceEver_AndDoesNotResetOnANewTurn()
	{
		// Renown: "if it isn't renowned". A per-turn cap would silently make it grow every turn.
		var freeblade = CardFactory
			.Creature("Topan Freeblade", manaCost: 2, power: 2, toughness: 2)
			.WithRenown(1)
			.Build();

		var (s1, card) = PutOnBattlefield(_state, freeblade, _ids.Player1Id);

		var (afterFirst, _) = s1.AddAction(Attack(card.Id, _ids.Player2Id)).ProcessAllActions();
		Assert.That(afterFirst.GetEffectivePower(card.Id), Is.EqualTo(3), "Renown fires once");

		var battlefieldId = afterFirst.GetPlayerZoneId(_ids.Player1Id, ZoneType.Battlefield);
		var (nextTurn, _) = afterFirst
			.AddAction(
				new StartTurnAction
				{
					ActivePlayerId = _ids.Player1Id,
					BattlefieldId = battlefieldId,
					SkipDraw = true,
				}
			)
			.ProcessAllActions();

		var (afterSecond, _) = nextTurn
			.AddAction(Attack(card.Id, _ids.Player2Id))
			.ProcessAllActions();

		Assert.That(
			afterSecond.GetEffectivePower(card.Id),
			Is.EqualTo(3),
			"Renown must not fire a second time on a later turn"
		);
	}

	[Test]
	public void MaxTriggersPerTurn_ResetsOnANewTurn()
	{
		var grower = CardFactory
			.Creature("Grower", manaCost: 2, power: 2, toughness: 2)
			.WithTriggeredAbility(
				"Grow",
				TriggerConditions.OnSelfDealsCombatDamageToPlayer(),
				eb =>
					eb.WithAction(
						new AddModifierAction
						{
							PowerBonus = 1,
							ToughnessBonus = 1,
							Duration = ModifierDuration.Permanent,
							TargetContextKey = ContextKeys.SourceCardId,
						},
						TargetingStrategy.NoTarget()
					),
				maxPerTurn: 1
			)
			.Build();

		var (s1, card) = PutOnBattlefield(_state, grower, _ids.Player1Id);

		var (afterFirst, _) = s1.AddAction(Attack(card.Id, _ids.Player2Id)).ProcessAllActions();
		Assert.That(afterFirst.GetEffectivePower(card.Id), Is.EqualTo(3));

		var battlefieldId = afterFirst.GetPlayerZoneId(_ids.Player1Id, ZoneType.Battlefield);
		var (nextTurn, _) = afterFirst
			.AddAction(
				new StartTurnAction
				{
					ActivePlayerId = _ids.Player1Id,
					BattlefieldId = battlefieldId,
					SkipDraw = true,
				}
			)
			.ProcessAllActions();

		var (afterSecond, _) = nextTurn
			.AddAction(Attack(card.Id, _ids.Player2Id))
			.ProcessAllActions();

		Assert.That(
			afterSecond.GetEffectivePower(card.Id),
			Is.EqualTo(4),
			"A per-turn cap resets, unlike a lifetime cap"
		);
	}

	// ===== CONDITIONAL P/T =====

	[Test]
	public void LifeTotalBonus_AppliesOnlyAtOrAboveThreshold()
	{
		var angel = CardFactory
			.Creature("Angel of Vitality", manaCost: 3, power: 2, toughness: 2)
			.WithFlying()
			.WithLifeTotalBonus(2, 2, minimum: 25)
			.Build();

		var (s1, card) = PutOnBattlefield(_state, angel, _ids.Player1Id);

		Assert.That(s1.GetEffectivePower(card.Id), Is.EqualTo(2), "At 20 life, no bonus");

		var player = s1.GetPlayer(_ids.Player1Id);
		var s2 = s1.UpdateObject(_ids.Player1Id, player with { Life = 25 });

		Assert.That(s2.GetEffectivePower(card.Id), Is.EqualTo(4), "At 25 life, +2/+2");
		Assert.That(s2.GetEffectiveToughness(card.Id), Is.EqualTo(4));

		// And it must switch back off — this is why it is live-evaluated, not stamped.
		var s3 = s2.UpdateObject(_ids.Player1Id, s2.GetPlayer(_ids.Player1Id) with { Life = 24 });
		Assert.That(s3.GetEffectivePower(card.Id), Is.EqualTo(2));
	}

	[Test]
	public void CreatureCount_GivesStarStarPowerAndToughness()
	{
		var crusader = CardFactory
			.Creature("Crusader of Odric", manaCost: 3, power: 0, toughness: 0)
			.WithPowerEqualToCreatureCount()
			.Build();

		var (s1, card) = PutOnBattlefield(_state, crusader, _ids.Player1Id);

		Assert.That(s1.GetEffectivePower(card.Id), Is.EqualTo(1), "Counts itself");

		var bear = CardFactory.Creature("Bear", 2, 2, 2).Build();
		var (s2, _) = PutOnBattlefield(s1, bear, _ids.Player1Id);
		var (s3, _) = PutOnBattlefield(s2, bear, _ids.Player1Id);

		Assert.That(s3.GetEffectivePower(card.Id), Is.EqualTo(3));
		Assert.That(s3.GetEffectiveToughness(card.Id), Is.EqualTo(3));

		// Opponent creatures must not count.
		var (s4, _) = PutOnBattlefield(s3, bear, _ids.Player2Id);
		Assert.That(s4.GetEffectivePower(card.Id), Is.EqualTo(3));
	}

	// ===== SPELL TAX =====

	[Test]
	public void SpellTax_IncreasesNoncreatureCostForBothPlayers()
	{
		var wingmare = CardFactory
			.Creature("Vryn Wingmare", manaCost: 3, power: 2, toughness: 1)
			.WithFlying()
			.WithSpellTax(1)
			.Build();

		var (s1, _) = PutOnBattlefield(_state, wingmare, _ids.Player1Id);

		var bolt = CardFactory
			.Spell("Bolt", manaCost: 1)
			.WithDamage(3)
			.WithTarget(Single().PlayersOrCreatures())
			.Build();
		var bear = CardFactory.Creature("Bear", manaCost: 2, power: 2, toughness: 2).Build();

		Assert.That(
			s1.ComputeEffectiveCost(bolt, _ids.Player1Id),
			Is.EqualTo(2),
			"Its own controller is taxed too"
		);
		Assert.That(s1.ComputeEffectiveCost(bolt, _ids.Player2Id), Is.EqualTo(2));
		Assert.That(
			s1.ComputeEffectiveCost(bear, _ids.Player1Id),
			Is.EqualTo(2),
			"Creatures are not taxed"
		);
	}

	// ===== HELPERS =====

	private AttackAction Attack(int attackerId, int targetId) =>
		new()
		{
			AttackerId = attackerId,
			TargetId = targetId,
			AttackingPlayerId = _ids.Player1Id,
		};

	/// <summary>
	/// Places a card template on a battlefield ready to act. Uses AddObject rather than
	/// PutIntoBattlefieldAction so no ETB trigger fires — these tests are about other mechanics.
	/// </summary>
	private static (GameState, Card) PutOnBattlefield(GameState state, Card template, int ownerId)
	{
		var ready = (Card)
			template.WithComponentReplaced(
				template.GetComponent<CreatureComponent>()! with
				{
					HasSummoningSickness = false,
				}
			);

		return state.AddObject(
			ready with
			{
				OwnerId = ownerId,
				ControllerId = ownerId,
			},
			parentId: state.GetPlayerZoneId(ownerId, ZoneType.Battlefield)
		);
	}
}
