using System;
using System.Collections.Generic;
using Common.Core;

namespace Common.Cards;

public partial class CardUI2D : Node2D
{
	private InternalCardUI2D _internalCardUI2D;
	private Node2D _hoverContainer;

	[Export]
	public float HoverScale { get; set; } = 0.75f;

	[Export]
	public int HoverZIndex { get; set; } = 100;

	[Export]
	public int DragZIndex { get; set; } = 100;

	public string Id { get; set; }

	/// <summary>
	/// ZIndex that should persist even after hovering over the card.
	/// </summary>
	public int SavedZIndex
	{
		get { return _savedZIndex; }
		set
		{
			_savedZIndex = value;
			ZIndex = value;
		}
	}

	private int _savedZIndex = 0;

	private Vector2 _savedPosition = Vector2.Zero;

	private Tween _currentTween;

	[Export]
	public float TweenAnimationTime { get; set; } = 0.1f;

	public bool IsPosLerping { get; set; } = false;

	/// <summary>
	/// The enlarged card which appears when the mouse is hovering over it.
	/// </summary>
	private Node2D _hoverCard = null;
	public Action<DragEndContext> DragEnd { get; set; }
	public event Action<CardUI2D, bool> SelectionChanged;

	/// <summary>
	/// Stores whether the card is selected for some external purpose (i.e, resolving a spell that needs you to select certain cards)
	/// </summary>
	public bool IsSelected { get; private set; }

	public void SetSelected(bool selected)
	{
		if (IsSelected != selected)
		{
			IsSelected = selected;
			UpdateSelectionVisual();
			SelectionChanged?.Invoke(this, selected);
		}
	}

	private void UpdateSelectionVisual()
	{
		// Add glow, border, or other visual feedback
	}

	public override void _Ready()
	{
		_internalCardUI2D =
			GetNodeOrNull<InternalCardUI2D>("InternalCard2D")
			?? throw new InvalidOperationException("Could not find IntenalCard2D in CardUI2D");
		_hoverContainer =
			GetNodeOrNull<Node2D>("HoverContainer")
			?? throw new InvalidOperationException("Could not find HoverContainer in CardUI2D");

		var dragNode2D =
			GetNodeOrNull<DraggableNode2D>("DraggableNode2d")
			?? throw new InvalidOperationException("Could not find DraggableNode2D in CardUI2D");

		dragNode2D.CanDrag = () =>
			CardUIManager.CurrentHoveredCard == this || CardUIManager.DraggingCard == this;
		dragNode2D.OnDragBegin += (drag) => _DragBegin();
		dragNode2D.OnDragEnd += (drag) => _DragEnd(drag);

		SavedZIndex = ZIndex;
	}

	public class DragEndContext
	{
		public CardUI2D CardUI2D { get; set; }
		public List<Area2D> SelectedAreas { get; set; }
		public Vector2 DragEndPoint { get; set; }
	}

	private void _DragBegin()
	{
		GD.Print("Drag begun");

		CardUIManager.DraggingCard = this;
		_internalCardUI2D.ZIndex = 100;
		GetNode<DraggableNode2D>("DraggableNode2d").Scale =
			new Vector2(HoverScale, HoverScale) / 0.5f;
	}

	private void _DragEnd(DraggableNode2D draggableNode2D)
	{
		if (CardUIManager.DraggingCard == this)
		{
			GD.Print("Drag ended");
			CardUIManager.DraggingCard = null;
			_internalCardUI2D.ZIndex = 0;
			GetNode<DraggableNode2D>("DraggableNode2d").Scale = new Vector2(1, 1);

			DragEnd(
				new DragEndContext
				{
					CardUI2D = this,
					SelectedAreas = draggableNode2D.GetSelectedAreas(),
					DragEndPoint = GlobalPosition,
				}
			);
		}
	}

	/// <summary>
	/// Connected via a godot signal
	/// </summary>
	public void OnHover()
	{
		CardUIManager.Instance.AddMouseOvered(this);
	}

	/// <summary>
	/// Connected via a godot signal
	/// </summary>
	public void OnHoverEnd()
	{
		CardUIManager.Instance.RemoveMouseOvered(this);
	}

	public void Highlight()
	{
		_internalCardUI2D.OutlineThickness = 5;
	}

	public void NoHighlight()
	{
		_internalCardUI2D.OutlineThickness = 0;
	}

	public void StartHover()
	{
		// Consider more explicit null checking where appropriate
		if (_currentTween?.IsValid() == true)
		{
			_currentTween.Stop();
			_currentTween = null;
		}

		ResetHover();

		if (IsPosLerping)
			return;

		_currentTween = _hoverContainer.CreateTween();

		var cardDup = _internalCardUI2D.Duplicate() as InternalCardUI2D;
		cardDup.ZIndex = HoverZIndex;

		_hoverContainer.AddChild(cardDup);
		_hoverCard = cardDup;

		_internalCardUI2D.Visible = false;

		var targetScale = new Vector2(HoverScale, HoverScale);

		_currentTween.TweenProperty(_hoverCard, "scale", targetScale, TweenAnimationTime);

		// using the main viewport and the current card's position
		var mainViewportRect = GetViewportRect();
		var cardHeight =
			_hoverCard.GetNode<Sprite2D>("%MainFrame").GetRect2().Size.Y
			* (_hoverCard.GlobalScale.Y / _hoverCard.Scale.Y) //for some reason here, i'm not sure why, but it only works properly when we do not use its own scaling into its calcultion.
			* targetScale.Y;

		var targetY = mainViewportRect.Size.Y - cardHeight / 2;

		_currentTween.Parallel();
		_currentTween.TweenProperty(
			_hoverCard,
			"global_position",
			//We may need to pass in the final scaling value into here in order for it to work properly
			new Vector2(GlobalPosition.X, targetY),
			TweenAnimationTime
		);

		_currentTween.Parallel();
		_currentTween.TweenProperty(_hoverCard, "rotation", -1 * Rotation, TweenAnimationTime);

		_currentTween.TweenCallback(
			Callable.From(() =>
			{
				_currentTween = null;
			})
		);
	}

	public void StopHover()
	{
		_currentTween?.Stop();

		//This code might run, when we have queued the hover card for deletion but it hasn't been fully removed yet.
		//This is causing errors, so hopefully this code will prevent those errors from happening.

		//!GodotObject.IsInstanceValid(_hoverCard)
		if (
			(
				_hoverCard != null
				&& (!IsInstanceValid(_hoverCard) || _hoverCard.IsQueuedForDeletion())
			)
			|| _hoverCard == null
		)
		{
			return;
		}

		_currentTween = _hoverContainer.CreateTween();

		//Make sure that other hovered cards get priority as this one is doing its animation.
		_hoverCard.ZIndex = HoverZIndex - 1;

		_currentTween.TweenProperty(
			_hoverCard,
			"scale",
			_internalCardUI2D.Scale,
			TweenAnimationTime
		);
		_currentTween.Parallel();
		_currentTween.TweenProperty(
			_hoverCard,
			"position",
			_internalCardUI2D.Position,
			TweenAnimationTime
		);
		_currentTween.Parallel();
		_currentTween.TweenProperty(
			_hoverCard,
			"rotation",
			_internalCardUI2D.Rotation,
			TweenAnimationTime
		);

		_currentTween.TweenCallback(
			Callable.From(() =>
			{
				_currentTween = null;
				ResetHover();
			})
		);
	}

	private void ResetHover()
	{
		_internalCardUI2D.Visible = true;
		ClearHoverContainer();
	}

	/// <summary>
	/// Removes all nodes from the hover container
	/// </summary>
	private void ClearHoverContainer()
	{
		foreach (var node in _hoverContainer.GetChildren())
		{
			node.QueueFree();
		}

		_hoverCard = null;
	}

	public override void _ExitTree()
	{
		_currentTween?.Kill(); // More forceful than Stop()
		DragEnd = null; // Clear event handlers
		base._ExitTree();
	}

	public void ApplyTo(InternalCardUI2D.Details details)
	{
		details.ApplyTo(_internalCardUI2D);
	}
}
