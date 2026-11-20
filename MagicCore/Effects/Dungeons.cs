using System.Collections.Immutable;
using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Filters;
using MagicCore.Sets;
using MagicCore.Targeting;
using MagicCore.Turns;

namespace MagicCore.Effects;

/// <summary>
/// A dungeon (CR 309) in its venturer's command zone — Feywild Caretaker's initiative: "venture into Undercity". The
/// card is Undercity (CR 725.2: the only dungeon the initiative ventures into; the others wait for a card that
/// ventures into them); Room is where the venture marker is, an index into <see cref="Undercity.Rooms"/>. A room is
/// no Saga chapter: Undercity branches, so two rooms share a depth and no count says which one you are in.
/// </summary>
public record DungeonComponent(int Room) : GameComponent;

/// <summary>A dungeon room (CR 309.4): its ability, triggered as the venture marker moves into it, and the rooms below.</summary>
public sealed record Room(
	string Name,
	ImmutableArray<int> Exits,
	ImmutableList<TargetSpec> Targets,
	ImmutableList<GameAction> Effects
);

/// <summary>Undercity (Battle for Baldur's Gate) — the initiative's dungeon (CR 725.2), its nine rooms top to bottom.</summary>
public static class Undercity
{
	public const string Name = "Undercity";

	/// <summary>Catacombs: "Create a 4/1 black Skeleton creature token with menace."</summary>
	static readonly CardData Skeleton = Tokens.Token(
		"Skeleton",
		"Creature — Skeleton",
		"4",
		"1",
		["B"],
		["Menace"]
	);

	public static readonly ImmutableArray<Room> Rooms =
	[
		// 0
		new(
			"Secret Entrance",
			[1, 2],
			[],
			[
				new SearchLibrary(CardType.Land, ZoneType.Hand)
				{
					Supertype = Supertype.Basic,
					Reveal = true,
				},
			]
		),
		// 1 — "Put two +1/+1 counters on target creature."
		new("Forge", [3, 4], [TargetSpec.Creature], [new PutCounters(Ref.Target(0), 2)]),
		// 2 — "Scry 2."
		new("Lost Well", [4, 5], [], [new Surveil(2) { To = ZoneType.Library }]),
		// 3 — "Target player loses 5 life."
		new("Trap!", [6], [TargetSpec.Player()], [new LoseLife(Ref.Target(0), 5)]),
		// 4 — "Goad target creature."
		new("Arena", [6, 7], [TargetSpec.Creature], [Goad(Ref.Target(0))]),
		// 5 — "Create a Treasure token."
		new("Stash", [7], [], [Tokens.CreateTreasure(1)]),
		// 6 — "Draw a card."
		new("Archives", [8], [], [new DrawCards(Ref.You, 1)]),
		// 7
		new("Catacombs", [8], [], [new CreateTokens(Skeleton, 1)]),
		// 8 — "Reveal the top ten cards of your library. Put a creature card from among them onto the battlefield
		// with three +1/+1 counters on it. It gains hexproof until your next turn. Then shuffle."
		new(
			"Throne of the Dead Three",
			[],
			[],
			[
				new LookAndTake(10, Is.Creature)
				{
					To = ZoneType.Battlefield,
					Reveal = true,
					Must = true,
					Then =
					[
						new PutCounters(Ref.ThisWay, 3),
						new ModifyUntilEndOfTurn(Ref.ThisWay, 0, 0, "Hexproof")
						{
							UntilYourNextTurn = true,
						},
					],
				},
				new ShuffleLibrary(Ref.You),
			]
		),
	];

	/// <summary>
	/// Goad (CR 701.15): "until your next turn, that creature attacks each combat if able and attacks a player other
	/// than you if able" — Combat.Goaded, until the goader's next turn. ponytail: two players, so "a player other than
	/// you" is no rule — the only player it can attack is you.
	/// </summary>
	public static ModifyUntilEndOfTurn Goad(Ref what) =>
		new(what, 0, 0, Combat.Goaded) { UntilYourNextTurn = true };

	/// <summary>The dungeon card in a player's command zone, if they have one.</summary>
	public static Card? Of(GameState s, int playerId) =>
		s.CardsIn(playerId, ZoneType.Command)
			.FirstOrDefault(c => c.HasComponent<DungeonComponent>());

	/// <summary>
	/// CR 309.7 (704.5t): a dungeon whose marker is on its last room is removed once that room's ability has left the
	/// stack — the state-based check asks this beside the Saga's sacrifice.
	/// </summary>
	public static bool IsOnLastRoom(Card dungeon) =>
		dungeon.GetComponent<DungeonComponent>() is { } d && Rooms[d.Room].Exits.IsEmpty;

	/// <summary>
	/// CR 309.7: a dungeon removed on its last room is completed — removed, and counted on its venturer
	/// (MagicPlayer.DungeonsCompleted: Caves of Chaos Adventurer's "if you've completed a dungeon").
	/// </summary>
	public static GameState Complete(GameState s, int playerId, Card dungeon)
	{
		s = s.RemoveObject(dungeon.Id);
		var player = s.GetPlayer(playerId);
		return s.WithPlayer(player with { DungeonsCompleted = player.DungeonsCompleted + 1 });
	}
}

/// <summary>
/// "Venture into Undercity" (CR 701.49, 725.2) — the initiative's: with no dungeon, Undercity goes into your command
/// zone with the venture marker on its first room; otherwise the marker moves to a room below — a choice when there
/// are two (one choice, never a move per path). The room's ability triggers then, as a reflexive trigger is put on
/// the stack (WhenYouDo's path, Triggers.QueueTargets), its source the dungeon. On the last room (its ability not yet
/// left the stack, or the state check not yet run) that dungeon is completed — removed — and a new one begun.
/// </summary>
public record Venture : Effect
{
	public override ActionResult Execute(GameState s) => new(Into(s, Controller));

	public static GameState Into(GameState s, int playerId)
	{
		if (Undercity.Of(s, playerId) is { } done && Undercity.IsOnLastRoom(done))
			s = Undercity.Complete(s, playerId, done);
		if (Undercity.Of(s, playerId) is not { } dungeon)
		{
			s = GetEmblem.ToCommandZone(s, playerId, Undercity.Name, [new DungeonComponent(0)]);
			return EnterRoom(s, Undercity.Of(s, playerId)!.Id, 0);
		}
		var exits = Undercity.Rooms[dungeon.GetComponent<DungeonComponent>()!.Room].Exits;
		if (exits.Length == 1)
			return EnterRoom(s, dungeon.Id, exits[0]);
		return s.SpawnAction(
			new PipelineAction
			{
				Steps =
				[
					new ChooseKeywordChoice(
						playerId,
						[.. exits.Select(r => Undercity.Rooms[r].Name)]
					)
					{
						Prompt = "Venture into",
					},
					new MoveVentureMarkerAction(dungeon.Id, exits),
				],
			}
		);
	}

	public static GameState EnterRoom(GameState s, int dungeonId, int room)
	{
		s = s.UpdateObject(
			dungeonId,
			s.GetCard(dungeonId).WithComponentReplaced(new DungeonComponent(room))
		);
		var r = Undercity.Rooms[room];
		return Turns.Triggers.QueueTargets(
			s,
			new Turns.Triggers.Pending(
				s.GetCard(dungeonId),
				new TriggeredAbilityComponent(TriggerEvent.Reflexive, r.Effects)
				{
					Targets = r.Targets,
				},
				0
			),
			r.Targets,
			r.Effects
		);
	}
}

/// <summary>After Venture's choice: the marker into the chosen room below.</summary>
public record MoveVentureMarkerAction(int DungeonId, ImmutableArray<int> Exits) : GameAction
{
	public override ActionResult Execute(GameState s) =>
		InputContext.GetValueOrDefault(ChooseKeywordChoice.Chosen) is int id
			? new(Venture.EnterRoom(s, DungeonId, Exits[id - 1]))
			: new(s);
}

/// <summary>
/// The initiative (CR 725), held by one player: an object in its holder's command zone carrying the designation's
/// two triggers — "at the beginning of your upkeep, venture into Undercity" and "whenever one or more creatures deal
/// combat damage to you, their controller takes the initiative". Nearest piece: an emblem, which is forever and
/// never changes hands.
/// </summary>
public record InitiativeComponent : GameComponent
{
	public const string Name = "The Initiative";

	static readonly ImmutableArray<GameComponent> Abilities =
	[
		new InitiativeComponent(),
		new TriggeredAbilityComponent(TriggerEvent.YourUpkeep, [new Venture()]),
		new TriggeredAbilityComponent(
			new Trigger(EventKind.CombatDamageToPlayers) { Subject = Is.Player & Is.Yours },
			// ponytail: the active player is the attackers' controller with two players; multiplayer needs the
			// damage's source (DesignNotes)
			[new TakeTheInitiative(Ref.ActivePlayer)]
		),
	];

	/// <summary>Who has the initiative — 0 for nobody.</summary>
	public static int HolderOf(GameState s) =>
		new[] { s.Player1Id(), s.Player2Id() }.FirstOrDefault(p =>
			s.CardsIn(p, ZoneType.Command).Any(c => c.HasComponent<InitiativeComponent>())
		);

	/// <summary>CR 725.3: one holder — the designation leaves whoever had it, then is the new holder's.</summary>
	public static GameState Take(GameState s, int playerId)
	{
		foreach (var p in new[] { s.Player1Id(), s.Player2Id() })
		foreach (
			var held in s.CardsIn(p, ZoneType.Command)
				.Where(c => c.HasComponent<InitiativeComponent>())
				.ToList()
		)
			s = s.RemoveObject(held.Id);
		return GetEmblem.ToCommandZone(s, playerId, Name, Abilities);
	}
}

/// <summary>
/// Feywild Caretaker: "When this creature enters, you take the initiative." (CR 725.2) — the designation moves, then
/// its taker ventures into Undercity; taking it while holding it ventures again. ponytail: "whenever a player takes
/// the initiative, that player ventures" is folded into the take, not a trigger of its own — no window to respond
/// between them (DesignNotes).
/// </summary>
public record TakeTheInitiative(Ref Who) : Effect
{
	public override ActionResult Execute(GameState s)
	{
		foreach (var player in All(Who, s).Take(1).ToList())
			s = Venture.Into(InitiativeComponent.Take(s, player), player);
		return new(s);
	}
}

/// <summary>Caves of Chaos Adventurer: "If you've completed a dungeon" — this game (MagicPlayer.DungeonsCompleted).</summary>
public record YouCompletedADungeon : Condition
{
	public override bool Holds(GameState s, ImmutableDictionary<string, object> context) =>
		s.GetPlayer((int)context[ContextKeys.Controller]).DungeonsCompleted > 0;
}

/// <summary>Feywild Caretaker: "if you have the initiative".</summary>
public record YouHaveTheInitiative : Condition
{
	public override bool Holds(GameState s, ImmutableDictionary<string, object> context) =>
		InitiativeComponent.HolderOf(s) == (int)context[ContextKeys.Controller];
}
