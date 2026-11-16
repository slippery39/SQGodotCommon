using System.Collections.Immutable;
using ImmutableGameObjects;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Filters;
using MagicCore.Targeting;
using MagicCore.Turns;
using static MagicCore.Sets.Scripting;
using static MagicCore.Sets.Tokens;

namespace MagicCore.Sets.Lsv;

/// <summary>
/// LSVPower's hand-written abilities, keyed by card name (story MC-4) — as <see cref="Fra.FraCards"/>:
/// names, costs, types, P/T and keywords come from LSV.json; only what the rules text DOES is written
/// here. A card another set already scripts (a reprint) is NOT scripted again — <see cref="CardRegistry"/>
/// refuses it. The survey and what each card waits on: <c>MagicCore/LsvSurvey.md</c>. The original duals
/// (Tundra …) need no script: their text is reminder text, their mana their land types (CR 305.6).
/// </summary>
public static class LsvCards
{
	/// <summary>"{T}: Add {C}." — Strip Mine, Wasteland.</summary>
	static ManaAbilityComponent ColorlessMana(int amount = 1) =>
		new(ManaColor.Colorless) { Amount = amount };

	/// <summary>The Talismans: "{T}: Add {C}. {T}: Add {A} or {B}. This artifact deals 1 damage to you."</summary>
	static ImmutableArray<GameComponent> Talisman(ManaColor a, ManaColor b) =>
		[
			ColorlessMana(),
			new ManaAbilityComponent(a) { DealsDamageToYou = 1 },
			new ManaAbilityComponent(b) { DealsDamageToYou = 1 },
		];

	/// <summary>"{T}: Add {A}, {B}, or {C}." — three abilities (Noble Hierarch).</summary>
	static IEnumerable<GameComponent> ManaOf(params ManaColor[] colors) =>
		colors.Select(c => new ManaAbilityComponent(c));

	/// <summary>
	/// The fetchlands: "{T}, Pay 1 life, Sacrifice this land: Search your library for a [Plains or Island] card,
	/// put it onto the battlefield, then shuffle." Prismatic Vista: "a basic land card".
	/// </summary>
	static ActivatedAbilityComponent Fetch(Filter which, string text) =>
		new(
			ManaCost.None,
			[
				new SearchLibrary(CardType.Land, ZoneType.Battlefield)
				{
					Filter = which,
					FilterText = text,
				},
			]
		)
		{
			Costs = [new TapSelfCost(), new PayLifeCost(1), new SacrificeSelfCost()],
		};

	static ActivatedAbilityComponent Fetch(string a, string b) =>
		Fetch(Is.Subtype(a) | Is.Subtype(b), $"{a} or {b}");

	/// <summary>Strip Mine, Wasteland: "{T}, Sacrifice this land: Destroy target [nonbasic] land."</summary>
	static ActivatedAbilityComponent SacrificeToDestroy(Filter land) =>
		new(ManaCost.None, [new Destroy(Ref.Target())])
		{
			Costs = [new TapSelfCost(), new SacrificeSelfCost()],
			Targets = [Target(land)],
		};

	/// <summary>Mana Leak: "Counter target [noncreature] spell unless its controller pays {N}."</summary>
	/// <summary>Force of Will, Force of Negation: "exile a blue card from your hand" (not this one).</summary>
	static ExileFromHandCost ExileABlueCard => new(Is.Color(ManaColor.Blue));

	static SpellEffectsComponent CounterUnless(string cost, Filter? spells = null) =>
		Spell(
			[TargetSpec.Spell(spells)],
			new CounterUnlessPays(Ref.Target(), ManaCost.Parse(cost))
		);

	/// <summary>
	/// Path to Exile, Erode: "Its controller may search their library for a basic land card, put it onto the
	/// battlefield tapped, then shuffle." — scripted BEFORE the removal: once the creature has left, "its controller"
	/// reads its owner (a new object), so a stolen creature's land would go to the wrong player (as 70,000 Light-Years).
	/// </summary>
	static readonly SearchLibrary ItsControllerMayFetch =
		new(CardType.Land, ZoneType.Battlefield)
		{
			Supertype = Supertype.Basic,
			Tapped = true,
			Who = Ref.ControllerOfTarget(0),
		};

	/// <summary>Pyroblast: "… if it's blue" — any spell or permanent is a legal target; only a blue one is hit.</summary>
	static If IfBlue(GameAction effect) =>
		new(new Matches(Ref.Target(), Is.Color(ManaColor.Blue)), [effect]);

	/// <summary>Brainstorm: "then put two cards from your hand on top of your library in any order" — one at a time, the second on top.</summary>
	static MoveChosen PutOnTop(string prompt) =>
		new(ZoneType.Hand, null, ZoneType.Library) { OnTop = true, Prompt = prompt };

	/// <summary>The Monoliths and Mana Vault: "This artifact doesn't untap during your untap step."</summary>
	static readonly DoesntUntap DoesntUntapItself = new(Is.Source);

	internal static readonly Dictionary<string, ImmutableArray<GameComponent>> Scripts =
		new()
		{
			// ===== MC-4.3a: batch 1, power and mana =====
			["Mox Pearl"] = [new ManaAbilityComponent(ManaColor.White)],
			["Mox Sapphire"] = [new ManaAbilityComponent(ManaColor.Blue)],
			["Mox Jet"] = [new ManaAbilityComponent(ManaColor.Black)],
			["Mox Ruby"] = [new ManaAbilityComponent(ManaColor.Red)],
			["Mox Emerald"] = [new ManaAbilityComponent(ManaColor.Green)],
			["Black Lotus"] = [SacrificeForMana(3)],
			["Lotus Petal"] = [SacrificeForMana(1)],
			["Sol Ring"] = [ColorlessMana(2)],
			["Mana Crypt"] =
			[
				When(TriggerEvent.YourUpkeep, [], new FlipACoin([new DealDamage(Ref.You, 3)])),
				ColorlessMana(2),
			],
			["Mana Vault"] =
			[
				DoesntUntapItself,
				When(
					TriggerEvent.YourUpkeep,
					[],
					new May(
						"Pay {4} to untap Mana Vault?",
						new CanPay(ManaCost.Parse("{4}")),
						[new PayMana(ManaCost.Parse("{4}"), [new Untap(Ref.Source)])]
					)
				),
				When(
					TriggerEvent.Beginning(Step.Draw, Whose.You),
					[],
					new DealDamage(Ref.You, 1)
				) with
				{
					If = new Matches(Ref.Source, Is.Tapped),
				},
				ColorlessMana(3),
			],
			["Grim Monolith"] =
			[
				DoesntUntapItself,
				ColorlessMana(3),
				new ActivatedAbilityComponent(ManaCost.Parse("{4}"), [new Untap(Ref.Source)]),
			],
			["Basalt Monolith"] =
			[
				DoesntUntapItself,
				ColorlessMana(3),
				new ActivatedAbilityComponent(ManaCost.Parse("{3}"), [new Untap(Ref.Source)]),
			],
			["Talisman of Conviction"] = Talisman(ManaColor.Red, ManaColor.White),
			["Talisman of Creativity"] = Talisman(ManaColor.Blue, ManaColor.Red),
			["Talisman of Curiosity"] = Talisman(ManaColor.Green, ManaColor.Blue),
			["Talisman of Dominance"] = Talisman(ManaColor.Blue, ManaColor.Black),
			["Talisman of Indulgence"] = Talisman(ManaColor.Black, ManaColor.Red),
			["Talisman of Progress"] = Talisman(ManaColor.White, ManaColor.Blue),
			["Llanowar Elves"] = [new ManaAbilityComponent(ManaColor.Green)],
			["Avacyn's Pilgrim"] = [new ManaAbilityComponent(ManaColor.White)],
			["Birds of Paradise"] = [.. AnyColorMana(ManaRestriction.None)],
			["Noble Hierarch"] =
			[
				Exalted,
				.. ManaOf(ManaColor.Green, ManaColor.White, ManaColor.Blue),
			],
			["Ignoble Hierarch"] =
			[
				Exalted,
				.. ManaOf(ManaColor.Black, ManaColor.Red, ManaColor.Green),
			],
			["Dark Ritual"] = [Spell([], new AddMana(ManaColor.Black, 3))],
			["Cabal Ritual"] =
			[
				Spell(
					[],
					new If(Threshold, [new AddMana(ManaColor.Black, 5)])
					{
						Otherwise = [new AddMana(ManaColor.Black, 3)],
					}
				),
			],
			["Strip Mine"] = [ColorlessMana(), SacrificeToDestroy(Is.Land)],
			["Wasteland"] =
			[
				ColorlessMana(),
				SacrificeToDestroy(Is.Land & !Is.Supertype(Supertype.Basic)),
			],
			["Ancient Tomb"] = [ColorlessMana(2) with { DealsDamageToYou = 2 }],
			["Mishra's Workshop"] = [ColorlessMana(3) with { Restriction = new(Is.Artifact) }],
			["City of Traitors"] =
			[
				When(
					new Trigger(EventKind.LandPlayed) { By = Whose.You, Subject = !Is.Source },
					[],
					new Sacrifice(Ref.Source)
				),
				ColorlessMana(2),
			],
			["Gaea's Cradle"] =
			[
				new ManaAbilityComponent(ManaColor.Green) { Amount = Count.CreaturesYouControl },
			],
			["Tolarian Academy"] =
			[
				new ManaAbilityComponent(ManaColor.Blue)
				{
					Amount = Count.NumberOf(Ref.Permanents(Is.Artifact & Is.Yours)),
				},
			],
			["Mana Confluence"] =
			[
				ManaAbilityComponent.OfAnyOneColor(1) with
				{
					Costs = [new TapSelfCost(), new PayLifeCost(1)],
				},
			],
			["Arid Mesa"] = [Fetch("Mountain", "Plains")],
			["Bloodstained Mire"] = [Fetch("Swamp", "Mountain")],
			["Flooded Strand"] = [Fetch("Plains", "Island")],
			["Marsh Flats"] = [Fetch("Plains", "Swamp")],
			["Misty Rainforest"] = [Fetch("Forest", "Island")],
			["Polluted Delta"] = [Fetch("Island", "Swamp")],
			["Scalding Tarn"] = [Fetch("Island", "Mountain")],
			["Verdant Catacombs"] = [Fetch("Swamp", "Forest")],
			["Windswept Heath"] = [Fetch("Forest", "Plains")],
			["Wooded Foothills"] = [Fetch("Mountain", "Forest")],
			["Prismatic Vista"] = [Fetch(Is.Supertype(Supertype.Basic), "basic land")],
			// ===== MC-4.3b: batch 1, spells =====
			["Counterspell"] = [Spell([TargetSpec.Spell()], new Counter(Ref.Target()))],
			["Mana Leak"] = [CounterUnless("{3}")],
			["Mana Tithe"] = [CounterUnless("{1}")],
			["Spell Pierce"] = [CounterUnless("{2}", !Is.Creature)],
			["Memory Lapse"] =
			[
				Spell([TargetSpec.Spell()], new Counter(Ref.Target()) { To = ZoneType.Library }),
			],
			["Remand"] =
			[
				Spell(
					[TargetSpec.Spell()],
					new Counter(Ref.Target()) { To = ZoneType.Hand },
					new DrawCards(Ref.You, 1)
				),
			],
			["Stern Scolding"] =
			[
				Spell(
					[TargetSpec.Spell(Is.Creature & (Is.PowerAtMost(2) | Is.ToughnessAtMost(2)))],
					new Counter(Ref.Target())
				),
			],
			["Lightning Bolt"] = [Spell([TargetSpec.AnyTarget()], new DealDamage(Ref.Target(), 3))],
			["Burst Lightning"] =
			[
				new KickerComponent(ManaCost.Parse("{4}")),
				Spell(
					[TargetSpec.AnyTarget()],
					new If(new WasKicked(), [new DealDamage(Ref.Target(), 4)])
					{
						Otherwise = [new DealDamage(Ref.Target(), 2)],
					}
				),
			],
			["Swords to Plowshares"] =
			[
				// The power is read before it leaves (last-known information).
				Spell(
					[Creature],
					new GainLife(Ref.ControllerOfTarget(0), Count.PowerOf(Ref.Target())),
					new Exile(Ref.Target())
				),
			],
			["Path to Exile"] = [Spell([Creature], ItsControllerMayFetch, new Exile(Ref.Target()))],
			["Erode"] =
			[
				Spell([CreatureOrPlaneswalker], ItsControllerMayFetch, new Destroy(Ref.Target())),
			],
			["Cut Down"] =
			[
				Spell(
					[Target(Is.Creature & Is.TotalPowerAndToughnessAtMost(5))],
					new Destroy(Ref.Target())
				),
			],
			["Vindicate"] = [Spell([Target()], new Destroy(Ref.Target()))],
			["Abrupt Decay"] =
			[
				new CantBeCountered(),
				Spell([Target(!Is.Land & Is.ManaValueAtMost(3))], new Destroy(Ref.Target())),
			],
			["Wrath of God"] = [Spell([], new Destroy(Ref.Permanents(Is.Creature)))],
			["Abrade"] =
			[
				ModalSpell(
					"Abrade",
					1,
					M([Creature], new DealDamage(Ref.Target(), 3)),
					M([Target(Is.Artifact)], new Destroy(Ref.Target()))
				),
			],
			["Pyroblast"] =
			[
				ModalSpell(
					"Pyroblast",
					1,
					M([TargetSpec.Spell()], IfBlue(new Counter(Ref.Target()))),
					M([Target()], IfBlue(new Destroy(Ref.Target())))
				),
			],
			["Time Walk"] = [Spell([], new TakeExtraTurn())],
			["Time Warp"] =
			[
				Spell([TargetSpec.Player()], new TakeExtraTurn { Who = Ref.Target() }),
			],
			["Ancestral Recall"] = [Spell([TargetSpec.Player()], new DrawCards(Ref.Target(), 3))],
			// ===== batch 2 (MC-4.4a): the costs batch 1 left out =====
			["Force of Will"] =
			[
				new MayCastFromHandFree(Is.Source) { Costs = [new PayLifeCost(1), ExileABlueCard] },
				Spell([TargetSpec.Spell()], new Counter(Ref.Target())),
			],
			["Force of Negation"] =
			[
				new MayCastFromHandFree(Is.Source)
				{
					If = new Not(new DuringYourTurn()),
					Costs = [ExileABlueCard],
				},
				Spell(
					[TargetSpec.Spell(!Is.Creature)],
					new Counter(Ref.Target()) { To = ZoneType.Exile }
				),
			],
			["Daze"] =
			[
				new MayCastFromHandFree(Is.Source)
				{
					Costs = [new ReturnToHandCost(Is.Subtype("Island"))],
				},
				CounterUnless("{1}"),
			],
			["Snuff Out"] =
			[
				new MayCastFromHandFree(Is.Source)
				{
					If = new AtLeast(
						Count.NumberOf(Ref.Permanents(Is.Yours & Is.Subtype("Swamp"))),
						1
					),
					Costs = [new PayLifeCost(4)],
				},
				// "It can't be regenerated": the engine has no regeneration yet (Wrath of God's note, MC-4.3b).
				Spell(
					[Target(Is.Creature & !Is.Color(ManaColor.Black))],
					new Destroy(Ref.Target())
				),
			],
			["Baleful Mastery"] =
			[
				new MayCastFromHandFree(Is.Source) { Mana = ManaCost.Parse("{1}{B}") },
				Spell(
					[CreatureOrPlaneswalker],
					new If(new AlternativeCostWasPaid(), [new DrawCards(Ref.EachOpponent, 1)]),
					new Exile(Ref.Target())
				),
			],
			["Bitter Triumph"] =
			[
				new AdditionalCostsComponent([new DiscardCost() { Or = new PayLifeCost(3) }]),
				Spell([CreatureOrPlaneswalker], new Destroy(Ref.Target())),
			],
			["Bone Shards"] =
			[
				new AdditionalCostsComponent(
					[new SacrificeCost(Is.Creature) { Or = new DiscardCost() }]
				),
				Spell([CreatureOrPlaneswalker], new Destroy(Ref.Target())),
			],
			["Lose Focus"] = [new ReplicateComponent(ManaCost.Parse("{U}")), CounterUnless("{2}")],
			["Chain Lightning"] =
			[
				// "may pay {R}{R}. If the player does, they may copy this spell" — one yes: paying is only for the copy.
				Spell(
					[TargetSpec.AnyTarget()],
					new DealDamage(Ref.Target(), 3),
					new May(
						"Pay {R}{R} to copy Chain Lightning?",
						new CanPay(ManaCost.Parse("{R}{R}")),
						[new PayMana(ManaCost.Parse("{R}{R}"), [new CopySpell(Ref.Source)])]
					)
					{
						Who = Ref.ControllerOfTarget(0),
						ThenAsThem = true,
					}
				),
			],
			["Mana Drain"] =
			[
				Spell(
					[TargetSpec.Spell()],
					new AtNextEndStep([new AddMana(ManaColor.Colorless, Count.X)])
					{
						NextMainPhase = true,
						X = Count.GreatestManaValueOf(Ref.Target(0)),
					},
					new Counter(Ref.Target())
				),
			],
			["Brainstorm"] =
			[
				Spell(
					[],
					new DrawCards(Ref.You, 3),
					PutOnTop(
						"Put a card from your hand on top of your library (the next goes above it)"
					),
					PutOnTop("Put a card from your hand on top of your library — you draw it next")
				),
			],
			// ===== MC-4.4b: the mana batch 1 left out =====
			["Elvish Spirit Guide"] = [SpiritGuide(ManaColor.Green)],
			["Simian Spirit Guide"] = [SpiritGuide(ManaColor.Red)],
			["Delighted Halfling"] =
			[
				new ManaAbilityComponent(ManaColor.Colorless),
				ManaAbilityComponent.OfAnyOneColor(1) with
				{
					Restriction = new(Is.Legendary) { CantBeCountered = true },
				},
			],
			// ===== MC-4.4c part 1: red's tier 1, tested pieces =====
			["Flame Slash"] = [Spell([Creature], new DealDamage(Ref.Target(), 4))],
			["Sear"] = [Spell([CreatureOrPlaneswalker], new DealDamage(Ref.Target(), 4))],
			["Flametongue Kavu"] =
			[
				When(TriggerEvent.SelfEnters, [Creature], new DealDamage(Ref.Target(), 4)),
			],
			["Wheel of Fortune"] =
			[
				Spell([], new DiscardHand(Ref.EachPlayer), new DrawCards(Ref.EachPlayer, 7)),
			],
			["Faithless Looting"] =
			[
				Spell([], new DrawCards(Ref.You, 2), new Discard(Ref.You, 2)),
				new FlashbackComponent(ManaCost.Parse("{2}{R}")),
			],
			["Mine Collapse"] =
			[
				new MayCastFromHandFree(Is.Source)
				{
					If = new DuringYourTurn(),
					Costs = [new SacrificeCost(Is.Land & Is.Subtype("Mountain"))],
				},
				Spell([CreatureOrPlaneswalker], new DealDamage(Ref.Target(), 5)),
			],
			["Suplex"] =
			[
				ModalSpell(
					"Suplex",
					1,
					M(
						[Creature],
						new DealDamage(Ref.Target(), 3),
						new ModifyUntilEndOfTurn(Ref.Target(), 0, 0) { ExileIfItWouldDie = true }
					),
					M([Target(Is.Artifact)], new Exile(Ref.Target()))
				),
			],
			["Legion Extruder"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[TargetSpec.AnyTarget()],
					new DealDamage(Ref.Target(), 2)
				),
				new ActivatedAbilityComponent(ManaCost.Parse("{2}"), [new CreateTokens(Golem, 1)])
				{
					Costs = [new TapSelfCost(), new SacrificeCost(Is.Artifact & !Is.Source)],
				},
			],
			["Seasoned Pyromancer"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[],
					new Discard(Ref.You, 2)
					{
						Then =
						[
							new DrawCards(Ref.You, 2),
							new CreateTokens(Elemental, Count.NonlandCardsChosen),
						],
					}
				),
				new ActivatedAbilityComponent(
					ManaCost.Parse("{3}{R}{R}"),
					[new CreateTokens(Elemental, 2)]
				)
				{
					Costs = [new ExileSelfCost()],
					From = ZoneType.Graveyard,
				},
			],
			["Pyrogoyf"] =
			[
				new StaticEffectComponent(Is.Source)
				{
					Power = Count.CardTypesInAllGraveyards,
					Toughness = Count.CardTypesInAllGraveyards,
				},
				// "this creature or another Lhurgoyf creature you control" — "that creature deals"
				When(
					new Trigger(EventKind.Entered)
					{
						Subject = Is.Creature & Is.Yours & (Is.Source | Is.Subtype("Lhurgoyf")),
					},
					[TargetSpec.AnyTarget()],
					new DamageFrom(Ref.EventCard, Ref.Target(), Count.PowerOf(Ref.EventCard))
				),
			],
			["Generous Plunderer"] =
			[
				When(
					TriggerEvent.YourUpkeep,
					[],
					new May(
						"Create a Treasure token?",
						new Always(),
						[
							CreateTreasure(1),
							new WhenYouDo(
								[TargetSpec.Player(Is.Opponents)],
								[
									CreateTreasure(1) with
									{
										ControlledBy = Ref.Target(),
										Tapped = true,
									},
								]
							),
						]
					)
				),
				// "defending player" — the one opponent
				When(
					TriggerEvent.SelfAttacks,
					[],
					new DealDamage(
						Ref.EachOpponent,
						Count.NumberOf(Ref.Permanents(Is.Artifact & Is.Opponents))
					)
				),
			],
			// ===== MC-4.4c2a: red's options =====
			["Broadside Bombardiers"] =
			[
				// Boast: "Activate only if this creature attacked this turn and only once each turn." The sacrificed
				// permanent is the ability's event card, its mana value read as it last was.
				new ActivatedAbilityComponent(
					ManaCost.None,
					[
						new DealDamage(
							Ref.Target(),
							Count.GreatestManaValueOf(Ref.EventCard) with
							{
								Plus = 2,
							}
						),
					]
				)
				{
					Keyword = "Boast",
					Costs = [new SacrificeCost((Is.Creature | Is.Artifact) & !Is.Source)],
					Targets = [TargetSpec.AnyTarget()],
					OnlyIf = new Matches(Ref.Source, Is.AttackedThisTurn),
					OncePerTurn = true,
				},
			],
			["Cori-Steel Cutter"] =
			[
				AttachedGets(1, 1),
				new StaticEffectComponent(Is.AttachedToSource, "Trample"),
				new StaticEffectComponent(Is.AttachedToSource, "Haste"),
				// Flurry — "Whenever you cast your second spell each turn"
				When(
					TriggerEvent.YouCastSpell with
					{
						SpellNumber = 2,
					},
					[],
					new CreateTokens(Monk, 1)
					{
						Abilities = [Prowess],
						Then =
						[
							new May(
								"Attach Cori-Steel Cutter to the Monk?",
								new Always(),
								[new AttachSourceTo(Ref.ThisWay)]
							),
						],
					}
				),
				Equip("{1}{R}"),
			],
			["Bonehoard Dracosaur"] =
			[
				When(
					TriggerEvent.YourUpkeep,
					[],
					new ExileTopPlayable
					{
						N = 2,
						Play = new() { ThisTurn = true },
						Then =
						[
							new If(
								new Matches(Ref.ThisWay, Is.Land),
								[new CreateTokens(Dinosaur, 1)]
							),
							new If(new Matches(Ref.ThisWay, !Is.Land), [CreateTreasure(1)]),
						],
					}
				),
			],
			["Inti, Seneschal of the Sun"] =
			[
				When(
					TriggerEvent.YouAttack,
					[],
					new MayDiscard(
						[
							new WhenYouDo(
								[Target(Is.Creature & Is.Attacking)],
								[
									new PutCounters(Ref.Target(), 1),
									new ModifyUntilEndOfTurn(Ref.Target(), 0, 0, "Trample"),
								]
							),
						]
					)
				),
				When(
					TriggerEvent.APlayerDiscards with
					{
						By = Whose.You,
					},
					[],
					new ExileTopPlayable { Play = new() { UntilYourNextEndStep = true } }
				),
			],
			["Magda, Brazen Outlaw"] =
			[
				new StaticEffectComponent(Is.Creature & Is.Yours & Is.Subtype("Dwarf") & !Is.Source)
				{
					Power = 1,
				},
				When(
					new Trigger(EventKind.BecameTapped)
					{
						Subject = Is.Yours & Is.Subtype("Dwarf"),
					},
					[],
					CreateTreasure(1)
				),
				new ActivatedAbilityComponent(
					ManaCost.None,
					[
						new SearchLibrary(CardType.None, ZoneType.Battlefield)
						{
							Filter = Is.Artifact | Is.Subtype("Dragon"),
							FilterText = "artifact or Dragon",
						},
					]
				)
				{
					Costs = [new SacrificeCost(Is.Artifact & Is.Subtype("Treasure")) { Count = 5 }],
				},
			],
			["Tersa Lightshatter"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[],
					new Discard(Ref.You, 2)
					{
						UpTo = true,
						Then = [new DrawCards(Ref.You, Count.NumberOf(Ref.Chosen))],
					}
				),
				When(
					TriggerEvent.SelfAttacks,
					[],
					new ExileTopPlayable
					{
						From = ZoneType.Graveyard,
						AtRandom = true,
						Play = new() { ThisTurn = true },
					}
				) with
				{
					If = new AtLeast(Count.NumberOf(Ref.CardsInYourGraveyard), 7),
				},
			],
			// ===== MC-4.4c2b: red's options, part b =====
			["Sneak Attack"] =
			[
				new ActivatedAbilityComponent(
					ManaCost.Parse("{R}"),
					[
						new MoveChosen(ZoneType.Hand, Is.Creature, ZoneType.Battlefield)
						{
							May = true,
							Then =
							[
								// "That creature gains haste." — no duration: while it stays that permanent
								new ModifyUntilEndOfTurn(Ref.Chosen, 0, 0, "Haste")
								{
									ForGood = true,
								},
								new AtNextEndStep([new Sacrifice(Ref.Source)]) { On = Ref.Chosen },
							],
						},
					]
				),
			],
			["Orcish Lumberjack"] =
			[
				new ManaAbilityComponent(ManaColor.Red)
				{
					Amount = 3,
					OrColor = ManaColor.Green,
					Costs = [new TapSelfCost(), new SacrificeCost(Is.Subtype("Forest"))],
				},
			],
			["Fiery Confluence"] =
			[
				ModalSpell(
					"Fiery Confluence",
					new()
					{
						AtLeast = 3,
						AtMost = 3,
						Repeats = true,
					},
					M([], new DealDamage(Ref.AllCreatures, 1)),
					M([], new DealDamage(Ref.EachOpponent, 2)),
					M([Target(Is.Artifact)], new Destroy(Ref.Target()))
				),
			],
			["Screaming Nemesis"] =
			[
				When(
					TriggerEvent.SelfDealtDamage,
					[TargetSpec.AnyTarget(!Is.Source)],
					new DealDamage(Ref.Target(), Count.X)
					{
						Then =
						[
							new ThisTurn(new LifeTotalCantChange { GainOnly = true })
							{
								On = Ref.ThisWay,
								RestOfTheGame = true,
							},
						],
					}
				),
			],
			["Goblin Welder"] =
			[
				new ActivatedAbilityComponent(
					ManaCost.None,
					[
						// "If both targets are still legal as this ability resolves": one gone illegal reads as nothing
						new If(
							new Both(
								new Matches(Ref.Target(0), Is.Artifact),
								new Matches(Ref.Target(1), Is.Artifact)
							),
							[
								new Sacrifice(Ref.Target(0)),
								new MoveTo(Ref.Target(1), ZoneType.Battlefield)
								{
									From = [ZoneType.Graveyard],
								},
							]
						),
					]
				)
				{
					Costs = [new TapSelfCost()],
					Targets =
					[
						Target(Is.Artifact),
						TargetSpec.InAnyGraveyard(Is.Artifact) with
						{
							OfThePreviousTargetsController = true,
						},
					],
				},
			],
			["Robber of the Rich"] =
			[
				When(
					TriggerEvent.SelfAttacks,
					[],
					new ExileTopPlayable
					{
						Of = Ref.EachOpponent,
						Play = new()
						{
							Forever = true,
							CastOnly = true,
							AnyType = true,
							If = new SubtypeEnteredThisTurn(["Rogue"]) { Attacked = true },
						},
					}
				) with
				{
					// "if defending player has more cards in hand than you" — the one opponent
					If = new AtLeast(
						Count.NumberOf(Ref.All(ZoneType.Hand, of: Ref.EachOpponent)),
						1
					)
					{
						MoreThan = Count.NumberOf(Ref.CardsInYourHand),
					},
				},
			],
			["Underworld Breach"] =
			[
				// Escape: cast from your graveyard for its mana cost plus three other cards exiled from there
				new MayPlayFromZone(ZoneType.Graveyard, !Is.Land)
				{
					AnyTime = true,
					Costs = [new ExileFromGraveyardCost(!Is.Source) { Count = 3 }],
				},
				When(TriggerEvent.EachEndStep, [], new Sacrifice(Ref.Source)),
			],
			// ===== MC-4.4c2c: the zone-change event =====
			["Laelia, the Blade Reforged"] =
			[
				When(
					TriggerEvent.SelfAttacks,
					[],
					new ExileTopPlayable { Play = new() { ThisTurn = true } }
				),
				// "from your library and/or your graveyard": the event's moves but a permanent's off the battlefield
				When(
					new Trigger(EventKind.ChangedZone)
					{
						By = Whose.You,
						To = ZoneType.Exile,
						NotFrom = [ZoneType.Battlefield],
						OneOrMore = true,
					},
					[],
					new PutCounters(Ref.Source, 1)
				),
			],
			// ===== MC-4.4c2e: damage divided among any number of targets =====
			["Pyrokinesis"] =
			[
				new MayCastFromHandFree(Is.Source)
				{
					Costs = [new ExileFromHandCost(Is.Color(ManaColor.Red))],
				},
				Spell([Creature with { Divide = 4 }], new DealDamage(Ref.EachTarget, Count.Share)),
			],
			// ===== MC-4.4c2d: entering from a zone, a die roll =====
			["\"Name Sticker\" Goblin"] =
			[
				When(
					TriggerEvent.SelfEnters with
					{
						NotFrom = [ZoneType.Graveyard, ZoneType.Exile],
					},
					[],
					new RollDie(
						20,
						[
							new(1, 6, [new AddMana(ManaColor.Red, 4)]),
							new(7, 14, [new AddMana(ManaColor.Red, 5)]),
							new(15, 20, [new AddMana(ManaColor.Red, 6)]),
						]
					)
				) with
				{
					// "if it's on the battlefield and you control 9 or fewer creatures named …" — a
					// permanent now: Ref.Source would read it as it last was once it is gone
					If = new Both(
						new Matches(Ref.Permanents(Is.Source), Is.AnyCard),
						new Not(
							new AtLeast(
								Count.NumberOf(
									Ref.Permanents(
										Is.Creature & Is.Yours & Is.Named("\"Name Sticker\" Goblin")
									)
								),
								10
							)
						)
					),
				},
			],
			// ===== MC-4.4d1: white's tier 1, tested pieces =====
			["Thraben Inspector"] = [When(TriggerEvent.SelfEnters, [], CreateClue(1))],
			["Cathar Commando"] =
			[
				new ActivatedAbilityComponent(ManaCost.Parse("{1}"), [new Destroy(Ref.Target())])
				{
					Costs = [new SacrificeSelfCost()],
					Targets = [Target(Is.Artifact | Is.Enchantment)],
				},
			],
			["Luminarch Aspirant"] =
			[
				When(
					TriggerEvent.BeginningOfCombatOnYourTurn,
					[Target(Is.Creature & Is.Yours)],
					new PutCounters(Ref.Target(), 1)
				),
			],
			["Agent Bishop, Man in Black"] =
			[
				When(
					TriggerEvent.BeginningOfCombatOnYourTurn,
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
					new PutCounters(Ref.EachTarget, 1)
				),
			],
			["Spectacular Spider-Man"] =
			[
				new ActivatedAbilityComponent(
					ManaCost.Parse("{1}"),
					[new ModifyUntilEndOfTurn(Ref.Source, 0, 0, "Flying")]
				),
				new ActivatedAbilityComponent(
					ManaCost.Parse("{1}"),
					[
						new ModifyUntilEndOfTurn(Ref.CreaturesYouControl, 0, 0, "Hexproof"),
						new ModifyUntilEndOfTurn(Ref.CreaturesYouControl, 0, 0, "Indestructible"),
					]
				)
				{
					Costs = [new SacrificeSelfCost()],
				},
			],
			["Stoneforge Mystic"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[],
					new SearchLibrary(CardType.Artifact, ZoneType.Hand)
					{
						Subtype = "Equipment",
						Reveal = true,
					}
				),
				new ActivatedAbilityComponent(
					ManaCost.Parse("{1}{W}"),
					[
						new MoveChosen(ZoneType.Hand, Is.Subtype("Equipment"), ZoneType.Battlefield)
						{
							May = true,
						},
					]
				)
				{
					Costs = [new TapSelfCost()],
				},
			],
			["Thalia, Guardian of Thraben"] = [new CostChange(1, !Is.Creature) { By = Whose.Any }],
			["Elite Spellbinder"] =
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
			["Loran of the Third Path"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[Target(Is.Artifact | Is.Enchantment) with { Optional = true }],
					new Destroy(Ref.Target())
				),
				new ActivatedAbilityComponent(
					ManaCost.None,
					[new DrawCards(Ref.You, 1), new DrawCards(Ref.Target(), 1)]
				)
				{
					Costs = [new TapSelfCost()],
					Targets = [TargetSpec.Player(Is.Opponents)],
				},
			],
			["Sanguine Evangelist"] =
			[
				// Battle cry — "each other attacking creature gets +1/+0 until end of turn"
				When(
					TriggerEvent.SelfAttacks,
					[],
					new ModifyUntilEndOfTurn(
						Ref.Permanents(Is.Creature & Is.Attacking & !Is.Source),
						1,
						0
					)
				),
				When(TriggerEvent.SelfEnters, [], new CreateTokens(Bat, 1)),
				When(TriggerEvent.SelfDies, [], new CreateTokens(Bat, 1)),
			],
			["Auriok Salvagers"] =
			[
				new ActivatedAbilityComponent(
					ManaCost.Parse("{1}{W}"),
					[new MoveTo(Ref.Target(), ZoneType.Hand) { From = [ZoneType.Graveyard] }]
				)
				{
					Targets = [TargetSpec.InYourGraveyard(Is.Artifact & Is.ManaValueAtMost(1))],
				},
			],
			["Reprieve"] =
			[
				Spell(
					[TargetSpec.Spell()],
					new MoveTo(Ref.Target(), ZoneType.Hand) { From = [ZoneType.Stack] },
					new DrawCards(Ref.You, 1)
				),
			],
			["Exorcise"] =
			[
				Spell(
					[Target(Is.Artifact | Is.Enchantment | (Is.Creature & Is.PowerAtLeast(4)))],
					new Exile(Ref.Target())
				),
			],
			["Portable Hole"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[Target(Is.Opponents & !Is.Land & Is.ManaValueAtMost(2))],
					new ExileUntilSourceLeaves(Ref.Target())
				),
			],
			["Leyline Binding"] =
			[
				// Domain — "costs {1} less to cast for each basic land type among lands you control"
				new CostChange(Count.BasicLandTypesAmongLandsYouControl.Negated)
				{
					OnItself = true,
				},
				When(
					TriggerEvent.SelfEnters,
					[Target(Is.Opponents & !Is.Land)],
					new ExileUntilSourceLeaves(Ref.Target())
				),
			],
			["Lingering Souls"] =
			[
				Spell([], new CreateTokens(Spirit, 2)),
				new FlashbackComponent(ManaCost.Parse("{1}{B}")),
			],
			["Touch the Spirit Realm"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[Target(Is.Artifact | Is.Creature) with { Optional = true }],
					new ExileUntilSourceLeaves(Ref.Target())
				),
				// Channel — "{1}{W}, Discard this card:"
				new ActivatedAbilityComponent(
					ManaCost.Parse("{1}{W}"),
					[new Flicker(Ref.Target()) { AtNextEndStep = true }]
				)
				{
					Costs = [new DiscardSelfCost()],
					From = ZoneType.Hand,
					Targets = [Target(Is.Artifact | Is.Creature)],
				},
			],

			["Glimmer Lens"] =
			[
				// For Mirrodin! — "create a 2/2 red Rebel creature token, then attach this to it"
				When(
					TriggerEvent.SelfEnters,
					[],
					new CreateTokens(Rebel, 1) { Then = [new AttachSourceTo(Ref.ThisWay)] }
				),
				When(
					TriggerEvent.AttachedCreatureAttacks with
					{
						// "and at least one other creature attack"
						While = new AtLeast(
							Count.NumberOf(Ref.Permanents(Is.Creature & Is.Attacking)),
							2
						),
					},
					[],
					new DrawCards(Ref.You, 1)
				),
				Equip("{1}{W}"),
			],
			["Staff of the Storyteller"] =
			[
				When(TriggerEvent.SelfEnters, [], new CreateTokens(Spirit, 1)),
				// ponytail: "create" is read as a creature token ENTERING under your control — a copy of a
				// creature spell resolving (Sage of the Skies) becomes a token without being created
				// (CR 608.3f) and counts here too; give CreateTokens its own event if a card tells them apart.
				When(
					new Trigger(EventKind.Entered)
					{
						Subject = Is.Creature & Is.Token & Is.Yours,
						OneOrMore = true,
					},
					[],
					new PutCounters(Ref.Source, 1) { Kind = CounterKinds.Story }
				),
				new ActivatedAbilityComponent(ManaCost.Parse("{W}"), [new DrawCards(Ref.You, 1)])
				{
					Costs =
					[
						new TapSelfCost(),
						new RemoveCountersCost(1, Is.Source) { Kind = CounterKinds.Story },
					],
				},
			],
			["Sage of the Skies"] =
			[
				When(TriggerEvent.SelfCast, [], new CopySpell(Ref.Source)) with
				{
					If = new AtLeast(Count.SpellsYouCastThisTurn, 2),
				},
			],
			["Enlightened Tutor"] =
			[
				Spell(
					[],
					new SearchLibrary(CardType.Artifact | CardType.Enchantment, ZoneType.Library)
					{
						Reveal = true,
					}
				),
			],
			// ===== MC-4.4d2a: white's options, zones and tokens =====
			["Oust"] =
			[
				// The life first: once the creature has left, "its controller" would read its owner.
				Spell(
					[Creature],
					new GainLife(Ref.ControllerOfTarget(0), 3),
					new MoveTo(Ref.Target(), ZoneType.Library) { Beneath = 1 }
				),
			],
			["Unexpectedly Absent"] =
			[
				Spell(
					[Target(!Is.Land)],
					new MoveTo(Ref.Target(), ZoneType.Library) { Beneath = Count.X }
				),
			],
			["Phelia, Exuberant Shepherd"] =
			[
				When(
					TriggerEvent.SelfAttacks,
					[Target(!Is.Land & !Is.Source) with { Optional = true }],
					new Flicker(Ref.Target())
					{
						AtNextEndStep = true,
						// In the delayed return "you" is still Phelia's controller, and Phelia is its EventObject.
						Then =
						[
							new If(
								new Matches(Ref.ThisWay, Is.Yours),
								[new PutCounters(Ref.EventObject, 1)]
							),
						],
					}
				),
			],
			["Elspeth, Storm Slayer"] =
			[
				new NumericReplacement(ReplacedEvent.Tokens) { Affected = Is.Yours, Times = 2 },
				Loyalty(1, [], new CreateTokens(Soldier, 1)),
				Loyalty(
					0,
					[],
					new PutCounters(Ref.CreaturesYouControl, 1),
					new ModifyUntilEndOfTurn(Ref.CreaturesYouControl, 0, 0, "Flying")
					{
						UntilYourNextTurn = true,
					}
				),
				Loyalty(
					-3,
					[Target(Is.Creature & Is.Opponents & Is.ManaValueAtLeast(3))],
					new Destroy(Ref.Target())
				),
			],
			["Containment Priest"] =
			[
				new PermanentsEnter(Is.Creature & !Is.Token) { ExiledUnlessCast = true },
			],
			["Enduring Innocence"] =
			[
				When(
					new Trigger(EventKind.Entered)
					{
						Subject = Is.Creature & Is.Yours & !Is.Source & Is.PowerAtMost(2),
						OneOrMore = true,
					},
					[],
					new DrawCards(Ref.You, 1)
				) with
				{
					OncePerTurn = true,
				},
				// "If it was a creature": only a creature dies — returned, it is none, and this never triggers again.
				When(
					TriggerEvent.SelfDies,
					[],
					new MoveTo(Ref.EventCard, ZoneType.Battlefield)
					{
						From = [ZoneType.Graveyard],
						Loses = CardType.Creature,
					}
				),
			],
			// Ravenous: enters with X counters; "if X is 5 or more, draw a card when it enters".
			["Jacked Rabbit"] =
			[
				new EntersWithCountersComponent(Count.X),
				When(TriggerEvent.SelfEnters, [], new DrawCards(Ref.You, 1)) with
				{
					If = new AtLeast(Count.X, 5),
				},
				When(
					TriggerEvent.SelfAttacks,
					[],
					new CreateTokens(Rabbit, Count.PowerOf(Ref.Source))
				),
			],

			// ===== MC-4.4d2b: white's options, restrictions and timing =====
			["Archon of Emeria"] =
			[
				new CantCastOrActivate { Abilities = false, SpellsEachTurn = 1 },
				new PermanentsEnter(Is.Land & !Is.Supertype(Supertype.Basic) & Is.Opponents)
				{
					Tapped = true,
				},
			],
			["Voice of Victory"] =
			[
				// Mobilize 2: the tokens enter tapped and attacking, and are sacrificed at the next end step.
				When(
					TriggerEvent.SelfAttacks,
					[],
					new CreateTokens(Warrior, 2)
					{
						Tapped = true,
						Attacking = true,
						SacrificeAtNextEndStep = true,
					}
				),
				new CantCastOrActivate
				{
					By = Whose.Opponent,
					Abilities = false,
					If = new DuringYourTurn(),
				},
			],
			["Clarion Conqueror"] =
			[
				// Mana abilities are activated abilities: no Mox, no Llanowar Elves — a land still taps.
				new CantCastOrActivate
				{
					Spells = false,
					ManaAbilitiesToo = true,
					Of = Is.Artifact | Is.Creature | Is.Planeswalker,
				},
			],
			["The Wandering Emperor"] =
			[
				new LoyaltyAtInstantSpeed(Is.EnteredThisTurn),
				Loyalty(
					1,
					[Creature with { Optional = true }],
					new PutCounters(Ref.Target(), 1),
					new ModifyUntilEndOfTurn(Ref.Target(), 0, 0, "First strike")
				),
				Loyalty(-1, [], new CreateTokens(Samurai, 1)),
				Loyalty(
					-2,
					[Target(Is.Creature & Is.Tapped)],
					new Exile(Ref.Target()),
					new GainLife(Ref.You, 2)
				),
			],
			["Anointed Peacekeeper"] =
			[
				// ponytail: a loyalty ability of the named card isn't taxed — it has no mana payment (DesignNotes).
				new ChoosesNameAsItEnters { LooksAtHand = true, AnyName = true },
				new CostChange(2, Is.NamedChosenName) { By = Whose.Opponent },
				new CostChange(2, Is.NamedChosenName) { Abilities = true },
			],
			// ===== MC-4.4d3: white's options, leaving the battlefield =====
			["Skyclave Apparition"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[
						Target(!Is.Land & !Is.Token & Is.Opponents & Is.ManaValueAtMost(4)) with
						{
							Optional = true,
						},
					],
					new Exile(Ref.Target()) { Linked = true }
				),
				// "the exiled card": this object's, asked from the ability's source as it was — it has left
				When(
					TriggerEvent.SelfLeaves,
					[],
					new CreateTokens(Illusion, 1)
					{
						ControlledBy = Ref.OwnerOf(Ref.All(ZoneType.Exile, Is.ExiledBySource)),
						Size = Count.GreatestManaValueOf(
							Ref.All(ZoneType.Exile, Is.ExiledBySource)
						),
					}
				),
			],
			["Serra Paragon"] =
			[
				// "play a land … or cast a permanent spell with mana value 3 or less": a land's is 0
				new MayPlayFromZone(
					ZoneType.Graveyard,
					Is.Type(PermanentTypes) & Is.ManaValueAtMost(3)
				)
				{
					OncePerTurn = true,
					Gains = When(
						TriggerEvent.SelfLeaves with
						{
							To = ZoneType.Graveyard,
						},
						[],
						new MoveTo(Ref.EventCard, ZoneType.Exile) { From = [ZoneType.Graveyard] },
						new GainLife(Ref.You, 2)
					),
				},
			],
			// ===== MC-4.4d4: white's options, the rest beyond the fewest =====
			["Balance"] =
			[
				// "the same way": lands, then cards in hand, then creatures — each counted as its turn comes,
				// so a land creature sacrificed as a land is no creature by then
				Spell(
					[],
					new EachSacrifices(Ref.EachPlayer, Is.Land)
					{
						N = Count.BeyondTheFewest(ZoneType.Battlefield, Is.Land),
					},
					new Discard(Ref.EachPlayer, Count.BeyondTheFewest(ZoneType.Hand)),
					new EachSacrifices(Ref.EachPlayer, Is.Creature)
					{
						N = Count.BeyondTheFewest(ZoneType.Battlefield, Is.Creature),
					}
				),
			],
		};

	/// <summary>Skyclave Apparition: "an X/X blue Illusion creature token" — its X is CreateTokens.Size.</summary>
	static CardData Illusion => Token("Illusion", "Creature — Illusion", "0", "0", ["U"]);

	/// <summary>Voice of Victory's mobilize: "1/1 red Warrior creature tokens".</summary>
	static CardData Warrior => Token("Warrior", "Creature — Warrior", "1", "1", ["R"]);

	/// <summary>The Wandering Emperor: "a 2/2 white Samurai creature token with vigilance".</summary>
	static CardData Samurai =>
		Token("Samurai", "Creature — Samurai", "2", "2", ["W"], ["Vigilance"]);

	/// <summary>Elspeth, Storm Slayer: "a 1/1 white Soldier creature token".</summary>
	static CardData Soldier => Token("Soldier", "Creature — Soldier", "1", "1", ["W"]);

	/// <summary>Jacked Rabbit: "1/1 white Rabbit creature tokens".</summary>
	static CardData Rabbit => Token("Rabbit", "Creature — Rabbit", "1", "1", ["W"]);

	/// <summary>Sanguine Evangelist: "a 1/1 black Bat creature token with flying".</summary>
	static CardData Bat => Token("Bat", "Creature — Bat", "1", "1", ["B"], ["Flying"]);

	/// <summary>Lingering Souls, Staff of the Storyteller: "a 1/1 white Spirit creature token with flying".</summary>
	static CardData Spirit => Token("Spirit", "Creature — Spirit", "1", "1", ["W"], ["Flying"]);

	/// <summary>Glimmer Lens' For Mirrodin!: "a 2/2 red Rebel creature token".</summary>
	static CardData Rebel => Token("Rebel", "Creature — Rebel", "2", "2", ["R"]);

	/// <summary>Cori-Steel Cutter: "a 1/1 white Monk creature token with prowess".</summary>
	static CardData Monk => Token("Monk", "Creature — Monk", "1", "1", ["W"]);

	/// <summary>Bonehoard Dracosaur: "a 3/1 red Dinosaur creature token".</summary>
	static CardData Dinosaur => Token("Dinosaur", "Creature — Dinosaur", "3", "1", ["R"]);

	/// <summary>Seasoned Pyromancer: "a 1/1 red Elemental creature token".</summary>
	static CardData Elemental => Token("Elemental", "Creature — Elemental", "1", "1", ["R"]);

	/// <summary>Legion Extruder: "a 3/3 colorless Golem artifact creature token".</summary>
	static CardData Golem => Token("Golem", "Artifact Creature — Golem", "3", "3");

	/// <summary>"Exile this card from your hand: Add {G}." — a mana ability of the card in hand.</summary>
	static ManaAbilityComponent SpiritGuide(ManaColor color) =>
		new(color) { From = ZoneType.Hand, Costs = [new ExileSelfCost()] };
}
