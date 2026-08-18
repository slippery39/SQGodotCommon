using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Returns up to N creature cards from the caster's graveyard to the battlefield, cheapest-first,
/// optionally capped by mana value — Return to the Ranks.
///
/// N comes from pipeline context (CountContextKey), which is how an X spell scales: the X chosen
/// at cast time is injected by ResolveSpellAction under ContextKeys.XValue.
///
/// Targets are picked here rather than by a targeting strategy because "X target creature cards"
/// is a variable-sized target list, and TargetingStrategy resolves a fixed shape — single, all
/// valid, or random.
/// </summary>
public record ReanimateManyAction : GameAction
{
	public int Count { get; init; } = 1;
	public string CountContextKey { get; init; } = "";
	public string PlayerIdContextKey { get; init; } = ContextKeys.CastingPlayerId;

	/// <summary>Only creatures at or below this mana value. 0 means no cap.</summary>
	public int MaxManaCost { get; init; } = 0;

	/// <summary>
	/// Scan BOTH graveyards — "all creature cards from all graveyards" (Liliana Vess's ultimate).
	/// They arrive under the casting player's control; PutIntoBattlefieldAction reassigns
	/// ControllerId from context.
	/// </summary>
	public bool FromAllGraveyards { get; init; } = false;

	public override ActionResult Execute(GameState gameState)
	{
		var playerId = GetInput<int>(PlayerIdContextKey, 0);
		if (playerId == 0)
			return new ActionResult(gameState);

		var count = string.IsNullOrEmpty(CountContextKey)
			? Count
			: GetInput<int>(CountContextKey, Count);

		if (count <= 0)
			return new ActionResult(gameState);

		var graveyardIds = FromAllGraveyards
			? new[] { MtgObjectKeys.Player1, MtgObjectKeys.Player2 }
				.Select(gameState.GetWellKnownId)
				.Select(pid => gameState.GetPlayerZoneId(pid, ZoneType.Graveyard))
				.Where(id => id != 0)
				.ToList()
			: [gameState.GetPlayerZoneId(playerId, ZoneType.Graveyard)];

		if (graveyardIds.Count == 0 || graveyardIds[0] == 0)
			return new ActionResult(gameState);

		// Cheapest first: with a fixed number of slots, more bodies beats bigger bodies in a
		// go-wide deck, which is the deck this card is in.
		var targets = graveyardIds
			.SelectMany(gameState.GetCardsInZone)
			.Where(c => c.HasComponent<CreatureComponent>())
			.Where(c => MaxManaCost == 0 || c.ManaCost <= MaxManaCost)
			.OrderBy(c => c.ManaCost)
			.ThenBy(c => c.Id)
			.Take(count)
			.Select(c => c.Id)
			.ToImmutableList();

		if (targets.IsEmpty)
			return new ActionResult(gameState);

		// InputContext must be carried across, not dropped: PutIntoBattlefieldAction reads
		// CastingPlayerId from it to set the new controller, and "under your control" is the
		// whole point of raiding an opponent's graveyard. Without this the spawned action sees
		// no caster and hands their creatures straight back to them.
		return new ActionResult(
			gameState.SpawnAction(
				new PutIntoBattlefieldAction { TargetIds = targets, InputContext = InputContext }
			)
		);
	}
}
