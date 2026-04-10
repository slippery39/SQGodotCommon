using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Begins the active player's turn.
///
/// Responsibilities:
///   - Increments MaxMana by 1 (capped at 10) and refills CurrentMana to MaxMana
///   - Applies BonusMana on top of CurrentMana (does not affect MaxMana)
///   - Draws one card, unless SkipDraw is true
///   - Draws BonusDraws additional cards after the normal draw
///   - Clears HasSummoningSickness, HasAttacked, Damage on CreatureComponent
///   - Clears HasActivated on ActivatedAbilityComponents
///   All of the above apply only to creatures the active player controls.
///
/// BonusMana and BonusDraws are used for first-turn Player 2 compensation.
///
/// Emits TurnStartedEvent.
/// </summary>
public record StartTurnAction : GameAction
{
	private const int MaxManaCap = 10;

	public int ActivePlayerId { get; init; }
	public int BattlefieldId { get; init; }
	public bool SkipDraw { get; init; } = false;

	/// <summary>
	/// Extra mana granted this turn only, on top of the normal MaxMana refill.
	/// Does not affect MaxMana. Used for Player 2 first-turn compensation.
	/// </summary>
	public int BonusMana { get; init; } = 0;

	/// <summary>
	/// Extra cards drawn after the normal turn draw.
	/// Used for Player 2 first-turn compensation.
	/// </summary>
	public int BonusDraws { get; init; } = 0;

	public override ActionResult Execute(GameState gameState)
	{
		var state = gameState;

		// Increment max mana, refill, then apply bonus
		var player = state.GetPlayer(ActivePlayerId);
		var newMax = Math.Min(player.MaxMana + 1, MaxManaCap);
		var newCurrent = Math.Min(newMax + BonusMana, MaxManaCap);
		var updatedPlayer = player with { MaxMana = newMax, CurrentMana = newCurrent };
		state = state.UpdateObject(ActivePlayerId, updatedPlayer);

		// Reset per-turn flags on all permanents the active player controls
		var battlefieldCards = state
			.GetCardsInZone(BattlefieldId)
			.Where(c => c.ControllerId == ActivePlayerId)
			.ToList();

		foreach (var card in battlefieldCards)
		{
			// First clear end-of-turn modifiers (Giant Growth etc.)
			state = state.ClearEndOfTurnModifiers(card.Id);

			// Re-fetch card in case it was updated by ClearEndOfTurnModifiers
			var currentCard = (Card)state.GetObject(card.Id);
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
					currentCard.Id,
					currentCard with
					{
						Components = updatedComponents,
					}
				);
		}

		var spawned = ImmutableList<GameAction>.Empty;

		if (!SkipDraw)
			spawned = spawned.Add(new DrawCardsAction { PlayerId = ActivePlayerId, Amount = 1 });

		if (BonusDraws > 0)
			spawned = spawned.Add(
				new DrawCardsAction { PlayerId = ActivePlayerId, Amount = BonusDraws }
			);

		var finalState = spawned.IsEmpty ? state : state.SpawnActions(spawned);
		return new ActionResult(finalState)
		{
			Events = ImmutableList.Create<GameEvent>(
				new TurnStartedEvent { PlayerId = ActivePlayerId }
			),
		};
	}
}
