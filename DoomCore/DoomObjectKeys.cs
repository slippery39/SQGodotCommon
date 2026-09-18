namespace DoomCore;

/// <summary>
/// Keys for the well-known object registry on GameState. Registered once by
/// <see cref="DoomBattleFactory"/> and never changed.
/// </summary>
public static class DoomObjectKeys
{
	public const string Battle = "Battle";
	public const string Player = "Player";
	public const string Draw = "Draw";
	public const string Hand = "Hand";
	public const string Discard = "Discard";
	public const string Exhausted = "Exhausted";
	public const string Field = "Field";
	public const string Enemies = "Enemies";
	public const string Opponent = "Opponent";
}
