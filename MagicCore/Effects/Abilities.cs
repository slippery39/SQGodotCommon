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

	/// <summary>How many of the Modes (ModeChoice) — Sublime Epiphany: new() { AtMost = 5 }.</summary>
	public ModeChoice Choose { get; init; } = 1;

	/// <summary>
	/// Escalate (CR 702.120; Collective Brutality: "Escalate—Discard a card. (Pay this cost for each mode chosen beyond
	/// the first.)") — paid once per mode beyond the first: a DiscardCost with that Count, its cards picked after the
	/// cast (CastSpellAction.CostsOf), never a cast per card.
	/// </summary>
	public DiscardCost? Escalate { get; init; }
}

/// <summary>
/// "Replicate [cost] (When you cast this spell, copy it for each time you paid its replicate cost. You
/// may choose new targets for the copies.)" — CastSpellAction.Replicate: Consign to Memory's {1}, Lose
/// Focus' {U} — the cost added once per payment, beside the spell's own.
/// </summary>
public record ReplicateComponent(ManaCost Cost) : GameComponent
{
	/// <summary>
	/// Squad (Sicarian Infiltrator): "As an additional cost to cast this spell, you may pay {2} any number of times.
	/// When this creature enters, create that many tokens that are copies of it." — paid as replicate is, but nothing
	/// is copied as it is cast: Scripting.Squad's trigger reads the payments (Count.TimesReplicated).
	/// </summary>
	public bool AsItEnters { get; init; }
}

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

	/// <summary>
	/// Thieving Skydiver: "Kicker {X}. X can't be 0." — the least X a kicked cast may choose. A kicker with {X} lets the
	/// kicked cast choose X (CastSpellAction.X), paid as any X and read as it enters (Card.CastX).
	/// </summary>
	public int MinX { get; init; }

	/// <summary>
	/// Teamwork (We Say Thee Nay!, LsvCards.Teamwork) is this optional additional cost by another name: "if this
	/// spell was cast using teamwork" is WasKicked. Only the cast's description reads it ("(teamwork)").
	/// </summary>
	public string Word { get; init; } = "kicked";
}

/// <summary>
/// Splice (CR 702.47) — Goryo's Vengeance: "Splice onto Arcane {2}{B} (As you cast an Arcane spell, you may reveal this
/// card from your hand and pay its splice cost. If you do, add this card's effects to that spell.)" — the card in its
/// owner's hand, the spell cast matching <see cref="Onto"/>: CastSpellAction.Splice, the text carried by Spliced.
/// </summary>
public record SpliceComponent(ManaCost Cost, Filters.Filter Onto) : GameComponent;

/// <summary>The named counter kinds (Card.Counters) scripts and rules share.</summary>
public static class CounterKinds
{
	public const string Lore = "lore";
	public const string Quest = "quest";
	public const string Promotion = "promotion";
	public const string Peace = "peace"; // Peace in Our Time
	public const string Story = "story"; // Staff of the Storyteller
	public const string Chorus = "chorus"; // Malcolm, Alluring Scoundrel
	public const string Wish = "wish"; // Wishclaw Talisman
	public const string Void = "void"; // Dauthi Voidwalker
	public const string Corpse = "corpse"; // From the Catacombs

	/// <summary>Drivnod, Carnage Dominus: a keyword counter — it has that keyword (CR 122.1b, Card.HasOwn).</summary>
	public const string Indestructible = "indestructible";

	/// <summary>Metamorphosis Fanatic: "with a lifelink counter on it" — a keyword counter, as Drivnod's.</summary>
	public const string Lifelink = "lifelink";

	// The older kinds keep their own fields on Card (rules read them everywhere); MagicState.PutCounters
	// is still the one way any of them is put on.
	public const string PlusOne = "+1/+1";
	public const string MinusOne = "-1/-1";
	public const string Charge = "charge";
	public const string Stun = "stun";
	public const string Time = "time";
	public const string Loyalty = "loyalty";

	/// <summary>The kinds with their own field on Card — every other kind is named, in Card.Counters.</summary>
	public static readonly string[] Fielded = [PlusOne, MinusOne, Charge, Stun, Time, Loyalty];
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
	ChangedZone, // Vash: "whenever a card leaves your graveyard"; Laelia: "put into exile from your library and/or your graveyard"; Skyclave Apparition: "when this leaves the battlefield" — MagicState.MoveCard, every path (Trigger.From, To)
	Drew, // Data, New Life: "whenever you draw a card" — MagicState.Draw, one per card
	AbilityActivated, // Kai Winn: "activates an ability of a source with the chosen name" — mana abilities too
	Reflexive, // "When you do": put on the stack by WhenYouDo, never raised
	LandPlayed, // Prosper, Tome-Bound: "whenever you play a card from exile" — a land played (PlayLandAction), not one put onto the battlefield
	BecameLevel, // Does Machines: "When this Class becomes level 2" — GainLevel (Trigger.Chapter)
	SpellCopied, // Sedgemoor Witch: magecraft's "or copy" — a copy of a spell put on the stack (CopySpell); a copy is never cast
}

/// <summary>
/// Where a trigger is read from. Almost all: the battlefield, matched against each event. Three look
/// back (CR 603.10a) and are read in Triggers.Collect from the card itself instead.
/// </summary>
public enum ReadFrom
{
	Battlefield,
	TheCardThatLeft, // "when this dies", "when this leaves the battlefield" — the card that left, as it last existed; Subject asked of it
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
	NotTheTurnsDraw, // Orcish Bowmasters: "except the first one they draw in each of their draw steps" (DrewCardEvent.TurnDraw)
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
	/// least N — written with Scripting.Chapter. On BecameLevel, a Class's level N (CR 716.2c, Does Machines:
	/// "When this Class becomes level 2") — the level it became.
	/// </summary>
	public int Chapter
	{
		get => chapter;
		init =>
			chapter =
				value == 0 || On is EventKind.CountersPut or EventKind.BecameLevel
					? value
					: throw new ArgumentException(
						$"A Chapter only fits CountersPut or BecameLevel, not {On}"
					);
	}

	readonly int nth;

	/// <summary>
	/// Stella Lee, Wild Card: "Whenever you cast your second spell each turn"; Faerie Mastermind: "Whenever an
	/// opponent draws their second card each turn" — the event of that player's Nth spell (SpellCast) or Nth
	/// card drawn (Drew) this turn, numbered as it happens (SpellCastEvent.SpellNumber, DrewCardEvent.Number).
	/// </summary>
	public int Nth
	{
		get => nth;
		init =>
			nth =
				value == 0 || On is EventKind.SpellCast or EventKind.Drew
					? value
					: throw new ArgumentException($"An Nth only fits SpellCast or Drew, not {On}");
	}

	readonly string? ability;

	/// <summary>
	/// Decree of Pain: "When you cycle this card" — an AbilityActivated event of an ability with this
	/// keyword (ActivatedAbilityComponent.Keyword), so landcycling is cycling too.
	/// </summary>
	public string? Ability
	{
		get => ability;
		init =>
			ability =
				value is null || On == EventKind.AbilityActivated
					? value
					: throw new ArgumentException($"Ability only fits AbilityActivated, not {On}");
	}

	readonly string? counters;

	/// <summary>
	/// U.S.S. Enterprise-D: "Whenever one or more charge counters are put on [it]" — a CountersPut
	/// event of this kind (Emperor of Bones: +1/+1). MagicState.PutCounters raises it for every kind but loyalty (its own event), so that is refused rather than built inert.
	/// </summary>
	public string? Counters
	{
		get => counters;
		init =>
			counters =
				value is null || (On == EventKind.CountersPut && value != CounterKinds.Loyalty)
					? value
					: throw new ArgumentException(
						$"Counters fits CountersPut of any kind but loyalty, not {On} / {value}"
					);
	}

	readonly ZoneType? from;

	/// <summary>
	/// Prosper, Tome-Bound: "Whenever you play a card from exile" — a spell cast from, or a land played from,
	/// this zone (SpellCastEvent.From, LandPlayedEvent.From). "Play" is both: two rows, as Isshin's are.
	/// A permanent entering from it too (EnteredBattlefieldEvent.From).
	/// </summary>
	public ZoneType? From
	{
		get => from;
		init => from = value is null || KnowsFrom ? value : throw FromDoesntFit("From");
	}

	bool KnowsFrom =>
		On
			is EventKind.SpellCast
				or EventKind.LandPlayed
				or EventKind.ChangedZone
				or EventKind.Entered;

	ArgumentException FromDoesntFit(string option) =>
		new($"{option} fits SpellCast, LandPlayed, ChangedZone or Entered, not {On}");

	readonly ImmutableList<ZoneType> notFrom = [];

	/// <summary>
	/// "Name Sticker" Goblin: "When this creature enters from anywhere other than a graveyard or exile" — the
	/// zones the event's From must NOT be; a token just made came from no zone, so it triggers.
	/// </summary>
	public ImmutableList<ZoneType> NotFrom
	{
		get => notFrom;
		init => notFrom = value.IsEmpty || KnowsFrom ? value : throw FromDoesntFit("NotFrom");
	}

	readonly ZoneType? to;

	/// <summary>
	/// Laelia, the Blade Reforged: "Whenever one or more cards are put into exile from your library and/or your
	/// graveyard" — the zone a card moved TO (ChangedZoneEvent.To); with no From, from any zone the event is raised
	/// for (hers: NotFrom = [Battlefield]). Vash's "leaves your graveyard" is From = Graveyard, any To.
	/// </summary>
	public ZoneType? To
	{
		get => to;
		init =>
			to =
				value is null || On is EventKind.ChangedZone
					? value
					: throw new ArgumentException($"To fits ChangedZone, not {On}");
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
			EventTest.NotTheTurnsDraw => on == EventKind.Drew,
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
		new(EventKind.ChangedZone) { By = Whose.You, From = ZoneType.Graveyard };

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
		new(EventKind.Died) { Subject = Filters.Is.Source, ReadFrom = ReadFrom.TheCardThatLeft };

	/// <summary>
	/// Skyclave Apparition: "When this creature leaves the battlefield" — to any zone, read from the card as it last
	/// existed (CR 603.10a). Serra Paragon's "When this permanent is put into a graveyard from the battlefield" is
	/// this with { To = Graveyard }.
	/// </summary>
	public static readonly Trigger SelfLeaves =
		new(EventKind.ChangedZone)
		{
			Subject = Filters.Is.Source,
			ReadFrom = ReadFrom.TheCardThatLeft, // the battlefield: no From to say
		};

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

	/// <summary>Data, New Life: "Whenever you draw a card" — once per card drawn.</summary>
	public static readonly Trigger YouDrawACard = new(EventKind.Drew) { By = Whose.You };

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
	// ponytail: OneOrMore is one trigger per player per priority pass — one player's two separate discards before
	// anyone has priority trigger it once; a batch number on DiscardedEvent when a card does that (DesignNotes).
	public static readonly Trigger APlayerDiscards = new(EventKind.Discarded) { OneOrMore = true };

	/// <summary>Titanbones: "when you discard this card" — From = Graveyard, where the card now is.</summary>
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

	/// <summary>
	/// Mana Drain's delayed trigger: "add an amount of {C} equal to that spell's mana value" — a number fixed as it
	/// was set up (AtNextEndStep.X), its effects' X when it triggers. 0: the event's ("that much damage").
	/// </summary>
	public int X { get; init; }

	/// <summary>"When this enters, choose one —": chosen as it goes on the stack (CR 700.2b).</summary>
	public ImmutableList<Mode> Modes { get; init; } = [];

	/// <summary>
	/// How many of the Modes (ModeChoice) — Ertai Resurrected: new() { AtLeast = 0 }; Kirk's AnyNumberIf and
	/// Kobayashi Maru's NotChosenBefore are on it too.
	/// </summary>
	public ModeChoice Choose { get; init; } = 1;

	/// <summary>"At …, if [condition], …" — intervening: checked as it triggers AND as it resolves (CR 603.4).</summary>
	public Condition? If { get; init; }

	/// <summary>
	/// Where the card is when it triggers — the battlefield unless said (as ActivatedAbilityComponent.From). Darklight
	/// Phoenix: "return this card from your graveyard" (Graveyard); miracle's "when you draw it" (Hand, Scripting.Miracle).
	/// </summary>
	public ZoneType From { get; init; } = ZoneType.Battlefield;

	/// <summary>"This ability triggers only once each turn." Stamped on the card as it goes on the stack.</summary>
	public bool OncePerTurn { get; init; }
}

/// <summary>"[Cost]: [effects]." Uses the stack. Mana abilities are <see cref="ManaAbilityComponent"/>, which does not.</summary>
public record ActivatedAbilityComponent(ManaCost Cost, ImmutableList<GameAction> Effects)
	: GameComponent
{
	/// <summary>
	/// The keyword this ability is, if any — Decree of Pain: "Cycling {3}{B}{B}" is "Cycling" (Scripting.Cycling),
	/// basic landcycling too (CR 702.29e: a cycling ability). What "when you cycle" reads (Trigger.Ability).
	/// </summary>
	public string? Keyword { get; init; }

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
			_ => [tap], // tap three or more: one cost picking the whole set (TapIds)
		};

	readonly ImmutableList<AdditionalCost> costs = [];

	public ImmutableList<TargetSpec> Targets { get; init; } = [];

	/// <summary>
	/// Tea, Earl Grey, Hot: "{2}, {T}, Sacrifice this artifact: Choose one — …" — chosen as it is
	/// activated (CR 602.2b), like a spell's (ActivateAbilityAction.Modes).
	/// </summary>
	public ImmutableList<Mode> Modes { get; init; } = [];

	/// <summary>How many of the Modes (ModeChoice).</summary>
	public ModeChoice Choose { get; init; } = 1;

	/// <summary>"Activate only as a sorcery."</summary>
	public bool SorcerySpeed { get; init; }

	/// <summary>Where it is activated from: the battlefield, or a card in your hand (cycling), graveyard or exile (Q's gift: "Activate only … if this card is exiled").</summary>
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

/// <summary>
/// What mana may pay for (MC-4): "Spend this mana only to cast [Spells]" — Mishra's Workshop: artifact spells
/// (<c>new(Is.Artifact)</c>); Gideon's Memorial: a planeswalker spell; Chandra, Chill of Compliance: a noncreature
/// spell. The filter is asked of the spell being paid for. <c>default</c> is no restriction. Read by
/// <c>ManaAbilities.Allows</c> alone — a permanent's mana and floating mana alike.
/// </summary>
public readonly record struct ManaRestriction(Filters.Filter? Spells)
{
	public static readonly ManaRestriction None = default;

	/// <summary>Isolinear Processor: "only to cast an artifact spell or activate an ability of an artifact source" — Spells asked of the ability's source too.</summary>
	public bool Abilities { get; init; }

	/// <summary>Heartwood Crafter: "This mana can't be spent to cast spells from your hand."</summary>
	public bool NotFromHand { get; init; }

	/// <summary>
	/// Delighted Halfling: "Spend this mana only to cast a legendary spell, and that spell can't be countered." — a
	/// spell this mana paid for is stamped OnStackComponent.CantBeCountered (ManaPayment.MakesUncounterable).
	/// </summary>
	public bool CantBeCountered { get; init; }
}

/// <summary>
/// A mana ability (CR 605) — an activated ability that doesn't use the stack: "[Cost], [Costs]: Add
/// [Amount] [Color]." "{T}: Add {C}." on a non-land (basic land types produce mana by their subtype,
/// CR 305.6); Rok-Tahk's "{T}: Add {G}{G}" (Amount 2); Loot, the Nexus: "{T}: Choose a color. Add one
/// mana of that color for each [Amount]" (OfAnyOneColor — counted as it is tapped, the permanent its
/// source); Treasure: "{T}, Sacrifice this: Add one mana of any color" (Costs). Read by ManaAbilities.
/// </summary>
public record ManaAbilityComponent(ManaColor Color) : GameComponent
{
	static readonly ImmutableList<AdditionalCost> TapOnly = [new TapSelfCost()];

	/// <summary>"This mana can't be spent to cast spells from your hand" / "Spend this mana only to cast a planeswalker spell".</summary>
	public ManaRestriction Restriction { get; init; }

	/// <summary>How much: Rok-Tahk's {G}{G} is 2; Loot, the Nexus counts it as it is tapped.</summary>
	public Count Amount { get; init; } = 1;

	/// <summary>
	/// Talisman of Conviction: "{T}: Add {R} or {W}. This artifact deals 1 damage to you."; Ancient Tomb's 2 —
	/// dealt as this ability makes the mana (ManaAbilities.PayAsUsed), through Damage.Deal.
	/// </summary>
	public int DealsDamageToYou { get; init; }

	/// <summary>"Add [Amount] mana of any one color" — every unit the one colour chosen; Color is unused.</summary>
	public bool AnyOneColor { get; init; }

	/// <summary>
	/// Orcish Lumberjack: "Add three mana in any combination of {R} and/or {G}." — each unit Color or this one; how
	/// many of this one is the activation's (TapForManaAction.OfTheOther, one move per split). Read for an ability
	/// whose cost picks a card. ponytail: a plain {T} ability with it makes only Color — a yield unit of two colours
	/// (ManaAbilities.YieldOf) when a card needs one.
	/// </summary>
	public ManaColor? OrColor { get; init; }

	/// <summary>
	/// Starfleet Academy: "{5}, {T}: Choose a color. Add …"; Talon Gates of Madara: "{1}, {T}: Add one
	/// mana of any color" — a mana ability that costs mana (Shayne, 2026-11-04: a real one). Never a
	/// plain tap: only ManaPayment.Find uses it, when nothing else pays.
	/// </summary>
	public ManaCost? Cost { get; init; }

	/// <summary>
	/// The costs beyond mana — the shared list (AdditionalCosts.cs); "{T}" unless it says otherwise.
	/// Treasure: [TapSelfCost, SacrificeSelfCost]. Phyrexian Tower: "{T}, Sacrifice a creature" — a cost
	/// that picks (ChoosesCost) is never auto-paid: it is a move (TapForManaAction.SacrificeId). So is one
	/// without {T} (Rain of Filth's gained "Sacrifice this land: Add {B}." — TapForManaAction.NoTap): the solver's
	/// sources tap once, and this one works on a tapped land.
	/// </summary>
	public ImmutableList<AdditionalCost> Costs { get; init; } = TapOnly;

	/// <summary>
	/// Elvish Spirit Guide: "Exile this card from your hand: Add {G}." (Costs = [ExileSelfCost]) — a mana ability of a
	/// card in that zone, never of the permanent; ManaPayment.Find spends it only when nothing on the battlefield pays.
	/// </summary>
	public ZoneType From { get; init; } = ZoneType.Battlefield;

	/// <summary>"Add one mana of any color" / "Choose a color. Add N of it".</summary>
	public static ManaAbilityComponent OfAnyOneColor(Count amount) =>
		new(ManaColor.Colorless) { Amount = amount, AnyOneColor = true };

	/// <summary>A cost that picks a card (Phyrexian Tower's creature) — never paid by the solver.</summary>
	public bool ChoosesCost => Costs.Any(c => c.Slot != CostSlot.None);

	/// <summary>Treasure: it is sacrificed as it makes the mana.</summary>
	public bool SacrificesItself => Costs.Any(c => c is SacrificeSelfCost);

	/// <summary>
	/// Rain of Filth's gained "Sacrifice this land: Add {B}." — a PERMANENT's mana ability without {T}: a move, never the
	/// solver's. A card's in a hand never taps (a Spirit Guide's exile is its whole cost), so From = Hand is never this.
	/// </summary>
	public bool NoTap => From == ZoneType.Battlefield && !Costs.Any(c => c.Taps);

	/// <summary>Tapped as payment or by hand with nothing more to pay: no mana cost, nothing to pick, a {T} (on a permanent).</summary>
	public bool IsPlain => Cost is null && !ChoosesCost && !NoTap;
}

/// <summary>
/// "[Permanents] have '[mana ability]'" — Rok-Tahk, Budding Scientist: "Creatures you control have
/// '{T}: Add {G}{G}.'"; Rishkar, Peema Renegade: "Each creature you control with a counter on it has
/// '{T}: Add {G}.'" Read by ManaAbilities.AbilitiesOf for each permanent Affects matches (from the
/// granter's side), as GrantsTriggeredAbility is; a permanent that lost all abilities has none.
/// </summary>
public record GrantsManaAbility(Filters.Filter Affects, ManaAbilityComponent Ability)
	: GameComponent;

/// <summary>Draconic Visitor: "If one or more artifact tokens would be created under your control, that many [Token] are created instead."</summary>
public record ArtifactTokensBecome(Cards.CardData Token, ImmutableArray<GameComponent> Abilities)
	: GameComponent
{
	/// <summary>Holo-Engineer: "If one or more tokens would be created under your control, that many 3/3 colorless Illusion artifact creature tokens are created instead." — any token, not only artifacts.</summary>
	public bool AnyToken { get; init; }
}

/// <summary>
/// Sunburst (Engineered Explosives): "This artifact enters with a charge counter on it for each color of
/// mana spent to cast it." — the cast pays with as many different colours as it can
/// (ManaPayment.FindMostColors) and records them (OnStackComponent.ColorsSpent). ponytail: charge
/// counters only, as on an artifact; a sunburst creature's +1/+1 counters wait for one.
/// </summary>
public record Sunburst : GameComponent;

/// <summary>
/// "This permanent enters tapped [unless …]." Applied by MagicState.EnterBattlefield, judged on the
/// board it is entering, with this permanent as the source ("other lands" is !Is.Source).
/// </summary>
public record EntersTappedComponent(Condition? Unless = null) : GameComponent
{
	/// <summary>
	/// The shocklands (Blood Crypt): "As this land enters, you may pay 2 life. If you don't, it enters
	/// tapped." — asked as it enters, only when this text is what taps it (not under Warp Speed or Root
	/// Maze) and its controller has the life to pay (CR 119.4).
	/// </summary>
	public int UnlessYouPayLife { get; init; }
}

/// <summary>
/// "… cost {N} more / less to cast" (CR 601.2f) — Amount is added to the generic cost (negative: less).
/// Thalia: new CostChange(1, !Is.Creature) { By = Whose.Opponent } — on a permanent, for the spells
/// its controller's opponents cast; Ghalta: new CostChange(Count.GreatestPowerOf(Ref.CreaturesYouControl)
/// .Negated) { OnItself = true }. Read by Costs.Of — and, with Abilities, by Costs.OfAbility only.
/// </summary>
public record CostChange(Count Amount, Filters.Filter? Spells = null) : GameComponent
{
	/// <summary>
	/// Anointed Peacekeeper: "Activated abilities of sources with the chosen name cost {2} more to activate
	/// unless they're mana abilities." — the filter is asked of the ability's SOURCE, whoever activates it (By
	/// isn't read), and no spell is changed.
	/// </summary>
	public bool Abilities { get; init; }

	/// <summary>Whose spells it changes, from the permanent's controller: "you cast", "your opponents cast".</summary>
	public Whose By { get; init; } = Whose.You;

	/// <summary>Wrath of the Bloodmane: "if you control a legendary creature".</summary>
	public Condition? If { get; init; }

	/// <summary>"This spell costs …": read from the card being cast (any zone), not from the battlefield.</summary>
	public bool OnItself { get; init; }

	/// <summary>
	/// Bilbo, Thief in the Night: "Spells you cast from anywhere other than your hand" — [Hand]: not for a spell cast
	/// from these zones. Asked of where the card is as it is cast (Costs.Of runs before it moves to the stack).
	/// </summary>
	public ImmutableList<ZoneType> NotFrom { get; init; } = [];

	/// <summary>
	/// Forensic Gadgeteer: "This effect can't reduce the mana in that cost to less than one mana." — a reduction never
	/// takes the cost below Floor mana (nor below what it was): applied after every other change (Costs.OfAbility).
	/// </summary>
	public int Floor { get; init; }
}

/// <summary>
/// "As an additional cost to cast this spell, …" — Silence the Echo: [SacrificeCost(Is.Creature |
/// Is.Planeswalker) { Or = new PayManaCost(3) }]; Countersculpt: [BeholdCost("Jace") { Or = new PayManaCost(1) }];
/// Bitter Triumph: [DiscardCost() { Or = new PayLifeCost(3) }]; Bone Shards: [SacrificeCost(Is.Creature) { Or = new DiscardCost() }].
/// </summary>
public record AdditionalCostsComponent(ImmutableList<AdditionalCost> Costs) : GameComponent;

/// <summary>"This creature enters with N (or X) +1/+1 counters on it." Applied by MagicState.EnterBattlefield.</summary>
public record EntersWithCountersComponent(Count Count) : GameComponent
{
	/// <summary>Wishclaw Talisman: "This artifact enters with three wish counters on it." — the kind (CounterKinds); +1/+1 unless said.</summary>
	public string Kind { get; init; } = CounterKinds.PlusOne;
}

/// <summary>On a card exiled "until [source] leaves the battlefield" — MagicState.MoveCard returns it then.</summary>
public record ExiledUntilLeaves(int SourceId, int SourceIncarnation) : GameComponent
{
	/// <summary>Where it returns: onto the battlefield, or its owner's hand (Deep-Cavern Bat's card).</summary>
	public ZoneType From { get; init; } = ZoneType.Battlefield;
}

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
/// MC-6.15: "[It] doesn't untap during its controller's untap step" — the holder's rule over what its filter matches,
/// asked from the holder's side: Traxos, Scourge Eternal's own (Is.Source), Infinite Coursework's and Marooned's
/// "enchanted creature/permanent" (Is.AttachedToSource). Read by the untap step ONLY; an untap effect still untaps it
/// (MtgCore's freeze blocks every untap: Dungeon Geists' wording, not this one). It is the HOLDER's ability (read with
/// AbilitiesOf): the enchanted creature losing its own changes nothing; Traxos — or an Aura — losing its own ends it.
/// </summary>
public record DoesntUntap(Filters.Filter Which) : GameComponent;

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
public record PlayableFromExile(int PlayerId, int UntilTurn) : GameComponent
{
	/// <summary>Vulcan Mind-Meld: "Mana of any type can be spent to cast spells this way." — Costs.Of.</summary>
	public bool AnyType { get; init; }

	/// <summary>Zero, Incorporeal Sensate: "A spell cast this way costs {2} more to cast." — Costs.Of.</summary>
	public int Tax { get; init; }

	/// <summary>
	/// A top card of its controller's library exiled UNSEEN (ExileTopPlayable): what it is was luck, so the AI's search prices it as
	/// a random card and never plays the sample's (Evaluator Terms.ImpulseValue). A known card — Emrakul, a chosen one — is not.
	/// </summary>
	public bool Blind { get; init; }

	/// <summary>
	/// Robber of the Rich: "During any turn you attacked with a Rogue, you may cast that card" — judged with the
	/// player it names as "you" (the card's own controller is its owner, the opponent).
	/// </summary>
	public Condition? If { get; init; }

	/// <summary>Robber of the Rich: "you may CAST that card" — never a land played.</summary>
	public bool CastOnly { get; init; }

	/// <summary>
	/// Urza, Lord High Artificer: "Until end of turn, you may play that card without paying its mana cost." — answered
	/// by Playable.FreeCast, so the free cast replaces the paid one.
	/// </summary>
	public bool Free { get; init; }

	/// <summary>Warp (Quantum Riddler): "Its owner may cast this card after the current turn has ended" — the first turn it may be (PlayPermission.LaterTurn).</summary>
	public int FromTurn { get; init; }
}

/// <summary>
/// Vulcan Mind-Meld: "exiles it face down" — in exile, seen only by whoever Card.KnownTo says (the
/// player who looked). Gone with the zone change: cast or played, it is public. Card.IsHiddenFrom.
/// ponytail: the search's Hidden.Sample doesn't guess the ones its player hasn't seen — sample them
/// as a library is sampled if the AI misplays on that knowledge (DesignNotes).
/// </summary>
public record ExiledFaceDown : GameComponent;

/// <summary>Room of Refuge: "As it enters, choose a color." Applied by MagicState.EnterBattlefield.</summary>
public record ChoosesColorAsItEnters : GameComponent;

/// <summary>"{T}: Add one mana of the chosen color." — set as it entered; gone when it leaves.</summary>
public record ChosenColorMana(ManaColor Color) : GameComponent;

/// <summary>
/// Nyota Uhura: "Hailing Frequencies — As Nyota Uhura enters, choose a number between 0 and 10." Applied
/// by MagicState.EnterBattlefield; the AI names a mana value its opponent is likely to cast (GreedyAi).
/// </summary>
public record ChoosesNumberAsItEnters(int Max) : GameComponent
{
	/// <summary>
	/// Gollum, Riddle Master: "As Gollum enters, choose odd or even. (Zero is even.)" — the number chosen (Max 1) is 0
	/// (even) or 1 (odd), and a mana value matches it by its parity (ChosenNumber.OddOrEven, Is.ManaValueIsChosenNumber).
	/// </summary>
	public bool OddOrEven { get; init; }
}

/// <summary>The number chosen as it entered (Nyota Uhura) — gone when it leaves.</summary>
public record ChosenNumber(int N) : GameComponent
{
	/// <summary>Gollum, Riddle Master: N is a parity (0 even, 1 odd) — kept with the choice, so a copy effect can't change what it means.</summary>
	public bool OddOrEven { get; init; }
}

/// <summary>
/// A Class's level (CR 716) — Does Machines: "{1}{U}: Level 2". No counter (Brad Boimler doesn't add to it, "a
/// permanent with a counter" doesn't see it): status, gone as it leaves and never copied. None is level 1, as every
/// Class enters (Count.ClassLevelOf). Set by GainLevel.
/// </summary>
public record ClassLevel(int N) : GameComponent;

/// <summary>
/// Kai Winn, Faithless Leader: "As Kai Winn enters, choose a nonland card name." Applied by
/// MagicState.EnterBattlefield — the names every nonland card printed in the game's sets has
/// (CardRegistry.CardNames, Shayne 2026-11-06); the AI names the opponent's commonest known (GreedyAi).
/// Anointed Peacekeeper: "As this creature enters, look at an opponent's hand, then choose any card name."
/// </summary>
public record ChoosesNameAsItEnters : GameComponent
{
	/// <summary>Anointed Peacekeeper: "look at an opponent's hand, then" — known to you before the name is asked.</summary>
	public bool LooksAtHand { get; init; }

	/// <summary>Anointed Peacekeeper: "any card name" — the lands' too.</summary>
	public bool AnyName { get; init; }
}

/// <summary>The card name chosen as it entered (Kai Winn) — gone when it leaves.</summary>
public record ChosenName(string Name) : GameComponent;

/// <summary>
/// True-Name Nemesis: "As this creature enters, choose a player." Applied by MagicState.EnterBattlefield — a
/// ChooseKeywordChoice of the two players (Bo1), an opponent first.
/// </summary>
public record ChoosesPlayerAsItEnters : GameComponent;

/// <summary>The player chosen as it entered (True-Name Nemesis) — gone when it leaves.</summary>
public record ChosenPlayer(int PlayerId) : GameComponent;

/// <summary>
/// Protection (CR 702.16) — True-Name Nemesis: "This creature has protection from the chosen player." What it is
/// protected FROM is a filter asked of the SOURCE (a spell, an ability's source, a creature dealing damage or
/// blocking, an Aura or Equipment) from the holder's side: "from [a player]" is the source's controller
/// (Is.ControlledByChosenPlayer); "from everything" (Hexdrinker) is Is.AnyCard. DEBT, each in its one place: damage
/// from it is prevented (Damage.Prevented), it can't enchant or equip it (Attachments), block it (Combat.CanBlock)
/// or target it (TargetSpec). Read through AbilitiesOf, so one gained until end of turn (Giver of Runes) counts and
/// losing all abilities ends it.
/// </summary>
public record Protection(Filters.Filter From) : GameComponent
{
	/// <summary>THE protection question: does this permanent's protection stop that source?</summary>
	public static bool Stops(GameState s, Cards.Card holder, Cards.Card source) =>
		s.AbilitiesOf<Protection>(holder)
			.Any(p => p.From.Matches(s, source, new(holder.ControllerId, holder.Id)));
}

/// <summary>The City on the Edge of Forever's Human: "Permanents can't phase in." — read by Rules.Phasing.PhaseIn.</summary>
public record PermanentsCantPhaseIn : GameComponent;

/// <summary>Stingcaster Mage: "gains flashback until end of turn. The flashback cost is equal to its mana cost." Lost on any zone change.</summary>
public record GrantedFlashback(int OnTurn) : GameComponent;

/// <summary>A prepare card's spell half (CR: "a preparation card is a creature card in every zone"): its face and its text.</summary>
public record PrepareSpellComponent(Cards.CardFace Face, SpellEffectsComponent Text)
	: GameComponent;

/// <summary>
/// An adventurer card's Adventure (CR 715; Brazen Borrower // Petty Theft: "Return target nonland permanent an
/// opponent controls to its owner's hand."): its face and its text. The card is cast AS it from the hand
/// (CastSpellAction.Adventure, Card.AsAdventure); resolved, it is exiled and its caster may cast the creature from
/// there (FinishSpellAction). Prepare's shape, not its rules: a prepare spell is a copy in exile, never the card.
/// </summary>
public record AdventureComponent(Cards.CardFace Face, SpellEffectsComponent Text) : GameComponent;

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
/// Necromancy: "You may cast this spell as though it had flash. If you cast it any time a sorcery couldn't have been
/// cast, the controller of the permanent it becomes sacrifices it at the beginning of the next cleanup step." — read
/// by Priority.AtInstantSpeed; AtNextCleanup is a delayed trigger's effects, put on the permanent as it enters
/// (EnterFromStackAction) when the cast was so (OnStackComponent.NotAtSorcerySpeed).
/// </summary>
public record CastAsThoughItHadFlash(ImmutableList<GameAction> AtNextCleanup) : GameComponent;

/// <summary>
/// Proft: "You can't cast this spell unless [When]." A restriction, not a cost — read by
/// Playable.CanCastFromHere, so the card is never offered (MtgCore's CastRestrictionComponent).
/// </summary>
public record CastOnlyIf(Condition When) : GameComponent;

/// <summary>The events a numeric replacement can change — each the ONE place it happens.</summary>
public enum ReplacedEvent
{
	Damage, // Rules.Damage.Deal (and Excess)
	Counters, // MagicState.PutCounters — every kind, entering with them included
	Draw, // MagicState.Draw
	Mill, // Mill.Top
	Tokens, // CreateTokens and CreateTokenCopy — the player they are created under
}

/// <summary>
/// "If [an event] would happen, [twice that many / that much plus N] instead" (CR 614) — on a permanent
/// (or a "this turn" object, ThisTurnReplacement); Rules.Replacements applies every one where the event
/// happens. Filters are asked from its holder's side. Tomik: "a source you control would deal noncombat
/// damage to an opponent or a permanent an opponent controls … plus 1" (Damage, Source = Is.Yours,
/// Affected = Is.Opponents, Combat = false, Plus = 1); Torbran: "a red source you control … plus 2";
/// Yoshimaru: "+1/+1 counters would be put on a creature you control … plus one"; Geordi: "twice that many
/// charge counters on an artifact you control"; Dr. M'Benga: every kind on a creature you control, twice;
/// Mr. Spock: "If you would draw a card, draw two cards instead" (Draw, Affected = Is.Yours, Times = 2);
/// Bruvac: "If an opponent would mill one or more cards, they mill twice that many"; Elspeth, Storm Slayer: "If
/// one or more tokens would be created under your control, twice that many of those tokens are created instead"
/// (Tokens, Affected = Is.Yours, Times = 2).
/// </summary>
public record NumericReplacement(ReplacedEvent Event) : GameComponent
{
	/// <summary>Damage: the source dealing it — Torbran: "a red source you control". None: any.</summary>
	public Filters.Filter? Source { get; init; }

	/// <summary>What it happens to: the player drawing or milling, the permanent getting counters, what is dealt damage (a player or a permanent). None: any.</summary>
	public Filters.Filter? Affected { get; init; }

	/// <summary>Counters: which kind (CounterKinds) — Geordi's "charge". None: every kind.</summary>
	public string? CounterKind { get; init; }

	/// <summary>Damage: Tomik's "noncombat" (false). None: either.</summary>
	public bool? Combat { get; init; }

	/// <summary>"Twice that many": 2.</summary>
	public int Times { get; init; } = 1;

	/// <summary>"That much plus N".</summary>
	public int Plus { get; init; }

	/// <summary>
	/// Jace, Wielder of Mysteries: "while your library has no cards in it"; Narset, Parter of Veils: "can't draw
	/// more than one card each turn" (they have drawn one) — judged from its holder's side as the event would
	/// happen, Ref.It what it happens to (the player drawing). None: always.
	/// </summary>
	public Condition? If { get; init; }

	readonly ImmutableList<GameAction>? instead;

	/// <summary>
	/// Draw only — what happens in place of ONE card's draw (MagicState.Draw), run as the rule's controller; the
	/// numbers don't apply. Hullbreacher: "instead you create a Treasure token"; Jace: "you win the game instead";
	/// Narset: "can't draw" is empty — nothing instead, and it is asked before any other. On a PERMANENT only:
	/// one in a ThisTurnReplacement is not read (Replacements.InsteadOfDraw) — widen it there when a card
	/// says "this turn, if [a player] would draw".
	/// </summary>
	public ImmutableList<GameAction>? Instead
	{
		get => instead;
		init =>
			instead =
				value is null || Event == ReplacedEvent.Draw
					? value
					: throw new ArgumentException($"An Instead only fits Draw, not {Event}");
	}

	/// <summary>
	/// Hullbreacher: "except the first one they draw in each of their draw steps" — the draw step's own draw is
	/// left alone. ponytail: when the turn's draw didn't happen (the starting player's first turn), another
	/// card drawn in that draw step isn't excepted; count the draw step's draws in the tally if it matters.
	/// </summary>
	public bool ExceptTheTurnsDraw { get; init; }
}

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
/// Troll of Khazad-dûm: "This creature can't be blocked except by three or more creatures." — menace's "two or more"
/// as a number (Combat.MinimumBlockers). Not a CantBeBlockedBy option: that asks one blocker at a time, this counts them.
/// </summary>
public record MinimumBlockers(int N) : GameComponent;

/// <summary>
/// Brazen Borrower: "This creature can block only creatures with flying." — the BLOCKER's filter, asked of each
/// attacker from its side (Combat.CanBlock); CantBeBlockedBy is the attacker's, asked of its blockers.
/// </summary>
public record CanBlockOnly(Filters.Filter Attackers) : GameComponent;

/// <summary>
/// Species 8472: "This creature can't be the target of abilities from artifact sources your opponents
/// control." — abilities only, never spells; the filter asked of the ability's source on the battlefield
/// (TargetSpec). ponytail: a source sacrificed to pay the cost is gone by resolution, so the target is
/// legal again then — keep the source as it was (StackAbility.Source) if a card ever cares.
/// </summary>
public record CantBeTargetedByAbilitiesFrom(Filters.Filter Sources) : GameComponent;

/// <summary>Samut: "Instant and sorcery spells you control have split second."</summary>
public record YourInstantsAndSorceriesHaveSplitSecond : GameComponent;

/// <summary>Tomik, Orzhov Lawmage: "Planeswalkers you control have 'No more than one creature can attack this planeswalker each combat.'"</summary>
public record OneAttackerPerPlaneswalkerYouControl : GameComponent;

/// <summary>Infinite Coursework: "Enchanted creature loses all abilities" — Characteristics.HasLostAllAbilities.</summary>
public record EnchantedLosesAllAbilities : GameComponent
{
	/// <summary>
	/// The Wondrous Wasp: "It loses all abilities for as long as The Wondrous Wasp remains on the battlefield" —
	/// this object (id, incarnation: CR 400.7) in place of what the holder is attached to. Stamped on the source
	/// by ModifyUntilEndOfTurn.WhileSourceRemains, as Lwaxana's link is: an effect's result, so it holds while the
	/// source has lost its own abilities, and goes as the source leaves (Card.IsStatus).
	/// </summary>
	public Filters.TheObject? Object { get; init; }
}

/// <summary>De-evolve: "Enchanted creature … is a Salamander. (It loses all other creature types.)" — Characteristics.Subtypes.</summary>
public record EnchantedIsCreatureType(string Subtype) : GameComponent;

/// <summary>
/// MC-6.9: "You may cast this card from your graveyard / from exile" — the card's own permission, read by
/// Playable.CanCastFromHere. Storied Veteran: (Graveyard) { Finality } — "If you do, it enters with a finality
/// counter on it"; unlike flashback it isn't exiled leaving the stack (MagicState.MoveCard), so it resolves onto
/// the battlefield (StackResolution). Khan, Vengeful Heart: (Exile) — your own exile; face down it has no
/// abilities (CR 708.2), so not then.
/// </summary>
public record MayCastThisFrom(ZoneType From) : GameComponent
{
	public bool Finality { get; init; }

	/// <summary>
	/// Escape (CR 702.138) — From the Catacombs: "Escape—{3}{B}{B}, Exile five other cards from your graveyard." — the
	/// costs beside its mana cost, from the graveyard only (CastSpellAction.CostsOf); not flashback, so it goes back to
	/// the graveyard as it resolves.
	/// </summary>
	public ImmutableList<AdditionalCost> Costs { get; init; } = [];

	/// <summary>The card's own permission from <paramref name="zone"/> — what it printed, not what it was granted.</summary>
	public static bool On(Cards.Card card, ZoneType zone) =>
		card.Components.OfType<MayCastThisFrom>().Any(m => m.From == zone);
}

/// <summary>
/// MC-6.9: "[Once during each of your turns,] you may play [cast] [cards] from your graveyard / the top of your
/// library [/ outside the game]" — a permanent's permission for its controller, during their turn, read by
/// Playable.ZonePermission for casts and land plays alike (What filters which: a "cast" says !Is.Land).
/// Overflow Submatrix: (Graveyard, Is.Artifact &amp; Is.Creature) { OncePerTurn, Finality } — "that artifact creature
/// enters with a finality counter on it". The Holodeck: (Graveyard, a permanent card) { OncePerTurn, OutsideTheGame }
/// — the sideboard too, when the game has one. Nog's gained "You may play lands from your graveyard": (Graveyard,
/// Is.Land). Zefram Cochrane: "Once during each of your turns, you may cast an artifact spell from the top of your
/// library." (Library, Is.Artifact &amp; !Is.Land) { OncePerTurn }. Captain Pike: "You may play lands from the top of
/// your library." (Library, Is.Land). A once-each-turn use stamps the permanent (Card.PermissionUsedOn). Not
/// flashback: a spell cast from the graveyard this way isn't exiled leaving the stack (OnStackComponent.ByPermission).
/// Null Summoner: "As long as [threshold], you may cast the exiled card, and mana of any type can be spent to cast that
/// spell." (Exile, Is.ExiledBySource) { Of = Ref.EachPlayer, AnyTime, If, AnyType } — the card it exiled, whoever owns it, while that
/// same object is on the battlefield (CR 607.2a). Sen Triplets: "You may play lands and cast spells from that player's
/// hand this turn." — (Hand, any card) { Of = Ref.Target(0) } in a ThisTurn: the whole zone while it lasts.
/// </summary>
public record MayPlayFromZone(ZoneType From, Filters.Filter What) : ThisTurnEffect
{
	public bool OncePerTurn { get; init; }
	public bool Finality { get; init; }
	public bool OutsideTheGame { get; init; }

	/// <summary>Whose zone; unset is yours. Resolved from the holder's side; in a ThisTurn, bound as it is made (OwnerId).</summary>
	public Ref? Of { get; init; }

	/// <summary>Of as it resolved, for a "this turn" permission (Sen Triplets: a target can't be read later).</summary>
	public int OwnerId { get; init; }

	/// <summary>Null Summoner: no "during your turn" — at whatever speed the card itself may be cast.</summary>
	public bool AnyTime { get; init; }

	/// <summary>Null Summoner: "As long as [threshold]" — judged from the holder's side.</summary>
	public Condition? If { get; init; }

	/// <summary>Null Summoner: "mana of any type can be spent to cast that spell" — Costs.Of.</summary>
	public bool AnyType { get; init; }

	/// <summary>
	/// Serra Paragon: "If you do, it gains 'When this permanent is put into a graveyard from the battlefield, exile
	/// it and you gain 2 life.'" — a triggered ability the permanent has from entering until it leaves, carried as
	/// Finality is (a spell: OnStackComponent.Gains; a land: PlayLandAction) into EnterBattlefield.
	/// </summary>
	public TriggeredAbilityComponent? Gains { get; init; }

	/// <summary>
	/// Underworld Breach: "Each nonland card in your graveyard has escape. The escape cost is equal to the card's mana
	/// cost plus exile three other cards from your graveyard." — (Graveyard, !Is.Land) { AnyTime, Costs =
	/// [ExileFromGraveyardCost(!Is.Source) { Count = 3 }] }: what a cast by this rule costs beside its mana
	/// (CastSpellAction.CostsOf). Not flashback: an escaped spell isn't exiled as it leaves the stack.
	/// </summary>
	public ImmutableList<AdditionalCost> Costs { get; init; } = [];

	/// <summary>
	/// Bolas's Citadel: "If you cast a spell this way, pay life equal to its mana value rather than pay its mana cost."
	/// — a spell cast by this rule is cast without paying its mana cost, paying Costs instead (Playable.FreeCast); the
	/// paid cast is refused (CastSpellAction.ValidateAdd). A land is played as ever.
	/// </summary>
	public bool Free { get; init; }
}

/// <summary>
/// "If [Cause] causes a triggered ability of [WhoseAbilities] to trigger, that ability triggers an
/// additional time" (CR 603.2d) — read by Triggers.Collect. Dr. Beverly Crusher: "If you gaining life
/// causes a triggered ability of a permanent you control to trigger" (YouGainLife, Is.Yours — her own
/// included); Isshin: "a creature attacking" (Attacked, and AttackersDeclared: "whenever you attack");
/// Drivnod: "a creature dying". The Cause is matched by the one Trigger matcher, from this permanent's
/// side (By, Subject); null is any cause. WhoseAbilities is asked of the ability's source (as it last
/// was, if it is dying).
/// </summary>
public record TriggersAdditionalTime(Trigger? Cause, Filters.Filter WhoseAbilities) : GameComponent
{
	/// <summary>
	/// Lwaxana Troi, Charming Guest: "For as long as Lwaxana Troi remains tapped, if a triggered ability
	/// of that creature triggers, …" — stamped on her by her ability (TheObject: that object of the
	/// creature, CR 400.7), an effect's result rather than her ability; gone as she untaps
	/// (MagicState.Untap) or leaves (Card.IsStatus).
	/// </summary>
	public bool UntilUntapped { get; init; }
}

/// <summary>
/// Lwaxana Troi, Charming Guest: "You may choose not to untap Lwaxana Troi during your untap step." —
/// asked by the untap step (TurnStructure), only while her link (TriggersAdditionalTime.UntilUntapped) is
/// to a creature still there: without one, untapping costs nothing.
/// </summary>
public record MayChooseNotToUntap : GameComponent;

/// <summary>
/// Scotty, Miracle Worker: "During your main phase, each card in your graveyard has beam me up. The beam
/// me up cost is equal to that spell's mana cost. Scotty can't be beamed up to cast a spell this way." —
/// read by Costs.Flashback, for instants and sorceries (only they can be cast with beam me up).
/// ponytail: a card with its own beam me up or flashback uses its own; offer both if a card makes the
/// choice matter.
/// </summary>
public record GraveyardSpellsHaveBeamMeUp : GameComponent;

/// <summary>Quark, Humble Proprietor: "You can't sacrifice Treasure tokens." — MagicState.CanSacrifice, asked by every sacrifice (costs, choices, Treasure's own mana).</summary>
public record CantSacrificeTreasures : GameComponent;

/// <summary>Captain Kathryn Janeway: "You may play an additional land on each of your turns." — MagicState.LandDrops.</summary>
public record AdditionalLandEachTurn : GameComponent;

/// <summary>
/// Georgiou, Terran Emperor: "You don't lose the game for having 0 or less life." — the state-based
/// check (TurnStructure); she leaves, and the next check takes the game.
/// </summary>
public record DontLoseForZeroLife : GameComponent;

/// <summary>
/// Zefram Cochrane: "You may look at the top card of your library any time." — MagicState.TopIsKnownToOwner:
/// the search doesn't reshuffle it, the table's log names it. ponytail: a log line, not the card face
/// up beside your library — TBL-6 (Shayne, 2026-11-01).
/// </summary>
public record MayLookAtTopOfLibrary : GameComponent;

/// <summary>Marooned: "Enchanted permanent … can't be beamed up." — read by ReturnToHandCost: never returned to pay one.</summary>
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

	/// <summary>
	/// The object whose ability set it up, as it was then (CR 603.7f: the delayed trigger's source) — Phelia,
	/// Exuberant Shepherd: "… return that card … put a +1/+1 counter on Phelia". Its "this" (Ref.Source) is the
	/// card it is on; this is its Ref.EventObject — nothing once that object has left (CR 400.7).
	/// </summary>
	public Cards.Card? MadeBy { get; init; }
}

/// <summary>
/// Dr. McCoy, Surly Surgeon: "If another creature you control with a +1/+1 counter on it would be
/// destroyed, instead remove all +1/+1 counters from it and all damage already dealt to it is healed."
/// — MagicState.Destroy, every destruction.
/// </summary>
public record CountersRemovedInsteadOfDestruction : GameComponent;

/// <summary>Garruk, Veiled Butcher: "If a creature an opponent controls would die, exile it instead." — MagicState.MoveCard.</summary>
public record OpponentsCreaturesExiledInsteadOfDying : GameComponent;

/// <summary>
/// A type change while its source is on the battlefield, on what <see cref="Scope"/> matches — Puppet
/// Crafting: "Enchanted permanent is a [Subtype] creature with base power and toughness P/T in addition to
/// its other types." (Is.AttachedToSource, Add = Creature); Mogis, God of Slaughter: "As long as your
/// devotion to black and red is less than seven, Mogis isn't a creature." (Is.Source, Remove = Creature,
/// If). Stamped (Card.Animated), put right by the state-based check and as a permanent enters
/// (Attachments.Retype) — every rule reads card.Is(Creature).
/// </summary>
public record BecomesType(Filters.Filter Scope) : GameComponent
{
	public Cards.CardType Add { get; init; }

	/// <summary>Mogis: "isn't a creature".</summary>
	public Cards.CardType Remove { get; init; }

	/// <summary>Puppet Crafting: "a Construct", in addition to its other types.</summary>
	public string? Subtype { get; init; }

	/// <summary>Puppet Crafting: "with base power and toughness 5/5"; unset keeps its own.</summary>
	public int? Power { get; init; }

	public int? Toughness { get; init; }

	/// <summary>Mogis: "As long as …" — asked of the source.</summary>
	public Condition? If { get; init; }
}

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
/// MC-6.10: "[permanents] enter tapped / untapped / with N additional [kind] counters / tapped and attacking" —
/// a permanent's rule for OTHER permanents entering, never itself (CR 614.12; Metallic Mimic's ruling), read by
/// MagicState.EnterBattlefield. The filter is asked from the rule's holder (Is.Yours: its controller's).
/// Root Maze: (Is.Artifact | Is.Land) { Tapped }. Warp Speed: "Lands you control enter untapped." (Is.Land &amp;
/// Is.Yours) { Untapped } — over every "enters tapped": the land's own, Root Maze's, a token created tapped,
/// "onto the battlefield tapped" (Shayne, 2026-11-01). Zefram Cochrane: "Spacecraft you control enter with two
/// additional charge counters on them." { CounterKind, Counters }. Kira Nerys, Firebrand: "If a creature you
/// control would enter while you control an attacking creature, instead it enters tapped and attacking."
/// (Is.Creature &amp; Is.Yours) { Attacking } — never declared, so "whenever … attacks" doesn't trigger (CR 508.4).
/// ponytail: Kira's attacks the opponent, never a planeswalker — the rules let its controller choose; make it a
/// choice if a planeswalker deck wants it. A card's OWN "this enters tapped / with counters" is its own
/// component (EntersTappedComponent, EntersWithCountersComponent): its replacement on itself.
/// </summary>
public record PermanentsEnter(Filters.Filter Which) : GameComponent
{
	/// <summary>
	/// Containment Priest: "If a nontoken creature would enter and it wasn't cast, exile it instead." — decided
	/// before the entry: it goes to its owner's exile and never enters (no event, nothing of the entry).
	/// </summary>
	public bool ExiledUnlessCast { get; init; }

	public bool Tapped { get; init; }
	public bool Untapped { get; init; }
	public string? CounterKind { get; init; }
	public int Counters { get; init; }
	public bool Attacking { get; init; }
}

/// <summary>
/// Flesh Duplicate: "You may have this creature enter as a copy of any creature on the battlefield,
/// except it has vanishing N if that creature doesn't have vanishing." Applied by
/// MagicState.EnterBattlefield: the choice as it enters, then BecomeCopyOfChosenAction.
/// </summary>
public record MayEnterAsCopy : GameComponent
{
	/// <summary>"Except it has vanishing N if that creature doesn't have vanishing" — 0: no exception.</summary>
	public int VanishingIfNone { get; init; }

	/// <summary>Dominion Saboteur: "a copy of any artifact or creature on the battlefield" — what it may copy; unset is a creature.</summary>
	public Filters.Filter? Of { get; init; }

	/// <summary>Dominion Saboteur: "except it isn't legendary".</summary>
	public bool NotLegendary { get; init; }

	/// <summary>Phyrexian Metamorph: "except it's an artifact in addition to its other types" — the type added to the copy's.</summary>
	public Cards.CardType AlsoType { get; init; }

	/// <summary>Dominion Saboteur: "it enters with additional counters on it equal to the same number and kinds of counters the copied permanent has on it".</summary>
	public bool WithCounters { get; init; }

	/// <summary>Dominion Saboteur: "you may" offered as may — a 3/3 that copies nothing is still a 3/3 (Flesh Duplicate's must is its shortcut).</summary>
	public bool Optional { get; init; }
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
/// instead." — your graveyard is the one you own; read by MagicState.MoveCard, so nothing dies. On a permanent, or
/// in a ThisTurn: Yawgmoth's Will's "If a card would be put into your graveyard from anywhere this turn, exile that
/// card instead." (What = !Is.Token).
/// </summary>
public record ExileInsteadOfYourGraveyard : ThisTurnEffect
{
	/// <summary>Which of what would go there — Yawgmoth's Will's "a card" (!Is.Token); unset, anything.</summary>
	public Filters.Filter? What { get; init; }

	/// <summary>
	/// Dauthi Voidwalker: "If a card would be put into an opponent's graveyard from anywhere" — whose graveyard, asked of
	/// its owner from the holder's side (Is.Opponents); unset, yours. On a permanent only: a "this turn" one is read from
	/// the card's owner's command zone alone.
	/// </summary>
	public Filters.Filter? Whose { get; init; }

	/// <summary>Dauthi Voidwalker: "instead exile it with a void counter on it" — put on as it is exiled.</summary>
	public string? CounterKind { get; init; }
}

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
/// "You may cast [spells] from your hand without paying their mana costs" — Omnipresence: "spells with
/// mana value less than or equal to the number of creatures you control" (Is.ManaValueAtMost(
/// Count.CreaturesYouControl)), asked from its holder's side (CastSpellAction.WithoutPayingManaCost,
/// Playable.FreeCosts). On the card itself (What = Is.Source) it is the card's own alternative cost,
/// from wherever it is cast — Flare of Fortitude, with <see cref="Costs"/>.
/// </summary>
public record MayCastFromHandFree(Filters.Filter What) : GameComponent
{
	/// <summary>
	/// Flare of Fortitude: "You may sacrifice a nontoken white creature rather than pay this spell's mana
	/// cost." — paid instead (CastSpellAction.SacrificeId); a free cast with a cost is offered beside the paid one.
	/// Force of Will: [PayLifeCost(1), ExileFromHandCost(blue)]; Daze: [ReturnToHandCost(Island)].
	/// </summary>
	public ImmutableList<AdditionalCost> Costs { get; init; } = [];

	/// <summary>Snuff Out: "If you control a Swamp, you may pay 4 life rather than …"; Force of Negation: "If it's not your turn" — judged as it is cast.</summary>
	public Condition? If { get; init; }

	/// <summary>Baleful Mastery: "You may pay {1}{B} rather than pay this spell's mana cost." — mana paid instead (AlternativeCostWasPaid reads it).</summary>
	public ManaCost? Mana { get; init; }

	/// <summary>
	/// Emerge (CR 702.119, Crabomination): "You may cast this spell by sacrificing an artifact and paying the emerge cost
	/// reduced by that artifact's mana value" — the Mana less the mana value of the card the cast's SacrificeCost picked
	/// (CastSpellAction.SacrificeId), generic only (CR 601.2f): Scripting.Emerge.
	/// </summary>
	public bool LessTheSacrificed { get; init; }

	/// <summary>
	/// Overload (CR 702.96) — Damn: "Overload {2}{W}{W} (You may cast this spell for its overload cost. If you do, change
	/// "target" in its text to "each.")" — cast for this cost, the spell has no target words (StackResolution.TextOf), and
	/// what "each" reads is the script's If(AlternativeCostWasPaid) over Ref.Permanents (Scripting.Overload).
	/// </summary>
	public bool Overload { get; init; }

	/// <summary>
	/// Warp (Quantum Riddler): "You may cast this card from your HAND by paying [cost]" — the zones the card's own
	/// alternative cost is cast from, the CASTER's (never another's hand under Sen Triplets); empty: wherever it is cast
	/// (Flare of Fortitude).
	/// </summary>
	public ImmutableList<ZoneType> From { get; init; } = [];

	/// <summary>
	/// Warp (Quantum Riddler): "If this spell's warp cost was paid, exile the permanent this spell becomes at the
	/// beginning of the next end step" — set up as the spell resolves (EnterFromStackAction), a delayed trigger on the
	/// permanent, controlled by the spell's controller (CR 603.7d): no trigger to answer as it enters (CR 702.185a).
	/// </summary>
	public ImmutableList<GameAction> PaidAtNextEndStep { get; init; } = [];
}

/// <summary>On a card a permanent exiled for a linked ability (CR 607.2a) — which object of it.</summary>
public record ExiledBy(int SourceId, int SourceIncarnation) : GameComponent;
