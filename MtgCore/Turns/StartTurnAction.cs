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

	/// <summary>
	/// Expires everything stamped UntilYourNextTurn on the given player and their board. Mirrors
	/// EndTurnAction.ClearEndOfTurnReplacements, but fires a turn later and for one player.
	///
	/// TWO axes, and missing either leaves an effect that never wears off:
	///
	/// - BOTH HOMES. Prevention lives on the player (Safe Passage) or on a single creature
	///   (Gods Willing, see PreventDamageAction), so the player object alone is not enough.
	/// - BOTH COMPONENT KINDS. ReplacementModifierComponent AND AppliedKeywordComponent.
	///   ClearEndOfTurnModifiers only ever handles UntilEndOfTurn, so a keyword granted for a
	///   turn cycle — Gods Willing's hexproof — had nothing anywhere that would remove it.
	///
	/// Both failures are silent: no error, just a creature that is quietly permanently hexproof
	/// or permanently immune to damage. PowerToughnessModifier is deliberately NOT swept here;
	/// nothing grants P/T for a turn cycle and CLAUDE.md records that as a known limitation.
	/// </summary>
	private static GameState ClearUntilYourNextTurnEffects(GameState state, int playerId)
	{
		if (state.GetObject(playerId) is MtgPlayer player)
		{
			var kept = Strip(player.Components);
			if (kept.Length != player.Components.Length)
				state = state.UpdateObject(playerId, player with { Components = kept });
		}

		var battlefieldId = state.GetPlayerZoneId(playerId, ZoneType.Battlefield);
		if (battlefieldId == 0)
			return state;

		foreach (var card in state.GetCardsInZone(battlefieldId).ToList())
		{
			var kept = Strip(card.Components);
			if (kept.Length != card.Components.Length)
				state = state.UpdateObject(card.Id, card with { Components = kept });
		}

		return state;

		static ImmutableArray<GameComponent> Strip(ImmutableArray<GameComponent> components) =>
			components
				.Where(c =>
					c switch
					{
						ReplacementModifierComponent r => r.Duration
							!= ModifierDuration.UntilYourNextTurn,
						AppliedKeywordComponent k => k.Duration
							!= ModifierDuration.UntilYourNextTurn,
						_ => true,
					}
				)
				.ToImmutableArray();
	}

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

		// LifeLostThisTurn resets for BOTH players, unlike the per-player counters above.
		// Life loss is overwhelmingly something that happens to the NON-active player — you
		// attack them, you drain them — so an active-player-only reset would let the defender's
		// count carry over from their own turn and "a player lost 4 life this turn" would fire
		// on a total spanning two turns. LifeGainedThisTurn has the same shape but its only
		// reader checks its own controller at its own turn end, so it never surfaced.
		foreach (var id in new[] { MtgObjectKeys.Player1, MtgObjectKeys.Player2 })
		{
			var pid = state.GetWellKnownId(id);
			if (state.GetObject(pid) is MtgPlayer p)
				state = state.UpdateObject(pid, p with { LifeLostThisTurn = 0 });
		}

		// "Until the start of your next turn" expires HERE, for the player whose turn is beginning
		// — that is what lets a shield cast on your turn survive the opponent's turn in between.
		// EndTurnAction deliberately leaves this duration alone; it only strips UntilEndOfTurn.
		state = ClearUntilYourNextTurnEffects(state, ActivePlayerId);

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
					CreaturesDiedThisTurn = 0,
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
							// Cover burns down on ITS CONTROLLER's turn, same clock as
							// FrozenTurns. That is what makes Cover 1 mean "survives the
							// opponent's next turn" rather than expiring before it ever
							// protected anything.
							CoverTurns = Math.Max(0, cc.CoverTurns - 1),
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
