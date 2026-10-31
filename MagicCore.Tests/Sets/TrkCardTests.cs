using System.Text.RegularExpressions;
using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Sets;
using MagicCore.Sets.Trk;

namespace MagicCore.Tests.Sets;

/// <summary>
/// Star Trek's cards (story MC-5), each against its PRINTED text through real play: the Planet duals
/// (MC-5.5), and the tokens TRK's cards make — Clue, Food and New Planet do what their reminder text says.
/// </summary>
public class TrkCardTests : FraBoardTest
{
	static readonly Regex PlanetText =
		new(
			@"^This land enters tapped\.\nWhen this land enters, you gain 1 life\.\n\{T\}: Add \{(.)\} or \{(.)\}\.$"
		);

	static IEnumerable<CardData> PrintedPlanets =>
		CardDataSet
			.Load("TRK")
			.Select(p => p.Card)
			.Where(c => PlanetText.IsMatch(c.OracleText ?? ""));

	static IEnumerable<string> PlanetNames() => PrintedPlanets.Select(c => c.Name);

	[Test]
	public void TheTenPlanetDuals_AreScripted()
	{
		Assert.That(PlanetNames().Count(), Is.EqualTo(10));
		Assert.That(PlanetNames().Where(n => !CardRegistry.Scripted.Contains(n)), Is.Empty);
	}

	[TestCaseSource(nameof(PlanetNames))]
	public void APlanet_EntersTapped_GainsOneLife_AndTapsForItsTwoColours(string name)
	{
		var printed = PlanetText.Match(PrintedPlanets.Single(c => c.Name == name).OracleText!);
		var colours = new[] { printed.Groups[1].Value, printed.Groups[2].Value }
			.Select(ManaCost.ColorOf)
			.ToList();
		var land = Hand(CardRegistry.Get(name, "TRK"), me);
		var life = Life(me);

		s = s.Act(new PlayLandAction { PlayerId = me, CardId = land });
		Resolve();

		Assert.That(s.GetCard(land).IsTapped, "enters tapped");
		Assert.That(Life(me), Is.EqualTo(life + 1), "you gain 1 life");

		s = s.UpdateObject(land, s.GetCard(land) with { IsTapped = false }); // TEST SETUP: untapped to tap it
		foreach (var colour in Enum.GetValues<ManaColor>())
			Assert.That(
				s.TryAddAction(
					new TapForManaAction
					{
						PlayerId = me,
						CardId = land,
						Color = colour,
					}
				).Success,
				Is.EqualTo(colours.Contains(colour)),
				$"{name}: {colour}"
			);
	}

	// ===== The tokens: what their reminder text says, through the real activation =====

	int Make(CreateTokens tokens, string name)
	{
		var maker = Hand(TestGames.Inline("Maker", "{0}", "Sorcery", null, tokens), me);
		Cast(maker, []);
		return s.CardsIn(me, ZoneType.Battlefield).Single(c => c.Name == name).Id;
	}

	[Test]
	public void AClue_TwoAndSacrificeIt_DrawsACard()
	{
		var clue = Make(Tokens.CreateClue(1), "Clue");
		var hand = HandCount(me);

		s = s.Act(new ActivateAbilityAction { PlayerId = me, CardId = clue });
		Resolve();

		Assert.That((HandCount(me), Alive(clue)), Is.EqualTo((hand + 1, false)));
	}

	[Test]
	public void AFood_TwoTapAndSacrificeIt_GainsThreeLife()
	{
		var food = Make(Tokens.CreateFood(1), "Food");
		var life = Life(me);

		s = s.Act(new ActivateAbilityAction { PlayerId = me, CardId = food });
		Resolve();

		Assert.That((Life(me), Alive(food)), Is.EqualTo((life + 3, false)));
	}

	[Test]
	public void ANewPlanet_EntersTapped_AndTapsForAnyColour()
	{
		var planet = Make(TrkCards.CreateNewPlanet, "New Planet");
		Assert.That(s.GetCard(planet).IsTapped);

		s = s.UpdateObject(planet, s.GetCard(planet) with { IsTapped = false }); // TEST SETUP
		foreach (var colour in ManaAbilities.AnyColor)
			Assert.That(
				s.TryAddAction(
					new TapForManaAction
					{
						PlayerId = me,
						CardId = planet,
						Color = colour,
					}
				).Success,
				colour.ToString()
			);
	}
}
