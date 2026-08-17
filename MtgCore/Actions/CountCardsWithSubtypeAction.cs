using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Counts cards with a given subtype in one of a player's zones and writes the
/// result to pipeline context. Used by Krenko, Mob Boss to count Goblins on the
/// battlefield, and by graveyard tribal payoffs to count Zombies in the graveyard.
///
/// Zone defaults to Battlefield, which is the behaviour this action shipped with.
///
/// Leaving Subtype empty counts every card in the zone — that is how "X equal to the
/// number of cards in your graveyard" effects are expressed.
///
/// Player resolution:
///   - Set PlayerId directly, or
///   - Set PlayerIdContextKey to read the player ID from pipeline context
///
/// If PlayerIdContextKey is set it takes priority over PlayerId.
/// </summary>
public record CountCardsWithSubtypeAction : GameAction
{
	public string Subtype { get; init; } = "";
	public ZoneType Zone { get; init; } = ZoneType.Battlefield;
	public string OutputKey { get; init; } = "";
	public string PlayerIdContextKey { get; init; } = "";
	public int PlayerId { get; init; } = 0;

	/// <summary>
	/// Count only cards with a CreatureComponent. Needed for "for each creature you control"
	/// (Lena) — an empty Subtype on the battlefield otherwise counts artifacts and enchantments
	/// too, which is a silently wrong number rather than an approximate one.
	/// </summary>
	public bool CreaturesOnly { get; init; } = false;

	public override ActionResult Execute(GameState gameState)
	{
		var playerId = string.IsNullOrEmpty(PlayerIdContextKey)
			? PlayerId
			: GetInput<int>(PlayerIdContextKey, 0);

		if (playerId == 0)
			return new ActionResult(gameState);

		var zoneId = gameState.GetPlayerZoneId(playerId, Zone);
		var cards = gameState.GetCardsInZone(zoneId);

		if (CreaturesOnly)
			cards = cards.Where(c => c.HasComponent<CreatureComponent>());

		var count = string.IsNullOrEmpty(Subtype)
			? cards.Count()
			: cards.Count(c => c.HasSubtype(Subtype));

		return new ActionResult(gameState).WithOutput(OutputKey, count);
	}
}
