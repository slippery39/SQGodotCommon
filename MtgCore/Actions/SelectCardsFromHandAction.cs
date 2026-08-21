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

	/// <summary>
	/// The player whose hand it is decides — discarding is the case that made this necessary.
	/// An opponent's Avaricious Dragon discards at the end of THEIR turn, and the active-player
	/// fallback prompted the human to pick which of the opponent's cards to throw away.
	/// </summary>
	public override int GetDecidingPlayerId(
		GameState gameState,
		ImmutableDictionary<string, object> pipelineContext
	) => ResolvePlayerId(pipelineContext);

	private int ResolvePlayerId(ImmutableDictionary<string, object> pipelineContext) =>
		PlayerId != 0 ? PlayerId
		: pipelineContext.TryGetValue(ContextKeys.CastingPlayerId, out var id) ? (int)id
		: 0;

	public override ImmutableList<ChoiceOption> GetOptions(
		GameState gameState,
		ImmutableDictionary<string, object> pipelineContext
	)
	{
		var playerId = ResolvePlayerId(pipelineContext);

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
