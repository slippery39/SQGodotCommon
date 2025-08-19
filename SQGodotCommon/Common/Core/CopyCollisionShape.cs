//Our own Godot namespace is screwing things up here... we need to change the namespace name.
namespace Common.Core;

/// <summary>
/// Allows a CollisionShape to be automatically used in multiple places.
/// </summary>
[Tool]
public partial class CopyCollisionShape : Node
{
	[Export]
	public CollisionShape2D TargetCollisionShape { get; set; }

	[Export]
	public bool AutoUpdate { get; set; } = true;

	[Export]
	public bool RefreshNow { get; set; } = false;

	private CollisionShape2D _parentCollisionShape;
	private Shape2D _lastTargetShape;

	public override void _Ready()
	{
		// Get the parent CollisionShape2D
		_parentCollisionShape = GetParent<CollisionShape2D>();

		if (_parentCollisionShape == null)
		{
			GD.PrintErr("CopyCollisionShape must be a child of a CollisionShape2D node");
			return;
		}

		// Copy the shape initially
		CopyShape();
	}

	public override void _Process(double delta)
	{
		if (!AutoUpdate || TargetCollisionShape == null)
			return;

		// Check if the target shape has changed
		if (TargetCollisionShape.Shape != _lastTargetShape)
		{
			CopyShape();
		}
	}

	public override void _ValidateProperty(Godot.Collections.Dictionary property)
	{
		// Handle the RefreshNow button property
		if (property["name"].AsString() == "RefreshNow")
		{
			if (RefreshNow)
			{
				RefreshNow = false;
				CallDeferred(MethodName.CopyShape);
			}
		}
	}

	public void CopyShape()
	{
		if (TargetCollisionShape == null || _parentCollisionShape == null)
		{
			if (Engine.IsEditorHint())
				GD.PrintErr("Target collision shape or parent collision shape is null");
			return;
		}

		if (TargetCollisionShape.Shape == null)
		{
			if (Engine.IsEditorHint())
				GD.PrintErr("Target collision shape has no shape assigned");
			return;
		}

		// Copy the shape from the target
		_parentCollisionShape.Shape = TargetCollisionShape.Shape;
		_lastTargetShape = TargetCollisionShape.Shape;

		if (Engine.IsEditorHint())
		{
			GD.Print(
				$"Copied shape from {TargetCollisionShape.Name} to {_parentCollisionShape.Name}"
			);
			// Mark the scene as modified in the editor
			_parentCollisionShape.NotifyPropertyListChanged();
		}
	}

	// Method to manually trigger shape copying
	public void RefreshShape()
	{
		CopyShape();
	}
}
