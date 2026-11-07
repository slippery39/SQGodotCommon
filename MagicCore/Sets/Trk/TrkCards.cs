using System.Collections.Immutable;
using ImmutableGameObjects;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Filters;
using MagicCore.Targeting;
using MagicCore.Turns;
using static MagicCore.Sets.Scripting;
using static MagicCore.Sets.Tokens;

namespace MagicCore.Sets.Trk;

/// <summary>
/// Star Trek's hand-written abilities, keyed by card name (story MC-5) — as <see cref="Fra.FraCards"/>:
/// names, costs, types, P/T and keywords come from TRK.json; only what the rules text DOES is written
/// here. A card another set already scripts (a reprint) is NOT scripted again — <see cref="CardRegistry"/>
/// refuses it. Survey, tiers and what each card waits on: <c>MagicCore/TrkCoverage.md</c>.
/// </summary>
public static class TrkCards
{
	// ===== Tokens (TRK's own; Treasure, Clue and Food are Tokens') =====

	/// <summary>"A 1/1 red Officer creature token."</summary>
	public static readonly CardData Officer = Token(
		"Officer",
		"Creature — Officer",
		"1",
		"1",
		["R"]
	);

	/// <summary>"A 1/1 colorless Borg artifact creature token."</summary>
	public static readonly CardData Borg = Token("Borg", "Artifact Creature — Borg", "1", "1");

	/// <summary>"A 2/1 red Klingon Warrior creature token with haste."</summary>
	public static readonly CardData KlingonWarrior = Token(
		"Klingon Warrior",
		"Creature — Klingon Warrior",
		"2",
		"1",
		["R"],
		["Haste"]
	);

	/// <summary>"A 2/2 black Romulan creature token."</summary>
	public static readonly CardData Romulan = Token(
		"Romulan",
		"Creature — Romulan",
		"2",
		"2",
		["B"]
	);

	/// <summary>"A 3/3 colorless Illusion artifact creature token."</summary>
	public static readonly CardData ArtifactIllusion = Token(
		"Illusion",
		"Artifact Creature — Illusion",
		"3",
		"3"
	);

	/// <summary>Cleveland Booker, Empath: "a 2/2 red and green Worm creature token with 'This token can't block.'"</summary>
	public static readonly CardData Worm = Token("Worm", "Creature — Worm", "2", "2", ["R", "G"]);

	/// <summary>Captain Jonathan Archer: "Porthos, a legendary 1/1 green Dog creature token".</summary>
	public static readonly CardData Porthos = Token(
		"Porthos",
		"Legendary Creature — Dog",
		"1",
		"1",
		["G"]
	);

	/// <summary>Gwyndala, the Progeny: "a colorless Equipment artifact token named Neuroflux Heirloom with 'Equipped creature gets +1/+1' and equip {2}".</summary>
	public static readonly CardData NeurofluxHeirloom = Token(
		"Neuroflux Heirloom",
		"Artifact — Equipment"
	);

	/// <summary>The City on the Edge of Forever: "a 0/1 white Human creature token with 'Permanents can't phase in.'"</summary>
	public static readonly CardData CityHuman = Token("Human", "Creature — Human", "0", "1", ["W"]);

	/// <summary>Rok-Tahk, Budding Scientist: "Murf, a legendary 0/1 green Ooze Slug creature token with indestructible".</summary>
	public static readonly CardData Murf = Token(
		"Murf",
		"Legendary Creature — Ooze Slug",
		"0",
		"1",
		["G"],
		["Indestructible"]
	);

	/// <summary>"A tapped Planet land token named New Planet with '{T}: Add one mana of any color.'"</summary>
	public static readonly CardData NewPlanet = Token("New Planet", "Land — Planet");

	/// <summary>"Create a tapped Planet land token named New Planet …" (Genesis Effect, Stable Wormhole).</summary>
	public static CreateTokens CreateNewPlanet =>
		new(NewPlanet, 1)
		{
			Abilities = [.. ManaAbilities.AnyColor.Select(c => new ManaAbilityComponent(c))],
			Tapped = true,
		};

	/// <summary>
	/// The Planet duals: "This land enters tapped. When this land enters, you gain 1 life.
	/// {T}: Add {A} or {B}."
	/// </summary>
	static ImmutableArray<GameComponent> PlanetDual(ManaColor a, ManaColor b) =>
		[
			new EntersTappedComponent(),
			new TriggeredAbilityComponent(TriggerEvent.SelfEnters, [new GainLife(Ref.You, 1)]),
			new ManaAbilityComponent(a),
			new ManaAbilityComponent(b),
		];

	/// <summary>
	/// The enters-untapped lands (Federation Outpost …): "This land enters tapped unless you control two
	/// or more other lands. When this land enters untapped, choose one — • [mode] • Surveil 1. {T}: Add
	/// [color]." Untapped is read as it enters, so Warp Speed's lands enter untapped and trigger too.
	/// </summary>
	static ImmutableArray<GameComponent> EntersUntapped(string name, ManaColor color, Mode mode) =>
		[
			new EntersTappedComponent(YouControlTwoOrMoreOtherLands),
			ModalWhen(
				name,
				TriggerEvent.SelfEnters with
				{
					Subject = Is.Source & !Is.Tapped,
				},
				1,
				mode,
				M([], new Surveil(1))
			),
			new ManaAbilityComponent(color),
		];

	/// <summary>
	/// Captain James T. Kirk: "Whenever Captain Kirk enters or attacks, choose one. If you have no cards
	/// in hand, choose one or more instead. • Discard a card, then draw a card. • Create a 1/1 red
	/// Officer creature token. • Creatures you control get +1/+0 until end of turn."
	/// </summary>
	static TriggeredAbilityComponent CaptainKirk(Trigger on) =>
		ModalWhen(
			"Captain James T. Kirk",
			on,
			1,
			M([], new Discard(Ref.You, 1), new DrawCards(Ref.You, 1)),
			M([], new CreateTokens(Officer, 1)),
			M([], new ModifyUntilEndOfTurn(Ref.CreaturesYouControl, 1, 0))
		) with
		{
			ChooseAnyNumberIf = new Not(new AtLeast(Count.NumberOf(Ref.CardsInYourHand), 1)),
		};

	/// <summary>"Artifact creatures you control" (Assault Drone, Locutus, Command Subadjunct).</summary>
	static readonly Filter ArtifactCreaturesYouControl = Is.Artifact & Is.Creature & Is.Yours;

	/// <summary>
	/// The Anomaly lands: "{T}: Add {C}. [cost], {T}, Sacrifice this land: [effects]. Create a tapped
	/// Planet land token named New Planet …"
	/// </summary>
	static ImmutableArray<GameComponent> Anomaly(
		string cost,
		TargetSpec[] targets,
		params GameAction[] effects
	) =>
		[
			new ManaAbilityComponent(ManaColor.Colorless),
			new ActivatedAbilityComponent(ManaCost.Parse(cost), [.. effects, CreateNewPlanet])
			{
				Costs = [new TapSelfCost(), new SacrificeSelfCost()],
				Targets = [.. targets],
			},
		];

	/// <summary>
	/// V'Ger, the Intruder: "Whenever V'Ger enters or attacks, choose one — • Look at target opponent's
	/// hand, then draw a card. • Creatures your opponents control get -1/-0 until your next turn."
	/// </summary>
	static TriggeredAbilityComponent VGer(Trigger on) =>
		ModalWhen(
			"V'Ger, the Intruder",
			on,
			1,
			M(
				[TargetSpec.Player(Is.Opponents)],
				new LookAtHand(Ref.Target()),
				new DrawCards(Ref.You, 1)
			),
			M(
				[],
				new ModifyUntilEndOfTurn(Ref.Permanents(Is.Creature & Is.Opponents), -1, 0)
				{
					UntilYourNextTurn = true,
				}
			)
		);

	/// <summary>
	/// Beam me up [cost] (MC-5.2): "You may cast this card from your graveyard for [cost] if you also
	/// return a creature you control to its owner's hand. Then exile this spell." — a flashback whose
	/// cost beside the mana is <see cref="BeamMeUpCost"/>.
	/// </summary>
	static FlashbackComponent BeamMeUp(string cost) =>
		new(ManaCost.Parse(cost)) { Costs = [new BeamMeUpCost()] };

	/// <summary>Strange new worlds (MC-5.2): "the number of differently named lands you control".</summary>
	static Count DifferentlyNamedLands => Count.DistinctNamesOf(Ref.LandsYouControl);

	/// <summary>Federation (MC-5.2): "the number of creature types among non-Borg creatures you control" — Borg not considered at all.</summary>
	static Count Federation =>
		Count.CreatureTypesAmong(Ref.Permanents(Is.Creature & Is.Yours & !Is.Subtype("Borg")));

	/// <summary>
	/// Station (MC-5.2):"Station (Tap another creature you control: Put charge counters equal to its
	/// power on this Spacecraft. Station only as a sorcery. It's an artifact creature at N+.)" and the
	/// keywords in the "N+" box beside its P/T — Oberth-Class Starship: Station(6, "Flying"). The
	/// creature tapped is the ability's event card, its power read as it resolves.
	/// </summary>
	static GameComponent[] Station(int creatureAt, params string[] keywords) =>
		[
			new StationComponent(creatureAt),
			StationAbility,
			.. keywords.Select(k => new StaticEffectComponent(Is.Source, k)
			{
				If = new AtLeast(Count.ChargeCountersOn(Ref.Source), creatureAt),
			}),
		];

	internal static readonly Dictionary<string, ImmutableArray<GameComponent>> Scripts =
		new()
		{
			// ===== MC-5.5: the Planet duals =====
			["Andoria"] = PlanetDual(ManaColor.Blue, ManaColor.Red),
			["Bajor"] = PlanetDual(ManaColor.Red, ManaColor.White),
			["Cardassia Prime"] = PlanetDual(ManaColor.White, ManaColor.Black),
			["Earth, Sector 001"] = PlanetDual(ManaColor.Green, ManaColor.Blue),
			["Ferenginar"] = PlanetDual(ManaColor.Black, ManaColor.Green),
			["Orion"] = PlanetDual(ManaColor.Red, ManaColor.Green),
			["Qo'noS"] = PlanetDual(ManaColor.Black, ManaColor.Red),
			["Risa"] = PlanetDual(ManaColor.Green, ManaColor.White),
			["Romulus"] = PlanetDual(ManaColor.Blue, ManaColor.Black),
			["Vulcan"] = PlanetDual(ManaColor.White, ManaColor.Blue),

			// ===== MC-5.3 batch 1: tier 0–1 commons =====
			["Assault Drone"] =
			[
				new StaticEffectComponent(ArtifactCreaturesYouControl) { Power = 1 },
			],
			["Automated Warfare System"] =
			[
				When(
					TriggerEvent.SelfAttacks,
					[],
					new MaySacrifice(
						(Is.Creature | Is.Artifact) & !Is.Source,
						[new DrawCards(Ref.You, 1)]
					)
				),
			],
			["Battle-Scarred Survivalist"] =
			[
				When(
					new Trigger(EventKind.Died) { Subject = Is.Creature & !Is.Source },
					[],
					new AddCounters(Ref.Source, 1)
				) with
				{
					OncePerTurn = true,
				},
			],
			["Captain's Tutelage"] =
			[
				new AuraComponent(Creature),
				When(
					TriggerEvent.SelfEnters,
					[],
					new ModifyUntilEndOfTurn(Ref.EnchantedCreature, 0, 0, "First strike")
				),
				AttachedGets(2, 0),
			],
			["Cetacean Ops"] =
			[
				Ward("{1}"),
				When(TriggerEvent.YouCastNoncreatureSpell, [], new AddCounters(Ref.Source, 1)),
			],
			["Cha'DIch Investigator"] =
			[
				When(TriggerEvent.SelfEnters, [], new CreateTokens(KlingonWarrior, 1)),
			],
			["Cloistered Telepath"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[],
					new DrawCards(Ref.You, 1),
					new Discard(Ref.You, 1)
				),
			],
			["Command Decision"] =
			[
				ModalSpell(
					"Command Decision",
					1,
					M([Target(Is.Creature & Is.PowerAtLeast(4))], new Destroy(Ref.Target())),
					M([], new DrawCards(Ref.You, 1))
				),
			],
			["Consider the Prime Directive"] =
			[
				ModalSpell(
					"Consider the Prime Directive",
					1,
					M(
						[TargetSpec.Spell()],
						new CounterUnlessPays(Ref.Target(), ManaCost.Parse("{3}"))
					),
					M(
						[Creature with { Optional = true }],
						new Tap(Ref.Target()),
						new AddStunCounters(Ref.Target(), 1),
						new DrawCards(Ref.You, 1)
					)
				),
			],
			["Cybernetic Specialist"] =
			[
				When(TriggerEvent.SelfEnters, [], new Surveil(1)),
				new ActivatedAbilityComponent(
					ManaCost.Parse("{1}"),
					[new PutIntoLibrary(Ref.Target(), OnTop: false)]
				)
				{
					Costs = [new TapSelfCost()],
					Targets = [TargetSpec.InYourGraveyard()],
				},
			],
			["Dahar Master"] =
			[
				// "you may sacrifice another nonland permanent. When you do, … 2 damage to any target."
				When(
					TriggerEvent.SelfEnters,
					[],
					new MaySacrifice(
						!Is.Land & !Is.Source,
						[new WhenYouDo([TargetSpec.AnyTarget()], [new DealDamage(Ref.Target(), 2)])]
					)
				),
			],
			["Dathon and Picard at El-Adrel"] =
			[
				ModalSpell(
					"Dathon and Picard at El-Adrel",
					1,
					M([Creature], new ModifyUntilEndOfTurn(Ref.Target(), 3, 3, "Trample")),
					M(
						[],
						new EachSacrifices(
							Ref.EachOpponent,
							(Is.Artifact | Is.Enchantment) & !Is.Token
						)
					)
				),
			],
			["Dominion Assault Force"] =
			[
				ModalWhen(
					"Dominion Assault Force",
					TriggerEvent.SelfEnters,
					1,
					M([TargetSpec.InAnyGraveyard()], new ExileFromGraveyard(Ref.Target())),
					M([], new Surveil(1))
				),
			],
			["Emergency Medical Hologram"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[Creature],
					new AddCounters(Ref.Target(), 1),
					new GainLife(Ref.You, 1)
				),
			],
			["Federation Probe"] =
			[
				ModalWhen(
					"Federation Probe",
					TriggerEvent.SelfEnters,
					1,
					M([Creature], new ModifyUntilEndOfTurn(Ref.Target(), 1, 1)),
					M([], new Surveil(1))
				),
			],
			["Free Borg Revolutionaries"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[],
					new ModifyUntilEndOfTurn(Ref.CreaturesYouControl, 1, 1)
				),
				BasicLandcycling,
			],
			["Fugitive Listener"] =
			[
				When(TriggerEvent.SelfEnters, [], new Discard(Ref.EachOpponent, 1)),
			],
			["Genesis Effect"] =
			[
				Spell(
					[Creature, Target(Is.Land & Is.Yours)],
					new DealDamage(Ref.Target(0), 6),
					new Destroy(Ref.Target(1)),
					CreateNewPlanet
				),
			],
			["He's Dead, Jim"] = [Spell([Creature], new Destroy(Ref.Target()), new Surveil(1))],
			["Horta"] = [BasicLandcycling],
			["Humpback Whales"] = [Ward("{2}"), BasicLandcycling],
			["Interstellar Effigy"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[Target(Is.Creature & Is.Opponents)],
					new ReturnToHand(Ref.Target())
				),
			],
			["Make It So"] =
			[
				ModalSpell(
					"Make It So",
					1,
					M(
						[Target(Is.Yours), TargetSpec.Player()],
						new Untap(Ref.Target(0)),
						new DrawCards(Ref.Target(1), 2)
					),
					M([], new CreateTokens(Officer, 1), new DrawCards(Ref.You, 2))
				),
			],
			["Malfunctioning Holodeck"] =
			[
				Ward("{2}"),
				When(
					TriggerEvent.SelfEnters,
					[Target(!Is.Land & Is.Opponents)],
					new ExileUntilSourceLeaves(Ref.Target())
				),
			],
			["Moopsy"] =
			[
				new ActivatedAbilityComponent(
					ManaCost.Parse("{2}"),
					[new ModifyUntilEndOfTurn(Ref.Source, 0, 0, "Reach")]
				),
			],
			["Perils of the Past"] =
			[
				ModalSpell(
					"Perils of the Past",
					1,
					M([Creature], new ModifyUntilEndOfTurn(Ref.Target(), 3, 2)),
					M([Target(Is.Artifact)], new Destroy(Ref.Target()))
				),
			],
			["Photosynthetic Physician"] =
			[
				new ActivatedAbilityComponent(ManaCost.Parse("{G}"), [new CreateTokens(Officer, 1)])
				{
					Costs = [new ExileSelfCost()],
					From = ZoneType.Graveyard,
					SorcerySpeed = true,
				},
			],
			["Planetary Bombardment"] =
			[
				// "2 damage … 4 damage to that creature instead if a creature died this turn"
				Spell(
					[Creature],
					new If(
						new AtLeast(Count.CreaturesDiedThisTurn, 1),
						[new DealDamage(Ref.Target(), 4)]
					)
					{
						Otherwise = [new DealDamage(Ref.Target(), 2)],
					}
				),
			],
			["Planetary Patrol"] =
			[
				When(
					TriggerEvent.LandYouControlEnters,
					[],
					new ModifyUntilEndOfTurn(Ref.Source, 1, 1)
				),
			],
			["Plasma Cascade"] =
			[
				Spell(
					[TargetSpec.AnyTarget()],
					new DealDamage(Ref.Target(), 3),
					new MayDiscard([new DrawCards(Ref.You, 1)])
				),
			],
			["Relentless Drednok"] =
			[
				new ActivatedAbilityComponent(
					ManaCost.Parse("{2}{B}"),
					[new DrawCards(Ref.You, 1), new LoseLife(Ref.You, 1)]
				)
				{
					Costs = [new ExileSelfCost()],
					From = ZoneType.Graveyard,
				},
			],
			["Rogue Artificial Intelligence"] =
			[
				ModalWhen(
					"Rogue Artificial Intelligence",
					TriggerEvent.SelfEnters,
					1,
					M([], new GainLife(Ref.You, 3)),
					M([], new Surveil(2))
				),
				BasicLandcycling,
			],
			["Saurian Explorer"] =
			[
				When(
					TriggerEvent.LandYouControlEnters,
					[],
					new ModifyUntilEndOfTurn(Ref.Source, 2, 2)
				),
				new ActivatedAbilityComponent(ManaCost.Parse("{6}"), [CreateNewPlanet])
				{
					SorcerySpeed = true,
				},
			],
			["Savvy Profiteer"] =
			[
				new ManaAbilityComponent(ManaColor.Red),
				new ActivatedAbilityComponent(ManaCost.Parse("{3}"), [CreateTreasure(1)])
				{
					Costs = [new TapSelfCost()],
				},
			],
			["Set Phasers to . . ."] =
			[
				ModalSpell(
					"Set Phasers to . . .",
					1,
					M([Creature], new Tap(Ref.Target()), new AddStunCounters(Ref.Target(), 1)),
					M([Target(Is.Creature & Is.Attacking)], new DealDamage(Ref.Target(), 3))
				),
			],
			["Shields Up!"] =
			[
				// "If it's a creature, put a +1/+1 counter on it" — AddCounters skips a noncreature.
				Spell(
					[Target((Is.Artifact | Is.Creature) & Is.Yours)],
					new ModifyUntilEndOfTurn(Ref.Target(), 0, 0, "Hexproof"),
					new ModifyUntilEndOfTurn(Ref.Target(), 0, 0, "Indestructible"),
					new AddCounters(Ref.Target(), 1)
				),
			],
			["Silicate Surveyor"] = [When(TriggerEvent.SelfEnters, [], new ExileTopPlayable())],
			["Starfleet Security"] =
			[
				When(TriggerEvent.SelfDies, [], CreateClue(1)) with
				{
					If = new DuringYourTurn(),
				},
			],
			["Starfleet Spanner Jockey"] =
			[
				new StaticEffectComponent(Is.Source, Combat.AttacksDespiteDefender)
				{
					If = new AtLeast(Count.NoncreatureSpellsYouCastThisTurn, 1),
				},
			],
			["Tal Shiar Analyst"] =
			[
				new ActivatedAbilityComponent(ManaCost.None, [new DealDamage(Ref.EachOpponent, 1)])
				{
					Costs = [new TapSelfCost()],
				},
				When(TriggerEvent.YouCastNoncreatureSpell, [], new Untap(Ref.Source)),
			],
			["Tal Shiar Operative"] =
			[
				ModalWhen(
					"Tal Shiar Operative",
					TriggerEvent.SelfAttacks,
					1,
					M(
						[Target(Is.Creature & Is.Yours)],
						new ModifyUntilEndOfTurn(Ref.Target(), 0, 0, "Deathtouch")
					),
					M([], new Surveil(1))
				),
			],
			["Tea, Earl Grey, Hot"] =
			[
				When(TriggerEvent.SelfEnters, [], new DrawCards(Ref.You, 1)),
				// "{2}, {T}, Sacrifice this artifact: Choose one —" — a real modal activation (Shayne: dilemma counts it).
				new ActivatedAbilityComponent(ManaCost.Parse("{2}"), [])
				{
					Costs = [new TapSelfCost(), new SacrificeSelfCost()],
					Modes = Printed(
						"Tea, Earl Grey, Hot",
						[
							M([TargetSpec.Player(Is.Opponents)], new DealDamage(Ref.Target(), 2)),
							M([], new GainLife(Ref.You, 3)),
						]
					),
				},
			],
			["Tenacious Drone"] = [When(TriggerEvent.SelfDies, [], new CreateTokens(Borg, 1))],
			["Tricorder"] =
			[
				AttachedGets(1, 1),
				When(TriggerEvent.AttachedCreatureAttacks, [], new Surveil(1)),
				Equip("{1}"),
			],
			["Unexplored Territory"] =
			[
				new ManaAbilityComponent(ManaColor.Colorless),
				new ActivatedAbilityComponent(
					ManaCost.Parse("{1}"),
					[
						new SearchLibrary(CardType.Land, ZoneType.Battlefield)
						{
							Supertype = Supertype.Basic,
							Tapped = true,
						},
					]
				)
				{
					Costs = [new TapSelfCost(), new SacrificeSelfCost()],
				},
			],
			["Vexing Drone"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[Target(Is.Creature & Is.Opponents)],
					new Tap(Ref.Target())
				),
			],
			["Xenobotanist"] = [.. AnyColorMana(ManaRestriction.None)],
			["Xindi Surveyors"] =
			[
				When(TriggerEvent.LandYouControlEnters, [], new AddCounters(Ref.Source, 1)),
			],
			// ===== MC-5.3 batch 1: tier 1–3 uncommons =====
			["Academy Instructor"] =
			[
				new StaticEffectComponent(Is.Source)
				{
					Power = Count.NumberOf(
						Ref.Permanents(Is.Token & Is.Yours & Is.Subtype("Officer"))
					),
				},
				When(TriggerEvent.YouCastNoncreatureSpell, [], new CreateTokens(Officer, 1)),
			],
			["Artificial Anomaly"] = Anomaly(
				"{3}{B}",
				[],
				new LoseLife(Ref.EachOpponent, 2),
				new GainLife(Ref.You, 2)
			),
			["Assembly Specialist"] =
			[
				When(TriggerEvent.SelfEnters, [], new CreateTokens(Borg, 1)),
			],
			["Bat'leth"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[Target(Is.Creature & Is.Opponents)],
					new ModifyUntilEndOfTurn(Ref.Target(), -2, -2)
				),
				AttachedGets(2, 0),
				Equip("{2}"),
			],
			["Collective Drone"] =
			[
				// "*/2": power equal to the number of artifact creatures you control.
				new StaticEffectComponent(Is.Source)
				{
					Power = Count.NumberOf(Ref.Permanents(ArtifactCreaturesYouControl)),
				},
			],
			["Command Subadjunct"] =
			[
				new StaticEffectComponent(ArtifactCreaturesYouControl, "Vigilance"),
			],
			["Commander Raffi Musiker"] =
			[
				ModalWhen(
					"Commander Raffi Musiker",
					TriggerEvent.SelfEnters,
					1,
					M(
						[TargetSpec.InAnyGraveyard() with { Optional = true }],
						new ExileFromGraveyard(Ref.Target()),
						new GainLife(Ref.You, 2)
					),
					M([], CreateClue(1), new LoseLife(Ref.You, 2))
				),
			],
			["Constable Odo"] =
			[
				ModalWhen(
					"Constable Odo",
					TriggerEvent.SelfEnters,
					1,
					M(
						[Target(Is.Creature & Is.Opponents)],
						new Tap(Ref.Target()),
						new AddStunCounters(Ref.Target(), 2)
					),
					M([], CreateClue(2))
				),
			],
			["Culber and Stamets, Star-Crossed"] =
			[
				ModalWhen(
					"Culber and Stamets, Star-Crossed",
					TriggerEvent.SelfEnters,
					1,
					M([Creature], new AddCounters(Ref.Target(), 1)),
					M([], new AddManaOfAnyOneColor(2))
				),
			],
			["D'Ghor Bloodletter"] =
			[
				ModalWhen(
					"D'Ghor Bloodletter",
					TriggerEvent.SelfEnters,
					1,
					M(
						[],
						new ModifyUntilEndOfTurn(Ref.Permanents(Is.Creature & !Is.Source), -1, -1)
					),
					M([], new EachSacrifices(Ref.EachPlayer, Is.Creature))
				),
			],
			["D'Vana Tendi, Optimistic Ensign"] =
			[
				new ActivatedAbilityComponent(ManaCost.None, [new Untap(Ref.Target())])
				{
					Costs = [new TapSelfCost()],
					Targets = [Target(!Is.Source)],
				},
				new ActivatedAbilityComponent(ManaCost.Parse("{3}"), [new Surveil(1)]),
			],
			["Daniels, Temporal Agent"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[TargetSpec.InYourGraveyard(Is.Creature)],
					new ReturnFromGraveyard(Ref.Target(), ZoneType.Hand)
				),
				new ActivatedAbilityComponent(ManaCost.Parse("{2}"), [new GainLife(Ref.You, 2)])
				{
					Costs = [new PutSelfOnBottomCost()],
					From = ZoneType.Graveyard,
				},
			],
			["David and Carol Marcus, Creators"] =
			[
				When(TriggerEvent.SelfEnters, [], CreateNewPlanet),
			],
			["Gaseous Anomaly"] = Anomaly("{5}{U}", [], new DrawCards(Ref.You, 2)),
			["Impossible Choice"] =
			[
				ModalSpell(
					"Impossible Choice",
					1,
					// The tokens first: "its controller" is read while the creature is still there.
					M(
						[Creature],
						new CreateTokens(Officer, 2) { ControlledBy = Ref.ControllerOfTarget(0) },
						new Destroy(Ref.Target())
					),
					M([], new DrawCards(Ref.You, 2), new LoseLife(Ref.EachPlayer, 2))
				),
			],
			["Khaaaaaaaaaaaannn!"] =
			[
				Spell(
					[Creature],
					new ModifyUntilEndOfTurn(Ref.Target(), 0, 0) { ExileIfItWouldDie = true },
					new DealDamage(Ref.Target(), Count.X with { Times = 2 })
				),
			],
			["Khan, Despot by Design"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[],
					new DealDamage(Ref.EachOpponent, 1),
					new DealDamage(Ref.Permanents(Is.Creature & Is.Opponents), 1)
				),
			],
			["Locutus, the One Who Speaks"] =
			[
				new StaticEffectComponent(ArtifactCreaturesYouControl) { Power = 1, Toughness = 1 },
				new ActivatedAbilityComponent(
					ManaCost.Parse("{W}{B}"),
					[new DealDamage(Ref.Target(), 2), new GainLife(Ref.You, 2)]
				)
				{
					Costs = [new DiscardSelfCost()],
					From = ZoneType.Hand,
					Targets = [Creature],
				},
			],
			["Lore, Positronic Provocateur"] =
			[
				ModalWhen(
					"Lore, Positronic Provocateur",
					TriggerEvent.SelfEnters,
					1,
					M(
						[Creature],
						new ModifyUntilEndOfTurn(Ref.Target(), 1, 0, Combat.CantBeBlocked)
					),
					M([], new Surveil(2))
				),
			],
			["Munitions Enthusiast"] =
			[
				When(
					TriggerEvent.SelfDies,
					[TargetSpec.AnyTarget()],
					new DealDamage(Ref.Target(), 1)
				),
			],
			["Neelix, Moral Support"] =
			[
				When(TriggerEvent.SelfEnters, [], CreateFood(1)),
				When(TriggerEvent.YouGainLife, [], new AddCounters(Ref.Source, 1)),
			],
			["Organic Avulsion Unit"] =
			[
				new ActivatedAbilityComponent(
					ManaCost.Parse("{2}"),
					[
						new ModifyUntilEndOfTurn(
							Ref.Permanents(ArtifactCreaturesYouControl),
							0,
							0,
							"Menace"
						),
					]
				),
			],
			["Picard, Leading by Example"] =
			[
				new ActivatedAbilityComponent(
					ManaCost.Parse("{1}"),
					[
						new ModifyUntilEndOfTurn(
							Ref.Target(),
							Count.PowerOf(Ref.Source),
							Count.PowerOf(Ref.Source)
						),
					]
				)
				{
					Costs = [new TapSelfCost()],
					Targets = [Target(Is.Creature & !Is.Source)],
					SorcerySpeed = true,
				},
			],
			["Ravenous Cosmozoan"] =
			[
				new CostChange(
					Count
						.NumberOf(Ref.All(ZoneType.Graveyard, !Is.Creature & !Is.Land, Ref.You))
						.Negated
				)
				{
					OnItself = true,
				},
			],
			["Red Alert!"] =
			[
				new CostChange(-2)
				{
					OnItself = true,
					If = new AtLeast(
						Count.NumberOf(Ref.Permanents(Is.Creature & Is.AttackingYou)),
						1
					),
				},
				Spell([], new ModifyUntilEndOfTurn(Ref.CreaturesYouControl, 2, 0)),
			],
			["Resistance Is Futile"] =
			[
				ModalSpell(
					"Resistance Is Futile",
					1,
					M([Target(Is.Creature & !Is.Artifact)], new Destroy(Ref.Target())),
					M(
						[
							TargetSpec.InYourGraveyard(
								Is.Artifact & Is.Creature & Is.ManaValueAtMost(3)
							),
						],
						new ReturnFromGraveyard(Ref.Target(), ZoneType.Battlefield)
					)
				),
			],
			["Saru, Unthreatened"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[Target(Is.Creature & Is.Yours & !Is.Source)],
					new ModifyUntilEndOfTurn(Ref.Target(), 0, 0, "Hexproof")
				),
			],
			["Sickbay"] =
			[
				When(TriggerEvent.CreatureYouControlEnters, [], new GainLife(Ref.You, 1)),
				When(
					TriggerEvent.BeginningOfCombatOnYourTurn,
					[Target(Is.Creature & Is.Yours)],
					new AddCounters(Ref.Target(), Count.LifeYouGainedThisTurn with { Plus = 1 })
				),
			],
			["Spatial Anomaly"] = Anomaly("{4}{R}", [Creature], new DealDamage(Ref.Target(), 2)),
			["Subspace Anomaly"] = Anomaly("{4}{W}", [], new CreateTokens(Officer, 2)),
			["Syndicate Liquidators"] =
			[
				When(
					TriggerEvent.LandYouControlEnters,
					[Target(Is.Creature & Is.Yours)],
					new AddCounters(Ref.Target(), 2)
				),
			],
			["Targ Wrangler"] =
			[
				When(TriggerEvent.YourEndStep, [], new EachSacrifices(Ref.You, !Is.Land)) with
				{
					If = new Matches(Ref.Source, Is.AttackedThisTurn),
				},
			],
			["Temporal Anomaly"] = Anomaly("{3}{G}", [], new MillAndMayTake(4, PermanentTypes)),
			["Test of Conviction"] =
			[
				ModalSpell(
					"Test of Conviction",
					1,
					M([], new AddCounters(Ref.CreaturesYouControl, 1)),
					M(
						[TargetSpec.Player(Is.Opponents)],
						new DrawCards(Ref.Target(), 1),
						new DrawCards(Ref.You, 2)
					)
				),
			],
			["Test of Courage"] =
			[
				ModalSpell(
					"Test of Courage",
					1,
					M(
						[TargetSpec.InAnyGraveyard()],
						new ExileFromGraveyard(Ref.Target()),
						new GainLife(Ref.You, 2)
					),
					M(
						[Target((Is.Artifact | Is.Enchantment) & Is.ManaValueAtMost(3))],
						new Destroy(Ref.Target())
					)
				),
			],
			["Test of Identity"] =
			[
				ModalSpell(
					"Test of Identity",
					1,
					M(
						[Target(Is.Creature & Is.Subtype("Klingon"))],
						new ModifyUntilEndOfTurn(Ref.Target(), 1, 1, "Double strike")
					),
					// "Discard a card. If you do, draw two cards." — you do whenever your hand isn't empty.
					M(
						[],
						new If(
							new AtLeast(Count.NumberOf(Ref.CardsInYourHand), 1),
							[new Discard(Ref.You, 1), new DrawCards(Ref.You, 2)]
						)
					)
				),
			],
			["Test of Loyalty"] =
			[
				ModalSpell(
					"Test of Loyalty",
					1,
					M(
						[TargetSpec.InAnyGraveyard()],
						new ExileFromGraveyard(Ref.Target()),
						new DrawCards(Ref.You, 1)
					),
					M(
						[TargetSpec.InYourGraveyard(Is.Creature)],
						new ReturnFromGraveyard(Ref.Target(), ZoneType.Hand)
					)
				),
			],
			["The Duras Sisters"] =
			[
				When(TriggerEvent.SelfDies, [], new CreateTokens(KlingonWarrior, 1)),
			],
			["Type-2 Phaser"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[Target(Is.Creature & Is.Opponents)],
					new Tap(Ref.Target()),
					new AddStunCounters(Ref.Target(), 1)
				),
				AttachedGets(3, 0),
				Equip("{3}"),
			],
			["V'Ger, the Intruder"] =
			[
				VGer(TriggerEvent.SelfEnters),
				VGer(TriggerEvent.SelfAttacks),
			],
			// ===== MC-5.3 batch 2: Station (MC-5.2) and the commons and uncommons waiting on it =====
			["Oberth-Class Starship"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[],
					new SearchLibrary(CardType.Land, ZoneType.Hand)
					{
						Supertype = Supertype.Basic,
						Subtype = "Plains",
						Reveal = true,
					}
				),
				.. Station(6, "Flying"),
			],
			["Suurok-Class Science Vessel"] =
			[
				When(TriggerEvent.SelfEnters, [], new DrawCards(Ref.You, 1)),
				.. Station(5, "Flying"),
			],
			["Class-F Shuttlecraft"] =
			[
				.. AnyColorMana(ManaRestriction.None),
				.. Station(6, "Flying", "Vigilance"),
			],
			["Emergency Holographic Crew"] =
			[
				ModalWhen(
					"Emergency Holographic Crew",
					TriggerEvent.SelfEnters,
					1,
					M(
						[TargetSpec.InYourGraveyard(Is.Subtype("Spacecraft"))],
						new ReturnFromGraveyard(Ref.Target(), ZoneType.Hand)
					),
					M([], new Surveil(2))
				),
			],
			["Guidance Failure"] =
			[
				new CostChange(-1)
				{
					OnItself = true,
					If = new Matches(Ref.Target(0), Is.Attacking),
				},
				Spell(
					[Target(Is.Creature | Is.Subtype("Spacecraft"))],
					new May(
						"Put it on top of your library? (No: it goes on the bottom.)",
						new Always(),
						[new PutIntoLibrary(Ref.Target(), OnTop: true)]
					)
					{
						Who = Ref.OwnerOfTarget(),
						Otherwise = [new PutIntoLibrary(Ref.Target(), OnTop: false)],
					}
				),
			],
			["Stray from History"] =
			[
				ModalSpell(
					"Stray from History",
					1,
					M(
						[
							TargetSpec.InYourGraveyard(Is.Creature | Is.Subtype("Spacecraft")),
							TargetSpec.InYourGraveyard(Is.Creature | Is.Subtype("Spacecraft")) with
							{
								Optional = true,
								NotThePrevious = true,
							},
						],
						new ReturnFromGraveyard(Ref.Target(0), ZoneType.Hand),
						new ReturnFromGraveyard(Ref.Target(1), ZoneType.Hand)
					),
					M(
						[TargetSpec.Player(Is.Opponents)],
						new YouChooseTheyDiscard(Ref.Target(), NonlandTypes) { ExileInstead = true }
					)
				),
			],
			["Warp Core"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[],
					new SearchLibrary(CardType.Land, ZoneType.Hand)
					{
						Supertype = Supertype.Basic,
						Reveal = true,
					}
				),
				new ActivatedAbilityComponent(
					ManaCost.Parse("{1}"),
					[new AddChargeCounters(Ref.Target(), 2)]
				)
				{
					Costs = [new TapSelfCost()],
					Targets = [Target(Is.Artifact & Is.Subtype("Spacecraft"))],
					SorcerySpeed = true,
				},
			],
			["Enterprise NX-01"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[Target(Is.Creature & Is.Opponents)],
					new Tap(Ref.Target()),
					new AddStunCounters(Ref.Target(), 1)
				),
				.. Station(6, "Flying", "Vigilance"),
			],
			["Sarcophagus, Ship of the Dead"] =
			[
				new StaticEffectComponent(Is.Creature & Is.Yours & !Is.Source) { Power = 1 },
				// Not its own death: as a creature its "if" fails (it was one), and otherwise it can't die —
				// but the "if" can't see it as it last was, so the look-back (CR 603.10a) would fire it.
				When(
					new Trigger(EventKind.Died) { Subject = Is.Creature & !Is.Source },
					[],
					new AddChargeCounters(Ref.Source, 1)
				) with
				{
					If = new Not(new Matches(Ref.Source, Is.Creature)),
				},
				.. Station(10, "Flying"),
			],
			["U.S.S. Cerritos, Following Up"] =
			[
				When(TriggerEvent.SelfEnters, [], new CreateTokens(Officer, 2)),
				.. Station(6, "Flying"),
			],
			["U.S.S. Excelsior, Cutting Edge"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[Target(Is.Creature & Is.Opponents & Is.Tapped)],
					new Destroy(Ref.Target())
				),
				.. Station(8, "Flying"),
			],
			["U.S.S. Voyager, Lone Wanderer"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[],
					new RevealUntilOntoBattlefield(CardType.Land) { HowMany = 2, Tapped = true }
				),
				.. Station(8, "Flying", "Vigilance"),
			],
			// ===== MC-5.3 batch 3: beam me up, strange new worlds, dilemma, power 8, becomes tapped, look and take, Federation (MC-5.2) =====
			["Boarding Action"] =
			[
				Spell([], new CreateTokens(KlingonWarrior, 1)),
				BeamMeUp("{2}{R}"),
			],
			["Final Frontier"] =
			[
				Spell(
					[],
					new SearchLibrary(CardType.Land, ZoneType.Battlefield)
					{
						Filter = Is.Supertype(Supertype.Basic) | Is.Subtype("Planet"),
						FilterText = "basic land or Planet",
						Tapped = true,
					}
				),
				BeamMeUp("{1}{G}"),
			],
			["Open Communications"] = [Spell([], new DrawCards(Ref.You, 1)), BeamMeUp("{2}{U}")],
			["Pattern Buffer Preservation"] =
			[
				Spell([], new LookAndTake(2) { Must = true, RestToGraveyard = true }),
				BeamMeUp("{1}{B}"),
			],
			["Pure Energy"] =
			[
				Spell(
					[Creature, Creature with { Optional = true, NotThePrevious = true }],
					new AddCounters(Ref.Target(0), 1),
					new AddCounters(Ref.Target(1), 1),
					// One target chosen: both counters on it. Two: one each, decided as it was cast.
					new If(new Not(new TargetChosen(1)), [new AddCounters(Ref.Target(0), 1)])
				),
				BeamMeUp("{2}{W}"),
			],
			["Adversary by Algorithm"] =
			[
				Spell([], new CreateTokens(ArtifactIllusion, 1)),
				BeamMeUp("{3}{G}"),
			],
			["Energize"] =
			[
				Spell([Target(Is.Creature & Is.Opponents)], new ReturnToHand(Ref.Target())),
				BeamMeUp("{3}{U}"),
			],
			["Transporter Malfunction"] =
			[
				Spell([TargetSpec.AnyTarget()], new DealDamage(Ref.Target(), 3)),
				BeamMeUp("{3}{R}{R}{R}"),
			],
			["There's Coffee in that Nebula"] =
			[
				Spell(
					[],
					new LookAndTake(
						5,
						Is.Subtype("Plains")
							| (
								Is.Type(
									CardType.Artifact
										| CardType.Creature
										| CardType.Enchantment
										| CardType.Planeswalker
								)
								& !Is.Land
								& Is.ManaValueAtMost(3)
							)
					)
					{
						Reveal = true,
					}
				),
				BeamMeUp("{2}{W}"),
			],
			["Marooned"] =
			[
				new AuraComponent(Target(Is.Artifact | Is.Creature)),
				When(
					TriggerEvent.SelfEnters,
					[],
					new May(
						"Pay {3} to tap the enchanted permanent?",
						new CanPay(ManaCost.Parse("{3}")),
						[new PayMana(ManaCost.Parse("{3}"), [new Tap(Ref.EnchantedCreature)])]
					)
				),
				new EnchantedDoesntUntap(),
				new EnchantedCantBeBeamedUp(),
			],
			["Suus Mahna Practitioner"] =
			[
				new ActivatedAbilityComponent(ManaCost.Parse("{4}{U}"), [new Tap(Ref.Target())])
				{
					Costs = [new TapSelfCost()],
					Targets = [Creature],
					CostChange = new(DifferentlyNamedLands.Negated),
				},
			],
			["Cloak and Phaser"] =
			[
				new CostChange(DifferentlyNamedLands.Negated) { OnItself = true },
				Spell(
					[],
					new CreateTokens(Romulan, 2),
					new DrawCards(Ref.You, 2),
					new Discard(Ref.You, 1)
				),
			],
			["The Traveler"] =
			[
				new StaticEffectComponent(Is.Source) { Power = DifferentlyNamedLands },
			],
			["Melkotian Adjudicator"] =
			[
				When(TriggerEvent.YouFaceADilemma, [], new AddCounters(Ref.Source, 1)),
			],
			["Obsidian Order Operative"] =
			[
				When(
					TriggerEvent.YouFaceADilemma,
					[],
					new LoseLife(Ref.EachOpponent, 1),
					new GainLife(Ref.You, 1)
				),
			],
			["Seven of Nine"] = [When(TriggerEvent.YouFaceADilemma, [], new DrawCards(Ref.You, 1))],
			["Gabriel Lorca, Mirror Menace"] =
			[
				When(
					TriggerEvent.YouFaceADilemma,
					[],
					new May(
						"Pay {B} to return Gabriel Lorca from your graveyard to your hand?",
						new CanPay(ManaCost.Parse("{B}")),
						[
							new PayMana(
								ManaCost.Parse("{B}"),
								[new ReturnFromGraveyard(Ref.Source, ZoneType.Hand)]
							),
						]
					)
				) with
				{
					FromGraveyard = true,
				},
			],
			["Tell Me What You See"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[TargetSpec.Player(Is.Opponents)],
					new May(
						"Let your opponent draw a card? If not, you lose 4 life and they surveil 1.",
						new Always(),
						[new DrawCards(Ref.You, 1)]
					)
					{
						Who = Ref.Target(0),
						Otherwise = [new LoseLife(Ref.Target(0), 4), new Surveil(1)],
					}
				),
				When(
					TriggerEvent.YouFaceADilemma,
					[],
					new May(
						"Return Tell Me What You See to your hand?",
						new Always(),
						[new ReturnToHand(Ref.Source)]
					)
				),
			],
			["Jett Reno, Problem Solver"] =
			[
				When(
					TriggerEvent.SelfBecomesTapped,
					[],
					new If(new Matches(Ref.Source, Is.PowerAtLeast(4)), [new DrawCards(Ref.You, 1)])
					{
						Otherwise = [new Surveil(1)],
					}
				),
				// Scryfall prints "+1/1 counter": a +1/+1 counter (TrkCoverage.md).
				When(TriggerEvent.YouFaceADilemma, [], new AddCounters(Ref.Source, 1)),
			],
			["Hologram Janeway"] =
			[
				When(
					TriggerEvent.YouAttackWithTotalPower8,
					[Creature],
					new AddCounters(Ref.Target(), 1)
				),
			],
			["Tactical Officer"] =
			[
				When(
					TriggerEvent.YouAttackWithTotalPower8,
					[],
					new AddCounters(Ref.Source, 1),
					new Untap(Ref.Source)
				),
			],
			["Cantankerous Captain"] =
			[
				When(
					TriggerEvent.BeginningOfCombatOnYourTurn,
					[Target(Is.Creature & Is.Yours)],
					new ModifyUntilEndOfTurn(Ref.Target(), 1, 0),
					// Judged as it resolves — "Then … if", not the attack trigger's "while".
					new If(
						new AtLeast(Count.TotalPowerOf(Ref.CreaturesYouControl), 8),
						[new ModifyUntilEndOfTurn(Ref.Target(), 0, 0, "Menace")]
					)
				),
			],
			["Una Chin-Riley, Number One"] =
			[
				When(
					TriggerEvent.YouAttackWithTotalPower8,
					[Target(Is.Creature & Is.Attacking)],
					new ModifyUntilEndOfTurn(Ref.Target(), 2, 0, "First strike")
				),
			],
			["Kirk, Commanding Officer"] =
			[
				When(
					TriggerEvent.YouAttackWithTotalPower8,
					[],
					new ExileTopPlayable { ThisTurn = true }
				),
				new ActivatedAbilityComponent(ManaCost.Parse("{2}"), [new Destroy(Ref.Target())])
				{
					Costs = [new SacrificeSelfCost()],
					Targets = [Target(Is.Artifact | Is.Enchantment)],
				},
			],
			["Worf, Honor-Bound Warrior"] =
			[
				// "Double Worf's power": +X/+0 where X is his power as it resolves.
				When(
					TriggerEvent.YouAttackWithTotalPower8,
					[],
					new ModifyUntilEndOfTurn(Ref.Source, Count.PowerOf(Ref.Source), 0)
				),
				new ActivatedAbilityComponent(ManaCost.None, [new AddCounters(Ref.Source, 1)])
				{
					Costs = [new TapUntappedCost(2, Is.Artifact | Is.Creature)],
					SorcerySpeed = true,
				},
			],
			["Shuttle Ace"] =
			[
				new StaticEffectComponent(Is.Source, "Flying") { If = new DuringYourTurn() },
				When(TriggerEvent.SelfBecomesTapped, [], new GainLife(Ref.You, 2)),
			],
			["Hemmer, Fixing What Is Broken"] =
			[
				new StaticEffectComponent(Is.Subtype("Spacecraft") & Is.Yours, "Hexproof")
				{
					If = new DuringYourTurn(),
				},
				When(
					TriggerEvent.SelfBecomesTapped,
					[],
					new DrawCards(Ref.You, 1),
					new Discard(Ref.You, 1)
				),
			],
			["Rutherford, Upgraded Ensign"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[],
					new LookAndTake(6, Is.Artifact) { Reveal = true }
				),
				When(
					TriggerEvent.YouCastSpell with
					{
						Subject = Is.Subtype("Spacecraft"),
					},
					[Creature],
					new AddCounters(Ref.Target(), 1)
				),
			],
			["Spock, Always Your Friend"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[],
					new LookAndTake(4, !Is.Creature & !Is.Land) { Reveal = true }
				),
			],
			["Hoshi Sato, Exolinguist"] =
			[
				When(TriggerEvent.SelfEnters, [], new LookAndTake(Federation) { Must = true }),
			],
			["Saavik, Stoic Student"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[Target(Is.Creature & Is.Opponents & Is.InCombat)],
					new DealDamage(Ref.Target(), Federation)
				),
			],
			// ===== MC-5.3 batch 4a: counters of any kind, quests, Sagas, kicker, can't block, once each turn =====
			["Dominion Supervisor"] =
			[
				When(TriggerEvent.SelfEnters, [Creature], new AddCounters(Ref.Target(), 1)),
				new StaticEffectComponent(Is.Creature & Is.Yours & Is.WithAnyCounter, "Trample"),
			],
			["The Doctor, EMH"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[Creature],
					new AddCounters(Ref.Target(), 1),
					new AddCounters(Ref.Permanents(Is.Creature & Is.Yours & Is.WithAnyCounter), 1)
				),
			],
			["Federation Field Medic"] =
			[
				ModalWhen(
					"Federation Field Medic",
					TriggerEvent.SelfEnters,
					1,
					M(
						[Creature],
						new AddCounters(Ref.Target(), 1),
						new ModifyUntilEndOfTurn(Ref.Target(), 0, 0, "Vigilance")
					),
					M(
						[],
						new If(
							new AtLeast(
								Count.NumberOf(Ref.Permanents(Is.Yours & Is.WithAnyCounter)),
								1
							),
							[new DrawCards(Ref.You, 1)]
						)
					)
				),
			],
			["Phlox, Unorthodox Doctor"] =
			[
				When(
					new Trigger(EventKind.PlayerDealtDamage)
					{
						CombatDamageBy = Is.Creature & Is.Yours & Is.WithAnyCounter,
					},
					[],
					new DrawCards(Ref.You, 1)
				),
			],
			["Beckett Mariner, Impetuous Ensign"] =
			[
				When(
					TriggerEvent.SelfBecomesTapped,
					[],
					new PutCounters(Ref.Source, CounterKinds.Promotion, 1),
					new If(
						new AtLeast(Count.CountersOfKind(Ref.Source, CounterKinds.Promotion), 3),
						[
							new RemoveCountersOfKind(Ref.Source, CounterKinds.Promotion),
							new DealDamage(Ref.EachOpponent, 2),
						]
					)
				),
			],
			["Anthropological Mission"] =
			[
				When(
					TriggerEvent.YouCastNoncreatureSpell,
					[],
					new PutCounters(Ref.Source, CounterKinds.Quest, 1),
					new DrawCards(Ref.You, 1),
					new If(new Not(Quests(3)), [new Discard(Ref.You, 1)])
				),
			],
			["Conquest Mission"] =
			[
				When(
					new Trigger(EventKind.Entered) { Subject = Is.Artifact & !Is.Token & Is.Yours },
					[],
					new PutCounters(Ref.Source, CounterKinds.Quest, 1),
					new If(Quests(6), [new CreateTokens(Borg, 2)])
					{
						Otherwise = [new CreateTokens(Borg, 1)],
					}
				),
			],
			["Support Mission"] =
			[
				When(
					TriggerEvent.CreatureYouControlEnters,
					[],
					new PutCounters(Ref.Source, CounterKinds.Quest, 1),
					new If(Quests(5), [new AddCounters(Ref.EventCard, 2)])
					{
						Otherwise = [new AddCounters(Ref.EventCard, 1)],
					}
				),
			],
			["Vengeance Mission"] =
			[
				When(
					new Trigger(EventKind.Died) { Subject = Is.Creature },
					[TargetSpec.Player(Is.Opponents)],
					new PutCounters(Ref.Source, CounterKinds.Quest, 1),
					new If(Quests(7), [new LoseLife(Ref.Target(), 2)])
					{
						Otherwise = [new LoseLife(Ref.Target(), 1)],
					}
				),
			],
			["Amok Time"] =
			[
				new SagaComponent(3),
				Chapter(
					1,
					[
						Target(Is.Creature & Is.Yours),
						Target(Is.Creature & Is.Opponents) with
						{
							Optional = true,
						},
					],
					new Fight(Ref.Target(0), Ref.Target(1))
				),
				Chapter(2, [], new GainLife(Ref.You, 3)),
				Chapter(
					3,
					[TargetSpec.InYourGraveyard(Is.Creature)],
					new ReturnFromGraveyard(Ref.Target(), ZoneType.Hand)
				),
			],
			["In the Pale Moonlight"] =
			[
				new SagaComponent(3),
				Chapter(1, [], new EachSacrifices(Ref.EachOpponent, Is.Creature & !Is.Token)),
				Chapter(2, [], CreateClue(1)),
				Chapter(
					3,
					[],
					new MaySacrifice(Is.Artifact | Is.Creature, [new CreateTokens(Romulan, 2)])
				),
			],
			["The Measure of a Man"] =
			[
				new SagaComponent(3),
				Chapter(
					1,
					[Target(!Is.Token & (Is.Artifact | Is.Creature)) with { Optional = true }],
					new Exile(Ref.Target()) { Linked = true }
				),
				Chapter(2, [], CreateClue(1)),
				Chapter(3, [], new ReturnLinkedExile(), new GainLife(Ref.You, 2)),
			],
			["The Naked Time"] =
			[
				new SagaComponent(3),
				Chapter(
					1,
					[],
					new SearchLibrary(CardType.Land, ZoneType.Battlefield)
					{
						Supertype = Supertype.Basic,
						Subtype = "Mountain",
						Tapped = true,
					}
				),
				Chapter(
					2,
					[],
					new WheneverThisTurn(
						When(
							TriggerEvent.CreatureYouControlEnters,
							[],
							new AddCounters(Ref.EventCard, 1),
							new ModifyUntilEndOfTurn(Ref.EventCard, 0, 0, "Haste")
						)
					)
				),
				Chapter(3, [], new EachSacrifices(Ref.You, Is.Land)),
			],
			["Those Old Scientists"] =
			[
				new SagaComponent(3),
				Chapter(1, [], new DrawCards(Ref.You, 2), new Discard(Ref.You, 2)),
				Chapter(
					2,
					[
						TargetSpec.InYourGraveyard(Is.Artifact | Is.Instant | Is.Sorcery) with
						{
							Optional = true,
						},
					],
					new ReturnFromGraveyard(Ref.Target(), ZoneType.Hand)
				),
				Chapter(
					3,
					[
						TargetSpec.InAnyGraveyard() with
						{
							Optional = true,
						},
						TargetSpec.InAnyGraveyard() with
						{
							Optional = true,
							NotThePrevious = true,
						},
					],
					new ShuffleIntoLibrary(Ref.Target(0)) { FromGraveyards = true },
					new ShuffleIntoLibrary(Ref.Target(1)) { FromGraveyards = true }
				),
			],
			["Eject the Warp Core"] =
			[
				new KickerComponent(ManaCost.None)
				{
					Costs = [new SacrificeCost(Is.Artifact | Is.Creature)],
				},
				Spell(
					[Creature],
					new If(new WasKicked(), [new ModifyUntilEndOfTurn(Ref.Target(), -6, -6)])
					{
						Otherwise = [new ModifyUntilEndOfTurn(Ref.Target(), -2, -2)],
					}
				),
			],
			["Highly Illogical"] =
			[
				new KickerComponent(ManaCost.Parse("{1}{U}")),
				Spell(
					[
						TargetSpec.Spell(Is.ManaValueAtMost(2)) with
						{
							IfKicked = TargetSpec.Spell(),
						},
					],
					new Counter(Ref.Target())
				),
			],
			["La'An Noonien-Singh, Security"] =
			[
				When(
					TriggerEvent.YouAttackWithTotalPower8,
					[Creature],
					new ModifyUntilEndOfTurn(Ref.Target(), 0, 0, Combat.CantBlock)
				),
			],
			["Cleveland Booker, Empath"] =
			[
				When(
					TriggerEvent.LandYouControlEnters,
					[],
					new CreateTokens(Worm, 1)
					{
						Abilities = [new StaticEffectComponent(Is.Source, Combat.CantBlock)],
					}
				),
				new ActivatedAbilityComponent(
					ManaCost.Parse("{8}"),
					[new ModifyUntilEndOfTurn(Ref.CreaturesYouControl, 2, 0)]
				),
			],
			["Astute Terraformer"] =
			[
				new ActivatedAbilityComponent(
					ManaCost.Parse("{6}"),
					[new ModifyUntilEndOfTurn(Ref.Source, 2, 2)]
				)
				{
					CostChange = new(DifferentlyNamedLands.Negated),
					OncePerTurn = true,
				},
			],
			["DOT-7 Repair Squad"] =
			[
				new ActivatedAbilityComponent(ManaCost.Parse("{1}{W}"), [new Untap(Ref.Target())])
				{
					Targets = [Creature],
					SorcerySpeed = true,
					OncePerTurn = true,
				},
			],
			["Reckless Impulse"] = [Spell([], new ExileTopPlayable { N = 2 })],
			["Common Goal"] =
			[
				Spell(
					[
						Target(Is.Creature & Is.Yours),
						Target(Is.Creature & Is.Yours) with
						{
							Optional = true,
							NotThePrevious = true,
						},
						Target(Is.Creature & Is.Opponents),
					],
					new DamageFrom(Ref.Target(0), Ref.Target(2), Count.PowerOf(Ref.Target(0))),
					new DamageFrom(Ref.Target(1), Ref.Target(2), Count.PowerOf(Ref.Target(1)))
				),
			],
			["70,000 Light-Years from Home"] =
			[
				Spell(
					[Target(Is.Creature | Is.Subtype("Spacecraft"))],
					// The search first, while "its controller" is still on the battlefield to ask; the
					// exile after it changes nothing the search could see.
					new SearchLibrary(CardType.Land, ZoneType.Hand)
					{
						Supertype = Supertype.Basic,
						Reveal = true,
						Who = Ref.ControllerOfTarget(0),
					},
					new Exile(Ref.Target())
				),
			],
			["L'Rell, Fierce Visionary"] =
			[
				When(
					TriggerEvent.BeginningOfCombatOnYourTurn,
					[],
					new CreateTokens(KlingonWarrior, 1) { SacrificeAtNextEndStep = true }
				),
				new ActivatedAbilityComponent(ManaCost.Parse("{2}"), [new Surveil(1)])
				{
					Costs = [new SacrificeCost((Is.Creature | Is.Artifact) & !Is.Source)],
				},
			],
			["Gray and Adira, Forever Joined"] =
			[
				When(
					TriggerEvent.BeginningOfCombatOnYourTurn,
					[Target(Is.Creature & Is.Yours)],
					new CreateTokenCopy(Ref.Target())
					{
						AddingKeywords = ["Haste"],
						NotLegendary = true,
						SacrificeAtNextEndStep = true,
					}
				) with
				{
					If = new AtLeast(Count.NoncreatureSpellsYouCastThisTurn, 1),
				},
			],
			["Dal R'El, Augment Orphan"] =
			[
				When(
					TriggerEvent.LandYouControlEnters,
					[],
					new AddManaOfAnyOneColor(1) { Colors = [ManaColor.Red, ManaColor.Green] }
				),
				new ActivatedAbilityComponent(ManaCost.Parse("{3}"), [new DrawCards(Ref.You, 1)])
				{
					Costs = [new SacrificeCost(Is.Land)],
				},
			],
			["Temporal Conqueror"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[],
					new Mill(Ref.You, 3),
					new ReturnFromYourGraveyard(Is.Land) { Otherwise = [CreateTreasure(1)] }
				),
			],
			["Andromedan Automatons"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[],
					new SearchLibrary(CardType.None, ZoneType.Hand)
					{
						Filter = Is.Named("Andromedan Automatons"),
						FilterText = "Andromedan Automatons",
						UpTo = Count.NumberOf(
							Ref.All(
								ZoneType.Library,
								Is.Named("Andromedan Automatons"),
								of: Ref.You
							)
						),
						Reveal = true,
					}
				),
			],
			// ===== MC-5.3 batch 4b: delayed return, leaves the graveyard, type change, second resolution, granted trigger, control, counters plus one, block and target restrictions =====
			["Ghost of Illyria"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[Target(!Is.Land & Is.Yours & !Is.Source) with { Optional = true }],
					new Flicker(Ref.Target()) { AtNextEndStep = true }
				),
			],
			["Test of Priorities"] =
			[
				ModalSpell(
					"Test of Priorities",
					1,
					M([], new Surveil(2), new DrawCards(Ref.You, 1)),
					M(
						[Target(Is.Creature & Is.Yours)],
						new Flicker(Ref.Target()) { AtNextEndStep = true }
					)
				),
			],
			["Cold-Blooded Crew"] =
			[
				When(TriggerEvent.SelfEnters, [Creature], new AddCounters(Ref.Target(), 2)),
				new CantBeBlockedBy(Is.PowerAtMost(2)),
				BasicLandcycling,
			],
			["Species 8472"] =
			[
				When(TriggerEvent.SelfEnters, [], new GainLife(Ref.You, DifferentlyNamedLands)),
				new CantBeBlockedBy(Is.Artifact & Is.Creature),
				new CantBeTargetedByAbilitiesFrom(Is.Artifact & Is.Opponents),
			],
			["Blastoneuron"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[TargetSpec.InYourGraveyard(Is.Creature & Is.ManaValueAtMost(3))],
					new ExileFromGraveyard(Ref.Target()),
					new CreateTokenCopy(Ref.Target())
					{
						OfAnExiledCard = true,
						AddingKeywords = ["Haste"],
						SacrificeAtNextEndStep = true,
					}
				),
				When(
					TriggerEvent.OneOrMoreCardsLeaveYourGraveyard,
					[],
					new LoseLife(Ref.EachOpponent, 1),
					new GainLife(Ref.You, 1)
				),
			],
			["Hologram Stalker"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[TargetSpec.InYourGraveyard(Is.Type(PermanentTypes))],
					new ReturnFromGraveyard(Ref.Target(), ZoneType.Hand)
				),
				When(
					TriggerEvent.CardLeavesYourGraveyard,
					[],
					new CreateTokens(ArtifactIllusion, 1)
				) with
				{
					OncePerTurn = true,
				},
			],
			["Vash, Freelance Archaeologist"] =
			[
				When(TriggerEvent.YourUpkeep, [], new Surveil(1)),
				When(TriggerEvent.CardLeavesYourGraveyard, [], new DrawCards(Ref.You, 1)) with
				{
					OncePerTurn = true,
				},
			],
			["I'm a Doctor, Not a . . ."] =
			[
				Spell(
					[Target(Is.Creature & Is.Yours)],
					new AddCounters(Ref.Target(), 1),
					new Untap(Ref.Target()),
					new ModifyUntilEndOfTurn(Ref.Target(), 1, 1)
					{
						BecomesCreatureTypes = ["Doctor"],
					}
				),
			],
			["De-evolve"] =
			[
				new AuraComponent(Creature),
				AttachedGets(-4, 0),
				new StaticEffectComponent(Is.AttachedToSource, Combat.CantBlock),
				new EnchantedIsCreatureType("Salamander"),
				When(TriggerEvent.EachUpkeep, [], new Exile(Ref.EnchantedCreature)) with
				{
					If = Threshold,
				},
			],
			["Captain Michael Burnham"] =
			[
				When(
					TriggerEvent.LandYouControlEnters,
					[],
					new DealDamage(Ref.EachOpponent, 1),
					new IfNthResolutionThisTurn(2, [new AddCounters(Ref.Source, 1)])
				),
			],
			["Jake Sisko, War Correspondent"] =
			[
				When(
					TriggerEvent.LandYouControlEnters,
					[],
					new GainLife(Ref.You, 1),
					new IfNthResolutionThisTurn(2, [CreateClue(1)])
				),
			],
			["Hive Mind Coprocessor"] =
			[
				new GrantsTriggeredAbility(
					ArtifactCreaturesYouControl,
					When(
						TriggerEvent.SelfDealsCombatDamageToPlayer,
						[],
						new May(
							"Pay 1 life to draw a card?",
							new Always(), // paying life is losing it (CR 119.4); at 0 the game is already over
							[new LoseLife(Ref.You, 1), new DrawCards(Ref.You, 1)]
						)
					)
				),
			],
			["Rules of Acquisition"] =
			[
				ModalSpell(
					"Rules of Acquisition",
					1,
					M(
						[Target(!Is.Land & Is.Opponents)],
						new DrawCards(Ref.ControllerOfTarget(0), 3), // "that opponent" — asked before it changes
						new GainControl(Ref.Target(), Ref.You)
					),
					M(
						[TargetSpec.Player(Is.Opponents), Target(!Is.Land & Is.Yours)],
						new GainControl(Ref.Target(1), Ref.Target(0)),
						new If(
							new Matches(Ref.Target(1), Is.Opponents),
							[new DrawCards(Ref.You, 3)]
						) // "if they do"
					)
				),
			],
			["Brad Boimler, Eager Ensign"] =
			[
				When(
					TriggerEvent.SelfBecomesTapped,
					[],
					new ThisTurn(
						new ThisTurnReplacement(
							new NumericReplacement(ReplacedEvent.Counters)
							{
								Affected = Is.Yours,
								Plus = 1,
							}
						)
					)
				),
			],
			["Exploratory Mission"] =
			[
				When(
					TriggerEvent.LandYouControlEnters,
					[],
					new PutCounters(Ref.Source, CounterKinds.Quest, 1),
					new ExileTopPlayable { UntilNonland = true, ThisTurn = true },
					new If(Quests(5), [new AddMana(ManaColor.Red, 2)])
				),
			],
			["Storied Veteran"] =
			[
				new StaticEffectComponent(Is.Source, Combat.CantBlock),
				new MayCastFromYourGraveyard(),
			],
			["Erica Ortegas, Plucky Pilot"] =
			[
				When(
					TriggerEvent.SelfBecomesTapped,
					[],
					new MayPutFromHandOntoBattlefield(
						CardType.Land,
						[new AtNextEndStep([new ReturnYourPermanentToHand(Is.Land)])]
					)
				),
			],
			// ===== MC-5.3 batch 4c: the one-offs — play from exile (face down too), a destroy replacement, draw, can't attack alone, lands enter untapped, Zefram Cochrane =====
			["Ensign Pavel Chekov"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[],
					new ExileTopPlayable
					{
						Of = Ref.EachOpponent,
						N = DifferentlyNamedLands,
						Forever = true,
						Choose = Is.Land,
					}
				),
			],
			["Warp Speed"] =
			[
				new LandsYouControlEnterUntapped(),
				When(
					TriggerEvent.SelfEnters,
					[],
					new SearchLibrary(CardType.Land, ZoneType.Hand)
					{
						Filter = Is.Supertype(Supertype.Basic) | Is.Subtype("Planet"),
						FilterText = "basic land or Planet",
						Reveal = true,
					}
				),
				new ActivatedAbilityComponent(
					ManaCost.Parse("{7}{U}{U}"),
					[new DrawCards(Ref.You, 2)]
				)
				{
					Costs = [new SacrificeSelfCost()],
					CostChange = new(DifferentlyNamedLands.Negated),
				},
			],
			["Pet Sehlat"] = [new StaticEffectComponent(Is.Source, Combat.CantAttackAlone)],
			["Combadge"] =
			[
				When(TriggerEvent.SelfCast, [], new CopySpell(Ref.EventCard)),
				new GrantsTriggeredAbility(
					Is.AttachedToSource,
					When(
						TriggerEvent.SelfAttacks with
						{
							While = new AtLeast(
								Count.NumberOf(
									Ref.Permanents(
										Is.Creature
											& Is.Yours
											& !Is.Source
											& Is.WithAttached(Is.Subtype("Communicator"))
									)
								),
								1
							),
						},
						[],
						new ModifyUntilEndOfTurn(Ref.Source, 0, 0, Combat.CantBeBlocked)
					)
				),
				Equip("{2}"),
			],
			["Data, New Life"] =
			[
				When(TriggerEvent.YouDrawACard, [], new AddCounters(Ref.Source, 1)),
			],
			["Dr. McCoy, Surly Surgeon"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[Target(Is.Creature & Is.Yours & !Is.Source)],
					new AddCounters(Ref.Target(), 1)
				),
				new CountersRemovedInsteadOfDestruction(),
			],
			["Vulcan Mind-Meld"] =
			[
				Spell(
					[TargetSpec.Player(Is.Opponents)],
					new ExileTopPlayable // the opponent looks at YOUR top card and exiles it
					{
						By = Ref.Target(0),
						Forever = true,
						AnyType = true,
						FaceDown = true,
					},
					new ExileTopPlayable // you look at their top two
					{
						Of = Ref.Target(0),
						N = 2,
						Forever = true,
						AnyType = true,
						FaceDown = true,
					}
				),
			],
			["Zero, Incorporeal Sensate"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[TargetSpec.Player(Is.Opponents)],
					new YouChooseTheyDiscard(Ref.Target(), NonlandTypes)
					{
						Optional = true,
						OwnerMayPlayTaxed = 2,
					}
				),
			],
			["Dominion Saboteur"] =
			[
				new MayEnterAsCopy
				{
					Of = Is.Artifact | Is.Creature,
					NotLegendary = true,
					WithCounters = true,
					Optional = true,
				},
			],
			["Sylvia Tilly, Joy of Discovery"] =
			[
				When(TriggerEvent.SelfEnters, [], new Surveil(2)),
				When(
					TriggerEvent.BeginningOfCombatOnYourTurn,
					[],
					new May(
						"Pay {1} and exile a card from your graveyard?",
						new Both(
							new CanPay(ManaCost.Parse("{1}")),
							new AtLeast(Count.NumberOf(Ref.CardsInYourGraveyard), 1)
						),
						[
							new PayMana(
								ManaCost.Parse("{1}"),
								[
									new ReturnFromYourGraveyard(null) { To = ZoneType.Exile },
									new WhenYouDo([Creature], [new AddCounters(Ref.Target(), 1)]),
								]
							),
						]
					)
				),
			],
			["First Contact"] =
			[
				Spell(
					[],
					new MillAndMayTake(4, PermanentTypes),
					new If(new FirstSpellYouCastThisGame(), [new GainLife(Ref.You, 2)])
				),
			],
			["A Good Day to Die"] =
			[
				Spell(
					[Creature],
					new ModifyUntilEndOfTurn(Ref.Target(), 2, 0),
					new WheneverThisTurn(
						new TriggeredAbilityComponent(
							new Trigger(EventKind.Died),
							[new DrawCards(Ref.You, 1)]
						)
					)
					{
						About = Ref.Target(0),
					}
				),
			],
			["Isolinear Processor"] =
			[
				new ManaAbilityComponent(ManaColor.Colorless)
				{
					Restriction = ManaRestriction.OnlyArtifacts,
				},
				new ActivatedAbilityComponent(
					ManaCost.Parse("{2}"),
					[new DrawCards(Ref.You, 1), new Discard(Ref.You, 1)]
				)
				{
					Costs = [new TapSelfCost()],
				},
			],
			["Zefram Cochrane, Warp Pioneer"] =
			[
				new MayLookAtTopOfLibrary(),
				new MayCastArtifactFromLibraryTop(),
				new YourPermanentsEnterWithCounters(
					Is.Subtype("Spacecraft"),
					CounterKinds.Charge,
					2
				),
			],
			// ===== MC-5.3 batch 5a: the tier 1–2 rares and mythics — enters untapped, no maximum hand size, any token an Illusion =====
			["Armus, Skin of Evil"] =
			[
				When(
					TriggerEvent.YourEndStep,
					[TargetSpec.Player(Is.Opponents)],
					new Discard(Ref.Target(), 2)
				),
				new ActivatedAbilityComponent(
					ManaCost.Parse("{2}{B}"),
					[new ModifyUntilEndOfTurn(Ref.Target(), -4, -4)]
				)
				{
					Costs = [new DiscardSelfCost()],
					From = ZoneType.Hand,
					Targets = [Creature],
				},
			],
			["Captain James T. Kirk"] =
			[
				CaptainKirk(TriggerEvent.SelfEnters),
				CaptainKirk(TriggerEvent.SelfAttacks),
			],
			["Captain Jonathan Archer"] =
			[
				When(TriggerEvent.SelfEnters, [], new CreateTokens(Porthos, 1)),
				new ActivatedAbilityComponent(
					ManaCost.Parse("{G}"),
					[new ModifyUntilEndOfTurn(Ref.Target(), Federation, Federation)]
				)
				{
					Costs = [new TapSelfCost()],
					Targets = [Target(Is.Creature & Is.Yours)],
					SorcerySpeed = true,
				},
			],
			["Crystalline Entity"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[],
					new Destroy(Ref.Permanents(Is.Creature & !Is.Artifact))
				) with
				{
					If = new SourceWasCast(),
				},
			],
			["D-7 Battle Cruiser"] =
			[
				.. Station(8, "Flying"),
				When(
					TriggerEvent.BeginningOfCombatOnYourTurn,
					[],
					new CreateTokens(KlingonWarrior, 1) { SacrificeAtNextEndStep = true }
				) with
				{
					If = new AtLeast(Count.ChargeCountersOn(Ref.Source), 3),
				},
			],
			["Deep Space Scan"] =
			[
				Spell(
					[],
					new DrawCards(Ref.You, DifferentlyNamedLands),
					new NoMaximumHandSizeUntilYourNextTurn()
				),
			],
			["Elim Garak, Simple Tailor"] =
			[
				When(
					TriggerEvent.YouFaceADilemma,
					[],
					new LoseLife(Ref.EachOpponent, 2),
					new GainLife(Ref.You, 2)
				),
			],
			["Encounter the Unknown"] =
			[
				ModalSpell(
					"Encounter the Unknown",
					1,
					M([], new ExileTopPlayable { N = 2 }),
					M([], new DealDamage(Ref.AllCreatures, 2), new DealDamage(Ref.EachPlayer, 2))
				),
			],
			["Excalbian Gamemaster"] =
			[
				When(TriggerEvent.SelfEnters, [], new CreateTokens(Officer, DifferentlyNamedLands)),
				When(
					TriggerEvent.CreatureYouControlDies,
					[Target(Is.Creature & Is.Yours)],
					new AddCounters(Ref.Target(), 1)
				),
			],
			["Federation Outpost"] = EntersUntapped(
				"Federation Outpost",
				ManaColor.White,
				M([], new GainLife(Ref.You, 2))
			),
			["Federation Resupply"] =
			[
				Spell(
					[],
					new SearchLibrary(CardType.None, ZoneType.Hand)
					{
						Filter =
							Is.Subtype("Aura") | Is.Subtype("Equipment") | Is.Subtype("Spacecraft"),
						FilterText = "Aura, Equipment, or Spacecraft",
						Reveal = true,
					}
				),
				BeamMeUp("{3}{W}"),
			],
			["General Chang, Cold Warrior"] =
			[
				new ActivatedAbilityComponent(
					ManaCost.Parse("{1}"),
					[new AddCounters(Ref.Permanents(Is.Subtype("Klingon") & Is.Yours), 1)]
				)
				{
					Costs = [new SacrificeCost(Is.Creature)],
				},
			],
			["Gowron, Eager for Battle"] =
			[
				When(
					new Trigger(EventKind.Died) { Subject = Is.Creature & !Is.Source },
					[],
					new AddCounters(Ref.Source, 1)
				),
				new ActivatedAbilityComponent(
					ManaCost.None,
					[
						new ModifyUntilEndOfTurn(Ref.Source, 0, 0, "Trample"),
						new If(
							new Matches(Ref.EventCard, Is.Subtype("Klingon"))
							{
								AsItLastWas = true,
							},
							[new DrawCards(Ref.You, 1), new LoseLife(Ref.You, 1)]
						),
					]
				)
				{
					Costs = [new SacrificeCost(Is.Creature & !Is.Source)],
				},
			],
			["Grand Nagus Zek"] =
			[
				new StaticEffectComponent(Is.Source)
				{
					Power = Count.NumberOf(Ref.Permanents(Is.Subtype("Treasure") & Is.Yours)),
					Toughness = Count.NumberOf(Ref.Permanents(Is.Subtype("Treasure") & Is.Yours)),
				},
				When(
					new Trigger(EventKind.Died) { Subject = Is.Creature & !Is.Source },
					[],
					CreateTreasure(1)
				) with
				{
					OncePerTurn = true,
				},
			],
			["Hit It!"] =
			[
				new StaticEffectComponent(Is.Creature & Is.Yours, "Haste"),
				When(
					TriggerEvent.SelfEnters,
					[],
					new LookAndTake(5, Is.Land)
					{
						Reveal = true,
						Must = true,
						RestToGraveyard = true,
						OntoBattlefield = true,
						Tapped = true,
					}
				),
			],
			["Holo-Engineer"] =
			[
				When(TriggerEvent.SelfEnters, [], new CreateTokens(ArtifactIllusion, 1)),
				new ArtifactTokensBecome(ArtifactIllusion, []) { AnyToken = true },
			],
			["Jadzia Dax, Living Legacy"] =
			[
				When(
					TriggerEvent.YourFirstMainPhase,
					[],
					new AddCounters(Ref.Source, 1),
					new AddMana(ManaColor.Green, Count.AllCountersOn(Ref.Source))
				),
				new ActivatedAbilityComponent(
					ManaCost.Parse("{2}{G}"),
					[new AddCounters(Ref.Target(), 2)]
				)
				{
					Costs = [new DiscardSelfCost()],
					From = ZoneType.Hand,
					Targets = [Creature],
				},
			],
			["Khan, Vengeful Heart"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[],
					new MaySacrifice(
						(Is.Creature | Is.Artifact) & !Is.Source,
						[new DrawCards(Ref.You, 2)]
					)
				),
				new MayCastFromExile(),
			],
			["Live Long and Prosper"] =
			[
				new CantBeCountered(),
				Spell(
					[],
					new GainLife(Ref.You, Count.LifeTotalOf(Ref.You)),
					new DrawCards(Ref.You, Count.NumberOf(Ref.CardsInYourHand)),
					new NoMaximumHandSizeUntilYourNextTurn()
				),
			],
			["MACO Strike Team"] =
			[
				When(
					new Trigger(EventKind.BecameTarget)
					{
						Subject = Is.Creature & Is.Yours,
						By = Whose.Opponent,
					},
					[],
					new CreateTokens(Officer, 1) { Tapped = true }
				),
			],
			["Ni'Var Science Academy"] = EntersUntapped(
				"Ni'Var Science Academy",
				ManaColor.Blue,
				M([Creature], new ModifyUntilEndOfTurn(Ref.Target(), 0, 0, "Flying"))
			),
			["Pah-Wraith"] =
			[
				When(
					TriggerEvent.SelfAttacks,
					[],
					new ModifyUntilEndOfTurn(Ref.CreaturesYouControl, DifferentlyNamedLands, 0)
				),
			],
			["Primary Unimatrix Adjunct"] =
			[
				new StaticEffectComponent(ArtifactCreaturesYouControl, "Double strike"),
			],
			["Q Continuum"] = EntersUntapped(
				"Q Continuum",
				ManaColor.Green,
				M([Creature], new ModifyUntilEndOfTurn(Ref.Target(), 1, 1, "Vigilance"))
			),
			["Quark's Bar"] = EntersUntapped(
				"Quark's Bar",
				ManaColor.Red,
				M([], new DealDamage(Ref.EachOpponent, 1))
			),
			["Replicator"] =
			[
				When(TriggerEvent.SelfEnters, [], CreateFood(1)),
				new ActivatedAbilityComponent(
					ManaCost.Parse("{2}"),
					[new CreateTokenCopy(Ref.Target()) { Tapped = true }]
				)
				{
					Costs = [new TapSelfCost()],
					Targets = [Target(Is.Token & Is.Yours)],
				},
			],
			["Romulan Bird-of-Prey"] =
			[
				new StaticEffectComponent(Is.Source, "Hexproof")
				{
					If = new Not(new Matches(Ref.Source, Is.Creature)),
				},
				When(
					TriggerEvent.YouCastNoncreatureSpell,
					[],
					new AddChargeCounters(Ref.Source, 2)
				) with
				{
					If = new Not(new Matches(Ref.Source, Is.Creature)),
				},
				.. Station(8, "Flying"),
			],
			["Salt Vampire"] =
			[
				// "look at" is a reveal here: the only other player is the hand's owner.
				When(
					TriggerEvent.SelfEnters,
					[TargetSpec.Player(Is.Opponents)],
					new YouChooseTheyDiscard(Ref.Target(), NonlandTypes)
					{
						Optional = true,
						ExileInstead = true,
						Then = [new DrawCards(Ref.Target(), 1)],
					}
				),
			],
			["Sarek, Federation Ambassador"] =
			[
				When(
					TriggerEvent.BeginningOfCombatOnYourTurn,
					[],
					new DrawCards(Ref.You, Federation with { Per = 5 }),
					// the types again, not the cards drawn: drawing changes no creature's types
					new If(
						new AtLeast(Federation with { Per = 5 }, 3),
						[new AddCounters(Ref.CreaturesYouControl, 2)]
					)
				),
			],
			["Stable Wormhole"] = [When(TriggerEvent.YouAttack, [], CreateNewPlanet)],
			["Subcommander T'Pol"] =
			[
				ModalWhen(
					"Subcommander T'Pol",
					TriggerEvent.SelfEnters,
					1,
					M([TargetSpec.Ability()], new Counter(Ref.Target())),
					M(
						[Target(Is.Artifact | Is.Creature) with { Optional = true }],
						new Tap(Ref.Target()),
						new AddStunCounters(Ref.Target(), 1)
					)
				),
			],
			["Sulu, Space Swashbuckler"] =
			[
				When(TriggerEvent.SelfBecomesTapped, [], new DrawCards(Ref.You, 1)) with
				{
					If = new SubtypeEnteredThisTurn(["Equipment", "Spacecraft"]),
				},
			],
			["Tasha Yar, Out of Time"] =
			[
				When(TriggerEvent.SelfDies, [], new ExileTopPlayable()),
				new ActivatedAbilityComponent(
					ManaCost.Parse("{2}{R}"),
					[new CreateTokenCopy(Ref.Source) { SacrificeAtNextEndStep = true }]
				)
				{
					Costs = [new ExileSelfCost()],
					From = ZoneType.Graveyard,
					SorcerySpeed = true,
				},
			],
			["The Destruction of Praxis"] =
			[
				Spell(
					[],
					new ModifyUntilEndOfTurn(
						Ref.AllCreatures,
						DifferentlyNamedLands.Negated,
						DifferentlyNamedLands.Negated
					),
					new If(
						new AtLeast(DifferentlyNamedLands, 6),
						[new CreateTokens(KlingonWarrior, 2)]
					)
				),
			],
			["The Unicomplex"] = EntersUntapped(
				"The Unicomplex",
				ManaColor.Black,
				M(
					[TargetSpec.InYourGraveyard(Is.Creature)],
					new PutIntoLibrary(Ref.Target(), OnTop: true)
				)
			),
			["Troubling Tribbles"] =
			[
				new StaticEffectComponent(Is.Source, Combat.CantBlock),
				When(TriggerEvent.YourUpkeep, [], new CreateTokenCopy(Ref.Source)),
			],
			["U.S.S. Enterprise-A, Free Spirit"] =
			[
				Ward("{2}"),
				When(TriggerEvent.SelfEnters, [], new CreateTokens(Officer, 3)),
				.. Station(6, "Flying", "Lifelink"),
			],
			["U.S.S. Reliant, From Hell's Heart"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[TargetSpec.InYourGraveyard(Is.Creature & Is.ManaValueAtMost(4))],
					new ReturnFromGraveyard(Ref.Target(), ZoneType.Battlefield) { Finality = true }
				),
				When(
					TriggerEvent.SelfDealsCombatDamageToPlayer,
					[TargetSpec.InYourGraveyard(Is.Creature & Is.ManaValueAtMost(4))],
					new ReturnFromGraveyard(Ref.Target(), ZoneType.Battlefield) { Finality = true }
				),
				.. Station(10, "Flying"),
			],
			["Wesley Crusher, Starfleet Cadet"] =
			[
				ModalWhen(
					"Wesley Crusher, Starfleet Cadet",
					TriggerEvent.SelfEnters,
					2,
					M(
						[TargetSpec.Spell(!Is.Creature)],
						new CounterUnlessPays(Ref.Target(), ManaCost.Parse("{2}"))
					),
					M(
						[Target(Is.Creature & Is.ManaValueAtMost(2))],
						new ReturnToHand(Ref.Target())
					),
					M(
						[Creature],
						new ModifyUntilEndOfTurn(Ref.Target(), 0, 0, Combat.CantBeBlocked)
					),
					M(
						[TargetSpec.Player()],
						new DrawCards(Ref.Target(), 2),
						new Discard(Ref.Target(), 2)
					)
				),
			],
			["Year of Hell"] =
			[
				new SagaComponent(3),
				Chapter(1, [], new DrawCards(Ref.EachPlayer, 1), new DealDamage(Ref.EachPlayer, 2)),
				Chapter(2, [], new DrawCards(Ref.EachPlayer, 1), new DealDamage(Ref.EachPlayer, 2)),
				Chapter(3, [], new DrawCards(Ref.EachPlayer, 1), new DealDamage(Ref.EachPlayer, 2)),
			],
			// ===== MC-5.3 batch 5b: a delayed return on a dead card, charge counters doubled, a mana ability that costs mana, tap three or seven, life lost this turn, base P/T for good, reveal the top X =====
			["Dr. Leonard \"Bones\" McCoy"] =
			[
				ModalWhen(
					"Dr. Leonard \"Bones\" McCoy",
					TriggerEvent.SelfEnters,
					1,
					M(
						[
							TargetSpec.InYourGraveyard(
								Is.Type(PermanentTypes) & !Is.Subtype("Doctor")
							),
						],
						new ReturnFromGraveyard(Ref.Target(), ZoneType.Hand)
					),
					M(
						[Target(Is.Creature & !Is.Source) with { Optional = true }],
						new WheneverThisTurn(
							new TriggeredAbilityComponent(
								new Trigger(EventKind.Died),
								[
									new AtNextEndStep(
										[
											new ReturnFromGraveyard(
												Ref.Source,
												ZoneType.Battlefield
											)
											{
												Counters = 1,
											},
										]
									)
									{
										On = Ref.EventCard,
									},
								]
							)
						)
						{
							About = Ref.Target(0),
						}
					)
				),
			],
			["Lt. Cmdr. Geordi La Forge"] =
			[
				new NumericReplacement(ReplacedEvent.Counters)
				{
					Affected = Is.Artifact & Is.Yours,
					CounterKind = CounterKinds.Charge,
					Times = 2,
				},
			],
			["Starfleet Academy"] =
			[
				new ManaAbilityComponent(ManaColor.Colorless),
				ManaAbilityComponent.OfAnyOneColor(Federation) with
				{
					Cost = ManaCost.Parse("{5}"),
				},
			],
			["Collation Trimatrix"] =
			[
				new ActivatedAbilityComponent(
					ManaCost.None,
					[new DrawCards(Ref.You, 1), new LoseLife(Ref.You, 1)]
				)
				{
					Costs = [new TapUntappedCost(3, Is.Artifact & Is.Creature)],
				},
			],
			["Captain Jean-Luc Picard"] =
			[
				new StaticEffectComponent(Is.Creature & Is.Yours & !Is.Source)
				{
					Power = 1,
					Toughness = 1,
				},
				When(
					TriggerEvent.YouCastSpell with
					{
						Subject = Is.Creature | Is.Subtype("Spacecraft"),
					},
					[],
					new CreateTokens(Officer, 1)
				),
				new ActivatedAbilityComponent(
					ManaCost.None,
					[
						new SearchLibrary(CardType.Artifact, ZoneType.Battlefield)
						{
							Subtype = "Spacecraft",
							ChargeCounters = 7,
						},
					]
				)
				{
					Costs = [new TapUntappedCost(7, Is.Subtype("Officer"))],
				},
			],
			["Gul Dukat, Colonial Prefect"] =
			[
				// "each opponent loses life equal to the life that player lost this turn. You gain life
				// equal to the life lost this way" — the gain first, so both read the count before the loss.
				When(
					TriggerEvent.EachEndStep,
					[],
					new GainLife(Ref.You, Count.LifeLostThisTurnOf(Ref.EachOpponent)),
					new LoseLife(Ref.EachOpponent, Count.LifeLostThisTurnOf(Ref.EachOpponent))
				),
			],
			["Seven of Nine, Unassimilated"] =
			[
				When(
					TriggerEvent.YourFirstMainPhase,
					[],
					new DrawCards(Ref.You, 1),
					new If(
						new Not(new AtLeast(DifferentlyNamedLands, 5)),
						[new Discard(Ref.You, 1)]
					)
				),
				new ActivatedAbilityComponent(
					ManaCost.Parse("{6}{U}"),
					[
						new ModifyUntilEndOfTurn(Ref.Source, 0, 0)
						{
							SetBase = (7, 9),
							ForGood = true,
						},
					]
				)
				{
					OnlyIf = new AtLeast(DifferentlyNamedLands, 7),
				},
			],
			["Branching Timeline"] =
			[
				Spell(
					[],
					new RevealUntilOntoBattlefield(PermanentTypes)
					{
						Top = DifferentlyNamedLands,
						Tapped = true,
					}
				),
			],
			// ===== MC-5.3 batch 6a: the shocklands, sunburst, X targets and changeling, a number chosen as it enters =====
			["Blood Crypt"] = [Shockland],
			["Breeding Pool"] = [Shockland],
			["Godless Shrine"] = [Shockland],
			["Hallowed Fountain"] = [Shockland],
			["Overgrown Tomb"] = [Shockland],
			["Sacred Foundry"] = [Shockland],
			["Steam Vents"] = [Shockland],
			["Stomping Ground"] = [Shockland],
			["Temple Garden"] = [Shockland],
			["Watery Grave"] = [Shockland],
			["Engineered Explosives"] =
			[
				new Sunburst(),
				new ActivatedAbilityComponent(
					ManaCost.Parse("{2}"),
					[
						new Destroy(
							Ref.Permanents(!Is.Land) with
							{
								ManaValueIs = Count.ChargeCountersOn(Ref.Source),
							}
						),
					]
				)
				{
					Costs = [new SacrificeSelfCost()],
				},
			],
			["Dominion Infiltration"] =
			[
				Spell(
					[Target(Is.Creature & Is.Opponents) with { PerX = true }],
					new CreateTokenCopy(Ref.EachTarget)
					{
						AddingKeywords = ["Changeling", "Haste"],
						ExileAtNextEndStep = true,
					}
				),
			],
			["Nyota Uhura, Communications"] =
			[
				new ChoosesNumberAsItEnters(10),
				When(
					new Trigger(EventKind.SpellCast)
					{
						By = Whose.Opponent,
						Subject = Is.ManaValueIsChosenNumber,
					},
					[],
					new DrawCards(Ref.You, 1)
				),
			],
			["Mirror Universe Intrusion"] =
			[
				new KickerComponent(ManaCost.Parse("{4}{U}{B}")),
				Spell(
					[
						Target(Is.Creature & Is.Opponents) with
						{
							IfKicked = TargetSpec.Player(Is.Opponents),
						},
					],
					new If(
						new WasKicked(),
						[
							new CreateTokenCopy(
								Ref.All(ZoneType.Battlefield, Is.Creature, Ref.Target(0))
							)
							{
								AddingKeywords = ["Menace"],
							},
						]
					)
					{
						Otherwise =
						[
							new CreateTokenCopy(Ref.Target(0)) { AddingKeywords = ["Menace"] },
						],
					}
				),
			],
			// ===== MC-5.3 batch 6b: the re-tier's cheap ten, then Spock, Georgiou, Miles — lands from the library's top, counters doubled, charge counters put, draws doubled, no loss at 0 life, a permanent returned to hand =====
			["Sela, Tal Shiar Paradox"] =
			[
				When(
					TriggerEvent.SelfAttacks,
					[TargetSpec.InAnyGraveyard() with { Optional = true }],
					new ExileFromGraveyard(Ref.Target())
				),
				When(
					TriggerEvent.OneOrMoreCardsLeaveYourGraveyard,
					[],
					new CreateTokens(Romulan, 1) { Tapped = true }
				),
			],
			["Antagonist Incorporator"] =
			[
				new GrantsTriggeredAbility(
					ArtifactCreaturesYouControl,
					When(
						TriggerEvent.SelfDealsCombatDamageToPlayer,
						[],
						new May(
							"Pay {1} to create a Borg token?",
							new CanPay(ManaCost.Parse("{1}")),
							[new PayMana(ManaCost.Parse("{1}"), [new CreateTokens(Borg, 1)])]
						)
					)
				),
			],
			["Deanna Troi, Psychic Counsel"] =
			[
				new YourPermanentsEnterWithCounters(Is.Creature, CounterKinds.PlusOne, 1), // its own entry is skipped: "other"
			],
			["Will Riker, First in the Field"] =
			[
				When(
					TriggerEvent.BeginningOfCombatOnYourTurn,
					[],
					new RevealUntilOntoBattlefield(CardType.Land)
					{
						HowMany = 2,
						SacrificeAtNextEndStep = true,
					}
				),
			],
			["Captain Pike, Fullness of Time"] =
			[
				new StaticEffectComponent(Is.Source)
				{
					Power = DifferentlyNamedLands,
					Toughness = DifferentlyNamedLands,
				},
				new MayLookAtTopOfLibrary(),
				new MayPlayLandsFromLibraryTop(),
			],
			["Dr. Joseph M'Benga"] =
			[
				new StaticEffectComponent(
					Is.Creature & Is.Yours & !Is.Source & Is.WithAnyCounter,
					"Hexproof"
				),
				new NumericReplacement(ReplacedEvent.Counters)
				{
					Affected = Is.Creature & Is.Yours,
					Times = 2,
				},
			],
			["Dathon, His Heart Open"] =
			[
				When(
					TriggerEvent.YouCastSpell with
					{
						Subject = Is.Enchantment,
					},
					[],
					new DrawCards(Ref.You, 1),
					new If(
						new Matches(Ref.EventCard, Is.Subtype("Saga")),
						[new DealDamage(Ref.EachOpponent, 2)]
					)
				),
				new ActivatedAbilityComponent(
					ManaCost.Parse("{2}"),
					[new AddAnotherCounterOfAKind(Ref.Target())]
				)
				{
					Targets = [Target(Is.Enchantment & Is.Yours)],
					SorcerySpeed = true,
				},
			],
			["The Inner Light"] =
			[
				new SagaComponent(3),
				Chapter(
					1,
					[
						TargetSpec.InYourGraveyard(
							Is.Creature & (Is.Color(ManaColor.Green) | Is.Color(ManaColor.White))
						),
					],
					new ReturnFromGraveyard(Ref.Target(), ZoneType.Battlefield)
				),
				Chapter(
					2,
					[Creature],
					new WheneverThisTurn(
						new TriggeredAbilityComponent(
							new Trigger(EventKind.PlayerDealtDamage)
							{
								CombatDamageBy = Is.Creature,
							},
							[new DrawCards(Ref.You, Count.X)] // "that many": the damage dealt
						)
					)
					{
						About = Ref.Target(0),
					}
				),
				Chapter(
					3,
					[],
					new GainLife(Ref.You, Count.GreatestPowerOf(Ref.CreaturesYouControl)),
					CreateTreasure(1)
				),
			],
			["Deep Space 9"] =
			[
				When(TriggerEvent.SelfEnters, [], DeepSpace9Exile),
				When(TriggerEvent.YourUpkeep, [], DeepSpace9Exile),
				StationAbility,
				new StaticEffectComponent(Is.Creature & Is.Yours)
				{
					Power = 3,
					Toughness = 3,
					If = new AtLeast(Count.ChargeCountersOn(Ref.Source), 9),
				},
			],
			["U.S.S. Enterprise-D, Galaxy-Class"] =
			[
				When(
					new Trigger(EventKind.CountersPut)
					{
						Subject = Is.Source,
						Counters = CounterKinds.Charge,
					},
					[],
					new ExileTopPlayable { ThisTurn = true }
				) with
				{
					OncePerTurn = true, // "for the first time each turn": the trigger is only about itself
				},
				.. Station(7, "Flying", "Vigilance"),
			],
			["Mr. Spock, Science Officer"] =
			[
				new NumericReplacement(ReplacedEvent.Draw) { Affected = Is.Yours, Times = 2 },
			],
			["Georgiou, Terran Emperor"] =
			[
				new DontLoseForZeroLife(),
				When(
					TriggerEvent.YourEndStep,
					[],
					new LoseLife(Ref.You, 3),
					new DrawCards(Ref.You, 1),
					new If(
						new Not(new AtLeast(Count.LifeTotalOf(Ref.You), 1)),
						[new CreateTokens(Officer, 10)]
					)
				),
			],
			["Miles O'Brien, Transporter Ops"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[
						Target((Is.Creature | Is.Artifact) & Is.Yours & !Is.Source) with
						{
							Optional = true,
						},
					],
					new ReturnToHand(Ref.Target())
				),
				When(TriggerEvent.EachEndStep, [], new DrawCards(Ref.You, 1)) with
				{
					If = new AtLeast(Count.PermanentsReturnedToHandThisTurn(Ref.You), 1),
				},
			],
			// ===== MC-5.3 batch 7a: the grouped tier 3 — trigger twice, assimilate, control until end of turn, dilemmas this turn, casting from odd zones =====
			["Worf, Intelligence Contractor"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[
						Target(Is.Creature & Is.Opponents & Is.ManaValueAtMost(3)) with
						{
							Optional = true,
						},
					],
					new GainControl(Ref.Target(), Ref.You) { UntilEndOfTurn = true },
					new Untap(Ref.Target()),
					new ModifyUntilEndOfTurn(Ref.Target(), 0, 0, "Haste")
					{
						AlsoCreatureType = "Warrior",
					}
				),
			],
			["The Wrath of Khan"] =
			[
				new SagaComponent(3),
				Chapter(
					1,
					[Target(Is.Creature & Is.Opponents)],
					new DealDamage(Ref.Target(), 4),
					new AtBeginningOfNextCombat(
						Ref.Target(),
						[
							new GainControl(Ref.Source, Ref.You) { UntilEndOfTurn = true },
							new Untap(Ref.Source),
							new ModifyUntilEndOfTurn(Ref.Source, 0, 0, "Haste"),
						]
					)
				),
				Chapter(2, [], new MayDiscard([new DrawCards(Ref.You, 1)]) { Exile = true }),
				Chapter(3, [], new EachSacrifices(Ref.EachOpponent, !Is.Token)),
			],
			["Dr. Beverly Crusher"] =
			[
				new TriggersAdditionalTime(TriggerEvent.YouGainLife, Is.Yours),
				When(TriggerEvent.YouGainLife, [], new AddCounters(Ref.Source, 1)),
			],
			["Lwaxana Troi, Charming Guest"] =
			[
				new MayChooseNotToUntap(),
				new ActivatedAbilityComponent(
					ManaCost.Parse("{2}"),
					[new TriggersAdditionalTimeWhileSourceTapped(Ref.Target())]
				)
				{
					Costs = [new TapSelfCost()],
					Targets = [Target(Is.Creature & Is.Yours & !Is.Source)],
				},
			],
			["Death Is Irrelevant"] =
			[
				new StaticEffectComponent(Is.Artifact & Is.Creature & Is.Yours, "Indestructible"),
				When(
					TriggerEvent.YourEndStep,
					[TargetSpec.InAnyGraveyard(Is.Creature)],
					new Assimilate(Ref.Target())
				),
			],
			["Borg Cube"] =
			[
				// Affinity for artifact creatures
				new CostChange(
					Count.NumberOf(Ref.Permanents(Is.Artifact & Is.Creature & Is.Yours)).Negated
				)
				{
					OnItself = true,
				},
				BorgCubeAssimilates(TriggerEvent.SelfEnters),
				BorgCubeAssimilates(TriggerEvent.SelfAttacks),
				.. Station(12),
				new StaticEffectComponent(Is.Artifact & Is.Creature & Is.Yours, "Flying")
				{
					If = new AtLeast(Count.ChargeCountersOn(Ref.Source), 12),
				},
			],
			["U.S.S. Discovery, at Black Alert"] =
			[
				When(TriggerEvent.SelfEnters, [], Discover(5)),
				When(TriggerEvent.SelfAttacks, [], Discover(5)),
				.. Station(6, "Flying"),
			],
			["Captain Benjamin Sisko"] =
			[
				When(
					TriggerEvent.YourEndStep,
					[],
					new CreateTokens(Officer, Count.DilemmasFacedThisTurnBy(Ref.You))
				),
				new ActivatedAbilityComponent(
					ManaCost.Parse("{2}{W}{U}{B}{R}{G}"),
					[
						new ExileTopAndCastFree(
							5,
							Count.NumberOf(Ref.Permanents(Is.Subtype("Officer") & Is.Yours))
						),
					]
				)
				{
					Costs = [new TapSelfCost()],
				},
			],
			["The Caretaker"] =
			[
				Ward("{2}"),
				When(
					TriggerEvent.EachEndStep,
					[TargetSpec.Player(Is.Opponents)],
					new ExileUntilNonlandCastFree(Ref.Target())
				) with
				{
					If = new AtLeast(Count.DilemmasFacedThisTurnBy(Ref.You), 1),
				},
			],
			["Scotty, Miracle Worker"] = [new GraveyardSpellsHaveBeamMeUp()],
			["Overflow Submatrix"] =
			[
				new MayPlayFromYourGraveyard(Is.Artifact & Is.Creature) { Finality = true },
			],
			["The Holodeck"] =
			[
				new MayPlayFromYourGraveyard(Is.Type(PermanentTypes)) { OutsideTheGame = true },
			],
			["Infinite Diversity"] =
			[
				Spell(
					[TargetSpec.InYourGraveyard(Is.Instant | Is.Sorcery)],
					new CastCopiesOf(Ref.Target(), Federation),
					new ExileFromGraveyard(Ref.Target()),
					new ExileSourceAndCopiesFromGraveyard(0) // "Exile Infinite Diversity."
				),
			],
			["Brazen Bluff"] =
			[
				Spell(
					[TargetSpec.Spell(!Is.Creature & Is.Opponents)],
					new May("Exile your spell?", new Always(), [new ExileSpell(Ref.Target())])
					{
						Who = Ref.ControllerOfTarget(0),
						Otherwise = [new CastFromHandFree(!Is.Creature)],
					}
				),
			],
			// ===== MC-5.3 batch 7b: the one-offs — modes not chosen before, can't sacrifice Treasures, enters attacking, a granted mana ability, explore, a player's hexproof and damage prevented, counters removed from among =====
			["Gwyndala, the Progeny"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[],
					new CreateTokens(NeurofluxHeirloom, 1)
					{
						Abilities = [AttachedGets(1, 1), Equip("{2}")],
						AttachTo = Ref.Source,
					}
				),
				new ActivatedAbilityComponent(
					ManaCost.Parse("{2}"),
					[
						new BecomeCopyUntilEndOfTurn(
							Ref.Permanents(Is.Token & Is.Yours & Is.Subtype("Equipment")),
							Ref.Target()
						),
					]
				)
				{
					Targets = [Target(Is.Subtype("Equipment") & Is.Yours)],
				},
			],
			["Genesis Device"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[],
					new SearchLibrary(CardType.None, ZoneType.Battlefield)
					{
						Filter = (Is.Land & Is.Supertype(Supertype.Basic)) | Is.Subtype("Planet"),
						FilterText = "basic land or Planet",
						Tapped = true,
					}
				),
				new ActivatedAbilityComponent(
					ManaCost.Parse("{3}{G}{G}"),
					[
						new MaySacrifice(
							Is.Land,
							[
								new SearchLibrary(CardType.Land, ZoneType.Battlefield)
								{
									UpTo = Count.PermanentsChosen,
									Tapped = true,
								},
							]
						)
						{
							AnyNumber = true,
						},
					]
				)
				{
					Costs = [new TapSelfCost(), new SacrificeSelfCost()],
					SorcerySpeed = true,
				},
			],
			["Kobayashi Maru Scenario"] =
			[
				ModalWhen(
					"Kobayashi Maru Scenario",
					TriggerEvent.YourUpkeep,
					1,
					M(
						[
							Target(Is.Creature & Is.Opponents & Is.ManaValueAtMost(3)) with
							{
								Optional = true,
							},
						],
						new ReturnToHand(Ref.Target())
					),
					M([], new DrawCards(Ref.You, 2)),
					M([], new CreateTokens(Officer, 2)),
					M([], new LoseTheGame())
				) with
				{
					ModesNotChosenBefore = true,
				},
			],
			["Quark, Humble Proprietor"] =
			[
				new CantSacrificeTreasures(),
				When(TriggerEvent.EachUpkeep, [], CreateTreasure(1)),
			],
			["Kira Nerys, Firebrand"] =
			[
				new YourCreaturesEnterAttacking(),
				When(
					TriggerEvent.AnotherCreatureYouControlEnters,
					[],
					new ModifyUntilEndOfTurn(Ref.EventCard, 1, 1)
				),
			],
			["All Good Things..."] =
			[
				new SagaComponent(4),
				Chapter(1, [], new DrawCards(Ref.You, 2), ExileAGoodThing),
				Chapter(2, [], new DrawCards(Ref.You, 2), ExileAGoodThing),
				Chapter(3, [], new DrawCards(Ref.You, 2), ExileAGoodThing),
				Chapter(
					4,
					[],
					new If(new ExiledWithSourceShareACardType(), [new GainLife(Ref.You, 10)])
				),
			],
			["Peace in Our Time"] =
			[
				When(
					TriggerEvent.Beginning(Step.BeginCombat, Whose.Any),
					[],
					new May(
						"Draw a card? If you do, you can't attack this turn.",
						new Always(),
						[
							new DrawCards(Ref.You, 1),
							new ThisTurn(new CantAttackThisTurn()),
							new PutCounters(Ref.Source, CounterKinds.Peace, 1),
						]
					)
					{
						Who = Ref.ActivePlayer,
						ThenAsThem = true,
					}
				),
				When(TriggerEvent.YourUpkeep, [], new WinTheGame()) with
				{
					If = new AtLeast(Count.CountersOfKind(Ref.Source, CounterKinds.Peace), 12),
				},
			],
			["The Needs of the Many"] =
			[
				new AdditionalCostsComponent([new SacrificeCost(Is.Creature)]),
				Spell(
					[],
					new ModifyUntilEndOfTurn(Ref.CreaturesYouControl, 0, 0, "Hexproof"),
					new ModifyUntilEndOfTurn(Ref.CreaturesYouControl, 0, 0, "Indestructible"),
					new ThisTurn(new YouHaveHexproof()),
					new ThisTurn(new PreventAllDamageToYou()),
					new ExileSourceAndCopiesFromGraveyard(0) // "Exile The Needs of the Many."
				),
			],
			["Lieutenant Commander Data"] =
			[
				When(
					TriggerEvent.YouCastNoncreatureSpell with
					{
						By = Whose.Any,
					},
					[],
					new AddCounters(Ref.Source, 1)
				),
				new ActivatedAbilityComponent(ManaCost.Parse("{1}"), [new DrawCards(Ref.You, 1)])
				{
					Costs = [new RemoveCountersFromAmongCost(3, Is.Creature)],
				},
			],
			["Rok-Tahk, Budding Scientist"] =
			[
				new GrantsManaAbility(
					Is.Creature & Is.Yours,
					new ManaAbilityComponent(ManaColor.Green) { Amount = 2 }
				),
				When(TriggerEvent.SelfEnters, [], new CreateTokens(Murf, 1)),
			],
			["Captain Kathryn Janeway"] =
			[
				new AdditionalLandEachTurn(),
				When(TriggerEvent.CreatureYouControlEnters, [], new Explore(Ref.EventCard)),
			],
			// ===== MC-5.3 batch 7c =====
			["Nog, Ambitious Cadet"] =
			[
				new ActivatedAbilityComponent(
					ManaCost.Parse("{1}"),
					[
						new If(
							new Matches(Ref.Source, Is.Subtype("Rogue")),
							[
								new ModifyUntilEndOfTurn(Ref.Source, 0, 0)
								{
									ForGood = true,
									BecomesCreatureTypes = ["Ferengi", "Scout"],
									SetBase = (3, 4),
									Trigger = When(
										TriggerEvent.SelfBecomesTapped,
										[],
										new Surveil(2)
									),
								},
							]
						),
					]
				),
				new ActivatedAbilityComponent(
					ManaCost.Parse("{3}"),
					[
						new If(
							new Matches(Ref.Source, Is.Subtype("Scout")),
							[
								new ModifyUntilEndOfTurn(Ref.Source, 0, 0)
								{
									ForGood = true,
									BecomesCreatureTypes = ["Ferengi", "Officer"],
									Gains =
									[
										new MayPlayFromYourGraveyard(Is.Land) { Unlimited = true },
										new AdditionalLandEachTurn(),
									],
								},
							]
						),
					]
				),
			],
			["Kai Winn, Faithless Leader"] =
			[
				new ChoosesNameAsItEnters(),
				When(
					new Trigger(EventKind.SpellCast)
					{
						By = Whose.Opponent,
						Subject = Is.NamedChosenName,
					},
					[],
					new LoseLife(Ref.EachOpponent, 3) // "that player": the one opponent
				),
				When(
					new Trigger(EventKind.AbilityActivated)
					{
						By = Whose.Opponent,
						Subject = Is.NamedChosenName,
					},
					[],
					new LoseLife(Ref.EachOpponent, 3) // "that player": the one opponent
				),
			],
			["H.M.S. Bounty, Unlikely Timeship"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[],
					new SearchLibrary(CardType.Creature, ZoneType.Hand)
					{
						Subtype = "Whale",
						Reveal = true,
					}
				),
				.. Station(8, "Flying"),
				When(
					TriggerEvent.SelfDealsCombatDamageToPlayer,
					[],
					new DrawCards(Ref.You, 1),
					new If(
						new AtLeast(
							Count.NumberOf(Ref.Permanents(Is.Subtype("Whale") & Is.Yours)),
							1
						),
						[new If(new HaventBegunAnExtraTurn(), [new TakeExtraTurn()])]
					)
				),
			],
			["The City on the Edge of Forever"] =
			[
				new SagaComponent(3),
				Chapter(
					1,
					[],
					new PhaseOut(Ref.Permanents(Is.Artifact | Is.Creature)),
					new CreateTokens(CityHuman, 1) { Abilities = [new PermanentsCantPhaseIn()] }
				),
				Chapter(2, [], new DrawCards(Ref.You, 2), new LoseLife(Ref.EachPlayer, 2)),
				Chapter(
					3,
					[],
					new MaySacrifice(Is.Creature & Is.Token, [new GainLife(Ref.You, 5)])
					{
						Must = true,
					}
				),
			],
			["Q, Stacking the Deck"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[],
					new ExileUntilNonlandCastFree(Ref.You)
					{
						ShuffleFirst = true,
						Gains = StackTheDeck,
					}
				),
			],
			// ===== MC-5.6 batch 1: the Stardates — tier 1 =====
			["Empress Galina"] =
			[
				new ActivatedAbilityComponent(
					ManaCost.Parse("{U}{U}"),
					[new GainControl(Ref.Target(), Ref.You)]
				)
				{
					Costs = [new TapSelfCost()],
					Targets = [Target(Is.Legendary)],
				},
			],
			["Sheoldred, the Apocalypse"] =
			[
				When(TriggerEvent.YouDrawACard, [], new GainLife(Ref.You, 2)),
				// "they" — the one opponent in a two-player game.
				When(
					TriggerEvent.YouDrawACard with
					{
						By = Whose.Opponent,
					},
					[],
					new LoseLife(Ref.EachOpponent, 2)
				),
			],
			["Tatyova, Benthic Druid"] =
			[
				When(
					TriggerEvent.LandYouControlEnters,
					[],
					new GainLife(Ref.You, 1),
					new DrawCards(Ref.You, 1)
				),
			],
			["Hull Breach"] =
			[
				ModalSpell(
					"Hull Breach",
					1,
					M([Target(Is.Artifact)], new Destroy(Ref.Target())),
					M([Target(Is.Enchantment)], new Destroy(Ref.Target())),
					M(
						[Target(Is.Artifact), Target(Is.Enchantment)],
						new Destroy(Ref.Target(0)),
						new Destroy(Ref.Target(1))
					)
				),
			],
			["Jhoira, Weatherlight Captain"] =
			[
				When(
					TriggerEvent.YouCastSpell with
					{
						Subject = Is.Artifact | Is.Legendary | Is.Subtype("Saga"),
					},
					[],
					new DrawCards(Ref.You, 1)
				),
			],
			["Captain Sisay"] =
			[
				new ActivatedAbilityComponent(
					ManaCost.None,
					[
						new SearchLibrary(CardType.None, ZoneType.Hand)
						{
							Supertype = Supertype.Legendary,
							Reveal = true,
						},
					]
				)
				{
					Costs = [new TapSelfCost()],
				},
			],
			["Minamo, School at Water's Edge"] =
			[
				new ManaAbilityComponent(ManaColor.Blue),
				new ActivatedAbilityComponent(ManaCost.Parse("{U}"), [new Untap(Ref.Target())])
				{
					Costs = [new TapSelfCost()],
					Targets = [Target(Is.Legendary)],
				},
			],
			["Kenrith, the Returned King"] =
			[
				new ActivatedAbilityComponent(
					ManaCost.Parse("{R}"),
					[
						new ModifyUntilEndOfTurn(Ref.AllCreatures, 0, 0, "Trample"),
						new ModifyUntilEndOfTurn(Ref.AllCreatures, 0, 0, "Haste"),
					]
				),
				new ActivatedAbilityComponent(
					ManaCost.Parse("{1}{G}"),
					[new AddCounters(Ref.Target(), 1)]
				)
				{
					Targets = [Creature],
				},
				new ActivatedAbilityComponent(
					ManaCost.Parse("{2}{W}"),
					[new GainLife(Ref.Target(), 5)]
				)
				{
					Targets = [TargetSpec.Player()],
				},
				new ActivatedAbilityComponent(
					ManaCost.Parse("{3}{U}"),
					[new DrawCards(Ref.Target(), 1)]
				)
				{
					Targets = [TargetSpec.Player()],
				},
				new ActivatedAbilityComponent(
					ManaCost.Parse("{4}{B}"),
					[new ReturnFromGraveyard(Ref.Target(), ZoneType.Battlefield)]
				)
				{
					Targets = [TargetSpec.InAnyGraveyard(Is.Creature)],
				},
			],
			["Arcum Dagsson"] =
			[
				new ActivatedAbilityComponent(
					ManaCost.None,
					[
						// The search first, while "that player" — the target's controller — is still
						// known; nothing it finds can change the sacrifice (a noncreature artifact).
						new SearchLibrary(CardType.Artifact, ZoneType.Battlefield)
						{
							Filter = !Is.Creature,
							FilterText = "noncreature",
							Who = Ref.ControllerOfTarget(0),
						},
						new Sacrifice(Ref.Target()),
					]
				)
				{
					Costs = [new TapSelfCost()],
					Targets = [Target(Is.Artifact & Is.Creature)],
				},
			],
			// ===== MC-5.6 batch 1: the Stardates on MC-6's composed pieces =====
			["Isshin, Two Heavens as One"] =
			[
				// "a creature attacking" — each attacker's own event, and "whenever you attack" (Shayne).
				new TriggersAdditionalTime(new Trigger(EventKind.Attacked), Is.Yours),
				new TriggersAdditionalTime(new Trigger(EventKind.AttackersDeclared), Is.Yours),
			],
			["Torbran, Thane of Red Fell"] =
			[
				new NumericReplacement(ReplacedEvent.Damage)
				{
					Source = Is.Color(ManaColor.Red) & Is.Yours,
					Affected = Is.Opponents,
					Plus = 2,
				},
			],
			["Bruvac the Grandiloquent"] =
			[
				new NumericReplacement(ReplacedEvent.Mill) { Affected = Is.Opponents, Times = 2 },
			],
			["Rishkar, Peema Renegade"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[
						Creature with
						{
							Optional = true,
						},
						Creature with
						{
							Optional = true,
							NotThePrevious = true,
						},
					],
					new AddCounters(Ref.Target(0), 1),
					new AddCounters(Ref.Target(1), 1)
				),
				new GrantsManaAbility(
					Is.Creature & Is.Yours & Is.WithAnyCounter,
					new ManaAbilityComponent(ManaColor.Green)
				),
			],
			["Talon Gates of Madara"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[Creature with { Optional = true }],
					new PhaseOut(Ref.Target())
				),
				new ManaAbilityComponent(ManaColor.Colorless),
				ManaAbilityComponent.OfAnyOneColor(1) with
				{
					Cost = ManaCost.Parse("{1}"),
				},
				new ActivatedAbilityComponent(
					ManaCost.Parse("{4}"),
					[
						new ReturnFromGraveyard(Ref.Source, ZoneType.Battlefield)
						{
							From = ZoneType.Hand,
						},
					]
				)
				{
					From = ZoneType.Hand,
				},
			],
			["Phyrexian Tower"] =
			[
				new ManaAbilityComponent(ManaColor.Colorless),
				// Never auto-paid: a move, one per creature (Shayne, 2026-11-06).
				new ManaAbilityComponent(ManaColor.Black)
				{
					Amount = 2,
					Costs = [new TapSelfCost(), new SacrificeCost(Is.Creature)],
				},
			],
			["Sisay, Weatherlight Captain"] =
			[
				new StaticEffectComponent(Is.Source)
				{
					Power = Count.ColorsAmong(Ref.Permanents(Is.Legendary & Is.Yours & !Is.Source)),
					Toughness = Count.ColorsAmong(
						Ref.Permanents(Is.Legendary & Is.Yours & !Is.Source)
					),
				},
				new ActivatedAbilityComponent(
					ManaCost.Parse("{W}{U}{B}{R}{G}"),
					[
						new SearchLibrary(CardType.None, ZoneType.Battlefield)
						{
							Supertype = Supertype.Legendary,
							// "a legendary permanent card with mana value less than Sisay's power"
							Filter =
								!Is.Instant
								& !Is.Sorcery
								& Is.ManaValueAtMost(Count.PowerOf(Ref.Source) with { Plus = -1 }),
							FilterText = "permanent",
						},
					]
				),
			],
			["Loot, Exuberant Explorer"] =
			[
				new AdditionalLandEachTurn(),
				new ActivatedAbilityComponent(
					ManaCost.Parse("{4}{G}{G}"),
					[
						new LookAndTake(6, Is.Creature & Is.ManaValueAtMost(Count.LandsYouControl))
						{
							Reveal = true,
							OntoBattlefield = true,
						},
					]
				)
				{
					Costs = [new TapSelfCost()],
				},
			],
		};

	/// <summary>
	/// Q, Stacking the Deck's gift: "Exile a card from your hand: Create a copy of this card. You may cast the
	/// copy. Activate only during your turn and only if this card is exiled."
	/// </summary>
	static ActivatedAbilityComponent StackTheDeck =>
		new(ManaCost.None, [new CastCopiesOf(Ref.Source, 1) { PayCost = true }])
		{
			From = ZoneType.Exile,
			OnlyIf = new DuringYourTurn(),
			Costs = [new ExileFromHandCost()],
		};

	/// <summary>
	/// Borg Cube: "Whenever this Spacecraft enters or attacks, assimilate a creature card exiled with it.
	/// If you can't, exile up to one target creature."
	/// </summary>
	static TriggeredAbilityComponent BorgCubeAssimilates(Trigger on) =>
		When(
			on,
			[Target(Is.Creature) with { Optional = true }],
			new AssimilateExiledWithSource([new Exile(Ref.Target()) { Linked = true }])
		);

	/// <summary>Deep Space 9: "exile the top four cards of your library. Put all permanent cards exiled this way onto the battlefield."</summary>
	static RevealUntilOntoBattlefield DeepSpace9Exile =>
		new(PermanentTypes) { Top = 4, Exile = true };

	/// <summary>
	/// The station ability alone: "Tap another creature you control: Put charge counters equal to its
	/// power on this Spacecraft. Station only as a sorcery." — Deep Space 9 is never a creature.
	/// </summary>
	static ActivatedAbilityComponent StationAbility =>
		new(ManaCost.None, [new AddChargeCounters(Ref.Source, Count.PowerOf(Ref.EventCard))])
		{
			Costs = [new TapUntappedCost(1, Is.Creature & !Is.Source)],
			SorcerySpeed = true,
		};

	/// <summary>
	/// Discover N (U.S.S. Discovery, at Black Alert): "Exile cards from the top of your library until you
	/// exile a nonland card with mana value N or less. Cast it without paying its mana cost or put it into
	/// your hand. Put the rest on the bottom in a random order."
	/// </summary>
	static ExileUntilNonlandCastFree Discover(int n) =>
		new(Ref.You)
		{
			ManaValueAtMost = n,
			OrIntoHand = true,
			RestOnBottom = true,
		};

	/// <summary>The shocklands (Blood Crypt …): "As this land enters, you may pay 2 life. If you don't, it enters tapped."</summary>
	static EntersTappedComponent Shockland => new() { UnlessYouPayLife = 2 };

	/// <summary>All Good Things..., chapters I–III: "then exile a card from your hand" — linked to this Saga for chapter IV.</summary>
	static MayDiscard ExileAGoodThing =>
		new([])
		{
			Exile = true,
			Must = true,
			Linked = true,
		};

	/// <summary>A quest's threshold: "if this enchantment has N or more quest counters on it".</summary>
	static Condition Quests(int n) =>
		new AtLeast(Count.CountersOfKind(Ref.Source, CounterKinds.Quest), n);
}
