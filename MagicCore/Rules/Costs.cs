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
	public static ManaCost Of(GameState s, int casterId, Card card)
	{
		var delta = 0;
		if (!card.Is(CardType.Creature))
			delta += s.CardsIn(s.OpponentOf(casterId), ZoneType.Battlefield)
				.SelectMany(c => c.GetComponents<OpponentsNoncreatureSpellsCostMore>())
				.Sum(t => t.Amount);

		var controlsLegendaryCreature = s.CardsIn(casterId, ZoneType.Battlefield)
			.Any(c => c.Is(CardType.Creature) && c.Is(Supertype.Legendary));
		if (controlsLegendaryCreature)
			delta -= card.GetComponents<CostsLessIfYouControlLegendaryCreature>()
				.Sum(r => r.Amount);

		return delta == 0 ? card.ManaCost : card.ManaCost.WithGeneric(delta);
	}
}
