using Godot;

namespace MtgGame;

public static class MtgUiStyles
{
	public static readonly Color DarkBg = new(0.06f, 0.06f, 0.10f, 1f);
	public static readonly Color CardBg = new(0.10f, 0.10f, 0.16f, 1f);
	public static readonly Color GoldBorder = new(0.80f, 0.62f, 0.22f, 1f);
	public static readonly Color DimBorder = new(0.30f, 0.30f, 0.35f, 1f);
	public static readonly Color ArtBlock = new(0.22f, 0.16f, 0.08f, 1f);

	public static StyleBoxFlat DarkPanel(int borderWidth = 1, Color? borderColor = null)
	{
		var s = new StyleBoxFlat();
		s.BgColor = DarkBg;
		s.SetBorderWidthAll(borderWidth);
		s.BorderColor = borderColor ?? GoldBorder;
		s.CornerRadiusTopLeft =
			s.CornerRadiusTopRight =
			s.CornerRadiusBottomLeft =
			s.CornerRadiusBottomRight =
				4;
		return s;
	}

	public static StyleBoxFlat ButtonNormal()
	{
		var s = new StyleBoxFlat();
		s.BgColor = DarkBg;
		s.SetBorderWidthAll(2);
		s.BorderColor = GoldBorder;
		s.CornerRadiusTopLeft =
			s.CornerRadiusTopRight =
			s.CornerRadiusBottomLeft =
			s.CornerRadiusBottomRight =
				4;
		return s;
	}

	public static StyleBoxFlat ButtonHover()
	{
		var s = new StyleBoxFlat();
		s.BgColor = new Color(0.15f, 0.12f, 0.05f, 1f);
		s.SetBorderWidthAll(2);
		s.BorderColor = new Color(1f, 0.82f, 0.35f, 1f);
		s.CornerRadiusTopLeft =
			s.CornerRadiusTopRight =
			s.CornerRadiusBottomLeft =
			s.CornerRadiusBottomRight =
				4;
		return s;
	}

	public static StyleBoxFlat ButtonDisabled()
	{
		var s = new StyleBoxFlat();
		s.BgColor = new Color(0.04f, 0.04f, 0.07f, 1f);
		s.SetBorderWidthAll(1);
		s.BorderColor = DimBorder;
		s.CornerRadiusTopLeft =
			s.CornerRadiusTopRight =
			s.CornerRadiusBottomLeft =
			s.CornerRadiusBottomRight =
				4;
		return s;
	}

	public static StyleBoxFlat ManaPip(bool filled)
	{
		var s = new StyleBoxFlat();
		s.BgColor = filled ? GoldBorder : DimBorder;
		s.CornerRadiusTopLeft =
			s.CornerRadiusTopRight =
			s.CornerRadiusBottomLeft =
			s.CornerRadiusBottomRight =
				7;
		return s;
	}
}
