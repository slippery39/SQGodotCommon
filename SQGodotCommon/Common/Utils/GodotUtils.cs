using Godot;

namespace Common.Core;

public static class GodotUtils
{
	/// <summary>
	/// Sets a value on a track key in an animation.
	/// TrackName is relative to the root object of the scene.
	/// So if you are animating the position of a child object called "Sprite2D", the trackname would be "Sprite2D:position"
	/// For Root animations, you put a "." instead. So ".:position"
	/// </summary>
	/// <param name="animation"></param>
	/// <param name="trackName"></param>
	/// <param name="keyIndex"></param>
	/// <param name="value"></param>
	/// <exception cref="System.ArgumentNullException"></exception>
	/// <exception cref="System.InvalidOperationException"></exception>
	public static void SetAnimationTrackKey(
		Animation animation,
		string trackName,
		int keyIndex,
		Variant value
	)
	{
		if (animation == null)
		{
			GD.PrintErr("Animation was null in call to SetAnimationTrackKey");
			throw new System.ArgumentNullException("animation");
		}

		var count = animation.GetTrackCount();
		var trackFound = false;
		for (var i = 0; i < count; i++)
		{
			var track = animation.TrackGetPath(i);
			if (trackName == $"{track.GetName(0)}:{track.GetSubName(0)}")
			{
				animation.TrackSetKeyValue(i, keyIndex, value);
				trackFound = true;
				break;
			}
		}

		if (!trackFound)
		{
			GD.PrintErr($"Could not find track {trackName} in method SetAnimationTrackKey()");
			throw new System.InvalidOperationException(
				$"Could not find track {trackName} in method SetAnimationTrackKey()"
			);
		}
	}

	/// <summary>
	/// Finds the first ancestor of a node that is of type T.
	/// Returns null if no ancestor can be found.
	/// </summary>
	/// <typeparam name="T"></typeparam>
	/// <param name="node"></param>
	/// <returns></returns>
	public static T FindFirstAncestorOfType<T>(this Node node)
		where T : Node
	{
		var currentParent = node.GetParent();

		while (currentParent is not T)
		{
			//We went to the end and could not find an ancestor of type T
			if (currentParent == null)
			{
				return null;
			}
			currentParent = currentParent.GetParent();
		}

		//If we get here we have found the
		return currentParent as T;
	}

	/// <summary>
	/// Returns a random point in a given area 2d
	/// </summary>
	/// <param name="area2D"></param>
	/// <returns></returns>
	public static Vector2I GetRandomPointInArea2D(Area2D area2D)
	{
		var collisionShape = area2D.GetNodeOfType<CollisionShape2D>();
		var rect = collisionShape.Shape.GetRect();
		rect.Position += area2D.GlobalPosition;

		var randomX = GD.RandRange(rect.Position.X, rect.End.X);
		var randomY = GD.RandRange(rect.Position.Y, rect.End.Y);

		return new Vector2I((int)randomX, (int)randomY);
	}

	/// <summary>
	/// Returns if the mouse specified by mousePos is inside of a colShape2D
	/// </summary>
	/// <param name="mousePos"></param>
	/// <returns></returns>
	public static bool IsPointInside(this CollisionShape2D colShape2D, Vector2 mousePos)
	{
		switch (colShape2D.Shape)
		{
			case CircleShape2D circle:
			{
				var globalPos = colShape2D.GlobalPosition;
				var radius = circle.Radius;

				return mousePos.DistanceTo(globalPos) < radius;
			}
			case RectangleShape2D rect:
			{
				var localRect = rect.GetRect();

				//Assuming the position needs to be offset to the center.
				var globalPos = colShape2D.GlobalPosition;
				globalPos.X -= localRect.Size.X / 2;
				globalPos.Y -= localRect.Size.Y / 2;
				var globalRect = new Rect2(globalPos, localRect.Size);
				return globalRect.HasPoint(mousePos);
			}
			default:
			{
				var localRect = colShape2D.Shape.GetRect();

				//Assuming the position needs to be offset to the center.
				var globalPos = colShape2D.GlobalPosition;
				globalPos.X -= localRect.Size.X / 2;
				globalPos.Y -= localRect.Size.Y / 2;
				var globalRect = new Rect2(globalPos, localRect.Size);
				return globalRect.HasPoint(mousePos);
			}
		}
	}

	/// <summary>
	/// Returns a rect2 with dimensions according to the size of the sprite in the game. Includes current scaling
	/// </summary>
	/// <param name="sprite"></param>
	/// <returns></returns>
	public static Rect2 GetRect2(this Sprite2D sprite)
	{
		return new Rect2(
			sprite.GlobalPosition - sprite.Texture.GetSize() * sprite.Scale / 2,
			sprite.Texture.GetSize() * sprite.Scale
		);
	}
}
