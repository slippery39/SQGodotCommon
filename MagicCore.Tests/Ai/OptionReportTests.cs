using ImmutableGameObjects;
using MagicCore.Ai;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Sets;
using MagicCore.Targeting;

namespace MagicCore.Tests.Ai;

/// <summary>
/// The option report (story AI-26.2) on hand-built positions, each printed for Shayne to read against his own
/// judgment — nothing in the AI reads the report yet, so these assert its arithmetic, never a play. To read them:
/// <c>dotnet test MagicCore.Tests --filter "TestCategory=OptionReport" --logger "console;verbosity=detailed"</c>
/// (explicit: they take ~15 s and assert only the report's arithmetic).
/// </summary>
public class OptionReportTests
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

	static CardDefinition Spell(
		string name,
		string cost,
		string type,
		TargetSpec target,
		GameAction effect
	) =>
		new(
			TestGames.Inline(name, cost, type).Data,
			[new SpellEffectsComponent([effect]) { Targets = [target] }]
		);

	/// <summary>Shayne's example: "lightning bolting a vanilla 1/1 at 20 life".</summary>
	static readonly CardDefinition Bolt = Spell(
		"Bolt",
		"{R}",
		"Instant",
		TargetSpec.AnyTarget(),
		new DealDamage(Ref.Target(), 3)
	);

	static readonly CardDefinition Kill = Spell(
		"Kill",
		"{2}{B}",
		"Sorcery",
		TargetSpec.Creature,
		new Destroy(Ref.Target())
	);

	string Report()
	{
		var report = OptionReport.Of(s, me);
		TestContext.Out.WriteLine(report);
		return report;
	}

	/// <summary>The first number after a colon on the report's first line that starts with <paramref name="label"/>.</summary>
	static double Read(string report, string label) =>
		double.Parse(
			System
				.Text.RegularExpressions.Regex.Match(
					report.Split('\n').First(l => l.StartsWith(label)),
					@": (-?[\d.]+)"
				)
				.Groups[1]
				.Value,
			System.Globalization.CultureInfo.InvariantCulture
		);

	[Test]
	public void TheSpareAnswer_IsWorthTheNthBestTarget()
	{
		// three alike answers against targets worth 9, 5 and 2: the third answer adds the third-best target
		double[] targets = [9, 5, 2];
		var three = OptionPrice.Assign([targets, targets, targets], [0, 0, 0]);
		var two = OptionPrice.Assign([targets, targets], [0, 0]);
		Assert.That(three - two, Is.EqualTo(targets[2]));
		// an answer with no target worth more takes its floor (burn at the face)
		Assert.That(
			OptionPrice.Assign(
				[
					[1.0],
				],
				[3.0]
			),
			Is.EqualTo(3.0)
		);
	}

	// Life 20 each; they have a vanilla 1/1, three lands and four unseen cards; we have three Mountains and one or
	// three "Bolt" ({R} instant: 3 damage to any target).
	[Test, Explicit("a report to read, ~15 s"), Category("OptionReport")]
	public void ABoltOnAVanillaOneOne_IsHeldDearer_AsTheOnlyAnswerThanAsOneOfThree()
	{
		Put(them, TestGames.Creature("Scrub", "1/1"));
		Many(3, them, TestGames.Card("Forest"));
		Many(4, them, TestGames.Card("Forest"), ZoneType.Hand);
		Many(3, me, TestGames.Card("Mountain"));
		Put(me, Bolt, ZoneType.Hand);
		TestContext.Out.WriteLine("===== one Bolt =====");
		var one = Read(Report(), "Held, in this hand");
		Many(2, me, Bolt, ZoneType.Hand);
		TestContext.Out.WriteLine("===== three Bolts =====");
		var three = Read(Report(), "Held, in this hand");
		Assert.That(three, Is.LessThan(one), "the spare answer is priced below the only one");
		// Shayne, 2026-11-16: no Bolt on the 1/1 with three in hand, "not even close". His rule (a vanilla 1/1 is 0.33
		// to 0.5 of a card) and eight turns priced hold it by 1.5; ten turns by 2.1 (findings, AI-26.2)
		TestContext.Out.WriteLine("===== three Bolts, 10 turns priced =====");
		TestContext.Out.WriteLine(OptionReport.Of(s, me, 10));
		Searches("three Bolts");
		// the two sides with no look-ahead: holding all three, against the Bolt spent on the 1/1
		var held = BeamSearchAi.DefaultRollout.Held;
		var bolt = s.LegalActions()
			.OfType<global::MagicCore.Actions.CastSpellAction>()
			.First(c => c.Targets.Any(id => s.HasObject(id) && s.IsOnBattlefield(id)));
		var after = Lookahead.Settle(s.Act(bolt));
		foreach (var on in new[] { false, true })
			OptionPrice.WithRoot(
				on,
				s,
				me,
				() =>
				{
					TestContext.Out.WriteLine(
						$"No look-ahead, option price {(on ? "on" : "off")}: hold {Evaluator.Evaluate(s, me, held):0.00}, Bolt the 1/1 {Evaluator.Evaluate(after, me, held):0.00}"
					);
					foreach (
						var (term, value) in Evaluator
							.Explain(s, me, held)
							.Zip(
								Evaluator.Explain(after, me, held),
								(a, b) => (a.Term, b.Value - a.Value)
							)
							.Where(x => x.Item2 != 0)
					)
						TestContext.Out.WriteLine($"    {term}: {value:+0.00;-0.00}");
					return 0;
				}
			);
	}

	// Shayne, 2026-11-16: with nothing but Bolts "I would be forced to lightning bolt the 1/1's because I would
	// eventually die. But i'm wondering if that logic changes if I have like a 3 mana 2/2 or 4 mana 3/3 in my hand that
	// would be able to potentially block those 1/1's". The three-Bolt position with that creature in hand too.
	[TestCase("{2}{R}", "2/2")]
	[TestCase("{3}{R}", "3/3")]
	[Explicit("a report to read"), Category("OptionReport")]
	public void ThreeBolts_AndABlockerInHand_Printed(string cost, string body)
	{
		Put(them, TestGames.Creature("Scrub", "1/1"));
		Many(3, them, TestGames.Card("Forest"));
		Many(4, them, TestGames.Card("Forest"), ZoneType.Hand);
		Many(3, me, TestGames.Card("Mountain"));
		Many(3, me, Bolt, ZoneType.Hand);
		Put(me, TestGames.CreatureCosting(cost, "Blocker", body), ZoneType.Hand);
		foreach (var (label, settings) in Searched)
		{
			var ai = new BeamSearchAi(me, settings);
			TestContext.Out.WriteLine(
				$"===== three Bolts and a {cost} {body}, {label}: {ai.Decide(s).What} ====="
			);
			// each first move's score, and the whole line the chosen one came from
			var blocks = ai.Explain().Split("\n## ");
			foreach (var block in blocks.Skip(1))
				TestContext.Out.WriteLine("## " + block.Split('\n')[0]);
			TestContext.Out.WriteLine("## " + blocks[1]);
		}
	}

	// After the suite's White-Black vs White-Blue Jace seed 2 move 47 (Shayne, 2026-11-16): a kill spell, and on their
	// side a 3/2 our 5/5 walls, a 2/1 and a 1/3 that makes the 2/1 unblockable. "on stats alone the 3/2 is the best
	// card. But the 1/3 that makes things unblockable is what allows the opponent to attack through … if we are just
	// pure racing, we might kill the 3/2 and attack. If we want to be more defensive, we kill the 1/3 and keep our guys
	// back to block … We should never destroy the 2/1 though." A guard on his "never", not a control: in the suite's
	// own position both trials did destroy the 2/1 before a creature we attack with stopped holding anything off
	// (findings, AI-26.14); in this hand-built one the rule before that fix took the 1/3, his defensive play.
	[Test]
	public void AKillSpell_NeverTakesTheirTwoOne_OverTheThreeTwoOrTheOneThreeThatFreesIt()
	{
		s = s.WithPlayer(s.GetPlayer(me) with { Life = 16 });
		s = s.WithPlayer(s.GetPlayer(them) with { Life = 18 });
		Put(me, CardRegistry.Get("Screeching Soulbreaker"));
		Put(me, TestGames.Creature("Wall of ours", "5/5"));
		Many(2, me, TestGames.Card("Swamp"));
		Put(me, CardRegistry.Get("Vraska's Final Mercy"), ZoneType.Hand);
		Put(them, CardRegistry.Get("Repurposed Enforcer"));
		var small = Put(them, CardRegistry.Get("Mindseeker Oculus"));
		var enabler = Put(them, CardRegistry.Get("Tetsuko Umezawa, Fugitive"));
		foreach (var id in new[] { small, enabler })
			s = s.UpdateObject(id, s.GetCard(id) with { IsTapped = true }); // TEST SETUP
		Many(5, them, TestGames.Card("Island"));
		Many(4, them, TestGames.Card("Island"), ZoneType.Hand);
		foreach (
			var (label, settings) in Searched.Append(("beam+byuse", new(Threads: 1, ByUse: true)))
		)
		{
			if (label.Contains("optionprice"))
				continue; // the option price samples 200 decks first (~10 s): the explicit tests print it
			var played = new BeamSearchAi(me, settings).Decide(s).What;
			TestContext.Out.WriteLine($"{label}: {played}");
			Assert.That(played, Does.Not.Contain("Mindseeker Oculus"), label);
		}
	}

	/// <summary>Today's search, with the option price (AI-26.4), and with their held-off creatures counting for less too (AI-26.12).</summary>
	static readonly (string Label, BeamSearchAi.Settings Settings)[] Searched =
	[
		("today", new(Threads: 1)),
		("beam+optionprice", new(Threads: 1, OptionPrice: true)),
		("beam+optionprice+byuse", new(Threads: 1, OptionPrice: true, ByUse: true)),
	];

	/// <summary>What each search plays here — each first move's score.</summary>
	void Searches(string what)
	{
		foreach (var (label, settings) in Searched)
		{
			var ai = new BeamSearchAi(me, settings);
			TestContext.Out.WriteLine($"The search, {label}, {what}: {ai.Decide(s).What}");
			foreach (var line in ai.Explain().Split('\n').Where(l => l.StartsWith("## ")))
				TestContext.Out.WriteLine(line);
		}
	}

	// Shayne's tempo swing (2026-11-16): "the opponent has 2 mid blockers and I can deal 7 damage to them if I remove
	// both of their threats. I might use 2 removal spells to do so … even better if I have follow up plays the next
	// turn". Hand-built, for him to confirm: they are at 14 with a 3/3 and a 2/4, five lands, four unseen cards; we have
	// a 4/4 and a 3/3, six Swamps, two "Kill" ({2}{B} sorcery: destroy target creature) and a 5-mana 5/5 for next turn.
	[Test, Explicit("a report to read, ~15 s"), Category("OptionReport")]
	public void TheTempoSwing_TwoRemovalSpellsForSevenDamage_Printed()
	{
		s = s.WithPlayer(s.GetPlayer(them) with { Life = 14 });
		Put(them, TestGames.Creature("Wall", "2/4"));
		Put(them, TestGames.Creature("Ogre", "3/3"));
		Many(5, them, TestGames.Card("Forest"));
		Many(4, them, TestGames.Card("Forest"), ZoneType.Hand);
		Put(me, TestGames.Creature("Brute", "4/4"));
		Put(me, TestGames.Creature("Hound", "3/3"));
		Many(6, me, TestGames.Card("Swamp"));
		Many(2, me, Kill, ZoneType.Hand);
		Put(me, TestGames.CreatureCosting("{4}{B}", "Giant", "5/5"), ZoneType.Hand);
		var report = Report();
		Searches("the tempo swing");
		Assert.That(report, Does.Contain("## Kill"));
	}
}
