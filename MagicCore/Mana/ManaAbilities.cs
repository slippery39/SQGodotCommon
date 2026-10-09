using ImmutableGameObjects;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Turns;

namespace MagicCore;

/// <summary>Which permanents make mana, and when they may. The one place both are answered.</summary>
public static class ManaAbilities
{
	public static IEnumerable<ManaColor> ColorsOf(GameState s, Card card)
	{
		// A basic land type's mana ability is intrinsic (CR 305.6) — an ability all the same.
		var intrinsic = card.Subtypes.Select(BasicLandColor).OfType<ManaColor>();
		if (intrinsic.Any() && s.HasLostAllAbilities(card))
			return [];
		return intrinsic
			.Concat(s.AbilitiesOf<ManaAbilityComponent>(card).Select(m => m.Color))
			.Concat(s.AbilitiesOf<ChosenColorMana>(card).Select(m => m.Color))
			.Distinct();
	}

	/// <summary>Plains, Island, Swamp, Mountain, Forest — domain counts these.</summary>
	public static bool IsBasicLandType(string subtype) => BasicLandColor(subtype) is not null;

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
		ColorsOf(s, card).Any() && CanPayTapCost(s, card);

	/// <summary>Permanents that can make mana for this player right now, optionally leaving one out
	/// (the permanent whose {T} is itself the cost being paid).</summary>
	public static IEnumerable<Card> Sources(GameState s, int playerId, int excludeId = 0) =>
		s.CardsIn(playerId, ZoneType.Battlefield).Where(c => c.Id != excludeId && CanTap(s, c));
}
