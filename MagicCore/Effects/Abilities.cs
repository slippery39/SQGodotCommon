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
	SelfDealsCombatDamageToPlayer,
	YourUpkeep,
	EachUpkeep, // "each player's upkeep"
	OpponentsDrawStep,
	YourEndStep,
	EachEndStep, // "the end step" — every turn's
	OpponentsCreatureWithPowerOrToughness1OrLessBlocks, // Tetsuko, Pursuer
	YouActivateLoyaltyAbility,
	YouActivateLoyaltyAbilityRemovingTwoOrMore,
	BeginningOfCombatOnYourTurn,
	YouPutLoyaltyCounters, // Inspired Tethermage: "whenever you put one or more loyalty counters on a planeswalker"
	Reflexive, // "When you do, …" — put on the stack by WhenYouDo, never matched against an event
	AnotherCreatureOrPlaneswalkerYouControlEnters, // Liliana the Repentant
	YouSurveil, // "whenever you scry or surveil" — FRA has no scry
	CreatureYouControlEnters, // Mabel: "Mabel or another creature you control"
	AttachedCreatureAttacks, // "equipped creature attacks", and "Whenever this creature attacks" granted by Equipment
	AttachedCreatureDies, // "When enchanted creature dies" — looks back: the Aura is already gone (CR 603.10a)
	SelfBecomesTargetOfOpponentsSpellOrAbility, // Ruric Thar, Biomagus
	AnotherNontokenCreatureYouControlEnters, // Arni, Humble Scribe
	YouCastArtifactOrCreatureSpell, // Traxos, Scourge Eternal
	APlayerDiscards, // Tinybones: "whenever a player discards one or more cards"
	SelfDiscarded, // Titanbones: "when you discard this card" — FromGraveyard, where the card now is
	CreatureYouControlAttacksPlayerAlone, // Jiang Yanggu, Yuriko — the only attacker, at a player
	YouCastEquipmentOrSpellTargetingYourCreature, // Danitha, Sword of Hope
	YouCastSpellTargetingOpponentOrTheirCreature, // Danitha, Spear of Agony
	YouCastFirstNoncreatureSpellEachTurn, // Plan for All Outcomes
	YouAttack, // Kiora of Salt and Sand: "whenever you attack" — once per declaration
	OpponentsDealtCombatDamageDuringYourTurn, // Fblthp, Impossibly Lost — "one or more", once a step
	CreatureYouControlAttacks, // Ingris Stingerquill — the event card is the attacker
	YouCastPreparedSpell, // Codie — the copy of a prepare spell, cast from exile
	OpponentCastsSpellWithManaValue2OrLess, // Solarium Sentry
	CreatureOpponentControlsEnters, // Gideon the Oathless
	OpponentActivatesLoyaltyAbility, // Gideon the Oathless
	YouCastCreatureSpell, // Flickering Hound
	SelfDealtDamage, // Hexhaven Invigorator — X is how much
	YourFirstMainPhase, // Gardenize: "at the beginning of your first main phase"
}

/// <summary>"When/Whenever [event], [effects]." Goes on the stack the next time a player would get priority.</summary>
public record TriggeredAbilityComponent(TriggerEvent On, ImmutableList<GameAction> Effects)
	: GameComponent
{
	public ImmutableList<TargetSpec> Targets { get; init; } = [];

	/// <summary>"When this enters, choose one —": chosen as it goes on the stack (CR 700.2b).</summary>
	public ImmutableList<Mode> Modes { get; init; } = [];
	public int Choose { get; init; } = 1;

	/// <summary>"At …, if [condition], …" — intervening: checked as it triggers AND as it resolves (CR 603.4).</summary>
	public Condition? If { get; init; }

	/// <summary>Darklight Phoenix: "return this card from your graveyard" — it triggers from there, not the battlefield.</summary>
	public bool FromGraveyard { get; init; }

	/// <summary>"This ability triggers only once each turn." Stamped on the card as it goes on the stack.</summary>
	public bool OncePerTurn { get; init; }

	/// <summary>
	/// Koth: "Whenever a Plains you control enters" — the event's card must have this subtype. Read for
	/// triggers from the battlefield or a graveyard (not "when this dies"), on an event that has a card.
	/// </summary>
	public string? EventSubtype { get; init; }
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

	/// <summary>Where it is activated from: the battlefield, or a card in your hand (cycling) or graveyard.</summary>
	public ZoneType From { get; init; } = ZoneType.Battlefield;

	/// <summary>"Exile this card from your graveyard:" — part of the cost.</summary>
	public bool ExileSelf { get; init; }

	/// <summary>Cycling: "Discard this card:" — part of the cost.</summary>
	public bool DiscardSelf { get; init; }

	/// <summary>Hallway Heckler: "Discard a card:" — the card is chosen like a sacrifice.</summary>
	public bool DiscardACard { get; init; }

	/// <summary>Tenured Tethermage: "Tap two untapped artifacts you control:" — how many, of what type.</summary>
	public int TapUntapped { get; init; }

	public Cards.CardType TapUntappedType { get; init; }

	/// <summary>Solitary Cell: "Discard a legendary card:" — what the discarded card must be.</summary>
	public Cards.Supertype DiscardMustBe { get; init; }

	/// <summary>Whether this card in hand may pay its discard cost.</summary>
	public bool MayDiscard(Cards.Card card, int sourceId) =>
		card.Id != sourceId && (DiscardMustBe == Cards.Supertype.None || card.Is(DiscardMustBe));

	/// <summary>
	/// Gallia, Tragic Host: "Exile another creature card from your graveyard:" — what may be exiled.
	/// A CardInYourGraveyard spec, so the source itself never qualifies: lifted from MtgCore's
	/// ExileFromGraveyardAdditionalCost, where a card exiling ITSELF paid for its own recursion.
	/// </summary>
	public TargetSpec? ExileFromGraveyard { get; init; }

	/// <summary>Warrior's Blades' equip: "{1} less for each +1/+1 counter on the creature it targets".</summary>
	public bool CostsLessPerCounterOnTarget { get; init; }

	/// <summary>"Activate only if [condition]" — judged as it is activated.</summary>
	public Condition? OnlyIf { get; init; }

	/// <summary>
	/// "Exhaust — … (Activate each exhaust ability only once.)" — stamped on the card as it is
	/// activated. ponytail: one stamp per card, as no FRA card has two exhaust abilities.
	/// Not MtgCore's "exhaust", which is its tapping.
	/// </summary>
	public bool Exhaust { get; init; }
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

/// <summary>What a permanent's mana may pay for.</summary>
public enum ManaRestriction
{
	None,
	NotForSpellsFromHand, // Heartwood Crafter
	OnlyPlaneswalkerSpells, // Gideon's Memorial
}

/// <summary>"{T}: Add {C}." on a non-land (basic land types produce mana by their subtype, CR 305.6).</summary>
public record ManaAbilityComponent(ManaColor Color) : GameComponent
{
	/// <summary>"This mana can't be spent to cast spells from your hand" / "Spend this mana only to cast a planeswalker spell".</summary>
	public ManaRestriction Restriction { get; init; }
}

/// <summary>
/// Loot, the Nexus: "{T}: Choose a color. Add one mana of that color for each [Amount]." Counted as
/// it is tapped, with the permanent as its source. Treasure and Lotus: "{T}, Sacrifice this: Add
/// [Amount] mana of any one color" (SacrificeSelf).
/// </summary>
public record ManaOfOneColorComponent(Count Amount) : GameComponent
{
	public bool SacrificeSelf { get; init; }
}

/// <summary>Draconic Visitor: "If one or more artifact tokens would be created under your control, that many [Token] are created instead."</summary>
public record ArtifactTokensBecome(Cards.CardData Token, ImmutableArray<GameComponent> Abilities)
	: GameComponent;

public enum EntersUntappedIf
{
	Never,
	YouControlAPlaneswalker,
	YouControlTwoOrMoreOtherLands,
	YouBeholdAJace, // Theorist's Sanctum: a Jace you control or one in your hand (beholding costs nothing, so always)
}

/// <summary>"This permanent enters tapped [unless …]." Applied by MagicState.EnterBattlefield.</summary>
public record EntersTappedComponent(EntersUntappedIf Unless = EntersUntappedIf.Never)
	: GameComponent;

/// <summary>Silence the Echo: "As an additional cost to cast this spell, sacrifice a [Sacrifice] or pay {Generic}."</summary>
public record SacrificeOrPayAdditionalCost(TargetSpec Sacrifice, int Generic) : GameComponent;

/// <summary>"This creature enters with N (or X) +1/+1 counters on it." Applied by MagicState.EnterBattlefield.</summary>
public record EntersWithCountersComponent(Count Count) : GameComponent;

/// <summary>"As long as [condition], this creature can attack as though it didn't have defender." (Surveillance Phantasm)</summary>
public record AttacksDespiteDefender(Condition While) : GameComponent;

/// <summary>On a card exiled "until [source] leaves the battlefield" — MagicState.MoveCard returns it then.</summary>
public record ExiledUntilLeaves(int SourceId, int SourceIncarnation) : GameComponent;

public enum Affects
{
	Self,
	CreaturesYouControl,
	OtherCreaturesYouControl,
	LandsYouControl, // Marwyn: "Lands you control have hexproof."
	AttachedCreature, // "Equipped/enchanted creature gets …": the permanent this is attached to
}

public enum StaticCondition
{
	Always,
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
	/// "As long as [condition]" — threshold, "an opponent was dealt noncombat damage this turn". Never
	/// one that reads power or toughness: Characteristics would recurse into itself.
	/// </summary>
	public Condition? If { get; init; }

	/// <summary>
	/// Tetsuko: "creatures you control with power or toughness 1 or less". Only on a keyword static —
	/// it reads real P/T, which a P/T static would then feed back into.
	/// </summary>
	public int? OnlyPowerOrToughnessAtMost { get; init; }

	/// <summary>Gallia: "each other creature you control with a +1/+1 counter on it".</summary>
	public bool OnlyWithCounter { get; init; }

	/// <summary>Saheeli: "Thopters you control have haste".</summary>
	public string? OnlySubtype { get; init; }

	/// <summary>Gideon's Memorial: "creature tokens you control".</summary>
	public bool OnlyTokens { get; init; }

	/// <summary>Vraska, Soul of Stone: "artifact creatures you control".</summary>
	public Cards.CardType OnlyTypes { get; init; }
}

/// <summary>Thalia: "Noncreature spells your opponents cast cost {N} more to cast."</summary>
public record OpponentsNoncreatureSpellsCostMore(int Amount) : GameComponent;

/// <summary>
/// Geist of Saint Thalia: "Noncreature spells you cast cost {N} less to cast." On a permanent, read
/// for the card being cast — MtgCore's ConditionalCostReductionComponent.AppliesTo case.
/// </summary>
public record YourNoncreatureSpellsCostLess(int Amount) : GameComponent;

/// <summary>
/// Traxos: "doesn't untap during your untap step" — read by the untap step ONLY; an untap effect
/// still untaps it. (MtgCore's freeze blocks every untap: Dungeon Geists' wording, not this one.)
/// </summary>
public record DoesntUntapInYourUntapStep : GameComponent;

/// <summary>
/// "This spell costs {X} less to cast, where X is the greatest power among creatures you control" —
/// or toughness (Ghalta the Immovable).
/// </summary>
public record CostsLessByGreatestStatYouControl(bool Toughness = false) : GameComponent;

/// <summary>Wrath of the Bloodmane: "This spell costs {N} less to cast if you control a legendary creature."</summary>
public record CostsLessIfYouControlLegendaryCreature(int Amount) : GameComponent;

/// <summary>
/// "Flashback [cost] (You may cast this card from your graveyard for its flashback cost. Then exile
/// it.)" Twinned Vision's flashback also costs "Discard a card".
/// </summary>
public record FlashbackComponent(ManaCost Cost) : GameComponent
{
	public bool DiscardACard { get; init; }
}

/// <summary>Impulse draw: this exiled card may be played by PlayerId through turn UntilTurn (Rules/Playable.cs).</summary>
public record PlayableFromExile(int PlayerId, int UntilTurn) : GameComponent;

/// <summary>Room of Refuge: "As it enters, choose a color." Applied by MagicState.EnterBattlefield.</summary>
public record ChoosesColorAsItEnters : GameComponent;

/// <summary>"{T}: Add one mana of the chosen color." — set as it entered; gone when it leaves.</summary>
public record ChosenColorMana(ManaColor Color) : GameComponent;

/// <summary>Stingcaster Mage: "gains flashback until end of turn. The flashback cost is equal to its mana cost." Lost on any zone change.</summary>
public record GrantedFlashback(int OnTurn) : GameComponent;

/// <summary>A prepare card's spell half (CR: "a preparation card is a creature card in every zone"): its face and its text.</summary>
public record PrepareSpellComponent(Cards.CardFace Face, SpellEffectsComponent Text)
	: GameComponent;

/// <summary>"This creature enters prepared." Applied by MagicState.EnterBattlefield.</summary>
public record EntersPreparedComponent : GameComponent;

/// <summary>On a prepared copy in exile: the creature (and which object of it) it belongs to.</summary>
public record PreparedCopyOf(int CreatureId, int Incarnation) : GameComponent;

/// <summary>
/// "Enchant [Enchant]" — an Aura spell targets what it will enchant as it is CAST, so it can't be
/// cast with nothing to enchant (MtgCore learned this from a Pacifism that resolved onto an empty
/// board and sat inert). It attaches as it resolves (Rules/Attachments.cs).
/// </summary>
public record AuraComponent(TargetSpec Enchant) : GameComponent;

/// <summary>
/// Proft: "You can't cast this spell unless [When]." A restriction, not a cost — read by
/// Playable.CanCastFromHere, so the card is never offered (MtgCore's CastRestrictionComponent).
/// </summary>
public record CastOnlyIf(Condition When) : GameComponent;

/// <summary>
/// Countersculpt: "As an additional cost, behold a [Subtype] or pay {Generic}" — choose one you
/// control or reveal one from your hand (not the spell itself). Nothing moves: beholding is showing.
/// </summary>
public record BeholdOrPayAdditionalCost(string Subtype, int Generic) : GameComponent
{
	public IEnumerable<int> Candidates(GameState s, int playerId, int spellId) =>
		s.CardsIn(playerId, ZoneType.Battlefield)
			.Concat(s.CardsIn(playerId, ZoneType.Hand).Where(c => c.Id != spellId))
			.Where(c => c.Subtypes.Contains(Subtype))
			.Select(c => c.Id);
}

public enum ReplaceableEvent
{
	NoncombatDamageToOpponent, // Tomik: "a source you control … to an opponent or a permanent an opponent controls"
	PlusOneCountersOnYourCreature, // Yoshimaru: "+1/+1 counters would be put on a creature you control"
}

/// <summary>"… that much plus Bonus instead" — on a permanent; Rules.Replacements reads it where the event happens.</summary>
public record AmountReplacement(ReplaceableEvent Event, int Bonus) : GameComponent;

/// <summary>Loot, the Anomaly: "If his power is negative, he assigns combat damage as though it were positive."</summary>
public record AssignsDamageAsThoughPowerPositive : GameComponent;

/// <summary>Ghalta: "Each creature you control with toughness greater than its power assigns combat damage equal to its toughness."</summary>
public record YourCreaturesAssignDamageByToughness : GameComponent;

/// <summary>Ghalta: "Creatures you control can attack as though they didn't have defender."</summary>
public record YourCreaturesAttackDespiteDefender : GameComponent;

/// <summary>Yuriko: "During combat, players can't cast spells or activate abilities that aren't mana abilities."</summary>
public record NoSpellsOrAbilitiesDuringCombat : GameComponent;

/// <summary>Samut: "Instant and sorcery spells you control have split second."</summary>
public record YourInstantsAndSorceriesHaveSplitSecond : GameComponent;

/// <summary>Tomik, Orzhov Lawmage: "Planeswalkers you control have 'No more than one creature can attack this planeswalker each combat.'"</summary>
public record OneAttackerPerPlaneswalkerYouControl : GameComponent;

/// <summary>Infinite Coursework: "Enchanted creature loses all abilities" — Characteristics.HasLostAllAbilities.</summary>
public record EnchantedLosesAllAbilities : GameComponent;

/// <summary>Infinite Coursework: "… and doesn't untap during its controller's untap step" — the Aura's, not the creature's.</summary>
public record EnchantedDoesntUntap : GameComponent;

/// <summary>"This spell can't be countered." Read by StackResolution.Counter.</summary>
public record CantBeCountered : GameComponent;

/// <summary>Traxos: "This spell costs {N} less to cast if you've cast a noncreature spell this turn."</summary>
public record CostsLessIfYouCastNoncreatureSpellThisTurn(int Amount) : GameComponent;

/// <summary>
/// A delayed triggered ability (CR 603.7) kept on the permanent it acts on — Vindictive Triumph's
/// "exile it at the beginning of the next end step". Not the permanent's ability: losing all
/// abilities keeps it, a copy doesn't take it, and it is gone with a zone change (Card.IsStatus).
/// ponytail: one-shot only because its effect moves the permanent; mark it fired if one ever doesn't.
/// </summary>
public record DelayedTrigger(TriggeredAbilityComponent Ability) : GameComponent;

/// <summary>Karn, Argent Defender: "Artifacts and creatures entering the battlefield don't cause abilities to trigger." Read by Triggers.Collect.</summary>
public record EnteringArtifactsAndCreaturesDontTrigger : GameComponent;

/// <summary>
/// Omnipresence: "You may cast spells with mana value less than or equal to the number of creatures
/// you control from your hand without paying their mana costs." (CastSpellAction.WithoutPayingManaCost)
/// </summary>
public record CastFromHandFreeUpToCreatureCount : GameComponent;

/// <summary>
/// Null Summoner: "As long as [While], you may cast the exiled card, and mana of any type can be
/// spent to cast that spell." On the source — a static, so only while it is on the battlefield;
/// the card it exiled is <see cref="ExiledBy"/> it (Rules/Playable.cs).
/// </summary>
public record MayCastWhatItExiled(Condition While) : GameComponent;

/// <summary>On a card a permanent exiled for a linked ability (CR 607.2a) — which object of it.</summary>
public record ExiledBy(int SourceId, int SourceIncarnation) : GameComponent;
