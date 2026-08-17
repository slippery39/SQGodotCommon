using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;
using NUnit.Framework;

namespace MtgCore.Tests;

/// <summary>
/// Structural checks on the Core Set Cube's white section.
///
/// These test the SET, so unlike the mechanic tests they look cards up by name deliberately.
/// They assert shape and wiring, not balance — whether a card is interesting is a human review
/// job, and whether it is correctly costed changes as the set is tuned.
/// </summary>
[TestFixture]
public class CoresetCubeWhiteTests
{
	[Test]
	public void AllWhiteCreatures_AreDefined()
	{
		Assert.That(CoresetCubeWhite.Cards, Has.Count.EqualTo(38));
	}

	[Test]
	public void CardNames_AreUnique()
	{
		var duplicates = CoresetCubeWhite
			.Cards.GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
			.Where(g => g.Count() > 1)
			.Select(g => g.Key)
			.ToList();

		Assert.That(duplicates, Is.Empty, $"Duplicate names: {string.Join(", ", duplicates)}");
	}

	[Test]
	public void EveryCard_IsAWellFormedCreature()
	{
		// PermanentComponent and CreatureComponent must both be present or the card cannot be
		// cast — CastCreatureAction and CastPermanentAction each reject the other's shape.
		foreach (var card in CoresetCubeWhite.Cards)
		{
			Assert.That(card.Name, Is.Not.Empty);
			Assert.That(
				card.HasComponent<PermanentComponent>(),
				Is.True,
				$"{card.Name} is missing PermanentComponent"
			);
			Assert.That(
				card.HasComponent<CreatureComponent>(),
				Is.True,
				$"{card.Name} is missing CreatureComponent"
			);
			Assert.That(card.Subtypes, Is.Not.Empty, $"{card.Name} has no creature type");
		}
	}

	[Test]
	public void ConditionalPowerCards_AreTheOnlyOnesWithZeroPower()
	{
		// A 0-power creature is almost always a mistake. Crusader of Odric is the exception —
		// it is printed */* and gets its stats from CreatureCountComponent.
		var zeroPower = CoresetCubeWhite
			.Cards.Where(c => c.GetComponent<CreatureComponent>()!.Power == 0)
			.Select(c => c.Name)
			.ToList();

		Assert.That(zeroPower, Is.EquivalentTo(new[] { "Crusader of Odric" }));
	}

	[Test]
	public void CardsWithPermanentModifiers_UseDurationPermanent()
	{
		// A live-evaluated modifier stamped UntilEndOfTurn is silently stripped by
		// StartTurnAction, which makes the card do nothing from turn two onward.
		foreach (var card in CoresetCubeWhite.Cards)
		foreach (var modifier in card.GetComponents<PowerToughnessModifier>())
			Assert.That(
				modifier.Duration,
				Is.EqualTo(ModifierDuration.Permanent),
				$"{card.Name} has a {modifier.GetType().Name} that StartTurnAction would strip"
			);
	}

	[Test]
	public void Renown_UsesALifetimeCapNotAPerTurnCap()
	{
		// "If it isn't renowned" is once EVER. A per-turn cap makes the creature grow forever.
		foreach (var name in new[] { "Topan Freeblade", "Kytheon's Irregulars" })
		{
			var card = Find(name);
			var renown = card.GetComponents<TriggeredAbilityComponent>()
				.Single(t => t.Name.StartsWith("Renown"));

			Assert.That(renown.MaxTriggers, Is.EqualTo(1), $"{name} renown must be once ever");
			Assert.That(renown.MaxTriggersPerTurn, Is.EqualTo(0));
		}
	}

	[Test]
	public void AngelOfVitality_UsesAReplacementEffectNotATrigger()
	{
		// As a trigger this is an infinite loop: gain life -> trigger -> gain life -> ...
		var angel = Find("Angel of Vitality");

		Assert.That(angel.GetComponent<LifeGainBonusComponent>(), Is.Not.Null);
		Assert.That(
			angel.GetComponents<TriggeredAbilityComponent>(),
			Is.Empty,
			"The life-gain clause must not be modelled as a trigger"
		);
	}

	[Test]
	public void TapperAbilities_RequireTap()
	{
		// Without RequiresTap these are free and repeatable, which is a different card.
		foreach (
			var name in new[] { "Gideon's Lawkeeper", "Intrepid Hero", "Anointer of Champions" }
		)
		{
			var ability = Find(name).GetComponents<ActivatedAbilityComponent>().First();
			Assert.That(ability.RequiresTap, Is.True, $"{name}'s ability should require tapping");
		}
	}

	[Test]
	public void SerraAvenger_HasItsCastRestriction()
	{
		Assert.That(Find("Serra Avenger").GetComponent<MinimumRoundCastRestriction>(), Is.Not.Null);
	}

	[Test]
	public void SpeakerOfTheHeavens_AbilityIsGatedOnLifeTotal()
	{
		var ability = Find("Speaker of the Heavens")
			.GetComponents<ActivatedAbilityComponent>()
			.First();

		Assert.That(ability.Condition, Is.TypeOf<LifeAboveStartingCondition>());
		Assert.That(((LifeAboveStartingCondition)ability.Condition!).Amount, Is.EqualTo(7));
	}

	[Test]
	public void SublimeArchangel_GrantsExaltedToTheTeam()
	{
		var archangel = Find("Sublime Archangel");

		Assert.That(archangel.GetComponent<ExaltedComponent>(), Is.Not.Null, "Its own instance");
		Assert.That(
			archangel.GetComponents<StaticGrantKeywordAbility>().Any(s => s.GrantsExalted),
			Is.True,
			"And the grant to other creatures"
		);
	}

	[Test]
	public void BaneslayerAngel_KeepsSubtypeProtection()
	{
		var protection = Find("Baneslayer Angel").GetComponent<ProtectionFromSubtypeComponent>();

		Assert.That(protection, Is.Not.Null);
		Assert.That(protection!.Subtypes, Is.EquivalentTo(new[] { "Demon", "Dragon" }));
	}

	[Test]
	public void Set_IsRegisteredAndDraftable()
	{
		var set = SetRegistry.Get(CoresetCube.Code);

		Assert.That(set.Cards, Has.Count.EqualTo(38));
		Assert.That(set.Draftable, Is.Not.Empty);
	}

	[Test]
	public void Tokens_AreNotInTheDraftPool()
	{
		var tokenNames = new[] { "Soldier", "Knight", "Cat", "Spirit", "Angel" };

		foreach (var name in tokenNames)
			Assert.That(
				CoresetCube.Cards.Any(c => c.Name == name),
				Is.False,
				$"The {name} token must never be draftable"
			);
	}

	[Test]
	public void EveryCard_CanBeCastAndResolve()
	{
		// The real smoke test: put each card into a hand with unlimited mana and cast it. Catches
		// malformed effects, bad targeting strategies, and ETB triggers that throw on resolution.
		foreach (var template in CoresetCubeWhite.Cards)
		{
			var (state, ids) = MtgGameFactory.CreateForTesting();

			// Serra Avenger cannot be cast in round 1 by design.
			var game = state.TryGetGame()!;
			state = state.UpdateObject(game.Id, game with { TurnNumber = 5 });

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

	private static Card Find(string name) =>
		CoresetCubeWhite.Cards.Single(c =>
			string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)
		);
}
