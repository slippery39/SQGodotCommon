using ImmutableGameObjects;
using MagicCore.Cards;
using MagicCore.Effects;

namespace MagicCore.Rules;

/// <summary>
/// Numeric replacement effects (CR 614): "if [it] would happen, it happens with N more instead".
/// Lifted from MtgCore's ReplacementEngine: scanned live where the event happens, and applied to
/// the amount INSIDE the one action — so one event is raised and nothing can feed itself. A
/// replacement on 0 does nothing: nothing would have happened.
///
/// ponytail: bonuses only — MtgCore's multipliers and prevention wait for a card that needs them.
/// </summary>
public static class Replacements
{
	/// <summary>The amount after every replacement of this kind on this player's permanents.</summary>
	/// <param name="exceptId">A permanent entering right now: its own replacement doesn't apply to its
	/// entry — it applies once it is on the battlefield (CR 614.12).</param>
	public static int Apply(
		GameState s,
		ReplaceableEvent evt,
		int playerId,
		int amount,
		int exceptId = 0
	) =>
		amount <= 0
			? amount
			: amount
				+ s.CardsIn(playerId, ZoneType.Battlefield)
					.Where(c => c.Id != exceptId)
					.SelectMany(s.AbilitiesOf<AmountReplacement>)
					.Where(r => r.Event == evt)
					.Sum(r => r.Bonus);

	/// <summary>
	/// Brad Boimler: "until end of turn, if one or more counters would be put on a permanent you
	/// control, that many plus one of each of those kinds of counters are put on that permanent
	/// instead." — ONE call at every place counters are put on a permanent (PutCounters, +1/+1, -1/-1,
	/// charge, stun, loyalty, time — entering with them and proliferate too): +1 per such effect its
	/// controller has this turn. The finality counter is a yes/no here, so one more changes nothing.
	/// </summary>
	public static int Counters(GameState s, int controllerId, int amount) =>
		amount <= 0
			? amount
			: amount + s.ThisTurnEffects<CountersPlusOneThisTurn>(controllerId).Count();
}
