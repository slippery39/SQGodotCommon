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

	/// <summary>
	/// **A card and its + version** (round 4: a spring can UPGRADE a card). The + keeps the card's
	/// family and rarity and takes its name with a "+".
	/// </summary>
	public static KinCard Plus(KinCard card, KinCard better) =>
		card with
		{
			Upgraded = better with
			{
				Name = card.Name + "+",
				Family = card.Family,
				Rarity = card.Rarity,
			},
		};

	// ===== The starters' old signature cards (monster decks are gone, 2026-09-28)

	/// <summary>Pike's, now colourless: moving in the line is every family's business.</summary>
	public static readonly KinCard Charge = Card(
		"Charge",
		1,
		"Send it to the front. +2 Power this turn.",
		new RallyAction(),
		new PowerAction { Amount = 2 }
	);

	public static readonly KinCard HoldTheLine = Card(
		"Hold the Line",
		0,
		"Swap it with the one ahead. At the front: gain 5 Block.",
		new SwapAction { AloneBlock = 5 }
	);

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

	// ===== The wild Broodvine's brood (Grove's own tokens are in `GroveCards`)

	private static Intent Hits(string name, int amount) =>
		new()
		{
			Name = name,
			Kind = IntentType.Attack,
			Amount = amount,
		};

	/// <summary>The Broodvine's brood — yours when it is yours, the foes' when it is wild.</summary>
	public static readonly TokenTemplate Grub =
		new(
			new PartyCompanion("Grub", 2, 0, [Hits("Bite", 2)]) { Family = Family.Grove },
			FadesIn: 2
		);

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
		"Costs all your energy. Attack all foes for 4 per energy.",
		new StrikeAction { PerX = 4, Aim = Aim.Sweep }
	) with
	{
		Components = [new SpendsAllEnergy()],
		Family = Family.Storm,
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
		"Attack 6. Costs 1 less per card discarded this turn.",
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
		"Attack for 1 per card in your hand.",
		new StrikeAction { Amount = 0, PlusCardsInHand = true }
	) with
	{
		Family = Family.Mire,
	};
}
