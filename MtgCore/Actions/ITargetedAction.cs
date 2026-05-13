using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Implemented by GameActions that require target IDs to be injected
/// at resolution time. The engine calls WithTargets() to produce the
/// final action with targets filled in before spawning it.
///
/// The original ActionTemplate is never mutated — WithTargets returns
/// a new record instance via 'with'.
///
/// Canonical pattern: one action instance holds all target IDs and loops
/// through them in Execute(). Do not spawn one action per target.
/// </summary>
public interface ITargetedAction
{
	/// <summary>
	/// Helper method to easily add targets to an action
	/// </summary>
	/// <param name="targetIds"></param>
	/// <returns></returns>
	GameAction WithTargets(ImmutableList<int> targetIds);
}
