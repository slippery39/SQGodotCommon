using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Marks a spell as having an {X} in its mana cost — Return to the Ranks.
///
/// The chosen X lives on the cast action (CastSpellAction.XValue), not here: X is a decision
/// made once at cast time, and storing it on the card would leak between copies of the card.
/// CostEngine adds it to the printed cost; the effect reads it back from pipeline context under
/// ContextKeys.XValue.
/// </summary>
public record XCostComponent : GameComponent
{
	/// <summary>
	/// Multiplier on X, for costs like {X}{X}{U}. 1 for the ordinary single-X case.
	/// </summary>
	public int Multiplier { get; init; } = 1;
}

/// <summary>
/// Convoke — "your creatures can help cast this spell".
///
/// Modelled as an automatic reduction rather than a per-creature choice: the spell costs {1}
/// less for each ready creature you control, up to its full cost, and exactly that many
/// creatures become exhausted when it is cast.
///
/// A per-creature choice would be strictly better for the player (choose WHICH creatures to
/// tap), but nothing in this engine distinguishes creatures for convoke purposes — there is no
/// colour to match and no summoning-sickness restriction on convoking — so the automatic
/// version reaches the same board state with no extra UI.
///
/// Creatures already exhausted or already attacked this turn cannot help, which is what keeps
/// convoke from being free after a full attack.
/// </summary>
public record ConvokeComponent : GameComponent { }
