using MtgCore;
using MtgCore.Cards.Builders;
using MtgGame;
using NUnit.Framework;

namespace SQGodotCommon.Tests;

/// <summary>
/// The card frame carries COLOUR, which since colours became a real constraint is the first thing
/// a drafter reads off a pack: can I even cast this?
///
/// Cards are built inline. The frame is a rendering decision about a card's colour, not about any
/// particular card, so a set retune must not be able to break these.
/// </summary>
[TestFixture]
public class MtgCardThemeTests
{
	private static Card Spell(ManaPool pips) =>
		CardFactory.Sorcery("Test Spell", manaCost: 2).Build() with
		{
			ColorPips = pips,
		};

	[Test]
	public void EachColour_GetsItsOwnFrame_AndNoTwoShareOne()
	{
		var frames = ManaPool
			.Colors.Select(c => MtgCardTheme.FrameColor(Spell(ManaPool.Empty.Add(c, 1))))
			.ToList();

		Assert.That(frames.Distinct().Count(), Is.EqualTo(5), "five colours, five frames");
	}

	[Test]
	public void AMulticolourCard_IsGold_RatherThanEitherOfItsColours()
	{
		// Not a blend: blue+red blended is a muddy purple that reads as a third colour.
		var gold = MtgCardTheme.FrameColor(Spell(new ManaPool { Blue = 1, Red = 1 }));

		Assert.Multiple(() =>
		{
			Assert.That(
				gold,
				Is.Not.EqualTo(MtgCardTheme.FrameColor(Spell(new ManaPool { Blue = 1 })))
			);
			Assert.That(
				gold,
				Is.Not.EqualTo(MtgCardTheme.FrameColor(Spell(new ManaPool { Red = 1 })))
			);
		});
	}

	[Test]
	public void ADoublePip_LooksTheSameAsASinglePip_OfThatColour()
	{
		// WW is still a white card. Depth belongs in the cost text, not the frame.
		Assert.That(
			MtgCardTheme.FrameColor(Spell(new ManaPool { White = 2 })),
			Is.EqualTo(MtgCardTheme.FrameColor(Spell(new ManaPool { White = 1 })))
		);
	}

	[Test]
	public void AColouredArtifact_TakesItsColour_NotTheArtifactFrame()
	{
		// Ancestral Blade is {1}{W}. Pips are checked before the artifact subtype for this reason.
		var blade = CardFactory.Artifact("Coloured Blade", manaCost: 2).Build() with
		{
			ColorPips = new ManaPool { White = 1 },
		};
		var plainArtifact = CardFactory.Artifact("Plain Rock", manaCost: 2).Build();

		Assert.That(
			MtgCardTheme.FrameColor(blade),
			Is.EqualTo(MtgCardTheme.FrameColor(Spell(new ManaPool { White = 1 })))
		);
		Assert.That(
			MtgCardTheme.FrameColor(blade),
			Is.Not.EqualTo(MtgCardTheme.FrameColor(plainArtifact))
		);
	}

	[Test]
	public void ALand_TakesTheLandFrame_NotTheColourlessOne()
	{
		// A land has no pips — its colour is what it PRODUCES — so without the land check first
		// every land would render as a colourless artifact.
		var land = CardLibrary.BasicLand(ManaColor.Red);
		var colourless = CardFactory.Artifact("Plain Rock", manaCost: 2).Build();

		Assert.That(
			MtgCardTheme.FrameColor(land),
			Is.Not.EqualTo(MtgCardTheme.FrameColor(colourless))
		);
	}

	[Test]
	public void EveryFrame_IsClearlyDistinctFromEveryOther()
	{
		// "Distinct" is not enough: the frame art is near-white, so the tint IS what the player
		// sees, and two near-identical tints cannot be told apart. (This checks the VALUES only —
		// when the card shader dropped COLOR, every frame rendered as one brown while this passed.
		// Rendering is checked by a capture, not here.)
		var cards = ManaPool
			.Colors.Select(c => Spell(ManaPool.Empty.Add(c, 1)))
			.Append(Spell(new ManaPool { White = 1, Black = 1 }))
			.Append(CardFactory.Artifact("Plain Rock", manaCost: 2).Build())
			.Append(CardLibrary.BasicLand(ManaColor.Green))
			.ToList();

		for (var i = 0; i < cards.Count; i++)
		for (var j = i + 1; j < cards.Count; j++)
		{
			var a = MtgCardTheme.FrameColor(cards[i]);
			var b = MtgCardTheme.FrameColor(cards[j]);
			var distance = Math.Sqrt(
				Math.Pow(a.R - b.R, 2) + Math.Pow(a.G - b.G, 2) + Math.Pow(a.B - b.B, 2)
			);
			Assert.That(
				distance,
				Is.GreaterThan(0.15),
				$"{cards[i].Name} and {cards[j].Name} frames are too alike to tell apart"
			);
		}
	}

	[Test]
	public void TheTypeLine_DoesNotCallAPlaneswalkerAPlaneswalkerTwice()
	{
		// "Planeswalker" rides in Subtypes like Artifact and Enchantment do.
		var walker = CardFactory.Planeswalker("Test Walker", manaCost: 4).Build();

		Assert.That(MtgCardTheme.OrderedTribes(walker), Does.Not.Contain("Planeswalker"));
	}

	[Test]
	public void ARaceOutranksAClass_OnTheNamePlate()
	{
		// A Goblin Wizard is read as a Goblin.
		var goblinWizard = CardFactory
			.Creature("Goblin Wizard", manaCost: 2, power: 1, toughness: 1)
			.WithSubtype("Wizard")
			.WithSubtype("Goblin")
			.Build();

		Assert.That(MtgCardTheme.OrderedTribes(goblinWizard).First(), Is.EqualTo("Goblin"));
	}
}
