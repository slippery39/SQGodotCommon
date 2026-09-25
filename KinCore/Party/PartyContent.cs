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
	int Speed,
	ImmutableList<Intent> Moves,
	string Passive = "",
	string PassiveRule = "",
	int Thorns = 0,
	int MomentumPerStep = 0,
	int Unbalances = 0
)
{
	/// <summary>
	/// **Its MONSTER DECK** (Shayne, 2026-09-24): baseline cards that work with it, in your draw
	/// pile only while it fights, so a monster is never on the board with nothing in the deck for
	/// it. Small and never pushed; the payoffs come from rewards.
	/// </summary>
	public ImmutableList<KinCard> Cards { get; init; } = [];

	/// <summary>
	/// **What it does to how you PLAY** — a `Trigger` ("when you draw, gain energy") or a static
	/// component ("your spells deal +2", MtgCore's StaticAbilityComponent idea). Put on its `Ally`
	/// and active only while it stands on the board.
	/// </summary>
	public ImmutableList<GameComponent> Abilities { get; init; } = [];
}

/// <summary>
/// A monster in a battle. `Hp` is where a RUN left it; null = full. A `Space` below 0 puts it on
/// the BENCH, waiting to step in.
/// </summary>
public record PlacedCompanion(PartyCompanion Companion, int Space, int? Hp = null);

/// <summary>
/// One fight of a run: who you face, and where they stand. `LeaderHp` above 0 makes it a GYM — a
/// trainer stands behind the creatures, and your swings into empty columns hit them.
/// </summary>
public record Encounter(string Name, ImmutableList<Foe> Foes, int LeaderHp = 0);

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
	int TrainerHp = PartyScenario.DefaultTrainerHp,
	int LeaderHp = 0
)
{
	public const int DefaultTrainerHp = 30;
}

/// <summary>
/// **AUTO-BATTLE v1 content.** Every number is a guess: this is exploring, not tuning — the question
/// is whether the decisions are real.
/// </summary>
public static class PartyContent
{
	private static KinCard Card(string name, int cost, string text, params GameAction[] steps) =>
		PartyCards.Card(name, cost, text, steps);

	private static readonly ImmutableList<int> Ahead = [0];
	private static readonly ImmutableList<int> ThreeWide = [-1, 0, 1];

	private static Intent Attack(string name, int amount, ImmutableList<int>? offsets = null) =>
		new()
		{
			Name = name,
			Kind = IntentType.Attack,
			Amount = amount,
			Offsets = offsets ?? Ahead,
		};

	/// <summary>
	/// **Bramble, the Wall — wants to be HIT.** Slow and tough: she acts last, so she swings at
	/// whatever the others left standing, and every foe that strikes her pays for it.
	/// </summary>
	public static readonly PartyCompanion Bramble =
		new(
			"Bramble",
			Hp: 30,
			Power: 2,
			Speed: 1,
			[Attack("Bash", 4), Attack("Sweep", 1, ThreeWide)],
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
	/// **Pike, the Skirmisher — wants never to be where the hit lands.** Fastest on the board, and
	/// its free step is damage — so the step is both the dodge and the aim.
	/// </summary>
	public static readonly PartyCompanion Pike =
		new(
			"Pike",
			Hp: 18,
			Power: 3,
			Speed: 3,
			[Attack("Jab", 2), Attack("Jab", 2), Attack("Flurry", 0, ThreeWide)],
			Passive: "MOMENTUM +2/step",
			PassiveRule: "Each step this turn adds 2 to its next attack.",
			MomentumPerStep: 2
		)
		{
			Cards =
			[
				Card(
					"Dash",
					0,
					"Take another step this turn. Draw a card.",
					new DashAction(),
					new DrawAction()
				),
				Card("Sprint", 0, "Take two more steps this turn.", new DashAction { Steps = 2 }),
			],
		};

	/// <summary>
	/// **Gale, the Controller — wants the FOES where it chooses.** Its Gust pushes the foe ahead, and
	/// while Gale stands every foe you move is Off-Balance. Gale is in the middle of the order, so its
	/// own Gust sets up only Bramble; a Gust CARD, played before anyone acts, sets up everyone.
	/// </summary>
	public static readonly PartyCompanion Gale =
		new(
			"Gale",
			Hp: 22,
			Power: 2,
			Speed: 2,
			[
				Attack("Buffet", 1, ThreeWide),
				new Intent
				{
					Name = "Gust",
					Kind = IntentType.Push,
					Amount = 1,
				},
			],
			Passive: "OFF-BALANCE +2",
			PassiveRule: "While Gale stands, a foe you move takes 2 more from every hit that turn.",
			Unbalances: 2
		)
		{
			Cards =
			[
				Card(
					"Gust",
					1,
					"Drop on an empty foe space: the foe beside it moves in.",
					new PushAction()
				),
				Card(
					"Tailwind",
					1,
					"Drop on an empty foe space: the foe beside it moves in. Draw a card.",
					new PushAction(),
					new DrawAction()
				),
			],
		};

	/// <summary>
	/// **The trainer's BASIC deck — every card is played ON something, and none belongs to a
	/// monster.** Each monster fighting adds its own deck on top (`PartyCompanion.Cards`), so the
	/// starter's two cards make it ten. The passive decides what a card means: Guard on Bramble is
	/// damage, a Dash on Pike is damage.
	/// </summary>
	public static readonly ImmutableList<KinCard> StarterDeck =
	[
		Card("Guard", 1, "Gain 6 Block.", new GuardAction { Amount = 6 }),
		Card("Guard", 1, "Gain 6 Block.", new GuardAction { Amount = 6 }),
		Card("Strike", 1, "It attacks ahead now: 3 + Power.", new StrikeAction { Amount = 3 }),
		Card("Strike", 1, "It attacks ahead now: 3 + Power.", new StrikeAction { Amount = 3 }),
		Card("Rally", 1, "+3 Power this turn.", new PowerAction { Amount = 3 }),
		Card("Rally", 1, "+3 Power this turn.", new PowerAction { Amount = 3 }),
		Card("Hasten", 1, "It plays its move now, not at end of turn.", new HastenAction()),
		Card("Stagger", 1, "Drop on a foe: it loses its next move.", new StaggerAction()),
	];

	public static Foe Boar(int space) =>
		new()
		{
			Name = "Boar",
			Hp = 22,
			MaxHp = 22,
			Speed = 1,
			Space = space,
			Pattern =
			[
				new Intent
				{
					Name = "Charge",
					Kind = IntentType.Attack,
					Amount = 9,
					Offsets = Ahead,
				},
				new Intent
				{
					Name = "Trample",
					Kind = IntentType.Attack,
					Amount = 5,
					Offsets = ThreeWide,
				},
			],
		};

	/// <summary>
	/// **The Old Tusker — the run's boss, and an exam of POSITION.** Its Gore is huge and one column
	/// wide (step out of it, or let Bramble take it on Thorns); its Stampede is three wide and cannot
	/// be stepped out of from the middle, so it asks for Block or a push.
	/// </summary>
	public static Foe OldTusker(int space) =>
		new()
		{
			Name = "Old Tusker",
			Hp = 48,
			MaxHp = 48,
			Catchable = false,
			Speed = 2,
			Space = space,
			Pattern =
			[
				new Intent
				{
					Name = "Gore",
					Kind = IntentType.Attack,
					Amount = 14,
					Offsets = Ahead,
				},
				new Intent
				{
					Name = "Stampede",
					Kind = IntentType.Attack,
					Amount = 7,
					Offsets = ThreeWide,
				},
				new Intent
				{
					Name = "Snort",
					Kind = IntentType.Block,
					Amount = 10,
				},
			],
		};

	private static readonly Intent Zap =
		new()
		{
			Name = "Zap",
			Kind = IntentType.Attack,
			Amount = 4,
			Homing = true,
		};

	public static Foe Wisp(int space) =>
		new()
		{
			Name = "Wisp",
			Hp = 12,
			MaxHp = 12,
			Speed = 3,
			Space = space,
			// Foes attack on most turns — the first run's Bramble drew Block and Thorns for foes that
			// were busy drifting and preening.
			Pattern =
			[
				Zap,
				Zap,
				new Intent
				{
					Name = "Drift",
					Kind = IntentType.Move,
					Amount = -1,
				},
			],
		};

	public static Foe Stonebeak(int space) =>
		new()
		{
			Name = "Stonebeak",
			Hp = 16,
			MaxHp = 16,
			Speed = 2,
			Space = space,
			Pattern =
			[
				Attack("Dive", 7, [-1, 0]),
				Attack("Dive", 7, [-1, 0]),
				new Intent
				{
					Name = "Preen",
					Kind = IntentType.Block,
					Amount = 6,
				},
			],
		};

	// ===== THE RUN, v1 — KinJam.md "THE RUN"

	/// <summary>Every companion, in the order the ones you did not start with join.</summary>
	public static readonly ImmutableList<PartyCompanion> Roster = [Bramble, Pike, Gale];

	/// <summary>
	/// **What a win can offer — trainer cards, for the whole deck.** Three are offered; take one.
	/// More damage than the starter deck, which the first run was short of.
	/// </summary>
	public static readonly ImmutableList<KinCard> Rewards =
	[
		Card("Strike", 1, "It attacks ahead now: 3 + Power.", new StrikeAction { Amount = 3 }),
		Card(
			"Whirl",
			2,
			"It attacks now: 2 + Power, three wide.",
			new StrikeAction { Amount = 2, Offsets = ThreeWide }
		),
		Card("Bulwark", 2, "Gain 12 Block.", new GuardAction { Amount = 12 }),
		Card("Frenzy", 1, "+5 Power this turn.", new PowerAction { Amount = 5 }),
		Card("Sprint", 0, "Take two more steps this turn.", new DashAction { Steps = 2 }),
		Card(
			"Tailwind",
			1,
			"Drop on an empty foe space: the foe beside it moves in. Draw a card.",
			new PushAction(),
			new DrawAction()
		),
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
			[new(Pike, 2)],
			[Boar(1), Wisp(3)],
			StarterDeck,
			[],
			Snares: 2
		);

	public static readonly PartyScenario Pair =
		new(
			"Two against three",
			"Bramble and Pike against a Boar, a Wisp and a Stonebeak.",
			[new(Bramble, 1), new(Pike, 3)],
			[Boar(1), Wisp(3), Stonebeak(4)],
			StarterDeck,
			[],
			Snares: 2
		);

	/// <summary>The full team: the Wall, the Controller and the Skirmisher, each wanting something else.</summary>
	public static readonly PartyScenario Trio =
		new(
			"Three against three",
			"Bramble, Gale and Pike against a Boar, a Wisp and a Stonebeak.",
			[new(Bramble, 0), new(Gale, 2), new(Pike, 4)],
			[Boar(2), Wisp(3), Stonebeak(4)],
			StarterDeck,
			["Gust", "Rally", "Guard", "Dash", "Stagger"],
			Snares: 2
		);

	/// <summary>
	/// **Discard + Draw, to try by hand** — round one builds a practice scenario per strategy so the
	/// cards are played before they are cut. A caught Magpie and Inkling (their decks bring Sift,
	/// Rummage and Jot); the payoffs in the deck; a Hoard Drake and a wild Magpie to test them.
	/// A property, not a field: it reads `PartyWorld`, which reads this class.
	/// </summary>
	public static PartyScenario Looting =>
		new(
			"Draw and discard",
			"Pike, a Magpie and an Inkling against a Hoard Drake and a wild Magpie.",
			[
				new(Pike, 0),
				new(PartyRun.FromFoe(PartyWorld.Magpie), 2),
				new(PartyRun.FromFoe(PartyWorld.Inkling), 4),
			],
			[PartyWorld.HoardDrake with { Space = 1 }, PartyWorld.Magpie with { Space = 3 }],
			[.. StarterDeck, PartyCards.ScrapHammer, PartyCards.PageStorm, PartyCards.Ration],
			["Sift"],
			Snares: 2
		);

	/// <summary>
	/// **Spellcraft, to try by hand.** A caught Emberling (+2 to spells) and Echo Owl against a
	/// Warden (spells deal half) and the Wisp and Briar Viper — homing and moving, the foes spells
	/// answer because they need no aim. A property: it reads `PartyWorld`.
	/// </summary>
	public static PartyScenario Spellcraft =>
		new(
			"Spellcraft",
			"Bramble, an Emberling and an Echo Owl against a Warden, a Wisp and a Briar Viper.",
			[
				new(Bramble, 0),
				new(PartyRun.FromFoe(PartyWorld.Emberling), 2),
				new(PartyRun.FromFoe(PartyWorld.EchoOwl), 4),
			],
			[
				PartyWorld.Warden with
				{
					Space = 0,
				},
				Wisp(2),
				PartyWorld.BriarViper with
				{
					Space = 4,
				},
			],
			[.. StarterDeck, PartyCards.Overload, PartyCards.Focus, PartyCards.SparkScroll],
			["Zap", "Zap", "Focus"],
			Snares: 2
		);

	/// <summary>
	/// **Surge, to try by hand.** A caught Glowmoth (unhit: +1 energy) and Stormbuck (a kill during
	/// your turn: +1 energy) against a Hushcap (your first card costs 1 more), whose homing Spores
	/// find the fragile moth. A property: it reads `PartyWorld`.
	/// </summary>
	public static PartyScenario SurgeScenario =>
		new(
			"Surge",
			"Pike, a Glowmoth and a Stormbuck against a Hushcap, a Boar and a Wisp.",
			[
				// The moth out of the Boar's column: the homing attacks still find it, which is the point.
				new(Pike, 2),
				new(PartyRun.FromFoe(PartyWorld.Glowmoth), 1),
				new(PartyRun.FromFoe(PartyWorld.Stormbuck), 4),
			],
			[PartyWorld.Hushcap with { Space = 0 }, Boar(2), Wisp(4)],
			[.. StarterDeck, PartyCards.Unleash, PartyCards.Meteor, PartyCards.BattleCry],
			["Surge", "Quicken", "Unleash"],
			Snares: 2
		);

	/// <summary>
	/// **Summon, to try by hand.** A caught Broodvine (its Brood summons Grubs) and Howler (tokens
	/// arrive +2/+1) against an Ironhorn, whose Trample makes a token wall paper, and a homing Wisp
	/// for the Decoy to answer. A property: it reads `PartyWorld`.
	/// </summary>
	public static PartyScenario SummonScenario =>
		new(
			"Summon",
			"Bramble, a Broodvine and a Howler against an Ironhorn, a Wisp and a Stonebeak.",
			[
				new(Bramble, 0),
				new(PartyRun.FromFoe(PartyWorld.Broodvine), 2),
				new(PartyRun.FromFoe(PartyWorld.Howler), 4),
			],
			[PartyWorld.Ironhorn with { Space = 1 }, Wisp(3), Stonebeak(4)],
			[.. StarterDeck, PartyCards.Swarm, PartyCards.Offering, PartyCards.DecoyCard],
			["Sow", "Call Sparks", "Swarm"],
			Snares: 2
		);

	public static ImmutableList<PartyScenario> Scenarios =>
		[Alone, Pair, Trio, Looting, Spellcraft, SurgeScenario, SummonScenario];
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
				TrainerHp = scenario.TrainerHp,
				LeaderHp = scenario.LeaderHp,
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

		foreach (var ((companion, space, hp), slot) in scenario.Companions.Select((c, i) => (c, i)))
		{
			(s, var ally) = s.AddObject(
				new Ally
				{
					Slot = slot,
					Name = companion.Name,
					Hp = hp ?? companion.Hp,
					MaxHp = companion.Hp,
					Power = companion.Power,
					Speed = companion.Speed,
					Space = space,
					Benched = space < 0,
					Pattern = companion.Moves,
					Passive = companion.Passive,
					PassiveRule = companion.PassiveRule,
					Thorns = companion.Thorns,
					MomentumPerStep = companion.MomentumPerStep,
					Unbalances = companion.Unbalances,
					Components = [.. companion.Abilities],
				},
				battle.Id
			);

			// Its deck waits UNDER it, and joins the draw pile only if it starts on the board.
			foreach (var card in companion.Cards)
				(s, _) = s.AddObject(
					card with
					{
						OwnerId = ally.Id,
						OwnerName = companion.Name,
					},
					ally.Id
				);
			if (space >= 0)
				s = s.DeployDeck(ally.Id);
		}

		foreach (var card in scenario.Deck)
			(s, _) = s.AddObject(card, draw.Id);

		foreach (var foe in scenario.Foes)
			(s, _) = s.AddObject(foe, battle.Id);

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

		return s.AddAction(new StartPartyTurnAction()).ProcessAllActions().State;
	}
}
