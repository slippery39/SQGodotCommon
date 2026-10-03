namespace KinCore.Party;

/// <summary>
/// **Walking a wild ROUTE** (`KinMapPlan.md` §4–5): a branching map from the town you left to the
/// next. You walk only to a place linked from where you stand, and only once its business is done —
/// a fight won, a find taken. A find or a spring resolves on arrival; a fight waits for its battle
/// (`NextFight`, `StartBattle`, `AfterBattle`, as on a trail). The end of the route is the next town.
/// </summary>
public partial record PartyRun
{
	/// <summary>Where you stand on the route.</summary>
	public RouteNode Here =>
		Route?.Nodes[NodeId] ?? throw new InvalidOperationException("Not on a route");

	/// <summary>Whether the place you stand on is done with — a fight is not until it is won.</summary>
	public bool HereIsCleared => Cleared.Contains(NodeId);

	/// <summary>
	/// **Out of the town, onto its route** — drawn now from the region, seeded, with the region's BOSS
	/// at its end.
	/// </summary>
	public PartyRun EnterRoute() =>
		CannotLeaveTown is not null
			? this
			: this with
			{
				Phase = RunPhase.Route,
				Route = PartyRoutes.Build(Region, new Random(Seed * 17 + RegionIndex * 7), Boss),
				NodeId = 0,
				Cleared = [0],
				Sold = [],
				RelicChoice = [],
				EvolutionDue = false,
			};

	/// <summary>Why you cannot walk to that place — or null if you can.</summary>
	public string? CannotMoveTo(int node) =>
		Phase != RunPhase.Route || Route is null ? "Not on a route"
		: !HereIsCleared
			? (
				Here.Kind == NodeKind.Rest
					? "Choose at the spring first"
					: "Win the fight here first"
			)
		: !Route.Linked(NodeId, node) ? "Not a path from here"
		: null;

	/// <summary>
	/// **Walks to a linked place and meets what is there.** A find or spring is taken at once; a fight
	/// — the boss at the end among them — waits for its battle.
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
			NodeKind.Find => run.Clear() with
			{
				Gold = Gold + (here.Find == FindKind.Gold ? FoundGold : 0),
			},
			// A SPRING waits for its choice: heal, or upgrade a card (round 4).
			NodeKind.Rest => run,
			_ when here.IsFight => run,
			_ => run.Clear(),
		};
	}

	private PartyRun Clear() => this with { Cleared = Cleared.Add(NodeId) };

	// ===== The SPRING: heal, or upgrade a card (round 4 — STS's campfire)

	/// <summary>Whether you stand at a spring that is still waiting for its choice.</summary>
	public bool AtSpring => Phase == RunPhase.Route && Here.Kind == NodeKind.Rest && !HereIsCleared;

	/// <summary>The deck's cards a spring can upgrade, by deck index — those with a + version.</summary>
	public IEnumerable<int> Upgradable =>
		Enumerable.Range(0, Deck.Count).Where(i => Deck[i].Upgraded is not null);

	/// <summary>**The spring's HEAL**: every monster heals a share of its max, and you walk on.</summary>
	public PartyRun HealAtSpring() =>
		AtSpring ? Clear() with { Team = Heal(Team, RestHeal) } : this;

	/// <summary>**The spring's UPGRADE**: that card becomes its + version, and you walk on.</summary>
	public PartyRun UpgradeAtSpring(int deckIndex) =>
		AtSpring && Upgradable.Contains(deckIndex)
			? Clear() with
			{
				Deck = Deck.SetItem(deckIndex, Deck[deckIndex].Upgraded!),
			}
			: this;

	/// <summary>What winning a route fight pays.</summary>
	private static int RouteGold(NodeKind kind) =>
		kind switch
		{
			NodeKind.Elite => EliteGold,
			NodeKind.End => BossGold,
			_ => WildGold,
		};
}
