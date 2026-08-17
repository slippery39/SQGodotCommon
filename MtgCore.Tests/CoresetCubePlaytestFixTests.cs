using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;
using NUnit.Framework;

namespace MtgCore.Tests;

/// <summary>
/// One test per bug found in the first playtest of the Core Set Cube.
///
/// Every card here BUILT, cast and resolved without throwing — CoresetCubeWhiteTests and
/// CoresetCubeBlueTests all passed on them. They just did nothing. That is the gap these close:
/// the smoke tests assert a card reaches the battlefield, not that its effect happened.
///
/// These use the real set cards deliberately: the point is that THESE cards work now.
/// </summary>
[TestFixture]
public class CoresetCubePlaytestFixTests
{
	private GameState _state;
	private MtgGameIds _ids;

	[SetUp]
	public void Setup() => (_state, _ids) = MtgGameFactory.CreateForTesting();

	// ===== Reported: "Condemn did not work" / "Aetherspouts also didn't work" =====

	[Test]
	public void Condemn_PutsTheCreatureOnTheBottomOfItsLibrary()
	{
		var (s, victim) = AddCreature(_state, _ids.Player2Id);

		var final = CastTargeting(s, "Condemn", victim.Id);

		Assert.That(
			final.GetCardZone(victim.Id).ZoneType,
			Is.EqualTo(ZoneType.Library),
			"Condemn resolved against no cards at all — MoveCardToBottomOfLibraryAction cannot "
				+ "receive targets"
		);
	}

	[Test]
	public void Aetherspouts_PutsOpposingCreaturesOnTopOfTheirLibrary()
	{
		var (s1, a) = AddCreature(_state, _ids.Player2Id);
		var (s2, b) = AddCreature(s1, _ids.Player2Id);

		var final = CastNoTarget(s2, "Aetherspouts");

		Assert.That(final.GetCardZone(a.Id).ZoneType, Is.EqualTo(ZoneType.Library));
		Assert.That(final.GetCardZone(b.Id).ZoneType, Is.EqualTo(ZoneType.Library));
	}

	[Test]
	public void AnchorToTheAether_MovesTheCreature()
	{
		var (s, victim) = AddCreature(_state, _ids.Player2Id);

		var final = CastTargeting(s, "Anchor to the Aether", victim.Id);

		Assert.That(final.GetCardZone(victim.Id).ZoneType, Is.EqualTo(ZoneType.Library));
	}

	// ===== Reported: "Pegasus Courser's ability did not work" =====

	[Test]
	public void PegasusCourser_GrantsFlyingWhenItAttacks()
	{
		// The whole class of bug: a user-select strategy inside a trigger resolves to an empty
		// target list, so the effect silently does nothing.
		var s = PutOnBattlefield(_state, "Pegasus Courser", _ids.Player1Id);
		var (s2, ally) = AddCreature(s, _ids.Player1Id);
		var courser = FindOnBattlefield(s2, "Pegasus Courser", _ids.Player1Id);

		Assert.That(s2.GetEffectiveStats(ally.Id).HasFlying, Is.False, "Precondition");

		var (final, _) = s2.AddAction(
				new AttackAction
				{
					AttackerId = courser.Id,
					TargetId = _ids.Player2Id,
					AttackingPlayerId = _ids.Player1Id,
				}
			)
			.ProcessAllActions();

		Assert.That(
			final.GetEffectiveStats(ally.Id).HasFlying,
			Is.True,
			"The attack trigger granted flying to nobody"
		);
	}

	[Test]
	public void FrostLynx_FreezesSomethingWhenItEnters()
	{
		// Same class as Pegasus Courser — WithFreeze defaults to single-target, which is correct
		// for the spells that use it and dead inside a trigger.
		var (s, victim) = AddCreature(_state, _ids.Player2Id);

		var withLynx = PutOnBattlefield(s, "Frost Lynx", _ids.Player1Id);

		Assert.That(
			((Card)withLynx.GetObject(victim.Id)).GetComponent<CreatureComponent>()!.IsExhausted,
			Is.True
		);
	}

	[Test]
	public void NoTriggerInTheSet_UsesUserSelectTargeting()
	{
		// The guard that keeps the class closed. TriggerTargeting downgrades user-select to
		// Random at build time, so this can only fail if that is bypassed.
		var offenders = CoresetCube
			.Cards.SelectMany(c =>
				c.GetComponents<TriggeredAbilityComponent>()
					.SelectMany(t => t.Effects)
					.Where(e => e.TargetingStrategy.RequiresUserSelection)
					.Select(_ => c.Name)
			)
			.Distinct()
			.ToList();

		Assert.That(offenders, Is.Empty, string.Join(", ", offenders));
	}

	// ===== Reported: "Aether Tunnel / Pacifism came into play and did nothing" =====

	[Test]
	public void Aura_IsUncastableWithNothingToEnchant()
	{
		// It used to resolve onto the battlefield and sit there inert forever.
		var handId = _state.GetPlayerZoneId(_ids.Player1Id, ZoneType.Hand);
		var (withCard, card) = _state.AddObject(
			Template("Pacifism") with
			{
				OwnerId = _ids.Player1Id,
				ControllerId = _ids.Player1Id,
			},
			parentId: handId
		);

		var offered = MtgActionGenerator
			.GetLegalActions(withCard, _ids.Player1Id)
			.OfType<CastPermanentAction>()
			.Any(a => a.CardId == card.Id);

		Assert.That(offered, Is.False, "An Aura with no legal target must not be castable");
	}

	[Test]
	public void Pacifism_AttachesAndStopsTheCreatureAttacking()
	{
		var (s, victim) = AddCreature(_state, _ids.Player2Id);

		var final = CastAura(s, "Pacifism", victim.Id);

		Assert.That(
			final.GetEffectiveStats(victim.Id).CantAttack,
			Is.True,
			"Pacifism attached to nothing"
		);
	}

	[Test]
	public void AetherTunnel_AttachesAndBuffs()
	{
		var (s, ally) = AddCreature(_state, _ids.Player1Id);
		var before = s.GetEffectivePower(ally.Id);

		var final = CastAura(s, "Aether Tunnel", ally.Id);

		Assert.That(final.GetEffectivePower(ally.Id), Is.EqualTo(before + 1));
	}

	// ===== Reported: "Harbinger of Tides did not return my creature" =====

	[Test]
	public void HarbingerOfTheTides_BouncesAnOpposingCreature()
	{
		// It was gated on the target being exhausted. Attacking does not exhaust in this engine,
		// so the clause that is live in real Magic was almost never satisfiable here.
		var (s, victim) = AddCreature(_state, _ids.Player2Id);

		var final = PutOnBattlefield(s, "Harbinger of the Tides", _ids.Player1Id);

		Assert.That(final.GetCardZone(victim.Id).ZoneType, Is.EqualTo(ZoneType.Hand));
	}

	// ===== Reported: "Disenchant says destroy target permanent, but that's not what it does" ====

	[Test]
	public void Disenchant_HitsOnlyArtifactsAndEnchantments()
	{
		var (s, creature) = AddCreature(_state, _ids.Player2Id);

		var handId = s.GetPlayerZoneId(_ids.Player1Id, ZoneType.Hand);
		var (withCard, card) = s.AddObject(
			Template("Disenchant") with
			{
				OwnerId = _ids.Player1Id,
				ControllerId = _ids.Player1Id,
			},
			parentId: handId
		);

		var targets = MtgActionGenerator
			.GetLegalActions(withCard, _ids.Player1Id)
			.OfType<CastSpellAction>()
			.Where(a => a.CardId == card.Id)
			.SelectMany(a => a.TargetIds.Values.SelectMany(v => v))
			.ToList();

		Assert.That(
			targets,
			Does.Not.Contain(creature.Id),
			"Disenchant must not be able to target a creature"
		);
	}

	// ===== HELPERS =====

	private static Card Template(string name) =>
		CoresetCube.Cards.Single(c =>
			string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)
		);

	private GameState CastTargeting(GameState state, string cardName, int targetId)
	{
		var handId = state.GetPlayerZoneId(_ids.Player1Id, ZoneType.Hand);
		var (withCard, card) = state.AddObject(
			Template(cardName) with
			{
				OwnerId = _ids.Player1Id,
				ControllerId = _ids.Player1Id,
			},
			parentId: handId
		);

		var action = MtgActionGenerator
			.GetLegalActions(withCard, _ids.Player1Id)
			.OfType<CastSpellAction>()
			.First(a => a.CardId == card.Id && a.TargetIds.Values.Any(v => v.Contains(targetId)));

		var (final, _) = withCard.AddAction(action).ProcessAllActions();
		return final;
	}

	private GameState CastNoTarget(GameState state, string cardName)
	{
		var handId = state.GetPlayerZoneId(_ids.Player1Id, ZoneType.Hand);
		var (withCard, card) = state.AddObject(
			Template(cardName) with
			{
				OwnerId = _ids.Player1Id,
				ControllerId = _ids.Player1Id,
			},
			parentId: handId
		);

		var action = MtgActionGenerator
			.GetLegalActions(withCard, _ids.Player1Id)
			.OfType<CastSpellAction>()
			.First(a => a.CardId == card.Id);

		var (final, _) = withCard.AddAction(action).ProcessAllActions();
		return final;
	}

	private GameState CastAura(GameState state, string cardName, int targetId)
	{
		var handId = state.GetPlayerZoneId(_ids.Player1Id, ZoneType.Hand);
		var (withCard, card) = state.AddObject(
			Template(cardName) with
			{
				OwnerId = _ids.Player1Id,
				ControllerId = _ids.Player1Id,
			},
			parentId: handId
		);

		var action = MtgActionGenerator
			.GetLegalActions(withCard, _ids.Player1Id)
			.OfType<CastPermanentAction>()
			.First(a => a.CardId == card.Id && a.TargetIds.Contains(targetId));

		var (final, _) = withCard.AddAction(action).ProcessAllActions();
		return final;
	}

	/// <summary>Casts a creature from the set through the real pipeline so its ETB fires.</summary>
	private GameState PutOnBattlefield(GameState state, string cardName, int ownerId)
	{
		var (withCard, card) = state.AddObject(
			Template(cardName) with
			{
				OwnerId = ownerId,
				ControllerId = ownerId,
			},
			parentId: state.GetPlayerZoneId(ownerId, ZoneType.Hand)
		);

		var (resolved, _) = withCard
			.AddAction(new CastCreatureAction { CardId = card.Id, CastingPlayerId = ownerId })
			.ProcessAllActions();

		var onBoard = (Card)resolved.GetObject(card.Id);
		return resolved.UpdateObject(
			card.Id,
			onBoard.WithComponentReplaced(
				onBoard.GetComponent<CreatureComponent>()! with
				{
					HasSummoningSickness = false,
				}
			)
		);
	}

	private Card FindOnBattlefield(GameState state, string name, int ownerId) =>
		state
			.GetCardsInZone(state.GetPlayerZoneId(ownerId, ZoneType.Battlefield))
			.First(c => c.Name == name);

	private static (GameState, Card) AddCreature(GameState state, int ownerId)
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
				}
			),
		};

		return state.AddObject(
			card,
			parentId: state.GetPlayerZoneId(ownerId, ZoneType.Battlefield)
		);
	}
}
