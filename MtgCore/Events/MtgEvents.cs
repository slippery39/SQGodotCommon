using ImmutableGameObjects;

namespace MtgCore;

public record PlayerDamagedEvent : GameEvent
{
	public int PlayerId { get; init; }
	public int Amount { get; init; }
}

public record CreatureDamagedEvent : GameEvent
{
	public int CreatureId { get; init; }
	public int Amount { get; init; }
}

public record CreatureDestroyedEvent : GameEvent
{
	public int CreatureId { get; init; }
}

public record CreatureAttackedEvent : GameEvent
{
	public int CreatureId { get; init; }
	public int AttackingPlayerId { get; init; }

	/// <summary>
	/// What was attacked — a player, a planeswalker, or a creature. The event carried only the
	/// attacker, so "whenever a creature attacks THIS" was unexpressible: Wall of Frost had to
	/// freeze every creature an opponent controlled whenever anything attacked at all.
	/// </summary>
	public int TargetId { get; init; }
}

/// <summary>
/// A spell was countered by a hand trap. The countered card has already moved to wherever the
/// trap sent it (graveyard, exile or hand) by the time this fires.
/// </summary>
public record SpellCounteredEvent : GameEvent
{
	public int CardId { get; init; }
	public int TrapCardId { get; init; }
	public int CastingPlayerId { get; init; }
}

/// <summary>
/// A creature became exhausted (this engine's equivalent of being tapped by a "tapper").
/// Emitted by ExhaustCreatureAction and by activating a RequiresTap ability.
/// Gideon's Avenger triggers on this.
/// </summary>
public record CreatureExhaustedEvent : GameEvent
{
	public int CreatureId { get; init; }
}

public record SpellCastEvent : GameEvent
{
	public int CardId { get; init; }
	public int CastingPlayerId { get; init; }
}

public record PlayerGainedLifeEvent : GameEvent
{
	public int PlayerId { get; init; }
	public int Amount { get; init; }
}

public record SpellResolvedEvent : GameEvent
{
	public int CardId { get; init; }
}

public record CardDrawnEvent : GameEvent
{
	public int PlayerId { get; init; }
	public int CardId { get; init; }
}

public record LibraryEmptyEvent : GameEvent
{
	public int PlayerId { get; init; }
}

public record CardRevealedEvent : GameEvent
{
	public int PlayerId { get; init; }
	public int CardId { get; init; }
	public int ManaCost { get; init; }
}

public record PlayerLostLifeEvent : GameEvent
{
	public int PlayerId { get; init; }
	public int Amount { get; init; }
}

public record CardDiscardedEvent : GameEvent
{
	public int PlayerId { get; init; }
	public int CardId { get; init; }
}

/// <summary>
/// A card moved from a library to its owner's graveyard. Distinct from CardDiscardedEvent
/// so "whenever you discard" payoffs do not fire on self-mill.
/// </summary>
public record CardMilledEvent : GameEvent
{
	public int PlayerId { get; init; }
	public int CardId { get; init; }
}

/// <summary>
/// A card entered a graveyard, from any zone and by any means. PermanentLeftBattlefieldEvent
/// carries no destination, so it cannot distinguish death from exile — this event can.
/// Used by StaticAbilityEngine to activate graveyard-active static abilities.
/// </summary>
public record CardEnteredGraveyardEvent : GameEvent
{
	public int CardId { get; init; }
	public int OwnerId { get; init; }
}

/// <summary>
/// A card left a graveyard (cast from it, reanimated, exiled, or shuffled away).
/// Deactivates graveyard-active static abilities.
/// </summary>
public record CardLeftGraveyardEvent : GameEvent
{
	public int CardId { get; init; }
	public int OwnerId { get; init; }
}

public record CreaturePlayedEvent : GameEvent
{
	public int CardId { get; init; }
	public int PlayerId { get; init; }
}

public record PermanentPlayedEvent : GameEvent
{
	public int CardId { get; init; }
	public int PlayerId { get; init; }
}

/// <summary>
/// Emitted when a player's loss condition is triggered (life <= 0 or empty library).
/// </summary>
public record PlayerLostEvent : GameEvent
{
	public int PlayerId { get; init; }
	public string Reason { get; init; } = "";
}

/// <summary>
/// Emitted when the game ends. Contains the winning player ID, or -1 for a draw.
/// </summary>
public record GameOverEvent : GameEvent
{
	/// <summary>
	/// The winning player's ID, or -1 if the game ended in a draw.
	/// </summary>
	public int WinnerPlayerId { get; init; }
}

public record CreatureModifiedEvent : GameEvent
{
	public int CreatureId { get; init; }
	public int PowerBonus { get; init; }
	public int ToughnessBonus { get; init; }
}

/// <summary>
/// One or more +1/+1 counters were PUT ON a creature. Emitted by AddCountersAction only when the
/// net change is positive, so removing a counter cannot feed a counter payoff.
///
/// Deliberately NOT CreatureModifiedEvent, which fires for Giant Growth and every other P/T
/// change. Reusing that would make Wildwood Scourge grow off any combat trick — the "confidently
/// wrong" failure mode, which is worse than no trigger at all.
///
/// A creature ENTERING with counters emits nothing; see EntersWithCountersComponent.
/// </summary>
public record CountersAddedEvent : GameEvent
{
	public int CardId { get; init; }
	public int PlayerId { get; init; }
	public int Amount { get; init; }
}

public record TurnStartedEvent : GameEvent
{
	public int PlayerId { get; init; }
}

public record TurnEndedEvent : GameEvent
{
	public int PlayerId { get; init; }
}

public record CardExiledEvent : GameEvent
{
	public int CardId { get; init; }
	public int PlayerId { get; init; }
}

public record CreatureEnteredBattlefieldEvent : GameEvent
{
	public int CardId { get; init; }
	public int PlayerId { get; init; }
}

/// <summary>
/// Emitted when any permanent leaves the battlefield (death, exile, sacrifice).
/// Consumed by CheckStateBasedEffectsAction to clean up applied static ability components.
/// Separate from CreatureDestroyedEvent — this fires for exile and sacrifice too.
/// </summary>
public record PermanentLeftBattlefieldEvent : GameEvent
{
	public int CardId { get; init; }
	public int OwnerId { get; init; }
}

/// <summary>
/// Emitted when an artifact permanent leaves the battlefield (sacrifice, destruction, exile).
/// Fired in addition to PermanentLeftBattlefieldEvent when the leaving permanent HasSubtype("Artifact").
/// Used by Disciple of the Vault and similar drain triggers.
/// </summary>
public record ArtifactLeftBattlefieldEvent : GameEvent
{
	public int CardId { get; init; }
	public int OwnerId { get; init; }
}

/// <summary>
/// Emitted when a creature deals combat damage directly to a player.
/// Fires once per strike — twice for double-strike creatures.
/// AttackerId is the subject so IsSourceCardSpecification filters can match it.
/// </summary>
public record CombatDamageDealtToPlayerEvent : GameEvent
{
	public int AttackerId { get; init; }
	public int DefendingPlayerId { get; init; }
	public int Amount { get; init; }
}

/// <summary>
/// Emitted when a non-creature permanent enters the battlefield via ResolvePermanentAction.
/// Used by StaticAbilityEngine and triggered abilities on enchantments and artifacts.
/// Creature permanents continue to emit CreatureEnteredBattlefieldEvent instead.
/// </summary>
public record PermanentEnteredBattlefieldEvent : GameEvent
{
	public int CardId { get; init; }
	public int PlayerId { get; init; }
}

/// <summary>
/// Emitted when any land enters play — whether played from hand (PlayLandAction) or
/// put into play by an effect (PutLandIntoPlayAction, Rampant Growth, Primeval Titan ETB).
/// Subject for EventTriggerCondition is PlayerId, so IsControlledByYouSpecification
/// filters to "your lands only" for Landfall triggers.
/// </summary>
public record LandPlayedEvent : GameEvent
{
	public int PlayerId { get; init; }
	public int CardId { get; init; }
}
