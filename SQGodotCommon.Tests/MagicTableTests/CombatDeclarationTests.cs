using System.Collections.Generic;
using System.Linq;
using MagicCore;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Sets.Fra;
using MagicCore.Turns;
using MagicTable;
using NUnit.Framework;

namespace SQGodotCommon.Tests;

public class CombatDeclarationTests
{
	/// <summary>
	/// Plays your seat lazily (keep, lands, creatures, pass, no attacks, first options) until <paramref name="at"/>
	/// is pending for you; the AI plays its own.
	/// </summary>
	static MagicTableManager Until(int seed, System.Func<MagicTableManager, bool> at, int turn = 0)
	{
		var m = new MagicTableManager(FraDecks.All["White-Black"], FraDecks.All["Red-Green"], seed);
		if (turn > 0)
			m.AutoplayTo(turn);
		for (var i = 0; i < 600 && !m.IsOver && !(m.HumanToAct && at(m)); i++)
		{
			while (m.StepAi() is not null) { }
			if (!m.HumanToAct || at(m))
				continue;
			if (m.State.GetPendingChoice() is DeclareAttackerChoice)
				m.DeclareCombat(new Dictionary<int, int>());
			else if (m.State.GetPendingChoice() is { } choice)
				m.Choose(
					choice.Options.Where(o => o.IsEnabled).Take(choice.MinChoices).Select(o => o.Id)
				);
			else
			{
				var actions = m.State.LegalActions();
				m.Act(
					actions.FirstOrDefault(a => a is PlayLandAction or KeepHandAction)
						// creatures, so there is something to block with
						?? actions.FirstOrDefault(a =>
							a is CastSpellAction c
							&& m.State.GetCard(c.CardId).Is(CardType.Creature)
						)
						?? actions.First(a => a is PassPriorityAction)
				);
			}
		}
		return m;
	}

	static bool Attacking(MagicTableManager m) =>
		m.State.GetPendingChoice() is DeclareAttackerChoice;

	static bool Blocking(MagicTableManager m) => m.State.GetPendingChoice() is DeclareBlockerChoice;

	[Test]
	public void AClickAttacks_ASecondClickTakesItBack()
	{
		var m = Until(5, Attacking, turn: 9);
		Assert.That(Attacking(m), "precondition");
		var declaration = new CombatDeclaration(attacking: true);
		var creature = Combat.AttackCandidates(m.State)[0].Id;

		declaration.Click(m.State, m.State.GetPendingChoice()!, creature);
		Assert.That(
			declaration.Declared[creature],
			Is.EqualTo(m.OpponentId),
			"at the player by default"
		);

		declaration.Click(m.State, m.State.GetPendingChoice()!, creature);
		Assert.That(declaration.Declared, Is.Empty);
	}

	[Test]
	public void APlaneswalkerClick_SendsTheLastAttackerThere_AndTheEngineAgrees()
	{
		var m = Until(5, Attacking, turn: 9); // seed 5: the AI has a Jace on the battlefield
		var jace = m
			.State.CardsIn(m.OpponentId, ZoneType.Battlefield)
			.FirstOrDefault(c => c.Is(CardType.Planeswalker));
		Assert.That(jace, Is.Not.Null, "precondition: a planeswalker to attack");
		var declaration = new CombatDeclaration(attacking: true);
		var creature = Combat.AttackCandidates(m.State)[0].Id;

		declaration.Click(m.State, m.State.GetPendingChoice()!, creature);
		declaration.Click(m.State, m.State.GetPendingChoice()!, jace!.Id);
		m.DeclareCombat(declaration.Declared);

		Assert.That(m.State.GetCard(creature).AttackingId, Is.EqualTo(jace.Id));
	}

	[Test]
	public void ABlockerThenAnAttacker_Blocks_AndAClickOnTheBlockerUndoesIt()
	{
		var m = Enumerable
			.Range(1, 30)
			.Select(seed => Until(seed, Blocking))
			.FirstOrDefault(Blocking);
		Assert.That(m, Is.Not.Null, "precondition: some game where you get to block");
		var s = m!.State;
		var blocker = Combat.BlockCandidates(s)[0];
		var attacker = Combat.Attackers(s).First(a => Combat.CanBlock(s, blocker, a));
		var declaration = new CombatDeclaration(attacking: false);

		var choice = s.GetPendingChoice()!;
		declaration.Click(s, choice, blocker.Id);
		Assert.That(
			declaration.Clickable(s, choice),
			Does.Contain(attacker.Id),
			"its attackers light up"
		);
		declaration.Click(s, choice, attacker.Id);
		Assert.That(declaration.Declared[blocker.Id], Is.EqualTo(attacker.Id));

		declaration.Click(s, choice, blocker.Id);
		Assert.That(declaration.Declared, Is.Empty);
	}

	[Test]
	public void AllAttack_DeclaresEveryCandidate()
	{
		var m = Until(5, Attacking, turn: 9);
		var declaration = new CombatDeclaration(attacking: true);

		declaration.AllAttack(m.State);

		Assert.That(
			declaration.Declared.Keys,
			Is.EquivalentTo(Combat.AttackCandidates(m.State).Select(c => c.Id))
		);
		Assert.That(
			declaration.Selected,
			Is.Not.Zero,
			"so a planeswalker click still redirects one"
		);
	}

	[Test]
	public void APickTheEngineRefuses_IsNamed_AndNothingIsCommitted()
	{
		var m = Until(5, Attacking, turn: 9);
		var (before, logged) = (m.State, m.Log.Count);
		var creature = Combat.AttackCandidates(m.State)[0].Id;

		var refused = m.DeclareCombat(new Dictionary<int, int> { [creature] = m.HumanId }); // attack yourself

		Assert.That(refused, Has.Count.EqualTo(1));
		Assert.That(refused[0], Does.Contain("isn't allowed"));
		Assert.That(m.State, Is.SameAs(before));
		Assert.That(m.Log.Count, Is.EqualTo(logged), "nothing logged, nothing to undo");
	}
}
