using System.Collections.Generic;
using System.IO;
using System.Linq;
using MagicCore;
using MagicCore.Actions;
using MagicCore.Ai;
using MagicCore.Sets.Fra;
using MagicTable;
using NUnit.Framework;

namespace SQGodotCommon.Tests;

public class MagicTableManagerTests
{
	static MagicTableManager New(int seed = 5, bool aiVsAi = false, bool search = false) =>
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
		m.AutoplayTo(9); // seed 5: your turn 9, a creature able to attack
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
		m.AutoplayTo(9); // seed 5: your turn 9, a creature able to attack
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
			m.Log.Count(l => l.Contains("Attack: ")),
			Is.EqualTo(1),
			"one line for the declaration"
		);
	}

	[Test]
	public void AManualTap_FloatsTheMana_AndKeepsYourPriority()
	{
		var m = New();
		m.AutoplayTo(9); // seed 5: your main phase, untapped Swamps
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

		m.Sandbox(m.HumanId, FraCards.Get("Last Gasp"), ZoneType.Hand);

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
		var m = new MagicTableManager(FraDecks.All["White-Black"], FraDecks.All["Red-Green"], 5)
		{
			RecordTo = dir,
		};
		m.AutoplayTo(9); // seed 5: your turn 9, a creature able to attack (the lazy human below casts none)
		RunAi(m);
		m.Sandbox(m.HumanId, FraCards.Get("Last Gasp"), ZoneType.Hand);
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
}
