using ImmutableGameObjects;
using KinCore.Party;

namespace KinCore.Tests;

/// <summary>
/// **THE FAMILIES — every Grove and Ember rule FIRES** (`KinFamiliesPlan.md`): Grow, Root, the Grove
/// passives and cards (Ember's are in `PartyTests.Ember.cs`); rewards leaning to your families. Uses
/// the battle fixtures in `PartyTests.cs`; card amounts are read from the cards, never restated.
/// </summary>
public partial class PartyTests
{
	private static PartyCompanion With(PartyCompanion m, params GameComponent[] abilities) =>
		m with
		{
			Abilities = [.. abilities],
		};

	private static GameState Solo(PartyCompanion m, KinCard[] hand, params Foe[] foes) =>
		Deal([new PlacedCompanion(m, 0)], hand, foes.Length > 0 ? foes : [Foe(0)]);

	// ===== The content

	[Test]
	public void EveryMonsterABossCanOfferIsItsFamilyAndBringsAnEngine()
	{
		foreach (var family in new[] { Family.Grove, Family.Ember })
		foreach (var monster in PartyContent.MonstersOf(family))
		{
			Assert.That(monster.Family, Is.EqualTo(family), monster.Name);
			Assert.That(monster.Abilities, Is.Not.Empty, $"{monster.Name} brings an engine");
		}
	}

	// ===== ONE FAMILY PER RUN (KinFamiliesPlan.md, round 2)

	[Test]
	public void ARunIsItsFamilyAndStartsWithTwoOfItsCards()
	{
		foreach (var family in PartyContent.Families)
		{
			var run = PartyRun.Start(family, 1);

			Assert.That(run.Family, Is.EqualTo(family).And.Not.EqualTo(Family.None));
			Assert.That(run.Deck.Count(c => c.Family == family), Is.EqualTo(2), family.ToString());
			Assert.That(run.Deck.Count, Is.EqualTo(PartyContent.StarterDeck.Count + 2));
		}
	}

	// ===== THREE FROM THE START (KinFamiliesPlan.md, round 5)

	[Test]
	public void ARunStartsWithThreeOfItsFamilysPoolRolledBySeed()
	{
		foreach (var family in PartyContent.Families)
		{
			var pool = PartyContent.PoolOf(family).Select(m => m.Name).ToList();
			var trios = Enumerable
				.Range(1, 30)
				.Select(seed => PartyRun.Start(family, seed).Team.Select(m => m.Companion.Name))
				.Select(names => string.Join(",", names))
				.ToList();

			foreach (var trio in trios.Select(t => t.Split(',')))
			{
				Assert.That(trio, Has.Length.EqualTo(PartyRun.TeamSize).And.Unique);
				Assert.That(trio, Is.SubsetOf(pool), family.ToString());
			}
			Assert.That(
				PartyRun.Start(family, 7).Team.Select(m => m.Companion.Name),
				Is.EqualTo(PartyRun.Start(family, 7).Team.Select(m => m.Companion.Name)),
				"a seed is always the same trio"
			);
			Assert.That(trios.Distinct().Count(), Is.GreaterThan(1), "seeds roll different trios");
		}
	}

	[Test]
	public void TheRerollGivesADifferentTrioOnceAndOnlyBeforeSettingOut()
	{
		var run = PartyRun.Start(Family.Ember, 3);
		var names = run.Team.Select(m => m.Companion.Name).ToHashSet();

		var rerolled = run.Reroll();
		Assert.That(rerolled.Team.Select(m => m.Companion.Name), Is.Not.EquivalentTo(names));
		Assert.That(rerolled.Team, Has.Count.EqualTo(PartyRun.TeamSize));

		Assert.That(rerolled.Reroll(), Is.SameAs(rerolled), "once only");
		var setOut = run.EnterRoute();
		Assert.That(setOut.Reroll(), Is.SameAs(setOut), "not once set out");
	}

	[Test]
	public void RewardsAndTheShopOfferOnlyYourFamilyAndColourless()
	{
		foreach (var family in PartyContent.Families)
		{
			var offered = Enumerable
				.Range(0, 100)
				.Select(seed => PartyRun.Start(family, seed))
				.SelectMany(run => run.RewardOffer().Concat(run.ShopCards()))
				.ToList();

			Assert.That(
				offered.Select(c => c.Family).Distinct(),
				Is.EquivalentTo(new[] { Family.None, family }),
				family.ToString()
			);
		}
	}

	[Test]
	public void ABossesWinOffersARare()
	{
		foreach (var seed in Enumerable.Range(0, 20))
		{
			var after = PartyRun.Start(PartyContent.Bramble, seed) with { Phase = RunPhase.Town };

			Assert.That(after.RewardOffer().Any(c => c.Rarity == Rarity.Rare), Is.True);
		}
	}

	/// <summary>
	/// **Every card a run can hold has SOMEWHERE to be dropped** (playtest, 2026-09-30: Ember Dart
	/// could not be played at all — its damage wanted a foe and its draw wanted your monster, and no
	/// place is both). An inert card throws no error, so each is asked place by place, + too.
	/// </summary>
	[Test]
	public void EveryCardARunCanHoldCanBePlayedSomewhere()
	{
		var cards = PartyContent
			.Rewards.Concat(PartyContent.StarterDeck)
			.SelectMany(c => c.Upgraded is { } plus ? new[] { c, plus } : [c]);
		var stuck = new List<string>();
		foreach (var card in cards)
		{
			// A monster behind a token, two foes, energy to spare: every card's precondition met.
			var s = Deal(
				[new PlacedCompanion(Mon("Keeper"), 0), new PlacedCompanion(Mon("Second"), 1)],
				[Summon(Sapling()), card],
				Foe(0),
				Foe(1)
			);
			s = Play(s, "Summon", 0);
			s = s.UpdateObject(s.GetParty().Id, s.GetParty() with { Energy = 10 });
			var inHand = s.CardsIn(ZoneType.Hand)
				.First(c => c.Name == card.Name && c.Effects[0].Text == card.Effects[0].Text);

			var playable = Enumerable
				.Range(0, PartyBattle.MaxLine)
				.SelectMany(space => new[] { (space, false), (space, true) })
				.Any(p =>
					new PlayPartyCardAction
					{
						CardId = inHand.Id,
						Space = p.space,
						FoeRow = p.Item2,
					}
						.ValidateAdd(s)
						.IsValid
				);

			if (!playable)
				stuck.Add(card.Name);
		}

		Assert.That(stuck, Is.Empty, "these cards have nowhere to be played");
	}

	/// <summary>
	/// **MTG's targeting rule** (Shayne, 2026-09-30): a card with no step that targets plays wherever it
	/// is dropped — no place at all; a card with one needs that place, whatever else it does.
	/// </summary>
	[Test]
	public void ACardWithNoTargetPlaysAnywhereAndATargetedOneNeedsItsPlace()
	{
		var surge = Card("Surge", 0, new GainEnergyAction { Amount = 2 }, new DrawAction());
		var dart = Card("Dart", 0, new SpellDamageAction { Amount = 2 }, new DrawAction());
		var sow = Summon(Sapling());
		var s = Solo(Mon("Keeper"), [surge, dart, sow]);

		Assert.That(surge.NeedsTarget(), Is.False);
		Assert.That(sow.NeedsTarget(), Is.False, "a token always arrives at the front");
		Assert.That(dart.NeedsTarget(), Is.True, "the draw does not stop the damage needing a foe");

		bool Plays(string name, int space, bool foeRow = false) =>
			new PlayPartyCardAction
			{
				CardId = InHand(s, name).Id,
				Space = space,
				FoeRow = foeRow,
			}
				.ValidateAdd(s)
				.IsValid;

		Assert.That(Plays("Surge", -1), Is.True, "no place at all");
		Assert.That(Plays("Summon", -1), Is.True);
		Assert.That(Plays("Dart", -1), Is.False);
		Assert.That(Plays("Dart", 0, foeRow: true), Is.True);
		Assert.That(Plays("Dart", 0), Is.False, "not on your own monster");
	}
}
