using System.Collections.Immutable;

namespace KinCore.Party;

/// <summary>What a place on a wild route is (`KinMapPlan.md` §4).</summary>
public enum NodeKind
{
	/// <summary>Where the route leaves the town.</summary>
	Start,

	/// <summary>A VISIBLE wild fight: its species are shown, so choosing it is choosing a catch.</summary>
	Wild,

	/// <summary>
	/// **An ELITE**: a mini-boss with its own patterns, SHOWN on the map, so taking it is a choice.
	/// Pays a rare-led card reward, a RELIC, and big XP and gold. Nothing in it can be caught.
	/// </summary>
	Elite,

	/// <summary>Something lying on the path — gold (<see cref="RouteNode.Find"/>).</summary>
	Find,

	/// <summary>A spring: every monster heals a share of its max.</summary>
	Rest,

	/// <summary>
	/// **The region's BOSS**, at the top of the map where it is seen all the way up. Beaten, the next
	/// town — or, the last, the run won. The row before it is always a spring.
	/// </summary>
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
	Encounter? Encounter = null,
	FindKind Find = FindKind.Gold
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
	/// <summary>
	/// The rows of choices between the town and the spring before the boss — 8, since five regions
	/// replaced ten (Shayne, 2026-09-28: "longer").
	/// </summary>
	public const int Middle = 8;

	/// <summary>The rows an elite may stand on: past the first choices, before the rare's row.</summary>
	private static readonly int[] EliteRows = [4, 5, 6, 7];

	public static RouteMap Build(Region region, Random rng, Encounter boss)
	{
		var nodes = new List<RouteNode>();
		var rows = new List<List<int>>();

		void Row(int row, IEnumerable<(NodeKind Kind, double X)> places)
		{
			var ids = new List<int>();
			foreach (var (kind, x) in places.OrderBy(p => p.X))
			{
				ids.Add(nodes.Count);
				nodes.Add(Place(nodes.Count, kind, row, x, region, rng, boss));
			}
			rows.Add(ids);
		}

		Row(0, [(NodeKind.Start, 0.5)]);
		// The first fork is one visible fight in each biome.
		Row(1, [(NodeKind.Wild, 0.25), (NodeKind.Wild, 0.75)]);
		// **1–2 ELITES, each on its own row** — shown, and a fork around each, so they can be dodged.
		var eliteRows = EliteRows.OrderBy(_ => rng.Next()).Take(rng.Next(1, 3)).ToHashSet();
		for (var row = 2; row <= Middle; row++)
		{
			var kinds = Enumerable.Range(0, rng.Next(2, 4)).Select(_ => Roll(rng)).ToList();
			if (eliteRows.Contains(row))
				kinds[rng.Next(kinds.Count)] = NodeKind.Elite;
			Row(row, kinds.Select((k, i) => (k, Spread(i, kinds.Count, rng))));
		}
		// **A spring before the boss, always — a share of HP, never a full heal** (Shayne: like STS).
		Row(Middle + 1, [(NodeKind.Rest, 0.5)]);
		Row(Middle + 2, [(NodeKind.End, 0.5)]);

		return new([.. nodes], [.. Link(rows, rng)]);
	}

	/// <summary>
	/// A middle place's kind: visible fights most often, then tall grass, finds, trainers and springs.
	/// Grass at 3 in 10 made one capture's route six-tenths "?" — a map of question marks is no map.
	/// </summary>
	private static NodeKind Roll(Random rng) =>
		rng.Next(10) switch
		{
			< 7 => NodeKind.Wild,
			< 9 => NodeKind.Find,
			_ => NodeKind.Rest,
		};

	/// <summary>Evenly across the map, with a little jitter so it does not read as a grid.</summary>
	private static double Spread(int i, int count, Random rng) =>
		0.08 + 0.84 * (i + 0.5) / count + (rng.NextDouble() - 0.5) * 0.1;

	/// <summary>The rows whose fights draw from the region's EASY list — a route's first fights.</summary>
	public const int EasyRows = 2;

	private static RouteNode Place(
		int id,
		NodeKind kind,
		int row,
		double x,
		Region region,
		Random rng,
		Encounter boss
	)
	{
		var node = new RouteNode(id, kind, row, x);
		var fights = row <= EasyRows ? region.Easy : region.Normal;
		return kind switch
		{
			NodeKind.Wild => node with { Encounter = fights[rng.Next(fights.Count)] },
			NodeKind.Elite => node with
			{
				Encounter = region.Elites[rng.Next(region.Elites.Count)],
			},
			NodeKind.End => node with { Encounter = boss },
			NodeKind.Find => node with { Find = FindKind.Gold },
			_ => node,
		};
	}

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
