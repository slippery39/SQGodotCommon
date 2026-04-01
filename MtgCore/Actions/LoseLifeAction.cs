using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Causes a player to lose a fixed amount of life, or an amount read
/// from pipeline context when AmountContextKey is set.
///
/// Player resolution:
///   - Set PlayerId directly, or
///   - Set PlayerIdContextKey to read the player ID from pipeline context
///
/// Amount resolution:
///   - Set Amount directly, or
///   - Set AmountContextKey to read from pipeline context
///
/// If the context key variants are set they take priority over the direct values.
/// If the resolved amount is 0, no life is lost.
/// </summary>
public record LoseLifeAction : GameAction
{
	public int PlayerId { get; init; }
	public string PlayerIdContextKey { get; init; } = "";
	public int Amount { get; init; }
	public string AmountContextKey { get; init; } = "";

	public override ActionResult Execute(GameState gameState)
	{
		var playerId = string.IsNullOrEmpty(PlayerIdContextKey)
			? PlayerId
			: GetInput<int>(PlayerIdContextKey, 0);

		if (playerId == 0 || !gameState.HasObject(playerId))
			return new ActionResult(gameState);

		var amount = string.IsNullOrEmpty(AmountContextKey)
			? Amount
			: GetInput<int>(AmountContextKey, 0);

		if (amount == 0)
			return new ActionResult(gameState);

		var player = (MtgPlayer)gameState.GetObject(playerId);
		var updated = player with { Life = player.Life - amount };
		var newState = gameState.UpdateObject(playerId, updated);

		return new ActionResult(newState).WithEvent(
			new PlayerLostLifeEvent { PlayerId = playerId, Amount = amount }
		);
	}
}
