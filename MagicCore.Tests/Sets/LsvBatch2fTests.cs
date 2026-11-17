using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Rules;
using MagicCore.Turns;
using Nu = NUnit.Framework.Is;

namespace MagicCore.Tests.Sets;

/// <summary>
/// MC-4.4f, LSVPower — blue's tier 2: each card's ability FIRES, against its PRINTED text, through real casting and
/// the real stack — affinity and a graveyard permission (Emry), squad, evoke, improvise, scry, Islandcycling.
/// </summary>
public class LsvBatch2fTests : FraBoardTest
{
	static CardDefinition Bear => TestGames.Creature("Bear", "2/2");

	static CardDefinition Bauble => TestGames.Inline("Bauble", "{0}", "Artifact");

	int[] Named(string name) =>
		[.. s.CardsIn(me, ZoneType.Battlefield).Where(c => c.Name == name).Select(c => c.Id)];

	int GraveyardCount(int player) => s.CardsIn(player, ZoneType.Graveyard).Count();

	[Test]
	public void Emry_CostsOneLessPerArtifact_MillsFour()
	{
		for (var i = 0; i < 3; i++)
			Field(TestCards.Trinket, me);
		var lands = UntappedLands();
		var graveyard = GraveyardCount(me);

		Cast(Hand(Lsv("Emry, Lurker of the Loch"), me), []);

		Assert.That(
			lands - UntappedLands(),
			Nu.EqualTo(1),
			"{2}{U} less {1} for each of 3 artifacts"
		);
		Assert.That(GraveyardCount(me) - graveyard, Nu.EqualTo(4), "mill four cards");
	}

	[Test]
	public void Emry_TapToCastAnArtifactCardFromYourGraveyardThisTurn()
	{
		var emry = Field(Lsv("Emry, Lurker of the Loch"), me);
		var trinket = Put(TestCards.Trinket, me, ZoneType.Graveyard);
		var bear = Put(Bear, me, ZoneType.Graveyard);
		Assert.That(Casts(trinket), Nu.Empty, "no permission yet");
		Assert.That(
			Activations(emry).SelectMany(a => a.Targets),
			Nu.EquivalentTo(new[] { trinket }),
			"target ARTIFACT card in your graveyard"
		);

		s = s.Act(Activations(emry).Single());
		Resolve();
		Cast(trinket, []);

		Assert.That(
			Alive(trinket),
			"cast from the graveyard; it resolved onto the battlefield, not exiled"
		);
		Assert.That(InGraveyard(bear));
	}

	[Test]
	public void Emry_ThePermissionIsThisTurnOnly()
	{
		var emry = Field(Lsv("Emry, Lurker of the Loch"), me);
		var trinket = Put(TestCards.Trinket, me, ZoneType.Graveyard);
		s = s.Act(Activations(emry).Single());
		Resolve();
		Assert.That(Casts(trinket), Nu.Not.Empty);

		var turn = s.GetGame().TurnNumber;
		s = TestGames.PassUntil(
			s,
			x => x.GetGame().TurnNumber == turn + 2 && x.GetGame().Step == Step.PreCombatMain
		);

		Assert.That(s.GetGame().ActivePlayerId, Nu.EqualTo(me));
		Assert.That(InGraveyard(trinket));
		Assert.That(Casts(trinket), Nu.Empty, "\"this turn\"");
	}

	[Test]
	public void Emry_MarkedAgainOnALaterTurn_CastableAgain()
	{
		var emry = Field(Lsv("Emry, Lurker of the Loch"), me);
		var trinket = Put(TestCards.Trinket, me, ZoneType.Graveyard);
		s = s.Act(Activations(emry).Single());
		Resolve(); // not cast: the mark stays on it in the graveyard, and runs out
		var turn = s.GetGame().TurnNumber;
		s = TestGames.PassUntil(
			s,
			x => x.GetGame().TurnNumber == turn + 2 && x.GetGame().Step == Step.PreCombatMain
		);

		s = s.Act(Activations(emry).Single());
		Resolve();

		Assert.That(
			Casts(trinket),
			Nu.Not.Empty,
			"the new mark, not the one left from an earlier turn"
		);
	}

	[Test]
	public void Emry_UnderUnderworldBreach_TheMarkAsksNoEscapeCost()
	{
		Field(Lsv("Underworld Breach"), me);
		var emry = Field(Lsv("Emry, Lurker of the Loch"), me);
		var trinket = Put(TestCards.Trinket, me, ZoneType.Graveyard); // no three other cards to exile
		s = s.Act(Activations(emry).Single());
		Resolve();

		Cast(trinket, []);

		Assert.That(Alive(trinket), "cast by Emry's mark for its mana cost alone");
	}

	[Test]
	public void Bilbo_ASorceryWithAMarkInTheGraveyard_IsStillExiledLeavingTheStack()
	{
		var bilbo = Field(Lsv("Bilbo, Thief in the Night"), me);
		var sorcery = Put(TestCards.Fodder, me, ZoneType.Graveyard);
		s = s.UpdateObject(
			sorcery,
			s.GetCard(sorcery)
				.WithComponent(new Effects.PlayableFromExile(me, s.GetGame().TurnNumber))
		); // TEST SETUP: a mark, as MakePlayable leaves

		AttackWith(On<ChooseAmongChoice>(sorcery), bilbo);

		Assert.That(InExile(sorcery), "Bilbo's cast exiles it, mark or none");
	}

	[Test]
	public void SicarianInfiltrator_GoneBeforeSquadResolves_StillCopiedAsItLastWas()
	{
		var infiltrator = Hand(Lsv("Sicarian Infiltrator"), me);
		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = infiltrator,
				Replicate = 1,
			}
		);
		s = TestGames.PassUntil(s, x => !x.StackIsEmpty() && x.IsOnBattlefield(infiltrator));
		s = s.MoveCard(infiltrator, s.ZoneId(me, ZoneType.Graveyard)); // TEST SETUP: killed in response

		Resolve();

		Assert.That(
			Named("Sicarian Infiltrator").Count(id => s.GetCard(id).IsToken),
			Nu.EqualTo(1),
			"that many tokens, copies of it as it last existed"
		);
	}

	[TestCase(0)]
	[TestCase(2)]
	public void SicarianInfiltrator_SquadMakesThatManyTokenCopies_EachDraws(int squads)
	{
		var infiltrator = Hand(Lsv("Sicarian Infiltrator"), me);
		var lands = UntappedLands();
		var hand = HandCount(me);

		s = s.Act(
			new CastSpellAction
			{
				PlayerId = me,
				CardId = infiltrator,
				Replicate = squads,
			}
		);
		Resolve();

		var all = Named("Sicarian Infiltrator");
		Assert.That(
			all,
			Has.Length.EqualTo(1 + squads),
			"create that many tokens that are copies of it"
		);
		Assert.That(all.Count(id => s.GetCard(id).IsToken), Nu.EqualTo(squads));
		Assert.That(
			lands - UntappedLands(),
			Nu.EqualTo(3 + 2 * squads),
			"{2}{U}, and {2} each time"
		);
		Assert.That(
			HandCount(me),
			Nu.EqualTo(hand + squads),
			"cast from the hand; each one draws as it enters"
		);
	}

	[Test]
	public void Subtlety_Evoked_TheirCreatureSpellToTheBottom_ThenItIsSacrificed()
	{
		var subtlety = Hand(Lsv("Subtlety"), me);
		var blue = Hand(Lsv("Counterspell"), me);
		TapMyLands(); // evoke costs no mana
		var bear = TheirSpellOnTheStack(TestGames.Creature("Bear", "2/2", "Flash"));

		s = s.Act(Casts(subtlety).Single(c => c.WithoutPayingManaCost && c.ExileId == blue));
		Resolve(Targeting(bear, yes: false));

		Assert.That(Library(them).Last(), Nu.EqualTo(bear), "its owner chose the bottom");
		Assert.That(InExile(blue), "evoke: exile a blue card from your hand");
		Assert.That(InGraveyard(subtlety), "evoked: sacrificed as it enters");
	}

	[Test]
	public void Subtlety_HardCast_StaysAndTheOwnerMayChooseTheTop()
	{
		var subtlety = Hand(Lsv("Subtlety"), me);
		var bear = TheirSpellOnTheStack(TestGames.Creature("Bear", "2/2", "Flash"));

		s = s.Act(Casts(subtlety).Single(c => !c.WithoutPayingManaCost));
		var asked = ResolveAsking(Targeting(bear, yes: true));

		Assert.That(
			asked.OfType<YesNoChoice>().Single().PlayerId,
			Nu.EqualTo(them),
			"ITS OWNER's choice"
		);
		Assert.That(Library(them).First(), Nu.EqualTo(bear), "its owner chose the top");
		Assert.That(Alive(subtlety), "not evoked: no sacrifice");
	}

	[Test]
	public void KappaCannoneer_ImproviseTapsArtifactsForGenericOnly()
	{
		var kappa = Hand(Lsv("Kappa Cannoneer"), me);
		var artifacts = Enumerable.Range(0, 6).Select(_ => Field(TestCards.Trinket, me)).ToList();
		TapMyLands();
		Assert.That(Casts(kappa), Nu.Empty, "six artifacts pay {5}, never the {U}");

		var island = s.CardsIn(me, ZoneType.Battlefield).First(c => c.Name == "Island");
		s = s.UpdateObject(island.Id, island with { IsTapped = false });
		Cast(kappa, []);

		Assert.That(Alive(kappa));
		Assert.That(artifacts.Count(id => s.GetCard(id).IsTapped), Nu.EqualTo(5), "{5} improvised");
	}

	[Test]
	public void KappaCannoneer_ItOrAnotherArtifactEnters_ACounterAndUnblockable()
	{
		var kappa = Hand(Lsv("Kappa Cannoneer"), me);
		Cast(kappa, []);
		Assert.That(s.GetCard(kappa).PlusOneCounters, Nu.EqualTo(1), "this creature enters");
		Assert.That(s.Has(s.GetCard(kappa), Combat.CantBeBlocked), "it can't be blocked this turn");

		var theirs = Put(Bauble, them, ZoneType.Hand);
		s = s.EnterBattlefield(theirs, them); // TEST SETUP: an artifact enters, but under THEIR control
		Cast(Hand(Bauble, me), []);

		Assert.That(
			s.GetCard(kappa).PlusOneCounters,
			Nu.EqualTo(2),
			"another artifact YOU control enters"
		);
	}

	[Test]
	public void Preordain_ScryTwo_OneToTheBottom_ThenDraw()
	{
		var second = OnTop(TestCards.Fodder);
		var top = OnTop(TestCards.Fodder);

		var asked = CastAsking(Hand(Lsv("Preordain"), me), On<ChooseFromLibraryTopChoice>(top));

		Assert.That(
			Offered<ChooseFromLibraryTopChoice>(asked),
			Nu.EquivalentTo(new[] { top, second })
		);
		Assert.That(Library(me).Last(), Nu.EqualTo(top), "put on the bottom");
		Assert.That(s.IsIn(second, me, ZoneType.Hand), "the rest on top, then draw a card");
		Assert.That(s.GetPlayer(me).ThisTurn.Surveils, Nu.EqualTo(0), "a scry is no surveil");
	}

	[Test]
	public void LorienRevealed_DrawsThree_IslandcyclingFindsAnIslandCard()
	{
		var hand = HandCount(me);
		Cast(Hand(Lsv("Lórien Revealed"), me), []);
		Assert.That(HandCount(me), Nu.EqualTo(hand + 3), "draw three cards");

		var tundra = Put(Lsv("Tundra"), me, ZoneType.Library); // an Island card that isn't basic
		var lorien = Hand(Lsv("Lórien Revealed"), me);
		var asked = new List<ChoiceAction>();
		s = s.Act(Activations(lorien).Single());
		Resolve(c =>
		{
			asked.Add(c);
			return c switch
			{
				ChooseFromLibraryChoice => [tundra],
				_ => c.Options.Take(c.MinChoices).Select(o => o.Id),
			};
		});

		Assert.That(InGraveyard(lorien), "discard this card");
		Assert.That(s.IsIn(tundra, me, ZoneType.Hand));
		Assert.That(
			asked.OfType<ChooseFromLibraryChoice>().Single().Options.Select(o => o.DisplayText),
			Does.Contain("Tundra").And.Not.Contain("Forest"),
			"an Island card — never a Forest"
		);
	}

	[Test]
	public void LegalActions_StayLinear_TwelveCreaturesTwelveArtifactsAFullHand()
	{
		for (var i = 0; i < 12; i++)
		{
			Field(Bear, me);
			Field(TestCards.Trinket, me);
			Put(TestCards.Trinket, me, ZoneType.Graveyard);
		}
		Field(Lsv("Emry, Lurker of the Loch"), me);
		var infiltrator = Hand(Lsv("Sicarian Infiltrator"), me);
		var subtlety = Hand(Lsv("Subtlety"), me);
		foreach (var name in new[] { "Kappa Cannoneer", "Preordain", "Lórien Revealed" })
			Hand(Lsv(name), me);
		Hand(Lsv("Counterspell"), me);

		var actions = s.LegalActions().ToList();
		TestContext.Out.WriteLine($"{actions.Count} legal actions");

		// one cast per number of squads 20 lands pay for: (20 - 3) / 2 — linear in the mana
		Assert.That(Casts(infiltrator).Count(), Nu.EqualTo(1 + 8));
		// hard cast, or evoke exiling one of the five other blue cards in hand
		Assert.That(Casts(subtlety).Count(), Nu.EqualTo(1 + 5));
		Assert.That(actions, Has.Count.LessThan(80));
	}
}
