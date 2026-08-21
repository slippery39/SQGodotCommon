using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;
using NUnit.Framework;

namespace MtgCore.Tests;

/// <summary>
/// Structural fixture for the Core Set Cube's green section, mirroring CoresetCubeRedTests.
///
/// These test the SET, so they look cards up by name deliberately. They assert shape and wiring,
/// not balance — whether a card is interesting is a human review job. The behaviour of the
/// mechanics themselves lives in GreenMechanicsTests, so a balance tweak here cannot delete that
/// coverage.
/// </summary>
[TestFixture]
public class CoresetCubeGreenTests
{
	private static Card Find(string name) =>
		CoresetCubeGreen
			.Cards.Concat(CoresetCubeGreenSpells.Cards)
			.Concat(CoresetCubeGreenPermanents.Cards)
			.First(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));

	[Test]
	public void SectionCounts_MatchTheCube()
	{
		Assert.Multiple(() =>
		{
			Assert.That(CoresetCubeGreen.Cards, Has.Count.EqualTo(36), "36 creatures");
			Assert.That(
				CoresetCubeGreenSpells.Cards,
				Has.Count.EqualTo(20),
				"10 instants + 10 sorceries"
			);
			Assert.That(
				CoresetCubeGreenPermanents.Cards,
				Has.Count.EqualTo(11),
				"7 enchantments + 1 equipment + 3 planeswalkers"
			);
		});
	}

	[Test]
	public void InstantsAndSorceries_DeclareTheirCardType()
	{
		var undeclared = CoresetCubeGreenSpells
			.Cards.Where(c => !c.HasType(CardType.Instant) && !c.HasType(CardType.Sorcery))
			.Select(c => c.Name)
			.ToList();

		Assert.That(undeclared, Is.Empty, "Spell mastery cannot see an undeclared spell");
	}

	[Test]
	public void Planeswalkers_HaveLoyaltyAndAreNotCreatures()
	{
		var walkers = CoresetCubeGreenPermanents
			.Cards.Where(c => c.HasComponent<PlaneswalkerComponent>())
			.ToList();

		Assert.That(walkers, Has.Count.EqualTo(3), "Garruk, Nissa, Vivien");

		foreach (var walker in walkers)
			Assert.Multiple(() =>
			{
				Assert.That(
					walker.HasComponent<CreatureComponent>(),
					Is.False,
					$"{walker.Name} would route through CastCreatureAction"
				);
				Assert.That(
					walker.GetComponent<PlaneswalkerComponent>()!.StartingLoyalty,
					Is.GreaterThan(0),
					$"{walker.Name} would die on arrival"
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
		var tokenNames = new[]
		{
			"Elf Warrior",
			"Beast",
			"Wolf",
			"Saproling",
			"Insect",
			"Elemental",
		};

		foreach (var name in tokenNames)
			Assert.That(
				CoresetCube.Cards.Any(c => c.Name == name),
				Is.False,
				$"{name} is a token and must never be drafted"
			);
	}

	/// <summary>
	/// The real smoke test: put each card into a hand with unlimited mana and cast it. Catches
	/// malformed effects, bad targeting strategies, and ETB triggers that throw on resolution.
	///
	/// It says NOTHING about whether the card did anything — that is what GreenMechanicsTests is
	/// for. Nine white cards passed this exact test while being completely inert.
	/// </summary>
	[Test]
	public void EveryCreature_CanBeCastAndResolve()
	{
		foreach (var template in CoresetCubeGreen.Cards)
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
				new CastCreatureAction
				{
					CardId = card.Id,
					CastingPlayerId = ids.Player1Id,
					XValue = card.HasComponent<XCostComponent>() ? 3 : 0,
				}
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
	/// The Hydras are printed 0/0 and are a dead card without their entry counters — this is the
	/// single assertion that catches an {X} creature whose X never reached the battlefield.
	/// </summary>
	[Test]
	public void Hydras_AreBuiltAtZeroZeroWithXCounters()
	{
		foreach (var name in new[] { "Wildwood Scourge", "Primordial Hydra" })
		{
			var card = Find(name);
			var creature = card.GetComponent<CreatureComponent>()!;
			var entersWith = card.GetComponent<EntersWithCountersComponent>();

			Assert.Multiple(() =>
			{
				Assert.That(creature.Power, Is.Zero, $"{name} is a 0/0 — X is its body");
				Assert.That(creature.Toughness, Is.Zero, name);
				Assert.That(
					card.HasComponent<XCostComponent>(),
					Is.True,
					$"{name} needs XCostComponent or its cost is fixed at its printed base"
				);
				Assert.That(entersWith, Is.Not.Null, name);
				Assert.That(
					entersWith!.FromXValue,
					Is.True,
					$"{name} would enter with a fixed count and ignore the X paid"
				);
			});
		}
	}

	/// <summary>
	/// A counter component stamped UntilEndOfTurn is silently wiped by StartTurnAction, which
	/// would turn Primordial Hydra back into a 0/0 and kill it on the next upkeep. The type
	/// forces Permanent in its constructor; this pins that it stays that way.
	/// </summary>
	[Test]
	public void PlusOneCounters_AreAlwaysPermanentDuration()
	{
		Assert.That(
			new PlusOneCounterComponent { Count = 3 }.Duration,
			Is.EqualTo(ModifierDuration.Permanent)
		);
	}

	/// <summary>
	/// Both Elf lords must count ELVES, not every creature. An empty subtype here would make them
	/// generic anthems, which is a different and much stronger card.
	/// </summary>
	[Test]
	public void ElfLords_BoostElvesOnly()
	{
		foreach (var name in new[] { "Elvish Archdruid", "Dwynen, Gilt-Leaf Daen" })
		{
			var boost = Find(name).GetComponent<StaticPTBoostAbility>();

			Assert.Multiple(() =>
			{
				Assert.That(boost, Is.Not.Null, name);
				Assert.That(
					boost!.Filter,
					Is.TypeOf<IsSubtypeSpecification>(),
					$"{name} must filter on a subtype"
				);
				Assert.That(
					((IsSubtypeSpecification)boost.Filter!).Subtype,
					Is.EqualTo(CoresetCubeGreen.Elf),
					$"{name} must count Elves, not every creature"
				);
			});
		}
	}

	/// <summary>
	/// The mana dorks are upkeep triggers, not activated abilities — see the file header. An
	/// activated mana ability here would be invisible to a player and re-derived every turn by
	/// the AI, and dropping the trigger entirely leaves a creature that quietly does nothing.
	/// </summary>
	[Test]
	public void ManaDorks_RampOnUpkeep_NotViaAnActivatedAbility()
	{
		foreach (
			var name in new[]
			{
				"Birds of Paradise",
				"Elvish Mystic",
				"Llanowar Elves",
				"Llanowar Visionary",
				"Elvish Archdruid",
			}
		)
		{
			var card = Find(name);

			Assert.Multiple(() =>
			{
				Assert.That(
					card.GetComponents<TriggeredAbilityComponent>(),
					Is.Not.Empty,
					$"{name} has no upkeep trigger and produces no mana at all"
				);
				Assert.That(
					card.GetComponents<ActivatedAbilityComponent>()
						.SelectMany(a => a.Effects)
						.Any(e => e.ActionTemplate is AddTemporaryManaAction),
					Is.False,
					$"{name} must not use a tap-for-mana ability — green ramps on upkeep"
				);
			});
		}
	}

	// ===== BEHAVIOUR, NOT SHAPE =====
	// An inert card throws no errors. These three are the green cards whose wiring is most
	// likely to be silently wrong, so each asserts the CONSEQUENCE rather than the components.

	/// <summary>
	/// Wildwood Scourge's whole trigger chain: another creature gets a counter, CountersAddedEvent
	/// reaches PendingGameEvents, the filter accepts it, and the Scourge grows. Four things that
	/// each fail silently.
	/// </summary>
	[Test]
	public void WildwoodScourge_GrowsWhenAnotherCreatureGetsACounter()
	{
		var (state, ids) = MtgGameFactory.CreateForTesting();
		var battlefieldId = state.GetPlayerZoneId(ids.Player1Id, ZoneType.Battlefield);

		// Cast for X = 2 rather than dropped onto the battlefield: it is printed 0/0, so a Scourge
		// placed directly would be destroyed by the zero-toughness rule before anything else runs.
		var handId = state.GetPlayerZoneId(ids.Player1Id, ZoneType.Hand);
		var (withInHand, scourge) = state.AddObject(
			Find("Wildwood Scourge") with
			{
				OwnerId = ids.Player1Id,
				ControllerId = ids.Player1Id,
			},
			parentId: handId
		);

		var (withScourge, _) = withInHand
			.AddAction(
				new CastCreatureAction
				{
					CardId = scourge.Id,
					CastingPlayerId = ids.Player1Id,
					XValue = 2,
				}
			)
			.ProcessAllActions();

		Assert.That(
			withScourge.GetCardZone(scourge.Id).ZoneType,
			Is.EqualTo(ZoneType.Battlefield),
			"setup: the Scourge must have survived its own arrival"
		);

		var (withOther, other) = withScourge.AddObject(
			Find("Deadly Recluse") with
			{
				OwnerId = ids.Player1Id,
				ControllerId = ids.Player1Id,
			},
			parentId: battlefieldId
		);

		var (final, _) = withOther
			.AddAction(
				new AddCountersAction { Amount = 2, TargetIds = ImmutableList.Create(other.Id) }
			)
			.ProcessAllActions();

		var counters =
			((Card)final.GetObject(scourge.Id)).GetComponent<PlusOneCounterComponent>()?.Count ?? 0;

		Assert.That(counters, Is.EqualTo(3), "X=2 on arrival, plus one from the trigger");
	}

	/// <summary>
	/// "If you control ANOTHER Elf" is expressed as Minimum = 2 because the Elite counts itself.
	/// Off by one in either direction is invisible: too low and it always makes a token, too high
	/// and it never does.
	/// </summary>
	[Test]
	public void DwynensElite_MakesItsTokenOnlyAlongsideAnotherElf()
	{
		Assert.Multiple(() =>
		{
			Assert.That(ElfTokensAfterCastingElite(withOtherElf: false), Is.Zero, "alone");
			Assert.That(
				ElfTokensAfterCastingElite(withOtherElf: true),
				Is.EqualTo(1),
				"with another Elf already out"
			);
		});
	}

	private static int ElfTokensAfterCastingElite(bool withOtherElf)
	{
		var (state, ids) = MtgGameFactory.CreateForTesting();

		if (withOtherElf)
		{
			var battlefieldId = state.GetPlayerZoneId(ids.Player1Id, ZoneType.Battlefield);
			(state, _) = state.AddObject(
				Find("Elvish Mystic") with
				{
					OwnerId = ids.Player1Id,
					ControllerId = ids.Player1Id,
				},
				parentId: battlefieldId
			);
		}

		var handId = state.GetPlayerZoneId(ids.Player1Id, ZoneType.Hand);
		var (withElite, elite) = state.AddObject(
			Find("Dwynen's Elite") with
			{
				OwnerId = ids.Player1Id,
				ControllerId = ids.Player1Id,
			},
			parentId: handId
		);

		var (final, _) = withElite
			.AddAction(
				new CastCreatureAction { CardId = elite.Id, CastingPlayerId = ids.Player1Id }
			)
			.ProcessAllActions();

		return final
			.GetCardsInZone(final.GetPlayerZoneId(ids.Player1Id, ZoneType.Battlefield))
			.Count(c => c.Name == "Elf Warrior");
	}

	/// <summary>
	/// The mana dorks are the colour's whole point and produce nothing if the upkeep trigger is
	/// mis-wired — a creature that quietly looks weak. Asserts the mana actually ARRIVES, and
	/// after the refill rather than being wiped by it.
	/// </summary>
	[Test]
	public void ManaDork_ActuallyAddsManaOnYourUpkeep()
	{
		var (state, ids) = MtgGameFactory.Create();
		var battlefieldId = state.GetPlayerZoneId(ids.Player1Id, ZoneType.Battlefield);

		state = state.UpdateObject(
			ids.Player1Id,
			state.GetPlayer(ids.Player1Id) with
			{
				MaxMana = 2,
				CurrentMana = 0,
			}
		);

		(state, _) = state.AddObject(
			Find("Llanowar Elves") with
			{
				OwnerId = ids.Player1Id,
				ControllerId = ids.Player1Id,
			},
			parentId: battlefieldId
		);

		var (final, _) = state
			.AddAction(
				new StartTurnAction
				{
					ActivePlayerId = ids.Player1Id,
					BattlefieldId = battlefieldId,
					SkipDraw = true,
				}
			)
			.ProcessAllActions();

		Assert.That(
			final.GetPlayer(ids.Player1Id).CurrentMana,
			Is.EqualTo(3),
			"2 from the refill plus 1 from the dork — a wrong order would leave it at 2"
		);
	}

	/// <summary>
	/// Elvish Archdruid is the only dork whose amount comes through a pipeline context key, and a
	/// broken chain there yields mana 0 while everything still resolves cleanly. The blanket
	/// no-target sweep cannot see it — a PipelineAction finds its own subjects.
	/// </summary>
	[Test]
	public void ElvishArchdruid_AddsManaEqualToYourElfCount()
	{
		var (state, ids) = MtgGameFactory.Create();
		var battlefieldId = state.GetPlayerZoneId(ids.Player1Id, ZoneType.Battlefield);

		state = state.UpdateObject(
			ids.Player1Id,
			state.GetPlayer(ids.Player1Id) with
			{
				MaxMana = 0,
				CurrentMana = 0,
			}
		);

		// The Archdruid itself is an Elf, plus two more: three Elves, so three mana.
		foreach (var name in new[] { "Elvish Archdruid", "Elvish Mystic", "Elvish Visionary" })
			(state, _) = state.AddObject(
				Find(name) with
				{
					OwnerId = ids.Player1Id,
					ControllerId = ids.Player1Id,
				},
				parentId: battlefieldId
			);

		var (final, _) = state
			.AddAction(
				new StartTurnAction
				{
					ActivePlayerId = ids.Player1Id,
					BattlefieldId = battlefieldId,
					SkipDraw = true,
				}
			)
			.ProcessAllActions();

		// 3 from the Archdruid counting Elves, plus 1 from the Mystic's own upkeep trigger.
		Assert.That(
			final.GetPlayer(ids.Player1Id).CurrentMana,
			Is.EqualTo(4),
			"a broken context-key chain yields 0 and still resolves cleanly"
		);
	}

	/// <summary>
	/// A NoTarget strategy resolves to an EMPTY target list, so an EffectAction paired with one
	/// affects nobody unless it carries a TargetContextKey. It renders perfectly and does nothing.
	///
	/// Barkhide Troll's hexproof ability shipped in exactly that state for one commit —
	/// WithGrantKeyword builds a bare GrantKeywordAction with no target key, and pairing it with
	/// NoTarget() looked completely reasonable. This sweeps the whole section so the next one is
	/// caught by the fixture rather than by a playtest.
	/// </summary>
	[Test]
	public void NoTargetEffects_AlwaysCarryATargetContextKey()
	{
		var offenders = new List<string>();

		var everyEffect = CoresetCubeGreen
			.Cards.Concat(CoresetCubeGreenSpells.Cards)
			.Concat(CoresetCubeGreenPermanents.Cards)
			.SelectMany(card =>
				card.GetComponents<ActivatedAbilityComponent>()
					.SelectMany(a => a.Effects)
					.Concat(
						card.GetComponents<TriggeredAbilityComponent>().SelectMany(t => t.Effects)
					)
					.Concat(card.GetComponent<SpellComponent>()?.Effects ?? [])
					.Select(e => (card.Name, Effect: e))
			);

		foreach (var (name, effect) in everyEffect)
		{
			if (effect.TargetingStrategy.SelectionMode != TargetSelectionMode.None)
				continue;

			// A pipeline finds its own subjects from context, and actions that derive a player
			// from PlayerIdContextKey need no targets at all.
			if (effect.ActionTemplate is not EffectAction action)
				continue;

			if (!string.IsNullOrEmpty(action.TargetContextKey) || !action.TargetIds.IsEmpty)
				continue;

			offenders.Add($"{name}: {action.GetType().Name}");
		}

		Assert.That(
			offenders,
			Is.Empty,
			$"These affect nobody and render as though they work: {string.Join(", ", offenders)}"
		);
	}

	/// <summary>
	/// A fight spell with an empty board is a silent no-op at full price, and the AI will happily
	/// cast it. Every one of them must refuse to be cast without a creature.
	/// </summary>
	[Test]
	public void FightSpells_CannotBeCastWithoutACreature()
	{
		foreach (
			var name in new[] { "Primal Might", "Rabid Bite", "Hunter's Edge", "Wild Instincts" }
		)
			Assert.That(
				Find(name).GetComponents<CastRestrictionComponent>(),
				Is.Not.Empty,
				$"{name} would be castable on an empty board and do nothing"
			);
	}
}
