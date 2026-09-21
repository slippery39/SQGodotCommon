namespace KinCore;

/// <summary>
/// Keys for the well-known object registry on GameState. Registered once by
/// <see cref="KinBattleFactory"/> and never changed.
/// </summary>
public static class KinObjectKeys
{
	public const string Battle = "Battle";
	public const string Player = "Player";
	public const string Draw = "Draw";
	public const string Hand = "Hand";
	public const string Discard = "Discard";
	public const string Exhausted = "Exhausted";
	public const string Taken = "Taken";
	public const string Field = "Field";
	public const string Enemies = "Enemies";
	public const string Opponent = "Opponent";
}
