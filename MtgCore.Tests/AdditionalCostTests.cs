using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;
using NUnit.Framework;

namespace MtgCore.Tests;

[TestFixture]
public class AdditionalCostTests
{
	private GameState _state;
	private MtgGameIds _ids;

	[SetUp]
	public void Setup()
	{
		(_state, _ids) = MtgGameFactory.CreateForTesting();
	}

	// ===== SACRIFICE COST =====

	[Test]
	public void SacrificeCost_CanCastSpell_WhenValidTargetExists()
	{
		var (stateWithGoblin, goblin) = _state.AddObject(
			MakeGoblin(_ids.Player1Id),
			parentId: _ids.Player1BattlefieldId
		);
		var (stateWithGrenade, grenadeId) = AddGrenadeToHand(stateWithGoblin, _ids.Player1Id);

		var (_, success) = stateWithGrenade.TryAddAction(
			MakeCastGrenadeAt(_ids.Player2Id, grenadeId, goblin.Id)
		);

		Assert.That(success, Is.True);
	}

	[Test]
	public void SacrificeCost_FailsIfNoValidSacrificeTarget()
	{
		var (stateWithGrenade, grenadeId) = AddGrenadeToHand(_state, _ids.Player1Id);

		var (_, success) = stateWithGrenade.TryAddAction(
			MakeCastGrenadeAt(_ids.Player2Id, grenadeId, sacrificeId: 9999)
		);

		Assert.That(success, Is.False);
	}

	[Test]
	public void SacrificeCost_FailsIfSacrificeTargetNotAGoblin()
	{
		var nonGoblin = new Card
		{
			Name = "Bear",
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
			Components = ImmutableList.Create<GameComponent>(
				new CreatureComponent { Power = 2, Toughness = 2 }
			),
		};
		var (stateWithBear, bear) = _state.AddObject(
			nonGoblin,
			parentId: _ids.Player1BattlefieldId
		);
		var (stateWithGrenade, grenadeId) = AddGrenadeToHand(stateWithBear, _ids.Player1Id);

		var (_, success) = stateWithGrenade.TryAddAction(
			MakeCastGrenadeAt(_ids.Player2Id, grenadeId, bear.Id)
		);

		Assert.That(success, Is.False);
	}

	[Test]
	public void SacrificeCost_SacrificeTargetMovesToGraveyard()
	{
		var (stateWithGoblin, goblin) = _state.AddObject(
			MakeGoblin(_ids.Player1Id),
			parentId: _ids.Player1BattlefieldId
		);
		var (stateWithGrenade, grenadeId) = AddGrenadeToHand(stateWithGoblin, _ids.Player1Id);

		var (finalState, _) = stateWithGrenade
			.AddAction(MakeCastGrenadeAt(_ids.Player2Id, grenadeId, goblin.Id))
			.ProcessAllActions();

		Assert.That(finalState.GetCardZone(goblin.Id).ZoneType, Is.EqualTo(ZoneType.Graveyard));
	}

	[Test]
	public void SacrificeCost_DoesNotDeductManaForSacrificeItself()
	{
		var (stateWithGoblin, goblin) = _state.AddObject(
			MakeGoblin(_ids.Player1Id),
			parentId: _ids.Player1BattlefieldId
		);
		var (stateWithGrenade, grenadeId) = AddGrenadeToHand(stateWithGoblin, _ids.Player1Id);
		var manaBefore = stateWithGrenade.GetPlayer(_ids.Player1Id).CurrentMana;

		var (finalState, _) = stateWithGrenade
			.AddAction(MakeCastGrenadeAt(_ids.Player2Id, grenadeId, goblin.Id))
			.ProcessAllActions();

		// Grenade costs 1 mana; sacrifice cost doesn't cost additional mana
		Assert.That(finalState.GetPlayer(_ids.Player1Id).CurrentMana, Is.EqualTo(manaBefore - 1));
	}

	[Test]
	public void SacrificeCost_SpellResolvesAndDealsExpectedDamage()
	{
		var (stateWithGoblin, goblin) = _state.AddObject(
			MakeGoblin(_ids.Player1Id),
			parentId: _ids.Player1BattlefieldId
		);
		var (stateWithGrenade, grenadeId) = AddGrenadeToHand(stateWithGoblin, _ids.Player1Id);
		var lifeBefore = stateWithGrenade.GetPlayer(_ids.Player2Id).Life;

		var (finalState, _) = stateWithGrenade
			.AddAction(MakeCastGrenadeAt(_ids.Player2Id, grenadeId, goblin.Id))
			.ProcessAllActions();

		Assert.That(finalState.GetPlayer(_ids.Player2Id).Life, Is.EqualTo(lifeBefore - 5));
	}

	// ===== DISCARD COST =====

	[Test]
	public void DiscardCost_CanActivateAbility_WhenCardsInHand()
	{
		var (stateWithMongrel, mongrelId) = AddWildMongrelToBattlefield(_state, _ids.Player1Id);
		var (stateWithCard, handCard) = stateWithMongrel.AddObject(
			TestCardFactory.MakeCreatureCard("Filler", _ids.Player1Id, 1, 1),
			parentId: _ids.Player1HandId
		);

		var (_, success) = stateWithCard.TryAddAction(MakeActivateMongrel(mongrelId, handCard.Id));

		Assert.That(success, Is.True);
	}

	[Test]
	public void DiscardCost_FailsIfHandIsEmpty()
	{
		var (stateWithMongrel, mongrelId) = AddWildMongrelToBattlefield(_state, _ids.Player1Id);

		var (_, success) = stateWithMongrel.TryAddAction(
			MakeActivateMongrel(mongrelId, discardId: 9999)
		);

		Assert.That(success, Is.False);
	}

	[Test]
	public void DiscardCost_DiscardedCardMovesToGraveyard()
	{
		var (stateWithMongrel, mongrelId) = AddWildMongrelToBattlefield(_state, _ids.Player1Id);
		var (stateWithCard, handCard) = stateWithMongrel.AddObject(
			TestCardFactory.MakeCreatureCard("Filler", _ids.Player1Id, 1, 1),
			parentId: _ids.Player1HandId
		);

		var (finalState, _) = stateWithCard
			.AddAction(MakeActivateMongrel(mongrelId, handCard.Id))
			.ProcessAllActions();

		Assert.That(finalState.GetCardZone(handCard.Id).ZoneType, Is.EqualTo(ZoneType.Graveyard));
	}

	// ===== LIFE COST =====

	[Test]
	public void LifeCost_ReducesCasterLife()
	{
		var (stateWithSpell, spellId) = AddLifeCostSpellToHand(_state, _ids.Player1Id, lifeCost: 3);
		var lifeBefore = stateWithSpell.GetPlayer(_ids.Player1Id).Life;

		var (finalState, _) = stateWithSpell
			.AddAction(new CastSpellAction { CardId = spellId, CastingPlayerId = _ids.Player1Id })
			.ProcessAllActions();

		Assert.That(finalState.GetPlayer(_ids.Player1Id).Life, Is.EqualTo(lifeBefore - 3));
	}

	[Test]
	public void LifeCost_FailsIfInsufficientLife()
	{
		var player = _state.GetPlayer(_ids.Player1Id);
		var stateWithLowLife = _state.UpdateObject(_ids.Player1Id, player with { Life = 2 });
		var (stateWithSpell, spellId) = AddLifeCostSpellToHand(
			stateWithLowLife,
			_ids.Player1Id,
			lifeCost: 3
		);

		var (_, success) = stateWithSpell.TryAddAction(
			new CastSpellAction { CardId = spellId, CastingPlayerId = _ids.Player1Id }
		);

		Assert.That(success, Is.False);
	}

	// ===== ACTION GENERATOR =====

	[Test]
	public void MtgActionGenerator_IncludesGrenadeAction_WhenGoblinAvailable()
	{
		var (stateWithGoblin, _) = _state.AddObject(
			MakeGoblin(_ids.Player1Id),
			parentId: _ids.Player1BattlefieldId
		);
		var (stateWithGrenade, _) = AddGrenadeToHand(stateWithGoblin, _ids.Player1Id);

		var legalActions = MtgActionGenerator.GetLegalActions(
			stateWithGrenade,
			_ids,
			_ids.Player1Id
		);

		Assert.That(legalActions.OfType<CastSpellAction>().Any(), Is.True);
	}

	[Test]
	public void MtgActionGenerator_ExcludesGrenadeAction_WhenNoGoblinAvailable()
	{
		var (stateWithGrenade, _) = AddGrenadeToHand(_state, _ids.Player1Id);

		var legalActions = MtgActionGenerator.GetLegalActions(
			stateWithGrenade,
			_ids,
			_ids.Player1Id
		);

		Assert.That(legalActions.OfType<CastSpellAction>().Any(), Is.False);
	}

	// ===== HELPERS =====

	private static Card MakeGoblin(int ownerId) =>
		new()
		{
			Name = "Goblin Token",
			OwnerId = ownerId,
			ControllerId = ownerId,
			Subtypes = ImmutableList.Create("Goblin"),
			Components = ImmutableList.Create<GameComponent>(
				new CreatureComponent { Power = 1, Toughness = 1 }
			),
		};

	private static Card MakeGrenadeCard(int ownerId) =>
		new()
		{
			Name = "Goblin Grenade",
			ManaCost = 1,
			OwnerId = ownerId,
			ControllerId = ownerId,
			AdditionalCastCosts = ImmutableList.Create<AdditionalCost>(
				new SacrificeAdditionalCost
				{
					Filter = new IsSubtypeSpecification { Subtype = "Goblin" },
					Count = 1,
				}
			),
			Components = ImmutableList.Create<GameComponent>(
				new SpellComponent
				{
					Effects = ImmutableList.Create(
						new CardEffect
						{
							TargetingStrategy = TargetingStrategy.SingleTarget(
								new IsPlayerSpecification()
							),
							ActionTemplate = new DealDamageAction { Amount = 5 },
						}
					),
				}
			),
		};

	// Wild Mongrel: 0-cost activated ability, discard a card as additional cost
	private static Card MakeWildMongrelCard(int ownerId) =>
		new()
		{
			Name = "Wild Mongrel",
			ManaCost = 2,
			OwnerId = ownerId,
			ControllerId = ownerId,
			Components = ImmutableList.Create<GameComponent>(
				new CreatureComponent { Power = 2, Toughness = 2 },
				new ActivatedAbilityComponent
				{
					Name = "Pump",
					ManaCost = 0,
					AdditionalCosts = ImmutableList.Create<AdditionalCost>(
						new DiscardAdditionalCost { Count = 1 }
					),
					Effect = new CardEffect
					{
						TargetingStrategy = TargetingStrategy.NoTarget(),
						ActionTemplate = new DrawCardsAction { PlayerId = ownerId, Amount = 1 },
					},
				}
			),
		};

	private (GameState, int spellId) AddGrenadeToHand(GameState state, int playerId)
	{
		var (newState, added) = state.AddObject(
			MakeGrenadeCard(playerId),
			parentId: _ids.Player1HandId
		);
		return (newState, added.Id);
	}

	private static (GameState, int cardId) AddWildMongrelToBattlefield(
		GameState state,
		int playerId
	)
	{
		var battlefieldId = state.GetPlayerZoneId(playerId, ZoneType.Battlefield);
		var (newState, added) = state.AddObject(
			MakeWildMongrelCard(playerId),
			parentId: battlefieldId
		);
		return (newState, added.Id);
	}

	private (GameState, int spellId) AddLifeCostSpellToHand(
		GameState state,
		int playerId,
		int lifeCost
	)
	{
		var spell = new Card
		{
			Name = "Life Cost Spell",
			ManaCost = 0,
			OwnerId = playerId,
			ControllerId = playerId,
			AdditionalCastCosts = ImmutableList.Create<AdditionalCost>(
				new LifeAdditionalCost { Amount = lifeCost }
			),
			Components = ImmutableList.Create<GameComponent>(
				new SpellComponent { Effects = ImmutableList<CardEffect>.Empty }
			),
		};
		var (newState, added) = state.AddObject(spell, parentId: _ids.Player1HandId);
		return (newState, added.Id);
	}

	private CastSpellAction MakeCastGrenadeAt(int targetId, int grenadeId, int sacrificeId) =>
		new()
		{
			CardId = grenadeId,
			CastingPlayerId = _ids.Player1Id,
			TargetIds = ImmutableDictionary<int, ImmutableList<int>>.Empty.Add(
				0,
				ImmutableList.Create(targetId)
			),
			AdditionalCostPayments = ImmutableDictionary<int, ImmutableList<int>>.Empty.Add(
				0,
				ImmutableList.Create(sacrificeId)
			),
		};

	private ActivateAbilityAction MakeActivateMongrel(int mongrelId, int discardId) =>
		new()
		{
			CardId = mongrelId,
			ActivatingPlayerId = _ids.Player1Id,
			AbilityIndex = 0,
			AdditionalCostPayments = ImmutableDictionary<int, ImmutableList<int>>.Empty.Add(
				0,
				ImmutableList.Create(discardId)
			),
		};
}
