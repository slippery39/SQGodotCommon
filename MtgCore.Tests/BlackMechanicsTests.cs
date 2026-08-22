using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;
using MtgCore.Cards.Builders;
using NUnit.Framework;
using static MtgCore.Cards.Builders.TargetBuilder;

namespace MtgCore.Tests;

/// <summary>
/// The engine mechanics added for the Core Set Cube's black section.
///
/// These are deliberately card-agnostic: they test the mechanic, not the card that motivated it,
/// so a later balance change to Grim Tutor or Fleshbag Marauder cannot quietly delete the
/// coverage. The set fixtures assert wiring; this one asserts behaviour.
/// </summary>
[TestFixture]
public class BlackMechanicsTests
{
	private GameState _state;
	private MtgGameIds _ids;

	[SetUp]
	public void Setup()
	{
		(_state, _ids) = MtgGameFactory.CreateForTesting();
	}

	// ===== SYMMETRIC EDICT =====

	/// <summary>
	/// "Each player sacrifices a creature" must hit BOTH sides, and must take the cheapest on
	/// each — a real edict lets each player choose, and each keeps their bomb.
	/// </summary>
	[Test]
	public void SymmetricEdict_KillsTheCheapestCreatureOnEachSide()
	{
		var state = _state;
		var kept = new Dictionary<int, int>();
		var killed = new Dictionary<int, int>();

		foreach (var ownerId in new[] { _ids.Player1Id, _ids.Player2Id })
		{
			var battlefieldId = state.GetPlayerZoneId(ownerId, ZoneType.Battlefield);
			(state, var cheap) = state.AddObject(
				MakeCreature("Chump", ownerId, 1, 1, manaCost: 1),
				parentId: battlefieldId
			);
			(state, var bomb) = state.AddObject(
				MakeCreature("Bomb", ownerId, 6, 6, manaCost: 6),
				parentId: battlefieldId
			);
			killed[ownerId] = cheap.Id;
			kept[ownerId] = bomb.Id;
		}

		var (final, _) = CastSpell(
			state,
			CardFactory.Sorcery("Fleshbag", manaCost: 3).WithSymmetricEdict().Build()
		);

		Assert.Multiple(() =>
		{
			foreach (var ownerId in new[] { _ids.Player1Id, _ids.Player2Id })
			{
				Assert.That(
					final.GetCardZone(killed[ownerId]).ZoneType,
					Is.EqualTo(ZoneType.Graveyard),
					$"Player {ownerId}'s cheapest creature should have been sacrificed"
				);
				Assert.That(
					final.GetCardZone(kept[ownerId]).ZoneType,
					Is.EqualTo(ZoneType.Battlefield),
					$"Player {ownerId}'s bomb should have survived"
				);
			}
		});
	}

	/// <summary>Call to the Grave's "non-Zombie creature" clause.</summary>
	[Test]
	public void SymmetricEdict_SkipsTheExcludedSubtype()
	{
		var battlefieldId = _state.GetPlayerZoneId(_ids.Player1Id, ZoneType.Battlefield);
		var zombie = MakeCreature("Rotter", _ids.Player1Id, 2, 2, manaCost: 1) with
		{
			Subtypes = ImmutableHashSet.Create(StringComparer.OrdinalIgnoreCase, "Zombie"),
		};
		var (state, zombieCard) = _state.AddObject(zombie, parentId: battlefieldId);
		var (state2, human) = state.AddObject(
			MakeCreature("Villager", _ids.Player1Id, 1, 1, manaCost: 4),
			parentId: battlefieldId
		);

		var (final, _) = CastSpell(
			state2,
			CardFactory.Sorcery("Call", manaCost: 5).WithSymmetricEdict("Zombie").Build()
		);

		Assert.Multiple(() =>
		{
			Assert.That(
				final.GetCardZone(zombieCard.Id).ZoneType,
				Is.EqualTo(ZoneType.Battlefield)
			);
			Assert.That(final.GetCardZone(human.Id).ZoneType, Is.EqualTo(ZoneType.Graveyard));
		});
	}

	// ===== GENERIC TUTOR =====

	/// <summary>
	/// An unrestricted tutor must RANK, not take the first match. Library order is random, so a
	/// first-match search with no subtype is just "draw the top card" — Grim Tutor would have
	/// been a strictly worse Sign in Blood and nothing would have looked wrong.
	/// </summary>
	[Test]
	public void GenericTutor_FindsTheMostExpensiveNonLandCard()
	{
		var libraryId = _state.GetPlayerZoneId(_ids.Player1Id, ZoneType.Library);
		var state = _state;

		(state, var cheapTop) = state.AddObject(
			MakeCreature("Cheap Top Card", _ids.Player1Id, 1, 1, manaCost: 1),
			parentId: libraryId
		);
		(state, var bomb) = state.AddObject(
			MakeCreature("Expensive Bomb", _ids.Player1Id, 8, 8, manaCost: 8),
			parentId: libraryId
		);

		var (final, _) = CastSpell(
			state,
			CardFactory.Sorcery("Grim Tutor", manaCost: 3).WithTutor().Build()
		);

		Assert.Multiple(() =>
		{
			Assert.That(final.GetCardZone(bomb.Id).ZoneType, Is.EqualTo(ZoneType.Hand));
			Assert.That(final.GetCardZone(cheapTop.Id).ZoneType, Is.EqualTo(ZoneType.Library));
		});
	}

	/// <summary>
	/// A player search offers the WHOLE library and takes what was picked — including the card the
	/// auto-picker would never have chosen.
	///
	/// Grim Tutor's mana and life buy "any card in your deck". Resolved by
	/// SelectCardFromLibraryAction's best-by-mana-cost ranking it was a worse Sign in Blood, while
	/// the rendered text still promised a search. Picking the CHEAP card here is the assertion:
	/// the auto-picker would have taken the bomb, so passing this cannot be an accident.
	/// </summary>
	[Test]
	public void PlayerSearch_OffersTheWholeLibraryAndTakesThePick()
	{
		var libraryId = _state.GetPlayerZoneId(_ids.Player1Id, ZoneType.Library);
		var state = _state;

		(state, var cheap) = state.AddObject(
			MakeCreature("Cheap Answer", _ids.Player1Id, 1, 1, manaCost: 1),
			parentId: libraryId
		);
		(state, var bomb) = state.AddObject(
			MakeCreature("Expensive Bomb", _ids.Player1Id, 8, 8, manaCost: 8),
			parentId: libraryId
		);

		var handId = state.GetPlayerZoneId(_ids.Player1Id, ZoneType.Hand);
		var (paused, _) = CastSpell(
			state,
			CardFactory.Sorcery("Player Tutor", manaCost: 3).WithSearchLibrary().Build()
		);

		Assert.That(paused.IsWaitingForChoice, Is.True, "a real search must ask");

		var choice = paused.GetPendingChoice()!;
		Assert.Multiple(() =>
		{
			Assert.That(
				choice.Options.Select(o => o.Id),
				Does.Contain(cheap.Id).And.Contain(bomb.Id),
				"every legal card in the library is on offer, not just the best one"
			);
			Assert.That(
				paused.GetPendingChoiceDecidingPlayerId(),
				Is.EqualTo(_ids.Player1Id),
				"the searching player decides — not whoever happens to be active"
			);
		});

		var (final, _) = paused.ResolveChoice(ImmutableList.Create(cheap.Id));

		Assert.Multiple(() =>
		{
			Assert.That(
				final.GetCardZone(cheap.Id).ZoneType,
				Is.EqualTo(ZoneType.Hand),
				"the chosen card is the one that moves"
			);
			Assert.That(
				final.GetCardZone(bomb.Id).ZoneType,
				Is.EqualTo(ZoneType.Library),
				"the auto-picker's answer stays put"
			);
		});
	}

	/// <summary>
	/// An empty library must not wedge the pipeline. MinChoices is 1, but ResolveChoice clamps it
	/// to the option count, so a search with nothing to find resolves as "fail to find".
	/// </summary>
	[Test]
	public void PlayerSearch_WithNothingToFind_DoesNotBlockTheStack()
	{
		var (final, _) = CastSpell(
			_state,
			CardFactory.Sorcery("Player Tutor", manaCost: 3).WithSearchLibrary().Build()
		);

		if (final.IsWaitingForChoice)
		{
			var choice = final.GetPendingChoice()!;
			Assert.That(choice.Options, Is.Empty, "nothing to find");
			(final, _) = final.ResolveChoice(ImmutableList<int>.Empty);
		}

		Assert.That(final.HasPendingActions, Is.False, "the pipeline must complete either way");
	}

	// ===== CHOSEN DISCARD =====

	/// <summary>
	/// "Reveal your hand, you choose a card" takes their best, where best is most expensive.
	/// Random discard off a full hand is a coin flip; choosing is real disruption, and the two
	/// are costed differently.
	/// </summary>
	[Test]
	public void ChosenDiscard_TakesTheOpponentsMostExpensiveCard()
	{
		var handId = _state.GetPlayerZoneId(_ids.Player2Id, ZoneType.Hand);
		var state = _state;

		(state, var chaff) = state.AddObject(
			MakeCreature("Chaff", _ids.Player2Id, 1, 1, manaCost: 1),
			parentId: handId
		);
		(state, var bomb) = state.AddObject(
			MakeCreature("Their Bomb", _ids.Player2Id, 7, 7, manaCost: 7),
			parentId: handId
		);

		var (final, _) = CastSpell(
			state,
			CardFactory.Sorcery("Distress", manaCost: 2).WithChosenDiscard().Build()
		);

		Assert.Multiple(() =>
		{
			Assert.That(final.GetCardZone(bomb.Id).ZoneType, Is.EqualTo(ZoneType.Graveyard));
			Assert.That(final.GetCardZone(chaff.Id).ZoneType, Is.EqualTo(ZoneType.Hand));
		});
	}

	// ===== LIFE LOST THIS TURN =====

	[Test]
	public void LifeLostThisTurn_AccumulatesFromDamageAndDrainAlike()
	{
		var (afterDamage, _) = _state
			.AddAction(
				new DealDamageAction
				{
					Amount = 3,
					TargetIds = ImmutableList.Create(_ids.Player2Id),
				}
			)
			.ProcessAllActions();

		var (afterLoss, _) = afterDamage
			.AddAction(
				new LoseLifeAction { Amount = 2, TargetIds = ImmutableList.Create(_ids.Player2Id) }
			)
			.ProcessAllActions();

		Assert.That(afterLoss.GetPlayer(_ids.Player2Id).LifeLostThisTurn, Is.EqualTo(5));
	}

	/// <summary>
	/// Resets for BOTH players, not just the active one. Life loss overwhelmingly happens to the
	/// NON-active player, so an active-player-only reset would let the defender's tally carry
	/// over and "a player lost 4 life this turn" would fire on a two-turn total.
	/// </summary>
	[Test]
	public void LifeLostThisTurn_ResetsForBothPlayersAtStartOfTurn()
	{
		var (damaged, _) = _state
			.AddAction(
				new DealDamageAction
				{
					Amount = 3,
					TargetIds = ImmutableList.Create(_ids.Player2Id),
				}
			)
			.ProcessAllActions();

		// Player 1 takes their turn — player 2's tally from the previous turn must not survive.
		var (afterTurn, _) = damaged
			.AddAction(new StartTurnAction { ActivePlayerId = _ids.Player1Id, SkipDraw = true })
			.ProcessAllActions();

		Assert.That(afterTurn.GetPlayer(_ids.Player2Id).LifeLostThisTurn, Is.EqualTo(0));
	}

	// ===== SET LIFE TOTAL =====

	[Test]
	public void SetLifeTotal_SetsAnAbsoluteValueRatherThanAdjusting()
	{
		var (final, _) = _state
			.AddAction(
				new SetLifeTotalAction
				{
					Amount = 10,
					TargetIds = ImmutableList.Create(_ids.Player2Id),
				}
			)
			.ProcessAllActions();

		Assert.That(final.GetPlayer(_ids.Player2Id).Life, Is.EqualTo(10));
	}

	/// <summary>
	/// Amount = 0 is how "you lose the game" is expressed. The state-based loss check already
	/// owns HasLost, PlayerLostEvent and winner determination, so routing the alternate loss
	/// through the life total reuses all of it.
	/// </summary>
	[Test]
	public void SetLifeTotal_ToZero_LosesTheGame()
	{
		var (final, _) = _state
			.AddAction(
				new SetLifeTotalAction
				{
					Amount = 0,
					TargetIds = ImmutableList.Create(_ids.Player1Id),
				}
			)
			.ProcessAllActions();

		Assert.That(final.GetPlayer(_ids.Player1Id).HasLost, Is.True);
	}

	// ===== TRIGGER AMOUNT =====

	/// <summary>
	/// "Whenever you lose life, draw THAT MANY cards" (Vilis). Before ContextKeys.TriggerAmount
	/// existed, a trigger could only act on a number baked into the card, so every "that many"
	/// clause in the engine had been flattened to a constant.
	/// </summary>
	[Test]
	public void TriggerAmount_CarriesTheEventsNumberIntoTheEffect()
	{
		var state = StockLibrary(_state, _ids.Player1Id);
		var vilis = CardFactory
			.Creature("Amount Reader", manaCost: 8, power: 8, toughness: 8)
			.WithTriggeredAbility(
				"Draw That Many",
				TriggerConditions.OnLoseLife(),
				eb =>
					eb.WithAction(
						new DrawCardsAction
						{
							AmountContextKey = ContextKeys.TriggerAmount,
							TargetContextKey = ContextKeys.CastingPlayerId,
						},
						TargetingStrategy.NoTarget()
					)
			)
			.Build() with
		{
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
		};

		(state, _) = state.AddObject(
			vilis,
			parentId: state.GetPlayerZoneId(_ids.Player1Id, ZoneType.Battlefield)
		);
		var handBefore = state
			.GetCardsInZone(state.GetPlayerZoneId(_ids.Player1Id, ZoneType.Hand))
			.Count();

		var (final, _) = state
			.AddAction(
				new LoseLifeAction { Amount = 3, TargetIds = ImmutableList.Create(_ids.Player1Id) }
			)
			.ProcessAllActions();

		var handAfter = final
			.GetCardsInZone(final.GetPlayerZoneId(_ids.Player1Id, ZoneType.Hand))
			.Count();

		Assert.That(
			handAfter - handBefore,
			Is.EqualTo(3),
			"Losing 3 life should have drawn 3 cards, not a constant"
		);
	}

	// ===== ONCE-EACH MODES =====

	/// <summary>
	/// "Choose one that hasn't been chosen" (Demonic Pact). The exclusion has to survive between
	/// separate resolutions of the same trigger, turn after turn, which is why it lives on the
	/// card rather than on the ability or in pipeline context.
	/// </summary>
	[Test]
	public void OnceEachModes_DoNotRepeatAcrossResolutions()
	{
		var pact = CardFactory
			.Enchantment("Test Pact", manaCost: 4)
			.WithTriggeredAbility(
				"Pact",
				TriggerConditions.OnYourUpkeep(),
				eb =>
					eb.WithModes(
						onceEach: true,
						(
							"Gain 4",
							new GainLifeAction { Amount = 4, TargetIds = ImmutableList<int>.Empty }
						),
						(
							"Gain 40",
							new GainLifeAction { Amount = 40, TargetIds = ImmutableList<int>.Empty }
						)
					)
			)
			.Build() with
		{
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
		};

		var (state, card) = _state.AddObject(
			pact,
			parentId: _state.GetPlayerZoneId(_ids.Player1Id, ZoneType.Battlefield)
		);

		var offeredFirstTime = new List<int>();
		var offeredSecondTime = new List<int>();

		state = TakeUpkeepMode(state, offeredFirstTime);
		var afterFirst = ((Card)state.GetObject(card.Id)).GetComponent<ChosenModesComponent>();
		Assert.That(
			afterFirst,
			Is.Not.Null,
			"The taken mode should have been recorded on the card"
		);

		state = TakeUpkeepMode(state, offeredSecondTime);
		var finalChosen = ((Card)state.GetObject(card.Id))
			.GetComponent<ChosenModesComponent>()!
			.ChosenIndices;

		Assert.Multiple(() =>
		{
			Assert.That(offeredFirstTime, Is.EquivalentTo(new[] { 0, 1 }));
			Assert.That(
				offeredSecondTime,
				Is.EquivalentTo(new[] { 1 }),
				"The mode taken last turn should no longer be offered"
			);
			Assert.That(
				finalChosen,
				Is.Unique,
				"A mode was taken twice — the exclusion did not survive between resolutions"
			);
			Assert.That(finalChosen.Count, Is.EqualTo(2));
		});
	}

	/// <summary>
	/// Runs one upkeep and answers the modal choice it pauses on, recording which modes were
	/// offered. A ChoiceAction suspends the pipeline, so ProcessAllActions alone leaves the
	/// trigger half-resolved.
	/// </summary>
	private GameState TakeUpkeepMode(GameState state, List<int> offered)
	{
		(state, _) = state
			.AddAction(new StartTurnAction { ActivePlayerId = _ids.Player1Id, SkipDraw = true })
			.ProcessAllActions();

		var choice = state.GetPendingChoice();
		Assert.That(choice, Is.Not.Null, "The modal trigger should have paused for a choice");

		offered.AddRange(choice!.Options.Select(o => o.Id));

		(state, _) = state.ResolveChoice(ImmutableList.Create(choice.Options[0].Id));
		(state, _) = state.ProcessAllActions();
		return state;
	}

	// ===== CONDITIONAL EFFECTS SHARING A TARGET =====

	/// <summary>
	/// Necromantic Summons' spell mastery buff must land on the SAME creature the reanimation
	/// chose. Both effects carry the identical targeting strategy, and MtgActionGenerator fills
	/// one chosen target into every user-select effect on a spell — but that only reaches the
	/// conditional half because ConditionalAction is now an ITargetedAction. Before that it could
	/// only ever be NoTarget, and would have buffed nothing.
	/// </summary>
	[Test]
	public void ConditionalEffect_LandsOnTheSameTargetAsTheEffectBesideIt()
	{
		var state = _state;
		var graveyardId = state.GetPlayerZoneId(_ids.Player1Id, ZoneType.Graveyard);

		// Spell mastery needs two instants/sorceries in the graveyard to be satisfied.
		for (var i = 0; i < 2; i++)
			(state, _) = state.AddObject(
				new Card
				{
					Name = $"Dead Spell {i}",
					ManaCost = 1,
					OwnerId = _ids.Player1Id,
					ControllerId = _ids.Player1Id,
					Types = CardType.Sorcery,
					Components = ImmutableArray.Create<GameComponent>(new SpellComponent()),
				},
				parentId: graveyardId
			);

		(state, var corpse) = state.AddObject(
			MakeCreature("Corpse", _ids.Player1Id, 2, 2, manaCost: 3),
			parentId: graveyardId
		);

		var summons = CoresetCube.Cards.Single(c => c.Name == "Necromantic Summons");
		var (withCard, card) = state.AddObject(
			summons with
			{
				OwnerId = _ids.Player1Id,
				ControllerId = _ids.Player1Id,
			},
			parentId: state.GetPlayerZoneId(_ids.Player1Id, ZoneType.Hand)
		);

		var legal = MtgActionGenerator
			.GetLegalActions(withCard, _ids, _ids.Player1Id)
			.OfType<CastSpellAction>()
			.FirstOrDefault(a =>
				a.CardId == card.Id && a.TargetIds.Values.Any(v => v.Contains(corpse.Id))
			);

		Assert.That(
			legal,
			Is.Not.Null,
			"Should be offered targeting the creature in the graveyard"
		);

		var (final, _) = withCard.AddAction(legal!).ProcessAllActions();

		Assert.Multiple(() =>
		{
			Assert.That(
				final.GetCardZone(corpse.Id).ZoneType,
				Is.EqualTo(ZoneType.Battlefield),
				"The creature should have been reanimated"
			);
			Assert.That(
				final.GetEffectivePower(corpse.Id),
				Is.EqualTo(4),
				"Spell mastery should have added +2/+2 to the reanimated creature, not to nothing"
			);
		});
	}

	/// <summary>
	/// "Put target creature card from a graveyard onto the battlefield UNDER YOUR CONTROL."
	/// A card in a graveyard still carries the ControllerId it had in play, so without the
	/// reassignment this handed the opponent's creature straight back to them — strictly worse
	/// than the spell doing nothing, and completely silent.
	/// </summary>
	[Test]
	public void Reanimation_TakesControlOfCreaturesFromTheOpponentsGraveyard()
	{
		var (state, corpse) = _state.AddObject(
			MakeCreature("Their Corpse", _ids.Player2Id, 4, 4, manaCost: 4),
			parentId: _state.GetPlayerZoneId(_ids.Player2Id, ZoneType.Graveyard)
		);

		var (final, _) = CastSpell(
			state,
			CardFactory
				.Sorcery("Test Obedience", manaCost: 6)
				.WithReanimate(fromAnyGraveyard: true)
				.Build(),
			targetId: corpse.Id
		);

		var reanimated = (Card)final.GetObject(corpse.Id);

		Assert.Multiple(() =>
		{
			Assert.That(
				reanimated.ControllerId,
				Is.EqualTo(_ids.Player1Id),
				"The reanimated creature should be under the caster's control"
			);
			Assert.That(
				final.GetCardZoneId(corpse.Id),
				Is.EqualTo(final.GetPlayerZoneId(_ids.Player1Id, ZoneType.Battlefield)),
				"and physically on the caster's battlefield, or no scan will see it"
			);
			Assert.That(
				reanimated.OwnerId,
				Is.EqualTo(_ids.Player2Id),
				"Ownership is untouched — it returns to their graveyard when it dies"
			);
		});
	}

	// ===== FLASHBACK ADDITIONAL COSTS =====

	/// <summary>
	/// A repeatable graveyard recursion bounded only by mana just comes back forever. The exile
	/// cost is what gives it a hard floor and makes graveyard hate live against it.
	/// </summary>
	[Test]
	public void GraveyardRecursion_PaysItsExileCostAndCannotEatItself()
	{
		var graveyardId = _state.GetPlayerZoneId(_ids.Player1Id, ZoneType.Graveyard);
		var state = _state;

		var despoiler = MakeCreature("Despoiler", _ids.Player1Id, 3, 1, manaCost: 2) with
		{
			Components = ImmutableArray.Create<GameComponent>(
				new PermanentComponent(),
				new CreatureComponent { Power = 3, Toughness = 1 },
				new FlashbackComponent
				{
					FlashbackManaCost = 2,
					AdditionalCosts = ImmutableList.Create<AdditionalCost>(
						new ExileFromGraveyardAdditionalCost { Count = 2 }
					),
				}
			),
		};

		(state, var despoilerCard) = state.AddObject(despoiler, parentId: graveyardId);
		(state, var fodder1) = state.AddObject(
			MakeCreature("Fodder A", _ids.Player1Id, 1, 1, manaCost: 1),
			parentId: graveyardId
		);
		(state, var fodder2) = state.AddObject(
			MakeCreature("Fodder B", _ids.Player1Id, 1, 1, manaCost: 1),
			parentId: graveyardId
		);

		var legal = MtgActionGenerator
			.GetLegalActions(state, _ids, _ids.Player1Id)
			.OfType<CastFromGraveyardAction>()
			.FirstOrDefault(a => a.CardId == despoilerCard.Id);

		Assert.That(legal, Is.Not.Null, "Recursion should be offered when the cost can be paid");

		var (final, _) = state.AddAction(legal!).ProcessAllActions();

		Assert.Multiple(() =>
		{
			Assert.That(
				final.GetCardZone(despoilerCard.Id).ZoneType,
				Is.EqualTo(ZoneType.Battlefield),
				"Despoiler should have returned to the battlefield"
			);
			Assert.That(
				final.GetCardZone(fodder1.Id).ZoneType,
				Is.EqualTo(ZoneType.Exile),
				"The cost should have exiled the fodder"
			);
			Assert.That(final.GetCardZone(fodder2.Id).ZoneType, Is.EqualTo(ZoneType.Exile));
		});
	}

	/// <summary>With nothing else in the graveyard the cost cannot be paid, so it is not offered.</summary>
	[Test]
	public void GraveyardRecursion_IsNotOfferedWhenTheExileCostCannotBePaid()
	{
		var graveyardId = _state.GetPlayerZoneId(_ids.Player1Id, ZoneType.Graveyard);
		var despoiler = MakeCreature("Despoiler", _ids.Player1Id, 3, 1, manaCost: 2) with
		{
			Components = ImmutableArray.Create<GameComponent>(
				new PermanentComponent(),
				new CreatureComponent { Power = 3, Toughness = 1 },
				new FlashbackComponent
				{
					FlashbackManaCost = 2,
					AdditionalCosts = ImmutableList.Create<AdditionalCost>(
						new ExileFromGraveyardAdditionalCost { Count = 2 }
					),
				}
			),
		};

		var (state, card) = _state.AddObject(despoiler, parentId: graveyardId);

		var legal = MtgActionGenerator
			.GetLegalActions(state, _ids, _ids.Player1Id)
			.OfType<CastFromGraveyardAction>()
			.FirstOrDefault(a => a.CardId == card.Id);

		Assert.That(legal, Is.Null);
	}

	// ===== "WHENEVER THIS OR ANOTHER X DIES" =====

	/// <summary>
	/// A death-payoff lord stops paying out once it is dead.
	///
	/// "Whenever this or another Human you control dies, make a Zombie" was declared
	/// ActiveInZone = Graveyard so it could catch its OWN death — by the time triggers are
	/// evaluated the card has already moved there. But the graveyard pass re-evaluates every card
	/// sitting in the graveyard on every batch, forever, so the lord kept making Zombies from the
	/// graveyard for the rest of the game. That is the QA report: "my opponent kept getting 2/2
	/// Zombies even though it had already died".
	///
	/// Inline card so rebalancing Xathrid Necromancer cannot delete the coverage.
	/// </summary>
	[Test]
	public void DeathPayoffLord_StopsTriggeringOnceItIsInTheGraveyard()
	{
		var battlefieldId = _state.GetPlayerZoneId(_ids.Player1Id, ZoneType.Battlefield);

		var (state, lord) = _state.AddObject(
			MakeHumanLord() with
			{
				OwnerId = _ids.Player1Id,
				ControllerId = _ids.Player1Id,
			},
			parentId: battlefieldId
		);

		// Kill the lord itself. That death is its own trigger, so one Zombie is correct.
		(state, _) = state
			.AddAction(new DestroyCreatureAction { TargetIds = [lord.Id] })
			.ProcessAllActions();

		var afterOwnDeath = CountTokens(state, battlefieldId);
		Assert.That(afterOwnDeath, Is.EqualTo(1), "its own death should still pay out once");

		// Now kill an unrelated Human while the lord sits in the graveyard.
		var (withHuman, human) = state.AddObject(
			MakeHuman("Villager") with
			{
				OwnerId = _ids.Player1Id,
				ControllerId = _ids.Player1Id,
			},
			parentId: battlefieldId
		);
		(state, _) = withHuman
			.AddAction(new DestroyCreatureAction { TargetIds = [human.Id] })
			.ProcessAllActions();

		Assert.That(
			CountTokens(state, battlefieldId),
			Is.EqualTo(afterOwnDeath),
			"a dead lord must not keep making Zombies from the graveyard"
		);
	}

	/// <summary>
	/// The other half: while it is alive it must pay out for another Human dying, including one
	/// killed by a SACRIFICE rather than by damage or destruction.
	/// </summary>
	[Test]
	public void DeathPayoffLord_TriggersOnAnotherHumanDying()
	{
		var battlefieldId = _state.GetPlayerZoneId(_ids.Player1Id, ZoneType.Battlefield);

		var (state, _) = _state.AddObject(
			MakeHumanLord() with
			{
				OwnerId = _ids.Player1Id,
				ControllerId = _ids.Player1Id,
			},
			parentId: battlefieldId
		);

		var (withHuman, human) = state.AddObject(
			MakeHuman("Villager") with
			{
				OwnerId = _ids.Player1Id,
				ControllerId = _ids.Player1Id,
			},
			parentId: battlefieldId
		);

		var before = CountTokens(withHuman, battlefieldId);

		(state, _) = withHuman
			.AddAction(new DestroyCreatureAction { TargetIds = [human.Id] })
			.ProcessAllActions();

		Assert.That(
			CountTokens(state, battlefieldId) - before,
			Is.EqualTo(1),
			"a living lord pays out when another Human dies"
		);
	}

	private static int CountTokens(GameState state, int battlefieldId) =>
		state.GetCardsInZone(battlefieldId).Count(c => c.Name == "Zombie");

	private static Card MakeHuman(string name) =>
		CardFactory
			.Creature(name, manaCost: 1, power: 1, toughness: 1)
			.WithSubtype("Human")
			.Build();

	/// <summary>
	/// "Whenever this or another Human you control dies, create a 2/2 Zombie."
	///
	/// TWO triggers, and the split is the whole point. "Another Human" is battlefield-active, so
	/// it stops the moment the lord leaves play. Its OWN death needs the graveyard pass, because
	/// the card has already moved there by the time triggers are evaluated — but scoped to itself
	/// via OnSelfDies, so sitting in the graveyard it can only respond to an event that cannot
	/// happen again.
	///
	/// One graveyard-active trigger with a broad filter looks like it covers both and covers
	/// neither correctly: inert while alive, permanent once dead.
	/// </summary>
	private static Card MakeHumanLord() =>
		CardFactory
			.Creature("Test Necromancer", manaCost: 3, power: 2, toughness: 2)
			.WithSubtype("Human")
			.WithTriggeredAbility(
				"Raise the Fallen",
				TriggerConditions.OnCreatureYouControlDies("Human"),
				eb => eb.WithCreateTokens(ZombieToken())
			)
			.WithDeathTrigger("Raise the Fallen", eb => eb.WithCreateTokens(ZombieToken()))
			.Build();

	private static Card ZombieToken() =>
		CardFactory
			.Creature("Zombie", manaCost: 0, power: 2, toughness: 2)
			.WithSubtype("Zombie")
			.Build();

	// ===== HELPERS =====

	private static Card MakeCreature(
		string name,
		int ownerId,
		int power,
		int toughness,
		int manaCost
	) =>
		new()
		{
			Name = name,
			ManaCost = manaCost,
			OwnerId = ownerId,
			ControllerId = ownerId,
			Types = CardType.Creature,
			Components = ImmutableArray.Create<GameComponent>(
				new PermanentComponent(),
				new CreatureComponent { Power = power, Toughness = toughness }
			),
		};

	private static GameState StockLibrary(GameState state, int playerId)
	{
		var libraryId = state.GetPlayerZoneId(playerId, ZoneType.Library);
		for (var i = 0; i < 20; i++)
			(state, _) = state.AddObject(
				MakeCreature($"Filler {i}", playerId, 1, 1, manaCost: 1),
				parentId: libraryId
			);
		return state;
	}

	/// <summary>
	/// Puts the template in player 1's hand, casts it, and resolves everything. Pass targetId for
	/// a spell that needs one; the target is keyed by EFFECT INDEX, not by 0.
	/// </summary>
	private (GameState State, int CardId) CastSpell(
		GameState state,
		Card template,
		int targetId = 0
	)
	{
		var handId = state.GetPlayerZoneId(_ids.Player1Id, ZoneType.Hand);
		var (withCard, card) = state.AddObject(
			template with
			{
				OwnerId = _ids.Player1Id,
				ControllerId = _ids.Player1Id,
			},
			parentId: handId
		);

		var cast = new CastSpellAction { CardId = card.Id, CastingPlayerId = _ids.Player1Id };
		if (targetId != 0)
		{
			var spell = ((Card)withCard.GetObject(card.Id)).GetComponent<SpellComponent>()!;
			var index = spell.Effects.FindIndex(e => e.TargetingStrategy.RequiresUserSelection);
			cast = cast with
			{
				TargetIds = ImmutableDictionary<int, ImmutableList<int>>.Empty.Add(
					index,
					ImmutableList.Create(targetId)
				),
			};
		}

		var (final, _) = withCard.AddAction(cast).ProcessAllActions();

		return (final, card.Id);
	}
}
