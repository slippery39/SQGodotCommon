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
	/// SameColor: every unit the one colour chosen (Loot, the Nexus). Restriction: what it may pay
	/// for. Sacrificed: it is sacrificed as it makes the mana (Treasure). A Mountain makes an extra
	/// {R} per Molten Tide this turn.
	/// </summary>
	public record Yield(
		ImmutableArray<ImmutableArray<ManaColor>> Units,
		bool SameColor,
		ManaRestriction Restriction,
		bool Sacrificed = false
	)
	{
		/// <summary>May this mana pay for that spell (null: an ability, or anything but a spell)?</summary>
		public bool CanPayFor(GameState s, Card? spell) =>
			Restriction switch
			{
				ManaRestriction.NotForSpellsFromHand => spell is null
					|| !s.IsIn(spell.Id, spell.OwnerId, ZoneType.Hand),
				ManaRestriction.OnlyPlaneswalkerSpells => spell is not null
					&& spell.Is(CardType.Planeswalker),
				_ => true,
			};
	}

	/// <summary>THE answer to "what does tapping this make" — payment, manual tapping and the X bound all ask.</summary>
	public static Yield YieldOf(GameState s, Card card)
	{
		var colors = ColorsOf(s, card).ToImmutableArray();
		if (colors.IsEmpty)
			return new([], false, ManaRestriction.None);
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
		// ponytail: one restriction per permanent — all its mana abilities share it (Heartwood
		// Crafter's {C}, Gideon's Memorial's five colours). Track it per unit if a card mixes them.
		var abilities = s.AbilitiesOf<ManaAbilityComponent>(card).ToList();
		var restriction =
			abilities.Count > 0 && !card.Subtypes.Any(IsBasicLandType)
				? abilities[0].Restriction
				: ManaRestriction.None;
		return new(units, oneColor is not null, restriction, oneColor?.SacrificeSelf == true);
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
