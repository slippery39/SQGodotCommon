using System.Collections;
using System.Collections.Immutable;
using System.Reflection;
using ImmutableGameObjects;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Filters;
using MagicCore.Targeting;
using MagicCore.Turns;
using Is = MagicCore.Filters.Is; // the filter vocabulary, over NUnit's Is
using Nu = NUnit.Framework.Is;

namespace MagicCore.Tests.Filters;

/// <summary>
/// THE MIGRATION TABLE (MagicCore/FilterRefactorPlan.md) — the guide for phases 2–5. Every RefKind,
/// TargetSpec field, static scope (Affects + Only*) and filter-shaped TriggerEvent, written as a
/// zone and a filter and checked on <see cref="FilterBoard"/> — the same objects, in the same order.
/// Phase 1 checked every row against the OLD code; a phase that replaces a vocabulary moves its rows
/// onto the new one (Ref and TargetSpec now: the new code against the test's own zone walk), and
/// checks its card scripts old-vs-new with a probe (FilterRefactorPlan.md). What isn't a filter is
/// listed with why, and a completeness test fails when a value is in neither.
/// Each row must pick out some but not all of its candidates, so the board really tells them apart.
/// </summary>
public class FilterMigrationTests
{
	static readonly FilterBoard B = FilterBoard.Build();

	/// <summary>Where candidates come from — never in a filter. Phase 2's TargetSpec `From`, phase 3's `All(zone, …)`.</summary>
	public enum Zone
	{
		Players,
		Battlefield,
		Stack,
		YourGraveyard,
		AnyGraveyard,
		YourHand,
		TargetPlayersBattlefield, // phase 3's open question: the zone comes from a Ref
	}

	// ---------------------------------------------------------------- Ref (phase 3)

	static IEnumerable<TestCaseData> RefRows()
	{
		TestCaseData Row(string name, Ref old, Zone from, Filter? filter) =>
			new TestCaseData(old, from, filter).SetName($"Ref_{name}");
		var bf = Zone.Battlefield;
		yield return Row(
			"CreaturesYouControl",
			Ref.CreaturesYouControl,
			bf,
			Is.Creature & Is.Yours
		);
		yield return Row("LandsYouControl", Ref.LandsYouControl, bf, Is.Land & Is.Yours);
		yield return Row(
			"OpponentsPermanents",
			Ref.OpponentsPermanents(CardType.Creature | CardType.Planeswalker),
			bf,
			Is.Type(CardType.Creature | CardType.Planeswalker) & Is.Opponents
		);
		yield return Row("AllPermanents", Ref.AllCreatures, bf, Is.Creature);
		yield return Row("YourGraveyard", Ref.CardsInYourGraveyard, Zone.YourGraveyard, null);
		yield return Row(
			"YourGraveyardOfType",
			Ref.YourGraveyard(CardType.Creature),
			Zone.YourGraveyard,
			Is.Creature
		);
		yield return Row("YourHand", Ref.CardsInYourHand, Zone.YourHand, null);
		yield return Row(
			"YourPermanentsWithCounters",
			Ref.YourCreaturesWithCounters,
			bf,
			Is.Creature & Is.Yours & Is.WithPlusOneCounter
		);
		yield return Row(
			"AllPermanentsExceptYourTokens",
			Ref.CreaturesExceptYourTokens,
			bf,
			Is.Creature & !(Is.Token & Is.Yours)
		);
		yield return Row("YourTokens", Ref.TokensYouControl, bf, Is.Token & Is.Yours);
		yield return Row(
			"YourOtherPermanents",
			Ref.OtherCreaturesYouControl,
			bf,
			Is.Creature & Is.Yours & !Is.Source
		);
		yield return Row(
			"TargetPlayersPermanents",
			Ref.PermanentsOfTargetPlayer(CardType.Creature, 0),
			Zone.TargetPlayersBattlefield,
			Is.Creature
		);
		yield return Row(
			"YourCreaturesAttackingAPlayer",
			Ref.Permanents(Is.Yours & Is.AttackingAPlayer),
			bf,
			Is.Yours & Is.AttackingAPlayer // the old kind never asked "creature": only creatures attack
		);
		yield return Row(
			"Subtype",
			Ref.Permanents(Is.Creature & Is.Yours & !Is.Source & Is.Subtype("Wizard")),
			bf,
			Is.Creature & Is.Yours & !Is.Source & Is.Subtype("Wizard")
		);
		yield return Row(
			"ExceptEventCard", // Roiling Canopy: "five OTHER Forests" (Gates here)
			Ref.Permanents(Is.Land & Is.Yours & Is.Subtype("Gate") & !Is.EventCard),
			bf,
			Is.Land & Is.Yours & Is.Subtype("Gate") & !Is.EventCard
		);
	}

	/// <summary>Real referents — "you", "target", "its controller". They stay Refs; no filter replaces them.</summary>
	static readonly RefKind[] Referents =
	[
		RefKind.You,
		RefKind.EachOpponent,
		RefKind.EachPlayer,
		RefKind.Target,
		RefKind.ControllerOfTarget,
		RefKind.OwnerOfTarget,
		RefKind.Source,
		RefKind.SourceHost, // keeps the Aura's last-known information
		RefKind.EventCard,
		RefKind.EventObject,
		RefKind.ControllerOfEventCard,
		RefKind.EachTarget, // the chosen targets themselves
		RefKind.ActivePlayer, // Peace in Our Time: whose turn it is
		RefKind.It, // Bristly Bill: the object an effect is acting on
		RefKind.ThisWay, // Decree of Pain: what the effect did its thing to
	];

	[TestCaseSource(nameof(RefRows))]
	public void RefRow(Ref reference, Zone from, Filter? filter)
	{
		var context = ImmutableDictionary<string, object>
			.Empty.Add(ContextKeys.Controller, B.Me)
			.Add(ContextKeys.Source, B["Src"])
			.Add(ContextKeys.Targets, ImmutableArray.Create(B.Them))
			.Add(ContextKeys.EventCard, B["Grove"]);
		// Phase 1 proved the old Ref equal to this zone + filter; now the new one must be.
		// A Ref lists YOUR objects first.
		Same(
			reference.Resolve(B.S, context),
			Select(B.Me, B.Context(eventCard: "Grove"), (from, filter)),
			In(from, B.Me),
			filter is not null
		);
	}

	[Test]
	public void EveryRefKind_IsARowOrAReferent() =>
		Assert.That(
			Enum.GetValues<RefKind>()
				.Except(RefRows().Select(r => ((Ref)r.Arguments[0]!).Kind))
				.Except(Referents),
			Nu.Empty
		);

	// ---------------------------------------------------------------- TargetSpec (phase 2)

	static IEnumerable<TestCaseData> TargetRows()
	{
		TestCaseData Row(string name, TargetSpec spec, params (Zone, Filter?)[] from) =>
			new TestCaseData(spec, from).SetName($"Target_{name}");
		var bf = Zone.Battlefield;
		yield return Row("Creature", TargetSpec.Creature, (bf, Is.Creature));
		yield return Row(
			"CreatureOrPlaneswalker",
			TargetSpec.Permanent(Is.Creature | Is.Planeswalker),
			(bf, Is.Creature | Is.Planeswalker)
		);
		yield return Row(
			"AnyTarget",
			TargetSpec.AnyTarget(),
			(Zone.Players, null),
			(bf, Is.Creature | Is.Planeswalker)
		);
		yield return Row("Player", TargetSpec.Player(), (Zone.Players, null));
		yield return Row("Permanent", TargetSpec.Permanent(), (bf, null));
		yield return Row(
			"SpellOrCreature", // phase 2: two clauses, since "spell" is a zone, not a property
			TargetSpec.Spell() with
			{
				Or = TargetSpec.Creature,
			},
			(bf, Is.Creature),
			(Zone.Stack, null)
		);
		yield return Row(
			"CardInYourGraveyard",
			TargetSpec.InYourGraveyard(),
			(Zone.YourGraveyard, null)
		);
		yield return Row(
			"CardInAnyGraveyard",
			TargetSpec.InAnyGraveyard(),
			(Zone.AnyGraveyard, null)
		);
		yield return Row("Spell", TargetSpec.Spell(), (Zone.Stack, null));
		yield return Row(
			"TriggeredAbilityOrColorlessSpell", // Consign to Memory; no ability is on this board's stack
			TargetSpec.Ability(Is.TriggeredAbility) with
			{
				Or = TargetSpec.Spell(Is.Colorless),
			},
			(Zone.Stack, Is.Colorless)
		);
		yield return Row(
			"WhoseYou",
			TargetSpec.Permanent(Is.Creature & Is.Yours),
			(bf, Is.Creature & Is.Yours)
		);
		yield return Row(
			"WhoseOpponent",
			TargetSpec.AnyTarget(Is.Opponents),
			(Zone.Players, Is.Opponents),
			(bf, (Is.Creature | Is.Planeswalker) & Is.Opponents)
		);
		yield return Row(
			"Another",
			TargetSpec.Permanent(Is.Creature & !Is.Source),
			(bf, Is.Creature & !Is.Source)
		);
		yield return Row(
			"Nonlegendary",
			TargetSpec.Permanent(Is.Creature & !Is.Legendary),
			(bf, Is.Creature & !Is.Legendary)
		);
		yield return Row(
			"WithSupertype",
			TargetSpec.InYourGraveyard(Is.Legendary),
			(Zone.YourGraveyard, Is.Legendary)
		);
		yield return Row(
			"OrSupertype", // Icy Reception: "creature or legendary spell"
			TargetSpec.Spell(Is.Creature | Is.Legendary),
			(Zone.Stack, Is.Creature | Is.Legendary)
		);
		yield return Row(
			"AnyOfColors",
			TargetSpec.Permanent(
				Is.Creature & (Is.Color(ManaColor.Blue) | Is.Color(ManaColor.Red))
			),
			(bf, Is.Creature & (Is.Color(ManaColor.Blue) | Is.Color(ManaColor.Red)))
		);
		yield return Row(
			"AnyOfTypes",
			TargetSpec.Permanent(Is.Type(CardType.Artifact | CardType.Enchantment)),
			(bf, Is.Type(CardType.Artifact | CardType.Enchantment))
		);
		yield return Row(
			"NotTypes",
			TargetSpec.Permanent(!Is.Type(CardType.Creature | CardType.Land)),
			(bf, !Is.Type(CardType.Creature | CardType.Land))
		);
		yield return Row(
			"NotSubtype", // Puppet Crafting: "non-Aura enchantment"
			TargetSpec.Permanent(Is.Enchantment & !Is.Subtype("Aura")),
			(bf, Is.Enchantment & !Is.Subtype("Aura"))
		);
		yield return Row(
			"MaxPower",
			TargetSpec.Permanent(Is.Creature & Is.PowerAtMost(2)),
			(bf, Is.Creature & Is.PowerAtMost(2))
		);
		yield return Row(
			"MinToughness",
			TargetSpec.Permanent(Is.Creature & Is.ToughnessAtLeast(4)),
			(bf, Is.Creature & Is.ToughnessAtLeast(4))
		);
		yield return Row(
			"WithKeyword",
			TargetSpec.Permanent(Is.Creature & Is.With("Flying")),
			(bf, Is.Creature & Is.With("Flying"))
		);
		yield return Row(
			"EnteredThisTurn",
			TargetSpec.Permanent(Is.Creature & Is.EnteredThisTurn),
			(bf, Is.Creature & Is.EnteredThisTurn)
		);
		yield return Row(
			"AttackedThisTurn",
			TargetSpec.Permanent(Is.Creature & Is.AttackedThisTurn),
			(bf, Is.Creature & Is.AttackedThisTurn)
		);
		yield return Row(
			"Attacking",
			TargetSpec.Permanent(Is.Creature & Is.Attacking),
			(bf, Is.Creature & Is.Attacking)
		);
		yield return Row(
			"InCombat",
			TargetSpec.Permanent(Is.Creature & Is.InCombat),
			(bf, Is.Creature & Is.InCombat)
		);
		yield return Row(
			"WithPlusOneCounter",
			TargetSpec.Permanent(Is.Creature & Is.WithPlusOneCounter),
			(bf, Is.Creature & Is.WithPlusOneCounter)
		);
		yield return Row(
			"MaxManaValueOnTheStack", // X counts there
			TargetSpec.Spell(Is.ManaValueAtMost(2)),
			(Zone.Stack, Is.ManaValueAtMost(2))
		);
		yield return Row(
			"MaxManaValueInAGraveyard",
			TargetSpec.InYourGraveyard(Is.ManaValueAtMost(2)),
			(Zone.YourGraveyard, Is.ManaValueAtMost(2))
		);
		yield return Row(
			"MinManaValue",
			TargetSpec.InAnyGraveyard(Is.ManaValueAtLeast(4)),
			(Zone.AnyGraveyard, Is.ManaValueAtLeast(4))
		);
	}

	[TestCaseSource(nameof(TargetRows))]
	public void TargetRow(TargetSpec spec, (Zone, Filter?)[] from) =>
		// Phase 1 proved the old TargetSpec equal to these zones + filters; now the new one must be.
		// Candidates in TargetSpec's order: players, permanents (player 1's first), stack, graveyards.
		Same(
			spec.Candidates(B.S, B.Me, B["Src"]),
			Select(B.S.Player1Id(), B.Context(), from),
			from.SelectMany(f => In(f.Item1, B.S.Player1Id())),
			from.Any(f => f.Item2 is not null)
		);

	[Test]
	public void EveryFrom_IsARow()
	{
		IEnumerable<Targeting.From> Zones(TargetSpec? t) =>
			t is null ? [] : [t.From, .. Zones(t.Or)];
		Assert.That(
			Enum.GetValues<Targeting.From>()
				.Except(TargetRows().SelectMany(r => Zones((TargetSpec)r.Arguments[0]!))),
			Nu.Empty
		);
	}

	// ---------------------------------------------------------------- statics (phase 4)

	// Phase 1 proved each old Affects value and Only* field equal to these filters; phase 4 made the
	// filter the static's scope. A row now checks Characteristics' own AppliesTo against the filter,
	// so the source's controller and id reach it ("you", "this", "equipped creature").
	static IEnumerable<TestCaseData> StaticRows()
	{
		TestCaseData Row(string name, Filter f, string source = "Src") =>
			new TestCaseData(f, source).SetName($"Static_{name}");
		yield return Row("Self", Is.Source);
		yield return Row("CreaturesYouControl", Is.Creature & Is.Yours);
		yield return Row("OtherCreaturesYouControl", Is.Creature & Is.Yours & !Is.Source);
		yield return Row("LandsYouControl", Is.Land & Is.Yours);
		yield return Row("AttachedCreature", Is.AttachedToSource, source: "Sword");
		yield return Row(
			"OnlyWithCounter", // Gallia
			Is.Creature & Is.Yours & !Is.Source & Is.WithPlusOneCounter
		);
		yield return Row("OnlySubtype", Is.Creature & Is.Yours & Is.Subtype("Wizard"));
		yield return Row("OnlyTokens", Is.Creature & Is.Yours & Is.Token);
		yield return Row("OnlyTypes", Is.Artifact & Is.Creature & Is.Yours); // Vraska
		yield return Row(
			"OnlyPowerOrToughnessAtMost", // Tetsuko: a keyword static may read P/T
			Is.Creature & Is.Yours & (Is.PowerAtMost(1) | Is.ToughnessAtMost(1))
		);
	}

	static readonly MethodInfo AppliesTo = typeof(Characteristics).GetMethod(
		"AppliesTo",
		BindingFlags.NonPublic | BindingFlags.Static
	)!;

	[TestCaseSource(nameof(StaticRows))]
	public void StaticRow(Filter filter, string source)
	{
		var permanents = In(Zone.Battlefield, B.S.Player1Id()).Select(B.S.GetCard).ToList();
		var effect = new StaticEffectComponent(filter, "Shroud");
		Same(
			permanents
				.Where(c => (bool)AppliesTo.Invoke(null, [B.S, B.Card(source), effect, c])!)
				.Select(c => c.Id),
			permanents.Where(c => filter.Matches(B.S, c, B.Context(source))).Select(c => c.Id),
			permanents.Select(c => c.Id),
			filtered: true
		);
	}

	// Phase 4's guard. Characteristics asks a static's scope while computing P/T and keywords, so a
	// scope that reads keywords — or a P/T static's that reads P/T — would ask itself forever.
	[Test]
	public void APtStaticWhoseScopeReadsPt_IsRefused()
	{
		Assert.Throws<ArgumentException>(
			() => _ = new StaticEffectComponent(Is.Creature & Is.PowerAtMost(1)) { Power = 1 }
		);
		var anthem = new StaticEffectComponent(Is.Creature & Is.Yours) { Toughness = 1 };
		Assert.Throws<ArgumentException>(
			() => _ = anthem with { Affects = Is.Creature & Is.ToughnessAtLeast(4) }
		);
	}

	[Test]
	public void AScopeThatReadsKeywords_IsRefused() =>
		Assert.Throws<ArgumentException>(
			() => _ = new StaticEffectComponent(Is.Creature & Is.With("Flying"), "Haste")
		);

	[Test]
	public void AKeywordStaticMayReadPt_AndAPtStaticMayReadManaValue() =>
		Assert.DoesNotThrow(() =>
		{
			_ = new StaticEffectComponent(Is.Creature & Is.PowerAtMost(1), "Menace"); // Tetsuko
			_ = new StaticEffectComponent(Is.Creature & Is.ManaValueAtMost(2)) { Power = 1 };
		});

	// ---------------------------------------------------------------- triggers (phase 5)

	/// <summary>The event records a subject-filtered trigger listens to — one per subject, and per player where someone did it.</summary>
	public enum Ev
	{
		Enters,
		Attacks,
		Dies,
		Blocks,
		Cast, // by the spell's controller: its caster
		Targeted, // by either player
		Damaged,
		Discarded, // by either player
	}

	static IEnumerable<TestCaseData> TriggerRows()
	{
		TestCaseData Row(
			string name,
			Trigger on,
			Ev ev,
			Filter? subject,
			Whose by = Whose.Any,
			string source = "Src"
		) => new TestCaseData(on, ev, subject, by, source).SetName($"Trigger_{name}");
		var another = Is.Yours & !Is.Source;
		yield return Row("SelfEnters", TriggerEvent.SelfEnters, Ev.Enters, Is.Source);
		yield return Row(
			"AnotherCreatureYouControlEnters",
			TriggerEvent.AnotherCreatureYouControlEnters,
			Ev.Enters,
			Is.Creature & another
		);
		yield return Row(
			"AnotherCreatureOrPlaneswalkerYouControlEnters",
			TriggerEvent.AnotherCreatureOrPlaneswalkerYouControlEnters,
			Ev.Enters,
			(Is.Creature | Is.Planeswalker) & another
		);
		yield return Row(
			"LandYouControlEnters",
			TriggerEvent.LandYouControlEnters,
			Ev.Enters,
			Is.Land & Is.Yours
		);
		yield return Row(
			"EventSubtype", // Koth: "whenever a Plains you control enters" (a Gate here)
			new Trigger(EventKind.Entered) { Subject = Is.Land & Is.Yours & Is.Subtype("Gate") },
			Ev.Enters,
			Is.Land & Is.Yours & Is.Subtype("Gate")
		);
		yield return Row(
			"CreatureYouControlEnters",
			TriggerEvent.CreatureYouControlEnters,
			Ev.Enters,
			Is.Creature & Is.Yours
		);
		yield return Row(
			"AnotherNontokenCreatureYouControlEnters",
			TriggerOf("Arni, Humble Scribe", EventKind.Entered),
			Ev.Enters,
			Is.Creature & another & !Is.Token
		);
		yield return Row(
			"CreatureOpponentControlsEnters",
			TriggerOf("Gideon the Oathless", EventKind.Entered),
			Ev.Enters,
			Is.Creature & Is.Opponents
		);
		yield return Row(
			"CreatureWithPower4OrMoreYouControlEnters",
			TriggerOf("Garruk, Curse Breaker", EventKind.Entered),
			Ev.Enters,
			Is.Creature & Is.Yours & Is.PowerAtLeast(4)
		);
		yield return Row("SelfAttacks", TriggerEvent.SelfAttacks, Ev.Attacks, Is.Source);
		yield return Row(
			"AttachedCreatureAttacks",
			TriggerEvent.AttachedCreatureAttacks,
			Ev.Attacks,
			Is.AttachedToSource,
			source: "Sword"
		);
		yield return Row(
			"CreatureYouControlAttacks",
			TriggerEvent.CreatureYouControlAttacks,
			Ev.Attacks,
			Is.Creature & Is.Yours
		);
		yield return Row(
			"CreatureAttacksYouOrYourPlaneswalker",
			TriggerOf("Jace, Reality Sculptor", EventKind.Attacked),
			Ev.Attacks,
			Is.AttackingYou
		);
		yield return Row(
			"AnotherCreatureOrPlaneswalkerYouControlDies",
			TriggerEvent.AnotherCreatureOrPlaneswalkerYouControlDies, // the subject as it last was
			Ev.Dies,
			(Is.Creature | Is.Planeswalker) & another
		);
		yield return Row(
			"CreatureYouControlDies",
			TriggerEvent.CreatureYouControlDies,
			Ev.Dies,
			Is.Creature & Is.Yours
		);
		yield return Row(
			"OpponentsCreatureWithPowerOrToughness1OrLessBlocks",
			TriggerOf("Tetsuko Umezawa, Pursuer", EventKind.Blocks), // no "creature": only creatures block
			Ev.Blocks,
			Is.Opponents & (Is.PowerAtMost(1) | Is.ToughnessAtMost(1))
		);
		yield return Row(
			"SelfBecomesTargetOfOpponentsSpellOrAbility",
			TriggerEvent.SelfBecomesTargetOfOpponentsSpellOrAbility,
			Ev.Targeted,
			Is.Source,
			Whose.Opponent
		);
		yield return Row("SelfDealtDamage", TriggerEvent.SelfDealtDamage, Ev.Damaged, Is.Source);
		yield return Row("SelfDiscarded", TriggerEvent.SelfDiscarded, Ev.Discarded, Is.Source);
		yield return Row("YouCastSpell", TriggerEvent.YouCastSpell, Ev.Cast, null, Whose.You);
		yield return Row(
			"YouCastNoncreatureSpell",
			TriggerEvent.YouCastNoncreatureSpell,
			Ev.Cast,
			!Is.Creature,
			Whose.You
		);
		yield return Row(
			"YouCastCreatureSpell",
			TriggerEvent.YouCastCreatureSpell,
			Ev.Cast,
			Is.Creature,
			Whose.You
		);
		yield return Row(
			"YouCastArtifactOrCreatureSpell",
			TriggerOf("Traxos, Scourge Eternal", EventKind.SpellCast),
			Ev.Cast,
			Is.Type(CardType.Artifact | CardType.Creature),
			Whose.You
		);
		yield return Row(
			"YouCastPreparedSpell",
			TriggerOf("Codie, Ravenous Codex", EventKind.SpellCast),
			Ev.Cast,
			Is.PreparedCopy,
			Whose.You
		);
		yield return Row(
			"OpponentCastsSpellWithManaValue2OrLess",
			TriggerOf("Solarium Sentry", EventKind.SpellCast),
			Ev.Cast,
			Is.ManaValueAtMost(2),
			Whose.Opponent
		);
	}

	/// <summary>No subject: the event, and who did it (phase 5's `By`).</summary>
	static readonly Trigger[] EventAndWho =
	[
		TriggerEvent.YourUpkeep,
		TriggerEvent.EachUpkeep,
		TriggerEvent.OpponentsDrawStep,
		TriggerEvent.YourEndStep,
		TriggerEvent.EachEndStep,
		TriggerEvent.BeginningOfCombatOnYourTurn,
		TriggerEvent.YourFirstMainPhase,
		TriggerEvent.YouGainLife,
		TriggerEvent.YouSurveil,
		TriggerEvent.YouPutLoyaltyCounters,
		TriggerEvent.YouActivateLoyaltyAbility,
		TriggerEvent.OpponentActivatesLoyaltyAbility,
		TriggerEvent.APlayerDiscards,
		TriggerEvent.YouAttack,
		TriggerEvent.OpponentDealtNoncombatDamage,
		TriggerEvent.YouFaceADilemma,
		TriggerEvent.CardLeavesYourGraveyard,
		TriggerEvent.YouDrawACard,
		new Trigger(EventKind.AbilityActivated) { By = Whose.Opponent }, // Kai Winn: the source, Is.NamedChosenName
	];

	/// <summary>
	/// Residue: they test event data beyond the subject, so each becomes a small event predicate in
	/// phase 5 — keep this list short. Where there is a subject, its filter is noted.
	/// </summary>
	static readonly Trigger[] Residue =
	[
		TriggerEvent.YouActivateLoyaltyAbilityRemovingTwoOrMore, // the cost
		TriggerEvent.CreatureYouControlAttacksPlayerAlone, // Is.Creature & Is.Yours & Is.AttackingAPlayer + "alone"
		TriggerEvent.YouCastEquipmentOrSpellTargetingYourCreature, // the spell's targets
		TriggerEvent.YouCastSpellTargetingOpponentOrTheirCreature, // the spell's targets
		TriggerEvent.YouCastFirstNoncreatureSpellEachTurn, // numbered as cast
		TriggerEvent.OpponentsDealtCombatDamageDuringYourTurn, // whose turn, which players
		TriggerEvent.CreaturesAttackYourOpponent, // any attacker at a player
		TriggerEvent.SelfDealsCombatDamageToPlayer, // Is.Source as the damage source + combat
		TriggerEvent.SelfBecomesTapped, // Is.Source, on the one tap path (MagicState.Tap)
		TriggerEvent.YouAttackWithTotalPower8, // YouAttack + While, judged as it triggers
		new Trigger(EventKind.CountersPut) { Subject = Is.Source, Chapter = 1 }, // a Saga's chapter: the lore count crossing N (Scripting.Chapter)
		TriggerEvent.OneOrMoreCardsLeaveYourGraveyard, // one per batch (Trigger.OneOrMore)
	];

	/// <summary>Read from the card itself, not matched against an event on the battlefield (Triggers.Collect).</summary>
	static readonly Trigger[] ReadFromTheCard =
	[
		TriggerEvent.SelfDies, // the dying card as it last was
		TriggerEvent.AttachedCreatureDies, // looks back through DiedEvent.Attached
		TriggerEvent.SelfCast, // the spell on the stack
		TriggerEvent.Reflexive, // put on the stack by WhenYouDo
	];

	static readonly MethodInfo OldMatches = typeof(MagicCore.Turns.Triggers).GetMethod(
		"Matches",
		BindingFlags.NonPublic | BindingFlags.Static
	)!;

	[TestCaseSource(nameof(TriggerRows))]
	public void TriggerRow(Trigger on, Ev ev, Filter? subject, Whose by, string source)
	{
		// Phase 1 proved the old TriggerEvent switch equal to these subjects + who; now the new
		// matcher (Trigger + MagicEvent's subjects) must be.
		var src = B.Card(source);
		var events = Events(ev).ToList();
		bool Old(GameEvent e) => (bool)OldMatches.Invoke(null, [B.S, src, on, e])!;
		bool New(GameObject subjectObject, int byPlayer) =>
			(subject?.Matches(B.S, subjectObject, B.Context(source)) ?? true)
			&& by switch
			{
				Whose.You => byPlayer == src.ControllerId,
				Whose.Opponent => byPlayer != src.ControllerId,
				_ => true,
			};
		string Label((GameObject Subject, int By, GameEvent E) x) =>
			$"{B.LabelOf(x.Subject.Id)} by {x.By}";
		Assert.That(
			events.Where(x => New(x.Subject, x.By)).Select(Label),
			Nu.EqualTo(events.Where(x => Old(x.E)).Select(Label))
		);
		Assert.That(events.Count(x => New(x.Subject, x.By)), Nu.InRange(1, events.Count - 1));
	}

	/// <summary>A card's own trigger, as scripted — the rows read it, never restate it.</summary>
	static Trigger TriggerOf(string card, EventKind kind)
	{
		IEnumerable<TriggeredAbilityComponent> All(object? o, int depth) =>
			o switch
			{
				null or string or CardData => [],
				TriggeredAbilityComponent a => [a],
				System.Collections.IEnumerable list when depth < 12 => list.Cast<object>()
					.SelectMany(x => All(x, depth + 1)),
				_ when depth < 12 && o.GetType().Namespace?.StartsWith("MagicCore") == true =>
					o.GetType()
						.GetProperties()
						.Where(p =>
							p.GetIndexParameters().Length == 0 && p.Name != "EqualityContract"
						)
						.SelectMany(p => All(p.GetValue(o), depth + 1)),
				_ => [],
			};
		return All(MagicCore.Sets.CardRegistry.Get(card).Components, 0)
			.Select(a => a.On)
			.Single(on => on.On == kind && on.Subject is not null);
	}

	static IEnumerable<(GameObject Subject, int By, GameEvent E)> Events(Ev ev)
	{
		var s = B.S;
		var players = new[] { s.Player1Id(), s.Player2Id() };
		var subjects = In(ev == Ev.Cast ? Zone.Stack : Zone.Battlefield, s.Player1Id())
			.Select(s.GetCard)
			.ToList();
		foreach (var c in subjects)
			switch (ev)
			{
				case Ev.Cast:
					yield return (c, c.ControllerId, new SpellCastEvent(c.Id, c.ControllerId));
					break;
				case Ev.Targeted:
				case Ev.Discarded:
					foreach (var p in players)
						yield return (
							c,
							p,
							ev == Ev.Targeted
								? new BecameTargetEvent(c.Id, p, 0)
								: new DiscardedEvent(p, [c.Id])
						);
					break;
				default:
					yield return (
						c,
						0,
						ev switch
						{
							Ev.Enters => new EnteredBattlefieldEvent(c.Id),
							Ev.Attacks => new AttackedEvent(c.Id),
							Ev.Dies => new DiedEvent(c),
							Ev.Blocks => new BlocksEvent(c.Id),
							_ => new CreatureDealtDamageEvent(c.Id, 1),
						}
					);
					break;
			}
	}

	[Test]
	public void EveryNamedTrigger_AndEventKind_IsARowOrListed()
	{
		var covered = TriggerRows()
			.Select(r => (Trigger)r.Arguments[0]!)
			.Concat(EventAndWho)
			.Concat(Residue)
			.Concat(ReadFromTheCard)
			.ToList();
		var named = typeof(TriggerEvent)
			.GetFields(BindingFlags.Public | BindingFlags.Static)
			.Select(f => f.GetValue(null))
			.OfType<Trigger>();
		Assert.That(named.Except(covered), Nu.Empty);
		Assert.That(Enum.GetValues<EventKind>().Except(covered.Select(x => x.On)), Nu.Empty);
		Assert.That(OldMatches, Nu.Not.Null);
		Assert.That(AppliesTo, Nu.Not.Null);
	}

	// ---------------------------------------------------------------- costs (phase 6)

	[Test]
	public void TapSelf_BesideTapUntapped_LeavesTheSourceOut()
	{
		var ability = new ActivatedAbilityComponent(ManaCost.None, [])
		{
			Costs = [new TapSelfCost(), new TapUntappedCost(2, Is.Artifact)],
		};
		// "Tap two" is split into two costs that each pick one (Worf: the player chooses both).
		Assert.That(
			ability.Costs.OfType<TapUntappedCost>().Select(c => (c.NotTheSource, c.Slot)),
			Nu.EqualTo(new[] { (true, CostSlot.Tap), (true, CostSlot.SecondTap) })
		);
		Assert.That(
			new ActivatedAbilityComponent(ManaCost.None, [])
			{
				Costs = [new TapUntappedCost(2, Is.Artifact)],
			}
				.Costs.OfType<TapUntappedCost>()
				.Select(c => c.NotTheSource),
			Has.All.False
		);
	}

	[Test]
	public void TwoCostsPickingIntoOneSlot_Throw_RatherThanPayOne() =>
		Assert.Throws<InvalidOperationException>(
			() => CostList.In([new DiscardCost(), new DiscardCost(Is.Legendary)], CostSlot.Discard)
		);

	// ---------------------------------------------------------------- helpers

	static IEnumerable<int> In(Zone from, int first)
	{
		var s = B.S;
		var players = new[] { first, s.OpponentOf(first) };
		IEnumerable<int> CardsOf(int p, ZoneType z) => s.CardsIn(p, z).Select(c => c.Id);
		return from switch
		{
			Zone.Players => players,
			Zone.Battlefield => players.SelectMany(p => CardsOf(p, ZoneType.Battlefield)),
			Zone.Stack => s.GetChildrenIds(s.GetWellKnownId(MagicKeys.Stack)),
			Zone.YourGraveyard => CardsOf(B.Me, ZoneType.Graveyard),
			Zone.AnyGraveyard => players.SelectMany(p => CardsOf(p, ZoneType.Graveyard)),
			Zone.YourHand => CardsOf(B.Me, ZoneType.Hand),
			_ => CardsOf(B.Them, ZoneType.Battlefield), // the Ref rows target them
		};
	}

	/// <summary>Each clause's zone, in the order given, filtered.</summary>
	static IEnumerable<int> Select(
		int first,
		FilterContext c,
		params (Zone From, Filter? Filter)[] clauses
	) =>
		clauses.SelectMany(clause =>
			In(clause.From, first)
				.Where(id => clause.Filter?.Matches(B.S, B.S.GetObject(id), c) ?? true)
		);

	static void Same(
		IEnumerable<int> old,
		IEnumerable<int> now,
		IEnumerable<int> all,
		bool filtered
	)
	{
		var expected = old.Select(B.LabelOf).ToList();
		var actual = now.Select(B.LabelOf).ToList();
		Assert.That(actual, Nu.EqualTo(expected)); // same objects, same order
		Assert.That(actual, Nu.Not.Empty, "the board doesn't exercise this row");
		if (filtered)
			Assert.That(
				actual,
				Has.Count.LessThan(all.Count()),
				"the filter excludes nothing here"
			);
	}

	static IEnumerable<string> FieldsOf<T>() =>
		typeof(T)
			.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
			.Select(p => p.Name);

	/// <summary>The fields a row sets away from the plain value — what it covers.</summary>
	static IEnumerable<string> Changed<T>(T row, T plain) =>
		typeof(T)
			.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
			.Where(p => !SameValue(p.GetValue(row), p.GetValue(plain)))
			.Select(p => p.Name);

	static bool SameValue(object? a, object? b) =>
		a is IEnumerable x && b is IEnumerable y && a is not string
			? x.Cast<object>().SequenceEqual(y.Cast<object>())
			: Equals(a, b);
}
