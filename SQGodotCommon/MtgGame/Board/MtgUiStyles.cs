using Godot;
using Godot.Collections;

namespace MtgGame;

/// <summary>
/// The Dark Arcane kit (MtgVisualDesign.md §6): brass-rimmed leather plates, an oxblood primary
/// button, parchment banners, Cinzel for titles. Textures come from <c>tools/make_mtg_ui.py</c> as
/// 9-slices — a solid <see cref="StyleBoxFlat"/> is what reads as "default Godot". Body text is
/// EB Garamond through the project theme (<c>MtgGame/Assets/mtg_theme.tres</c>), so it needs no
/// helper.
/// </summary>
public static class MtgUiStyles
{
	private const string UiDir = "res://MtgGame/Assets/UI/";

	/// Must match CORNER in tools/make_mtg_ui.py.
	private const int Corner = 16;

	/// The primary button's pointed ends; stretched only between them.
	private const int PointEnd = 28;

	public static readonly Color DarkBg = new("2d170c");
	public static readonly Color CardBg = new("292219");
	public static readonly Color GoldBorder = new("e6c77e");
	public static readonly Color DimBorder = new("7a706a");
	public static readonly Color ArtBlock = new("371a0c");
	public static readonly Color Ink = new("2a1a0c");

	public static StyleBoxTexture DarkPanel() => Plate("plate", 14);

	public static StyleBoxTexture ButtonNormal() => Plate("plate", 8);

	public static StyleBoxTexture ButtonHover() => Plate("plate_hover", 8);

	public static StyleBoxTexture ButtonDisabled() => Plate("plate_disabled", 8);

	public static StyleBoxTexture Banner() => Plate("banner", 12);

	/// <summary>
	/// The table a screen sits on, behind everything. Its own CanvasLayer at -1: the default layer
	/// 1 draws OVER plain Node2D children such as the hand (godot-frontend.md).
	/// </summary>
	public static void AddBackdrop(Node parent)
	{
		var layer = new CanvasLayer { Layer = -1 };
		// ExpandMode before Texture, or the texture's pixel size becomes the minimum size.
		var image = new TextureRect
		{
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
			Texture = GD.Load<Texture2D>(UiDir + "backdrop.jpg"),
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		image.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		layer.AddChild(image);
		parent.AddChild(layer);
	}

	/// <summary>The oxblood END TURN / PICK button, in Cinzel.</summary>
	public static void StylePrimary(Button button, int fontSize = 30)
	{
		button.AddThemeStyleboxOverride("normal", Primary("button"));
		button.AddThemeStyleboxOverride("pressed", Primary("button"));
		button.AddThemeStyleboxOverride("hover", Primary("button_hover"));
		button.AddThemeStyleboxOverride("disabled", Primary("button_disabled"));
		button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
		button.AddThemeFontOverride("font", TitleFont);
		button.AddThemeFontSizeOverride("font_size", fontSize);
		button.AddThemeColorOverride("font_color", GoldBorder);
		button.AddThemeColorOverride("font_hover_color", new Color("fff0c0"));
		button.AddThemeColorOverride("font_disabled_color", DimBorder);
	}

	/// <summary>Cinzel Bold — banners, buttons, big numerals.</summary>
	public static readonly Font TitleFont = new FontVariation
	{
		BaseFont = GD.Load<FontFile>("res://MtgGame/Assets/Fonts/Cinzel.ttf"),
		VariationOpentype = new Dictionary
		{
			{ TextServerManager.GetPrimaryInterface().NameToTag("wght"), 700 },
		},
	};

	public static StyleBoxFlat ManaPip(bool filled)
	{
		var s = new StyleBoxFlat();
		s.BgColor = filled ? GoldBorder : DimBorder;
		s.SetCornerRadiusAll(7);
		return s;
	}

	private static StyleBoxTexture Plate(string name, int padding) =>
		NineSlice(name, Corner, Corner, padding, padding);

	private static StyleBoxTexture Primary(string name) =>
		NineSlice(name, PointEnd, Corner, PointEnd, 6);

	private static StyleBoxTexture NineSlice(
		string name,
		int sliceX,
		int sliceY,
		int padX,
		int padY
	)
	{
		var s = new StyleBoxTexture { Texture = GD.Load<Texture2D>(UiDir + name + ".png") };
		s.TextureMarginLeft = s.TextureMarginRight = sliceX;
		s.TextureMarginTop = s.TextureMarginBottom = sliceY;
		s.ContentMarginLeft = s.ContentMarginRight = padX;
		s.ContentMarginTop = s.ContentMarginBottom = padY;
		return s;
	}
}
