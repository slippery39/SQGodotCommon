using System.Collections.Immutable;
using ImmutableGameObjects;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Targeting;

namespace MagicCore.Sets.Fra;

/// <summary>
/// Reality Fracture's hand-written abilities, keyed by card name. Names, costs, types, P/T and
/// keywords come from FRA.json; only what the rules text DOES is written here.
///
/// A card is playable when it is scripted here or its text is nothing but keywords the engine
/// implements — see <see cref="IsImplemented"/>. Anything else would play silently vanilla.
/// </summary>
public static class FraCards
{
	static readonly Dictionary<string, CardData> Data = CardDataSet
		.Load("FRA")
		.ToDictionary(c => c.Name);

	/// <summary>"A 2/2 colorless Wizard Soldier creature token named Cadet."</summary>
	public static readonly CardData Cadet =
		new(
			"Cadet",
			"token",
			null,
			0,
			"Creature — Wizard Soldier",
			null,
			"2",
			"2",
			null,
			[],
			[],
			"token",
			"T",
			null
		);

	/// <summary>"A 4/4 green Beast creature token with trample."</summary>
	public static readonly CardData Beast =
		new(
			"Beast",
			"token",
			null,
			0,
			"Creature — Beast",
			null,
			"4",
			"4",
			null,
			["G"],
			["Trample"],
			"token",
			"T",
			null
		);

	/// <summary>"[cost]: effects" — a loyalty ability, own or granted.</summary>
	static LoyaltyAbilityComponent Loyalty(
		int cost,
		TargetSpec[] targets,
		params GameAction[] effects
	) => new(cost, [.. effects]) { Targets = [.. targets] };

	/// <summary>"Planeswalkers you control have '[cost]: effects'."</summary>
	static GrantsLoyaltyAbility Grant(
		int cost,
		TargetSpec[] targets,
		params GameAction[] effects
	) => new(Loyalty(cost, targets, effects));

	static ImmutableArray<GameComponent> DualLand(
		EntersUntappedIf unless,
		ManaColor a,
		ManaColor b
	) =>
		[
			new EntersTappedComponent(unless),
			new ManaAbilityComponent(a),
			new ManaAbilityComponent(b),
		];

	/// <summary>Keywords the engine implements; a text line made only of these needs no script.</summary>
	public static readonly ImmutableHashSet<string> EngineKeywords = ImmutableHashSet.Create(
		StringComparer.OrdinalIgnoreCase,
		"Flying",
		"Reach",
		"Trample",
		"Deathtouch",
		"Lifelink",
		"First strike",
		"Double strike",
		"Vigilance",
		"Haste",
		"Menace",
		"Defender",
		"Flash"
	);

	static readonly TargetSpec Creature = new(TargetKind.Creature);
	static readonly TargetSpec CreatureOrPlaneswalker = new(TargetKind.CreatureOrPlaneswalker);

	static SpellEffectsComponent Spell(TargetSpec[] targets, params GameAction[] effects) =>
		new([.. effects]) { Targets = [.. targets] };

	static TriggeredAbilityComponent When(
		TriggerEvent on,
		TargetSpec[] targets,
		params GameAction[] effects
	) => new(on, [.. effects]) { Targets = [.. targets] };

	static readonly Dictionary<string, ImmutableArray<GameComponent>> Scripts =
		new()
		{
			// ===== White =====
			["Prophesied End"] =
			[
				// The draw checks "wasn't attacking" first: once destroyed, the card has left combat.
				Spell(
					[Creature],
					new If(
						new TargetIsNotAttacking(0),
						new DrawCards(Ref.ControllerOfTarget(0), 1)
					),
					new Destroy(Ref.Target())
				),
			],
			["Thalia, the Survivor"] = [new OpponentsNoncreatureSpellsCostMore(1)],

			// ===== Black =====
			["Last Gasp"] = [Spell([Creature], new ModifyUntilEndOfTurn(Ref.Target(), -3, -3))],
			["Terminal Criticism"] =
			[
				Spell(
					[CreatureOrPlaneswalker with { AnyOfColors = [ManaColor.Blue, ManaColor.Red] }],
					new Destroy(Ref.Target()),
					new GainLife(Ref.You, 1)
				),
			],
			["Extended Absence"] =
			[
				Spell(
					[CreatureOrPlaneswalker],
					new Exile(Ref.Target()),
					new DealDamage(Ref.EachOpponent, 1),
					new GainLife(Ref.You, 1)
				),
			],
			["Cast Away Doubt"] =
			[
				Spell([], new DrawCards(Ref.You, 2), new DealDamage(Ref.EachPlayer, 2)),
			],
			["Rank Rat"] = [When(TriggerEvent.SelfEnters, [], new Discard(Ref.EachOpponent, 1))],
			["Rampart Hunter"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[Creature],
					new ModifyUntilEndOfTurn(Ref.Target(), 2, 2),
					new ModifyUntilEndOfTurn(Ref.Target(), 0, 0, "Deathtouch")
				),
			],
			["Screeching Soulbreaker"] =
			[
				When(
					TriggerEvent.SelfAttacks,
					[],
					new DealDamage(Ref.EachOpponent, 1),
					new GainLife(Ref.You, 1)
				),
			],

			// ===== Red =====
			["Wrath of the Bloodmane"] =
			[
				new CostsLessIfYouControlLegendaryCreature(1),
				Spell([CreatureOrPlaneswalker], new DealDamage(Ref.Target(), 4)),
			],
			["Artifist Acumen"] =
			[
				Spell(
					[],
					new ModifyUntilEndOfTurn(Ref.CreaturesYouControl, 0, 0, "First strike"),
					new DrawCards(Ref.You, 1)
				),
			],
			["Heartstring Puller"] =
			[
				When(TriggerEvent.SelfEnters, [], new CreateTokens(Cadet, 1)),
			],
			["Chandra's Emberling"] =
			[
				When(TriggerEvent.YouCastNoncreatureSpell, [], new AddCounters(Ref.Source, 1)),
			],
			["Arni, Renowned Champion"] =
			[
				When(
					TriggerEvent.AnotherCreatureYouControlEnters,
					[],
					new ModifyUntilEndOfTurn(Ref.Source, 0, 0) { PowerFromEventCard = true }
				),
			],
			["Koth, the Geomancer"] =
			[
				When(
					TriggerEvent.LandYouControlEnters,
					[],
					new DealDamage(Ref.EachOpponent, 1),
					new If(new EventCardHasSubtype("Mountain"), new AddMana(ManaColor.Red))
				),
			],

			// ===== Green =====
			["Tethermage's Advantage"] =
			[
				Spell(
					[Creature],
					new ModifyUntilEndOfTurn(Ref.Target(), 2, 2),
					new ModifyUntilEndOfTurn(Ref.Target(), 0, 0, "Reach"),
					new Untap(Ref.Target())
				),
			],
			["Yoshimaru, Scrappy Stray"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[
						new TargetSpec(TargetKind.Creature, Whose.You) { Another = true },
						new TargetSpec(TargetKind.Creature, Whose.Opponent) { Optional = true },
					],
					new Fight(Ref.Target(0), Ref.Target(1))
				),
				new ActivatedAbilityComponent(
					ManaCost.Parse("{6}"),
					[new AddCounters(Ref.Target(), 1)]
				)
				{
					Targets = [Creature with { Nonlegendary = true }],
				},
			],
			["Greenhouse Propagator"] =
			[
				When(TriggerEvent.AnotherCreatureYouControlEnters, [], new GainLife(Ref.You, 1)),
				new ManaAbilityComponent(ManaColor.Green),
			],

			// ===== Dual lands =====
			// Ten commons: "enters tapped unless you control a planeswalker." Five rares: "unless you
			// control two or more other lands." Each "{T}: Add {A} or {B}."
			["Dedicated Commons"] = DualLand(
				EntersUntappedIf.YouControlAPlaneswalker,
				ManaColor.Red,
				ManaColor.White
			),
			["Fatehold Annex"] = DualLand(
				EntersUntappedIf.YouControlAPlaneswalker,
				ManaColor.White,
				ManaColor.Blue
			),
			["Formidable Commons"] = DualLand(
				EntersUntappedIf.YouControlAPlaneswalker,
				ManaColor.Black,
				ManaColor.Green
			),
			["Innovative Commons"] = DualLand(
				EntersUntappedIf.YouControlAPlaneswalker,
				ManaColor.Blue,
				ManaColor.Red
			),
			["Konstrari Annex"] = DualLand(
				EntersUntappedIf.YouControlAPlaneswalker,
				ManaColor.Red,
				ManaColor.Green
			),
			["Meticulous Commons"] = DualLand(
				EntersUntappedIf.YouControlAPlaneswalker,
				ManaColor.White,
				ManaColor.Black
			),
			["Stingerquill Annex"] = DualLand(
				EntersUntappedIf.YouControlAPlaneswalker,
				ManaColor.Black,
				ManaColor.Red
			),
			["Theorix Annex"] = DualLand(
				EntersUntappedIf.YouControlAPlaneswalker,
				ManaColor.Blue,
				ManaColor.Black
			),
			["Transformative Commons"] = DualLand(
				EntersUntappedIf.YouControlAPlaneswalker,
				ManaColor.Green,
				ManaColor.Blue
			),
			["Vigorbloom Annex"] = DualLand(
				EntersUntappedIf.YouControlAPlaneswalker,
				ManaColor.Green,
				ManaColor.White
			),
			["Deserted Beach"] = DualLand(
				EntersUntappedIf.YouControlTwoOrMoreOtherLands,
				ManaColor.White,
				ManaColor.Blue
			),
			["Haunted Ridge"] = DualLand(
				EntersUntappedIf.YouControlTwoOrMoreOtherLands,
				ManaColor.Black,
				ManaColor.Red
			),
			["Overgrown Farmland"] = DualLand(
				EntersUntappedIf.YouControlTwoOrMoreOtherLands,
				ManaColor.Green,
				ManaColor.White
			),
			["Rockfall Vale"] = DualLand(
				EntersUntappedIf.YouControlTwoOrMoreOtherLands,
				ManaColor.Red,
				ManaColor.Green
			),
			["Shipwreck Marsh"] = DualLand(
				EntersUntappedIf.YouControlTwoOrMoreOtherLands,
				ManaColor.Blue,
				ManaColor.Black
			),

			// ===== Empower Jace (see EmpowerJace.cs) =====
			["Academic Ascent"] =
			[
				Spell(
					[Creature],
					new ModifyUntilEndOfTurn(Ref.Target(), 2, 2),
					new ModifyUntilEndOfTurn(Ref.Target(), 0, 0, "Flying"),
					new EmpowerJace(2)
				),
			],
			["Repurposed Enforcer"] =
			[
				When(TriggerEvent.SelfAttacks, [], new EmpowerJace(Count.CreaturesYouControl)),
			],
			["Way of the Healer"] =
			[
				When(TriggerEvent.SelfEnters, [], new EmpowerJace(5)),
				Grant(-2, [], new CreateTokens(Cadet, 1), new Surveil(1)),
			],
			["Way of the Mentor"] =
			[
				When(TriggerEvent.SelfEnters, [], new EmpowerJace(5)),
				When(TriggerEvent.YouGainLife, [], new AddLoyalty(Ref.PlaneswalkersYouControl, 1)),
			],
			["Mindseeker Oculus"] = [When(TriggerEvent.SelfEnters, [], new EmpowerJace(4))],
			["Protege's Awakening"] = [Spell([], new EmpowerJace(6), new DrawCards(Ref.You, 1))],
			["Sanctum Lurker"] =
			[
				When(TriggerEvent.SelfEnters, [], new EmpowerJace(1)),
				new YourPlaneswalkersSurviveZeroLoyalty(),
				Grant(2, [], new DealDamage(Ref.EachOpponent, 1), new GainLife(Ref.You, 1)),
			],
			["No Admittance"] =
			[
				Spell(
					[new TargetSpec(TargetKind.AnyTarget)],
					new DealDamage(Ref.Target(), 3),
					new EmpowerJace(1)
				),
			],
			["Way of the Pyromancer"] =
			[
				When(TriggerEvent.SelfEnters, [], new EmpowerJace(2)),
				Grant(1, [], new AddMana(ManaColor.Red)),
			],
			["Way of the Warlord"] =
			[
				When(TriggerEvent.SelfEnters, [], new EmpowerJace(5)),
				// "This planeswalker deals …" — the granted ability's source is the planeswalker.
				Grant(
					-4,
					[
						CreatureOrPlaneswalker with
						{
							Optional = true,
						},
						new TargetSpec(TargetKind.Player),
					],
					new DealDamage(Ref.Target(0), 2),
					new DealDamage(Ref.Target(1), 2)
				),
			],
			["Arcane Amphisbaena"] = [When(TriggerEvent.SelfEnters, [], new EmpowerJace(2))],
			["Way of the Wildspeaker"] =
			[
				When(TriggerEvent.SelfEnters, [], new EmpowerJace(7)),
				Grant(-4, [], new CreateTokens(Beast, 1)),
			],
			["Tam's Resistance"] =
			[
				Spell(
					[Creature with { Optional = true }],
					new AddCounters(Ref.Target(), 1),
					new ModifyUntilEndOfTurn(Ref.Target(), 0, 0, "Vigilance"),
					new EmpowerJace(4)
				),
			],
			["Avatar of Burgeoning Echoes"] =
			[
				When(TriggerEvent.LandYouControlEnters, [], new EmpowerJace(2)),
				Grant(-10, [Creature], new AddCounters(Ref.Target(), Count.LandsYouControl)),
			],
			["Keeper of the Quiet Hour"] = [When(TriggerEvent.SelfEnters, [], new EmpowerJace(2))],
		};

	public static CardDefinition Get(string name) =>
		new(Data[name], Scripts.TryGetValue(name, out var components) ? components : []);

	/// <summary>Scripted, or nothing in its text but keywords the engine already implements.</summary>
	public static bool IsImplemented(string name) =>
		Scripts.ContainsKey(name)
		|| Data[name].TypeLine.StartsWith("Basic Land") // mana from the subtype, CR 305.6
		|| (
			Data[name].CardFaces is null
			&& (Data[name].OracleText ?? "")
				.Split('\n', StringSplitOptions.RemoveEmptyEntries)
				.All(line => line.Split(", ").All(EngineKeywords.Contains))
		);

	public static IEnumerable<string> Scripted => Scripts.Keys;

	public static IEnumerable<string> AllNames => Data.Keys;
}
