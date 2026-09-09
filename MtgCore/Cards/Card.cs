using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Base record for all cards. Cards are always children of a Zone in GameState.
///
/// A card's type and behaviour are determined entirely by its components:
///   - SpellComponent   — the card is a spell with auto-resolving effects
///   - CreatureComponent — the card is a creature with power/toughness/damage
///
/// Multiple components can be active simultaneously (e.g. a creature-land).
/// OwnerId and ControllerId default to 0 and are stamped on at deck
/// construction time via 'with'.
/// Subtypes (e.g. "Goblin", "Elf", "Beast") support tribal and type-based queries.
/// </summary>
public record Card : GameObject
{
	public int ManaCost { get; init; }

	/// <summary>
	/// The card's coloured requirement — its pips. "1W" is ManaCost 1 with ColorPips {White=1};
	/// "3WW" is ManaCost 3 with {White=2}. Empty means colourless, which is why an artifact needs
	/// no marker.
	///
	/// Paid from a SEPARATE track to ManaCost and never substitutable with it, so casting has no
	/// payment-ordering choice. Cost reductions (affinity, convoke, Goreclaw) reduce ManaCost only
	/// and never touch pips, matching MTG — a spell reduced to 0 generic still needs its colours.
	///
	/// Colour IDENTITY is derived from this rather than stored: a card is white iff its pips
	/// contain White. A second field would be a second source of truth to keep in step.
	/// </summary>
	public ManaPool ColorPips { get; init; } = ManaPool.Empty;
	public ImmutableList<AdditionalCost> AdditionalCastCosts { get; init; } =
		ImmutableList<AdditionalCost>.Empty;
	public int OwnerId { get; init; }
	public int ControllerId { get; init; }
	public ImmutableHashSet<string> Subtypes { get; init; } =
		ImmutableHashSet.Create<string>(StringComparer.OrdinalIgnoreCase);

	/// <summary>
	/// The card's declared types. Set by the card builders. Leave it None on a hand-built card
	/// and <see cref="EffectiveTypes"/> will derive them — see below.
	/// </summary>
	public CardType Types { get; init; } = CardType.None;

	public bool HasSubtype(string subtype) => Subtypes.Contains(subtype);

	/// <summary>
	/// The card's types, falling back to derivation when <see cref="Types"/> was never set.
	///
	/// The fallback exists so the type system could be added without editing every hand-built
	/// card in CardLibrary: those identify as artifacts and enchantments through magic strings in
	/// Subtypes, and as creatures through CreatureComponent. Derivation reads both, so a legacy
	/// card answers HasType correctly with no edit.
	///
	/// Declared types always win when present — derivation cannot tell an instant from a sorcery,
	/// which is exactly why the field exists.
	/// </summary>
	public CardType EffectiveTypes
	{
		get
		{
			if (Types != CardType.None)
				return Types;

			var derived = CardType.None;

			if (HasComponent<CreatureComponent>())
				derived |= CardType.Creature;
			if (HasSubtype("Artifact"))
				derived |= CardType.Artifact;
			if (HasSubtype("Enchantment"))
				derived |= CardType.Enchantment;
			if (HasSubtype("Land"))
				derived |= CardType.Land;
			if (HasSubtype("Planeswalker"))
				derived |= CardType.Planeswalker;

			// A card with no permanent marker and no other type is an instant or sorcery. Which
			// one is unknowable here, so report both — "is it a spell" is answerable, "is it an
			// instant" is not. Set Types explicitly if a card needs that distinction.
			if (derived == CardType.None && HasComponent<SpellComponent>())
				derived |= CardType.AnySpell;

			return derived;
		}
	}

	/// <summary>True if the card has ANY of the given types.</summary>
	public bool HasType(CardType type) => (EffectiveTypes & type) != CardType.None;
}
