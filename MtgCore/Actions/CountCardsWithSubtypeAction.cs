using System.Collections.Immutable;
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

	/// <summary>
	/// Also emit the IDS of the cards counted, so a later pipeline step can act on them.
	///
	/// **Mass targeting cannot reach inside a pipeline** — `PipelineAction` is not an
	/// `ITargetedAction`, so nothing can inject an "all valid" list into one of its steps. That is
	/// why <see cref="CountGreatestPowerAction"/> emits an id list beside its number, and it is the
	/// only shape in which "count your creatures, then pump exactly those creatures" is expressible.
	/// A Craterhoof-style card needs both halves from one scan or the two could disagree.
	///
	/// Empty (the default) skips it entirely, so every existing caller is unaffected.
	/// </summary>
	public string CountedIdsOutputKey { get; init; } = "";

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

		var matched = string.IsNullOrEmpty(Subtype)
			? cards.ToList()
			: cards.Where(c => c.HasSubtype(Subtype)).ToList();

		var result = new ActionResult(gameState).WithOutput(OutputKey, matched.Count);

		// The ids come from the SAME filtered list the count came from, so the number and the
		// targets cannot disagree — a second scan could see a different board if anything in
		// between moved a permanent.
		// ImmutableList<int>, matching CountGreatestPowerAction — that is the type
		// EffectAction.TargetContextKey reads, so a plain List would silently target nothing.
		return string.IsNullOrEmpty(CountedIdsOutputKey)
			? result
			: result.WithOutput(CountedIdsOutputKey, matched.Select(c => c.Id).ToImmutableList());
	}
}
