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
	/// <summary>A run on a straight route: the town, the given places in a line, the region's boss.</summary>
	private static PartyRun OnRoute(PartyRun run, params RouteNode[] middle)
	{
		var nodes = new List<RouteNode> { new(0, NodeKind.Start, 0, 0.5) };
		nodes.AddRange(middle.Select((n, i) => n with { Id = i + 1, Row = i + 1 }));
		nodes.Add(new(nodes.Count, NodeKind.End, nodes.Count, 0.5, Encounter: run.Boss));
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
	) => new(0, kind, 0, 0.5, fight, find);

	private static IEnumerable<RouteMap> Routes(int seeds = 40) =>
		Enumerable.Range(0, seeds).Select(seed => (Run() with { Seed = seed }).EnterRoute().Route!);

	// ===== Leaving town — and the BOSS at the route's end

	[Test]
	public void EveryTownLetsYouSetOutAndItsBossIsKnownAlready()
	{
		foreach (var region in new[] { 0, 1 })
		{
			var run = Run() with { RegionIndex = region };

			Assert.That(run.CannotLeaveTown, Is.Null);
			var route = run.EnterRoute();
			Assert.That(route.Phase, Is.EqualTo(RunPhase.Route));
			Assert.That(
				route.Route!.End.Encounter,
				Is.EqualTo(run.Boss),
				"the boss, shown from town"
			);
		}
	}

	[Test]
	public void TheRowBeforeTheBossIsAlwaysASpring()
	{
		foreach (var route in Routes())
		{
			var before = route.Nodes.Where(n => n.Row == route.End.Row - 1).ToList();
			Assert.That(before.Select(n => n.Kind), Is.EqualTo(new[] { NodeKind.Rest }));
		}
	}

	[Test]
	public void EveryRouteHasOneOrTwoElitesOnTheirOwnRows()
	{
		foreach (var route in Routes())
		{
			var elites = route.Nodes.Where(n => n.Kind == NodeKind.Elite).ToList();
			Assert.That(elites.Count, Is.InRange(1, 2));
			Assert.That(elites.Select(n => n.Row).Distinct().Count(), Is.EqualTo(elites.Count));
		}
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
	public void TheFirstForkIsTwoWildFights()
	{
		foreach (var route in Routes())
			Assert.That(
				route.Nodes.Where(n => n.Row == 1).Select(n => n.Kind),
				Is.EqualTo(new[] { NodeKind.Wild, NodeKind.Wild })
			);
	}

	/// <summary>
	/// **A wild fight is one of the region's AUTHORED encounters** (`KinEnemiesPlan.md`): the route's
	/// first rows from its easy list, the rest from its normal one.
	/// </summary>
	[Test]
	public void AWildFightIsAnAuthoredEncounterEasyOnTheFirstRows()
	{
		// By their foes' names: each Run() builds its regions afresh, so the lists are new instances.
		static string Key(Encounter e) => string.Join(",", e.Foes.Select(f => f.Name));
		var region = Run().Region;
		foreach (var route in Routes())
		foreach (var node in route.Nodes.Where(n => n.Kind == NodeKind.Wild))
			Assert.That(
				(node.Row <= PartyRoutes.EasyRows ? region.Easy : region.Normal).Select(Key),
				Does.Contain(Key(node.Encounter!)),
				$"row {node.Row}"
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
	[TestCase(NodeKind.Elite, PartyRun.EliteGold)]
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

	[Test]
	public void AFindIsTakenOnArrival()
	{
		var run = OnRoute(Run(), Place(NodeKind.Find, find: FindKind.Gold));

		var after = run.MoveTo(1);

		Assert.That(after.Gold - run.Gold, Is.EqualTo(PartyRun.FoundGold));
		Assert.That(after.HereIsCleared, "you can walk straight on");
	}

	[Test]
	public void ASpringWaitsForItsChoiceAndItsHealIsAShare()
	{
		var run = OnRoute(
				WithTeam(Run(), A) with
				{
					Team = [new RunCompanion(A, 10)],
				},
				Place(NodeKind.Rest),
				Place(NodeKind.Find)
			)
			.MoveTo(1);

		Assert.That(run.AtSpring, Is.True);
		Assert.That(Hp(run, "A"), Is.EqualTo(10), "nothing until you choose");
		Assert.That(run.CannotMoveTo(2), Is.EqualTo("Choose at the spring first"));

		var healed = run.HealAtSpring();
		Assert.That(Hp(healed, "A"), Is.EqualTo(10 + 12), "30% of 40");
		Assert.That(healed.CannotMoveTo(2), Is.Null);
	}

	[Test]
	public void ASpringCanUpgradeACardInsteadToItsPlusVersion()
	{
		var plain = Wipe("Punch");
		var better = Wipe("Punch") with { Cost = 0 };
		var run = OnRoute(
				Run() with
				{
					Deck = [Wipe("Wipe"), PartyCards.Plus(plain, better)],
				},
				Place(NodeKind.Rest)
			)
			.MoveTo(1);

		Assert.That(run.Upgradable, Is.EqualTo(new[] { 1 }), "only the card with a +");

		var after = run.UpgradeAtSpring(1);

		Assert.That(after.Deck[1].Name, Is.EqualTo("Punch+"));
		Assert.That(after.Deck[1].Cost, Is.Zero);
		Assert.That(after.Upgradable, Is.Empty, "a + is not upgraded again");
		Assert.That(after.HereIsCleared, Is.True, "and you walk on");
	}

	[Test]
	public void TheBossAtTheEndMustBeBeatenAndThenIsTheNextTown()
	{
		var run = OnRoute(Run() with { Team = [new RunCompanion(A, 30)] }).MoveTo(1);

		Assert.That(run.AtBoss && run.Phase == RunPhase.Route, "the end is a fight now");
		Assert.That(run.NextFight, Is.EqualTo(run.Boss));

		(var after, var report) = WinNext(run);

		Assert.That(report.Gold, Is.EqualTo(PartyRun.BossGold));
		Assert.That(after.Phase, Is.EqualTo(RunPhase.Town));
		Assert.That(after.RegionIndex, Is.EqualTo(1));
		Assert.That(after.Route, Is.Null);
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
