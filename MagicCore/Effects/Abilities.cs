using System.Collections.Immutable;
using ImmutableGameObjects;
using MagicCore.Targeting;

namespace MagicCore.Effects;

/// <summary>An instant's or sorcery's text: its target words and its effects, in order.</summary>
public record SpellEffectsComponent(ImmutableList<GameAction> Effects) : GameComponent
{
	public ImmutableList<TargetSpec> Targets { get; init; } = [];

	/// <summary>"Choose one/two —": the bullets. Empty for a non-modal spell.</summary>
	public ImmutableList<Mode> Modes { get; init; } = [];
	public int Choose { get; init; } = 1;
}

public enum TriggerEvent
{
	SelfEnters,
	AnotherCreatureYouControlEnters,
	SelfAttacks,
	YouCastNoncreatureSpell,
	LandYouControlEnters, // landfall
	YouGainLife,
	SelfDies, // read from the dying card as it last existed (CR 603.10a)
	AnotherCreatureOrPlaneswalkerYouControlDies,
	CreatureYouControlDies,
	OpponentDealtNoncombatDamage,
}

/// <summary>"When/Whenever [event], [effects]." Goes on the stack the next time a player would get priority.</summary>
public record TriggeredAbilityComponent(TriggerEvent On, ImmutableList<GameAction> Effects)
	: GameComponent
{
	public ImmutableList<TargetSpec> Targets { get; init; } = [];

	/// <summary>"When this enters, choose one —": chosen as it goes on the stack (CR 700.2b).</summary>
	public ImmutableList<Mode> Modes { get; init; } = [];
	public int Choose { get; init; } = 1;
}

/// <summary>"[Cost]: [effects]." Uses the stack. Mana abilities are <see cref="ManaAbilityComponent"/>, which does not.</summary>
public record ActivatedAbilityComponent(ManaCost Cost, ImmutableList<GameAction> Effects)
	: GameComponent
{
	public bool TapCost { get; init; }
	public ImmutableList<TargetSpec> Targets { get; init; } = [];

	/// <summary>"Sacrifice this [permanent]:" — part of the cost.</summary>
	public bool SacrificeSelf { get; init; }

	/// <summary>"Sacrifice another creature or planeswalker:" — what may be sacrificed as the cost.</summary>
	public TargetSpec? Sacrifice { get; init; }

	/// <summary>"Activate only as a sorcery."</summary>
	public bool SorcerySpeed { get; init; }
}

/// <summary>
/// "[+N]/[−N]/[0]: [effects]" — a planeswalker's loyalty ability (CR 606). Sorcery speed, once per
/// turn per permanent; the cost adds or removes loyalty counters.
/// </summary>
public record LoyaltyAbilityComponent(int Cost, ImmutableList<GameAction> Effects) : GameComponent
{
	public ImmutableList<TargetSpec> Targets { get; init; } = [];
}

/// <summary>The Way cycle and friends: "Planeswalkers you control have [loyalty ability]."</summary>
public record GrantsLoyaltyAbility(LoyaltyAbilityComponent Ability) : GameComponent;

/// <summary>Sanctum Lurker: "Planeswalkers you control aren't put into their owners' graveyards for having 0 loyalty."</summary>
public record YourPlaneswalkersSurviveZeroLoyalty : GameComponent;

/// <summary>"{T}: Add {C}." on a non-land (basic land types produce mana by their subtype, CR 305.6).</summary>
public record ManaAbilityComponent(ManaColor Color) : GameComponent;

public enum EntersUntappedIf
{
	Never,
	YouControlAPlaneswalker,
	YouControlTwoOrMoreOtherLands,
}

/// <summary>"This permanent enters tapped [unless …]." Applied by MagicState.EnterBattlefield.</summary>
public record EntersTappedComponent(EntersUntappedIf Unless = EntersUntappedIf.Never)
	: GameComponent;

/// <summary>Silence the Echo: "As an additional cost to cast this spell, sacrifice a [Sacrifice] or pay {Generic}."</summary>
public record SacrificeOrPayAdditionalCost(TargetSpec Sacrifice, int Generic) : GameComponent;

/// <summary>"This creature enters with N +1/+1 counters on it." Applied by MagicState.EnterBattlefield.</summary>
public record EntersWithCountersComponent(int Count) : GameComponent;

/// <summary>On a card exiled "until [source] leaves the battlefield" — MagicState.MoveCard returns it then.</summary>
public record ExiledUntilLeaves(int SourceId, int SourceIncarnation) : GameComponent;

public enum Affects
{
	Self,
	CreaturesYouControl,
	OtherCreaturesYouControl,
}

public enum StaticCondition
{
	Always,
	DuringYourTurn,
	YouControlAJacePlaneswalker,
}

/// <summary>
/// A static ability that changes characteristics while its source is on the battlefield — "creatures
/// you control have haste", "this gets +1/+0 for each …". Read only by Characteristics.
/// </summary>
public record StaticEffectComponent(Affects Affects, string? Keyword = null) : GameComponent
{
	public Count Power { get; init; } = 0;
	public Count Toughness { get; init; } = 0;
	public StaticCondition While { get; init; }

	/// <summary>
	/// Tetsuko: "creatures you control with power or toughness 1 or less". Only on a keyword static —
	/// it reads real P/T, which a P/T static would then feed back into.
	/// </summary>
	public int? OnlyPowerOrToughnessAtMost { get; init; }
}

/// <summary>Thalia: "Noncreature spells your opponents cast cost {N} more to cast."</summary>
public record OpponentsNoncreatureSpellsCostMore(int Amount) : GameComponent;

/// <summary>Ghalta: "This spell costs {X} less to cast, where X is the greatest power among creatures you control."</summary>
public record CostsLessByGreatestPowerYouControl : GameComponent;

/// <summary>Wrath of the Bloodmane: "This spell costs {N} less to cast if you control a legendary creature."</summary>
public record CostsLessIfYouControlLegendaryCreature(int Amount) : GameComponent;
