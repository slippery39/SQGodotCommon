using System.Collections.Generic;
using System.Linq;
using ImmutableGameObjects;
using MagicCore.Actions;
using MagicTable;
using NUnit.Framework;

namespace SQGodotCommon.Tests;

public class TargetingTests
{
	static GameAction Cast(params int[] targets) =>
		new CastSpellAction
		{
			PlayerId = 1,
			CardId = 10,
			Targets = [.. targets],
		};

	[Test]
	public void OneTarget_ClickNarrowsToTheMove()
	{
		List<GameAction> moves = [Cast(5), Cast(6)];

		Assert.That(Targeting.NextIndex(moves, 0), Is.EqualTo(0));
		Assert.That(Targeting.Options(moves, 0), Is.EquivalentTo(new[] { 5, 6 }));
		Assert.That(Targeting.Narrow(moves, 0, 6), Is.EqualTo(new[] { moves[1] }));
	}

	[Test]
	public void TwoTargets_AreClickedInOrder_SkippingAnIndexThatNoLongerDiffers()
	{
		List<GameAction> moves = [Cast(5, 7), Cast(5, 8), Cast(6, 7)];

		Assert.That(Targeting.NextIndex(moves, 0), Is.EqualTo(0));
		var after5 = Targeting.Narrow(moves, 0, 5);
		Assert.That(Targeting.NextIndex(after5, 1), Is.EqualTo(1));
		Assert.That(Targeting.Options(after5, 1), Is.EquivalentTo(new[] { 7, 8 }));

		var after6 = Targeting.Narrow(moves, 0, 6);
		Assert.That(Targeting.NextIndex(after6, 1), Is.EqualTo(-1), "one move left: cast it");
	}

	[Test]
	public void MovesThatDifferByTargetCount_AreListedNotClicked()
	{
		List<GameAction> moves = [Cast(5), Cast(6, 7)]; // the first target differs, yet the counts do too

		Assert.That(Targeting.NextIndex(moves, 0), Is.EqualTo(-1));
	}

	[Test]
	public void ASameFirstTarget_MovesOnToTheSecond()
	{
		List<GameAction> moves = [Cast(5, 7), Cast(5, 8)];

		Assert.That(Targeting.NextIndex(moves, 0), Is.EqualTo(1));
		Assert.That(moves.Select(Targeting.TargetsOf).All(t => t.Length == 2));
	}
}
