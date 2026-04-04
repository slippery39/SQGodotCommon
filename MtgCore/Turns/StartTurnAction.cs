using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Begins the active player's turn.
///
/// Responsibilities:
///   - Increments MaxMana by 1 (capped at 10) and refills CurrentMana to MaxMana
///   - Draws one card, unless SkipDraw is true (used for the first turn of the game)
///   - Clears HasSummoningSickness, HasAttacked, and Damage on all creatures
///     the active player controls on the battlefield
///
/// Emits TurnStartedEvent.
/// </summary>
public record StartTurnAction : GameAction
{
	private const int MaxManaCap = 10;

	public int ActivePlayerId { get; init; }
	public int BattlefieldId { get; init; }

	/// <summary>
	/// When true the draw step is skipped. Used for the first player's opening turn.
	/// </summary>
	public bool SkipDraw { get; init; } = false;

	public override ActionResult Execute(GameState gameState)
	{
		var state = gameState;

		// Increment max mana and refill
		var player = state.GetPlayer(ActivePlayerId);
		var newMax = Math.Min(player.MaxMana + 1, MaxManaCap);
		var updatedPlayer = player with { MaxMana = newMax, CurrentMana = newMax };
		state = state.UpdateObject(ActivePlayerId, updatedPlayer);

		// Clear per-turn flags on all creatures the active player controls
		var battlefieldCreatures = state
			.GetCardsInZone(BattlefieldId)
			.Where(c => c.ControllerId == ActivePlayerId && c.HasComponent<CreatureComponent>())
			.ToList();

		foreach (var creature in battlefieldCreatures)
		{
			var component = creature.GetComponent<CreatureComponent>()!;
			var reset = component with
			{
				HasSummoningSickness = false,
				HasAttacked = false,
				Damage = 0,
			};
			state = state.UpdateObject(creature.Id, creature.WithComponentReplaced(reset));
		}

		var spawned = ImmutableList<GameAction>.Empty;

		if (!SkipDraw)
		{
			spawned = spawned.Add(new DrawCardsAction { PlayerId = ActivePlayerId, Amount = 1 });
		}

		return new ActionResult(state)
		{
			Events = ImmutableList.Create<GameEvent>(
				new TurnStartedEvent { PlayerId = ActivePlayerId }
			),
			SpawnedActions = spawned,
		};
	}
}
