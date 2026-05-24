namespace MtgCore.Cards.Builders;

/// <summary>
/// Static helpers for common trigger conditions. Prefer these over constructing
/// EventTriggerCondition inline — they read like card text and centralise the
/// EventTypeNames / filter patterns.
/// </summary>
public static class TriggerConditions
{
	/// <summary>
	/// Fires when this exact card enters the battlefield ("when ~ enters the battlefield").
	/// Uses IsSourceCardSpecification so it only triggers for the card that owns the ability.
	/// </summary>
	public static TriggerCondition OnSelfEntersBattlefield() =>
		new EventTriggerCondition
		{
			EventTypeName = EventTypeNames.CreatureEnteredBattlefield,
			Filter = new IsSourceCardSpecification(),
		};

	/// <summary>
	/// Fires when this exact non-creature permanent enters the battlefield (artifacts, enchantments).
	/// Uses PermanentEnteredBattlefield and IsSourceCardSpecification so it only triggers
	/// for the card that owns the ability. Use OnSelfEntersBattlefield() for creatures.
	/// </summary>
	public static TriggerCondition OnSelfEntersBattlefieldAsNonCreature() =>
		new EventTriggerCondition
		{
			EventTypeName = EventTypeNames.PermanentEnteredBattlefield,
			Filter = new IsSourceCardSpecification(),
		};

	/// <summary>
	/// Fires at the start of the controller's turn ("at the beginning of your upkeep").
	/// </summary>
	public static TriggerCondition OnYourUpkeep() =>
		new EventTriggerCondition { EventTypeName = EventTypeNames.TurnStarted };

	/// <summary>Fires when this exact creature dies.</summary>
	public static TriggerCondition OnSelfDies() =>
		new EventTriggerCondition
		{
			EventTypeName = EventTypeNames.CreatureDestroyed,
			Filter = new IsSourceCardSpecification(),
		};

	/// <summary>Fires whenever any creature dies.</summary>
	public static TriggerCondition OnAnyCreatureDies() =>
		new EventTriggerCondition { EventTypeName = EventTypeNames.CreatureDestroyed };

	/// <summary>Fires whenever any creature attacks.</summary>
	public static TriggerCondition OnAnyCreatureAttacks() =>
		new EventTriggerCondition { EventTypeName = EventTypeNames.CreatureAttacked };

	/// <summary>Fires whenever this card attacks (HasAttacked event for self).</summary>
	public static TriggerCondition OnSelfAttacks() =>
		new EventTriggerCondition
		{
			EventTypeName = EventTypeNames.CreatureAttacked,
			Filter = new IsSourceCardSpecification(),
		};

	/// <summary>
	/// Fires whenever the controller plays a land ("landfall").
	/// Filter checks that the land was played by the card's controlling player.
	/// </summary>
	public static TriggerCondition OnLandfall() =>
		new EventTriggerCondition
		{
			EventTypeName = EventTypeNames.LandPlayed,
			Filter = new IsControlledByYouSpecification(),
		};
}
