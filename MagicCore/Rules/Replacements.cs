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
					.SelectMany(c => c.GetComponents<AmountReplacement>())
					.Where(r => r.Event == evt)
					.Sum(r => r.Bonus);
}
