using System.Text.RegularExpressions;
using Godot;

namespace KinGame;

/// <summary>
/// The five flat colours, and only five. See KinUI.md.
///
/// **Gold and Red are reserved and must not be spent on decoration.** Gold means "yours, and
/// precious" — the Companion and your energy, nothing else, which is what makes the Companion
/// findable across five lanes. Red means "the enemy, and life".
///
/// Red doing double duty on enemy health and your unit's remaining toughness is deliberate: a unit
/// absorbs up to its remaining toughness and the excess hits the face behind it, so a body in a lane
/// is worth exactly its toughness in life. One colour for both says that without a tutorial.
/// </summary>
public static class KinPalette
{
	public static readonly Color Navy = Color.FromHtml("#16212E");
	public static readonly Color Slate = Color.FromHtml("#233444");
	public static readonly Color Bone = Color.FromHtml("#E8EEF2");
	public static readonly Color Gold = Color.FromHtml("#E3B23C");
	public static readonly Color Red = Color.FromHtml("#C73E3A");

	/// <summary>An empty lane: darker than the frame, so a held lane reads as the exception.</summary>
	public static readonly Color EmptySlot = Color.FromHtml("#1B2836");

	/// <summary>
	/// **Each companion's identity colour — its cards, its cell, anything that says "this one".**
	/// Playtest (2026-09-23): every card looked the same, so a card played for Bramble was believed
	/// to be Pike's. Never gold or red: those are reserved for "yours" and for the enemy.
	/// </summary>
	/// <summary>
	/// **A monster's colour is its FAMILY's** (Shayne, 2026-09-28: the playtest could not tell which
	/// monsters and cards were kin). The companion colour is only the fallback for a family-less one;
	/// a card's medallion still says whose deck it came from. Ember is ORANGE, not the foes' red.
	/// </summary>
	public static Color Family(KinCore.Party.Family family, string name = "") =>
		family switch
		{
			KinCore.Party.Family.Grove => Color.FromHtml("#3F7A34"),
			KinCore.Party.Family.Ember => Color.FromHtml("#C2621F"),
			KinCore.Party.Family.Storm => Color.FromHtml("#2F72A8"),
			KinCore.Party.Family.Mire => Color.FromHtml("#6A4A8E"),
			_ => Companion(name),
		};

	public static Color Companion(string name) =>
		name switch
		{
			"Bramble" => Color.FromHtml("#2F6A3A"),
			"Pike" => Color.FromHtml("#2A4F86"),
			"Gale" => Color.FromHtml("#5A3F80"),
			"Magpie" => Color.FromHtml("#2E6F6A"),
			"Inkling" => Color.FromHtml("#6A4A2A"),
			"Hoard Drake" => Color.FromHtml("#5E6B2E"),
			"Emberling" => Color.FromHtml("#8A4A2E"),
			"Echo Owl" => Color.FromHtml("#4A3F6A"),
			"Warden" => Color.FromHtml("#4F5A63"),
			"Glowmoth" => Color.FromHtml("#6A6A2A"),
			"Stormbuck" => Color.FromHtml("#2F4F6F"),
			"Hushcap" => Color.FromHtml("#6A3A5A"),
			"Broodvine" => Color.FromHtml("#3F5A2A"),
			"Howler" => Color.FromHtml("#5A4A3A"),
			"Ironhorn" => Color.FromHtml("#4A4A55"),
			// Tokens: pale, so a body that fades reads as lesser than a monster.
			"Sprout" or "Spark" or "Decoy" or "Grub" => Color.FromHtml("#56606A"),
			// Dull on purpose: a companion without a colour should LOOK unfinished.
			_ => Slate,
		};

	public static StyleBoxFlat Box(Color fill, Color? border = null, int borderWidth = 2)
	{
		var box = new StyleBoxFlat
		{
			BgColor = fill,
			CornerRadiusTopLeft = 6,
			CornerRadiusTopRight = 6,
			CornerRadiusBottomLeft = 6,
			CornerRadiusBottomRight = 6,
			ContentMarginLeft = 8,
			ContentMarginRight = 8,
			ContentMarginTop = 6,
			ContentMarginBottom = 6,
		};

		if (border is { } b)
		{
			box.BorderColor = b;
			box.BorderWidthLeft = borderWidth;
			box.BorderWidthRight = borderWidth;
			box.BorderWidthTop = borderWidth;
			box.BorderWidthBottom = borderWidth;
		}

		return box;
	}

	/// <summary>
	/// A number in a coloured disc. The board's one repeated unit: attack and life in a lane, energy
	/// and life in the status strip. Same shape everywhere, so colour is the only thing carrying
	/// meaning — navy is a number, red is life, gold is yours.
	/// </summary>
	public static (PanelContainer Panel, Label Label) Pip(Color fill, int fontSize = 15)
	{
		var box = Box(fill, fill);
		box.CornerRadiusTopLeft = box.CornerRadiusTopRight = 16;
		box.CornerRadiusBottomLeft = box.CornerRadiusBottomRight = 16;
		box.ContentMarginLeft = box.ContentMarginRight = 10;
		box.ContentMarginTop = box.ContentMarginBottom = 2;

		var panel = new PanelContainer();
		panel.AddThemeStyleboxOverride("panel", box);

		var label = Text("", fontSize, Bone);
		panel.AddChild(label);

		return (panel, label);
	}

	/// <summary>
	/// A PascalCase content name as display text: `AiUprising` -> "AI UPRISING".
	///
	/// Formatting, not content. The scenarios have no authored display name and do not need one —
	/// but `AIUPRISING` on a screen is a bug in the typography, and two screens spelling the same
	/// doom differently is worse.
	/// </summary>
	public static string Caps(string pascalCase) =>
		Regex.Replace(pascalCase, "(?<!^)([A-Z])", " $1").ToUpperInvariant();

	public static Label Text(
		string text,
		int size,
		Color colour,
		HorizontalAlignment align = HorizontalAlignment.Center
	)
	{
		var label = new Label { Text = text, HorizontalAlignment = align };
		label.AddThemeFontSizeOverride("font_size", size);
		label.AddThemeColorOverride("font_color", colour);
		return label;
	}
}
