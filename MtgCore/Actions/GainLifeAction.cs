using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Restores a fixed amount of life to each target player.
/// </summary>
public record GainLifeAction : GameAction, ITargetedAction
{
	public string PlayerIdContextKey { get; init; } = "";
	public int Amount { get; init; }
	public string AmountContextKey { get; init; } = "";
	public ImmutableList<int> TargetIds { get; init; } = ImmutableList<int>.Empty;

	public GameAction WithTargets(ImmutableList<int> targetIds) =>
		this with
		{
			TargetIds = targetIds,
		};

	public override ActionResult Execute(GameState gameState)
	{
		var state = gameState;
		var events = ImmutableList<GameEvent>.Empty;

		var targetIds = string.IsNullOrEmpty(PlayerIdContextKey)
			? TargetIds
			: [GetInput<int>(PlayerIdContextKey, 0)];

		var amount = string.IsNullOrEmpty(AmountContextKey)
			? Amount
			: GetInput<int>(AmountContextKey, 0);

		if (amount == 0)
			return new ActionResult(gameState);

		foreach (var targetId in targetIds)
		{
			if (state.GetObject(targetId) is not MtgPlayer player)
				continue;

			var updated = player with { Life = player.Life + amount };
			state = state.UpdateObject(player.Id, updated);
			events = events.Add(
				new PlayerGainedLifeEvent { PlayerId = player.Id, Amount = amount }
			);
		}

		return new ActionResult(state) { Events = events };
	}

	public override ValidationResult ValidateResolve(GameState gameState)
	{
		var targetIds = string.IsNullOrEmpty(PlayerIdContextKey)
			? TargetIds
			: [GetInput<int>(PlayerIdContextKey, 0)];

		foreach (var targetId in targetIds)
		{
			if (!gameState.HasObject(targetId))
				return ValidationResult.Invalid($"Target {targetId} no longer exists");
		}
		return ValidationResult.Valid;
	}
}
