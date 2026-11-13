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
		};

	/// <summary>"Exile this card from your hand: Add {G}." — a mana ability of the card in hand.</summary>
	static ManaAbilityComponent SpiritGuide(ManaColor color) =>
		new(color) { From = ZoneType.Hand, Costs = [new ExileSelfCost()] };
}
