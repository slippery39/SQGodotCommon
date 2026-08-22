using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;
using MtgCore.Cards.Builders;
using NUnit.Framework;
using static MtgCore.Cards.Builders.TargetBuilder;

namespace MtgCore.Tests;

/// <summary>
/// The engine mechanics added for the Core Set Cube's colourless and multicolour sections.
///
/// Card-agnostic on purpose, like GreenMechanicsTests and RedMechanicsTests: these assert the
/// mechanic rather than the card that motivated it, so rebalancing an artifact cannot quietly
/// delete the coverage.
///
/// Every test here asserts a CONSEQUENCE — a board state, a zone, a P/T, a loss that did or did
/// not happen. None of them assert that a card builds. Seven green cards built, cast and resolved
/// while doing literally nothing, and that is the failure mode this fixture exists to catch.
/// </summary>
[TestFixture]
public class ColourlessMechanicsTests
{
	private GameState _state;
	private MtgGameIds _ids;

	[SetUp]
	public void Setup()
	{
		(_state, _ids) = MtgGameFactory.CreateForTesting();
	}

	// ===== EQUIPPED-BY-SOURCE TARGETING =====

	/// <summary>
	/// The whole point of IsEquippedBySourceSpecification: an equipment's own trigger must land on
	/// the creature it is attached to, and on nothing else. Two creatures are on the battlefield
	/// so a spec that merely matches "a creature you control" passes the first half and fails here.
	/// </summary>
	[Test]
	public void EquippedBySource_MatchesOnlyTheWornCreature()
	{
		var (state, wearerId) = PutOnBattlefield(MakeCreature("Wearer", 2, 2));
		var (withOther, bystanderId) = AddToBattlefield(
			state,
			MakeCreature("Bystander", 2, 2),
			_ids.Player1Id
		);

		var (withRing, ringId) = AddToBattlefield(
			withOther,
			MakeEquipment("Ring", wearerId),
			_ids.Player1Id
		);

		var spec = new IsEquippedBySourceSpecification();
		var context = new TargetingContext
		{
			GameState = withRing,
			SourceCardId = ringId,
			CastingPlayerId = _ids.Player1Id,
		};

		Assert.Multiple(() =>
		{
			Assert.That(spec.IsSatisfiedBy(wearerId, context), Is.True, "the wearer");
			Assert.That(
				spec.IsSatisfiedBy(bystanderId, context),
				Is.False,
				"an unequipped creature must not be reachable by the equipment's own trigger"
			);
		});
	}

	/// <summary>
	/// An unattached equipment must target nothing at all. Returning the source, or falling through
	/// to "any creature", is how a Ring on the battlefield doing nothing would silently start
	/// buffing a random creature every upkeep.
	/// </summary>
	[Test]
	public void EquippedBySource_MatchesNothingWhenUnattached()
	{
		var (state, creatureId) = PutOnBattlefield(MakeCreature("Wearer", 2, 2));
		var (withRing, ringId) = AddToBattlefield(state, MakeEquipment("Ring", 0), _ids.Player1Id);

		var context = new TargetingContext
		{
			GameState = withRing,
			SourceCardId = ringId,
			CastingPlayerId = _ids.Player1Id,
		};

		Assert.That(
			new IsEquippedBySourceSpecification().IsSatisfiedBy(creatureId, context),
			Is.False
		);
	}

	/// <summary>
	/// End to end through the real resolution path, which is what proves the spec is usable from a
	/// trigger rather than merely correct in isolation: the counter must land on the wearer.
	/// </summary>
	[Test]
	public void EquippedBySource_DeliversACounterToTheWearer()
	{
		var (state, wearerId) = PutOnBattlefield(MakeCreature("Wearer", 2, 2));
		var (withOther, bystanderId) = AddToBattlefield(
			state,
			MakeCreature("Bystander", 2, 2),
			_ids.Player1Id
		);
		var (withRing, ringId) = AddToBattlefield(
			withOther,
			MakeEquipment("Ring", wearerId),
			_ids.Player1Id
		);

		var final = Run(
			withRing,
			new ResolveEffectAction
			{
				CastingPlayerId = _ids.Player1Id,
				SourceCardId = ringId,
				Effects =
				[
					new CardEffect
					{
						ActionTemplate = new AddCountersAction { Amount = 1 },
						TargetingStrategy = AllValid()
							.WithSpec(new IsEquippedBySourceSpecification()),
					},
				],
			}
		);

		Assert.Multiple(() =>
		{
			Assert.That(CounterCount(final, wearerId), Is.EqualTo(1), "the wearer gets it");
			Assert.That(CounterCount(final, bystanderId), Is.Zero, "and nobody else does");
		});
	}

	// ===== TYPE-FILTERED GRAVEYARD COUNT =====

	/// <summary>
	/// Enigma Drake counts instants and sorceries only, and is a */4 — so a creature card in the
	/// graveyard must not raise its power, and nothing at all may raise its toughness.
	/// </summary>
	[Test]
	public void TypeFilteredGraveyardCount_CountsOnlySpells_AndPowerOnly()
	{
		var drake = MakeCreature("Drake", 0, 4);
		drake = drake with
		{
			Components = drake.Components.Add(
				new GraveyardCountComponent
				{
					Types = CardType.AnySpell,
					AffectsToughness = false,
					Duration = ModifierDuration.Permanent,
				}
			),
		};

		var (state, drakeId) = PutOnBattlefield(drake);

		state = AddToGraveyard(state, MakeSpell("Bolt"), _ids.Player1Id);
		state = AddToGraveyard(state, MakeSpell("Counterspell"), _ids.Player1Id);
		state = AddToGraveyard(state, MakeCreature("Bear", 2, 2), _ids.Player1Id);

		Assert.Multiple(() =>
		{
			Assert.That(
				state.GetEffectivePower(drakeId),
				Is.EqualTo(2),
				"two spells, and the creature card must not count"
			);
			Assert.That(
				state.GetEffectiveToughness(drakeId),
				Is.EqualTo(4),
				"AffectsToughness = false is what makes a */N creature possible"
			);
		});
	}

	/// <summary>
	/// Tarmogoyf must be untouched by the two new fields, or adding them was a silent balance
	/// change to a card nobody was editing.
	/// </summary>
	[Test]
	public void UnfilteredGraveyardCount_StillCountsEverything_OnBothStats()
	{
		var goyf = MakeCreature("Goyf", 0, 1);
		goyf = goyf with
		{
			Components = goyf.Components.Add(
				new GraveyardCountComponent { Duration = ModifierDuration.Permanent }
			),
		};

		var (state, goyfId) = PutOnBattlefield(goyf);
		state = AddToGraveyard(state, MakeSpell("Bolt"), _ids.Player1Id);
		state = AddToGraveyard(state, MakeCreature("Bear", 2, 2), _ids.Player1Id);

		Assert.Multiple(() =>
		{
			Assert.That(state.GetEffectivePower(goyfId), Is.EqualTo(2));
			Assert.That(state.GetEffectiveToughness(goyfId), Is.EqualTo(3));
		});
	}

	/// <summary>
	/// The count is the CONTROLLER's graveyard, not both. An opponent filling their own yard must
	/// not grow your Drake.
	/// </summary>
	[Test]
	public void TypeFilteredGraveyardCount_IgnoresTheOpponentsGraveyard()
	{
		var drake = MakeCreature("Drake", 0, 4);
		drake = drake with
		{
			Components = drake.Components.Add(
				new GraveyardCountComponent
				{
					Types = CardType.AnySpell,
					AffectsToughness = false,
					Duration = ModifierDuration.Permanent,
				}
			),
		};

		var (state, drakeId) = PutOnBattlefield(drake);
		state = AddToGraveyard(state, MakeSpell("Bolt"), _ids.Player2Id);

		Assert.That(state.GetEffectivePower(drakeId), Is.Zero);
	}

	// ===== COUNTER REPLACEMENT (Conclave Mentor) =====

	/// <summary>
	/// One extra counter, applied ONCE, no matter how many are being placed. The replacement lives
	/// inside AddCountersAction, so exactly one event is emitted and it already carries the
	/// increased number — a trigger-based implementation would loop forever instead.
	/// </summary>
	[Test]
	public void CounterBonus_AddsOne_AndEmitsASingleEventWithTheFinalNumber()
	{
		var (state, mentorId) = PutOnBattlefield(
			WithComponent(MakeCreature("Mentor", 2, 2), new CounterBonusComponent { Amount = 1 })
		);
		_ = mentorId;

		var (withTarget, targetId) = AddToBattlefield(
			state,
			MakeCreature("Target", 1, 1),
			_ids.Player1Id
		);

		var (final, events) = withTarget
			.AddAction(new AddCountersAction { Amount = 3, TargetIds = [targetId] })
			.ProcessAllActions();

		Assert.Multiple(() =>
		{
			Assert.That(CounterCount(final, targetId), Is.EqualTo(4), "3 plus one, not 3 plus 3");
			Assert.That(
				events.OfType<CountersAddedEvent>().Select(e => e.Amount),
				Is.EqualTo(new[] { 4 }),
				"one event, carrying the replaced amount"
			);
			Assert.That(final.GetEffectivePower(targetId), Is.EqualTo(5));
		});
	}

	/// <summary>
	/// A bonus meant for counters being PUT ON must never touch a removal, or "remove a counter"
	/// silently becomes "remove a counter, then add one back".
	/// </summary>
	[Test]
	public void CounterBonus_DoesNotApplyToRemoval()
	{
		var (state, _) = PutOnBattlefield(
			WithComponent(MakeCreature("Mentor", 2, 2), new CounterBonusComponent { Amount = 1 })
		);

		var (withTarget, targetId) = AddToBattlefield(
			state,
			MakeCreature("Target", 1, 1),
			_ids.Player1Id
		);

		// 2 placed becomes 3 via the bonus; removing 1 must land on exactly 2.
		var afterAdd = Run(
			withTarget,
			new AddCountersAction { Amount = 2, TargetIds = [targetId] }
		);
		var afterRemove = Run(
			afterAdd,
			new AddCountersAction { Amount = -1, TargetIds = [targetId] }
		);

		Assert.Multiple(() =>
		{
			Assert.That(CounterCount(afterAdd, targetId), Is.EqualTo(3));
			Assert.That(CounterCount(afterRemove, targetId), Is.EqualTo(2), "not 3");
		});
	}

	/// <summary>
	/// The bonus belongs to the controller of the creature receiving counters. An opponent's Mentor
	/// must not grow your creatures.
	/// </summary>
	[Test]
	public void CounterBonus_DoesNotCrossTheTable()
	{
		var (state, _) = AddToBattlefield(
			_state,
			WithComponent(MakeCreature("Mentor", 2, 2), new CounterBonusComponent { Amount = 1 }),
			_ids.Player2Id
		);

		var (withTarget, targetId) = AddToBattlefield(
			state,
			MakeCreature("Target", 1, 1),
			_ids.Player1Id
		);

		var final = Run(withTarget, new AddCountersAction { Amount = 2, TargetIds = [targetId] });

		Assert.That(CounterCount(final, targetId), Is.EqualTo(2));
	}

	// ===== CHARGE COUNTERS =====

	/// <summary>
	/// The single assertion that fails for every wrong implementation: a charge counter must go on
	/// a NON-creature (a +1/+1 counter cannot), and must not behave like a +1/+1 counter if that
	/// permanent later has a body. Reusing PlusOneCounterComponent passes the first half.
	/// </summary>
	[Test]
	public void ChargeCounters_LandOnANonCreature_AndAreNotPlusOneCounters()
	{
		var (state, artifactId) = PutOnBattlefield(MakeArtifact("Hoard"));

		var (final, events) = state
			.AddAction(
				new AddChargeCountersAction
				{
					Kind = "gold",
					Amount = 2,
					TargetIds = [artifactId],
				}
			)
			.ProcessAllActions();

		Assert.Multiple(() =>
		{
			Assert.That(ChargeCount(final, artifactId, "gold"), Is.EqualTo(2));
			Assert.That(
				((Card)final.GetObject(artifactId)).GetComponent<PlusOneCounterComponent>(),
				Is.Null,
				"a gold counter is not a +1/+1 counter"
			);
			Assert.That(
				events.OfType<CountersAddedEvent>(),
				Is.Empty,
				"a counters-matter payoff must not fire on a charge counter"
			);
		});
	}

	/// <summary>
	/// Kinds must not bleed into each other, or a card spending gold counters could pay with
	/// somebody else's charge counters.
	/// </summary>
	[Test]
	public void ChargeCounters_AreTrackedPerKind()
	{
		var (state, artifactId) = PutOnBattlefield(MakeArtifact("Hoard"));

		state = Run(
			state,
			new AddChargeCountersAction
			{
				Kind = "gold",
				Amount = 2,
				TargetIds = [artifactId],
			}
		);
		state = Run(
			state,
			new AddChargeCountersAction
			{
				Kind = "charge",
				Amount = 5,
				TargetIds = [artifactId],
			}
		);

		Assert.Multiple(() =>
		{
			Assert.That(ChargeCount(state, artifactId, "gold"), Is.EqualTo(2));
			Assert.That(ChargeCount(state, artifactId, "charge"), Is.EqualTo(5));
		});
	}

	/// <summary>
	/// Conclave Mentor says "+1/+1 counters". If the replacement reached charge counters, a gold
	/// counter would arrive worth two and Dragon's Hoard would draw at twice its printed rate.
	/// </summary>
	[Test]
	public void CounterBonus_DoesNotApplyToChargeCounters()
	{
		var (state, _) = PutOnBattlefield(
			WithComponent(MakeCreature("Mentor", 2, 2), new CounterBonusComponent { Amount = 1 })
		);
		var (withArtifact, artifactId) = AddToBattlefield(
			state,
			MakeArtifact("Hoard"),
			_ids.Player1Id
		);

		var final = Run(
			withArtifact,
			new AddChargeCountersAction
			{
				Kind = "gold",
				Amount = 1,
				TargetIds = [artifactId],
			}
		);

		Assert.That(ChargeCount(final, artifactId, "gold"), Is.EqualTo(1), "not 2");
	}

	/// <summary>
	/// The cost is a real gate, and paying it really spends. An ability that validates but does
	/// not decrement is an infinite draw engine — the exact failure a bounded resource exists to
	/// prevent.
	/// </summary>
	[Test]
	public void RemoveCounterCost_GatesActivation_AndSpendsTheCounter()
	{
		var hoard = MakeArtifact("Hoard");
		hoard = hoard with
		{
			Components = hoard.Components.Add(
				new ActivatedAbilityComponent
				{
					Name = "Spend",
					ManaCost = 0,
					MaxActivationsPerTurn = 0,
					AdditionalCosts =
					[
						new RemoveCounterAdditionalCost { Kind = "gold", Count = 1 },
					],
					Effects =
					[
						new CardEffect
						{
							ActionTemplate = new DrawCardsAction { Amount = 1 },
							TargetingStrategy = TargetingStrategy.Self(),
						},
					],
				}
			),
		};

		var (state, hoardId) = PutOnBattlefield(hoard);

		var activate = new ActivateAbilityAction
		{
			CardId = hoardId,
			ActivatingPlayerId = _ids.Player1Id,
			AbilityIndex = 0,
		};

		Assert.That(
			activate.ValidateAdd(state).IsValid,
			Is.False,
			"no counters means the ability is not available"
		);

		state = Run(
			state,
			new AddChargeCountersAction
			{
				Kind = "gold",
				Amount = 1,
				TargetIds = [hoardId],
			}
		);

		Assert.That(activate.ValidateAdd(state).IsValid, Is.True, "one counter unlocks it");

		var after = Run(state, activate);

		Assert.Multiple(() =>
		{
			Assert.That(ChargeCount(after, hoardId, "gold"), Is.Zero, "paying must spend");
			Assert.That(
				activate.ValidateAdd(after).IsValid,
				Is.False,
				"and the gate must close again"
			);
		});
	}

	// ===== CONTROLS A CARD BY NAME (the Empires trio) =====

	[Test]
	public void ControlsCardNamed_ReadsYourBattlefieldOnly()
	{
		var (state, _) = PutOnBattlefield(MakeArtifact("Throne of Empires"));
		var (both, _) = AddToBattlefield(state, MakeArtifact("Scepter of Empires"), _ids.Player2Id);

		var mine = new ControlsCardNamedCondition { CardName = "Throne of Empires" };
		var theirs = new ControlsCardNamedCondition { CardName = "Scepter of Empires" };

		Assert.Multiple(() =>
		{
			Assert.That(mine.IsSatisfied(both, 0, _ids.Player1Id), Is.True);
			Assert.That(
				theirs.IsSatisfied(both, 0, _ids.Player1Id),
				Is.False,
				"an opponent's copy must not complete your combo"
			);
			Assert.That(theirs.IsSatisfied(both, 0, _ids.Player2Id), Is.True);
		});
	}

	/// <summary>
	/// The Empires clause needs BOTH names. One out of two must read false, or all three cards
	/// upgrade themselves on an incomplete board.
	/// </summary>
	[Test]
	public void ControlsAllCardsNamed_RequiresBoth()
	{
		var (one, _) = PutOnBattlefield(MakeArtifact("Crown of Empires"));

		var condition = new ControlsAllCardsNamedCondition
		{
			FirstName = "Crown of Empires",
			SecondName = "Scepter of Empires",
		};

		Assert.That(condition.IsSatisfied(one, 0, _ids.Player1Id), Is.False, "half the combo");

		var (both, _) = AddToBattlefield(one, MakeArtifact("Scepter of Empires"), _ids.Player1Id);

		Assert.That(condition.IsSatisfied(both, 0, _ids.Player1Id), Is.True);
	}

	// ===== NONLAND PERMANENTS AS A MASS TARGET =====

	/// <summary>
	/// Perilous Vault and Ugin. Before NonlandPermanents() every mass helper was creature-shaped,
	/// so an artifact, enchantment or planeswalker simply could not be named by a sweeper.
	/// </summary>
	[Test]
	public void NonlandPermanents_SweepsEveryPermanentTypeOnBothSides()
	{
		var state = _state;
		var (s1, creatureId) = AddToBattlefield(state, MakeCreature("Bear", 2, 2), _ids.Player1Id);
		var (s2, artifactId) = AddToBattlefield(s1, MakeArtifact("Rock"), _ids.Player1Id);
		var (s3, enchantId) = AddToBattlefield(s2, MakeEnchantment("Aura"), _ids.Player2Id);
		var (s4, oppCreatureId) = AddToBattlefield(s3, MakeCreature("Ogre", 3, 3), _ids.Player2Id);

		// A card in hand must survive — the spec is battlefield-scoped.
		var handId = s4.GetPlayerZoneId(_ids.Player1Id, ZoneType.Hand);
		var (s5, handCard) = s4.AddObject(
			MakeArtifact("In Hand") with
			{
				OwnerId = _ids.Player1Id,
				ControllerId = _ids.Player1Id,
			},
			parentId: handId
		);

		var final = Run(
			s5,
			new ResolveEffectAction
			{
				CastingPlayerId = _ids.Player1Id,
				Effects =
				[
					new CardEffect
					{
						ActionTemplate = new ExileAction(),
						TargetingStrategy = AllValid().NonlandPermanents(),
					},
				],
			}
		);

		var battlefield1 = final.GetPlayerZoneId(_ids.Player1Id, ZoneType.Battlefield);
		var battlefield2 = final.GetPlayerZoneId(_ids.Player2Id, ZoneType.Battlefield);

		Assert.Multiple(() =>
		{
			Assert.That(final.GetChildrenIds(battlefield1), Is.Empty, "your side is swept too");
			Assert.That(final.GetChildrenIds(battlefield2), Is.Empty, "and theirs");
			Assert.That(
				final.GetChildrenIds(handId),
				Does.Contain(handCard.Id),
				"a hand card is not a permanent on the battlefield"
			);
			Assert.That(
				new[] { creatureId, artifactId, enchantId, oppCreatureId },
				Has.All.Matches<int>(id =>
					final.GetParent(id)
					== final.GetPlayerZoneId(((Card)final.GetObject(id)).OwnerId, ZoneType.Exile)
				),
				"every permanent type must be reachable, not just creatures"
			);
		});
	}

	// ===== ANIMATION =====

	/// <summary>
	/// The baseline: a permanent with no body gets one, with the right stats, and can attack the
	/// turn it is animated. Summoning sickness here would make every animation a do-nothing on the
	/// turn you paid for it.
	/// </summary>
	[Test]
	public void Animate_GivesABody_ThatIsNotSummoningSick()
	{
		var (state, artifactId) = PutOnBattlefield(MakeArtifact("Plate Mail"));

		Assert.That(
			((Card)state.GetObject(artifactId)).HasComponent<CreatureComponent>(),
			Is.False,
			"precondition"
		);

		var final = Run(
			state,
			new AnimateAction
			{
				Power = 4,
				Toughness = 4,
				Subtype = "Spirit",
				TargetIds = [artifactId],
			}
		);

		var card = (Card)final.GetObject(artifactId);

		Assert.Multiple(() =>
		{
			Assert.That(final.GetEffectivePower(artifactId), Is.EqualTo(4));
			Assert.That(final.GetEffectiveToughness(artifactId), Is.EqualTo(4));
			Assert.That(card.HasType(CardType.Creature), Is.True, "type line must follow the body");
			Assert.That(card.HasSubtype("Spirit"), Is.True);
			Assert.That(card.HasSubtype("Artifact"), Is.True, "it is still an artifact");
			Assert.That(
				card.GetComponent<CreatureComponent>()!.HasSummoningSickness,
				Is.False,
				"it has been under your control since the turn began"
			);
		});
	}

	/// <summary>
	/// Trap 2 from the design. StaticAbilityEngine is a push model driven by
	/// CreatureEnteredBattlefieldEvent, so an animated permanent misses every anthem unless the
	/// engine is called by hand — and staging that event instead would fire every ETB payoff on
	/// the board for a permanent that entered nothing.
	/// </summary>
	[Test]
	public void Animate_PicksUpAnthems_WithoutFiringEtbTriggers()
	{
		var anthem = MakeArtifact("Anthem");
		anthem = anthem with
		{
			Components = anthem.Components.Add(
				new StaticPTBoostAbility
				{
					PowerBonus = 1,
					ToughnessBonus = 1,
					Filter = new IsCreatureSpecification().And(
						new IsControlledByYouSpecification()
					),
				}
			),
		};

		var (state, anthemId) = PutOnBattlefield(anthem);
		state = RegisterStatics(state, anthemId);
		var (withTarget, artifactId) = AddToBattlefield(
			state,
			MakeArtifact("Plate Mail"),
			_ids.Player1Id
		);

		var (final, events) = withTarget
			.AddAction(
				new AnimateAction
				{
					Power = 4,
					Toughness = 4,
					TargetIds = [artifactId],
				}
			)
			.ProcessAllActions();

		Assert.Multiple(() =>
		{
			Assert.That(
				final.GetEffectivePower(artifactId),
				Is.EqualTo(5),
				"4/4 plus the anthem — a push model that is not poked reads 4"
			);
			Assert.That(
				events.OfType<CreatureEnteredBattlefieldEvent>(),
				Is.Empty,
				"it entered nothing; every ETB payoff on the board would fire"
			);
		});
	}

	/// <summary>
	/// Trap 3. A CreatureComponent is not a PowerToughnessModifier, so nothing in the normal
	/// end-of-turn cleanup removes it — and the added subtype must go back off without taking a
	/// printed one with it.
	/// </summary>
	[Test]
	public void Animate_WearsOffAtEndOfTurn_LeavingPrintedSubtypesIntact()
	{
		var (state, artifactId) = PutOnBattlefield(MakeEquipment("Plate Mail", 0));

		state = Run(
			state,
			new AnimateAction
			{
				Power = 4,
				Toughness = 4,
				Subtype = "Spirit",
				TargetIds = [artifactId],
			}
		);

		state = Run(state, EndTurn());

		var card = (Card)state.GetObject(artifactId);

		Assert.Multiple(() =>
		{
			Assert.That(card.HasComponent<CreatureComponent>(), Is.False, "the body is gone");
			Assert.That(card.HasComponent<AnimatedUntilEndOfTurnComponent>(), Is.False);
			Assert.That(card.HasType(CardType.Creature), Is.False, "and so is the type");
			Assert.That(card.HasSubtype("Spirit"), Is.False, "the granted subtype comes off");
			Assert.That(card.HasSubtype("Artifact"), Is.True, "the printed ones do not");
			Assert.That(card.HasSubtype("Equipment"), Is.True);
		});
	}

	/// <summary>
	/// The accounting failure that made a bounced creature collect a second anthem boost, in its
	/// new home: animate, revert, animate again must read 5, never 6.
	/// </summary>
	[Test]
	public void Animate_TwiceAcrossTurns_DoesNotStackAnthems()
	{
		var anthem = MakeArtifact("Anthem");
		anthem = anthem with
		{
			Components = anthem.Components.Add(
				new StaticPTBoostAbility
				{
					PowerBonus = 1,
					ToughnessBonus = 1,
					Filter = new IsCreatureSpecification().And(
						new IsControlledByYouSpecification()
					),
				}
			),
		};

		var (state, anthemId) = PutOnBattlefield(anthem);
		state = RegisterStatics(state, anthemId);
		var (withTarget, artifactId) = AddToBattlefield(
			state,
			MakeArtifact("Plate Mail"),
			_ids.Player1Id
		);

		var animate = new AnimateAction
		{
			Power = 4,
			Toughness = 4,
			TargetIds = [artifactId],
		};

		var s = Run(withTarget, animate);
		s = Run(s, EndTurn());
		s = Run(s, animate);

		Assert.That(s.GetEffectivePower(artifactId), Is.EqualTo(5), "not 6");
	}

	/// <summary>
	/// Animating something that already has a body must be a no-op, not a second CreatureComponent
	/// and a second round of anthem stamps.
	/// </summary>
	[Test]
	public void Animate_DoesNothingToAnExistingCreature()
	{
		var (state, creatureId) = PutOnBattlefield(MakeCreature("Bear", 2, 2));

		var final = Run(
			state,
			new AnimateAction
			{
				Power = 4,
				Toughness = 4,
				TargetIds = [creatureId],
			}
		);

		Assert.Multiple(() =>
		{
			Assert.That(final.GetEffectivePower(creatureId), Is.EqualTo(2), "still a Bear");
			Assert.That(
				((Card)final.GetObject(creatureId)).GetComponents<CreatureComponent>().Count(),
				Is.EqualTo(1)
			);
		});
	}

	/// <summary>
	/// A counter placed on a permanent before it had a body must be waiting when the body arrives.
	/// This is the whole reason the non-creature guard came out of AddCountersAction, and it is
	/// invisible without animation — which is why the guard was harmless until now.
	/// </summary>
	[Test]
	public void CountersPlacedBeforeAnimation_AreVisibleAfterIt()
	{
		var (state, artifactId) = PutOnBattlefield(MakeArtifact("Plate Mail"));

		state = Run(state, new AddCountersAction { Amount = 2, TargetIds = [artifactId] });
		state = Run(
			state,
			new AnimateAction
			{
				Power = 1,
				Toughness = 1,
				TargetIds = [artifactId],
			}
		);

		Assert.That(state.GetEffectivePower(artifactId), Is.EqualTo(3), "1/1 plus two counters");
	}

	/// <summary>
	/// An animated permanent is a real creature, so lethal damage kills it and it goes to the
	/// graveyard. Half-animation — stats but no participation in state-based effects — is the
	/// plausible wrong implementation this catches.
	/// </summary>
	[Test]
	public void AnAnimatedPermanent_DiesToLethalDamage()
	{
		var (state, artifactId) = PutOnBattlefield(MakeArtifact("Plate Mail"));

		state = Run(
			state,
			new AnimateAction
			{
				Power = 2,
				Toughness = 2,
				TargetIds = [artifactId],
			}
		);
		state = Run(state, new DealDamageAction { Amount = 2, TargetIds = [artifactId] });
		state = Run(state, MakeStateBasedCheck());

		var graveyardId = state.GetPlayerZoneId(_ids.Player1Id, ZoneType.Graveyard);

		Assert.That(state.GetParent(artifactId), Is.EqualTo(graveyardId));
	}

	// ===== CANNOT LOSE (Platinum Angel) =====

	/// <summary>
	/// Life below zero with an Angel out is not a loss — and the moment the Angel leaves, the
	/// pending loss lands on the very next state-based check. Suppressing the CAUSE instead of the
	/// outcome would leave the player alive at 20 after the Angel died.
	/// </summary>
	[Test]
	public void CannotLose_SuppressesTheLoss_UntilTheSourceLeaves()
	{
		var (state, angelId) = PutOnBattlefield(
			WithComponent(MakeCreature("Angel", 4, 4), new CannotLoseComponent())
		);

		state = SetLife(state, _ids.Player1Id, -3);
		state = Run(state, MakeStateBasedCheck());

		Assert.That(
			((MtgPlayer)state.GetObject(_ids.Player1Id)).HasLost,
			Is.False,
			"the Angel is the whole card"
		);

		var graveyardId = state.GetPlayerZoneId(_ids.Player1Id, ZoneType.Graveyard);
		state = state.MoveCardTracked(angelId, graveyardId);
		state = Run(state, MakeStateBasedCheck());

		Assert.That(
			((MtgPlayer)state.GetObject(_ids.Player1Id)).HasLost,
			Is.True,
			"killing it must collect the loss that was waiting, not merely stop the bleeding"
		);
	}

	/// <summary>
	/// It protects its controller, not everyone. An opponent at zero life still loses.
	/// </summary>
	[Test]
	public void CannotLose_DoesNotProtectTheOpponent()
	{
		var (state, _) = PutOnBattlefield(
			WithComponent(MakeCreature("Angel", 4, 4), new CannotLoseComponent())
		);

		state = SetLife(state, _ids.Player2Id, 0);
		state = Run(state, MakeStateBasedCheck());

		Assert.That(((MtgPlayer)state.GetObject(_ids.Player2Id)).HasLost, Is.True);
	}

	// ===== DEFERRED LAND FETCH =====

	/// <summary>
	/// "Put it onto the battlefield TAPPED" — MaxMana rises so the land is real, CurrentMana does
	/// not so it is unusable this turn. Without this a fetch that says tapped is silently as good
	/// as one that does not.
	/// </summary>
	[Test]
	public void DeferredManaGain_RaisesMaxManaOnly()
	{
		var before = (MtgPlayer)_state.GetObject(_ids.Player1Id);

		var final = Run(
			_state,
			new GainPermanentManaAction
			{
				Amount = 1,
				Deferred = true,
				TargetIds = [_ids.Player1Id],
			}
		);

		var after = (MtgPlayer)final.GetObject(_ids.Player1Id);

		Assert.Multiple(() =>
		{
			Assert.That(after.MaxMana, Is.EqualTo(before.MaxMana + 1));
			Assert.That(after.CurrentMana, Is.EqualTo(before.CurrentMana), "not usable this turn");
			Assert.That(after.LandsPlayedTotal, Is.EqualTo(before.LandsPlayedTotal + 1));
		});
	}

	[Test]
	public void UndeferredManaGain_StillRaisesBoth()
	{
		var before = (MtgPlayer)_state.GetObject(_ids.Player1Id);

		var final = Run(
			_state,
			new GainPermanentManaAction { Amount = 1, TargetIds = [_ids.Player1Id] }
		);

		var after = (MtgPlayer)final.GetObject(_ids.Player1Id);

		Assert.Multiple(() =>
		{
			Assert.That(after.MaxMana, Is.EqualTo(before.MaxMana + 1));
			Assert.That(after.CurrentMana, Is.EqualTo(before.CurrentMana + 1));
		});
	}

	// ===== PLAY FROM THE TOP OF YOUR LIBRARY =====

	/// <summary>
	/// The predicate and the generator must agree. If IsInCastableZone allows it but the generator
	/// never offers it, the card is dead for everyone; if the generator offers it but the predicate
	/// refuses, the action is generated and then fails validation. Both halves, one test.
	/// </summary>
	[Test]
	public void LibraryTop_IsPlayableAndOffered_OnlyWithAnEnabler()
	{
		var (state, landId) = PutOnLibraryTop(MakeLand("Forest"));

		Assert.Multiple(() =>
		{
			Assert.That(
				state.IsInCastableZone(landId, _ids.Player1Id),
				Is.False,
				"no enabler, no play"
			);
			Assert.That(LegalLandPlays(state), Does.Not.Contain(landId));
		});

		var (withEnabler, _) = AddToBattlefield(state, MakeRadha(), _ids.Player1Id);

		Assert.Multiple(() =>
		{
			Assert.That(withEnabler.IsInCastableZone(landId, _ids.Player1Id), Is.True);
			Assert.That(
				LegalLandPlays(withEnabler),
				Does.Contain(landId),
				"the generator must offer what the predicate allows"
			);
		});
	}

	/// <summary>
	/// The TOP card only. A deeper card being playable would let a player rummage their whole deck,
	/// and — worse — hand the AI perfect information about it.
	/// </summary>
	[Test]
	public void LibraryTop_DoesNotReachTheSecondCard()
	{
		var (state, topId) = PutOnLibraryTop(MakeLand("Forest"));
		var (deeper, secondId) = AddBelowLibraryTop(state, MakeLand("Island"));
		var (withEnabler, _) = AddToBattlefield(deeper, MakeRadha(), _ids.Player1Id);

		Assert.Multiple(() =>
		{
			Assert.That(withEnabler.IsInCastableZone(topId, _ids.Player1Id), Is.True);
			Assert.That(withEnabler.IsInCastableZone(secondId, _ids.Player1Id), Is.False);
		});
	}

	/// <summary>
	/// Radha says LANDS. An enabler must not quietly turn into Future Sight.
	/// </summary>
	[Test]
	public void LibraryTop_RespectsTheEnablersTypeFilter()
	{
		var (state, spellId) = PutOnLibraryTop(MakeSpell("Bolt"));
		var (withEnabler, _) = AddToBattlefield(state, MakeRadha(), _ids.Player1Id);

		Assert.That(withEnabler.IsInCastableZone(spellId, _ids.Player1Id), Is.False);
	}

	/// <summary>
	/// Your enabler does not open your opponent's library, and it does not open yours to them.
	/// </summary>
	[Test]
	public void LibraryTop_DoesNotCrossTheTable()
	{
		var (state, landId) = PutOnLibraryTop(MakeLand("Forest"));
		var (withEnabler, _) = AddToBattlefield(state, MakeRadha(), _ids.Player1Id);

		Assert.That(withEnabler.IsInCastableZone(landId, _ids.Player2Id), Is.False);
	}

	/// <summary>
	/// End to end: playing the top land really adds mana and really removes it from the library.
	/// A play action that validates and then no-ops is the failure this catches.
	/// </summary>
	[Test]
	public void LibraryTop_LandPlayActuallyRamps()
	{
		var (start, _) = MtgGameFactory.Create();
		var ids = _ids;
		_ = ids;

		var (state, landId) = PutOnLibraryTop(MakeLand("Forest"));
		var (withEnabler, _) = AddToBattlefield(state, MakeRadha(), _ids.Player1Id);

		var before = (MtgPlayer)withEnabler.GetObject(_ids.Player1Id);
		var libraryId = withEnabler.GetPlayerZoneId(_ids.Player1Id, ZoneType.Library);

		var final = Run(
			withEnabler,
			new PlayLandAction { CardId = landId, CastingPlayerId = _ids.Player1Id }
		);

		var after = (MtgPlayer)final.GetObject(_ids.Player1Id);

		Assert.Multiple(() =>
		{
			Assert.That(after.MaxMana, Is.EqualTo(before.MaxMana + 1), "it must actually ramp");
			Assert.That(
				final.GetChildrenIds(libraryId),
				Does.Not.Contain(landId),
				"and actually leave the library"
			);
		});
	}

	// ===== END STEP TRIGGER =====

	/// <summary>
	/// The filter is load-bearing. TurnEndedEvent's subject is the player whose turn ended, so an
	/// unfiltered end-step trigger fires on both turns at double the printed rate — the exact bug
	/// OnYourUpkeep once had.
	/// </summary>
	[Test]
	public void OnYourEndStep_FiresOnYourTurnOnly()
	{
		var condition = TriggerConditions.OnYourEndStep();

		var mine = new TurnEndedEvent { PlayerId = _ids.Player1Id };
		var theirs = new TurnEndedEvent { PlayerId = _ids.Player2Id };

		Assert.Multiple(() =>
		{
			Assert.That(condition.IsSatisfiedBy(mine, TriggerCtx()), Is.True);
			Assert.That(condition.IsSatisfiedBy(theirs, TriggerCtx()), Is.False);
		});
	}

	// ===== HELPERS =====

	private TriggerContext TriggerCtx() =>
		new()
		{
			GameState = _state,
			ControllingPlayerId = _ids.Player1Id,
			SourceCardId = 0,
		};

	/// <summary>
	/// Registers a hand-placed permanent as a static-ability source, which a real cast does via
	/// ResolvePermanentAction staging PermanentEnteredBattlefieldEvent. AddObject skips that, so
	/// without this an anthem built in a test is inert and every anthem assertion passes vacuously.
	/// </summary>
	private GameState RegisterStatics(GameState state, int cardId) =>
		StaticAbilityEngine.ProcessPermanentEntered(state, cardId, _ids.GameId);

	private GameAction EndTurn() =>
		new EndTurnAction
		{
			GameId = _ids.GameId,
			Player1Id = _ids.Player1Id,
			Player2Id = _ids.Player2Id,
		};

	private GameAction MakeStateBasedCheck() =>
		new CheckStateBasedEffectsAction
		{
			GameId = _ids.GameId,
			Player1Id = _ids.Player1Id,
			Player2Id = _ids.Player2Id,
			Player1BattlefieldId = _state.GetPlayerZoneId(_ids.Player1Id, ZoneType.Battlefield),
			Player2BattlefieldId = _state.GetPlayerZoneId(_ids.Player2Id, ZoneType.Battlefield),
			Player1GraveyardId = _state.GetPlayerZoneId(_ids.Player1Id, ZoneType.Graveyard),
			Player2GraveyardId = _state.GetPlayerZoneId(_ids.Player2Id, ZoneType.Graveyard),
		};

	private static GameState SetLife(GameState state, int playerId, int life) =>
		state.UpdateObject(playerId, ((MtgPlayer)state.GetObject(playerId)) with { Life = life });

	/// <summary>Ids of every land PlayLandAction the generator currently offers Player 1.</summary>
	private IEnumerable<int> LegalLandPlays(GameState state) =>
		MtgActionGenerator
			.GetLegalActions(state, _ids, _ids.Player1Id)
			.OfType<PlayLandAction>()
			.Select(a => a.CardId);

	private (GameState State, int CardId) PutOnLibraryTop(Card template)
	{
		var libraryId = _state.GetPlayerZoneId(_ids.Player1Id, ZoneType.Library);

		// CreateForTesting leaves libraries empty, so the first card added is the top one.
		var (withCard, card) = _state.AddObject(
			template with
			{
				OwnerId = _ids.Player1Id,
				ControllerId = _ids.Player1Id,
			},
			parentId: libraryId
		);
		return (withCard, card.Id);
	}

	private (GameState State, int CardId) AddBelowLibraryTop(GameState state, Card template)
	{
		var libraryId = state.GetPlayerZoneId(_ids.Player1Id, ZoneType.Library);
		var (withCard, card) = state.AddObject(
			template with
			{
				OwnerId = _ids.Player1Id,
				ControllerId = _ids.Player1Id,
			},
			parentId: libraryId
		);
		return (withCard, card.Id);
	}

	private static Card MakeLand(string name) =>
		new()
		{
			Name = name,
			Types = CardType.Land,
			Subtypes = ImmutableHashSet.Create(StringComparer.OrdinalIgnoreCase, "Land"),
			Components = ImmutableArray<GameComponent>.Empty,
		};

	private static Card MakeRadha() =>
		new()
		{
			Name = "Radha",
			Types = CardType.Creature,
			Subtypes = ImmutableHashSet<string>.Empty.WithComparer(
				StringComparer.OrdinalIgnoreCase
			),
			Components = ImmutableArray.Create<GameComponent>(
				new PermanentComponent(),
				new CreatureComponent { Power = 3, Toughness = 3 },
				new PlayFromLibraryTopComponent { Types = CardType.Land }
			),
		};

	private static int ChargeCount(GameState state, int cardId, string kind) =>
		((Card)state.GetObject(cardId))
			.GetComponents<ChargeCounterComponent>()
			.FirstOrDefault(c => c.IsKind(kind))
			?.Count ?? 0;

	private static int CounterCount(GameState state, int cardId) =>
		((Card)state.GetObject(cardId)).GetComponent<PlusOneCounterComponent>()?.Count ?? 0;

	private static Card WithComponent(Card card, GameComponent component) =>
		card with
		{
			Components = card.Components.Add(component),
		};

	private static Card MakeCreature(string name, int power, int toughness) =>
		new()
		{
			Name = name,
			Types = CardType.Creature,
			Subtypes = ImmutableHashSet<string>.Empty.WithComparer(
				StringComparer.OrdinalIgnoreCase
			),
			Components = ImmutableArray.Create<GameComponent>(
				new PermanentComponent(),
				new CreatureComponent { Power = power, Toughness = toughness }
			),
		};

	private static Card MakeArtifact(string name) =>
		new()
		{
			Name = name,
			Types = CardType.Artifact,
			Subtypes = ImmutableHashSet.Create(StringComparer.OrdinalIgnoreCase, "Artifact"),
			Components = ImmutableArray.Create<GameComponent>(new PermanentComponent()),
		};

	private static Card MakeEnchantment(string name) =>
		new()
		{
			Name = name,
			Types = CardType.Enchantment,
			Subtypes = ImmutableHashSet.Create(StringComparer.OrdinalIgnoreCase, "Enchantment"),
			Components = ImmutableArray.Create<GameComponent>(new PermanentComponent()),
		};

	private static Card MakeSpell(string name) =>
		new()
		{
			Name = name,
			Types = CardType.Instant,
			Subtypes = ImmutableHashSet<string>.Empty.WithComparer(
				StringComparer.OrdinalIgnoreCase
			),
			Components = ImmutableArray.Create<GameComponent>(new SpellComponent()),
		};

	private static Card MakeEquipment(string name, int equippedToCardId) =>
		new()
		{
			Name = name,
			Types = CardType.Artifact,
			Subtypes = ImmutableHashSet.Create(
				StringComparer.OrdinalIgnoreCase,
				"Artifact",
				"Equipment"
			),
			Components = ImmutableArray.Create<GameComponent>(
				new PermanentComponent(),
				new EquipmentComponent { EquippedToCardId = equippedToCardId }
			),
		};

	private (GameState State, int CardId) PutOnBattlefield(Card template) =>
		AddToBattlefield(_state, template, _ids.Player1Id);

	private static (GameState State, int CardId) AddToBattlefield(
		GameState state,
		Card template,
		int ownerId
	)
	{
		var battlefieldId = state.GetPlayerZoneId(ownerId, ZoneType.Battlefield);
		var (withCard, card) = state.AddObject(
			template with
			{
				OwnerId = ownerId,
				ControllerId = ownerId,
			},
			parentId: battlefieldId
		);
		return (withCard, card.Id);
	}

	private static GameState AddToGraveyard(GameState state, Card template, int ownerId)
	{
		var graveyardId = state.GetPlayerZoneId(ownerId, ZoneType.Graveyard);
		var (withCard, _) = state.AddObject(
			template with
			{
				OwnerId = ownerId,
				ControllerId = ownerId,
			},
			parentId: graveyardId
		);
		return withCard;
	}

	private static GameState Run(GameState state, GameAction action)
	{
		var (final, _) = state.AddAction(action).ProcessAllActions();
		return final;
	}
}
