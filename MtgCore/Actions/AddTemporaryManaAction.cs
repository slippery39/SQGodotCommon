using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Adds mana to a player's current mana pool without changing their max mana.
/// Used by fast-mana spells (Rite of Flame, Seething Song, Lotus Bloom) that
/// grant temporary mana for this turn only.
///
/// Player resolution:
///   - Set PlayerId directly, or
///   - Set PlayerIdContextKey to read the player ID from pipeline context
/// If PlayerIdContextKey is set it takes priority over PlayerId.
///
/// Amount bonus:
///   - Base amount is Amount.
///   - If BonusAmountContextKey is set, reads an int from pipeline context and
///     adds it on top of Amount. Used by Rite of Flame to add 1 per Rite in graveyard.
/// </summary>
public record AddTemporaryManaAction : GameAction
{
	public int PlayerId { get; init; } = 0;
	public string PlayerIdContextKey { get; init; } = "";
	public int Amount { get; init; } = 0;
	public string BonusAmountContextKey { get; init; } = "";

	public override ActionResult Execute(GameState gameState)
	{
		var playerId = string.IsNullOrEmpty(PlayerIdContextKey)
			? PlayerId
			: GetInput<int>(PlayerIdContextKey, PlayerId);

		if (playerId == 0)
			return new ActionResult(gameState);

		var bonus = string.IsNullOrEmpty(BonusAmountContextKey)
			? 0
			: GetInput<int>(BonusAmountContextKey, 0);

		var player = gameState.GetPlayer(playerId);
		var updated = player with { CurrentMana = player.CurrentMana + Amount + bonus };
		return new ActionResult(gameState.UpdateObject(playerId, updated));
	}
}
