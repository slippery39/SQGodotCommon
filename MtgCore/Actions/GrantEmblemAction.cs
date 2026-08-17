using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Gives a player an emblem — "you get an emblem with ...".
///
/// This is what a planeswalker ultimate resolves into. Emblems already existed as a
/// player-owned persistent trigger (MtgPlayer.Emblems, scanned by
/// CheckStateBasedEffectsAction); they were only reachable by playing a land with
/// GrantEmblemComponent, so there was no way for an effect to grant one.
///
/// Emblems have no zone and cannot be removed, which is correct — that is exactly what an
/// emblem is.
/// </summary>
public record GrantEmblemAction : EffectAction
{
	public Emblem? Emblem { get; init; }

	public override ActionResult Execute(GameState gameState)
	{
		if (Emblem == null)
			return new ActionResult(gameState);

		var state = gameState;

		foreach (var targetId in ResolveTargetIds())
		{
			if (state.GetObject(targetId) is not MtgPlayer player)
				continue;

			// Emblems stack — two ultimates give two emblems, both firing.
			state = state.UpdateObject(
				targetId,
				player with
				{
					Emblems = player.Emblems.Add(Emblem),
				}
			);
		}

		return new ActionResult(state);
	}
}
