using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Ai;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Targeting;

namespace MagicCore.Tests.Ai;

/// <summary>
/// When the search uses a free tap ability (story AI-26.5, AI-13's lesson 6) — a measurement first: hand-built
/// positions, the two turns played out and printed, nothing asserted about the play until Shayne has answered each.
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

	/// <summary>Our turn and theirs played out, the default search in both seats: every decision but a bare pass, with its turn and step.</summary>
	List<string> TwoTurns()
	{
		var (x, start) = (s, s.GetGame().TurnNumber);
		var ais = new Dictionary<int, IAi>
		{
			[me] = new BeamSearchAi(me, new(Threads: 1)),
			[them] = new BeamSearchAi(them, new(Threads: 1)),
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

	// We: 20 life, three lands, the Tapper 1/1 and a vanilla 2/2. They: 20 life, three lands, a vanilla 3/3 and a
	// vanilla 2/2, three unseen cards. Nothing in either hand to cast.
	[TestCase(false)]
	[TestCase(true)]
	[Explicit("a position to read"), Category("AbilityTiming")]
	public void ATapper_FacingTwoAttackers_Printed(bool fromUpkeep)
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
		Assert.That(TwoTurns(), Is.Not.Empty);
	}

	// We: 20 life, three lands, the Pinger 1/1 and a vanilla 2/2. They: 20 life, three lands, a vanilla 3/3, three
	// unseen cards.
	[Test, Explicit("a position to read"), Category("AbilityTiming")]
	public void APinger_FacingAThreeThree_Printed()
	{
		Put(me, Pinger);
		Put(me, TestGames.Creature("Bear", "2/2"));
		Many(3, me, TestGames.Card("Forest"));
		Put(them, TestGames.Creature("Ogre", "3/3"));
		Many(3, them, TestGames.Card("Forest"));
		Many(3, them, TestGames.Card("Forest"), ZoneType.Hand);
		Assert.That(TwoTurns(), Is.Not.Empty);
	}

	// The same Pinger with nothing it can kill: alone against a vanilla 2/3, nothing else on the battlefields or in
	// hand (the libraries are the filler deck — what they draw is printed if cast). From our
	// first main phase, and from our upkeep (where "as soon as possible" would show).
	[TestCase(false)]
	[TestCase(true)]
	[Explicit("a position to read"), Category("AbilityTiming")]
	public void APinger_WithNothingToKill_Printed(bool fromUpkeep)
	{
		if (fromUpkeep)
			FromOurUpkeep();
		Put(me, Pinger);
		Many(3, me, TestGames.Card("Forest"));
		Put(them, TestGames.Creature("Guard", "2/3"));
		Many(3, them, TestGames.Card("Forest"));
		Many(3, them, TestGames.Card("Forest"), ZoneType.Hand);
		Assert.That(TwoTurns(), Is.Not.Empty);
	}
}
