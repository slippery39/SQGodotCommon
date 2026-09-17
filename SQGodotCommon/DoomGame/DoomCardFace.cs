using System.Linq;
using Common.Cards;
using DoomCore;
using Godot;
using ImmutableGameObjects;

namespace DoomGame;

/// <summary>
/// **What a DOOMJAM card LOOKS like** — the whole face, in one place.
///
/// Split out of `DoomHandView` because the hand is not the only thing that draws a card: the
/// preview scene (`doom_card_preview.tscn`) draws the same face with no board, no engine and no
/// battle, which is how a card change gets looked at in seconds instead of by starting a run. Two
/// copies of this would drift within a day, and the drift would only show up in whichever surface
/// nobody happened to open.
///
/// `Card2D` is a stack of Sprite2Ds — frame, name plate, art window, cost badge, rules box — and
/// every one of those textures is settable through `Details`. So DOOMJAM keeps the whole shared
/// card: its drag, its hover, its fan, its shader. **Only the pixels change.** Forking `CardUI2D`
/// to get a flat look would have cost 300 lines of duplicated interaction to change the colour of
/// a rectangle.
/// </summary>
public static class DoomCardFace
{
	/// <summary>How much a Card2D is shrunk to sit in one band of the board rather than fill a screen.</summary>
	public const float CardScale = 0.95f;

	/// <summary>
	/// The card scene's OWN scale, from `card_2d_canvasgroup.tscn`. **This is the term that was
	/// missing**, and it is why every label on a card rendered at 40% of its authored size while the
	/// arithmetic said 57%.
	///
	/// It is not fixed there because that scene is shared with the MTG card scenes, and this project
	/// is required to be a clean no-op for them. It is compensated for instead — see <see cref="Pt"/>.
	/// </summary>
	private const float SceneScale = 0.7f;

	/// <summary>
	/// The authored font size that renders at <paramref name="canvasPx"/> in the 1920x1080 design
	/// canvas.
	///
	/// **Author against the canvas, never against the window.** The window scale (0.833 at 1600x900)
	/// applies to the whole screen equally, so a card label sized in canvas pixels lands beside a
	/// board label of the same number. The readability floor in DoomUI.md is in REAL pixels, so the
	/// canvas target is that floor divided by the window scale — 16 real px is 20 canvas px.
	/// </summary>
	private static int Pt(float canvasPx) => Mathf.RoundToInt(canvasPx / (SceneScale * CardScale));

	/// <summary>
	/// The two pieces of the shared card that `Details` does not reach.
	///
	/// Both are plain nodes inside the card scene rather than swappable textures, so they are found
	/// by name and restyled once, when the card is created. Reaching into another scene's tree is
	/// not free — if either node is renamed this silently stops working, which is why it degrades to
	/// doing nothing rather than throwing.
	/// </summary>
	public static void Style(CardUI2D ui)
	{
		// The "Unit" type band: a dark stripe straight across the card face. The reference card has
		// no such band, and the type line is already implied by the stat badge.
		if (ui.FindChild("TypeLineBand", true, false) is ColorRect band)
			band.Color = Colors.Transparent;

		// **The name was being eaten by the cost disc.** The shared card centres the name across the
		// full width and hangs the cost badge over its left end, so "Breaching Charge" rendered as
		// "reaching Charge" — a card whose name is wrong is worse than a card with no name.
		//
		// **The fix is to move the COST, not to shrink the name.** Sharing the top row, the widest
		// name the pool can produce still did not fit once the disc had taken its end — so the disc
		// moves down onto the art's top-left corner, where the mockup has it anyway, and the name
		// gets the whole row. `ApplyStats` still eases the type down for long names, but from a
		// starting width that can actually hold them. Truncating was never an option: a name is how
		// a card is talked about, and half of one is no better than none.
		if (ui.FindChild("ManaCost", true, false) is Sprite2D cost)
			cost.Position = new Vector2(-112, -146);

		if (ui.FindChild("NameLabel", true, false) is Label name)
		{
			name.OffsetLeft = -129f;
			name.OffsetRight = 129f;
		}

		// **The rules text was running into the stats.** Its box is 140 tall and centred, so the
		// last line landed on top of the sword and the disc at the card's foot. The box stops above
		// them now; the text that fits is the text a card is allowed to have (DoomUI.md).
		if (ui.FindChild("RulesTextLabel", true, false) is Label rules)
		{
			rules.OffsetBottom = 22f;
			rules.VerticalAlignment = VerticalAlignment.Center;
		}

		StyleStats(ui);

		// Canvas pixels, converted to authored points by `Pt`. The floors are in DoomUI.md; rules
		// text at 20 canvas px is the 16 real px minimum at 1600x900 and must not go lower.
		Enlarge(ui, "NameLabel", Pt(26));
		Enlarge(ui, "ManaCostLabel", Pt(34));
		Enlarge(ui, "RulesTextLabel", Pt(20));
		Enlarge(ui, "PowerToughnessLabel", Pt(30));
		Enlarge(ui, AttackLabelName, Pt(30));
	}

	private const string AttackLabelName = "DoomAttackLabel";
	private const string AttackIconName = "DoomAttackIcon";

	/// <summary>
	/// Splits the one `12/20` badge into the mockup's two marks: a bone SWORD with the power beside
	/// it at bottom-left, and a red DISC holding the toughness at bottom-right.
	///
	/// This is the same language the lane cell speaks — power left, life right — so a card and the
	/// body it becomes are read the same way round. It also retires the last overflow: `12/20` in
	/// one pill ran off the card's own edge even after the pill was widened, because five glyphs do
	/// not fit inside 312px of card next to a margin. Two numbers of two digits do.
	///
	/// The attack pair does not exist in the shared scene, so it is BUILT here, once, and found by
	/// name afterwards. Adding nodes to another project's scene instance is fine; editing that
	/// scene is not.
	/// </summary>
	private static void StyleStats(CardUI2D ui)
	{
		if (ui.FindChild("PowerToughnessBadge", true, false) is not Sprite2D badge)
			return;

		badge.Texture = DoomArt.LifeDisc;

		// Well inside the card's own corner. The shared card hangs this badge past its edge, which
		// suited a frame that bled outwards and clips against a flat one.
		badge.Position = new Vector2(104, 180);

		if (badge.FindChild("PowerToughnessLabel", true, false) is Label toughness)
		{
			toughness.OffsetLeft = -40f;
			toughness.OffsetRight = 40f;
			toughness.OffsetTop = -32f;
			toughness.OffsetBottom = 32f;
		}

		if (
			badge.GetParent() is not Node2D container
			|| container.FindChild(AttackIconName, true, false) is not null
		)
			return;

		var icon = new Sprite2D
		{
			Name = AttackIconName,
			Texture = DoomArt.AttackIcon,
			Position = new Vector2(-112, 180),

			// The icon file is 512 square; the card wants roughly 56. Scaled rather than re-exported
			// so that swapping in a different game-icons glyph stays a one-line change.
			Scale = new Vector2(56f / 512f, 56f / 512f),
		};
		container.AddChild(icon);

		var label = DoomPalette.Text("", 30, DoomPalette.Bone);
		label.Name = AttackLabelName;
		label.VerticalAlignment = VerticalAlignment.Center;
		label.OffsetLeft = -64f;
		label.OffsetRight = 16f;
		label.OffsetTop = 150f;
		label.OffsetBottom = 210f;
		label.MouseFilter = Control.MouseFilterEnum.Ignore;
		container.AddChild(label);
	}

	/// <summary>
	/// Fills in the numbers that `Details` cannot carry.
	///
	/// `InternalCardUI2D.Details` knows about one combined power/toughness string, so the split pair
	/// has to be written separately — and it must be written EVERY time the details are applied, or
	/// a card recycled into a new hand keeps the previous card's power. Call it right after
	/// `SetCardsDetails` / `ApplyTo`.
	/// </summary>
	public static void ApplyStats(CardUI2D ui, DoomCard card)
	{
		var unit = card.GetComponent<UnitComponent>();

		// **Long names get a smaller type, rather than an ellipsis.** "Breaching Charge" does not
		// fit the name row at full size and never will; the alternatives were truncating it or
		// widening a box that has the cost disc on one side and the card edge on the other. This
		// stays above the 16px floor at every length the pool produces, and it is applied per card
		// because `Style` runs before the card knows what it is.
		var length = (card.Name ?? "").Length;
		Enlarge(
			ui,
			"NameLabel",
			Pt(
				length > 15 ? 20
				: length > 12 ? 22
				: 26
			)
		);

		if (ui.FindChild(AttackLabelName, true, false) is Label attack)
		{
			attack.Text = unit is null ? "" : unit.Power.ToString();
			attack.Visible = unit is not null;
		}

		// A Rite has no body, so it shows neither mark. That absence IS the tell, and it is why the
		// frame colour carries the unit/rite distinction as well — one signal for it is not enough.
		if (ui.FindChild(AttackIconName, true, false) is Sprite2D icon)
			icon.Visible = unit is not null;
	}

	/// <summary>
	/// Sets one card label's font size, in the CARD's own space.
	///
	/// **Two scales stack before this reaches a screen**, which is how the numbers got unreadable:
	/// the fan is drawn at <see cref="CardScale"/>, and the 1920-wide canvas is itself letterboxed
	/// into a smaller window. The shared card's 18pt rules text came out at 8 real pixels. Sizes
	/// here are chosen so that <c>authored x CardScale</c> lands where the board's own labels are.
	///
	/// **The LabelSettings is DUPLICATED first, always.** It is an `ext_resource` shared with the
	/// MTG card scenes, and mutating it in place would reach straight into a project this one is
	/// required to leave alone — a resource edited at runtime is not local just because the node is.
	/// </summary>
	private static void Enlarge(CardUI2D ui, string label, int fontSize)
	{
		if (ui.FindChild(label, true, false) is not Label found || found.LabelSettings is null)
			return;

		found.LabelSettings = (LabelSettings)found.LabelSettings.Duplicate();
		found.LabelSettings.FontSize = fontSize;
	}

	/// <summary>
	/// What the card's text box says, in priority order: what it DOES, then what it is marked with,
	/// then what it is.
	///
	/// **A card is never blank.** Every card in the game authors a `Description` — it comes down
	/// from `RunCard` and is already on the battle card — so the vanilla units that used to render
	/// an empty box have had a line waiting for them the whole time. Flavour is not rules, but a
	/// vanilla body's rules ARE its stat badge, and an empty box reads as a broken card.
	/// </summary>
	public static string RulesTextFor(DoomCard card)
	{
		if (!card.Effects.IsEmpty)
			return string.Join("\n", card.Effects.Select(e => e.Text));

		// A tag is a mark an apocalypse LEFT on the card, so it outranks flavour — "Irradiated"
		// costs a life when drawn and the player has to be able to see that coming.
		if (!card.Tags.IsEmpty)
			return string.Join(", ", card.Tags);

		return card.Description ?? "";
	}

	public static InternalCardUI2D.Details For(DoomCard card)
	{
		var unit = card.GetComponent<UnitComponent>();

		return new InternalCardUI2D.Details
		{
			Id = card.Id.ToString(),
			CardName = card.Name,
			ManaCost = card.Cost.ToString(),
			// Blank: the reference card has no type line, and "Unit" floating across the face says
			// nothing a stat badge does not already say. A Rite has no badge, which is the tell.
			TypeLine = "",

			// A rite's text is the only thing telling you what it does, so it goes where rules text
			// goes. It is authored beside the effect it describes — see DoomEffect.Text.
			//
			// **The fallback is why most of the deck rendered a blank text box.** A vanilla unit has
			// no effects and no tags, and this returned the empty string — so `Description`, which is
			// authored for every card in the game, was displayed nowhere at all. It reads as a font
			// bug and is not one: there was no text to size.
			RulesText = RulesTextFor(card),
			// TOUGHNESS ONLY — power is drawn beside the sword at bottom-left. See StyleStats.
			PowerToughness = unit is null ? "" : unit.Toughness.ToString(),

			// Every part of the shared card swapped for a flat one. The interaction is untouched —
			// only the pixels change. See DoomArt.
			// ONE solid shape. The frame is the whole card face; the name plate and rules box are
			// cleared so nothing stacks on top of it and leaves a seam across the middle.
			// **The frame says what KIND of card this is** — a body you place, or a Rite that
			// resolves and is gone. That is the first question a turn asks of a hand, and it used
			// to be answerable only by noticing that a stat badge was missing.
			MainFrameTexture = DoomArt.Body(unit is null ? DoomArt.RiteCard : DoomArt.UnitCard),
			NameFrameTexture = DoomArt.Blank(279, 53),

			// The art window is a real window again, and it is a MID-TONE. The drawings are
			// near-black silhouettes: on the card body they were a dark shape on a dark shape and
			// disappeared at hand size. A consistent lighter ground behind the art is what makes
			// every drawing legible without tuning each one.
			//
			// Leaving this unset is not an option — the shared card's stone window reappears as a
			// brown rectangle behind the figure. A default returning is not the same as a value
			// never set, and it looks like a regression you did not make.
			ArtFrameTexture = DoomArt.ArtBlock(DoomArt.ColourFor(card.Name)),
			RulesTextFrameTexture = DoomArt.Blank(279, 158),
			ManaCostFrameTexture = DoomArt.CostBadge,
			ArtworkTexture = DoomArt.CardArt(card.Name, DoomArt.ColourFor(card.Name)),

			NameColor = DoomPalette.Bone,
			ManaCostColor = DoomPalette.Bone,
			RulesTextColor = DoomPalette.Bone,

			// The border is painted into the frame texture, so the shader outline would only
			// double it.
			OutlineThickness = 0f,

			// Explicitly OFF. The scene sets enable_holographic false, but UpdateHolographicShader
			// rewrites the shader from the C# field at _Ready, so the scene's value does not
			// survive. Left unset it laid a rainbow-noise wash over every card — invisible on
			// saturated colours, and unmistakable on the flat mid-tones this design uses.
			Holographic = false,
			HolographicIntensity = 0f,
		};
	}
}
