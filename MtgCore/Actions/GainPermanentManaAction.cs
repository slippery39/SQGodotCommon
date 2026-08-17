using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Permanently raises each target player's mana, as playing a land does.
///
/// This is how "search your library for a Plains and put it onto the battlefield" is expressed
/// here (Knight of the White Orchid). Lands in this engine are not battlefield permanents — they
/// are consumed into MaxMana and exiled by PlayLandAction — so there is no Plains card to fetch.
/// The land's entire game effect is +1 mana, and that is what this grants.
///
/// Contrast AddTemporaryManaAction, which raises CurrentMana only so the bonus evaporates at the
/// next turn start. This raises both, so the gain is permanent.
///
/// LandsPlayedThisTurn is untouched: a fetched land must not consume the land drop, matching
/// PutLandIntoPlayAction. LandsPlayedTotal IS incremented so Terravore-style counters stay honest.
/// </summary>
public record GainPermanentManaAction : EffectAction
{
	public int Amount { get; init; } = 1;

	public override ActionResult Execute(GameState gameState)
	{
		var state = gameState;
		var amount = ResolveAmount(Amount);

		if (amount <= 0)
			return new ActionResult(gameState);

		foreach (var targetId in ResolveTargetIds())
		{
			if (state.GetObject(targetId) is not MtgPlayer player)
				continue;

			state = state.UpdateObject(
				targetId,
				player with
				{
					MaxMana = player.MaxMana + amount,
					CurrentMana = player.CurrentMana + amount,
					LandsPlayedTotal = player.LandsPlayedTotal + amount,
				}
			);
		}

		return new ActionResult(state);
	}
}
