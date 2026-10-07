using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MagicCore.Cards;

[Flags]
public enum CardType
{
	None = 0,
	Artifact = 1 << 0,
	Battle = 1 << 1,
	Creature = 1 << 2,
	Enchantment = 1 << 3,
	Instant = 1 << 4,
	Kindred = 1 << 5,
	Land = 1 << 6,
	Planeswalker = 1 << 7,
	Sorcery = 1 << 8,
}

[Flags]
public enum Supertype
{
	None = 0,
	Basic = 1 << 0,
	Legendary = 1 << 1,
	Snow = 1 << 2,
	World = 1 << 3,
}

/// <summary>
/// A card in any zone. Printed characteristics are parsed from <see cref="CardData"/> by
/// <see cref="CardFactory"/> — never typed in by hand. GameObject.Name is the Scryfall name,
/// the key back to the printed data.
/// </summary>
public record Card : GameObject
{
	public int OwnerId { get; init; }
	public int ControllerId { get; init; }
	public CardType Types { get; init; }
	public Supertype Supertypes { get; init; }
	public ImmutableArray<string> Subtypes { get; init; } = ImmutableArray<string>.Empty;
	public ManaCost ManaCost { get; init; } = ManaCost.None;

	/// <summary>Scryfall's printed keywords ("Flash", "Flying"). Rules read these; nothing retypes them.</summary>
	public ImmutableArray<string> Keywords { get; init; } = ImmutableArray<string>.Empty;
	public ImmutableArray<ManaColor> Colors { get; init; } = ImmutableArray<ManaColor>.Empty;
	public int BasePower { get; init; }
	public int BaseToughness { get; init; }
	public bool IsToken { get; init; }

	/// <summary>
	/// The ONE place power and toughness are computed: printed + counters + until-end-of-turn
	/// effects. Every rule reads these, never BasePower — a second sum drifts.
	/// </summary>
	public int Power =>
		BasePower + PlusOneCounters + Components.OfType<UntilEndOfTurn>().Sum(u => u.Power);
	public int Toughness =>
		BaseToughness + PlusOneCounters + Components.OfType<UntilEndOfTurn>().Sum(u => u.Toughness);

	// ===== Permanent-only state — wiped by Fresh() whenever the card changes zones =====

	public bool IsTapped { get; init; }
	public int PlusOneCounters { get; init; }
	public int Damage { get; init; }
	public bool DamagedByDeathtouch { get; init; }

	/// <summary>TurnNumber it came under its controller's control — summoning sickness reads it.</summary>
	public int ControlledSinceTurn { get; init; }

	/// <summary>The player (later: planeswalker) it is attacking; 0 when not attacking.</summary>
	public int AttackingId { get; init; }

	/// <summary>The attacker it blocks; 0 when not blocking.</summary>
	public int BlockingId { get; init; }

	/// <summary>Stays true after its blockers leave — a blocked creature deals no damage without trample (CR 509.1h).</summary>
	public bool IsBlocked { get; init; }

	/// <summary>
	/// CR 400.7: a card that changes zones is a new object with no memory of its previous one.
	/// The ONLY reset — every zone change goes through MagicState.MoveCard, which calls this.
	/// </summary>
	public Card Fresh() =>
		this with
		{
			IsTapped = false,
			PlusOneCounters = 0,
			Damage = 0,
			DamagedByDeathtouch = false,
			ControlledSinceTurn = 0,
			AttackingId = 0,
			BlockingId = 0,
			IsBlocked = false,
			Components = Components.RemoveAll(c => c is Actions.OnStackComponent or UntilEndOfTurn),
		};

	/// <summary>Printed keywords plus any granted until end of turn.</summary>
	public bool Has(string keyword) =>
		Keywords.Contains(keyword)
		|| Components.Any(c => c is UntilEndOfTurn u && u.Keyword == keyword);

	public bool Is(CardType type) => (Types & type) != 0;

	public bool Is(Supertype supertype) => (Supertypes & supertype) != 0;
}

/// <summary>
/// "Gets +P/+T and gains Keyword until end of turn" on a permanent. Removed in cleanup (CR 514.2)
/// and by any zone change.
/// </summary>
public record UntilEndOfTurn(int Power = 0, int Toughness = 0, string? Keyword = null)
	: GameComponent;

/// <summary>
/// What goes in a deck: the printed data plus the hand-written ability components. A card with no
/// components plays as its printed types and cost only — a vanilla creature, an instant that does
/// nothing. That is correct for a vanilla card and silently wrong for any other.
/// </summary>
public sealed record CardDefinition(CardData Data, ImmutableArray<GameComponent> Components)
{
	public static CardDefinition Vanilla(CardData data) => new(data, []);
}

public static class CardFactory
{
	public static Card Create(CardDefinition definition, int ownerId)
	{
		var data = definition.Data;
		// A multi-face card (FRA's "prepare") is its front face everywhere but the stack.
		var front = data.CardFaces?[0];
		var (types, supertypes, subtypes) = ParseTypeLine(front?.TypeLine ?? data.TypeLine);
		return new Card
		{
			Name = data.Name,
			OwnerId = ownerId,
			ControllerId = ownerId,
			Types = types,
			Supertypes = supertypes,
			Subtypes = subtypes,
			ManaCost = ManaCost.Parse(front?.ManaCost ?? data.ManaCost),
			BasePower = ParsePowerToughness(front?.Power ?? data.Power),
			BaseToughness = ParsePowerToughness(front?.Toughness ?? data.Toughness),
			Colors = [.. (data.Colors ?? []).Select(ManaCost.ColorOf)],
			// Scryfall merges every face's keywords; keep the ones the front face's text names.
			Keywords = front is null
				? [.. data.Keywords]
				:
				[
					.. data.Keywords.Where(k =>
						front.OracleText?.Contains(k, StringComparison.OrdinalIgnoreCase) == true
					),
				],
			Components = definition.Components,
		};
	}

	/// <summary>
	/// ponytail: "*" and "1+*" read as their number part; the card's own ability must define the rest.
	/// Negative is real — Loot, the Anomaly is -2/4.
	/// </summary>
	static int ParsePowerToughness(string? text) =>
		int.TryParse(
			System.Text.RegularExpressions.Regex.Match(text ?? "", @"^-?\d+").Value,
			out var n
		)
			? n
			: 0;

	/// <summary>
	/// "Legendary Artifact Creature — Human Wizard". Throws on a word it does not know, so a set
	/// that introduces a new type fails at import time instead of producing a typeless card.
	/// </summary>
	public static (CardType, Supertype, ImmutableArray<string>) ParseTypeLine(string typeLine)
	{
		var parts = typeLine.Split(" — ", 2);
		var types = CardType.None;
		var supertypes = Supertype.None;

		foreach (var word in parts[0].Split(' ', StringSplitOptions.RemoveEmptyEntries))
		{
			if (Enum.TryParse<CardType>(word, out var type))
				types |= type;
			else if (Enum.TryParse<Supertype>(word, out var supertype))
				supertypes |= supertype;
			else
				throw new FormatException($"Unknown card type '{word}' in \"{typeLine}\"");
		}

		var subtypes =
			parts.Length > 1
				? parts[1].Split(' ', StringSplitOptions.RemoveEmptyEntries).ToImmutableArray()
				: ImmutableArray<string>.Empty;
		return (types, supertypes, subtypes);
	}
}
