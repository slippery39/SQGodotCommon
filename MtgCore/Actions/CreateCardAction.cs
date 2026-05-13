using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Creates one or more cards and places them onto a player's battlefield by spawning
/// a PutIntoBattlefieldAction per card. The ETB ceremony (sickness stamp, events)
/// is handled entirely by PutIntoBattlefieldAction.
///
/// ControllerId resolution:
///   - If ControllerId is non-zero, that player controls the created cards.
///   - If ControllerId is 0, reads from InputContext[CastingPlayerId].
///
/// Count resolution:
///   - If CountInputKey is set, reads the count from pipeline context.
///   - Otherwise uses Count directly.
/// </summary>
public record CreateCardAction : GameAction
{
	public Card CardTemplate { get; init; } = null!;
	public int ControllerId { get; init; } = 0;
	public int Count { get; init; } = 1;
	public string CountInputKey { get; init; } = "";

	public override ActionResult Execute(GameState gameState)
	{
		var controllerId =
			ControllerId != 0 ? ControllerId : GetInput<int>(ContextKeys.CastingPlayerId, 0);
		var count = !string.IsNullOrEmpty(CountInputKey) ? GetInput<int>(CountInputKey, 0) : Count;

		if (controllerId == 0 || count <= 0)
			return new ActionResult(gameState);

		var card = CardTemplate with { OwnerId = controllerId, ControllerId = controllerId };
		var state = gameState;

		for (int i = 0; i < count; i++)
			state = state.SpawnAction(new PutIntoBattlefieldAction { CardTemplate = card });

		return new ActionResult(state);
	}
}
