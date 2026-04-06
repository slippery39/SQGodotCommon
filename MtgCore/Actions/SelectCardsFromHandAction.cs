using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// A ChoiceAction that dynamically builds its options from the cards
/// currently in the target player's hand at the moment the pipeline pauses.
///
/// PlayerId can be set directly or read from pipeline context via
/// ContextKeys.CastingPlayerId when used inside a spell effect pipeline.
/// </summary>
public record SelectCardsFromHandAction : ChoiceAction
{
	public int PlayerId { get; init; }

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
		{
			return ImmutableList<ChoiceOption>.Empty;
		}

		var handId = gameState.GetPlayerZoneId(playerId, ZoneType.Hand);
		var cards = gameState.GetCardsInZone(handId).ToList();

		return cards
			.Select(c => new ChoiceOption { Id = c.Id, DisplayText = c.Name })
			.ToImmutableList();
	}
}
