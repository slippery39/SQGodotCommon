using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;
using MtgCore.Cards.Builders;
using NUnit.Framework;
using static MtgCore.Cards.Builders.TargetBuilder;

namespace MtgCore.Tests;

/// <summary>
/// Loyalty, loyalty abilities, attacking a planeswalker, and death at 0 loyalty.
/// Cards are defined inline so card balance changes cannot break these.
/// </summary>
[TestFixture]
public class PlaneswalkerTests
{
	private GameState _state;
	private MtgGameIds _ids;

	[SetUp]
	public void Setup() => (_state, _ids) = MtgGameFactory.CreateForTesting();

	// ===== ENTERING PLAY =====

	[Test]
	public void Planeswalker_EntersAtStartingLoyalty_WhenCast()
	{
		var (state, card) = Cast(_state, MakeWalker());

		Assert.That(Loyalty(state, card.Id), Is.EqualTo(4));
		Assert.That(
			state.GetCardZone(card.Id).ZoneType,
			Is.EqualTo(ZoneType.Battlefield),
			"A planeswalker is a permanent and routes through CastPermanentAction"
		);
	}

	[Test]
	public void Planeswalker_IsNotACreature()
	{
		var walker = MakeWalker();

		Assert.That(walker.HasComponent<CreatureComponent>(), Is.False);
		Assert.That(walker.HasType(CardType.Planeswalker), Is.True);
		Assert.That(walker.HasType(CardType.Creature), Is.False);
	}

	// ===== LOYALTY ABILITIES =====

	[Test]
	public void PlusAbility_AddsLoyalty()
	{
		var (state, card) = Cast(_state, MakeWalker());

		var (after, _) = state.AddAction(Activate(card.Id, 0)).ProcessAllActions();

		Assert.That(Loyalty(after, card.Id), Is.EqualTo(5));
	}

	[Test]
	public void MinusAbility_SpendsLoyalty()
	{
		var (state, card) = Cast(_state, MakeWalker());

		var (after, _) = state.AddAction(Activate(card.Id, 1, _ids.Player2Id)).ProcessAllActions();

		Assert.That(Loyalty(after, card.Id), Is.EqualTo(1), "4 - 3 = 1");
		Assert.That(
			after.GetPlayer(_ids.Player2Id).Life,
			Is.EqualTo(17),
			"The -3 ability's effect should also resolve"
		);
	}

	[Test]
	public void MinusAbility_IsIllegalWithoutEnoughLoyalty()
	{
		var (state, card) = Cast(_state, MakeWalker());

		// Drop loyalty to 2, below the -3 ability's cost.
		var stamped = SetLoyalty(state, card.Id, 2);

		var (_, success) = stamped.TryAddAction(Activate(card.Id, 1));
		Assert.That(success, Is.False);
	}

	[Test]
	public void OnlyOneLoyaltyAbility_PerPlaneswalker_PerTurn()
	{
		// The limit is per WALKER, not per ability — using the +1 must lock out the -3 too.
		// Tracking it on ActivatedAbilityComponent.ActivationCount would allow both.
		var (state, card) = Cast(_state, MakeWalker());

		var (afterPlus, _) = state.AddAction(Activate(card.Id, 0)).ProcessAllActions();

		var (_, success) = afterPlus.TryAddAction(Activate(card.Id, 1));
		Assert.That(success, Is.False, "A different loyalty ability is still locked out");
	}

	[Test]
	public void LoyaltyAbility_BecomesAvailableAgainNextTurn()
	{
		var (state, card) = Cast(_state, MakeWalker());
		var (used, _) = state.AddAction(Activate(card.Id, 0)).ProcessAllActions();

		var (nextTurn, _) = used.AddAction(
				new StartTurnAction
				{
					ActivePlayerId = _ids.Player1Id,
					BattlefieldId = used.GetPlayerZoneId(_ids.Player1Id, ZoneType.Battlefield),
					SkipDraw = true,
				}
			)
			.ProcessAllActions();

		var (_, success) = nextTurn.TryAddAction(Activate(card.Id, 0));
		Assert.That(success, Is.True);
		Assert.That(
			Loyalty(nextTurn, card.Id),
			Is.EqualTo(5),
			"Loyalty itself must NOT reset — only the activation does"
		);
	}

	[Test]
	public void ZeroCostLoyaltyAbility_IsStillALoyaltyAbility()
	{
		// Inferring "is a loyalty ability" from a nonzero cost would treat Gideon Jura's
		// 0 ability as a normal free ability, usable alongside a +1 on the same turn.
		var (state, card) = Cast(_state, MakeWalker());

		var (afterZero, _) = state.AddAction(Activate(card.Id, 2)).ProcessAllActions();

		Assert.That(Loyalty(afterZero, card.Id), Is.EqualTo(4), "0 changes nothing");

		var (_, success) = afterZero.TryAddAction(Activate(card.Id, 0));
		Assert.That(success, Is.False, "But it still spends the turn's activation");
	}

	// ===== COMBAT =====

	[Test]
	public void Creature_CanAttackAnOpposingPlaneswalker()
	{
		var (withWalker, walker) = Cast(_state, MakeWalker(), _ids.Player2Id);
		var (state, attacker) = AddCreature(withWalker, "Bear", 2, 2, _ids.Player1Id);

		var (final, _) = state.AddAction(Attack(attacker.Id, walker.Id)).ProcessAllActions();

		Assert.That(Loyalty(final, walker.Id), Is.EqualTo(2), "4 loyalty - 2 power");
		Assert.That(
			final.GetPlayer(_ids.Player2Id).Life,
			Is.EqualTo(20),
			"Damage went to the walker, not the player"
		);
		Assert.That(
			((Card)final.GetObject(attacker.Id)).GetComponent<CreatureComponent>()!.Damage,
			Is.EqualTo(0),
			"A planeswalker deals no damage back"
		);
	}

	[Test]
	public void Planeswalker_DiesAtZeroLoyalty()
	{
		var (withWalker, walker) = Cast(_state, MakeWalker(), _ids.Player2Id);
		var (state, attacker) = AddCreature(withWalker, "Giant", 5, 5, _ids.Player1Id);

		var (final, _) = state.AddAction(Attack(attacker.Id, walker.Id)).ProcessAllActions();

		Assert.That(final.GetCardZone(walker.Id).ZoneType, Is.EqualTo(ZoneType.Graveyard));
	}

	[Test]
	public void PlaneswalkerDeath_DoesNotFireCreatureDeathTriggers()
	{
		// A walker is not a creature. Emitting CreatureDestroyedEvent would make every
		// "whenever a creature dies" payoff trigger off a planeswalker dying.
		var (withWalker, walker) = Cast(_state, MakeWalker(), _ids.Player2Id);
		var (state, attacker) = AddCreature(withWalker, "Giant", 5, 5, _ids.Player1Id);

		var (_, events) = state.AddAction(Attack(attacker.Id, walker.Id)).ProcessAllActions();

		Assert.That(
			events.OfType<CreatureDestroyedEvent>().Any(e => e.CreatureId == walker.Id),
			Is.False
		);
	}

	[Test]
	public void Lifelink_AppliesWhenAttackingAPlaneswalker()
	{
		var (withWalker, walker) = Cast(_state, MakeWalker(), _ids.Player2Id);
		var (state, attacker) = AddCreature(
			withWalker,
			"Lifelinker",
			2,
			2,
			_ids.Player1Id,
			lifelink: true
		);

		var (final, _) = state.AddAction(Attack(attacker.Id, walker.Id)).ProcessAllActions();

		Assert.That(final.GetPlayer(_ids.Player1Id).Life, Is.EqualTo(22));
	}

	[Test]
	public void ActionGenerator_OffersThePlaneswalkerAsATarget()
	{
		var (withWalker, walker) = Cast(_state, MakeWalker(), _ids.Player2Id);
		var (state, attacker) = AddCreature(withWalker, "Bear", 2, 2, _ids.Player1Id);

		var actions = MtgActionGenerator.GetLegalActions(state, _ids.Player1Id);

		Assert.That(
			actions.OfType<AttackAction>().Any(a => a.TargetId == walker.Id),
			Is.True,
			"A human UI and the AI both need the walker offered as an attack target"
		);
	}

	[Test]
	public void Reanimated_Planeswalker_ComesBackAtFullLoyalty()
	{
		var (withWalker, walker) = Cast(_state, MakeWalker(), _ids.Player1Id);
		var damaged = SetLoyalty(withWalker, walker.Id, 1);

		var graveyardId = damaged.GetPlayerZoneId(_ids.Player1Id, ZoneType.Graveyard);
		var inGraveyard = damaged.MoveCardTracked(walker.Id, graveyardId);

		var (final, _) = inGraveyard
			.AddAction(new PutIntoBattlefieldAction { TargetIds = ImmutableList.Create(walker.Id) })
			.ProcessAllActions();

		Assert.That(Loyalty(final, walker.Id), Is.EqualTo(4));
	}

	// ===== EMBLEMS =====

	[Test]
	public void Ultimate_CanGrantAnEmblem()
	{
		var emblem = new Emblem
		{
			Name = "Test Emblem",
			Condition = TriggerConditions.OnYourUpkeep(),
			Effect = new CardEffect
			{
				TargetingStrategy = TargetingStrategy.Self(),
				ActionTemplate = new GainLifeAction { Amount = 1 },
			},
		};

		var walker = CardFactory
			.Planeswalker("Ultimatum", manaCost: 3)
			.WithLoyalty(6)
			.WithLoyaltyAbility(
				"-6: Emblem",
				-6,
				eb =>
					eb.WithAction(
						new GrantEmblemAction { Emblem = emblem },
						TargetingStrategy.Self()
					)
			)
			.Build();

		var (state, card) = Cast(_state, walker);

		var (after, _) = state.AddAction(Activate(card.Id, 0)).ProcessAllActions();

		Assert.That(after.GetPlayer(_ids.Player1Id).Emblems, Has.Count.EqualTo(1));
		Assert.That(
			after.GetCardZone(card.Id).ZoneType,
			Is.EqualTo(ZoneType.Graveyard),
			"Spending all its loyalty kills the walker"
		);
	}

	/// <summary>
	/// A dig on a LOYALTY ability must actually put a card in hand — Vivien Reid's +1 is
	/// "look at the top four cards, take one", and QA reported it doing nothing.
	///
	/// Worth its own test because a loyalty ability reaches the effect through
	/// ActivateAbilityAction rather than a cast, and a dig pauses mid-pipeline on nothing while
	/// still depending on ContextKeys.CastingPlayerId being injected. Inline card, so rebalancing
	/// Vivien cannot delete the coverage.
	/// </summary>
	[Test]
	public void LoyaltyAbility_Dig_PutsACardIntoHand()
	{
		var walker = CardFactory
			.Planeswalker("Test Digger", manaCost: 5)
			.WithLoyalty(4)
			.WithLoyaltyAbility("+1: Look at the top four; take one", 1, eb => eb.WithDig(4))
			.Build();

		var (state, card) = Cast(_state, walker);

		// Four cards to dig through, all lands — the case the AllowLands filter used to swallow.
		for (var i = 0; i < 4; i++)
		{
			var (withLand, _) = state.AddObject(
				new Card
				{
					Name = $"Forest {i}",
					ManaCost = 0,
					OwnerId = _ids.Player1Id,
					ControllerId = _ids.Player1Id,
					Types = CardType.Land,
					Subtypes = ImmutableHashSet.Create(StringComparer.OrdinalIgnoreCase, "Land"),
				},
				parentId: state.GetPlayerZoneId(_ids.Player1Id, ZoneType.Library)
			);
			state = withLand;
		}

		var handId = state.GetPlayerZoneId(_ids.Player1Id, ZoneType.Hand);
		var before = state.GetCardsInZone(handId).Count();

		var (after, _) = state.AddAction(Activate(card.Id, 0)).ProcessAllActions();

		Assert.That(
			after.GetCardsInZone(handId).Count() - before,
			Is.EqualTo(1),
			"the +1 must take one of the revealed cards, land or not"
		);
	}

	/// <summary>
	/// "Creatures attack Gideon if able" — Taunt on a PLANESWALKER.
	///
	/// The walker was always a legal attack target; the Taunt scan just never looked at anything
	/// without a CreatureComponent, so the grant sat on him doing nothing and the card's whole
	/// defensive half was reskinned away on the belief that the engine had no forced-attack rule.
	/// It had one, aimed only at creatures.
	/// </summary>
	[Test]
	public void TauntingPlaneswalker_MustBeAttackedBeforeItsController()
	{
		var walker = CardFactory
			.Planeswalker("Test Wall", manaCost: 5)
			.WithLoyalty(6)
			.WithLoyaltyAbility(
				"+2: Taunt",
				2,
				eb =>
					eb.WithAction(
						new GrantKeywordAction
						{
							GrantsTaunt = true,
							Duration = ModifierDuration.UntilEndOfTurn,
							TargetContextKey = ContextKeys.SourceCardId,
						},
						TargetingStrategy.NoTarget()
					)
			)
			.Build();

		var (state, card) = Cast(_state, walker);

		// An attacker on the other side, ready to swing at the walker's controller.
		var (withAttacker, attacker) = state.AddObject(
			new Card
			{
				Name = "Raider",
				ManaCost = 2,
				OwnerId = _ids.Player2Id,
				ControllerId = _ids.Player2Id,
				Types = CardType.Creature,
				Components = ImmutableArray.Create<GameComponent>(
					new PermanentComponent(),
					new CreatureComponent
					{
						Power = 2,
						Toughness = 2,
						HasSummoningSickness = false,
					}
				),
			},
			parentId: state.GetPlayerZoneId(_ids.Player2Id, ZoneType.Battlefield)
		);

		var faceAttack = new AttackAction
		{
			AttackerId = attacker.Id,
			TargetId = _ids.Player1Id,
			AttackingPlayerId = _ids.Player2Id,
		};

		Assert.That(
			faceAttack.ValidateAdd(withAttacker).IsValid,
			Is.True,
			"precondition: with no Taunt out, going to the face is legal"
		);

		var (taunting, _) = withAttacker.AddAction(Activate(card.Id, 0)).ProcessAllActions();

		Assert.Multiple(() =>
		{
			Assert.That(
				faceAttack.ValidateAdd(taunting).IsValid,
				Is.False,
				"a taunting walker must be attacked before its controller"
			);
			Assert.That(
				new AttackAction
				{
					AttackerId = attacker.Id,
					TargetId = card.Id,
					AttackingPlayerId = _ids.Player2Id,
				}
					.ValidateAdd(taunting)
					.IsValid,
				Is.True,
				"and attacking the walker itself is the legal move"
			);
		});
	}

	/// <summary>
	/// A one-turn token is gone when the turn ends — otherwise "becomes a 6/6 until end of turn"
	/// is a permanent 6/6 and Gideon's 0 is the best card in the set.
	/// </summary>
	[Test]
	public void ExileAtEndOfTurnToken_DoesNotSurviveTheTurn()
	{
		var battlefieldId = _state.GetPlayerZoneId(_ids.Player1Id, ZoneType.Battlefield);
		var (state, token) = _state.AddObject(
			CoresetCubeTokens.GideonAvatar() with
			{
				OwnerId = _ids.Player1Id,
				ControllerId = _ids.Player1Id,
			},
			parentId: battlefieldId
		);

		Assert.That(
			state.GetEffectiveStats(token.Id).HasHaste,
			Is.True,
			"it has to be able to attack the turn it appears, or it does nothing at all"
		);

		var (after, _) = state
			.AddAction(
				new EndTurnAction
				{
					GameId = _ids.GameId,
					Player1Id = _ids.Player1Id,
					Player2Id = _ids.Player2Id,
				}
			)
			.ProcessAllActions();

		Assert.That(
			after.GetCardZone(token.Id).ZoneType,
			Is.EqualTo(ZoneType.Exile),
			"the token must be exiled when the turn ends"
		);
	}

	// ===== HELPERS =====

	/// <summary>
	/// +1: gain 1 life. -3: deal 3 damage to the opponent. 0: draw a card.
	/// Three abilities so the once-per-turn-per-walker rule has something to be wrong about.
	/// </summary>
	private static Card MakeWalker() =>
		CardFactory
			.Planeswalker("Test Walker", manaCost: 3)
			.WithLoyalty(4)
			.WithLoyaltyAbility("+1: Gain life", 1, eb => eb.WithLifeGain(1).NoTarget())
			.WithLoyaltyAbility(
				"-3: Damage",
				-3,
				eb => eb.WithDamage(3).WithTarget(Single().Opponent())
			)
			.WithLoyaltyAbility("0: Draw", 0, eb => eb.WithDraw(1).NoTarget())
			.Build();

	private ActivateAbilityAction Activate(int cardId, int abilityIndex, int targetId = 0) =>
		new()
		{
			CardId = cardId,
			ActivatingPlayerId = _ids.Player1Id,
			AbilityIndex = abilityIndex,
			TargetIds = targetId == 0 ? ImmutableList<int>.Empty : ImmutableList.Create(targetId),
		};

	private AttackAction Attack(int attackerId, int targetId) =>
		new()
		{
			AttackerId = attackerId,
			TargetId = targetId,
			AttackingPlayerId = _ids.Player1Id,
		};

	private static int Loyalty(GameState state, int cardId) =>
		((Card)state.GetObject(cardId)).GetComponent<PlaneswalkerComponent>()!.Loyalty;

	private static GameState SetLoyalty(GameState state, int cardId, int loyalty)
	{
		var card = (Card)state.GetObject(cardId);
		return state.UpdateObject(
			cardId,
			card.WithComponentReplaced(
				card.GetComponent<PlaneswalkerComponent>()! with
				{
					Loyalty = loyalty,
				}
			)
		);
	}

	/// <summary>Casts a permanent through the real pipeline so ETB and loyalty stamping run.</summary>
	private (GameState, Card) Cast(GameState state, Card template, int ownerId = 0)
	{
		ownerId = ownerId == 0 ? _ids.Player1Id : ownerId;

		var (withCard, card) = state.AddObject(
			template with
			{
				OwnerId = ownerId,
				ControllerId = ownerId,
			},
			parentId: state.GetPlayerZoneId(ownerId, ZoneType.Hand)
		);

		var (final, _) = withCard
			.AddAction(new CastPermanentAction { CardId = card.Id, CastingPlayerId = ownerId })
			.ProcessAllActions();

		return (final, card);
	}

	private static (GameState, Card) AddCreature(
		GameState state,
		string name,
		int power,
		int toughness,
		int ownerId,
		bool lifelink = false
	)
	{
		var card = new Card
		{
			Name = name,
			ManaCost = 2,
			OwnerId = ownerId,
			ControllerId = ownerId,
			Types = CardType.Creature,
			Components = ImmutableArray.Create<GameComponent>(
				new PermanentComponent(),
				new CreatureComponent
				{
					Power = power,
					Toughness = toughness,
					HasSummoningSickness = false,
					HasLifelink = lifelink,
				}
			),
		};

		return state.AddObject(
			card,
			parentId: state.GetPlayerZoneId(ownerId, ZoneType.Battlefield)
		);
	}
}
