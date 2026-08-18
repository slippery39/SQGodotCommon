using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Marker: this spell cannot be countered — Banefire at X 5+, Exquisite Firecraft with spell
/// mastery, Fry.
///
/// Checked by CounterTrapEngine.TryCounterCast, which returns before any trap is chosen, so an
/// uncounterable spell does not even SPEND the opponent's trap. That is the real rule: a trap that
/// cannot counter its target was never a legal response to it.
///
/// Worth having rather than cutting, unlike most "can't be X" clauses here, because counterspells
/// genuinely exist in this engine — blue's traps fire from hand off unspent mana — so this is a
/// live interaction rather than reminder text about a mechanic the game lacks.
///
/// Conditional cases (Banefire's "if X is 5 or more") carry a Condition; null means unconditional.
/// </summary>
public record CannotBeCounteredComponent : GameComponent
{
	/// <summary>
	/// Gate for the clause. Null means always uncounterable. Reuses ActivationCondition so
	/// SpellMasteryCondition works here with no new type.
	/// </summary>
	public ActivationCondition? Condition { get; init; }

	public bool IsActive(GameState state, int cardId, int controllerId) =>
		Condition == null || Condition.IsSatisfied(state, cardId, controllerId);
}
