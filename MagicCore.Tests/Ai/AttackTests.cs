using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Ai;
using MagicCore.Cards;
using MagicCore.Sets;
using MagicCore.Sets.Fra;

namespace MagicCore.Tests.Ai;

/// <summary>
/// AI-33: the right attack pinned in small positions, each in Shayne's words (2026-11-20: "particular tests for
/// certain attack outcomes, so we can easier measure the performance and the quality of the attacks"). Each prints its
/// cost in engine steps, so an attack speed change (shortlist2, AI-21's grouping) is judged on both. Our turn, our
/// declare-attackers, both at 20 life and empty hands unless said; creatures are vanilla unless said.
/// Every position under the default search AND each attack speed change (AI-33.3) - a change that fails one here
/// changes an attack Shayne named.
/// </summary>
[TestFixture("beam")]
[TestFixture("beam+shortlist2")]
[TestFixture("beam+shortlist1")]
[TestFixture("beam+noblockers")]
public class AttackTests(string ai)
{
	GameState s = null!;

	[SetUp]
	public void Fresh() => s = TestGames.EmptyHands(TestGames.InFirstMain());

	int Me => s.GetGame().StartingPlayerId;
	int Them => s.OpponentOf(Me);

	int Ours(string name, string pt, params string[] keywords)
	{
		(s, var id) = TestGames.Put(
			s,
			Me,
			TestGames.Creature(name, pt, keywords),
			ZoneType.Battlefield
		);
		return id;
	}

	int Theirs(CardDefinition card, bool tapped = false)
	{
		(s, var id) = TestGames.Put(s, Them, card, ZoneType.Battlefield);
		if (tapped)
			s = s.UpdateObject(id, s.GetCard(id) with { IsTapped = true }); // TEST SETUP
		return id;
	}

	void Life(int player, int life) => s = s.WithPlayer(s.GetPlayer(player) with { Life = life });

	/// <summary>Each creature's answer, by name (0: it stays home), and the attack's cost in engine steps.</summary>
	List<(string Name, int Target)> Attack(string? other = null)
	{
		s = TestGames.PassUntil(s, x => x.GetPendingChoice() is DeclareAttackerChoice);
		var search = AiMatch.Create(other ?? ai, Me, 1);
		var answers = new List<(string, int)>();
		long steps = 0;
		while (s.GetPendingChoice() is DeclareAttackerChoice ask && ask.PlayerId == Me)
		{
			var name = s.GetCard(ask.CreatureId).Name;
			var (decided, n) = MagicRules.CountingSteps(() => search.Decide(s));
			steps += n;
			answers.Add((name, decided.Answer!.Single()));
			s = decided.State;
		}
		TestContext.Out.WriteLine(
			$"{other ?? ai}, {steps:N0} engine steps: "
				+ string.Join(
					", ",
					answers.Select(a =>
						$"{a.Item1} {(a.Item2 == CombatOption.None ? "home" : "attacks")}"
					)
				)
		);
		return answers;
	}

	IEnumerable<string> Attacking(List<(string Name, int Target)> answers) =>
		answers.Where(a => a.Target != CombatOption.None).Select(a => a.Name);

	// 1. Lethal on board: everyone attacks - one block stops at most 3.
	[Test]
	public void LethalOnBoard_EveryoneAttacks()
	{
		Ours("Three", "3/3");
		Ours("Two A", "2/2");
		Ours("Two B", "2/2");
		Theirs(TestGames.Creature("One", "1/1"));
		Life(Them, 4);
		Assert.That(Attack(), Has.All.Matches<(string Name, int Target)>(a => a.Target == Them));
	}

	// 2. A bigger blocker: the 2/2 stays home against their untapped 4/4.
	[Test]
	public void ABiggerBlocker_TheTwoTwoStaysHome()
	{
		Ours("Two", "2/2");
		Theirs(TestGames.Creature("Four", "4/4"));
		Assert.That(Attacking(Attack()), Is.Empty);
	}

	// 3. Nothing can block: the 2/2 attacks.
	[Test]
	public void NothingCanBlock_TheTwoTwoAttacks()
	{
		Ours("Two", "2/2");
		Assert.That(Attacking(Attack()), Is.EquivalentTo(new[] { "Two" }));
	}

	// 4. The suite's seed 2 #102: we at 10 with a 2/2 Cadet and a Jace token at 10 whose −2 (Way of the Healer) makes
	// another; they at 12 with two Rampart Hunters (3/3 deathtouch) and a Screeching Soulbreaker (1/4 flier, drains 1
	// when it attacks), all tapped. Shayne: keep the 2/2 back "to try and double block one of their 3/3's the next turn".
	// The control: the one-attack clock (before AI-33.4) attacks with it.
	[TestCase(false)]
	[TestCase(true)]
	public void TheCadetBoard_TheCadetStaysHome_ToDoubleBlockAHunter(bool oneAttack)
	{
		Ours("Cadet", "2/2");
		(s, var jace) = TestGames.Put(s, Me, EmpowerJace.JaceToken, ZoneType.Battlefield);
		s = s.UpdateObject(jace, s.GetCard(jace) with { Loyalty = 10, IsToken = true }); // TEST SETUP
		(s, _) = TestGames.Put(s, Me, CardRegistry.Get("Way of the Healer"), ZoneType.Battlefield);
		Theirs(CardRegistry.Get("Rampart Hunter"), tapped: true);
		Theirs(CardRegistry.Get("Rampart Hunter"), tapped: true);
		Theirs(CardRegistry.Get("Screeching Soulbreaker"), tapped: true);
		Life(Me, 10);
		Life(Them, 12);
		if (oneAttack)
			Assert.That(
				Attacking(Attack($"{ai}+oneattack")),
				Is.EquivalentTo(new[] { "Cadet" }),
				"control"
			);
		else
			Assert.That(Attacking(Attack()), Is.Empty);
	}

	// 6. Evasion only: the 2/2 flier attacks, the 3/3 stays home against their untapped 4/4 without reach.
	[Test]
	public void EvasionOnly_TheFlierAttacks_TheThreeThreeStaysHome()
	{
		Ours("Bird", "2/2", "Flying");
		Ours("Three", "3/3");
		Theirs(TestGames.Creature("Four", "4/4"));
		Assert.That(Attacking(Attack()), Is.EquivalentTo(new[] { "Bird" }));
	}

	// 7. A deathtouch blocker: our 5/5 and 2/2 against their untapped 1/1 deathtouch. Shayne: the 2/2 attacks, the 5/5
	// stays home - "You usually don't want to trade your 5/5 for their 1/1 deathtouch unless maybe you have multiple
	// large creatures and are trying to push through damage. But on its own no."
	[Test]
	public void ADeathtouchBlocker_TheTwoTwoAttacks_TheFiveFiveStaysHome()
	{
		Ours("Five", "5/5");
		Ours("Two", "2/2");
		Theirs(TestGames.Creature("Snake", "1/1", "Deathtouch"));
		Assert.That(Attacking(Attack()), Is.EquivalentTo(new[] { "Two" }));
	}

	// 9. A big board of copies: ten 1/1 tokens, they at 8 with one untapped 2/2. Shayne: "everyone attacks" (nine get
	// through). Its cost against a board of three is the measure for AI-21's grouping.
	[Test]
	public void TenOneOnes_EveryoneAttacks()
	{
		for (var i = 0; i < 10; i++)
			Ours("Token", "1/1");
		Theirs(TestGames.Creature("Two", "2/2"));
		Life(Them, 8);
		var answers = Attack();
		Assert.That(answers, Has.Count.EqualTo(10));
		Assert.That(answers, Has.All.Matches<(string Name, int Target)>(a => a.Target == Them));
	}
}
