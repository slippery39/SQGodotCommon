using System.Collections.Immutable;
using ImmutableGameObjects;

namespace KinCore.Party;

/// <summary>
/// A monster as authored: stats, a PASSIVE, the CYCLE of moves it plays on its own, and a small
/// MONSTER DECK that joins the trainer's while it fights (KinJam.md).
/// </summary>
public record PartyCompanion(
	string Name,
	int Hp,
	int Power,
	ImmutableList<Intent> Moves,
	string Passive = "",
	string PassiveRule = "",
	int Thorns = 0,
	int FinisherPerAlly = 0,
	int Unbalances = 0
)
{
	/// <summary>
	/// **Its MONSTER DECK** (Shayne, 2026-09-24): baseline cards that work with it, in your draw
	/// pile only while it fights, so a monster is never in the line with nothing in the deck for it.
	/// Small and never pushed; the payoffs come from rewards.
	/// </summary>
	public ImmutableList<KinCard> Cards { get; init; } = [];

	/// <summary>
	/// **What it does to how you PLAY** — a `Trigger` ("when you draw, gain energy") or a static
	/// component ("your spells deal +2", MtgCore's StaticAbilityComponent idea). Put on its `Ally`
	/// and active only while it stands in the line.
	/// </summary>
	public ImmutableList<GameComponent> Abilities { get; init; } = [];

	/// <summary>The level these stats are at (`PartyLevels.Scale` sets it; the base is Lv 5).</summary>
	public int Level { get; init; } = PartyLevels.Base;
}

/// <summary>
/// A monster in a battle. `Position` is its place in your line (0 = the front); below 0 puts it on
/// the BENCH. `Hp` is where a RUN left it; null = full.
/// </summary>
public record PlacedCompanion(PartyCompanion Companion, int Position, int? Hp = null);

/// <summary>One fight of a run: who you face, in their line's order (the first is their front).</summary>
public record Encounter(string Name, ImmutableList<Foe> Foes);

/// <summary>
/// One battle to play. `OpeningHand` names cards to put on top of the shuffled deck, so a first
/// play can be a known puzzle rather than a random one.
/// </summary>
public record PartyScenario(
	string Name,
	string Description,
	ImmutableList<PlacedCompanion> Companions,
	ImmutableList<Foe> Foes,
	ImmutableList<KinCard> Deck,
	ImmutableList<string> OpeningHand,
	int Snares = 0,
	bool Deploy = false
);

/// <summary>
/// **THE RELAY content** (`KinRelayPlan.md`). Every number is a guess: this is exploring, not
/// tuning — the question is whether the decisions are real.
/// </summary>
public static class PartyContent
{
	private static KinCard Card(string name, int cost, string text, params GameAction[] steps) =>
		PartyCards.Card(name, cost, text, steps);

	private static Intent Attack(string name, int amount, Aim aim = Aim.Front) =>
		new()
		{
			Name = name,
			Kind = IntentType.Attack,
			Amount = amount,
			Target = aim,
		};

	private static Intent Guard(string name, int amount) =>
		new()
		{
			Name = name,
			Kind = IntentType.Block,
			Amount = amount,
		};

	/// <summary>
	/// **Bramble, the Wall — wants the FRONT.** Every foe that strikes her pays for it, and in a line
	/// the front is struck every round.
	/// </summary>
	public static readonly PartyCompanion Bramble =
		new(
			"Bramble",
			Hp: 30,
			Power: 2,
			[Attack("Bash", 4), Guard("Brace", 6)],
			Passive: "THORNS 2",
			PassiveRule: "A foe that attacks her takes 2, even if she blocks it.",
			Thorns: 2
		)
		{
			Cards =
			[
				Card("Thornhide", 1, "Gain 3 Thorns this turn.", new ThornsAction { Amount = 3 }),
				Card(
					"Bristle",
					0,
					"Gain 2 Thorns this turn. Draw a card.",
					new ThornsAction { Amount = 2 },
					new DrawAction()
				),
			],
		};

	/// <summary>
	/// **Pike, the Finisher — wants the front, and is fragile there.** The line acts back to front, so
	/// in front Pike acts LAST and cashes everyone behind it — where the blows land.
	/// </summary>
	public static readonly PartyCompanion Pike =
		new(
			"Pike",
			Hp: 18,
			Power: 3,
			[Attack("Jab", 2), Attack("Jab", 2), Attack("Flurry", 0, Aim.Sweep)],
			Passive: "FINISHER +2",
			PassiveRule: "+2 damage for each of your monsters that acted before it this round.",
			FinisherPerAlly: 2
		)
		{
			Cards =
			[
				Card(
					"Charge",
					1,
					"Send it to the front. +2 Power this turn.",
					new RallyAction(),
					new PowerAction { Amount = 2 }
				),
				Card(
					"Hold the Line",
					0,
					"Swap it with the one ahead. At the front: gain 5 Block.",
					new SwapAction { AloneBlock = 5 }
				),
			],
		};

	/// <summary>
	/// **Gale, the Controller — wants the BACK.** It acts first, so its Gust reorders their front
	/// before anyone else swings — and every foe it moves is Off-Balance for the round.
	/// </summary>
	public static readonly PartyCompanion Gale =
		new(
			"Gale",
			Hp: 22,
			Power: 2,
			[Attack("Buffet", 1, Aim.Sweep), new Intent { Name = "Gust", Kind = IntentType.Shove }],
			Passive: "OFF-BALANCE +2",
			PassiveRule: "While Gale stands, a foe you move takes 2 more from every hit that round.",
			Unbalances: 2
		)
		{
			Cards = [PartyCards.Gust, PartyCards.Tailwind],
		};

	/// <summary>
	/// **The trainer's BASIC deck — every card is played ON something, and none belongs to a
	/// monster.** Each monster fighting adds its own deck on top (`PartyCompanion.Cards`), so the
	/// starter's two cards make it ten. Made clearly stronger on 2026-09-27 (Shayne: the early game
	/// was far too hard) — Strike 3→5, Guard 6→8, Rally +3→+4.
	/// </summary>
	public static readonly ImmutableList<KinCard> StarterDeck =
	[
		Card("Guard", 1, "Gain 8 Block.", new GuardAction { Amount = 8 }),
		Card("Guard", 1, "Gain 8 Block.", new GuardAction { Amount = 8 }),
		Card(
			"Strike",
			1,
			"It attacks their front now: 5 + Power.",
			new StrikeAction { Amount = 5 }
		),
		Card(
			"Strike",
			1,
			"It attacks their front now: 5 + Power.",
			new StrikeAction { Amount = 5 }
		),
		Card("Rally", 1, "+4 Power this turn.", new PowerAction { Amount = 4 }),
		Card("Rally", 1, "+4 Power this turn.", new PowerAction { Amount = 4 }),
		Card("Hasten", 1, "It plays its move now, not at end of turn.", new HastenAction()),
		Card("Stagger", 1, "Drop on a foe: it loses its next move.", new StaggerAction()),
	];

	private static Foe Creature(string name, int hp, params Intent[] cycle) =>
		new()
		{
			Name = name,
			Hp = hp,
			MaxHp = hp,
			Pattern = [.. cycle],
		};

	public static Foe Boar(int position) =>
		Creature("Boar", 22, Attack("Charge", 9), Attack("Thrash", 5, Aim.Sweep)) with
		{
			Position = position,
		};

	/// <summary>
	/// **The Old Tusker — a gym's exam of the FRONT.** Its Gore is huge and lands on your front (put
	/// Bramble there, on Thorns, or Guard it); its Stampede hits the whole line.
	/// </summary>
	public static Foe OldTusker(int position) =>
		Creature(
			"Old Tusker",
			48,
			Attack("Gore", 14),
			Attack("Stampede", 7, Aim.Sweep),
			Guard("Snort", 10)
		) with
		{
			Catchable = false,
			Position = position,
		};

	/// <summary>**Wisp — HUNTS the weakest**, wherever it stands, then drifts toward the back.</summary>
	public static Foe Wisp(int position) =>
		Creature(
			"Wisp",
			12,
			Attack("Zap", 4, Aim.Hunt),
			Attack("Zap", 4, Aim.Hunt),
			new Intent
			{
				Name = "Drift",
				Kind = IntentType.Move,
				Amount = 1,
			}
		) with
		{
			Position = position,
		};

	/// <summary>**Stonebeak — PIERCES your front two**, so a stacked front pair both bleed.</summary>
	public static Foe Stonebeak(int position) =>
		Creature(
			"Stonebeak",
			16,
			Attack("Dive", 7, Aim.Pierce),
			Attack("Dive", 7, Aim.Pierce),
			Guard("Preen", 6)
		) with
		{
			Position = position,
		};

	// ===== THE RUN

	/// <summary>Every companion, in the order the ones you did not start with join.</summary>
	public static readonly ImmutableList<PartyCompanion> Roster = [Bramble, Pike, Gale];

	/// <summary>**What a win can offer — trainer cards, for the whole deck.** Three are offered; take one.</summary>
	public static readonly ImmutableList<KinCard> Rewards =
	[
		Card(
			"Strike",
			1,
			"It attacks their front now: 3 + Power.",
			new StrikeAction { Amount = 3 }
		),
		Card(
			"Whirl",
			2,
			"It attacks their whole line now: 2 + Power.",
			new StrikeAction { Amount = 2, Aim = Aim.Sweep }
		),
		Card("Bulwark", 2, "Gain 12 Block.", new GuardAction { Amount = 12 }),
		Card("Frenzy", 1, "+5 Power this turn.", new PowerAction { Amount = 5 }),
		PartyCards.Tailwind,
		Card(
			"Bristle",
			0,
			"Gain 2 Thorns this turn. Draw a card.",
			new ThornsAction { Amount = 2 },
			new DrawAction()
		),
		PartyCards.Sow,
		PartyCards.CallSparks,
		PartyCards.DecoyCard,
		PartyCards.Swarm,
		PartyCards.Offering,
		PartyCards.Surge,
		PartyCards.Quicken,
		PartyCards.BattleCry,
		PartyCards.Unleash,
		PartyCards.Meteor,
		PartyCards.Zap,
		PartyCards.Arc,
		PartyCards.SparkScroll,
		PartyCards.Overload,
		PartyCards.Focus,
		PartyCards.Sift,
		PartyCards.Rummage,
		PartyCards.Ration,
		PartyCards.ScrapHammer,
		PartyCards.PageStorm,
	];

	public static readonly PartyScenario Alone =
		new(
			"One against two",
			"Pike alone against a Boar and a Wisp.",
			[new(Pike, 0)],
			[Boar(0), Wisp(1)],
			StarterDeck,
			[],
			Snares: 2,
			Deploy: true
		);

	public static readonly PartyScenario Pair =
		new(
			"Two against three",
			"Bramble holds the front for Pike against a Boar, a Stonebeak and a Wisp.",
			[new(Bramble, 0), new(Pike, 1)],
			[Boar(0), Stonebeak(1), Wisp(2)],
			StarterDeck,
			[],
			Snares: 2,
			Deploy: true
		);

	/// <summary>
	/// **The Relay's intro: the Wall in front, the Finisher behind her, the Controller at the back.**
	/// Charge swaps Pike to the front to cash the relay — where the Boar's Charge lands.
	/// </summary>
	public static readonly PartyScenario Trio =
		new(
			"Three against three",
			"Bramble, Pike and Gale against a Boar, a Stonebeak and a Wisp.",
			[new(Bramble, 0), new(Pike, 1), new(Gale, 2)],
			[Boar(0), Stonebeak(1), Wisp(2)],
			StarterDeck,
			["Charge", "Gust", "Rally", "Guard", "Stagger"],
			Snares: 2,
			Deploy: true
		);

	/// <summary>
	/// **Discard + Draw, to try by hand.** A caught Magpie and Inkling (their decks bring Sift,
	/// Rummage and Jot); the payoffs in the deck; a Hoard Drake and a wild Magpie to test them.
	/// A property, not a field: it reads `PartyWorld`, which reads this class.
	/// </summary>
	public static PartyScenario Looting =>
		new(
			"Draw and discard",
			"Pike, a Magpie and an Inkling against a Hoard Drake and a wild Magpie.",
			[
				new(Pike, 0),
				new(PartyRun.FromFoe(PartyWorld.Magpie), 1),
				new(PartyRun.FromFoe(PartyWorld.Inkling), 2),
			],
			[PartyWorld.HoardDrake with { Position = 0 }, PartyWorld.Magpie with { Position = 1 }],
			[.. StarterDeck, PartyCards.ScrapHammer, PartyCards.PageStorm, PartyCards.Ration],
			["Sift"],
			Snares: 2,
			Deploy: true
		);

	/// <summary>
	/// **Spellcraft, to try by hand.** A caught Emberling (+2 to spells) and Echo Owl against a
	/// Warden in front (spells deal half) guarding a Wisp and a Briar Viper — spells reach past it.
	/// </summary>
	public static PartyScenario Spellcraft =>
		new(
			"Spellcraft",
			"Bramble, an Emberling and an Echo Owl against a Warden, a Wisp and a Briar Viper.",
			[
				new(Bramble, 0),
				new(PartyRun.FromFoe(PartyWorld.Emberling), 1),
				new(PartyRun.FromFoe(PartyWorld.EchoOwl), 2),
			],
			[
				PartyWorld.Warden with
				{
					Position = 0,
				},
				Wisp(1),
				PartyWorld.BriarViper with
				{
					Position = 2,
				},
			],
			[.. StarterDeck, PartyCards.Overload, PartyCards.Focus, PartyCards.SparkScroll],
			["Zap", "Zap", "Focus"],
			Snares: 2,
			Deploy: true
		);

	/// <summary>
	/// **Surge, to try by hand.** A caught Glowmoth (unhit: +1 energy) and Stormbuck (a kill during
	/// your turn: +1 energy) against a Hushcap (your first card costs 1 more), whose Spores HUNT the
	/// fragile moth wherever it stands.
	/// </summary>
	public static PartyScenario SurgeScenario =>
		new(
			"Surge",
			"Pike, a Stormbuck and a Glowmoth against a Hushcap, a Boar and a Wisp.",
			[
				new(Pike, 0),
				new(PartyRun.FromFoe(PartyWorld.Stormbuck), 1),
				new(PartyRun.FromFoe(PartyWorld.Glowmoth), 2),
			],
			[PartyWorld.Hushcap with { Position = 0 }, Boar(1), Wisp(2)],
			[.. StarterDeck, PartyCards.Unleash, PartyCards.Meteor, PartyCards.BattleCry],
			["Surge", "Quicken", "Unleash"],
			Snares: 2,
			Deploy: true
		);

	/// <summary>
	/// **Summon, to try by hand.** A caught Broodvine (its Brood puts Grubs in front) and Howler
	/// (tokens arrive +2/+1) against an Ironhorn, whose Trample goes through a token into the one
	/// behind, and a Wisp that the Decoy answers.
	/// </summary>
	public static PartyScenario SummonScenario =>
		new(
			"Summon",
			"Bramble, a Broodvine and a Howler against an Ironhorn, a Wisp and a Stonebeak.",
			[
				new(Bramble, 0),
				new(PartyRun.FromFoe(PartyWorld.Broodvine), 1),
				new(PartyRun.FromFoe(PartyWorld.Howler), 2),
			],
			[PartyWorld.Ironhorn with { Position = 0 }, Wisp(1), Stonebeak(2)],
			[.. StarterDeck, PartyCards.Swarm, PartyCards.Offering, PartyCards.DecoyCard],
			["Sow", "Call Sparks", "Swarm"],
			Snares: 2,
			Deploy: true
		);

	public static ImmutableList<PartyScenario> Scenarios =>
		[Trio, Alone, Pair, Looting, Spellcraft, SurgeScenario, SummonScenario];
}

/// <summary>Builds one battle's GameState from a scenario and deals the first hand.</summary>
public static class PartyBattleFactory
{
	public static GameState Create(PartyScenario scenario, int seed = 0)
	{
		var s = new GameState
		{
			RngSeed = seed,
			PostActionProcessor = new FirePartyTriggersAction(),
		};

		(s, var battle) = s.AddObject(
			new PartyBattle
			{
				Name = scenario.Name,
				Description = scenario.Description,
				Energy = 3,
				Snares = scenario.Snares,
			}
		);
		(s, var draw) = s.AddObject(
			new Zone { Name = "Draw", ZoneType = ZoneType.Draw },
			battle.Id
		);
		(s, var hand) = s.AddObject(
			new Zone { Name = "Hand", ZoneType = ZoneType.Hand },
			battle.Id
		);
		(s, var discard) = s.AddObject(
			new Zone { Name = "Discard", ZoneType = ZoneType.Discard },
			battle.Id
		);

		// The deck zones reuse the OLD game's keys, so `StartTurnAction.DrawCards` works unchanged.
		s = s.RegisterWellKnownId(PartyState.BattleKey, battle.Id)
			.RegisterWellKnownId(KinObjectKeys.Draw, draw.Id)
			.RegisterWellKnownId(KinObjectKeys.Hand, hand.Id)
			.RegisterWellKnownId(KinObjectKeys.Discard, discard.Id);

		foreach (
			var ((companion, position, hp), slot) in scenario.Companions.Select((c, i) => (c, i))
		)
		{
			(s, var ally) = s.AddObject(
				new Ally
				{
					Slot = slot,
					Name = companion.Name,
					Level = companion.Level,
					Hp = hp ?? companion.Hp,
					MaxHp = companion.Hp,
					Power = companion.Power,
					Position = position,
					Benched = position < 0,
					Pattern = companion.Moves,
					Passive = companion.Passive,
					PassiveRule = companion.PassiveRule,
					Thorns = companion.Thorns,
					FinisherPerAlly = companion.FinisherPerAlly,
					Unbalances = companion.Unbalances,
					Components = [.. companion.Abilities],
				},
				battle.Id
			);

			// Its deck waits UNDER it, and joins the draw pile only if it starts in the line.
			foreach (var card in companion.Cards)
				(s, _) = s.AddObject(
					card with
					{
						OwnerId = ally.Id,
						OwnerName = companion.Name,
					},
					ally.Id
				);
			if (position >= 0)
				s = s.DeployDeck(ally.Id);
		}

		foreach (var foe in scenario.Foes)
			(s, _) = s.AddObject(foe, battle.Id);

		// Both lines closed up from whatever positions the scenario named — contiguous from the front.
		(s, _) = s.Settle();

		foreach (var card in scenario.Deck)
			(s, _) = s.AddObject(card, draw.Id);

		s = KinRng.ShuffleZone(s, draw.Id);

		// Reversed, so the FIRST name ends up on top.
		var used = new HashSet<int>();
		foreach (var name in scenario.OpeningHand.Reverse())
		{
			var id = s.GetChildren(draw.Id)
				.OfType<KinCard>()
				.First(c => c.Name == name && !used.Contains(c.Id))
				.Id;
			used.Add(id);
			s = s.MoveObjectToFront(id, draw.Id);
		}

		s = s.AddAction(new StartPartyTurnAction()).ProcessAllActions().State;

		// DEPLOY (R2): the hand is dealt, and the fight waits for FIGHT.
		return scenario.Deploy
			? s.UpdateObject(s.GetParty().Id, s.GetParty() with { Deploying = true })
			: s;
	}
}
