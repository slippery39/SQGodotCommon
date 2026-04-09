using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Checks state-based loss conditions and evaluates pending triggered abilities.
///
/// Registered as GameState.PostActionProcessor at game setup. Runs automatically
/// after every standalone action and every completed pipeline — never between
/// individual pipeline steps.
///
/// While GameState.SuppressPostProcessor is true this action never runs.
///
/// Player1BattlefieldId and Player2BattlefieldId are stored directly to avoid
/// scanning the entire IdToGameObjectMap on every execution — a significant
/// performance improvement given this runs after every action.
///
/// Order of operations:
///   1. Evaluate PendingGameEvents against all TriggeredAbilityComponents
///      on both battlefields and spawn ResolveEffectAction for each match
///   2. Clear PendingGameEvents
///   3. Check loss conditions (life <= 0 only)
///
/// IsPostProcessor = true prevents recursive post-processing cycles.
/// </summary>
public record CheckStateBasedEffectsAction : GameAction
{
	public int Player1Id { get; init; }
	public int Player2Id { get; init; }
	public int Player1BattlefieldId { get; init; }
	public int Player2BattlefieldId { get; init; }

	public override bool IsPostProcessor => true;

	public override ActionResult Execute(GameState gameState)
	{
		var state = gameState;
		var events = ImmutableList<GameEvent>.Empty;

		var player1 = state.GetPlayer(Player1Id);
		var player2 = state.GetPlayer(Player2Id);

		if (player1.HasLost && player2.HasLost)
			return new ActionResult(
				state with
				{
					PendingGameEvents = ImmutableList<GameEvent>.Empty,
				}
			);

		// ===== TRIGGERED ABILITIES =====
		// Evaluated before loss conditions so triggers resolve against the full game state.

		var pendingEvents = state.PendingGameEvents;

		if (!pendingEvents.IsEmpty)
		{
			// Use known battlefield IDs directly — no full object map scan needed
			var battlefieldCards = state
				.GetCardsInZone(Player1BattlefieldId)
				.Concat(state.GetCardsInZone(Player2BattlefieldId));

			foreach (var card in battlefieldCards)
			{
				var triggeredAbilities = card.GetComponents<TriggeredAbilityComponent>();

				foreach (var ability in triggeredAbilities)
				{
					var triggerContext = new TriggerContext
					{
						GameState = state,
						SourceCardId = card.Id,
						ControllingPlayerId = card.ControllerId,
					};

					foreach (var pendingEvent in pendingEvents)
					{
						if (ability.Condition.IsSatisfiedBy(pendingEvent, triggerContext))
						{
							state = state.SpawnAction(
								new ResolveEffectAction
								{
									Effects = ImmutableList.Create(ability.Effect),
									CastingPlayerId = card.ControllerId,
									SourceCardId = card.Id,
								}
							);
						}
					}
				}
			}
		}

		state = state with { PendingGameEvents = ImmutableList<GameEvent>.Empty };

		// ===== STATE-BASED EFFECTS =====

		var p1ShouldLose = !player1.HasLost && player1.Life <= 0;
		var p2ShouldLose = !player2.HasLost && player2.Life <= 0;

		if (p1ShouldLose)
		{
			player1 = player1 with { HasLost = true };
			state = state.UpdateObject(Player1Id, player1);
			events = events.Add(
				new PlayerLostEvent { PlayerId = Player1Id, Reason = "life total reached zero" }
			);
		}

		if (p2ShouldLose)
		{
			player2 = player2 with { HasLost = true };
			state = state.UpdateObject(Player2Id, player2);
			events = events.Add(
				new PlayerLostEvent { PlayerId = Player2Id, Reason = "life total reached zero" }
			);
		}

		if (p1ShouldLose || p2ShouldLose)
		{
			var winnerId = (p1ShouldLose, p2ShouldLose) switch
			{
				(true, true) => -1,
				(true, false) => Player2Id,
				(false, true) => Player1Id,
				_ => -1,
			};

			events = events.Add(new GameOverEvent { WinnerPlayerId = winnerId });
		}

		return new ActionResult(state) { Events = events };
	}
}
