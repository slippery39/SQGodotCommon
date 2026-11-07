using MagicCore.Cards;
using MagicCore.Targeting;

namespace MagicCore.Filters;

/// <summary>
/// The filter vocabulary card scripts read: <c>Is.Creature &amp; Is.Yours &amp; !Is.Source</c> is "another
/// creature you control". A new card needing a new PROPERTY adds a leaf here; one needing a new
/// combination writes it with &amp; | ! in its script.
/// </summary>
public static class Is
{
	public static readonly Filter Creature = Type(CardType.Creature);
	public static readonly Filter Planeswalker = Type(CardType.Planeswalker);
	public static readonly Filter Land = Type(CardType.Land);
	public static readonly Filter Artifact = Type(CardType.Artifact);
	public static readonly Filter Enchantment = Type(CardType.Enchantment);
	public static readonly Filter Instant = Type(CardType.Instant);
	public static readonly Filter Sorcery = Type(CardType.Sorcery);
	public static readonly Filter Legendary = Supertype(Cards.Supertype.Legendary);

	/// <summary>"You control" — a card's controller, or you yourself.</summary>
	public static readonly Filter Yours = new OfController(Whose.You);

	/// <summary>"An opponent controls" — or an opponent.</summary>
	public static readonly Filter Opponents = new OfController(Whose.Opponent);
	public static readonly Filter Player = new HasProperty(Property.Player);

	/// <summary>"This" — so "another" is !Is.Source.</summary>
	public static readonly Filter Source = new HasProperty(Property.Source);

	/// <summary>"That creature" — the card the trigger event was about.</summary>
	public static readonly Filter EventCard = new HasProperty(Property.EventCard);

	/// <summary>"Equipped creature", "enchanted creature".</summary>
	public static readonly Filter AttachedToSource = new HasProperty(Property.AttachedToSource);
	public static readonly Filter Token = new HasProperty(Property.Token);
	public static readonly Filter Copy = new HasProperty(Property.Copy);
	public static readonly Filter Prepared = new HasProperty(Property.Prepared);
	public static readonly Filter PreparedCopy = new HasProperty(Property.PreparedCopy);
	public static readonly Filter WithPlusOneCounter = new HasProperty(Property.PlusOneCounter);
	public static readonly Filter WithAnyCounter = new HasProperty(Property.AnyCounter);
	public static readonly Filter EnteredThisTurn = new HasProperty(Property.EnteredThisTurn);
	public static readonly Filter AttackedThisTurn = new HasProperty(Property.AttackedThisTurn);
	public static readonly Filter Attacking = new HasProperty(Property.Attacking);

	/// <summary>U.S.S. Excelsior: "target tapped creature".</summary>
	public static readonly Filter Tapped = new HasProperty(Property.Tapped);

	/// <summary>Riku of Many Paths: "Whenever you cast a modal spell".</summary>
	public static readonly Filter Modal = new HasProperty(Property.Modal);
	public static readonly Filter AttackingAPlayer = new HasProperty(Property.AttackingAPlayer);

	/// <summary>"Attacks you or a planeswalker you control".</summary>
	public static readonly Filter AttackingYou = new HasProperty(Property.AttackingYou);
	public static readonly Filter Blocking = new HasProperty(Property.Blocking);

	/// <summary>Ruric Thar: "haven't dealt combat damage yet" is !Is.DealtCombatDamage.</summary>
	public static readonly Filter DealtCombatDamage = new HasProperty(Property.DealtCombatDamage);

	/// <summary>Gideon's Memorial: "attacking or blocking creature". Built from leaves, not fields: no init order.</summary>
	public static readonly Filter InCombat =
		new HasProperty(Property.Attacking) | new HasProperty(Property.Blocking);

	/// <summary>None matches nothing — the old vocabularies' "None = any" is NO filter, so it throws.</summary>
	public static Filter Type(CardType anyOf) =>
		anyOf == CardType.None
			? throw new ArgumentException("CardType.None: write no filter for \"any card\"")
			: new OfType(anyOf);

	public static Filter Supertype(Supertype anyOf) =>
		anyOf == Cards.Supertype.None
			? throw new ArgumentException("Supertype.None: write no filter for \"any card\"")
			: new OfSupertype(anyOf);

	public static Filter Subtype(string subtype) => new OfSubtype(subtype);

	public static Filter Named(string name) => new Named(name);

	public static readonly Filter ManaValueIsChosenNumber = new ManaValueIsChosenNumber();
	public static readonly Filter NamedChosenName = new NamedChosenName();

	/// <summary>Combadge: "with a Communicator attached to it".</summary>
	public static Filter WithAttached(Filter attachment) => new WithAttached(attachment);

	public static Filter Color(ManaColor color) => new OfColor(color);

	/// <summary>Consign to Memory: "target triggered ability" — with TargetSpec.Ability.</summary>
	public static readonly Filter TriggeredAbility = new AbilityKind(Triggered: true);

	/// <summary>"Target activated ability" — loyalty abilities are activated too.</summary>
	public static readonly Filter ActivatedAbility = new AbilityKind(Triggered: false);

	/// <summary>Eye of Ugin: "colorless" — a card with no colour at all.</summary>
	public static readonly Filter Colorless = new Colorless();

	/// <summary>"With flying" — granted keywords count.</summary>
	public static Filter With(string keyword) => new HasKeyword(keyword);

	public static Filter PowerAtLeast(Effects.Count n) =>
		new StatBetween(Stat.Power, n, int.MaxValue);

	public static Filter PowerAtMost(Effects.Count n) =>
		new StatBetween(Stat.Power, int.MinValue, n);

	public static Filter ToughnessAtLeast(Effects.Count n) =>
		new StatBetween(Stat.Toughness, n, int.MaxValue);

	public static Filter ToughnessAtMost(Effects.Count n) =>
		new StatBetween(Stat.Toughness, int.MinValue, n);

	public static Filter ManaValueAtLeast(Effects.Count n) =>
		new StatBetween(Stat.ManaValue, n, int.MaxValue);

	/// <summary>Omnipresence: "mana value less than or equal to the number of creatures you control" — a Count.</summary>
	public static Filter ManaValueAtMost(Effects.Count n) =>
		new StatBetween(Stat.ManaValue, int.MinValue, n);
}
