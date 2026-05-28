using Common.Cards;

namespace MtgGame;

public partial class CardPreviewPopup : CanvasLayer
{
	[Export]
	public PackedScene InternalCardScene { get; set; }

	private Control _panel;
	private InternalCardUI2D _cardNode;

	public override void _Ready()
	{
		Layer = 3;

		_panel = new PanelContainer();
		_panel.AddThemeStyleboxOverride("panel", MtgUiStyles.DarkPanel(borderWidth: 2));
		_panel.MouseFilter = Control.MouseFilterEnum.Ignore;
		AddChild(_panel);

		if (InternalCardScene != null)
		{
			_cardNode = InternalCardScene.Instantiate<InternalCardUI2D>();
			_cardNode.Scale = new Vector2(0.65f, 0.65f);
			// Offset the Node2D origin so the card sits flush in the top-left of the panel.
			// Native card extends ±150px wide and from -235 to +235 tall (centered at 0,0).
			// At 0.65 scale: ~195px wide, ~305px tall. Shift by half those values.
			_cardNode.Position = new Vector2(97, 152);
			_panel.AddChild(_cardNode);
			_panel.CustomMinimumSize = new Vector2(195, 305);
		}

		Hide();
	}

	public void ShowCard(InternalCardUI2D.Details details, Vector2 cursorPos)
	{
		if (_cardNode == null)
			return;

		details.ApplyTo(_cardNode);

		// Clear outline — preview is display-only
		_cardNode.OutlineColor = new Color(0, 0, 0, 0);
		_cardNode.OutlineThickness = 0f;

		// Position near cursor, offset above-right, clamped to viewport
		var viewport = GetViewport().GetVisibleRect();
		var panelSize = _panel.CustomMinimumSize;
		var x = Mathf.Clamp(cursorPos.X + 20f, 0f, viewport.Size.X - panelSize.X);
		var y = Mathf.Clamp(cursorPos.Y - panelSize.Y - 10f, 0f, viewport.Size.Y - panelSize.Y);
		_panel.SetPosition(new Vector2(x, y));

		Show();
	}

	public void HideCard() => Hide();
}
