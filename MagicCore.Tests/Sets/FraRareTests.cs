using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Sets.Fra;
using MagicCore.Turns;

namespace MagicCore.Tests.Sets;

/// <summary>Rares and mythics (8a) — each ability fires.</summary>
public class FraRareTests : FraBoardTest
{
	static CardDefinition Trinket => TestGames.Inline("Trinket", "{1}", "Artifact");

	static CardDefinition Typed(CardDefinition card, string typeLine) =>
		card with
		{
			Data = card.Data with { TypeLine = typeLine },
		};

	static CardDefinition Kill =>
		new(
			TestGames.Inline("Kill", "{1}", "Instant").Data,
			[
				new SpellEffectsComponent([new Destroy(Ref.Target())])
				{
					Targets = [new Targeting.TargetSpec(Targeting.TargetKind.Creature)],
				},
			]
		);

	IEnumerable<Card> Named(int player, string name) =>
		s.CardsIn(player, ZoneType.Battlefield).Where(c => c.Name == name);

	void AttackWith(params int[] attackers)
	{
		s = TestGames.PassUntil(s, x => x.IsWaitingForChoice);
		while (s.GetPendingChoice() is DeclareAttackerChoice declare)
			s = s.Choose([attackers.Contains(declare.CreatureId) ? them : CombatOption.None]);
		s = TestGames.PassUntil(s, x => x.GetGame().Step == Step.PostCombatMain);
	}

	[Test]
	public void AeridKonstrari_AHeartwoodOnEnteringAndDying_AndForSix()
	{
		var aerid = Hand("Aerid Konstrari", me);
		Cast(aerid, []);
		Assert.That(Named(me, "Heartwood").Count(), Is.EqualTo(1));

		s = s.Act(Activations(aerid).Single());
		Resolve();
		Assert.That(Named(me, "Heartwood").Count(), Is.EqualTo(2));
		Assert.That(
			s.Power(s.GetCard(aerid)),
			Is.EqualTo(5 + 2),
			"+X/+0 for the two artifacts, the new one too"
		);

		Cast(Hand(Kill, me), [], aerid);
		Assert.That(Named(me, "Heartwood").Count(), Is.EqualTo(3), "and one as it dies");
	}

	[Test]
	public void CraterclawColossus_YourCreaturesGetTrampleAndPlusOnePerArtifact()
	{
		Field(Trinket, me);
		var bear = Field(TestGames.Creature("Bear", "2/2"), me);
		Cast(Hand("Craterclaw Colossus", me), []);
		var card = s.GetCard(bear);
		Assert.That(
			(s.Power(card), s.Has(card, "Trample")),
			Is.EqualTo((2 + 2, true)),
			"the Trinket and the Colossus"
		);
	}

	[Test]
	public void IngrisStingerquill_EachAttackerPingsThem_AndACadetWithHaste()
	{
		var ingris = Field(FraCards.Get("Ingris Stingerquill"), me);
		var bear = Field(TestGames.Creature("Bear", "2/2"), me);
		var life = Life(them);
		AttackWith(bear);
		Assert.That(Life(them), Is.EqualTo(life - 1 - 2), "1 from Ingris, 2 from combat");

		s = s.Act(Activations(ingris).Single());
		Resolve();
		var cadet = Named(me, "Cadet").Single();
		Assert.That(s.Has(cadet, "Haste"));
	}

	[Test]
	public void OverwriteTheMultiverse_ExilesEveryCreature_JaceForEach()
	{
		var a = Field(TestGames.Creature("A", "2/2"), me);
		var b = Field(TestGames.Creature("B", "2/2"), them);
		var c = Field(TestGames.Creature("C", "2/2"), them);
		Cast(Hand("Overwrite the Multiverse", me), []);
		Assert.That(
			(
				s.IsIn(a, me, ZoneType.Exile),
				s.IsIn(b, them, ZoneType.Exile),
				s.IsIn(c, them, ZoneType.Exile)
			),
			Is.EqualTo((true, true, true))
		);
		Assert.That(Jace(me)?.Loyalty, Is.EqualTo(3));
	}

	[Test]
	public void EchoverseFulcrum_LootsOnEntering_AndExilesItselfToWipeTheBoard()
	{
		Hand(Trinket, me);
		var fulcrum = Hand("The Echoverse Fulcrum", me);
		var hand = HandCount(me);
		Cast(fulcrum, []);
		Assert.That(HandCount(me), Is.EqualTo(hand - 1), "draw one, discard one");

		var a = Field(TestGames.Creature("A", "2/2"), me);
		var b = Field(TestGames.Creature("B", "2/2"), them);
		s = s.Act(Activations(fulcrum).Single());
		Resolve();
		Assert.That((Alive(a), Alive(b)), Is.EqualTo((false, false)));
		Assert.That(s.IsIn(fulcrum, me, ZoneType.Exile));
	}

	[Test]
	public void Tarmogoyf_CountsCardTypesInAllGraveyards()
	{
		var goyf = Field(FraCards.Get("Tarmogoyf"), me);
		Assert.That((s.Power(s.GetCard(goyf)), s.Toughness(s.GetCard(goyf))), Is.EqualTo((0, 1)));
		(s, _) = TestGames.Put(s, me, TestGames.Creature("Dead", "1/1"), ZoneType.Graveyard);
		(s, _) = TestGames.Put(
			s,
			them,
			TestGames.Inline("Spent", "{1}", "Instant"),
			ZoneType.Graveyard
		);
		(s, _) = TestGames.Put(
			s,
			them,
			TestGames.Inline("Also", "{1}", "Instant"),
			ZoneType.Graveyard
		);
		(s, _) = TestGames.Put(s, them, TestGames.Card("Forest"), ZoneType.Graveyard);
		Assert.That(
			(s.Power(s.GetCard(goyf)), s.Toughness(s.GetCard(goyf))),
			Is.EqualTo((3, 4)),
			"creature, instant, land — in either graveyard"
		);
	}

	[Test]
	public void DarkMatterManipulator_MillsThree_AndTwoPowerPerSeven()
	{
		var manipulator = Hand("Dark Matter Manipulator", me);
		var graveyard = s.CardsIn(me, ZoneType.Graveyard).Count();
		Cast(manipulator, []);
		Assert.That(s.CardsIn(me, ZoneType.Graveyard).Count(), Is.EqualTo(graveyard + 3));
		while (s.CardsIn(me, ZoneType.Graveyard).Count() < 14)
			(s, _) = TestGames.Put(s, me, TestGames.Creature("Dead", "1/1"), ZoneType.Graveyard);
		Assert.That(s.Power(s.GetCard(manipulator)), Is.EqualTo(1 + 2 * 2));
	}

	[Test]
	public void SimulacrumShaper_ABasicLandOntoTheBattlefieldTapped_AndDrawsAsItDies()
	{
		int plains;
		(s, plains) = TestGames.Put(s, me, TestGames.Card("Plains"), ZoneType.Library);
		var shaper = Hand("Simulacrum Shaper", me);
		s = s.Act(new CastSpellAction { PlayerId = me, CardId = shaper });
		Resolve(choice =>
			choice switch
			{
				ChooseFromLibraryChoice => [plains],
				_ => [],
			}
		);
		Assert.That((Alive(plains), s.GetCard(plains).IsTapped), Is.EqualTo((true, true)));

		var hand = HandCount(me);
		var kill = Hand(Kill, me);
		Cast(kill, [], shaper);
		Assert.That(HandCount(me), Is.EqualTo(hand + 1 - 1 + 1), "the Kill cast, one card drawn");
	}

	[Test]
	public void LyraArchangelOfDawn_LifeGainGrowsYourAngels()
	{
		Field(FraCards.Get("Lyra, Archangel of Dawn"), me);
		var angel = Field(Typed(TestGames.Creature("Angel", "2/2"), "Creature — Angel"), me);
		var bear = Field(TestGames.Creature("Bear", "2/2"), me);
		Cast(
			Hand(TestGames.Inline("Heal", "{W}", "Instant", null, new GainLife(Ref.You, 1)), me),
			[]
		);
		Assert.That(
			(s.GetCard(angel).PlusOneCounters, s.GetCard(bear).PlusOneCounters),
			Is.EqualTo((1, 0))
		);
	}

	[Test]
	public void MasterOfBarbs_NoncombatDamageToThemPumpsYourTeam()
	{
		Field(FraCards.Get("Master of Barbs"), me);
		var bear = Field(TestGames.Creature("Bear", "2/2"), me);
		Cast(
			Hand(
				TestGames.Inline(
					"Ping",
					"{R}",
					"Instant",
					null,
					new DealDamage(Ref.EachOpponent, 1)
				),
				me
			),
			[]
		);
		Assert.That(s.Power(s.GetCard(bear)), Is.EqualTo(2 + 1));
	}

	[Test]
	public void SolariumSentry_TheirCheapSpellsGainYouTwo()
	{
		Field(FraCards.Get("Solarium Sentry"), me);
		var life = Life(me);
		TheirSpellOnTheStack(TestGames.Inline("Cheap", "{1}{R}", "Instant"));
		Resolve();
		Assert.That(Life(me), Is.EqualTo(life + 2));
		TheirSpellOnTheStack(TestGames.Inline("Pricey", "{2}{R}", "Instant"));
		Resolve();
		Assert.That(Life(me), Is.EqualTo(life + 2), "mana value 3: nothing");
	}

	[Test]
	public void LilianaTheFaultless_LifeAsYourCreaturesEnter_AndHexproofForADiscard()
	{
		var liliana = Field(FraCards.Get("Liliana the Faultless"), me);
		var life = Life(me);
		Cast(Hand(TestGames.Creature("Bear", "2/2"), me), []);
		Assert.That(Life(me), Is.EqualTo(life + 1));

		var bear = Named(me, "Bear").Single().Id;
		var fodder = Hand(Trinket, me);
		s = s.Act(Activations(liliana).Single(a => a.Targets[0] == bear && a.DiscardId == fodder));
		Resolve();
		Assert.That(
			(s.Has(s.GetCard(bear), "Hexproof"), s.IsIn(fodder, me, ZoneType.Graveyard)),
			Is.EqualTo((true, true))
		);
	}

	[Test]
	public void IngrisStingerquill_TheAttackerKilledInResponseStillPings()
	{
		Field(FraCards.Get("Ingris Stingerquill"), me);
		var bear = Field(TestGames.Creature("Bear", "2/2"), me);
		s = TestGames.PassUntil(s, x => x.IsWaitingForChoice);
		while (s.GetPendingChoice() is DeclareAttackerChoice declare)
			s = s.Choose([declare.CreatureId == bear ? them : CombatOption.None]);
		Assert.That(s.StackIsEmpty(), Is.False, "Ingris's trigger");
		var life = Life(them);
		Cast(Hand(Kill, me), [], bear);
		Assert.That((Alive(bear), Life(them)), Is.EqualTo((false, life - 1)), "as it last existed");
	}
}
