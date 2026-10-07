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

	/// <summary>
	/// The scale of a full-card preview. On a phone a card's rules text only reads when the card
	/// is near full screen height (445 x 2.2 = 979 of 1080); a desktop keeps the caller's size.
	/// </summary>
	public static float PreviewScale(float desktop) => OS.HasFeature("mobile") ? 2.2f : desktop;

	/// <summary>A screen or section title: Cinzel, gold, outlined — it sits on the backdrop.</summary>
	public static Label Title(string text, int size = 52) =>
		new()
		{
			Text = text,
			HorizontalAlignment = HorizontalAlignment.Center,
			LabelSettings = new LabelSettings
			{
				Font = TitleFont,
				FontSize = size,
				FontColor = GoldBorder,
				OutlineSize = size / 6,
				OutlineColor = new Color(0, 0, 0, 0.85f),
			},
		};

	/// <summary>
	/// A choice among several (a deck, a menu entry): a leather plate, or PARCHMENT with dark ink
	/// when selected — the one selected state every screen uses.
	/// </summary>
	public static void StyleChoice(Button button, bool selected)
	{
		var idle = selected ? Banner() : ButtonNormal();
		button.AddThemeStyleboxOverride("normal", idle);
		button.AddThemeStyleboxOverride("pressed", idle);
		button.AddThemeStyleboxOverride("hover", selected ? Banner() : ButtonHover());
		button.AddThemeStyleboxOverride("disabled", ButtonDisabled());
		button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
		var ink = selected ? Ink : new Color("f2e6c8");
		button.AddThemeColorOverride("font_color", ink);
		button.AddThemeColorOverride("font_hover_color", selected ? Ink : Colors.White);
		button.AddThemeColorOverride("font_pressed_color", ink);
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
