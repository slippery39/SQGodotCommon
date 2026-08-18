using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;
using NUnit.Framework;

namespace MtgCore.Tests;

/// <summary>
/// Structural fixture for the Core Set Cube's red section, mirroring CoresetCubeBlackTests.
///
/// These test the SET, so they look cards up by name deliberately. They assert shape and wiring,
/// not balance — whether a card is interesting is a human review job. The behaviour of the
/// mechanics themselves lives in RedMechanicsTests, so a balance tweak here cannot delete that
/// coverage.
/// </summary>
[TestFixture]
public class CoresetCubeRedTests
{
	[Test]
	public void SectionCounts_MatchTheCube()
	{
		Assert.Multiple(() =>
		{
			Assert.That(CoresetCubeRed.Cards, Has.Count.EqualTo(38), "38 creatures");
			Assert.That(
				CoresetCubeRedSpells.Cards,
				Has.Count.EqualTo(20),
				"12 instants + 8 sorceries"
			);
			Assert.That(
				CoresetCubeRedPermanents.Cards,
				Has.Count.EqualTo(9),
				"4 enchantments + 1 artifact + 4 planeswalkers"
			);
		});
	}

	[Test]
	public void InstantsAndSorceries_DeclareTheirCardType()
	{
		var undeclared = CoresetCubeRedSpells
			.Cards.Where(c => !c.HasType(CardType.Instant) && !c.HasType(CardType.Sorcery))
			.Select(c => c.Name)
			.ToList();

		Assert.That(undeclared, Is.Empty, "Spell mastery cannot see an undeclared spell");
	}

	[Test]
	public void Planeswalkers_HaveLoyaltyAndAreNotCreatures()
	{
		var walkers = CoresetCubeRedPermanents
			.Cards.Where(c => c.HasComponent<PlaneswalkerComponent>())
			.ToList();

		Assert.That(walkers, Has.Count.EqualTo(4), "3 Chandras + Sarkhan");

		foreach (var walker in walkers)
			Assert.Multiple(() =>
			{
				Assert.That(
					walker.HasComponent<CreatureComponent>(),
					Is.False,
					$"{walker.Name} must route through CastPermanentAction"
				);
				Assert.That(
					walker
						.GetComponents<ActivatedAbilityComponent>()
						.Count(a => a.IsLoyaltyAbility),
					Is.EqualTo(3),
					$"{walker.Name} should have three loyalty abilities"
				);
			});
	}

	[Test]
	public void Tokens_AreNotInTheDraftPool()
	{
		var tokenNames = new[] { "Goblin", "Elemental", "Thopter", "Dragon" };
		Assert.That(
			CoresetCube.Cards.Select(c => c.Name).Intersect(tokenNames),
			Is.Empty,
			"Tokens must never appear in a draft pack"
		);
	}

	/// <summary>
	/// The real smoke test: put each card into a hand with unlimited mana and cast it. Catches
	/// malformed effects, bad targeting strategies, and ETB triggers that throw on resolution.
	///
	/// It says NOTHING about whether the card did anything — that is what the named tests below
	/// and AllSetsCardBugTests are for. Nine white cards passed this exact test while being
	/// completely inert.
	/// </summary>
	[Test]
	public void EveryCard_CanBeCastAndResolve()
	{
		foreach (var template in CoresetCubeRed.Cards)
		{
			var (state, ids) = MtgGameFactory.CreateForTesting();
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
				new CastCreatureAction { CardId = card.Id, CastingPlayerId = ids.Player1Id }
			);

			Assert.That(success, Is.True, $"{template.Name} could not be cast");
			Assert.DoesNotThrow(
				() => added.ProcessAllActions(),
				$"{template.Name} threw while resolving"
			);

			var (final, _) = added.ProcessAllActions();
			Assert.That(
				final.GetCardZone(card.Id).ZoneType,
				Is.EqualTo(ZoneType.Battlefield),
				$"{template.Name} did not reach the battlefield"
			);
		}
	}

	/// <summary>
	/// Every Goblin payoff in the section counts the Goblin subtype, so a Goblin that forgot its
	/// subtype would silently stop feeding Chieftain, Piledriver, Krenko and Volley Veteran while
	/// looking completely correct on the board.
	/// </summary>
	[Test]
	public void GoblinCount_IsLargeEnoughToSupportTheTribalPayoffs()
	{
		var goblins = CoresetCubeRed.Cards.Count(c => c.HasSubtype(CoresetCubeRed.Goblin));

		Assert.That(
			goblins,
			Is.GreaterThanOrEqualTo(12),
			"The tribal payoffs are only live if red is actually full of Goblins"
		);
	}

	/// <summary>
	/// The count modifiers must be Permanent or StartTurnAction strips them at the start of the
	/// next turn, quietly turning Goblin Piledriver into a vanilla 1/2. Duration is the single
	/// easiest thing to forget on a live-evaluated modifier.
	/// </summary>
	[Test]
	public void CountBasedBuffs_ArePermanentDuration()
	{
		foreach (var name in new[] { "Goblin Piledriver", "Goblin Rabblemaster" })
		{
			var modifier = Find(name).GetComponent<CreatureCountComponent>();

			Assert.Multiple(() =>
			{
				Assert.That(modifier, Is.Not.Null, name);
				Assert.That(
					modifier!.Duration,
					Is.EqualTo(ModifierDuration.Permanent),
					$"{name}'s count modifier would be stripped every turn"
				);
				Assert.That(
					modifier.Subtype,
					Is.EqualTo(CoresetCubeRed.Goblin),
					$"{name} must count Goblins, not every creature"
				);
				Assert.That(modifier.CountsSelf, Is.False, $"{name} counts OTHER Goblins");
			});
		}
	}

	/// <summary>
	/// Renown is a LIFETIME cap, not a per-turn one. A per-turn cap silently turns a renown
	/// creature into one that grows every single turn.
	/// </summary>
	[Test]
	public void RenownCreatures_GrowOnlyOnce()
	{
		foreach (var name in new[] { "Goblin Glory Chaser", "Scab-Clan Berserker" })
		{
			var renown = Find(name)
				.GetComponents<TriggeredAbilityComponent>()
				.Single(t => t.Name.StartsWith("Renown"));

			Assert.That(renown.MaxTriggers, Is.EqualTo(1), $"{name} must be renowned only once");
		}
	}

	/// <summary>
	/// Chandra's Phoenix recurs itself from the graveyard, so its trigger must be graveyard-active.
	/// A Battlefield-scoped trigger on a card that is in the graveyard silently never fires — the
	/// same trap every death trigger has.
	/// </summary>
	[Test]
	public void ChandrasPhoenix_RecursFromTheGraveyard()
	{
		var trigger = Find("Chandra's Phoenix")
			.GetComponents<TriggeredAbilityComponent>()
			.Single(t => t.Name == "Rekindle");

		Assert.That(trigger.ActiveInZone, Is.EqualTo(ZoneType.Graveyard));
	}

	private static Card Find(string name) =>
		CoresetCubeRed.Cards.Single(c =>
			string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)
		);
}
