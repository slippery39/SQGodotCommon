using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Creates one token per point of the source creature's effective power, plus an offset — or per
/// +1/+1 counter on it when UseCounters is set.
///
/// USE COUNTERS FOR ANY "X = the number of +1/+1 counters" CARD. The power route was written
/// before counters existed and is subtly broken on a DEATH trigger: MoveCardTracked strips
/// StaticPowerToughnessModifier when a card leaves the battlefield, and it does so BEFORE
/// CheckStateBasedEffectsAction resolves the death trigger. Chasm Skulker therefore measured
/// power 1, applied its -1 offset, and created ZERO tokens — a shipped card that silently did
/// nothing. (The comment that used to sit here claimed "its modifiers travel with it". They do
/// not.)
///
/// PlusOneCounterComponent is deliberately absent from that strip list — counters are cleared when
/// a card ENTERS the battlefield instead, so a card in the graveyard still knows what it had. See
/// PutIntoBattlefieldAction.ApplyEntryCounters.
///
/// The power route is kept for any card that genuinely means power rather than counters.
/// </summary>
public record CreateTokensPerPowerAction : GameAction
{
	public Card? CardTemplate { get; init; }

	/// <summary>Added to the measured count. -1 for a 1/1 base whose counters start at zero.</summary>
	public int Offset { get; init; } = 0;

	/// <summary>Count +1/+1 counters rather than effective power. Survives a zone change.</summary>
	public bool UseCounters { get; init; } = false;

	public override ActionResult Execute(GameState gameState)
	{
		if (CardTemplate == null)
			return new ActionResult(gameState);

		var sourceId = GetInput<int>(ContextKeys.SourceCardId, 0);
		var controllerId = GetInput<int>(ContextKeys.CastingPlayerId, 0);

		if (sourceId == 0 || controllerId == 0 || !gameState.HasObject(sourceId))
			return new ActionResult(gameState);

		var measured = UseCounters
			? (gameState.GetObject(sourceId) as Card)
				?.GetComponent<PlusOneCounterComponent>()
				?.Count ?? 0
			: gameState.GetEffectivePower(sourceId);

		var count = measured + Offset;
		if (count <= 0)
			return new ActionResult(gameState);

		return new ActionResult(
			gameState.SpawnAction(
				new CreateCardAction
				{
					CardTemplate = CardTemplate,
					ControllerId = controllerId,
					Count = count,
				}
			)
		);
	}
}
