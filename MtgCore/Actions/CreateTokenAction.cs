using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Creates one or more token cards directly onto a player's battlefield.
///
/// ControllerId resolution:
///   - If ControllerId is non-zero, that player gets the tokens.
///   - If ControllerId is 0, reads from InputContext[CastingPlayerId] (set by ResolveEffectAction).
///
/// Count resolution:
///   - If CountInputKey is set, reads the count from InputContext[CountInputKey] (pipeline context).
///   - Otherwise uses the Count field directly.
///
/// Each token is added as a fresh game object — tokens never come from a player's hand or library.
/// HasSummoningSickness is set based on the token template's HasHaste flag.
/// CreatureEnteredBattlefieldEvent is emitted per token so ETB triggers fire normally.
/// </summary>
public record CreateTokenAction : GameAction
{
	public Card TokenTemplate { get; init; } = null!;
	public int ControllerId { get; init; } = 0;
	public int Count { get; init; } = 1;

	/// <summary>
	/// Pipeline context key to read the token count from. Takes precedence over Count if set.
	/// </summary>
	public string CountInputKey { get; init; } = "";

	public override ActionResult Execute(GameState gameState)
	{
		var controllerId =
			ControllerId != 0 ? ControllerId : GetInput<int>(ContextKeys.CastingPlayerId, 0);

		var count = !string.IsNullOrEmpty(CountInputKey) ? GetInput<int>(CountInputKey, 0) : Count;

		if (controllerId == 0 || count <= 0)
			return new ActionResult(gameState);

		var state = gameState;
		var events = ImmutableList<GameEvent>.Empty;
		var battlefieldId = state.GetPlayerZoneId(controllerId, ZoneType.Battlefield);

		for (int i = 0; i < count; i++)
		{
			var creature = TokenTemplate.GetComponent<CreatureComponent>();
			if (creature == null)
				continue;

			var token = TokenTemplate with
			{
				OwnerId = controllerId,
				ControllerId = controllerId,
				Components = TokenTemplate
					.Components.RemoveAll(c => c is CreatureComponent)
					.Add(creature with { HasSummoningSickness = !creature.HasHaste }),
			};

			(state, var addedToken) = state.AddObject(token, battlefieldId);

			var enteredEvent = new CreatureEnteredBattlefieldEvent
			{
				CardId = addedToken.Id,
				PlayerId = controllerId,
			};
			events = events.Add(enteredEvent);
			state = state with { PendingGameEvents = state.PendingGameEvents.Add(enteredEvent) };
		}

		return new ActionResult(state) { Events = events };
	}
}
