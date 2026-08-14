using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;
using NUnit.Framework;

namespace MtgCore.Tests;

/// <summary>
/// Reproductions for cards reported as not working in play. These deliberately use the real
/// set cards rather than inline definitions — the whole point is to prove the shipped card
/// behaves as its text claims, which an inline copy could not do.
/// </summary>
[TestFixture]
public class HollowmereCardBugTests
{
	private GameState _state;
	private MtgGameIds _ids;

	[SetUp]
	public void Setup()
	{
		(_state, _ids) = MtgGameFactory.CreateForTesting();
	}

	private static Card Card(string name) =>
		Hollowmere.Cards.First(c => c.Name == name) with
		{
			OwnerId = 0,
			ControllerId = 0,
		};

	private Card Owned(string name) =>
		Hollowmere.Cards.First(c => c.Name == name) with
		{
			OwnerId = _ids.Player1Id,
			ControllerId = _ids.Player1Id,
		};

	// ===== REPORT 1: Zombie Apocalypse reanimated nothing =====

	[Test]
	public void ZombieApocalypse_ReturnsZombiesFromGraveyardToPlay()
	{
		var state = _state;
		var zombieIds = new List<int>();
		for (var i = 0; i < 2; i++)
		{
			var (s, added) = state.AddObject(Owned("Diregraf Ghoul"), _ids.Player1GraveyardId);
			state = s;
			zombieIds.Add(added.Id);
		}

		var (withSpell, spell) = state.AddObject(Owned("Zombie Apocalypse"), _ids.Player1HandId);

		var (final, _) = withSpell
			.AddAction(TestCardFactory.MakeCastAction(spell.Id, _ids.Player1Id))
			.ProcessAllActions();

		foreach (var id in zombieIds)
			Assert.That(
				final.GetCardZone(id).ZoneType,
				Is.EqualTo(ZoneType.Battlefield),
				"Zombie should have been reanimated"
			);
	}

	// ===== REPORT 2: an Angel's ETB destroy did nothing =====

	[Test]
	public void AngelOfBrokenVigils_DestroysACreatureWhenItEnters()
	{
		var (state, victim) = AddToBattlefield(
			_state,
			TestCardFactory.MakeCreatureCard("Victim", _ids.Player2Id, 2, 2),
			_ids.Player2Id
		);

		var (withAngel, angel) = state.AddObject(
			Owned("Angel of Broken Vigils"),
			_ids.Player1HandId
		);

		var (final, _) = withAngel
			.AddAction(new PutIntoBattlefieldAction { TargetIds = ImmutableList.Create(angel.Id) })
			.ProcessAllActions();

		Assert.That(
			final.GetCardZone(victim.Id).ZoneType,
			Is.EqualTo(ZoneType.Graveyard),
			"ETB destroy should have killed the opposing creature"
		);
	}

	// ===== REPORT 3: discard-to-reanimate was uncastable =====

	[Test]
	public void BargainAtTheCrossroads_IsCastableWithACreatureInGraveyard()
	{
		var state = _state;
		var (s1, corpse) = state.AddObject(Owned("Diregraf Ghoul"), _ids.Player1GraveyardId);
		var (s2, fodder) = s1.AddObject(Owned("Diregraf Ghoul"), _ids.Player1HandId);
		var (withSpell, spell) = s2.AddObject(
			Owned("Bargain at the Crossroads"),
			_ids.Player1HandId
		);

		var actions = MtgActionGenerator.GetLegalActions(withSpell, _ids.Player1Id);

		Assert.That(
			actions.OfType<CastSpellAction>().Any(a => a.CardId == spell.Id),
			Is.True,
			"Spell should be castable: a discard is available and a creature is in the graveyard"
		);
	}

	/// <summary>
	/// The generator-level cause behind report 3. A spell with two user-select effects needs
	/// targets supplied for BOTH effect indices, or CastSpellAction.ValidateAdd rejects it.
	/// </summary>
	[Test]
	public void SpellsWithTwoTargetedEffects_AreOfferedByTheActionGenerator()
	{
		var state = _state;
		var (s1, _) = state.AddObject(Owned("Diregraf Ghoul"), _ids.Player1GraveyardId);
		var (s2, _) = s1.AddObject(Owned("Diregraf Ghoul"), _ids.Player1HandId);

		var offenders = new List<string>();
		foreach (var card in Hollowmere.Cards)
		{
			var spell = card.GetComponent<SpellComponent>();
			if (spell == null)
				continue;
			if (spell.Effects.Count(e => e.TargetingStrategy.RequiresUserSelection) < 2)
				continue;
			offenders.Add(card.Name);
		}

		Assert.That(
			offenders,
			Is.Empty,
			"Cards with 2+ user-select effects cannot be generated: " + string.Join(", ", offenders)
		);
	}

	/// <summary>
	/// The cause behind report 2. Triggered abilities spawn ResolveEffectAction with no
	/// TargetIds, so a UserSelect effect inside a trigger resolves to an empty target list and
	/// silently does nothing. Triggers must use AllValid, Random, CastingPlayer or a context key.
	/// </summary>
	[Test]
	public void TriggeredAbilities_DoNotUseUserSelectTargeting()
	{
		var offenders = new List<string>();
		foreach (var card in Hollowmere.Cards)
		foreach (var trigger in card.GetComponents<TriggeredAbilityComponent>())
		foreach (var effect in trigger.Effects)
		{
			if (effect.TargetingStrategy.RequiresUserSelection)
				offenders.Add($"{card.Name} ({trigger.Name})");
		}

		Assert.That(
			offenders.Distinct(),
			Is.Empty,
			"Triggers with user-select targeting silently do nothing: "
				+ string.Join("; ", offenders.Distinct())
		);
	}

	/// <summary>
	/// Activated abilities DO resolve user-select targets — the generator enumerates them and
	/// ActivateAbilityAction validates them — but only for the first effect. Any later effect
	/// that also wants a target would silently get none.
	/// </summary>
	[Test]
	public void ActivatedAbilities_TargetOnlyInTheirFirstEffect()
	{
		var offenders = new List<string>();
		foreach (var card in Hollowmere.Cards)
		foreach (var ability in card.GetComponents<ActivatedAbilityComponent>())
		{
			var targeted = ability
				.Effects.Skip(1)
				.Where(e => e.TargetingStrategy.RequiresUserSelection);
			if (targeted.Any())
				offenders.Add($"{card.Name} ({ability.Name})");
		}

		Assert.That(offenders.Distinct(), Is.Empty, string.Join("; ", offenders.Distinct()));
	}

	/// <summary>
	/// An activated ability built from several .With* calls must keep them all. The builder
	/// used to take only the first, so "discard a card, then draw a card" silently never drew.
	/// </summary>
	[Test]
	public void ActivatedAbilities_KeepEveryEffect()
	{
		var neonate = Hollowmere.Cards.First(c => c.Name == "Insolent Neonate");
		var rummage = neonate.GetComponents<ActivatedAbilityComponent>().First();

		Assert.That(
			rummage.Effects.Count,
			Is.EqualTo(2),
			"Rummage is discard-then-draw; the draw was being dropped at build time"
		);
	}

	private (GameState, Card) AddToBattlefield(GameState state, Card template, int playerId)
	{
		var battlefieldId = state.GetPlayerZoneId(playerId, ZoneType.Battlefield);
		var card = template with { OwnerId = playerId, ControllerId = playerId };
		var (newState, added) = state.AddObject(card, battlefieldId);
		var creature = added.GetComponent<CreatureComponent>();
		if (creature != null)
			newState = newState.UpdateObject(
				added.Id,
				added.WithComponentReplaced(creature with { HasSummoningSickness = false })
			);
		return (newState, (Card)newState.GetObject(added.Id));
	}
}
