using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// The choice half of Scry: offers the top N cards of a player's library and lets them pick any
/// subset to put on the bottom.
///
/// MinChoices is 0 — keeping everything on top is a legal scry, and the most common one. An
/// unconditional "put the top card on the bottom" is not scry at all; it is strictly worse than
/// doing nothing about half the time, which is why this exists rather than a plain
/// MoveCardToBottomOfLibraryAction.
///
/// Mirrors SelectCardsFromHandAction: a ChoiceAction that builds its options from live state at
/// the moment the pipeline pauses.
/// </summary>
public record SelectTopCardsToBottomAction : ChoiceAction
{
	public int PlayerId { get; init; }
	public int Amount { get; init; } = 1;

	public override ImmutableList<ChoiceOption> GetOptions(
		GameState gameState,
		ImmutableDictionary<string, object> pipelineContext
	)
	{
		var playerId =
			PlayerId != 0 ? PlayerId
			: pipelineContext.TryGetValue(ContextKeys.CastingPlayerId, out var id) ? (int)id
			: 0;

		if (playerId == 0)
			return ImmutableList<ChoiceOption>.Empty;

		var libraryId = gameState.GetPlayerZoneId(playerId, ZoneType.Library);

		return gameState
			.GetCardsInZone(libraryId)
			.Take(Amount)
			.Select(c => new ChoiceOption { Id = c.Id, DisplayText = $"Bottom: {c.Name}" })
			.ToImmutableList();
	}
}
