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

	/// <summary>
	/// Cards removed from this battle by <see cref="RunCard.Exhausts"/>. They are NOT reshuffled
	/// when Draw runs dry, which is the whole mechanic.
	///
	/// **Battle scope only — the run deck is untouched.** Every battle is built fresh from
	/// `Run.Deck`, so an exhausted card is back next fight. Nothing but a doom transform may remove
	/// a card from a run, and that rule is not being bent here.
	///
	/// A zone rather than deleting the object, so the card is still there to be counted, shown, and
	/// read by anything that asks. A vanished object is the kind of state that is impossible to
	/// debug later.
	/// </summary>
	Exhausted,
}
