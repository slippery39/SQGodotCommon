using System;

namespace MtgGame;

/// <summary>
/// A zone as a small stack beside a hero portrait — library, graveyard, exile — with its count on
/// a brass plate. The face is a card back or the top card's art. Clickable when it has cards and
/// somewhere to show them; replaces the old "Graveyard (3)" text buttons.
/// </summary>
public partial class ZonePile : Control
{
	public static readonly Vector2 PileSize = new(66, 92);

	private TextureRect _face = null!;
	private Label _count = null!;
	private bool _clickable;

	public event Action? Clicked;

	public override void _Ready()
	{
		CustomMinimumSize = PileSize;
		MouseFilter = MouseFilterEnum.Stop;

		// A second back peeking out underneath makes it read as a pile, not one card.
		var under = new Panel
		{
			Position = new Vector2(4, -4),
			Size = PileSize,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		under.AddThemeStyleboxOverride("panel", MtgUiStyles.ButtonDisabled());
		AddChild(under);

		var frame = new Panel { Size = PileSize, MouseFilter = MouseFilterEnum.Ignore };
		frame.AddThemeStyleboxOverride("panel", MtgUiStyles.ButtonNormal());
		AddChild(frame);
		_face = new TextureRect
		{
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
			Position = new Vector2(4, 4),
			Size = PileSize - new Vector2(8, 8),
			MouseFilter = MouseFilterEnum.Ignore,
		};
		AddChild(_face);

		var plate = new Panel
		{
			Position = new Vector2(10, PileSize.Y - 22),
			Size = new Vector2(PileSize.X - 20, 30),
			MouseFilter = MouseFilterEnum.Ignore,
		};
		plate.AddThemeStyleboxOverride("panel", MtgUiStyles.ButtonNormal());
		AddChild(plate);
		_count = new Label
		{
			LabelSettings = new LabelSettings
			{
				Font = MtgUiStyles.TitleFont,
				FontSize = 20,
				FontColor = MtgUiStyles.GoldBorder,
				OutlineSize = 5,
				OutlineColor = Colors.Black,
			},
			Size = plate.Size,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		plate.AddChild(_count);

		GuiInput += e =>
		{
			if (
				_clickable
				&& e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }
			)
				Clicked?.Invoke();
		};
	}

	/// <param name="face">Card back, or the top card's art. Null shows an empty slot.</param>
	/// <param name="clickable">Whether a click opens a viewer — never for an empty pile.</param>
	public void Refresh(Texture2D? face, int count, string name, bool clickable)
	{
		_face.Texture = count > 0 ? face : null;
		_count.Text = $"{count}";
		_clickable = clickable && count > 0;
		TooltipText = name;
		MouseDefaultCursorShape = _clickable ? CursorShape.PointingHand : CursorShape.Arrow;
		Modulate = count > 0 ? Colors.White : new Color(1, 1, 1, 0.55f);
	}
}
