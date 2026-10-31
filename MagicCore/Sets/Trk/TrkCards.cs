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
		};
}
