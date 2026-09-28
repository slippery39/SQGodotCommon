using System.Collections.Immutable;
using ImmutableGameObjects;

namespace KinCore.Party;

/// <summary>
/// **Cards that more than one content file uses** — monster decks in `PartyWorld`, rewards and
/// scenarios in `PartyContent`. Kept apart and dependency-free so the two never initialise each
/// other. Each card is classified (docs/paper/round-one-synergies.md).
/// </summary>
public static class PartyCards
{
	public static KinCard Card(string name, int cost, string text, params GameAction[] steps) =>
		new()
		{
			Name = name,
			Cost = cost,
			// The text rides on the first step only — it is the card's one rules line.
			Effects =
			[
				.. steps.Select(
					(t, i) => new KinEffect { Template = t, Text = i == 0 ? text : "" }
				),
			],
		};

	// ===== SPELLCRAFT — damage from the card itself: no aim needed.

	/// <summary>Enabler · filler: a fine card anywhere, and the spell every Spellcraft payoff counts.</summary>
	public static readonly KinCard Zap = Card(
		"Zap",
		1,
		"Drop on a foe: deal 4.",
		new SpellDamageAction { Amount = 4 }
	) with
	{
		Family = Family.Ember,
	};

	/// <summary>Enabler · standard: the answer to a swarm.</summary>
	public static readonly KinCard Arc = Card(
		"Arc",
		2,
		"Deal 2 to every foe.",
		new SpellDamageAction { Amount = 2, Target = SpellTarget.All }
	) with
	{
		Family = Family.Ember,
	};

	/// <summary>Bridge (Spellcraft and Discard) · standard: a spell whether you cast it or toss it.</summary>
	public static readonly KinCard SparkScroll = Card(
		"Spark Scroll",
		1,
		"Drop on a foe: deal 3. TOSS: deal 3 to a random foe.",
		new SpellDamageAction { Amount = 3 }
	) with
	{
		Components =
		[
			new Trigger
			{
				Name = "Toss",
				When = new OnSelfDiscarded(),
				Effects = [new SpellDamageAction { Amount = 3, Target = SpellTarget.Random }],
			},
		],
		Family = Family.Ember,
	};

	/// <summary>Payoff · narrow: nothing on its own, the whole turn's spells again after them.</summary>
	public static readonly KinCard Overload = Card(
		"Overload",
		2,
		"Drop on a foe: deal all the spell damage dealt this turn.",
		new SpellDamageAction { FromSpellDamageThisTurn = true }
	) with
	{
		Family = Family.Ember,
	};

	/// <summary>Payoff · narrow: a turn of dropped spells, each three wide.</summary>
	public static readonly KinCard Focus = Card(
		"Focus",
		1,
		"This turn, your spells also hit the foe behind their target.",
		new SplashSpellsAction()
	) with
	{
		Family = Family.Ember,
	};

	// ===== THE LINE — Gale's deck: reorder THEIR front.

	/// <summary>Enabler (Control) · standard: their front two swap — Off-Balance while Gale stands.</summary>
	public static readonly KinCard Gust = Card(
		"Gust",
		1,
		"Their front two swap. A lone foe takes 5 instead.",
		new GustAction { AloneDamage = 5 }
	) with
	{
		Family = Family.Storm,
	};

	/// <summary>Enabler (Control, Draw) · standard.</summary>
	public static readonly KinCard Tailwind = Card(
		"Tailwind",
		1,
		"Their front two swap (a lone foe takes 3). Draw a card.",
		new GustAction { AloneDamage = 3 },
		new DrawAction()
	) with
	{
		Family = Family.Storm,
	};

	// ===== SUMMON — tokens: bodies that fade.

	private static Intent Still(string name) =>
		new()
		{
			Name = name,
			Kind = IntentType.Block,
			Amount = 0,
		};

	private static Intent Hits(string name, int amount) =>
		new()
		{
			Name = name,
			Kind = IntentType.Attack,
			Amount = amount,
		};

	/// <summary>A wall that stays two turns, and shields its neighbours when it falls.</summary>
	public static readonly TokenTemplate Sprout =
		new(
			new PartyCompanion("Sprout", 3, 0, [Still("Rooted")])
			{
				Abilities = [new FaintShield { Amount = 3 }],
				Family = Family.Grove,
			},
			FadesIn: 2
		);

	/// <summary>A spell that walks: fast, one hit, gone.</summary>
	public static readonly TokenTemplate Spark =
		new(new PartyCompanion("Spark", 1, 0, [Hits("Spark", 2)]), FadesIn: 1);

	/// <summary>The answer to snipers and hunters: BACK and HUNT attacks aim at it.</summary>
	public static readonly TokenTemplate Decoy =
		new(
			new PartyCompanion("Decoy", 5, 0, [Still("Lure")]) { Abilities = [new Lure()] },
			FadesIn: 1
		);

	/// <summary>The Broodvine's brood — yours when it is yours, the foes' when it is wild.</summary>
	public static readonly TokenTemplate Grub =
		new(
			new PartyCompanion("Grub", 2, 0, [Hits("Bite", 2)]) { Family = Family.Grove },
			FadesIn: 2
		);

	/// <summary>Enabler (Summon, Block) · filler: a movable wall.</summary>
	public static readonly KinCard Sow = Card(
		"Sow",
		1,
		"Summon a Sprout in front (3 HP, 2 turns). When it falls, the ones beside it gain 3 Block.",
		new SummonTokenAction { Token = Sprout }
	) with
	{
		Family = Family.Grove,
	};

	/// <summary>Bridge (Summon and Spellcraft) · standard: two hits that need a column each.</summary>
	public static readonly KinCard CallSparks = Card(
		"Call Sparks",
		1,
		"Summon two Sparks in front (1 HP, hit 2). They fade after this round.",
		new SummonTokenAction { Token = Spark, Count = 2 }
	) with
	{
		Family = Family.Grove,
	};

	/// <summary>Enabler (Summon) + answer (homing) · standard.</summary>
	public static readonly KinCard DecoyCard = Card(
		"Decoy",
		1,
		"Summon a Decoy in front (5 HP) for a round. Back and hunting attacks aim at it.",
		new SummonTokenAction { Token = Decoy }
	) with
	{
		Family = Family.Grove,
	};

	/// <summary>Payoff · narrow: nothing without tokens.</summary>
	public static readonly KinCard Swarm = Card(
		"Swarm",
		2,
		"Each of your tokens attacks their front now: 2 + Power.",
		new TokensAttackAction { Amount = 2 }
	) with
	{
		Family = Family.Grove,
	};

	/// <summary>Payoff (Summon → Draw, Surge) · narrow: a token spent, a hand refilled.</summary>
	public static readonly KinCard Offering = Card(
		"Offering",
		0,
		"Drop on a token: it faints. Draw 2 cards, +1 energy.",
		new SacrificeTokenAction(),
		new DrawAction { Count = 2 },
		new GainEnergyAction()
	) with
	{
		Family = Family.Grove,
	};

	// ===== SURGE — more energy than the turn allows, at a price.

	/// <summary>Enabler · standard: borrowing, so the tension is built in.</summary>
	public static readonly KinCard Surge = Card(
		"Surge",
		0,
		"+2 energy. Next turn, 1 less.",
		new GainEnergyAction { Amount = 2 },
		new BorrowEnergyAction { Amount = 1 }
	) with
	{
		Family = Family.Storm,
	};

	/// <summary>Enabler · standard: the bomb's discount, or two cheap cards' worth.</summary>
	public static readonly KinCard Quicken = Card(
		"Quicken",
		1,
		"The next card you play this turn costs 0.",
		new NextCardFreeAction()
	) with
	{
		Family = Family.Storm,
	};

	/// <summary>Bridge (Surge and Combat) · standard: pays after a kill made with your hand.</summary>
	public static readonly KinCard BattleCry = Card(
		"Battle Cry",
		1,
		"Draw a card. If a foe died this turn, +2 energy.",
		new DrawAction(),
		new GainEnergyIfFoeDiedAction { Amount = 2 }
	) with
	{
		Family = Family.Storm,
	};

	/// <summary>Payoff · pushed, rare: all your energy, three wide.</summary>
	public static readonly KinCard Unleash = Card(
		"Unleash",
		0,
		"Costs all your energy. It attacks their whole line now: 4 per energy.",
		new StrikeAction { PerX = 4, Aim = Aim.Sweep }
	) with
	{
		Components = [new SpendsAllEnergy()],
		Family = Family.Storm,
	};

	/// <summary>Payoff (Spellcraft) · narrow: the four-energy spell a Surge turn is for.</summary>
	public static readonly KinCard Meteor = Card(
		"Meteor",
		4,
		"Drop on a foe: deal 14, and 4 to the one behind it.",
		new SpellDamageAction { Amount = 14 },
		new SpellDamageAction { Amount = 4, Target = SpellTarget.Behind }
	) with
	{
		Family = Family.Ember,
	};

	// ===== DISCARD + DRAW

	/// <summary>Enabler (both) · filler: card selection anywhere. MtgCore's looting pipeline.</summary>
	public static readonly KinCard Sift = Card(
		"Sift",
		0,
		"Draw 2 cards, then discard 1.",
		PartyDiscard.DrawThenDiscard(2, 1)
	) with
	{
		Family = Family.Mire,
	};

	/// <summary>Enabler (both) · standard: as many discards as you want to pay for.</summary>
	public static readonly KinCard Rummage = Card(
		"Rummage",
		1,
		"Discard any number of cards, then draw that many.",
		PartyDiscard.DiscardThenDraw()
	) with
	{
		Family = Family.Mire,
	};

	/// <summary>Enabler (Draw) · filler, bridge to Block: the Inkling's cantrip.</summary>
	public static readonly KinCard Jot = Card(
		"Jot",
		1,
		"Gain 4 Block. Draw a card.",
		new GuardAction { Amount = 4 },
		new DrawAction()
	) with
	{
		Family = Family.Mire,
	};

	/// <summary>
	/// Bridge (Discard → Surge, Block) · standard. **TOSS**: its ability sits on the card and fires
	/// when a card or ability discards it.
	/// </summary>
	public static readonly KinCard Ration = Card(
		"Ration",
		1,
		"Gain 5 Block. TOSS: +1 energy.",
		new GuardAction { Amount = 5 }
	) with
	{
		Components =
		[
			new Trigger
			{
				Name = "Toss",
				When = new OnSelfDiscarded(),
				Effects = [new GainEnergyAction()],
			},
		],
		Family = Family.Mire,
	};

	/// <summary>Payoff (Discard, Combat) · narrow: 3 energy alone, free after three discards.</summary>
	public static readonly KinCard ScrapHammer = Card(
		"Scrap Hammer",
		3,
		"It attacks their front now: 6 + Power. Costs 1 less per card discarded this turn.",
		new StrikeAction { Amount = 6 }
	) with
	{
		Components = [new CostReduction { PerDiscardThisTurn = 1 }],
		Family = Family.Mire,
	};

	/// <summary>Payoff (Draw) · narrow: weak off an empty hand, big after Sift.</summary>
	public static readonly KinCard PageStorm = Card(
		"Page Storm",
		1,
		"It attacks their front now: Power + 1 per card in your hand.",
		new StrikeAction { Amount = 0, PlusCardsInHand = true }
	) with
	{
		Family = Family.Mire,
	};

	// ===== GROVE — the engine is TIME (KinFamiliesPlan.md §3)

	/// <summary>Enabler · the key to non-token Grove: any monster grows.</summary>
	public static readonly KinCard Graft = Card(
		"Graft",
		1,
		"It gains GROW for this fight: +1 Power and +2 HP each turn.",
		new GraftAction()
	) with
	{
		Family = Family.Grove,
	};

	/// <summary>Enabler for THORNWALL: Block that stays.</summary>
	public static readonly KinCard Root = Card(
		"Root",
		1,
		"Gain 6 ROOTED Block: it does not vanish at your turn start.",
		new RootAction { Amount = 6 }
	) with
	{
		Family = Family.Grove,
	};

	/// <summary>Accelerator: time, now.</summary>
	public static readonly KinCard Overgrow = Card(
		"Overgrow",
		2,
		"Everything of yours with GROW grows twice, now.",
		new GrowNowAction { Times = 2 }
	) with
	{
		Family = Family.Grove,
	};

	/// <summary>Bridge: growth becomes a wall.</summary>
	public static readonly KinCard Thicket = Card(
		"Thicket",
		1,
		"Each of your Grove monsters gains Block equal to its Power.",
		new ThicketAction()
	) with
	{
		Family = Family.Grove,
	};

	/// <summary>THE BIG TURN (an experiment — KinFamiliesPlan.md §6).</summary>
	public static readonly KinCard Harvest = Card(
		"Harvest",
		2,
		"Each of your tokens falls, and deals its HP to their front.",
		new HarvestAction()
	) with
	{
		Family = Family.Grove,
	};

	/// <summary>Payoff for a patient wall.</summary>
	public static readonly KinCard DeepRoots = Card(
		"Deep Roots",
		0,
		"Its ROOTED Block doubles.",
		new DeepRootsAction()
	) with
	{
		Family = Family.Grove,
	};

	// ===== EMBER — the engine is SPELL COUNT (KinFamiliesPlan.md §4)

	/// <summary>Enabler: Kindle without a spell.</summary>
	public static readonly KinCard Stoke = Card(
		"Stoke",
		1,
		"+3 KINDLE. Draw a card.",
		new KindleAction { Amount = 3 },
		new DrawAction()
	) with
	{
		Family = Family.Ember,
	};

	/// <summary>Bridge: the fire keeps you alive.</summary>
	public static readonly KinCard Cinderwall = Card(
		"Cinderwall",
		1,
		"It gains Block equal to your KINDLE.",
		new CinderwallAction()
	) with
	{
		Family = Family.Ember,
	};

	/// <summary>Accelerator: a spell twice is two Kindle.</summary>
	public static readonly KinCard FanTheFlames = Card(
		"Fan the Flames",
		1,
		"Your next spell is cast twice.",
		new FanFlamesAction()
	) with
	{
		Family = Family.Ember,
	};

	/// <summary>THE BIG TURN (an experiment — KinFamiliesPlan.md §6).</summary>
	public static readonly KinCard Flashpoint = Card(
		"Flashpoint",
		2,
		"Drop on a foe: spend all your KINDLE, and it takes 3 for each.",
		new FlashpointAction { PerKindle = 3 }
	) with
	{
		Family = Family.Ember,
	};
}
