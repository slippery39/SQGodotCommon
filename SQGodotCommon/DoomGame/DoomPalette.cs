using Godot;

namespace DoomGame;

/// <summary>
/// The five flat colours, and only five. See DoomUI.md.
///
/// **Gold and Red are reserved and must not be spent on decoration.** Gold means "yours, and
/// precious" — the Companion and your energy, nothing else, which is what makes the Companion
/// findable across five lanes. Red means "the enemy, and life".
///
/// Red doing double duty on enemy health and your unit's remaining toughness is deliberate: a unit
/// absorbs up to its remaining toughness and the excess hits the face behind it, so a body in a lane
/// is worth exactly its toughness in life. One colour for both says that without a tutorial.
/// </summary>
public static class DoomPalette
{
	public static readonly Color Navy = Color.FromHtml("#16212E");
	public static readonly Color Slate = Color.FromHtml("#233444");
	public static readonly Color Bone = Color.FromHtml("#E8EEF2");
	public static readonly Color Gold = Color.FromHtml("#E3B23C");
	public static readonly Color Red = Color.FromHtml("#C73E3A");

	/// <summary>An empty lane: darker than the frame, so a held lane reads as the exception.</summary>
	public static readonly Color EmptySlot = Color.FromHtml("#1B2836");

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
