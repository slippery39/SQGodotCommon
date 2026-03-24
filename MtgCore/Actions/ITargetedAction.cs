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
/// </summary>
public interface ITargetedAction
{
	GameAction WithTargets(ImmutableList<int> targetIds);
}
