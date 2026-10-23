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
/// Who knows a card in a hidden zone, relative to its owner — a library or hand card is always in
/// its owner's zone, so "owner" and "opponent" say it for both players (story AI-3).
/// </summary>
[Flags]
public enum Knowers
{
	None = 0,
	Owner = 1 << 0,
	Opponent = 1 << 1,
	Everyone = Owner | Opponent,
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

	/// <summary>A copy of a spell (a prepared copy) — not a card; it ceases to exist off the stack (CR 704.5e).</summary>
	public bool IsCopy { get; init; }

	/// <summary>
	/// Bumped by every zone change: ids survive a zone change but the object does not (CR 400.7).
	/// "Is this still the same permanent?" compares this, never the id alone.
	/// </summary>
	public int Incarnation { get; init; }

	/// <summary>The loyalty printed in the corner; a planeswalker enters with that many counters (CR 306.5b).</summary>
	public int PrintedLoyalty { get; init; }

	/// <summary>
	/// The card's OWN power and toughness: printed (or a base-setting effect) + counters + until-end-
	/// of-turn effects. NOT what rules read — s.Power(card) adds every static on the battlefield.
	/// </summary>
	public int OwnPower =>
		(CurrentBase?.Power ?? BasePower)
		+ PlusOneCounters
		- MinusOneCounters
		+ Pumped(power: true);
	public int OwnToughness =>
		(CurrentBase?.Toughness ?? BaseToughness)
		+ PlusOneCounters
		- MinusOneCounters
		+ Pumped(power: false);

	// Loops, not LINQ, here and in HasOwn: LINQ boxes the ImmutableArray, and these are read at every
	// step the search simulates (story AI-7.3c).
	int Pumped(bool power)
	{
		var sum = 0;
		foreach (var component in Components)
			if (component is UntilEndOfTurn u)
				sum += power ? u.Power : u.Toughness;
		return sum;
	}

	/// <summary>The latest "has base power and toughness P/T" effect, if any (CR 613.4b, timestamp order).</summary>
	(int Power, int Toughness)? CurrentBase
	{
		get
		{
			(int, int)? latest = null;
			foreach (var component in Components)
				if (component is UntilEndOfTurn { SetBase: { } set })
					latest = set;
			return latest;
		}
	}

	/// <summary>
	/// Who knows this card while it is in a library or a hand — in a library, who knows its POSITION;
	/// in a hand, whether the opponent has seen it (the owner always knows its hand). For the search's
	/// "shuffle only what isn't known" (story AI-3). <see cref="MagicState.MoveCard"/> sets it on every
	/// zone change; surveil and "reveal" add to it; a shuffle clears the library's.
	/// </summary>
	public Knowers KnownTo { get; init; }

	// ===== Permanent-only state — wiped by Fresh() whenever the card changes zones =====

	public bool IsTapped { get; init; }
	public int PlusOneCounters { get; init; }

	/// <summary>Loyalty counters on a planeswalker. Damage removes them (CR 120.3c); at 0 it dies (CR 704.5i).</summary>
	public int Loyalty { get; init; }

	/// <summary>The X paid to cast this permanent — its "when this enters" abilities use it (CR 107.3m).</summary>
	public int CastX { get; init; }

	/// <summary>Null Summoner: "if you cast it" — it entered by resolving as a spell.</summary>
	public bool WasCast { get; init; }

	/// <summary>Gardenize's charge counters — a resource; nothing but the card that holds them reads them.</summary>
	public int ChargeCounters { get; init; }

	/// <summary>Vanishing's time counters (Flesh Duplicate) — one removed each upkeep; the last sacrifices it.</summary>
	public int TimeCounters { get; init; }

	/// <summary>The TurnNumber a loyalty ability of this permanent was last activated — once per turn (CR 606.3).</summary>
	public int LoyaltyActivatedOnTurn { get; init; }
	public int Damage { get; init; }
	public bool DamagedByDeathtouch { get; init; }

	/// <summary>TurnNumber it came under its controller's control — summoning sickness reads it.</summary>
	public int ControlledSinceTurn { get; init; }

	/// <summary>TurnNumber it entered the battlefield — "target creature that entered this turn".</summary>
	public int EnteredOnTurn { get; init; }

	/// <summary>TurnNumber it was last declared as an attacker — Hexhaven's "creature that attacked this turn".</summary>
	public int AttackedOnTurn { get; init; }

	/// <summary>-1/-1 counters — annihilated with +1/+1 counters by the state-based check (CR 704.5q).</summary>
	public int MinusOneCounters { get; init; }

	/// <summary>TurnNumber its "triggers only once each turn" ability last triggered.</summary>
	public int OncePerTurnTriggeredOn { get; init; }

	/// <summary>Prepare: its prepare spell's copy waits in exile (Rules/Prepare.cs).</summary>
	public bool IsPrepared { get; init; }

	/// <summary>An Aura or Equipment: the permanent it is attached to; 0 if none (Rules/Attachments.cs).</summary>
	public int AttachedToId { get; init; }

	/// <summary>Which object of that id — a host that left and came back is a new one (CR 400.7).</summary>
	public int AttachedToIncarnation { get; init; }

	/// <summary>"If a creature with a finality counter on it would die, exile it instead" — MagicState.MoveCard.</summary>
	public bool HasFinalityCounter { get; init; }

	/// <summary>"If a permanent with a stun counter would become untapped, remove one from it instead" — MagicState.Untap.</summary>
	public int StunCounters { get; init; }

	/// <summary>Ruric Thar, Magecrusher: "as long as they haven't dealt combat damage yet" — set by Rules.Damage.</summary>
	public bool HasDealtCombatDamage { get; init; }

	/// <summary>Its exhaust ability has been activated — never again for this object.</summary>
	public bool ExhaustUsed { get; init; }

	/// <summary>The player OR planeswalker it is attacking; 0 when not attacking. Never assume a player.</summary>
	public int AttackingId { get; init; }

	/// <summary>The attacker it blocks; 0 when not blocking.</summary>
	public int BlockingId { get; init; }

	/// <summary>Stays true after its blockers leave — a blocked creature deals no damage without trample (CR 509.1h).</summary>
	public bool IsBlocked { get; init; }

	/// <summary>
	/// CR 400.7: a card that changes zones is a new object with no memory of its previous one.
	/// The ONLY reset — every zone change goes through MagicState.MoveCard, which calls this.
	/// </summary>
	public Card Fresh() => WithoutAnimation().WithoutCopyEffect().Reset();

	Card Reset() =>
		this with
		{
			Incarnation = Incarnation + 1,
			ControllerId = OwnerId, // off the battlefield and stack it is its owner's (CR 108.4a); entering or casting sets it
			IsTapped = false,
			PlusOneCounters = 0,
			MinusOneCounters = 0,
			Loyalty = 0,
			CastX = 0,
			WasCast = false,
			ChargeCounters = 0,
			TimeCounters = 0,
			LoyaltyActivatedOnTurn = 0,
			EnteredOnTurn = 0,
			AttackedOnTurn = 0,
			OncePerTurnTriggeredOn = 0,
			IsPrepared = false,
			AttachedToId = 0,
			AttachedToIncarnation = 0,
			HasFinalityCounter = false,
			StunCounters = 0,
			HasDealtCombatDamage = false,
			ExhaustUsed = false,
			Damage = 0,
			DamagedByDeathtouch = false,
			ControlledSinceTurn = 0,
			AttackingId = 0,
			BlockingId = 0,
			IsBlocked = false,
			Components = Components.RemoveAll(IsStatus),
		};

	/// <summary>
	/// What this object was given or decided here, not what it is: gone with a zone change, and never
	/// copied (CR 707.2) — a copy of an exiling permanent doesn't own its exile, nor a pumped one its pump.
	/// </summary>
	public static bool IsStatus(GameComponent c) =>
		c
			is Actions.OnStackComponent
				or UntilEndOfTurn
				or Effects.ExiledUntilLeaves
				or Effects.GrantedFlashback
				or Effects.PlayableFromExile
				or Effects.ExiledBy
				or Effects.ChosenColorMana
				or CopyUntilEndOfTurn
				or EnteredAsCopy
				or Effects.DelayedTrigger
				or Animated
				or Effects.TapsForTwoColorlessWhileExiled;

	/// <summary>
	/// CR 707.2: this object with <paramref name="source"/>'s copiable values — name, cost, types,
	/// colours, P/T, keywords and abilities (themselves already copied, if it is a copy: CR 707.3).
	/// Its own status stays: counters, damage, tapped, what it was given. The ONE place a copy is made.
	/// </summary>
	public Card WithCopiableValuesOf(Card source) => WithValuesOf(source.WithoutAnimation()); // an Aura's type change isn't copiable

	Card WithValuesOf(Card source) =>
		this with
		{
			Name = source.Name,
			Types = source.Types,
			Supertypes = source.Supertypes,
			Subtypes = source.Subtypes,
			ManaCost = source.ManaCost,
			Keywords = source.Keywords,
			Colors = source.Colors,
			BasePower = source.BasePower,
			BaseToughness = source.BaseToughness,
			PrintedLoyalty = source.PrintedLoyalty,
			Components =
			[
				.. source.Components.Where(c => !IsStatus(c)),
				.. Components.Where(IsStatus),
			],
		};

	/// <summary>
	/// Animated by an Aura (Puppet Crafting): a creature of that subtype with that base P/T, in
	/// addition to what it was — and what it was, recorded to put back.
	/// </summary>
	public Card AnimatedBy(Card aura, Effects.EnchantedBecomesCreature becomes) =>
		(Card)
			(
				this with
				{
					Types = Types | CardType.Creature,
					Subtypes = Subtypes.Contains(becomes.Subtype)
						? Subtypes
						: Subtypes.Add(becomes.Subtype),
					BasePower = becomes.Power,
					BaseToughness = becomes.Toughness,
				}
			).WithComponent(
				new Animated(
					aura.Id,
					aura.Incarnation,
					Types,
					Subtypes.Contains(becomes.Subtype) ? null : becomes.Subtype,
					BasePower,
					BaseToughness
				)
			);

	/// <summary>Back to what it was before an Aura animated it — the Aura left, or it does (CR 400.7).</summary>
	public Card WithoutAnimation() =>
		GetComponent<Animated>() is { } a
			? (Card)
				(
					this with
					{
						Types = a.Types,
						Subtypes = a.AddedSubtype is null
							? Subtypes
							: Subtypes.Remove(a.AddedSubtype),
						BasePower = a.BasePower,
						BaseToughness = a.BaseToughness,
					}
				).WithoutComponents<Animated>()
			: this;

	/// <summary>Hall of Echoes back to itself — at cleanup, or as it leaves (CR 400.7).</summary>
	/// <summary>Leaving the battlefield: every copy effect undone — until end of turn first, then entered-as.</summary>
	public Card WithoutCopyEffect() =>
		WithoutCopyUntilEndOfTurn() is var card && card.GetComponent<EnteredAsCopy>() is { } entered
			? (Card)card.WithCopiableValuesOf(entered.Original).WithoutComponents<EnteredAsCopy>()
			: card;

	/// <summary>Cleanup: only Hall of Echoes' "until end of turn" copy ends — a Flesh Duplicate stays one.</summary>
	public Card WithoutCopyUntilEndOfTurn() =>
		GetComponent<CopyUntilEndOfTurn>() is { } copy
			? (Card)WithCopiableValuesOf(copy.Original).WithoutComponents<CopyUntilEndOfTurn>()
			: this;

	/// <summary>Printed keywords plus any granted until end of turn.</summary>
	public bool HasOwn(string keyword)
	{
		if (Keywords.Contains(keyword))
			return true;
		foreach (var component in Components)
			if (component is UntilEndOfTurn u && u.Keyword == keyword)
				return true;
		return false;
	}

	public bool Is(CardType type) => (Types & type) != 0;

	/// <summary>Its mana value — on the stack X counts (CR 202.3e); anywhere else X is 0.</summary>
	public int ManaValueNow =>
		ManaCost.ManaValue + (GetComponent<Actions.OnStackComponent>()?.X ?? 0);

	public bool Is(Supertype supertype) => (Supertypes & supertype) != 0;
}

/// <summary>
/// "Gets +P/+T and gains Keyword until end of turn" on a permanent. Removed in cleanup (CR 514.2)
/// and by any zone change.
/// </summary>
public record UntilEndOfTurn(int Power = 0, int Toughness = 0, string? Keyword = null)
	: GameComponent
{
	/// <summary>
	/// "Until your next turn": it lasts until this player's next turn begins (its untap step), not
	/// past this cleanup. 0 is "until end of turn".
	/// </summary>
	public int UntilTurnOf { get; init; }

	/// <summary>"Has base power and toughness P/T" - replaces the printed numbers, under counters and pumps.</summary>
	public (int Power, int Toughness)? SetBase { get; init; }

	/// <summary>Lyra: "Until end of turn, whenever [this] deals combat damage to a player, …" — Collect reads it.</summary>
	public Effects.TriggeredAbilityComponent? Trigger { get; init; }

	/// <summary>Essence Burn: "if that permanent would die this turn, exile it instead" — MoveCard reads it.</summary>
	public bool ExileIfItWouldDie { get; init; }

	/// <summary>Flourishing Grapple: "loses all abilities until end of turn" — Characteristics.HasLostAllAbilities.</summary>
	public bool LosesAllAbilities { get; init; }
}

/// <summary>
/// Hall of Echoes: "becomes a copy of [it] until end of turn" — what it was before, restored in
/// cleanup or on leaving the battlefield (Card.WithoutCopyEffect).
/// </summary>
public record CopyUntilEndOfTurn(Card Original) : GameComponent;

/// <summary>
/// Flesh Duplicate: it entered as a copy — what it was, restored as it leaves the battlefield
/// (Card.WithoutCopyEffect), never in cleanup.
/// </summary>
public record EnteredAsCopy(Card Original) : GameComponent;

/// <summary>Animated by this Aura (this object of it); the rest is what it was, to put back.</summary>
public record Animated(
	int AuraId,
	int AuraIncarnation,
	CardType Types,
	string? AddedSubtype,
	int BasePower,
	int BaseToughness
) : GameComponent;

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
			PrintedLoyalty = ParsePowerToughness(front?.Loyalty ?? data.Loyalty),
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
