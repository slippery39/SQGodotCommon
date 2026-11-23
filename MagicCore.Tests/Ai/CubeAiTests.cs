using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Ai;
using MagicCore.Cards;
using MagicCore.Tests.Sets;
using Nu = NUnit.Framework.Is;

namespace MagicCore.Tests.Ai;

/// <summary>
/// MC-9: the cube's cards in the AI's hands — each item watched first (the position below is the one watched), then
/// fixed. A realistic board, not the base board's twenty basics: six lands, two each of Swamp (or another), Island and Mountain.
/// </summary>
public class CubeAiTests : FraBoardTest
{
	void SixLands(string first = "Swamp")
	{
		EmptyBoard();
		s = TestGames.EmptyHands(s);
		foreach (var land in new[] { first, first, "Island", "Island", "Mountain", "Mountain" })
			Field(TestGames.Card(land), me);
	}

	/// <summary>The search plays the rest of this turn (and <paramref name="more"/> after it), both seats; what each decision was, in order.</summary>
	List<string> SearchPlaysTheTurn(int more = 0)
	{
		var (end, played) = (s.GetGame().TurnNumber + 1 + more, new List<string>());
		for (var i = 0; i < 30 * (1 + more) && s.GetGame().TurnNumber < end && !s.IsOver(); i++)
		{
			s = s.AutoPass();
			if (s.GetGame().TurnNumber >= end || s.IsOver())
				break;
			var decision = new BeamSearchAi(s.DecidingPlayerId()).Decide(s);
			(s, played) = (decision.State, [.. played, decision.What]);
		}
		return played;
	}

	int TheirCreatures() =>
		s.CardsIn(them, ZoneType.Battlefield).Count(c => c.Is(CardType.Creature));

	// MC-9.1, watched 2026-11-23: past BeamSearchAi.MaxPicks target sets the search took GreedyAi's one target, so Fire
	// Covenant was X = 4 on one 4/4 of twelve creatures. Now a kill set: each target the damage that kills it.
	[Test]
	public void FireCovenant_GreedyDividesItsXAmongWhatItKills()
	{
		SixLands();
		var small = new[] { "2/2", "1/1", "2/1" }
			.Select(size => Field(TestGames.Creature($"Small {size}", size), them))
			.ToList();
		var big = Field(TestGames.Creature("Giant", "4/4"), them);
		var covenant = Hand(Lsv("Fire Covenant"), me);
		var greedy = new GreedyAi(me);

		s = s.Act(Casts(covenant).Single(c => c.X == 4));
		var asked = ResolveAsking(choice => greedy.Answer(s, choice));

		Assert.That(small.All(InGraveyard), Nu.True, "2 + 1 + 1 = 4: three killed");
		Assert.That(Alive(big), Nu.True);
		Assert.That(
			asked.OfType<ChooseNumberChoice>().All(n => n.ShareOf > 0),
			Nu.True,
			"each share asked for its target"
		);
	}

	[Test]
	public void FireCovenant_TheSearchKillsSeveralOfTwelve()
	{
		SixLands();
		var sizes = new[] { "2/2", "3/3", "4/4", "1/1", "5/5", "2/1" };
		for (var i = 0; i < 12; i++)
			Field(TestGames.Creature($"C{i}", sizes[i % sizes.Length]), them);
		Hand(Lsv("Fire Covenant"), me);
		SearchPlaysTheTurn();

		Assert.That(TheirCreatures(), Nu.LessThanOrEqualTo(12 - 2), "more than one killed");
	}

	// MC-9.2, watched 2026-11-23: with no noncreature spell to grow her first, the search took Vivi's {0} mana move at
	// power 0 after its last spell - no mana, and the turn's one activation used.
	[Test]
	public void Vivi_AtPowerZero_HerManaMoveIsLeftOutOfTheSearch_NotTheRules()
	{
		SixLands();
		var vivi = Field(Lsv("Vivi Ornitier"), me);
		var bolt = Hand(Lsv("Lightning Bolt"), me);
		bool Offered(IEnumerable<GameAction> moves) =>
			moves.OfType<TapForManaAction>().Any(m => m.CardId == vivi);

		Assert.That(Offered(s.LegalActions()), Nu.True, "the rules offer it");
		Assert.That(Offered(s.SearchMoves()), Nu.False, "at power 0 it adds nothing");
		Cast(bolt, [], them); // "whenever you cast a noncreature spell, put a +1/+1 counter on Vivi"
		Assert.That(s.Power(s.GetCard(vivi)), Nu.GreaterThan(0), "setup: she grew");
		Assert.That(Offered(s.SearchMoves()), Nu.True, "with power, offered");
	}

	[Test]
	public void Vivi_TheSearchsTurnWithoutASpell_NeverTakesHerManaMove()
	{
		SixLands();
		Field(Lsv("Vivi Ornitier"), me);
		Hand(TestGames.CreatureCosting("{4}{U}", "Big Blue", "5/5"), me);

		var played = SearchPlaysTheTurn();

		Assert.That(played, Has.Some.StartsWith("Cast Big Blue"), "setup: the turn was played");
		Assert.That(played, Has.None.StartsWith("Vivi Ornitier"));
	}

	// MC-9.5, watched 2026-11-23 on six lands: a mana move's mana floats and its counter or exhaust is spent for good,
	// yet the score sees neither, so every tie went to making it - the Prism's two counters removed with nothing to cast,
	// and both removed for a 7-drop that needed one; Loot exhausted for a 3-drop its lands paid. A mana move must now
	// beat what makes fewer (Shayne: free abilities have no opportunity cost - "you don't need to use them right away").
	int Prism(int counters)
	{
		var prism = Field(Lsv("Pentad Prism"), me);
		s = s.UpdateObject(prism, s.GetCard(prism) with { ChargeCounters = counters }); // TEST SETUP
		return prism;
	}

	[Test]
	public void PentadPrism_NothingToCast_ItsCountersAreKept()
	{
		SixLands();
		var prism = Prism(2);

		var played = SearchPlaysTheTurn();

		Assert.That(played, Has.None.StartsWith("Pentad Prism"));
		Assert.That(s.GetCard(prism).ChargeCounters, Nu.EqualTo(2));
	}

	[Test]
	public void PentadPrism_ASpellTheLandsPay_IsCastFromLands_ItsCountersKept()
	{
		SixLands();
		var prism = Prism(2);
		var bear = Hand(TestGames.CreatureCosting("{2}{B}", "Bear", "3/3"), me);

		SearchPlaysTheTurn();

		Assert.That(Alive(bear), Nu.True, "setup: cast");
		Assert.That(s.GetCard(prism).ChargeCounters, Nu.EqualTo(2));
	}

	[Test]
	public void PentadPrism_ASpellOneManaShort_SpendsOneCounter_NotBoth()
	{
		SixLands();
		var prism = Prism(2);
		var huge = Hand(TestGames.CreatureCosting("{6}{B}", "Huge", "7/7"), me);
		var missing = s.GetCard(huge).ManaCost.ManaValue - 6;

		SearchPlaysTheTurn();

		Assert.That(Alive(huge), Nu.True, "setup: cast with the Prism's help");
		Assert.That(s.GetCard(prism).ChargeCounters, Nu.EqualTo(2 - missing));
	}

	[Test]
	public void Loot_ASpellTheLandsPay_ItsExhaustManaIsKept()
	{
		SixLands("Forest");
		Field(Lsv("Loot, the Pathfinder"), me);
		var bear = Hand(TestGames.CreatureCosting("{2}{G}", "Bear", "3/3"), me);

		var played = SearchPlaysTheTurn();

		Assert.That(Alive(bear), Nu.True, "setup: cast");
		Assert.That(played, Has.None.StartsWith("Pay for and exhaust"));
	}

	// The rollout too: it exhausted Loot for White on its own turn 3 with nothing to spend, tapping it out of the attack.
	[Test]
	public void Loot_TheRolloutNeverMakesItsManaForNothing()
	{
		SixLands("Forest");
		var loot = Field(Lsv("Loot, the Pathfinder"), me);
		var made = 0;

		Rollout.Traced(
			(_, who, d) =>
				made += who == me && d.Action is TapForManaAction t && t.CardId == loot ? 1 : 0,
			() => Rollout.Score(s, me, Rollout.Horizon(s, Rollout.Default), Rollout.Default)
		);

		Assert.That(made, Nu.EqualTo(0));
	}

	// MC-9.6, watched 2026-11-23 on six lands: Zuran Orb ("Sacrifice a land: You gain 2 life") - at 6 life against 4
	// power the search sacrificed all six lands in its own turn; at 5 against 6 power the same; at 3 against a Lightning
	// Bolt it answered the Bolt, then went on to one land. Shayne: "The player effectively already has that life" - so
	// a note (Notes/FreeLifeGain): offered only when passing would lose, or the sacrifice triggers something of ours.
	void Orb(int life)
	{
		SixLands();
		Field(Lsv("Zuran Orb"), me);
		s = s.WithPlayer(s.GetPlayer(me) with { Life = life }); // TEST SETUP
	}

	int MyLands() => s.CardsIn(me, ZoneType.Battlefield).Count(c => c.Is(CardType.Land));

	void TheyHave(string size, int count)
	{
		for (var i = 0; i < count; i++)
			Field(TestGames.Creature($"Attacker {i}", size), them);
	}

	[Test]
	public void ZuranOrb_LowLife_NothingLethal_ItsLandsAreKept()
	{
		Orb(6);
		TheyHave("2/2", 2);
		var lands = MyLands();

		SearchPlaysTheTurn();

		Assert.That(MyLands(), Nu.EqualTo(lands));
		Assert.That(Life(me), Nu.EqualTo(6));
	}

	[Test]
	public void ZuranOrb_TheirLethalAttack_SacrificesOnlyWhatItTakesToSurvive()
	{
		Orb(5);
		TheyHave("3/3", 2);
		var lands = MyLands();

		SearchPlaysTheTurn(more: 1); // ours, then theirs: they attack for 6

		Assert.That(s.IsOver(), Nu.False, "survived");
		Assert.That(MyLands(), Nu.EqualTo(lands - 1), "one land: 5 + 2 is 7, past their 6");
	}

	[Test]
	public void ZuranOrb_ALethalBoltOnTheStack_OneSacrifice_ThenItHolds()
	{
		Orb(3);
		var bolt = Hand(Lsv("Lightning Bolt"), them);
		Field(TestGames.Card("Mountain"), them);
		var lands = MyLands();
		s = s.Act(new PassPriorityAction { PlayerId = me });
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = them,
				CardId = bolt,
				Targets = [me],
			}
		);
		s = s.Act(new PassPriorityAction { PlayerId = them });

		SearchPlaysTheTurn();

		Assert.That(s.IsOver(), Nu.False, "survived");
		Assert.That(MyLands(), Nu.EqualTo(lands - 1), "one land: 3 + 2 is 5, past the Bolt's 3");
	}

	[Test]
	public void ZuranOrb_WithTitania_TheSacrificeIsOffered_ForItsTokens()
	{
		Orb(20);
		Field(Lsv("Titania, Protector of Argoth"), me);
		var lands = MyLands();

		SearchPlaysTheTurn();

		Assert.That(MyLands(), Nu.LessThan(lands), "a 5/3 for each land");
	}

	[Test]
	public void ZuranOrb_TheRolloutSacrificesToSurviveTheirAttack()
	{
		Orb(5);
		TheyHave("3/3", 2);
		var made = 0;

		Rollout.Traced(
			(_, who, d) => made += who == me && d.Action is ActivateAbilityAction ? 1 : 0,
			() => Rollout.Score(s, me, Rollout.Horizon(s, Rollout.Default), Rollout.Default)
		);

		Assert.That(
			made,
			Nu.GreaterThan(0),
			"on their turn, at instant speed - our rollout plays only at sorcery speed"
		);
	}

	// MC-10: the card-value sandbox crashed on Pest Infestation ("Choose up to X target artifacts and/or enchantments")
	// with nothing to target - GreedyAi's "up to X" answer offered one target each and never none, so it was null.
	[Test]
	public void PestInfestation_NothingToTarget_GreedyAnswersNone()
	{
		SixLands("Forest");
		var pest = Hand(Lsv("Pest Infestation"), me);
		var cast = Casts(pest).First(c => c.X == 1);
		s = s.Act(cast);
		var targets = s.GetPendingChoice();
		Assert.That(
			targets,
			Is.TypeOf<ChooseXTargetsChoice>().And.Property("Options").Empty,
			"setup: asked, with nothing to target"
		);

		var answer = new GreedyAi(me).Answer(s, targets!); // the test helpers mask a null answer with the minimum

		Assert.That(answer, Is.Not.Null.And.Empty);
		s = s.Choose(answer);
		Resolve();
		Assert.That(
			s.CardsIn(me, ZoneType.Battlefield).Count(c => c.IsToken),
			Nu.EqualTo(2 * cast.X),
			"twice X Pests"
		);
	}
}
