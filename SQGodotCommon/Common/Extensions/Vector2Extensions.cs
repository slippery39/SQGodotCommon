public static class Vector2Extensions
{
	public static Vector2I ToVector2I(this Vector2 v)
	{
		return new Vector2I((int)v.X, (int)v.Y);
	}

	public static Vector2 ToVector2(this Vector2I vector2I)
	{
		return new Vector2(vector2I.X, vector2I.Y);
	}
}
