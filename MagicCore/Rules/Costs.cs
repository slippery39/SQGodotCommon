using ImmutableGameObjects;
using MagicCore.Cards;
using MagicCore.Effects;

namespace MagicCore.Rules;

/// <summary>
/// The ONE place a spell's effective cost is computed (CR 601.2f): printed cost, plus taxes, minus
/// reductions. Casting validates and pays through it, and so does any UI showing a cost in hand.
/// </summary>
public static class Costs
{
	/// <summary>
	/// A card in its owner's graveyard may be cast for this — its flashback, or its mana cost when
	/// it gained flashback this turn — or not at all (null). The ONE place either is decided.
	/// </summary>
	///
	/// ponytail: with both, the granted one (mana cost, no discard) is taken — cheaper for every FRA
	/// card. Make it a choice on the cast if a card's own flashback is ever the better deal.
	public static FlashbackComponent? Flashback(GameState s, Card card)
	{
		if (!s.IsIn(card.Id, card.OwnerId, ZoneType.Graveyard))
			return null;
		if (card.GetComponents<GrantedFlashback>().Any(g => g.OnTurn == s.GetGame().TurnNumber))
			return new FlashbackComponent(card.ManaCost);
		return card.GetComponent<FlashbackComponent>();
	}

	/// <summary>
	/// An activated ability's effective cost for these targets — Warrior's Blades' equip is {1} less
	/// for each +1/+1 counter on the creature it targets.
	/// </summary>
	public static ManaCost OfAbility(
		GameState s,
		ActivatedAbilityComponent ability,
		System.Collections.Immutable.ImmutableArray<int> targets
	)
	{
		if (
			!ability.CostsLessPerCounterOnTarget
			|| targets.IsDefaultOrEmpty
			|| !s.IsOnBattlefield(targets[0])
		)
			return ability.Cost;
		var counters = s.GetCard(targets[0]).PlusOneCounters;
		return counters == 0 ? ability.Cost : ability.Cost.WithGeneric(-counters);
	}

	/// <param name="addedGeneric">
	/// X and any additional generic cost ("or pay {N}"). Taxes and reductions apply to the TOTAL (CR
	/// 601.2f) — Geist's {1} off an {X}{R} spell would be lost on the printed cost, which has no generic.
	/// </param>
	/// <param name="free">"Without paying its mana cost" (Omnipresence) — taxes and additional costs still apply.</param>
	public static ManaCost Of(
		GameState s,
		int casterId,
		Card card,
		int addedGeneric = 0,
		bool free = false
	)
	{
		var printed = free ? ManaCost.None : Flashback(s, card)?.Cost ?? card.ManaCost; // flashback is an alternative cost: taxes still apply
		// Null Summoner: "mana of any type can be spent to cast that spell".
		if (!free && Playable.IsCastableThroughLink(s, card, casterId))
			printed = printed.OfAnyType();
		var yours = s.CardsIn(casterId, ZoneType.Battlefield).ToList();
		var delta = addedGeneric;
		if (!card.Is(CardType.Creature))
			delta +=
				s.CardsIn(s.OpponentOf(casterId), ZoneType.Battlefield)
					.SelectMany(s.AbilitiesOf<OpponentsNoncreatureSpellsCostMore>)
					.Sum(t => t.Amount)
				- yours.SelectMany(s.AbilitiesOf<YourNoncreatureSpellsCostLess>).Sum(r => r.Amount);

		var controlsLegendaryCreature = yours.Any(c =>
			c.Is(CardType.Creature) && c.Is(Supertype.Legendary)
		);
		if (controlsLegendaryCreature)
			delta -= card.GetComponents<CostsLessIfYouControlLegendaryCreature>()
				.Sum(r => r.Amount);

		if (s.GetPlayer(casterId).ThisTurn.NoncreatureSpellsCast > 0)
			delta -= card.GetComponents<CostsLessIfYouCastNoncreatureSpellThisTurn>()
				.Sum(r => r.Amount);

		if (card.GetComponent<CostsLessByGreatestStatYouControl>() is { } greatest)
			delta -= yours
				.Where(c => c.Is(CardType.Creature))
				.Select(c => Math.Max(0, greatest.Toughness ? s.Toughness(c) : s.Power(c)))
				.DefaultIfEmpty(0)
				.Max();

		return delta == 0 ? printed : printed.WithGeneric(delta);
	}
}
