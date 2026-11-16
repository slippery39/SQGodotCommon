using ImmutableGameObjects;
using MagicCore.Cards;
using MagicCore.Effects;

namespace MagicCore.Rules;

/// <summary>
/// Numeric replacement effects (CR 614): "if [it] would happen, [twice that many / that much plus N]
/// instead" — every NumericReplacement on either battlefield, and every ThisTurnReplacement. Lifted from
/// MtgCore's ReplacementEngine: scanned live where the event happens, and applied to the amount INSIDE
/// the one action — so one event is raised and nothing can feed itself. A replacement on 0 does
/// nothing: nothing would have happened.
///
/// ponytail: every plus first, then every "twice" — of the orders the affected player may pick (CR 616.1)
/// (n + 1) × 2 is always the most, which is the choice for your own; for an opponent's (Torbran beside an
/// opponent's doubler) they would pick the least. Ask them if such a pair ever meets. Prevention is
/// Damage.Prevented, not a replacement here.
/// </summary>
public static class Replacements
{
	/// <summary>The amount after every replacement of this event.</summary>
	/// <param name="affected">The player drawing or milling, the permanent getting counters, what is dealt damage.</param>
	/// <param name="source">Damage: its source.</param>
	/// <param name="exceptId">A permanent entering right now: its own replacement doesn't apply to its
	/// entry — it applies once it is on the battlefield (CR 614.12).</param>
	public static int Apply(
		GameState s,
		ReplacedEvent evt,
		GameObject affected,
		int amount,
		Card? source = null,
		string? kind = null,
		bool combat = false,
		int exceptId = 0
	)
	{
		if (amount <= 0)
			return amount;
		var plus = 0;
		var times = 1;
		// Loops, not LINQ: every damage, draw and counter asks, and almost never finds one.
		foreach (var player in (int[])[s.Player1Id(), s.Player2Id()])
		{
			foreach (var holder in s.CardsIn(player, ZoneType.Battlefield))
				if (holder.Id != exceptId)
					foreach (var r in s.AbilitiesOf<NumericReplacement>(holder))
						Add(r, holder);
			foreach (var holder in s.CardsIn(player, ZoneType.Command))
			foreach (var t in holder.GetComponents<ThisTurnReplacement>())
				Add(t.Replacement, holder);
		}
		return (amount + plus) * times;

		void Add(NumericReplacement r, Card holder)
		{
			if (r.Event != evt || r.Instead is not null)
				return;
			var asking = new Filters.FilterContext(holder.ControllerId, holder.Id);
			if (
				!Affects(s, r, holder, affected)
				|| r.Source is { } from && (source is null || !from.Matches(s, source, asking))
				|| r.CounterKind is { } k && k != kind
				|| r.Combat is { } c && c != combat
			)
				return;
			plus += r.Plus;
			times *= r.Times;
		}
	}

	/// <summary>Is this what the rule is about, and does its "if" hold — both asked from its holder's side.</summary>
	static bool Affects(GameState s, NumericReplacement r, Card holder, GameObject affected) =>
		r.Affected?.Matches(s, affected, new Filters.FilterContext(holder.ControllerId, holder.Id))
			!= false
		&& (
			r.If is not { } when
			|| when.Holds(s, Conditions.Of(holder).Add(ContextKeys.It, affected.Id))
		);

	/// <summary>
	/// What happens in place of ONE card's draw (NumericReplacement.Instead), and whose rule it is — null: the
	/// card is drawn. A "can't draw" (nothing instead: Narset) before any other, so Hullbreacher beside her makes
	/// no Treasure. ponytail: else the first found, player one's permanents first — of two that would apply the
	/// drawing player chooses (CR 616.1); ask them if such a pair ever meets.
	/// </summary>
	/// <param name="turnDraw">The draw step's own draw (Hullbreacher's "except the first one … in their draw steps").</param>
	public static (NumericReplacement Rule, Card Holder)? InsteadOfDraw(
		GameState s,
		MagicPlayer drawing,
		bool turnDraw
	)
	{
		(NumericReplacement, Card)? found = null;
		foreach (var player in (int[])[s.Player1Id(), s.Player2Id()])
		foreach (var holder in s.CardsIn(player, ZoneType.Battlefield))
		foreach (var r in s.AbilitiesOf<NumericReplacement>(holder))
		{
			if (
				r.Event != ReplacedEvent.Draw
				|| r.Instead is null
				|| turnDraw && r.ExceptTheTurnsDraw
				|| !Affects(s, r, holder, drawing)
			)
				continue;
			if (r.Instead.IsEmpty)
				return (r, holder);
			found ??= (r, holder);
		}
		return found;
	}
}
