using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Causes a player to lose a fixed amount of life, or an amount read
/// from pipeline context when AmountContextKey is set.
///
/// Fixed amount usage:
///   new LoseLifeAction { PlayerId = x, Amount = 3 }
///
/// Dynamic amount usage (e.g. Dark Confidant):
///   new LoseLifeAction { PlayerId = x, AmountContextKey = ContextKeys.RevealedCardManaCost }
///
/// If AmountContextKey is set it takes priority over Amount.
/// If the key is missing or resolves to 0, no life is lost.
/// </summary>
public record LoseLifeAction : GameAction
{
	public int PlayerId { get; init; }
	public int Amount { get; init; }
	public string AmountContextKey { get; init; } = "";

	public override ActionResult Execute(GameState gameState)
	{
		var amount = string.IsNullOrEmpty(AmountContextKey)
			? Amount
			: GetInput<int>(AmountContextKey, 0);

		if (amount == 0)
			return new ActionResult(gameState);

		if (!gameState.HasObject(PlayerId))
			return new ActionResult(gameState);

		var player = (MtgPlayer)gameState.GetObject(PlayerId);
		var updated = player with { Life = player.Life - amount };
		var newState = gameState.UpdateObject(PlayerId, updated);

		return new ActionResult(newState).WithEvent(
			new PlayerLostLifeEvent { PlayerId = PlayerId, Amount = amount }
		);
	}
}
