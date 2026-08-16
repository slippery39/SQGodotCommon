using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;
using MtgCore.Cards.Builders;
using MtgGame;
using NUnit.Framework;

namespace SQGodotCommon.Tests;

/// <summary>
/// The type line is the card's identity in the UI — before it existed a drafter could not tell a
/// Zombie from a Spirit, because the engine's only categorical data (Card.Subtypes and component
/// presence) was never rendered anywhere.
///
/// Cards are defined inline rather than looked up from a set, so tuning card balance cannot break
/// these assertions.
/// </summary>
[TestFixture]
public class MtgCardTypeLineTests
{
	[Test]
	public void Creature_WithNoSubtypes_ReadsCreature()
	{
		var card = CardFactory
			.Creature("Nameless Thing", manaCost: 2, power: 2, toughness: 2)
			.Build();

		Assert.That(MtgCardMapper.GetTypeLine(card), Is.EqualTo("Creature"));
	}

	/// <summary>
	/// No "Creature — " prefix once there are subtypes: the P/T badge already establishes that the
	/// card is a creature, and the prefix ate half the band's ~24-character budget.
	/// </summary>
	[Test]
	public void Creature_WithSubtypes_ListsThemWithoutTheCreaturePrefix()
	{
		var card = CardFactory
			.Creature("Chapel Guard", manaCost: 2, power: 2, toughness: 2)
			.WithSubtype("Human")
			.WithSubtype("Soldier")
			.Build();

		Assert.That(MtgCardMapper.GetTypeLine(card), Is.EqualTo("Human Soldier"));
	}

	/// <summary>
	/// Multi-tribe cards are common, so the order has to be deterministic and race-first — the
	/// distinctive half is what a player scans for. Declaration order must not leak through.
	/// </summary>
	[Test]
	public void Creature_SubtypeOrder_PutsRaceBeforeClass()
	{
		var card = CardFactory
			.Creature("Risen Adept", manaCost: 3, power: 2, toughness: 3)
			.WithSubtype("Wizard")
			.WithSubtype("Zombie")
			.Build();

		Assert.That(MtgCardMapper.GetTypeLine(card), Is.EqualTo("Zombie Wizard"));
	}

	[Test]
	public void Spell_ReadsSpell()
	{
		var card = CardFactory.Spell("Sudden Jolt", manaCost: 1).WithDamage(3).Build();

		Assert.That(MtgCardMapper.GetTypeLine(card), Is.EqualTo("Spell"));
	}

	[Test]
	public void BasicLand_ReadsBasicLand()
	{
		var card = new Card
		{
			Name = "Plains",
			Subtypes = ImmutableHashSet.Create("Land", "Basic"),
		};

		Assert.That(MtgCardMapper.GetTypeLine(card), Is.EqualTo("Basic Land"));
	}

	/// <summary>
	/// "Land" and "Basic" live in Subtypes alongside real tribes, since the engine has no card-type
	/// system. They name the type, which the type line already states, so they must not be listed
	/// again as if they were tribes.
	/// </summary>
	[Test]
	public void SupertypeStrings_AreNotListedAsTribes()
	{
		var card = CardFactory
			.Creature("Walking Reliquary", manaCost: 4, power: 3, toughness: 4)
			.WithSubtype("Artifact")
			.WithSubtype("Horror")
			.Build();

		Assert.That(MtgCardMapper.GetTypeLine(card), Does.Not.Contain("Artifact"));
		Assert.That(MtgCardTheme.OrderedTribes(card), Is.EqualTo(new[] { "Horror" }));
	}

	// ===== Power / toughness =====

	[Test]
	public void PowerToughness_IsNullForNonCreatures()
	{
		var card = CardFactory.Spell("Sudden Jolt", manaCost: 1).WithDamage(3).Build();

		Assert.That(MtgCardMapper.GetPowerToughness(card, state: null), Is.Null);
	}

	/// <summary>Draft packs have no GameState, so P/T falls back to the printed values.</summary>
	[Test]
	public void PowerToughness_WithoutState_UsesPrintedStats()
	{
		var card = CardFactory
			.Creature("Chapel Guard", manaCost: 2, power: 2, toughness: 3)
			.Build();

		Assert.That(MtgCardMapper.GetPowerToughness(card, state: null), Is.EqualTo("2/3"));
	}

	/// <summary>
	/// The badge must read live stats. The old overlay and the rules text disagreed once a card
	/// carried a modifier — that disagreement is the whole reason P/T now has one source.
	/// </summary>
	[Test]
	public void PowerToughness_WithState_UsesEffectiveStats()
	{
		var (state, ids) = MtgGameFactory.CreateForTesting();
		var battlefieldId = state.GetPlayerZoneId(ids.Player1Id, ZoneType.Battlefield);

		var template = CardFactory
			.Creature("Chapel Guard", manaCost: 2, power: 2, toughness: 2)
			.Build() with
		{
			OwnerId = ids.Player1Id,
			ControllerId = ids.Player1Id,
		};
		var (withCard, card) = state.AddObject(template, parentId: battlefieldId);

		var buffed = withCard.UpdateObject(
			card.Id,
			card with
			{
				Components = card.Components.Add(
					new StaticPowerToughnessModifier
					{
						PowerBonus = 2,
						ToughnessBonus = 2,
						Duration = ModifierDuration.Permanent,
					}
				),
			}
		);

		Assert.That(
			MtgCardMapper.GetPowerToughness((Card)buffed.GetObject(card.Id), buffed),
			Is.EqualTo("4/4")
		);
	}

	/// <summary>Damage goes on its own line so it fits inside the round badge.</summary>
	[Test]
	public void PowerToughness_WhenDamaged_ShowsDamageOnASecondLine()
	{
		var (state, ids) = MtgGameFactory.CreateForTesting();
		var battlefieldId = state.GetPlayerZoneId(ids.Player1Id, ZoneType.Battlefield);

		var template = new Card
		{
			Name = "Chapel Guard",
			OwnerId = ids.Player1Id,
			ControllerId = ids.Player1Id,
			Components = ImmutableArray.Create<GameComponent>(
				new CreatureComponent
				{
					Power = 3,
					Toughness = 4,
					Damage = 2,
				}
			),
		};
		var (withCard, card) = state.AddObject(template, parentId: battlefieldId);

		Assert.That(MtgCardMapper.GetPowerToughness(card, withCard), Is.EqualTo("3/4\n-2"));
	}
}
