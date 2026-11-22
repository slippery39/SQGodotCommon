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
/// <param name="SourceIncarnation">The ability's source AS IT WAS (ContextKeys.SourceSnapshot), for "the exiled card"
/// asked once the source has left; none where nothing resolves (a static, a cost): the source as it is now.</param>
public readonly record struct FilterContext(
	int Controller,
	int Source = 0,
	int EventCard = 0,
	int? SourceIncarnation = null
);

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

/// <summary>
/// Nyota Uhura: "Whenever an opponent casts a spell with mana value equal to the chosen number" — the
/// number the source chose as it entered (ChosenNumber); X counts on the stack. Gollum, Riddle Master: "a spell with mana
/// value of the chosen quality" — odd or even (ChosenNumber.OddOrEven), the number its parity.
/// </summary>
public sealed record ManaValueIsChosenNumber : Filter
{
	public override bool ReadsPowerOrToughness() => false;

	public override bool ReadsKeywords() => false;

	public override bool Matches(GameState s, GameObject o, FilterContext c) =>
		o is Card card
		&& s.HasObject(c.Source)
		&& s.GetObject(c.Source) is Card source
		&& source.GetComponent<Effects.ChosenNumber>() is { } chosen
		&& (chosen.OddOrEven ? card.ManaValueNow % 2 : card.ManaValueNow) == chosen.N;
}

/// <summary>Kai Winn: "a spell with the chosen name", "a source with the chosen name" — the name the source chose as it entered (ChosenName).</summary>
public sealed record NamedChosenName : Filter
{
	public override bool ReadsPowerOrToughness() => false;

	public override bool ReadsKeywords() => false;

	public override bool Matches(GameState s, GameObject o, FilterContext c) =>
		o is Card card
		&& s.HasObject(c.Source)
		&& s.GetObject(c.Source) is Card source
		&& source.GetComponent<Effects.ChosenName>() is { } chosen
		&& card.Name == chosen.Name;
}

/// <summary>True-Name Nemesis: "protection from the chosen player" — an object that player controls (ChosenPlayer on the source).</summary>
public sealed record ControlledByChosenPlayer : Filter
{
	public override bool ReadsPowerOrToughness() => false;

	public override bool ReadsKeywords() => false;

	public override bool Matches(GameState s, GameObject o, FilterContext c) =>
		o is Card card
		&& s.HasObject(c.Source)
		&& s.GetObject(c.Source) is Card source
		&& source.GetComponent<Effects.ChosenPlayer>() is { } chosen
		&& card.ControllerId == chosen.PlayerId;
}

/// <summary>Koth: "a Plains"; Saheeli: "Thopters".</summary>
public sealed record OfSubtype(string Subtype) : Filter
{
	public override bool ReadsPowerOrToughness() => false;

	public override bool ReadsKeywords() => false;

	public override bool Matches(GameState s, GameObject o, FilterContext c) =>
		o is Card card && s.HasSubtype(card, Subtype);
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
		o is Card card && s.ColorsOf(card).Contains(Color);
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
		o is Card card && s.ColorsOf(card).Length == 0;
}

/// <summary>
/// Urza's Saga: "an artifact card with mana cost {0} or {1}" — the printed cost itself, never the mana value:
/// Walking Ballista's {X}{X} and a card with no mana cost have mana value 0 and neither cost; {W} is no {1}.
/// </summary>
public sealed record ManaCostIs(string Cost) : Filter
{
	public override bool ReadsPowerOrToughness() => false;

	public override bool ReadsKeywords() => false;

	public override bool Matches(GameState s, GameObject o, FilterContext c) =>
		o is Card card && card.ManaCost.ToString() == Cost;
}

/// <summary>Konstrari Charm: "creature with flying" — through s.Has, so a granted keyword counts.</summary>
public sealed record HasKeyword(string Keyword) : Filter
{
	public override bool ReadsPowerOrToughness() => false;

	public override bool ReadsKeywords() => true;

	public override bool Matches(GameState s, GameObject o, FilterContext c) =>
		o is Card card && s.Has(card, Keyword);
}

/// <summary>"With a +1/+1 counter on it"; Dauthi Voidwalker: "with a void counter on it" — Card.CountersOf.</summary>
public sealed record HasCounter(string Kind) : Filter
{
	public override bool ReadsPowerOrToughness() => false;

	public override bool ReadsKeywords() => false;

	public override bool Matches(GameState s, GameObject o, FilterContext c) =>
		o is Card card && card.CountersOf(Kind) > 0;
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
	PowerPlusToughness, // Cut Down: "total power and toughness"
}

/// <summary>
/// Tetsuko: "power or toughness 1 or less"; Your Fate Ends Here: "mana value 3 or greater". Real
/// P/T, through s.Power / s.Toughness — a noncreature has 0, so add Is.Creature where the text says it.
/// A bound may be counted (an int converts): Omnipresence's "less than or equal to the number of
/// creatures you control", Sisay's "less than Sisay's power" — counted as it is asked, with the filter's
/// controller and source (X is the source's: Thieving Skydiver).
/// </summary>
public sealed record StatBetween(Stat Stat, Effects.Count Min, Effects.Count Max) : Filter
{
	// A counted bound may read power (Sisay's): never in a P/T static's scope.
	public override bool ReadsPowerOrToughness() =>
		Stat != Stat.ManaValue
		|| Min.Kind != Effects.CountOf.Fixed
		|| Max.Kind != Effects.CountOf.Fixed;

	public override bool ReadsKeywords() => false;

	public override bool Matches(GameState s, GameObject o, FilterContext c)
	{
		if (o is not Card card)
			return false;
		var n = Stat switch
		{
			Stat.Power => s.Power(card),
			Stat.Toughness => s.Toughness(card),
			Stat.PowerPlusToughness => s.Power(card) + s.Toughness(card),
			_ => card.ManaValueNow,
		};
		return n >= Bound(s, Min, c) && n <= Bound(s, Max, c);
	}

	static int Bound(GameState s, Effects.Count bound, FilterContext c) =>
		bound.Value(
			s,
			bound.Kind == Effects.CountOf.Fixed
				? System.Collections.Immutable.ImmutableDictionary<string, object>.Empty
				: System
					.Collections.Immutable.ImmutableDictionary<string, object>.Empty.Add(
						Effects.ContextKeys.Controller,
						c.Controller
					)
					.Add(Effects.ContextKeys.Source, c.Source)
					// Thieving Skydiver: "target artifact with mana value X or less" — the source's X, on the stack
					// or as it was cast (CR 107.3m)
					.Add(
						Effects.ContextKeys.X,
						bound.Kind == Effects.CountOf.X ? XOf(s, c.Source) : 0
					)
		);

	/// <summary>
	/// The source's X: on the stack its OnStackComponent.X, on the battlefield the X it was cast for (CR 107.3m).
	/// ponytail: a SPELL's own target filter is judged in the hand as it is cast, where X reads 0, and an "{X}, {T}:"
	/// ability's filter reads the permanent's cast X, not the X paid — thread the action's X through FilterContext
	/// when such a card is scripted (DesignNotes).
	/// </summary>
	static int XOf(GameState s, int sourceId) =>
		sourceId != 0 && s.HasObject(sourceId) && s.GetObject(sourceId) is Cards.Card source
			? source.GetComponent<Actions.OnStackComponent>()?.X ?? source.CastX
			: 0;
}

/// <summary>The yes/no properties HasProperty reads — one per property, never per combination.</summary>
public enum Property
{
	Player,
	Source, // "this", so !Is.Source is "another"
	EventCard, // "that creature"; !Is.EventCard is Roiling Canopy's "other"
	AttachedToSource, // "equipped/enchanted creature"
	Attached, // Lion Sash's reconfigure: "while attached" — this Aura or Equipment is on a permanent
	Token,
	Copy,
	Prepared, // the creature is prepared (it has a copy of its spell in exile)
	PreparedCopy, // Codie: the copy of a prepare spell, cast from exile
	AnyCounter, // Dominion Supervisor: "with counters on them" — any kind (Card.HasAnyCounter)
	EnteredThisTurn,
	AttackedThisTurn,
	Attacking,
	AttackingAPlayer, // Garruk, Curse Breaker: not a planeswalker
	AttackingYou, // Jace, Reality Sculptor: "attacks you or a planeswalker you control"
	Blocking,
	DealtCombatDamage, // Ruric Thar: "as long as they haven't dealt combat damage yet"
	Tapped, // U.S.S. Excelsior: "target tapped creature"
	Modal, // Riku of Many Paths: "a modal spell" — its text has modes ("choose one —")
	ExiledBySource, // Null Summoner: "the exiled card" — exiled by THIS object of the source (ExiledBy, CR 607.2a)
	Spell, // MACO Strike Team: "a creature you control" is never a creature SPELL — a card on the stack
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
			Property.Attached => Rules.Attachments.HostOf(s, card) is not null,
			Property.Token => card.IsToken,
			Property.Copy => card.IsCopy,
			Property.Prepared => card.IsPrepared,
			Property.PreparedCopy => card.HasComponent<Effects.PreparedCopyOf>(),
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
			Property.Modal => card.GetComponent<Effects.SpellEffectsComponent>()
				is { Modes.IsEmpty: false },
			// Skyclave Apparition's "the exiled card", asked as it leaves: the object the ability came from.
			Property.ExiledBySource => card.GetComponent<Effects.ExiledBy>() is { } link
				&& link.SourceId == c.Source
				&& (
					c.SourceIncarnation is { } was
						? link.SourceIncarnation == was
						: s.HasObject(c.Source)
							&& s.GetObject(c.Source) is Card source
							&& source.Incarnation == link.SourceIncarnation
				),
			Property.Spell => s.IsOnStack(card.Id),
			_ => false,
		};
}
