using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;
using NUnit.Framework;

namespace MtgCore.Tests;

/// <summary>
/// Covers the engine primitives added for the Hollowmere set: mill, threshold,
/// graveyard-active statics, the werewolf spell counter, deathtouch, temporary keyword
/// grants, discard triggers (the madness reskin), and creature recursion.
///
/// All cards here are defined inline rather than looked up from CardLibrary, so card
/// balance changes cannot break these tests.
/// </summary>
[TestFixture]
public class HollowmereMechanicsTests
{
	private GameState _state;
	private MtgGameIds _ids;

	[SetUp]
	public void Setup()
	{
		(_state, _ids) = MtgGameFactory.CreateForTesting();
	}

	// ===== MILL =====

	[Test]
	public void Mill_MovesCardsFromLibraryToGraveyard()
	{
		var state = TestCardFactory.AddCardsToLibrary(_state, _ids.Player1Id, "A", "B", "C", "D");

		var (final, _) = state.AddAction(MakeMill(3, _ids.Player1Id)).ProcessAllActions();

		Assert.That(final.GetCardsInZone(_ids.Player1GraveyardId).Count(), Is.EqualTo(3));
		Assert.That(final.GetCardsInZone(_ids.Player1LibraryId).Count(), Is.EqualTo(1));
	}

	[Test]
	public void Mill_TakesFromTopOfLibraryInOrder()
	{
		var state = TestCardFactory.AddCardsToLibrary(
			_state,
			_ids.Player1Id,
			"First",
			"Second",
			"Third"
		);

		var (final, _) = state.AddAction(MakeMill(2, _ids.Player1Id)).ProcessAllActions();

		var graveyardNames = final.GetCardsInZone(_ids.Player1GraveyardId).Select(c => c.Name);
		Assert.That(graveyardNames, Is.EquivalentTo(new[] { "First", "Second" }));
	}

	[Test]
	public void Mill_OnEmptyLibrary_DoesNotThrow()
	{
		var (final, _) = _state.AddAction(MakeMill(3, _ids.Player1Id)).ProcessAllActions();

		Assert.That(final.GetCardsInZone(_ids.Player1GraveyardId), Is.Empty);
	}

	[Test]
	public void Mill_MoreThanLibraryHolds_MillsEverythingAvailable()
	{
		var state = TestCardFactory.AddCardsToLibrary(_state, _ids.Player1Id, "A", "B");

		var (final, _) = state.AddAction(MakeMill(10, _ids.Player1Id)).ProcessAllActions();

		Assert.That(final.GetCardsInZone(_ids.Player1GraveyardId).Count(), Is.EqualTo(2));
		Assert.That(final.GetCardsInZone(_ids.Player1LibraryId), Is.Empty);
	}

	[Test]
	public void Mill_DoesNotTouchOpponentLibrary()
	{
		var state = TestCardFactory.AddCardsToLibrary(_state, _ids.Player1Id, "A", "B", "C");
		state = TestCardFactory.AddCardsToLibrary(state, _ids.Player2Id, "X", "Y", "Z");

		var (final, _) = state.AddAction(MakeMill(2, _ids.Player1Id)).ProcessAllActions();

		Assert.That(final.GetCardsInZone(_ids.Player2GraveyardId), Is.Empty);
		Assert.That(final.GetCardsInZone(_ids.Player2LibraryId).Count(), Is.EqualTo(3));
	}

	// ===== THRESHOLD =====

	[Test]
	public void Threshold_Inactive_WhenGraveyardBelowMinimum()
	{
		var (state, reveler) = AddToBattlefield(_state, MakeThresholdCreature());

		Assert.That(state.GetEffectivePower(reveler.Id), Is.EqualTo(2), "Base 2/2 below threshold");
		Assert.That(state.GetEffectiveToughness(reveler.Id), Is.EqualTo(2));
	}

	[Test]
	public void Threshold_Active_WhenGraveyardMeetsMinimum()
	{
		var (state, reveler) = AddToBattlefield(_state, MakeThresholdCreature());
		state = FillGraveyard(state, _ids.Player1Id, 7);

		Assert.That(state.GetEffectivePower(reveler.Id), Is.EqualTo(5), "Base 2 + threshold 3");
		Assert.That(state.GetEffectiveToughness(reveler.Id), Is.EqualTo(5));
	}

	[Test]
	public void Threshold_TurnsOffAgain_WhenGraveyardShrinks()
	{
		var (state, reveler) = AddToBattlefield(_state, MakeThresholdCreature());
		state = FillGraveyard(state, _ids.Player1Id, 7);
		Assert.That(
			state.GetEffectivePower(reveler.Id),
			Is.EqualTo(5),
			"Precondition: threshold on"
		);

		// Exile one card to drop back under the minimum. This is the case a push-model
		// static would miss — nothing here fires a battlefield event.
		var first = state.GetCardsInZone(_ids.Player1GraveyardId).First();
		state = state.MoveObject(first.Id, _ids.Player1ExileId);

		Assert.That(state.GetEffectivePower(reveler.Id), Is.EqualTo(2), "Threshold must turn off");
	}

	[Test]
	public void Threshold_GrantsKeywords_OnlyWhenActive()
	{
		var (state, reveler) = AddToBattlefield(_state, MakeThresholdCreature());
		Assert.That(state.GetEffectiveFlying(reveler.Id), Is.False, "No flying below threshold");

		state = FillGraveyard(state, _ids.Player1Id, 7);

		Assert.That(state.GetEffectiveFlying(reveler.Id), Is.True, "Flying once threshold is met");
	}

	[Test]
	public void Threshold_CountsOnlyControllerGraveyard()
	{
		var (state, reveler) = AddToBattlefield(_state, MakeThresholdCreature());
		state = FillGraveyard(state, _ids.Player2Id, 10);

		Assert.That(
			state.GetEffectivePower(reveler.Id),
			Is.EqualTo(2),
			"Opponent graveyard must not count"
		);
	}

	// ===== GRAVEYARD-ACTIVE STATICS (Wonder) =====

	[Test]
	public void GraveyardStatic_GrantsKeyword_WhileSourceIsInGraveyard()
	{
		var (state, bear) = AddToBattlefield(
			_state,
			TestCardFactory.MakeCreatureCard("Bear", _ids.Player1Id, 2, 2)
		);
		Assert.That(state.GetEffectiveFlying(bear.Id), Is.False, "Precondition: no flying");

		state = AddWonderToGraveyard(state);

		Assert.That(
			state.GetEffectiveFlying(bear.Id),
			Is.True,
			"Wonder grants flying from graveyard"
		);
	}

	[Test]
	public void GraveyardStatic_Deactivates_WhenSourceLeavesGraveyard()
	{
		var (state, bear) = AddToBattlefield(
			_state,
			TestCardFactory.MakeCreatureCard("Bear", _ids.Player1Id, 2, 2)
		);
		state = AddWonderToGraveyard(state);
		Assert.That(state.GetEffectiveFlying(bear.Id), Is.True, "Precondition: flying granted");

		var wonder = state.GetCardsInZone(_ids.Player1GraveyardId).First(c => c.Name == "Wonder");
		state = state.MoveObject(wonder.Id, _ids.Player1ExileId);
		state = StaticAbilityEngine.ProcessZoneSourceLeft(state, wonder.Id, _ids.GameId);

		Assert.That(state.GetEffectiveFlying(bear.Id), Is.False, "Flying must be revoked on exile");
	}

	[Test]
	public void GraveyardStatic_IsNotActive_WhileSourceIsOnBattlefield()
	{
		var (state, bear) = AddToBattlefield(
			_state,
			TestCardFactory.MakeCreatureCard("Bear", _ids.Player1Id, 2, 2)
		);
		var (state2, _) = AddToBattlefield(state, MakeWonder());

		Assert.That(
			state2.GetEffectiveFlying(bear.Id),
			Is.False,
			"ActiveInZone=Graveyard must not apply from the battlefield"
		);
	}

	// ===== WEREWOLF SPELL COUNTER =====

	[Test]
	public void SpellsCastLastTurn_StartsAtZero()
	{
		var game = (MtgGame)_state.GetObject(_ids.GameId);
		Assert.That(game.SpellsCastLastTurn, Is.EqualTo(0));
	}

	[Test]
	public void SpellsCastLastTurn_ReceivesPreviousTurnCount_OnTurnStart()
	{
		var game = (MtgGame)_state.GetObject(_ids.GameId);
		var state = _state.UpdateObject(_ids.GameId, game with { SpellsCastThisTurn = 3 });

		var (final, _) = state
			.AddAction(
				new StartTurnAction
				{
					ActivePlayerId = _ids.Player1Id,
					BattlefieldId = _ids.Player1BattlefieldId,
					SkipDraw = true,
				}
			)
			.ProcessAllActions();

		var finalGame = (MtgGame)final.GetObject(_ids.GameId);
		Assert.That(finalGame.SpellsCastLastTurn, Is.EqualTo(3), "Previous count rolls over");
		Assert.That(finalGame.SpellsCastThisTurn, Is.EqualTo(0), "Current count resets");
	}

	// ===== DEATHTOUCH =====

	[Test]
	public void Deathtouch_KillsLargerCreature()
	{
		var attacker = TestCardFactory.MakeCreatureCard("Deathtoucher", _ids.Player1Id, 1, 1);
		var (state, attackerCard) = AddToBattlefield(_state, WithDeathtouch(attacker));
		var (state2, defender) = AddToBattlefield(
			state,
			TestCardFactory.MakeCreatureCard("Giant", _ids.Player2Id, 6, 6) with
			{
				OwnerId = _ids.Player2Id,
				ControllerId = _ids.Player2Id,
			},
			_ids.Player2Id
		);

		var (final, _) = state2
			.AddAction(
				new AttackAction
				{
					AttackerId = attackerCard.Id,
					TargetId = defender.Id,
					AttackingPlayerId = _ids.Player1Id,
				}
			)
			.ProcessAllActions();

		var zone = final.GetCardZone(defender.Id);
		Assert.That(
			zone.ZoneType,
			Is.EqualTo(ZoneType.Graveyard),
			"1 deathtouch damage kills a 6/6"
		);
	}

	[Test]
	public void Deathtouch_WithoutIt_LargerCreatureSurvives()
	{
		var (state, attacker) = AddToBattlefield(
			_state,
			TestCardFactory.MakeCreatureCard("Squire", _ids.Player1Id, 1, 1)
		);
		var (state2, defender) = AddToBattlefield(
			state,
			TestCardFactory.MakeCreatureCard("Giant", _ids.Player2Id, 6, 6) with
			{
				OwnerId = _ids.Player2Id,
				ControllerId = _ids.Player2Id,
			},
			_ids.Player2Id
		);

		var (final, _) = state2
			.AddAction(
				new AttackAction
				{
					AttackerId = attacker.Id,
					TargetId = defender.Id,
					AttackingPlayerId = _ids.Player1Id,
				}
			)
			.ProcessAllActions();

		Assert.That(final.GetCardZone(defender.Id).ZoneType, Is.EqualTo(ZoneType.Battlefield));
	}

	[Test]
	public void Deathtouch_OnDefender_KillsAttackerBack()
	{
		var (state, attacker) = AddToBattlefield(
			_state,
			TestCardFactory.MakeCreatureCard("Brute", _ids.Player1Id, 5, 5)
		);
		var defender = WithDeathtouch(
			TestCardFactory.MakeCreatureCard("Spiky", _ids.Player2Id, 1, 1)
		) with
		{
			OwnerId = _ids.Player2Id,
			ControllerId = _ids.Player2Id,
		};
		var (state2, defenderCard) = AddToBattlefield(state, defender, _ids.Player2Id);

		var (final, _) = state2
			.AddAction(
				new AttackAction
				{
					AttackerId = attacker.Id,
					TargetId = defenderCard.Id,
					AttackingPlayerId = _ids.Player1Id,
				}
			)
			.ProcessAllActions();

		Assert.That(
			final.GetCardZone(attacker.Id).ZoneType,
			Is.EqualTo(ZoneType.Graveyard),
			"Defender's deathtouch kills the 5/5 back"
		);
	}

	// ===== TEMPORARY KEYWORD GRANTS =====

	[Test]
	public void GrantKeyword_AppliesImmediately()
	{
		var (state, bear) = AddToBattlefield(
			_state,
			TestCardFactory.MakeCreatureCard("Bear", _ids.Player1Id, 2, 2)
		);

		var (final, _) = state
			.AddAction(
				new GrantKeywordAction
				{
					GrantsFlying = true,
					TargetIds = ImmutableList.Create(bear.Id),
				}
			)
			.ProcessAllActions();

		Assert.That(final.GetEffectiveFlying(bear.Id), Is.True);
	}

	[Test]
	public void GrantKeyword_UntilEndOfTurn_ExpiresOnNextTurnStart()
	{
		var (state, bear) = AddToBattlefield(
			_state,
			TestCardFactory.MakeCreatureCard("Bear", _ids.Player1Id, 2, 2)
		);

		var (granted, _) = state
			.AddAction(
				new GrantKeywordAction
				{
					GrantsFlying = true,
					Duration = ModifierDuration.UntilEndOfTurn,
					TargetIds = ImmutableList.Create(bear.Id),
				}
			)
			.ProcessAllActions();
		Assert.That(granted.GetEffectiveFlying(bear.Id), Is.True, "Precondition: granted");

		var (final, _) = granted
			.AddAction(
				new StartTurnAction
				{
					ActivePlayerId = _ids.Player1Id,
					BattlefieldId = _ids.Player1BattlefieldId,
					SkipDraw = true,
				}
			)
			.ProcessAllActions();

		Assert.That(final.GetEffectiveFlying(bear.Id), Is.False, "Grant must expire");
	}

	[Test]
	public void GrantKeyword_Permanent_SurvivesTurnStart()
	{
		var (state, bear) = AddToBattlefield(
			_state,
			TestCardFactory.MakeCreatureCard("Bear", _ids.Player1Id, 2, 2)
		);

		var (granted, _) = state
			.AddAction(
				new GrantKeywordAction
				{
					GrantsFlying = true,
					Duration = ModifierDuration.Permanent,
					TargetIds = ImmutableList.Create(bear.Id),
				}
			)
			.ProcessAllActions();

		var (final, _) = granted
			.AddAction(
				new StartTurnAction
				{
					ActivePlayerId = _ids.Player1Id,
					BattlefieldId = _ids.Player1BattlefieldId,
					SkipDraw = true,
				}
			)
			.ProcessAllActions();

		Assert.That(final.GetEffectiveFlying(bear.Id), Is.True, "Permanent grant must persist");
	}

	// ===== MADNESS RESKIN (discard triggers) =====

	[Test]
	public void DiscardTrigger_FiresWhenCardIsDiscarded()
	{
		var temper = MakeMadnessCard();
		var (state, added) = _state.AddObject(temper, _ids.Player1HandId);

		var (final, _) = state
			.AddAction(new DiscardCardsAction { TargetIds = ImmutableList.Create(added.Id) })
			.ProcessAllActions();

		Assert.That(
			final.GetPlayer(_ids.Player2Id).Life,
			Is.EqualTo(17),
			"Madness trigger drains 3 life"
		);
		Assert.That(final.GetPlayer(_ids.Player1Id).Life, Is.EqualTo(23));
	}

	[Test]
	public void DiscardTrigger_DoesNotFireOnMill()
	{
		var temper = MakeMadnessCard();
		var (state, _) = _state.AddObject(temper, _ids.Player1LibraryId);

		var (final, _) = state.AddAction(MakeMill(1, _ids.Player1Id)).ProcessAllActions();

		Assert.That(
			final.GetPlayer(_ids.Player2Id).Life,
			Is.EqualTo(20),
			"Milling is not discarding — the madness trigger must not fire"
		);
	}

	// ===== CREATURE RECURSION =====

	[Test]
	public void CreatureWithFlashback_CanBeCastFromGraveyard()
	{
		var crawler = MakeRecursiveCreature();
		var (state, added) = _state.AddObject(crawler, _ids.Player1GraveyardId);

		var actions = MtgActionGenerator.GetLegalActions(state, _ids.Player1Id);

		Assert.That(
			actions.OfType<CastFromGraveyardAction>().Any(a => a.CardId == added.Id),
			Is.True,
			"Action generator must offer creature recursion"
		);
	}

	[Test]
	public void CreatureRecursion_PutsCreatureOntoBattlefield_AndDoesNotExileIt()
	{
		var crawler = MakeRecursiveCreature();
		var (state, added) = _state.AddObject(crawler, _ids.Player1GraveyardId);

		var (final, _) = state
			.AddAction(
				new CastFromGraveyardAction
				{
					CardId = added.Id,
					CastingPlayerId = _ids.Player1Id,
					TargetIds = ImmutableDictionary<int, ImmutableList<int>>.Empty,
				}
			)
			.ProcessAllActions();

		Assert.That(
			final.GetCardZone(added.Id).ZoneType,
			Is.EqualTo(ZoneType.Battlefield),
			"Creature recursion resolves onto the battlefield, unlike spell flashback"
		);
	}

	// ===== HELPERS =====

	private MillAction MakeMill(int amount, int playerId) =>
		new() { Amount = amount, TargetIds = ImmutableList.Create(playerId) };

	private static Card WithDeathtouch(Card card)
	{
		var creature = card.GetComponent<CreatureComponent>()!;
		return (Card)card.WithComponentReplaced(creature with { HasDeathtouch = true });
	}

	/// A 2/2 that becomes 5/5 with flying once seven cards are in its controller's graveyard.
	private Card MakeThresholdCreature() =>
		new()
		{
			Name = "Nightfall Reveler",
			ManaCost = 3,
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
			Components = ImmutableArray.Create<GameComponent>(
				new PermanentComponent(),
				new CreatureComponent { Power = 2, Toughness = 2 },
				new ThresholdComponent
				{
					Minimum = 7,
					PowerBonus = 3,
					ToughnessBonus = 3,
					GrantsFlying = true,
					Duration = ModifierDuration.Permanent,
				}
			),
		};

	/// Wonder: while in your graveyard, creatures you control have flying.
	private Card MakeWonder() =>
		new()
		{
			Name = "Wonder",
			ManaCost = 4,
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
			Components = ImmutableArray.Create<GameComponent>(
				new PermanentComponent(),
				new CreatureComponent { Power = 2, Toughness = 2 },
				new StaticGrantKeywordAbility
				{
					GrantsFlying = true,
					ActiveInZone = ZoneType.Graveyard,
					Filter = new IsOnBattlefieldSpecification()
						.And(new IsCreatureSpecification())
						.And(new IsControlledByYouSpecification()),
				}
			),
		};

	/// Fiery Temper: when you discard this, it deals 3 damage to the opponent.
	private Card MakeMadnessCard() =>
		new()
		{
			Name = "Fiery Temper",
			ManaCost = 2,
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
			Components = ImmutableArray.Create<GameComponent>(
				new SpellComponent(),
				new TriggeredAbilityComponent
				{
					Name = "Madness",
					Condition = new EventTriggerCondition
					{
						EventTypeName = EventTypeNames.CardDiscarded,
						Filter = new IsSourceCardSpecification(),
					},
					// DrainLifeAction derives the opponent from CastingPlayerId in context.
					// A targeted action would not work here: NoTarget() resolves to an empty
					// target list, which ResolveEffectAction injects over any hardcoded TargetIds.
					Effect = new CardEffect
					{
						TargetingStrategy = TargetingStrategy.NoTarget(),
						ActionTemplate = new DrainLifeAction
						{
							Amount = 3,
							TargetOpponent = true,
							PlayerIdContextKey = ContextKeys.CastingPlayerId,
						},
					},
					ActiveInZone = ZoneType.Graveyard,
				}
			),
		};

	/// Gravecrawler: a creature castable from the graveyard.
	private Card MakeRecursiveCreature() =>
		new()
		{
			Name = "Gravecrawler",
			ManaCost = 1,
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
			Components = ImmutableArray.Create<GameComponent>(
				new PermanentComponent(),
				new CreatureComponent { Power = 2, Toughness = 1 },
				new FlashbackComponent { FlashbackManaCost = 1 }
			),
		};

	private GameState FillGraveyard(GameState state, int playerId, int count)
	{
		var graveyardId = state.GetPlayerZoneId(playerId, ZoneType.Graveyard);
		for (int i = 0; i < count; i++)
			state = state
				.AddObject(TestCardFactory.MakePlainCard($"Filler{i}", playerId), graveyardId)
				.GameState;
		return state;
	}

	private GameState AddWonderToGraveyard(GameState state)
	{
		var (withWonder, wonder) = state.AddObject(MakeWonder(), _ids.Player1GraveyardId);
		return StaticAbilityEngine.ProcessZoneSourceEntered(withWonder, wonder.Id, _ids.GameId);
	}

	private (GameState, Card) AddToBattlefield(GameState state, Card template, int playerId = 0)
	{
		var owner = playerId == 0 ? _ids.Player1Id : playerId;
		var battlefieldId = state.GetPlayerZoneId(owner, ZoneType.Battlefield);
		var card = template with { OwnerId = owner, ControllerId = owner };
		var (newState, added) = state.AddObject(card, battlefieldId);

		var creature = added.GetComponent<CreatureComponent>();
		if (creature != null)
			newState = newState.UpdateObject(
				added.Id,
				added.WithComponentReplaced(creature with { HasSummoningSickness = false })
			);

		newState = StaticAbilityEngine.ProcessPermanentEntered(newState, added.Id, _ids.GameId);
		return (newState, (Card)newState.GetObject(added.Id));
	}
}
