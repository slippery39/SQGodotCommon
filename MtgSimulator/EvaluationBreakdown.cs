namespace MtgSimulator;

/// <summary>
/// A <see cref="StateEvaluator"/> score split into the terms that produced it.
///
/// "This action scores 4.2" is not a diagnosis. "creatures +3.0, power +4.0, race -3.2,
/// hand -1.4, lands-in-hand +0.0" is — the land-pricing defect is visible on sight in the
/// second form and invisible in the first. This exists so the AI inspector can show the
/// second form, and so the numbers it shows are the ones the search actually used.
///
/// A <c>readonly record struct</c> on purpose. <see cref="StateEvaluator.Evaluate"/> is the
/// hottest call in the engine — a single move runs hundreds of rollouts, each evaluating many
/// states — so producing this on every evaluation must not allocate. Returning a struct by
/// value keeps the shared implementation (no risk of a display path drifting from the scoring
/// path) without putting anything on the heap.
/// </summary>
public readonly record struct EvaluationBreakdown(
	float Life,
	float Creatures,
	float Power,
	float CreatureDamage,
	float NonCreaturePermanents,
	float Hand,
	float Mana,
	float Race,
	float Toughness,
	float Keywords,
	float Total,
	bool IsTerminal
)
{
	/// <summary>
	/// The terms in display order, paired with the labels the inspector shows. Terminal states
	/// short-circuit before any term is computed, so they report a single row rather than eight
	/// zeroes and a total that appears to come from nowhere.
	/// </summary>
	public IEnumerable<(string Label, float Value)> Terms =>
		IsTerminal
			? [(Total > 0 ? "win" : "loss", Total)]
			:
			[
				("life", Life),
				("creatures", Creatures),
				("power", Power),
				("creature damage", CreatureDamage),
				("other permanents", NonCreaturePermanents),
				("hand", Hand),
				("mana", Mana),
				("race", Race),
				("toughness", Toughness),
				("keywords", Keywords),
			];
}
