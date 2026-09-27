using System.Collections.Immutable;
using KinCore.Party;

namespace KinCore.Tests;

/// <summary>
/// **THE WILD ROUTE — every rule FIRES** (`KinMapPlan.md` §4–5): the map's shape, what lives where,
/// walking along links only, a fight before you walk on, finds and springs on arrival, the next town
/// at the end. Uses the run fixtures in `PartyRunTests.cs`.
/// </summary>
public partial class PartyRunTests
{
	/// <summary>A run on a straight route: the town, the given places in a line, the next town.</summary>
	private static PartyRun OnRoute(PartyRun run, params RouteNode[] middle)
	{
		var nodes = new List<RouteNode> { new(0, NodeKind.Start, 0, 0.5) };
		nodes.AddRange(middle.Select((n, i) => n with { Id = i + 1, Row = i + 1 }));
		nodes.Add(new(nodes.Count, NodeKind.End, nodes.Count, 0.5));
		var links = Enumerable.Range(0, nodes.Count - 1).Select(i => new RouteLink(i, i + 1));
		return run with
		{
			Phase = RunPhase.Route,
			Route = new([.. nodes], [.. links]),
			NodeId = 0,
			Cleared = [0],
		};
	}

	/// <summary>Standing on the first of a line of route fights — for the rules that need a foe that hits.</summary>
	private static PartyRun OnFights(PartyRun run, params Encounter[] fights) =>
		OnRoute(run, [.. fights.Select(f => Place(NodeKind.Wild, f))]).MoveTo(1);

	private static RouteNode Place(
		NodeKind kind,
		Encounter? fight = null,
		FindKind find = default
	) => new(0, kind, 0, 0.5, 0, fight, find);

	private static IEnumerable<RouteMap> Routes(int seeds = 40) =>
		Enumerable.Range(0, seeds).Select(seed => (Run() with { Seed = seed }).EnterRoute().Route!);

	// ===== Leaving town — and the leader

	[Test]
	public void TheFirstTownHasNoLeaderSoYouCanSetOut()
	{
		var run = Run();

		Assert.That(run.HasLeader, Is.False);
		Assert.That(run.CannotLeaveTown, Is.Null);
		Assert.That(run.FightLeader(), Is.EqualTo(run), "no leader to fight");
		Assert.That(run.EnterRoute().Phase, Is.EqualTo(RunPhase.Route));
	}

	[Test]
	public void FromTheSecondTownTheLeaderMustBeBeatenBeforeYouLeave()
	{
		var run = Run() with
		{
			RegionIndex = 1,
			Regions = [Region("R1"), Region("R2"), Region("R3")],
		};

		Assert.That(run.CannotLeaveTown, Is.EqualTo("Beat the leader first"));
		Assert.That(run.EnterRoute(), Is.EqualTo(run), "a refused exit changes nothing");

		run = run.FightLeader();
		Assert.That(run.Phase, Is.EqualTo(RunPhase.Gym));
		Assert.That(run.NextFight.Foes.Single().Name, Is.EqualTo("R2 Gym"));

		(run, var report) = WinNext(run);

		Assert.That(report.Gold, Is.EqualTo(PartyRun.GymGold));
		Assert.That(run.Phase, Is.EqualTo(RunPhase.Town), "back in the town");
		Assert.That(run.LeaderBeaten && run.CannotLeaveTown is null, Is.True);
		Assert.That(run.FightLeader(), Is.EqualTo(run), "a leader is beaten once");
		Assert.That(run.EnterRoute().Phase, Is.EqualTo(RunPhase.Route));
	}

	[Test]
	public void ANewTownsLeaderIsUnbeaten()
	{
		var run = OnRoute(Run() with { LeaderBeaten = true });

		Assert.That(run.MoveTo(1).LeaderBeaten, Is.False);
	}

	// ===== The map

	[Test]
	public void EveryPlaceOnARouteIsOnAPathFromTheTownToTheNext()
	{
		foreach (var route in Routes())
		{
			Assert.That(route.Start.Kind, Is.EqualTo(NodeKind.Start));
			Assert.That(route.End.Kind, Is.EqualTo(NodeKind.End));
			Assert.That(
				route.Links.All(l => route.Nodes[l.To].Row == route.Nodes[l.From].Row + 1),
				"links only run forward, one row at a time"
			);

			var reached = new HashSet<int> { 0 };
			foreach (var node in route.Nodes.OrderBy(n => n.Row))
				if (reached.Contains(node.Id))
					reached.UnionWith(route.Next(node.Id).Select(n => n.Id));
			Assert.That(reached, Has.Count.EqualTo(route.Nodes.Count), "every place is reachable");

			var leads = new HashSet<int> { route.End.Id };
			foreach (var node in route.Nodes.OrderByDescending(n => n.Row))
				if (route.Next(node.Id).Any(n => leads.Contains(n.Id)))
					leads.Add(node.Id);
			Assert.That(leads, Has.Count.EqualTo(route.Nodes.Count), "every place leads on");
		}
	}

	[Test]
	public void TheFirstForkIsAVisibleFightInEachAreaAndEveryRouteHasTallGrassAndOneRare()
	{
		foreach (var route in Routes())
		{
			var first = route.Nodes.Where(n => n.Row == 1).ToList();
			Assert.That(first.Select(n => n.Kind), Is.All.EqualTo(NodeKind.Wild));
			Assert.That(first.Select(n => n.Area), Is.EquivalentTo(new[] { 0, 1 }));

			Assert.That(route.Nodes.Count(n => n.Kind == NodeKind.Grass), Is.GreaterThan(0));
			var rare = route.Nodes.Single(n => n.Kind == NodeKind.Rare);
			Assert.That(
				rare.Encounter!.Foes.Select(f => f.Name),
				Does.Contain(Run().Region.Areas[rare.Area].Rare.Name)
			);
		}
	}

	[Test]
	public void AWildFightComesFromThePoolOfTheAreaItLiesIn()
	{
		var areas = Run().Region.Areas;
		foreach (var route in Routes())
		foreach (var node in route.Nodes.Where(n => n.Kind is NodeKind.Wild or NodeKind.Grass))
			Assert.That(
				node.Encounter!.Foes.Select(f => f.Name),
				Is.All.AnyOf(areas[node.Area].Pool.Select(f => f.Name).ToArray())
			);
	}

	[Test]
	public void NothingInATrainersLineCanBeCaught()
	{
		var trainers = Routes(80).SelectMany(r => r.Nodes).Where(n => n.Kind == NodeKind.Trainer);
		Assert.That(trainers, Is.Not.Empty, "80 routes field a trainer somewhere");
		Assert.That(
			trainers.SelectMany(n => n.Encounter!.Foes),
			Has.All.Matches<Foe>(f => !f.Catchable)
		);
	}

	// ===== Walking it

	[Test]
	public void YouWalkOnlyAlongALink()
	{
		var run = Run().EnterRoute();
		var ahead = run.Route!.Next(0).First().Id;

		Assert.That(
			run.CannotMoveTo(run.Route.End.Id),
			Is.Not.Null,
			"the end is not one step away"
		);
		Assert.That(
			run.MoveTo(run.Route.End.Id),
			Is.EqualTo(run),
			"a refused walk changes nothing"
		);
		Assert.That(run.CannotMoveTo(ahead), Is.Null);
		Assert.That(run.MoveTo(ahead).NodeId, Is.EqualTo(ahead));
	}

	[TestCase(NodeKind.Wild, PartyRun.WildGold)]
	[TestCase(NodeKind.Rare, PartyRun.DeepGold)]
	[TestCase(NodeKind.Trainer, PartyRun.TrainerGold)]
	public void AFightMustBeWonBeforeYouWalkOnAndPaysByItsKind(NodeKind kind, int gold)
	{
		var run = OnRoute(Run(), Place(kind, Fight(Foe("W"))), Place(NodeKind.Find)).MoveTo(1);

		Assert.That(run.CannotMoveTo(2), Is.EqualTo("Win the fight here first"));

		(run, var report) = WinNext(run);

		Assert.That(report.Gold, Is.EqualTo(gold));
		Assert.That(run.Phase, Is.EqualTo(RunPhase.Route), "you stay where you fought");
		Assert.That(run.HereIsCleared);
		Assert.That(run.MoveTo(2).NodeId, Is.EqualTo(2));
	}

	[TestCase(FindKind.Snare)]
	[TestCase(FindKind.Gold)]
	public void AFindIsTakenOnArrival(FindKind find)
	{
		var run = OnRoute(Run(), Place(NodeKind.Find, find: find));

		var after = run.MoveTo(1);

		Assert.That(after.Snares - run.Snares, Is.EqualTo(find == FindKind.Snare ? 1 : 0));
		Assert.That(
			after.Gold - run.Gold,
			Is.EqualTo(find == FindKind.Gold ? PartyRun.FoundGold : 0)
		);
		Assert.That(after.HereIsCleared, "you can walk straight on");
	}

	[Test]
	public void ASpringHealsAShareOnArrival()
	{
		var run = OnRoute(
			WithTeam(Run(), A) with
			{
				Team = [new RunCompanion(A, 10)],
			},
			Place(NodeKind.Rest)
		);

		Assert.That(Hp(run.MoveTo(1), "A"), Is.EqualTo(10 + 12), "30% of 40");
	}

	[Test]
	public void TheEndOfTheRouteIsTheNextTownWhereNobodyIsHealedForFree()
	{
		var run = OnRoute(Run() with { Team = [new RunCompanion(A, 5)] });

		var after = run.MoveTo(1);

		Assert.That(after.Phase, Is.EqualTo(RunPhase.Town));
		Assert.That(after.RegionIndex, Is.EqualTo(1));
		Assert.That(after.Route, Is.Null);
		Assert.That(Hp(after, "A"), Is.EqualTo(5), "the hospital sells healing now");
	}

	[Test]
	public void TheEndOfTheLastRegionsRouteWinsTheRun()
	{
		var run = OnRoute(Run() with { RegionIndex = 1 });

		Assert.That(run.MoveTo(1).Phase, Is.EqualTo(RunPhase.Won));
	}

	[Test]
	public void ARouteReplaysFromItsSeed()
	{
		var once = (Run() with { Seed = 7 }).EnterRoute().Route!;
		var again = (Run() with { Seed = 7 }).EnterRoute().Route!;

		Assert.That(
			again.Nodes.Select(n => (n.Kind, n.X)),
			Is.EqualTo(once.Nodes.Select(n => (n.Kind, n.X)))
		);
		Assert.That(again.Links, Is.EqualTo(once.Links));
	}
}
