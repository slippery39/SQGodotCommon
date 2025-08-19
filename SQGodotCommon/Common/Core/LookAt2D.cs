namespace Common.Core;

public partial class LookAt2D : Node2D
{
	[Export]
	public Node2D Target { get; set; } // Assign the player node in the editor

	[Export]
	public float RotationSpeed { get; set; } = 100f; // Adjust rotation speed for smooth turning

	public override void _Process(double delta)
	{
		if (Target == null)
			return;

		var parent = GetParent() as Node2D;

		Vector2 direction = (Target.GlobalPosition - parent.GlobalPosition).Normalized();
		float targetAngle = direction.Angle();

		// Smooth rotation towards the player
		parent.Rotation = Mathf.LerpAngle(
			parent.Rotation,
			targetAngle,
			(float)delta * RotationSpeed
		);
	}
}
