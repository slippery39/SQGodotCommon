using Common.Cards;

namespace MtgGame;

public partial class CardPreviewPopup : CanvasLayer
{
	[Export]
	public PackedScene InternalCardScene { get; set; }

	/// <summary>
	/// Preview scale. Must be set before the node enters the tree — <see cref="_Ready"/> applies
	/// it. The default suits the battlefield's small cards; a screen showing larger cards needs
	/// a larger preview, or hovering shrinks the card instead of magnifying it.
	/// </summary>
	[Export]
	public float PreviewScale { get; set; } = 0.65f;

	private InternalCardUI2D _cardNode;

	// Card native size, before scaling.
	private const float CardWidth = 300f;
	private const float CardHeight = 470f;

	private float HalfW => CardWidth * PreviewScale / 2f;
	private float HalfH => CardHeight * PreviewScale / 2f;

	public override void _Ready()
	{
		Layer = 3;

		if (InternalCardScene != null)
		{
			_cardNode = InternalCardScene.Instantiate<InternalCardUI2D>();
			_cardNode.Scale = new Vector2(PreviewScale, PreviewScale);
			AddChild(_cardNode);
		}

		Hide();
	}

	public void ShowCard(InternalCardUI2D.Details details, Vector2 cursorPos)
	{
		if (_cardNode == null)
			return;

		details.ApplyTo(_cardNode);
		_cardNode.OutlineColor = new Color(0, 0, 0, 0);
		_cardNode.OutlineThickness = 0f;
		_cardNode.Modulate = Colors.White;

		// Position card above-right of cursor, clamped to viewport.
		// Node2D Position is the center of the card.
		var viewport = GetViewport().GetVisibleRect();
		var topLeft = cursorPos + new Vector2(20f, -HalfH * 2 - 10f);
		topLeft.X = Mathf.Clamp(topLeft.X, 0f, viewport.Size.X - HalfW * 2);
		topLeft.Y = Mathf.Clamp(topLeft.Y, 0f, viewport.Size.Y - HalfH * 2);
		_cardNode.Position = topLeft + new Vector2(HalfW, HalfH);

		Show();
	}

	public void HideCard() => Hide();
}
