using System.Diagnostics;
using ImmutableGameObjects;
using MagicCore.Ai;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Sets;
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
	// neither attacks; together one gets through for exactly 3. Scored at the end of this turn. The
	// CONTROL: each attacker judged alone (the look-ahead's default before AI-17.6).
	[Test]
	public void AllIn_FindsTheAttackThatOnlyWorksTogether()
	{
		(s, _) = TestGames.Put(s, me, TestGames.Creature("A", "3/3"), ZoneType.Battlefield);
		(s, _) = TestGames.Put(s, me, TestGames.Creature("B", "3/3"), ZoneType.Battlefield);
		(s, _) = TestGames.Put(s, them, TestGames.Creature("Wall", "4/4"), ZoneType.Battlefield);
		s = WithLife(s, them, 3);
		var thisTurn = Rollout.Default with { LookaheadTurns = 0 };
		double Scored(Rollout.Settings x) => Rollout.Score(s, me, Rollout.Horizon(s, x), x);

		Assert.That(
			Scored(thisTurn with { Attacks = Rollout.Attacks.EachAlone }),
			Is.LessThan(5_000)
		);
		Assert.That(
			Scored(thisTurn with { Attacks = Rollout.Attacks.AllInToo }),
			Is.GreaterThan(5_000)
		);
		// the attack as one decision (AI-17.3; the look-ahead's default since AI-17.6) finds it too
		Assert.That(
			Scored(thisTurn with { Attacks = Rollout.Attacks.AsOne }),
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
			Is.GreaterThan(Scored(thisTurn with { Targets = Rollout.Targets.FirstLegal }) + 2)
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

	// Blocks are judged by what they lead to: facing lethal, a chump block keeps us alive — a turn longer
	// (since AI-17.6 the look-ahead plays their next swing back too, where nothing is left to chump).
	[Test]
	public void FacingLethal_WeChumpBlock()
	{
		(s, _) = TestGames.Put(s, them, TestGames.Creature("Theirs", "4/4"), ZoneType.Battlefield);
		s = WithLife(s, me, 3);
		var withoutAChump = Score(s);
		(s, _) = TestGames.Put(s, me, TestGames.Creature("Chump", "1/1"), ZoneType.Battlefield);

		// two turns later: a loss decays 0.95 a turn, so later by more than one turn's gap
		Assert.That(
			Score(s),
			Is.GreaterThan(
				withoutAChump + Evaluator.WinScore * (1 - Rollout.Default.TerminalDiscount)
			),
			"a later loss: the chump bought a turn"
		);
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

	// Shayne, 2026-10-29 (AI-17.3): "you always want to chump with your least valuable creatures first" —
	// and a 4/4 and a 1/1 against a 5/5 "should actually be blocked by both", since together they kill it;
	// against a 6/6 they don't, so the 1/1 chumps alone. At 3 life, whichever the engine asks first. When
	// only the 4/4 can block (a flier; it has reach), the 4/4 chumps. The CONTROL: the look-ahead's
	// defender before AI-17.3 never chumps.
	[TestCase("5/5", "", false, ExpectedResult = "Big,Small")]
	[TestCase("6/6", "", false, ExpectedResult = "Small")]
	[TestCase("6/6", "Flying", false, ExpectedResult = "Big")]
	[TestCase("6/6", "", true, ExpectedResult = "")]
	public string FacingLethal_TheLeastValuableCreatureChumps(
		string attackerPt,
		string attackerKeyword,
		bool noChump
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
		(s, _) = TestGames.Put(
			s,
			them,
			TestGames.Creature("Big", "4/4", "Reach"),
			ZoneType.Battlefield
		);
		(s, _) = TestGames.Put(s, them, TestGames.Creature("Small", "1/1"), ZoneType.Battlefield);
		s = s.WithPlayer(s.GetPlayer(them) with { Life = 3 }); // TEST SETUP
		s = TestGames.PassUntil(s, x => x.IsWaitingForChoice);
		s = s.Choose([them]);
		s = TestGames.PassUntil(s, x => x.GetPendingChoice() is Actions.DeclareBlockerChoice);

		var chumpers = new List<string>();
		while (s.GetPendingChoice() is Actions.DeclareBlockerChoice block)
		{
			var answer = Evaluator.Without(
				noChump ? Evaluator.Terms.ChumpBlocks : Evaluator.Terms.None,
				() => Lookahead.QuickAnswer(s, block).Single()
			);
			if (answer == attacker)
				chumpers.Add(s.GetCard(block.CreatureId).Name);
			s = s.Choose([answer]);
		}
		return string.Join(",", chumpers.Order());
	}

	// Shayne, 2026-10-29 (AI-17.3 (b), the attack as one decision): "maybe there is a planeswalker on 4
	// loyalty. Your 4 power creature might want to attack that one, and your 2 and 3 power creature might
	// want to attack their face" — for a real planeswalker ("an actual planeswalker with more than just draw
	// a card? Very likely"; a Jace token depends on the board): Chandra, Torch of Defiance at 4 loyalty. The
	// search answers the declaration's questions one at a time, as in a game, from the whole attack it
	// chose at the first.
	// KNOWN DEFECT (Shayne, 2026-10-29: "as long as its noted as a known defect"): the whole attack offers
	// "the 4/4 at Chandra, the rest at the face", but the evaluator prices Chandra by her body and loyalty
	// alone (3.40 at 4, 2.70 at 2 — her abilities unpriced, AI-9.3), so the search chips her with the 2/2
	// and sends the 4/4 face. Un-ignore once AI-9.3 prices a walker's abilities.
	[Test]
	[Ignore(
		"Known defect: walker abilities unpriced (AI-9.3) — the 2/2 chips Chandra, the 4/4 goes face"
	)]
	public void AWholeAttack_SendsTheFourPowerAtTheWalker_TheRestAtTheFace()
	{
		var s = TestGames.InFirstMain();
		var me = s.GetGame().StartingPlayerId;
		var them = s.OpponentOf(me);
		foreach (var (name, pt) in new[] { ("Four", "4/4"), ("Three", "3/3"), ("Two", "2/2") })
			(s, _) = TestGames.Put(s, me, TestGames.Creature(name, pt), ZoneType.Battlefield);
		(s, var walker) = TestGames.Put(
			s,
			them,
			CardRegistry.Get("Chandra, Torch of Defiance"),
			ZoneType.Battlefield
		);
		s = s.UpdateObject(walker, s.GetCard(walker) with { Loyalty = 4 }); // TEST SETUP: Put sets no loyalty
		s = TestGames.PassUntil(s, x => x.GetPendingChoice() is Actions.DeclareAttackerChoice);

		var ai = new BeamSearchAi(me);
		var targets = new Dictionary<string, int>();
		while (s.GetPendingChoice() is Actions.DeclareAttackerChoice ask && ask.PlayerId == me)
		{
			var decided = ai.Decide(s);
			targets[s.GetCard(ask.CreatureId).Name] = decided.Answer!.Single();
			s = decided.State;
		}
		Assert.That(
			targets,
			Is.EquivalentTo(
				new Dictionary<string, int>
				{
					["Four"] = walker,
					["Three"] = them,
					["Two"] = them,
				}
			)
		);
	}

	// AI-17.3's review: an answer of "don't attack" is still an answer — the first question's None used to
	// leave the next looking like the first, so the AI dropped the whole attack it chose and searched
	// again at every question. Now each question after the first is answered from the plan, unsearched.
	[Test]
	public void AWholeAttack_AnswersEveryLaterQuestion_FromItsPlan()
	{
		var s = TestGames.InFirstMain();
		var me = s.GetGame().StartingPlayerId;
		var them = s.OpponentOf(me);
		foreach (var (name, pt) in new[] { ("A", "1/1"), ("B", "3/3"), ("C", "2/2") })
			(s, _) = TestGames.Put(s, me, TestGames.Creature(name, pt), ZoneType.Battlefield);
		(s, _) = TestGames.Put(s, them, TestGames.Creature("Wall", "0/4"), ZoneType.Battlefield);
		s = TestGames.PassUntil(s, x => x.GetPendingChoice() is Actions.DeclareAttackerChoice);
		var first = (Actions.DeclareAttackerChoice)s.GetPendingChoice()!;
		Assert.That(WholeAttacks.IsFirst(s, first));
		Assert.That(
			WholeAttacks.IsFirst(s.Choose([Actions.CombatOption.None]), first),
			Is.False,
			"after a \"don't attack\", not the first"
		);

		var ai = new BeamSearchAi(me);
		s = ai.Decide(s).State;
		Assert.That(ai.FirstMoveScores, Is.Not.Empty, "the first question: searched");
		while (s.GetPendingChoice() is Actions.DeclareAttackerChoice ask && ask.PlayerId == me)
		{
			s = ai.Decide(s).State;
			Assert.That(ai.FirstMoveScores, Is.Empty, "a later question: from the plan");
		}
	}

	/// <summary>Cast a card from our hand and see it through the cheap look-ahead, the given terms off.</summary>
	GameState CastAndSettle(int card, Evaluator.Terms without) =>
		Evaluator.Without(
			without,
			() =>
				Lookahead.Settle(
					s.Act(
						s.LegalActions()
							.OfType<Actions.CastSpellAction>()
							.First(a => a.CardId == card)
					)
				)
		);

	// The cheap look-ahead two choices deep (AI-17.6): Lich's Relic — "When this Equipment enters, you may pay
	// {2}. When you do, for each opponent, destroy up to one target creature or planeswalker that player
	// controls." One level down, the "when you do" target got the cheap answer (none), so paying read as
	// paying for nothing and the look-ahead declined. The CONTROL: one level, their 5/5 lives.
	[TestCase(Evaluator.Terms.None, ExpectedResult = false)]
	[TestCase(Evaluator.Terms.SettleDeep, ExpectedResult = true)]
	public bool TheCheapLookAhead_PaysForLichsRelic_AndDestroysTheirThreat(Evaluator.Terms without)
	{
		for (var i = 0; i < 3; i++)
			(s, _) = TestGames.Put(s, me, TestGames.Card("Swamp"), ZoneType.Battlefield);
		(s, var relic) = TestGames.Put(s, me, CardRegistry.Get("Lich's Relic"), ZoneType.Hand);
		(s, var threat) = TestGames.Put(
			s,
			them,
			TestGames.Creature("Threat", "5/5"),
			ZoneType.Battlefield
		);

		return CastAndSettle(relic, without).IsOnBattlefield(threat); // alive: the Relic did nothing
	}

	// Every single pick by its chooser's best (AI-17.6): "Each opponent sacrifices a creature" — they keep
	// their 5/5 and give up the 1/1. The first listed (the 5/5, put first) was the cheap answer, so every
	// edict in the look-ahead read as killing their best. The CONTROL: the first listed.
	[TestCase(Evaluator.Terms.None, ExpectedResult = true)]
	[TestCase(Evaluator.Terms.SettleDeep, ExpectedResult = false)]
	public bool TheCheapLookAhead_AnEdict_TakesTheirLeastValuable(Evaluator.Terms without)
	{
		(s, _) = TestGames.Put(s, me, TestGames.Card("Forest"), ZoneType.Battlefield);
		(s, var edict) = TestGames.Put(
			s,
			me,
			new CardDefinition(
				TestGames.Inline("Edict", "{1}", "Sorcery").Data,
				[
					new SpellEffectsComponent(
						[
							new EachSacrifices(
								Ref.EachOpponent,
								global::MagicCore.Filters.Is.Creature
							),
						]
					),
				]
			),
			ZoneType.Hand
		);
		(s, var big) = TestGames.Put(
			s,
			them,
			TestGames.Creature("Big", "5/5"),
			ZoneType.Battlefield
		);
		(s, _) = TestGames.Put(s, them, TestGames.Creature("Small", "1/1"), ZoneType.Battlefield);

		return CastAndSettle(edict, without).IsOnBattlefield(big); // they keep the 5/5
	}
}
