namespace ImmutableGameObjects;

public static class GameStateExtensions
{
	public static GameState ThenAdd<T>(this GameState state, T obj, int parentId = 0)
		where T : GameObject
	{
		return state.AddObject(obj, parentId).GameState;
	}
}
