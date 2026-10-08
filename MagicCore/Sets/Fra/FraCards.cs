using System.Collections.Immutable;
using ImmutableGameObjects;
using MagicCore.Cards;
using MagicCore.Effects;
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

	/// <summary>"Permanent card" / "nonland permanent card". FRA has no battles.</summary>
	const CardType NonlandPermanentTypes =
		CardType.Creature | CardType.Artifact | CardType.Enchantment | CardType.Planeswalker;
	const CardType PermanentTypes = NonlandPermanentTypes | CardType.Land;
	static readonly TargetSpec CreatureOrPlaneswalker = new(TargetKind.CreatureOrPlaneswalker);

	static SpellEffectsComponent Spell(TargetSpec[] targets, params GameAction[] effects) =>
		new([.. effects]) { Targets = [.. targets] };

	static TriggeredAbilityComponent When(
		TriggerEvent on,
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
			If = new Not(new SourceIsPrepared()),
		};

	/// <summary>Threshold — "as long as there are seven or more cards in your graveyard".</summary>
	static readonly AtLeast Threshold = new(Count.NumberOf(Ref.CardsInYourGraveyard), 7);

	/// <summary>Seed Suture: "Put a +1/+1 counter on target creature. You gain 1 life."</summary>
	static PrepareSpellComponent SeedSuture(string card) =>
		PrepareSpell(card, [Creature], new AddCounters(Ref.Target(), 1), new GainLife(Ref.You, 1));

	/// <summary>Peer Review: "Create a ... Cadet. Surveil 1."</summary>
	static PrepareSpellComponent PeerReview(string card) =>
		PrepareSpell(card, [], new CreateTokens(Cadet, 1), new Surveil(1));

	/// <summary>Vicious Verse: "deals 1 damage to target opponent."</summary>
	static PrepareSpellComponent ViciousVerse(string card) =>
		PrepareSpell(
			card,
			[new TargetSpec(TargetKind.Player, Whose.Opponent)],
			new DealDamage(Ref.Target(), 1)
		);

	/// <summary>Omit Variables: "Mill three cards."</summary>
	static PrepareSpellComponent OmitVariables(string card) =>
		PrepareSpell(card, [], new Mill(Ref.You, 3));

	/// <summary>Soul Tether: "Create a Heartwood token."</summary>
	static PrepareSpellComponent SoulTether(string card) =>
		PrepareSpell(
			card,
			[],
			new CreateTokens(Heartwood, 1)
			{
				Abilities =
				[
					new ManaAbilityComponent(ManaColor.Red),
					new ManaAbilityComponent(ManaColor.Green),
				],
			}
		);

	/// <summary>"Basic landcycling {2} ({2}, Discard this card: Search your library for a basic land card, reveal it, put it into your hand, then shuffle.)"</summary>
	static ActivatedAbilityComponent BasicLandcycling =>
		new(
			ManaCost.Parse("{2}"),
			[new SearchLibrary(CardType.Land, ZoneType.Hand) { Supertype = Supertype.Basic }]
		)
		{
			From = ZoneType.Hand,
			DiscardSelf = true,
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

	/// <summary>One bullet: its target words and effects. Its text is filled in from the card.</summary>
	static Mode M(TargetSpec[] targets, params GameAction[] effects) =>
		new("", [.. targets], [.. effects]);

	/// <summary>"Choose one/two —" spell. The modes must match the printed bullets one for one.</summary>
	static SpellEffectsComponent ModalSpell(string name, int choose, params Mode[] modes) =>
		new([]) { Modes = Printed(name, modes), Choose = choose };

	static TriggeredAbilityComponent ModalWhen(
		string name,
		TriggerEvent on,
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
						new TargetIsNotAttacking(0),
						[new DrawCards(Ref.ControllerOfTarget(0), 1)]
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
					new ModifyUntilEndOfTurn(Ref.Source, Count.PowerOf(Ref.EventCard), 0)
				),
			],
			["Koth, the Geomancer"] =
			[
				When(
					TriggerEvent.LandYouControlEnters,
					[],
					new DealDamage(Ref.EachOpponent, 1),
					new If(new EventCardHasSubtype("Mountain"), [new AddMana(ManaColor.Red)])
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

			// ===== Modal: "Choose one/two —" (Modes.cs) =====
			["Surgical Precision"] =
			[
				ModalSpell(
					"Surgical Precision",
					1,
					M(
						[Creature with { MinToughness = 4 }],
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
							new TargetSpec(TargetKind.Creature, Whose.You),
							CreatureOrPlaneswalker with
							{
								Whose = Whose.Opponent,
							},
						],
						new DamageFrom(Ref.Target(0), Ref.Target(1), Count.PowerOf(Ref.Target(0)))
					),
					M(
						[
							new TargetSpec(TargetKind.Permanent, Whose.You)
							{
								AnyOfTypes = CardType.Planeswalker,
							},
							CreatureOrPlaneswalker with
							{
								Whose = Whose.Opponent,
							},
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
					M([new TargetSpec(TargetKind.SpellOrCreature)], new ReturnToHand(Ref.Target())),
					M([], new ModifyUntilEndOfTurn(Ref.CreaturesYouControl, 1, 2))
				),
			],
			["Konstrari Charm"] =
			[
				ModalSpell(
					"Konstrari Charm",
					1,
					M([Creature with { WithKeyword = "Flying" }], new DealDamage(Ref.Target(), 6)),
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
					M([new TargetSpec(TargetKind.AnyTarget)], new DealDamage(Ref.Target(), 3)),
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
						[
							new TargetSpec(TargetKind.Permanent)
							{
								NotTypes = CardType.Creature | CardType.Land,
							},
						],
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
							new TargetIs(0, CardType.Enchantment, Supertype.Legendary),
							[new DrawCards(Ref.You, 1)]
						),
						new Destroy(Ref.Target()),
					]
				)
				{
					SacrificeSelf = true,
					SorcerySpeed = true,
					Targets =
					[
						new TargetSpec(TargetKind.Permanent)
						{
							AnyOfTypes = CardType.Artifact | CardType.Enchantment,
						},
					],
				},
			],
			["Living Library"] =
			[
				new ActivatedAbilityComponent(
					ManaCost.Parse("{6}"),
					[new ShuffleIntoLibrary(Ref.Target())]
				)
				{
					SacrificeSelf = true,
					Targets = [CreatureOrPlaneswalker with { Whose = Whose.Opponent }],
				},
			],
			["Marwyn, the Clearcutter"] =
			[
				new ActivatedAbilityComponent(ManaCost.Parse("{2}"), [new DrawCards(Ref.You, 1)])
				{
					TapCost = true,
					Sacrifice = new TargetSpec(TargetKind.Permanent, Whose.You)
					{
						AnyOfTypes = CardType.Artifact | CardType.Land,
					},
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
					Sacrifice = CreatureOrPlaneswalker with { Whose = Whose.You, Another = true },
				},
			],
			["Break Under Pressure"] =
			[
				Spell(
					[new TargetSpec(TargetKind.Player, Whose.Opponent)],
					new SacrificeGreatestManaValue(
						Ref.Target(),
						CardType.Creature | CardType.Planeswalker
					),
					new GainLife(Ref.You, 2)
				),
			],
			["Silence the Echo"] =
			[
				new SacrificeOrPayAdditionalCost(
					CreatureOrPlaneswalker with
					{
						Whose = Whose.You,
					},
					3
				),
				Spell([CreatureOrPlaneswalker], new Destroy(Ref.Target())),
			],
			["Way of the Deathbringer"] =
			[
				When(TriggerEvent.SelfEnters, [], new EmpowerJace(5)),
				Grant(
					-2,
					[],
					new MaySacrifice(
						new TargetSpec(TargetKind.Creature, Whose.You),
						[new CreateTokens(Beast, 1)]
					)
				),
			],
			["Graft Surgeon"] =
			[
				new EntersWithCountersComponent(1),
				When(
					TriggerEvent.SelfDies,
					[new TargetSpec(TargetKind.Creature, Whose.You) { Optional = true }],
					new PutEventCardCounters(Ref.Target())
				),
			],
			["Massacre Girl, Most Wanted"] =
			[
				When(
					TriggerEvent.AnotherCreatureOrPlaneswalkerYouControlDies,
					[new TargetSpec(TargetKind.Player, Whose.Opponent)],
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
					[
						new TargetSpec(TargetKind.Permanent, Whose.Opponent)
						{
							NotTypes = CardType.Land,
						},
					],
					new ExileUntilSourceLeaves(Ref.Target())
				),
			],
			// ===== Continuous effects (Rules/Characteristics.cs) =====
			["Samut, Hazoret's Champion"] =
			[
				new StaticEffectComponent(Affects.CreaturesYouControl, "Haste"),
			],
			["Ajani's Anguish"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[new TargetSpec(TargetKind.AnyTarget)],
					new DealDamage(Ref.Target(), Count.X)
				),
				new StaticEffectComponent(Affects.CreaturesYouControl, "Trample"),
			],
			["Ghalta the Unstoppable"] =
			[
				new CostsLessByGreatestPowerYouControl(),
				new StaticEffectComponent(Affects.OtherCreaturesYouControl, "Trample"),
			],
			["Skilled Battlecarver"] =
			[
				new StaticEffectComponent(Affects.Self, "First strike")
				{
					While = StaticCondition.DuringYourTurn,
				},
				new ActivatedAbilityComponent(
					ManaCost.Parse("{1}{R}"),
					[new ModifyUntilEndOfTurn(Ref.Source, 1, 0)]
				),
			],
			["Mind Meanderer"] =
			[
				new StaticEffectComponent(Affects.Self, "Vigilance")
				{
					While = StaticCondition.YouControlAJacePlaneswalker,
				},
				When(
					TriggerEvent.SelfEnters,
					[new TargetSpec(TargetKind.Creature, Whose.Opponent) { Optional = true }],
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
						CreatureOrPlaneswalker with
						{
							Whose = Whose.You,
						},
						[
							new EachSacrifices(
								Ref.EachOpponent,
								new TargetSpec(TargetKind.Creature, Whose.You)
							),
						]
					)
				),
				new StaticEffectComponent(Affects.Self)
				{
					Power = Count.NumberOf(
						Ref.YourGraveyard(CardType.Creature | CardType.Planeswalker)
					),
				},
			],
			["Tetsuko Umezawa, Fugitive"] =
			[
				new StaticEffectComponent(Affects.CreaturesYouControl, Combat.CantBeBlocked)
				{
					OnlyPowerOrToughnessAtMost = 1,
				},
			],
			["Eardrum Rattler"] =
			[
				new ActivatedAbilityComponent(
					ManaCost.Parse("{1}"),
					[new ModifyUntilEndOfTurn(Ref.Target(), 0, 0, Combat.CantBeBlocked)]
				)
				{
					TapCost = true,
					Targets =
					[
						new TargetSpec(TargetKind.Creature, Whose.You)
						{
							Another = true,
							MaxPower = 2,
						},
					],
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
				new CostsLessIfYouCastNoncreatureSpellThisTurn(2),
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
					TriggerEvent.OpponentsCreatureWithPowerOrToughness1OrLessBlocks,
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
						new TargetSpec(TargetKind.Permanent, Whose.Opponent)
						{
							AnyOfTypes = CardType.Artifact | CardType.Creature,
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
					[new TargetSpec(TargetKind.Creature, Whose.You) { Another = true }],
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
				new AttacksDespiteDefender(new AtLeast(Count.YourSurveilsThisTurn, 1)),
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
				new StaticEffectComponent(Affects.OtherCreaturesYouControl, "Haste")
				{
					OnlyWithCounter = true,
				},
				new ActivatedAbilityComponent(
					ManaCost.Parse("{1}{R}"),
					[new AddCounters(Ref.Target(), 1)]
				)
				{
					TapCost = true,
					Targets = [Creature with { EnteredThisTurn = true }],
				},
			],
			["Mabel, Valley Hero"] =
			[
				When(
					TriggerEvent.CreatureYouControlEnters,
					[Creature with { EnteredThisTurn = true }],
					new AddCounters(Ref.Target(), 1)
				),
			],
			["Command the Stage"] =
			[
				// "Create a Cadet, then a counter on each OTHER Wizard token" — counters first, then
				// the Cadet: the same result without remembering which token is new.
				Spell(
					[],
					new AddCounters(Ref.TokensYouControl with { Subtype = "Wizard" }, 1),
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
					[
						new TargetSpec(TargetKind.CardInYourGraveyard)
						{
							AnyOfTypes = CardType.Creature,
							MaxManaValue = 3,
						},
					],
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
					[
						new TargetSpec(TargetKind.CardInYourGraveyard)
						{
							AnyOfTypes = CardType.Instant | CardType.Sorcery,
						},
					],
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
				new FlashbackComponent(ManaCost.Parse("{1}{U/R}{U/R}")) { DiscardACard = true },
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
					[Creature with { Whose = Whose.Opponent, MaxManaValue = 3 }],
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
					[new TargetSpec(TargetKind.Player)],
					new DrawCards(Ref.Target(), 3),
					new LoseLife(Ref.Target(), 3)
				),
			],
			["Void Extrapolator // Omit Variables"] =
			[
				EntersPrepared,
				new StaticEffectComponent(Affects.Self)
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
					[new TargetSpec(TargetKind.CardInYourGraveyard) { AnyOfTypes = CardType.Land }],
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
					[new PutOnBottomOfLibrary(Ref.Target())]
				)
				{
					Targets = [new TargetSpec(TargetKind.CardInYourGraveyard)],
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
				new StaticEffectComponent(Affects.Self) { Power = 1, If = Threshold },
				new StaticEffectComponent(Affects.Self, "Flying") { If = Threshold },
				OmitVariables("Theorix Metamage // Omit Variables"),
			],
			["Vigorbloom Vanguard // Seed Suture"] =
			[
				EntersPrepared,
				new StaticEffectComponent(Affects.CreaturesYouControl, "Vigilance")
				{
					OnlyWithCounter = true,
				},
				SeedSuture("Vigorbloom Vanguard // Seed Suture"),
			],
			["Whiplash Wordsmith // Vicious Verse"] =
			[
				EntersPrepared,
				new StaticEffectComponent(Affects.Self, "Flying")
				{
					If = new AtLeast(Count.NoncombatDamageToOpponentsThisTurn, 1),
				},
				new StaticEffectComponent(Affects.Self, "Haste")
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
						CreatureOrPlaneswalker with
						{
							Whose = Whose.Opponent,
						},
						Creature with
						{
							Whose = Whose.You,
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
					From = ZoneType.Graveyard,
					ExileSelf = true,
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
					From = ZoneType.Graveyard,
					ExileSelf = true,
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
					From = ZoneType.Graveyard,
					ExileSelf = true,
					// "another target creature card": a graveyard target is never its source
					Targets =
					[
						new TargetSpec(TargetKind.CardInYourGraveyard)
						{
							AnyOfTypes = CardType.Creature,
						},
					],
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
					From = ZoneType.Graveyard,
					ExileFromGraveyard = new TargetSpec(TargetKind.CardInYourGraveyard)
					{
						AnyOfTypes = CardType.Creature,
					},
				},
			],
			["Hallway Heckler // Vicious Verse"] =
			[
				EntersPrepared,
				new ActivatedAbilityComponent(ManaCost.None, [new DrawCards(Ref.You, 1)])
				{
					TapCost = true,
					DiscardACard = true,
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
						new TargetSpec(TargetKind.CardInYourGraveyard)
						{
							AnyOfTypes = CardType.Creature | CardType.Planeswalker,
						},
					],
				},
			],
			["Marwyn, the Preserver"] =
			[
				new StaticEffectComponent(Affects.LandsYouControl, "Hexproof"),
				new ActivatedAbilityComponent(
					ManaCost.Parse("{2}"),
					[new ReturnFromGraveyard(Ref.Target(), ZoneType.Hand)]
				)
				{
					Targets =
					[
						new TargetSpec(TargetKind.CardInYourGraveyard)
						{
							AnyOfTypes = CardType.Land,
						},
					],
				},
			],
			["Fateshaper Aspirant"] =
			[
				ModalWhen(
					"Fateshaper Aspirant",
					TriggerEvent.SelfEnters,
					1,
					M(
						[
							new TargetSpec(TargetKind.CardInYourGraveyard)
							{
								WithSupertype = Supertype.Legendary,
							},
						],
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
					[
						new TargetSpec(TargetKind.CardInYourGraveyard)
						{
							AnyOfTypes = PermanentTypes,
						},
					],
					new ReturnFromGraveyard(Ref.Target(), ZoneType.Hand),
					new GainLife(Ref.You, 4)
				),
			],
			["Rewrite Regrets"] =
			[
				Spell(
					[
						new TargetSpec(TargetKind.CardInYourGraveyard)
						{
							AnyOfTypes = CardType.Creature | CardType.Planeswalker,
							MaxManaValue = 6,
						},
					],
					new ReturnFromGraveyard(Ref.Target(), ZoneType.Battlefield),
					new EmpowerJace(2)
				),
			],
			["Ruric Thar, Magecrusher"] =
			[
				new CantBeCountered(),
				new StaticEffectComponent(Affects.Self, "Hexproof")
				{
					If = new Not(new SourceHasDealtCombatDamage()),
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
					From = ZoneType.Hand,
					DiscardSelf = true,
					Targets = [Creature with { WithKeyword = "Flying" }],
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
					[new TargetSpec(TargetKind.CardInAnyGraveyard) { Optional = true }],
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
					TapCost = true,
					DiscardACard = true,
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
					TapCost = true,
					SacrificeSelf = true,
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
						[
							new WhenYouDo(
								[new TargetSpec(TargetKind.AnyTarget)],
								[new DealDamage(Ref.Target(), 2)]
							),
						]
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
					[new TargetSpec(TargetKind.Player, Whose.Opponent)],
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
						[
							new TargetSpec(TargetKind.Spell)
							{
								AnyOfTypes = CardType.Creature,
								OrSupertype = Supertype.Legendary,
							},
						],
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
