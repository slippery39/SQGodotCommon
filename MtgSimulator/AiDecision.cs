namespace MtgSimulator;

/// <summary>
/// One decision the AI made, captured for the inspector and the debug snapshot.
///
/// <paramref name="StateBefore"/> is the position the decision was made from; each candidate
/// carries the position its action leads to. The difference between them is the diagnostic view —
/// what a given action actually buys, term by term — and it is left to the renderer rather than
/// stored, so absolute and delta views come from the same capture.
/// </summary>
public record AiDecision(
	string Description,
	float Score,
	IReadOnlyList<AiActionCandidate> Candidates,
	bool IsChoiceResolution = false,
	EvaluationBreakdown? StateBefore = null
);

/// <summary>
/// One action the search considered.
///
/// <paramref name="Score"/> is what the search ranked on — for the multi-turn strategy that is a
/// ROLLOUT score, the evaluation of a state two turns ahead, not of <paramref name="Breakdown"/>.
/// <paramref name="Breakdown"/> is the immediate position after the action. The two answer
/// different questions and will not agree: the gap between them is exactly what the lookahead
/// contributed, which makes it worth showing both rather than reconciling them.
/// </summary>
public record AiActionCandidate(
	string Description,
	float Score,
	bool WasChosen,
	EvaluationBreakdown? Breakdown = null
);
