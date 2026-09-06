using MtgCore;
using MtgCore.Cards.Builders;
using NUnit.Framework;

namespace MtgSimulator.Tests;

/// <summary>
/// **Does the pool contain cards that cheat on mana, and can we tell them from cards that do not?**
///
/// The demand model cannot: `Raise the Sunken` and `Gravedigger` ask the identical question, so
/// they build the same core and dedupe into one archetype. These tests establish that the gap
/// separates them, on a pool where the answer is not in doubt.
/// </summary>
[TestFixture]
public class CostInversionTests
{
	private static Card Fatty(string name, int cost) =>
		CardFactory.Creature(name, manaCost: cost, power: 7, toughness: 7).Build();

	/// <summary>
	/// **A synthetic pool needs cards that do NOT answer the demand, or the demand is dropped.**
	///
	/// `PoolFeatures` discards any demand answered by more than `UninformativeShare` (0.99) of the
	/// pool, because a question the whole pool answers cannot separate two decks. In a fixture of
	/// three creatures, "a creature card in your graveyard" is answered by 3 of 3 — so the demand
	/// vanishes, `Rank` finds nothing, and the test fails as though the feature were broken.
	///
	/// It cost a debugging session here. The failure is silent: `Demands.Count` is simply 0, with
	/// no entry in `Failures` and nothing naming the card that was dropped.
	/// </summary>
	private static IEnumerable<Card> Ballast() =>
		[
			CardFactory.Spell("Bolt", manaCost: 1).WithDamage(3).Build(),
			CardFactory.Spell("Growth", manaCost: 1).WithDraw(1).Build(),
		];

	/// <summary>
	/// **The headline discrimination, and the reason this file exists.**
	///
	/// Both spells read "a creature card in your graveyard". One puts it onto the battlefield for
	/// one mana; the other returns it to your hand for four, where you still have to pay eight.
	/// Only the first is arbitrage, and the action type is the only thing that says so.
	///
	/// `Digger`'s assertions are the vacuity guard: it must be IN the pool and must ASK the same
	/// demand, so its absence from the ranking can only be the action type. A rule that simply
	/// ranked cheap cards first would fail them.
	/// </summary>
	[Test]
	public void AReanimationSpellIsArbitrage_AndAReturnToHandSpellIsNot()
	{
		var raise = CardFactory
			.Spell("Raise", manaCost: 1)
			.WithReanimate()
			.WithTarget(TargetBuilder.Single().CreatureInYourGraveyard())
			.Build();

		var digger = CardFactory
			.Spell("Digger", manaCost: 4)
			.WithReturnCreatureFromGraveyard()
			.Build();

		var pool = new List<Card>
		{
			raise,
			digger,
			Fatty("Colossus", 8),
			Fatty("Ogre", 3),
			CardFactory.Creature("Runt", manaCost: 1, power: 1, toughness: 1).Build(),
		};

		var features = PoolFeatures.Build(pool);
		var rows = CostInversion.Rank(pool, features);

		var raised = rows.SingleOrDefault(r => r.Card == "Raise");
		var dug = rows.SingleOrDefault(r => r.Card == "Digger");

		TestContext.Out.WriteLine(
			string.Join(
				"\n",
				rows.Select(r =>
					$"{r.Card,-10} paid {r.Paid} cheated {r.Cheated} gap {r.Gap,3} "
						+ $"candidates {r.Candidates} big {r.Big} via {r.Via}"
				)
			)
		);

		Assert.Multiple(() =>
		{
			Assert.That(raised, Is.Not.Null, "reanimation is the canonical mana cheat");
			Assert.That(
				raised!.Cheated,
				Is.EqualTo(8),
				"the cheat is worth the BIGGEST thing it can reach, because you choose your deck"
			);
			Assert.That(raised.Gap, Is.EqualTo(7), "8 cheated against 1 paid");

			// The guard: present, and asking the same question — so only the action type can be
			// what excluded it.
			Assert.That(
				pool.Any(c => c.Name == "Digger"),
				Is.True,
				"fixture broken: the control card is not in the pool"
			);
			Assert.That(
				features.DemandsOf("Digger"),
				Is.Not.Empty,
				"fixture broken: the control card asks nothing, so its absence proves nothing"
			);
			Assert.That(
				dug,
				Is.Null,
				"returning a creature to HAND is not arbitrage — you still pay for it"
			);
		});
	}

	/// <summary>
	/// A cheat reached through an activated ability costs the card AND the activation, so a 3-drop
	/// with a 4-mana ability is a worse deal than a 1-mana sorcery doing the same thing. Without
	/// this the two rank identically and the report claims every recursive creature is Reanimate.
	/// </summary>
	[Test]
	public void AnActivatedCheatPaysForTheCardAndTheActivation()
	{
		var stitcher = CardFactory
			.Creature("Stitcher", manaCost: 3, power: 0, toughness: 3)
			.WithActivatedAbility(
				"Reanimate",
				manaCost: 4,
				effect: eb => eb.WithReanimate(),
				costs: c => c.SacrificeSelf()
			)
			.Build();

		List<Card> pool = [stitcher, Fatty("Colossus", 8), Fatty("Ogre", 3), .. Ballast()];
		var rows = CostInversion.Rank(pool, PoolFeatures.Build(pool));

		var row = rows.SingleOrDefault(r => r.Card == "Stitcher");
		Assert.That(row, Is.Not.Null, "an activated reanimate is still a cheat");
		Assert.That(row!.Paid, Is.EqualTo(7), "3 for the body plus 4 to activate");
		Assert.That(row.Gap, Is.EqualTo(1), "8 cheated against 7 paid — a real but thin edge");
	}

	/// <summary>
	/// Read the ranking before wiring this into seeding. `MTG_SET` selects the pool.
	/// </summary>
	[Test]
	[Explicit("Diagnostic — prints the mana-arbitrage ranking for a real pool.")]
	public void DumpCostInversionForARealPool()
	{
		var set = SetRegistry.Get(Environment.GetEnvironmentVariable("MTG_SET") ?? "ALL");
		var spells = set.Cards.Where(c => !c.HasSubtype("Land")).ToList();
		var rows = CostInversion.Rank(spells, PoolFeatures.Build(spells));

		TestContext.Out.WriteLine($"{set.Code}: {rows.Count} cards put something into play\n");
		TestContext.Out.WriteLine(
			$"{"card",-32}{"paid",5}{"cheat",6}{"gap",5}{"cand",6}{"big",5}  via"
		);
		foreach (var r in rows)
			TestContext.Out.WriteLine(
				$"{r.Card,-32}{r.Paid,5}{r.Cheated,6}{r.Gap,5}{r.Candidates,6}{r.Big,5}  {r.Via}"
			);
	}
}
