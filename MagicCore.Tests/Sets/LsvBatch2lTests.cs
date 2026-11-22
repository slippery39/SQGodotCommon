using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Sets;
using Nu = NUnit.Framework.Is;

namespace MagicCore.Tests.Sets;

/// <summary>
/// MC-4.4l1, LSVPower — colourless' tested LANDS against their printed text, a family at a time: a land is played,
/// never cast, so the fuzz probe reads every one of them NEVER USED. The fast lands' "two or fewer other lands", the
/// surveil lands' entering, the Triomes' cycling, the horizon lands' life and sacrifice, the Verges' "activate only
/// if", Library of Alexandria's "exactly seven" and Abandoned Air Temple's basic land.
/// </summary>
public class LsvBatch2lTests : FraBoardTest
{
	/// <summary>No lands at all — the base board's twenty basics answer every "unless you control".</summary>
	void EmptyBoard()
	{
		s = TestGames.InFirstMain();
		me = s.GetGame().StartingPlayerId;
		them = s.OpponentOf(me);
	}

	int Play(string land)
	{
		var id = Hand(Lsv(land), me);
		s = s.Act(new PlayLandAction { PlayerId = me, CardId = id });
		return id;
	}

	int PlayAmong(string land, int otherLands, CardDefinition? other = null)
	{
		EmptyBoard();
		for (var i = 0; i < otherLands; i++)
			Field(other ?? TestGames.Card("Plains"), me);
		return Play(land);
	}

	[TestCase("Seachrome Coast", ManaColor.White, ManaColor.Blue)]
	[TestCase("Darkslick Shores", ManaColor.Blue, ManaColor.Black)]
	[TestCase("Blackcleave Cliffs", ManaColor.Black, ManaColor.Red)]
	[TestCase("Copperline Gorge", ManaColor.Red, ManaColor.Green)]
	[TestCase("Razorverge Thicket", ManaColor.Green, ManaColor.White)]
	[TestCase("Concealed Courtyard", ManaColor.White, ManaColor.Black)]
	[TestCase("Spirebluff Canal", ManaColor.Blue, ManaColor.Red)]
	[TestCase("Blooming Marsh", ManaColor.Black, ManaColor.Green)]
	[TestCase("Inspiring Vantage", ManaColor.Red, ManaColor.White)]
	[TestCase("Botanical Sanctum", ManaColor.Green, ManaColor.Blue)]
	public void FastLand_EntersTappedUnlessTwoOrFewerOtherLands_AndTapsForItsTwoColours(
		string name,
		ManaColor a,
		ManaColor b
	)
	{
		var early = PlayAmong(name, 2);
		Assert.That(s.GetCard(early).IsTapped, Nu.False, "two other lands: untapped");
		foreach (var colour in Enum.GetValues<ManaColor>())
			Assert.That(
				CanTapFor(early, colour),
				Nu.EqualTo(colour == a || colour == b),
				$"{colour}"
			);

		var late = PlayAmong(name, 3);
		Assert.That(s.GetCard(late).IsTapped, "three other lands: tapped");
	}

	[TestCase("Meticulous Archive")]
	[TestCase("Undercity Sewers")]
	[TestCase("Raucous Theater")]
	[TestCase("Commercial District")]
	[TestCase("Lush Portico")]
	[TestCase("Shadowy Backstreet")]
	[TestCase("Thundering Falls")]
	[TestCase("Underground Mortuary")]
	[TestCase("Elegant Parlor")]
	[TestCase("Hedge Maze")]
	public void SurveilLand_EntersTappedAndSurveils(string name)
	{
		var top = OnTop(TestGames.Creature("Bear", "2/2"));

		var land = Play(name);
		Resolve(choice =>
			choice switch
			{
				ChooseFromLibraryTopChoice => choice.Options.Select(o => o.Id),
				_ => choice.Options.Take(choice.MinChoices).Select(o => o.Id),
			}
		);

		Assert.That(s.GetCard(land).IsTapped, "enters tapped");
		Assert.That(
			s.CardsIn(me, ZoneType.Graveyard).Select(c => c.Id),
			Nu.EqualTo(new[] { top }),
			"surveil 1: the top card, put into the graveyard"
		);
	}

	[TestCase("Spara's Headquarters")]
	[TestCase("Raffine's Tower")]
	[TestCase("Xander's Lounge")]
	[TestCase("Ziatora's Proving Ground")]
	[TestCase("Jetmir's Garden")]
	[TestCase("Savai Triome")]
	[TestCase("Ketria Triome")]
	[TestCase("Indatha Triome")]
	[TestCase("Raugrin Triome")]
	[TestCase("Zagoth Triome")]
	public void Triome_EntersTapped_AndCyclesForThree(string name)
	{
		var cycled = Hand(Lsv(name), me);
		var drawn = OnTop(TestGames.Creature("Bear", "2/2"));
		var untapped = UntappedLands();

		s = s.Act(Activations(cycled).Single());
		Resolve();

		Assert.That(s.CardsIn(me, ZoneType.Graveyard).Select(c => c.Id), Does.Contain(cycled));
		Assert.That(s.CardsIn(me, ZoneType.Hand).Select(c => c.Id), Does.Contain(drawn));
		Assert.That(untapped - UntappedLands(), Nu.EqualTo(3), "cycling {3}");

		var played = Play(name);
		Assert.That(s.GetCard(played).IsTapped, "enters tapped");
	}

	[TestCase("Horizon Canopy", ManaColor.Green, ManaColor.White)]
	[TestCase("Fiery Islet", ManaColor.Blue, ManaColor.Red)]
	[TestCase("Nurturing Peatland", ManaColor.Black, ManaColor.Green)]
	[TestCase("Sunbaked Canyon", ManaColor.Red, ManaColor.White)]
	[TestCase("Waterlogged Grove", ManaColor.Green, ManaColor.Blue)]
	public void HorizonLand_TapsForLife_AndIsSacrificedToDraw(string name, ManaColor a, ManaColor b)
	{
		var land = Field(Lsv(name), me);
		foreach (var colour in Enum.GetValues<ManaColor>())
			Assert.That(
				CanTapFor(land, colour),
				Nu.EqualTo(colour == a || colour == b),
				$"{colour}"
			);

		var life = Life(me);
		s = s.Act(
			new TapForManaAction
			{
				PlayerId = me,
				CardId = land,
				Color = b,
			}
		);
		Assert.That(s.GetPlayer(me).ManaPool[b], Nu.EqualTo(1));
		Assert.That(Life(me), Nu.EqualTo(life - 1), "pay 1 life");

		var second = Field(Lsv(name), me);
		var drawn = OnTop(TestGames.Creature("Bear", "2/2"));
		s = s.Act(Activations(second).Single());
		Resolve();

		Assert.That(Alive(second), Nu.False, "sacrificed");
		Assert.That(s.CardsIn(me, ZoneType.Hand).Select(c => c.Id), Does.Contain(drawn));
	}

	[TestCase("Blazemire Verge", ManaColor.Red, "Plains", "Mountain")]
	[TestCase("Blazemire Verge", ManaColor.Red, "Forest", "Swamp")]
	[TestCase("Bleachbone Verge", ManaColor.White, "Mountain", "Plains")]
	[TestCase("Bleachbone Verge", ManaColor.White, "Island", "Swamp")]
	public void Verge_ItsSecondColourOnlyWithOneOfItsLandTypes(
		string name,
		ManaColor second,
		string wrongLand,
		string rightLand
	)
	{
		EmptyBoard();
		var verge = Field(Lsv(name), me);
		Field(TestGames.Card(wrongLand), me);
		Assert.That(CanTapFor(verge, ManaColor.Black), "{B} always");
		Assert.That(CanTapFor(verge, second), Nu.False, $"no {second} beside a {wrongLand}");

		Field(TestGames.Card(rightLand), me);
		Assert.That(CanTapFor(verge, second), $"{second} beside a {rightLand}");
	}

	[Test]
	public void LibraryOfAlexandria_DrawsOnlyWithExactlySevenCardsInHand()
	{
		var library = Field(Lsv("Library of Alexandria"), me);
		Assert.That(HandCount(me), Nu.EqualTo(7), "the board's opening hand");
		Assert.That(Activations(library).Count(), Nu.EqualTo(1), "exactly seven");

		var eighth = Hand(TestGames.Creature("Bear", "2/2"), me);
		Assert.That(Activations(library), Nu.Empty, "eight cards");

		s = s.Discard(me, [eighth, s.CardsIn(me, ZoneType.Hand).First(c => c.Id != eighth).Id]);
		Assert.That(HandCount(me), Nu.EqualTo(6));
		Assert.That(Activations(library), Nu.Empty, "six cards");
	}

	// ===== Chromatic Star: "{1}, {T}, Sacrifice this artifact: Add one mana of any color." — a mana cost AND a sacrifice =====

	IEnumerable<TapForManaAction> CashIns(int card) =>
		s.LegalActions().OfType<TapForManaAction>().Where(a => a.CardId == card);

	[Test]
	public void ChromaticStar_CashedInByHand_PaysOne_IsSacrificed_AddsTheColour_AndDraws()
	{
		var star = Field(Lsv("Chromatic Star"), me);
		var drawn = OnTop(TestGames.Creature("Bear", "2/2"));
		var untapped = UntappedLands();
		Assert.That(
			CashIns(star).Select(a => a.Color),
			Nu.EquivalentTo(
				new[]
				{
					ManaColor.White,
					ManaColor.Blue,
					ManaColor.Black,
					ManaColor.Red,
					ManaColor.Green,
				}
			),
			"one move per colour"
		);

		s = s.Act(CashIns(star).Single(a => a.Color == ManaColor.Blue));
		Assert.That(untapped - UntappedLands(), Nu.EqualTo(1), "{1}, paid by a land");
		Assert.That(
			s.GetPlayer(me).ManaPool[ManaColor.Blue],
			Nu.EqualTo(1),
			"one mana of any color"
		);
		Assert.That(s.IsIn(star, me, ZoneType.Graveyard), "Sacrifice this artifact");

		Resolve();
		Assert.That(
			s.CardsIn(me, ZoneType.Hand).Select(c => c.Id),
			Does.Contain(drawn),
			"put into a graveyard from the battlefield: draw a card"
		);
	}

	[Test]
	public void ChromaticStar_AnyColorIsNeverColorless()
	{
		var star = Field(Lsv("Chromatic Star"), me);
		var colorless = new TapForManaAction
		{
			PlayerId = me,
			CardId = star,
			Color = ManaColor.Colorless,
			PaysItsCost = true,
		};
		Assert.That(colorless.ValidateAdd(s).IsValid, Nu.False);
	}

	// A mana ability is an activated ability (CR 605): Kai Winn's "activates an ability of a source with the chosen name".
	[Test]
	public void ChromaticStar_CashedIn_IsAnActivationKaiWinnSees()
	{
		var kai = Field(CardRegistry.Get("Kai Winn, Faithless Leader", "TRK"), them);
		s = s.UpdateObject(kai, s.GetCard(kai).WithComponent(new ChosenName("Chromatic Star")));
		var star = Field(Lsv("Chromatic Star"), me);
		var life = Life(me);

		s = s.Act(CashIns(star).First());
		Resolve();

		Assert.That(Life(me), Nu.EqualTo(life - 3), "Kai Winn's 3");
	}

	[Test]
	public void ChromaticStar_PayingForASpell_IsSacrificed_AndDraws()
	{
		foreach (
			var land in s.CardsIn(me, ZoneType.Battlefield)
				.Where(c => c.Is(CardType.Land))
				.Skip(1)
				.ToList()
		)
			s = s.Tap(land.Id); // one Plains left: it pays the Star's {1}, the Star the spell's {U}
		var star = Field(Lsv("Chromatic Star"), me);
		var drawn = OnTop(TestGames.Creature("Bear", "2/2"));
		var spell = Hand(TestGames.CreatureCosting("{U}", "Merfolk", "1/1"), me);

		Cast(spell, []);

		Assert.That(s.IsOnBattlefield(spell), "the Star made the {U}");
		Assert.That(
			s.IsIn(star, me, ZoneType.Graveyard),
			"sacrificed as it paid — never a filter that stays"
		);
		Assert.That(
			s.CardsIn(me, ZoneType.Hand).Select(c => c.Id),
			Does.Contain(drawn),
			"and it drew"
		);
	}

	[Test]
	public void ChromaticStar_WithNothingToPayItsOne_IsNoMove()
	{
		foreach (
			var land in s.CardsIn(me, ZoneType.Battlefield).Where(c => c.Is(CardType.Land)).ToList()
		)
			s = s.Tap(land.Id);
		var star = Field(Lsv("Chromatic Star"), me);
		Assert.That(CashIns(star), Nu.Empty, "its own mana never pays its own {1}");
	}

	[Test]
	public void AbandonedAirTemple_EntersTappedUnlessYouControlABasicLand()
	{
		var alone = PlayAmong("Abandoned Air Temple", 2, Lsv("Strip Mine"));
		Assert.That(s.GetCard(alone).IsTapped, "two nonbasic lands: tapped");

		var withBasic = PlayAmong("Abandoned Air Temple", 1);
		Assert.That(s.GetCard(withBasic).IsTapped, Nu.False, "a basic land: untapped");
	}
}
