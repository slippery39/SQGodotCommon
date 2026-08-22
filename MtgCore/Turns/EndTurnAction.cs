using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Ends the active player's turn and begins the next player's turn.
///
/// Responsibilities:
///   - Switches ActivePlayerId to the opponent
///   - Increments TurnNumber when a full round completes
///     (i.e. when Player 2 ends their turn — both players have gone)
///   - Spawns StartTurnAction for the next player
///
/// Emits TurnEndedEvent.
/// </summary>
public record EndTurnAction : GameAction
{
	public int GameId { get; init; }
	public int Player1Id { get; init; }
	public int Player2Id { get; init; }

	public override ActionResult Execute(GameState gameState)
	{
		var game = gameState.GetGame(GameId);
		var activePlayerId = game.ActivePlayerId;

		// An extra turn is spent here rather than passing play: the same player starts again, and
		// the round counter does not advance, because no round completed.
		var takingExtraTurn = game.ExtraTurnsQueued > 0;

		var nextPlayerId =
			takingExtraTurn ? activePlayerId
			: activePlayerId == Player1Id ? Player2Id
			: Player1Id;

		var newTurnNumber =
			!takingExtraTurn && activePlayerId == Player2Id ? game.TurnNumber + 1 : game.TurnNumber;

		var updatedGame = game with
		{
			ActivePlayerId = nextPlayerId,
			TurnNumber = newTurnNumber,
			Phase = TurnPhase.Main,
			ExtraTurnsQueued = takingExtraTurn ? game.ExtraTurnsQueued - 1 : 0,
		};

		var state = gameState.UpdateObject(GameId, updatedGame);

		// Strip "this turn" replacement effects from BOTH players. Done here rather than in
		// StartTurnAction because that only touches the active player, so a prevention effect
		// cleaned up there would linger through the opponent's entire turn.
		state = ClearEndOfTurnReplacements(state, Player1Id);
		state = ClearEndOfTurnReplacements(state, Player2Id);

		// Impulse draw ("you may play it this turn") expires for the player whose turn is
		// ending, not the one about to start — the same reason the line above lives here and
		// not in StartTurnAction.
		state = ClearExpiredImpulseDraws(state, activePlayerId);

		// "Exile it at end of turn" — the one-turn tokens. See ExileAtEndOfTurnComponent.
		state = ExileEndOfTurnPermanents(state, Player1Id);
		state = ExileEndOfTurnPermanents(state, Player2Id);

		// "Until end of turn, this permanent becomes a 4/4 creature" wears off. Both players for
		// the same reason as the two lines above: StartTurnAction only touches the active player,
		// so an animation cleaned up there would survive the opponent's entire turn.
		state = RevertAnimatedPermanents(state, Player1Id);
		state = RevertAnimatedPermanents(state, Player2Id);

		var nextBattlefieldId = state.GetPlayerZoneId(nextPlayerId, ZoneType.Battlefield);

		var startTurn = new StartTurnAction
		{
			ActivePlayerId = nextPlayerId,
			BattlefieldId = nextBattlefieldId,
		};

		// PendingGameEvents is the trigger feed. TurnEndedEvent was previously only on the
		// caller-visible Events list, so no "at the beginning of the end step" trigger could
		// ever fire — the same silent-inertness as CardDiscardedEvent and PlayerGainedLifeEvent.
		var turnEndedEvent = new TurnEndedEvent { PlayerId = activePlayerId };
		state = state with { PendingGameEvents = state.PendingGameEvents.Add(turnEndedEvent) };

		var events = ImmutableList.Create<GameEvent>(turnEndedEvent);

		// The end step's triggers must be DISPATCHED before the next turn begins, so the
		// post-processor is scheduled explicitly ahead of StartTurnAction rather than left to
		// the executor.
		//
		// The executor stages its post-processor alongside whatever the action spawned and
		// flushes once, and FlushSpawnQueue reverses as it pushes — so the processor lands
		// UNDERNEATH anything spawned here. StartTurnAction would therefore run first, and it
		// zeroes LifeLostThisTurn for BOTH players. Every "if a player lost N life this turn"
		// trigger then read a counter that had just been reset: Knight of the Ebon Legion never
		// grew and Chandra's Phoenix never returned, while the counter itself tested fine.
		//
		// Fixed here rather than in the executor because that ordering is load-bearing engine
		// wide — inverting it globally breaks 19 tests. Ending a turn is the one place that
		// genuinely needs its events dispatched before its own follow-up runs.
		var spawns = state.PostActionProcessor is { } processor
			? ImmutableList.Create<GameAction>(processor, startTurn)
			: ImmutableList.Create<GameAction>(startTurn);

		return new ActionResult(state.SpawnActions(spawns)) { Events = events };
	}

	/// <summary>
	/// Exiles every permanent the given player controls that is marked to last one turn.
	///
	/// MoveCardTracked rather than MoveObject, so the token announces
	/// PermanentLeftBattlefieldEvent on the way out — otherwise an Aura on it, a static ability it
	/// was granting, or a freeze it was holding would all quietly outlive the token itself.
	/// </summary>
	private static GameState ExileEndOfTurnPermanents(GameState state, int playerId)
	{
		var battlefieldId = state.GetPlayerZoneId(playerId, ZoneType.Battlefield);

		// Materialised first: the loop moves cards out of the zone it is reading.
		foreach (var card in state.GetCardsInZone(battlefieldId).ToList())
		{
			if (!card.HasComponent<ExileAtEndOfTurnComponent>())
				continue;

			state = state.MoveCardTracked(card.Id, state.GetPlayerZoneId(playerId, ZoneType.Exile));
		}

		return state;
	}

	/// <summary>
	/// Takes the creature body back off every permanent AnimateAction gave one to.
	///
	/// Three removals, and skipping any of them leaves a different silent bug behind:
	///   - the CreatureComponent and the marker, or the animation is permanent;
	///   - the subtype the animation added, and ONLY that one, read back off the marker;
	///   - the anthem effects the board stamped on it while it was a creature. StampEffect
	///     unconditionally Adds, so a permanent animated on three consecutive turns under a
	///     Glorious Anthem would accumulate three +1/+1 boosts — the same accounting failure that
	///     made a bounced creature collect a second one. ProcessPermanentLeft handles the other
	///     half, dropping this card out of every source's AffectedIds index.
	///
	/// Damage needs no handling: it lives on the CreatureComponent being removed, so an animated
	/// permanent that survived the turn is undamaged when it animates again. That matches
	/// MoveCardTracked, which clears damage for the same reason.
	/// </summary>
	private static GameState RevertAnimatedPermanents(GameState state, int playerId)
	{
		var battlefieldId = state.GetPlayerZoneId(playerId, ZoneType.Battlefield);
		var game = state.TryGetGame();

		foreach (var card in state.GetCardsInZone(battlefieldId).ToList())
		{
			var animated = card.GetComponent<AnimatedUntilEndOfTurnComponent>();
			if (animated == null)
				continue;

			if (game != null)
				state = StaticAbilityEngine.ProcessPermanentLeft(state, card.Id, game.Id);

			state = ZoneTransitionExtensions.StripAppliedComponents(state, card.Id);

			if (state.GetObject(card.Id) is not Card stripped)
				continue;

			var subtypes = string.IsNullOrEmpty(animated.AddedSubtype)
				? stripped.Subtypes
				: stripped.Subtypes.Remove(animated.AddedSubtype);

			state = state.UpdateObject(
				card.Id,
				stripped with
				{
					Types = stripped.EffectiveTypes & ~CardType.Creature,
					Subtypes = subtypes,
					Components = stripped
						.Components.Where(c =>
							c is not (CreatureComponent or AnimatedUntilEndOfTurnComponent)
						)
						.ToImmutableArray(),
				}
			);
		}

		return state;
	}

	/// <summary>
	/// Strips ExiledPlayableComponent from every card in the given player's exile zone. Anything
	/// impulse-drawn during the turn that just ended goes from "playable" to plain exiled.
	/// </summary>
	private static GameState ClearExpiredImpulseDraws(GameState state, int playerId)
	{
		var game = state.TryGetGame();
		if (game == null || game.PlayableExiledIds.IsEmpty)
			return state;

		// Walk the index rather than the exile zone — exile grows all game (every played land
		// lands there), while this set holds only the handful still playable.
		var exileId = state.GetPlayerZoneId(playerId, ZoneType.Exile);
		var expired = ImmutableHashSet<int>.Empty;

		foreach (var cardId in game.PlayableExiledIds)
		{
			// HasObject first: GetObject is a raw indexer and throws, and this set is explicitly
			// allowed to hold stale ids.
			if (!state.HasObject(cardId) || state.GetObject(cardId) is not Card card)
			{
				expired = expired.Add(cardId); // gone entirely — drop the stale id
				continue;
			}
			if (state.GetParent(cardId) != exileId)
				continue; // the other player's, or no longer in exile

			expired = expired.Add(cardId);
			if (card.HasComponent<ExiledPlayableComponent>())
				state = state.UpdateObject(
					cardId,
					card.WithoutComponents<ExiledPlayableComponent>()
				);
		}

		if (expired.IsEmpty)
			return state;

		var current = state.TryGetGame()!;
		return state.UpdateObject(
			current.Id,
			current with
			{
				PlayableExiledIds = current.PlayableExiledIds.Except(expired),
			}
		);
	}

	private static GameState ClearEndOfTurnReplacements(GameState state, int playerId)
	{
		if (state.GetObject(playerId) is not MtgPlayer player)
			return state;

		var kept = player
			.Components.Where(c =>
				c is not ReplacementModifierComponent r
				|| r.Duration != ModifierDuration.UntilEndOfTurn
			)
			.ToImmutableArray();

		return kept.Length == player.Components.Length
			? state
			: state.UpdateObject(playerId, player with { Components = kept });
	}
}
