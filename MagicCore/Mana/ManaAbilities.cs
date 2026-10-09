using System.Collections.Immutable;
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
			.Concat(s.HasAbility<ManaOfOneColorComponent>(card) ? AnyColor : [])
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

	/// <summary>White, blue, black, red, green — "any color" (never colorless).</summary>
	public static readonly ImmutableArray<ManaColor> AnyColor =
	[
		ManaColor.White,
		ManaColor.Blue,
		ManaColor.Black,
		ManaColor.Red,
		ManaColor.Green,
	];

	/// <summary>
	/// What tapping it for mana makes: one unit per mana, each with the colours it may be.
	/// SameColor: every unit the one colour chosen (Loot, the Nexus). NotForSpellsFromHand:
	/// Heartwood Crafter's restriction. A Mountain makes an extra {R} per Molten Tide this turn.
	/// </summary>
	public record Yield(
		ImmutableArray<ImmutableArray<ManaColor>> Units,
		bool SameColor,
		bool NotForSpellsFromHand
	);

	/// <summary>THE answer to "what does tapping this make" — payment, manual tapping and the X bound all ask.</summary>
	public static Yield YieldOf(GameState s, Card card)
	{
		var colors = ColorsOf(s, card).ToImmutableArray();
		if (colors.IsEmpty)
			return new([], false, false);
		var context = Conditions.Of(card);
		var oneColor = s.AbilitiesOf<ManaOfOneColorComponent>(card).FirstOrDefault();
		ImmutableArray<ImmutableArray<ManaColor>> units = oneColor is null
			? [colors]
			: [.. Enumerable.Repeat(AnyColor, Math.Max(0, oneColor.Amount.Value(s, context)))];
		// Molten Tide: "whenever you tap a Mountain for mana, add an additional {R}" — a mana
		// ability triggering on a mana ability, so it resolves at once (CR 605.1b), as more mana.
		if (card.Subtypes.Contains("Mountain"))
			units =
			[
				.. units,
				.. Enumerable.Repeat<ImmutableArray<ManaColor>>(
					[ManaColor.Red],
					s.GetPlayer(card.ControllerId).ThisTurn.ExtraRedFromMountains
				),
			];
		var abilities = s.AbilitiesOf<ManaAbilityComponent>(card).ToList();
		var restricted =
			abilities.Count > 0
			&& abilities.All(a => a.NotForSpellsFromHand)
			&& !card.Subtypes.Any(IsBasicLandType);
		return new(units, oneColor is not null, restricted);
	}

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
