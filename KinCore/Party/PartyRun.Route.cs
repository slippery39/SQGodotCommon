namespace KinCore.Party;

/// <summary>
/// **Walking a wild ROUTE** (`KinMapPlan.md` §4–5): a branching map from the town you left to the
/// next. You walk only to a place linked from where you stand, and only once its business is done —
/// a fight won, a find taken. A find or a spring resolves on arrival; a fight waits for its battle
/// (`NextFight`, `StartBattle`, `AfterBattle`, as on a trail). The end of the route is the next town.
/// </summary>
public partial record PartyRun
{
	public const int TrainerGold = 40;

	/// <summary>Where you stand on the route.</summary>
	public RouteNode Here =>
		Route?.Nodes[NodeId] ?? throw new InvalidOperationException("Not on a route");

	/// <summary>Whether the place you stand on is done with — a fight is not until it is won.</summary>
	public bool HereIsCleared => Cleared.Contains(NodeId);

	/// <summary>
	/// **Out of the town, onto its route** — drawn now from the region, seeded. Refused while
	/// <see cref="CannotLeaveTown"/> says so (the leader, from the second town on).
	/// </summary>
	public PartyRun EnterRoute() =>
		CannotLeaveTown is not null
			? this
			: this with
			{
				Phase = RunPhase.Route,
				Route = PartyRoutes.Build(Region, new Random(Seed * 17 + RegionIndex * 7)),
				NodeId = 0,
				Cleared = [0],
				Sold = [],
			};

	/// <summary>Why you cannot walk to that place — or null if you can.</summary>
	public string? CannotMoveTo(int node) =>
		Phase != RunPhase.Route || Route is null ? "Not on a route"
		: !HereIsCleared ? "Win the fight here first"
		: !Route.Linked(NodeId, node) ? "Not a path from here"
		: null;

	/// <summary>
	/// **Walks to a linked place and meets what is there.** A find or spring is taken at once; the end
	/// is the next town (or the run won, after the last region); a fight waits for its battle.
	/// A refused walk changes nothing (see <see cref="CannotMoveTo"/>).
	/// </summary>
	public PartyRun MoveTo(int node)
	{
		if (CannotMoveTo(node) is not null)
			return this;

		var run = this with { NodeId = node };
		var here = run.Here;
		return here.Kind switch
		{
			NodeKind.End => RegionIndex + 1 >= Regions.Count
				? run with
				{
					Phase = RunPhase.Won,
				}
				: run.EnterTown(RegionIndex + 1),
			NodeKind.Find => run.Clear() with
			{
				Snares = Snares + (here.Find == FindKind.Snare ? 1 : 0),
				Gold = Gold + (here.Find == FindKind.Gold ? FoundGold : 0),
			},
			NodeKind.Rest => run.Clear() with
			{
				Team = Heal(Team, RestHeal),
				Bench = Heal(Bench, RestHeal),
			},
			_ when here.IsFight => run,
			_ => run.Clear(),
		};
	}

	private PartyRun Clear() => this with { Cleared = Cleared.Add(NodeId) };

	/// <summary>What winning a route fight pays.</summary>
	private static int RouteGold(NodeKind kind) =>
		kind switch
		{
			NodeKind.Rare => DeepGold,
			NodeKind.Trainer => TrainerGold,
			_ => WildGold,
		};
}
