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
	///
	/// The filter is load-bearing, not decoration. TurnStartedEvent's subject is the player
	/// whose turn began, and without it this fired on BOTH turns — every upkeep trigger in the
	/// engine ran at double the printed rate, silently. Nothing errors and the board looks
	/// right; the card just does twice what it says.
	/// </summary>
	public static TriggerCondition OnYourUpkeep() =>
		new EventTriggerCondition
		{
			EventTypeName = EventTypeNames.TurnStarted,
			Filter = new IsControlledByYouSpecification(),
		};

	/// <summary>
	/// Fires at the start of an OPPONENT's turn — "at the beginning of the upkeep of enchanted
	/// creature's controller" (Stab Wound), read from the enchantment's own controller.
	/// </summary>
	public static TriggerCondition OnOpponentUpkeep() =>
		new EventTriggerCondition
		{
			EventTypeName = EventTypeNames.TurnStarted,
			Filter = new IsControlledByOpponentSpecification(),
		};

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

	/// <summary>
	/// Fires whenever a creature you control dies, optionally only one of a given subtype —
	/// "whenever this or another Human you control dies" (Xathrid Necromancer).
	///
	/// The controller check reads the dead card, which still carries its ControllerId in the
	/// graveyard, so this stays correct after the creature has left the battlefield.
	/// </summary>
	public static TriggerCondition OnCreatureYouControlDies(string subtype = "")
	{
		TargetSpecification filter = new IsControlledByYouSpecification();
		if (!string.IsNullOrEmpty(subtype))
			filter = filter.And(new IsSubtypeSpecification { Subtype = subtype });

		return new EventTriggerCondition
		{
			EventTypeName = EventTypeNames.CreatureDestroyed,
			Filter = filter,
		};
	}

	/// <summary>
	/// Fires whenever a creature an opponent controls dies — "whenever a creature an opponent
	/// controls dies, that player loses 2 life" (Massacre Wurm).
	/// </summary>
	public static TriggerCondition OnOpponentCreatureDies() =>
		new EventTriggerCondition
		{
			EventTypeName = EventTypeNames.CreatureDestroyed,
			Filter = new IsControlledByOpponentSpecification(),
		};

	/// <summary>
	/// Fires whenever a creature an opponent controls enters the battlefield — "whenever another
	/// creature enters under an opponent's control, that player loses 1 life" (Blood Seeker).
	/// </summary>
	public static TriggerCondition OnOpponentCreatureEnters() =>
		new EventTriggerCondition
		{
			EventTypeName = EventTypeNames.CreatureEnteredBattlefield,
			Filter = new IsControlledByOpponentSpecification(),
		};

	/// <summary>
	/// Fires whenever a creature an opponent controls attacks — "whenever a creature attacks
	/// you, its controller loses 1 life" (Blood Reckoning).
	///
	/// With no blocking there is no "attacks you" as distinct from "attacks": an attack is
	/// declared against you or your planeswalker and resolves immediately, so filtering the
	/// attacker to an opponent's creature is the whole clause.
	/// </summary>
	public static TriggerCondition OnCreatureAttacksYou() =>
		new EventTriggerCondition
		{
			EventTypeName = EventTypeNames.CreatureAttacked,
			Filter = new IsControlledByOpponentSpecification(),
		};

	/// <summary>
	/// Fires whenever a card is discarded, by either player. Pair with a filter-carrying
	/// condition or MaxTriggersPerTurn where the card needs narrowing.
	/// </summary>
	public static TriggerCondition OnCardDiscarded() =>
		new EventTriggerCondition { EventTypeName = EventTypeNames.CardDiscarded };

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

	/// <summary>Fires whenever any artifact leaves the battlefield (sacrifice, destruction, exile).</summary>
	public static TriggerCondition OnAnyArtifactDies() =>
		new EventTriggerCondition { EventTypeName = EventTypeNames.ArtifactLeftBattlefield };

	/// <summary>
	/// Fires whenever the controller gains life ("whenever you gain life").
	/// The event subject is the player, and IsControlledByYouSpecification matches an
	/// MtgPlayer by ID, so the filter reads as "you" — same as OnLandfall().
	/// </summary>
	public static TriggerCondition OnGainLife() =>
		new EventTriggerCondition
		{
			EventTypeName = EventTypeNames.PlayerGainedLife,
			Filter = new IsControlledByYouSpecification(),
		};

	/// <summary>
	/// Fires whenever the controller draws a card — Chasm Skulker, Teferi's Tutelage.
	/// The event's subject is the CARD drawn, so IsControlledByYouSpecification reads as
	/// "a card of yours was drawn".
	/// </summary>
	public static TriggerCondition OnYouDraw() =>
		new EventTriggerCondition
		{
			EventTypeName = EventTypeNames.CardDrawn,
			Filter = new IsControlledByYouSpecification(),
		};

	/// <summary>
	/// Fires whenever the controller casts an instant or sorcery — Talrand, prowess payoffs.
	/// </summary>
	public static TriggerCondition OnYouCastSpell() =>
		new EventTriggerCondition
		{
			EventTypeName = EventTypeNames.SpellCast,
			Filter = new IsControlledByYouSpecification(),
		};

	/// <summary>Fires whenever the controller loses life.</summary>
	public static TriggerCondition OnLoseLife() =>
		new EventTriggerCondition
		{
			EventTypeName = EventTypeNames.PlayerLostLife,
			Filter = new IsControlledByYouSpecification(),
		};

	/// <summary>Fires whenever any creature enters the battlefield, either player's.</summary>
	public static TriggerCondition OnAnyCreatureEnters() =>
		new EventTriggerCondition { EventTypeName = EventTypeNames.CreatureEnteredBattlefield };

	/// <summary>
	/// Fires whenever a creature OTHER than this one enters the battlefield, either player's —
	/// "whenever another creature enters" (Soul Warden).
	///
	/// The "another" is not decoration: without IsNotSelfSpecification the card triggers on its
	/// own arrival, which is an extra activation every time it is cast or reanimated.
	/// </summary>
	public static TriggerCondition OnAnotherCreatureEnters() =>
		new EventTriggerCondition
		{
			EventTypeName = EventTypeNames.CreatureEnteredBattlefield,
			Filter = new IsNotSelfSpecification(),
		};

	/// <summary>Fires when a creature its controller owns enters the battlefield.</summary>
	public static TriggerCondition OnCreatureYouControlEnters() =>
		new EventTriggerCondition
		{
			EventTypeName = EventTypeNames.CreatureEnteredBattlefield,
			Filter = new IsControlledByYouSpecification(),
		};

	/// <summary>
	/// Fires when this exact creature deals combat damage to a player — the renown trigger.
	/// CombatDamageDealtToPlayerEvent's subject is the ATTACKER, so IsSourceCardSpecification
	/// correctly means "this creature dealt the damage", not "this creature was damaged".
	/// </summary>
	public static TriggerCondition OnSelfDealsCombatDamageToPlayer() =>
		new EventTriggerCondition
		{
			EventTypeName = EventTypeNames.CombatDamageDealtToPlayer,
			Filter = new IsSourceCardSpecification(),
		};

	/// <summary>
	/// Fires when a creature an opponent controls becomes exhausted — the "tapper payoff"
	/// trigger (Gideon's Avenger). Pass opponentOnly: false to fire on either player's.
	/// </summary>
	public static TriggerCondition OnCreatureExhausted(bool opponentOnly = true) =>
		new EventTriggerCondition
		{
			EventTypeName = EventTypeNames.CreatureExhausted,
			Filter = opponentOnly ? new IsControlledByOpponentSpecification() : null,
		};
}
