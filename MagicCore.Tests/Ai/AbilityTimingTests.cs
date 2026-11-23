using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Ai;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Targeting;

namespace MagicCore.Tests.Ai;

/// <summary>
/// When the search uses a free tap ability (story AI-26.5, AI-13's lesson 6) — a measurement first: hand-built
/// positions, the two turns played out and printed, nothing asserted about the play until Shayne has answered each
/// (answered: the trial's ping at their end step, the one test here that isn't explicit).
/// Shayne, 2026-11-16: "the AI would use abilities as soon as possible, leading them to use their tappers during their
/// own upkeep (for no value) and pingers would tap asap (not keeping them up for blocks). In this case, the time limit
/// is before the end of the opponents turn". To read them:
/// <c>dotnet test MagicCore.Tests --filter "TestCategory=AbilityTiming" --logger "console;verbosity=detailed"</c>.
/// </summary>
public class AbilityTimingTests
{
	GameState s = null!;
	int me;
	int them;

	[SetUp]
	public void OurFirstMain_EmptyHands()
	{
		s = TestGames.EmptyHands(TestGames.InFirstMain());
		me = s.GetGame().StartingPlayerId;
		them = s.OpponentOf(me);
	}

	/// <summary>The same empty game, a step earlier: our upkeep, where an ability used "as soon as possible" would be used.</summary>
	void FromOurUpkeep()
	{
		s = TestGames.EmptyHands(TestGames.Started());
		Assert.That(s.GetGame().Step, Is.EqualTo(Step.Upkeep));
		me = s.GetGame().StartingPlayerId;
		them = s.OpponentOf(me);
		TestContext.Out.WriteLine("--- from our upkeep ---");
	}

	int Put(int player, CardDefinition card, ZoneType zone = ZoneType.Battlefield)
	{
		(s, var id) = TestGames.Put(s, player, card, zone);
		return id;
	}

	void Many(int n, int player, CardDefinition card, ZoneType zone = ZoneType.Battlefield)
	{
		for (var i = 0; i < n; i++)
			Put(player, card, zone);
	}

	/// <summary>A creature with "{T}: [effect on one target]".</summary>
	static CardDefinition WithTapAbility(
		string name,
		string body,
		TargetSpec target,
		GameAction effect
	) =>
		new(
			TestGames.Creature(name, body).Data,
			[
				new ActivatedAbilityComponent(ManaCost.None, [effect])
				{
					Costs = [new TapSelfCost()],
					Targets = [target],
				},
			]
		);

	/// <summary>"{T}: Tap target creature."</summary>
	static readonly CardDefinition Tapper = WithTapAbility(
		"Tapper",
		"1/1",
		TargetSpec.Creature,
		new Tap(Ref.Target())
	);

	/// <summary>"{T}: This creature deals 1 damage to any target."</summary>
	static readonly CardDefinition Pinger = WithTapAbility(
		"Pinger",
		"1/1",
		TargetSpec.AnyTarget(),
		new DealDamage(Ref.Target(), 1)
	);

	/// <summary>
	/// Our turn and theirs played out — our seat by <paramref name="ours"/> (a --match name), theirs by the default
	/// search: every decision but a bare pass, with its turn and step.
	/// </summary>
	List<string> TwoTurns(string ours)
	{
		TestContext.Out.WriteLine($"--- we play {ours} ---");
		var (x, start) = (s, s.GetGame().TurnNumber);
		var ais = new Dictionary<int, IAi>
		{
			[me] = AiMatch.Create(ours, me, 1, searchThreads: 1),
			[them] = AiMatch.Create("beam", them, 1, searchThreads: 1),
		};
		var log = new List<string>();
		for (var i = 0; ; i++)
		{
			Assert.That(i, Is.LessThan(400), "the two turns never ended");
			x = x.AutoPass();
			var game = x.GetGame();
			if (x.IsOver() || game.TurnNumber >= start + 2)
				break;
			var who = x.DecidingPlayerId();
			var decision = ais[who].Decide(x);
			if (decision.Action is not PassPriorityAction)
				log.Add(
					$"{(game.TurnNumber == start ? "our turn" : "their turn")}, {game.Step}: {(who == me ? "WE" : "they")} {decision.What}"
				);
			x = decision.State;
		}
		log.Add($"after both turns: we {x.GetPlayer(me).Life}, they {x.GetPlayer(them).Life}");
		foreach (var line in log)
			TestContext.Out.WriteLine(line);
		return log;
	}

	/// <summary>Today's search, the AI-26.5 trial, and the trial without AI-13.12's tie-break (WaitOnTies) it is meant to replace.</summary>
	static readonly string[] Searches = ["beam", "beam+lastchance", "beam+lastchance+actnow"];

	/// <summary>From our first main phase and from our upkeep, under each of <see cref="Searches"/>.</summary>
	static readonly object[] Both =
	[
		.. from upkeep in new[] { false, true }
		from ours in Searches
		select new object[] { upkeep, ours },
	];

	// We: 20 life, three lands, the Tapper 1/1 and a vanilla 2/2. They: 20 life, three lands, a vanilla 3/3 and a
	// vanilla 2/2, three unseen cards. Nothing in either hand to cast.
	[TestCaseSource(nameof(Both))]
	[Explicit("a position to read"), Category("AbilityTiming")]
	public void ATapper_FacingTwoAttackers_Printed(bool fromUpkeep, string ours)
	{
		if (fromUpkeep)
			FromOurUpkeep();
		Put(me, Tapper);
		Put(me, TestGames.Creature("Bear", "2/2"));
		Many(3, me, TestGames.Card("Forest"));
		Put(them, TestGames.Creature("Ogre", "3/3"));
		Put(them, TestGames.Creature("Wolf", "2/2"));
		Many(3, them, TestGames.Card("Forest"));
		Many(3, them, TestGames.Card("Forest"), ZoneType.Hand);
		Assert.That(TwoTurns(ours), Is.Not.Empty);
	}

	// We: 20 life, three lands, the Pinger 1/1 and a vanilla 2/2. They: 20 life, three lands, a vanilla 3/3, three
	// unseen cards.
	[TestCaseSource(nameof(Searches))]
	[Explicit("a position to read"), Category("AbilityTiming")]
	public void APinger_FacingAThreeThree_Printed(string ours)
	{
		Put(me, Pinger);
		Put(me, TestGames.Creature("Bear", "2/2"));
		Many(3, me, TestGames.Card("Forest"));
		Put(them, TestGames.Creature("Ogre", "3/3"));
		Many(3, them, TestGames.Card("Forest"));
		Many(3, them, TestGames.Card("Forest"), ZoneType.Hand);
		Assert.That(TwoTurns(ours), Is.Not.Empty);
	}

	// The same Pinger with nothing it can kill: alone against a vanilla 2/3, nothing else on the battlefields or in
	// hand (the libraries are the filler deck — what they draw is printed if cast). From our
	// first main phase, and from our upkeep (where "as soon as possible" would show).
	[TestCaseSource(nameof(Both))]
	[Explicit("a position to read"), Category("AbilityTiming")]
	public void APinger_WithNothingToKill_Printed(bool fromUpkeep, string ours)
	{
		NothingToKill(fromUpkeep);
		Assert.That(TwoTurns(ours), Is.Not.Empty);
	}

	/// <summary>
	/// The AI-26.5 trial's play there, which Shayne called right (2026-11-16: "Yeah those plays are correct"): the Pinger
	/// kept up through their turn, their face pinged once, at their end step.
	/// </summary>
	[TestCase(false)]
	[TestCase(true)]
	public void APinger_WithNothingToKill_PingsAtTheirEndStep_UnderTheTrial(bool fromUpkeep)
	{
		NothingToKill(fromUpkeep);
		var pings = TwoTurns("beam+lastchance").Where(l => l.Contains("Activate Pinger")).ToList();
		Assert.That(pings, Has.Count.EqualTo(1));
		Assert.That(pings[0], Does.StartWith("their turn, End: WE Activate Pinger"));
	}

	void NothingToKill(bool fromUpkeep)
	{
		if (fromUpkeep)
			FromOurUpkeep();
		Put(me, Pinger);
		Many(3, me, TestGames.Card("Forest"));
		Put(them, TestGames.Creature("Guard", "2/3"));
		Many(3, them, TestGames.Card("Forest"));
		Many(3, them, TestGames.Card("Forest"), ZoneType.Hand);
	}

	// AI-30.4f (Shayne, 2026-11-21: "planing usually starts after you draw your card for the turn"): with nothing on the
	// stack our upkeep passes unplanned - a Pinger in reach and a 1/1 of theirs to ping; without the setting it plans the
	// whole turn there. Measured before it: 308 such searches that passed were 40.6% of every engine step.
	[TestCase(true)]
	[TestCase(false)]
	public void OurUpkeep_PassesUnplanned_AfterTheDraw(bool afterTheDraw)
	{
		FromOurUpkeep();
		Put(me, Pinger);
		Put(them, TestGames.Creature("Mouse", "1/1"));
		Assert.That(
			s.DecidingPlayerId(),
			Is.EqualTo(me),
			"precondition: our priority in our upkeep"
		);
		var (decided, steps) = MagicRules.CountingSteps(
			() => new BeamSearchAi(me, new(AfterTheDraw: afterTheDraw)).Decide(s)
		);
		if (afterTheDraw)
		{
			Assert.That(decided.Action, Is.TypeOf<PassPriorityAction>());
			Assert.That(steps, Is.EqualTo(1), "no search - the one step is the pass itself");
		}
		else
			Assert.That(steps, Is.GreaterThan(100), "control: the turn is searched in the upkeep");
	}
}
