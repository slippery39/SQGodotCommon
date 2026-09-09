using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// The single place a land's mana reaches a player.
///
/// PlayLandAction and PutLandIntoPlayAction carried byte-identical copies of this — read the
/// BonusManaLandComponent, add to MaxMana, add to CurrentMana unless deferred — and a third
/// near-copy lives in GainPermanentManaAction for fetch effects. When colour was added, that
/// duplication was the bug waiting to happen: a land played from hand would produce its colour
/// and the same land fetched by Rampant Growth would not, and nothing would report it.
///
/// Mirrors <see cref="CostEngine"/>, which owns the other end for the same reason.
/// </summary>
public static class ManaEngine
{
	/// <summary>
	/// Applies a land's full mana contribution — generic and coloured — to its controller.
	///
	/// <paramref name="countsAsLandDrop"/> is the one thing the two callers disagree on: a land
	/// played from hand consumes the per-turn land drop, a land fetched onto the battlefield does
	/// not. LandsPlayedTotal increments either way, because Terravore counts both.
	/// </summary>
	public static GameState GrantLandMana(
		this GameState state,
		int playerId,
		Card? card,
		bool countsAsLandDrop
	)
	{
		var bonus = card?.GetComponent<BonusManaLandComponent>();
		var generic = 1 + (bonus?.ExtraMana ?? 0);
		var colors = card?.GetComponent<LandColorComponent>()?.Produces ?? ManaPool.Empty;

		// "Enters tapped": the land counts toward next turn's refill but produces nothing now.
		// Applies to BOTH tracks — a tap land that handed over its colour immediately would be
		// strictly better than an untapped one for any card whose cost is mostly pips.
		var deferred = bonus?.Deferred == true;

		var player = state.GetPlayer(playerId);

		return state.UpdateObject(
			playerId,
			player with
			{
				MaxMana = player.MaxMana + generic,
				CurrentMana = deferred ? player.CurrentMana : player.CurrentMana + generic,
				MaxColorMana = player.MaxColorMana.Add(colors),
				CurrentColorMana = deferred
					? player.CurrentColorMana
					: player.CurrentColorMana.Add(colors),
				LandsPlayedThisTurn = player.LandsPlayedThisTurn + (countsAsLandDrop ? 1 : 0),
				LandsPlayedTotal = player.LandsPlayedTotal + 1,
			}
		);
	}
}
