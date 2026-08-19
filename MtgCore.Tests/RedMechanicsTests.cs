using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;
using MtgCore.Cards.Builders;
using NUnit.Framework;
using static MtgCore.Cards.Builders.TargetBuilder;

namespace MtgCore.Tests;

/// <summary>
/// The engine mechanics added for the Core Set Cube's red section.
///
/// Card-agnostic on purpose, like BlackMechanicsTests: these assert the mechanic rather than the
/// card that motivated it, so rebalancing Abbot of Keral Keep cannot quietly delete the coverage.
/// </summary>
[TestFixture]
public class RedMechanicsTests
{
	private GameState _state;
	private MtgGameIds _ids;

	[SetUp]
	public void Setup()
	{
		(_state, _ids) = MtgGameFactory.CreateForTesting();
	}

	// ===== IMPULSE DRAW =====

	/// <summary>
	/// "Exile the top card of your library. You may play it this turn." The card must actually be
	/// castable out of exile — an impulse draw that exiles a card nobody can play is a Mill 1 that
	/// reads like card advantage, and nothing would throw.
	/// </summary>
	[Test]
	public void ImpulseDraw_ExiledCardIsCastableFromExile()
	{
		var libraryId = _state.GetPlayerZoneId(_ids.Player1Id, ZoneType.Library);
		var (state, top) = _state.AddObject(
			MakeCreature("Impulse Target", _ids.Player1Id, 2, 2, manaCost: 1),
			parentId: libraryId
		);

		state = Impulse(state);

		var exiled = (Card)state.GetObject(top.Id);
		Assert.Multiple(() =>
		{
			Assert.That(
				state.GetCardZone(top.Id).ZoneType,
				Is.EqualTo(ZoneType.Exile),
				"The card should have left the library for exile"
			);
			Assert.That(
				exiled.HasComponent<ExiledPlayableComponent>(),
				Is.True,
				"Without the marker nothing can distinguish it from an ordinary exiled card"
			);
			Assert.That(
				state.IsInCastableZone(top.Id, _ids.Player1Id),
				Is.True,
				"All three cast actions gate on this"
			);
		});

		// The generator's half of the same rule. The AI only ever plays what this returns, so a
		// card that validates but is never offered is still unplayable in practice.
		var offered = MtgActionGenerator
			.GetLegalActions(state, _ids.Player1Id)
			.OfType<CastCreatureAction>()
			.Any(a => a.CardId == top.Id);
		Assert.That(offered, Is.True, "The exiled card should be offered as a legal cast");

		var (afterCast, _) = state
			.AddAction(new CastCreatureAction { CardId = top.Id, CastingPlayerId = _ids.Player1Id })
			.ProcessAllActions();

		Assert.That(
			afterCast.GetCardZone(top.Id).ZoneType,
			Is.EqualTo(ZoneType.Battlefield),
			"Casting it from exile should put it onto the battlefield"
		);
	}

	/// <summary>
	/// "This turn" is the whole cost of the mechanic. If the marker never expired, impulse draw
	/// would be strictly better than drawing the card.
	/// </summary>
	[Test]
	public void ImpulseDraw_StopsBeingPlayableWhenTheTurnEnds()
	{
		var libraryId = _state.GetPlayerZoneId(_ids.Player1Id, ZoneType.Library);
		var (state, top) = _state.AddObject(
			MakeCreature("Expires", _ids.Player1Id, 2, 2, manaCost: 1),
			parentId: libraryId
		);

		state = Impulse(state);
		Assert.That(state.IsInCastableZone(top.Id, _ids.Player1Id), Is.True, "playable this turn");

		(state, _) = state
			.AddAction(
				new EndTurnAction
				{
					GameId = _ids.GameId,
					Player1Id = _ids.Player1Id,
					Player2Id = _ids.Player2Id,
				}
			)
			.ProcessAllActions();

		Assert.Multiple(() =>
		{
			Assert.That(
				((Card)state.GetObject(top.Id)).HasComponent<ExiledPlayableComponent>(),
				Is.False,
				"The marker must be stripped when the exiling player's turn ends"
			);
			Assert.That(
				state.IsInCastableZone(top.Id, _ids.Player1Id),
				Is.False,
				"It is now an ordinary exiled card"
			);
			Assert.That(
				state.GetCardZone(top.Id).ZoneType,
				Is.EqualTo(ZoneType.Exile),
				"Expiring does not move it — it stays exiled forever"
			);
		});
	}

	/// <summary>
	/// The marker is not a general "cast from exile" licence: it must not make every card in
	/// exile playable, or one impulse draw would unlock a graveyard-hate pile's worth of cards.
	/// </summary>
	[Test]
	public void ImpulseDraw_DoesNotMakeOtherExiledCardsPlayable()
	{
		var exileId = _state.GetPlayerZoneId(_ids.Player1Id, ZoneType.Exile);
		var (state, alreadyExiled) = _state.AddObject(
			MakeCreature("Was Exiled Earlier", _ids.Player1Id, 9, 9, manaCost: 1),
			parentId: exileId
		);

		var libraryId = state.GetPlayerZoneId(_ids.Player1Id, ZoneType.Library);
		(state, _) = state.AddObject(
			MakeCreature("Impulse Target", _ids.Player1Id, 2, 2, manaCost: 1),
			parentId: libraryId
		);

		state = Impulse(state);

		Assert.That(
			state.IsInCastableZone(alreadyExiled.Id, _ids.Player1Id),
			Is.False,
			"An unmarked exiled card must stay unplayable"
		);
	}

	/// <summary>An empty library must not throw — it simply exiles nothing.</summary>
	[Test]
	public void ImpulseDraw_OnAnEmptyLibraryDoesNothing()
	{
		var libraryId = _state.GetPlayerZoneId(_ids.Player1Id, ZoneType.Library);
		Assert.That(
			_state.GetCardsInZone(libraryId).Count(),
			Is.Zero,
			"CreateForTesting starts with an empty library"
		);

		var state = Impulse(_state);
		var exileId = state.GetPlayerZoneId(_ids.Player1Id, ZoneType.Exile);
		Assert.That(state.GetCardsInZone(exileId), Is.Empty);
	}

	// ===== SPRAY DAMAGE =====

	/// <summary>
	/// "Deals N damage divided as you choose" is modelled as N independent 1-damage effects, each
	/// randomly targeted — Arcane Missiles. The point of the test is that all N land somewhere
	/// rather than the first one being the only one that resolves.
	/// </summary>
	[Test]
	public void SprayDamage_DealsEveryPointOfDamage()
	{
		var opponentBattlefieldId = _state.GetPlayerZoneId(_ids.Player2Id, ZoneType.Battlefield);
		var (state, wall) = _state.AddObject(
			MakeCreature("Soak", _ids.Player2Id, 1, 10, manaCost: 1),
			parentId: opponentBattlefieldId
		);

		var spray = CardFactory.Sorcery("Spray", manaCost: 2);
		for (var i = 0; i < 3; i++)
			spray.WithDamage(1).WithTarget(Random().OpponentCreatures());

		var (final, _) = CastSpell(state, spray.Build());

		var damage = ((Card)final.GetObject(wall.Id)).GetComponent<CreatureComponent>()!.Damage;
		Assert.That(
			damage,
			Is.EqualTo(3),
			"All three points should have landed on the only target"
		);
	}

	// ===== "WHENEVER THIS IS DEALT DAMAGE" =====

	/// <summary>
	/// CreatureDamagedEvent reached only the caller-visible Events list and never
	/// PendingGameEvents, so no "whenever this creature is dealt damage" trigger had ever fired.
	/// The fifth instance of that bug and the best disguised: the event already had an
	/// EventTypeNames constant, an ExtractSubjectId entry and TriggerAmountOf support, so every
	/// downstream piece was ready for a trigger that could never arrive.
	///
	/// Asserted on both damage paths, because they are separate call sites that each had it wrong.
	/// </summary>
	[Test]
	public void CreatureDamaged_FiresATrigger([Values("effect", "combat")] string source)
	{
		var battlefieldId = _state.GetPlayerZoneId(_ids.Player1Id, ZoneType.Battlefield);
		var reflector = MakeCreature("Reflector", _ids.Player1Id, 1, 10, manaCost: 5) with
		{
			Components = ImmutableArray.Create<GameComponent>(
				new PermanentComponent(),
				new CreatureComponent { Power = 1, Toughness = 10 },
				new TriggeredAbilityComponent
				{
					Name = "Spite",
					Condition = new EventTriggerCondition
					{
						EventTypeName = EventTypeNames.CreatureDamaged,
						Filter = new IsSourceCardSpecification(),
					},
					Effects = ImmutableList.Create(
						new CardEffect
						{
							TargetingStrategy = TargetingStrategy.AllValid(
								new IsPlayerSpecification().And(
									new IsControlledByOpponentSpecification()
								)
							),
							ActionTemplate = new DealDamageAction
							{
								AmountContextKey = ContextKeys.TriggerAmount,
							},
						}
					),
				}
			),
		};

		var (state, card) = _state.AddObject(reflector, parentId: battlefieldId);
		var startingLife = state.GetPlayer(_ids.Player2Id).Life;

		if (source == "effect")
		{
			(state, _) = state
				.AddAction(
					new DealDamageAction { Amount = 3, TargetIds = ImmutableList.Create(card.Id) }
				)
				.ProcessAllActions();
		}
		else
		{
			var opponentBattlefieldId = state.GetPlayerZoneId(_ids.Player2Id, ZoneType.Battlefield);
			// Player 2 swings a 3/3 into the reflector, which survives and reflects the 3.
			// Added straight to the battlefield rather than cast, so it never went through the ETB
			// ceremony that would clear summoning sickness — set it explicitly.
			var attackerCard = MakeCreature("Attacker", _ids.Player2Id, 3, 3, manaCost: 3);
			var (withAttacker, attacker) = state.AddObject(
				attackerCard with
				{
					Components = ImmutableArray.Create<GameComponent>(
						new PermanentComponent(),
						new CreatureComponent
						{
							Power = 3,
							Toughness = 3,
							HasSummoningSickness = false,
						}
					),
				},
				parentId: opponentBattlefieldId
			);
			(state, _) = withAttacker
				.AddAction(
					new AttackAction
					{
						AttackerId = attacker.Id,
						TargetId = card.Id,
						AttackingPlayerId = _ids.Player2Id,
					}
				)
				.ProcessAllActions();
		}

		Assert.That(
			state.GetPlayer(_ids.Player2Id).Life,
			Is.EqualTo(startingLife - 3),
			$"The {source} damage trigger should have reflected 3 damage"
		);
	}

	// ===== COMBAT DAMAGE FEEDS THE LIFE-LOSS PAYOFFS =====

	/// <summary>
	/// Being attacked is the most common way a player loses life, and combat damage did not touch
	/// MtgPlayer.LifeLostThisTurn at all — only LoseLifeAction, DrainLifeAction and effect damage
	/// did. So every payoff that reads it was blind to attacks: bloodthirst (Stormblood Berserker,
	/// Duskhunter Bat), Chandra's Phoenix's recursion, Knight of the Ebon Legion's end-step growth.
	/// They all worked when you burned the opponent and silently did nothing when you hit them.
	/// </summary>
	[Test]
	public void CombatDamage_CountsTowardLifeLostThisTurn()
	{
		var battlefieldId = _state.GetPlayerZoneId(_ids.Player1Id, ZoneType.Battlefield);
		var attackerCard = MakeCreature("Attacker", _ids.Player1Id, 3, 3, manaCost: 3);
		var (state, attacker) = _state.AddObject(
			attackerCard with
			{
				Components = ImmutableArray.Create<GameComponent>(
					new PermanentComponent(),
					new CreatureComponent
					{
						Power = 3,
						Toughness = 3,
						HasSummoningSickness = false,
					}
				),
			},
			parentId: battlefieldId
		);

		Assert.That(state.GetPlayer(_ids.Player2Id).LifeLostThisTurn, Is.Zero, "nothing yet");

		(state, _) = state
			.AddAction(
				new AttackAction
				{
					AttackerId = attacker.Id,
					TargetId = _ids.Player2Id,
					AttackingPlayerId = _ids.Player1Id,
				}
			)
			.ProcessAllActions();

		Assert.That(
			state.GetPlayer(_ids.Player2Id).LifeLostThisTurn,
			Is.EqualTo(3),
			"a 3-power attack is 3 life lost this turn"
		);
	}

	// ===== GOBLIN COUNTING =====

	/// <summary>
	/// Goblin Piledriver's "+2/+0 for each other Goblin you control", live-evaluated: a token
	/// entering must be seen immediately, with no re-stamp. Also pins that it counts OTHER
	/// Goblins — counting itself would make a lone Piledriver a 3/2.
	/// </summary>
	[Test]
	public void GoblinPiledriver_ScalesWithOtherGoblinsOnly()
	{
		var battlefieldId = _state.GetPlayerZoneId(_ids.Player1Id, ZoneType.Battlefield);
		var (state, piledriver) = _state.AddObject(
			CoresetCubeRed.Cards.Single(c => c.Name == "Goblin Piledriver") with
			{
				OwnerId = _ids.Player1Id,
				ControllerId = _ids.Player1Id,
			},
			parentId: battlefieldId
		);

		Assert.That(
			state.GetEffectivePower(piledriver.Id),
			Is.EqualTo(1),
			"Alone it is a 1/2 — it must not count itself"
		);

		for (var i = 0; i < 2; i++)
			(state, _) = state.AddObject(
				CoresetCubeRedTokens.Goblin() with
				{
					OwnerId = _ids.Player1Id,
					ControllerId = _ids.Player1Id,
				},
				parentId: battlefieldId
			);

		Assert.That(
			state.GetEffectivePower(piledriver.Id),
			Is.EqualTo(5),
			"1 base + 2 per other Goblin, seen live with no re-stamp"
		);
	}

	// ===== DAMAGE TO PLANESWALKERS =====

	/// <summary>
	/// Burn must reduce loyalty. Before red, DealDamageAction's switch had arms for players and
	/// for creatures only, so a planeswalker fell through to the default and took NOTHING — no
	/// error, no event, the walker simply ignored every burn spell in the game.
	/// </summary>
	[Test]
	public void Burn_ReducesPlaneswalkerLoyalty()
	{
		var (state, walker) = AddWalker(_state, _ids.Player2Id, loyalty: 4);

		var (final, _) = state
			.AddAction(
				new DealDamageAction { Amount = 3, TargetIds = ImmutableList.Create(walker.Id) }
			)
			.ProcessAllActions();

		Assert.That(
			((Card)final.GetObject(walker.Id)).GetComponent<PlaneswalkerComponent>()!.Loyalty,
			Is.EqualTo(1)
		);
	}

	/// <summary>
	/// Lethal burn must actually kill it, via the same zero-loyalty state-based pass combat uses.
	/// </summary>
	[Test]
	public void Burn_KillsAPlaneswalkerWhoseLoyaltyReachesZero()
	{
		var (state, walker) = AddWalker(_state, _ids.Player2Id, loyalty: 3);

		var (final, _) = state
			.AddAction(
				new DealDamageAction { Amount = 5, TargetIds = ImmutableList.Create(walker.Id) }
			)
			.ProcessAllActions();

		Assert.That(final.GetCardZone(walker.Id).ZoneType, Is.EqualTo(ZoneType.Graveyard));
	}

	/// <summary>
	/// "Any target" has to OFFER the walker, not merely survive being handed one. The targeting
	/// helper every burn spell uses was built from players and creatures, and a walker is neither.
	/// </summary>
	[Test]
	public void AnyTarget_OffersAPlaneswalker()
	{
		var (state, walker) = AddWalker(_state, _ids.Player2Id, loyalty: 4);

		var valid = TargetingStrategy
			.SingleTarget(TargetSpecification.PlayersOrCreatures())
			.GetValidTargets(
				new TargetingContext { GameState = state, CastingPlayerId = _ids.Player1Id }
			);

		Assert.That(valid, Does.Contain(walker.Id));
	}

	// ===== DISCARD-A-LAND COST =====

	/// <summary>
	/// Magmatic Insight's cost. The filter must be enforced in Validate, not only in
	/// GetValidPayments — the generator reads the latter, but the human UI path builds an action
	/// by hand and reaches Validate directly.
	/// </summary>
	[Test]
	public void DiscardLandCost_AcceptsALandAndRejectsANonLand()
	{
		var handId = _state.GetPlayerZoneId(_ids.Player1Id, ZoneType.Hand);
		var (state, land) = _state.AddObject(
			MakeCard("Mountain", _ids.Player1Id, "Land"),
			parentId: handId
		);
		(state, var spell) = state.AddObject(
			MakeCard("Not A Land", _ids.Player1Id),
			parentId: handId
		);

		var cost = new DiscardAdditionalCost
		{
			Count = 1,
			Filter = new IsSubtypeSpecification { Subtype = "Land" },
		};

		Assert.Multiple(() =>
		{
			Assert.That(
				cost.GetValidPayments(state, _ids.Player1Id, 0),
				Is.EquivalentTo(new[] { land.Id }),
				"Only the land should be offered"
			);
			Assert.That(
				cost.Validate(state, _ids.Player1Id, 0, ImmutableList.Create(land.Id)).IsValid,
				Is.True
			);
			Assert.That(
				cost.Validate(state, _ids.Player1Id, 0, ImmutableList.Create(spell.Id)).IsValid,
				Is.False,
				"A non-land must not be able to pay a discard-a-land cost"
			);
		});
	}

	// ===== FLYING SPECIFICATION =====

	/// <summary>Earthquake's "each creature without flying".</summary>
	[Test]
	public void FlyingSpecification_SeparatesGroundFromAir()
	{
		var battlefieldId = _state.GetPlayerZoneId(_ids.Player1Id, ZoneType.Battlefield);
		var (state, ground) = _state.AddObject(
			MakeCreature("Ground", _ids.Player1Id, 2, 2, manaCost: 2),
			parentId: battlefieldId
		);

		var flyerCard = MakeCreature("Flyer", _ids.Player1Id, 2, 2, manaCost: 2);
		(state, var flyer) = state.AddObject(
			flyerCard with
			{
				Components = ImmutableArray.Create<GameComponent>(
					new PermanentComponent(),
					new CreatureComponent
					{
						Power = 2,
						Toughness = 2,
						HasFlying = true,
					}
				),
			},
			parentId: battlefieldId
		);

		var context = new TargetingContext
		{
			GameState = state,
			CastingPlayerId = _ids.Player1Id,
			IsNonTargeted = true,
		};
		var withoutFlying = new HasFlyingSpecification().Not();

		Assert.Multiple(() =>
		{
			Assert.That(withoutFlying.IsSatisfiedBy(ground.Id, context), Is.True);
			Assert.That(withoutFlying.IsSatisfiedBy(flyer.Id, context), Is.False);
		});
	}

	// ===== CANNOT BE COUNTERED =====

	/// <summary>
	/// An uncounterable spell must resolve AND must not spend the opponent's trap — a counterspell
	/// that cannot counter its target was never a legal response to it, so it should still be in
	/// hand afterwards. Checking only "the spell resolved" would pass on an implementation that
	/// eats the counterspell for nothing.
	/// </summary>
	[Test]
	public void CannotBeCountered_ResolvesAndLeavesTheTrapInHand()
	{
		var trapHandId = _state.GetPlayerZoneId(_ids.Player2Id, ZoneType.Hand);
		var (state, trap) = _state.AddObject(
			CardFactory.Instant("Counterspell", manaCost: 2).AsCounterTrap().Build() with
			{
				OwnerId = _ids.Player2Id,
				ControllerId = _ids.Player2Id,
			},
			parentId: trapHandId
		);

		var startingLife = state.GetPlayer(_ids.Player2Id).Life;

		var bolt = CardFactory
			.Instant("Uncounterable Bolt", manaCost: 1)
			.WithCannotBeCountered()
			.WithDamage(3)
			.WithTarget(Single().Opponent())
			.Build();

		var (final, _) = CastSpell(state, bolt, targetId: _ids.Player2Id);

		Assert.Multiple(() =>
		{
			Assert.That(
				final.GetPlayer(_ids.Player2Id).Life,
				Is.EqualTo(startingLife - 3),
				"The spell should have resolved"
			);
			Assert.That(
				final.GetCardZone(trap.Id).ZoneType,
				Is.EqualTo(ZoneType.Hand),
				"The trap must not be spent on a spell it could never counter"
			);
		});
	}

	/// <summary>The counterpart: without the marker the same spell IS countered.</summary>
	[Test]
	public void WithoutTheMarker_TheSameSpellIsCountered()
	{
		var trapHandId = _state.GetPlayerZoneId(_ids.Player2Id, ZoneType.Hand);
		var (state, trap) = _state.AddObject(
			CardFactory.Instant("Counterspell", manaCost: 2).AsCounterTrap().Build() with
			{
				OwnerId = _ids.Player2Id,
				ControllerId = _ids.Player2Id,
			},
			parentId: trapHandId
		);

		var startingLife = state.GetPlayer(_ids.Player2Id).Life;

		var bolt = CardFactory
			.Instant("Counterable Bolt", manaCost: 1)
			.WithDamage(3)
			.WithTarget(Single().Opponent())
			.Build();

		var (final, _) = CastSpell(state, bolt, targetId: _ids.Player2Id);

		Assert.Multiple(() =>
		{
			Assert.That(
				final.GetPlayer(_ids.Player2Id).Life,
				Is.EqualTo(startingLife),
				"The spell should have been countered"
			);
			Assert.That(
				final.GetCardZone(trap.Id).ZoneType,
				Is.EqualTo(ZoneType.Graveyard),
				"The trap is spent when it does counter"
			);
		});
	}

	// ===== HELPERS =====

	private (GameState, Card) AddWalker(GameState state, int ownerId, int loyalty)
	{
		var battlefieldId = state.GetPlayerZoneId(ownerId, ZoneType.Battlefield);
		return state.AddObject(
			new Card
			{
				Name = "Test Walker",
				ManaCost = 4,
				OwnerId = ownerId,
				ControllerId = ownerId,
				Types = CardType.Planeswalker,
				Components = ImmutableArray.Create<GameComponent>(
					new PermanentComponent(),
					new PlaneswalkerComponent { StartingLoyalty = loyalty, Loyalty = loyalty }
				),
			},
			parentId: battlefieldId
		);
	}

	private static Card MakeCard(string name, int ownerId, string subtype = "") =>
		new()
		{
			Name = name,
			ManaCost = 1,
			OwnerId = ownerId,
			ControllerId = ownerId,
			Subtypes = string.IsNullOrEmpty(subtype)
				? ImmutableHashSet<string>.Empty
				: ImmutableHashSet.Create(StringComparer.OrdinalIgnoreCase, subtype),
		};

	/// <summary>Runs an impulse draw for player 1 and resolves everything it spawns.</summary>
	private GameState Impulse(GameState state)
	{
		var (final, _) = state
			.AddAction(new ExileTopCardPlayableAction { TargetIds = [_ids.Player1Id] })
			.ProcessAllActions();
		return final;
	}

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

	/// <summary>Puts the template in player 1's hand, casts it, and resolves everything.</summary>
	/// <param name="targetId">
	/// Optional target. Keyed by EFFECT INDEX, not by 0 — ValidateAdd looks targets up under the
	/// index of the effect that needs them, so keying them anywhere else makes the spell silently
	/// uncastable rather than throwing.
	/// </param>
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
			if (index >= 0)
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
