namespace MtgSimulator;

public record AiDecision(
	string Description,
	float Score,
	IReadOnlyList<AiActionCandidate> Candidates,
	bool IsChoiceResolution = false
);

public record AiActionCandidate(string Description, float Score, bool WasChosen);
