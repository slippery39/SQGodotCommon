using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Rules;
using MagicCore.Sets.Fra;
using MagicCore.Turns;

namespace MagicCore.Tests.Sets;

/// <summary>Continuous effects (Rules/Characteristics.cs): each card's static ability applies — and stops.</summary>
public class FraStaticTests : FraBoardTest
{
	/// <summary>TEST SETUP: a creature that entered this turn — summoning sick.</summary>
	int Fresh(string pt, int player)
	{
		var id = Hand(TestGames.Creature($"Fresh{s.NextId}", pt), player);
		s = s.EnterBattlefield(id, player);
		return id;
	}

	[Test]
	public void Samut_GivesYourCreaturesHaste_AndStopsWhenSheLeaves()
	{
		var mine = Fresh("2/2", me);
		var theirs = Fresh("2/2", them);
		Assert.That(Combat.CanAttack(s, s.GetCard(mine)), Is.False, "sick, no Samut yet");

		var samut = Field(FraCards.Get("Samut, Hazoret's Champion"), me);
		Assert.That(Combat.CanAttack(s, s.GetCard(mine)));
		Assert.That(s.Has(s.GetCard(theirs), "Haste"), Is.False, "only yours");

		s = s.MoveCard(samut, s.ZoneId(me, ZoneType.Graveyard)); // TEST SETUP: she leaves
		Assert.That(
			Combat.CanAttack(s, s.GetCard(mine)),
			Is.False,
			"never stamped — gone with her"
		);
	}

	[Test]
	public void AjanisAnguish_DealsTheXPaid_AndGivesYourCreaturesTrample()
	{
		var mine = Field(TestGames.Creature("Mine", "2/2"), me);
		var theirs = Field(TestGames.Creature("Theirs", "2/2"), them);
		var anguish = Hand("Ajani's Anguish", me);
		var life = Life(them);

		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = anguish,
				X = 3,
			}
		);
		Resolve(choice =>
			choice switch
			{
				ChooseTargetChoice => [them],
				_ => [],
			}
		);

		Assert.That(Life(them), Is.EqualTo(life - 3));
		Assert.That(
			(s.Has(s.GetCard(mine), "Trample"), s.Has(s.GetCard(theirs), "Trample")),
			Is.EqualTo((true, false))
		);
	}

	[Test]
	public void Ghalta_CostsLessByYourGreatestPower_AndGivesOthersTrample()
	{
		var ghalta = Hand("Ghalta the Unstoppable", me);
		var printed = s.GetCard(ghalta).ManaCost.ManaValue;
		var big = Field(TestGames.Creature("Big", "5/5"), me);
		Assert.That(Costs.Of(s, me, s.GetCard(ghalta)).ManaValue, Is.EqualTo(printed - 5));

		s = s.Act(new CastSpellAction { PlayerId = me, CardId = ghalta });
		Resolve();
		Assert.That(s.Has(s.GetCard(big), "Trample"), "other creatures you control");
	}

	[Test]
	public void SkilledBattlecarver_FirstStrikeOnlyDuringYourTurn_AndPumps()
	{
		var carver = Field(FraCards.Get("Skilled Battlecarver"), me);
		Assert.That(s.Has(s.GetCard(carver), "First strike"));

		var power = s.Power(s.GetCard(carver));
		s = s.Act(new ActivateAbilityAction { PlayerId = me, CardId = carver });
		Resolve();
		Assert.That(s.Power(s.GetCard(carver)), Is.EqualTo(power + 1));

		s = TestGames.PassUntil(
			s,
			x => x.GetGame().ActivePlayerId == them && x.GetGame().Step == Step.Upkeep
		);
		Assert.That(s.Has(s.GetCard(carver), "First strike"), Is.False, "their turn");
	}

	[Test]
	public void MindMeanderer_VigilanceOnlyWithAJace_AndFightsOnEntering()
	{
		var victim = Field(TestGames.Creature("Victim", "3/3"), them);
		var meanderer = Hand("Mind Meanderer", me);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = meanderer });
		Resolve(choice =>
			choice switch
			{
				ChooseTargetChoice => [victim],
				_ => [],
			}
		);

		Assert.That(Alive(victim), Is.False, "fought: 4 damage");
		Assert.That(s.Has(s.GetCard(meanderer), "Vigilance"), Is.False, "no Jace yet");

		Cast(Hand("Protege's Awakening", me), []); // Empower Jace: a Jace planeswalker
		Assert.That(s.Has(s.GetCard(meanderer), "Vigilance"));
	}

	[Test]
	public void Winter_GrowsWithCreatureAndWalkerCards_AndHerSacrificeForcesTheirs()
	{
		var fodder = Field(TestGames.Creature("Fodder", "1/1"), me);
		var theirs = Field(TestGames.Creature("Theirs", "2/2"), them);
		var winter = Hand("Winter, Tormented Loner", me);
		var deciders = new List<int>();

		s = s.Act(new CastSpellAction { PlayerId = me, CardId = winter });
		Resolve(choice =>
		{
			deciders.Add(s.DecidingPlayerId());
			return choice switch
			{
				ChoosePermanentChoice { MinChoices: 0 } => [fodder],
				_ => choice.Options.Take(choice.MinChoices).Select(o => o.Id),
			};
		});

		Assert.That((Alive(fodder), Alive(theirs)), Is.EqualTo((false, false)));
		Assert.That(deciders, Is.EqualTo(new[] { me, them }), "they choose their own sacrifice");
		var cards = s.CardsIn(me, ZoneType.Graveyard)
			.Count(c => c.Is(CardType.Creature | CardType.Planeswalker));
		Assert.That(s.Power(s.GetCard(winter)), Is.EqualTo(s.GetCard(winter).BasePower + cards));
	}

	[Test]
	public void TetsukoFugitive_SmallCreaturesOfYoursCantBeBlocked()
	{
		var tetsuko = Field(FraCards.Get("Tetsuko Umezawa, Fugitive"), me); // 1/3: power 1
		var bear = Field(TestGames.Creature("Bear", "2/2"), me);
		var wall = Field(TestGames.Creature("Wall", "0/5"), me); // toughness 5, power 0
		var blocker = Field(TestGames.Creature("Blocker", "5/5"), them);
		var theirSmall = Field(TestGames.Creature("TheirSmall", "1/1"), them);

		Assert.That(Combat.CanBlock(s, s.GetCard(blocker), s.GetCard(tetsuko)), Is.False);
		Assert.That(Combat.CanBlock(s, s.GetCard(blocker), s.GetCard(wall)), Is.False, "power 0");
		Assert.That(
			Combat.CanBlock(s, s.GetCard(blocker), s.GetCard(bear)),
			"2/2: neither is 1 or less"
		);
		Assert.That(s.Has(s.GetCard(theirSmall), Combat.CantBeBlocked), Is.False, "only yours");
	}

	[Test]
	public void EardrumRattler_MakesAnotherSmallCreatureUnblockableThisTurn()
	{
		var rattler = Field(FraCards.Get("Eardrum Rattler"), me);
		var bear = Field(TestGames.Creature("Bear", "2/2"), me);
		var big = Field(TestGames.Creature("Big", "3/3"), me);
		var targets = s.LegalActions()
			.OfType<ActivateAbilityAction>()
			.Where(a => a.CardId == rattler)
			.Select(a => a.Targets[0]);
		Assert.That(targets, Does.Contain(bear).And.Not.Contain(big).And.Not.Contain(rattler));

		s = s.Act(
			new ActivateAbilityAction
			{
				PlayerId = me,
				CardId = rattler,
				Targets = [bear],
			}
		);
		Resolve();
		Assert.That(s.Has(s.GetCard(bear), Combat.CantBeBlocked));

		s = TestGames.PassUntil(s, x => x.GetGame().ActivePlayerId == them);
		Assert.That(s.Has(s.GetCard(bear), Combat.CantBeBlocked), Is.False, "this turn only");
	}

	// ===== Found by review =====

	// Tetsuko read PRINTED power: a Winter grown to 4 power still counted as "1 or less".
	[Test]
	public void TetsukoFugitive_ReadsRealPower_NotPrinted()
	{
		Field(FraCards.Get("Tetsuko Umezawa, Fugitive"), me);
		var winter = Field(FraCards.Get("Winter, Tormented Loner"), me); // printed 0/3
		for (var i = 0; i < 4; i++)
			Hand(TestGames.Creature($"Dead{i}", "1/1"), me);
		foreach (
			var dead in s.CardsIn(me, ZoneType.Hand).Where(c => c.Name.StartsWith("Dead")).ToList()
		)
			s = s.MoveCard(dead.Id, s.ZoneId(me, ZoneType.Graveyard)); // TEST SETUP: four creature cards
		var blocker = Field(TestGames.Creature("Blocker", "5/5"), them);

		Assert.That(s.Power(s.GetCard(winter)), Is.EqualTo(4));
		Assert.That(Combat.CanBlock(s, s.GetCard(blocker), s.GetCard(winter)), "4/3 is not small");
	}

	// The X paid for a permanent reached every ability of it, not only its own "enters" trigger.
	[Test]
	public void CastX_ReachesOnlyTheEntersTrigger()
	{
		var pinger = new CardDefinition(
			new CardData(
				"Pinger",
				"normal",
				"{X}",
				0,
				"Creature — Test",
				null,
				"1",
				"1",
				null,
				null,
				[],
				"common",
				"0",
				null
			),
			[
				new Effects.ActivatedAbilityComponent(
					ManaCost.None,
					[new Effects.DealDamage(Effects.Ref.EachOpponent, Effects.Count.X)]
				),
			]
		);
		var id = Hand(pinger, me);
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = id,
				X = 3,
			}
		);
		Resolve();
		var life = Life(them);

		s = s.Act(new ActivateAbilityAction { PlayerId = me, CardId = id });
		Resolve();

		Assert.That(Life(them), Is.EqualTo(life), "an activated ability's X is 0, not the cast X");
	}

	// The AI valued creatures by printed numbers, so a static that grew one was invisible to it.
	[Test]
	public void TheAiValuesWhatStaticsGive()
	{
		var ai = new global::MagicCore.Ai.GreedyAi(me);
		var winter = Field(FraCards.Get("Winter, Tormented Loner"), me);
		var before = ai.Evaluate(s);
		var dead = Hand(TestGames.Creature("Dead", "1/1"), me);
		s = s.MoveCard(dead, s.ZoneId(me, ZoneType.Graveyard)); // TEST SETUP: Winter is now 1/3
		Assert.That(s.Power(s.GetCard(winter)), Is.EqualTo(1));

		Assert.That(ai.Evaluate(s), Is.GreaterThan(before));
	}
}
