using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;
using NUnit.Framework;

namespace MtgCore.Tests;

/// <summary>
/// Structural checks on the white non-creature cards: 10 instants, 6 sorceries, 8 enchantments,
/// 1 equipment, 4 planeswalkers.
///
/// These test the SET, so they look cards up by name deliberately.
/// </summary>
[TestFixture]
public class CoresetCubeWhiteNonCreatureTests
{
	[Test]
	public void AllNonCreatureWhiteCards_AreDefined()
	{
		Assert.That(
			CoresetCubeWhiteSpells.Cards,
			Has.Count.EqualTo(16),
			"10 instants + 6 sorceries"
		);
		Assert.That(
			CoresetCubeWhitePermanents.Cards,
			Has.Count.EqualTo(13),
			"8 enchantments + 1 equipment + 4 planeswalkers"
		);
	}

	[Test]
	public void WholeWhiteSection_IsComplete()
	{
		Assert.That(CoresetCube.Cards, Has.Count.EqualTo(67), "38 creatures + 29 non-creatures");
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
		// Derivation cannot tell an instant from a sorcery, which is exactly why the explicit
		// type exists. A spell that leaves it None loses that distinction silently.
		foreach (var card in CoresetCubeWhiteSpells.Cards)
			Assert.That(
				card.HasType(CardType.Instant) ^ card.HasType(CardType.Sorcery),
				Is.True,
				$"{card.Name} should be exactly one of Instant or Sorcery"
			);
	}

	[Test]
	public void Planeswalkers_HaveLoyaltyAndLoyaltyAbilities()
	{
		var walkers = CoresetCubeWhitePermanents
			.Cards.Where(c => c.HasType(CardType.Planeswalker))
			.ToList();

		Assert.That(walkers, Has.Count.EqualTo(4));

		foreach (var walker in walkers)
		{
			var pw = walker.GetComponent<PlaneswalkerComponent>();
			Assert.That(pw, Is.Not.Null, $"{walker.Name} has no PlaneswalkerComponent");
			Assert.That(pw!.StartingLoyalty, Is.GreaterThan(0), $"{walker.Name} has no loyalty");

			var abilities = walker
				.GetComponents<ActivatedAbilityComponent>()
				.Where(a => a.IsLoyaltyAbility)
				.ToList();

			Assert.That(abilities, Has.Count.EqualTo(3), $"{walker.Name} should have 3 abilities");

			// A minus ability the walker can never afford on arrival is fine, but one that costs
			// more than it could ever reach by plussing is a dead ability.
			foreach (var ability in abilities)
				Assert.That(
					-ability.LoyaltyCost,
					Is.LessThanOrEqualTo(pw.StartingLoyalty + 4),
					$"{walker.Name}: '{ability.Name}' is unreachable"
				);
		}
	}

	[Test]
	public void Planeswalkers_AreNotCreatures()
	{
		foreach (
			var walker in CoresetCubeWhitePermanents.Cards.Where(c =>
				c.HasType(CardType.Planeswalker)
			)
		)
			Assert.That(
				walker.HasComponent<CreatureComponent>(),
				Is.False,
				$"{walker.Name} must route through CastPermanentAction, not CastCreatureAction"
			);
	}

	[Test]
	public void Auras_AttachOnEntry()
	{
		// An aura with no attach trigger enters the battlefield and does nothing at all.
		foreach (
			var aura in CoresetCubeWhitePermanents.Cards.Where(c =>
				c.GetComponent<EquipmentComponent>()?.IsAura == true
			)
		)
			Assert.That(
				aura.GetComponents<TriggeredAbilityComponent>().Any(t => t.Name == "Enchant"),
				Is.True,
				$"{aura.Name} is an aura with no attach trigger"
			);
	}

	[Test]
	public void OblivionRing_ReturnTriggerIsGraveyardActive()
	{
		// By the time the Ring's departure is scanned it has already moved, so a
		// battlefield-scoped return trigger would silently never fire.
		var ring = Find("Oblivion Ring");
		var release = ring.GetComponents<TriggeredAbilityComponent>()
			.Single(t => t.Name == "Release");

		Assert.That(release.ActiveInZone, Is.EqualTo(ZoneType.Graveyard));
	}

	[Test]
	public void ReturnToTheRanks_HasBothXAndConvoke()
	{
		var card = Find("Return to the Ranks");

		Assert.That(card.GetComponent<XCostComponent>(), Is.Not.Null);
		Assert.That(card.GetComponent<ConvokeComponent>(), Is.Not.Null);
	}

	[Test]
	public void EverySpell_CanBeCastAndResolve()
	{
		// The real smoke test. Catches malformed effects, bad targeting strategies and pipelines
		// that throw on resolution.
		foreach (var template in CoresetCubeWhiteSpells.Cards)
		{
			var (state, ids) = MtgGameFactory.CreateForTesting();

			// Several of these need a legal target to exist at all.
			state = AddBoard(state, ids);

			var handId = state.GetPlayerZoneId(ids.Player1Id, ZoneType.Hand);
			var (withCard, card) = state.AddObject(
				template with
				{
					OwnerId = ids.Player1Id,
					ControllerId = ids.Player1Id,
				},
				parentId: handId
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
		foreach (var template in CoresetCubeWhitePermanents.Cards)
		{
			var (state, ids) = MtgGameFactory.CreateForTesting();
			state = AddBoard(state, ids);

			var handId = state.GetPlayerZoneId(ids.Player1Id, ZoneType.Hand);
			var (withCard, card) = state.AddObject(
				template with
				{
					OwnerId = ids.Player1Id,
					ControllerId = ids.Player1Id,
				},
				parentId: handId
			);

			var (added, success) = withCard.TryAddAction(
				new CastPermanentAction { CardId = card.Id, CastingPlayerId = ids.Player1Id }
			);

			Assert.That(success, Is.True, $"{template.Name} could not be cast");
			Assert.DoesNotThrow(
				() => added.ProcessAllActions(),
				$"{template.Name} threw while resolving"
			);
		}
	}

	/// <summary>
	/// A creature and an enchantment on each side, so every targeted effect has something legal
	/// to hit. Without the enchantment, Disenchant is correctly never offered and the smoke test
	/// cannot tell "no legal target" apart from "the card is broken".
	/// </summary>
	private static GameState AddBoard(GameState state, MtgGameIds ids)
	{
		foreach (var ownerId in new[] { ids.Player1Id, ids.Player2Id })
		{
			var battlefieldId = state.GetPlayerZoneId(ownerId, ZoneType.Battlefield);

			// One ready and one exhausted, so "target tapped creature" (Swift Response) has a
			// legal target too.
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

			var enchantment = new Card
			{
				Name = "Test Enchantment",
				ManaCost = 2,
				OwnerId = ownerId,
				ControllerId = ownerId,
				Types = CardType.Enchantment,
				Components = ImmutableArray.Create<GameComponent>(new PermanentComponent()),
			};
			(state, _) = state.AddObject(enchantment, parentId: battlefieldId);
		}

		return state;
	}

	private static Card Find(string name) =>
		CoresetCube.Cards.Single(c =>
			string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)
		);
}
