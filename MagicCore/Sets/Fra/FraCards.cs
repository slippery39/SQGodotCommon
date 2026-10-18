using System.Collections.Immutable;
using ImmutableGameObjects;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Filters;
using MagicCore.Targeting;
using MagicCore.Turns;

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

	/// <summary>"A 1/1 blue Illusion creature token."</summary>
	public static readonly CardData Illusion =
		new(
			"Illusion",
			"token",
			null,
			0,
			"Creature — Illusion",
			null,
			"1",
			"1",
			null,
			["U"],
			[],
			"token",
			"T",
			null
		);

	/// <summary>Verdant Kraken: "a 3/3 green Forest Tentacle land creature token" — Forest gives it {T}: Add {G}.</summary>
	public static readonly CardData ForestTentacle =
		new(
			"Forest Tentacle",
			"token",
			null,
			0,
			"Land Creature — Forest Tentacle",
			null,
			"3",
			"3",
			null,
			["G"],
			[],
			"token",
			"T",
			null
		);

	/// <summary>"Mowu, a legendary 3/3 green Dog creature token."</summary>
	public static readonly CardData Mowu =
		new(
			"Mowu",
			"token",
			null,
			0,
			"Legendary Creature — Dog",
			null,
			"3",
			"3",
			null,
			["G"],
			[],
			"token",
			"T",
			null
		);

	/// <summary>Lyra: "a 3/3 blue Angel creature token with flying".</summary>
	public static readonly CardData Angel =
		new(
			"Angel",
			"token",
			null,
			0,
			"Creature — Angel",
			null,
			"3",
			"3",
			null,
			["U"],
			["Flying"],
			"token",
			"T",
			null
		);

	/// <summary>Saheeli: "a 1/1 colorless Thopter artifact creature token with flying".</summary>
	public static readonly CardData Thopter =
		new(
			"Thopter",
			"token",
			null,
			0,
			"Artifact Creature — Thopter",
			null,
			"1",
			"1",
			null,
			[],
			["Flying"],
			"token",
			"T",
			null
		);

	/// <summary>Kiora of Fire and Ashes: "a 5/5 red Dragon creature token with flying".</summary>
	public static readonly CardData Dragon =
		new(
			"Dragon",
			"token",
			null,
			0,
			"Creature — Dragon",
			null,
			"5",
			"5",
			null,
			["R"],
			["Flying"],
			"token",
			"T",
			null
		);

	/// <summary>Kiora of Salt and Sand: "an 8/8 blue Leviathan creature token with hexproof".</summary>
	public static readonly CardData Leviathan =
		new(
			"Leviathan",
			"token",
			null,
			0,
			"Creature — Leviathan",
			null,
			"8",
			"8",
			null,
			["U"],
			["Hexproof"],
			"token",
			"T",
			null
		);

	static CardData Token(
		string name,
		string typeLine,
		string? power = null,
		string? toughness = null
	) =>
		new(
			name,
			"token",
			null,
			0,
			typeLine,
			null,
			power,
			toughness,
			null,
			[],
			[],
			"token",
			"T",
			null
		);

	/// <summary>"A Treasure token. (It's an artifact with '{T}, Sacrifice this token: Add one mana of any color.')"</summary>
	public static readonly CardData Treasure = Token("Treasure", "Artifact — Treasure");

	/// <summary>Vraska, Soul of Stone: "a 1/1 colorless Sculpture Treasure artifact creature token".</summary>
	public static readonly CardData SculptureTreasure = Token(
		"Sculpture Treasure",
		"Artifact Creature — Sculpture Treasure",
		"1",
		"1"
	);

	/// <summary>Kwia Vigorbloom: "a colorless artifact token named Lotus".</summary>
	public static readonly CardData Lotus = Token("Lotus", "Artifact");

	/// <summary>"{T}, Sacrifice this token: Add [N] mana of any (one) color." — Treasure's and Lotus's.</summary>
	static ManaOfOneColorComponent SacrificeForMana(int amount) =>
		new(amount) { SacrificeSelf = true };

	/// <summary>Ajani Resolute: "a 2/2 white Cat Soldier creature token named Ajani's Pridemate".</summary>
	public static readonly CardData AjanisPridemate =
		new(
			"Ajani's Pridemate",
			"token",
			null,
			0,
			"Creature — Cat Soldier",
			null,
			"2",
			"2",
			null,
			["W"],
			[],
			"token",
			"T",
			null
		);

	/// <summary>"Create a 4/4 green Beast creature token with trample."</summary>
	static CreateTokens CreateBeast => new(Beast, 1);

	/// <summary>Soul Tether: "a Heartwood token (a red and green artifact with '{T}: Add {R} or {G}.')".</summary>
	public static readonly CardData Heartwood =
		new(
			"Heartwood",
			"token",
			null,
			0,
			"Artifact — Heartwood",
			null,
			null,
			null,
			null,
			["R", "G"],
			[],
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

	static ImmutableArray<GameComponent> DualLand(Condition unless, ManaColor a, ManaColor b) =>
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

	/// <summary>"Target [filter]" — a permanent: Target(Is.Creature &amp; Is.Yours) is "target creature you control".</summary>
	static TargetSpec Target(Filter? filter = null) => TargetSpec.Permanent(filter);

	static readonly TargetSpec Creature = TargetSpec.Creature;

	/// <summary>"Permanent card" / "nonland permanent card". FRA has no battles.</summary>
	const CardType NonlandPermanentTypes =
		CardType.Creature | CardType.Artifact | CardType.Enchantment | CardType.Planeswalker;
	const CardType PermanentTypes = NonlandPermanentTypes | CardType.Land;

	/// <summary>"A nonland card": every card type but land.</summary>
	const CardType NonlandTypes =
		NonlandPermanentTypes
		| CardType.Instant
		| CardType.Sorcery
		| CardType.Kindred
		| CardType.Battle;
	static readonly TargetSpec CreatureOrPlaneswalker = TargetSpec.CreatureOrPlaneswalker;

	static SpellEffectsComponent Spell(TargetSpec[] targets, params GameAction[] effects) =>
		new([.. effects]) { Targets = [.. targets] };

	static TriggeredAbilityComponent When(
		Trigger on,
		TargetSpec[] targets,
		params GameAction[] effects
	) => new(on, [.. effects]) { Targets = [.. targets] };

	/// <summary>
	/// "Prowess (Whenever you cast a noncreature spell, this creature gets +1/+1 until end of
	/// turn.)" A scripted trigger, not an engine keyword: Ruric Thar has it twice, and each instance
	/// triggers separately (CR 702.108b).
	/// </summary>
	static TriggeredAbilityComponent Prowess =>
		When(TriggerEvent.YouCastNoncreatureSpell, [], new ModifyUntilEndOfTurn(Ref.Source, 1, 1));

	// ===== Prepare: a card's spell half, from its second face in FRA.json =====

	static readonly EntersPreparedComponent EntersPrepared = new();

	static PrepareSpellComponent PrepareSpell(
		string card,
		TargetSpec[] targets,
		params GameAction[] effects
	) => new(Data[card].CardFaces![1], Spell(targets, effects));

	/// <summary>"At the beginning of your upkeep, if this creature isn't prepared, it becomes prepared."</summary>
	static TriggeredAbilityComponent PreparesAtYourUpkeep =>
		When(TriggerEvent.YourUpkeep, [], new BecomePrepared(Ref.Source)) with
		{
			If = new Not(new Matches(Ref.Source, Is.Prepared)),
		};

	/// <summary>Threshold — "as long as there are seven or more cards in your graveyard".</summary>
	static readonly AtLeast Threshold = new(Count.NumberOf(Ref.CardsInYourGraveyard), 7);

	/// <summary>Dual lands: "enters tapped unless you control a planeswalker" — judged as this land enters.</summary>
	static readonly Condition YouControlAPlaneswalker = new AtLeast(
		Count.NumberOf(Ref.PlaneswalkersYouControl),
		1
	);

	/// <summary>"Enters tapped unless you control two or more other lands".</summary>
	static readonly Condition YouControlTwoOrMoreOtherLands = new AtLeast(
		Count.NumberOf(Ref.Permanents(Is.Land & Is.Yours & !Is.Source)),
		2
	);

	/// <summary>"As long as you control a Jace planeswalker".</summary>
	static readonly Condition YouControlAJace = new AtLeast(
		Count.NumberOf(Ref.Permanents(Is.Planeswalker & Is.Yours & Is.Subtype("Jace"))),
		1
	);

	/// <summary>Seed Suture: "Put a +1/+1 counter on target creature. You gain 1 life."</summary>
	static PrepareSpellComponent SeedSuture(string card) =>
		PrepareSpell(card, [Creature], new AddCounters(Ref.Target(), 1), new GainLife(Ref.You, 1));

	/// <summary>Peer Review: "Create a ... Cadet. Surveil 1."</summary>
	static PrepareSpellComponent PeerReview(string card) =>
		PrepareSpell(card, [], new CreateTokens(Cadet, 1), new Surveil(1));

	/// <summary>Vicious Verse: "deals 1 damage to target opponent."</summary>
	static PrepareSpellComponent ViciousVerse(string card) =>
		PrepareSpell(card, [TargetSpec.Player(Is.Opponents)], new DealDamage(Ref.Target(), 1));

	/// <summary>Omit Variables: "Mill three cards."</summary>
	static PrepareSpellComponent OmitVariables(string card) =>
		PrepareSpell(card, [], new Mill(Ref.You, 3));

	/// <summary>"Create a Heartwood token. (It's a red and green artifact with '{T}: Add {R} or {G}.')"</summary>
	static CreateTokens CreateHeartwood =>
		new(Heartwood, 1)
		{
			Abilities =
			[
				new ManaAbilityComponent(ManaColor.Red),
				new ManaAbilityComponent(ManaColor.Green),
			],
		};

	/// <summary>"The number of artifacts you control".</summary>
	static readonly Count ArtifactsYouControl = Count.NumberOf(
		Ref.Permanents(Is.Artifact & Is.Yours)
	);

	/// <summary>"{T}: Add one mana of any color." — five abilities, one per colour, sharing a restriction.</summary>
	static GameComponent[] AnyColorMana(ManaRestriction restriction) =>
		[
			.. ManaAbilities.AnyColor.Select(c => new ManaAbilityComponent(c)
			{
				Restriction = restriction,
			}),
		];

	/// <summary>Soul Tether: "Create a Heartwood token."</summary>
	static PrepareSpellComponent SoulTether(string card) => PrepareSpell(card, [], CreateHeartwood);

	/// <summary>"Basic landcycling {2} ({2}, Discard this card: Search your library for a basic land card, reveal it, put it into your hand, then shuffle.)"</summary>
	static ActivatedAbilityComponent BasicLandcycling =>
		new(
			ManaCost.Parse("{2}"),
			[
				new SearchLibrary(CardType.Land, ZoneType.Hand)
				{
					Supertype = Supertype.Basic,
					Reveal = true,
				},
			]
		)
		{
			Costs = [new DiscardSelfCost()],
			From = ZoneType.Hand,
		};

	/// <summary>"[Cost]: Return this card from your graveyard to the battlefield with a finality counter on it."</summary>
	static ActivatedAbilityComponent ReturnsWithFinality(string cost, Condition onlyIf) =>
		new(
			ManaCost.Parse(cost),
			[new ReturnFromGraveyard(Ref.Source, ZoneType.Battlefield) { Finality = true }]
		)
		{
			From = ZoneType.Graveyard,
			OnlyIf = onlyIf,
		};

	/// <summary>
	/// "Ward {N} (Whenever this creature becomes the target of a spell or ability an opponent controls,
	/// counter it unless that player pays {N}.)" "It" is Ref.EventObject — the targeting spell or ability.
	/// </summary>
	static TriggeredAbilityComponent Ward(string cost) =>
		When(
			TriggerEvent.SelfBecomesTargetOfOpponentsSpellOrAbility,
			[],
			new CounterUnlessPays(Ref.EventObject, ManaCost.Parse(cost))
		);

	/// <summary>"Equip {N} ({N}: Attach to target creature you control. Equip only as a sorcery.)"</summary>
	static ActivatedAbilityComponent Equip(string cost) =>
		new(ManaCost.Parse(cost), [new AttachSourceTo(Ref.Target())])
		{
			SorcerySpeed = true,
			Targets = [Target(Is.Creature & Is.Yours)],
		};

	/// <summary>"Equipped/enchanted creature gets +P/+T."</summary>
	static StaticEffectComponent AttachedGets(int power, int toughness) =>
		new(Is.AttachedToSource) { Power = power, Toughness = toughness };

	/// <summary>
	/// The Teyos: "When Teyo enters, target permanent you control gains [keyword] until end of turn.
	/// Put a +1/+1 counter on it if it's a creature. Put a loyalty counter on it if it's a planeswalker."
	/// Each counter effect skips the wrong permanent type.
	/// </summary>
	static TriggeredAbilityComponent TeyoEnters(string keyword) =>
		When(
			TriggerEvent.SelfEnters,
			[Target(Is.Yours)],
			new ModifyUntilEndOfTurn(Ref.Target(), 0, 0, keyword),
			new AddCounters(Ref.Target(), 1),
			new AddLoyalty(Ref.Target(), 1)
		);

	/// <summary>One bullet: its target words and effects. Its text is filled in from the card.</summary>
	static Mode M(TargetSpec[] targets, params GameAction[] effects) =>
		new("", [.. targets], [.. effects]);

	/// <summary>"Choose one/two —" spell. The modes must match the printed bullets one for one.</summary>
	static SpellEffectsComponent ModalSpell(string name, int choose, params Mode[] modes) =>
		new([]) { Modes = Printed(name, modes), Choose = choose };

	static TriggeredAbilityComponent ModalWhen(
		string name,
		Trigger on,
		int choose,
		params Mode[] modes
	) => new(on, []) { Modes = Printed(name, modes), Choose = choose };

	/// <summary>Names each mode with its printed bullet — and refuses a script whose count differs.</summary>
	static ImmutableList<Mode> Printed(string name, Mode[] modes)
	{
		var bullets = (Data[name].OracleText ?? "")
			.Split('\n')
			.Where(l => l.StartsWith('•'))
			.ToList();
		if (bullets.Count != modes.Length)
			throw new InvalidOperationException(
				$"{name}: {modes.Length} modes scripted, {bullets.Count} printed"
			);
		return [.. modes.Select((m, i) => m with { Text = bullets[i] })];
	}

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
						new Matches(Ref.Target(0), !Is.Attacking),
						[new DrawCards(Ref.ControllerOfTarget(0), 1)]
					),
					new Destroy(Ref.Target())
				),
			],
			["Thalia, the Survivor"] = [new CostChange(1, !Is.Creature) { By = Whose.Opponent }],

			// ===== Black =====
			["Last Gasp"] = [Spell([Creature], new ModifyUntilEndOfTurn(Ref.Target(), -3, -3))],
			["Terminal Criticism"] =
			[
				Spell(
					[
						Target(
							(Is.Creature | Is.Planeswalker)
								& (Is.Color(ManaColor.Blue) | Is.Color(ManaColor.Red))
						),
					],
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
				new CostChange(-1)
				{
					OnItself = true,
					If = new AtLeast(
						Count.NumberOf(Ref.Permanents(Is.Creature & Is.Legendary & Is.Yours)),
						1
					),
				},
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
					new ModifyUntilEndOfTurn(Ref.Source, Count.PowerOf(Ref.EventCard), 0)
				),
			],
			["Koth, the Geomancer"] =
			[
				When(
					TriggerEvent.LandYouControlEnters,
					[],
					new DealDamage(Ref.EachOpponent, 1),
					new If(
						new Matches(Ref.EventCard, Is.Subtype("Mountain")),
						[new AddMana(ManaColor.Red)]
					)
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
						Target(Is.Creature & Is.Yours & !Is.Source),
						Target(Is.Creature & Is.Opponents) with
						{
							Optional = true,
						},
					],
					new Fight(Ref.Target(0), Ref.Target(1))
				),
				new ActivatedAbilityComponent(
					ManaCost.Parse("{6}"),
					[new AddCounters(Ref.Target(), 1)]
				)
				{
					Targets = [Target(Is.Creature & !Is.Legendary)],
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
				YouControlAPlaneswalker,
				ManaColor.Red,
				ManaColor.White
			),
			["Fatehold Annex"] = DualLand(YouControlAPlaneswalker, ManaColor.White, ManaColor.Blue),
			["Formidable Commons"] = DualLand(
				YouControlAPlaneswalker,
				ManaColor.Black,
				ManaColor.Green
			),
			["Innovative Commons"] = DualLand(
				YouControlAPlaneswalker,
				ManaColor.Blue,
				ManaColor.Red
			),
			["Konstrari Annex"] = DualLand(YouControlAPlaneswalker, ManaColor.Red, ManaColor.Green),
			["Meticulous Commons"] = DualLand(
				YouControlAPlaneswalker,
				ManaColor.White,
				ManaColor.Black
			),
			["Stingerquill Annex"] = DualLand(
				YouControlAPlaneswalker,
				ManaColor.Black,
				ManaColor.Red
			),
			["Theorix Annex"] = DualLand(YouControlAPlaneswalker, ManaColor.Blue, ManaColor.Black),
			["Transformative Commons"] = DualLand(
				YouControlAPlaneswalker,
				ManaColor.Green,
				ManaColor.Blue
			),
			["Vigorbloom Annex"] = DualLand(
				YouControlAPlaneswalker,
				ManaColor.Green,
				ManaColor.White
			),
			["Deserted Beach"] = DualLand(
				YouControlTwoOrMoreOtherLands,
				ManaColor.White,
				ManaColor.Blue
			),
			["Haunted Ridge"] = DualLand(
				YouControlTwoOrMoreOtherLands,
				ManaColor.Black,
				ManaColor.Red
			),
			["Overgrown Farmland"] = DualLand(
				YouControlTwoOrMoreOtherLands,
				ManaColor.Green,
				ManaColor.White
			),
			["Rockfall Vale"] = DualLand(
				YouControlTwoOrMoreOtherLands,
				ManaColor.Red,
				ManaColor.Green
			),
			["Shipwreck Marsh"] = DualLand(
				YouControlTwoOrMoreOtherLands,
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
					[TargetSpec.AnyTarget()],
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
						Target(Is.Creature | Is.Planeswalker) with
						{
							Optional = true,
						},
						TargetSpec.Player(),
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

			// ===== Modal: "Choose one/two —" (Modes.cs) =====
			["Surgical Precision"] =
			[
				ModalSpell(
					"Surgical Precision",
					1,
					M(
						[Target(Is.Creature & Is.ToughnessAtLeast(4))],
						new Destroy(Ref.Target()),
						new GainLife(Ref.You, 1)
					),
					M([], new DrawCards(Ref.You, 1), new GainLife(Ref.You, 2))
				),
			],
			["Divining Duelist"] =
			[
				ModalWhen(
					"Divining Duelist",
					TriggerEvent.SelfEnters,
					1,
					M([Creature], new Tap(Ref.Target())),
					M([Creature], new Untap(Ref.Target())),
					M([], new DrawCards(Ref.You, 1), new Discard(Ref.You, 1))
				),
			],
			["Perfected Theory"] =
			[
				ModalSpell(
					"Perfected Theory",
					1,
					M(
						[Creature],
						new ModifyUntilEndOfTurn(Ref.Target(), 0, 0) { SetBase = (1, 1) }
					),
					M([Creature], new ModifyUntilEndOfTurn(Ref.Target(), 0, 0) { SetBase = (4, 5) })
				),
			],
			["Rise of the Deathbringer"] =
			[
				// ponytail: "lose life equal to the number of cards drawn" reads X; a library shorter
				// than X draws fewer. Count the cards actually drawn if that ever matters.
				ModalSpell(
					"Rise of the Deathbringer",
					1,
					M(
						[],
						new DrawCards(Ref.You, Count.GreatestPowerOf(Ref.CreaturesYouControl)),
						new LoseLife(Ref.You, Count.GreatestPowerOf(Ref.CreaturesYouControl))
					),
					M([], new ModifyUntilEndOfTurn(Ref.AllCreatures, -3, -3))
				),
			],
			["Vraska's Final Mercy"] =
			[
				ModalSpell(
					"Vraska's Final Mercy",
					1,
					M(
						[CreatureOrPlaneswalker],
						new LoseLife(Ref.You, 2),
						new Destroy(Ref.Target())
					),
					M([], new LoseLife(Ref.You, 2), new EmpowerJace(6))
				),
			],
			["Fulminous Forte"] =
			[
				ModalSpell(
					"Fulminous Forte",
					1,
					M(
						[],
						new DealDamage(
							Ref.OpponentsPermanents(CardType.Creature | CardType.Planeswalker),
							1
						)
					),
					M([CreatureOrPlaneswalker], new DealDamage(Ref.Target(), 5))
				),
			],
			["Compel Brutality"] =
			[
				ModalSpell(
					"Compel Brutality",
					1,
					M(
						[
							Target(Is.Creature & Is.Yours),
							Target((Is.Creature | Is.Planeswalker) & Is.Opponents),
						],
						new DamageFrom(Ref.Target(0), Ref.Target(1), Count.PowerOf(Ref.Target(0)))
					),
					M(
						[
							Target(Is.Planeswalker & Is.Yours),
							Target((Is.Creature | Is.Planeswalker) & Is.Opponents),
						],
						new DamageFrom(Ref.Target(0), Ref.Target(1), Count.LoyaltyOf(Ref.Target(0)))
					)
				),
			],
			["Charge the Sanctum"] =
			[
				ModalSpell(
					"Charge the Sanctum",
					1,
					M([], new ModifyUntilEndOfTurn(Ref.CreaturesYouControl, 2, 0)),
					M(
						[Creature],
						new ModifyUntilEndOfTurn(Ref.Target(), 2, 0, "First strike"),
						new AddCounters(Ref.Target(), 1)
					)
				),
			],
			["Craftwork Crusher"] =
			[
				ModalWhen(
					"Craftwork Crusher",
					TriggerEvent.SelfEnters,
					2,
					M([CreatureOrPlaneswalker], new DealDamage(Ref.Target(), 4)),
					M([], new CreateTokens(Cadet, 1)),
					M([], new DrawCards(Ref.You, 1))
				),
			],
			["Fatehold Charm"] =
			[
				ModalSpell(
					"Fatehold Charm",
					1,
					M([], new DrawCards(Ref.You, 1), new EmpowerJace(2)),
					M(
						[TargetSpec.Spell() with { Or = TargetSpec.Creature }],
						new ReturnToHand(Ref.Target())
					),
					M([], new ModifyUntilEndOfTurn(Ref.CreaturesYouControl, 1, 2))
				),
			],
			["Konstrari Charm"] =
			[
				ModalSpell(
					"Konstrari Charm",
					1,
					M([Target(Is.Creature & Is.With("Flying"))], new DealDamage(Ref.Target(), 6)),
					M(
						[Creature],
						new AddCounters(Ref.Target(), 2),
						new ModifyUntilEndOfTurn(Ref.Target(), 0, 0, "Trample")
					),
					M([], new AddMana(ManaColor.Colorless, 3))
				),
			],
			["Stingerquill Charm"] =
			[
				ModalSpell(
					"Stingerquill Charm",
					1,
					M([TargetSpec.AnyTarget()], new DealDamage(Ref.Target(), 3)),
					M(
						[Creature],
						new ModifyUntilEndOfTurn(Ref.Target(), 0, 0, "First strike"),
						new ModifyUntilEndOfTurn(Ref.Target(), 0, 0, "Deathtouch")
					),
					M([], new CreateTokens(Cadet, 1) { GainsUntilEndOfTurn = "Haste" })
				),
			],
			["Archive Arbiter"] =
			[
				ModalWhen(
					"Archive Arbiter",
					TriggerEvent.SelfEnters,
					1,
					M(
						[Target(!Is.Type(CardType.Creature | CardType.Land))],
						new Destroy(Ref.Target())
					),
					M([], new GainLife(Ref.You, 4))
				),
			],
			["Yuriko, Hope from the Shadows"] =
			[
				ModalWhen(
					"Yuriko, Hope from the Shadows",
					TriggerEvent.SelfEnters,
					1,
					M(
						[Creature],
						new ModifyUntilEndOfTurn(
							Ref.Target(),
							Count.NumberOf(Ref.CardsInYourGraveyard).Negated,
							0
						)
					),
					M([], new Surveil(2))
				),
			],

			// ===== Sacrifice, dies, exile-until-leaves =====
			["Budding Insurgent"] =
			[
				new ActivatedAbilityComponent(
					ManaCost.None,
					[
						// "If that permanent was a legendary enchantment" — read before it is destroyed.
						new If(
							new Matches(Ref.Target(0), Is.Enchantment & Is.Legendary),
							[new DrawCards(Ref.You, 1)]
						),
						new Destroy(Ref.Target()),
					]
				)
				{
					Costs = [new SacrificeSelfCost()],
					SorcerySpeed = true,
					Targets = [Target(Is.Type(CardType.Artifact | CardType.Enchantment))],
				},
			],
			["Living Library"] =
			[
				new ActivatedAbilityComponent(
					ManaCost.Parse("{6}"),
					[new ShuffleIntoLibrary(Ref.Target())]
				)
				{
					Costs = [new SacrificeSelfCost()],
					Targets = [Target((Is.Creature | Is.Planeswalker) & Is.Opponents)],
				},
			],
			["Marwyn, the Clearcutter"] =
			[
				new ActivatedAbilityComponent(ManaCost.Parse("{2}"), [new DrawCards(Ref.You, 1)])
				{
					Costs =
					[
						new TapSelfCost(),
						new SacrificeCost(Is.Type(CardType.Artifact | CardType.Land)),
					],
				},
			],
			["Edgar, Ancient Bloodlord"] =
			[
				When(
					TriggerEvent.AnotherCreatureOrPlaneswalkerYouControlDies,
					[],
					new GainLife(Ref.You, 1)
				),
				new ActivatedAbilityComponent(
					ManaCost.Parse("{2}"),
					[
						new AddCounters(Ref.Source, 1),
						new ModifyUntilEndOfTurn(Ref.Source, 0, 0, "Menace"),
					]
				)
				{
					Costs = [new SacrificeCost((Is.Creature | Is.Planeswalker) & !Is.Source)],
				},
			],
			["Break Under Pressure"] =
			[
				Spell(
					[TargetSpec.Player(Is.Opponents)],
					new SacrificeGreatestManaValue(
						Ref.Target(),
						CardType.Creature | CardType.Planeswalker
					),
					new GainLife(Ref.You, 2)
				),
			],
			["Silence the Echo"] =
			[
				new AdditionalCostsComponent(
					[new SacrificeCost(Is.Creature | Is.Planeswalker) { OrPay = 3 }]
				),
				Spell([CreatureOrPlaneswalker], new Destroy(Ref.Target())),
			],
			["Way of the Deathbringer"] =
			[
				When(TriggerEvent.SelfEnters, [], new EmpowerJace(5)),
				Grant(-2, [], new MaySacrifice(Is.Creature, [new CreateTokens(Beast, 1)])),
			],
			["Graft Surgeon"] =
			[
				new EntersWithCountersComponent(1),
				When(
					TriggerEvent.SelfDies,
					[Target(Is.Creature & Is.Yours) with { Optional = true }],
					new PutEventCardCounters(Ref.Target())
				),
			],
			["Massacre Girl, Most Wanted"] =
			[
				When(
					TriggerEvent.AnotherCreatureOrPlaneswalkerYouControlDies,
					[TargetSpec.Player(Is.Opponents)],
					new DealDamage(Ref.Target(), 1),
					new GainLife(Ref.You, 1)
				),
				When(TriggerEvent.OpponentDealtNoncombatDamage, [], new AddCounters(Ref.Source, 1)),
			],
			["Way of the Necromancer"] =
			[
				When(TriggerEvent.SelfEnters, [], new EmpowerJace(2)),
				When(
					TriggerEvent.CreatureYouControlDies,
					[],
					new AddLoyalty(Ref.PlaneswalkersYouControl, 1)
				),
			],
			["Memory Trap"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[Target(Is.Opponents & !Is.Land)],
					new ExileUntilSourceLeaves(Ref.Target())
				),
			],
			// ===== Continuous effects (Rules/Characteristics.cs) =====
			["Samut, Hazoret's Champion"] =
			[
				new StaticEffectComponent(Is.Creature & Is.Yours, "Haste"),
			],
			["Ajani's Anguish"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[TargetSpec.AnyTarget()],
					new DealDamage(Ref.Target(), Count.X)
				),
				new StaticEffectComponent(Is.Creature & Is.Yours, "Trample"),
			],
			["Ghalta the Unstoppable"] =
			[
				new CostChange(Count.GreatestPowerOf(Ref.CreaturesYouControl).Negated)
				{
					OnItself = true,
				},
				new StaticEffectComponent(Is.Creature & Is.Yours & !Is.Source, "Trample"),
			],
			["Skilled Battlecarver"] =
			[
				new StaticEffectComponent(Is.Source, "First strike") { If = new DuringYourTurn() },
				new ActivatedAbilityComponent(
					ManaCost.Parse("{1}{R}"),
					[new ModifyUntilEndOfTurn(Ref.Source, 1, 0)]
				),
			],
			["Mind Meanderer"] =
			[
				new StaticEffectComponent(Is.Source, "Vigilance") { If = YouControlAJace },
				When(
					TriggerEvent.SelfEnters,
					[Target(Is.Creature & Is.Opponents) with { Optional = true }],
					new Fight(Ref.Source, Ref.Target())
				),
			],
			["Winter, Tormented Loner"] =
			[
				// ponytail: "When you do" is a reflexive trigger; here it happens inline, with no
				// chance to respond between the sacrifice and the opponents' sacrifices.
				When(
					TriggerEvent.SelfEnters,
					[],
					new MaySacrifice(
						Is.Creature | Is.Planeswalker,
						[new EachSacrifices(Ref.EachOpponent, Is.Creature)]
					)
				),
				new StaticEffectComponent(Is.Source)
				{
					Power = Count.NumberOf(
						Ref.YourGraveyard(CardType.Creature | CardType.Planeswalker)
					),
				},
			],
			["Tetsuko Umezawa, Fugitive"] =
			[
				new StaticEffectComponent(
					Is.Creature & Is.Yours & (Is.PowerAtMost(1) | Is.ToughnessAtMost(1)),
					Combat.CantBeBlocked
				),
			],
			["Eardrum Rattler"] =
			[
				new ActivatedAbilityComponent(
					ManaCost.Parse("{1}"),
					[new ModifyUntilEndOfTurn(Ref.Target(), 0, 0, Combat.CantBeBlocked)]
				)
				{
					Costs = [new TapSelfCost()],
					Targets = [Target(Is.Creature & Is.Yours & !Is.Source & Is.PowerAtMost(2))],
				},
			],

			// ===== Step triggers, per-turn tracking, prowess =====
			["Verdant Kraken"] =
			[
				When(TriggerEvent.EachUpkeep, [], new CreateTokens(ForestTentacle, 1)),
			],
			["Frostbite Pyromental"] =
			[
				When(TriggerEvent.SelfDealsCombatDamageToPlayer, [], new DrawCards(Ref.You, 2)),
				When(TriggerEvent.EachEndStep, [], new Sacrifice(Ref.Source)),
			],
			["Eye of Jace"] =
			[
				When(
					TriggerEvent.YourUpkeep,
					[],
					new Surveil(1),
					new If(
						new AtLeast(Count.NumberOf(Ref.CardsInYourGraveyard), 7),
						[
							new Sacrifice(Ref.Source),
							new DealDamage(Ref.EachOpponent, 2),
							new GainLife(Ref.You, 2),
						]
					)
				),
			],
			["Traxos, Academy Guardian"] =
			[
				new CostChange(-2)
				{
					OnItself = true,
					If = new AtLeast(Count.NoncreatureSpellsYouCastThisTurn, 1),
				},
				Prowess,
			],
			["Edgar, Moonlit Sovereign"] =
			[
				When(TriggerEvent.YourEndStep, [], new AddCounters(Ref.Source, 2)) with
				{
					If = new Not(new AtLeast(Count.SpellsYouCastThisTurn, 1)),
				},
				new ActivatedAbilityComponent(
					ManaCost.Parse("{4}{G}"),
					[new AddCounters(Ref.YourCreaturesWithCounters, 1)]
				),
			],
			["Tetsuko Umezawa, Pursuer"] =
			[
				Prowess,
				When(
					new Trigger(EventKind.Blocks)
					{
						Subject = Is.Opponents & (Is.PowerAtMost(1) | Is.ToughnessAtMost(1)),
					},
					[],
					new DealDamage(Ref.ControllerOfEventCard, 1)
				),
			],
			["Jiang Yanggu, Never Alone"] =
			[
				When(TriggerEvent.SelfEnters, [], new CreateTokens(Mowu, 1)),
				When(TriggerEvent.YourEndStep, [], new Untap(Ref.TokensYouControl)),
			],
			["Way of the Mind Sculptor"] =
			[
				When(TriggerEvent.SelfEnters, [], new EmpowerJace(5)),
				When(
					TriggerEvent.YouActivateLoyaltyAbilityRemovingTwoOrMore,
					[],
					new DrawCards(Ref.You, 1)
				),
			],
			["The Theorist, Jace Beleren"] =
			[
				When(TriggerEvent.OpponentsDrawStep, [], new DrawCards(Ref.You, 1)),
				Loyalty(1, [], new CreateTokens(Illusion, 1)),
				Loyalty(
					-2,
					[
						Target(Is.Type(CardType.Artifact | CardType.Creature) & Is.Opponents) with
						{
							Optional = true,
						},
					],
					new ReturnToHand(Ref.Target())
				),
				Loyalty(
					-6,
					[],
					new DrawCards(Ref.You, 3),
					new AddCounters(Ref.CreaturesYouControl, Count.NumberOf(Ref.CardsInYourHand))
				),
			],
			["Ajani Unrelenting"] =
			[
				When(TriggerEvent.YouActivateLoyaltyAbility, [], new CreateTokens(Cadet, 1)),
				Loyalty(1, [], new ModifyUntilEndOfTurn(Ref.CreaturesYouControl, 1, 0, "Haste")),
				Loyalty(
					-2,
					[],
					new DiscardHand(Ref.You),
					new DrawCards(Ref.You, Count.CreaturesYouControl)
				),
				Loyalty(-3, [], new DealDamage(Ref.CreaturesExceptYourTokens, 4)),
			],
			["Guiding Hydra"] =
			[
				new EntersWithCountersComponent(Count.X),
				When(
					TriggerEvent.BeginningOfCombatOnYourTurn,
					[],
					new May(
						"Remove a +1/+1 counter from Guiding Hydra?",
						new AtLeast(Count.CountersOn(Ref.Source), 1),
						[
							new AddCounters(Ref.Source, -1),
							new AddCounters(Ref.OtherCreaturesYouControl, 1),
						]
					)
				),
			],
			["Desperate Futurescribe"] =
			[
				When(
					TriggerEvent.BeginningOfCombatOnYourTurn,
					[Target(Is.Creature & Is.Yours & !Is.Source)],
					new If(
						new AtLeast(Count.YourSurveilsThisTurn, 1),
						[new AddCounters(Ref.Target(), 1)]
					)
					{
						Otherwise = [new ModifyUntilEndOfTurn(Ref.Target(), 1, 1)],
					}
				),
			],
			["Darklight Phoenix"] =
			[
				When(
					TriggerEvent.BeginningOfCombatOnYourTurn,
					[],
					new ReturnFromGraveyard(Ref.Source, ZoneType.Battlefield)
				) with
				{
					If = new AtLeast(Count.CreaturesDiedThisTurn, 2),
					FromGraveyard = true,
				},
			],
			["Ruric Thar, Biomagus"] =
			[
				Prowess,
				Prowess,
				When(
					TriggerEvent.SelfBecomesTargetOfOpponentsSpellOrAbility,
					[],
					new DrawCards(Ref.You, 1)
				),
			],
			["Lyra, Tolarian Archangel"] =
			[
				When(TriggerEvent.EachEndStep, [], new CreateTokens(Angel, 1)) with
				{
					If = new AtLeast(Count.CardsYouDrewThisTurn, 3),
				},
				new ActivatedAbilityComponent(
					ManaCost.Parse("{3}{U}{U}"),
					[
						new ModifyUntilEndOfTurn(Ref.Source, 0, 0)
						{
							Trigger = When(
								TriggerEvent.SelfDealsCombatDamageToPlayer,
								[],
								new DrawCards(Ref.You, 2)
							),
						},
					]
				),
			],
			["Germinate Recruits"] =
			[
				Spell([], new CreateTokens(Cadet, Count.LifeYouGainedThisTurn)),
			],
			["Enlightened Confidant"] =
			[
				When(
					TriggerEvent.YourEndStep,
					[],
					new Surveil(1)
					{
						Then =
						[
							new ReturnChosenToHandIfManaValueAtMost(Count.LifeYouGainedThisTurn),
						],
					}
				) with
				{
					If = new AtLeast(Count.LifeYouGainedThisTurn, 1),
				},
			],
			["Surveillance Phantasm"] =
			[
				new StaticEffectComponent(Is.Source, Combat.AttacksDespiteDefender)
				{
					If = new AtLeast(Count.YourSurveilsThisTurn, 1),
				},
				new ActivatedAbilityComponent(ManaCost.Parse("{3}{U}"), [new Surveil(1)]),
			],
			["Way of the Paradox"] =
			[
				When(TriggerEvent.SelfEnters, [], new EmpowerJace(5)),
				When(
					TriggerEvent.YouActivateLoyaltyAbility,
					[],
					new GainLife(Ref.You, 1),
					new AdditionalLandDrop()
				),
			],

			// ===== Whenever you surveil, entered this turn, last turn =====
			["Denzilore Fatehold"] =
			[
				When(TriggerEvent.YouSurveil, [], new AddCounters(Ref.CreaturesYouControl, 1)),
			],
			["Saheeli, Consul of Oversight"] =
			[
				When(TriggerEvent.YouSurveil, [], new CreateTokens(Thopter, 1)) with
				{
					OncePerTurn = true,
				},
			],
			["Proft, Consulting Detective"] =
			[
				When(
					TriggerEvent.YouSurveil,
					[],
					new May(
						"Pay {2}: a +1/+1 counter on Proft and draw a card?",
						new CanPay(ManaCost.Parse("{2}")),
						[
							new PayMana(
								ManaCost.Parse("{2}"),
								[new AddCounters(Ref.Source, 1), new DrawCards(Ref.You, 1)]
							),
						]
					)
				),
			],
			["Gallia, the Merrymaker"] =
			[
				new StaticEffectComponent(
					Is.Creature & Is.Yours & !Is.Source & Is.WithPlusOneCounter,
					"Haste"
				),
				new ActivatedAbilityComponent(
					ManaCost.Parse("{1}{R}"),
					[new AddCounters(Ref.Target(), 1)]
				)
				{
					Costs = [new TapSelfCost()],
					Targets = [Target(Is.Creature & Is.EnteredThisTurn)],
				},
			],
			["Mabel, Valley Hero"] =
			[
				When(
					TriggerEvent.CreatureYouControlEnters,
					[Target(Is.Creature & Is.EnteredThisTurn)],
					new AddCounters(Ref.Target(), 1)
				),
			],
			["Command the Stage"] =
			[
				// "Create a Cadet, then a counter on each OTHER Wizard token" — counters first, then
				// the Cadet: the same result without remembering which token is new.
				Spell(
					[],
					new AddCounters(Ref.Permanents(Is.Token & Is.Yours & Is.Subtype("Wizard")), 1),
					new CreateTokens(Cadet, 1)
				),
				When(
					TriggerEvent.EachUpkeep,
					[],
					new ReturnFromGraveyard(Ref.Source, ZoneType.Hand)
				) with
				{
					If = new AtLeast(Count.NoncombatDamageToOpponentsLastTurn, 1),
					FromGraveyard = true,
				},
			],

			// ===== Flashback =====
			["Generous Revival"] =
			[
				Spell(
					[TargetSpec.InYourGraveyard(Is.Creature & Is.ManaValueAtMost(3))],
					new ReturnFromGraveyard(Ref.Target(), ZoneType.Battlefield) { Counters = 1 }
				),
				new FlashbackComponent(ManaCost.Parse("{4}{W}")),
			],
			["Predictive Preparations"] =
			[
				Spell(
					[Creature, Creature with { Optional = true, NotThePrevious = true }],
					new AddCounters(Ref.Target(0), 1),
					new AddCounters(Ref.Target(1), 1)
				),
				new FlashbackComponent(ManaCost.Parse("{3}{W}")),
			],
			["Stingcaster Mage"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[TargetSpec.InYourGraveyard(Is.Type(CardType.Instant | CardType.Sorcery))],
					new GainFlashbackThisTurn(Ref.Target())
				),
			],
			["Bestial Incursion"] =
			[
				Spell([], new CreateTokens(Beast, 1)),
				new FlashbackComponent(ManaCost.Parse("{5}{G}")),
			],
			["Recursive Recruitment"] =
			[
				Spell(
					[],
					new If(
						new CastFrom(ZoneType.Graveyard),
						[
							new CreateTokens(Cadet, 2)
							{
								Counters = Count.NumberOf(Ref.CardsInYourGraveyard) with
								{
									Per = 3,
								},
							},
						]
					)
					{
						Otherwise = [new CreateTokens(Cadet, 2)],
					}
				),
				new FlashbackComponent(ManaCost.Parse("{6}{U}{B}")),
			],
			["Twinned Vision"] =
			[
				Spell(
					[],
					new If(new Not(new CastFrom(ZoneType.Hand)), [new DrawCards(Ref.You, 2)])
					{
						Otherwise = [new DrawCards(Ref.You, 1)],
					}
				),
				new FlashbackComponent(ManaCost.Parse("{1}{U/R}{U/R}"))
				{
					Costs = [new DiscardCost()],
				},
			],

			// ===== Prepare =====
			["Blossom-Blessed Angel // Seed Suture"] =
			[
				EntersPrepared,
				SeedSuture("Blossom-Blessed Angel // Seed Suture"),
			],
			["Diviner of Victory // Unwind History"] =
			[
				EntersPrepared,
				When(TriggerEvent.YouSurveil, [], new ModifyUntilEndOfTurn(Ref.Source, 1, 1)),
				PrepareSpell(
					"Diviner of Victory // Unwind History",
					[Target(Is.Creature & Is.Opponents & Is.ManaValueAtMost(3))],
					new ReturnToHand(Ref.Target()),
					new Surveil(1)
				),
			],
			["Semester Foreseer // Peer Review"] =
			[
				EntersPrepared,
				When(TriggerEvent.SelfEnters, [], new Surveil(1)),
				PeerReview("Semester Foreseer // Peer Review"),
			],
			["Variable Chaser // Arc of Fortune"] =
			[
				EntersPrepared,
				Prowess,
				PrepareSpell(
					"Variable Chaser // Arc of Fortune",
					[],
					new EachPlayerMay(
						"Discard your hand and draw seven cards?",
						[new DiscardHand(Ref.You), new DrawCards(Ref.You, 7)]
					)
				),
			],
			["Bloodline Recollector // Ancestral Craving"] =
			[
				When(TriggerEvent.EachEndStep, [], new BecomePrepared(Ref.Source)) with
				{
					If = new AtLeast(Count.CreaturesDiedThisTurn, 3),
				},
				PrepareSpell(
					"Bloodline Recollector // Ancestral Craving",
					[TargetSpec.Player()],
					new DrawCards(Ref.Target(), 3),
					new LoseLife(Ref.Target(), 3)
				),
			],
			["Void Extrapolator // Omit Variables"] =
			[
				EntersPrepared,
				new StaticEffectComponent(Is.Source)
				{
					Power = 1,
					Toughness = 1,
					If = Threshold,
				},
				OmitVariables("Void Extrapolator // Omit Variables"),
			],
			["Pompous Battlemage // Improvised Act"] =
			[
				Prowess,
				EntersPrepared,
				PrepareSpell(
					"Pompous Battlemage // Improvised Act",
					[],
					new MayDiscard([new DrawCards(Ref.You, 1)])
				),
			],
			["Carnivorous Cultivator // Enroot"] =
			[
				EntersPrepared,
				When(
					TriggerEvent.SelfDealsCombatDamageToPlayer,
					[TargetSpec.InYourGraveyard(Is.Land)],
					new ReturnFromGraveyard(Ref.Target(), ZoneType.Hand)
				),
				PrepareSpell(
					"Carnivorous Cultivator // Enroot",
					[],
					new SearchLibrary(CardType.Land, ZoneType.Graveyard)
				),
			],
			["Emergency Phytomedic // Seed Suture"] =
			[
				EntersPrepared,
				SeedSuture("Emergency Phytomedic // Seed Suture"),
			],
			["Fatehold Chronologist // Peer Review"] =
			[
				EntersPrepared,
				PeerReview("Fatehold Chronologist // Peer Review"),
			],
			["Konstrari Improviser // Soul Tether"] =
			[
				EntersPrepared,
				SoulTether("Konstrari Improviser // Soul Tether"),
			],
			["Paradox Shaper // Omit Variables"] =
			[
				PreparesAtYourUpkeep,
				new ActivatedAbilityComponent(
					ManaCost.Parse("{2}"),
					[new PutIntoLibrary(Ref.Target(), OnTop: false)]
				)
				{
					Targets = [TargetSpec.InYourGraveyard()],
				},
				OmitVariables("Paradox Shaper // Omit Variables"),
			],
			["Prudent Fateseer // Peer Review"] =
			[
				EntersPrepared,
				When(
					TriggerEvent.YouSurveil,
					[],
					new ModifyUntilEndOfTurn(Ref.CreaturesYouControl, 1, 0)
				) with
				{
					OncePerTurn = true,
				},
				PeerReview("Prudent Fateseer // Peer Review"),
			],
			["Stingerquill Voxmancer // Vicious Verse"] =
			[
				PreparesAtYourUpkeep,
				ViciousVerse("Stingerquill Voxmancer // Vicious Verse"),
			],
			["Theorix Metamage // Omit Variables"] =
			[
				EntersPrepared,
				new StaticEffectComponent(Is.Source) { Power = 1, If = Threshold },
				new StaticEffectComponent(Is.Source, "Flying") { If = Threshold },
				OmitVariables("Theorix Metamage // Omit Variables"),
			],
			["Vigorbloom Vanguard // Seed Suture"] =
			[
				EntersPrepared,
				new StaticEffectComponent(
					Is.Creature & Is.Yours & Is.WithPlusOneCounter,
					"Vigilance"
				),
				SeedSuture("Vigorbloom Vanguard // Seed Suture"),
			],
			["Whiplash Wordsmith // Vicious Verse"] =
			[
				EntersPrepared,
				new StaticEffectComponent(Is.Source, "Flying")
				{
					If = new AtLeast(Count.NoncombatDamageToOpponentsThisTurn, 1),
				},
				new StaticEffectComponent(Is.Source, "Haste")
				{
					If = new AtLeast(Count.NoncombatDamageToOpponentsThisTurn, 1),
				},
				ViciousVerse("Whiplash Wordsmith // Vicious Verse"),
			],
			["Woodwork Prodigy // Soul Tether"] =
			[
				PreparesAtYourUpkeep,
				SoulTether("Woodwork Prodigy // Soul Tether"),
			],

			// ===== Abilities from the hand and graveyard =====
			["Hexhaven Battalion"] =
			[
				Spell([], new CreateTokens(Cadet, 3), new EmpowerJace(2)),
				BasicLandcycling,
			],
			["Undulating Witness"] =
			[
				new ActivatedAbilityComponent(
					ManaCost.Parse("{2}"),
					[new ModifyUntilEndOfTurn(Ref.Source, 1, -1)]
				),
				BasicLandcycling,
			],
			["Apex Witchstalker"] =
			[
				When(TriggerEvent.SelfEnters, [], new GainLife(Ref.You, 2)),
				When(TriggerEvent.SelfDies, [], new GainLife(Ref.You, 2)),
				BasicLandcycling,
			],
			["Awaken the Inferno"] =
			[
				Spell(
					[
						Target((Is.Creature | Is.Planeswalker) & Is.Opponents),
						Target(Is.Creature & Is.Yours) with
						{
							Optional = true,
						},
					],
					new DealDamage(Ref.Target(0), 6),
					new AddCounters(Ref.Target(1), 1)
				),
				BasicLandcycling,
			],
			["Vinelasher Adept"] =
			[
				When(TriggerEvent.SelfEnters, [Creature], new AddCounters(Ref.Target(), 3)),
				BasicLandcycling,
			],
			["Campus Crier"] =
			[
				new ActivatedAbilityComponent(ManaCost.Parse("{1}"), [new EmpowerJace(2)])
				{
					Costs = [new ExileSelfCost()],
					From = ZoneType.Graveyard,
				},
			],
			["Cryotheory Adept"] =
			[
				Prowess,
				new ActivatedAbilityComponent(
					ManaCost.Parse("{3}{U}"),
					[new Tap(Ref.Target()), new AddStunCounters(Ref.Target(), 1)]
				)
				{
					Costs = [new ExileSelfCost()],
					From = ZoneType.Graveyard,
					SorcerySpeed = true,
					Targets = [Creature],
				},
			],
			["Theoretical Necromancer"] =
			[
				new ActivatedAbilityComponent(
					ManaCost.Parse("{3}{B}"),
					[new ReturnFromGraveyard(Ref.Target(), ZoneType.Hand)]
				)
				{
					Costs = [new ExileSelfCost()],
					From = ZoneType.Graveyard, // "another target creature card": a graveyard target is never its source
					Targets = [TargetSpec.InYourGraveyard(Is.Creature)],
				},
			],
			["Blessed Ghoul"] =
			[
				new ActivatedAbilityComponent(
					ManaCost.Parse("{2}{W/B}"),
					[new ReturnFromGraveyard(Ref.Source, ZoneType.Hand)]
				)
				{
					From = ZoneType.Graveyard,
				},
			],
			["Grim Repriser"] =
			[
				Prowess,
				ReturnsWithFinality(
					"{B}{R}",
					new AtLeast(Count.NoncombatDamageToOpponentsThisTurn, 1)
				),
			],
			["Proctor of Potential"] =
			[
				When(TriggerEvent.CreatureYouControlEnters, [], new Surveil(1)),
				ReturnsWithFinality("{W}{U}", new AtLeast(Count.YourSurveilsThisTurn, 1)),
			],
			["Gallia, Tragic Host"] =
			[
				new ActivatedAbilityComponent(
					ManaCost.Parse("{4}{B}"),
					[
						new ReturnFromGraveyard(Ref.Source, ZoneType.Battlefield)
						{
							Tapped = true,
							Counters = 1,
						},
					]
				)
				{
					Costs = [new ExileFromGraveyardCost(Is.Creature & !Is.Source)],
					From = ZoneType.Graveyard,
				},
			],
			["Hallway Heckler // Vicious Verse"] =
			[
				EntersPrepared,
				new ActivatedAbilityComponent(ManaCost.None, [new DrawCards(Ref.You, 1)])
				{
					Costs = [new TapSelfCost(), new DiscardCost()],
				},
				ViciousVerse("Hallway Heckler // Vicious Verse"),
			],

			// ===== Graveyard returns, hexproof, indestructible, exhaust =====
			["Liliana the Repentant"] =
			[
				When(
					TriggerEvent.AnotherCreatureOrPlaneswalkerYouControlEnters,
					[],
					new Mill(Ref.You, 2)
				),
				new ActivatedAbilityComponent(
					ManaCost.Parse("{5}{B}"),
					[
						new ReturnFromGraveyard(Ref.Target(), ZoneType.Battlefield),
						new AddCounters(Ref.Source, 1),
					]
				)
				{
					Exhaust = true,
					SorcerySpeed = true,
					Targets =
					[
						TargetSpec.InYourGraveyard(
							Is.Type(CardType.Creature | CardType.Planeswalker)
						),
					],
				},
			],
			["Marwyn, the Preserver"] =
			[
				new StaticEffectComponent(Is.Land & Is.Yours, "Hexproof"),
				new ActivatedAbilityComponent(
					ManaCost.Parse("{2}"),
					[new ReturnFromGraveyard(Ref.Target(), ZoneType.Hand)]
				)
				{
					Targets = [TargetSpec.InYourGraveyard(Is.Land)],
				},
			],
			["Fateshaper Aspirant"] =
			[
				ModalWhen(
					"Fateshaper Aspirant",
					TriggerEvent.SelfEnters,
					1,
					M(
						[TargetSpec.InYourGraveyard(Is.Legendary)],
						new ReturnFromGraveyard(Ref.Target(), ZoneType.Hand)
					),
					M(
						[Creature],
						new AddCounters(Ref.Target(), 1),
						new ModifyUntilEndOfTurn(Ref.Target(), 0, 0, "Vigilance"),
						new ModifyUntilEndOfTurn(Ref.Target(), 0, 0, "Indestructible")
					)
				),
			],
			["Return to the Light Realms"] =
			[
				Spell(
					[],
					new ReturnFromGraveyard(
						Ref.YourGraveyard(NonlandPermanentTypes),
						ZoneType.Battlefield
					)
				),
			],
			["Restore with Empathy"] =
			[
				Spell(
					[TargetSpec.InYourGraveyard(Is.Type(PermanentTypes))],
					new ReturnFromGraveyard(Ref.Target(), ZoneType.Hand),
					new GainLife(Ref.You, 4)
				),
			],
			["Rewrite Regrets"] =
			[
				Spell(
					[
						TargetSpec.InYourGraveyard(
							Is.Type(CardType.Creature | CardType.Planeswalker)
								& Is.ManaValueAtMost(6)
						),
					],
					new ReturnFromGraveyard(Ref.Target(), ZoneType.Battlefield),
					new EmpowerJace(2)
				),
			],
			["Ruric Thar, Magecrusher"] =
			[
				new CantBeCountered(),
				new StaticEffectComponent(Is.Source, "Hexproof")
				{
					If = new Not(new Matches(Ref.Source, Is.DealtCombatDamage)),
				},
			],

			// ===== Commons: impulse, reflexive triggers, revealed hands, chosen colors =====
			["Shatterwing Pegasus"] =
			[
				new ActivatedAbilityComponent(
					ManaCost.Parse("{4}{W}"),
					[new ModifyUntilEndOfTurn(Ref.CreaturesYouControl, 1, 1)]
				),
			],
			["Unsummon"] = [Spell([Creature], new ReturnToHand(Ref.Target()))],
			["Inspired Tethermage"] =
			[
				When(TriggerEvent.YouPutLoyaltyCounters, [], new AddCounters(Ref.Source, 1)),
				new ActivatedAbilityComponent(ManaCost.Parse("{6}"), [new EmpowerJace(2)]),
			],
			["Something Worth Saving"] =
			[
				Spell([], new MillAndMayTake(4, PermanentTypes), new GainLife(Ref.You, 1)),
			],
			["Sureshot Sower"] =
			[
				new ActivatedAbilityComponent(ManaCost.Parse("{3}{G}"), [new Destroy(Ref.Target())])
				{
					Costs = [new DiscardSelfCost()],
					From = ZoneType.Hand,
					Targets = [Target(Is.Creature & Is.With("Flying"))],
				},
			],
			["Afterthought Sentry"] =
			[
				new ActivatedAbilityComponent(
					ManaCost.Parse("{2}"),
					[new ModifyUntilEndOfTurn(Ref.Source, 0, 0, "Flying")]
				),
				When(
					TriggerEvent.SelfAttacks,
					[TargetSpec.InAnyGraveyard() with { Optional = true }],
					new ExileFromGraveyard(Ref.Target())
				),
			],
			["Murmuring Volume"] =
			[
				new ManaAbilityComponent(ManaColor.White),
				new ManaAbilityComponent(ManaColor.Blue),
				new ManaAbilityComponent(ManaColor.Black),
				new ManaAbilityComponent(ManaColor.Red),
				new ManaAbilityComponent(ManaColor.Green),
				new ActivatedAbilityComponent(ManaCost.Parse("{2}"), [new DrawCards(Ref.You, 1)])
				{
					Costs = [new TapSelfCost(), new DiscardCost()],
				},
			],
			["Room of Refuge"] =
			[
				new EntersTappedComponent(),
				new ChoosesColorAsItEnters(),
				new ActivatedAbilityComponent(
					ManaCost.Parse("{5}"),
					[new AddCounters(Ref.Target(), 2)]
				)
				{
					Costs = [new TapSelfCost(), new SacrificeSelfCost()],
					SorcerySpeed = true,
					Targets = [Creature],
				},
			],
			["Tether Technician"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[],
					new MayDiscard(
						[new WhenYouDo([TargetSpec.AnyTarget()], [new DealDamage(Ref.Target(), 2)])]
					)
				),
			],
			["Blazing Crescendo"] =
			[
				Spell(
					[Creature],
					new ModifyUntilEndOfTurn(Ref.Target(), 3, 1),
					new ExileTopPlayable()
				),
			],
			["Solve for Disappointment"] =
			[
				Spell(
					[TargetSpec.Player(Is.Opponents)],
					new YouChooseTheyDiscard(Ref.Target(), NonlandPermanentTypes),
					new EmpowerJace(1)
				),
			],

			// ===== Counterspells and ward =====
			["Icy Reception"] =
			[
				ModalSpell(
					"Icy Reception",
					1,
					M(
						[TargetSpec.Spell(Is.Creature | Is.Legendary)],
						new CounterUnlessPays(Ref.Target(), ManaCost.Parse("{3}"))
					),
					M([Creature], new ModifyUntilEndOfTurn(Ref.Target(), -5, 0))
				),
			],
			["Unflinching Hortimancer"] =
			[
				Ward("{1}"),
				When(TriggerEvent.YouGainLife, [], new AddCounters(Ref.Source, 1)),
			],
			["Wrecking Gecko"] =
			[
				Ward("{2}"),
				new ActivatedAbilityComponent(
					ManaCost.Parse("{6}{G}{G}"),
					[new ModifyUntilEndOfTurn(Ref.Source, 4, 4, "Trample")]
				),
			],

			// ===== Auras and Equipment =====
			["Medic's Kitesail"] =
			[
				AttachedGets(1, 0),
				new StaticEffectComponent(Is.AttachedToSource, "Flying"),
				// "and has 'Whenever this creature attacks, you gain 1 life.'"
				When(TriggerEvent.AttachedCreatureAttacks, [], new GainLife(Ref.You, 1)),
				Equip("{2}"),
			],
			["Ferocity of the Hunt"] =
			[
				new AuraComponent(Creature),
				AttachedGets(1, 0),
				new StaticEffectComponent(Is.AttachedToSource, "Deathtouch"),
				When(
					TriggerEvent.AttachedCreatureDies,
					[],
					new ReturnFromGraveyard(Ref.EventCard, ZoneType.Battlefield) { Tapped = true }
				),
			],
			["Hunter's Axe"] =
			[
				AttachedGets(2, 0),
				// "has 'Whenever this creature attacks, it gains your choice of trample or deathtouch
				// until end of turn.'" — a choice, not printed bullets, so the modes are named here.
				new TriggeredAbilityComponent(TriggerEvent.AttachedCreatureAttacks, [])
				{
					Modes =
					[
						new Mode(
							"trample",
							[],
							[new ModifyUntilEndOfTurn(Ref.EventCard, 0, 0, "Trample")]
						),
						new Mode(
							"deathtouch",
							[],
							[new ModifyUntilEndOfTurn(Ref.EventCard, 0, 0, "Deathtouch")]
						),
					],
					Choose = 1,
				},
				Equip("{2}"),
			],
			["Warrior's Blades"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[TargetSpec.AnyTarget()],
					new DealDamage(Ref.Target(), 3),
					new GainLife(Ref.You, 3)
				),
				AttachedGets(2, 1),
				Equip("{3}") with
				{
					CostChange = new(Count.CountersOn(Ref.Target(0)).Negated),
				},
			],
			["Lich's Relic"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[],
					new May(
						"Pay {2}: destroy up to one of their creatures or planeswalkers?",
						new CanPay(ManaCost.Parse("{2}")),
						[
							new PayMana(
								ManaCost.Parse("{2}"),
								[
									new WhenYouDo(
										[
											Target(
												(Is.Creature | Is.Planeswalker) & Is.Opponents
											) with
											{
												Optional = true,
											},
										],
										[new Destroy(Ref.Target())]
									),
								]
							),
						]
					)
				),
				AttachedGets(2, 1),
				Equip("{2}"),
			],

			// ===== Uncommons: removal and spells =====
			["Refute Destiny"] =
			[
				Spell(
					[
						Target(
							(Is.Creature | Is.Planeswalker)
								& (Is.Color(ManaColor.Green) | Is.Color(ManaColor.Blue))
						),
					],
					new Exile(Ref.Target()),
					new Surveil(1)
				),
			],
			["Your Fate Ends Here"] =
			[
				Spell(
					[Target((Is.Creature | Is.Planeswalker) & Is.ManaValueAtLeast(3))],
					new Destroy(Ref.Target()),
					new Surveil(1)
				),
			],
			["Precise Redaction"] =
			[
				Spell(
					[TargetSpec.Spell(Is.Color(ManaColor.White) | Is.Color(ManaColor.Black))],
					new Counter(Ref.Target())
				),
			],
			["Multiply by Zero"] =
			[
				Spell(
					[Creature],
					new ModifyUntilEndOfTurn(Ref.Target(), 0, 0) { SetBase = (0, 0) }
				),
			],
			["Essence Burn"] =
			[
				Spell(
					[
						Target(
							(Is.Creature | Is.Planeswalker)
								& (Is.Color(ManaColor.Black) | Is.Color(ManaColor.Green))
						),
					],
					new DealDamage(Ref.Target(), 5),
					new ModifyUntilEndOfTurn(Ref.Target(), 0, 0) { ExileIfItWouldDie = true }
				),
			],
			["Violent Echoes"] =
			[
				Spell(
					[CreatureOrPlaneswalker],
					new DealDamage(Ref.Target(), 6) { IfExcess = [new EmpowerJace(Count.X)] }
				),
			],
			["Clash of Elements"] =
			[
				Spell(
					[Target(!Is.Land)],
					new May(
						"Put it on top of your library and take 2 damage? (No: it goes on the bottom.)",
						new Always(),
						[
							new PutIntoLibrary(Ref.Target(), OnTop: true),
							new DealDamage(Ref.OwnerOfTarget(), 2),
						]
					)
					{
						Who = Ref.OwnerOfTarget(),
						Otherwise = [new PutIntoLibrary(Ref.Target(), OnTop: false)],
					}
				),
			],
			["Primal Witchstalker"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[],
					new Mill(Ref.You, 4)
					{
						IfMilled =
						[
							new WhenYouDo(
								[TargetSpec.InYourGraveyard(Is.Land)],
								[
									new ReturnFromGraveyard(Ref.Target(), ZoneType.Battlefield)
									{
										Tapped = true,
									},
								]
							),
						],
					}
				),
			],
			["Theorix Charm"] =
			[
				ModalSpell(
					"Theorix Charm",
					1,
					M(
						[TargetSpec.Spell(!Is.Creature)],
						new CounterUnlessPays(Ref.Target(), ManaCost.Parse("{2}"))
					),
					M([Creature], new ModifyUntilEndOfTurn(Ref.Target(), -2, -2)),
					M([], new Mill(Ref.You, 3), new DrawCards(Ref.You, 1))
				),
			],
			["Twisted Fates"] =
			[
				Spell(
					[Target(!Is.Land), TargetSpec.Player()],
					new Destroy(Ref.Target(0)),
					new AddCounters(Ref.PermanentsOfTargetPlayer(CardType.Creature, 1), 1)
				),
			],
			["Vigorbloom Charm"] =
			[
				ModalSpell(
					"Vigorbloom Charm",
					1,
					M(
						[Target(Is.Yours)],
						new ModifyUntilEndOfTurn(Ref.Target(), 0, 0, "Hexproof"),
						new ModifyUntilEndOfTurn(Ref.Target(), 0, 0, "Indestructible")
					),
					M([], new DrawCards(Ref.You, 1), new GainLife(Ref.You, 3)),
					M(
						[Target(Is.Creature & Is.Yours), Target(Is.Creature & Is.Opponents)],
						new AddCounters(Ref.Target(0), 1),
						new Fight(Ref.Target(0), Ref.Target(1))
					)
				),
			],

			// ===== Uncommons: creatures =====
			["Bloombrute"] =
			[
				When(TriggerEvent.YouGainLife, [], new DrawCards(Ref.You, 1)) with
				{
					OncePerTurn = true,
				},
				new ActivatedAbilityComponent(
					ManaCost.Parse("{4}{G}{W}"),
					[
						new ModifyUntilEndOfTurn(Ref.Target(), 0, 0, "Trample"),
						new ModifyUntilEndOfTurn(Ref.Target(), 0, 0, "Lifelink"),
					]
				)
				{
					Targets = [Creature],
				},
			],
			["Koth of the Homestead"] =
			[
				When(TriggerEvent.LandYouControlEnters, [], new GainLife(Ref.You, 1)),
				When(
					new Trigger(EventKind.Entered)
					{
						Subject = Is.Land & Is.Yours & Is.Subtype("Plains"),
					},
					[Creature],
					new AddCounters(Ref.Target(), 1)
				),
			],
			["Rescue Girl, First Responder"] =
			[
				new ActivatedAbilityComponent(ManaCost.None, [new ReturnToHand(Ref.Target())])
				{
					Costs = [new TapSelfCost()],
					Targets = [Target(Is.Yours & !Is.Source)],
					OnlyIf = new DuringYourTurn(),
				},
			],
			["Teyo, Lightshield Expert"] = [TeyoEnters("Hexproof")],
			["Teyo, Diamondblade Mage"] = [TeyoEnters("Deathtouch")],
			["Kiora of Fire and Ashes"] =
			[
				When(TriggerEvent.SelfEnters, [], new CreateTokens(Dragon, 1)),
				new ActivatedAbilityComponent(ManaCost.Parse("{8}"), [new CreateTokens(Dragon, 1)]),
			],
			["Pia, Determined Rebuilder"] =
			[
				When(TriggerEvent.SelfEnters, [], new CreateTokens(Thopter, 1)),
				new ActivatedAbilityComponent(
					ManaCost.Parse("{5}{R}"),
					[new ModifyUntilEndOfTurn(Ref.Target(), ArtifactsYouControl, 0)]
				)
				{
					Targets = [Creature],
				},
			],
			["Hapatra, the Desert Frost"] =
			[
				// "For each opponent, … up to one target creature that player controls" — one opponent.
				When(
					TriggerEvent.SelfEnters,
					[Target(Is.Creature & Is.Opponents) with { Optional = true }],
					new Tap(Ref.Target()),
					new AddStunCounters(Ref.Target(), 1)
				),
				new ActivatedAbilityComponent(ManaCost.Parse("{2}{U}"), [new Untap(Ref.Target())])
				{
					Targets = [Creature],
				},
			],
			["Geist of Saint Thalia"] = [new CostChange(-1, !Is.Creature)],
			["Arni, Humble Scribe"] =
			[
				When(
					new Trigger(EventKind.Entered)
					{
						Subject = Is.Creature & Is.Yours & !Is.Source & !Is.Token,
					},
					[],
					new Untap(Ref.Source)
				),
				new ActivatedAbilityComponent(
					ManaCost.None,
					[new DrawCards(Ref.You, 1), new Discard(Ref.You, 1)]
				)
				{
					Costs = [new TapSelfCost()],
				},
			],
			["Traxos, Scourge Eternal"] =
			[
				new DoesntUntapInYourUntapStep(),
				When(
					TriggerEvent.YouCastSpell with
					{
						Subject = Is.Type(CardType.Artifact | CardType.Creature),
					},
					[],
					new Untap(Ref.Source)
				),
			],
			["Saheeli, Jewel of Avishkar"] =
			[
				new StaticEffectComponent(Is.Creature & Is.Yours & Is.Subtype("Thopter"), "Haste"),
				When(TriggerEvent.YouCastNoncreatureSpell, [], new CreateTokens(Thopter, 1)),
			],
			["Hexhaven Dueling Arena"] =
			[
				new ManaAbilityComponent(ManaColor.Colorless),
				new ActivatedAbilityComponent(
					ManaCost.Parse("{2}"),
					[new BecomePrepared(Ref.Target())]
				)
				{
					Costs = [new TapSelfCost()],
					SorcerySpeed = true,
					Targets = [Target(Is.Creature & Is.AttackedThisTurn)],
				},
				new ActivatedAbilityComponent(
					ManaCost.Parse("{4}"),
					[new BecomePrepared(Ref.Target())]
				)
				{
					Costs = [new TapSelfCost()],
					Targets = [Creature],
				},
			],

			// ===== Uncommons: discard and cast triggers =====
			["Tinybones, Pocket Nuisance"] =
			[
				When(TriggerEvent.SelfEnters, [], new Discard(Ref.EachOpponent, 1)),
				When(TriggerEvent.APlayerDiscards, [], new DealDamage(Ref.EachOpponent, 1)),
			],
			["Titanbones, Towering Heart"] =
			[
				When(TriggerEvent.YouGainLife, [], new AddCounters(Ref.Source, 2)),
				When(TriggerEvent.SelfDiscarded, [], new GainLife(Ref.You, 3)) with
				{
					FromGraveyard = true,
				},
			],
			["Jiang Yanggu, Alone"] =
			[
				When(
					TriggerEvent.CreatureYouControlAttacksPlayerAlone,
					[],
					new Discard(Ref.You, 1),
					new DrawCards(Ref.You, 1),
					new AddCounters(Ref.EventCard, Count.CardsYouDiscardedThisTurn)
				),
			],
			["Pia, Aether Ascetic"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[],
					new MayDiscard(
						[new SearchLibrary(CardType.Enchantment, ZoneType.Hand) { Reveal = true }]
					)
				),
			],
			["Danitha, Sword of Hope"] =
			[
				When(
					TriggerEvent.YouCastEquipmentOrSpellTargetingYourCreature,
					[],
					new DrawCards(Ref.You, 1)
				) with
				{
					OncePerTurn = true,
				},
			],
			["Danitha, Spear of Agony"] =
			[
				When(
					TriggerEvent.YouCastSpellTargetingOpponentOrTheirCreature,
					[],
					new AddCounters(Ref.Source, 1)
				),
			],
			["Plan for All Outcomes"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[Target(!Is.Land & !Is.Source) with { Optional = true }],
					new May(
						"Put it on top of your library? (No: the bottom.)",
						new Always(),
						[new PutIntoLibrary(Ref.Target(), OnTop: true)]
					)
					{
						Who = Ref.OwnerOfTarget(),
						Otherwise = [new PutIntoLibrary(Ref.Target(), OnTop: false)],
					}
				),
				When(TriggerEvent.YouCastFirstNoncreatureSpellEachTurn, [], new EmpowerJace(1)),
			],
			["Proft, Sinister Mastermind"] =
			[
				new CastOnlyIf(Threshold),
				new ActivatedAbilityComponent(
					ManaCost.Parse("{B}"),
					[new ModifyUntilEndOfTurn(Ref.Target(), -3, -1)]
				)
				{
					Costs = [new DiscardSelfCost()],
					From = ZoneType.Hand,
					Targets = [Creature],
				},
			],
			["Countersculpt"] =
			[
				new AdditionalCostsComponent([new BeholdCost("Jace") { OrPay = 1 }]),
				Spell([TargetSpec.Spell()], new Counter(Ref.Target()), new EmpowerJace(1)),
			],

			// ===== Uncommons: replacements and counters =====
			["Tomik, Izzet Sparkmage"] =
			[
				Prowess,
				new AmountReplacement(ReplaceableEvent.NoncombatDamageToOpponent, 1),
			],
			["Yoshimaru, Beloved Companion"] =
			[
				new AmountReplacement(ReplaceableEvent.PlusOneCountersOnYourCreature, 1),
				new ActivatedAbilityComponent(
					ManaCost.Parse("{6}"),
					[new AddCounters(Ref.Target(), 1)]
				)
				{
					Targets = [Target(Is.Creature & Is.Legendary)],
				},
			],
			["Mabel, Bitter Recluse"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[Target((Is.Creature | Is.Planeswalker) & !Is.Source)],
					new RemoveCounters(Ref.Target(), 3)
				),
			],
			["Hapatra, the Desert Fang"] =
			[
				// "For each opponent, … up to one target creature that player controls" — one opponent.
				When(
					TriggerEvent.SelfEnters,
					[Target(Is.Creature & Is.Opponents) with { Optional = true }],
					new AddMinusOneCounters(
						Ref.Target(),
						Count.GreatestManaValueOf(Ref.CardsInYourGraveyard)
					)
				),
			],
			["Loot, the Anomaly"] =
			[
				new AssignsDamageAsThoughPowerPositive(),
				new ActivatedAbilityComponent(
					ManaCost.None,
					[new ModifyUntilEndOfTurn(Ref.Source, -2, 0)]
				)
				{
					Costs = [new SacrificeCost((Is.Creature | Is.Planeswalker) & !Is.Source)],
					OnlyIf = Threshold,
				},
			],
			["Ghalta the Immovable"] =
			[
				new CostChange(Count.GreatestToughnessOf(Ref.CreaturesYouControl).Negated)
				{
					OnItself = true,
				},
				new StaticEffectComponent(Is.Creature & Is.Yours, Combat.AttacksDespiteDefender),
				new YourCreaturesAssignDamageByToughness(),
			],

			// ===== Uncommons: combat and the rest =====
			["Yuriko, Blade of the Mighty"] =
			[
				new NoSpellsOrAbilitiesDuringCombat(),
				When(
					TriggerEvent.CreatureYouControlAttacksPlayerAlone,
					[],
					new ModifyUntilEndOfTurn(Ref.EventCard, 0, 0, "Double strike")
				),
			],
			["Tomik, Orzhov Lawmage"] =
			[
				new OneAttackerPerPlaneswalkerYouControl(),
				new ActivatedAbilityComponent(
					ManaCost.None,
					[new ModifyUntilEndOfTurn(Ref.Target(), 0, 0, "Flying")]
				)
				{
					Costs = [new TapSelfCost()],
					Targets = [Target(Is.Creature & Is.WithPlusOneCounter)],
				},
			],
			["Kiora of Salt and Sand"] =
			[
				When(
					TriggerEvent.YouAttack,
					[Target(Is.Creature & Is.Attacking)],
					new Untap(Ref.Target()),
					new ModifyUntilEndOfTurn(Ref.Target(), 0, 0, Combat.CantBeBlocked)
				) with
				{
					If = new AtLeast(Count.LoyaltyAbilitiesYouActivatedThisTurn, 1),
				},
				Grant(-8, [], new CreateTokens(Leviathan, 1)),
			],
			["Winter, Team Player"] =
			[
				When(
					TriggerEvent.YouCastNoncreatureSpell,
					[],
					new ModifyUntilEndOfTurn(Ref.CreaturesYouControl, 1, 0)
				),
			],
			["Fblthp, Impossibly Lost"] =
			[
				When(
					TriggerEvent.OpponentsDealtCombatDamageDuringYourTurn,
					[],
					new DrawCards(Ref.You, 2),
					new If(new YourLibraryIsEmpty(), [new WinTheGame()]),
					new ShuffleIntoLibrary(Ref.Source)
				),
			],
			["Fblthp, Knows the Way"] =
			[
				new StaticEffectComponent(Is.Source)
				{
					Power = Count.BasicLandTypesAmongLandsYouControl,
				},
				When(
					TriggerEvent.SelfEnters,
					[],
					new SearchLibrary(CardType.Land, ZoneType.Hand)
					{
						Supertype = Supertype.Basic,
						UpTo = Count.X,
						DifferentNames = true,
						Reveal = true,
					}
				),
			],

			// ===== Engine: "loses all abilities" =====
			["Flourishing Grapple"] =
			[
				Spell(
					[
						Target(
							(Is.Creature | Is.Planeswalker)
								& Is.Opponents
								& (Is.Color(ManaColor.Red) | Is.Color(ManaColor.White))
						),
						Target(Is.Creature & Is.Yours),
					],
					new ModifyUntilEndOfTurn(Ref.Target(0), 0, 0) { LosesAllAbilities = true },
					new DamageFrom(Ref.Target(1), Ref.Target(0), Count.PowerOf(Ref.Target(1)))
				),
			],
			["Infinite Coursework"] =
			[
				new AuraComponent(Creature),
				When(
					TriggerEvent.SelfEnters,
					[],
					new Tap(Ref.EnchantedCreature),
					new BecomeUnprepared(Ref.EnchantedCreature)
				),
				new EnchantedLosesAllAbilities(),
				new EnchantedDoesntUntap(),
			],

			// ===== Engine: mana that isn't one land, one mana =====
			["Loot, the Nexus"] =
			[
				new ManaOfOneColorComponent(Count.DistinctPowersOf(Ref.CreaturesYouControl)),
			],
			["Heartwood Crafter // Soul Tether"] =
			[
				EntersPrepared,
				SoulTether("Heartwood Crafter // Soul Tether"),
				new ManaAbilityComponent(ManaColor.Colorless)
				{
					Restriction = ManaRestriction.NotForSpellsFromHand,
				},
			],
			["Pyre Rhymer // Molten Tide"] =
			[
				Prowess,
				EntersPrepared,
				PrepareSpell(
					"Pyre Rhymer // Molten Tide",
					[],
					new ThisTurn(new ExtraRedFromMountains())
				),
			],

			// ===== Engine: copying a spell =====
			["Way of the Cryomancer"] =
			[
				When(TriggerEvent.SelfEnters, [], new EmpowerJace(5)),
				Grant(-3, [], new ThisTurn(new CopyNextInstantOrSorcery())),
			],

			// ===== Rares (8a): from existing pieces =====
			["Aerid Konstrari"] =
			[
				When(TriggerEvent.SelfEnters, [], CreateHeartwood),
				When(TriggerEvent.SelfDies, [], CreateHeartwood),
				// "Then … gets +X/+0": counted after the token, so it counts itself.
				new ActivatedAbilityComponent(
					ManaCost.Parse("{6}"),
					[CreateHeartwood, new ModifyUntilEndOfTurn(Ref.Source, ArtifactsYouControl, 0)]
				),
			],
			["Craterclaw Colossus"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[],
					new ModifyUntilEndOfTurn(
						Ref.CreaturesYouControl,
						ArtifactsYouControl,
						0,
						"Trample"
					)
				),
			],
			["Ingris Stingerquill"] =
			[
				When(
					TriggerEvent.CreatureYouControlAttacks,
					[],
					new DamageFrom(Ref.EventCard, Ref.EachOpponent, 1)
				),
				new ActivatedAbilityComponent(
					ManaCost.Parse("{4}"),
					[
						new CreateTokens(Cadet, 1),
						new ModifyUntilEndOfTurn(Ref.CreaturesYouControl, 0, 0, "Haste"),
					]
				),
			],
			["Overwrite the Multiverse"] =
			[
				// Exiled first, then Jace: nothing exiled sees the token enter.
				Spell(
					[],
					new Exile(Ref.AllCreatures) { ThenWithXExiled = [new EmpowerJace(Count.X)] }
				),
			],
			["The Echoverse Fulcrum"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[],
					new DrawCards(Ref.You, 1),
					new Discard(Ref.You, 1)
				),
				new ActivatedAbilityComponent(
					ManaCost.Parse("{5}"),
					[new Destroy(Ref.AllCreatures)]
				)
				{
					Costs = [new TapSelfCost(), new ExileSelfCost()],
					SorcerySpeed = true,
				},
			],
			["Tarmogoyf"] =
			[
				// "*/1+*": the printed 1 is its base toughness; the static adds the count to both.
				new StaticEffectComponent(Is.Source)
				{
					Power = Count.CardTypesInAllGraveyards,
					Toughness = Count.CardTypesInAllGraveyards,
				},
			],
			["Dark Matter Manipulator"] =
			[
				When(TriggerEvent.SelfEnters, [], new Mill(Ref.You, 3)),
				new StaticEffectComponent(Is.Source)
				{
					Power = Count.NumberOf(Ref.CardsInYourGraveyard) with { Per = 7, Times = 2 },
				},
			],
			["Simulacrum Shaper"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[],
					new SearchLibrary(CardType.Land, ZoneType.Battlefield)
					{
						Supertype = Supertype.Basic,
						Tapped = true,
					}
				),
				When(TriggerEvent.SelfDies, [], new DrawCards(Ref.You, 1)),
			],
			["Lyra, Archangel of Dawn"] =
			[
				When(
					TriggerEvent.YouGainLife,
					[],
					new AddCounters(Ref.Permanents(Is.Creature & Is.Yours & Is.Subtype("Angel")), 1)
				),
			],
			["Master of Barbs"] =
			[
				When(
					TriggerEvent.OpponentDealtNoncombatDamage,
					[],
					new ModifyUntilEndOfTurn(Ref.CreaturesYouControl, 1, 0)
				),
			],
			["Solarium Sentry"] =
			[
				When(
					new Trigger(EventKind.SpellCast)
					{
						By = Whose.Opponent,
						Subject = Is.ManaValueAtMost(2),
					},
					[],
					new GainLife(Ref.You, 2)
				),
			],
			["Liliana the Faultless"] =
			[
				When(
					TriggerEvent.AnotherCreatureOrPlaneswalkerYouControlEnters,
					[],
					new GainLife(Ref.You, 1)
				),
				new ActivatedAbilityComponent(
					ManaCost.Parse("{1}"),
					[new ModifyUntilEndOfTurn(Ref.Target(), 0, 0, "Hexproof")]
				)
				{
					Costs = [new TapSelfCost(), new DiscardCost()],
					Targets = [Target((Is.Creature | Is.Planeswalker) & Is.Yours & !Is.Source)],
				},
			],

			// ===== Rares (8b): small new pieces =====
			["Stinging Vitriol"] =
			[
				Spell(
					[TargetSpec.Player(Is.Opponents)],
					new DealDamage(Ref.Target(), 2),
					new YouChooseTheyDiscard(Ref.Target(), NonlandTypes)
				),
			],
			["Loyal Tutor"] =
			[
				Spell(
					[],
					new SearchLibrary(CardType.Planeswalker, ZoneType.Library) { Reveal = true }
				),
			],
			["Entrust the Spark"] =
			[
				Spell(
					[],
					new MaySacrifice(
						Is.Planeswalker,
						[new SearchLibrary(CardType.Planeswalker, ZoneType.Battlefield)]
					)
				),
			],
			["Codie, Ravenous Codex"] =
			[
				When(
					TriggerEvent.YouCastSpell with
					{
						Subject = Is.PreparedCopy,
					},
					[],
					new CopySpell(Ref.EventCard)
				),
				new ActivatedAbilityComponent(
					ManaCost.Parse("{W}{U}{B}{R}{G}"),
					[new BecomePrepared(Ref.CreaturesYouControl)]
				)
				{
					Costs = [new TapSelfCost()],
				},
			],
			["Karn, Gilded Guardian"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[],
					new DrawCards(
						Ref.You,
						Count.ColorsAmong(Ref.Permanents(Is.Artifact & Is.Yours & !Is.Source))
					)
				),
			],
			["Roiling Canopy"] =
			[
				new EntersTappedComponent(),
				new ManaAbilityComponent(ManaColor.Green),
				// "If you control at least five other Forests" — other than the one entering, checked
				// again on resolution, so it still counts if that Forest has gone.
				When(
					new Trigger(EventKind.Entered)
					{
						Subject = Is.Land & Is.Yours & Is.Subtype("Forest"),
					},
					[Target(Is.Creature & Is.Yours)],
					new ModifyUntilEndOfTurn(Ref.Target(), 3, 3)
				) with
				{
					If = new AtLeast(
						Count.NumberOf(
							Ref.Permanents(
								Is.Land & Is.Yours & Is.Subtype("Forest") & !Is.EventCard
							)
						),
						5
					),
				},
			],
			["Samut, Tyrant of Naktamun"] = [new YourInstantsAndSorceriesHaveSplitSecond()],
			["Jace's Machinations"] =
			[
				Spell([], new ThisTurn(new JaceLoyaltyAtInstantSpeed()), new EmpowerJace(8)),
			],
			["Theorist's Sanctum"] =
			[
				new EntersTappedComponent(new YouCanBehold("Jace")),
				new ActivatedAbilityComponent(ManaCost.Parse("{2}{U}"), [new EmpowerJace(2)])
				{
					Costs = [new TapSelfCost()],
				},
			],
			["Theorist's Proxy"] =
			[
				When(TriggerEvent.SelfEnters, [], new EmpowerJace(3)),
				new ActivatedAbilityComponent(
					ManaCost.Parse("{U}"),
					[new ThisTurn(new NextSpellCantBeCountered())]
				)
				{
					Costs = [new SacrificeSelfCost()],
				},
			],
			["Solitary Cell"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[Target(Is.Opponents & !Is.Land & Is.ManaValueAtMost(3))],
					new ExileUntilSourceLeaves(Ref.Target())
				),
				new ActivatedAbilityComponent(ManaCost.Parse("{1}"), [new DrawCards(Ref.You, 1)])
				{
					Costs = [new TapSelfCost(), new DiscardCost(Is.Legendary)],
				},
			],

			// ===== Rares (8c): Treasure and artifact tokens =====
			["Vraska, Soul of Stone"] =
			[
				new StaticEffectComponent(Is.Artifact & Is.Creature & Is.Yours, "Vigilance"),
				When(
					TriggerEvent.YouCastNoncreatureSpell,
					[],
					new CreateTokens(SculptureTreasure, 1) { Abilities = [SacrificeForMana(1)] }
				),
			],
			["Vraska, the Cutting Glare"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[Target(Is.Opponents)],
					new Destroy(Ref.Target()),
					new CreateTokens(Treasure, 1)
					{
						Abilities = [SacrificeForMana(1)],
						ControlledBy = Ref.ControllerOfTarget(),
					}
				) with
				{
					If = new AtLeast(Count.LandsYouControl, 6),
				},
			],
			["Kwia Vigorbloom"] =
			[
				Ward("{2}"),
				When(
					TriggerEvent.YouGainLife,
					[],
					new CreateTokens(Lotus, 1) { Abilities = [SacrificeForMana(3)] }
				) with
				{
					OncePerTurn = true,
				},
			],
			["Draconic Visitor"] = [new ArtifactTokensBecome(Dragon, [])],
			["Hungering Puppetbeast"] =
			[
				When(TriggerEvent.SelfEnters, [], CreateHeartwood),
				new ActivatedAbilityComponent(
					ManaCost.Parse("{1}"),
					[
						new AddCounters(Ref.Source, 1),
						new GainsChosenKeyword(Ref.Source, ["Trample", "Hexproof", "Haste"]),
					]
				)
				{
					Costs = [new SacrificeCost(Is.Artifact & !Is.Source)],
				},
			],
			["Tenured Tethermage"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[],
					new MaySacrifice(Is.Land, [CreateHeartwood with { Count = 2, Tapped = true }])
				),
				new ActivatedAbilityComponent(ManaCost.None, [new AddCounters(Ref.Source, 2)])
				{
					Costs = [new TapUntappedCost(2, Is.Artifact)],
				},
			],
			["Gideon's Memorial"] =
			[
				new StaticEffectComponent(Is.Creature & Is.Yours & Is.Token) { Power = 1 },
				new StaticEffectComponent(Is.Creature & Is.Yours & Is.Token, "Vigilance"),
				.. AnyColorMana(ManaRestriction.OnlyPlaneswalkerSpells),
				new ActivatedAbilityComponent(
					ManaCost.Parse("{1}{W}"),
					[new DealDamage(Ref.Target(), 4)]
				)
				{
					Costs = [new DiscardSelfCost()],
					From = ZoneType.Hand,
					Targets = [Target(Is.Creature & Is.InCombat)],
				},
			],

			// ===== Rares (8d): one-offs =====
			["Gideon the Oathless"] =
			[
				When(
					TriggerEvent.SelfBecomesTargetOfOpponentsSpellOrAbility,
					[],
					new CounterUnlessPays(Ref.EventObject, ManaCost.None)
					{
						Costs = [new DiscardCost()],
					}
				),
				When(
					new Trigger(EventKind.Entered) { Subject = Is.Creature & Is.Opponents },
					[],
					new DealDamage(Ref.EachOpponent, 1) // "that player" — even if the creature left in response
				),
				When(
					TriggerEvent.OpponentActivatesLoyaltyAbility,
					[],
					new DealDamage(Ref.EachOpponent, 1)
				),
			],
			["Flickering Hound"] =
			[
				When(
					TriggerEvent.YouCastCreatureSpell,
					[Target(Is.Creature & Is.Yours & !Is.Source) with { Optional = true }],
					new Flicker(Ref.Target())
				),
			],
			["Gardenize"] =
			[
				When(TriggerEvent.CreatureYouControlDies, [], new AddChargeCounters(Ref.Source, 1)),
				When(
					TriggerEvent.YourFirstMainPhase,
					[],
					new AddMana(ManaColor.Green, Count.ChargeCountersOn(Ref.Source))
				),
			],
			["Cruel Calculations"] =
			[
				Spell(
					[TargetSpec.Player()],
					new DrawCards(Ref.You, Count.CardsFromLibraryToGraveyardThisTurn(Ref.Target()))
				),
			],
			["Curse-Marred Demon"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[],
					new SearchLibrary(CardType.None, ZoneType.Hand), // "a card"
					new DiscardAtRandom(Ref.You, 1)
				),
			],
			["Hexhaven Invigorator"] =
			[
				When(
					TriggerEvent.SelfDealtDamage,
					[],
					new SearchLibrary(CardType.Land, ZoneType.Battlefield)
					{
						UpTo = Count.X,
						Tapped = true,
					}
				),
			],
			["Seasoned Cryomancer"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[],
					new DrawCards(Ref.You, 2),
					new Discard(Ref.You, 2)
					{
						Then =
						[
							new WhenYouDo(
								[Creature],
								[
									new Tap(Ref.Target(0)),
									new AddStunCounters(Ref.Target(0), 1),
									new Tap(Ref.Target(1)),
									new AddStunCounters(Ref.Target(1), 1),
								]
							)
							{
								TargetsUpTo = Count.NonlandCardsChosen,
							},
						],
					}
				),
				new ActivatedAbilityComponent(
					ManaCost.Parse("{3}{U}{U}"),
					[new DrawCards(Ref.You, 2)]
				)
				{
					Costs = [new ExileSelfCost()],
					From = ZoneType.Graveyard,
				},
			],
			["Identity Echo"] =
			[
				new ActivatedAbilityComponent(
					ManaCost.Parse("{3}{R}"),
					[
						new Exile(Ref.Target()),
						new RevealUntilOntoBattlefield(CardType.Creature | CardType.Planeswalker),
					]
				)
				{
					Targets = [Target((Is.Creature | Is.Planeswalker) & Is.Yours)],
					SorcerySpeed = true,
				},
			],
			["Kindred Judgment"] = [Spell([], new DestroyCreaturesNotOfChosenType())],
			["Karn, Argent Defender"] = [new EnteringArtifactsAndCreaturesDontTrigger()],
			["Sphinx's Approach"] =
			[
				Spell(
					[],
					new DrawCards(Ref.You, 2),
					new May(
						"Exile this and four Sphinx's Approach to search for a Sphinx?",
						new GraveyardHasCopiesOfSource(4),
						[
							new ExileSourceAndCopiesFromGraveyard(4),
							new SearchLibrary(CardType.Creature, ZoneType.Battlefield)
							{
								Subtype = "Sphinx",
							},
						]
					)
				),
			],
			["Null Summoner"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[TargetSpec.Player(Is.Opponents)],
					new YouChooseTheyDiscard(Ref.Target(), NonlandTypes) { ExileInstead = true }
				) with
				{
					If = new SourceWasCast(),
				},
				new MayCastWhatItExiled(Threshold),
			],
			["Omnipresence"] = [new CastFromHandFreeUpToCreatureCount()],

			// ===== Rares (8e): copying permanents =====
			["Sphinx of False Conclusions"] =
			[
				When(
					TriggerEvent.SelfAttacks,
					[],
					new DrawCards(Ref.You, 1),
					new Discard(Ref.You, 1)
				),
				When(TriggerEvent.SelfDies, [], new CreateTokenCopy(Ref.Source)) with
				{
					If = new Not(new Matches(Ref.Source, Is.Token)),
				},
			],
			["Face Yourself"] =
			[
				Spell(
					[TargetSpec.Player()],
					new CreateTokenCopy(Ref.PermanentsOfTargetPlayer(CardType.Creature, 0))
					{
						AddingKeywords = ["Haste"],
						Adding =
						[
							When(TriggerEvent.EachEndStep, [], new Sacrifice(Ref.Source)) with
							{
								If = new Not(
									new AtLeast(Count.NumberOf(Ref.PlaneswalkersYouControl), 1)
								),
							},
						],
					}
				),
			],
			["Vindictive Triumph"] =
			[
				Spell(
					[CreatureOrPlaneswalker],
					new If(
						new Matches(Ref.Target(0), Is.ManaValueAtMost(3)),
						[
							new Exile(Ref.Target()),
							new ReturnFromExileUnderYourControl(Ref.Target())
							{
								Tapped = true,
								ExileAtNextEndStep = true,
							},
						]
					)
					{
						Otherwise = [new Exile(Ref.Target())],
					}
				),
			],
			["Hall of Echoes"] =
			[
				new ManaAbilityComponent(ManaColor.Colorless),
				new ActivatedAbilityComponent(
					ManaCost.Parse("{5}"),
					[
						new BecomeCopyUntilEndOfTurn(Ref.Source, Ref.Target()),
						new ThisTurn(new NoLegendRule()),
					]
				)
				{
					Targets = [Target(Is.Creature & Is.Yours)],
				},
			],

			// ===== Mythics and rares (8f): planeswalkers — emblems, "until your next turn", −X =====
			["Ajani Resolute"] =
			[
				When(TriggerEvent.YouGainLife, [], new AddLoyalty(Ref.Source, 1)),
				Loyalty(0, [], new GainLife(Ref.You, 1)),
				Loyalty(
					-4,
					[],
					new CreateTokens(AjanisPridemate, 1)
					{
						Abilities =
						[
							When(TriggerEvent.YouGainLife, [], new AddCounters(Ref.Source, 1)),
						],
					}
				),
				Loyalty(
					-10,
					[],
					new GetEmblem(
						[
							new StaticEffectComponent(Is.Creature & Is.Yours)
							{
								Power = 2,
								Toughness = 2,
							},
						]
					)
				),
			],
			["Chandra, Chill of Compliance"] =
			[
				Loyalty(
					1,
					[],
					new Surveil(1)
					{
						Then =
						[
							new ReturnChosenToHandIfManaValueAtMost(int.MaxValue)
							{
								ExceptTypes = CardType.Creature | CardType.Land,
							},
						],
					}
				),
				Loyalty(
					1,
					[],
					new AddMana(ManaColor.Blue)
					{
						Restriction = ManaRestriction.OnlyNoncreatureSpells,
					}
				),
				new LoyaltyAbilityComponent(
					0,
					[new Tap(Ref.Target()), new AddStunCounters(Ref.Target(), Count.X)]
				)
				{
					MinusX = true,
					Targets = [Target(Is.Type(CardType.Artifact | CardType.Creature))],
				},
				Loyalty(
					-6,
					[],
					new GetEmblem([When(TriggerEvent.YouCastSpell, [], new DrawCards(Ref.You, 1))])
				),
			],
			["Chandra, Torch of Defiance"] =
			[
				Loyalty(1, [], new ExileTopMayCast([new DealDamage(Ref.EachOpponent, 2)])),
				Loyalty(1, [], new AddMana(ManaColor.Red, 2)),
				Loyalty(-3, [Creature], new DealDamage(Ref.Target(), 4)),
				Loyalty(
					-7,
					[],
					new GetEmblem(
						[
							When(
								TriggerEvent.YouCastSpell,
								[TargetSpec.AnyTarget()],
								new DealDamage(Ref.Target(), 5)
							),
						]
					)
				),
			],
			["Jace, Reality Sculptor"] =
			[
				Loyalty(
					1,
					[],
					new EmpowerJace(
						Count.NumberOf(Ref.Permanents(Is.Land & Is.Yours & Is.Subtype("Island")))
					)
				),
				Loyalty(
					-3,
					[],
					new UntilYourNextTurnWhenever(
						When(
							new Trigger(EventKind.Attacked) { Subject = Is.AttackingYou },
							[],
							new ModifyUntilEndOfTurn(Ref.EventCard, -5, 0)
						)
					)
				),
				Loyalty(0, [], new ExileAllButBottom(Ref.EachOpponent)) with
				{
					OnlyIf = new AtLeast(
						Count.TotalLoyaltyOf(
							Ref.Permanents(Is.Planeswalker & Is.Yours & Is.Subtype("Jace"))
						),
						25
					),
				},
			],
			["Garruk, Veiled Butcher"] =
			[
				new OpponentsCreaturesExiledInsteadOfDying(),
				Loyalty(
					2,
					[Creature with { Optional = true }],
					new ModifyUntilEndOfTurn(Ref.Target(), -4, -1) { UntilYourNextTurn = true }
				),
				Loyalty(
					-2,
					[],
					new EachSacrifices(Ref.EachPlayer, Is.Creature) { IfYouDid = [CreateBeast] }
				),
				Loyalty(
					-3,
					[],
					new Discard(Ref.EachOpponent, 2)
					{
						Then =
						[
							new If(
								new Not(new AtLeast(Count.NonlandCardsChosen, 2)),
								[new DrawCards(Ref.You, 1)]
							),
						],
					}
				),
			],
			["Garruk, Curse Breaker"] =
			[
				When(
					new Trigger(EventKind.Entered)
					{
						Subject = Is.Creature & Is.Yours & Is.PowerAtLeast(4),
					},
					[],
					new DrawCards(Ref.You, 1)
				),
				Loyalty(
					2,
					[
						Target(Is.Land) with
						{
							Optional = true,
						},
						Target(Is.Land) with
						{
							Optional = true,
							NotThePrevious = true,
						},
					],
					new Untap(Ref.Target(0)),
					new Untap(Ref.Target(1))
				),
				Loyalty(-3, [], CreateBeast),
				Loyalty(
					-4,
					[],
					new UntilYourNextTurnWhenever(
						When(
							TriggerEvent.CreaturesAttackYourOpponent,
							[],
							new ModifyUntilEndOfTurn(
								Ref.Permanents(Is.Yours & Is.AttackingAPlayer),
								2,
								2,
								"Trample"
							)
						)
					)
				),
			],
			["Tam, the Possibility"] =
			[
				new CostChange(-1, Is.Planeswalker),
				new ActivatedAbilityComponent(
					ManaCost.Parse("{W}{U}{B}{R}{G}"),
					[new Proliferate(Count.SubtypesAmong(Ref.PlaneswalkersYouControl))]
				)
				{
					Costs = [new TapSelfCost()],
				},
			],

			// ===== Mythics and a rare (8g): type change, casting from exile, casting mid-resolution =====
			["Puppet Crafting"] =
			[
				new AuraComponent(
					Target(Is.Type(CardType.Artifact | CardType.Enchantment) & !Is.Subtype("Aura"))
				),
				new EnchantedBecomesCreature("Construct", 5, 5),
				new ActivatedAbilityComponent(
					ManaCost.Parse("{4}{G}"),
					[new ReturnFromGraveyard(Ref.Source, ZoneType.Hand)]
				)
				{
					From = ZoneType.Graveyard,
				},
			],
			["Emrakul, the Exigent Doom"] =
			[
				When(TriggerEvent.SelfCast, [], new Untap(Ref.LandsYouControl)),
				When(
					TriggerEvent.SelfBecomesTargetOfOpponentsSpellOrAbility,
					[],
					new CounterUnlessPays(Ref.EventObject, ManaCost.None)
					{
						Costs = [new SacrificeCost { Count = 3 }],
					}
				),
				new ActivatedAbilityComponent(
					ManaCost.Parse("{3}"),
					[new LandTapsForTwoUntilThisIsCast(Ref.Target())]
				)
				{
					Costs = [new ExileSelfCost()],
					From = ZoneType.Hand, // ponytail: "target land" — yours: the grant replaces the land's own mana (one yield
					// per permanent), so on an opponent's land it would shut off a colour.
					Targets = [Target(Is.Land & Is.Yours)],
				},
			],
			["Uldaros Theorix"] =
			[
				When(TriggerEvent.SelfEnters, [], new ExileOneOfEachTypeAndCastCopies(6)) with
				{
					If = new SourceWasCast(),
				},
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
