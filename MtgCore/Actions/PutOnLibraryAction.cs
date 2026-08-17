using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Puts each target permanent on the top or bottom of its OWNER's library — Condemn,
/// Aetherspouts, Aether Gust, Anchor to the Aether.
///
/// Exists because MoveCardToTopOfLibraryAction and MoveCardToBottomOfLibraryAction are plain
/// GameActions that read their card ids from their own fields or a pipeline context key. They
/// cannot receive targets: ResolveEffectAction only injects into an ITargetedAction, so a card
/// built on them with a targeting strategy resolved against NO cards and silently did nothing.
/// Condemn and Aetherspouts both did exactly that in play.
///
/// Those two actions are left alone rather than converted — they work correctly inside the
/// pipelines that use them (scry, Sleight of Hand), and rewriting a working action to fix a
/// different caller is a bigger change than adding the targeted one.
///
/// The card goes to its OWNER's library, not the caster's, which is what "its owner's library"
/// means and matters for a stolen permanent.
/// </summary>
public record PutOnLibraryAction : EffectAction
{
	/// <summary>True for the bottom of the library, false for the top.</summary>
	public bool Bottom { get; init; } = true;

	public override ActionResult Execute(GameState gameState)
	{
		var state = gameState;
		var events = ImmutableList<GameEvent>.Empty;

		foreach (var targetId in ResolveTargetIds())
		{
			if (!state.HasObject(targetId))
				continue;
			if (state.GetObject(targetId) is not Card card)
				continue;

			// A permanent leaving the battlefield fires the leave event, so equipment detaches,
			// auras die with it, and statics are stripped — exactly as destruction would.
			var wasOnBattlefield = state.GetCardZone(targetId).ZoneType == ZoneType.Battlefield;

			if (wasOnBattlefield)
			{
				var leftEvent = new PermanentLeftBattlefieldEvent
				{
					CardId = card.Id,
					OwnerId = card.OwnerId,
				};
				state = state with { PendingGameEvents = state.PendingGameEvents.Add(leftEvent) };
				events = events.Add(leftEvent);
			}

			var libraryId = state.GetPlayerZoneId(card.OwnerId, ZoneType.Library);
			if (libraryId == 0)
				continue;

			state = state.MoveCardTracked(targetId, libraryId);

			// MoveCardTracked appends, which is the bottom. Only the top needs a second move.
			if (!Bottom)
				state = state.MoveObjectToFront(targetId, libraryId);
		}

		return new ActionResult(state) { Events = events };
	}
}
