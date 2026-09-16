namespace DoomCore;

/// <summary>
/// What is waiting on a floor. **Not every floor is a battle** — `Run.ActLength` has said so since
/// the run layer existed, and until now nothing implemented it: twenty floors meant twenty
/// consecutive fights with no healing anywhere.
///
/// That made the act arithmetically unfinishable rather than hard. With no healing, the whole run
/// has one life budget to spend across every battle, so the act's length and its difficulty are the
/// same dial — see `docs/findings/doom-balance.md`. A rest is what separates them again.
/// </summary>
public enum FloorKind
{
	/// <summary>An Opponent, its enemies, and an apocalypse on a clock.</summary>
	Battle = 0,

	/// <summary>
	/// No fight. You heal, and you walk on.
	///
	/// Deliberately not a choice yet. Slay the Spire's rest node offers heal-or-upgrade, and there
	/// is nothing to upgrade here — cards have no levels. Adding the fork is a content job for when
	/// there is a second thing worth doing with the stop.
	/// </summary>
	Rest,
}
