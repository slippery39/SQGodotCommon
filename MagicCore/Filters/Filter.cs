using ImmutableGameObjects;
using MagicCore.Cards;
using MagicCore.Targeting;

namespace MagicCore.Filters;

// "Which objects": one composable predicate, lifted from MtgCore's TargetSpecification and repaired
// (DesignAudit.md §10). Records, never delegates (Serialization Rule). Card scripts read it through
// Is: `Is.Creature & Is.Yours & !Is.Source`.
//
// A leaf is a PROPERTY OF ONE OBJECT. Leaves grow with properties, never with combinations.
// No zone in a filter — whoever supplies the candidates owns the zone. No targeting rules either:
// hexproof and "a spell can't target itself" stay in TargetSpec.

/// <summary>
/// Not targeting — "sacrifice a creature", "exile another creature card from your graveyard": the
/// chooser's own zone, filtered (no filter is any card there). Legality is membership, so the cost
/// and its candidates can't disagree.
/// </summary>
public static class FilterChoices
{
	public static IEnumerable<int> Among(
		this Filter? filter,
		GameState s,
		int playerId,
		ZoneType zone,
		int sourceId
	) =>
		s.CardsIn(playerId, zone)
			.Where(c => filter?.Matches(s, c, new(playerId, sourceId)) ?? true)
			.Select(c => c.Id);
}

/// <summary>Who is asking: "you", "this", "the event's card". 0 where there is none.</summary>
public readonly record struct FilterContext(int Controller, int Source = 0, int EventCard = 0);

/// <summary>
/// "Creature you control", "other nontoken creature", "card with mana value 3 or less" — asked about
/// an OBJECT, which may be a last-known snapshot (a creature as it died), never an id looked up again.
/// </summary>
public abstract record Filter
{
	public abstract bool Matches(GameState s, GameObject o, FilterContext c);

	/// <summary>
	/// Reads power or toughness (s.Power / s.Toughness). A P/T static's scope must not: Characteristics
	/// would ask itself forever — StaticEffectComponent throws as it is built. Abstract, so a new leaf
	/// must say: a forgotten answer would let the guard pass a loop.
	/// </summary>
	public abstract bool ReadsPowerOrToughness();

	/// <summary>Reads keywords (s.Has). No static's scope may: the keyword pass would ask itself.</summary>
	public abstract bool ReadsKeywords();

	public static Filter operator &(Filter a, Filter b) => new AllOf(a, b);

	public static Filter operator |(Filter a, Filter b) => new AnyOf(a, b);

	public static Filter operator !(Filter a) => new NotFilter(a);
}

/// <summary>"Nontoken creature": both.</summary>
public sealed record AllOf(Filter A, Filter B) : Filter
{
	public override bool ReadsPowerOrToughness() =>
		A.ReadsPowerOrToughness() || B.ReadsPowerOrToughness();

	public override bool ReadsKeywords() => A.ReadsKeywords() || B.ReadsKeywords();

	public override bool Matches(GameState s, GameObject o, FilterContext c) =>
		A.Matches(s, o, c) && B.Matches(s, o, c);
}

/// <summary>"Creature or planeswalker": either.</summary>
public sealed record AnyOf(Filter A, Filter B) : Filter
{
	public override bool ReadsPowerOrToughness() =>
		A.ReadsPowerOrToughness() || B.ReadsPowerOrToughness();

	public override bool ReadsKeywords() => A.ReadsKeywords() || B.ReadsKeywords();

	public override bool Matches(GameState s, GameObject o, FilterContext c) =>
		A.Matches(s, o, c) || B.Matches(s, o, c);
}

/// <summary>"Noncreature", "another": not.</summary>
public sealed record NotFilter(Filter Inner) : Filter
{
	public override bool ReadsPowerOrToughness() => Inner.ReadsPowerOrToughness();

	public override bool ReadsKeywords() => Inner.ReadsKeywords();

	public override bool Matches(GameState s, GameObject o, FilterContext c) =>
		!Inner.Matches(s, o, c);
}

/// <summary>Vraska: "artifact creatures"; "artifact or enchantment" — a card of ANY of these types.</summary>
public sealed record OfType(CardType Types) : Filter
{
	public override bool ReadsPowerOrToughness() => false;

	public override bool ReadsKeywords() => false;

	public override bool Matches(GameState s, GameObject o, FilterContext c) =>
		o is Card card && card.Is(Types);
}

/// <summary>Fateshaper Aspirant: "target legendary card" — any of these supertypes.</summary>
public sealed record OfSupertype(Supertype Supertypes) : Filter
{
	public override bool ReadsPowerOrToughness() => false;

	public override bool ReadsKeywords() => false;

	public override bool Matches(GameState s, GameObject o, FilterContext c) =>
		o is Card card && card.Is(Supertypes);
}

/// <summary>Koth: "a Plains"; Saheeli: "Thopters".</summary>
public sealed record OfSubtype(string Subtype) : Filter
{
	public override bool ReadsPowerOrToughness() => false;

	public override bool ReadsKeywords() => false;

	public override bool Matches(GameState s, GameObject o, FilterContext c) =>
		o is Card card && s.Subtypes(card).Contains(Subtype);
}

/// <summary>
/// Combadge: "another creature with a Communicator attached to it" — a permanent with an Aura or
/// Equipment on it that the inner filter matches.
/// </summary>
public sealed record WithAttached(Filter Attachment) : Filter
{
	public override bool ReadsPowerOrToughness() => Attachment.ReadsPowerOrToughness();

	public override bool ReadsKeywords() => Attachment.ReadsKeywords();

	public override bool Matches(GameState s, GameObject o, FilterContext c) =>
		o is Card card && Rules.Attachments.On(s, card).Any(a => Attachment.Matches(s, a, c));
}

/// <summary>
/// A Good Day to Die: "When that creature dies this turn" — one object, this incarnation of it (CR
/// 400.7), bound as the spell resolves (WheneverThisTurn.About); never written in a script.
/// </summary>
public sealed record TheObject(int Id, int Incarnation) : Filter
{
	public override bool ReadsPowerOrToughness() => false;

	public override bool ReadsKeywords() => false;

	public override bool Matches(GameState s, GameObject o, FilterContext c) =>
		o is Card card && card.Id == Id && card.Incarnation == Incarnation;
}

/// <summary>Andromedan Automatons: "any number of cards named Andromedan Automatons".</summary>
public sealed record Named(string Name) : Filter
{
	public override bool ReadsPowerOrToughness() => false;

	public override bool ReadsKeywords() => false;

	public override bool Matches(GameState s, GameObject o, FilterContext c) =>
		o is Card card && card.Name == Name;
}

/// <summary>Terminal Criticism: "that's blue or red" — one colour each, joined with |.</summary>
public sealed record OfColor(ManaColor Color) : Filter
{
	public override bool ReadsPowerOrToughness() => false;

	public override bool ReadsKeywords() => false;

	public override bool Matches(GameState s, GameObject o, FilterContext c) =>
		o is Card card && card.Colors.Contains(Color);
}

/// <summary>Consign to Memory: "target triggered ability" — an ability on the stack, triggered or activated.</summary>
public sealed record AbilityKind(bool Triggered) : Filter
{
	public override bool ReadsPowerOrToughness() => false;

	public override bool ReadsKeywords() => false;

	public override bool Matches(GameState s, GameObject o, FilterContext c) =>
		o is Actions.StackAbility a && a.Triggered == Triggered;
}

/// <summary>Eye of Ugin: "colorless Eldrazi spells", "a colorless creature card" — a card with no colour; never a player.</summary>
public sealed record Colorless : Filter
{
	public override bool ReadsPowerOrToughness() => false;

	public override bool ReadsKeywords() => false;

	public override bool Matches(GameState s, GameObject o, FilterContext c) =>
		o is Card { Colors.Length: 0 };
}

/// <summary>Konstrari Charm: "creature with flying" — through s.Has, so a granted keyword counts.</summary>
public sealed record HasKeyword(string Keyword) : Filter
{
	public override bool ReadsPowerOrToughness() => false;

	public override bool ReadsKeywords() => true;

	public override bool Matches(GameState s, GameObject o, FilterContext c) =>
		o is Card card && s.Has(card, Keyword);
}

/// <summary>Mabel: "creature you control"; Gideon the Oathless: "an opponent controls" — or the player itself.</summary>
public sealed record OfController(Whose Whose) : Filter
{
	public override bool ReadsPowerOrToughness() => false;

	public override bool ReadsKeywords() => false;

	public override bool Matches(GameState s, GameObject o, FilterContext c)
	{
		var id = o switch
		{
			Card card => card.ControllerId,
			MagicPlayer p => p.Id,
			_ => 0,
		};
		return Whose.Includes(id, c.Controller);
	}
}

/// <summary>The number StatBetween reads.</summary>
public enum Stat
{
	Power,
	Toughness,
	ManaValue, // on the stack X counts (CR 202.3e)
}

/// <summary>
/// Tetsuko: "power or toughness 1 or less"; Your Fate Ends Here: "mana value 3 or greater". Real
/// P/T, through s.Power / s.Toughness — a noncreature has 0, so add Is.Creature where the text says it.
/// </summary>
public sealed record StatBetween(Stat Stat, int Min, int Max) : Filter
{
	public override bool ReadsPowerOrToughness() => Stat != Stat.ManaValue;

	public override bool ReadsKeywords() => false;

	public override bool Matches(GameState s, GameObject o, FilterContext c) =>
		o is Card card
		&& (
			Stat switch
			{
				Stat.Power => s.Power(card),
				Stat.Toughness => s.Toughness(card),
				_ => card.ManaValueNow,
			}
		)
			is var n
		&& n >= Min
		&& n <= Max;
}

/// <summary>The yes/no properties HasProperty reads — one per property, never per combination.</summary>
public enum Property
{
	Player,
	Source, // "this", so !Is.Source is "another"
	EventCard, // "that creature"; !Is.EventCard is Roiling Canopy's "other"
	AttachedToSource, // "equipped/enchanted creature"
	Token,
	Copy,
	Prepared, // the creature is prepared (it has a copy of its spell in exile)
	PreparedCopy, // Codie: the copy of a prepare spell, cast from exile
	PlusOneCounter, // "with a +1/+1 counter on it"
	AnyCounter, // Dominion Supervisor: "with counters on them" — any kind (Card.HasAnyCounter)
	EnteredThisTurn,
	AttackedThisTurn,
	Attacking,
	AttackingAPlayer, // Garruk, Curse Breaker: not a planeswalker
	AttackingYou, // Jace, Reality Sculptor: "attacks you or a planeswalker you control"
	Blocking,
	DealtCombatDamage, // Ruric Thar: "as long as they haven't dealt combat damage yet"
	Tapped, // U.S.S. Excelsior: "target tapped creature"
}

/// <summary>Gallia: "each other creature you control with a +1/+1 counter on it" — a yes/no property of one object.</summary>
public sealed record HasProperty(Property Property) : Filter
{
	public override bool ReadsPowerOrToughness() => false;

	public override bool ReadsKeywords() => false;

	public override bool Matches(GameState s, GameObject o, FilterContext c) =>
		Property switch
		{
			Property.Player => o is MagicPlayer,
			Property.Source => o.Id == c.Source,
			Property.EventCard => o.Id == c.EventCard,
			_ => o is Card card && Of(s, card, c),
		};

	bool Of(GameState s, Card card, FilterContext c) =>
		Property switch
		{
			Property.AttachedToSource => s.HasObject(c.Source)
				&& s.GetObject(c.Source) is Card source
				&& Rules.Attachments.IsOn(source, card),
			Property.Token => card.IsToken,
			Property.Copy => card.IsCopy,
			Property.Prepared => card.IsPrepared,
			Property.PreparedCopy => card.HasComponent<Effects.PreparedCopyOf>(),
			Property.PlusOneCounter => card.PlusOneCounters > 0,
			Property.AnyCounter => card.HasAnyCounter,
			Property.EnteredThisTurn => card.EnteredOnTurn == s.GetGame().TurnNumber,
			Property.AttackedThisTurn => card.AttackedOnTurn == s.GetGame().TurnNumber,
			Property.Attacking => card.AttackingId != 0,
			Property.AttackingAPlayer => card.AttackingId != 0
				&& s.HasObject(card.AttackingId) // a planeswalker token it attacked may be gone
				&& s.GetObject(card.AttackingId) is MagicPlayer,
			Property.AttackingYou => card.AttackingId is var at
				&& at != 0
				&& (
					at == c.Controller
					|| (s.IsOnBattlefield(at) && s.GetCard(at).ControllerId == c.Controller)
				),
			Property.Blocking => card.BlockingId != 0,
			Property.DealtCombatDamage => card.HasDealtCombatDamage,
			Property.Tapped => card.IsTapped,
			_ => false,
		};
}
