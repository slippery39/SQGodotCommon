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

	/// <summary>Sublime Epiphany: "Choose one or more —" is ChooseAtLeast 1, Choose every mode. Unset: exactly Choose.</summary>
	public int? ChooseAtLeast { get; init; }
}

/// <summary>
/// "Replicate {N} (When you cast this spell, copy it for each time you paid its replicate cost. You
/// may choose new targets for the copies.)" — CastSpellAction.Replicate. ponytail: a generic cost
/// only (Consign to Memory's {1}); a coloured one would add symbols, not generic.
/// </summary>
public record ReplicateComponent(int Generic) : GameComponent;

/// <summary>
/// A Saga (CR 714) — Amok Time: "(As this Saga enters and after your draw step, add a lore counter.
/// Sacrifice after III.)" The lore counters: MagicState.EnterBattlefield and the precombat main
/// phase's turn-based action (TurnStructure); the sacrifice: the state-based check. Its chapters are
/// triggered abilities (Scripting.Chapter).
/// </summary>
public record SagaComponent(int Chapters) : GameComponent;

/// <summary>
/// Kicker (CR 702.33): Highly Illogical: "Kicker {1}{U} (You may pay an additional {1}{U} as you cast
/// this spell.)" — CastSpellAction.Kicked; "if this spell was kicked" is the condition WasKicked, a
/// target word that changes when kicked is TargetSpec.IfKicked.
/// </summary>
public record KickerComponent(ManaCost Mana) : GameComponent
{
	/// <summary>Eject the Warp Core: "Kicker—Sacrifice an artifact or creature." — paid only when kicked.</summary>
	public ImmutableList<AdditionalCost> Costs { get; init; } = [];
}

/// <summary>The named counter kinds (Card.Counters) scripts and rules share.</summary>
public static class CounterKinds
{
	public const string Lore = "lore";
	public const string Quest = "quest";
	public const string Promotion = "promotion";

	// The older kinds keep their own fields on Card (rules read them everywhere); MagicState.PutCounters
	// is still the one way any of them is put on.
	public const string PlusOne = "+1/+1";
	public const string MinusOne = "-1/-1";
	public const string Charge = "charge";
	public const string Stun = "stun";
	public const string Time = "time";
	public const string Loyalty = "loyalty";
}

/// <summary>What happened, for triggers — one value per event record. Grows only with a new kind of event.</summary>
public enum EventKind
{
	Entered,
	Attacked,
	AttackersDeclared,
	CombatDamageToPlayers,
	SpellCast,
	Died,
	PlayerDealtDamage,
	CreatureDealtDamage,
	StepBegan,
	Blocks,
	Surveilled,
	Discarded,
	LoyaltyCountersPut,
	BecameTarget,
	LoyaltyActivated,
	LifeGained,
	BecameTapped, // Hemmer: "whenever Hemmer becomes tapped"
	FacedDilemma, // "whenever you face a dilemma" — modes chosen for a spell or ability
	CountersPut, // a Saga's chapter: "as the number of lore counters becomes at least N" (Trigger.Chapter)
	LeftGraveyard, // Vash: "whenever a card leaves your graveyard" — MagicState.MoveCard, every path
	Reflexive, // "When you do": put on the stack by WhenYouDo, never raised
}

/// <summary>
/// Where a trigger is read from. Almost all: the battlefield, matched against each event. Three look
/// back (CR 603.10a) and are read in Triggers.Collect from the card itself instead.
/// </summary>
public enum ReadFrom
{
	Battlefield,
	TheDyingCard, // "when this dies" — the dying card as it last existed; Subject asked of it
	ItsAttachment, // "when enchanted creature dies" — read from the Aura as it was; Subject not asked (it has left)
	TheSpellCast, // Emrakul: "when you cast this spell" — the spell on the stack; Subject asked of it
}

/// <summary>The few event tests that are not about the subject or who did it — keep this list short.</summary>
public enum EventTest
{
	None,
	RemovingTwoOrMore, // Way of the Mind Sculptor: a loyalty ability that removed two or more
	Alone, // Jiang Yanggu, Yuriko: the only creature its controller has attacking
	EquipmentOrTargetsYourCreature, // Danitha, Sword of Hope: an Equipment spell, or a spell targeting your creature
	TargetsAnOpponentOrTheirCreature, // Danitha, Spear of Agony
	FirstNoncreatureSpell, // Plan for All Outcomes: numbered as it is cast
	CombatDamageFromThis, // "whenever this deals combat damage to a player"
	Noncombat, // "whenever an opponent is dealt noncombat damage"
}

/// <summary>
/// "When/Whenever [something happens]" — the kind of event, then which of its subjects (Subject:
/// "a creature you control"), who did it (By: "you cast"), at which step, and a rare EventTest.
/// Gideon the Oathless: new(EventKind.Entered) { Subject = Is.Creature &amp; Is.Opponents }.
/// </summary>
public record Trigger(EventKind On)
{
	// Combinations that could never fire are refused as the trigger is built — a card that compiles
	// and never triggers is the silent no-op this engine keeps finding the hard way.
	readonly Step? step;
	readonly EventTest also;

	/// <summary>Koth: "a Plains you control" — the event's subject (a dead creature as it last was). None: any.</summary>
	public Filters.Filter? Subject { get; init; }

	/// <summary>Who did it: "you cast", "an opponent activates"; for a step, whose turn it is.</summary>
	public Whose By { get; init; }

	/// <summary>"At the beginning of [step]" — only for EventKind.StepBegan.</summary>
	public Step? Step
	{
		get => step;
		init =>
			step =
				value is null || On == EventKind.StepBegan
					? value
					: throw new ArgumentException($"A Step only fits StepBegan, not {On}");
	}

	/// <summary>Jiang Yanggu: "attacks alone" — a test on the event beyond its subject and who did it; each fits one kind.</summary>
	public EventTest Also
	{
		get => also;
		init =>
			also = Fits(On, value)
				? value
				: throw new ArgumentException($"EventTest {value} can't fire on {On}");
	}

	/// <summary>"When this dies", "when you cast this": read from the card itself (Triggers.Collect), never matched on the battlefield.</summary>
	public ReadFrom ReadFrom { get; init; }

	/// <summary>
	/// Power 8: "whenever you attack WHILE creatures you control have total power 8 or greater" — part
	/// of the trigger condition, judged only as the event happens; unlike an intervening If, never
	/// again as it resolves (release notes).
	/// </summary>
	public Condition? While { get; init; }

	/// <summary>
	/// Blastoneuron: "Whenever one or more cards leave your graveyard" — at most one trigger per batch:
	/// the events staged since a player last had priority (Shayne, 2026-11-01). ponytail: one resolution
	/// moving cards in two separate steps triggers once, where the rules trigger twice.
	/// </summary>
	public bool OneOrMore { get; init; }

	readonly int chapter;

	/// <summary>
	/// A Saga's chapter N (CR 714.2b): it triggers as the number of lore counters on this becomes at
	/// least N — written with Scripting.Chapter.
	/// </summary>
	public int Chapter
	{
		get => chapter;
		init =>
			chapter =
				value == 0 || On == EventKind.CountersPut
					? value
					: throw new ArgumentException($"A Chapter only fits CountersPut, not {On}");
	}

	readonly Filters.Filter? combatDamageBy;

	/// <summary>
	/// Phlox, Unorthodox Doctor: "Whenever a creature you control with a counter on it deals combat
	/// damage to a player" — combat damage from a source this matches.
	/// </summary>
	public Filters.Filter? CombatDamageBy
	{
		get => combatDamageBy;
		init =>
			combatDamageBy =
				value is null || On == EventKind.PlayerDealtDamage
					? value
					: throw new ArgumentException(
						$"CombatDamageBy only fits PlayerDealtDamage, not {On}"
					);
	}

	static bool Fits(EventKind on, EventTest test) =>
		test switch
		{
			EventTest.None => true,
			EventTest.RemovingTwoOrMore => on == EventKind.LoyaltyActivated,
			EventTest.Alone => on == EventKind.Attacked,
			EventTest.CombatDamageFromThis or EventTest.Noncombat => on
				== EventKind.PlayerDealtDamage,
			_ => on == EventKind.SpellCast,
		};
}

/// <summary>The triggers that read like their text, by name — a one-off writes its own Trigger.</summary>
public static class TriggerEvent
{
	static readonly Filters.Filter AnotherOfYours = Filters.Is.Yours & !Filters.Is.Source;

	public static readonly Trigger SelfEnters =
		new(EventKind.Entered) { Subject = Filters.Is.Source };
	public static readonly Trigger AnotherCreatureYouControlEnters =
		new(EventKind.Entered) { Subject = Filters.Is.Creature & AnotherOfYours };

	/// <summary>Liliana the Repentant.</summary>
	public static readonly Trigger AnotherCreatureOrPlaneswalkerYouControlEnters =
		new(EventKind.Entered)
		{
			Subject = (Filters.Is.Creature | Filters.Is.Planeswalker) & AnotherOfYours,
		};

	/// <summary>Landfall.</summary>
	public static readonly Trigger LandYouControlEnters =
		new(EventKind.Entered) { Subject = Filters.Is.Land & Filters.Is.Yours };

	/// <summary>Mabel: "Mabel or another creature you control".</summary>
	public static readonly Trigger CreatureYouControlEnters =
		new(EventKind.Entered) { Subject = Filters.Is.Creature & Filters.Is.Yours };
	public static readonly Trigger SelfAttacks =
		new(EventKind.Attacked) { Subject = Filters.Is.Source };

	/// <summary>"Equipped creature attacks", and "whenever this creature attacks" granted by Equipment.</summary>
	public static readonly Trigger AttachedCreatureAttacks =
		new(EventKind.Attacked) { Subject = Filters.Is.AttachedToSource };

	/// <summary>Ingris Stingerquill — the event card is the attacker.</summary>
	public static readonly Trigger CreatureYouControlAttacks =
		new(EventKind.Attacked) { Subject = Filters.Is.Creature & Filters.Is.Yours };

	/// <summary>Jiang Yanggu, Yuriko — the only attacker, at a player.</summary>
	public static readonly Trigger CreatureYouControlAttacksPlayerAlone =
		new(EventKind.Attacked)
		{
			Subject = Filters.Is.Yours & Filters.Is.AttackingAPlayer,
			Also = EventTest.Alone,
		};

	/// <summary>Kiora of Salt and Sand: "whenever you attack" — once per declaration.</summary>
	public static readonly Trigger YouAttack = new(EventKind.AttackersDeclared) { By = Whose.You };

	/// <summary>Power 8: "Whenever you attack while creatures you control have total power 8 or greater" — judged as attackers are declared.</summary>
	public static readonly Trigger YouAttackWithTotalPower8 = YouAttack with
	{
		While = new AtLeast(Count.TotalPowerOf(Ref.CreaturesYouControl), 8),
	};

	/// <summary>Hemmer, Shuttle Ace: "Whenever this creature becomes tapped" — MagicState.Tap, every tap path.</summary>
	public static readonly Trigger SelfBecomesTapped =
		new(EventKind.BecameTapped) { Subject = Filters.Is.Source };

	/// <summary>Vash, Hologram Stalker: "Whenever a card leaves your graveyard" — to any zone; a token isn't a card.</summary>
	public static readonly Trigger CardLeavesYourGraveyard =
		new(EventKind.LeftGraveyard) { By = Whose.You };

	/// <summary>Blastoneuron: "Whenever one or more cards leave your graveyard" — once per batch (Trigger.OneOrMore).</summary>
	public static readonly Trigger OneOrMoreCardsLeaveYourGraveyard = CardLeavesYourGraveyard with
	{
		OneOrMore = true,
	};

	/// <summary>Face a dilemma: "Whenever you face a dilemma" — as you choose one or more modes for a spell or ability.</summary>
	public static readonly Trigger YouFaceADilemma = new(EventKind.FacedDilemma) { By = Whose.You };

	/// <summary>Garruk, Curse Breaker's −4: "one or more creatures attack one of your opponents".</summary>
	public static readonly Trigger CreaturesAttackYourOpponent =
		new(EventKind.AttackersDeclared) { By = Whose.You, Subject = Filters.Is.AttackingAPlayer };

	/// <summary>Read from the dying card as it last existed (CR 603.10a).</summary>
	public static readonly Trigger SelfDies =
		new(EventKind.Died) { Subject = Filters.Is.Source, ReadFrom = ReadFrom.TheDyingCard };

	/// <summary>"When enchanted creature dies" — looks back: the Aura is already gone (CR 603.10a).</summary>
	public static readonly Trigger AttachedCreatureDies =
		new(EventKind.Died)
		{
			Subject = Filters.Is.AttachedToSource,
			ReadFrom = ReadFrom.ItsAttachment,
		};
	public static readonly Trigger AnotherCreatureOrPlaneswalkerYouControlDies =
		new(EventKind.Died)
		{
			Subject = (Filters.Is.Creature | Filters.Is.Planeswalker) & AnotherOfYours,
		};
	public static readonly Trigger CreatureYouControlDies =
		new(EventKind.Died) { Subject = Filters.Is.Creature & Filters.Is.Yours };

	/// <summary>Chandra's emblems: "whenever you cast a spell".</summary>
	public static readonly Trigger YouCastSpell = new(EventKind.SpellCast) { By = Whose.You };
	public static readonly Trigger YouCastNoncreatureSpell = YouCastSpell with
	{
		Subject = !Filters.Is.Creature,
	};

	/// <summary>Flickering Hound.</summary>
	public static readonly Trigger YouCastCreatureSpell = YouCastSpell with
	{
		Subject = Filters.Is.Creature,
	};

	/// <summary>Plan for All Outcomes.</summary>
	public static readonly Trigger YouCastFirstNoncreatureSpellEachTurn = YouCastSpell with
	{
		Also = EventTest.FirstNoncreatureSpell,
	};

	/// <summary>Danitha, Sword of Hope.</summary>
	public static readonly Trigger YouCastEquipmentOrSpellTargetingYourCreature = YouCastSpell with
	{
		Also = EventTest.EquipmentOrTargetsYourCreature,
	};

	/// <summary>Danitha, Spear of Agony.</summary>
	public static readonly Trigger YouCastSpellTargetingOpponentOrTheirCreature = YouCastSpell with
	{
		Also = EventTest.TargetsAnOpponentOrTheirCreature,
	};

	/// <summary>Emrakul: "When you cast this spell" — read from the spell on the stack.</summary>
	public static readonly Trigger SelfCast =
		new(EventKind.SpellCast) { Subject = Filters.Is.Source, ReadFrom = ReadFrom.TheSpellCast };
	public static readonly Trigger YouGainLife = new(EventKind.LifeGained) { By = Whose.You };

	/// <summary>"Whenever you scry or surveil" — FRA has no scry.</summary>
	public static readonly Trigger YouSurveil = new(EventKind.Surveilled) { By = Whose.You };

	/// <summary>Inspired Tethermage: "whenever you put one or more loyalty counters on a planeswalker".</summary>
	public static readonly Trigger YouPutLoyaltyCounters =
		new(EventKind.LoyaltyCountersPut) { By = Whose.You };
	public static readonly Trigger YouActivateLoyaltyAbility =
		new(EventKind.LoyaltyActivated) { By = Whose.You };
	public static readonly Trigger YouActivateLoyaltyAbilityRemovingTwoOrMore =
		YouActivateLoyaltyAbility with
		{
			Also = EventTest.RemovingTwoOrMore,
		};

	/// <summary>Gideon the Oathless.</summary>
	public static readonly Trigger OpponentActivatesLoyaltyAbility =
		new(EventKind.LoyaltyActivated) { By = Whose.Opponent };

	/// <summary>Tinybones: "whenever a player discards one or more cards".</summary>
	public static readonly Trigger APlayerDiscards = new(EventKind.Discarded);

	/// <summary>Titanbones: "when you discard this card" — FromGraveyard, where the card now is.</summary>
	public static readonly Trigger SelfDiscarded =
		new(EventKind.Discarded) { Subject = Filters.Is.Source };

	/// <summary>Ruric Thar, Biomagus.</summary>
	public static readonly Trigger SelfBecomesTargetOfOpponentsSpellOrAbility =
		new(EventKind.BecameTarget) { Subject = Filters.Is.Source, By = Whose.Opponent };

	/// <summary>Hexhaven Invigorator — X is how much.</summary>
	public static readonly Trigger SelfDealtDamage =
		new(EventKind.CreatureDealtDamage) { Subject = Filters.Is.Source };
	public static readonly Trigger SelfDealsCombatDamageToPlayer =
		new(EventKind.PlayerDealtDamage) { Also = EventTest.CombatDamageFromThis };
	public static readonly Trigger OpponentDealtNoncombatDamage =
		new(EventKind.PlayerDealtDamage)
		{
			Subject = Filters.Is.Opponents,
			Also = EventTest.Noncombat,
		};

	/// <summary>Fblthp, Impossibly Lost — "one or more", once a step.</summary>
	public static readonly Trigger OpponentsDealtCombatDamageDuringYourTurn =
		new(EventKind.CombatDamageToPlayers) { Subject = Filters.Is.Opponents, By = Whose.You };
	public static readonly Trigger YourUpkeep = Beginning(Step.Upkeep, Whose.You);

	/// <summary>"Each player's upkeep".</summary>
	public static readonly Trigger EachUpkeep = Beginning(Step.Upkeep, Whose.Any);
	public static readonly Trigger OpponentsDrawStep = Beginning(Step.Draw, Whose.Opponent);
	public static readonly Trigger YourEndStep = Beginning(Step.End, Whose.You);

	/// <summary>"The end step" — every turn's.</summary>
	public static readonly Trigger EachEndStep = Beginning(Step.End, Whose.Any);
	public static readonly Trigger BeginningOfCombatOnYourTurn = Beginning(
		Step.BeginCombat,
		Whose.You
	);

	/// <summary>Gardenize: "at the beginning of your first main phase".</summary>
	public static readonly Trigger YourFirstMainPhase = Beginning(Step.PreCombatMain, Whose.You);

	/// <summary>"When you do, …" — put on the stack by WhenYouDo, never matched against an event.</summary>
	public static readonly Trigger Reflexive = new(EventKind.Reflexive);

	/// <summary>"At the beginning of [whose] [step]".</summary>
	public static Trigger Beginning(Step step, Whose whose) =>
		new(EventKind.StepBegan) { Step = step, By = whose };
}

/// <summary>"When/Whenever [event], [effects]." Goes on the stack the next time a player would get priority.</summary>
public record TriggeredAbilityComponent(Trigger On, ImmutableList<GameAction> Effects)
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
}

/// <summary>"[Cost]: [effects]." Uses the stack. Mana abilities are <see cref="ManaAbilityComponent"/>, which does not.</summary>
public record ActivatedAbilityComponent(ManaCost Cost, ImmutableList<GameAction> Effects)
	: GameComponent
{
	/// <summary>
	/// The rest of the cost, in the order it is paid — Gallia, Tragic Host: [TapSelfCost,
	/// ExileFromGraveyardCost(Is.Creature &amp; !Is.Source)]. Taps first; the source is seen as it
	/// was activated after that.
	/// </summary>
	public ImmutableList<AdditionalCost> Costs
	{
		get => costs;
		// "{T}, tap two untapped artifacts": the source is already spoken for (the old TapCost check).
		// "Tap two" becomes two costs that each pick one — the action names both (TapId, SecondTapId);
		// a list already split (a copy, a deserialized state) is not split again.
		init =>
			costs = [
				.. value.SelectMany(c =>
					c is TapUntappedCost tap
						? Split(
							tap with
							{
								NotTheSource = tap.NotTheSource || value.Any(v => v is TapSelfCost),
							},
							value.Any(v => v is TapUntappedCost { Second: true })
						)
						: [c]
				),
			];
	}

	static IEnumerable<AdditionalCost> Split(TapUntappedCost tap, bool alreadySplit) =>
		(tap.Count, alreadySplit) switch
		{
			(1, _) or (2, true) => [tap],
			(2, false) => [tap, tap with { Second = true }],
			_ => throw new ArgumentException("Tap three or more: an action names two taps at most"),
		};

	readonly ImmutableList<AdditionalCost> costs = [];

	public ImmutableList<TargetSpec> Targets { get; init; } = [];

	/// <summary>
	/// Tea, Earl Grey, Hot: "{2}, {T}, Sacrifice this artifact: Choose one — …" — chosen as it is
	/// activated (CR 602.2b), like a spell's (ActivateAbilityAction.Modes).
	/// </summary>
	public ImmutableList<Mode> Modes { get; init; } = [];
	public int Choose { get; init; } = 1;

	/// <summary>"Activate only as a sorcery."</summary>
	public bool SorcerySpeed { get; init; }

	/// <summary>Where it is activated from: the battlefield, or a card in your hand (cycling) or graveyard.</summary>
	public ZoneType From { get; init; } = ZoneType.Battlefield;

	/// <summary>Warrior's Blades' equip: "{1} less for each +1/+1 counter on the creature it targets" — the targets are known.</summary>
	public CostChange? CostChange { get; init; }

	/// <summary>"Activate only if [condition]" — judged as it is activated.</summary>
	public Condition? OnlyIf { get; init; }

	/// <summary>
	/// DOT-7 Repair Squad: "Activate only once each turn." — stamped on the card as it is activated
	/// (Card.ActivatedOncePerTurnOn). ponytail: one stamp per card, as Exhaust's — no TRK card has two.
	/// </summary>
	public bool OncePerTurn { get; init; }

	/// <summary>
	/// "Exhaust — … (Activate each exhaust ability only once.)" — stamped on the card as it is
	/// activated. ponytail: one stamp per card, as no FRA card has two exhaust abilities.
	/// Not MtgCore's "exhaust", which is its tapping.
	/// </summary>
	public bool Exhaust { get; init; }

	/// <summary>"{T}" is part of the cost.</summary>
	public bool TapsItself => Costs.Any(c => c is TapSelfCost);
}

/// <summary>
/// "[+N]/[−N]/[0]: [effects]" — a planeswalker's loyalty ability (CR 606). Sorcery speed, once per
/// turn per permanent; the cost adds or removes loyalty counters.
/// </summary>
public record LoyaltyAbilityComponent(int Cost, ImmutableList<GameAction> Effects) : GameComponent
{
	public ImmutableList<TargetSpec> Targets { get; init; } = [];

	/// <summary>"−X:" — the activator chooses X (up to its loyalty); X reaches the effects (Count.X).</summary>
	public bool MinusX { get; init; }

	/// <summary>Jace, Reality Sculptor: "Activate only if …".</summary>
	public Condition? OnlyIf { get; init; }
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
	OnlyNoncreatureSpells, // Chandra, Chill of Compliance
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

/// <summary>
/// "This permanent enters tapped [unless …]." Applied by MagicState.EnterBattlefield, judged on the
/// board it is entering, with this permanent as the source ("other lands" is !Is.Source).
/// </summary>
public record EntersTappedComponent(Condition? Unless = null) : GameComponent;

/// <summary>
/// "… cost {N} more / less to cast" (CR 601.2f) — Amount is added to the generic cost (negative: less).
/// Thalia: new CostChange(1, !Is.Creature) { By = Whose.Opponent } — on a permanent, for the spells
/// its controller's opponents cast; Ghalta: new CostChange(Count.GreatestPowerOf(Ref.CreaturesYouControl)
/// .Negated) { OnItself = true }. Read by Costs.Of only.
/// </summary>
public record CostChange(Count Amount, Filters.Filter? Spells = null) : GameComponent
{
	/// <summary>Whose spells it changes, from the permanent's controller: "you cast", "your opponents cast".</summary>
	public Whose By { get; init; } = Whose.You;

	/// <summary>Wrath of the Bloodmane: "if you control a legendary creature".</summary>
	public Condition? If { get; init; }

	/// <summary>"This spell costs …": read from the card being cast (any zone), not from the battlefield.</summary>
	public bool OnItself { get; init; }
}

/// <summary>
/// "As an additional cost to cast this spell, …" — Silence the Echo: [SacrificeCost(Is.Creature |
/// Is.Planeswalker) { OrPay = 3 }]; Countersculpt: [BeholdCost("Jace") { OrPay = 1 }].
/// </summary>
public record AdditionalCostsComponent(ImmutableList<AdditionalCost> Costs) : GameComponent;

/// <summary>"This creature enters with N (or X) +1/+1 counters on it." Applied by MagicState.EnterBattlefield.</summary>
public record EntersWithCountersComponent(Count Count) : GameComponent;

/// <summary>On a card exiled "until [source] leaves the battlefield" — MagicState.MoveCard returns it then.</summary>
public record ExiledUntilLeaves(int SourceId, int SourceIncarnation) : GameComponent;

/// <summary>
/// A static ability that changes characteristics while its source is on the battlefield — Saheeli:
/// "Thopters you control have haste" is (Is.Creature &amp; Is.Yours &amp; Is.Subtype("Thopter"),
/// "Haste"); "this gets +1/+0 for each …" is Is.Source. Read only by Characteristics.
/// </summary>
public record StaticEffectComponent(Filters.Filter Affects, string? Keyword = null) : GameComponent
{
	// Characteristics asks a static's scope while computing P/T and keywords: a scope that reads
	// keywords, or a P/T static's scope that reads P/T, would ask itself forever. Refused here, as
	// it is built (a `with` included), rather than as a stack overflow mid-game.
	// ponytail: only the scope is checked — an If or a Power Count reading keywords (or, on a P/T
	// static, P/T) through a filter would still loop. Check Count/Condition too if a card ever needs one.
	readonly Filters.Filter affects = Scope(Affects, 0, 0);
	readonly Count power = 0;
	readonly Count toughness = 0;

	/// <summary>Who it affects — Tetsuko: "creatures you control with power or toughness 1 or less".</summary>
	public Filters.Filter Affects
	{
		get => affects;
		init => affects = Scope(value, power, toughness);
	}

	public Count Power
	{
		get => power;
		init
		{
			Scope(affects, value, toughness);
			power = value;
		}
	}

	public Count Toughness
	{
		get => toughness;
		init
		{
			Scope(affects, power, value);
			toughness = value;
		}
	}

	/// <summary>
	/// "As long as [condition]" — threshold, "as long as you control a Jace planeswalker". Never one
	/// that reads keywords, nor on a P/T static power or toughness: Characteristics would recurse.
	/// </summary>
	public Condition? If { get; init; }

	/// <summary>A P/T static — what Characteristics' P/T pass reads, and what the guard holds to the stricter rule.</summary>
	public bool ChangesPowerOrToughness => Power != 0 || Toughness != 0;

	static Filters.Filter Scope(Filters.Filter affects, Count power, Count toughness) =>
		affects.ReadsKeywords()
			? throw new ArgumentException($"A static's scope can't read keywords: {affects}")
		: affects.ReadsPowerOrToughness() && (power != 0 || toughness != 0)
			? throw new ArgumentException(
				$"A P/T static's scope can't read power or toughness: {affects}"
			)
		: affects;
}

/// <summary>
/// Traxos: "doesn't untap during your untap step" — read by the untap step ONLY; an untap effect
/// still untaps it. (MtgCore's freeze blocks every untap: Dungeon Geists' wording, not this one.)
/// </summary>
public record DoesntUntapInYourUntapStep : GameComponent;

/// <summary>
/// "Flashback [cost] (You may cast this card from your graveyard for its flashback cost. Then exile
/// it.)" Twinned Vision's flashback also costs "Discard a card".
/// </summary>
public record FlashbackComponent(ManaCost Cost) : GameComponent
{
	/// <summary>Twinned Vision: [DiscardCost()] — what the flashback costs beyond mana.</summary>
	public ImmutableList<AdditionalCost> Costs { get; init; } = [];
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

/// <summary>
/// Cold-Blooded Crew: "This creature can't be blocked by creatures with power 2 or less."; Species 8472:
/// "… by artifact creatures." — the filter asked of each would-be blocker (Combat.CanBlock).
/// </summary>
public record CantBeBlockedBy(Filters.Filter Blockers) : GameComponent;

/// <summary>
/// Species 8472: "This creature can't be the target of abilities from artifact sources your opponents
/// control." — abilities only, never spells; the filter asked of the ability's source on the battlefield
/// (TargetSpec). ponytail: a source sacrificed to pay the cost is gone by resolution, so the target is
/// legal again then — keep the source as it was (StackAbility.Source) if a card ever cares.
/// </summary>
public record CantBeTargetedByAbilitiesFrom(Filters.Filter Sources) : GameComponent;

/// <summary>Yuriko: "During combat, players can't cast spells or activate abilities that aren't mana abilities."</summary>
public record NoSpellsOrAbilitiesDuringCombat : GameComponent;

/// <summary>Samut: "Instant and sorcery spells you control have split second."</summary>
public record YourInstantsAndSorceriesHaveSplitSecond : GameComponent;

/// <summary>Tomik, Orzhov Lawmage: "Planeswalkers you control have 'No more than one creature can attack this planeswalker each combat.'"</summary>
public record OneAttackerPerPlaneswalkerYouControl : GameComponent;

/// <summary>Infinite Coursework: "Enchanted creature loses all abilities" — Characteristics.HasLostAllAbilities.</summary>
public record EnchantedLosesAllAbilities : GameComponent;

/// <summary>De-evolve: "Enchanted creature … is a Salamander. (It loses all other creature types.)" — Characteristics.Subtypes.</summary>
public record EnchantedIsCreatureType(string Subtype) : GameComponent;

/// <summary>
/// Storied Veteran: "You may cast this card from your graveyard. If you do, it enters with a finality
/// counter on it." — read by Playable.CanCastFromHere; unlike flashback it isn't exiled leaving the stack
/// (MagicState.MoveCard), so it resolves onto the battlefield (StackResolution).
/// </summary>
public record MayCastFromYourGraveyard : GameComponent;

/// <summary>Infinite Coursework: "… and doesn't untap during its controller's untap step" — the Aura's, not the creature's.</summary>
public record EnchantedDoesntUntap : GameComponent;

/// <summary>Marooned: "Enchanted permanent … can't be beamed up." — read by BeamMeUpCost: never returned to pay one.</summary>
public record EnchantedCantBeBeamedUp : GameComponent;

/// <summary>"This spell can't be countered." Read by StackResolution.Counter.</summary>
public record CantBeCountered : GameComponent;

/// <summary>
/// A delayed triggered ability (CR 603.7) kept on the permanent it acts on — Vindictive Triumph's
/// "exile it at the beginning of the next end step". Not the permanent's ability: losing all
/// abilities keeps it, a copy doesn't take it, and it is gone with a zone change (Card.IsStatus).
/// It triggers only once (CR 603.7c): Triggers.PutOnStack takes it off its host as it triggers, so one
/// countered never comes back. Also on a card it exiled (Ghost of Illyria) and on an object in the
/// command zone (AtNextEndStep).
/// </summary>
public record DelayedTrigger(TriggeredAbilityComponent Ability) : GameComponent
{
	/// <summary>Who controls it (CR 603.7d: whoever created it) — Ghost of Illyria's controller, not the exiled card's owner. 0: its host's.</summary>
	public int ControllerId { get; init; }
}

/// <summary>Garruk, Veiled Butcher: "If a creature an opponent controls would die, exile it instead." — MagicState.MoveCard.</summary>
public record OpponentsCreaturesExiledInsteadOfDying : GameComponent;

/// <summary>
/// Puppet Crafting: "Enchanted permanent is a [Subtype] creature with base power and toughness P/T in
/// addition to its other types." Stamped on the host while the Aura is on it (Attachments.Check).
/// </summary>
public record EnchantedBecomesCreature(string Subtype, int Power, int Toughness) : GameComponent;

/// <summary>
/// Station (CR 702.184; Oberth-Class Starship: "It's an artifact creature at 6+."): a creature with its
/// printed base P/T while it has CreatureAt or more charge counters. Read live by Card.Is from the
/// card's OWN counters — never stamped, so it is right the moment a counter is put or removed — and a
/// type change, so "loses all abilities" (layer 6) leaves it a creature. The station ability and the
/// N+ abilities are ordinary components (TrkCards.Station).
/// </summary>
public record StationComponent(int CreatureAt) : GameComponent;

/// <summary>Emrakul's land: "{T}: Add {C}{C}" until that card (this object of it) is cast from exile.</summary>
public record TapsForTwoColorlessWhileExiled(int CardId, int Incarnation) : GameComponent;

/// <summary>Karn, Argent Defender: "Artifacts and creatures entering the battlefield don't cause abilities to trigger." Read by Triggers.Collect.</summary>
public record EnteringArtifactsAndCreaturesDontTrigger : GameComponent;

/// <summary>
/// Root Maze: "Artifacts and lands enter tapped." — every player's permanents the filter matches,
/// read by MagicState.EnterBattlefield while this is on the battlefield.
/// </summary>
public record PermanentsEnterTapped(Filters.Filter Which) : GameComponent;

/// <summary>
/// Flesh Duplicate: "You may have this creature enter as a copy of any creature on the battlefield,
/// except it has vanishing N if that creature doesn't have vanishing." Applied by
/// MagicState.EnterBattlefield: the choice as it enters, then BecomeCopyOfChosenAction.
/// </summary>
public record MayEnterAsCopy : GameComponent
{
	/// <summary>"Except it has vanishing N if that creature doesn't have vanishing" — 0: no exception.</summary>
	public int VanishingIfNone { get; init; }
}

/// <summary>
/// "Vanishing N (A permanent with vanishing N enters with N time counters on it. At the beginning of
/// your upkeep, remove a time counter from it. When the last is removed, sacrifice it.)" The marker;
/// <see cref="Of"/> is the whole keyword, its upkeep trigger included.
/// </summary>
public record Vanishing(int N) : GameComponent
{
	public static GameComponent[] Of(int n) =>
		[
			new Vanishing(n),
			new TriggeredAbilityComponent(
				TriggerEvent.YourUpkeep,
				[new RemoveTimeCounter(Ref.Source)]
			),
		];
}

/// <summary>Necrodominance: "Skip your draw step." — it never begins: no draw, no trigger, no priority. Read by TurnStructure.</summary>
public record SkipYourDrawStep : GameComponent;

/// <summary>Necrodominance: "Your maximum hand size is five." The lowest applies; read by the cleanup step (TurnStructure.MaxHandSizeOf).</summary>
public record MaximumHandSize(int N) : GameComponent;

/// <summary>
/// Necrodominance: "If a card or token would be put into your graveyard from anywhere, exile it
/// instead." — your graveyard is the one you own; read by MagicState.MoveCard, so nothing dies.
/// </summary>
public record ExileInsteadOfYourGraveyard : GameComponent;

/// <summary>
/// Splinter Twin: "Enchanted creature has '[ability]'" — an activated ability the host gets while
/// this Aura is on it. Its "this creature" is the host. Read by Characteristics.ActivatedAbilitiesOf.
/// </summary>
public record GrantsActivatedAbility(ActivatedAbilityComponent Ability) : GameComponent;

/// <summary>
/// Hive Mind Coprocessor: "Artifact creatures you control have 'Whenever this creature deals combat
/// damage to a player, you may pay 1 life. If you do, draw a card.'" — a static: Triggers.Collect gives
/// the ability to each permanent the scope matches, and "this creature" is that permanent. Not on the
/// card, so a copy doesn't have it; a permanent that lost all abilities doesn't either.
/// </summary>
public record GrantsTriggeredAbility(Filters.Filter Affects, TriggeredAbilityComponent Ability)
	: GameComponent;

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
