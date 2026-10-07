using System.Collections.Immutable;
using ImmutableGameObjects;
using MagicCore.Targeting;

namespace MagicCore.Effects;

/// <summary>An instant's or sorcery's text: its target words and its effects, in order.</summary>
public record SpellEffectsComponent(ImmutableList<GameAction> Effects) : GameComponent
{
	public ImmutableList<TargetSpec> Targets { get; init; } = [];
}

public enum TriggerEvent
{
	SelfEnters,
	AnotherCreatureYouControlEnters,
	SelfAttacks,
	YouCastNoncreatureSpell,
	LandYouControlEnters, // landfall
}

/// <summary>"When/Whenever [event], [effects]." Goes on the stack the next time a player would get priority.</summary>
public record TriggeredAbilityComponent(TriggerEvent On, ImmutableList<GameAction> Effects)
	: GameComponent
{
	public ImmutableList<TargetSpec> Targets { get; init; } = [];
}

/// <summary>"[Cost]: [effects]." Uses the stack. Mana abilities are <see cref="ManaAbilityComponent"/>, which does not.</summary>
public record ActivatedAbilityComponent(ManaCost Cost, ImmutableList<GameAction> Effects)
	: GameComponent
{
	public bool TapCost { get; init; }
	public ImmutableList<TargetSpec> Targets { get; init; } = [];
}

/// <summary>"{T}: Add {C}." on a non-land (basic land types produce mana by their subtype, CR 305.6).</summary>
public record ManaAbilityComponent(ManaColor Color) : GameComponent;

/// <summary>Thalia: "Noncreature spells your opponents cast cost {N} more to cast."</summary>
public record OpponentsNoncreatureSpellsCostMore(int Amount) : GameComponent;

/// <summary>Wrath of the Bloodmane: "This spell costs {N} less to cast if you control a legendary creature."</summary>
public record CostsLessIfYouControlLegendaryCreature(int Amount) : GameComponent;
