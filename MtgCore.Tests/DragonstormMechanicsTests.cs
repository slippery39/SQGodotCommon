using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;
using NUnit.Framework;

namespace MtgCore.Tests;

[TestFixture]
public class DragonstormMechanicsTests
{
	private GameState _state;
	private MtgGameIds _ids;

	[SetUp]
	public void Setup()
	{
		(_state, _ids) = MtgGameFactory.CreateForTesting();
	}

	// ===== AddTemporaryManaAction =====

	[Test]
	public void AddTemporaryManaAction_IncreasesCurrentMana()
	{
		var action = new AddTemporaryManaAction { TargetIds = [_ids.Player1Id], Amount = 5 };
		var (finalState, _) = _state.AddAction(action).ProcessAllActions();
		Assert.That(finalState.GetPlayer(_ids.Player1Id).CurrentMana, Is.EqualTo(104)); // 99 + 5
	}

	[Test]
	public void AddTemporaryManaAction_DoesNotChangeMaxMana()
	{
		var action = new AddTemporaryManaAction { TargetIds = [_ids.Player1Id], Amount = 5 };
		var (finalState, _) = _state.AddAction(action).ProcessAllActions();
		Assert.That(finalState.GetPlayer(_ids.Player1Id).MaxMana, Is.EqualTo(99));
	}

	// ===== Lotus Bloom =====

	[Test]
	public void LotusBoom_AddsThreeMana()
	{
		var card = CardLibrary.LotusBoom() with
		{
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
		};
		var (s, added) = _state.AddObject(card, parentId: _ids.Player1HandId);

		var (finalState, _) = s.AddAction(MakeCastSpell(added.Id)).ProcessAllActions();

		// 99 (start) - 0 (cost) + 3 (effect) = 102
		Assert.That(finalState.GetPlayer(_ids.Player1Id).CurrentMana, Is.EqualTo(102));
	}

	[Test]
	public void LotusBoom_DoesNotChangeMaxMana()
	{
		var card = CardLibrary.LotusBoom() with
		{
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
		};
		var (s, added) = _state.AddObject(card, parentId: _ids.Player1HandId);

		var (finalState, _) = s.AddAction(MakeCastSpell(added.Id)).ProcessAllActions();

		Assert.That(finalState.GetPlayer(_ids.Player1Id).MaxMana, Is.EqualTo(99));
	}

	// ===== Seething Song =====

	[Test]
	public void SeethingSong_AddsNetTwoMana()
	{
		var card = MakeSeethingSong() with
		{
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
		};
		var (s, added) = _state.AddObject(card, parentId: _ids.Player1HandId);

		var (finalState, _) = s.AddAction(MakeCastSpell(added.Id)).ProcessAllActions();

		// 99 - 3 (cost) + 5 (effect) = 101
		Assert.That(finalState.GetPlayer(_ids.Player1Id).CurrentMana, Is.EqualTo(101));
	}

	// ===== Rite of Flame =====

	[Test]
	public void RiteOfFlame_WithNoGraveyardCopies_AddsNetOneMana()
	{
		var card = MakeRiteOfFlame() with
		{
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
		};
		var (s, added) = _state.AddObject(card, parentId: _ids.Player1HandId);

		var (finalState, _) = s.AddAction(MakeCastSpell(added.Id)).ProcessAllActions();

		// 99 - 1 (cost) + 2 (base) + 0 (graveyard bonus) = 100
		Assert.That(finalState.GetPlayer(_ids.Player1Id).CurrentMana, Is.EqualTo(100));
	}

	[Test]
	public void RiteOfFlame_WithTwoGraveyardCopies_AddsBonusMana()
	{
		var s = _state;
		for (var i = 0; i < 2; i++)
		{
			var grave = MakeRiteOfFlame() with
			{
				OwnerId = _ids.Player1Id,
				ControllerId = _ids.Player1Id,
			};
			(s, _) = s.AddObject(grave, parentId: _ids.Player1GraveyardId);
		}

		var card = MakeRiteOfFlame() with
		{
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
		};
		var (s2, added) = s.AddObject(card, parentId: _ids.Player1HandId);

		var (finalState, _) = s2.AddAction(MakeCastSpell(added.Id)).ProcessAllActions();

		// 99 - 1 (cost) + 2 (base) + 2 (graveyard bonus) = 102
		Assert.That(finalState.GetPlayer(_ids.Player1Id).CurrentMana, Is.EqualTo(102));
	}

	// ===== SelectCardFromLibraryAction + PutIntoBattlefieldAction =====

	[Test]
	public void SelectAndDeploy_PutsDragonOnBattlefield()
	{
		var dragon = CardLibrary.HuntedDragon() with
		{
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
		};
		var (s, addedDragon) = _state.AddObject(dragon, parentId: _ids.Player1LibraryId);

		var pipeline = new PipelineAction
		{
			Steps = ImmutableList.Create<GameAction>(
				new SelectCardFromLibraryAction
				{
					Subtype = "Dragon",
					OutputKey = "target",
					PlayerId = _ids.Player1Id,
				},
				new PutIntoBattlefieldAction { CardIdContextKey = "target" }
			),
		};
		// Run via ResolveEffectAction so PipelineAction gets CastingPlayerId in context
		var resolve = new ResolveEffectAction
		{
			Effects = ImmutableList.Create(
				new CardEffect
				{
					TargetingStrategy = TargetingStrategy.NoTarget(),
					ActionTemplate = pipeline,
				}
			),
			CastingPlayerId = _ids.Player1Id,
			SourceCardId = 0,
			TargetIds = ImmutableDictionary<int, ImmutableList<int>>.Empty,
		};
		var (finalState, _) = s.AddAction(resolve).ProcessAllActions();

		Assert.That(
			finalState.GetCardZone(addedDragon.Id).ZoneType,
			Is.EqualTo(ZoneType.Battlefield)
		);
	}

	[Test]
	public void SelectAndDeploy_RemovesDragonFromLibrary()
	{
		var dragon = CardLibrary.HuntedDragon() with
		{
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
		};
		var (s, _) = _state.AddObject(dragon, parentId: _ids.Player1LibraryId);

		var pipeline = new PipelineAction
		{
			Steps = ImmutableList.Create<GameAction>(
				new SelectCardFromLibraryAction
				{
					Subtype = "Dragon",
					OutputKey = "target",
					PlayerId = _ids.Player1Id,
				},
				new PutIntoBattlefieldAction { CardIdContextKey = "target" }
			),
		};
		var resolve = new ResolveEffectAction
		{
			Effects = ImmutableList.Create(
				new CardEffect
				{
					TargetingStrategy = TargetingStrategy.NoTarget(),
					ActionTemplate = pipeline,
				}
			),
			CastingPlayerId = _ids.Player1Id,
			SourceCardId = 0,
			TargetIds = ImmutableDictionary<int, ImmutableList<int>>.Empty,
		};
		var (finalState, _) = s.AddAction(resolve).ProcessAllActions();

		Assert.That(finalState.GetCardsInZone(_ids.Player1LibraryId).Count(), Is.EqualTo(0));
	}

	// ===== Dragonstorm — storm count integration =====

	[Test]
	public void Dragonstorm_WithTwoPriorSpells_DeploysThreeDragons()
	{
		var s = _state;

		// CastSpellAction increments SpellsCastThisTurn; setting to 2 before cast means
		// DragonStormEffectAction will read 3 and deploy 3 dragons.
		var game = s.GetGame(_ids.GameId);
		s = s.UpdateObject(_ids.GameId, game with { SpellsCastThisTurn = 2 });

		for (var i = 0; i < 5; i++)
		{
			var dragon = CardLibrary.HuntedDragon() with
			{
				OwnerId = _ids.Player1Id,
				ControllerId = _ids.Player1Id,
			};
			(s, _) = s.AddObject(dragon, parentId: _ids.Player1LibraryId);
		}

		var ds = CardLibrary.Dragonstorm() with
		{
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
		};
		var (s2, addedDs) = s.AddObject(ds, parentId: _ids.Player1HandId);

		var (finalState, _) = s2.AddAction(MakeCastSpell(addedDs.Id)).ProcessAllActions();

		var dragons = finalState
			.GetCardsInZone(_ids.Player1BattlefieldId)
			.Where(c => c.HasSubtype("Dragon"))
			.ToList();
		Assert.That(dragons.Count, Is.EqualTo(3));
	}

	[Test]
	public void Dragonstorm_WithZeroPriorSpells_DeploysOnedragon()
	{
		var s = _state;

		var dragon = CardLibrary.HuntedDragon() with
		{
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
		};
		(s, _) = s.AddObject(dragon, parentId: _ids.Player1LibraryId);

		var ds = CardLibrary.Dragonstorm() with
		{
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
		};
		var (s2, addedDs) = s.AddObject(ds, parentId: _ids.Player1HandId);

		var (finalState, _) = s2.AddAction(MakeCastSpell(addedDs.Id)).ProcessAllActions();

		var dragons = finalState
			.GetCardsInZone(_ids.Player1BattlefieldId)
			.Where(c => c.HasSubtype("Dragon"))
			.ToList();
		Assert.That(dragons.Count, Is.EqualTo(1));
	}

	// ===== Bogardan Hellkite ETB =====

	[Test]
	public void BogardanHellkite_WhenCast_DealsFiveDamageToOpponent()
	{
		var initialLife = _state.GetPlayer(_ids.Player2Id).Life;

		var card = CardLibrary.BogardanHellkite() with
		{
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
		};
		var (s, added) = _state.AddObject(card, parentId: _ids.Player1HandId);

		var cast = new CastCreatureAction { CardId = added.Id, CastingPlayerId = _ids.Player1Id };
		var (finalState, _) = s.AddAction(cast).ProcessAllActions();

		Assert.That(finalState.GetPlayer(_ids.Player2Id).Life, Is.EqualTo(initialLife - 5));
	}

	[Test]
	public void BogardanHellkite_WhenCast_LandsOnBattlefield()
	{
		var card = CardLibrary.BogardanHellkite() with
		{
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
		};
		var (s, added) = _state.AddObject(card, parentId: _ids.Player1HandId);

		var cast = new CastCreatureAction { CardId = added.Id, CastingPlayerId = _ids.Player1Id };
		var (finalState, _) = s.AddAction(cast).ProcessAllActions();

		Assert.That(finalState.GetCardZone(added.Id).ZoneType, Is.EqualTo(ZoneType.Battlefield));
	}

	// ===== Helpers =====

	private CastSpellAction MakeCastSpell(int cardId) =>
		new()
		{
			CardId = cardId,
			CastingPlayerId = _ids.Player1Id,
			TargetIds = ImmutableDictionary<int, ImmutableList<int>>.Empty,
		};

	// Test-local cards with fixed mana values — independent of CardLibrary changes.

	// Seething Song: cost 3, adds 5 mana (net +2).
	private static Card MakeSeethingSong() =>
		new()
		{
			Name = "Seething Song",
			ManaCost = 3,
			Components =
			[
				new SpellComponent
				{
					Effects =
					[
						new CardEffect
						{
							TargetingStrategy = TargetingStrategy.Self(),
							ActionTemplate = new AddTemporaryManaAction { Amount = 5 },
						},
					],
				},
			],
		};

	// Rite of Flame: cost 1, adds 2 base + 1 per copy in graveyard.
	private static Card MakeRiteOfFlame() =>
		new()
		{
			Name = "Rite of Flame",
			ManaCost = 1,
			Components =
			[
				new SpellComponent
				{
					Effects =
					[
						new CardEffect
						{
							TargetingStrategy = TargetingStrategy.NoTarget(),
							ActionTemplate = new PipelineAction
							{
								Steps = ImmutableList.Create<GameAction>(
									new CountCardsWithNameAction
									{
										CardName = "Rite of Flame",
										Zone = ZoneType.Graveyard,
										OutputKey = "rite_count",
										PlayerIdContextKey = ContextKeys.CastingPlayerId,
									},
									new AddTemporaryManaAction
									{
										Amount = 2,
										BonusAmountContextKey = "rite_count",
										TargetContextKey = ContextKeys.CastingPlayerId,
									}
								),
							},
						},
					],
				},
			],
		};
}
