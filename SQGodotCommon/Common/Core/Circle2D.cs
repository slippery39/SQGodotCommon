namespace Common.Core;

[Tool]
public partial class Circle2D : Node2D
{
	[Export]
	public float Radius { get; set; } = 50f;

	[Export]
	public Color LineColor { get; set; } = Colors.White;

	[Export]
	public float LineWidth { get; set; } = 2f;

	[Export]
	public bool Filled { get; set; } = false;

	public override void _Draw()
	{
		//Godot spams warnings if we don't do it this way.
		if (Filled)
		{
			DrawCircle(Position, Radius, LineColor, Filled);
		}
		else
		{
			DrawCircle(Position, Radius, LineColor, Filled, LineWidth);
		}
	}

	public override void _Process(double delta)
	{
		if (Engine.IsEditorHint())
			QueueRedraw();
	}
}
