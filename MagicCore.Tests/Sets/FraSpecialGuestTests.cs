using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Rules;
using MagicCore.Sets.Fra;

namespace MagicCore.Tests.Sets;

/// <summary>
/// The FRA Special Guests (SPG #159–168, story MC-3) whose scripts use a new piece or option: each
/// FIRES, through real casting and the real stack. Consider and Austere Command are made of tested
/// pieces — their card-sheet entries are the check.
/// </summary>
public class FraSpecialGuestTests : FraBoardTest
{
	static CardDefinition Eldrazi(string name, params string[] colors) =>
		TestGames.Inline(name, "{4}", "Creature — Eldrazi") with
		{
			Data = TestGames.Inline(name, "{4}", "Creature — Eldrazi").Data with
			{
				Colors = colors,
				Power = "4",
				Toughness = "4",
			},
		};

	void ToTheirTurn() => s = TestGames.PassUntil(s, x => x.GetGame().ActivePlayerId == them);

	// ===== Mind Twist =====

	[Test]
	public void MindTwist_DiscardsXCardsAtRandom()
	{
		for (var i = 0; i < 5; i++)
			Hand(TestGames.Creature($"Card {i}", "1/1"), them);
		var twist = Hand("Mind Twist", me);
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = twist,
				X = 3,
				Targets = [them],
			}
		);
		var hand = HandCount(them);
		Resolve();
		Assert.That(
			(HandCount(them), s.CardsIn(them, ZoneType.Graveyard).Count()),
			Is.EqualTo((hand - 3, 3))
		);
	}

	// ===== Eye of Ugin =====

	[Test]
	public void EyeOfUgin_ColorlessEldraziCostLess_OthersDont()
	{
		Field(FraCards.Get("Eye of Ugin"), me);
		var spawn = Hand(Eldrazi("Spawn"), me);
		var greenSpawn = Hand(Eldrazi("Green Spawn", "G"), me);
		var (colorless, green) = (s.GetCard(spawn), s.GetCard(greenSpawn));
		Assert.That(
			Costs.Of(s, me, colorless).ManaValue,
			Is.LessThan(colorless.ManaCost.ManaValue)
		);
		Assert.That(Costs.Of(s, me, green).ManaValue, Is.EqualTo(green.ManaCost.ManaValue));
	}

	[Test]
	public void EyeOfUgin_SearchesForAColorlessCreatureOnly()
	{
		var eye = Field(FraCards.Get("Eye of Ugin"), me);
		(s, _) = TestGames.Put(s, me, Eldrazi("Spawn"), ZoneType.Library);
		(s, _) = TestGames.Put(s, me, Eldrazi("Green Spawn", "G"), ZoneType.Library);
		s = s.Act(Activations(eye).Single());
		IEnumerable<string> offered = [];
		Resolve(choice =>
		{
			if (choice is not ChooseFromLibraryChoice)
				return choice.Options.Take(choice.MinChoices).Select(o => o.Id);
			offered = [.. choice.Options.Select(o => o.DisplayText)];
			return choice.Options.Where(o => o.DisplayText == "Spawn").Select(o => o.Id);
		});
		Assert.That(offered, Does.Contain("Spawn").And.Not.Contain("Green Spawn"));
		Assert.That(s.CardsIn(me, ZoneType.Hand).Select(c => c.Name), Does.Contain("Spawn"));
	}

	// ===== Root Maze =====

	[Test]
	public void RootMaze_EveryonesArtifactsEnterTapped_CreaturesDont()
	{
		Field(FraCards.Get("Root Maze"), them);
		var trinket = Hand(TestGames.Inline("Trinket", "{1}", "Artifact"), me);
		Cast(trinket, []);
		var bear = Hand(TestGames.Creature("Bear", "2/2"), me);
		Cast(bear, []);
		Assert.That(
			(s.GetCard(trinket).IsTapped, s.GetCard(bear).IsTapped),
			Is.EqualTo((true, false))
		);
	}

	// ===== Splinter Twin =====

	[Test]
	public void SplinterTwin_EnchantedCreatureMakesAHastyCopy_ExiledAtTheEndStep()
	{
		var bear = Field(TestGames.Creature("Bear", "2/2"), me);
		Cast(Hand("Splinter Twin", me), [], bear);
		s = s.Act(Activations(bear).Single());
		Resolve();
		var token = s.CardsIn(me, ZoneType.Battlefield).Single(c => c.IsToken);
		Assert.That(
			(token.Name, s.Has(token, "Haste"), s.GetCard(bear).IsTapped),
			Is.EqualTo(("Bear", true, true))
		);
		Assert.That(Activations(token.Id), Is.Empty, "the copy isn't enchanted");
		ToTheirTurn();
		Assert.That(s.CardsIn(me, ZoneType.Battlefield).Where(c => c.IsToken), Is.Empty);
	}

	// ===== batch 2 helpers =====

	static CardDefinition Herald =>
		TestGames.Creature("Herald", "2/2") with
		{
			Components =
			[
				new TriggeredAbilityComponent(TriggerEvent.SelfEnters, [new GainLife(Ref.You, 3)]),
			],
		};

	IEnumerable<GameObject> Stack => s.GetChildren(s.GetWellKnownId(MagicKeys.Stack));

	/// <summary>I cast a Herald; its "enters" trigger waits on the stack, and I have priority.</summary>
	int HeraldTriggerOnTheStack()
	{
		var herald = Hand(Herald, me);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = herald });
		s = TestGames.PassUntil(
			s,
			x => x.GetChildren(x.GetWellKnownId(MagicKeys.Stack)).OfType<StackAbility>().Any()
		);
		return Stack.OfType<StackAbility>().Single().Id;
	}

	static CardDefinition Instant(string name, params string[] colors) =>
		TestGames.Inline(name, "{1}", "Instant") with
		{
			Data = TestGames.Inline(name, "{1}", "Instant").Data with { Colors = colors },
		};

	void ToMyNextMain() =>
		s = TestGames.PassUntil(
			s,
			x =>
				x.GetGame().ActivePlayerId == me
				&& x.GetGame().Step == Step.PreCombatMain
				&& x.StackIsEmpty()
				&& x.GetGame().TurnNumber > 1
		);

	// ===== Necrodominance =====

	[Test]
	public void Necrodominance_SkipsYourDrawStep()
	{
		Field(FraCards.Get("Necrodominance"), me);
		ToTheirTurn();
		var hand = HandCount(me);
		ToMyNextMain();
		Assert.That(HandCount(me), Is.EqualTo(hand));
	}

	[Test]
	public void Necrodominance_AtYourEndStep_PayLifeDrawThatMany()
	{
		Field(FraCards.Get("Necrodominance"), me);
		s = TestGames.PassUntil(s, x => x.GetPendingChoice() is ChooseNumberChoice);
		var (life, hand) = (Life(me), HandCount(me));
		Assert.That(
			s.GetPendingChoice()!.Options.Max(o => int.Parse(o.DisplayText)),
			Is.LessThan(life),
			"never your whole life: that loses the game"
		);
		s = s.Choose([4]); // the number 3
		Assert.That((Life(me), HandCount(me)), Is.EqualTo((life - 3, hand + 3)));
	}

	[Test]
	public void Necrodominance_MaximumHandSizeFive()
	{
		Field(FraCards.Get("Necrodominance"), me);
		for (var i = 0; i < 8; i++)
			Hand(TestGames.Creature($"Card {i}", "1/1"), me);
		ToTheirTurn();
		Assert.That(HandCount(me), Is.EqualTo(5));
	}

	[Test]
	public void Necrodominance_YourCardsAreExiledInsteadOfGoingToYourGraveyard()
	{
		Field(FraCards.Get("Necrodominance"), me);
		var bear = Field(TestGames.Creature("Bear", "2/2"), me);
		var kill = Hand(TestCards.Kill(), me);
		Cast(kill, [], bear);
		Assert.That(
			(s.IsIn(bear, me, ZoneType.Exile), s.IsIn(kill, me, ZoneType.Exile)),
			Is.EqualTo((true, true))
		);
		Assert.That(s.CardsIn(me, ZoneType.Graveyard), Is.Empty);
	}

	// ===== Flesh Duplicate =====

	[Test]
	public void FleshDuplicate_EntersAsACopyWithVanishing_AndIsItselfAgainWhenItGoes()
	{
		var ogre = Field(TestGames.Creature("Ogre", "3/3"), them);
		var duplicate = Hand("Flesh Duplicate", me);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = duplicate });
		Resolve(choice =>
			choice switch
			{
				ChooseCreatureToCopyChoice => [ogre],
				_ => choice.Options.Take(choice.MinChoices).Select(o => o.Id),
			}
		);
		var copy = s.GetCard(duplicate);
		Assert.That(
			(Alive(duplicate), copy.Name, s.Power(copy), copy.TimeCounters),
			Is.EqualTo((true, "Ogre", 3, 3))
		);
		s = s.UpdateObject(duplicate, copy with { TimeCounters = 1 });
		ToMyNextMain();
		Assert.That(Alive(duplicate), Is.False, "the last time counter removed: sacrificed");
		Assert.That(s.GetCard(duplicate).Name, Is.EqualTo("Flesh Duplicate"));
	}

	/// <summary>Cleanup ends "until end of turn" copies only (review, 2026-10-23: it undid this one).</summary>
	[Test]
	public void FleshDuplicate_DamagedStaysACopyThroughCleanup()
	{
		var ogre = Field(TestGames.Creature("Ogre", "3/3"), them);
		var duplicate = Hand("Flesh Duplicate", me);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = duplicate });
		Resolve(); // the default answer — the AI's look-ahead's — copies: the choice is a must
		s = s.UpdateObject(duplicate, s.GetCard(duplicate) with { Damage = 1 });
		ToTheirTurn();
		Assert.That((Alive(duplicate), s.GetCard(duplicate).Name), Is.EqualTo((true, "Ogre")));
		Assert.That(Alive(ogre));
	}

	[Test]
	public void FleshDuplicate_CopyingAVanishingCopy_KeepsItsTimeCounters()
	{
		Field(TestGames.Creature("Ogre", "3/3"), them);
		var first = Hand("Flesh Duplicate", me);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = first });
		Resolve();
		var second = Hand("Flesh Duplicate", me);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = second });
		Resolve(choice =>
			choice switch
			{
				ChooseCreatureToCopyChoice => [first],
				_ => choice.Options.Take(choice.MinChoices).Select(o => o.Id),
			}
		);
		Assert.That(s.GetCard(second).TimeCounters, Is.EqualTo(s.GetCard(first).TimeCounters));
		Assert.That(
			s.GetCard(second).TimeCounters,
			Is.GreaterThan(0),
			"it has vanishing, so it leaves"
		);
	}

	// ===== Consign to Memory =====

	[Test]
	public void ConsignToMemory_CountersATriggeredAbility()
	{
		var trigger = HeraldTriggerOnTheStack();
		var life = Life(me);
		var consign = Hand("Consign to Memory", me);
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = consign,
				Targets = [trigger],
			}
		);
		Resolve();
		Assert.That(Life(me), Is.EqualTo(life));
	}

	[Test]
	public void ConsignToMemory_TargetsAColorlessSpell_NotAColouredOne()
	{
		var consign = Hand("Consign to Memory", me);
		var colorless = TheirSpellOnTheStack(Instant("Grey Bolt"));
		Assert.That(Casts(consign).Select(c => c.Targets[0]), Does.Contain(colorless));
		Resolve();
		consign = Hand("Consign to Memory", me);
		var red = TheirSpellOnTheStack(Instant("Red Bolt", "R"));
		Assert.That(Casts(consign).Select(c => c.Targets[0]), Does.Not.Contain(red));
	}

	[Test]
	public void ConsignToMemory_Replicate_PaysAgainAndCopiesIt()
	{
		var trigger = HeraldTriggerOnTheStack();
		var consign = Hand("Consign to Memory", me);
		Assert.That(Casts(consign).Select(c => c.Replicate), Does.Contain(1));
		var untapped = s.CardsIn(me, ZoneType.Battlefield).Count(c => !c.IsTapped);
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = consign,
				Targets = [trigger],
				Replicate = 1,
			}
		);
		Assert.That(
			s.CardsIn(me, ZoneType.Battlefield).Count(c => !c.IsTapped),
			Is.EqualTo(untapped - 2),
			"{U} and one {1} for replicate"
		);
		var copies = 0;
		for (var i = 0; i < 100 && !s.StackIsEmpty(); i++)
		{
			copies = Math.Max(copies, Stack.OfType<Card>().Count(c => c.IsCopy));
			s = TestGames.PassOrChoose(s);
		}
		Assert.That(copies, Is.EqualTo(1));
	}

	// ===== Sublime Epiphany =====

	[Test]
	public void SublimeEpiphany_ChooseOneOrMore()
	{
		var sublime = Hand("Sublime Epiphany", me);
		var spell = TheirSpellOnTheStack(Instant("Red Bolt", "R"));
		Assert.That(
			Casts(sublime).Select(c => c.Modes.Length).Distinct().Order(),
			Is.EqualTo(new[] { 1, 2 }),
			"one mode or two: only the spell and a player have targets here"
		);
		var hand = HandCount(me);
		Cast(sublime, [0, 4], spell, me);
		Assert.That(
			(s.IsIn(spell, them, ZoneType.Graveyard), HandCount(me)),
			Is.EqualTo((true, hand))
		);
	}
}
