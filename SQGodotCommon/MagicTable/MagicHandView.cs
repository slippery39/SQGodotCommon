using System;
using System.Collections.Generic;
using System.Linq;
using Common.Cards;

namespace MagicTable;

/// <summary>
/// Your hand as Common's fan — `Hand2D` + `Card2D` — copied and adapted from KIN's `KinHandView`
/// (story TBL-3). Each card's face is its Scryfall image, whole: the printed Oracle text is the
/// point of the table, so no frame or text layer is drawn over it. Presentation only — a click or
/// a drop hands the card id to the table, which asks the engine.
///
/// <b>Click and drag both work</b> (Arena has both). On a mouse a `Card2D` drag starts on the
/// PRESS, which clears the hover its click needs, so a draggable card can never be clicked
/// (godot-frontend.md). So dragging is enabled only once the pointer has moved
/// <see cref="DragDistance"/> from the press: the press merely arms the drag, and a press and release
/// that never moved is a click — taken on the RELEASE, like every card on the table
/// (`CardViews.Hook`). The table feeds its input to <see cref="Track"/>.
/// </summary>
public sealed class MagicHandView
{
	/// <summary>The fan's scale: a Card2D is 315 tall in its own space; this makes it ~200, as the old hand.</summary>
	public const float Scale = 0.63f;

	/// <summary>A pointer that moves this far from its press is dragging, not clicking (design pixels).</summary>
	private const float DragDistance = 12;

	/// <summary>A press held this long zooms instead of clicking — the same as every card (CardViews.Hook).</summary>
	private const double HoldSeconds = 0.4;

	/// <summary>Half a Card2D's width in the hand's own space (its HoverArea is 232 wide).</summary>
	private const float HalfCard = 116;

	private readonly Hand2D _hand;
	private readonly Func<int, string?> _play;
	private readonly Action<int> _click;
	private readonly Action<string?> _preview;
	private readonly Action<string> _report;
	private readonly Func<bool> _covered;

	/// <summary>Each card id's last face — the preview reads it, and a card dragged out of a hand that no longer holds it keeps it.</summary>
	private readonly Dictionary<string, (int Id, string Image, Glow Glow, string? From)> _faces =
		new();

	private Vector2 _pressAt;
	private Vector2 _pointer;
	private bool _down;
	private bool _moved;
	private bool _zoomed;
	private int _press;
	private CardUI2D? _pressed;

	/// <param name="play">A card dropped on the table: null when it went to the table's router, else why it can't be played.</param>
	/// <param name="click">A card clicked — the same router.</param>
	/// <param name="report">Says a refused drop's reason — a card that silently slides back tells the player nothing.</param>
	/// <param name="covered">A panel is open over the table: the fan takes no click or drop under it.</param>
	public MagicHandView(
		Node parent,
		Func<int, string?> play,
		Action<int> click,
		Action<string?> preview,
		Action<string> report,
		Func<bool> covered
	)
	{
		(_play, _click, _preview, _report, _covered) = (play, click, preview, report, covered);

		// The scene, not `new Hand2D()`: Hand.tscn carries the curves, the card scene and the
		// LeftMostPoint / RightMostPoint anchors Hand2D looks up by name.
		_hand = GD.Load<PackedScene>("res://Common/Cards/2D/Hand2D/Hand.tscn")
			.Instantiate<Hand2D>();
		_hand.Scale = new Vector2(Scale, Scale);
		// The pressed card only, once the pointer has moved. The live mouse is read HERE, when a drag
		// asks: the drag sees each motion before the table's Track does, so the motion that crosses
		// the line would otherwise start nothing (a quick flick fell between). The last event's point
		// as well: synthetic input (--tap) moves no real mouse.
		_hand.DragEnabled = () =>
			_down
			&& !_zoomed
			&& !_covered()
			&& CardUIManager.CurrentHoveredCard == _pressed
			&& Mathf.Max(
				_hand.GetGlobalMousePosition().DistanceTo(_pressAt),
				_pointer.DistanceTo(_pressAt)
			) > DragDistance;
		parent.AddChild(_hand);
		// Hand2D's _Input is a debug hook alone — "D" draws a blank card, even while typing in the
		// sandbox's search. A card with no id broke every redraw after it.
		_hand.SetProcessInput(false);

		// AFTER AddChild: Hand2D._Ready assigns its own handler, which would overwrite this.
		_hand.CardDragEnd = Dropped;
	}

	/// <summary>
	/// Where the fan sits: centred over the hand's space, as wide as it, its band's top the line a
	/// drop must cross to be a play.
	/// </summary>
	public void Place(Rect2 space)
	{
		_hand.GlobalPosition = space.GetCenter() - new Vector2(0, 10);
		_bandTop = space.Position.Y;
		var half = Mathf.Max(0, space.Size.X / 2 / Scale - HalfCard);
		_hand.GetNode<Node2D>("LeftMostPoint").Position = new Vector2(-half, 38);
		_hand.GetNode<Node2D>("RightMostPoint").Position = new Vector2(half, 27);
	}

	private float _bandTop;

	/// <summary>The table's input, every event: click, hold-to-zoom, the preview (see the class summary).</summary>
	public void Track(InputEvent e)
	{
		var hovered =
			CardUIManager.CurrentHoveredCard is { } h && _hand.GetCards().Contains(h) ? h : null;
		switch (e)
		{
			case InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true }
				when hovered is not null && !_covered():
				CardViews.Zoom?.Invoke(Image(hovered));
				break;
			case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } press:
				(_pressAt, _pointer, _down, _moved, _zoomed, _pressed) = (
					press.GlobalPosition,
					press.GlobalPosition,
					true,
					false,
					false,
					hovered
				);
				var mine = ++_press;
				if (hovered is not null && CardViews.Zoom is not null)
					_hand.GetTree().CreateTimer(HoldSeconds).Timeout += () =>
					{
						if (
							mine == _press
							&& _down
							&& !_moved
							&& CardUIManager.DraggingCard is null
						)
						{
							_zoomed = true;
							CardViews.Zoom?.Invoke(Image(hovered));
						}
					};
				break;
			case InputEventMouseMotion motion:
				_pointer = motion.GlobalPosition;
				if (_down && _pointer.DistanceTo(_pressAt) > DragDistance)
					_moved = true;
				// The card that is raised is the one a click plays: preview THAT one.
				if (hovered is not null && !_down)
					_preview(Image(hovered));
				break;
			case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false }:
				if (
					_down
					&& !_moved
					&& !_zoomed
					&& !_covered()
					&& hovered is not null
					&& hovered == _pressed
					&& int.TryParse(hovered.Id, out var id)
				)
					_click(id);
				_press++;
				(_down, _moved, _zoomed, _pressed) = (false, false, false, null);
				break;
		}
	}

	private string? Image(CardUI2D ui) =>
		ui.Id is not null && _faces.TryGetValue(ui.Id, out var face) ? face.Image : null;

	private void Dropped(Hand2D.DragEndContext context)
	{
		var card = context.CardUI2D;
		// Released over the hand, or under an open panel: it went back — not a play, nothing to report.
		if (!int.TryParse(card.Id, out var id) || context.DragEndPoint.Y > _bandTop || _covered())
		{
			_hand.LerpCardTransform(card);
			return;
		}
		var refusal = _play(id);
		// Played: the redraw takes it out of the fan. Waiting for a target, or refused: home it goes.
		if (_hand.GetCards().Contains(card))
			_hand.LerpCardTransform(card);
		if (refusal is not null)
			_report(refusal);
	}

	/// <summary>
	/// Brings the fan into line with these cards, matched by id, so a redraw adds and removes only
	/// what changed (a Card2D is a living node with tweens — KIN's Sync). A card being DRAGGED stays
	/// until it is let go: freed mid-drag, it left Common's static "dragging" set forever, and every
	/// drag after it was refused (an AI move or Ctrl+Z redraws during a drag).
	/// </summary>
	public void Sync(IReadOnlyList<(int Id, string Image, Glow Glow, string? From)> cards)
	{
		var wanted = cards.ToDictionary(c => c.Id.ToString());
		foreach (var ui in _hand.GetCards().ToList())
			if ((ui.Id is null || !wanted.ContainsKey(ui.Id)) && CardUIManager.DraggingCard != ui)
				_hand.DiscardCard(ui.Id ?? "");

		var present = _hand.GetCards().Select(c => c.Id).ToHashSet();
		foreach (var card in cards)
			if (!present.Contains(card.Id.ToString()))
			{
				// From below the fan: the parameterless DrawCard flies in from the screen's left edge.
				var ui = _hand.DrawCard(_hand.GlobalPosition + new Vector2(0, 340));
				ui.Id = card.Id.ToString();
			}

		foreach (var card in cards)
			_faces[card.Id.ToString()] = card;
		// Positional: in the order the fan holds its cards, not the order the zone does.
		_hand.SetCardsDetails([.. _hand.GetCards().Select(ui => Face(_faces[ui.Id]))]);
	}

	/// <summary>The card's face: its Scryfall image as the whole card, the glow behind it.</summary>
	private static InternalCardUI2D.Details Face(
		(int Id, string Image, Glow Glow, string? From) card
	)
	{
		var image = MagicArt.Full(card.Image);
		return new InternalCardUI2D.Details
		{
			Id = card.Id.ToString(),
			CardName = card.Image,
			MainFrameTexture = image,
			// The real card's own colours: no tint, no holographic shimmer over the printed text.
			FrameColor = Colors.White,
			Holographic = false,
			Decorate = internalCard => Decorate(internalCard, image, card.Glow, card.From),
		};
	}

	/// <summary>The frame's own height (MainFrame.PNG is 312x445): the image is scaled to it.</summary>
	private const float FrameHeight = 445;

	/// <summary>
	/// On the instance, never the shared scene; idempotent, since a reused node is applied many
	/// times: every layer but the frame hidden, the frame holding the image, the glow as a plate
	/// behind it (the shader's outline traces transparency, and a Scryfall JPG has none), a badge
	/// for "from …".
	/// </summary>
	private static void Decorate(InternalCardUI2D card, Texture2D? image, Glow glow, string? from)
	{
		var container = card.GetNode<Node2D>("%CardContainer");
		foreach (var layer in container.GetChildren().OfType<CanvasItem>())
			layer.Visible = layer.Name.ToString() is "MainFrame" or FromBadge or GlowPlate;
		var size = new Vector2(312, FrameHeight);
		if (image is not null)
		{
			var scale = FrameHeight / image.GetHeight();
			container.GetNode<Sprite2D>("MainFrame").Scale = Vector2.One * scale;
			size = image.GetSize() * scale;
		}

		if (container.GetNodeOrNull<ColorRect>(GlowPlate) is not { } plate)
		{
			plate = new ColorRect
			{
				Name = GlowPlate,
				MouseFilter = Control.MouseFilterEnum.Ignore,
			};
			container.AddChild(plate);
			container.MoveChild(plate, 0); // behind the image
		}
		var edge = glow is Glow.Choosable or Glow.Selected ? 12 : 8;
		plate.Position = -size / 2 - new Vector2(edge, edge);
		plate.Size = size + new Vector2(2 * edge, 2 * edge);
		plate.Color = CardViews.Outline(glow).BorderColor;
		plate.Visible = glow != Glow.None;

		if (container.GetNodeOrNull<Label>(FromBadge) is not { } badge)
		{
			badge = new Label
			{
				Name = FromBadge,
				Position = new Vector2(-150, 160),
				Size = new Vector2(300, 50),
				HorizontalAlignment = HorizontalAlignment.Center,
			};
			badge.AddThemeFontSizeOverride("font_size", 32);
			badge.AddThemeColorOverride("font_color", new Color("f4e29b"));
			var back = new StyleBoxFlat { BgColor = new Color(0.1f, 0.06f, 0.03f, 0.85f) };
			back.SetCornerRadiusAll(6);
			badge.AddThemeStyleboxOverride("normal", back);
			container.AddChild(badge);
		}
		badge.Text = from is null ? "" : $"from {from}";
		badge.Visible = from is not null;
	}

	private const string FromBadge = "MagicFromBadge";
	private const string GlowPlate = "MagicGlowPlate";
}
