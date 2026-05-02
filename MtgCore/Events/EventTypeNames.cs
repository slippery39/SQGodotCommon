namespace MtgCore;

/// <summary>
/// String constants for GameEvent type names used in EventTriggerCondition.
/// Using constants prevents typos and makes refactoring easier — if an event
/// is renamed, only this file needs updating.
///
/// Add a new constant here whenever a new GameEvent is created that cards
/// might want to trigger on.
/// </summary>
public static class EventTypeNames
{
	public const string CreatureDestroyed = nameof(CreatureDestroyedEvent);
	public const string CreaturePlayed = nameof(CreaturePlayedEvent);
	public const string CreatureAttacked = nameof(CreatureAttackedEvent);
	public const string CreatureDamaged = nameof(CreatureDamagedEvent);
	public const string PlayerDamaged = nameof(PlayerDamagedEvent);
	public const string PlayerGainedLife = nameof(PlayerGainedLifeEvent);
	public const string CardDrawn = nameof(CardDrawnEvent);
	public const string CardDiscarded = nameof(CardDiscardedEvent);
	public const string CardRevealed = nameof(CardRevealedEvent);
	public const string SpellCast = nameof(SpellCastEvent);
	public const string TurnStarted = nameof(TurnStartedEvent);
	public const string TurnEnded = nameof(TurnEndedEvent);
	public const string LibraryEmpty = nameof(LibraryEmptyEvent);
	public const string CardExiled = nameof(CardExiledEvent);
	public const string CreatureEnteredBattlefield = nameof(CreatureEnteredBattlefieldEvent);
	public const string CombatDamageDealtToPlayer = nameof(CombatDamageDealtToPlayerEvent);
	public const string PermanentEnteredBattlefield = nameof(PermanentEnteredBattlefieldEvent);
}
