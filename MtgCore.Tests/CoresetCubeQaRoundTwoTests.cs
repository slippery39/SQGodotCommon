using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;
using NUnit.Framework;

namespace MtgCore.Tests;

/// <summary>
/// Second QA round on the Core Set Cube. Same rule as CoresetCubePlaytestFixTests: one test per
/// bug actually seen in play, written before the fix.
///
/// The theme this round is a permanent LEAVING the battlefield by a route other than dying.
/// Bounce and "return to hand" never announced the departure at all, so everything the permanent
/// was doing to other cards stayed stamped on them forever.
/// </summary>
[TestFixture]
public class CoresetCubeQaRoundTwoTests
{
	private GameState _state;
	private MtgGameIds _ids;

	[SetUp]
	public void Setup() => (_state, _ids) = MtgGameFactory.CreateForTesting();

	// ===== Reported: "returning sensory deprivation did not give my creature back 3 power" =====

	[Test]
	public void BouncingAnAura_RemovesItsBoostFromTheCreature()
	{
		// Sensory Deprivation only enchants an opponent's creature.
		var (s, victim) = AddCreature(_state, _ids.Player2Id);
		var basePower = s.GetEffectivePower(victim.Id);

		var attached = CastAura(s, "Sensory Deprivation", victim.Id);
		var aura = FindOnBattlefield(attached, "Sensory Deprivation", _ids.Player1Id);
		Assert.That(
			attached.GetEffectivePower(victim.Id),
			Is.EqualTo(basePower - 3),
			"Precondition"
		);

		var (bounced, _) = attached
			.AddAction(new ReturnToHandAction { TargetIds = ImmutableList.Create(aura.Id) })
			.ProcessAllActions();

		Assert.That(
			bounced.GetEffectivePower(victim.Id),
			Is.EqualTo(basePower),
			"The Aura left the battlefield but its -3/-0 stayed stamped on the creature"
		);
	}

	[Test]
	public void DestroyingAnAura_RemovesItsBoostFromTheCreature()
	{
		var (s, victim) = AddCreature(_state, _ids.Player2Id);
		var basePower = s.GetEffectivePower(victim.Id);

		var attached = CastAura(s, "Sensory Deprivation", victim.Id);
		var aura = FindOnBattlefield(attached, "Sensory Deprivation", _ids.Player1Id);

		var (destroyed, _) = attached
			.AddAction(new DestroyPermanentAction { TargetIds = ImmutableList.Create(aura.Id) })
			.ProcessAllActions();

		Assert.That(destroyed.GetEffectivePower(victim.Id), Is.EqualTo(basePower));
	}

	// ===== Reported: "removing oblivion ring did not return my creature to play" =====

	[Test]
	public void DestroyingOblivionRing_ReturnsTheExiledPermanent()
	{
		var (s, victim) = AddCreature(_state, _ids.Player2Id);

		var withRing = CastPermanent(s, "Oblivion Ring");
		Assert.That(
			withRing.GetCardZone(victim.Id).ZoneType,
			Is.EqualTo(ZoneType.Exile),
			"Precondition: the Ring should have exiled the only opposing permanent"
		);

		var ring = FindOnBattlefield(withRing, "Oblivion Ring", _ids.Player1Id);
		var (destroyed, _) = withRing
			.AddAction(new DestroyPermanentAction { TargetIds = ImmutableList.Create(ring.Id) })
			.ProcessAllActions();

		Assert.That(
			destroyed.GetCardZone(victim.Id).ZoneType,
			Is.EqualTo(ZoneType.Battlefield),
			"The Ring left play and the exiled card never came back"
		);
	}

	[Test]
	public void BouncingOblivionRing_ReturnsTheExiledPermanent()
	{
		var (s, victim) = AddCreature(_state, _ids.Player2Id);

		var withRing = CastPermanent(s, "Oblivion Ring");
		var ring = FindOnBattlefield(withRing, "Oblivion Ring", _ids.Player1Id);

		var (bounced, _) = withRing
			.AddAction(new ReturnToHandAction { TargetIds = ImmutableList.Create(ring.Id) })
			.ProcessAllActions();

		Assert.That(
			bounced.GetCardZone(victim.Id).ZoneType,
			Is.EqualTo(ZoneType.Battlefield),
			"A bounced Ring never announced leaving, so the release trigger never fired"
		);
	}

	// ===== Reported: "creatures with negative power actually heal my opponent" =====

	[Test]
	public void AttackingWithNegativePower_DealsNoDamageToThePlayer()
	{
		var (s, attacker) = AddCreature(_state, _ids.Player1Id);
		var weakened = Weaken(s, attacker.Id, -5);
		Assert.That(weakened.GetEffectivePower(attacker.Id), Is.LessThan(0), "Precondition");

		var lifeBefore = weakened.GetPlayer(_ids.Player2Id).Life;

		var (final, _) = weakened
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
			final.GetPlayer(_ids.Player2Id).Life,
			Is.EqualTo(lifeBefore),
			"Negative power healed the defending player"
		);
	}

	[Test]
	public void AttackingWithNegativePower_DoesNotHealTheDefendingCreature()
	{
		var (s1, attacker) = AddCreature(_state, _ids.Player1Id);
		var (s2, blocker) = AddCreature(s1, _ids.Player2Id);

		// The defender is already damaged; negative attacker power must not heal that off.
		var damaged = s2.UpdateObject(
			blocker.Id,
			blocker.WithComponentReplaced(
				blocker.GetComponent<CreatureComponent>()! with
				{
					Damage = 1,
				}
			)
		);
		var weakened = Weaken(damaged, attacker.Id, -5);

		var (final, _) = weakened
			.AddAction(
				new AttackAction
				{
					AttackerId = attacker.Id,
					TargetId = blocker.Id,
					AttackingPlayerId = _ids.Player1Id,
				}
			)
			.ProcessAllActions();

		var after = (Card)final.GetObject(blocker.Id);
		Assert.That(after.GetComponent<CreatureComponent>()!.Damage, Is.EqualTo(1));
	}

	[Test]
	public void NegativePowerLifelink_GainsNoLife()
	{
		var (s, attacker) = AddCreature(_state, _ids.Player1Id, lifelink: true);
		var weakened = Weaken(s, attacker.Id, -5);
		var lifeBefore = weakened.GetPlayer(_ids.Player1Id).Life;

		var (final, _) = weakened
			.AddAction(
				new AttackAction
				{
					AttackerId = attacker.Id,
					TargetId = _ids.Player2Id,
					AttackingPlayerId = _ids.Player1Id,
				}
			)
			.ProcessAllActions();

		Assert.That(final.GetPlayer(_ids.Player1Id).Life, Is.EqualTo(lifeBefore));
	}

	// ===== HELPERS =====

	private static Card Template(string name) =>
		CoresetCube.Cards.Single(c =>
			string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)
		);

	private static GameState Weaken(GameState state, int cardId, int powerBonus)
	{
		var card = (Card)state.GetObject(cardId);
		return state.UpdateObject(
			cardId,
			card with
			{
				Components = card.Components.Add(
					new StaticPowerToughnessModifier
					{
						PowerBonus = powerBonus,
						ToughnessBonus = 0,
						Duration = ModifierDuration.Permanent,
					}
				),
			}
		);
	}

	private GameState CastAura(GameState state, string cardName, int targetId)
	{
		var (withCard, card) = state.AddObject(
			Template(cardName) with
			{
				OwnerId = _ids.Player1Id,
				ControllerId = _ids.Player1Id,
			},
			parentId: state.GetPlayerZoneId(_ids.Player1Id, ZoneType.Hand)
		);

		var action = MtgActionGenerator
			.GetLegalActions(withCard, _ids.Player1Id)
			.OfType<CastPermanentAction>()
			.First(a => a.CardId == card.Id && a.TargetIds.Contains(targetId));

		var (final, _) = withCard.AddAction(action).ProcessAllActions();
		return final;
	}

	private GameState CastPermanent(GameState state, string cardName)
	{
		var (withCard, card) = state.AddObject(
			Template(cardName) with
			{
				OwnerId = _ids.Player1Id,
				ControllerId = _ids.Player1Id,
			},
			parentId: state.GetPlayerZoneId(_ids.Player1Id, ZoneType.Hand)
		);

		var (final, _) = withCard
			.AddAction(
				new CastPermanentAction { CardId = card.Id, CastingPlayerId = _ids.Player1Id }
			)
			.ProcessAllActions();
		return final;
	}

	private Card FindOnBattlefield(GameState state, string name, int ownerId) =>
		state
			.GetCardsInZone(state.GetPlayerZoneId(ownerId, ZoneType.Battlefield))
			.First(c => c.Name == name);

	private static (GameState, Card) AddCreature(
		GameState state,
		int ownerId,
		bool lifelink = false
	)
	{
		var card = new Card
		{
			Name = "Test Bear",
			ManaCost = 2,
			OwnerId = ownerId,
			ControllerId = ownerId,
			Types = CardType.Creature,
			Components = ImmutableArray.Create<GameComponent>(
				new PermanentComponent(),
				new CreatureComponent
				{
					Power = 2,
					Toughness = 2,
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
