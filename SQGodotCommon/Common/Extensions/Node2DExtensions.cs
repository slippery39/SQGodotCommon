public static class Node2DExtensions
{
	/// <summary>
	/// Converts a view position into a world position.
	/// Mainly when we need to convert mouse coordinates into world coordinates.
	/// You would pass the mouse position as the position and you would get the position of the mouse in relation to the world.
	/// If your object is not at position 0,0, then you will need to do Mouse.GlobalPosition - Node.GlobalPosition in order to
	/// get the proper coordinate.
	/// </summary>
	/// <param name="node"></param>
	/// <param name="position"></param>
	/// <returns></returns>
	public static Vector2 ViewToWorldPosition(this Node2D node, Vector2 position)
	{
		var viewToWorld = node.GetCanvasTransform().AffineInverse();
		var worldPosition = viewToWorld * position;
		return worldPosition;
	}
}
