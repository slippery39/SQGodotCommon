using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;
using MtgCore.Cards.Builders;
using NUnit.Framework;

namespace MtgCore.Tests;

[TestFixture]
public class AffinityTests
{
	private GameState _state;
	private MtgGameIds _ids;

	[SetUp]
	public void Setup()
	{
		(_state, _ids) = MtgGameFactory.CreateForTesting();
	}

	// ===== AFFINITY COST REDUCTION =====

	[Test]
	public void Affinity_ReducesCreatureCost_ByArtifactCount()
	{
		// 3 artifacts on battlefield — Frogmite (base 4) should cost 1
		var state = AddArtifactsToField(
			_state,
			_ids.Player1BattlefieldId,
			_ids.Player1Id,
			count: 3
		);
		state = SetMana(state, _ids.Player1Id, current: 1, max: 1);
		var (stateWithCard, frogmiteId) = AddFrogmiteToHand(state, _ids.Player1Id);

		var (_, success) = stateWithCard.TryAddAction(
			new CastCreatureAction { CardId = frogmiteId, CastingPlayerId = _ids.Player1Id }
		);

		Assert.That(success, Is.True);
	}

	[Test]
	public void Affinity_FailsWithoutEnoughMana_AfterReduction()
	{
		// 2 artifacts — Frogmite costs 2, but player only has 1 mana
		var state = AddArtifactsToField(
			_state,
			_ids.Player1BattlefieldId,
			_ids.Player1Id,
			count: 2
		);
		state = SetMana(state, _ids.Player1Id, current: 1, max: 1);
		var (stateWithCard, frogmiteId) = AddFrogmiteToHand(state, _ids.Player1Id);

		var (_, success) = stateWithCard.TryAddAction(
			new CastCreatureAction { CardId = frogmiteId, CastingPlayerId = _ids.Player1Id }
		);

		Assert.That(success, Is.False);
	}

	[Test]
	public void Affinity_CostFlooredAtZero()
	{
		// 6 artifacts — Frogmite (base 4) should cost 0 (not go negative)
		var state = AddArtifactsToField(
			_state,
			_ids.Player1BattlefieldId,
			_ids.Player1Id,
			count: 6
		);
		state = SetMana(state, _ids.Player1Id, current: 0, max: 0);
		var (stateWithCard, frogmiteId) = AddFrogmiteToHand(state, _ids.Player1Id);

		var (_, success) = stateWithCard.TryAddAction(
			new CastCreatureAction { CardId = frogmiteId, CastingPlayerId = _ids.Player1Id }
		);

		Assert.That(success, Is.True);
	}

	[Test]
	public void Affinity_ReducesSpellCost_ByArtifactCount()
	{
		// 3 artifacts — Thoughtcast (base 5) should cost 2
		var state = AddArtifactsToField(
			_state,
			_ids.Player1BattlefieldId,
			_ids.Player1Id,
			count: 3
		);
		state = SetMana(state, _ids.Player1Id, current: 2, max: 2);
		var (stateWithCard, thoughtcastId) = AddThoughtcastToHand(state, _ids.Player1Id);

		var (_, success) = stateWithCard.TryAddAction(
			new CastSpellAction { CardId = thoughtcastId, CastingPlayerId = _ids.Player1Id }
		);

		Assert.That(success, Is.True);
	}

	[Test]
	public void Affinity_OnlyCountsOwnArtifacts()
	{
		// Opponent has 3 artifacts — should not reduce your cost
		var state = AddArtifactsToField(
			_state,
			_ids.Player2BattlefieldId,
			_ids.Player2Id,
			count: 3
		);
		state = SetMana(state, _ids.Player1Id, current: 1, max: 1);
		var (stateWithCard, frogmiteId) = AddFrogmiteToHand(state, _ids.Player1Id);

		var (_, success) = stateWithCard.TryAddAction(
			new CastCreatureAction { CardId = frogmiteId, CastingPlayerId = _ids.Player1Id }
		);

		Assert.That(success, Is.False);
	}

	// ===== MULTIPLE ACTIVATIONS (ARCBOUND RAVAGER) =====

	[Test]
	public void Ravager_CanActivateMultipleTimes_SameTurn()
	{
		var (state, ravagerId) = AddRavagerToField(_state, _ids.Player1Id);
		var (state2, artifact1) = AddArtifactToField(
			state,
			_ids.Player1BattlefieldId,
			_ids.Player1Id
		);
		var (state3, artifact2) = AddArtifactToField(
			state2,
			_ids.Player1BattlefieldId,
			_ids.Player1Id
		);
		var (state4, artifact3) = AddArtifactToField(
			state3,
			_ids.Player1BattlefieldId,
			_ids.Player1Id
		);

		var (after1, ok1) = state4.TryAddAction(
			MakeRavagerActivation(ravagerId, artifact1.Id, _ids.Player1Id)
		);
		var (state5, _) = after1.ProcessAllActions();

		var (after2, ok2) = state5.TryAddAction(
			MakeRavagerActivation(ravagerId, artifact2.Id, _ids.Player1Id)
		);
		var (state6, _) = after2.ProcessAllActions();

		var (after3, ok3) = state6.TryAddAction(
			MakeRavagerActivation(ravagerId, artifact3.Id, _ids.Player1Id)
		);

		Assert.That(ok1, Is.True);
		Assert.That(ok2, Is.True);
		Assert.That(ok3, Is.True);
	}

	[Test]
	public void Ravager_BuffsSelf_OnEachActivation()
	{
		var (state, ravagerId) = AddRavagerToField(_state, _ids.Player1Id);
		var (state2, artifact1) = AddArtifactToField(
			state,
			_ids.Player1BattlefieldId,
			_ids.Player1Id
		);
		var (state3, artifact2) = AddArtifactToField(
			state2,
			_ids.Player1BattlefieldId,
			_ids.Player1Id
		);

		var (after1, _) = state3
			.AddAction(MakeRavagerActivation(ravagerId, artifact1.Id, _ids.Player1Id))
			.ProcessAllActions();
		var (after2, _) = after1
			.AddAction(MakeRavagerActivation(ravagerId, artifact2.Id, _ids.Player1Id))
			.ProcessAllActions();

		Assert.That(after2.GetEffectivePower(ravagerId), Is.EqualTo(3)); // 1 base + 2 activations
		Assert.That(after2.GetEffectiveToughness(ravagerId), Is.EqualTo(3));
	}

	[Test]
	public void Ravager_CanSacrificeItself()
	{
		var (state, ravagerId) = AddRavagerToField(_state, _ids.Player1Id);

		var (_, ok) = state.TryAddAction(
			MakeRavagerActivation(ravagerId, ravagerId, _ids.Player1Id)
		);

		Assert.That(ok, Is.True);
	}

	// ===== DISCIPLE OF THE VAULT =====

	[Test]
	public void Disciple_DrainOpponent_WhenArtifactSacrificed()
	{
		var (state, _) = AddDiscipleToField(_state, _ids.Player1Id);
		var (state2, ravagerId) = AddRavagerToField(state, _ids.Player1Id);
		var (state3, artifact) = AddArtifactToField(
			state2,
			_ids.Player1BattlefieldId,
			_ids.Player1Id
		);
		var lifeBefore = state3.GetPlayer(_ids.Player2Id).Life;

		var (finalState, _) = state3
			.AddAction(MakeRavagerActivation(ravagerId, artifact.Id, _ids.Player1Id))
			.ProcessAllActions();

		Assert.That(finalState.GetPlayer(_ids.Player2Id).Life, Is.EqualTo(lifeBefore - 1));
	}

	[Test]
	public void Disciple_GainsLife_WhenArtifactSacrificed()
	{
		var (state, _) = AddDiscipleToField(_state, _ids.Player1Id);
		var (state2, ravagerId) = AddRavagerToField(state, _ids.Player1Id);
		var (state3, artifact) = AddArtifactToField(
			state2,
			_ids.Player1BattlefieldId,
			_ids.Player1Id
		);
		var lifeBefore = state3.GetPlayer(_ids.Player1Id).Life;

		var (finalState, _) = state3
			.AddAction(MakeRavagerActivation(ravagerId, artifact.Id, _ids.Player1Id))
			.ProcessAllActions();

		Assert.That(finalState.GetPlayer(_ids.Player1Id).Life, Is.EqualTo(lifeBefore + 1));
	}

	[Test]
	public void Disciple_DoesNotTrigger_WhenCreatureSacrificed()
	{
		var (state, _) = AddDiscipleToField(_state, _ids.Player1Id);
		var (state2, ravagerId) = AddRavagerToField(state, _ids.Player1Id);
		var (state3, creature) = state2.AddObject(
			TestCardFactory.MakeCreatureCard("Bear", _ids.Player1Id, 2, 2),
			parentId: _ids.Player1BattlefieldId
		);
		var lifeBefore = state3.GetPlayer(_ids.Player2Id).Life;

		var (finalState, _) = state3
			.AddAction(MakeRavagerActivation(ravagerId, creature.Id, _ids.Player1Id))
			.ProcessAllActions();

		Assert.That(finalState.GetPlayer(_ids.Player2Id).Life, Is.EqualTo(lifeBefore));
	}

	// ===== HELPERS =====

	private static GameState AddArtifactsToField(
		GameState state,
		int battlefieldId,
		int ownerId,
		int count
	)
	{
		for (int i = 0; i < count; i++)
		{
			var token = CardLibrary.ClueToken() with { OwnerId = ownerId, ControllerId = ownerId };
			(state, _) = state.AddObject(token, parentId: battlefieldId);
		}
		return state;
	}

	private static (GameState, GameObject) AddArtifactToField(
		GameState state,
		int battlefieldId,
		int ownerId
	)
	{
		var token = CardLibrary.ClueToken() with { OwnerId = ownerId, ControllerId = ownerId };
		return state.AddObject(token, parentId: battlefieldId);
	}

	private static (GameState, int) AddFrogmiteToHand(GameState state, int ownerId)
	{
		var card = new Card
		{
			Name = "Test Frogmite",
			ManaCost = 4,
			OwnerId = ownerId,
			ControllerId = ownerId,
			Subtypes = ImmutableList.Create("Artifact"),
			Components = ImmutableList.Create<GameComponent>(
				new PermanentComponent(),
				new CreatureComponent { Power = 2, Toughness = 2 },
				new AffinityComponent()
			),
		};
		var (newState, added) = state.AddObject(
			card,
			parentId: state.GetPlayerZoneId(ownerId, ZoneType.Hand)
		);
		return (newState, added.Id);
	}

	private static (GameState, int) AddThoughtcastToHand(GameState state, int ownerId)
	{
		var card = new Card
		{
			Name = "Test Thoughtcast",
			ManaCost = 5,
			OwnerId = ownerId,
			ControllerId = ownerId,
			Components = ImmutableList.Create<GameComponent>(
				new SpellComponent(),
				new AffinityComponent()
			),
		};
		var (newState, added) = state.AddObject(
			card,
			parentId: state.GetPlayerZoneId(ownerId, ZoneType.Hand)
		);
		return (newState, added.Id);
	}

	private static (GameState, int) AddRavagerToField(GameState state, int ownerId)
	{
		var ravager = new Card
		{
			Name = "Test Ravager",
			OwnerId = ownerId,
			ControllerId = ownerId,
			Subtypes = ImmutableList.Create("Artifact"),
			Components = ImmutableList.Create<GameComponent>(
				new PermanentComponent(),
				new CreatureComponent { Power = 1, Toughness = 1 },
				new ActivatedAbilityComponent
				{
					Name = "Devour",
					ManaCost = 0,
					MaxActivationsPerTurn = 0,
					AdditionalCosts = ImmutableList.Create<AdditionalCost>(
						new SacrificeAdditionalCost { Filter = null, Count = 1 }
					),
					Effect = new CardEffect
					{
						TargetingStrategy = TargetingStrategy.NoTarget(),
						ActionTemplate = new AddModifierAction
						{
							PowerBonus = 1,
							ToughnessBonus = 1,
							Duration = ModifierDuration.Permanent,
							TargetContextKey = ContextKeys.SourceCardId,
						},
					},
				}
			),
		};
		var (newState, card) = state.AddObject(
			ravager,
			parentId: state.GetPlayerZoneId(ownerId, ZoneType.Battlefield)
		);
		return (newState, card.Id);
	}

	private static (GameState, int) AddDiscipleToField(GameState state, int ownerId)
	{
		var disciple = new Card
		{
			Name = "Test Disciple",
			OwnerId = ownerId,
			ControllerId = ownerId,
			Components = ImmutableList.Create<GameComponent>(
				new PermanentComponent(),
				new CreatureComponent { Power = 1, Toughness = 1 },
				new TriggeredAbilityComponent
				{
					Name = "Drain",
					Condition = TriggerConditions.OnAnyArtifactDies(),
					Effect = new CardEffect
					{
						TargetingStrategy = TargetingStrategy.NoTarget(),
						ActionTemplate = new DrainLifeAction
						{
							Amount = 1,
							TargetOpponent = true,
							PlayerIdContextKey = ContextKeys.CastingPlayerId,
						},
					},
				}
			),
		};
		var (newState, card) = state.AddObject(
			disciple,
			parentId: state.GetPlayerZoneId(ownerId, ZoneType.Battlefield)
		);
		return (newState, card.Id);
	}

	private static GameState SetMana(GameState state, int playerId, int current, int max)
	{
		var player = state.GetPlayer(playerId);
		return state.UpdateObject(playerId, player with { CurrentMana = current, MaxMana = max });
	}

	private static ActivateAbilityAction MakeRavagerActivation(
		int ravagerId,
		int sacrificeId,
		int playerId
	) =>
		new()
		{
			CardId = ravagerId,
			ActivatingPlayerId = playerId,
			AbilityIndex = 0,
			AdditionalCostPayments = ImmutableDictionary<int, ImmutableList<int>>.Empty.Add(
				0,
				ImmutableList.Create(sacrificeId)
			),
		};
}
