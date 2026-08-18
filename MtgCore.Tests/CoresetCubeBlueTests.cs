using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;
using NUnit.Framework;

namespace MtgCore.Tests;

/// <summary>
/// Structural checks on the Core Set Cube's blue section: 28 creatures, 18 instants,
/// 11 sorceries, 6 enchantments, 4 planeswalkers.
///
/// These test the SET, so they look cards up by name deliberately.
/// </summary>
[TestFixture]
public class CoresetCubeBlueTests
{
	[Test]
	public void BlueSection_IsComplete()
	{
		Assert.That(CoresetCubeBlue.Cards, Has.Count.EqualTo(28), "creatures");
		Assert.That(
			CoresetCubeBlueSpells.Cards,
			Has.Count.EqualTo(29),
			"18 instants + 11 sorceries"
		);
		Assert.That(
			CoresetCubeBluePermanents.Cards,
			Has.Count.EqualTo(10),
			"6 enchantments + 4 planeswalkers"
		);
	}

	[Test]
	public void WholeSet_IsWhitePlusBluePlusBlack()
	{
		Assert.That(CoresetCube.Cards, Has.Count.EqualTo(201), "67 white + 67 blue + 67 black");
	}

	[Test]
	public void CardNames_AreUniqueAcrossTheSet()
	{
		var duplicates = CoresetCube
			.Cards.GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
			.Where(g => g.Count() > 1)
			.Select(g => g.Key)
			.ToList();

		Assert.That(duplicates, Is.Empty, $"Duplicate names: {string.Join(", ", duplicates)}");
	}

	[Test]
	public void InstantsAndSorceries_DeclareTheirType()
	{
		foreach (var card in CoresetCubeBlueSpells.Cards)
			Assert.That(
				card.HasType(CardType.Instant) ^ card.HasType(CardType.Sorcery),
				Is.True,
				$"{card.Name} should be exactly one of Instant or Sorcery"
			);
	}

	[Test]
	public void CounterTraps_AreNotCastable()
	{
		// A trap has a SpellComponent but no effects. Without the guard in MtgActionGenerator it
		// would be offered as a blank spell at full price.
		var traps = CoresetCubeBlueSpells
			.Cards.Where(c => c.HasComponent<CounterTrapComponent>())
			.ToList();

		// Six printed counterspells plus Unsubstantiate, whose "return target spell" half is a
		// counter that bounces rather than destroys.
		Assert.That(traps, Has.Count.EqualTo(7));

		foreach (var trap in traps)
		{
			var (state, ids) = MtgGameFactory.CreateForTesting();
			var (withCard, card) = state.AddObject(
				trap with
				{
					OwnerId = ids.Player1Id,
					ControllerId = ids.Player1Id,
				},
				parentId: state.GetPlayerZoneId(ids.Player1Id, ZoneType.Hand)
			);

			var actions = MtgActionGenerator.GetLegalActions(withCard, ids.Player1Id);

			Assert.That(
				actions.OfType<CastSpellAction>().Any(a => a.CardId == card.Id),
				Is.False,
				$"{trap.Name} must never be offered as castable"
			);
		}
	}

	[Test]
	public void Walls_UseTauntAsTheirBlockerSubstitute()
	{
		foreach (var name in new[] { "Fog Bank", "Wall of Frost" })
		{
			var wall = Find(name);
			Assert.That(
				wall.GetComponent<CreatureComponent>()!.HasTaunt,
				Is.True,
				$"{name} needs Taunt or it does nothing at all"
			);
		}
	}

	[Test]
	public void FogBank_DropsTauntAfterBeingAttacked()
	{
		// Otherwise a damage-immune Taunt creature is unanswerable — every attack is compelled
		// into it forever, and there is no going wide in this engine.
		var fogBank = Find("Fog Bank");

		Assert.That(fogBank.GetComponent<TauntUntilAttackedComponent>(), Is.Not.Null);
		Assert.That(fogBank.GetComponent<PreventsCombatDamageComponent>(), Is.Not.Null);
	}

	[Test]
	public void Planeswalkers_HaveLoyaltyAndThreeAbilities()
	{
		var walkers = CoresetCubeBluePermanents
			.Cards.Where(c => c.HasType(CardType.Planeswalker))
			.ToList();

		Assert.That(walkers, Has.Count.EqualTo(4));

		foreach (var walker in walkers)
		{
			var pw = walker.GetComponent<PlaneswalkerComponent>();
			Assert.That(pw, Is.Not.Null, $"{walker.Name} has no PlaneswalkerComponent");
			Assert.That(pw!.StartingLoyalty, Is.GreaterThan(0));
			Assert.That(
				walker.GetComponents<ActivatedAbilityComponent>().Count(a => a.IsLoyaltyAbility),
				Is.EqualTo(3),
				$"{walker.Name} should have 3 loyalty abilities"
			);
		}
	}

	[Test]
	public void Auras_TargetWhenCast()
	{
		// See CoresetCubeWhiteNonCreatureTests.Auras_TargetWhenCast — an ETB trigger cannot make
		// the spell illegal, so an aura cast with nothing to enchant used to sit there inert.
		foreach (
			var aura in CoresetCubeBluePermanents.Cards.Where(c =>
				c.GetComponent<EquipmentComponent>()?.IsAura == true
			)
		)
			Assert.That(
				aura.GetComponent<AuraTargetComponent>(),
				Is.Not.Null,
				$"{aura.Name} is an aura that does not target on cast"
			);
	}

	[Test]
	public void EveryCreature_CanBeCastAndResolve()
	{
		foreach (var template in CoresetCubeBlue.Cards)
		{
			var (state, ids) = MtgGameFactory.CreateForTesting();
			state = AddBoard(state, ids);
			state = AdvanceTurn(state);

			var (withCard, card) = state.AddObject(
				template with
				{
					OwnerId = ids.Player1Id,
					ControllerId = ids.Player1Id,
				},
				parentId: state.GetPlayerZoneId(ids.Player1Id, ZoneType.Hand)
			);

			var (added, success) = withCard.TryAddAction(
				new CastCreatureAction { CardId = card.Id, CastingPlayerId = ids.Player1Id }
			);

			Assert.That(success, Is.True, $"{template.Name} could not be cast");
			Assert.DoesNotThrow(
				() => added.ProcessAllActions(),
				$"{template.Name} threw while resolving"
			);
		}
	}

	[Test]
	public void EverySpell_CanBeCastAndResolve()
	{
		foreach (var template in CoresetCubeBlueSpells.Cards)
		{
			// Counter traps are never cast — they are covered by CounterTraps_AreNotCastable.
			if (template.HasComponent<CounterTrapComponent>())
				continue;

			var (state, ids) = MtgGameFactory.CreateForTesting();
			state = AddBoard(state, ids);
			state = StockLibraries(state, ids);
			state = AdvanceTurn(state);

			var (withCard, card) = state.AddObject(
				template with
				{
					OwnerId = ids.Player1Id,
					ControllerId = ids.Player1Id,
				},
				parentId: state.GetPlayerZoneId(ids.Player1Id, ZoneType.Hand)
			);

			var legal = MtgActionGenerator
				.GetLegalActions(withCard, ids.Player1Id)
				.OfType<CastSpellAction>()
				.FirstOrDefault(a => a.CardId == card.Id);

			Assert.That(legal, Is.Not.Null, $"{template.Name} was never offered as castable");
			Assert.DoesNotThrow(
				() => withCard.AddAction(legal!).ProcessAllActions(),
				$"{template.Name} threw while resolving"
			);
		}
	}

	[Test]
	public void EveryPermanent_CanBeCastAndResolve()
	{
		foreach (var template in CoresetCubeBluePermanents.Cards)
		{
			var (state, ids) = MtgGameFactory.CreateForTesting();
			state = AddBoard(state, ids);
			state = StockLibraries(state, ids);

			var (withCard, card) = state.AddObject(
				template with
				{
					OwnerId = ids.Player1Id,
					ControllerId = ids.Player1Id,
				},
				parentId: state.GetPlayerZoneId(ids.Player1Id, ZoneType.Hand)
			);

			// Through the generator rather than a hand-built action: an Aura needs a cast-time
			// target, and the generator is what supplies one in a real game.
			var legal = MtgActionGenerator
				.GetLegalActions(withCard, ids.Player1Id)
				.OfType<CastPermanentAction>()
				.FirstOrDefault(a => a.CardId == card.Id);

			Assert.That(legal, Is.Not.Null, $"{template.Name} was never offered as castable");
			Assert.DoesNotThrow(
				() => withCard.AddAction(legal!).ProcessAllActions(),
				$"{template.Name} threw while resolving"
			);
		}
	}

	// ===== HELPERS =====

	/// <summary>Illusory Angel refuses to be cast as the first spell of a turn.</summary>
	private static GameState AdvanceTurn(GameState state)
	{
		var game = state.TryGetGame()!;
		return state.UpdateObject(game.Id, game with { SpellsCastThisTurn = 1, TurnNumber = 5 });
	}

	private static GameState AddBoard(GameState state, MtgGameIds ids)
	{
		foreach (var ownerId in new[] { ids.Player1Id, ids.Player2Id })
		{
			var battlefieldId = state.GetPlayerZoneId(ownerId, ZoneType.Battlefield);

			foreach (var exhausted in new[] { false, true })
			{
				var creature = new Card
				{
					Name = exhausted ? "Test Bear (tapped)" : "Test Bear",
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
							IsExhausted = exhausted,
						}
					),
				};
				(state, _) = state.AddObject(creature, parentId: battlefieldId);
			}
		}

		return state;
	}

	/// <summary>Draw and mill effects need cards to work with.</summary>
	private static GameState StockLibraries(GameState state, MtgGameIds ids)
	{
		foreach (var ownerId in new[] { ids.Player1Id, ids.Player2Id })
		{
			var libraryId = state.GetPlayerZoneId(ownerId, ZoneType.Library);
			for (var i = 0; i < 30; i++)
				(state, _) = state.AddObject(
					new Card
					{
						Name = $"Filler {i}",
						ManaCost = 1,
						OwnerId = ownerId,
						ControllerId = ownerId,
						Types = CardType.Sorcery,
						Components = ImmutableArray.Create<GameComponent>(new SpellComponent()),
					},
					parentId: libraryId
				);
		}

		return state;
	}

	private static Card Find(string name) =>
		CoresetCube.Cards.Single(c =>
			string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)
		);
}
