using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Abstract base for all effect actions — actions that change game state by
/// affecting entities (deal damage, gain/lose life, draw cards, etc.).
///
/// Provides unified target and amount resolution via two interchangeable mechanisms:
///   1. TargetIds — targets injected at cast time via ITargetedAction.WithTargets()
///   2. TargetContextKey — key to read ImmutableList&lt;int&gt; (or int, coerced) from
///      pipeline InputContext at execution time
/// TargetContextKey wins when set; falls back to TargetIds otherwise.
/// AmountContextKey follows the same pattern over the direct Amount field on subclasses.
///
/// ValidateResolve (lenient): only fizzles if ALL resolved targets are gone.
/// Individual missing targets are skipped silently in Execute via 'continue'.
/// </summary>
public abstract record EffectAction : GameAction, ITargetedAction
{
	public string TargetContextKey { get; init; } = "";
	public string AmountContextKey { get; init; } = "";
	public ImmutableList<int> TargetIds { get; init; } = ImmutableList<int>.Empty;

	public GameAction WithTargets(ImmutableList<int> targetIds) =>
		this with
		{
			TargetIds = targetIds,
		};

	protected ImmutableList<int> ResolveTargetIds()
	{
		if (string.IsNullOrEmpty(TargetContextKey))
			return TargetIds;

		var raw = InputContext.GetValueOrDefault(TargetContextKey);
		return raw switch
		{
			ImmutableList<int> list => list,
			int id => ImmutableList.Create(id),
			_ => TargetIds,
		};
	}

	protected int ResolveAmount(int directValue)
	{
		return string.IsNullOrEmpty(AmountContextKey)
			? directValue
			: GetInput<int>(AmountContextKey, 0);
	}

	public override ValidationResult ValidateResolve(GameState gameState)
	{
		var targets = ResolveTargetIds();
		if (targets.IsEmpty)
			return ValidationResult.Valid;
		return targets.All(id => !gameState.HasObject(id))
			? ValidationResult.Invalid("All targets no longer exist")
			: ValidationResult.Valid;
	}
}
