using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Remembers which card an Oblivion Ring-style permanent exiled, so it can be given back when
/// that permanent leaves the battlefield.
///
/// Stored on the exiling permanent rather than the exiled card, because the exiled card may
/// change zones again and the link must survive that.
/// </summary>
public record LinkedExileComponent : GameComponent
{
	public int ExiledCardId { get; init; }
}

/// <summary>
/// "When this enters the battlefield, exile another target nonland permanent" — the first half
/// of Oblivion Ring.
///
/// Records the exiled card on the source so ReturnLinkedExileAction can find it again. Plain
/// ExileAction is not enough: it forgets what it exiled, and the return half would have nothing
/// to look up.
/// </summary>
public record ExileLinkedAction : GameAction, ITargetedAction
{
	public ImmutableList<int> TargetIds { get; init; } = ImmutableList<int>.Empty;

	public GameAction WithTargets(ImmutableList<int> targetIds) =>
		this with
		{
			TargetIds = targetIds,
		};

	public override ActionResult Execute(GameState gameState)
	{
		var sourceId = GetInput<int>(ContextKeys.SourceCardId, 0);
		if (sourceId == 0 || TargetIds.IsEmpty)
			return new ActionResult(gameState);

		var targetId = TargetIds[0];
		if (!gameState.HasObject(targetId) || gameState.GetObject(targetId) is not Card target)
			return new ActionResult(gameState);

		var state = gameState;

		var leftEvent = new PermanentLeftBattlefieldEvent
		{
			CardId = target.Id,
			OwnerId = target.OwnerId,
		};
		state = state with { PendingGameEvents = state.PendingGameEvents.Add(leftEvent) };

		var exileId = state.GetPlayerZoneId(target.OwnerId, ZoneType.Exile);
		state = state.MoveCardTracked(targetId, exileId);

		if (state.GetObject(sourceId) is Card source)
			state = state.UpdateObject(
				sourceId,
				source with
				{
					Components = source.Components.Add(
						new LinkedExileComponent { ExiledCardId = targetId }
					),
				}
			);

		return new ActionResult(state) { Events = ImmutableList.Create<GameEvent>(leftEvent) };
	}
}

/// <summary>
/// "When this leaves the battlefield, return the exiled card to the battlefield under its
/// owner's control" — the second half of Oblivion Ring.
///
/// Must be used on a Graveyard-active trigger: by the time the leave is scanned, the Ring has
/// already moved, so a Battlefield-scoped trigger silently never fires.
/// </summary>
public record ReturnLinkedExileAction : GameAction
{
	public override ActionResult Execute(GameState gameState)
	{
		var sourceId = GetInput<int>(ContextKeys.SourceCardId, 0);
		if (sourceId == 0 || gameState.GetObject(sourceId) is not Card source)
			return new ActionResult(gameState);

		var link = source.GetComponent<LinkedExileComponent>();
		if (link == null || !gameState.HasObject(link.ExiledCardId))
			return new ActionResult(gameState);

		var state = gameState;

		// Clear the link first so a Ring that somehow leaves twice cannot return the card twice.
		state = state.UpdateObject(
			sourceId,
			source with
			{
				Components = source.Components.Remove(link),
			}
		);

		return new ActionResult(
			state.SpawnAction(
				new PutIntoBattlefieldAction { TargetIds = ImmutableList.Create(link.ExiledCardId) }
			)
		);
	}
}
