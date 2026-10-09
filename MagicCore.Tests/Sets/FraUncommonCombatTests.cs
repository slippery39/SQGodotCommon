using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Sets.Fra;
using MagicCore.Turns;

namespace MagicCore.Tests.Sets;

/// <summary>Uncommons (7e-2e): combat and the rest — each ability fires.</summary>
public class FraUncommonCombatTests : FraBoardTest
{
	static CardDefinition Trick =>
		TestGames.Inline("Trick", "{1}", "Instant", null, new GainLife(Ref.You, 1));

	static CardDefinition Colored(CardDefinition card, params string[] colors) =>
		card with
		{
			Data = card.Data with { Colors = colors },
		};

	/// <summary>Declares attackers: each creature named attacks what it maps to; the rest stay home.</summary>
	void Attack(Dictionary<int, int> at)
	{
		s = TestGames.PassUntil(s, x => x.IsWaitingForChoice);
		while (s.GetPendingChoice() is DeclareAttackerChoice declare)
			s = s.Choose([at.GetValueOrDefault(declare.CreatureId, CombatOption.None)]);
	}

	void ToMainTwo() => s = TestGames.PassUntil(s, x => x.GetGame().Step == Step.PostCombatMain);

	void TapAllLands(int player)
	{
		foreach (
			var land in s.CardsIn(player, ZoneType.Battlefield)
				.Where(c => c.Is(CardType.Land))
				.ToList()
		)
			s = s.UpdateObject(land.Id, land with { IsTapped = true }); // TEST SETUP
	}

	int Walker(int player, int loyalty, params LoyaltyAbilityComponent[] abilities)
	{
		var walker = Field(TestGames.Planeswalker("Walker", loyalty, abilities), player);
		s = s.UpdateObject(walker, s.GetCard(walker) with { Loyalty = loyalty }); // TEST SETUP: Put skips entering
		return walker;
	}

	[Test]
	public void Yuriko_AttackingAloneGivesDoubleStrike()
	{
		Field(FraCards.Get("Yuriko, Blade of the Mighty"), me);
		var bear = Field(TestGames.Creature("Bear", "2/2"), me);
		var life = Life(them);
		Attack(new() { [bear] = them });
		ToMainTwo();
		Assert.That(Life(them), Is.EqualTo(life - 2 * 2));
	}

	[Test]
	public void Yuriko_NoSpellsDuringCombat_ButBeforeIt()
	{
		Field(FraCards.Get("Yuriko, Blade of the Mighty"), me);
		var trick = Hand(Trick, me);
		Assert.That(Casts(trick), Is.Not.Empty, "main phase");
		s = TestGames.PassUntil(
			s,
			x => x.GetGame().Step == Step.BeginCombat && x.GetGame().PriorityPlayerId == me
		);
		Assert.That(Casts(trick), Is.Empty);
	}

	[Test]
	public void TomikOrzhov_OnlyOneCreatureMayAttackTheirPlaneswalker()
	{
		Field(FraCards.Get("Tomik, Orzhov Lawmage"), them);
		var walker = Walker(them, 5);
		var a = Field(TestGames.Creature("A", "2/2"), me);
		var b = Field(TestGames.Creature("B", "2/2"), me);
		s = TestGames.PassUntil(s, x => x.IsWaitingForChoice);
		var first = (DeclareAttackerChoice)s.GetPendingChoice()!;
		Assert.That(first.Options.Select(o => o.Id), Does.Contain(walker));
		s = s.Choose([walker]);
		var second = (DeclareAttackerChoice)s.GetPendingChoice()!;
		Assert.That(second.CreatureId, Is.EqualTo(first.CreatureId == a ? b : a));
		Assert.That(second.Options.Select(o => o.Id), Does.Not.Contain(walker).And.Contain(them));
	}

	[Test]
	public void TomikOrzhov_FlyingForACreatureWithACounter()
	{
		var tomik = Field(FraCards.Get("Tomik, Orzhov Lawmage"), me);
		var plain = Field(TestGames.Creature("Plain", "2/2"), me);
		var grown = Field(TestGames.Creature("Grown", "2/2"), me);
		s = s.UpdateObject(grown, s.GetCard(grown) with { PlusOneCounters = 1 }); // TEST SETUP
		Assert.That(
			Activations(tomik).Select(x => x.Targets[0]),
			Does.Contain(grown).And.Not.Contain(plain)
		);
		s = s.Act(Activations(tomik).Single(x => x.Targets[0] == grown));
		Resolve();
		Assert.That(s.Has(s.GetCard(grown), "Flying"));
	}

	[Test]
	public void KioraSaltAndSand_AfterALoyaltyAbility_AttackingUntapsAndMakesUnblockable()
	{
		Field(FraCards.Get("Kiora of Salt and Sand"), me);
		var walker = Walker(me, 3, new LoyaltyAbilityComponent(1, []));
		var bear = Field(TestGames.Creature("Bear", "2/2"), me);
		s = s.Act(s.LegalActions().OfType<ActivateLoyaltyAction>().First(x => x.CardId == walker));
		Resolve();
		Attack(new() { [bear] = them });
		Resolve(choice =>
			choice switch
			{
				ChooseTargetChoice => [bear],
				_ => [],
			}
		);
		var card = s.GetCard(bear);
		Assert.That((card.IsTapped, s.Has(card, Combat.CantBeBlocked)), Is.EqualTo((false, true)));
	}

	[Test]
	public void KioraSaltAndSand_NoLoyaltyAbility_NoTrigger()
	{
		Field(FraCards.Get("Kiora of Salt and Sand"), me);
		var bear = Field(TestGames.Creature("Bear", "2/2"), me);
		Attack(new() { [bear] = them });
		Resolve();
		Assert.That(s.GetCard(bear).IsTapped, "an intervening if: nothing happens");
	}

	[Test]
	public void KioraSaltAndSand_GivesYourPlaneswalkersALeviathan()
	{
		Field(FraCards.Get("Kiora of Salt and Sand"), me);
		var walker = Walker(me, 8);
		s = s.Act(s.LegalActions().OfType<ActivateLoyaltyAction>().Single(x => x.CardId == walker));
		Resolve();
		var leviathan = s.CardsIn(me, ZoneType.Battlefield).Single(c => c.Name == "Leviathan");
		Assert.That((s.Power(leviathan), s.Has(leviathan, "Hexproof")), Is.EqualTo((8, true)));
	}

	[Test]
	public void Winter_NoncreatureSpellsPumpYourTeam()
	{
		Field(FraCards.Get("Winter, Team Player"), me);
		var bear = Field(TestGames.Creature("Bear", "2/2"), me);
		Cast(Hand(Trick, me), []);
		Assert.That(s.Power(s.GetCard(bear)), Is.EqualTo(2 + 1));
	}

	[Test]
	public void Winter_ConvokeTapsCreaturesForGenericAndTheirColour()
	{
		var winter = Hand("Winter, Team Player", me);
		TapAllLands(me);
		var creatures = new[]
		{
			Field(Colored(TestGames.Creature("Red", "1/1"), "R"), me),
			Field(TestGames.Creature("A", "1/1"), me),
			Field(TestGames.Creature("B", "1/1"), me),
			Field(TestGames.Creature("C", "1/1"), me),
			Field(TestGames.Creature("D", "1/1"), me),
		};
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = winter });
		Assert.That(creatures.All(c => s.GetCard(c).IsTapped), "four for {4}, the red one for {R}");
	}

	[Test]
	public void Winter_ConvokeCantPayAColourNoCreatureHas()
	{
		var winter = Hand("Winter, Team Player", me);
		TapAllLands(me);
		for (var i = 0; i < 5; i++)
			Field(TestGames.Creature("Grey", "1/1"), me);
		Assert.That(Casts(winter), Is.Empty, "colourless creatures pay generic only");
	}

	[Test]
	public void Winter_AManaCreatureConvokesForItsOwnColour()
	{
		var winter = Hand("Winter, Team Player", me);
		TapAllLands(me);
		var dork = Colored(TestGames.Creature("Red Dork", "1/1"), "R");
		Field(dork with { Components = [new ManaAbilityComponent(ManaColor.Green)] }, me);
		for (var i = 0; i < 4; i++)
			Field(TestGames.Creature("Grey", "1/1"), me);
		Assert.That(Casts(winter), Is.Not.Empty, "it makes {G}, but convokes for {R}");
	}

	[Test]
	public void Convoke_CreaturesCountTowardX()
	{
		var flood = Hand(
			TestGames.Inline("Flood", "{X}", "Sorcery", ["Convoke"], new GainLife(Ref.You, 1)),
			me
		);
		TapAllLands(me);
		for (var i = 0; i < 3; i++)
			Field(TestGames.Creature("Grey", "1/1"), me);
		Assert.That(Casts(flood).Max(c => c.X), Is.EqualTo(3));
	}

	[Test]
	public void CombatDamage_ToAPlaneswalkerTokenThatAlreadyDied_IsNoCrash()
	{
		var walker = Walker(them, 1);
		s = s.UpdateObject(walker, s.GetCard(walker) with { IsToken = true }); // TEST SETUP: a Jace-like token
		var striker = Field(TestGames.Creature("Striker", "1/1", "First strike"), me);
		var bear = Field(TestGames.Creature("Bear", "2/2"), me);
		Attack(new() { [striker] = walker, [bear] = walker });
		ToMainTwo(); // first strike kills the token, which ceases to exist before the bear's damage
		Assert.That(s.HasObject(walker), Is.False);
	}

	[Test]
	public void FblthpLost_CombatDamageDrawsTwo_ThenHeShufflesIn()
	{
		var fblthp = Field(FraCards.Get("Fblthp, Impossibly Lost"), me);
		var bear = Field(TestGames.Creature("Bear", "2/2"), me);
		var hand = HandCount(me);
		Attack(new() { [bear] = them });
		ToMainTwo();
		Assert.That(HandCount(me), Is.EqualTo(hand + 2));
		Assert.That(s.IsIn(fblthp, me, ZoneType.Library));
	}

	[Test]
	public void FblthpLost_AnEmptyLibraryWins()
	{
		Field(FraCards.Get("Fblthp, Impossibly Lost"), me);
		var bear = Field(TestGames.Creature("Bear", "2/2"), me);
		foreach (var card in s.CardsIn(me, ZoneType.Library).ToList())
			s = s.MoveObject(card.Id, s.ZoneId(me, ZoneType.Exile)); // TEST SETUP: an empty library
		Attack(new() { [bear] = them });
		s = TestGames.PassUntil(s, x => x.IsOver());
		Assert.That(s.WinnerId(), Is.EqualTo(me));
	}

	[Test]
	public void FblthpKnowsTheWay_DomainPower_AndUpToXBasicsWithDifferentNames()
	{
		int plains;
		(s, plains) = TestGames.Put(s, me, TestGames.Card("Plains"), ZoneType.Library);
		var fblthp = Hand("Fblthp, Knows the Way", me);
		int LandsInHand() => s.CardsIn(me, ZoneType.Hand).Count(c => c.Is(CardType.Land));
		var lands = LandsInHand();
		IEnumerable<int> offered = [];
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = fblthp,
				X = 2,
			}
		);
		Resolve(choice =>
		{
			if (choice is not ChooseFromLibraryChoice)
				return [];
			offered = choice.Options.Select(o => o.Id).ToList();
			Assert.That(choice.MaxChoices, Is.EqualTo(2));
			return offered.Take(2);
		});
		Assert.That(s.Power(s.GetCard(fblthp)), Is.EqualTo(5), "all five basic types on the board");
		Assert.That(
			offered.Select(id => s.GetCard(id).Name),
			Is.EqualTo(new[] { "Forest", "Plains" })
		);
		Assert.That(LandsInHand(), Is.EqualTo(lands + 2), "one of each name");
	}
}
