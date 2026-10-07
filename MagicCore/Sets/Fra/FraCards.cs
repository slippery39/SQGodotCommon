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
