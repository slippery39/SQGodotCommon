namespace ImmutableGameObjects;

public static class GameStateExtensions
{
	public static GameState ThenAdd<T>(this GameState state, T obj, int parentId = 0)
		where T : GameObject
	{
		return state.AddObject(obj, parentId).GameState;
	}

	/// <summary>
	/// Returns a random index in [0, count) and a new state with the seed advanced.
	/// When RngSeed is 0 the pick is truly random and the seed is not advanced.
	/// </summary>
	public static (int Value, GameState State) ConsumeRandom(this GameState state, int count)
	{
		if (state.RngSeed == 0)
			return (new Random().Next(count), state);

		var rng = new Random(state.RngSeed);
		var value = rng.Next(count);
		var nextSeed = rng.Next();
		return (value, state with { RngSeed = nextSeed });
	}
}
