using System.Collections.Immutable;
using ImmutableGameObjects;

namespace KinCore.Party;

/// <summary>
/// **EMBER's cards and monsters — DRAFT 2, as approved** (`KinFamiliesPlan.md`; Shayne, 2026-09-28:
/// "push the cards' limits first, then nerf"). Every number is a guess to tune after play. The rules
/// are in `PartyEmber`.
/// </summary>
public static class EmberCards
{
	private static KinCard Card(
		string name,
		int cost,
		Rarity rarity,
		string text,
		params GameAction[] steps
	) => PartyCards.Card(name, cost, text, steps) with { Family = Family.Ember, Rarity = rarity };

	private static KinCard Plus(KinCard card, KinCard better) => PartyCards.Plus(card, better);

	private static SpellDamageAction Deal(int amount) => new() { Amount = amount };

	private static SpellDamageAction DealAll(int amount) =>
		new() { Amount = amount, Target = SpellTarget.All };

	private const Rarity C = Rarity.Common;
	private const Rarity U = Rarity.Uncommon;
	private const Rarity R = Rarity.Rare;

	// ===== Commons

	public static readonly KinCard Zap = Plus(
		Card("Zap", 1, C, "Drop on a foe: deal 8.", Deal(8)),
		Card("Zap", 1, C, "Drop on a foe: deal 11.", Deal(11))
	);

	public static readonly KinCard Kindle = Plus(
		Card(
			"Kindle",
			1,
			C,
			"+3 Spell Power this turn. Draw 2 cards.",
			new SpellPowerAction { Amount = 3 },
			new DrawAction { Count = 2 }
		),
		Card(
			"Kindle",
			1,
			C,
			"+4 Spell Power this turn. Draw 2 cards.",
			new SpellPowerAction { Amount = 4 },
			new DrawAction { Count = 2 }
		)
	);

	public static readonly KinCard Spark = Plus(
		Card("Spark", 0, C, "Drop on a foe: deal 4.", Deal(4)),
		Card("Spark", 0, C, "Drop on a foe: deal 6.", Deal(6))
	);

	public static readonly KinCard EmberDart = Plus(
		Card("Ember Dart", 0, C, "Drop on a foe: deal 3. Draw a card.", Deal(3), new DrawAction()),
		Card("Ember Dart", 0, C, "Drop on a foe: deal 5. Draw a card.", Deal(5), new DrawAction())
	);

	/// <summary>The Sparks Kindling makes — plain, never upgraded.</summary>
	private static readonly KinCard MadeSpark = Card(
		"Spark",
		0,
		C,
		"Drop on a foe: deal 4.",
		Deal(4)
	);

	public static readonly KinCard Kindling = Plus(
		Card(
			"Kindling",
			1,
			C,
			"Add 2 Sparks to your hand.",
			new AddCardsAction { Card = MadeSpark, Count = 2 }
		),
		Card(
			"Kindling",
			1,
			C,
			"Add 3 Sparks to your hand.",
			new AddCardsAction { Card = MadeSpark, Count = 3 }
		)
	);

	public static readonly KinCard Singe = Plus(
		Card(
			"Singe",
			1,
			C,
			"Drop on a foe: deal 6. Apply 5 Burn.",
			Deal(6),
			new BurnAction { Amount = 5 }
		),
		Card(
			"Singe",
			1,
			C,
			"Drop on a foe: deal 8. Apply 6 Burn.",
			Deal(8),
			new BurnAction { Amount = 6 }
		)
	);

	public static readonly KinCard FireFan = Plus(
		Card(
			"Fire Fan",
			1,
			C,
			"Deal 5 to every foe. Apply 3 Burn to each.",
			DealAll(5),
			new BurnAction { Amount = 3, All = true }
		),
		Card(
			"Fire Fan",
			1,
			C,
			"Deal 7 to every foe. Apply 4 Burn to each.",
			DealAll(7),
			new BurnAction { Amount = 4, All = true }
		)
	);

	public static readonly KinCard Flicker = Plus(
		Card("Flicker", 1, C, "Draw 3 cards.", new DrawAction { Count = 3 }),
		Card("Flicker", 0, C, "Draw 3 cards.", new DrawAction { Count = 3 })
	);

	public static readonly KinCard ChargeUp = Plus(
		Card(
			"Charge Up",
			1,
			C,
			"Next turn, +2 energy. Draw a card.",
			new EnergyNextTurnAction { Amount = 2 },
			new DrawAction()
		),
		Card(
			"Charge Up",
			1,
			C,
			"Next turn, +3 energy. Draw a card.",
			new EnergyNextTurnAction { Amount = 3 },
			new DrawAction()
		)
	);

	public static readonly KinCard FlameWard = Plus(
		Card(
			"Flame Ward",
			1,
			C,
			"Gain Block: 10 + three times your Spell Power.",
			new EmberBlockAction { Amount = 10, PerSpellPower = 3 }
		),
		Card(
			"Flame Ward",
			1,
			C,
			"Gain Block: 13 + three times your Spell Power.",
			new EmberBlockAction { Amount = 13, PerSpellPower = 3 }
		)
	);

	public static readonly KinCard HeatHaze = Plus(
		Card(
			"Heat Haze",
			1,
			C,
			"Gain 9 Block. If you have cast 2 spells this turn, 12 more.",
			new EmberBlockAction
			{
				Amount = 9,
				IfSpellsCast = 2,
				Bonus = 12,
			}
		),
		Card(
			"Heat Haze",
			1,
			C,
			"Gain 11 Block. If you have cast 2 spells this turn, 14 more.",
			new EmberBlockAction
			{
				Amount = 11,
				IfSpellsCast = 2,
				Bonus = 14,
			}
		)
	);

	// ===== Uncommons

	public static readonly KinCard Stoke = Plus(
		Card(
			"Stoke",
			1,
			U,
			"+2 Spell Power for the rest of the fight.",
			new SpellPowerAction { Amount = 2, ForFight = true }
		),
		Card(
			"Stoke",
			1,
			U,
			"+3 Spell Power for the rest of the fight.",
			new SpellPowerAction { Amount = 3, ForFight = true }
		)
	);

	public static readonly KinCard FanTheFlames = Plus(
		Card(
			"Fan the Flames",
			1,
			U,
			"Your next 2 spells this turn are cast twice.",
			new CastTwiceAction { Count = 2 }
		),
		Card(
			"Fan the Flames",
			0,
			U,
			"Your next 2 spells this turn are cast twice.",
			new CastTwiceAction { Count = 2 }
		)
	);

	public static readonly KinCard SpellSurge = Plus(
		// Cost 3 and rare (Shayne, 2026-09-30): at 1 it refunded a whole turn with no setup. It takes
		// the turn's energy now, so it pays only in a deck built for it.
		Card(
			"Spell Surge",
			3,
			R,
			"Your spells cost 1 less this turn.",
			new SpellDiscountAction { Amount = 1 }
		),
		Card(
			"Spell Surge",
			2,
			R,
			"Your spells cost 1 less this turn.",
			new SpellDiscountAction { Amount = 1 }
		)
	);

	public static readonly KinCard Wildfire = Plus(
		Card(
			"Wildfire",
			1,
			U,
			"Drop on a foe: deal 9. Costs 0 if you have cast 2 spells this turn.",
			Deal(9)
		) with
		{
			Components = [new FreeAfterSpells { Spells = 2 }],
		},
		Card(
			"Wildfire",
			1,
			U,
			"Drop on a foe: deal 12. Costs 0 if you have cast 2 spells this turn.",
			Deal(12)
		) with
		{
			Components = [new FreeAfterSpells { Spells = 2 }],
		}
	);

	public static readonly KinCard ChainLightning = Plus(
		Card(
			"Chain Lightning",
			1,
			U,
			"Drop on a foe: deal 5 for each spell cast this turn, this one too.",
			new SpellDamageAction { PerSpellThisTurn = 5 }
		),
		Card(
			"Chain Lightning",
			1,
			U,
			"Drop on a foe: deal 6 for each spell cast this turn, this one too.",
			new SpellDamageAction { PerSpellThisTurn = 6 }
		)
	);

	public static readonly KinCard Ignite = Plus(
		// A floor of its own (the balance pass, 2026-09-30): a combo piece is never dead in hand.
		Card(
			"Ignite",
			1,
			U,
			"Drop on a foe: apply 3 Burn, then double its Burn.",
			new BurnAction { Amount = 3 },
			new BurnAction { Double = true }
		),
		Card(
			"Ignite",
			0,
			U,
			"Drop on a foe: apply 3 Burn, then double its Burn.",
			new BurnAction { Amount = 3 },
			new BurnAction { Double = true }
		)
	);

	public static readonly KinCard SpreadingFlames = Plus(
		Card(
			"Spreading Flames",
			1,
			U,
			"Every foe's Burn rises to the highest among them, then 3 more.",
			new BurnAction { RiseToHighest = true, Amount = 3 }
		),
		Card(
			"Spreading Flames",
			1,
			U,
			"Every foe's Burn rises to the highest among them, then 5 more.",
			new BurnAction { RiseToHighest = true, Amount = 5 }
		)
	);

	public static readonly KinCard SmokeScreen = Plus(
		Card(
			"Smoke Screen",
			1,
			U,
			"Gain 6 Block for each spell you have cast this turn.",
			new EmberBlockAction { PerSpellThisTurn = 6 }
		),
		Card(
			"Smoke Screen",
			1,
			U,
			"Gain 8 Block for each spell you have cast this turn.",
			new EmberBlockAction { PerSpellThisTurn = 8 }
		)
	);

	public static readonly KinCard CinderShield = Plus(
		Card(
			"Cinder Shield",
			1,
			U,
			"Gain Block equal to three times the Burn on their line.",
			new EmberBlockAction { PerBurnOnTheirLine = 3 }
		),
		Card(
			"Cinder Shield",
			1,
			U,
			"Gain Block equal to four times the Burn on their line.",
			new EmberBlockAction { PerBurnOnTheirLine = 4 }
		)
	);

	public static readonly KinCard HeatSurge = Plus(
		Card("Heat Surge", 0, U, "+2 energy.", new GainEnergyAction { Amount = 2 }),
		Card("Heat Surge", 0, U, "+3 energy.", new GainEnergyAction { Amount = 3 })
	);

	// ===== Rares

	public static readonly KinCard Flashpoint = Plus(
		Card(
			"Flashpoint",
			2,
			R,
			"Drop on a foe: deal 6 + three times its Burn. The Burn stays.",
			new SpellDamageAction { Amount = 6, PerBurn = 3 }
		),
		Card(
			"Flashpoint",
			2,
			R,
			"Drop on a foe: deal 8 + four times its Burn. The Burn stays.",
			new SpellDamageAction { Amount = 8, PerBurn = 4 }
		)
	);

	public static readonly KinCard Meteor = Plus(
		Card(
			"Meteor",
			0,
			R,
			"Spend all energy: deal 10 per energy to a foe, and 5 per energy to the one behind.",
			new SpellDamageAction { PerX = 10 },
			new SpellDamageAction { PerX = 5, Target = SpellTarget.Behind }
		) with
		{
			Components = [new SpendsAllEnergy()],
		},
		Card(
			"Meteor",
			0,
			R,
			"Spend all energy: deal 13 per energy to a foe, and 6 per energy to the one behind.",
			new SpellDamageAction { PerX = 13 },
			new SpellDamageAction { PerX = 6, Target = SpellTarget.Behind }
		) with
		{
			Components = [new SpendsAllEnergy()],
		}
	);

	public static readonly KinCard Overload = Plus(
		Card(
			"Overload",
			1,
			R,
			"Drop on a foe: deal all the spell damage dealt this turn.",
			new SpellDamageAction { FromSpellDamageThisTurn = true }
		),
		Card(
			"Overload",
			0,
			R,
			"Drop on a foe: deal all the spell damage dealt this turn.",
			new SpellDamageAction { FromSpellDamageThisTurn = true }
		)
	);

	public static readonly KinCard Pyroblast = Plus(
		Card(
			"Pyroblast",
			3,
			R,
			"Drop on a foe: deal 18. Your Spell Power counts three times.",
			new SpellDamageAction { Amount = 18, SpellPowerTimes = 3 }
		),
		Card(
			"Pyroblast",
			3,
			R,
			"Drop on a foe: deal 24. Your Spell Power counts three times.",
			new SpellDamageAction { Amount = 24, SpellPowerTimes = 3 }
		)
	);

	public static readonly KinCard Firestorm = Plus(
		Card(
			"Firestorm",
			2,
			R,
			"Cast every 0-cost spell in your discard pile, each at a random foe.",
			new FirestormAction()
		),
		Card(
			"Firestorm",
			1,
			R,
			"Cast every 0-cost spell in your discard pile, each at a random foe.",
			new FirestormAction()
		)
	);

	public static readonly KinCard InnerFire = Plus(
		Card(
			"Inner Fire",
			2,
			R,
			"AURA: at the start of each of your turns, +1 Spell Power for the rest of the fight.",
			new AuraAction { Aura = new InnerFireAura() }
		),
		Card(
			"Inner Fire",
			1,
			R,
			"AURA: at the start of each of your turns, +1 Spell Power for the rest of the fight.",
			new AuraAction { Aura = new InnerFireAura() }
		)
	);

	public static readonly KinCard Everburn = Plus(
		Card(
			"Everburn",
			1,
			R,
			"AURA: Burn no longer drops at the foe's turn.",
			new AuraAction { Aura = new EverburnAura() }
		),
		Card(
			"Everburn",
			0,
			R,
			"AURA: Burn no longer drops at the foe's turn.",
			new AuraAction { Aura = new EverburnAura() }
		)
	);

	public static readonly KinCard Spellweaver = Plus(
		Card(
			"Spellweaver",
			2,
			R,
			"AURA: whenever you cast your 3rd spell in a turn, draw 2 and gain 1 energy.",
			new AuraAction { Aura = new SpellweaverAura() }
		),
		Card(
			"Spellweaver",
			1,
			R,
			"AURA: whenever you cast your 3rd spell in a turn, draw 2 and gain 1 energy.",
			new AuraAction { Aura = new SpellweaverAura() }
		)
	);

	/// <summary>**Every Ember card a reward or a shop can offer.**</summary>
	public static readonly ImmutableList<KinCard> Pool =
	[
		Zap,
		Kindle,
		Spark,
		EmberDart,
		Kindling,
		Singe,
		FireFan,
		Flicker,
		ChargeUp,
		FlameWard,
		HeatHaze,
		Stoke,
		FanTheFlames,
		SpellSurge,
		Wildfire,
		ChainLightning,
		Ignite,
		SpreadingFlames,
		SmokeScreen,
		CinderShield,
		HeatSurge,
		Flashpoint,
		Meteor,
		Overload,
		Pyroblast,
		Firestorm,
		InnerFire,
		Everburn,
		Spellweaver,
	];

	/// <summary>The two Ember cards in the starting deck.</summary>
	public static readonly ImmutableList<KinCard> Starting = [Zap, Kindle];

	// ===== The monsters (Pike, the starter, is in `PartyContent`)

	private static readonly ImmutableList<Intent> NoMoves =
	[
		new Intent { Name = "Wait", Kind = IntentType.Block },
	];

	private static PartyCompanion Monster(
		string name,
		int hp,
		int power,
		int spellPower,
		string passive,
		string rule,
		params GameComponent[] abilities
	) =>
		new(name, hp, power, NoMoves, Passive: passive, PassiveRule: rule)
		{
			Family = Family.Ember,
			SpellPower = spellPower,
			Abilities = [.. abilities],
		};

	/// <summary>**STACK** — Spell Power, then a nuke.</summary>
	public static readonly PartyCompanion Emberling = Monster(
		"Emberling",
		20,
		0,
		2,
		"STOKER",
		"When a card gives Spell Power, it gives 1 more.",
		new Stoker(),
		new FirstAttack { FightSpellPower = 1 }
	);

	/// <summary>**BURN** — every spell leaves a flame.</summary>
	public static readonly PartyCompanion CinderNewt = Monster(
		"Cinder Newt",
		24,
		1,
		1,
		"SMOULDER",
		"Your spells apply 1 Burn to each foe they hit.",
		new Smoulder(),
		new FirstAttack { Burn = 2 }
	);

	/// <summary>**CHAINS** — the third spell is a double.</summary>
	public static readonly PartyCompanion EchoOwl = Monster(
		"Echo Owl",
		22,
		1,
		1,
		"ECHO",
		"Your 3rd spell each turn is cast twice.",
		new EchoNthSpell { Nth = 3 },
		new FirstAttack { Draw = 1 }
	);

	/// <summary>**ENERGY** — bank it, then dump it.</summary>
	public static readonly PartyCompanion Ironhorn = Monster(
		"Ironhorn",
		30,
		4,
		0,
		"BANK",
		"Up to 2 unspent energy carries into your next turn.",
		new Bank { Most = 2 },
		new FirstAttack { Energy = 1 }
	);

	/// <summary>The four a boss can offer an Ember run.</summary>
	public static readonly ImmutableList<PartyCompanion> Monsters =
	[
		Emberling,
		CinderNewt,
		EchoOwl,
		Ironhorn,
	];
}
