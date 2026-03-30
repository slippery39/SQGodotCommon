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
		var log = new System.Text.StringBuilder();
		log.AppendLine(
			$"GetOptions called. PipelineContext keys: {string.Join(", ", pipelineContext.Keys)}"
		);

		var playerId =
			PlayerId != 0 ? PlayerId
			: pipelineContext.TryGetValue(ContextKeys.CastingPlayerId, out var id) ? (int)id
			: 0;

		log.AppendLine($"Resolved playerId: {playerId}");

		if (playerId == 0)
		{
			log.AppendLine("PlayerId was 0, returning empty");
			File.AppendAllText("C:/temp/mtg_debug.txt", log.ToString());
			return ImmutableList<ChoiceOption>.Empty;
		}

		var handId = gameState.GetPlayerZoneId(playerId, ZoneType.Hand);
		var cards = gameState.GetCardsInZone(handId).ToList();
		log.AppendLine($"Cards in hand: {cards.Count}");

		File.AppendAllText("C:/temp/mtg_debug.txt", log.ToString());

		return cards
			.Select(c => new ChoiceOption { Id = c.Id, DisplayText = c.Name })
			.ToImmutableList();
	}
}
