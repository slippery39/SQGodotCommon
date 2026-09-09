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
	public void EveryFrameStaysLight_SoSelfModulateCannotMuddyTheArt()
	{
		// The frame tint is MULTIPLIED against the frame art. Anything dark turns it to mud, and
		// "black" is the one that will tempt someone to use an actually-black value.
		var cards = ManaPool
			.Colors.Select(c => Spell(ManaPool.Empty.Add(c, 1)))
			.Append(Spell(new ManaPool { White = 1, Black = 1 }))
			.Append(CardFactory.Artifact("Plain Rock", manaCost: 2).Build())
			.Append(CardLibrary.BasicLand(ManaColor.Green));

		foreach (var card in cards)
		{
			var frame = MtgCardTheme.FrameColor(card);
			var luminance = 0.299f * frame.R + 0.587f * frame.G + 0.114f * frame.B;
			Assert.That(
				luminance,
				Is.GreaterThan(0.55f),
				$"{card.Name} frame is too dark to multiply"
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
