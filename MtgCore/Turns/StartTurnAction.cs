using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Begins the active player's turn.
///
/// Responsibilities:
///   - Increments MaxMana by 1 (capped at 10) and refills CurrentMana to MaxMana
///   - Draws one card, unless SkipDraw is true (used for the first turn of the game)
///   - Clears HasSummoningSickness, HasAttacked on CreatureComponent
///   - Clears HasActivated on all ActivatedAbilityComponents
///   - Resets Damage on CreatureComponent
///   All of the above apply only to creatures the active player controls.
///
/// Emits TurnStartedEvent.
/// </summary>
public record StartTurnAction : GameAction
{
	private const int MaxManaCap = 10;

	public int ActivePlayerId { get; init; }
	public int BattlefieldId { get; init; }
	public bool SkipDraw { get; init; } = false;

	public override ActionResult Execute(GameState gameState)
	{
		var state = gameState;

		// Increment max mana and refill
		var player = state.GetPlayer(ActivePlayerId);
		var newMax = Math.Min(player.MaxMana + 1, MaxManaCap);
		var updatedPlayer = player with { MaxMana = newMax, CurrentMana = newMax };
		state = state.UpdateObject(ActivePlayerId, updatedPlayer);

		// Reset per-turn flags on all permanents the active player controls
		var battlefieldCards = state
			.GetCardsInZone(BattlefieldId)
			.Where(c => c.ControllerId == ActivePlayerId)
			.ToList();

		foreach (var card in battlefieldCards)
		{
			var updatedComponents = card.Components;

			for (int i = 0; i < updatedComponents.Count; i++)
			{
				updatedComponents = updatedComponents[i] switch
				{
					CreatureComponent cc => updatedComponents.SetItem(
						i,
						cc with
						{
							HasSummoningSickness = false,
							HasAttacked = false,
							Damage = 0,
						}
					),
					ActivatedAbilityComponent ac => updatedComponents.SetItem(
						i,
						ac with
						{
							HasActivated = false,
						}
					),
					_ => updatedComponents,
				};
			}

			if (updatedComponents != card.Components)
				state = state.UpdateObject(card.Id, card with { Components = updatedComponents });
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
