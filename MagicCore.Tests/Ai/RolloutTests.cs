using System.Diagnostics;
using ImmutableGameObjects;
using MagicCore.Ai;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Sets.Fra;
using MagicCore.Targeting;

namespace MagicCore.Tests.Ai;

public class RolloutTests
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

	double Score(GameState state) =>
		Rollout.Score(state, me, Rollout.Horizon(state, Rollout.Default), Rollout.Default);

	GameState WithLife(GameState state, int player, int life) =>
		state.WithPlayer(state.GetPlayer(player) with { Life = life });

	[Test]
	public void ItAttacksForLethal_OnBoard()
	{
		(s, _) = TestGames.Put(s, me, TestGames.Creature("Ours", "3/3"), ZoneType.Battlefield);
		s = WithLife(s, them, 3);

		Assert.That(Score(s), Is.GreaterThan(5_000));
	}

	// BoardOnly: the opponent attacks with its board — a rollout that never let it would see no danger.
	[Test]
	public void ItSeesTheOpponentAttackForLethal()
	{
		(s, _) = TestGames.Put(s, them, TestGames.Creature("Theirs", "4/4"), ZoneType.Battlefield);
		s = WithLife(s, me, 3);

		Assert.That(Score(s), Is.LessThan(-5_000));
	}

	// the whole hand, as beam+casts — the default plays only the cards we know (AI-10.4)
	static readonly Rollout.Settings Casts = Rollout.Default with
	{
		Opponent = Rollout.OpponentModel.CastsFromHand,
		KnownOnly = false,
	};

	// CastsFromHand: the opponent's creature in hand arrives inside the rollout — compared with the
	// same position without it, so cards it draws can't pass for the one in its hand.
	[Test]
	public void ACastingOpponent_CastsFromItsHand_InTheRollout()
	{
		(s, _) = TestGames.Put(s, them, TestGames.Card("Forest"), ZoneType.Battlefield);
		var (withIt, _) = TestGames.Put(
			s,
			them,
			TestGames.Creature("Theirs", "4/4"),
			ZoneType.Hand
		);

		var casting = Rollout.Score(withIt, me, Rollout.Horizon(s, Casts), Casts);
		var without = Rollout.Score(s, me, Rollout.Horizon(s, Casts), Casts);

		Assert.That(casting, Is.LessThan(without - 2));
	}

	// Its turn, played by OpponentStep: the land, ONE spell (ActionsPerTurn) though it could pay for
	// two, and the planeswalker's loyalty ability after the spell is spent.
	[Test]
	public void ACastingOpponent_CastsOneSpell_AndUsesItsPlaneswalker()
	{
		var walker = TheirsWith(out var after, Casts);

		Assert.That(after.GetPlayer(them).ThisTurn.SpellsCast, Is.EqualTo(1));
		Assert.That(
			after.CardsIn(them, ZoneType.Battlefield).Count(c => c.Name == "Theirs"),
			Is.EqualTo(1)
		);
		Assert.That(
			after.GetCard(walker).LoyaltyActivatedOnTurn,
			Is.EqualTo(after.GetGame().TurnNumber)
		);
	}

	static readonly Rollout.Settings Responds = Rollout.Default with
	{
		Opponent = Rollout.OpponentModel.Responds,
		KnownOnly = false,
	};

	static readonly CardDefinition Kill =
		new(
			TestGames.Inline("Kill", "{1}", "Instant").Data,
			[
				new SpellEffectsComponent([new Destroy(Ref.Target())])
				{
					Targets = [TargetSpec.Creature],
				},
			]
		);

	// Our creature spell on the stack, and they hold an instant kill spell with the mana for it.
	GameState OurSpellOnTheStack(bool ourCreatureInPlay)
	{
		if (ourCreatureInPlay)
			(s, _) = TestGames.Put(s, me, TestGames.Creature("Ours", "3/3"), ZoneType.Battlefield);
		else
			(s, _) = TestGames.Put(
				s,
				them,
				TestGames.Creature("Theirs", "3/3"),
				ZoneType.Battlefield
			);
		(s, _) = TestGames.Put(s, me, TestGames.Card("Forest"), ZoneType.Battlefield);
		(s, _) = TestGames.Put(s, them, TestGames.Card("Forest"), ZoneType.Battlefield);
		(s, _) = TestGames.Put(s, them, Kill, ZoneType.Hand);
		int spell;
		(s, spell) = TestGames.Put(s, me, TestGames.Creature("Next", "1/1"), ZoneType.Hand);
		s = s.Act(new Actions.CastSpellAction { PlayerId = me, CardId = spell });
		s = s.Act(new Actions.PassPriorityAction { PlayerId = me });
		Assert.That(s.DecidingPlayerId(), Is.EqualTo(them));
		return s;
	}

	// Responds (AI-3.6): our spell on the stack is a window — it kills our creature in response, which
	// beats letting it be. CastsFromHand plays at sorcery speed only, so it lets our spell resolve.
	[Test]
	public void ARespondingOpponent_KillsOurCreature_InResponseToOurSpell()
	{
		var before = OurSpellOnTheStack(ourCreatureInPlay: true);

		var responding = Rollout.OpponentStep(before, them, Responds);
		var casting = Rollout.OpponentStep(before, them, Casts);

		Assert.That(Stops.TopOfStackControllerId(responding), Is.EqualTo(them));
		Assert.That(responding.GetPlayer(them).ThisTurn.SpellsCast, Is.EqualTo(1));
		Assert.That(casting.GetPlayer(them).ThisTurn.SpellsCast, Is.Zero);
	}

	// A response must beat passing: killing only its own creature would not, so it passes.
	[Test]
	public void ARespondingOpponent_Passes_WhenNoResponseBeatsPassing()
	{
		var before = OurSpellOnTheStack(ourCreatureInPlay: false);

		var after = Rollout.OpponentStep(before, them, Responds);

		Assert.That(after.GetPlayer(them).ThisTurn.SpellsCast, Is.Zero);
		Assert.That(after.CardsIn(them, ZoneType.Battlefield).Any(c => c.Name == "Theirs"));
	}

	// Our combat is a window only when something attacks: after we declare an attack it kills the
	// attacker; with no attack declared it holds its kill (a combat with no attack is no window).
	[TestCase(true, 1)]
	[TestCase(false, 0)]
	public void ARespondingOpponent_AnswersOurCombat_OnlyWhenWeAttack(bool attack, int spellsCast)
	{
		(s, _) = TestGames.Put(s, me, TestGames.Creature("Ours", "3/3"), ZoneType.Battlefield);
		(s, _) = TestGames.Put(s, them, TestGames.Card("Forest"), ZoneType.Battlefield);
		(s, _) = TestGames.Put(s, them, Kill, ZoneType.Hand);
		for (
			var i = 0;
			i < 50 && !(s.GetGame().Step == Step.DeclareAttackers && s.DecidingPlayerId() == them);
			i++
		)
			s = s.GetPendingChoice() is Actions.DeclareAttackerChoice choice
				? s.Choose(
					[
						attack
							? choice.Options.First(o => o.Id != Actions.CombatOption.None).Id
							: Actions.CombatOption.None,
					]
				)
				: s.Act(new Actions.PassPriorityAction { PlayerId = s.DecidingPlayerId() });
		Assert.That(s.GetGame().Step, Is.EqualTo(Step.DeclareAttackers));

		var after = Rollout.OpponentStep(s, them, Responds);

		Assert.That(after.GetPlayer(them).ThisTurn.SpellsCast, Is.EqualTo(spellsCast));
	}

	// Greedy (AI-3.7): GreedyAi plays our side — every play that beats passing, not one a turn. Two
	// creatures and the mana for both, scored at the end of this turn: the cheap policy casts one.
	[Test]
	public void AGreedyPolicy_PlaysEveryPlayThatBeatsPassing_NotOneATurn()
	{
		(s, _) = TestGames.Put(s, me, TestGames.Card("Forest"), ZoneType.Battlefield);
		(s, _) = TestGames.Put(s, me, TestGames.Card("Forest"), ZoneType.Battlefield);
		(s, _) = TestGames.Put(s, me, TestGames.Creature("A", "3/3"), ZoneType.Hand);
		(s, _) = TestGames.Put(s, me, TestGames.Creature("B", "3/3"), ZoneType.Hand);
		var thisTurn = Rollout.Default with { LookaheadTurns = 0 };
		double Scored(Rollout.Settings x) => Rollout.Score(s, me, Rollout.Horizon(s, x), x);

		Assert.That(
			Scored(thisTurn with { Ours = Rollout.OurPolicy.Greedy }),
			Is.GreaterThan(Scored(thisTurn))
		);
	}

	// AllInToo (AI-3.8): two 3/3s into their 4/4 at 3 life. Alone, each would be blocked and die, so
	// neither attacks; together one gets through for exactly 3. Scored at the end of this turn.
	[Test]
	public void AllIn_FindsTheAttackThatOnlyWorksTogether()
	{
		(s, _) = TestGames.Put(s, me, TestGames.Creature("A", "3/3"), ZoneType.Battlefield);
		(s, _) = TestGames.Put(s, me, TestGames.Creature("B", "3/3"), ZoneType.Battlefield);
		(s, _) = TestGames.Put(s, them, TestGames.Creature("Wall", "4/4"), ZoneType.Battlefield);
		s = WithLife(s, them, 3);
		var thisTurn = Rollout.Default with { LookaheadTurns = 0 };
		double Scored(Rollout.Settings x) => Rollout.Score(s, me, Rollout.Horizon(s, x), x);

		Assert.That(Scored(thisTurn), Is.LessThan(5_000));
		Assert.That(
			Scored(thisTurn with { Attacks = Rollout.Attacks.AllInToo }),
			Is.GreaterThan(5_000)
		);
	}

	// OwnersBest (AI-3.8): our creature's "enters, 3 damage to target creature" — the first legal target
	// is not theirs; judged by its owner, the trigger kills theirs.
	[Test]
	public void OwnersBest_AimsATrigger_AtWhatIsBestForItsOwner()
	{
		(s, _) = TestGames.Put(s, me, TestGames.Creature("Ours", "2/2"), ZoneType.Battlefield);
		(s, _) = TestGames.Put(s, them, TestGames.Creature("Theirs", "2/2"), ZoneType.Battlefield);
		(s, _) = TestGames.Put(s, me, TestGames.Card("Forest"), ZoneType.Battlefield);
		var pinger = TestGames.Creature("Pinger", "1/1") with
		{
			Components =
			[
				new TriggeredAbilityComponent(
					TriggerEvent.SelfEnters,
					[new DealDamage(Ref.Target(), 3)]
				)
				{
					Targets = [TargetSpec.Creature],
				},
			],
		};
		int card;
		(s, card) = TestGames.Put(s, me, pinger, ZoneType.Hand);
		s = s.Act(new Actions.CastSpellAction { PlayerId = me, CardId = card });
		s = TestGames.PassUntil(s, x => x.GetPendingChoice() is Actions.ChooseTargetChoice);
		var first = Lookahead.QuickAnswer(s, s.GetPendingChoice()!).Single();
		Assert.That(s.GetCard(first).Name, Is.Not.EqualTo("Theirs")); // what the cheap answer gets wrong
		var thisTurn = Rollout.Default with { LookaheadTurns = 0 };
		double Scored(Rollout.Settings x) => Rollout.Score(s, me, Rollout.Horizon(s, x), x);

		Assert.That(
			Scored(thisTurn with { Targets = Rollout.Targets.OwnersBest }),
			Is.GreaterThan(Scored(thisTurn) + 2)
		);
	}

	// Its planeswalkers are on its board, so they act (AI-8.1) — what a walker would do is what makes it
	// worth attacking; with TheirWalkers off (before AI-8.1) the board-only opponent never touched one.
	// The default (AI-10.4) casts nothing from a hand we don't know either: it plays only the cards we know.
	[TestCase(true, true)]
	[TestCase(false, true)]
	[TestCase(true, false)]
	[TestCase(false, false)]
	public void BoardOnly_NeverCasts_ButItsWalkersAct(bool theirWalkers, bool boardOnly)
	{
		var walker = TheirsWith(
			out var after,
			(
				boardOnly
					? Rollout.Default with
					{
						Opponent = Rollout.OpponentModel.BoardOnly,
					}
					: Rollout.Default
			) with
			{
				TheirWalkers = theirWalkers,
			}
		);

		Assert.That(after.GetPlayer(them).ThisTurn.SpellsCast, Is.Zero);
		Assert.That(after.GetPlayer(them).ThisTurn.LandsPlayed, Is.Zero);
		// (a casting model, the default among them, uses a walker by our policy when TheirWalkers is off)
		if (boardOnly)
			Assert.That(
				after.GetCard(walker).LoyaltyActivatedOnTurn != 0,
				Is.EqualTo(theirWalkers)
			);
	}

	int KnownTheirs;

	// AI-10.4 (Shayne): "board only, except for cards we know about" — of its two 4/4s in hand, the
	// default opponent casts the one we know in its own turn, and never the other. They share a name: the
	// filter comes before DistinctMoves, which keeps one move per name and would keep the unknown one.
	[Test]
	public void TheDefaultOpponent_CastsOnlyTheCardWeKnow()
	{
		TheirsWith(out var after, Rollout.Default, knowTheSecond: true);

		Assert.That(after.GetPlayer(them).ThisTurn.SpellsCast, Is.EqualTo(1));
		Assert.That(after.IsOnBattlefield(KnownTheirs), "the one we know");
		Assert.That(after.GetPlayer(them).ThisTurn.LandsPlayed, Is.Zero, "its land isn't known");
	}

	/// <summary>
	/// The opponent with two 4/4s in hand, mana for both and a planeswalker, through its next turn by
	/// <paramref name="settings"/>' model — <paramref name="after"/> is its turn's last state, before
	/// the tally resets. Returns the planeswalker's id.
	/// </summary>
	/// <param name="knowTheSecond">The second 4/4 is one we know (AI-10.4) — the first, the same name, isn't.</param>
	int TheirsWith(out GameState after, Rollout.Settings settings, bool knowTheSecond = false)
	{
		for (var i = 0; i < 2; i++)
		{
			(s, _) = TestGames.Put(s, them, TestGames.Card("Forest"), ZoneType.Battlefield);
			(s, var theirs) = TestGames.Put(
				s,
				them,
				TestGames.Creature("Theirs", "4/4"),
				ZoneType.Hand
			);
			if (knowTheSecond && i == 1)
			{
				s = s.UpdateObject(theirs, s.GetCard(theirs) with { KnownTo = Knowers.Everyone }); // TEST SETUP
				KnownTheirs = theirs;
			}
		}
		(s, _) = TestGames.Put(s, them, TestGames.Card("Forest"), ZoneType.Hand);
		(s, var walker) = TestGames.Put(s, them, Walker, ZoneType.Hand);
		s = s.EnterBattlefield(walker, them); // at its printed loyalty: Put's 0 would kill it

		var theirTurn = s.GetGame().TurnNumber + 1;
		after = TestGames.PassUntil(s, x => x.GetGame().TurnNumber == theirTurn);
		for (var i = 0; i < 1_000; i++)
		{
			var next =
				after.DecidingPlayerId() == them
					? Rollout.OpponentStep(after, them, settings)
					: TestGames.PassOrChoose(after);
			if (next.GetGame().TurnNumber != theirTurn)
				return walker;
			after = next;
		}
		Assert.Fail("their turn never ended");
		return walker;
	}

	static readonly CardDefinition Walker = TestGames.Planeswalker(
		"Walker",
		3,
		new LoyaltyAbilityComponent(1, [new GainLife(Ref.You, 1)])
	);

	// Blocks are judged by what they lead to: facing lethal, a chump block keeps us alive.
	[Test]
	public void FacingLethal_WeChumpBlock()
	{
		(s, _) = TestGames.Put(s, me, TestGames.Creature("Chump", "1/1"), ZoneType.Battlefield);
		(s, _) = TestGames.Put(s, them, TestGames.Creature("Theirs", "4/4"), ZoneType.Battlefield);
		s = WithLife(s, me, 3);

		Assert.That(Score(s), Is.GreaterThan(-5_000));
	}

	// The discount counts from the SEARCH's root, which the horizon fixes — so two candidates that lose
	// on the same turn score the same, though one of them had already passed into the next turn.
	[Test]
	public void ALossOnTheSameTurn_ScoresTheSame_WhereverTheRolloutStarted()
	{
		(s, _) = TestGames.Put(s, them, TestGames.Creature("Theirs", "4/4"), ZoneType.Battlefield);
		s = WithLife(s, me, 3);
		var horizon = Rollout.Horizon(s, Rollout.Default);
		var thisTurn = s.GetGame().TurnNumber;
		var theirTurn = TestGames.PassUntil(s, x => x.GetGame().TurnNumber > thisTurn);

		var fromHere = Rollout.Score(s, me, horizon, Rollout.Default);
		var fromTheirTurn = Rollout.Score(theirTurn, me, horizon, Rollout.Default);

		Assert.That(fromHere, Is.LessThan(-5_000));
		Assert.That(fromTheirTurn, Is.EqualTo(fromHere));
	}

	// A sooner win scores more (the terminal discount), and any win far more than any board.
	[Test]
	public void ASoonerWin_ScoresHigher()
	{
		(s, _) = TestGames.Put(s, me, TestGames.Creature("Ours", "3/3"), ZoneType.Battlefield);

		var now = Score(WithLife(s, them, 3));
		var nextTurn = Score(WithLife(s, them, 6));

		Assert.That(now, Is.GreaterThan(nextTurn));
		Assert.That(nextTurn, Is.GreaterThan(5_000));
	}

	[Test]
	public void TheSamePosition_ScoresTheSame()
	{
		(s, _) = TestGames.Put(s, me, TestGames.Creature("Ours", "3/3"), ZoneType.Battlefield);
		(s, _) = TestGames.Put(s, them, TestGames.Creature("Theirs", "2/4"), ZoneType.Battlefield);

		Assert.That(Score(s), Is.EqualTo(Score(s)));
	}

	// What one rollout costs — the unit the search's budget is counted in. Measured at every one of
	// GreedyAi's decisions in real games; recorded in docs/findings/magic-ai.md.
	[Test, Explicit("measurement: prints the cost of a rollout")]
	public void Cost_OfOneRollout()
	{
		var times = new List<double>();
		var decks = FraDecks.All.Keys.ToList();
		foreach (
			var (first, second) in decks.SelectMany(a =>
				decks.Where(b => b != a).Select(b => (a, b))
			)
		)
		{
			var g = MagicRules.NewGame(FraDecks.All[first], FraDecks.All[second], seed: 1);
			var ais = new[] { g.Player1Id(), g.Player2Id() }.ToDictionary(
				p => p,
				p => new GreedyAi(p)
			);
			for (var i = 0; i < 5_000 && !g.IsOver(); i++)
			{
				g = g.AutoPass();
				if (g.IsOver())
					break;
				var who = g.DecidingPlayerId();
				if (g.GetGame().Step != Step.PreGame && !g.IsWaitingForChoice)
				{
					var start = Stopwatch.GetTimestamp();
					Rollout.Score(g, who, Rollout.Horizon(g, Rollout.Default), Rollout.Default);
					times.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
				}
				g = ais[who].Decide(g).State;
			}
		}
		times.Sort();
		double At(double q) => times[(int)(q * (times.Count - 1))];
		TestContext.Out.WriteLine(
			$"{times.Count} rollouts: mean {times.Average():0.0} ms, p50 {At(0.5):0.0}, p90 {At(0.9):0.0}, "
				+ $"p99 {At(0.99):0.0}, max {times[^1]:0.0} ms"
		);
	}

	// AI-8.3 (Shayne's sealed game): the look-ahead's "blocks only what it survives" counted a double
	// striker's damage once — Yuriko's 4/3 deals 4 twice — so a 7/5 blocked it in every look-ahead
	// and died, and the search kept the 4/3 alive to win a trade the opponent would never make. The
	// blocker here is a 7/5, with the keyword and the damage marked on it that each case names.
	[TestCase("4/3", "", "", 0, false, ExpectedResult = true)]
	[TestCase("4/3", "Double strike", "", 0, false, ExpectedResult = false)]
	[TestCase("1/1", "Deathtouch", "", 0, false, ExpectedResult = false)]
	[TestCase("3/3", "", "", 3, false, ExpectedResult = false)] // 3 more on 2 left
	// first strike kills the 4/3 before its second hit: 4 taken, 5 toughness
	[TestCase("4/3", "Double strike", "First strike", 0, false, ExpectedResult = true)]
	[TestCase("4/3", "Double strike", "", 0, true, ExpectedResult = true)] // +oldblocks: the old check
	public bool ALookAheadBlocker_BlocksOnlyWhatItSurvives(
		string attackerPt,
		string attackerKeyword,
		string blockerKeyword,
		int blockerDamage,
		bool oldBlocks
	)
	{
		var s = TestGames.InFirstMain();
		var me = s.GetGame().StartingPlayerId;
		var them = s.OpponentOf(me);
		string[] Keywords(string k) => k == "" ? [] : [k];
		(s, var attacker) = TestGames.Put(
			s,
			me,
			TestGames.Creature("Attacker", attackerPt, Keywords(attackerKeyword)),
			ZoneType.Battlefield
		);
		(s, var blocker) = TestGames.Put(
			s,
			them,
			TestGames.Creature("Blocker", "7/5", Keywords(blockerKeyword)),
			ZoneType.Battlefield
		);
		s = s.UpdateObject(blocker, s.GetCard(blocker) with { Damage = blockerDamage }); // TEST SETUP
		s = TestGames.PassUntil(s, x => x.IsWaitingForChoice);
		s = s.Choose([them]);
		s = TestGames.PassUntil(s, x => x.GetPendingChoice() is Actions.DeclareBlockerChoice);

		var answer = Evaluator.Without(
			oldBlocks ? Evaluator.Terms.BlockSurvival : Evaluator.Terms.None,
			() => Lookahead.QuickAnswer(s, s.GetPendingChoice()!).Single()
		);
		return answer == attacker;
	}

	// AI-15 (Shayne's replay, flag 4): the look-ahead's blockers were judged one at a time, each blocking
	// only what it survives, so no second blocker ever joined a block — and a menace attacker blocked
	// by one is unblocked. The second 2/2 now joins the first on an attacker the two kill together, or
	// on a menace attacker; never on one the first already kills, nor on one the two can't.
	[TestCase("3/3", "Menace", false, ExpectedResult = true)]
	[TestCase("3/3", "", false, ExpectedResult = true)] // 2 + 2 kill it
	[TestCase("2/1", "", false, ExpectedResult = false)] // the first already kills it (and a 2/2 blocking it alone dies)
	[TestCase("5/5", "", false, ExpectedResult = false)] // 2 + 2 don't
	[TestCase("5/5", "Menace", false, ExpectedResult = false)] // menace, but a chump: 2 + 2 don't kill it
	[TestCase("3/3", "Menace", true, ExpectedResult = false)] // +nogang: never joins
	public bool ALookAheadBlocker_JoinsABlock_TheTwoMake(
		string attackerPt,
		string attackerKeyword,
		bool noGang
	)
	{
		var s = TestGames.InFirstMain();
		var me = s.GetGame().StartingPlayerId;
		var them = s.OpponentOf(me);
		(s, var attacker) = TestGames.Put(
			s,
			me,
			TestGames.Creature(
				"Attacker",
				attackerPt,
				attackerKeyword == "" ? [] : [attackerKeyword]
			),
			ZoneType.Battlefield
		);
		for (var i = 0; i < 2; i++)
			(s, _) = TestGames.Put(
				s,
				them,
				TestGames.Creature("Blocker", "2/2"),
				ZoneType.Battlefield
			);
		s = TestGames.PassUntil(s, x => x.IsWaitingForChoice);
		s = s.Choose([them]);
		s = TestGames.PassUntil(s, x => x.GetPendingChoice() is Actions.DeclareBlockerChoice);
		s = s.Choose([attacker]); // the first blocker blocks it
		Assert.That(
			s.GetPendingChoice(),
			Is.InstanceOf<Actions.DeclareBlockerChoice>(),
			"the second blocker's choice"
		);

		var answer = Evaluator.Without(
			noGang ? Evaluator.Terms.GangBlocks : Evaluator.Terms.None,
			() => Lookahead.QuickAnswer(s, s.GetPendingChoice()!).Single()
		);
		return answer == attacker;
	}
}
