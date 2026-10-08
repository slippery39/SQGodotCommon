using ImmutableGameObjects;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Turns;

namespace MagicCore;

/// <summary>Which permanents make mana, and when they may. The one place both are answered.</summary>
public static class ManaAbilities
{
	public static IEnumerable<ManaColor> ColorsOf(Card card) =>
		card
			.Subtypes.Select(BasicLandColor)
			.OfType<ManaColor>()
			.Concat(card.GetComponents<ManaAbilityComponent>().Select(m => m.Color))
			.Concat(card.GetComponents<ChosenColorMana>().Select(m => m.Color))
			.Distinct();

	static ManaColor? BasicLandColor(string subtype) =>
		subtype switch
		{
			"Plains" => ManaColor.White,
			"Island" => ManaColor.Blue,
			"Swamp" => ManaColor.Black,
			"Mountain" => ManaColor.Red,
			"Forest" => ManaColor.Green,
			_ => null,
		};

	/// <summary>{T} in ANY cost: untapped, and a creature must not be summoning sick (CR 302.6).</summary>
	public static bool CanPayTapCost(GameState s, Card card) =>
		!card.IsTapped
		&& (
			!card.Is(CardType.Creature) || s.Has(card, "Haste") || !Combat.IsSummoningSick(s, card)
		);

	public static bool CanTap(GameState s, Card card) =>
		ColorsOf(card).Any() && CanPayTapCost(s, card);

	/// <summary>Permanents that can make mana for this player right now, optionally leaving one out
	/// (the permanent whose {T} is itself the cost being paid).</summary>
	public static IEnumerable<Card> Sources(GameState s, int playerId, int excludeId = 0) =>
		s.CardsIn(playerId, ZoneType.Battlefield).Where(c => c.Id != excludeId && CanTap(s, c));
}
