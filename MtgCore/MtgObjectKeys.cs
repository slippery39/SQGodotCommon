namespace MtgCore;

/// <summary>
/// String constants for the well-known object registry on GameState.
/// Use these with state.GetWellKnownId(key) or the typed extension methods on
/// MtgGameStateExtensions (GetGame, GetStack, GetPlayerZoneId, etc.).
/// All keys are registered once in MtgGameFactory.Create() and never change.
/// </summary>
public static class MtgObjectKeys
{
	public const string Game = "Game";
	public const string Stack = "Stack";

	public const string Player1 = "Player1";
	public const string Player1Hand = "Player1Hand";
	public const string Player1Library = "Player1Library";
	public const string Player1Graveyard = "Player1Graveyard";
	public const string Player1Battlefield = "Player1Battlefield";
	public const string Player1Exile = "Player1Exile";

	public const string Player2 = "Player2";
	public const string Player2Hand = "Player2Hand";
	public const string Player2Library = "Player2Library";
	public const string Player2Graveyard = "Player2Graveyard";
	public const string Player2Battlefield = "Player2Battlefield";
	public const string Player2Exile = "Player2Exile";
}
