using System.Collections.Generic;
using System.Linq;
using ImmutableGameObjects;
using MagicCore;
using MagicCore.Actions;
using MagicCore.Sets.Fra;
using MagicTable;
using NUnit.Framework;

namespace SQGodotCommon.Tests;

public class BoardChoiceTests
{
	/// <summary>A mulligan, then keep: the London bottom choice, which offers the cards in your hand.</summary>
	static MagicTableManager AtTheBottomChoice()
	{
		var m = new MagicTableManager(FraDecks.All["White-Black"], FraDecks.All["Red-Green"], 5);
		for (var i = 0; i < 50 && m.State.GetPendingChoice() is null; i++)
		{
			while (m.StepAi() is not null) { }
			var actions = m.State.LegalActions();
			m.Act(
				m.State.GetPlayer(m.HumanId).MulligansTaken == 0
					? actions.OfType<MulliganAction>().First()
					: actions.OfType<KeepHandAction>().First()
			);
		}
		Assert.That(
			m.State.GetPendingChoice(),
			Is.InstanceOf<ChooseCardsFromHandChoice>(),
			"precondition"
		);
		return m;
	}

	static HashSet<int> Hand(MagicTableManager m) =>
		m.State.CardsIn(m.HumanId, ZoneType.Hand).Select(c => c.Id).ToHashSet();

	[Test]
	public void CardsInYourHand_AreClickedWhereTheyAre()
	{
		var m = AtTheBottomChoice();

		var board = BoardChoice.For(m.State, m.State.GetPendingChoice()!, Hand(m));

		Assert.That(board, Is.Not.Null);
		Assert.That(board!.Clickable, Is.EquivalentTo(Hand(m)));
		Assert.That(board.Buttons, Is.Empty);
	}

	[Test]
	public void AnOfferedObjectOffTheTable_SendsTheChoiceToThePanel()
	{
		var m = AtTheBottomChoice();
		var someOfTheHand = Hand(m).Skip(1).ToHashSet();

		Assert.That(BoardChoice.For(m.State, m.State.GetPendingChoice()!, someOfTheHand), Is.Null);
	}

	[Test]
	public void NonObjectOptions_AreButtons_AndOptionZeroIsNeverLookedUp()
	{
		var m = AtTheBottomChoice();
		var choice = new DeclareAttackerChoice(m.HumanId, Hand(m).First())
		{
			Options =
			[
				new ChoiceOption { Id = CombatOption.None, DisplayText = "Don't attack" },
				new ChoiceOption { Id = m.OpponentId, DisplayText = "Attack AI" },
			],
		};

		var board = BoardChoice.For(m.State, choice, new HashSet<int> { m.OpponentId });

		Assert.That(board, Is.Not.Null);
		Assert.That(board!.Clickable, Is.EquivalentTo(new[] { m.OpponentId }));
		Assert.That(board.Buttons.Select(b => b.Id), Is.EqualTo(new[] { CombatOption.None }));
		Assert.That(board.AboutId, Is.EqualTo(choice.CreatureId));
	}

	[Test]
	public void ModesAndYesNo_StayInThePanel()
	{
		var m = AtTheBottomChoice();
		var yesNo = new YesNoChoice(m.HumanId, "Pay?")
		{
			Options = [new ChoiceOption { Id = m.HumanId }],
		};

		Assert.That(BoardChoice.For(m.State, yesNo, new HashSet<int> { m.HumanId }), Is.Null);
	}
}
