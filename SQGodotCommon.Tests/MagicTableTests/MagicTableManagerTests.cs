using System.Linq;
using MagicCore;
using MagicCore.Actions;
using MagicCore.Sets.Fra;
using MagicTable;
using NUnit.Framework;

namespace SQGodotCommon.Tests;

public class MagicTableManagerTests
{
	static MagicTableManager New(int seed = 5, bool aiVsAi = false) =>
		new(FraDecks.All["White-Black"], FraDecks.All["Red-Green"], seed, aiVsAi);

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

		m.DeclineCombat();

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
	public void Replay_DealsTheSameOpeningHand()
	{
		var m = New(seed: 77);
		var replay = m.Replay();

		string Hand(MagicTableManager x) =>
			string.Join(",", x.State.CardsIn(x.HumanId, ZoneType.Hand).Select(c => c.Name));
		Assert.That(Hand(replay), Is.EqualTo(Hand(m)));
		Assert.That(replay.Seed, Is.EqualTo(77));
	}
}
