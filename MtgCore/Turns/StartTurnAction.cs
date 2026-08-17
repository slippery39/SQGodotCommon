using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Begins the active player's turn.
///
/// Responsibilities:
///   - Refills CurrentMana to MaxMana (permanent mana from lands; no auto-increment)
///   - Resets LandsPlayedThisTurn to 0
///   - Draws one card, unless SkipDraw is true
///   - Clears HasSummoningSickness, HasAttacked on CreatureComponent (Damage persists between turns)
///   - Clears HasActivated on ActivatedAbilityComponents
///   All of the above apply only to permanents the active player controls.
///
/// Emits TurnStartedEvent.
/// </summary>
public record StartTurnAction : GameAction
{
	public int ActivePlayerId { get; init; }
	public int BattlefieldId { get; init; }
	public bool SkipDraw { get; init; } = false;

	/// <summary>
	/// True when this creature must stay tapped through its own untap step — either it still owes
	/// frozen turns, or something on the battlefield is holding it down.
	/// </summary>
	private static bool StaysExhausted(CreatureComponent creature) =>
		creature.FrozenTurns > 0 || creature.FrozenBySourceId != 0;

	public override ActionResult Execute(GameState gameState)
	{
		var state = gameState;

		// Refill current mana to max (no auto-increment — mana comes from lands)
		var player = state.GetPlayer(ActivePlayerId);
		var updatedPlayer = player with
		{
			CurrentMana = player.MaxMana,
			LandsPlayedThisTurn = 0,
			LifeGainedThisTurn = 0,
		};
		state = state.UpdateObject(ActivePlayerId, updatedPlayer);

		// Roll the storm counter into last turn's count, then reset it. The roll-over is what
		// lets werewolf transform conditions ask "were no spells cast last turn?".
		var game = state.TryGetGame();
		if (game != null)
			state = state.UpdateObject(
				game.Id,
				game with
				{
					SpellsCastLastTurn = game.SpellsCastThisTurn,
					SpellsCastThisTurn = 0,
				}
			);

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

			for (int i = 0; i < updatedComponents.Length; i++)
			{
				updatedComponents = updatedComponents[i] switch
				{
					// IsExhausted clears here, for the ACTIVE player only — that is the untap
					// step. Exhausting an opponent's creature during your turn therefore costs
					// them exactly one attack, not zero and not two.
					//
					// A frozen creature does NOT untap: FrozenTurns burns down one per untap
					// step, and a source-linked freeze (Dungeon Geists) holds indefinitely until
					// CheckStateBasedEffectsAction sees its source leave the battlefield.
					CreatureComponent cc => updatedComponents.SetItem(
						i,
						cc with
						{
							HasSummoningSickness = false,
							HasAttacked = false,
							WasAttackedThisTurn = false,
							IsExhausted = StaysExhausted(cc),
							FrozenTurns = Math.Max(0, cc.FrozenTurns - 1),
						}
					),
					ActivatedAbilityComponent ac => updatedComponents.SetItem(
						i,
						ac with
						{
							ActivationCount = 0,
						}
					),
					// Only the per-turn count resets. TriggerCountTotal is deliberately left
					// alone — it is what makes renown "once ever".
					TriggeredAbilityComponent tc => updatedComponents.SetItem(
						i,
						tc with
						{
							TriggerCountThisTurn = 0,
						}
					),
					// Loyalty itself never resets — only the once-per-turn activation does.
					PlaneswalkerComponent pw => updatedComponents.SetItem(
						i,
						pw with
						{
							HasActivatedThisTurn = false,
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
			spawned = spawned.Add(new DrawCardsAction { TargetIds = [ActivePlayerId], Amount = 1 });

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
