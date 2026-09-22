namespace KinCore;

public enum ZoneType
{
	/// <summary>The battle deck, drawn from the top. Reshuffled from Discard when it empties.</summary>
	Draw,
	Hand,
	Discard,

	/// <summary>The player's units in play. Emptied every turn — units withdraw, see combat v3.</summary>
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

	/// <summary>
	/// Cards the water took. **Out of circulation, and coming back** — which is the one thing
	/// <see cref="Exhausted"/> never does, so it is a second zone rather than a flag on the first.
	///
	/// A card here is in no deck at all: not drawable, not reshuffled, not on the board. It returns
	/// to Discard at the start of the turn stamped on its <see cref="TakenComponent"/>.
	///
	/// **This is what Flood takes now.** Washing the board to Discard stopped meaning anything the
	/// moment combat v3 made the board wash itself every turn — so the doom takes the CARD instead,
	/// which is the only thing left that was not leaving anyway.
	/// </summary>
	Taken,
}
