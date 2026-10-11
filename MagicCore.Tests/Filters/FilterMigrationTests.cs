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
/// zone and a filter and checked against the OLD code on <see cref="FilterBoard"/>: the same
/// objects, in the same order. What isn't a filter is listed with why, and a completeness test fails
/// when a value is in neither. A phase that replaces a vocabulary moves its rows onto the new code.
/// Each row must pick out some but not all of its candidates, so the board really tells them apart.
/// </summary>
public class FilterMigrationTests
{
	static readonly FilterBoard B = FilterBoard.Build();

	/// <summary>Where candidates come from — never in a filter. Phase 2's TargetSpec `From`, phase 3's `All(zone, …)`.</summary>
	public enum From
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
		TestCaseData Row(string name, Ref old, From from, Filter? filter) =>
			new TestCaseData(old, from, filter).SetName($"Ref_{name}");
		var bf = From.Battlefield;
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
		yield return Row("YourGraveyard", Ref.CardsInYourGraveyard, From.YourGraveyard, null);
		yield return Row(
			"YourGraveyardOfType",
			Ref.YourGraveyard(CardType.Creature),
			From.YourGraveyard,
			Is.Creature
		);
		yield return Row("YourHand", Ref.CardsInYourHand, From.YourHand, null);
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
			From.TargetPlayersBattlefield,
			Is.Creature
		);
		yield return Row(
			"YourCreaturesAttackingAPlayer",
			new Ref(RefKind.YourCreaturesAttackingAPlayer),
			bf,
			Is.Yours & Is.AttackingAPlayer // the old code never asks "creature": only creatures attack
		);
		yield return Row(
			"Subtype",
			Ref.OtherCreaturesYouControl with
			{
				Subtype = "Wizard",
			},
			bf,
			Is.Creature & Is.Yours & !Is.Source & Is.Subtype("Wizard")
		);
		yield return Row(
			"ExceptEventCard", // Roiling Canopy: "five OTHER Forests" (Gates here)
			new Ref(RefKind.YourPermanents, Type: CardType.Land)
			{
				Subtype = "Gate",
				ExceptEventCard = true,
			},
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
	];

	[TestCaseSource(nameof(RefRows))]
	public void RefRow(Ref old, From from, Filter? filter)
	{
		var context = ImmutableDictionary<string, object>
			.Empty.Add(ContextKeys.Controller, B.Me)
			.Add(ContextKeys.Source, B["Src"])
			.Add(ContextKeys.Targets, ImmutableArray.Create(B.Them))
			.Add(ContextKeys.EventCard, B["Grove"]);
		// A Ref lists YOUR objects first.
		Same(
			old.Resolve(B.S, context),
			Select(B.Me, B.Context(eventCard: "Grove"), (from, filter)),
			In(from, B.Me),
			filter is not null
		);
	}

	[Test]
	public void EveryRefKind_IsARowOrAReferent()
	{
		var rows = RefRows().Select(r => (Ref)r.Arguments[0]!).ToList();
		Assert.That(
			Enum.GetValues<RefKind>().Except(rows.Select(r => r.Kind)).Except(Referents),
			Nu.Empty
		);
		Assert.That(rows.Any(r => r.Subtype is not null) && rows.Any(r => r.ExceptEventCard));
	}

	// ---------------------------------------------------------------- TargetSpec (phase 2)

	static IEnumerable<TestCaseData> TargetRows()
	{
		TestCaseData Row(string name, TargetSpec old, params (From, Filter?)[] from) =>
			new TestCaseData(old, from).SetName($"Target_{name}");
		var bf = From.Battlefield;
		var creature = new TargetSpec(TargetKind.Creature);
		yield return Row("Creature", creature, (bf, Is.Creature));
		yield return Row(
			"CreatureOrPlaneswalker",
			new(TargetKind.CreatureOrPlaneswalker),
			(bf, Is.Creature | Is.Planeswalker)
		);
		yield return Row(
			"AnyTarget",
			new(TargetKind.AnyTarget),
			(From.Players, null),
			(bf, Is.Creature | Is.Planeswalker)
		);
		yield return Row("Player", new(TargetKind.Player), (From.Players, null));
		yield return Row("Permanent", new(TargetKind.Permanent), (bf, null));
		yield return Row(
			"SpellOrCreature", // phase 2: two clauses, since "spell" is a zone, not a property
			new(TargetKind.SpellOrCreature),
			(bf, Is.Creature),
			(From.Stack, null)
		);
		yield return Row(
			"CardInYourGraveyard",
			new(TargetKind.CardInYourGraveyard),
			(From.YourGraveyard, null)
		);
		yield return Row(
			"CardInAnyGraveyard",
			new(TargetKind.CardInAnyGraveyard),
			(From.AnyGraveyard, null)
		);
		yield return Row("Spell", new(TargetKind.Spell), (From.Stack, null));
		yield return Row(
			"WhoseYou",
			new(TargetKind.Creature, Whose.You),
			(bf, Is.Creature & Is.Yours)
		);
		yield return Row(
			"WhoseOpponent",
			new(TargetKind.AnyTarget, Whose.Opponent),
			(From.Players, Is.Opponents),
			(bf, (Is.Creature | Is.Planeswalker) & Is.Opponents)
		);
		yield return Row(
			"Another",
			creature with
			{
				Another = true,
			},
			(bf, Is.Creature & !Is.Source)
		);
		yield return Row(
			"Nonlegendary",
			creature with
			{
				Nonlegendary = true,
			},
			(bf, Is.Creature & !Is.Legendary)
		);
		yield return Row(
			"WithSupertype",
			new(TargetKind.CardInYourGraveyard) { WithSupertype = Supertype.Legendary },
			(From.YourGraveyard, Is.Legendary)
		);
		yield return Row(
			"OrSupertype", // Icy Reception: "creature or legendary spell"
			new(TargetKind.Spell)
			{
				AnyOfTypes = CardType.Creature,
				OrSupertype = Supertype.Legendary,
			},
			(From.Stack, Is.Creature | Is.Legendary)
		);
		yield return Row(
			"AnyOfColors",
			creature with
			{
				AnyOfColors = [ManaColor.Blue, ManaColor.Red],
			},
			(bf, Is.Creature & (Is.Color(ManaColor.Blue) | Is.Color(ManaColor.Red)))
		);
		yield return Row(
			"AnyOfTypes",
			new(TargetKind.Permanent) { AnyOfTypes = CardType.Artifact | CardType.Enchantment },
			(bf, Is.Type(CardType.Artifact | CardType.Enchantment))
		);
		yield return Row(
			"NotTypes",
			new(TargetKind.Permanent) { NotTypes = CardType.Creature | CardType.Land },
			(bf, !Is.Type(CardType.Creature | CardType.Land))
		);
		yield return Row(
			"NotSubtype", // Puppet Crafting: "non-Aura enchantment"
			new(TargetKind.Permanent) { AnyOfTypes = CardType.Enchantment, NotSubtype = "Aura" },
			(bf, Is.Enchantment & !Is.Subtype("Aura"))
		);
		yield return Row(
			"MaxPower",
			creature with
			{
				MaxPower = 2,
			},
			(bf, Is.Creature & Is.PowerAtMost(2))
		);
		yield return Row(
			"MinToughness",
			creature with
			{
				MinToughness = 4,
			},
			(bf, Is.Creature & Is.ToughnessAtLeast(4))
		);
		yield return Row(
			"WithKeyword",
			creature with
			{
				WithKeyword = "Flying",
			},
			(bf, Is.Creature & Is.With("Flying"))
		);
		yield return Row(
			"EnteredThisTurn",
			creature with
			{
				EnteredThisTurn = true,
			},
			(bf, Is.Creature & Is.EnteredThisTurn)
		);
		yield return Row(
			"AttackedThisTurn",
			creature with
			{
				AttackedThisTurn = true,
			},
			(bf, Is.Creature & Is.AttackedThisTurn)
		);
		yield return Row(
			"Attacking",
			creature with
			{
				Attacking = true,
			},
			(bf, Is.Creature & Is.Attacking)
		);
		yield return Row(
			"InCombat",
			creature with
			{
				InCombat = true,
			},
			(bf, Is.Creature & Is.InCombat)
		);
		yield return Row(
			"WithPlusOneCounter",
			creature with
			{
				WithPlusOneCounter = true,
			},
			(bf, Is.Creature & Is.WithPlusOneCounter)
		);
		yield return Row(
			"MaxManaValueOnTheStack", // X counts there
			new(TargetKind.Spell) { MaxManaValue = 2 },
			(From.Stack, Is.ManaValueAtMost(2))
		);
		yield return Row(
			"MaxManaValueInAGraveyard",
			new(TargetKind.CardInYourGraveyard) { MaxManaValue = 2 },
			(From.YourGraveyard, Is.ManaValueAtMost(2))
		);
		yield return Row(
			"MinManaValue",
			new(TargetKind.CardInAnyGraveyard) { MinManaValue = 4 },
			(From.AnyGraveyard, Is.ManaValueAtLeast(4))
		);
	}

	/// <summary>How many targets and how they relate — TargetSpec keeps these; they're not about one object.</summary>
	static readonly string[] TargetingStructure =
	[
		nameof(TargetSpec.Kind), // becomes From; every value is a row above
		nameof(TargetSpec.Optional),
		nameof(TargetSpec.NotThePrevious),
	];

	[TestCaseSource(nameof(TargetRows))]
	public void TargetRow(TargetSpec old, (From, Filter?)[] from) =>
		// Candidates in TargetSpec's order: players, permanents (player 1's first), stack, graveyards.
		Same(
			old.Candidates(B.S, B.Me, B["Src"]),
			Select(B.S.Player1Id(), B.Context(), from),
			from.SelectMany(f => In(f.Item1, B.S.Player1Id())),
			from.Any(f => f.Item2 is not null)
		);

	[Test]
	public void EveryTargetSpecField_AndKind_IsARow()
	{
		var rows = TargetRows().Select(r => (TargetSpec)r.Arguments[0]!).ToList();
		Assert.That(Enum.GetValues<TargetKind>().Except(rows.Select(r => r.Kind)), Nu.Empty);
		Assert.That(
			FieldsOf<TargetSpec>()
				.Except(rows.SelectMany(r => Changed(r, new TargetSpec(r.Kind))))
				.Except(TargetingStructure),
			Nu.Empty
		);
	}

	// ---------------------------------------------------------------- statics (phase 4)

	static IEnumerable<TestCaseData> StaticRows()
	{
		TestCaseData Row(string name, StaticEffectComponent old, Filter f, string source = "Src") =>
			new TestCaseData(old, f, source).SetName($"Static_{name}");
		var yours = new StaticEffectComponent(Affects.CreaturesYouControl);
		yield return Row("Self", new(Affects.Self), Is.Source);
		yield return Row("CreaturesYouControl", yours, Is.Creature & Is.Yours);
		yield return Row(
			"OtherCreaturesYouControl",
			new(Affects.OtherCreaturesYouControl),
			Is.Creature & Is.Yours & !Is.Source
		);
		yield return Row("LandsYouControl", new(Affects.LandsYouControl), Is.Land & Is.Yours);
		yield return Row(
			"AttachedCreature",
			new(Affects.AttachedCreature),
			Is.AttachedToSource,
			source: "Sword"
		);
		yield return Row(
			"OnlyWithCounter", // Gallia
			new(Affects.OtherCreaturesYouControl) { OnlyWithCounter = true },
			Is.Creature & Is.Yours & !Is.Source & Is.WithPlusOneCounter
		);
		yield return Row(
			"OnlySubtype",
			yours with
			{
				OnlySubtype = "Wizard",
			},
			Is.Creature & Is.Yours & Is.Subtype("Wizard")
		);
		yield return Row(
			"OnlyTokens",
			yours with
			{
				OnlyTokens = true,
			},
			Is.Creature & Is.Yours & Is.Token
		);
		yield return Row(
			"OnlyTypes", // Vraska: "artifact creatures you control"
			yours with
			{
				OnlyTypes = CardType.Artifact,
			},
			Is.Creature & Is.Yours & Is.Artifact
		);
		yield return Row(
			"OnlyPowerOrToughnessAtMost", // Tetsuko — phase 4's P/T guard: never on a P/T static
			yours with
			{
				OnlyPowerOrToughnessAtMost = 1,
			},
			Is.Creature & Is.Yours & (Is.PowerAtMost(1) | Is.ToughnessAtMost(1))
		);
	}

	/// <summary>What the static DOES, and its conditions (phase 4 makes While a Condition) — not who it affects.</summary>
	static readonly string[] NotScope =
	[
		nameof(StaticEffectComponent.Keyword),
		nameof(StaticEffectComponent.Power),
		nameof(StaticEffectComponent.Toughness),
		nameof(StaticEffectComponent.While),
		nameof(StaticEffectComponent.If),
	];

	static readonly MethodInfo AppliesTo = typeof(Characteristics).GetMethod(
		"AppliesTo",
		BindingFlags.NonPublic | BindingFlags.Static
	)!;

	[TestCaseSource(nameof(StaticRows))]
	public void StaticRow(StaticEffectComponent old, Filter filter, string source)
	{
		var permanents = In(From.Battlefield, B.S.Player1Id()).Select(B.S.GetCard).ToList();
		Same(
			permanents
				.Where(c => (bool)AppliesTo.Invoke(null, [B.S, B.Card(source), old, c])!)
				.Select(c => c.Id),
			permanents.Where(c => filter.Matches(B.S, c, B.Context(source))).Select(c => c.Id),
			permanents.Select(c => c.Id),
			filtered: true
		);
	}

	[Test]
	public void EveryAffectsValue_AndOnlyField_IsARow()
	{
		var rows = StaticRows().Select(r => (StaticEffectComponent)r.Arguments[0]!).ToList();
		Assert.That(Enum.GetValues<Affects>().Except(rows.Select(r => r.Affects)), Nu.Empty);
		Assert.That(
			FieldsOf<StaticEffectComponent>()
				.Except(rows.SelectMany(r => Changed(r, new StaticEffectComponent(r.Affects))))
				.Except(NotScope)
				.Except([nameof(StaticEffectComponent.Affects)]),
			Nu.Empty
		);
	}

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
			TriggerEvent on,
			Ev ev,
			Filter? subject,
			Whose by = Whose.Any,
			string source = "Src",
			string? eventSubtype = null
		) =>
			new TestCaseData(on, ev, subject, by, source, eventSubtype).SetName(
				$"Trigger_{on}{(eventSubtype is null ? "" : "_EventSubtype")}"
			);
		var another = Is.Yours & !Is.Source;
		yield return Row(TriggerEvent.SelfEnters, Ev.Enters, Is.Source);
		yield return Row(
			TriggerEvent.AnotherCreatureYouControlEnters,
			Ev.Enters,
			Is.Creature & another
		);
		yield return Row(
			TriggerEvent.AnotherCreatureOrPlaneswalkerYouControlEnters,
			Ev.Enters,
			(Is.Creature | Is.Planeswalker) & another
		);
		yield return Row(TriggerEvent.LandYouControlEnters, Ev.Enters, Is.Land & Is.Yours);
		yield return Row(
			TriggerEvent.LandYouControlEnters, // Koth: "whenever a Plains you control enters" (a Gate here)
			Ev.Enters,
			Is.Land & Is.Yours & Is.Subtype("Gate"),
			eventSubtype: "Gate"
		);
		yield return Row(TriggerEvent.CreatureYouControlEnters, Ev.Enters, Is.Creature & Is.Yours);
		yield return Row(
			TriggerEvent.AnotherNontokenCreatureYouControlEnters,
			Ev.Enters,
			Is.Creature & another & !Is.Token
		);
		yield return Row(
			TriggerEvent.CreatureOpponentControlsEnters,
			Ev.Enters,
			Is.Creature & Is.Opponents
		);
		yield return Row(
			TriggerEvent.CreatureWithPower4OrMoreYouControlEnters,
			Ev.Enters,
			Is.Creature & Is.Yours & Is.PowerAtLeast(4)
		);
		yield return Row(TriggerEvent.SelfAttacks, Ev.Attacks, Is.Source);
		yield return Row(
			TriggerEvent.AttachedCreatureAttacks,
			Ev.Attacks,
			Is.AttachedToSource,
			source: "Sword"
		);
		yield return Row(
			TriggerEvent.CreatureYouControlAttacks,
			Ev.Attacks,
			Is.Creature & Is.Yours
		);
		yield return Row(
			TriggerEvent.CreatureAttacksYouOrYourPlaneswalker,
			Ev.Attacks,
			Is.AttackingYou
		);
		yield return Row(
			TriggerEvent.AnotherCreatureOrPlaneswalkerYouControlDies, // the subject as it last was
			Ev.Dies,
			(Is.Creature | Is.Planeswalker) & another
		);
		yield return Row(TriggerEvent.CreatureYouControlDies, Ev.Dies, Is.Creature & Is.Yours);
		yield return Row(
			TriggerEvent.OpponentsCreatureWithPowerOrToughness1OrLessBlocks, // no "creature": only creatures block
			Ev.Blocks,
			Is.Opponents & (Is.PowerAtMost(1) | Is.ToughnessAtMost(1))
		);
		yield return Row(
			TriggerEvent.SelfBecomesTargetOfOpponentsSpellOrAbility,
			Ev.Targeted,
			Is.Source,
			Whose.Opponent
		);
		yield return Row(TriggerEvent.SelfDealtDamage, Ev.Damaged, Is.Source);
		yield return Row(TriggerEvent.SelfDiscarded, Ev.Discarded, Is.Source);
		yield return Row(TriggerEvent.YouCastSpell, Ev.Cast, null, Whose.You);
		yield return Row(TriggerEvent.YouCastNoncreatureSpell, Ev.Cast, !Is.Creature, Whose.You);
		yield return Row(TriggerEvent.YouCastCreatureSpell, Ev.Cast, Is.Creature, Whose.You);
		yield return Row(
			TriggerEvent.YouCastArtifactOrCreatureSpell,
			Ev.Cast,
			Is.Type(CardType.Artifact | CardType.Creature),
			Whose.You
		);
		yield return Row(TriggerEvent.YouCastPreparedSpell, Ev.Cast, Is.PreparedCopy, Whose.You);
		yield return Row(
			TriggerEvent.OpponentCastsSpellWithManaValue2OrLess,
			Ev.Cast,
			Is.ManaValueAtMost(2),
			Whose.Opponent
		);
	}

	/// <summary>No subject: the event, and who did it (phase 5's `By`).</summary>
	static readonly TriggerEvent[] EventAndWho =
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
	];

	/// <summary>
	/// Residue: they test event data beyond the subject, so each becomes a small event predicate in
	/// phase 5 — keep this list short. Where there is a subject, its filter is noted.
	/// </summary>
	static readonly TriggerEvent[] Residue =
	[
		TriggerEvent.YouActivateLoyaltyAbilityRemovingTwoOrMore, // the cost
		TriggerEvent.CreatureYouControlAttacksPlayerAlone, // Is.Creature & Is.Yours & Is.AttackingAPlayer + "alone"
		TriggerEvent.YouCastEquipmentOrSpellTargetingYourCreature, // the spell's targets
		TriggerEvent.YouCastSpellTargetingOpponentOrTheirCreature, // the spell's targets
		TriggerEvent.YouCastFirstNoncreatureSpellEachTurn, // numbered as cast
		TriggerEvent.OpponentsDealtCombatDamageDuringYourTurn, // whose turn, which players
		TriggerEvent.CreaturesAttackYourOpponent, // any attacker at a player
		TriggerEvent.SelfDealsCombatDamageToPlayer, // Is.Source as the damage source + combat
	];

	/// <summary>Read from the card itself, not matched against an event on the battlefield (Triggers.Collect).</summary>
	static readonly TriggerEvent[] ReadFromTheCard =
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
	static readonly MethodInfo OldOfSubtype = typeof(MagicCore.Turns.Triggers).GetMethod(
		"OfSubtype",
		BindingFlags.NonPublic | BindingFlags.Static
	)!;

	[TestCaseSource(nameof(TriggerRows))]
	public void TriggerRow(
		TriggerEvent on,
		Ev ev,
		Filter? subject,
		Whose by,
		string source,
		string? eventSubtype
	)
	{
		var src = B.Card(source);
		var ability = new TriggeredAbilityComponent(on, []) { EventSubtype = eventSubtype };
		var events = Events(ev).ToList();
		bool Old(GameEvent e) =>
			(bool)OldMatches.Invoke(null, [B.S, src, on, e])!
			&& (bool)OldOfSubtype.Invoke(null, [B.S, ability, e])!;
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

	static IEnumerable<(GameObject Subject, int By, GameEvent E)> Events(Ev ev)
	{
		var s = B.S;
		var players = new[] { s.Player1Id(), s.Player2Id() };
		var subjects = In(ev == Ev.Cast ? From.Stack : From.Battlefield, s.Player1Id())
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
	public void EveryTriggerEvent_IsARowOrListed()
	{
		var rows = TriggerRows().Select(r => (TriggerEvent)r.Arguments[0]!);
		Assert.That(
			Enum.GetValues<TriggerEvent>()
				.Except(rows)
				.Except(EventAndWho)
				.Except(Residue)
				.Except(ReadFromTheCard),
			Nu.Empty
		);
		Assert.That(OldMatches, Nu.Not.Null);
		Assert.That(OldOfSubtype, Nu.Not.Null);
		Assert.That(AppliesTo, Nu.Not.Null);
	}

	// ---------------------------------------------------------------- helpers

	static IEnumerable<int> In(From from, int first)
	{
		var s = B.S;
		var players = new[] { first, s.OpponentOf(first) };
		IEnumerable<int> Zone(int p, ZoneType z) => s.CardsIn(p, z).Select(c => c.Id);
		return from switch
		{
			From.Players => players,
			From.Battlefield => players.SelectMany(p => Zone(p, ZoneType.Battlefield)),
			From.Stack => s.GetChildrenIds(s.GetWellKnownId(MagicKeys.Stack)),
			From.YourGraveyard => Zone(B.Me, ZoneType.Graveyard),
			From.AnyGraveyard => players.SelectMany(p => Zone(p, ZoneType.Graveyard)),
			From.YourHand => Zone(B.Me, ZoneType.Hand),
			_ => Zone(B.Them, ZoneType.Battlefield), // the Ref rows target them
		};
	}

	/// <summary>Each clause's zone, in the order given, filtered.</summary>
	static IEnumerable<int> Select(
		int first,
		FilterContext c,
		params (From From, Filter? Filter)[] clauses
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
