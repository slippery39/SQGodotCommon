using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Begins the active player's turn.
///
/// Responsibilities:
///   - Increments MaxMana by 1 (capped at 10) and refills CurrentMana to MaxMana
///   - Draws one card, unless SkipDraw is true
///   - Clears HasSummoningSickness, HasAttacked, Damage on CreatureComponent
///   - Clears HasActivated on ActivatedAbilityComponents
///   All of the above apply only to permanents the active player controls.
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

		// Increment max mana, refill, then apply bonus
		var player = state.GetPlayer(ActivePlayerId);
		var newMax = Math.Min(player.MaxMana + 1, MaxManaCap);
		var newCurrent = newMax;
		var updatedPlayer = player with { MaxMana = newMax, CurrentMana = newCurrent };
		state = state.UpdateObject(ActivePlayerId, updatedPlayer);

		// Reset storm counter at the start of each turn
		var game = state.TryGetGame();
		if (game != null)
			state = state.UpdateObject(game.Id, game with { SpellsCastThisTurn = 0 });

		// Reset per-turn flags on all permanents the active player controls
		var battlefieldCardIds = state
			.GetCardsInZone(BattlefieldId)
			.Where(c => c.ControllerId == ActivePlayerId)
			.Select(c => c.Id)
			.ToList();

		foreach (var cardId in battlefieldCardIds)
		{
			// First clear end-of-turn modifiers (Giant Growth etc.)
			state = state.ClearEndOfTurnModifiers(cardId);

			// Re-fetch card in case it was updated by ClearEndOfTurnModifiers
			var currentCard = (Card)state.GetObject(cardId);
			var updatedComponents = currentCard.Components;

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

			if (updatedComponents != currentCard.Components)
				state = state.UpdateObject(
					cardId,
					currentCard with
					{
						Components = updatedComponents,
					}
				);
		}

		var spawned = ImmutableList<GameAction>.Empty;

		if (!SkipDraw)
			spawned = spawned.Add(new DrawCardsAction { PlayerId = ActivePlayerId, Amount = 1 });

		var turnStartedEvent = new TurnStartedEvent { PlayerId = ActivePlayerId };
		var stateWithEvent = (spawned.IsEmpty ? state : state.SpawnActions(spawned)) with
		{
			PendingGameEvents = state.PendingGameEvents.Add(turnStartedEvent),
		};
		return new ActionResult(stateWithEvent)
		{
			Events = ImmutableList.Create<GameEvent>(turnStartedEvent),
		};
	}
}
