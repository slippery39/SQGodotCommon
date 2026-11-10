using System.Collections.Generic;
using System.IO;
using System.Linq;
using MagicCore;
using MagicCore.Actions;
using MagicCore.Ai;
using MagicCore.Sets;
using MagicCore.Sets.Fra;
using MagicTable;
using NUnit.Framework;

namespace SQGodotCommon.Tests;

public class MagicTableManagerTests
{
	/// <summary>
	/// The seeded game most tests here autoplay to turn 9 — your turn, a creature able to attack, and a later
	/// declaration of two. GreedyAi plays it, so a change in how GreedyAi plays can move it: pick another seed
	/// that keeps those moments — 15 since AI-17.6 (5 before; GreedyAi's look-ahead began choosing its targets).
	/// </summary>
	const int Seed = 15;

	static MagicTableManager New(int seed = Seed, bool aiVsAi = false, bool search = false) =>
		new(FraDecks.All["White-Black"], FraDecks.All["Red-Green"], seed, aiVsAi, search);

	static void RunAi(MagicTableManager m)
	{
		while (m.StepAi() is not null) { }
	}

	/// <summary>
	/// The lazy human: keeps, plays a land when it can, otherwise passes; answers every choice with
	/// its first options. The lands matter — with mana, an instant is castable at the steps a stop
	/// must skip, so a missed stop would show.
	/// </summary>
	static void PassOrFirst(MagicTableManager m)
	{
		if (m.State.GetPendingChoice() is { } choice)
			m.Choose(
				choice.Options.Where(o => o.IsEnabled).Take(choice.MinChoices).Select(o => o.Id)
			);
		else
		{
			var actions = m.State.LegalActions();
			m.Act(
				actions.FirstOrDefault(a => a is PlayLandAction or KeepHandAction)
					?? actions.First(a => a is PassPriorityAction)
			);
		}
	}

	[Test]
	public void AiVsAi_PlaysAGameToTheEnd()
	{
		var m = New(aiVsAi: true);
		for (var i = 0; i < 5000 && !m.IsOver; i++)
			m.StepAi();

		Assert.That(m.IsOver);
		Assert.That(m.HumanToAct, Is.False);
		Assert.That(m.Log.Count, Is.GreaterThan(10));
	}

	[Test]
	public void TheHuman_IsAskedOnlyAtAStop()
	{
		var m = New();
		var decisions = 0;
		for (; decisions < 400 && !m.IsOver; decisions++)
		{
			RunAi(m);
			if (!m.HumanToAct)
				continue;
			Assert.That(m.Stops.StopsAt(m.State, m.HumanId), $"asked at {m.State.GetGame().Step}");
			PassOrFirst(m);
		}

		Assert.That(m.State.GetGame().TurnNumber, Is.GreaterThan(4), "the game went on");
	}

	[Test]
	public void Undo_RestoresTheStateBeforeYourDecision()
	{
		var m = New();
		RunAi(m);
		Assert.That(m.HumanToAct, "precondition");
		var (before, logCount) = (m.State, m.Log.Count);

		PassOrFirst(m);
		RunAi(m);
		Assert.That(m.State, Is.Not.SameAs(before));

		m.Undo();
		Assert.That(m.State, Is.SameAs(before));
		Assert.That(m.Log.Count, Is.EqualTo(logCount));
		Assert.That(m.CanUndo, Is.False);
	}

	[Test]
	public void ARejectedMove_LeavesNoUndoStep()
	{
		var m = New();
		RunAi(m);
		var hand = m.State.CardsIn(m.HumanId, ZoneType.Hand).First();

		Assert.That(
			() => m.Act(new PlayLandAction { PlayerId = m.HumanId, CardId = hand.Id }),
			Throws.Exception,
			"no land is played during the mulligan"
		);
		Assert.That(m.CanUndo, Is.False);
	}

	[Test]
	public void ChangingTheStops_PassesAStepThatIsNoLongerOne()
	{
		var m = New();
		for (
			var i = 0;
			i < 400 && !(m.HumanToAct && m.State.GetGame().Step == Step.PreCombatMain);
			i++
		)
		{
			RunAi(m);
			if (m.HumanToAct && m.State.GetGame().Step != Step.PreCombatMain)
				PassOrFirst(m);
		}
		Assert.That(m.State.LegalActions().OfType<PlayLandAction>(), Is.Not.Empty, "precondition");

		m.Stops = new Stops([], []);

		Assert.That(
			m.HumanToAct
				&& m.State.GetGame().Step == Step.PreCombatMain
				&& !m.State.IsWaitingForChoice,
			Is.False
		);
	}

	[Test]
	public void AutoplayTo_ReachesTheTurn_AndLeavesNoUndo()
	{
		var m = New();
		m.AutoplayTo(5);

		Assert.That(m.IsOver || m.State.GetGame().TurnNumber >= 5);
		Assert.That(m.CanUndo, Is.False);
		Assert.That(m.HumanToAct || m.AiToAct || m.IsOver, "the table can carry on");
	}

	[Test]
	public void NoAttacks_DeclinesTheWholeDeclaration_AsOneUndoStep()
	{
		var m = New();
		m.AutoplayTo(9); // the seed: your turn 9, a creature able to attack
		for (var i = 0; i < 50 && m.State.GetPendingChoice() is not DeclareAttackerChoice; i++)
		{
			RunAi(m);
			if (m.HumanToAct && m.State.GetPendingChoice() is null)
				m.Act(m.State.LegalActions().OfType<PassPriorityAction>().First());
		}
		Assert.That(
			m.HumanToAct && m.State.GetPendingChoice() is DeclareAttackerChoice,
			"precondition"
		);
		var atTheDeclaration = m.State;

		m.DeclareCombat(new Dictionary<int, int>());

		Assert.That(
			m.HumanToAct && m.State.GetPendingChoice() is DeclareAttackerChoice,
			Is.False,
			"every attacker answered"
		);
		Assert.That(m.Log.Count(l => l.EndsWith("No attacks")), Is.EqualTo(1));
		m.Undo();
		Assert.That(m.State, Is.SameAs(atTheDeclaration));
	}

	[Test]
	public void DeclareCombat_AttacksWithThePickedCreatures_AsOneDecision()
	{
		var m = New();
		m.AutoplayTo(9); // the seed: your turn 9, a creature able to attack
		for (var i = 0; i < 50 && m.State.GetPendingChoice() is not DeclareAttackerChoice; i++)
		{
			RunAi(m);
			if (m.HumanToAct && m.State.GetPendingChoice() is null)
				m.Act(m.State.LegalActions().OfType<PassPriorityAction>().First());
		}
		var attackers = global::MagicCore.Turns.Combat.AttackCandidates(m.State);
		Assert.That(attackers, Is.Not.Empty, "precondition");

		m.DeclareCombat(attackers.ToDictionary(c => c.Id, _ => m.OpponentId));

		Assert.That(
			attackers.Select(c => m.State.GetCard(c.Id).AttackingId),
			Is.All.EqualTo(m.OpponentId),
			"every picked creature attacks the AI"
		);
		Assert.That(
			m.Log.Count(l => l.Contains("You: Attack: ")),
			Is.EqualTo(1),
			"one line for your declaration (the AI's attacks read alike since TBL-4.2)"
		);
	}

	[Test]
	public void ALoneCreatureThatCantAttackAlone_IsRefusedWithWhy_NothingCommitted()
	{
		var m = New();
		m.AutoplayTo(4); // the AI's turn: put in now, both can attack on yours
		foreach (var name in new[] { "Pet Sehlat", "Graft Surgeon" })
		{
			while (!m.CanSandbox && m.AiToAct)
				m.StepAi();
			m.Sandbox(m.HumanId, CardRegistry.Get(name), ZoneType.Battlefield);
		}
		for (
			var i = 0;
			i < 80 && !(m.HumanToAct && m.State.GetPendingChoice() is DeclareAttackerChoice);
			i++
		)
		{
			RunAi(m);
			if (m.HumanToAct && m.State.GetPendingChoice() is DeclareBlockerChoice)
				m.DeclareCombat(new Dictionary<int, int>());
			else if (m.HumanToAct && m.State.GetPendingChoice() is null)
				m.Act(m.State.LegalActions().OfType<PassPriorityAction>().First());
		}
		Assert.That(
			m.HumanToAct && m.State.GetPendingChoice() is DeclareAttackerChoice,
			$"precondition: your declaration (turn {m.State.GetGame().TurnNumber}, {m.State.GetPendingChoice()?.GetType().Name})"
		);
		Assert.That(
			m.State.CardsIn(m.HumanId, ZoneType.Battlefield).Select(c => c.Name),
			Does.Contain("Pet Sehlat"),
			"precondition: it survived the AI's turn"
		);
		var candidates = global::MagicCore.Turns.Combat.AttackCandidates(m.State);
		var sehlat = candidates.Single(c => c.Name == "Pet Sehlat");
		var partner = candidates.First(c => c.Id != sehlat.Id);
		var atTheDeclaration = m.State;

		var refused = m.DeclareCombat(new Dictionary<int, int> { [sehlat.Id] = m.OpponentId });

		Assert.That(refused, Is.EqualTo(new[] { "Pet Sehlat can't attack alone" }));
		Assert.That(
			m.State,
			Is.SameAs(atTheDeclaration),
			"nothing committed: fix it and submit again"
		);

		refused = m.DeclareCombat(
			new Dictionary<int, int> { [sehlat.Id] = m.OpponentId, [partner.Id] = m.OpponentId }
		);

		Assert.That(refused, Is.Empty);
		Assert.That(
			m.State.GetCard(sehlat.Id).AttackingId,
			Is.EqualTo(m.OpponentId),
			"not alone: it attacks"
		);
	}

	[Test]
	public void AManualTap_FloatsTheMana_AndKeepsYourPriority()
	{
		var m = New();
		m.AutoplayTo(9); // the seed: your main phase, untapped Swamps
		RunAi(m);
		Assert.That(m.HumanToAct && m.State.GetPendingChoice() is null, "precondition");
		var step = m.State.GetGame().Step;
		var land = m
			.State.CardsIn(m.HumanId, ZoneType.Battlefield)
			.First(c => m.State.TapOptions(m.HumanId, c.Id).Any());

		m.Act(m.State.TapOptions(m.HumanId, land.Id).First());

		Assert.That(m.State.GetPlayer(m.HumanId).ManaPool.Total, Is.EqualTo(1));
		Assert.That(m.HumanToAct && m.State.GetGame().Step == step, "still yours, same step");
		Assert.That(m.CanUndo, "a tap is a decision you can take back");
	}

	[Test]
	public void Sandbox_PutsACard_AsAnUndoableDecision()
	{
		var m = New();
		m.AutoplayTo(3);
		RunAi(m);
		Assert.That(m.CanSandbox, "precondition");
		var before = m.State;
		var hand = m.State.CardsIn(m.HumanId, ZoneType.Hand).Count();

		m.Sandbox(m.HumanId, CardRegistry.Get("Last Gasp"), ZoneType.Hand);

		Assert.That(m.State.CardsIn(m.HumanId, ZoneType.Hand).Count(), Is.EqualTo(hand + 1));
		Assert.That(m.Log[^1], Does.Contain("[sandbox] Last Gasp"));
		m.Undo();
		Assert.That(m.State, Is.SameAs(before));
	}

	/// <summary>
	/// Every kind of move the table makes — the stand-in, a sandbox put, a manual tap, a whole combat
	/// declaration, an undone decision, the AI, your stops, the rules — and the file it wrote replays
	/// to exactly this position. An undone move left in the record, or a pass made but not recorded,
	/// makes the replay stale.
	/// </summary>
	[Test]
	public void TheRecordFile_ReplaysTheGameAsPlayed()
	{
		var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
		var m = new MagicTableManager(FraDecks.All["White-Black"], FraDecks.All["Red-Green"], Seed)
		{
			RecordTo = dir,
		};
		m.AutoplayTo(9); // the seed: your turn 9, a creature able to attack (the lazy human below casts none)
		RunAi(m);
		m.Sandbox(m.HumanId, CardRegistry.Get("Last Gasp"), ZoneType.Hand);
		var (tapped, undone) = (false, false);
		for (var i = 0; i < 400 && !m.IsOver && m.State.GetGame().TurnNumber < 13; i++)
		{
			RunAi(m);
			if (!m.HumanToAct)
				continue;
			var land = m
				.State.CardsIn(m.HumanId, ZoneType.Battlefield)
				.FirstOrDefault(c => m.State.TapOptions(m.HumanId, c.Id).Any());
			if (!tapped && m.State.GetPendingChoice() is null && land is not null)
			{
				m.Act(m.State.TapOptions(m.HumanId, land.Id).First());
				tapped = true;
			}
			else if (m.State.GetPendingChoice() is DeclareAttackerChoice)
				m.DeclareCombat(
					global::MagicCore
						.Turns.Combat.AttackCandidates(m.State)
						.ToDictionary(c => c.Id, _ => m.OpponentId)
				);
			else
				PassOrFirst(m);
			if (!undone && m.State.GetGame().TurnNumber >= 11)
			{
				RunAi(m);
				m.Undo();
				undone = true;
			}
		}
		var record = GameRecord.FromJson(File.ReadAllText(Directory.GetFiles(dir).Single()));
		Directory.Delete(dir, recursive: true);

		var (_, last) = record.Replay();

		Assert.That(MoveTokens.Offers(last), Is.EqualTo(MoveTokens.Offers(m.State)));
		Assert.That(MoveTokens.Ending(last), Is.EqualTo(MoveTokens.Ending(m.State)));
		Assert.That(tapped && undone, "precondition");
		Assert.That(
			m.Log.Any(l => l.Contains("You: Attack: ")),
			"precondition: you attacked\n" + string.Join("\n", m.Log)
		);
		Assert.That(
			record.Moves.Select(mv => mv.Kind).Distinct(),
			Is.EquivalentTo(new[] { "auto", "ai", "rule", "stop", "sandbox", "you" })
		);
	}

	/// <summary>
	/// Seed 5 from your turn 9 to 12: the stand-in, then a second creature put in (the lazy human casts
	/// none), then the lazy human, attacking with everything it can.
	/// </summary>
	static MagicTableManager PlayedToTurn12()
	{
		var m = New();
		m.AutoplayTo(9);
		RunAi(m);
		m.Sandbox(
			m.HumanId,
			FraDecks.All["White-Black"].First(c => c.Data.TypeLine.Contains("Creature")),
			ZoneType.Battlefield
		);
		for (var i = 0; i < 400 && !m.IsOver && m.State.GetGame().TurnNumber < 12; i++)
		{
			RunAi(m);
			if (!m.HumanToAct)
				continue;
			if (m.State.GetPendingChoice() is DeclareAttackerChoice)
				m.DeclareCombat(
					global::MagicCore
						.Turns.Combat.AttackCandidates(m.State)
						.ToDictionary(c => c.Id, _ => m.OpponentId)
				);
			else
				PassOrFirst(m);
		}
		return m;
	}

	static string Position(MagicTableManager m) =>
		MoveTokens.Offers(m.State) + MoveTokens.Ending(m.State);

	/// <summary>
	/// TBL-5.7: a resumed record is the same game with the same undo steps — each Undo lands where the
	/// original's did, a combat declaration one step, the stand-in's moves none — so you can step back
	/// to before any decision of yours.
	/// </summary>
	[Test]
	public void Resume_IsTheSameGame_WithTheSameUndoSteps()
	{
		var m = PlayedToTurn12();

		var r = MagicTableManager.Resume(m.Record, search: false);

		Assert.That(
			r.Record.Moves.Select(x => x.Token),
			Is.EqualTo(m.Record.Moves.Select(x => x.Token))
		);
		Assert.That(Position(r), Is.EqualTo(Position(m)));
		Assert.That(r.Log[^1], Does.StartWith("[resumed] all"));
		Assert.That(
			m.Log.Any(l => l.Contains("You: Attack: ") && l.Split('→').Length > 2),
			"precondition: a declaration of two creatures, to group\n" + string.Join("\n", m.Log)
		);
		var steps = 0;
		for (; m.CanUndo; steps++)
		{
			m.Undo();
			r.Undo();
			Assert.That(Position(r), Is.EqualTo(Position(m)), $"undo {steps + 1}");
		}
		Assert.That(r.CanUndo, Is.False, "no more undo steps than the original");
		Assert.That(steps, Is.GreaterThan(3), "precondition");
	}

	/// <summary>When the engine has moved, the replay goes on while moves apply, and says where it differs and stops.</summary>
	[Test]
	public void Resume_AStaleRecord_SaysWhereItDiffersAndStops()
	{
		var record = PlayedToTurn12().Record;
		var n = record.Moves.Count;

		var moved = record with
		{
			Moves = record.Moves.SetItem(9, record.Moves[9] with { Offers = "changed" }),
		};
		var broken = record with
		{
			Moves = record.Moves.SetItem(9, record.Moves[9] with { Token = "a99999" }),
		};

		Assert.That(
			MagicTableManager.Resume(moved, search: false).Log[^1],
			Does.Contain($"move 10; {n} of {n} moves replayed")
		);
		Assert.That(
			MagicTableManager.Resume(broken, search: false).Log[^1],
			Does.Contain($"move 10; 9 of {n} moves replayed")
		);
	}

	/// <summary>
	/// TBL-4.2: an AI's attack reads like yours — "Attack: X → You" — never its prompt ("Attack with X
	/// 2/2? Attack You"), and a creature it keeps home shows nothing.
	/// </summary>
	[Test]
	public void TheAisCombat_IsLoggedAsADeclaration_NotItsPrompt()
	{
		var m = New(aiVsAi: true);
		for (var i = 0; i < 5000 && !m.IsOver; i++)
			m.StepAi();

		Assert.That(m.Log.Any(l => l.Contains(": Attack: ")), "precondition: an attack");
		Assert.That(m.Log.Where(l => l.Contains('?')), Is.Empty);
		Assert.That(m.Log.Where(l => l.Contains("Don't")), Is.Empty);
	}

	[Test]
	public void AnAiGame_IsRecordedToItsEnd()
	{
		var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
		var m = new MagicTableManager(
			FraDecks.All["White-Black"],
			FraDecks.All["Red-Green"],
			5,
			aiVsAi: true
		)
		{
			RecordTo = dir,
		};
		for (var i = 0; i < 5000 && !m.IsOver; i++)
			m.StepAi();
		var record = GameRecord.FromJson(File.ReadAllText(Directory.GetFiles(dir).Single()));
		Directory.Delete(dir, recursive: true);

		Assert.That(m.IsOver, "precondition");
		Assert.That(record.End, Is.EqualTo(MoveTokens.Ending(m.State)));
		Assert.That(MoveTokens.Ending(record.Replay().Last), Is.EqualTo(record.End));
	}

	[Test]
	public void Replay_DealsTheSameOpeningHand()
	{
		var m = New(seed: 77);
		var replay = m.Replay();

		string Hand(MagicTableManager x) =>
			string.Join(",", x.State.CardsIn(x.HumanId, ZoneType.Hand).Select(c => c.Name));
		Assert.That(Hand(replay), Is.EqualTo(Hand(m)));
		Assert.That(replay.Seed, Is.EqualTo(77));
	}

	[Test]
	public void TheSearch_PlaysTheTable_AndReplayKeepsIt()
	{
		var m = New(aiVsAi: true, search: true);
		for (var i = 0; i < 200 && !m.IsOver && m.State.GetGame().TurnNumber < 3; i++)
			m.StepAi();

		Assert.That(
			m.State.GetGame().TurnNumber,
			Is.GreaterThanOrEqualTo(3),
			string.Join(" | ", m.Log)
		);
		Assert.That(m.Replay().Search);
	}

	// Shayne, 2026-11-08: two Temporal Conquerors double-blocked Elim Garak and he lived, with no word
	// why — Dr. McCoy took his +1/+1 counter instead of the destruction. A real game, kept by
	// `--keep` (MagicCore.Tests/Records), replayed move by move.
	[Test]
	public void ACreatureSavedByDrMcCoy_SaysWhyItLived()
	{
		var root = AppContext.BaseDirectory;
		while (!Directory.Exists(Path.Combine(root, "MagicCore.Tests")))
			root = Path.GetDirectoryName(root)!;
		var record = GameRecord.FromJson(
			File.ReadAllText(
				Path.Combine(root, "MagicCore.Tests", "Records", "McCoySavesGarak.json")
			)
		);

		var m = MagicTableManager.Resume(record, search: false);

		Assert.That(
			m.Log.Single(l => l.Contains("would be destroyed")),
			Does.Contain("Elim Garak, Simple Tailor").And.Contain("Dr. McCoy, Surly Surgeon")
		);
	}

	[Test]
	public void ALookAtTheirHand_IsLogged_CardByCard()
	{
		var m = New();
		RunAi(m);
		while (!m.CanSandbox || !m.HumanToAct)
		{
			PassOrFirst(m);
			RunAi(m);
		}
		// V'Ger, the Intruder enters: "choose one — • Look at target opponent's hand, then draw a card."
		m.Sandbox(m.HumanId, CardRegistry.Get("V'Ger, the Intruder", "TRK"), ZoneType.Battlefield);
		for (var i = 0; i < 50 && m.Log.All(l => !l.Contains("seen in")); i++)
		{
			RunAi(m);
			if (m.State.GetPendingChoice() is ChooseModesChoice)
				m.Choose([0]);
			else if (m.State.GetPendingChoice() is ChooseTargetChoice)
				m.Choose([m.OpponentId]);
			else if (m.HumanToAct)
				PassOrFirst(m);
		}
		var theirs = m.State.CardsIn(m.OpponentId, ZoneType.Hand).Select(c => c.Name);
		var seen = m.Log.Single(l => l.Contains("seen in"));
		Assert.That(theirs, Is.Not.Empty, "precondition: they hold cards");
		Assert.That(theirs.All(seen.Contains), seen);
	}

	[Test]
	public void Conceding_EndsTheGameForTheAi_AndUndoTakesItBack()
	{
		var m = new MagicTableManager(FraDecks.All["White-Black"], FraDecks.All["Red-Green"], 5);
		m.AutoplayTo(3);
		Assert.That(m.IsOver, Is.False, "precondition");

		m.Concede();

		Assert.That(m.IsOver, Is.True);
		Assert.That(m.WinnerId, Is.EqualTo(m.OpponentId));
		Assert.That(m.AiToAct || m.HumanToAct, Is.False, "nobody plays on");
		Assert.That(m.Record.End, Is.Null, "the record is not the engine's ending");

		m.Undo();

		Assert.That(m.IsOver, Is.False, "Undo takes the concession back, nothing before it");
	}
}
