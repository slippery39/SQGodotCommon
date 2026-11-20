using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Targeting;
using Nu = NUnit.Framework.Is;

namespace MagicCore.Tests.Sets;

/// <summary>
/// MC-4.4h2d, LSVPower — black's triggers: magecraft's "cast or copy" (Sedgemoor Witch, a copy's own event) and its
/// ward paid in life; an opponent's draw but the first of their draw step (Orcish Bowmasters) and amass; odd or even
/// (Gollum, Riddle Master); an exile whose permission is decided after (Black Widow, Super Spy).
/// </summary>
public class LsvBatch2h2dTests : FraBoardTest
{
	static CardDefinition Bear => TestGames.Creature("Bear", "2/2");

	int Count(int player, string name) =>
		s.CardsIn(player, ZoneType.Battlefield).Count(c => c.Name == name);

	Card? Army(int player) =>
		s.CardsIn(player, ZoneType.Battlefield).SingleOrDefault(c => c.Name == "Orc Army");

	// ===== Sedgemoor Witch =====

	[Test]
	public void SedgemoorWitch_Magecraft_APestPerCastAndPerCopy()
	{
		Field(Lsv("Sedgemoor Witch"), me);
		var bear = Field(Bear, them);
		var shock = Hand(TestCards.Shock(1), me);
		var twin = Hand(
			TestCards.Targeted("Twin", TargetSpec.Spell(), new CopySpell(Ref.Target())),
			me
		);
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = shock,
				Targets = [bear],
			}
		);
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = twin,
				Targets = [shock],
			}
		);
		Resolve(On<ChooseTargetChoice>(bear));
		Assert.That(Count(me, "Pest"), Nu.EqualTo(3), "Shock cast, Twin cast, Shock copied");
		Assert.That(Alive(bear), Nu.False, "two 1-damage Shocks");
	}

	[Test]
	public void SedgemoorWitch_ACreatureSpellOrTheirInstant_NoPest_APestDiesForALife()
	{
		Field(Lsv("Sedgemoor Witch"), me);
		Cast(Hand(Bear, me), []);
		TheirSpellOnTheStack(TestGames.Inline("Theirs", "{U}", "Instant"));
		Resolve();
		Assert.That(Count(me, "Pest"), Nu.EqualTo(0), "you cast an instant or sorcery");
		Cast(Hand(TestGames.Inline("Mine", "{0}", "Sorcery"), me), []);
		var pest = s.CardsIn(me, ZoneType.Battlefield).Single(c => c.Name == "Pest").Id;
		var life = Life(me);
		Cast(Hand(TestCards.Kill(), me), [], pest);
		Assert.That(Life(me), Nu.EqualTo(life + 1), "when this token dies, you gain 1 life");
	}

	[Test]
	public void SedgemoorWitch_WardPayThreeLife()
	{
		var witch = Field(Lsv("Sedgemoor Witch"), me);
		var kill = Hand(TestCards.Kill(), them);
		s = s.Act(new PassPriorityAction { PlayerId = me });
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = them,
				CardId = kill,
				Targets = [witch],
			}
		);
		var life = Life(them);
		Resolve(On<PayToKeepChoice>(1));
		Assert.That(Life(them), Nu.EqualTo(life - 3), "pay 3 life");
		Assert.That(Alive(witch), Nu.False, "paid: the Kill resolves");

		var second = Field(Lsv("Sedgemoor Witch"), me);
		var again = Hand(TestCards.Kill(), them);
		s = s.Act(new PassPriorityAction { PlayerId = me });
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = them,
				CardId = again,
				Targets = [second],
			}
		);
		Resolve(On<PayToKeepChoice>());
		Assert.That(Alive(second), "not paid: countered");
		Assert.That(Life(them), Nu.EqualTo(life - 3));
	}

	// ===== Orcish Bowmasters =====

	[Test]
	public void OrcishBowmasters_Enters_OneDamage_ThenAmassMakesAnArmy()
	{
		var elf = Field(TestGames.Creature("Elf", "1/1"), them);
		var bowmasters = Hand(Lsv("Orcish Bowmasters"), me);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = bowmasters });
		Resolve(Targeting(elf, true));
		Assert.That(Alive(elf), Nu.False, "1 damage to any target");
		Assert.That(Army(me), Nu.Not.Null, "no Army: a 0/0 Orc Army first");
		Assert.That(s.Power(Army(me)!), Nu.EqualTo(1), "then a +1/+1 counter on it");
	}

	[Test]
	public void OrcishBowmasters_AnOpponentsDraws_ButNotTheFirstOfTheirDrawStep()
	{
		Field(Lsv("Orcish Bowmasters"), me);
		var life = Life(them);
		Cast(Hand(Lsv("Ancestral Recall"), me), [], me);
		Assert.That(Army(me), Nu.Null, "an opponent draws — not you");
		s = TestGames.PassUntil(
			s,
			x => x.GetGame().Step == Step.Draw && x.GetGame().ActivePlayerId == them
		);
		Resolve(Targeting(them, true));
		Assert.That(Army(me), Nu.Null, "the first one they draw in their draw step");
		var recall = Hand(Lsv("Ancestral Recall"), them);
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = them,
				CardId = recall,
				Targets = [them],
			}
		);
		Resolve(Targeting(them, true));
		Assert.That(Life(them), Nu.EqualTo(life - 3), "each other card: 1 damage");
		Assert.That(s.Power(Army(me)!), Nu.EqualTo(3), "and amass Orcs 1, three times");
	}

	// ===== Gollum, Riddle Master =====

	int GollumChoosing(bool odd)
	{
		var gollum = Hand(Lsv("Gollum, Riddle Master"), me);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = gollum });
		var asked = ResolveAsking(On<ChooseNumberChoice>(odd ? 2 : 1));
		Assert.That(
			asked
				.OfType<ChooseNumberChoice>()
				.Single()
				.GetOptions(s, [])
				.Select(o => o.DisplayText),
			Nu.EqualTo(new[] { "Even", "Odd" })
		);
		return gollum;
	}

	/// <summary>They cast it at instant speed; Gollum's mode (if it triggers) chosen as told; it all resolves.</summary>
	void TheyCast(CardDefinition card, int mode = -1)
	{
		var spell = Hand(card, them);
		s = s.Act(new PassPriorityAction { PlayerId = me });
		s = s.Act(new CastSpellAction { PlayerId = them, CardId = spell });
		Resolve(mode < 0 ? null : On<ChooseModesChoice>(mode));
	}

	[Test]
	public void Gollum_OddChosen_EachModeOnce_ThenNothing()
	{
		var gollum = GollumChoosing(odd: true);
		var (mine, theirs, hand) = (Life(me), Life(them), HandCount(me));
		TheyCast(TestGames.Inline("One", "{1}", "Instant"), 0);
		Assert.That(s.Power(s.GetCard(gollum)), Nu.EqualTo(4), "a +1/+1 counter on Gollum");
		TheyCast(TestGames.Inline("Two", "{2}", "Instant"));
		Assert.That(Life(them), Nu.EqualTo(theirs), "mana value 2 is even");
		var three = Hand(TestGames.Inline("Three", "{3}", "Instant"), them);
		s = s.Act(new PassPriorityAction { PlayerId = me });
		s = s.Act(new CastSpellAction { PlayerId = them, CardId = three });
		var asked = ResolveAsking(On<ChooseModesChoice>(1));
		Assert.That(
			asked.OfType<ChooseModesChoice>().Single().Options.Select(o => o.Id),
			Nu.EquivalentTo(new[] { 1, 2 }),
			"one that hasn't been chosen"
		);
		Assert.That((Life(me), Life(them)), Nu.EqualTo((mine + 2, theirs - 2)));
		TheyCast(TestGames.Inline("Five", "{5}", "Instant"), 2);
		Assert.That(HandCount(me), Nu.EqualTo(hand + 1), "draw a card");
		TheyCast(TestGames.Inline("Another", "{1}", "Instant"));
		Assert.That(
			(s.Power(s.GetCard(gollum)), Life(them), HandCount(me)),
			Nu.EqualTo((4, theirs - 2, hand + 1)),
			"all three chosen: nothing"
		);
	}

	[Test]
	public void Gollum_EvenChosen_ZeroIsEven()
	{
		GollumChoosing(odd: false);
		var hand = HandCount(me);
		TheyCast(TestGames.Inline("Zero", "{0}", "Instant"), 2);
		Assert.That(HandCount(me), Nu.EqualTo(hand + 1));
	}

	[Test]
	public void Gollum_TheAi_NamesTheCommonerParity()
	{
		Field(TestGames.CreatureCosting("{2}{G}", "Ape", "3/3"), them);
		Field(TestGames.CreatureCosting("{G}", "Elf", "1/1"), them);
		Field(TestGames.CreatureCosting("{1}{G}", "Bear", "2/2"), them);
		Field(TestGames.CreatureCosting("{1}{G}", "Bear2", "2/2"), them);
		Field(TestGames.CreatureCosting("{4}{G}", "Giant", "5/5"), them);
		var ai = new global::MagicCore.Ai.GreedyAi(me);
		Assert.That(
			ai.Answer(
				s,
				new ChooseNumberChoice(me, 1) { NamesAManaValue = true, OddOrEven = true }
			),
			Nu.EqualTo(new[] { 1 + 1 }),
			"two twos, but a one, a three and a five: odd"
		);
	}

	// ===== Black Widow, Super Spy =====

	(int widow, int land, int five) WidowOverTheirLibrary()
	{
		var widow = Field(Lsv("Black Widow, Super Spy"), me);
		var five = OnTop(TestGames.Inline("Five Whites", "{W}{W}{W}{W}{W}", "Sorcery"), them);
		var land = OnTop(TestGames.Card("Island"), them);
		return (widow, land, five);
	}

	[Test]
	public void BlackWidow_NoCounter_CastTheExiledNonlandWithAnyMana()
	{
		var (widow, land, five) = WidowOverTheirLibrary();
		AttackWith(On<YesNoChoice>(), widow);
		Assert.That(
			(InExile(land), InExile(five)),
			Nu.EqualTo((true, true)),
			"until a nonland card"
		);
		Assert.That(s.GetCard(widow).PlusOneCounters, Nu.EqualTo(0));
		Assert.That(Casts(land), Nu.Empty, "a land exiled on the way isn't played");
		Assert.That(
			Casts(five),
			Nu.Not.Empty,
			"{W}{W}{W}{W}{W} with four Plains: mana of any type"
		);
	}

	[Test]
	public void BlackWidow_GoneBeforeItResolves_NoCounterToPut_SoYouMayCast()
	{
		var (widow, _, five) = WidowOverTheirLibrary();
		s = TestGames.PassUntil(s, x => x.GetPendingChoice() is DeclareAttackerChoice);
		while (s.GetPendingChoice() is DeclareAttackerChoice d)
			s = s.Choose([d.CreatureId == widow ? them : 0]);
		s = TestGames.PassUntil(s, x => !x.StackIsEmpty());
		s = s.MoveCard(widow, s.ZoneId(me, ZoneType.Graveyard)); // killed in response
		var asked = ResolveAsking(On<YesNoChoice>(1));
		Assert.That(
			asked.OfType<YesNoChoice>(),
			Nu.Empty,
			"nothing to put a counter on: not asked"
		);
		Assert.That(
			s.GetCard(five).HasComponent<PlayableFromExile>(),
			"you didn't: you may cast it"
		);
	}

	[Test]
	public void BlackWidow_TheCounter_NoCast()
	{
		var (widow, _, five) = WidowOverTheirLibrary();
		AttackWith(On<YesNoChoice>(1), widow);
		Assert.That(s.GetCard(widow).PlusOneCounters, Nu.EqualTo(1));
		Assert.That(InExile(five));
		Assert.That(
			s.GetCard(five).HasComponent<PlayableFromExile>(),
			Nu.False,
			"if you don't — you did: no permission at all"
		);
	}
}
