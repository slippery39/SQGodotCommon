using System.Collections.Immutable;
using ImmutableGameObjects;

namespace KinCore.Party;

/// <summary>
/// A companion as authored: stats, a PASSIVE, and the cards it brings to the combined deck.
/// `Passive` is the player-facing line; `Thorns` and `MomentumPerStep` are the rules it names.
/// </summary>
public record PartyCompanion(
	string Name,
	int Hp,
	int Power,
	int Speed,
	ImmutableList<KinCard> Cards,
	string Passive = "",
	string PassiveRule = "",
	int Thorns = 0,
	int MomentumPerStep = 0,
	int Unbalances = 0
);

/// <summary>A companion on the board. `Hp` is where a RUN left it; null = full.</summary>
public record PlacedCompanion(PartyCompanion Companion, int Space, int? Hp = null);

/// <summary>One fight of a run: who you face, and where they stand.</summary>
public record Encounter(string Name, ImmutableList<Foe> Foes);

/// <summary>A card on offer after a win, and the companion whose deck it joins.</summary>
public record RewardCard(string Companion, KinCard Card);

/// <summary>
/// One battle to play. `OpeningHand` names cards to put on top of the shuffled deck, so a first
/// play can be a known puzzle rather than a random one.
/// </summary>
public record PartyScenario(
	string Name,
	string Description,
	ImmutableList<PlacedCompanion> Companions,
	ImmutableList<Foe> Foes,
	ImmutableList<string> OpeningHand
);

/// <summary>
/// **The first slice, exactly as `docs/paper/companion-slice.md` states it.** Every number is a
/// guess: this is exploring, not tuning — the question is whether the decisions are real.
/// </summary>
public static class PartyContent
{
	private static KinCard Card(string name, int cost, string text, params GameAction[] steps) =>
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

	private static readonly ImmutableList<int> Ahead = [0];
	private static readonly ImmutableList<int> ThreeWide = [-1, 0, 1];

	/// <summary>
	/// **Bramble, the Wall — wants to be HIT** (KITS v2, KinJam.md). She steps INTO the attacks Pike
	/// steps out of, pulls single hits off her neighbours, and punishes whatever strikes her. Her only
	/// attack scales with how braced she is.
	/// </summary>
	public static readonly PartyCompanion Bramble =
		new(
			"Bramble",
			Hp: 30,
			Power: 2,
			Speed: 1,
			[
				Card("Bark Skin", 1, "Gain 6 Block.", new GuardAction { Amount = 6 }),
				Card("Thornhide", 1, "Gain 3 Thorns this turn.", new ThornsAction { Amount = 3 }),
				Card(
					"Retaliate",
					1,
					"Deal your Block ahead.",
					new StrikeAction { AddPower = false, AddBlock = true }
				),
				Card(
					"Root Wall",
					2,
					"Gain 5 Block. So do neighbours.",
					new GuardAction { Amount = 5, AndBeside = true }
				),
				Card(
					"Draw Fire",
					1,
					"This turn, single hits on a neighbour hit this instead.",
					new DrawFireAction()
				),
			],
			Passive: "THORNS 2",
			PassiveRule: "A foe that attacks her takes 2, even if she blocks it.",
			Thorns: 2
		);

	/// <summary>
	/// **Pike, the Skirmisher — wants to never be where the attack lands** (KITS v2). Every step
	/// feeds the next attack, so the decision is the ROUTE: how far, in what order, and where it
	/// ends the turn.
	/// </summary>
	public static readonly PartyCompanion Pike =
		new(
			"Pike",
			Hp: 18,
			Power: 3,
			Speed: 3,
			[
				Card("Jab", 1, "Deal 2 + Power ahead.", new StrikeAction { Amount = 2 }),
				Card(
					"Feint",
					0,
					"Step 1. Draw a card.",
					new StepAction(),
					new DrawAction { Count = 1 }
				),
				Card(
					"Lunge",
					1,
					"Step 1, then deal 2 + Power ahead.",
					new StepAction(),
					new StrikeAction { Amount = 2 }
				),
				Card(
					"Hit and Run",
					1,
					"Deal 2 + Power ahead, then step 1.",
					new StrikeAction { Amount = 2 },
					new StepAction()
				),
				Card(
					"Flank",
					1,
					"Deal 3 + Power ahead. Double vs a lone foe.",
					new StrikeAction { Amount = 3, DoubleIfAlone = true }
				),
			],
			Passive: "MOMENTUM +2/step",
			PassiveRule: "Each step this turn adds 2 to its next attack.",
			MomentumPerStep: 2
		);

	/// <summary>
	/// **Gale, the Controller — wants the FOES where it chooses** (KITS v2, KinJam.md). Every intent
	/// is a shape anchored on the foe's column, so moving a foe re-aims its attack; and a foe Gale
	/// moves is Off-Balance, so Gale sets up the others' hits. The decision is the ORDER of plays.
	/// Weak against homing attacks (the Wisp), which no push re-aims.
	/// </summary>
	public static readonly PartyCompanion Gale =
		new(
			"Gale",
			Hp: 22,
			Power: 2,
			Speed: 2,
			[
				Card("Gust", 1, "Push the foe ahead one column.", new PushAction()),
				Card("Gust", 1, "Push the foe ahead one column.", new PushAction()),
				Card(
					"Slam",
					1,
					"Push the foe ahead. If a foe is there, both take 5.",
					new PushAction { Collision = 5 }
				),
				Card(
					"Whirlwind",
					1,
					"Swap the foe ahead with the one beside it.",
					new SwapAction()
				),
				Card("Buffet", 1, "Deal 2 + Power ahead.", new StrikeAction { Amount = 2 }),
			],
			Passive: "OFF-BALANCE +2",
			PassiveRule: "A foe it moves takes 2 more from every hit this turn.",
			Unbalances: 2
		);

	public static Foe Boar(int space) =>
		new()
		{
			Name = "Boar",
			Hp = 22,
			MaxHp = 22,
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

	public static Foe Wisp(int space) =>
		new()
		{
			Name = "Wisp",
			Hp = 12,
			MaxHp = 12,
			Space = space,
			Pattern =
			[
				new Intent
				{
					Name = "Zap",
					Kind = IntentType.Attack,
					Amount = 4,
					Homing = true,
				},
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
			Space = space,
			Pattern =
			[
				new Intent
				{
					Name = "Dive",
					Kind = IntentType.Attack,
					Amount = 7,
					Offsets = [-1, 0],
				},
				new Intent
				{
					Name = "Preen",
					Kind = IntentType.Block,
					Amount = 6,
				},
			],
		};

	/// <summary>Pike's five cards twice over — the deck is ten either way.</summary>
	// ===== THE RUN, v1 — KinJam.md "THE RUN"

	/// <summary>Every companion, in the order the ones you did not start with join.</summary>
	public static readonly ImmutableList<PartyCompanion> Roster = [Bramble, Pike, Gale];

	/// <summary>
	/// **What each companion can be offered after a win** — three per companion, each pushing that
	/// companion's OWN goal further (the `design-card` skill). Offers come only from the team you have.
	/// </summary>
	public static readonly ImmutableDictionary<string, ImmutableList<KinCard>> Rewards =
		new Dictionary<string, ImmutableList<KinCard>>
		{
			["Bramble"] =
			[
				Card(
					"Bristle",
					0,
					"Gain 2 Thorns this turn. Draw a card.",
					new ThornsAction { Amount = 2 },
					new DrawAction { Count = 1 }
				),
				Card(
					"Taunt",
					1,
					"Step 1. Gain 4 Block.",
					new StepAction(),
					new GuardAction { Amount = 4 }
				),
				Card(
					"Briar Burst",
					2,
					"Deal your Thorns ahead and to both sides.",
					new StrikeAction
					{
						AddPower = false,
						AddThorns = true,
						Offsets = ThreeWide,
					}
				),
			],
			["Pike"] =
			[
				Card(
					"Quickstep",
					0,
					"Step 1. Gain 2 Momentum.",
					new StepAction(),
					new MomentumAction { Amount = 2 }
				),
				Card(
					"Pierce",
					1,
					"Deal 3 + Power ahead, ignoring Block.",
					new StrikeAction { Amount = 3, IgnoreBlock = true }
				),
				Card(
					"Whirling Strike",
					2,
					"Deal 2 + Power ahead and to both sides.",
					new StrikeAction { Amount = 2, Offsets = ThreeWide }
				),
			],
			["Gale"] =
			[
				Card(
					"Tailwind",
					1,
					"Push the foe ahead. Draw a card.",
					new PushAction(),
					new DrawAction { Count = 1 }
				),
				Card(
					"Downdraft",
					0,
					"The foe ahead is Off-Balance this turn.",
					new UnbalanceAction()
				),
				Card(
					"Cyclone",
					2,
					"Swap the foe ahead with the one beside it. Both take 3.",
					new SwapAction { Damage = 3 }
				),
			],
		}.ToImmutableDictionary();

	/// <summary>
	/// **Five fights, harder each time, the Old Tusker last.** Placed for a team that grows from one
	/// to three: the first fight is a single foe in front of a lone starter.
	/// </summary>
	public static readonly ImmutableList<Encounter> Encounters =
	[
		new("A wild Boar", [Boar(2)]),
		new("Boar and Wisp", [Boar(1), Wisp(3)]),
		new("The Stonebeak's perch", [Stonebeak(2), Wisp(4)]),
		new("Three at once", [Boar(1), Wisp(2), Stonebeak(4)]),
		new("The Old Tusker", [OldTusker(2), Wisp(4)]),
	];

	public static readonly PartyScenario Alone =
		new(
			"One against two",
			"Pike alone against a Boar and a Wisp.",
			[new(Pike with { Cards = Pike.Cards.AddRange(Pike.Cards) }, 2)],
			[Boar(1), Wisp(3)],
			[]
		);

	public static readonly PartyScenario Pair =
		new(
			"Two against three",
			"Bramble and Pike against a Boar, a Wisp and a Stonebeak.",
			[new(Bramble, 1), new(Pike, 3)],
			[Boar(1), Wisp(3), Stonebeak(4)],
			["Draw Fire", "Thornhide", "Bark Skin", "Feint", "Lunge"]
		);

	/// <summary>The full team: the Wall, the Controller and the Skirmisher, each wanting something else.</summary>
	public static readonly PartyScenario Trio =
		new(
			"Three against three",
			"Bramble, Gale and Pike against a Boar, a Wisp and a Stonebeak.",
			[new(Bramble, 0), new(Gale, 2), new(Pike, 4)],
			[Boar(2), Wisp(3), Stonebeak(4)],
			["Gust", "Slam", "Thornhide", "Lunge", "Flank"]
		);

	public static readonly ImmutableList<PartyScenario> Scenarios = [Alone, Pair, Trio];
}

/// <summary>Builds one battle's GameState from a scenario and deals the first hand.</summary>
public static class PartyBattleFactory
{
	public static GameState Create(PartyScenario scenario, int seed = 0)
	{
		var s = new GameState { RngSeed = seed };

		(s, var battle) = s.AddObject(
			new PartyBattle
			{
				Name = scenario.Name,
				Description = scenario.Description,
				Energy = 3,
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

		foreach (var (companion, space, hp) in scenario.Companions)
		{
			(s, var ally) = s.AddObject(
				new Ally
				{
					Name = companion.Name,
					Hp = hp ?? companion.Hp,
					MaxHp = companion.Hp,
					Power = companion.Power,
					Speed = companion.Speed,
					Space = space,
					Passive = companion.Passive,
					PassiveRule = companion.PassiveRule,
					Thorns = companion.Thorns,
					MomentumPerStep = companion.MomentumPerStep,
					Unbalances = companion.Unbalances,
				},
				battle.Id
			);

			var owner = new OwnedBy { AllyId = ally.Id, AllyName = ally.Name };
			foreach (var card in companion.Cards)
				(s, _) = s.AddObject(card with { Components = [owner] }, draw.Id);
		}

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
