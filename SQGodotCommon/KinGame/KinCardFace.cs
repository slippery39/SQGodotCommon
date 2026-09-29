using System.Linq;
using Common.Cards;
using Godot;
using ImmutableGameObjects;
using KinCore;

namespace KinGame;

/// <summary>
/// **What a DOOMJAM card LOOKS like** — the whole face, in one place.
///
/// Split out of `KinHandView` because the hand is not the only thing that draws a card: the
/// preview scene (`kin_card_preview.tscn`) draws the same face with no board, no engine and no
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
public static class KinCardFace
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
	/// board label of the same number. The readability floor in KinUI.md is in REAL pixels, so the
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
		// them now; the text that fits is the text a card is allowed to have (KinUI.md).
		if (ui.FindChild("RulesTextLabel", true, false) is Label rules)
		{
			// **112 tall, because the shared fitter assumes 112** (`FitRulesTextToBox`, which MTG also
			// uses, so it is not ours to change). At 104 the fitter chose a size that "fit" 112 and the
			// box clipped the rest — Root Wall silently lost "Block." at its foot (2026-09-23).
			rules.OffsetTop = -90f;
			rules.OffsetBottom = 22f;
			rules.VerticalAlignment = VerticalAlignment.Center;
		}

		StyleStats(ui);

		// Canvas pixels, converted to authored points by `Pt`. The floors are in KinUI.md; rules
		// text at 20 canvas px is the 16 real px minimum at 1600x900 and must not go lower.
		Enlarge(ui, "NameLabel", Pt(26));
		Enlarge(ui, "ManaCostLabel", Pt(34));
		Enlarge(ui, "RulesTextLabel", Pt(22));
		// Dark ink on parchment (style D) needs no outline and no shadow: the shared settings carry a
		// black outline AND a size-10 black shadow, which smeared the ink into a blot.
		if (ui.FindChild("RulesTextLabel", true, false) is Label { LabelSettings: { } ink })
			ink.OutlineSize = ink.ShadowSize = 0;
		Enlarge(ui, "PowerToughnessLabel", Pt(30));
		Enlarge(ui, AttackLabelName, Pt(30));
	}

	/// <summary>Where the art window's centre sits for each of the two heights, in card space.</summary>
	private const int ShortArtCentre = -71;

	private const int TallArtCentre = -14;

	private const string AttackLabelName = "KinAttackLabel";
	private const string AttackIconName = "KinAttackIcon";
	private const string MedallionName = "KinOwnerMedallion";

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

		badge.Texture = KinArt.LifeDisc;

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

		// The owner's face at the card's foot (style D): a monster-deck card leaves with its monster.
		container.AddChild(new Sprite2D { Name = MedallionName, Position = new Vector2(0, 206) });

		var icon = new Sprite2D
		{
			Name = AttackIconName,
			Texture = KinArt.AttackIcon,
			Position = new Vector2(-112, 180),

			// The icon file is 512 square; the card wants roughly 56. Scaled rather than re-exported
			// so that swapping in a different game-icons glyph stays a one-line change.
			Scale = new Vector2(56f / 512f, 56f / 512f),
		};
		container.AddChild(icon);

		var label = KinPalette.Text("", 30, KinPalette.Bone);
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
	public static void ApplyStats(CardUI2D ui, KinCard card)
	{
		var unit = card.GetComponent<UnitComponent>();

		// **Re-centre the art window, per card.** A Sprite2D draws its texture centred on its own
		// position, so a taller art texture alone would grow upward into the name and downward into
		// the stats. `Style` cannot do this — it runs once when the card is created, and a card in
		// the fan is recycled for whatever the hand holds next.
		if (ui.FindChild("ArtFrame", true, false) is Sprite2D frame)
			frame.Position = new Vector2(
				2,
				ArtHeightFor(card) == TallArt ? TallArtCentre : ShortArtCentre
			);

		// **A card with no stat row gets the stat row's room for its text.** The shared fitter measures
		// without the label's line spacing, so three lines it judged to fit 112px rendered taller and
		// lost their last line (Flank lost "lone foe.", 2026-09-23). The box stops above the sword and
		// disc only when there IS a sword and disc. Per card, because cards in the fan are recycled.
		if (ui.FindChild("RulesTextLabel", true, false) is Label rules)
			rules.OffsetBottom = unit is null ? 60f : 22f;

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

		// No card belongs to a monster any more (monster decks dropped, 2026-09-28).
		if (ui.FindChild(MedallionName, true, false) is Sprite2D medallion)
			medallion.Texture = null;
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
	/// What the card's text box says: what it DOES, or what an apocalypse has MARKED it with.
	///
	/// **Flavour is not rules and no longer appears here.** `Description` used to be the fallback,
	/// which meant a vanilla body carried "Takes what is left." in the rules box — words that read
	/// as rules, sat where rules go, and said nothing about how to play the card. Playtested and
	/// cut.
	///
	/// A card with no ability now returns EMPTY on purpose, and the face grows its art into the
	/// space instead (see <see cref="ArtHeightFor"/>). The useful consequence: a text box that is
	/// present means this card does something, which is worth knowing at a glance across a hand.
	/// </summary>
	/// <summary>
	/// Does this card have to be dropped ON a lane? True for a body, and true for a rite whose
	/// effects read a lane — Gallows Feast sacrifices "the unit in this lane", so a drop anywhere
	/// else would silently mean lane 0 and eat the wrong unit.
	/// </summary>
	public static bool NeedsALane(KinCard card) =>
		card.HasComponent<UnitComponent>()
		|| card.Effects.Any(e => KinTargeting.IsLaneScoped(e.Target));

	/// <summary>
	/// A spell's damage numbers with the bonus added. Every spell's text says its damage as "deal N"
	/// or, for a splash, "and N" — `IsSpell` gates it, so no other card's numbers are touched.
	/// </summary>
	private static string Boost(string text, int bonus) =>
		System.Text.RegularExpressions.Regex.Replace(
			text,
			@"\b(deal|Deal|and) (\d+)",
			m => $"{m.Groups[1].Value} {int.Parse(m.Groups[2].Value) + bonus}"
		);

	public static string RulesTextFor(KinCard card)
	{
		// Assembled in KinCore so the console dump cannot disagree with the card face — and so a
		// keyword that is a FLAG rather than an effect (Devour) appears on both.
		return string.Join("\n", KinRulesText.Lines(card));
	}

	/// <summary>
	/// The art window on a card that has something to say.
	///
	/// Shortened from 198 to buy the rules box room for a THIRD line. The text had to grow — at
	/// 20 canvas px it fell to 13 real pixels in a 1280x720 window — and the longest authored
	/// effect ("when the doom fires: 6 to every enemy") needs three lines once it does.
	/// </summary>
	private const int ShortArt = 172;

	/// <summary>
	/// The art window on a card that does not. It runs from under the name plate to just above the
	/// stats — the whole face, minus the two rows that carry numbers.
	/// </summary>
	private const int TallArt = 284;

	private static int ArtHeightFor(KinCard card) =>
		RulesTextFor(card).Length == 0 ? TallArt : ShortArt;

	public static InternalCardUI2D.Details For(KinCard card) => For(card, 0);

	/// <summary>
	/// **A card's TYPE, FAMILY and, above common, its RARITY** — "SPELL · GROVE · RARE". The type
	/// matters to play: every card but an attack is a spell, and Ember counts spells. The card FACE
	/// leaves the rarity out (`rarity: false`): "SPELL · EMBER · UNCOMMON" clipped at hand size, and
	/// rarity matters where a card is chosen — the reward and shop tiles, which keep it.
	/// </summary>
	public static string Tag(KinCard card, bool rarity = true) =>
		string.Join(
				" · ",
				new[]
				{
					KinCore.Party.PartySpells.IsAttack(card) ? "ATTACK" : "SPELL",
					card.Family == KinCore.Party.Family.None ? "" : card.Family.ToString(),
					!rarity || card.Rarity == KinCore.Party.Rarity.Common
						? ""
						: card.Rarity.ToString(),
				}.Where(p => p.Length > 0)
			)
			.ToUpperInvariant();

	/// <summary>
	/// **The card as it plays NOW** (Shayne, 2026-09-28: the playtest could not tell whether Ember
	/// ever fired): a spell's damage with the Spell Power already added — in green, the way a boosted
	/// number reads in Slay the Spire.
	/// </summary>
	public static InternalCardUI2D.Details For(KinCard card, int spellBonus)
	{
		var unit = card.GetComponent<UnitComponent>();

		// Style D: the EDGE is the card's FAMILY colour (2026-09-28), neutral steel for colourless.
		var edge =
			card.Family != KinCore.Party.Family.None
				? KinPalette.Family(card.Family)
				: KinCardKit.Neutral;
		var boosted = spellBonus > 0 && KinCore.Party.PartySpells.IsSpell(card);
		var ground = KinArt.ColourFor(card.Name);
		// A + version draws its base card's art ("Strike+" is Strike's picture).
		var subject = card.Name.TrimEnd('+');

		return new InternalCardUI2D.Details
		{
			Id = card.Id.ToString(),
			CardName = card.Name.ToUpperInvariant(),
			ManaCost = card.Cost.ToString(),
			// Blank: the reference card has no type line, and "Unit" floating across the face says
			// nothing a stat badge does not already say. A Rite has no badge, which is the tell.
			// **The companion game puts the card's FAMILY and rarity here** ("GROVE · RARE").
			TypeLine = Tag(card, rarity: false),

			// A rite's text is the only thing telling you what it does, so it goes where rules text
			// goes. It is authored beside the effect it describes — see KinEffect.Text.
			//
			// **The fallback is why most of the deck rendered a blank text box.** A vanilla unit has
			// no effects and no tags, and this returned the empty string — so `Description`, which is
			// authored for every card in the game, was displayed nowhere at all. It reads as a font
			// bug and is not one: there was no text to size.
			RulesText = boosted ? Boost(RulesTextFor(card), spellBonus) : RulesTextFor(card),
			// TOUGHNESS ONLY — power is drawn beside the sword at bottom-left. See StyleStats.
			PowerToughness = unit is null ? "" : unit.Toughness.ToString(),

			// Every part of the shared card swapped for a flat one. The interaction is untouched —
			// only the pixels change. See KinArt.
			// ONE solid shape. The frame is the whole card face; the name plate and rules box are
			// cleared so nothing stacks on top of it and leaves a seam across the middle.
			// **The frame says what KIND of card this is** — a body you place, or a Rite that
			// resolves and is gone. That is the first question a turn asks of a hand, and it used
			// to be answerable only by noticing that a stat badge was missing.
			MainFrameTexture = KinCardKit.Frame(edge),
			NameFrameTexture = KinCardKit.NamePlate,

			// The art window is a real window again, and it is a MID-TONE. The drawings are
			// near-black silhouettes: on the card body they were a dark shape on a dark shape and
			// disappeared at hand size. A consistent lighter ground behind the art is what makes
			// every drawing legible without tuning each one.
			//
			// Leaving this unset is not an option — the shared card's stone window reappears as a
			// brown rectangle behind the figure. A default returning is not the same as a value
			// never set, and it looks like a regression you did not make.
			ArtFrameTexture = KinArt.ArtBlock(ground, ArtHeightFor(card)),
			RulesTextFrameTexture = KinCardKit.ParchmentBox,
			ManaCostFrameTexture = KinCardKit.CostGem,
			// The ACTION picture when there is one (style D); else the old subject art.
			ArtworkTexture =
				KinCardKit.Illustration(subject, ArtHeightFor(card))
				?? KinArt.CardArt(subject, ground, ArtHeightFor(card)),

			NameColor = KinPalette.Bone,
			ManaCostColor = KinPalette.Bone,
			RulesTextColor = boosted ? Color.FromHtml("#1F7A2E") : KinCardKit.Ink,

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
