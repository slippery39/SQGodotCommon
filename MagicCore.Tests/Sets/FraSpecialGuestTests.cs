using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
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
}
