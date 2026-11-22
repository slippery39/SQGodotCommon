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
		SacrificeThis(null, tap: true, [Target(land)], new Destroy(Ref.Target()));

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
			May = true,
		};

	/// <summary>Pyroblast: "… if it's blue" — any spell or permanent is a legal target; only a blue one is hit.</summary>
	static If IfBlue(GameAction effect) =>
		new(new Matches(Ref.Target(), Is.Color(ManaColor.Blue)), [effect]);

	/// <summary>Brainstorm: "then put two cards from your hand on top of your library in any order" — one at a time, the second on top.</summary>
	static MoveChosen PutOnTop(string prompt) =>
		new(ZoneType.Hand, null, ZoneType.Library)
		{
			As = new(Ref.Chosen, ZoneType.Library) { OnTop = true },
			Prompt = prompt,
		};

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
					If = YouControlA(Is.Subtype("Swamp")),
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
						new ModifyUntilEndOfTurn(Ref.Target(), 0, 0)
						{
							ExileInsteadOf = [ZoneType.Graveyard],
						}
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
				LhurgoyfSize,
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
						Nth = 2,
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
								[new Attach(Ref.Source, Ref.ThisWay)]
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
						May = true,
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
					new CreateTokens(Rebel, 1) { Then = [new Attach(Ref.Source, Ref.ThisWay)] }
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
				OpponentsCantCastDuringYourTurn,
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
			// ===== MC-4.4e1: blue's tier 1, tested pieces =====
			["Mystical Tutor"] =
			[
				Spell(
					[],
					new SearchLibrary(CardType.Instant | CardType.Sorcery, ZoneType.Library)
					{
						Reveal = true,
					}
				),
			],
			["Spellseeker"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[],
					new SearchLibrary(CardType.Instant | CardType.Sorcery, ZoneType.Hand)
					{
						Filter = Is.ManaValueAtMost(2),
						FilterText = "mana value 2 or less",
						Reveal = true,
						May = true,
					}
				),
			],
			["Trinket Mage"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[],
					new SearchLibrary(CardType.Artifact, ZoneType.Hand)
					{
						Filter = Is.ManaValueAtMost(1),
						FilterText = "mana value 1 or less",
						Reveal = true,
						May = true,
					}
				),
			],
			["Tinker"] =
			[
				new AdditionalCostsComponent([new SacrificeCost(Is.Artifact)]),
				Spell([], new SearchLibrary(CardType.Artifact, ZoneType.Battlefield)),
			],
			["Snapcaster Mage"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[TargetSpec.InYourGraveyard(Is.Type(CardType.Instant | CardType.Sorcery))],
					new GainFlashbackThisTurn(Ref.Target())
				),
			],
			["Displacer Kitten"] =
			[
				When(
					TriggerEvent.YouCastNoncreatureSpell,
					[Target(!Is.Land & Is.Yours) with { Optional = true }],
					new Flicker(Ref.Target())
				),
			],
			["Boomerang Basics"] =
			[
				// "If you controlled that permanent": asked before it leaves, the card drawn after
				Spell(
					[Target(!Is.Land)],
					new If(
						new Matches(Ref.Target(0), Is.Yours),
						[new MoveTo(Ref.Target(), ZoneType.Hand), new DrawCards(Ref.You, 1)]
					)
					{
						Otherwise = [new MoveTo(Ref.Target(), ZoneType.Hand)],
					}
				),
			],
			["Upheaval"] = [Spell([], new MoveTo(Ref.Permanents(Is.AnyCard), ZoneType.Hand))],
			["Timetwister"] = [Timetwister],
			["Echo of Eons"] = [Timetwister, new FlashbackComponent(ManaCost.Parse("{2}{U}"))],
			["Cryogen Relic"] =
			[
				When(TriggerEvent.SelfEnters, [], new DrawCards(Ref.You, 1)),
				When(TriggerEvent.SelfLeaves, [], new DrawCards(Ref.You, 1)),
				new ActivatedAbilityComponent(
					ManaCost.Parse("{1}{U}"),
					[new PutCounters(Ref.Target(), 1) { Kind = CounterKinds.Stun }]
				)
				{
					Costs = [new SacrificeSelfCost()],
					Targets = [Target(Is.Creature & Is.Tapped) with { Optional = true }],
				},
			],
			["Sewer-veillance Cam"] =
			[
				// "enters or leaves": two triggers. "You may tap or untap": tapping a tapped creature (or
				// untapping an untapped one) does nothing, so the one real choice is whether to turn it.
				When(TriggerEvent.SelfEnters, [Creature], MayTapOrUntapTheTarget),
				When(TriggerEvent.SelfLeaves, [Creature], MayTapOrUntapTheTarget),
				new ActivatedAbilityComponent(ManaCost.Parse("{3}{U}"), [new DrawCards(Ref.You, 2)])
				{
					Costs = [new SacrificeSelfCost()],
				},
			],
			// ===== MC-4.4e2a: blue's options, the library's top =====
			["Ponder"] =
			[
				Spell(
					[],
					new LookAndTake(3) { Take = 0, Rest = TheRest.TopInAnyOrder },
					new May("Shuffle your library?", new Always(), [new ShuffleLibrary(Ref.You)]),
					new DrawCards(Ref.You, 1)
				),
			],
			["Stock Up"] = [Spell([], new LookAndTake(5) { Must = true, Take = 2 })],
			["Consult the Star Charts"] =
			[
				new KickerComponent(ManaCost.Parse("{1}{U}")),
				Spell(
					[],
					new If(
						new WasKicked(),
						[new LookAndTake(Count.LandsYouControl) { Must = true, Take = 2 }]
					)
					{
						Otherwise = [new LookAndTake(Count.LandsYouControl) { Must = true }],
					}
				),
			],
			["Thassa's Oracle"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[],
					new LookAndTake(Count.DevotionTo(ManaColor.Blue)) { To = ZoneType.Library },
					// "If X is greater than or equal to the number of cards in your library": at least 0 more.
					new If(
						new AtLeast(Count.DevotionTo(ManaColor.Blue), 0)
						{
							MoreThan = Count.NumberOf(Ref.All(ZoneType.Library, null, Ref.You)),
						},
						[new WinTheGame()]
					)
				),
			],
			// ===== MC-4.4e2b: blue's options, draws =====
			["Faerie Mastermind"] =
			[
				When(
					new Trigger(EventKind.Drew) { By = Whose.Opponent, Nth = 2 },
					[],
					new DrawCards(Ref.You, 1)
				),
				new ActivatedAbilityComponent(
					ManaCost.Parse("{3}{U}"),
					[new DrawCards(Ref.EachPlayer, 1)]
				),
			],
			["Hullbreacher"] =
			[
				new NumericReplacement(ReplacedEvent.Draw)
				{
					Affected = Is.Opponents,
					ExceptTheTurnsDraw = true,
					Instead = [CreateTreasure(1)],
				},
			],
			["Narset, Parter of Veils"] =
			[
				// "Can't draw more than one": once they have drawn one, nothing instead.
				new NumericReplacement(ReplacedEvent.Draw)
				{
					Affected = Is.Opponents,
					If = new AtLeast(Count.CardsYouDrewThisTurn with { Of = Ref.It }, 1),
					Instead = [],
				},
				Loyalty(-2, [], new LookAndTake(4, !Is.Creature & !Is.Land) { Reveal = true }),
			],
			["Jace, Wielder of Mysteries"] =
			[
				new NumericReplacement(ReplacedEvent.Draw)
				{
					Affected = Is.Yours,
					If = new YourLibraryIsEmpty(),
					Instead = [new WinTheGame()],
				},
				Loyalty(
					1,
					[TargetSpec.Player()],
					new Mill(Ref.Target(), 2),
					new DrawCards(Ref.You, 1)
				),
				Loyalty(
					-8,
					[],
					new DrawCards(Ref.You, 7),
					new If(new YourLibraryIsEmpty(), [new WinTheGame()])
				),
			],
			// ===== MC-4.4e2c: blue's options, mana and costs =====
			["Gitaxian Probe"] =
			[
				Spell(
					[TargetSpec.Player()],
					new LookAtHand(Ref.Target()),
					new DrawCards(Ref.You, 1)
				),
			],
			["Phyrexian Metamorph"] =
			[
				new MayEnterAsCopy
				{
					Of = Is.Artifact | Is.Creature,
					AlsoType = CardType.Artifact,
					Optional = true,
				},
			],
			["Urza, Lord High Artificer"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[],
					new CreateTokens(Construct, 1)
					{
						Abilities =
						[
							new StaticEffectComponent(Is.Source)
							{
								Power = Count.NumberOf(Ref.Permanents(Is.Artifact & Is.Yours)),
								Toughness = Count.NumberOf(Ref.Permanents(Is.Artifact & Is.Yours)),
							},
						],
					}
				),
				new ManaAbilityComponent(ManaColor.Blue)
				{
					Costs = [new TapUntappedCost(1, Is.Artifact & Is.Yours)],
				},
				new ActivatedAbilityComponent(
					ManaCost.Parse("{5}"),
					[
						new ExileTopPlayable
						{
							ShuffleFirst = true,
							Play = new() { ThisTurn = true, Free = true },
						},
					]
				),
			],
			["Forensic Gadgeteer"] =
			[
				When(
					new Trigger(EventKind.SpellCast) { By = Whose.You, Subject = Is.Artifact },
					[],
					CreateClue(1)
				),
				new CostChange(-1, Is.Artifact & Is.Yours) { Abilities = true, Floor = 1 },
			],
			["Thieving Skydiver"] =
			[
				new KickerComponent(ManaCost.Parse("{X}")) { MinX = 1 },
				When(
					TriggerEvent.SelfEnters,
					[Target(Is.Artifact & Is.ManaValueAtMost(Count.X))],
					new GainControl(Ref.Target(), Ref.You),
					new If(
						new Matches(Ref.Target(0), Is.Subtype("Equipment")),
						[new Attach(Ref.Target(), Ref.Source)]
					)
				) with
				{
					If = new WasKicked(),
				},
			],
			// ===== MC-4.4e2d: blue's options, casting off the hand =====
			["Bilbo, Thief in the Night"] =
			[
				new CostChange(-1) { NotFrom = [ZoneType.Hand] },
				When(
					TriggerEvent.SelfAttacks,
					[],
					new CastOneOf(
						Ref.All(ZoneType.Graveyard, Is.Artifact | Is.Instant | Is.Sorcery, Ref.You)
					)
					{
						Free = false,
						Exiled = Is.Instant | Is.Sorcery,
					}
				),
			],
			["Malcolm, Alluring Scoundrel"] =
			[
				When(
					TriggerEvent.SelfDealsCombatDamageToPlayer,
					[],
					new PutCounters(Ref.Source, 1) { Kind = CounterKinds.Chorus },
					new DrawCards(Ref.You, 1),
					new Discard(Ref.You, 1)
					{
						Then =
						[
							new If(
								new AtLeast(
									Count.CountersOfKind(Ref.Source, CounterKinds.Chorus),
									4
								),
								[new CastOneOf(Ref.Chosen)]
							),
						],
					}
				),
			],
			["Flash"] =
			[
				Spell(
					[],
					new MoveChosen(ZoneType.Hand, Is.Creature, ZoneType.Battlefield)
					{
						May = true,
						Then =
						[
							MayPay(
								"Pay its mana cost reduced by {2}?",
								new PayMana(ManaCost.None, [])
								{
									ManaCostOf = Ref.Chosen,
									ReducedBy = 2,
								},
								new Sacrifice(Ref.Chosen)
							),
						],
					}
				),
			],
			["Transmute Artifact"] = [TransmuteArtifact],
			// ===== MC-4.4e2e: blue's options, "for as long as this remains on the battlefield" =====
			["The Wondrous Wasp"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[Creature with { Optional = true }],
					new Tap(Ref.Target()),
					new ModifyUntilEndOfTurn(Ref.Target(), 0, 0)
					{
						LosesAllAbilities = true,
						WhileSourceRemains = true,
					}
				),
			],
			["Tishana's Tidebinder"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[TargetSpec.Ability() with { Optional = true }],
					new Counter(Ref.Target())
					{
						Then =
						[
							new If(
								new Matches(
									Ref.ThisWay,
									Is.Artifact | Is.Creature | Is.Planeswalker
								),
								[
									new ModifyUntilEndOfTurn(Ref.ThisWay, 0, 0)
									{
										LosesAllAbilities = true,
										WhileSourceRemains = true,
									},
								]
							),
						],
					}
				),
			],
			// ===== MC-4.4e2f: blue's options, choices after the cast =====
			["Frantic Search"] =
			[
				Spell(
					[],
					new DrawCards(Ref.You, 2),
					new Discard(Ref.You, 2),
					new Untap(Ref.Permanents(Is.Land)) { UpTo = 3 }
				),
			],
			["Time Spiral"] =
			[
				Spell(
					[],
					[
						new ExileSourceAndCopiesFromGraveyard(0), // "Exile Time Spiral."
						.. TimetwisterSteps,
						new Untap(Ref.Permanents(Is.Land)) { UpTo = 6 },
					]
				),
			],
			["Mystic Confluence"] =
			[
				ModalSpell(
					"Mystic Confluence",
					new()
					{
						AtLeast = 3,
						AtMost = 3,
						Repeats = true,
					},
					M(
						[TargetSpec.Spell()],
						new CounterUnlessPays(Ref.Target(), ManaCost.Parse("{3}"))
					),
					M([Creature], new MoveTo(Ref.Target(), ZoneType.Hand)),
					M([], new DrawCards(Ref.You, 1))
				),
			],
			["Occult Epiphany"] =
			[
				Spell(
					[],
					new DrawCards(Ref.You, Count.X),
					new Discard(Ref.You, Count.X)
					{
						Then = [new CreateTokens(Spirit, Count.CardTypesAmong(Ref.Chosen))],
					}
				),
			],
			// ===== MC-4.4f: blue's tier 2 =====
			["Emry, Lurker of the Loch"] =
			[
				// Affinity for artifacts: "This spell costs {1} less to cast for each artifact you control."
				new CostChange(Count.NumberOf(Ref.Permanents(Is.Artifact & Is.Yours)).Negated)
				{
					OnItself = true,
				},
				When(TriggerEvent.SelfEnters, [], new Mill(Ref.You, 4)),
				new ActivatedAbilityComponent(
					ManaCost.None,
					[new MakePlayable(Ref.Target(), new() { ThisTurn = true, CastOnly = true })]
				)
				{
					Costs = [new TapSelfCost()],
					Targets = [TargetSpec.InYourGraveyard(Is.Artifact)],
				},
			],
			["Sicarian Infiltrator"] =
			[
				.. Squad("{2}"),
				When(TriggerEvent.SelfEnters, [], new DrawCards(Ref.You, 1)),
			],
			["Subtlety"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[TargetSpec.Spell(Is.Creature | Is.Planeswalker) with { Optional = true }],
					new May(
						"Put it on top of your library? (No: the bottom.)",
						new Matches(Ref.Target(0), Is.AnyCard),
						[
							new MoveTo(Ref.Target(), ZoneType.Library)
							{
								From = [ZoneType.Stack],
								OnTop = true,
							},
						]
					)
					{
						Who = Ref.OwnerOfTarget(),
						Otherwise =
						[
							new MoveTo(Ref.Target(), ZoneType.Library) { From = [ZoneType.Stack] },
						],
					}
				),
				.. Evoke(ManaColor.Blue),
			],
			["Kappa Cannoneer"] =
			[
				Ward("{4}"),
				When(
					new Trigger(EventKind.Entered) { Subject = Is.Source | Is.Artifact & Is.Yours },
					[],
					new PutCounters(Ref.Source, 1),
					new ModifyUntilEndOfTurn(Ref.Source, 0, 0, Combat.CantBeBlocked)
				),
			],
			["Preordain"] =
			[
				Spell(
					[],
					new Surveil(2) { To = ZoneType.Library }, // scry 2
					new DrawCards(Ref.You, 1)
				),
			],
			["Lórien Revealed"] =
			[
				Spell([], new DrawCards(Ref.You, 3)),
				Cycling(
					"{1}",
					new SearchLibrary(CardType.Land, ZoneType.Hand)
					{
						Subtype = "Island",
						Reveal = true,
					}
				),
			],
			// ===== MC-4.4g1: protection =====
			["True-Name Nemesis"] =
			[
				new ChoosesPlayerAsItEnters(),
				new Protection(Is.ControlledByChosenPlayer),
			],
			// ===== MC-4.4g2: delve (CastSpellAction.Delve, read from the printed keyword) =====
			["Dig Through Time"] =
			[
				Spell(
					[],
					new LookAndTake(7)
					{
						Take = 2,
						Must = true,
						Rest = TheRest.BottomInAnyOrder,
					}
				),
			],
			["Treasure Cruise"] = [Spell([], new DrawCards(Ref.You, 3))],
			// ===== MC-4.4g3: dungeons and the initiative (Undercity, Effects/Dungeons.cs) =====
			["Feywild Caretaker"] =
			[
				When(TriggerEvent.SelfEnters, [], new TakeTheInitiative(Ref.You)),
				When(TriggerEvent.YourEndStep, [], new CreateTokens(FaerieDragon, 1)) with
				{
					If = new YouHaveTheInitiative(),
				},
			],
			// ===== MC-4.4g4: face-down permanents (Card.TurnedFaceDown, TurnFaceUpAction) =====
			["Abhorrent Oculus"] =
			[
				new AdditionalCostsComponent(
					[new ExileFromGraveyardCost(!Is.Source) { Count = 6 }]
				),
				When(TriggerEvent.Beginning(Step.Upkeep, Whose.Opponent), [], ManifestDread),
			],
			// ===== MC-4.4g5: Adventures (AdventureComponent, CastSpellAction.Adventure) =====
			["Brazen Borrower // Petty Theft"] =
			[
				new CanBlockOnly(Is.With("Flying")),
				Adventure(
					"Brazen Borrower // Petty Theft",
					[Target(!Is.Land & Is.Opponents)],
					new MoveTo(Ref.Target(), ZoneType.Hand)
				),
			],
			// ===== MC-4.4g6: teamwork (KickerComponent with TapUntappedCost.TotalPower) =====
			["We Say Thee Nay!"] =
			[
				Teamwork(2),
				Spell(
					[TargetSpec.Spell()],
					new If(
						new WasKicked(),
						[new CounterUnlessPays(Ref.Target(), ManaCost.Parse("{4}"))]
					)
					{
						Otherwise = [new CounterUnlessPays(Ref.Target(), ManaCost.Parse("{2}"))],
					}
				),
			],
			// ===== MC-4.4g7: storm (Scripting.Storm, CopySpell { N }, Count.SpellsCastBefore) =====
			["Brain Freeze"] = [Spell([TargetSpec.Player()], new Mill(Ref.Target(), 3)), Storm],
			// ===== MC-4.4g8: spree (Mode.Cost from the printed "+ {cost} —", targets word by word) =====
			["Three Steps Ahead"] =
			[
				ModalSpell(
					"Three Steps Ahead",
					new() { AtMost = 3 },
					M([TargetSpec.Spell()], new Counter(Ref.Target())),
					M(
						[Target((Is.Artifact | Is.Creature) & Is.Yours)],
						new CreateTokenCopy(Ref.Target())
					),
					M([], new DrawCards(Ref.You, 2), new Discard(Ref.You, 1))
				),
			],
			// ===== MC-4.4g9: warp (Scripting.Warp: MayCastFromHandFree { From, PaidAtNextEndStep }, PlayPermission.LaterTurn) =====
			["Quantum Riddler"] =
			[
				When(TriggerEvent.SelfEnters, [], new DrawCards(Ref.You, 1)),
				new NumericReplacement(ReplacedEvent.Draw)
				{
					Affected = Is.Yours,
					If = new Not(
						new AtLeast(Count.NumberOf(Ref.All(ZoneType.Hand, of: Ref.You)), 2)
					),
					Plus = 1,
				},
				Warp("{1}{U}"),
			],

			// ===== MC-4.4g10: Classes (ClassLevel, Scripting.Level / WhenLevel / AtLevel) =====
			["Does Machines"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[],
					new Mill(Ref.You, 2),
					new DrawCards(Ref.You, 2),
					new Discard(Ref.You, 2)
				),
				Level("{1}{U}", 2),
				WhenLevel(
					2,
					[
						TargetSpec.InYourGraveyard(Is.Artifact) with
						{
							Optional = true,
						},
						TargetSpec.InYourGraveyard(Is.Artifact) with
						{
							Optional = true,
							NotThePrevious = true,
						},
					],
					new MoveTo(Ref.EachTarget, ZoneType.Hand) { From = [ZoneType.Graveyard] }
				),
				Level("{4}{U}", 3),
				When(
					TriggerEvent.BeginningOfCombatOnYourTurn with
					{
						While = AtLevel(3),
					},
					[TargetSpec.Permanent(Is.Artifact & Is.Yours)],
					new PutCounters(Ref.Target(), 3),
					new If(
						new Not(new Matches(Ref.Target(), Is.Creature)),
						[
							new ModifyUntilEndOfTurn(Ref.Target(), 0, 0)
							{
								ForGood = true,
								SetBase = (0, 0),
								AlsoCreatureType = "Robot",
								AlsoType = CardType.Creature,
							},
						]
					)
				),
			],
			// ===== MC-4.4h1: black's tier 1, tested pieces =====
			["Dark Confidant"] =
			[
				When(
					TriggerEvent.YourUpkeep,
					[],
					new LookAndTake(1)
					{
						Must = true,
						Reveal = true,
						Then = [new LoseLife(Ref.You, Count.GreatestManaValueOf(Ref.ThisWay))],
					}
				),
			],
			["Iron-Shield Elf"] =
			[
				new ActivatedAbilityComponent(
					ManaCost.None,
					[
						new ModifyUntilEndOfTurn(Ref.Source, 0, 0, "Indestructible"),
						new Tap(Ref.Source),
					]
				)
				{
					Costs = [new DiscardCost()],
				},
			],
			["Sunset Saboteur"] =
			[
				// Ward—Discard a card (Gideon the Oathless' ward).
				When(
					TriggerEvent.SelfBecomesTargetOfOpponentsSpellOrAbility,
					[],
					new CounterUnlessPays(Ref.EventObject, ManaCost.None)
					{
						Costs = [new DiscardCost()],
					}
				),
				When(
					TriggerEvent.SelfAttacks,
					[Target(Is.Creature & Is.Opponents)],
					new PutCounters(Ref.Target(), 1)
				),
			],
			["Harvester of Misery"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[],
					new ModifyUntilEndOfTurn(Ref.Permanents(Is.Creature & !Is.Source), -2, -2)
				),
				new ActivatedAbilityComponent(
					ManaCost.Parse("{1}{B}"),
					[new ModifyUntilEndOfTurn(Ref.Target(), -2, -2)]
				)
				{
					Costs = [new DiscardSelfCost()],
					From = ZoneType.Hand,
					Targets = [Creature],
				},
			],
			["Archon of Cruelty"] =
			[
				When(TriggerEvent.SelfEnters, [TargetSpec.Player(Is.Opponents)], ArchonsCruelty),
				When(TriggerEvent.SelfAttacks, [TargetSpec.Player(Is.Opponents)], ArchonsCruelty),
			],
			["Griselbrand"] =
			[
				new ActivatedAbilityComponent(ManaCost.None, [new DrawCards(Ref.You, 7)])
				{
					Costs = [new PayLifeCost(7)],
				},
			],
			["Grief"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[TargetSpec.Player(Is.Opponents)],
					new YouChooseTheyDiscard(Ref.Target(), NonlandTypes)
				),
				.. Evoke(ManaColor.Black),
			],
			["Entomb"] = [Spell([], new SearchLibrary(CardType.None, ZoneType.Graveyard))],
			["Demonic Tutor"] = [Spell([], new SearchLibrary(CardType.None, ZoneType.Hand))],
			// Mystical Tutor's search: "then shuffle and put that card on top".
			["Vampiric Tutor"] =
			[
				Spell(
					[],
					new SearchLibrary(CardType.None, ZoneType.Library),
					new LoseLife(Ref.You, 2)
				),
			],
			["Imperial Seal"] =
			[
				Spell(
					[],
					new SearchLibrary(CardType.None, ZoneType.Library),
					new LoseLife(Ref.You, 2)
				),
			],
			["Sheoldred's Edict"] =
			[
				ModalSpell(
					"Sheoldred's Edict",
					1,
					M([], new EachSacrifices(Ref.EachOpponent, Is.Creature & !Is.Token)),
					M([], new EachSacrifices(Ref.EachOpponent, Is.Creature & Is.Token)),
					M([], new EachSacrifices(Ref.EachOpponent, Is.Planeswalker))
				),
			],
			["Dismember"] = [Spell([Creature], new ModifyUntilEndOfTurn(Ref.Target(), -5, -5))],
			["Thoughtseize"] =
			[
				Spell(
					[TargetSpec.Player()],
					new YouChooseTheyDiscard(Ref.Target(), NonlandTypes),
					new LoseLife(Ref.You, 2)
				),
			],
			["Hymn to Tourach"] =
			[
				Spell([TargetSpec.Player()], new DiscardAtRandom(Ref.Target(), 2)),
			],
			["Reanimate"] =
			[
				// The loss first: the card that enters is a new object (the same number).
				Spell(
					[TargetSpec.InAnyGraveyard(Is.Creature)],
					new LoseLife(Ref.You, Count.GreatestManaValueOf(Ref.Target())),
					new MoveTo(Ref.Target(), ZoneType.Battlefield)
					{
						From = [ZoneType.Graveyard],
						UnderYourControl = true,
					}
				),
			],
			["Unearth"] =
			[
				Spell(
					[TargetSpec.InYourGraveyard(Is.Creature & Is.ManaValueAtMost(3))],
					new MoveTo(Ref.Target(), ZoneType.Battlefield) { From = [ZoneType.Graveyard] }
				),
				Cycling("{2}"),
			],
			// ===== MC-4.4h2a: hands (YouChooseTheyDiscard { Filter, UntilSourceLeaves }, escalate, a copy by another) =====
			["Inquisition of Kozilek"] =
			[
				Spell(
					[TargetSpec.Player()],
					new YouChooseTheyDiscard(Ref.Target(), CardType.None)
					{
						Filter = !Is.Land & Is.ManaValueAtMost(3),
					}
				),
			],
			["Duress"] =
			[
				Spell(
					[TargetSpec.Player(Is.Opponents)],
					new YouChooseTheyDiscard(Ref.Target(), CardType.None)
					{
						Filter = !Is.Creature & !Is.Land,
					}
				),
			],
			["Deep-Cavern Bat"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[TargetSpec.Player(Is.Opponents)],
					new YouChooseTheyDiscard(Ref.Target(), CardType.None)
					{
						Filter = !Is.Land,
						Optional = true,
						UntilSourceLeaves = true,
					}
				),
			],
			["Collective Brutality"] =
			[
				ModalSpell(
					"Collective Brutality",
					new() { AtMost = 3 },
					M(
						[TargetSpec.Player(Is.Opponents)],
						new YouChooseTheyDiscard(Ref.Target(), CardType.Instant | CardType.Sorcery)
					),
					M([Creature], new ModifyUntilEndOfTurn(Ref.Target(), -2, -2)),
					M(
						[TargetSpec.Player(Is.Opponents)],
						new LoseLife(Ref.Target(), 2),
						new GainLife(Ref.You, 2)
					)
				) with
				{
					Escalate = new DiscardCost(),
				},
			],
			["Chain of Smog"] =
			[
				// ponytail: "that player may copy this spell" is asked only while their opponent has a card in hand (DesignNotes).
				Spell(
					[TargetSpec.Player()],
					new Discard(Ref.Target(), 2),
					new May(
						"Copy Chain of Smog?",
						new AtLeast(
							Count.NumberOf(Ref.All(ZoneType.Hand, Is.AnyCard, Ref.EachOpponent)),
							1
						),
						[new CopySpell(Ref.Source)]
					)
					{
						Who = Ref.Target(),
						ThenAsThem = true,
					}
				),
			],
			// ===== MC-4.4h2b: graveyard returns (Ref.All { Top }, MoveChosen { Who, As } from exile, +1/+1 counters put) =====
			["Shallow Grave"] = [Spell([], ShallowGrave)],
			["Corpse Dance"] =
			[
				// Buyback is kicker by another name: an optional additional cost, "if kicked" put back into the hand.
				new KickerComponent(ManaCost.Parse("{2}")) { Word = "buyback" },
				Spell(
					[],
					ShallowGrave,
					new If(
						new WasKicked(),
						[new MoveTo(Ref.Source, ZoneType.Hand) { From = [ZoneType.Stack] }]
					)
				),
			],
			["Exhume"] =
			[
				Spell(
					[],
					new MoveChosen(ZoneType.Graveyard, Is.Creature, ZoneType.Battlefield)
					{
						Who = Ref.EachPlayer,
					}
				),
			],
			["Emperor of Bones"] =
			[
				When(
					TriggerEvent.BeginningOfCombatOnYourTurn,
					[TargetSpec.InAnyGraveyard() with { Optional = true }],
					new Exile(Ref.Target()) { Linked = true }
				),
				// Adapt 2 (CR 701.46): "If this creature has no +1/+1 counters on it, put two +1/+1 counters on it."
				new ActivatedAbilityComponent(
					ManaCost.Parse("{1}{B}"),
					[
						new If(
							new Not(new AtLeast(Count.CountersOn(Ref.Source), 1)),
							[new PutCounters(Ref.Source, 2)]
						),
					]
				),
				When(
					new Trigger(EventKind.CountersPut)
					{
						Subject = Is.Source,
						Counters = CounterKinds.PlusOne,
					},
					[],
					new MoveChosen(
						ZoneType.Exile,
						Is.Creature & Is.ExiledBySource,
						ZoneType.Battlefield
					)
					{
						As = new(Ref.Chosen, ZoneType.Battlefield)
						{
							UnderYourControl = true,
							Finality = true,
						},
						Then =
						[
							new ModifyUntilEndOfTurn(Ref.Chosen, 0, 0, "Haste") { ForGood = true },
							new AtNextEndStep([new Sacrifice(Ref.Source)]) { On = Ref.Chosen },
						],
					}
				),
			],
			["Barrowgoyf"] =
			[
				LhurgoyfSize,
				When(
					TriggerEvent.SelfDealsCombatDamageToPlayer,
					[],
					new May(
						"Mill that many cards?",
						new Always(),
						[new MillAndMayTake(Count.X, CardType.Creature)]
					)
				),
			],
			// ===== MC-4.4h2c: costs and permissions (PayLifeCost counted, X from a cost, MayPlayFromZone.Free, a mana ability without {T}, a "this turn" exile-instead, entering with any kind) =====
			["Toxic Deluge"] =
			[
				new AdditionalCostsComponent([new PayLifeCost(Count.X)]),
				Spell(
					[],
					new ModifyUntilEndOfTurn(
						Ref.Permanents(Is.Creature),
						Count.X with
						{
							Sign = -1,
						},
						Count.X with
						{
							Sign = -1,
						}
					)
				),
			],
			["Bolas's Citadel"] =
			[
				new MayLookAtTopOfLibrary(),
				new MayPlayFromZone(ZoneType.Library, Is.AnyCard)
				{
					AnyTime = true,
					Free = true,
					Costs = [new PayLifeCost(Count.GreatestManaValueOf(Ref.Source))],
				},
				new ActivatedAbilityComponent(ManaCost.None, [new LoseLife(Ref.EachOpponent, 10)])
				{
					Costs = [new TapSelfCost(), new SacrificeCost(!Is.Land) { Count = 10 }],
				},
			],
			["Rain of Filth"] =
			[
				Spell(
					[],
					new ModifyUntilEndOfTurn(Ref.Permanents(Is.Land & Is.Yours), 0, 0)
					{
						Gains =
						[
							new ManaAbilityComponent(ManaColor.Black)
							{
								Costs = [new SacrificeSelfCost()],
							},
						],
					}
				),
			],
			["Yawgmoth's Will"] =
			[
				Spell(
					[],
					new ThisTurn(new MayPlayFromZone(ZoneType.Graveyard, Is.AnyCard)),
					new ThisTurn(new ExileInsteadOfYourGraveyard { What = !Is.Token })
				),
			],
			["Wishclaw Talisman"] =
			[
				new EntersWithCountersComponent(3) { Kind = CounterKinds.Wish },
				new ActivatedAbilityComponent(
					ManaCost.Parse("{1}"),
					[
						new SearchLibrary(CardType.None, ZoneType.Hand),
						new GainControl(Ref.Source, Ref.EachOpponent),
					]
				)
				{
					Costs =
					[
						new TapSelfCost(),
						new RemoveCountersCost(1, Is.Source) { Kind = CounterKinds.Wish },
					],
					OnlyIf = new DuringYourTurn(),
				},
			],
			// ===== MC-4.4h2d: triggers (a copy's event, the turn's draw, odd or even, an exile Then decides) =====
			["Sedgemoor Witch"] =
			[
				When(
					TriggerEvent.SelfBecomesTargetOfOpponentsSpellOrAbility,
					[],
					new CounterUnlessPays(Ref.EventObject, ManaCost.None)
					{
						Costs = [new PayLifeCost(3)],
					}
				),
				.. Magecraft(
					new CreateTokens(Pest, 1)
					{
						Abilities = [When(TriggerEvent.SelfDies, [], new GainLife(Ref.You, 1))],
					}
				),
			],
			["Orcish Bowmasters"] =
			[
				When(TriggerEvent.SelfEnters, [TargetSpec.AnyTarget()], BowmastersShot),
				When(
					new Trigger(EventKind.Drew)
					{
						By = Whose.Opponent,
						Also = EventTest.NotTheTurnsDraw,
					},
					[TargetSpec.AnyTarget()],
					BowmastersShot
				),
			],
			["Gollum, Riddle Master"] =
			[
				new ChoosesNumberAsItEnters(1) { OddOrEven = true },
				ModalWhen(
					"Gollum, Riddle Master",
					new Trigger(EventKind.SpellCast)
					{
						By = Whose.Opponent,
						Subject = Is.ManaValueIsChosenNumber,
					},
					new() { NotChosenBefore = true },
					M([], new PutCounters(Ref.Source, 1)),
					M([], new LoseLife(Ref.EachOpponent, 2), new GainLife(Ref.You, 2)),
					M([], new DrawCards(Ref.You, 1))
				),
			],
			["Black Widow, Super Spy"] =
			[
				When(
					TriggerEvent.SelfDealsCombatDamageToPlayer,
					[],
					new ExileTopPlayable
					{
						Of = Ref.EachOpponent, // "that player": the one dealt the damage, the opponent
						Until = !Is.Land,
						Play = null, // decided below
						Then =
						[
							// Gone, she can't have the counter: you didn't, so you may cast it.
							new If(
								new Matches(Ref.Permanents(Is.Source), Is.Creature),
								[
									new May(
										"Put a +1/+1 counter on Black Widow?",
										new Always(),
										[new PutCounters(Ref.Source, 1)]
									)
									{
										Otherwise = [WidowsCast],
									},
								]
							)
							{
								Otherwise = [WidowsCast],
							},
						],
					}
				),
			],
			// ===== MC-4.4h2e: zones and combat (revolt, three blockers, shadow and void counters, escape) =====
			["Fatal Push"] =
			[
				Spell(
					[Creature],
					new If(
						new Matches(Ref.Target(), Is.ManaValueAtMost(2)),
						[new Destroy(Ref.Target())]
					)
					{
						Otherwise =
						[
							new If(
								new Both(
									new AtLeast(
										Count.PermanentsLeftTheBattlefieldThisTurn(Ref.You),
										1
									),
									new Matches(Ref.Target(), Is.ManaValueAtMost(4))
								),
								[new Destroy(Ref.Target())]
							),
						],
					}
				),
			],
			["Troll of Khazad-dûm"] =
			[
				new MinimumBlockers(3),
				Cycling(
					"{1}",
					new SearchLibrary(CardType.Land, ZoneType.Hand)
					{
						Subtype = "Swamp",
						Reveal = true,
					}
				),
			],
			["Dauthi Voidwalker"] =
			[
				.. Shadow,
				new ExileInsteadOfYourGraveyard
				{
					Whose = Is.Opponents,
					What = !Is.Token,
					CounterKind = CounterKinds.Void,
				},
				new ActivatedAbilityComponent(
					ManaCost.None,
					[
						new MoveChosen(
							ZoneType.Exile,
							Is.Opponents & Is.WithCounter(CounterKinds.Void),
							ZoneType.Exile
						)
						{
							Prompt = "Choose an exiled card with a void counter",
							Then =
							[
								new MakePlayable(
									Ref.Chosen,
									new() { ThisTurn = true, Free = true }
								),
							],
						},
					]
				)
				{
					Costs = [new TapSelfCost(), new SacrificeSelfCost()],
				},
			],
			["From the Catacombs"] =
			[
				Spell(
					[TargetSpec.InAnyGraveyard(Is.Creature)],
					new MoveTo(Ref.Target(), ZoneType.Battlefield)
					{
						From = [ZoneType.Graveyard],
						UnderYourControl = true,
						Then =
						[
							// ponytail: the corpse counter is put on after it enters, not "with" it (DesignNotes)
							new PutCounters(Ref.ThisWay, 1) { Kind = CounterKinds.Corpse },
							new ModifyUntilEndOfTurn(Ref.ThisWay, 0, 0)
							{
								ForGood = true,
								ExileInsteadOf =
								[
									ZoneType.Graveyard,
									ZoneType.Hand,
									ZoneType.Library,
								],
							},
						],
					},
					new TakeTheInitiative(Ref.You)
				),
				new MayCastThisFrom(ZoneType.Graveyard)
				{
					Costs = [new ExileFromGraveyardCost(!Is.Source) { Count = 5 }],
				},
			],
			// ===== MC-4.4h3: overload (an alternative cost that removes the target words) =====
			["Damn"] =
			[
				Overload("{2}{W}{W}"),
				// "A creature destroyed this way can't be regenerated": the engine has no regeneration (Wrath of God's note).
				Spell(
					[Creature],
					new If(new AlternativeCostWasPaid(), [new Destroy(Ref.Permanents(Is.Creature))])
					{
						Otherwise = [new Destroy(Ref.Target())],
					}
				),
			],
			// ===== MC-4.4h4: splice onto Arcane (CastSpellAction.Splice, Spliced) =====
			["Goryo's Vengeance"] =
			[
				Spell(
					[TargetSpec.InYourGraveyard(Is.Creature & Is.Legendary)],
					new MoveTo(Ref.Target(), ZoneType.Battlefield)
					{
						From = [ZoneType.Graveyard],
						ExileAtNextEndStep = true,
						// "That creature gains haste." — no duration: while it stays that permanent
						Then =
						[
							new ModifyUntilEndOfTurn(Ref.ThisWay, 0, 0, "Haste") { ForGood = true },
						],
					}
				),
				SpliceOntoArcane("{2}{B}"),
			],
			// ===== MC-4.4h5: Auras that return a creature from a graveyard (an Aura on a graveyard card, its enchant changed) =====
			["Animate Dead"] =
			[
				new AuraComponent(TargetSpec.InAnyGraveyard(Is.Creature)),
				When(
					TriggerEvent.SelfEnters,
					[],
					new ModifyUntilEndOfTurn(Ref.Source, 0, 0)
					{
						ForGood = true,
						Enchant = TargetSpec.Creature,
					},
					new MoveTo(Ref.EnchantedCreature, ZoneType.Battlefield)
					{
						From = [ZoneType.Graveyard],
						UnderYourControl = true,
						Then = [new Attach(Ref.Source, Ref.ThisWay)],
					}
				) with
				{
					// "if it's on the battlefield" — a permanent now (Ref.Source would read it as it last was)
					If = new Matches(Ref.Permanents(Is.Source), Is.AnyCard),
				},
				When(TriggerEvent.SelfLeaves, [], new Sacrifice(Ref.EnchantedCreature)),
				AttachedGets(-1, 0),
			],
			["Necromancy"] =
			[
				new CastAsThoughItHadFlash([new Sacrifice(Ref.Source)]),
				When(
					TriggerEvent.SelfEnters,
					[TargetSpec.InAnyGraveyard(Is.Creature)],
					new ModifyUntilEndOfTurn(Ref.Source, 0, 0)
					{
						ForGood = true,
						Enchant = TargetSpec.Creature,
					},
					new MoveTo(Ref.Target(), ZoneType.Battlefield)
					{
						From = [ZoneType.Graveyard],
						UnderYourControl = true,
						Then = [new Attach(Ref.Source, Ref.ThisWay)],
					}
				) with
				{
					If = new Matches(Ref.Permanents(Is.Source), Is.AnyCard),
				},
				When(TriggerEvent.SelfLeaves, [], new Sacrifice(Ref.EnchantedCreature)),
			],
			// ===== MC-4.4h6: naming a card as a spell resolves (ChooseCardName: the name on the spell on the stack) =====
			["Demonic Consultation"] =
			[
				Spell(
					[],
					new ChooseCardName { YourOwn = true },
					new ExileTopPlayable { N = 6, Play = null },
					new RevealUntilOntoBattlefield(CardType.None)
					{
						Matching = Is.NamedChosenName,
						To = ZoneType.Hand,
						Exile = true,
					}
				),
			],
			// ===== MC-4.4h7: saddle (teamwork's total-power tap cost on a sorcery-speed ability; "saddled" until end of turn) =====
			["Caustic Bronco"] =
			[
				When(
					TriggerEvent.SelfAttacks,
					[],
					new LookAndTake(1)
					{
						Must = true,
						Reveal = true,
						Then =
						[
							new If(
								new Matches(Ref.Source, Saddled),
								[
									new LoseLife(
										Ref.EachOpponent,
										Count.GreatestManaValueOf(Ref.ThisWay)
									),
								]
							)
							{
								Otherwise =
								[
									new LoseLife(Ref.You, Count.GreatestManaValueOf(Ref.ThisWay)),
								],
							},
						],
					}
				),
				Saddle(3),
			],
			// ===== MC-4.4h8: emerge (an alternative cost reduced by the sacrificed's mana value); three linked exiles =====
			["Crabomination"] =
			[
				Emerge("{5}{B}{B}", Is.Artifact),
				When(
					TriggerEvent.SelfEnters,
					[TargetSpec.Player(Is.Opponents)],
					new ExileTopPlayable
					{
						Of = Ref.Target(),
						Play = null,
						Linked = true,
					},
					new ExileTopPlayable
					{
						Of = Ref.Target(),
						From = ZoneType.Graveyard,
						AtRandom = true,
						Play = null,
						Linked = true,
					},
					new ExileTopPlayable
					{
						Of = Ref.Target(),
						From = ZoneType.Hand,
						AtRandom = true,
						Play = null,
						Linked = true,
					},
					new CastOneOf(Ref.All(ZoneType.Exile, Is.ExiledBySource, Ref.Target()))
				),
			],
			// ===== MC-4.4h9: miracle (a trigger from the hand on the card's own draw; a cast paying the miracle cost) =====
			["Metamorphosis Fanatic"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[TargetSpec.InYourGraveyard(Is.Creature) with { Optional = true }],
					new MoveTo(Ref.Target(), ZoneType.Battlefield)
					{
						From = [ZoneType.Graveyard],
						Then = [new PutCounters(Ref.ThisWay, 1) { Kind = CounterKinds.Lifelink }],
					}
				),
				Miracle("{1}{B}"),
			],
			// ===== MC-4.4i1: red's tested pieces =====
			["Embereth Shieldbreaker // Battle Display"] =
			[
				Adventure(
					"Embereth Shieldbreaker // Battle Display",
					[Target(Is.Artifact)],
					new Destroy(Ref.Target())
				),
			],
			["Oliphaunt"] =
			[
				When(
					TriggerEvent.SelfAttacks,
					[Target(Is.Creature & Is.Yours & !Is.Source)],
					new ModifyUntilEndOfTurn(Ref.Target(), 2, 0, "Trample")
				),
				Cycling(
					"{1}",
					new SearchLibrary(CardType.Land, ZoneType.Hand)
					{
						Subtype = "Mountain",
						Reveal = true,
					}
				),
			],
			["Unholy Heat"] =
			[
				Spell(
					[CreatureOrPlaneswalker],
					new If(Delirium, [new DealDamage(Ref.Target(), 6)])
					{
						Otherwise = [new DealDamage(Ref.Target(), 2)],
					}
				),
			],
			["Through the Breach"] =
			[
				Spell(
					[],
					new MoveChosen(ZoneType.Hand, Is.Creature, ZoneType.Battlefield)
					{
						May = true,
						Then =
						[
							// "That creature gains haste." — no duration: while it stays that permanent
							new ModifyUntilEndOfTurn(Ref.Chosen, 0, 0, "Haste") { ForGood = true },
							new AtNextEndStep([new Sacrifice(Ref.Source)]) { On = Ref.Chosen },
						],
					}
				),
				SpliceOntoArcane("{2}{R}{R}"),
			],
			// "Choose a Background" has no rules force outside a Commander game — no Background is in the cube.
			["Gut, True Soul Zealot"] =
			[
				When(
					TriggerEvent.YouAttack,
					[],
					new MaySacrifice(
						Is.Creature & !Is.Source | Is.Artifact,
						[new CreateTokens(Skeleton, 1) { Tapped = true, Attacking = true }]
					)
				),
			],
			["Headliner Scarlett"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[TargetSpec.Player()],
					new ModifyUntilEndOfTurn(
						Ref.PermanentsOfTargetPlayer(CardType.Creature, 0),
						0,
						0,
						Combat.CantBlock
					)
				),
				When(
					TriggerEvent.YourUpkeep,
					[],
					new ExileTopPlayable
					{
						FaceDown = true,
						Play = new() { ThisTurn = true },
					}
				),
			],
			// ===== MC-4.4i2a: red's options, triggers and combat =====
			["Fury"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[CreatureOrPlaneswalker with { Divide = 4 }],
					new DealDamage(Ref.EachTarget, Count.Share)
				),
				.. Evoke(ManaColor.Red),
			],
			["Ivora, Insatiable Heir"] =
			[
				When(TriggerEvent.SelfEnters, [], CreateBlood(1)),
				When(TriggerEvent.SelfDealsCombatDamageToPlayer, [], CreateBlood(1)),
				When(
					new Trigger(EventKind.Discarded) { By = Whose.You },
					[],
					new PutCounters(Ref.Source, 1)
				),
			],
			["Dragon's Rage Channeler"] =
			[
				When(TriggerEvent.YouCastNoncreatureSpell, [], new Surveil(1)),
				new StaticEffectComponent(Is.Source, "Flying")
				{
					Power = 2,
					Toughness = 2,
					If = Delirium,
				},
				new StaticEffectComponent(Is.Source, Combat.AttacksEachCombat) { If = Delirium },
			],
			["Caves of Chaos Adventurer"] =
			[
				When(TriggerEvent.SelfEnters, [], new TakeTheInitiative(Ref.You)),
				When(
					TriggerEvent.SelfAttacks,
					[],
					new ExileTopPlayable
					{
						Play = null,
						Then =
						[
							new If(
								new YouCompletedADungeon(),
								[
									new MakePlayable(
										Ref.ThisWay,
										new() { ThisTurn = true, Free = true }
									),
								]
							)
							{
								Otherwise =
								[
									new MakePlayable(Ref.ThisWay, new() { ThisTurn = true }),
								],
							},
						],
					}
				),
			],
			// ===== MC-4.4i2b + i3: multikicker, dash, backup, an additional combat phase =====
			["Batroc the Leaper"] =
			[
				Multikicker("{2}"),
				new EntersWithCountersComponent(Count.TimesReplicated(Ref.Source)),
				When(
					TriggerEvent.SelfEnters,
					[
						TargetSpec.AnyTarget() with
						{
							PerX = Count.TimesReplicated(Ref.Source),
							Optional = true,
						},
					],
					new DealDamage(Ref.EachTarget, Count.PowerOf(Ref.Source))
				),
			],
			["Ragavan, Nimble Pilferer"] =
			[
				When(
					TriggerEvent.SelfDealsCombatDamageToPlayer,
					[],
					CreateTreasure(1),
					new ExileTopPlayable
					{
						Of = Ref.EachOpponent, // "that player": the one dealt the damage, the opponent
						Play = new() { ThisTurn = true, CastOnly = true },
					}
				),
				.. Dash("{1}{R}"),
			],
			["Death-Greeter's Champion"] = [.. Dash("{3}{R}"), Backup(1, "Double strike")],
			["Fear of Missing Out"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[],
					new Discard(Ref.You, 1),
					new DrawCards(Ref.You, 1)
				),
				When(
					TriggerEvent.SelfAttacks with
					{
						Nth = 1,
					},
					[Creature],
					new Untap(Ref.Target()),
					new AdditionalCombatPhase()
				) with
				{
					If = Delirium,
				},
			],
			// ===== MC-4.4i4–i6: counters on a player (energy, poison), transforming double-faced cards =====
			["Galvanic Discharge"] =
			[
				Spell(
					[CreatureOrPlaneswalker],
					new PutCounters(Ref.You, 3) { Kind = CounterKinds.Energy },
					new PayAnyAmount
					{
						Kind = CounterKinds.Energy,
						Prompt = "Pay how much {E}? Galvanic Discharge deals that much damage.",
						Then = [new DealDamage(Ref.Target(), Count.AmountPaid)],
					}
				),
			],
			["Fable of the Mirror-Breaker // Reflection of Kiki-Jiki"] =
			[
				new SagaComponent(3),
				Chapter(
					1,
					[],
					new CreateTokens(GoblinShaman, 1)
					{
						Abilities = [When(TriggerEvent.SelfAttacks, [], CreateTreasure(1))],
					}
				),
				Chapter(
					2,
					[],
					new Discard(Ref.You, 2)
					{
						UpTo = true,
						Then = [new DrawCards(Ref.You, Count.NumberOf(Ref.Chosen))],
					}
				),
				Chapter(
					3,
					[],
					new MoveTo(Ref.Source, ZoneType.Exile)
					{
						Then =
						[
							new MoveTo(Ref.ThisWay, ZoneType.Battlefield)
							{
								UnderYourControl = true,
								Transformed = true,
							},
						],
					}
				),
				BackFace(
					"Fable of the Mirror-Breaker // Reflection of Kiki-Jiki",
					new ActivatedAbilityComponent(
						ManaCost.Parse("{1}"),
						[
							new CreateTokenCopy(Ref.Target())
							{
								AddingKeywords = ["Haste"],
								SacrificeAtNextEndStep = true,
							},
						]
					)
					{
						Costs = [new TapSelfCost()],
						Targets = [Target(Is.Creature & Is.Yours & !Is.Source & !Is.Legendary)],
					}
				),
			],
			["Etali, Primal Conqueror // Etali, Primal Sickness"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[],
					new ExileTopPlayable
					{
						Of = Ref.You,
						Until = !Is.Land,
						Play = null,
						Linked = true,
					},
					new ExileTopPlayable
					{
						Of = Ref.EachOpponent,
						Until = !Is.Land,
						Play = null,
						Linked = true,
					},
					new CastOneOf(
						Ref.All(ZoneType.Exile, Is.ExiledBySource & !Is.Land, Ref.EachPlayer)
					)
					{
						AnyNumber = true,
					}
				),
				new ActivatedAbilityComponent(
					ManaCost.Parse("{9}{G/P}"),
					[new Transform(Ref.Source)]
				)
				{
					SorcerySpeed = true,
				},
				BackFace(
					"Etali, Primal Conqueror // Etali, Primal Sickness",
					When(
						TriggerEvent.SelfDealsCombatDamageToPlayer,
						[],
						new PutCounters(Ref.EachOpponent, Count.X) { Kind = CounterKinds.Poison }
					)
				),
			],
			// ===== MC-4.4i7: the blue and black DFCs every survey missed; a modal DFC =====
			["Tamiyo, Inquisitive Student // Tamiyo, Seasoned Scholar"] =
			[
				When(TriggerEvent.SelfAttacks, [], CreateClue(1)),
				When(
					new Trigger(EventKind.Drew) { By = Whose.You, Nth = 3 },
					[],
					ReturnTransformed
				),
				BackFace(
					"Tamiyo, Inquisitive Student // Tamiyo, Seasoned Scholar",
					Loyalty(
						2,
						[],
						new UntilYourNextTurnWhenever(
							When(
								new Trigger(EventKind.Attacked) { Subject = Is.AttackingYou },
								[],
								new ModifyUntilEndOfTurn(Ref.EventCard, -1, 0)
							)
						)
					),
					Loyalty(
						-3,
						[TargetSpec.InYourGraveyard(Is.Type(CardType.Instant | CardType.Sorcery))],
						new If(
							new Matches(Ref.Target(), Is.Color(ManaColor.Green)),
							[new AddManaOfAnyOneColor(1)]
						),
						new MoveTo(Ref.Target(), ZoneType.Hand) { From = [ZoneType.Graveyard] }
					),
					Loyalty(
						-7,
						[],
						new DrawCards(
							Ref.You,
							Count.NumberOf(Ref.All(ZoneType.Library, of: Ref.You)) with
							{
								Per = 2,
								RoundUp = true,
							}
						),
						new GetEmblem(
							[
								new UntilEndOfTurn
								{
									NoMaximumHandSize = true,
									UntilTurnOf = UntilEndOfTurn.Never,
								},
							]
						)
					)
				),
			],
			["Jace, Vryn's Prodigy // Jace, Telepath Unbound"] =
			[
				new ActivatedAbilityComponent(
					ManaCost.None,
					[
						new DrawCards(Ref.You, 1),
						new Discard(Ref.You, 1),
						new If(
							new AtLeast(Count.NumberOf(Ref.CardsInYourGraveyard), 5),
							[ReturnTransformed]
						),
					]
				)
				{
					Costs = [new TapSelfCost()],
				},
				BackFace(
					"Jace, Vryn's Prodigy // Jace, Telepath Unbound",
					Loyalty(
						1,
						[Target(Is.Creature) with { Optional = true }],
						new ModifyUntilEndOfTurn(Ref.Target(), -2, 0) { UntilYourNextTurn = true }
					),
					Loyalty(
						-3,
						[TargetSpec.InYourGraveyard(Is.Type(CardType.Instant | CardType.Sorcery))],
						new MakePlayable(
							Ref.Target(),
							new PlayPermission
							{
								ThisTurn = true,
								CastOnly = true,
								ExiledLeavingTheStack = true,
							}
						)
					),
					Loyalty(
						-9,
						[],
						new GetEmblem(
							[
								When(
									TriggerEvent.YouCastSpell,
									[TargetSpec.Player(Is.Opponents)],
									new Mill(Ref.Target(), 5)
								),
							]
						)
					)
				),
			],
			["Cecil, Dark Knight // Cecil, Redeemed Paladin"] =
			[
				When(
					TriggerEvent.SelfDealsDamage,
					[],
					new LoseLife(Ref.You, Count.X),
					new If(
						new Not(
							new AtLeast(
								Count.LifeTotalOf(Ref.You),
								MagicPlayer.StartingLife / 2 + 1
							)
						),
						[new Untap(Ref.Source), new Transform(Ref.Source)]
					)
				),
				BackFace(
					"Cecil, Dark Knight // Cecil, Redeemed Paladin",
					When(
						TriggerEvent.SelfAttacks,
						[],
						new ModifyUntilEndOfTurn(
							Ref.Permanents(Is.Attacking & !Is.Source),
							0,
							0,
							"Indestructible"
						)
					)
				),
			],
			["Concealing Curtains // Revealing Eye"] =
			[
				new ActivatedAbilityComponent(ManaCost.Parse("{2}{B}"), [new Transform(Ref.Source)])
				{
					SorcerySpeed = true,
				},
				BackFace(
					"Concealing Curtains // Revealing Eye",
					When(
						TriggerEvent.SelfTransforms,
						[TargetSpec.Player(Is.Opponents)],
						new YouChooseTheyDiscard(Ref.Target(), CardType.None)
						{
							Filter = !Is.Land,
							Optional = true,
							Then = [new DrawCards(Ref.Target(), 1)],
						}
					)
				),
			],
			["Sink into Stupor // Soporific Springs"] =
			[
				Spell(
					[TargetSpec.Spell(Is.Opponents) with { Or = Target(!Is.Land & Is.Opponents) }],
					new MoveTo(Ref.Target(), ZoneType.Hand)
					{
						From = [ZoneType.Battlefield, ZoneType.Stack],
					}
				),
				BackFace(
					"Sink into Stupor // Soporific Springs",
					new EntersTappedComponent { UnlessYouPayLife = 3 },
					new ManaAbilityComponent(ManaColor.Blue)
				) with
				{
					Modal = true,
				},
			],
			// ===== MC-4.4j1: white's tested pieces =====
			["Eagles of the North"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[],
					new ModifyUntilEndOfTurn(Ref.CreaturesYouControl, 1, 0, "First strike")
				),
				Cycling(
					"{1}",
					new SearchLibrary(CardType.Land, ZoneType.Hand)
					{
						Subtype = "Plains",
						Reveal = true,
					}
				),
			],
			["Solitude"] =
			[
				// Swords to Plowshares' life, its power read before it leaves (last-known information).
				When(
					TriggerEvent.SelfEnters,
					[Target(Is.Creature & !Is.Source) with { Optional = true }],
					new GainLife(Ref.ControllerOfTarget(0), Count.PowerOf(Ref.Target())),
					new Exile(Ref.Target())
				),
				.. Evoke(ManaColor.White),
			],
			["Witch Enchanter // Witch-Blessed Meadow"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[Target((Is.Artifact | Is.Enchantment) & Is.Opponents)],
					new Destroy(Ref.Target())
				),
				BackFace(
					"Witch Enchanter // Witch-Blessed Meadow",
					new EntersTappedComponent { UnlessYouPayLife = 3 },
					new ManaAbilityComponent(ManaColor.White)
				) with
				{
					Modal = true,
				},
			],
			["White Plume Adventurer"] =
			[
				When(TriggerEvent.SelfEnters, [], new TakeTheInitiative(Ref.You)),
				// "If you've completed a dungeon" is read as it resolves — no intervening if.
				When(
					TriggerEvent.Beginning(Step.Upkeep, Whose.Opponent),
					[],
					new If(new YouCompletedADungeon(), [new Untap(Ref.CreaturesYouControl)])
					{
						Otherwise = [new Untap(Ref.CreaturesYouControl) { UpTo = 1 }],
					}
				),
			],
			["Parallax Wave"] =
			[
				.. Fading(5),
				new ActivatedAbilityComponent(
					ManaCost.None,
					[new Exile(Ref.Target()) { Linked = true }]
				)
				{
					Costs = [new RemoveCountersCost(1, Is.Source) { Kind = CounterKinds.Fade }],
					Targets = [Creature],
				},
				// "each player returns … all cards they own exiled with it" — each to its owner (MoveTo). An exile
				// resolving after the Wave left links to the object that's gone, so that card stays: as printed.
				When(
					TriggerEvent.SelfLeaves,
					[],
					new MoveTo(Ref.All(ZoneType.Exile, Is.ExiledBySource), ZoneType.Battlefield)
				),
			],
			["Get Lost"] =
			[
				// The Maps first: once it has left, "its controller" reads its owner (Path to Exile's order).
				Spell(
					[Target(Is.Creature | Is.Enchantment | Is.Planeswalker)],
					CreateMap(2) with
					{
						ControlledBy = Ref.ControllerOfTarget(0),
					},
					new Destroy(Ref.Target())
				),
			],
			// ===== MC-4.4j2a: white's options — protection of your choice, a counted search, a dealt-damage trigger =====
			["Mother of Runes"] =
			[
				new ActivatedAbilityComponent(
					ManaCost.None,
					[ProtectionOfYourChoice(Ref.Target(), colorless: false)]
				)
				{
					Costs = [new TapSelfCost()],
					Targets = [Target(Is.Creature & Is.Yours)],
				},
			],
			["Giver of Runes"] =
			[
				new ActivatedAbilityComponent(
					ManaCost.None,
					[ProtectionOfYourChoice(Ref.Target(), colorless: true)]
				)
				{
					Costs = [new TapSelfCost()],
					Targets = [Target(Is.Creature & Is.Yours & !Is.Source)],
				},
			],
			["Seasoned Dungeoneer"] =
			[
				When(TriggerEvent.SelfEnters, [], new TakeTheInitiative(Ref.You)),
				When(
					TriggerEvent.YouAttack,
					[
						Target(
							Is.Creature
								& Is.Attacking
								& (
									Is.Subtype("Cleric")
									| Is.Subtype("Rogue")
									| Is.Subtype("Warrior")
									| Is.Subtype("Wizard")
								)
						),
					],
					new ModifyUntilEndOfTurn(Ref.Target(), 0, 0, "Protection from creatures")
					{
						Gains = [new Protection(Is.Creature)],
					},
					new Explore(Ref.Target())
				),
			],
			["Winds of Abandon"] =
			[
				Overload("{4}{W}{W}"),
				Spell(
					[Target(Is.Creature & !Is.Yours)],
					new If(
						new AlternativeCostWasPaid(),
						[
							// ponytail: "its controller" for each is the one opponent — a multiplayer game needs a
							// search per controller, each counting their own creatures exiled.
							new Exile(Ref.Permanents(Is.Creature & !Is.Yours))
							{
								ThenWithXExiled =
								[
									new SearchLibrary(CardType.Land, ZoneType.Battlefield)
									{
										Supertype = Supertype.Basic,
										Tapped = true,
										Who = Ref.EachOpponent,
										UpTo = Count.X,
									},
								],
							},
						]
					)
					{
						Otherwise =
						[
							ItsControllerMayFetch with
							{
								May = false,
							},
							new Exile(Ref.Target()),
						],
					}
				),
			],
			["Jennifer Walters // The Sensational She-Hulk"] =
			[
				OpponentsCantCastDuringYourTurn,
				new ActivatedAbilityComponent(
					ManaCost.Parse("{3}{G}{W}{W}"),
					[new Transform(Ref.Source)]
				)
				{
					SorcerySpeed = true,
				},
				BackFace(
					"Jennifer Walters // The Sensational She-Hulk",
					OpponentsCantCastDuringYourTurn,
					// ponytail: "Do this only once each turn" as OncePerTurn — the first such trigger each turn is the
					// only one; the rules let each trigger and you choose which to use (DesignNotes).
					When(
						new Trigger(EventKind.CreatureDealtDamage)
						{
							Subject = Is.Creature & Is.Yours,
						},
						[TargetSpec.AnyTarget()],
						new May(
							"Have The Sensational She-Hulk deal that much damage?",
							new Always(),
							[new DealDamage(Ref.Target(), Count.X)]
						)
					) with
					{
						OncePerTurn = true,
					}
				),
			],
			// ===== MC-4.4j2b + j3: white's counter, cost and token options; the monarch =====
			["Guide of Souls"] =
			[
				When(
					TriggerEvent.AnotherCreatureYouControlEnters,
					[],
					new GainLife(Ref.You, 1),
					new PutCounters(Ref.You, 1) { Kind = CounterKinds.Energy }
				),
				When(
					TriggerEvent.YouAttack,
					[],
					new May(
						"Pay {E}{E}{E}? Two +1/+1 counters and a flying counter on target attacking creature.",
						new AtLeast(Count.CountersOfKind(Ref.You, CounterKinds.Energy), 3),
						[
							new RemoveCounters(Ref.You, 3) { Kind = CounterKinds.Energy },
							new WhenYouDo(
								[Target(Is.Creature & Is.Attacking)],
								[
									new PutCounters(Ref.Target(), 2),
									new PutCounters(Ref.Target(), 1) { Kind = CounterKinds.Flying },
									new ModifyUntilEndOfTurn(Ref.Target(), 0, 0)
									{
										ForGood = true,
										AlsoCreatureType = "Angel",
									},
								]
							),
						]
					)
				),
			],
			["Prismatic Ending"] =
			[
				new Sunburst(), // converge: the cast spends the most colours it can (Count.ColorsSpent)
				Spell(
					[Target(!Is.Land)],
					new If(
						new Matches(
							Ref.Target(),
							Is.ManaValueAtMost(Count.ColorsSpent(Ref.Source))
						),
						[new Exile(Ref.Target())]
					)
				),
			],
			["Sunfall"] =
			[
				Spell(
					[],
					new Exile(Ref.Permanents(Is.Creature)) { ThenWithXExiled = [Incubate(Count.X)] }
				),
			],
			["Ajani, Nacatl Pariah // Ajani, Nacatl Avenger"] =
			[
				When(TriggerEvent.SelfEnters, [], new CreateTokens(CatWarrior, 1)),
				When(
					new Trigger(EventKind.Died)
					{
						Subject = Is.Creature & Is.Yours & Is.Subtype("Cat") & !Is.Source,
						OneOrMore = true,
					},
					[],
					new May(
						"Exile Ajani, then return him transformed?",
						new Always(),
						[ReturnTransformed]
					)
				),
				BackFace(
					"Ajani, Nacatl Pariah // Ajani, Nacatl Avenger",
					Loyalty(
						2,
						[],
						new PutCounters(Ref.Permanents(Is.Subtype("Cat") & Is.Yours), 1)
					),
					Loyalty(
						0,
						[],
						new CreateTokens(CatWarrior, 1),
						new WhenYouDo(
							[TargetSpec.AnyTarget()],
							[new DealDamage(Ref.Target(), Count.CreaturesYouControl)]
						)
						{
							If = new AtLeast(
								Count.NumberOf(
									Ref.Permanents(Is.Color(ManaColor.Red) & Is.Yours & !Is.Source)
								),
								1
							),
						}
					),
					Loyalty(
						-4,
						[],
						new EachSacrifices(Ref.EachOpponent, !Is.Land)
						{
							Keeps =
							[
								CardType.Artifact,
								CardType.Creature,
								CardType.Enchantment,
								CardType.Planeswalker,
							],
						}
					)
				),
			],
			["Palace Jailer"] =
			[
				When(TriggerEvent.SelfEnters, [], new BecomeTheMonarch(Ref.You)),
				When(
					TriggerEvent.SelfEnters,
					[Target(Is.Creature & !Is.Yours)],
					new ExileUntilSourceLeaves(Ref.Target())
					{
						UntilAnOpponentTakes = DesignationComponent.Monarch,
					}
				),
			],
			// ===== MC-4.4j4–j6: ascend (the city's blessing), reconfigure, voting (will of the council) =====
			["Ocelot Pride"] =
			[
				When(
					TriggerEvent.YourEndStep,
					[],
					new CreateTokens(Cat, 1),
					new If(
						new YouHaveTheCitysBlessing(),
						[
							new CreateTokenCopy(
								Ref.Permanents(Is.Token & Is.Yours & Is.EnteredThisTurn)
							),
						]
					)
				) with
				{
					If = new AtLeast(Count.LifeYouGainedThisTurn, 1),
				},
			],
			["Lion Sash"] =
			[
				new ActivatedAbilityComponent(
					ManaCost.Parse("{W}"),
					[
						new If(
							new Matches(Ref.Target(0), Is.Type(PermanentTypes)),
							[new PutCounters(Ref.Source, 1)]
						),
						new Exile(Ref.Target()),
					]
				)
				{
					Targets = [TargetSpec.InAnyGraveyard()],
				},
				new StaticEffectComponent(Is.AttachedToSource)
				{
					Power = Count.CountersOn(Ref.Source),
					Toughness = Count.CountersOn(Ref.Source),
				},
				.. Reconfigure("{2}"),
			],
			["Council's Judgment"] =
			[
				Spell([], new Vote(!Is.Land & !Is.Yours) { Then = [new Exile(Ref.Chosen)] }),
			],
			// ===== MC-4.4k1: green's tested pieces =====
			["Bejeweled Warg"] =
			[
				ModalWhen(
					"Bejeweled Warg",
					TriggerEvent.SelfDealsCombatDamageToPlayer,
					1,
					M(
						[Target(Is.Creature & Is.Yours & Is.Subtype("Wolf"))],
						new PutCounters(Ref.Target(), 1)
					),
					M([], CreateTreasure(1))
				),
			],
			["Keen-Eyed Curator"] =
			[
				new StaticEffectComponent(Is.Source, "Trample")
				{
					Power = 4,
					Toughness = 4,
					If = new AtLeast(
						Count.CardTypesAmong(
							Ref.All(ZoneType.Exile, Is.ExiledBySource, Ref.EachPlayer)
						),
						4
					),
				},
				new ActivatedAbilityComponent(
					ManaCost.Parse("{1}"),
					[new Exile(Ref.Target()) { Linked = true }]
				)
				{
					Targets = [TargetSpec.InAnyGraveyard()],
				},
			],
			["Lotus Cobra"] =
			[
				When(TriggerEvent.LandYouControlEnters, [], new AddManaOfAnyOneColor(1)),
			],
			["Scythecat Cub"] =
			[
				When(
					TriggerEvent.LandYouControlEnters,
					[Target(Is.Creature & Is.Yours)],
					new IfNthResolutionThisTurn(
						2,
						[new PutCounters(Ref.Target(), Count.CountersOn(Ref.It))]
					)
					{
						Otherwise = [new PutCounters(Ref.Target(), 1)],
					}
				),
			],
			["Sylvan Caryatid"] = [.. AnyColorMana(ManaRestriction.None)],
			["Emeritus of Abundance // Regrowth"] =
			[
				EntersPrepared,
				When(TriggerEvent.SelfAttacks, [], new BecomePrepared(Ref.Source)) with
				{
					If = new AtLeast(Count.LandsYouControl, 8),
				},
				PrepareSpell(
					"Emeritus of Abundance // Regrowth",
					[TargetSpec.InYourGraveyard()],
					new MoveTo(Ref.Target(), ZoneType.Hand) { From = [ZoneType.Graveyard] }
				),
			],
			["Endurance"] =
			[
				// ponytail: "in a random order" is the graveyard's order, known to its owner — shuffle them first
				// (MagicState.ToBottomAtRandom) if the AI ever reads that order.
				When(
					TriggerEvent.SelfEnters,
					[TargetSpec.Player() with { Optional = true }],
					new MoveTo(
						Ref.All(ZoneType.Graveyard, Is.AnyCard, Ref.Target()),
						ZoneType.Library
					)
					{
						From = [ZoneType.Graveyard],
					}
				),
				.. Evoke(ManaColor.Green),
			],
			["Formidable Speaker"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[],
					new MayDiscard(
						[new SearchLibrary(CardType.Creature, ZoneType.Hand) { Reveal = true }]
					)
				),
				new ActivatedAbilityComponent(ManaCost.Parse("{1}"), [new Untap(Ref.Target())])
				{
					Costs = [new TapSelfCost()],
					Targets = [Target(!Is.Source)],
				},
			],
			["Mole Man, Moloid Master"] =
			[
				new MayPlayFromZone(ZoneType.Graveyard, Is.Land),
				When(
					TriggerEvent.LandYouControlEnters,
					[],
					new CreateTokens(Moloid, 1)
					{
						Abilities =
						[
							When(
								TriggerEvent.SelfAttacks,
								[],
								new May("Mill a card?", new Always(), [new Mill(Ref.You, 1)])
							),
						],
					}
				),
			],
			["Sentinel of the Nameless City"] =
			[
				When(TriggerEvent.SelfEnters, [], CreateMap(1)),
				When(TriggerEvent.SelfAttacks, [], CreateMap(1)),
			],
			["Six"] =
			[
				When(TriggerEvent.SelfAttacks, [], new MillAndMayTake(3, CardType.Land)),
				// Retrace, during your turn: cast from the graveyard, discarding a land card beside its mana cost.
				new MayPlayFromZone(ZoneType.Graveyard, Is.Type(NonlandPermanentTypes) & !Is.Land)
				{
					Costs = [new DiscardCost(Is.Land)],
				},
			],
			["Icetill Explorer"] =
			[
				new AdditionalLandEachTurn(),
				new MayPlayFromZone(ZoneType.Graveyard, Is.Land),
				When(TriggerEvent.LandYouControlEnters, [], new Mill(Ref.You, 1)),
			],
			["Ouroboroid"] =
			[
				// X is fixed as it resolves (CR 608.2h): the others first, then itself, so its power is read unchanged.
				When(
					TriggerEvent.BeginningOfCombatOnYourTurn,
					[],
					new PutCounters(Ref.OtherCreaturesYouControl, Count.PowerOf(Ref.Source)),
					new PutCounters(Ref.Source, Count.PowerOf(Ref.Source))
				),
			],
			["Ulvenwald Oddity // Ulvenwald Behemoth"] =
			[
				new ActivatedAbilityComponent(
					ManaCost.Parse("{5}{G}{G}"),
					[new Transform(Ref.Source)]
				),
				BackFace(
					"Ulvenwald Oddity // Ulvenwald Behemoth",
					new StaticEffectComponent(Is.Creature & Is.Yours & !Is.Source, "Trample")
					{
						Power = 1,
						Toughness = 1,
					},
					new StaticEffectComponent(Is.Creature & Is.Yours & !Is.Source, "Haste")
				),
			],
			["Generous Ent"] =
			[
				When(TriggerEvent.SelfEnters, [], CreateFood(1)),
				Cycling(
					"{1}",
					new SearchLibrary(CardType.Land, ZoneType.Hand)
					{
						Subtype = "Forest",
						Reveal = true,
					}
				),
			],
			["Tear Asunder"] =
			[
				new KickerComponent(ManaCost.Parse("{1}{B}")),
				Spell(
					[Target(Is.Artifact | Is.Enchantment) with { IfKicked = Target(!Is.Land) }],
					new Exile(Ref.Target())
				),
			],
			["Force of Vigor"] =
			[
				new MayCastFromHandFree(Is.Source)
				{
					If = new Not(new DuringYourTurn()),
					Costs = [new ExileFromHandCost(Is.Color(ManaColor.Green))],
				},
				Spell(
					[
						Target(Is.Artifact | Is.Enchantment) with
						{
							Optional = true,
						},
						Target(Is.Artifact | Is.Enchantment) with
						{
							Optional = true,
							NotThePrevious = true,
						},
					],
					new Destroy(Ref.EachTarget)
				),
			],
			["Natural Order"] =
			[
				new AdditionalCostsComponent(
					[new SacrificeCost(Is.Creature & Is.Color(ManaColor.Green))]
				),
				Spell(
					[],
					new SearchLibrary(CardType.Creature, ZoneType.Battlefield)
					{
						Filter = Is.Color(ManaColor.Green),
						FilterText = "green",
					}
				),
			],
			["Malevolent Rumble"] =
			[
				Spell(
					[],
					new LookAndTake(4, Is.Type(PermanentTypes))
					{
						Reveal = true,
						Rest = TheRest.Graveyard,
					},
					new CreateTokens(EldraziSpawn, 1)
					{
						Abilities =
						[
							new ManaAbilityComponent(ManaColor.Colorless)
							{
								Costs = [new SacrificeSelfCost()],
							},
						],
					}
				),
			],
			["Titania, Protector of Argoth"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[TargetSpec.InYourGraveyard(Is.Land)],
					new MoveTo(Ref.Target(), ZoneType.Battlefield) { From = [ZoneType.Graveyard] }
				),
				When(
					new Trigger(EventKind.ChangedZone)
					{
						From = ZoneType.Battlefield,
						To = ZoneType.Graveyard,
						Subject = Is.Land & Is.Yours,
					},
					[],
					new CreateTokens(TitaniasElemental, 1)
				),
			],
			["Pest Infestation"] =
			[
				Spell(
					[Target(Is.Artifact | Is.Enchantment) with { PerX = Count.X, Optional = true }],
					new Destroy(Ref.EachTarget),
					new CreateTokens(Pest, Count.X with { Times = 2 })
					{
						Abilities = [When(TriggerEvent.SelfDies, [], new GainLife(Ref.You, 1))],
					}
				),
			],
			// ===== MC-4.4k2a: green's mana, land and token options (a mana ability's OnlyIf, eternalize, a sacrifice's event, any number of lands, shroud, persist) =====
			["Fanatic of Rhonas"] =
			[
				new ManaAbilityComponent(ManaColor.Green),
				new ManaAbilityComponent(ManaColor.Green)
				{
					Amount = 4,
					OnlyIf = new AtLeast(
						Count.NumberOf(Ref.Permanents(Is.Creature & Is.Yours & Is.PowerAtLeast(4))),
						1
					),
				},
				Eternalize("{2}{G}{G}"),
			],
			["Undermountain Adventurer"] =
			[
				When(TriggerEvent.SelfEnters, [], new TakeTheInitiative(Ref.You)),
				new ManaAbilityComponent(ManaColor.Green)
				{
					Amount = 2,
					OnlyIf = new Not(new YouCompletedADungeon()),
				},
				new ManaAbilityComponent(ManaColor.Green)
				{
					Amount = 6,
					OnlyIf = new YouCompletedADungeon(),
				},
			],
			["Baloth Prime"] =
			[
				new EntersTappedComponent(),
				new EntersWithCountersComponent(6) { Kind = CounterKinds.Stun },
				When(
					new Trigger(EventKind.ChangedZone)
					{
						From = ZoneType.Battlefield,
						To = ZoneType.Graveyard,
						Subject = Is.Land & Is.Yours,
						Also = EventTest.Sacrificed,
					},
					[],
					new CreateTokens(BalothBeast, 1) { Tapped = true },
					new Untap(Ref.Source)
				),
				new ActivatedAbilityComponent(ManaCost.Parse("{4}"), [new GainLife(Ref.You, 2)])
				{
					Costs = [new SacrificeCost(Is.Land)],
				},
			],
			["Fastbond"] =
			[
				new AdditionalLandEachTurn { AnyNumber = true },
				When(
					new Trigger(EventKind.LandPlayed) { By = Whose.You },
					[],
					new DealDamage(Ref.You, 1)
				) with
				{
					If = new AtLeast(Count.LandsYouPlayedThisTurn, 2),
				},
			],
			["Sylvan Safekeeper"] =
			[
				new ActivatedAbilityComponent(
					ManaCost.None,
					[new ModifyUntilEndOfTurn(Ref.Target(), 0, 0, "Shroud")]
				)
				{
					Costs = [new SacrificeCost(Is.Land)],
					Targets = [Target(Is.Creature & Is.Yours)],
				},
			],
			["Vaultborn Tyrant"] =
			[
				When(
					new Trigger(EventKind.Entered)
					{
						Subject = Is.Creature & Is.Yours & (Is.Source | Is.PowerAtLeast(4)),
					},
					[],
					new GainLife(Ref.You, 3),
					new DrawCards(Ref.You, 1)
				),
				When(
					TriggerEvent.SelfDies,
					[],
					new CreateTokenCopy(Ref.Source) { AlsoType = CardType.Artifact }
				) with
				{
					If = new Not(new Matches(Ref.Source, Is.Token)),
				},
			],
			["Woodfall Primus"] =
			[
				When(TriggerEvent.SelfEnters, [Target(!Is.Creature)], new Destroy(Ref.Target())),
				Persist,
			],
			// ===== MC-4.4k2b: green's trigger, combat and casting options (a spell becoming a target, "attacks that player", a counter of any kind removed, unpreventable combat damage, into a graveyard from anywhere, the first spell before it is cast, a spell shuffled in, level up) =====
			["Surrak, Elusive Hunter"] =
			[
				new CantBeCountered(),
				When(
					new Trigger(EventKind.BecameTarget)
					{
						Subject = Is.Creature & Is.Yours,
						By = Whose.Opponent,
					},
					[],
					new DrawCards(Ref.You, 1)
				),
			],
			["Ursine Monstrosity"] =
			[
				When(
					TriggerEvent.BeginningOfCombatOnYourTurn,
					[],
					new Mill(Ref.You, 1),
					new ModifyUntilEndOfTurn(Ref.Source, 0, 0, Combat.AttacksThePlayer),
					new ModifyUntilEndOfTurn(
						Ref.Source,
						Count.CardTypesAmong(Ref.CardsInYourGraveyard),
						Count.CardTypesAmong(Ref.CardsInYourGraveyard),
						"Indestructible"
					)
				),
			],
			["Leatherhead, Swamp Stalker"] =
			[
				new EntersWithCountersComponent(1) { Kind = CounterKinds.Hexproof },
				When(
					TriggerEvent.SelfDealsCombatDamageToPlayer,
					[],
					new May(
						"Remove a counter from Leatherhead?",
						// a counter RemoveCounters can take, on her as she is now — or no "when you do"
						new AtLeast(Count.AllCountersOn(Ref.Permanents(Is.Source)), 1),
						[
							new RemoveCounters(Ref.Source, 1) { AnyKind = true },
							new WhenYouDo(
								[Target((Is.Artifact | Is.Enchantment) & Is.Opponents)],
								[new Destroy(Ref.Target())]
							),
						]
					)
				),
			],
			["Questing Beast"] =
			[
				new CantBeBlockedBy(Is.PowerAtMost(2)),
				new CombatDamageCantBePrevented(Is.Creature & Is.Yours),
				When(
					TriggerEvent.SelfDealsCombatDamageToPlayer,
					[Target(Is.Planeswalker & Is.Opponents)],
					new DealDamage(Ref.Target(), Count.X)
				),
			],
			["Worldspine Wurm"] =
			[
				When(TriggerEvent.SelfDies, [], new CreateTokens(Wurm, 3)),
				When(
					new Trigger(EventKind.ChangedZone)
					{
						Subject = Is.Source,
						To = ZoneType.Graveyard,
					},
					[],
					new MoveTo(Ref.Source, ZoneType.Library)
					{
						From = [ZoneType.Graveyard],
						Shuffle = true,
					}
				) with
				{
					From = ZoneType.Graveyard,
				},
			],
			["Once Upon a Time"] =
			[
				new MayCastFromHandFree(Is.Source) { If = new FirstSpellYouCastThisGame() },
				Spell([], new LookAndTake(5, Is.Creature | Is.Land) { Reveal = true }),
			],
			["Green Sun's Zenith"] =
			[
				Spell(
					[],
					new SearchLibrary(CardType.Creature, ZoneType.Battlefield)
					{
						Filter = Is.Color(ManaColor.Green) & Is.ManaValueAtMost(Count.X),
						FilterText = "green",
					},
					new MoveTo(Ref.Source, ZoneType.Library)
					{
						From = [ZoneType.Stack],
						Shuffle = true,
					}
				),
			],
			// ponytail: a level's 4/4 and 6/6 are +2/+3 and +4/+5 on the printed 2/1 — right until an effect sets its
			// base power and toughness (CR 711.5: a level SETS them); a base-setting static when a card meets one.
			["Hexdrinker"] =
			[
				new ActivatedAbilityComponent(
					ManaCost.Parse("{1}"),
					[new PutCounters(Ref.Source, 1) { Kind = CounterKinds.Level }]
				)
				{
					Keyword = "Level up",
					SorcerySpeed = true,
				},
				new StaticEffectComponent(Is.Source)
				{
					Power = 2,
					Toughness = 3,
					If = new Both(HasLevel(3), new Not(HasLevel(8))),
				},
				new Protection(Is.Instant) { If = new Both(HasLevel(3), new Not(HasLevel(8))) },
				new StaticEffectComponent(Is.Source)
				{
					Power = 4,
					Toughness = 5,
					If = HasLevel(8),
				},
				new Protection(Is.AnyCard) { If = HasLevel(8) },
			],
			// ===== MC-4.4k3: green's new pieces (crew: a type until end of turn; earthbend: a land a creature for good, its return, "an additional {G}"; bestow: an enchant on the alternative cost; day/night: Rules.DayNight) =====
			["Esika's Chariot"] =
			[
				When(TriggerEvent.SelfEnters, [], new CreateTokens(GreenCat, 2)),
				When(
					TriggerEvent.SelfAttacks,
					[Target(Is.Token & Is.Yours)],
					new CreateTokenCopy(Ref.Target())
				),
				Crew(4),
			],
			["Badgermole Cub"] =
			[
				When(TriggerEvent.SelfEnters, [Target(Is.Land & Is.Yours)], Earthbend(1)),
				new ExtraMana(Is.Creature, ManaColor.Green),
			],
			["Springheart Nantuko"] =
			[
				.. Bestow("{1}{G}"),
				AttachedGets(1, 1),
				When(
					TriggerEvent.LandYouControlEnters,
					[],
					// "you may pay {1}{G} if this permanent is attached to a creature you control" — unattached, or
					// unable or unwilling to pay, no token was created this way: the Insect
					new If(
						new Matches(Ref.EnchantedCreature, Is.Creature & Is.Yours),
						[
							MayPay(
								"Pay {1}{G} to create a token that's a copy of the enchanted creature?",
								new PayMana(
									ManaCost.Parse("{1}{G}"),
									[new CreateTokenCopy(Ref.EnchantedCreature)]
								),
								new CreateTokens(Insect, 1)
							),
						]
					)
					{
						Otherwise = [new CreateTokens(Insect, 1)],
					}
				),
			],
			["Outland Liberator // Frenzied Trapbreaker"] =
			[
				LiberatorsSacrifice,
				BackFace(
					"Outland Liberator // Frenzied Trapbreaker",
					LiberatorsSacrifice,
					When(
						TriggerEvent.SelfAttacks,
						// "defending player controls" — the one opponent
						[Target((Is.Artifact | Is.Enchantment) & Is.Opponents)],
						new Destroy(Ref.Target())
					)
				),
			],
			// ===== MC-4.4l1: colourless' tested lands (surveil lands, Triomes, fast lands, horizon lands, Verges) =====
			["Meticulous Archive"] = SurveilLand,
			["Undercity Sewers"] = SurveilLand,
			["Raucous Theater"] = SurveilLand,
			["Commercial District"] = SurveilLand,
			["Lush Portico"] = SurveilLand,
			["Shadowy Backstreet"] = SurveilLand,
			["Thundering Falls"] = SurveilLand,
			["Underground Mortuary"] = SurveilLand,
			["Elegant Parlor"] = SurveilLand,
			["Hedge Maze"] = SurveilLand,
			["Spara's Headquarters"] = Triome,
			["Raffine's Tower"] = Triome,
			["Xander's Lounge"] = Triome,
			["Ziatora's Proving Ground"] = Triome,
			["Jetmir's Garden"] = Triome,
			["Savai Triome"] = Triome,
			["Ketria Triome"] = Triome,
			["Indatha Triome"] = Triome,
			["Raugrin Triome"] = Triome,
			["Zagoth Triome"] = Triome,
			["Seachrome Coast"] = FastLand(ManaColor.White, ManaColor.Blue),
			["Darkslick Shores"] = FastLand(ManaColor.Blue, ManaColor.Black),
			["Blackcleave Cliffs"] = FastLand(ManaColor.Black, ManaColor.Red),
			["Copperline Gorge"] = FastLand(ManaColor.Red, ManaColor.Green),
			["Razorverge Thicket"] = FastLand(ManaColor.Green, ManaColor.White),
			["Concealed Courtyard"] = FastLand(ManaColor.White, ManaColor.Black),
			["Spirebluff Canal"] = FastLand(ManaColor.Blue, ManaColor.Red),
			["Blooming Marsh"] = FastLand(ManaColor.Black, ManaColor.Green),
			["Inspiring Vantage"] = FastLand(ManaColor.Red, ManaColor.White),
			["Botanical Sanctum"] = FastLand(ManaColor.Green, ManaColor.Blue),
			["Horizon Canopy"] = HorizonLand(ManaColor.Green, ManaColor.White),
			["Fiery Islet"] = HorizonLand(ManaColor.Blue, ManaColor.Red),
			["Nurturing Peatland"] = HorizonLand(ManaColor.Black, ManaColor.Green),
			["Sunbaked Canyon"] = HorizonLand(ManaColor.Red, ManaColor.White),
			["Waterlogged Grove"] = HorizonLand(ManaColor.Green, ManaColor.Blue),
			["Blazemire Verge"] = Verge(ManaColor.Black, ManaColor.Red, "Swamp", "Mountain"),
			["Bleachbone Verge"] = Verge(ManaColor.Black, ManaColor.White, "Plains", "Swamp"),
			["Karakas"] =
			[
				new ManaAbilityComponent(ManaColor.White),
				new ActivatedAbilityComponent(
					ManaCost.None,
					[new MoveTo(Ref.Target(), ZoneType.Hand)]
				)
				{
					Costs = [new TapSelfCost()],
					Targets = [Target(Is.Creature & Is.Legendary)],
				},
			],
			["Library of Alexandria"] =
			[
				ColorlessMana(),
				new ActivatedAbilityComponent(ManaCost.None, [new DrawCards(Ref.You, 1)])
				{
					Costs = [new TapSelfCost()],
					// "exactly seven": seven or more, and not eight
					OnlyIf = new Both(
						new AtLeast(Count.NumberOf(Ref.CardsInYourHand), 7),
						new Not(new AtLeast(Count.NumberOf(Ref.CardsInYourHand), 8))
					),
				},
			],
			["Abandoned Air Temple"] =
			[
				new EntersTappedComponent(YouControlA(Is.Land & Is.Supertype(Supertype.Basic))),
				new ManaAbilityComponent(ManaColor.White),
				new ActivatedAbilityComponent(
					ManaCost.Parse("{3}{W}"),
					[new PutCounters(Ref.CreaturesYouControl, 1)]
				)
				{
					Costs = [new TapSelfCost()],
				},
			],
			// ===== MC-4.4l2: colourless' other tested pieces =====
			["Aether Spellbomb"] =
			[
				SacrificeThis(
					"{U}",
					tap: false,
					[Creature],
					new MoveTo(Ref.Target(), ZoneType.Hand)
				),
				SacrificeThis("{1}", tap: false, [], new DrawCards(Ref.You, 1)),
			],
			["Pyrite Spellbomb"] =
			[
				SacrificeThis(
					"{R}",
					tap: false,
					[TargetSpec.AnyTarget()],
					new DealDamage(Ref.Target(), 2)
				),
				SacrificeThis("{1}", tap: false, [], new DrawCards(Ref.You, 1)),
			],
			["Haywire Mite"] =
			[
				When(TriggerEvent.SelfDies, [], new GainLife(Ref.You, 2)),
				SacrificeThis(
					"{G}",
					tap: false,
					[Target((Is.Artifact | Is.Enchantment) & !Is.Creature)],
					new Exile(Ref.Target())
				),
			],
			["Expedition Map"] =
			[
				SacrificeThis(
					"{2}",
					tap: true,
					[],
					new SearchLibrary(CardType.Land, ZoneType.Hand) { Reveal = true }
				),
			],
			["Soul-Guide Lantern"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[TargetSpec.InAnyGraveyard()],
					new Exile(Ref.Target())
				),
				SacrificeThis(
					null,
					tap: true,
					[],
					new Exile(Ref.All(ZoneType.Graveyard, Is.AnyCard, Ref.EachOpponent))
				),
				SacrificeThis("{1}", tap: true, [], new DrawCards(Ref.You, 1)),
			],
			["Voltaic Key"] =
			[
				new ActivatedAbilityComponent(ManaCost.Parse("{1}"), [new Untap(Ref.Target())])
				{
					Costs = [new TapSelfCost()],
					Targets = [Target(Is.Artifact)],
				},
			],
			["Manifold Key"] =
			[
				new ActivatedAbilityComponent(ManaCost.Parse("{1}"), [new Untap(Ref.Target())])
				{
					Costs = [new TapSelfCost()],
					Targets = [Target(Is.Artifact & !Is.Source)],
				},
				new ActivatedAbilityComponent(
					ManaCost.Parse("{3}"),
					[new ModifyUntilEndOfTurn(Ref.Target(), 0, 0, Combat.CantBeBlocked)]
				)
				{
					Costs = [new TapSelfCost()],
					Targets = [Creature],
				},
			],
			["Zuran Orb"] =
			[
				new ActivatedAbilityComponent(ManaCost.None, [new GainLife(Ref.You, 2)])
				{
					Costs = [new SacrificeCost(Is.Land)],
				},
			],

			["Chromatic Star"] =
			[
				// a mana cost and its own sacrifice: the solver's last resort, and a move (ManaAbilityComponent.CashedIn)
				SacrificeForMana(1) with
				{
					Cost = ManaCost.Parse("{1}"),
				},
				When(
					TriggerEvent.SelfLeaves with
					{
						To = ZoneType.Graveyard,
					},
					[],
					new DrawCards(Ref.You, 1)
				),
			],
			["Gleemox"] = [.. AnyColorMana(ManaRestriction.None)], // "This card is banned." is no rules text
			["Mox Opal"] =
			[
				// metalcraft: "Activate only if you control three or more artifacts."
				.. AnyColorMana(ManaRestriction.None)
					.Cast<ManaAbilityComponent>()
					.Select(m =>
						m with
						{
							OnlyIf = new AtLeast(
								Count.NumberOf(Ref.Permanents(Is.Artifact & Is.Yours)),
								3
							),
						}
					),
			],
			["Retrofitter Foundry"] =
			[
				new ActivatedAbilityComponent(ManaCost.Parse("{3}"), [new Untap(Ref.Source)]),
				new ActivatedAbilityComponent(ManaCost.Parse("{2}"), [new CreateTokens(Servo, 1)])
				{
					Costs = [new TapSelfCost()],
				},
				new ActivatedAbilityComponent(
					ManaCost.Parse("{1}"),
					[new CreateTokens(Fra.FraCards.Thopter, 1)]
				)
				{
					Costs = [new TapSelfCost(), new SacrificeCost(Is.Subtype("Servo"))],
				},
				new ActivatedAbilityComponent(ManaCost.None, [new CreateTokens(BigConstruct, 1)])
				{
					Costs = [new TapSelfCost(), new SacrificeCost(Is.Subtype("Thopter"))],
				},
			],
			["Sensei's Divining Top"] =
			[
				new ActivatedAbilityComponent(
					ManaCost.Parse("{1}"),
					[new LookAndTake(3) { Take = 0, Rest = TheRest.TopInAnyOrder }]
				),
				new ActivatedAbilityComponent(
					ManaCost.None,
					[
						new DrawCards(Ref.You, 1),
						new MoveTo(Ref.Source, ZoneType.Library) { OnTop = true },
					]
				)
				{
					Costs = [new TapSelfCost()],
				},
			],
			["Skateboard"] =
			[
				When(TriggerEvent.SelfEnters, [Target()], new Tap(Ref.Target())),
				AttachedGets(1, 0),
				new StaticEffectComponent(Is.AttachedToSource, "Haste"),
				Equip("{1}"),
			],
			["Skullclamp"] =
			[
				AttachedGets(1, -1),
				When(TriggerEvent.AttachedCreatureDies, [], new DrawCards(Ref.You, 2)),
				Equip("{1}"),
			],
			["Smuggler's Copter"] =
			[
				When(TriggerEvent.SelfAttacks, [], MayLoot),
				When(new Trigger(EventKind.Blocks) { Subject = Is.Source }, [], MayLoot),
				Crew(1),
			],
			["Crucible of Worlds"] = [new MayPlayFromZone(ZoneType.Graveyard, Is.Land)],
			["Golos, Tireless Pilgrim"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[],
					new SearchLibrary(CardType.Land, ZoneType.Battlefield)
					{
						Tapped = true,
						May = true,
					}
				),
				new ActivatedAbilityComponent(
					ManaCost.Parse("{2}{W}{U}{B}{R}{G}"),
					[
						new ExileTopPlayable
						{
							N = 3,
							Play = new() { ThisTurn = true, Free = true },
						},
					]
				),
			],
			["Memnarch, the Warden"] =
			[
				When(TriggerEvent.SelfEnters, [], new CreateTokens(Myr, 2)),
				When(
					TriggerEvent.SelfAttacks,
					[],
					new DrawCards(Ref.You, Count.NumberOf(Ref.Permanents(Is.Artifact & Is.Yours)))
				),
			],
			["Tezzeret, Cruel Captain"] =
			[
				When(
					new Trigger(EventKind.Entered) { Subject = Is.Artifact & Is.Yours },
					[],
					new AddLoyalty(Ref.Source, 1)
				),
				Loyalty(
					0,
					[Target(Is.Artifact | Is.Creature)],
					new Untap(Ref.Target()),
					new If(
						new Matches(Ref.Target(), Is.Artifact & Is.Creature),
						[new PutCounters(Ref.Target(), 1)]
					)
				),
				Loyalty(
					-3,
					[],
					new SearchLibrary(CardType.Artifact, ZoneType.Hand)
					{
						Filter = Is.ManaValueAtMost(1),
						FilterText = "mana value 1 or less",
						Reveal = true,
					}
				),
				Loyalty(
					-7,
					[],
					new GetEmblem(
						[
							When(
								TriggerEvent.BeginningOfCombatOnYourTurn,
								[Target(Is.Artifact & Is.Yours)],
								new PutCounters(Ref.Target(), 3),
								new If(
									new Not(new Matches(Ref.Target(), Is.Creature)),
									[
										new ModifyUntilEndOfTurn(Ref.Target(), 0, 0)
										{
											ForGood = true,
											SetBase = (0, 0),
											AlsoCreatureType = "Robot",
											AlsoType = CardType.Creature,
										},
									]
								)
							),
						]
					)
				),
			],
			["Portal to Phyrexia"] =
			[
				When(
					TriggerEvent.SelfEnters,
					[],
					new EachSacrifices(Ref.EachOpponent, Is.Creature) { N = 3 }
				),
				When(
					TriggerEvent.YourUpkeep,
					[TargetSpec.InAnyGraveyard(Is.Creature)],
					new MoveTo(Ref.Target(), ZoneType.Battlefield)
					{
						From = [ZoneType.Graveyard],
						UnderYourControl = true,
						Then =
						[
							new ModifyUntilEndOfTurn(Ref.ThisWay, 0, 0)
							{
								ForGood = true,
								AlsoCreatureType = "Phyrexian",
							},
						],
					}
				),
			],
			["Cogwork Librarian"] = [], // ponytail: a 3/3 — its text is the DRAFT's, which Limited/Draft.cs doesn't know (DesignNotes)
		};

	/// <summary>
	/// The surveil lands (Meticulous Archive): "This land enters tapped. When this land enters, surveil 1." — their
	/// mana is their two basic land types' (CR 305.6), as the original duals'.
	/// </summary>
	static ImmutableArray<GameComponent> SurveilLand =>
		[new EntersTappedComponent(), When(TriggerEvent.SelfEnters, [], new Surveil(1))];

	/// <summary>The Triomes (Savai Triome): "This land enters tapped. Cycling {3}" — their mana is their three land types'.</summary>
	static ImmutableArray<GameComponent> Triome => [new EntersTappedComponent(), Cycling("{3}")];

	/// <summary>
	/// The fast lands (Seachrome Coast): "This land enters tapped unless you control two or fewer other lands.
	/// {T}: Add {A} or {B}." — tapped with three or more others.
	/// </summary>
	static ImmutableArray<GameComponent> FastLand(ManaColor a, ManaColor b) =>
		[
			new EntersTappedComponent(new Not(new AtLeast(OtherLandsYouControl, 3))),
			new ManaAbilityComponent(a),
			new ManaAbilityComponent(b),
		];

	/// <summary>
	/// The horizon lands (Horizon Canopy): "{T}, Pay 1 life: Add {A} or {B}. {1}, {T}, Sacrifice this land: Draw a
	/// card." — Mana Confluence's life, on each colour.
	/// </summary>
	static ImmutableArray<GameComponent> HorizonLand(ManaColor a, ManaColor b) =>
		[
			new ManaAbilityComponent(a) { Costs = [new TapSelfCost(), new PayLifeCost(1)] },
			new ManaAbilityComponent(b) { Costs = [new TapSelfCost(), new PayLifeCost(1)] },
			SacrificeThis("{1}", tap: true, [], new DrawCards(Ref.You, 1)),
		];

	/// <summary>
	/// The Verges (Blazemire Verge): "{T}: Add {A}. {T}: Add {B}. Activate only if you control a [Swamp] or a
	/// [Mountain]." — Fanatic of Rhonas' "activate only if", on the second colour.
	/// </summary>
	static ImmutableArray<GameComponent> Verge(
		ManaColor a,
		ManaColor b,
		string type1,
		string type2
	) =>
		[
			new ManaAbilityComponent(a),
			new ManaAbilityComponent(b)
			{
				OnlyIf = YouControlA(Is.Subtype(type1) | Is.Subtype(type2)),
			},
		];

	/// <summary>
	/// "You control a [filter]" — Abandoned Air Temple: "unless you control a basic land"; the Verges' land types;
	/// Snuff Out: "If you control a Swamp".
	/// </summary>
	static Condition YouControlA(Filter what) =>
		new AtLeast(Count.NumberOf(Ref.Permanents(what & Is.Yours)), 1);

	/// <summary>
	/// "[Cost], [{T},] Sacrifice this artifact: [effects]." — the Spellbombs (Aether Spellbomb: "{U}, Sacrifice this
	/// artifact: Return target creature to its owner's hand."), Expedition Map, Soul-Guide Lantern, the horizon lands,
	/// Strip Mine and Wasteland. <paramref name="tap"/>: the printed cost has {T} — say it by name at every call.
	/// </summary>
	static ActivatedAbilityComponent SacrificeThis(
		string? cost,
		bool tap,
		TargetSpec[] targets,
		params GameAction[] effects
	)
	{
		ImmutableList<AdditionalCost> costs = tap
			? [new TapSelfCost(), new SacrificeSelfCost()]
			: [new SacrificeSelfCost()];
		return new(cost is null ? ManaCost.None : ManaCost.Parse(cost), [.. effects])
		{
			Costs = costs,
			Targets = [.. targets],
		};
	}

	/// <summary>Smuggler's Copter: "you may draw a card. If you do, discard a card."</summary>
	static May MayLoot =>
		new(
			"Draw a card, then discard a card?",
			new Always(),
			[new DrawCards(Ref.You, 1), new Discard(Ref.You, 1)]
		);

	/// <summary>Retrofitter Foundry: "a 1/1 colorless Servo artifact creature token".</summary>
	static CardData Servo => Token("Servo", "Artifact Creature — Servo", "1", "1");

	/// <summary>Retrofitter Foundry: "a 4/4 colorless Construct artifact creature token".</summary>
	static CardData BigConstruct => Token("Construct", "Artifact Creature — Construct", "4", "4");

	/// <summary>Memnarch, the Warden: "1/1 colorless Myr artifact creature tokens".</summary>
	static CardData Myr => Token("Myr", "Artifact Creature — Myr", "1", "1");

	/// <summary>
	/// Crew N (CR 702.122, Esika's Chariot): "Tap any number of other untapped creatures you control with total power N
	/// or more: This Vehicle becomes an artifact creature until end of turn." — saddle's cost (TapUntappedCost
	/// { TotalPower }, the creatures picked after activating; summoning-sick ones may crew) at instant speed, and the
	/// creature type stamped until the cleanup step (ModifyUntilEndOfTurn.AlsoType); its printed power and toughness
	/// are read while it is one.
	/// </summary>
	static ActivatedAbilityComponent Crew(int power) =>
		new(
			ManaCost.None,
			[new ModifyUntilEndOfTurn(Ref.Source, 0, 0) { AlsoType = CardType.Creature }]
		)
		{
			Keyword = "Crew",
			Costs = [new TapUntappedCost(0, Is.Creature & !Is.Source) { TotalPower = power }],
		};

	/// <summary>
	/// Earthbend N (CR 701.66, Badgermole Cub): "Target land you control becomes a 0/0 creature with haste that's still
	/// a land. Put N +1/+1 counters on it. When it dies or is exiled, return it to the battlefield tapped." — the
	/// effects, on the ability's first target (a "target land you control" word). Does Machines' type change for good,
	/// and Serra Paragon's gained leave trigger twice — to a graveyard, to exile — each returning the card from there.
	/// ponytail: the return is the LAND's gained ability, so its controller's as it left, where CR 701.66 makes it the
	/// earthbender's delayed trigger ("under your control") — the same player unless the land changed hands.
	/// </summary>
	static GameAction[] Earthbend(int n) =>
		[
			new ModifyUntilEndOfTurn(Ref.Target(), 0, 0, "Haste")
			{
				ForGood = true,
				AlsoType = CardType.Creature,
				SetBase = (0, 0),
				Trigger = EarthbentReturns(ZoneType.Graveyard),
			},
			new ModifyUntilEndOfTurn(Ref.Target(), 0, 0)
			{
				ForGood = true,
				Trigger = EarthbentReturns(ZoneType.Exile),
			},
			new PutCounters(Ref.Target(), n),
		];

	/// <summary>Earthbend: "When it dies or is exiled, return it to the battlefield tapped." — one zone's half.</summary>
	static TriggeredAbilityComponent EarthbentReturns(ZoneType from) =>
		When(
			TriggerEvent.SelfLeaves with
			{
				To = from,
			},
			[],
			new MoveTo(Ref.EventCard, ZoneType.Battlefield) { From = [from], Tapped = true }
		);

	/// <summary>Outland Liberator, and Frenzied Trapbreaker: "{1}, Sacrifice this creature: Destroy target artifact or enchantment."</summary>
	static ActivatedAbilityComponent LiberatorsSacrifice =>
		new(ManaCost.Parse("{1}"), [new Destroy(Ref.Target())])
		{
			Costs = [new SacrificeSelfCost()],
			Targets = [Target(Is.Artifact | Is.Enchantment)],
		};

	/// <summary>Esika's Chariot: "a 2/2 green Cat creature token".</summary>
	static CardData GreenCat => Token("Cat", "Creature — Cat", "2", "2", ["G"]);

	/// <summary>Springheart Nantuko: "a 1/1 green Insect creature token".</summary>
	static CardData Insect => Token("Insect", "Creature — Insect", "1", "1", ["G"]);

	/// <summary>
	/// Eternalize (CR 702.129; Fanatic of Rhonas): "[cost], Exile this card from your graveyard: Create a token that's a
	/// copy of it, except it's a 4/4 black Zombie [its types] with no mana cost. Eternalize only as a sorcery." — the card
	/// copied as it last was in the graveyard (CreateTokenCopy's source snapshot).
	/// </summary>
	static ActivatedAbilityComponent Eternalize(string cost) =>
		new(
			ManaCost.Parse(cost),
			[
				new CreateTokenCopy(Ref.Source)
				{
					SetBase = (4, 4),
					Colors = [ManaColor.Black],
					AlsoSubtypes = ["Zombie"],
					NoManaCost = true,
				},
			]
		)
		{
			Keyword = "Eternalize",
			From = ZoneType.Graveyard,
			Costs = [new ExileSelfCost()],
			SorcerySpeed = true,
		};

	/// <summary>
	/// Persist (CR 702.79; Woodfall Primus): "When this creature dies, if it had no -1/-1 counters on it, return it to the
	/// battlefield under its owner's control with a -1/-1 counter on it." — the counters read as it last was.
	/// </summary>
	static TriggeredAbilityComponent Persist =>
		When(
			TriggerEvent.SelfDies,
			[],
			new MoveTo(Ref.EventCard, ZoneType.Battlefield)
			{
				From = [ZoneType.Graveyard],
				Counters = 1,
				CounterKind = CounterKinds.MinusOne,
			}
		) with
		{
			If = new Not(new Matches(Ref.Source, Is.WithCounter(CounterKinds.MinusOne))),
		};

	/// <summary>Worldspine Wurm: "three 5/5 green Wurm creature tokens with trample".</summary>
	static CardData Wurm => Token("Wurm", "Creature — Wurm", "5", "5", ["G"], ["Trample"]);

	/// <summary>Hexdrinker: "LEVEL 3-7", "LEVEL 8+" — it has N or more level counters (CR 711).</summary>
	static Condition HasLevel(int n) =>
		new AtLeast(Count.CountersOfKind(Ref.Source, CounterKinds.Level), n);

	/// <summary>Baloth Prime: "a tapped 4/4 green Beast creature token" (no trample — FRA's Beast has it).</summary>
	static CardData BalothBeast => Token("Beast", "Creature — Beast", "4", "4", ["G"]);

	/// <summary>Voice of Victory, Jennifer Walters: "Your opponents can't cast spells during your turn."</summary>
	static CantCastOrActivate OpponentsCantCastDuringYourTurn =>
		new()
		{
			By = Whose.Opponent,
			Abilities = false,
			If = new DuringYourTurn(),
		};

	/// <summary>
	/// Mother of Runes: "gains protection from the color of your choice until end of turn"; Giver of Runes: "from
	/// colorless or from the color of your choice" — one choice of six, each granting its Protection.
	/// </summary>
	static GainsChosenKeyword ProtectionOfYourChoice(Ref what, bool colorless)
	{
		ManaColor[] colors =
		[
			ManaColor.White,
			ManaColor.Blue,
			ManaColor.Black,
			ManaColor.Red,
			ManaColor.Green,
		];
		var words = colors
			.Select(c => $"Protection from {c.ToString().ToLowerInvariant()}")
			.ToList();
		List<GameComponent> grants = [.. colors.Select(c => new Protection(Is.Color(c)))];
		if (colorless)
		{
			words.Add("Protection from colorless");
			grants.Add(new Protection(Is.Colorless));
		}
		return new GainsChosenKeyword(what, [.. words]) { Grants = [.. grants] };
	}

	/// <summary>
	/// Fading N (CR 702.32; Parallax Wave): "This enchantment enters with N fade counters on it. At the beginning of
	/// your upkeep, remove a fade counter from it. If you can't, sacrifice it."
	/// </summary>
	static GameComponent[] Fading(int n) =>
		[
			new EntersWithCountersComponent(n) { Kind = CounterKinds.Fade },
			When(
				TriggerEvent.YourUpkeep,
				[],
				new If(
					new AtLeast(Count.CountersOfKind(Ref.Source, CounterKinds.Fade), 1),
					[new RemoveCounters(Ref.Source, 1) { Kind = CounterKinds.Fade }]
				)
				{
					Otherwise = [new Sacrifice(Ref.Source)],
				}
			),
		];

	/// <summary>
	/// Get Lost: "two Map tokens. (They're artifacts with '{1}, {T}, Sacrifice this token: Target creature you control
	/// explores. Activate only as a sorcery.')"
	/// </summary>
	static CreateTokens CreateMap(Count count) =>
		new(Token("Map", "Artifact — Map"), count)
		{
			Abilities =
			[
				new ActivatedAbilityComponent(ManaCost.Parse("{1}"), [new Explore(Ref.Target())])
				{
					Costs = [new TapSelfCost(), new SacrificeSelfCost()],
					Targets = [Target(Is.Creature & Is.Yours)],
					SorcerySpeed = true,
				},
			],
		};

	/// <summary>
	/// Tamiyo, Inquisitive Student; Jace, Vryn's Prodigy: "exile [it], then return [it] to the battlefield transformed under
	/// [its] owner's control" (Fable's chapter III, under its owner).
	/// </summary>
	static MoveTo ReturnTransformed =>
		new(Ref.Source, ZoneType.Exile)
		{
			Then = [new MoveTo(Ref.ThisWay, ZoneType.Battlefield) { Transformed = true }],
		};

	/// <summary>
	/// A transforming double-faced card's back face (CR 712): its second face in the set's JSON, the keywords its text
	/// names (Scryfall merges both faces'), and its abilities — Card.Transform turns the card into it.
	/// </summary>
	static TransformsInto BackFace(string card, params GameComponent[] abilities)
	{
		var data = CardRegistry.DataOf(card);
		var face = data.CardFaces![1];
		return new(
			face,
			[
				.. data.Keywords.Where(k =>
					face.OracleText?.Contains(k, StringComparison.OrdinalIgnoreCase) == true
				),
			],
			[.. abilities]
		);
	}

	/// <summary>
	/// Sunfall: "Incubate X. (Create an Incubator token with X +1/+1 counters on it and '{2}: Transform this token.' It
	/// transforms into a 0/0 Phyrexian artifact creature.)" — a transforming token (TransformsInto on its abilities).
	/// </summary>
	static CreateTokens Incubate(Count x) =>
		new(Token("Incubator", "Artifact — Incubator"), 1)
		{
			Counters = x,
			Abilities =
			[
				new ActivatedAbilityComponent(ManaCost.Parse("{2}"), [new Transform(Ref.Source)]),
				new TransformsInto(
					new CardFace(
						"Phyrexian",
						null,
						"Artifact Creature — Phyrexian",
						null,
						"0",
						"0",
						null,
						[]
					),
					[],
					[]
				),
			],
		};

	/// <summary>
	/// Reconfigure N (CR 702.151; Lion Sash): "{N}: Attach to target creature you control; or unattach from a creature.
	/// Reconfigure only as a sorcery. While attached, this isn't a creature." — two abilities, and Mogis' type change on
	/// itself while attached; Attachments.Detach lets an Equipment with reconfigure stay on as a creature (CR 301.5c).
	/// </summary>
	static GameComponent[] Reconfigure(string cost) =>
		[
			new ActivatedAbilityComponent(
				ManaCost.Parse(cost),
				[new Attach(Ref.Source, Ref.Target())]
			)
			{
				SorcerySpeed = true,
				Targets = [Target(Is.Creature & Is.Yours & !Is.Source)],
			},
			new ActivatedAbilityComponent(ManaCost.Parse(cost), [new Unattach(Ref.Source)])
			{
				SorcerySpeed = true,
				OnlyIf = new Matches(Ref.Source, Is.Attached),
			},
			new BecomesType(Is.Source & Is.Attached) { Remove = CardType.Creature },
		];

	/// <summary>Ocelot Pride: "a 1/1 white Cat creature token".</summary>
	static CardData Cat => Token("Cat", "Creature — Cat", "1", "1", ["W"]);

	/// <summary>Ajani, Nacatl Pariah: "a 2/1 white Cat Warrior creature token".</summary>
	static CardData CatWarrior => Token("Cat Warrior", "Creature — Cat Warrior", "2", "1", ["W"]);

	/// <summary>Fable of the Mirror-Breaker, chapter I: "a 2/2 red Goblin Shaman creature token".</summary>
	static CardData GoblinShaman =>
		Token("Goblin Shaman", "Creature — Goblin Shaman", "2", "2", ["R"]);

	/// <summary>
	/// Delirium (Unholy Heat, Dragon's Rage Channeler): "if there are four or more card types among cards in your
	/// graveyard".
	/// </summary>
	static Condition Delirium => new AtLeast(Count.CardTypesAmong(Ref.CardsInYourGraveyard), 4);

	/// <summary>Gut, True Soul Zealot: "a 4/1 black Skeleton creature token with menace".</summary>
	static CardData Skeleton =>
		Token("Skeleton", "Creature — Skeleton", "4", "1", ["B"], ["Menace"]);

	/// <summary>
	/// Saddle N (CR 702.171, Caustic Bronco): "Tap any number of other creatures you control with total power N or more:
	/// This Mount becomes saddled until end of turn. Saddle only as a sorcery." — teamwork's cost (TapUntappedCost
	/// { TotalPower }, the creatures picked after activating) on a sorcery-speed ability; "saddled" a keyword granted
	/// until end of turn (goad's shape), so it ends at cleanup or as the Mount leaves. ponytail: a saddled Mount that
	/// loses all abilities stops reading as saddled (CR 702.171b: a designation) — no card reads it but the Mount's own,
	/// which it has lost too; a status like goad's stamp read when one does (crew is the other half, later).
	/// </summary>
	static ActivatedAbilityComponent Saddle(int power) =>
		new(ManaCost.None, [new ModifyUntilEndOfTurn(Ref.Source, 0, 0, "Saddled")])
		{
			Costs = [new TapUntappedCost(0, Is.Creature & !Is.Source) { TotalPower = power }],
			SorcerySpeed = true,
		};

	/// <summary>Caustic Bronco: "if this creature isn't saddled" — Saddle's grant.</summary>
	static Filter Saddled => Is.With("Saddled");

	/// <summary>
	/// Magecraft (Sedgemoor Witch): "Whenever you cast or copy an instant or sorcery spell, [effects]" — two triggers, one
	/// on the cast and one on the copy (SpellCopiedEvent): a copy is never cast. ponytail: one printed ability, two on the
	/// card — a rule counting a permanent's triggered abilities would count two (none does; DesignNotes).
	/// </summary>
	static TriggeredAbilityComponent[] Magecraft(params GameAction[] effects) =>
		[
			When(
				new Trigger(EventKind.SpellCast)
				{
					By = Whose.You,
					Subject = Is.Instant | Is.Sorcery,
				},
				[],
				effects
			),
			When(
				new Trigger(EventKind.SpellCopied)
				{
					By = Whose.You,
					Subject = Is.Instant | Is.Sorcery,
				},
				[],
				effects
			),
		];

	/// <summary>
	/// Orcish Bowmasters: "this creature deals 1 damage to any target. Then amass Orcs 1." Amass N (CR 701.47): "If you
	/// don't control an Army creature, create a 0/0 black Orc Army creature token. Put N +1/+1 counters on an Army you
	/// control." ponytail: with two or more Armies (a changeling) each gets them — no choice of one; and an Army that
	/// isn't an Orc doesn't become one. Make it a ChoosePermanentChoice when a deck has two.
	/// </summary>
	static GameAction[] BowmastersShot =>
		[
			new DealDamage(Ref.Target(), 1),
			new If(
				new Not(new AtLeast(Count.NumberOf(Ref.Permanents(YourArmies)), 1)),
				[new CreateTokens(OrcArmy, 1)]
			),
			new PutCounters(Ref.Permanents(YourArmies), 1),
		];

	static Filter YourArmies => Is.Creature & Is.Yours & Is.Subtype("Army");

	/// <summary>
	/// Black Widow, Super Spy: "you may cast the exiled nonland card until end of turn and mana of any type can be spent to
	/// cast that spell" — the lands exiled on the way are marked too, but CastOnly: never played.
	/// </summary>
	static MakePlayable WidowsCast =>
		new(
			Ref.ThisWay,
			new()
			{
				ThisTurn = true,
				AnyType = true,
				CastOnly = true,
			}
		);

	/// <summary>
	/// Pyrogoyf, Barrowgoyf: "power is equal to the number of card types among cards in all graveyards and its toughness
	/// is equal to that number plus 1" — the plus 1 is the printed */1+*'s base toughness.
	/// </summary>
	static StaticEffectComponent LhurgoyfSize =>
		new(Is.Source)
		{
			Power = Count.CardTypesAmong(Ref.All(ZoneType.Graveyard)),
			Toughness = Count.CardTypesAmong(Ref.All(ZoneType.Graveyard)),
		};

	/// <summary>
	/// Shallow Grave, Corpse Dance: "Return the top creature card of your graveyard to the battlefield. That creature
	/// gains haste until end of turn. Exile it at the beginning of the next end step."
	/// </summary>
	static MoveTo ShallowGrave =>
		new(
			Ref.All(ZoneType.Graveyard, Is.Creature, Ref.You) with
			{
				Top = true,
			},
			ZoneType.Battlefield
		)
		{
			From = [ZoneType.Graveyard],
			ExileAtNextEndStep = true,
			Then = [new ModifyUntilEndOfTurn(Ref.ThisWay, 0, 0, "Haste")],
		};

	/// <summary>
	/// Archon of Cruelty: "target opponent sacrifices a creature or planeswalker of their choice, discards a card, and
	/// loses 3 life. You draw a card and gain 3 life." — on entering and on attacking alike.
	/// </summary>
	static GameAction[] ArchonsCruelty =>
		[
			new EachSacrifices(Ref.Target(), Is.Creature | Is.Planeswalker),
			new Discard(Ref.Target(), 1),
			new LoseLife(Ref.Target(), 3),
			new DrawCards(Ref.You, 1),
			new GainLife(Ref.You, 3),
		];

	/// <summary>
	/// Teamwork N (We Say Thee Nay!): "As an additional cost to cast this spell, you may tap any number of creatures you
	/// control with total power N or more." — an optional additional cost, kicker's cast (one with it, one without);
	/// "if this spell was cast using teamwork" is WasKicked.
	/// </summary>
	static KickerComponent Teamwork(int power) =>
		new(ManaCost.None)
		{
			Costs = [new TapUntappedCost(0, Is.Creature) { TotalPower = power }],
			Word = "teamwork",
		};

	/// <summary>An adventurer card's Adventure (CR 715): its second face in the set's JSON, and its text.</summary>
	static AdventureComponent Adventure(
		string card,
		TargetSpec[] targets,
		params GameAction[] effects
	) => new(CardRegistry.DataOf(card).CardFaces![1], Spell(targets, effects));

	/// <summary>
	/// Manifest dread (Abhorrent Oculus): "Look at the top two cards of your library. Put one onto the battlefield face
	/// down as a 2/2 creature and the other into your graveyard."
	/// </summary>
	public static LookAndTake ManifestDread =>
		new(2)
		{
			Must = true,
			To = ZoneType.Battlefield,
			FaceDown = true,
			Rest = TheRest.Graveyard,
		};

	/// <summary>Feywild Caretaker: "a 1/1 blue Faerie Dragon creature token with flying".</summary>
	static CardData FaerieDragon =>
		Token("Faerie Dragon", "Creature — Faerie Dragon", "1", "1", ["U"], ["Flying"]);

	/// <summary>
	/// Transmute Artifact: "Sacrifice an artifact. If you do, search your library for an artifact card. If that card's
	/// mana value is less than or equal to the sacrificed artifact's mana value, put it onto the battlefield. If it's
	/// greater, you may pay {X}, where X is the difference. If you do, put it onto the battlefield. If you don't, put
	/// it into its owner's graveyard. Then shuffle." — the sacrificed is Ref.ThisWay, the found card Ref.Chosen.
	/// </summary>
	static SpellEffectsComponent TransmuteArtifact
	{
		get
		{
			var found = Count.GreatestManaValueOf(Ref.Chosen);
			var sacrificed = Count.GreatestManaValueOf(Ref.ThisWay);
			GameAction Put(ZoneType to) => new MoveTo(Ref.Chosen, to) { From = [ZoneType.Library] };
			return Spell(
				[],
				new MaySacrifice(
					Is.Artifact,
					[
						new SearchLibrary(CardType.Artifact, ZoneType.Battlefield)
						{
							Then =
							[
								new If(
									new AtLeast(sacrificed, 0) { MoreThan = found },
									[Put(ZoneType.Battlefield)]
								)
								{
									Otherwise =
									[
										MayPay(
											"Pay the difference?",
											new PayMana(ManaCost.None, [Put(ZoneType.Battlefield)])
											{
												Generic = found,
												ReducedBy = sacrificed,
											},
											Put(ZoneType.Graveyard)
										),
									],
								},
							],
						},
					]
				)
				{
					Must = true,
				}
			);
		}
	}

	/// <summary>
	/// Timetwister, Echo of Eons: "Each player shuffles their hand and graveyard into their library, then draws
	/// seven cards." — the spell itself is still on the stack, so it isn't shuffled in. Each player shuffles once,
	/// with or without a hand or a graveyard to shuffle in.
	/// </summary>
	static SpellEffectsComponent Timetwister => Spell([], [.. TimetwisterSteps]);

	/// <summary>The same, as steps: Time Spiral's text has it between "Exile Time Spiral" and "You untap up to six lands".</summary>
	static GameAction[] TimetwisterSteps =>
		[
			new MoveTo(Ref.All(ZoneType.Hand, null, Ref.EachPlayer), ZoneType.Library),
			new MoveTo(Ref.All(ZoneType.Graveyard, null, Ref.EachPlayer), ZoneType.Library),
			new ShuffleLibrary(Ref.EachPlayer),
			new DrawCards(Ref.EachPlayer, 7),
		];

	/// <summary>Sewer-veillance Cam: "you may tap or untap target creature".</summary>
	static May MayTapOrUntapTheTarget =>
		new(
			"Tap or untap the creature?",
			new Always(),
			[
				new If(new Matches(Ref.Target(0), Is.Tapped), [new Untap(Ref.Target())])
				{
					Otherwise = [new Tap(Ref.Target())],
				},
			]
		);

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

	/// <summary>Urza, Lord High Artificer: "a 0/0 colorless Construct artifact creature token".</summary>
	static CardData Construct => Token("Construct", "Artifact Creature — Construct", "0", "0");

	/// <summary>Sedgemoor Witch: "a 1/1 black and green Pest creature token" (its life on dying is the script's).</summary>
	static CardData Pest => Token("Pest", "Creature — Pest", "1", "1", ["B", "G"]);

	/// <summary>Mole Man, Moloid Master: "a 1/1 green Minion creature token named Moloid" (its mill is the script's).</summary>
	static CardData Moloid => Token("Moloid", "Creature — Minion", "1", "1", ["G"]);

	/// <summary>Malevolent Rumble: "a 0/1 colorless Eldrazi Spawn creature token" (its mana is the script's).</summary>
	static CardData EldraziSpawn => Token("Eldrazi Spawn", "Creature — Eldrazi Spawn", "0", "1");

	/// <summary>Titania, Protector of Argoth: "a 5/3 green Elemental creature token".</summary>
	static CardData TitaniasElemental =>
		Token("Elemental", "Creature — Elemental", "5", "3", ["G"]);

	/// <summary>Amass Orcs: "a 0/0 black Orc Army creature token".</summary>
	static CardData OrcArmy => Token("Orc Army", "Creature — Orc Army", "0", "0", ["B"]);

	/// <summary>Legion Extruder: "a 3/3 colorless Golem artifact creature token".</summary>
	static CardData Golem => Token("Golem", "Artifact Creature — Golem", "3", "3");

	/// <summary>"Exile this card from your hand: Add {G}." — a mana ability of the card in hand.</summary>
	static ManaAbilityComponent SpiritGuide(ManaColor color) =>
		new(color) { From = ZoneType.Hand, Costs = [new ExileSelfCost()] };
}
