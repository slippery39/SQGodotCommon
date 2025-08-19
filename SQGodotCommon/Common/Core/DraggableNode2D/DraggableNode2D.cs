using System;
using System.Collections.Generic;
using System.Linq;

namespace Common.Core;

/// <summary>
/// Place under a Node2D, It will make that Node2D Draggable. Does not require a sprite like DraggableNode2D (the first version)
/// </summary>
public partial class DraggableNode2D : Node2D
{
	private bool _dragging = false;
	private Vector2 _dragOffset = new Vector2();

	[Export]
	public bool UseOffset { get; set; } = true;

	private Node2D DraggableObject { get; set; }

	/// <summary>
	/// The Area 2D that was injected in (if at all)
	/// </summary>
	private Area2D SuppliedDragDetector { get; set; } = null;

	private Area2D SuppliedDropAreaDetector { get; set; } = null;

	public static Node2D CurrentlyDraggingObject { get; set; } = null;

	private Node Parent { get; set; } = null;

	/// <summary>
	/// The specific area that we have currently "selected" by hovering over it. In case we have hovered over multiple
	/// we probably only want to select one.
	/// </summary>
	public Area2D SelectedArea { get; set; } = null;

	/// <summary>
	/// All of the hovered areas that we are currently over.
	/// </summary>
	public List<Area2D> HoveredAreas { get; set; } = new List<Area2D>();

	public Action<DraggableNode2D> OnDragBegin { get; set; }

	public Action<DraggableNode2D> OnDragEnd { get; set; }

	public Func<bool> CanDrag { get; set; }

	[Export]
	public bool MultiSelect { get; set; } = false;

	public override void _Ready()
	{
		DraggableObject = GetParent<Node2D>();
		if (DraggableObject == null)
		{
			GD.PushError(
				"Could not find parent for DraggableNode2DV2. Please check that you have placed it under a Node2D"
			);
		}

		Init();

		//Check and see if the user supplied an Area 2D or not.
		foreach (var node in GetChildren())
		{
			if (node.Owner != this && node is Area2D)
			{
				if (node.Name == "DropAreaDetector")
				{
					SuppliedDropAreaDetector = (Area2D)node;
					SuppliedDropAreaDetector.AreaEntered += DropAreaDetectorAreaEntered;
					SuppliedDropAreaDetector.AreaExited += DropAreaDetectorAreaExited;
				}
				else if (SuppliedDragDetector != null)
				{
					GD.Print(
						$"{Name} : Duplicate Area2D Found .We will only use the first one : {node.Name}. Owner : {node.Owner.Name}"
					);
				}
				else
				{
					SuppliedDragDetector = (Area2D)node;
				}
			}
		}
	}

	private void Init()
	{
		if (CanDrag == null)
		{
			CanDrag = () => true;
		}
	}

	public List<Area2D> GetSelectedAreas()
	{
		if (MultiSelect)
		{
			return HoveredAreas;
		}
		else
		{
			if (SelectedArea == null)
				return new List<Area2D>();
			else
				return new List<Area2D> { SelectedArea };
		}
	}

	private void DropAreaDetectorAreaEntered(Area2D otherArea)
	{
		if (!MultiSelect && SelectedArea == null)
		{
			SelectedArea = otherArea;
			var handler = otherArea.GetNodeOfType<DragDropEventHandlerNode>();
			if (handler != null)
			{
				handler.OnDragEnter(this);
			}
		}
		else if (MultiSelect && !GetSelectedAreas().Contains(otherArea))
		{
			var handler = otherArea.GetNodeOfType<DragDropEventHandlerNode>();
			if (handler != null)
			{
				handler.OnDragEnter(this);
			}
		}

		if (!HoveredAreas.Contains(otherArea))
		{
			HoveredAreas.Add(otherArea);
		}
	}

	private void DropAreaDetectorAreaExited(Area2D otherArea)
	{
		if (!MultiSelect && SelectedArea == otherArea)
		{
			var nextHover = HoveredAreas.Find(e => e != SelectedArea);
			SelectedArea = nextHover;

			if (nextHover != null)
			{
				var enteringHandler = SelectedArea.GetNodeOfType<DragDropEventHandlerNode>();
				enteringHandler.OnDragEnter(this);
			}

			var leavingHandler = otherArea.GetNodeOfType<DragDropEventHandlerNode>();

			if (leavingHandler != null)
			{
				leavingHandler.OnDragLeave(this);
			}
		}
		else if (MultiSelect)
		{
			var leavingHandler = otherArea.GetNodeOfType<DragDropEventHandlerNode>();

			if (leavingHandler != null)
			{
				leavingHandler.OnDragLeave(this);
			}
		}

		HoveredAreas.Remove(otherArea);
	}

	public override void _Input(InputEvent @event)
	{
		if (
			@event is InputEventMouseButton mouseButton
			&& MouseIsInArea(mouseButton.GlobalPosition)
		)
		{
			if (@event.IsActionPressed(GodotInputs.LeftMouse))
			{
				if (CurrentlyDraggingObject == null)
				{
					if (!CanDrag())
					{
						return;
					}

					CurrentlyDraggingObject = this;
					_dragging = true;
					if (UseOffset)
					{
						_dragOffset = DraggableObject.GlobalPosition - mouseButton.GlobalPosition;
					}
					else
					{
						_dragOffset = new Vector2(0, 0);
					}
					OnDragBegin?.Invoke(this);
				}
			}
			else
			{
				GD.Print(
					$"{this} : STOPPED DRAGGING. Currently Dragging was {CurrentlyDraggingObject}"
				);

				CurrentlyDraggingObject = null;
				_dragging = false;
				OnDragEnd?.Invoke(this);
			}
		}

		if (@event is InputEventMouseMotion mouseMotion && _dragging)
		{
			DraggableObject.GlobalPosition = mouseMotion.GlobalPosition + _dragOffset;
		}
	}

	private bool MouseIsInArea(Vector2 mousePos)
	{
		if (SuppliedDragDetector != null)
		{
			var collShape2D = SuppliedDragDetector
				.GetChildren()
				.OfType<CollisionShape2D>()
				.FirstOrDefault();

			if (collShape2D == null)
			{
				GD.PushWarning(
					"Could not find a collision shape 2d inside of the area 2d supplied for DraggableNode2D"
				);
				return false;
			}

			return collShape2D.IsPointInside(mousePos);
		}
		else if (DraggableObject is Sprite2D sprite)
		{
			return sprite.GetRect2().HasPoint(mousePos);
		}
		else
		{
			GD.PushError(
				"DraggableNode2DV2 - Invalid node type set. Possibly you forgot to add an Area2D?"
			);
			return new Rect2().HasPoint(mousePos);
		}
	}
}
