using MtgCore;

namespace MtgSimulator;

/// <summary>
/// Extends IAiStrategy with decision capture — the last action or choice decision
/// the strategy made, for debug snapshot export and UI transparency.
/// </summary>
public interface ICapturingAiStrategy : IAiStrategy
{
	AiDecision? LastDecision { get; }
}
