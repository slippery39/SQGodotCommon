namespace DoomCore;

public enum ZoneType
{
	/// <summary>The battle deck, drawn from the top. Reshuffled from Discard when it empties.</summary>
	Draw,
	Hand,
	Discard,

	/// <summary>The player's units in play. This is what a doom scenario reads.</summary>
	Field,

	/// <summary>The enemies. Owned by the battle, not the player.</summary>
	Enemies,
}
