using Godot;

namespace KinGame;

/// <summary>
/// **The style-D UI textures as styles** — the bevelled, gradient, gold-rimmed pieces made by
/// `tools/make_ui.py` into `Art/ui/`, handed out as 9-slice `StyleBoxTexture`s. This is what keeps
/// a panel from being a flat Godot colour box. Margins here must match the corner sizes there.
/// </summary>
public static class KinUiKit
{
	/// <summary>
	/// A 9-slice of `Art/ui/<name>.png`: <paramref name="side"/> px of each left/right edge and
	/// <paramref name="topBottom"/> px of each top/bottom edge are kept, the middle stretches.
	/// </summary>
	public static StyleBox Nine(
		string name,
		int side,
		int topBottom,
		Color? tint = null,
		int padX = 18,
		int padY = 8
	)
	{
		var texture = KinArt.Drawing("ui/" + name);
		if (texture is null)
			// Missing art must not break a screen: the old flat look, not a blank.
			return KinPalette.Box(new Color(KinPalette.Navy, 0.92f), KinPalette.Gold, 2);

		return new StyleBoxTexture
		{
			Texture = texture,
			TextureMarginLeft = side,
			TextureMarginRight = side,
			TextureMarginTop = topBottom,
			TextureMarginBottom = topBottom,
			ContentMarginLeft = padX,
			ContentMarginRight = padX,
			ContentMarginTop = padY,
			ContentMarginBottom = padY,
			ModulateColor = tint ?? Colors.White,
		};
	}

	/// <summary>A rounded plate with a bevelled rim: "gold" (yours / pressable), "red" (theirs), "bone" (information).</summary>
	public static StyleBox Plate(string rim, Color? tint = null) =>
		Nine("plate_" + rim, 16, 16, tint);

	/// <summary>
	/// **A button in the kit.** <paramref name="hex"/> for the big hex-ended one (END TURN); a
	/// plate otherwise. Pressed and disabled are the same texture, tinted.
	/// </summary>
	public static void Style(Button button, int fontSize, bool hex = false)
	{
		StyleBox Normal(Color? tint = null) =>
			hex ? Nine("button_hex", 44, 0, tint, 44, 8) : Plate("gold", tint);
		StyleBox Hover() =>
			hex ? Nine("button_hex_hover", 44, 0, null, 44, 8) : Nine("plate_gold_hover", 16, 16);

		button.AddThemeFontSizeOverride("font_size", fontSize);
		button.AddThemeColorOverride("font_color", KinPalette.Bone);
		button.AddThemeColorOverride("font_hover_color", KinPalette.Gold);
		button.AddThemeColorOverride("font_pressed_color", KinPalette.Gold);
		button.AddThemeConstantOverride("outline_size", 6);
		button.AddThemeColorOverride("font_outline_color", new Color(0.04f, 0.06f, 0.09f));
		button.AddThemeStyleboxOverride("normal", Normal());
		button.AddThemeStyleboxOverride("hover", Hover());
		button.AddThemeStyleboxOverride("pressed", Normal(new Color(0.8f, 0.8f, 0.85f)));
		button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
		button.AddThemeStyleboxOverride("disabled", Normal(new Color(0.55f, 0.55f, 0.6f, 0.8f)));
	}
}
