namespace Common.Core;

[Tool]
public partial class WorldBoundaryBox2D : Node2D
{
	/// <summary>
	///  Inside this box is where the "world" is located. Outside this box is not the world.
	/// </summary>
	[Export]
	public Rect2 BoundaryBox { get; set; }

	[Export]
	public Color BoundaryColor { get; set; } = Colors.White;

	[Export]
	public int BoundaryWidth { get; set; } = 30;

	public override void _Ready()
	{
		UpdateBoundaryPositions();
	}

	public override void _Process(double delta)
	{
		if (Engine.IsEditorHint())
		{
			UpdateBoundaryPositions();
			QueueRedraw();
		}
	}

	public override void _Draw()
	{
		DrawRect(BoundaryBox, BoundaryColor, false, BoundaryWidth);
	}

	private void UpdateBoundaryPositions()
	{
		Vector2 topLeft = BoundaryBox.Position;
		Vector2 bottomRight = BoundaryBox.End;
		Vector2 topRight = new Vector2(bottomRight.X, topLeft.Y);
		Vector2 bottomLeft = new Vector2(topLeft.X, bottomRight.Y);
		GetNode<StaticBody2D>("TopWorldBoundary").Position = topLeft;
		GetNode<StaticBody2D>("LeftWorldBoundary").Position = bottomLeft;
		GetNode<StaticBody2D>("BottomWorldBoundary").Position = bottomRight;
		GetNode<StaticBody2D>("RightWorldBoundary").Position = topRight;
	}
}
