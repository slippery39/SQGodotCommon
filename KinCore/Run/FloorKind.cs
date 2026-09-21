namespace KinCore;

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

	/// <summary>
	/// Somewhere to spend gold: cards to buy, and **a card to remove**.
	///
	/// Removal is the reason this exists. Combat v3 makes the deck your entire per-turn output, so
	/// a card you would not play crowds out one you would — and until now nothing but an apocalypse
	/// could take one out of a run.
	/// </summary>
	Shop,

	/// <summary>
	/// A choice with consequences and no fight. **NOT IMPLEMENTED — see <see cref="ActMap.Layout"/>,
	/// which deliberately does not place any yet.** A floor that silently behaves like a rest is
	/// indistinguishable from one that works, which is the failure this codebase keeps repeating.
	/// </summary>
	Event,
}
