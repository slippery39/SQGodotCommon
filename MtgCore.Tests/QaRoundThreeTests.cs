using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;
using MtgCore.Cards.Builders;
using NUnit.Framework;
using static MtgCore.Cards.Builders.TargetBuilder;

namespace MtgCore.Tests;

/// <summary>
/// One test per bug found in the third QA round.
///
/// The theme this round is a permanent carrying battlefield state OFF the battlefield. Applied
/// boosts, granted keywords and combat tricks are all stamped onto the card as components, and
/// nothing removed them when the card left play — so a bounced creature kept its anthem bonus in
/// hand and collected a second one when it was replayed, and a dead creature sat in the graveyard
/// still advertising a -4/-4 that had long since expired.
/// </summary>
[TestFixture]
public class QaRoundThreeTests
{
	private GameState _state;
	private MtgGameIds _ids;

	[SetUp]
	public void Setup()
	{
		(_state, _ids) = MtgGameFactory.CreateForTesting();
	}

	/// <summary>
	/// Reported: Glorious Anthem + Knight of Glory, Knight bounced and replayed, Knight came back
	/// with TWO anthem bonuses.
	///
	/// StaticAbilityEngine.ProcessPermanentLeft removed the departing card from each source's
	/// AffectedIds index but never stripped the AppliedStaticPTBoost from the card itself. The
	/// card carried the boost into hand, and re-entering the battlefield stamped a second one.
	/// </summary>
	[Test]
	public void BouncedCreature_DoesNotKeepItsAnthemBonus()
	{
		var (stamped, knightId) = AnthemAndKnightInPlay();

		Assert.That(
			stamped.GetEffectivePower(knightId),
			Is.EqualTo(3),
			"Precondition: the anthem should be giving +1/+1"
		);

		var (bounced, _) = stamped
			.AddAction(new ReturnToHandAction { TargetIds = ImmutableList.Create(knightId) })
			.ProcessAllActions();

		Assert.That(
			((Card)bounced.GetObject(knightId)).GetComponents<AppliedStaticPTBoost>(),
			Is.Empty,
			"A creature in hand must not still be carrying a battlefield anthem's bonus"
		);
	}

	/// <summary>
	/// The consequence a player actually sees: replay the bounced creature and it is bigger than
	/// the anthem says it should be.
	/// </summary>
	[Test]
	public void ReplayedCreature_GetsExactlyOneAnthemBonus()
	{
		var (stamped, knightId) = AnthemAndKnightInPlay();

		var (bounced, _) = stamped
			.AddAction(new ReturnToHandAction { TargetIds = ImmutableList.Create(knightId) })
			.ProcessAllActions();

		var (replayed, _) = bounced
			.AddAction(new PutIntoBattlefieldAction { TargetIds = ImmutableList.Create(knightId) })
			.ProcessAllActions();

		Assert.That(
			replayed.GetEffectivePower(knightId),
			Is.EqualTo(3),
			"One anthem must never give +2/+2, however many times the creature has been replayed"
		);
	}

	/// <summary>
	/// Reported: a creature given -4/-4 until end of turn died, and its card in the graveyard
	/// still showed the -4/-4 in its text box.
	///
	/// Marked damage was already cleared on a zone change for exactly this reason; the modifiers
	/// that produced the death were not.
	/// </summary>
	[Test]
	public void DeadCreature_DoesNotCarryItsCombatTrickToTheGraveyard()
	{
		var (state, bear) = _state.AddObject(
			Bear("Victim", _ids.Player1Id, 4, 4),
			parentId: _ids.Player1BattlefieldId
		);

		var (weakened, _) = state
			.AddAction(
				new AddModifierAction
				{
					PowerBonus = -4,
					ToughnessBonus = -4,
					Duration = ModifierDuration.UntilEndOfTurn,
					TargetIds = ImmutableList.Create(bear.Id),
				}
			)
			.ProcessAllActions();

		Assert.That(
			weakened.GetCardZone(bear.Id).ZoneType,
			Is.EqualTo(ZoneType.Graveyard),
			"Precondition: -4/-4 on a 4/4 should have killed it"
		);

		Assert.That(
			((Card)weakened.GetObject(bear.Id)).GetComponents<StaticPowerToughnessModifier>(),
			Is.Empty,
			"An expired combat trick must not follow the card into the graveyard"
		);
	}

	/// <summary>
	/// Reported: Teferi's Tutelage never milled the opponent.
	///
	/// DrawCardsAction added CardDrawnEvent to ActionResult.Events — the caller-visible log — and
	/// never to PendingGameEvents, which is the trigger feed. So NO "whenever you draw a card"
	/// trigger in the engine had ever fired. This is the fourth time this exact bug has been
	/// found; see the "Events That Must Reach PendingGameEvents" note in CLAUDE.md.
	/// </summary>
	[Test]
	public void DrawTriggers_Fire()
	{
		var state = StockLibrary(_state, _ids.Player1Id);

		var payoff = CardFactory
			.Enchantment("Test Tutelage", manaCost: 3)
			.WithTriggeredAbility(
				"Tutelage",
				TriggerConditions.OnYouDraw(),
				eb => eb.WithMill(2).WithTarget(Single().Opponent())
			)
			.Build() with
		{
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
		};

		(state, _) = state.AddObject(payoff, parentId: _ids.Player1BattlefieldId);
		state = StockLibrary(state, _ids.Player2Id);

		var before = state
			.GetCardsInZone(state.GetPlayerZoneId(_ids.Player2Id, ZoneType.Graveyard))
			.Count();

		var (final, _) = state
			.AddAction(
				new DrawCardsAction { Amount = 1, TargetIds = ImmutableList.Create(_ids.Player1Id) }
			)
			.ProcessAllActions();

		var after = final
			.GetCardsInZone(final.GetPlayerZoneId(_ids.Player2Id, ZoneType.Graveyard))
			.Count();

		Assert.That(
			after - before,
			Is.EqualTo(2),
			"Drawing a card should have milled the opponent 2"
		);
	}

	/// <summary>
	/// Reported: Wall of Frost froze things it had nothing to do with.
	///
	/// It listened for ANY CreatureAttackedEvent and froze EVERY creature an opponent controlled,
	/// because CreatureAttackedEvent carried only the attacker and there was no way to ask "did
	/// this attack ME". A three-mana one-sided Frost Breath every turn.
	/// </summary>
	[Test]
	public void WallOfFrost_FreezesOnlyTheCreatureThatAttackedIt()
	{
		var wall = CoresetCube.Cards.Single(c => c.Name == "Wall of Frost") with
		{
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
		};
		var (state, wallCard) = _state.AddObject(wall, parentId: _ids.Player1BattlefieldId);

		var (withAttacker, attacker) = state.AddObject(
			Ready("Attacker", _ids.Player2Id, 2, 2),
			parentId: _ids.Player2BattlefieldId
		);
		var (board, bystander) = withAttacker.AddObject(
			Ready("Bystander", _ids.Player2Id, 2, 2),
			parentId: _ids.Player2BattlefieldId
		);

		var (final, _) = board
			.AddAction(
				new AttackAction
				{
					AttackerId = attacker.Id,
					TargetId = wallCard.Id,
					AttackingPlayerId = _ids.Player2Id,
				}
			)
			.ProcessAllActions();

		Assert.Multiple(() =>
		{
			Assert.That(
				((Card)final.GetObject(attacker.Id)).GetComponent<CreatureComponent>()!.IsExhausted,
				Is.True,
				"The creature that attacked the wall should be frozen"
			);
			Assert.That(
				((Card)final.GetObject(bystander.Id))
					.GetComponent<CreatureComponent>()!
					.IsExhausted,
				Is.False,
				"A creature that did not attack the wall must be untouched"
			);
		});
	}

	/// <summary>
	/// Reported: Despoiler of Souls could not be cast from the graveyard.
	///
	/// Its recursion costs "exile two other CREATURE cards from your graveyard", so the graveyard
	/// has to hold two creatures besides Despoiler itself for the action to be offered at all.
	/// </summary>
	[Test]
	public void DespoilerOfSouls_IsOfferedWhenTwoOtherCreaturesAreInTheGraveyard()
	{
		var graveyardId = _state.GetPlayerZoneId(_ids.Player1Id, ZoneType.Graveyard);
		var despoiler = CoresetCube.Cards.Single(c => c.Name == "Despoiler of Souls") with
		{
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
		};

		var (state, card) = _state.AddObject(despoiler, parentId: graveyardId);
		for (var i = 0; i < 2; i++)
			(state, _) = state.AddObject(
				Bear($"Fodder {i}", _ids.Player1Id, 1, 1),
				parentId: graveyardId
			);

		var legal = MtgActionGenerator
			.GetLegalActions(state, _ids, _ids.Player1Id)
			.OfType<CastFromGraveyardAction>()
			.FirstOrDefault(a => a.CardId == card.Id);

		Assert.That(legal, Is.Not.Null, "Despoiler should be castable from the graveyard");

		var (final, _) = state.AddAction(legal!).ProcessAllActions();

		Assert.That(
			final.GetCardZone(card.Id).ZoneType,
			Is.EqualTo(ZoneType.Battlefield),
			"and should actually come back"
		);
	}

	/// <summary>
	/// Reported: a creature was bounced to hand and then "for some reason went to the graveyard".
	///
	/// The bounced creature kept the -X/-X that had been put on it in play. Replaying it put a
	/// creature with negative toughness onto the battlefield, and the zero-toughness state-based
	/// check killed it the instant it arrived — so the card appeared to go hand, then graveyard,
	/// with nothing in between to explain it.
	/// </summary>
	[Test]
	public void BouncedAndReplayedCreature_DoesNotDieToAnExpiredDebuff()
	{
		var (state, bear) = _state.AddObject(
			Bear("Knight", _ids.Player1Id, 2, 2),
			parentId: _ids.Player1BattlefieldId
		);

		var (weakened, _) = state
			.AddAction(
				new AddModifierAction
				{
					PowerBonus = -1,
					ToughnessBonus = -1,
					Duration = ModifierDuration.UntilEndOfTurn,
					TargetIds = ImmutableList.Create(bear.Id),
				}
			)
			.ProcessAllActions();

		var (bounced, _) = weakened
			.AddAction(new ReturnToHandAction { TargetIds = ImmutableList.Create(bear.Id) })
			.ProcessAllActions();

		Assert.That(
			bounced.GetCardZone(bear.Id).ZoneType,
			Is.EqualTo(ZoneType.Hand),
			"Precondition: the bounce should have put it in hand"
		);

		var (replayed, _) = bounced
			.AddAction(new PutIntoBattlefieldAction { TargetIds = ImmutableList.Create(bear.Id) })
			.ProcessAllActions();

		Assert.That(
			replayed.GetCardZone(bear.Id).ZoneType,
			Is.EqualTo(ZoneType.Battlefield),
			"A replayed creature must not immediately die to a debuff that expired when it left"
		);
	}

	/// <summary>
	/// The sharpest risk in stripping applied components on leaving the battlefield: an Aura must
	/// not lose the ability to buff when it is bounced and recast.
	///
	/// It does not, because the Aura's boost is a TEMPLATE held inside its EquipmentComponent
	/// rather than a top-level component, and the strip only filters top-level components. This
	/// test exists so that distinction cannot be quietly refactored away.
	/// </summary>
	[Test]
	public void BouncedAura_StillWorksWhenRecast()
	{
		var aura = CoresetCube.Cards.Single(c => c.Name == "Mark of the Vampire") with
		{
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
		};

		var handId = _state.GetPlayerZoneId(_ids.Player1Id, ZoneType.Hand);
		var (state, auraCard) = _state.AddObject(aura, parentId: handId);
		var (withBear, bear) = state.AddObject(
			Ready("Host", _ids.Player1Id, 2, 2),
			parentId: _ids.Player1BattlefieldId
		);

		GameState Cast(GameState s) =>
			s.AddAction(
					new CastPermanentAction
					{
						CardId = auraCard.Id,
						CastingPlayerId = _ids.Player1Id,
						TargetIds = ImmutableList.Create(bear.Id),
					}
				)
				.ProcessAllActions()
				.State;

		var afterFirst = Cast(withBear);
		Assert.That(
			afterFirst.GetEffectivePower(bear.Id),
			Is.EqualTo(4),
			"Precondition: +2/+2 on a 2/2"
		);

		var (bounced, _) = afterFirst
			.AddAction(new ReturnToHandAction { TargetIds = ImmutableList.Create(auraCard.Id) })
			.ProcessAllActions();

		Assert.That(
			bounced.GetEffectivePower(bear.Id),
			Is.EqualTo(2),
			"Bouncing the Aura must take its bonus with it"
		);

		Assert.That(
			Cast(bounced).GetEffectivePower(bear.Id),
			Is.EqualTo(4),
			"and recasting it must buff again — the strip must not have eaten its template"
		);
	}

	// ===== HELPERS =====

	private static Card Ready(string name, int ownerId, int power, int toughness) =>
		new()
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
				}
			),
		};

	private static Card Anthem(int ownerId) =>
		new()
		{
			Name = "Glorious Anthem",
			ManaCost = 3,
			OwnerId = ownerId,
			ControllerId = ownerId,
			Types = CardType.Enchantment,
			Components = ImmutableArray.Create<GameComponent>(
				new PermanentComponent(),
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

	private static Card Bear(string name, int ownerId, int power, int toughness) =>
		new()
		{
			Name = name,
			ManaCost = 2,
			OwnerId = ownerId,
			ControllerId = ownerId,
			Types = CardType.Creature,
			Components = ImmutableArray.Create<GameComponent>(
				new PermanentComponent(),
				new CreatureComponent { Power = power, Toughness = toughness }
			),
		};

	/// <summary>
	/// Anthem on the battlefield and a 2/2 brought into play through PutIntoBattlefieldAction —
	/// the real path, so the ETB event fires and the push-model static engine actually stamps.
	/// Dropping a card straight into a zone would skip both.
	/// </summary>
	private (GameState State, int KnightId) AnthemAndKnightInPlay()
	{
		var handId = _state.GetPlayerZoneId(_ids.Player1Id, ZoneType.Hand);

		// Both cast for real. StaticAbilityEngine is a push model keyed off the entering event,
		// so an anthem dropped straight onto the battlefield is never REGISTERED as a source and
		// silently boosts nothing.
		var (state, anthem) = _state.AddObject(Anthem(_ids.Player1Id), parentId: handId);
		var (withKnight, knight) = state.AddObject(
			Bear("Knight", _ids.Player1Id, 2, 2),
			parentId: handId
		);

		var (anthemOut, _) = withKnight
			.AddAction(
				new CastPermanentAction { CardId = anthem.Id, CastingPlayerId = _ids.Player1Id }
			)
			.ProcessAllActions();

		var (inPlay, _) = anthemOut
			.AddAction(
				new CastCreatureAction { CardId = knight.Id, CastingPlayerId = _ids.Player1Id }
			)
			.ProcessAllActions();

		return (inPlay, knight.Id);
	}

	private static GameState StockLibrary(GameState state, int playerId)
	{
		var libraryId = state.GetPlayerZoneId(playerId, ZoneType.Library);
		for (var i = 0; i < 20; i++)
			(state, _) = state.AddObject(
				new Card
				{
					Name = $"Filler {playerId}-{i}",
					ManaCost = 1,
					OwnerId = playerId,
					ControllerId = playerId,
					Types = CardType.Sorcery,
					Components = ImmutableArray.Create<GameComponent>(new SpellComponent()),
				},
				parentId: libraryId
			);
		return state;
	}
}
