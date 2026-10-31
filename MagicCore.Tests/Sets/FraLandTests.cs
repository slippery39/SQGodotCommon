using System.Text.RegularExpressions;
using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Sets;

namespace MagicCore.Tests.Sets;

/// <summary>
/// Every FRA dual land, one by one, against its PRINTED text: when it enters tapped, and what it
/// taps for — through the real land play and the real mana ability, never by inspecting a script.
/// </summary>
public class FraLandTests
{
	static readonly Regex DualText = new(@"\{T\}: Add \{(.)\} or \{(.)\}\.");

	// Lands only: three FRA creatures also print "{T}: Add {A} or {B}." (Hungering Puppetbeast …).
	static IEnumerable<CardData> PrintedDuals =>
		CardDataSet
			.Load("FRA")
			.Select(p => p.Card)
			.Where(c => c.TypeLine.Contains("Land") && DualText.IsMatch(c.OracleText ?? ""));

	static IEnumerable<string> DualNames() => PrintedDuals.Select(c => c.Name);

	[Test]
	public void EveryPrintedDual_IsScripted() =>
		Assert.That(DualNames().Where(n => !CardRegistry.Scripted.Contains(n)), Is.Empty);

	GameState s = null!;
	int me;

	[SetUp]
	public void Board()
	{
		s = TestGames.InFirstMain();
		me = s.GetGame().StartingPlayerId;
	}

	int Play(string land)
	{
		int id;
		(s, id) = TestGames.Put(s, me, CardRegistry.Get(land), ZoneType.Hand);
		s = s.Act(new PlayLandAction { PlayerId = me, CardId = id });
		return id;
	}

	[TestCaseSource(nameof(DualNames))]
	public void EntersTappedExactlyWhenItsPrintedConditionFails(string name)
	{
		var text = PrintedDuals.Single(c => c.Name == name).OracleText!;
		var needsWalker = text.Contains("unless you control a planeswalker");
		Assert.That(
			needsWalker || text.Contains("unless you control two or more other lands"),
			"an unknown condition"
		);

		var first = Play(name);
		Assert.That(s.GetCard(first).IsTapped, "the condition fails on an empty board");

		s = TestGames.PassUntil(
			s,
			x => x.GetGame().TurnNumber == 3 && x.GetGame().Step == Step.PreCombatMain
		);
		if (needsWalker)
		{
			int walker;
			(s, walker) = TestGames.Put(s, me, TestGames.Planeswalker("Walker", 3), ZoneType.Hand);
			s = s.EnterBattlefield(walker, me); // TEST SETUP
		}
		else
			(s, _) = TestGames.Put(s, me, TestGames.Card("Plains"), ZoneType.Battlefield); // the first dual is the other
		var second = Play(name);
		Assert.That(s.GetCard(second).IsTapped, Is.False, "the condition holds");
	}

	[TestCaseSource(nameof(DualNames))]
	public void TapsForEachPrintedColour_AndNoOther(string name)
	{
		var printed = DualText.Match(PrintedDuals.Single(c => c.Name == name).OracleText!);
		var colours = new[] { printed.Groups[1].Value, printed.Groups[2].Value }
			.Select(ManaCost.ColorOf)
			.ToList();
		int land;
		(s, land) = TestGames.Put(s, me, CardRegistry.Get(name), ZoneType.Battlefield);

		foreach (var colour in Enum.GetValues<ManaColor>())
		{
			var tap = new TapForManaAction
			{
				PlayerId = me,
				CardId = land,
				Color = colour,
			};
			Assert.That(
				s.TryAddAction(tap).Success,
				Is.EqualTo(colours.Contains(colour)),
				$"{name}: {colour}"
			);
		}
		s = s.Act(
			new TapForManaAction
			{
				PlayerId = me,
				CardId = land,
				Color = colours[1],
			}
		);
		Assert.That(s.GetPlayer(me).ManaPool[colours[1]], Is.EqualTo(1));
	}

	// MagicState.EnterBattlefield once judged "two or more other lands" on the card's OLD
	// controller's board. Entering under someone else's control must count the new board.
	[Test]
	public void TheConditionReadsTheBoardItEnters()
	{
		var them = s.OpponentOf(me);
		for (var i = 0; i < 2; i++)
			(s, _) = TestGames.Put(s, me, TestGames.Card("Plains"), ZoneType.Battlefield);
		int beach;
		(s, beach) = TestGames.Put(s, them, CardRegistry.Get("Deserted Beach"), ZoneType.Hand); // theirs, two lands of mine

		s = s.EnterBattlefield(beach, me);

		Assert.That(s.GetCard(beach).IsTapped, Is.False);
	}
}
