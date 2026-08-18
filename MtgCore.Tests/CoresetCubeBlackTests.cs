using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore;
using NUnit.Framework;

namespace MtgCore.Tests;

/// <summary>
/// Structural fixture for the Core Set Cube's black section, mirroring CoresetCubeBlueTests.
///
/// These test the SET, so they look cards up by name deliberately. They assert shape and wiring,
/// not balance — whether a card is interesting is a human review job. The behaviour of the
/// mechanics themselves lives in BlackMechanicsTests, so a balance tweak here cannot delete that
/// coverage.
/// </summary>
[TestFixture]
public class CoresetCubeBlackTests
{
	[Test]
	public void SectionCounts_MatchTheCube()
	{
		Assert.Multiple(() =>
		{
			Assert.That(CoresetCubeBlack.Cards, Has.Count.EqualTo(38), "38 creatures");
			Assert.That(
				CoresetCubeBlackSpells.Cards,
				Has.Count.EqualTo(18),
				"8 instants + 10 sorceries"
			);
			Assert.That(
				CoresetCubeBlackPermanents.Cards,
				Has.Count.EqualTo(11),
				"5 enchantments + 4 auras + 2 planeswalkers"
			);
		});
	}

	[Test]
	public void Tokens_AreNotInTheDraftPool()
	{
		var tokenNames = new[] { "Zombie", "Saproling", "Demon" };
		Assert.That(
			CoresetCube.Cards.Select(c => c.Name).Intersect(tokenNames),
			Is.Empty,
			"Tokens must never appear in a draft pack"
		);
	}

	[Test]
	public void InstantsAndSorceries_DeclareTheirCardType()
	{
		var undeclared = CoresetCubeBlackSpells
			.Cards.Where(c => !c.HasType(CardType.Instant) && !c.HasType(CardType.Sorcery))
			.Select(c => c.Name)
			.ToList();

		Assert.That(undeclared, Is.Empty, "Spell mastery cannot see an undeclared spell");
	}

	[Test]
	public void Planeswalkers_HaveLoyaltyAndAreNotCreatures()
	{
		foreach (var name in new[] { "Liliana Vess", "Sorin Markov" })
		{
			var walker = Find(name);
			Assert.Multiple(() =>
			{
				Assert.That(walker.GetComponent<PlaneswalkerComponent>(), Is.Not.Null, name);
				Assert.That(walker.HasComponent<CreatureComponent>(), Is.False, name);
				Assert.That(
					walker
						.GetComponents<ActivatedAbilityComponent>()
						.Count(a => a.IsLoyaltyAbility),
					Is.EqualTo(3),
					$"{name} should have three loyalty abilities"
				);
			});
		}
	}

	/// <summary>
	/// Renown-style lifetime caps and per-turn caps are independent, and the Knight's end-step
	/// trigger wants the per-turn one: it may grow every turn, but only once per turn.
	/// </summary>
	[Test]
	public void KnightOfTheEbonLegion_GrowsAtMostOncePerTurn()
	{
		var trigger = Find("Knight of the Ebon Legion")
			.GetComponents<TriggeredAbilityComponent>()
			.Single(t => t.Condition is LifeLostThisTurnCondition);

		Assert.Multiple(() =>
		{
			Assert.That(trigger.MaxTriggersPerTurn, Is.EqualTo(1));
			Assert.That(trigger.MaxTriggers, Is.EqualTo(0), "It is not renown — no lifetime cap");
		});
	}

	/// <summary>
	/// A repeatable recursion limited only by mana returns every turn forever. The exile cost is
	/// what gives Despoiler a floor, and it is the reason FlashbackComponent carries costs at all.
	/// </summary>
	[Test]
	public void DespoilerOfSouls_PaysAGraveyardCostToRecur()
	{
		var flashback = Find("Despoiler of Souls").GetComponent<FlashbackComponent>();

		Assert.That(flashback, Is.Not.Null);
		Assert.That(
			flashback!.AdditionalCosts.OfType<ExileFromGraveyardAdditionalCost>().Single().Count,
			Is.EqualTo(2)
		);
	}

	/// <summary>
	/// Demonic Pact's fourth mode is the entire card. Without onceEach it is a repeating value
	/// engine with a mode nothing ever picks.
	/// </summary>
	[Test]
	public void DemonicPact_StrikesOffModesAndCanLoseTheGame()
	{
		var pipeline = (PipelineAction)
			Find("Demonic Pact")
				.GetComponents<TriggeredAbilityComponent>()
				.Single()
				.Effects[0]
				.ActionTemplate!;

		var select = pipeline.Steps.OfType<SelectModeAction>().Single();
		var apply = pipeline.Steps.OfType<ApplyChosenModeAction>().Single();

		Assert.Multiple(() =>
		{
			Assert.That(select.ExcludeAlreadyChosen, Is.True);
			Assert.That(apply.RecordChoice, Is.True);
			Assert.That(apply.Modes, Has.Count.EqualTo(4));
			Assert.That(
				apply.Modes.OfType<SetLifeTotalAction>().Single().Amount,
				Is.EqualTo(0),
				"The fourth mode must actually end the game"
			);
		});
	}

	/// <summary>
	/// Royal Assassin is retargeted from "tapped" to "attacked this turn" precisely so it does
	/// not join the exhausted-target allowlist in CoresetCubeCardBugTests — black owns no tappers
	/// in this cube, so the printed wording would leave it blank.
	/// </summary>
	[Test]
	public void RoyalAssassin_TargetsCreaturesThatAttacked()
	{
		var ability = Find("Royal Assassin").GetComponents<ActivatedAbilityComponent>().Single();
		var spec = ability.Effects[0].TargetingStrategy.Specification;

		Assert.That(
			Flatten(spec).OfType<HasAttackedThisTurnSpecification>().Any(),
			Is.True,
			"Royal Assassin should read HasAttacked, not IsExhausted"
		);
		Assert.That(Flatten(spec).OfType<IsExhaustedSpecification>().Any(), Is.False);
	}

	// ===== SMOKE TESTS =====

	[Test]
	public void EveryCreature_CanBeCastAndResolve()
	{
		foreach (var template in CoresetCubeBlack.Cards)
		{
			var (state, ids) = Fresh();
			var (withCard, card) = AddToHand(state, ids, template);

			var legal = MtgActionGenerator
				.GetLegalActions(withCard, ids.Player1Id)
				.OfType<CastCreatureAction>()
				.FirstOrDefault(a => a.CardId == card.Id);

			Assert.That(legal, Is.Not.Null, $"{template.Name} was never offered as castable");
			Assert.DoesNotThrow(
				() => withCard.AddAction(legal!).ProcessAllActions(),
				$"{template.Name} threw while resolving"
			);
		}
	}

	[Test]
	public void EverySpell_CanBeCastAndResolve()
	{
		foreach (var template in CoresetCubeBlackSpells.Cards)
		{
			var (state, ids) = Fresh();
			var (withCard, card) = AddToHand(state, ids, template);

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
		foreach (var template in CoresetCubeBlackPermanents.Cards)
		{
			var (state, ids) = Fresh();
			var (withCard, card) = AddToHand(state, ids, template);

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

	private static (GameState, MtgGameIds) Fresh()
	{
		var (state, ids) = MtgGameFactory.CreateForTesting();
		state = AddBoard(state, ids);
		state = StockGraveyards(state, ids);
		state = StockLibraries(state, ids);
		return (state, ids);
	}

	private static (GameState, Card) AddToHand(GameState state, MtgGameIds ids, Card template) =>
		state.AddObject(
			template with
			{
				OwnerId = ids.Player1Id,
				ControllerId = ids.Player1Id,
			},
			parentId: state.GetPlayerZoneId(ids.Player1Id, ZoneType.Hand)
		);

	private static GameState AddBoard(GameState state, MtgGameIds ids)
	{
		foreach (var ownerId in new[] { ids.Player1Id, ids.Player2Id })
		{
			var battlefieldId = state.GetPlayerZoneId(ownerId, ZoneType.Battlefield);

			// A square 2/2 and a non-square 3/1 — Gilt-Leaf Winnower only sees the latter.
			foreach (var (power, toughness) in new[] { (2, 2), (3, 1) })
			{
				var creature = new Card
				{
					Name = $"Test Bear {power}/{toughness}",
					ManaCost = 2,
					OwnerId = ownerId,
					ControllerId = ownerId,
					Types = CardType.Creature,
					Components = ImmutableArray.Create<GameComponent>(
						new PermanentComponent(),
						new CreatureComponent
						{
							Power = power,
							Toughness = toughness,
							HasSummoningSickness = false,
						}
					),
				};
				(state, _) = state.AddObject(creature, parentId: battlefieldId);
			}
		}

		return state;
	}

	/// <summary>Reanimation and the graveyard-cost recursions need something to work with.</summary>
	private static GameState StockGraveyards(GameState state, MtgGameIds ids)
	{
		foreach (var ownerId in new[] { ids.Player1Id, ids.Player2Id })
		{
			var graveyardId = state.GetPlayerZoneId(ownerId, ZoneType.Graveyard);
			for (var i = 0; i < 6; i++)
				(state, _) = state.AddObject(
					new Card
					{
						Name = $"Dead Thing {ownerId}-{i}",
						ManaCost = 2,
						OwnerId = ownerId,
						ControllerId = ownerId,
						Types = CardType.Creature,
						Components = ImmutableArray.Create<GameComponent>(
							new PermanentComponent(),
							new CreatureComponent { Power = 2, Toughness = 2 }
						),
					},
					parentId: graveyardId
				);
		}

		return state;
	}

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

	/// <summary>Walks And/Or/Not composites so a spec can be found wherever it is nested.</summary>
	private static IEnumerable<TargetSpecification> Flatten(TargetSpecification? spec)
	{
		if (spec == null)
			yield break;

		yield return spec;

		foreach (
			var child in spec switch
			{
				AndSpecification a => new[] { a.Left, a.Right },
				OrSpecification o => new[] { o.Left, o.Right },
				NotSpecification n => new[] { n.Inner },
				_ => Enumerable.Empty<TargetSpecification>(),
			}
		)
		foreach (var nested in Flatten(child))
			yield return nested;
	}

	private static Card Find(string name) =>
		CoresetCube.Cards.Single(c =>
			string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)
		);
}
