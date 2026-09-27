using System.Collections.Immutable;

namespace KinCore.Party;

/// <summary>What a place on a wild route is (`KinMapPlan.md` §4).</summary>
public enum NodeKind
{
	/// <summary>Where the route leaves the town.</summary>
	Start,

	/// <summary>A VISIBLE wild fight: its species are shown, so choosing it is choosing a catch.</summary>
	Wild,

	/// <summary>Tall grass: a wild fight from the pool, not shown until you walk in.</summary>
	Grass,

	/// <summary>The route's rare, on one branch only: harder, and the only place it lives.</summary>
	Rare,

	/// <summary>A trainer's line: pays more, and nothing in it can be caught.</summary>
	Trainer,

	/// <summary>Something lying on the path — a Snare or gold (<see cref="RouteNode.Find"/>).</summary>
	Find,

	/// <summary>A spring: every monster heals a share of its max.</summary>
	Rest,

	/// <summary>The next town's gate.</summary>
	End,
}

/// <summary>
/// **One place on a route.** `Row` 0 is the town you left and the last row the next town; `X` is
/// 0–1 across the map, so the screen places it from DATA. `Area` is which of the region's two areas
/// (biomes) it lies in — its pool is that area's. A fight place carries its encounter, rolled when
/// the route is drawn, so a run replays (tall grass is rolled too, only not shown).
/// </summary>
public record RouteNode(
	int Id,
	NodeKind Kind,
	int Row,
	double X,
	int Area = 0,
	Encounter? Encounter = null,
	FindKind Find = FindKind.Snare
)
{
	public bool IsFight => Encounter is not null;
}

public record RouteLink(int From, int To);

/// <summary>
/// **A wild route: a branching path from one town to the next** (`KinMapPlan.md`). A layered graph:
/// links only run from a row to the next, so every walk moves forward and ends at the next town.
/// Node ids are their index in `Nodes`, in row order — the start is 0, the end is last.
/// </summary>
public record RouteMap(ImmutableList<RouteNode> Nodes, ImmutableList<RouteLink> Links)
{
	public RouteNode Start => Nodes[0];
	public RouteNode End => Nodes[^1];

	public IEnumerable<RouteNode> Next(int id) =>
		Links.Where(l => l.From == id).Select(l => Nodes[l.To]);

	public bool Linked(int from, int to) => Links.Any(l => l.From == from && l.To == to);
}

/// <summary>
/// **Draws a region's route** from its tier and its two areas' pools: the left of the map is its
/// first area, the right its second, so a fork is a choice of biome — and of what you can catch.
/// Every number is a guess (exploring, not tuning).
/// </summary>
public static class PartyRoutes
{
	/// <summary>The rows between the two towns.</summary>
	public const int Middle = 5;

	public static RouteMap Build(Region region, Random rng)
	{
		var nodes = new List<RouteNode>();
		var rows = new List<List<int>>();

		void Row(int row, IEnumerable<(NodeKind Kind, double X)> places)
		{
			var ids = new List<int>();
			foreach (var (kind, x) in places.OrderBy(p => p.X))
			{
				var area = x < 0.5 ? 0 : 1;
				ids.Add(nodes.Count);
				nodes.Add(Place(nodes.Count, kind, row, x, region, region.Areas[area], area, rng));
			}
			rows.Add(ids);
		}

		Row(0, [(NodeKind.Start, 0.5)]);
		// The first fork is one visible fight in each biome: the first choice is what to catch.
		Row(1, [(NodeKind.Wild, 0.25), (NodeKind.Wild, 0.75)]);
		for (var row = 2; row <= Middle; row++)
		{
			var kinds = Enumerable.Range(0, rng.Next(2, 4)).Select(_ => Roll(rng)).ToList();
			// Guarantees, so every route has each kind of choice: tall grass early, the rare late.
			if (row == 2)
				kinds[0] = NodeKind.Grass;
			if (row == Middle)
				kinds[rng.Next(kinds.Count)] = NodeKind.Rare;
			Row(row, kinds.Select((k, i) => (k, Spread(i, kinds.Count, rng))));
		}
		Row(Middle + 1, [(NodeKind.End, 0.5)]);

		return new([.. nodes], [.. Link(rows, rng)]);
	}

	/// <summary>
	/// A middle place's kind: visible fights most often, then tall grass, finds, trainers and springs.
	/// Grass at 3 in 10 made one capture's route six-tenths "?" — a map of question marks is no map.
	/// </summary>
	private static NodeKind Roll(Random rng) =>
		rng.Next(10) switch
		{
			< 3 => NodeKind.Wild,
			< 5 => NodeKind.Grass,
			< 7 => NodeKind.Find,
			< 9 => NodeKind.Trainer,
			_ => NodeKind.Rest,
		};

	/// <summary>Evenly across the map, with a little jitter so it does not read as a grid.</summary>
	private static double Spread(int i, int count, Random rng) =>
		0.08 + 0.84 * (i + 0.5) / count + (rng.NextDouble() - 0.5) * 0.1;

	private static RouteNode Place(
		int id,
		NodeKind kind,
		int row,
		double x,
		Region region,
		Area area,
		int areaIndex,
		Random rng
	)
	{
		Encounter Wild(int count, Foe? rare = null, int? level = null) =>
			PartyWorld.WildFight(
				area,
				count,
				rare,
				rng,
				level ?? region.MinLevel,
				level ?? region.MaxLevel,
				region.RareLevel
			);
		var node = new RouteNode(id, kind, row, x, areaIndex);
		return kind switch
		{
			NodeKind.Wild or NodeKind.Grass => node with
			{
				Encounter = Wild(rng.Next(region.MinFoes, region.MaxFoes + 1)),
			},
			NodeKind.Rare => node with
			{
				Encounter = Wild(Math.Min(PartyBattle.MaxLine, region.MaxFoes + 1), area.Rare),
			},
			NodeKind.Trainer => node with
			{
				Encounter = Trainer(Wild(region.MaxFoes, level: region.TrainerLevel)),
			},
			NodeKind.Find => node with { Find = rng.Next(2) == 0 ? FindKind.Snare : FindKind.Gold },
			_ => node,
		};
	}

	/// <summary>A trainer's line is their monsters, not wild ones: none can be caught.</summary>
	private static Encounter Trainer(Encounter wild) =>
		new("Trainer", [.. wild.Foes.Select(f => f with { Catchable = false })]);

	/// <summary>
	/// **Links from each row to the next, without crossing**: each place to its proportional place
	/// ahead, sometimes to the one beside it as well (a fork); then any place left unreached gets a
	/// link from the place proportionally behind it.
	/// </summary>
	private static IEnumerable<RouteLink> Link(List<List<int>> rows, Random rng)
	{
		var links = new HashSet<(int, int)>();
		for (var r = 0; r + 1 < rows.Count; r++)
		{
			var (from, to) = (rows[r], rows[r + 1]);
			int Ahead(int i) =>
				from.Count == 1
					? 0
					: (int)Math.Round(i * (to.Count - 1) / (double)(from.Count - 1));

			for (var i = 0; i < from.Count; i++)
			{
				if (from.Count == 1 || to.Count == 1)
				{
					// A town fans out to (or gathers from) the whole row.
					foreach (var t in to)
						links.Add((from[i], t));
					continue;
				}
				var j = Ahead(i);
				links.Add((from[i], to[j]));
				var nextStartsAfter = i == from.Count - 1 || Ahead(i + 1) > j;
				if (j + 1 < to.Count && nextStartsAfter && rng.Next(10) < 4)
					links.Add((from[i], to[j + 1]));
			}
			for (var k = 0; k < to.Count; k++)
				if (!links.Any(l => l.Item2 == to[k]))
				{
					var i = (int)
						Math.Round(k * (from.Count - 1) / (double)Math.Max(1, to.Count - 1));
					links.Add((from[i], to[k]));
				}
		}
		return links
			.OrderBy(l => l.Item1)
			.ThenBy(l => l.Item2)
			.Select(l => new RouteLink(l.Item1, l.Item2));
	}
}
