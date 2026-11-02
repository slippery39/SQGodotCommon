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
			new ActivatedAbilityComponent(
				ManaCost.None,
				[new AddChargeCounters(Ref.Source, Count.PowerOf(Ref.EventCard))]
			)
			{
				Costs = [new TapUntappedCost(1, Is.Creature & !Is.Source)],
				SorcerySpeed = true,
			},
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
				When(
					new Trigger(EventKind.Died) { Subject = Is.Creature },
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
					new ModifyUntilEndOfTurn(Ref.Target(), 1, 1) { BecomesCreatureType = "Doctor" }
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
					new ThisTurn(new CountersPlusOneThisTurn())
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
		};

	/// <summary>A quest's threshold: "if this enchantment has N or more quest counters on it".</summary>
	static Condition Quests(int n) =>
		new AtLeast(Count.CountersOfKind(Ref.Source, CounterKinds.Quest), n);
}
