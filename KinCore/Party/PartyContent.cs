using System.Collections.Immutable;
using ImmutableGameObjects;

namespace KinCore.Party;

/// <summary>A companion as authored: stats, and the cards it brings to the combined deck.</summary>
public record PartyCompanion(
	string Name,
	int Hp,
	int Power,
	int Speed,
	ImmutableList<KinCard> Cards
);

public record PlacedCompanion(PartyCompanion Companion, int Space);

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

	/// <summary>The wall: slow, blocks, can't dodge often.</summary>
	public static readonly PartyCompanion Bramble =
		new(
			"Bramble",
			Hp: 30,
			Power: 2,
			Speed: 1,
			[
				Card("Thump", 1, "Deal 3 + Power ahead.", new StrikeAction { Amount = 3 }),
				Card("Thump", 1, "Deal 3 + Power ahead.", new StrikeAction { Amount = 3 }),
				Card("Bark Skin", 1, "Gain 6 Block.", new GuardAction { Amount = 6 }),
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
			]
		);

	/// <summary>The skirmisher: fragile, dodges, aims.</summary>
	public static readonly PartyCompanion Pike =
		new(
			"Pike",
			Hp: 18,
			Power: 3,
			Speed: 3,
			[
				Card("Jab", 1, "Deal 2 + Power ahead.", new StrikeAction { Amount = 2 }),
				Card("Jab", 1, "Deal 2 + Power ahead.", new StrikeAction { Amount = 2 }),
				Card(
					"Lunge",
					1,
					"Step 1, then deal 2 + Power ahead.",
					new StepAction(),
					new StrikeAction { Amount = 2 }
				),
				Card(
					"Sweep",
					2,
					"Deal Power ahead and to both columns beside it.",
					new StrikeAction { Amount = 0, Offsets = ThreeWide }
				),
				Card(
					"Feint",
					0,
					"Step 1. Draw a card.",
					new StepAction(),
					new DrawAction { Count = 1 }
				),
			]
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
			["Thump", "Bark Skin", "Jab", "Lunge", "Sweep"]
		);

	public static readonly ImmutableList<PartyScenario> Scenarios = [Alone, Pair];
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

		foreach (var (companion, space) in scenario.Companions)
		{
			(s, var ally) = s.AddObject(
				new Ally
				{
					Name = companion.Name,
					Hp = companion.Hp,
					MaxHp = companion.Hp,
					Power = companion.Power,
					Speed = companion.Speed,
					Space = space,
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
