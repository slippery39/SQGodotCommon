using System.Collections.Generic;
using System.Linq;
using ImmutableGameObjects;
using MagicCore;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Sets;
using MagicCore.Sets.Fra;
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
		Assert.That(
			Targeting.NextIndex(after6, 1),
			Is.EqualTo(1),
			"one legal target is still clicked"
		);
		Assert.That(
			Targeting.NextIndex(Targeting.Narrow(after6, 1, 7), 2),
			Is.EqualTo(-1),
			"all clicked: cast it"
		);
	}

	[Test]
	public void ACostsPick_IsClickedAfterTheTargets_AndSaysWhatItIs()
	{
		List<GameAction> moves =
		[
			new ActivateAbilityAction
			{
				PlayerId = 1,
				CardId = 10,
				Targets = [5],
				SacrificeId = 7,
			},
		];

		Assert.That(Targeting.NextIndex(moves, 0), Is.EqualTo(0));
		Assert.That(
			Targeting.NextIndex(moves, 1),
			Is.EqualTo(1),
			"the only creature to sacrifice is still clicked"
		);
		Assert.That(Targeting.What(moves[0], 0), Is.EqualTo("target 1"));
		Assert.That(Targeting.What(moves[0], 1), Is.EqualTo("what to sacrifice"));
		Assert.That(Targeting.Options(moves, 1), Is.EqualTo(new[] { 7 }));
	}

	[Test]
	public void StationsCreatureToTap_IsClicked_AndSaysWhatItIs()
	{
		List<GameAction> moves =
		[
			new ActivateAbilityAction
			{
				PlayerId = 1,
				CardId = 10,
				TapId = 5,
			},
			new ActivateAbilityAction
			{
				PlayerId = 1,
				CardId = 10,
				TapId = 6,
			},
		];

		Assert.That(Targeting.NextIndex(moves, 0), Is.EqualTo(0), "one play, then a click");
		Assert.That(Targeting.What(moves[0], 0), Is.EqualTo("what to tap"));
		Assert.That(Targeting.Options(moves, 0), Is.EquivalentTo(new[] { 5, 6 }));
	}

	[Test]
	public void TapThree_IsASet_ClickedInAnyOrder()
	{
		static GameAction Taps(params int[] ids) =>
			new ActivateAbilityAction
			{
				PlayerId = 1,
				CardId = 10,
				TapIds = [.. ids],
			};
		List<GameAction> moves = [Taps(5, 6, 7), Taps(5, 6, 8), Taps(5, 7, 8), Taps(6, 7, 8)];

		Assert.That(Targeting.Options(moves, 0), Is.EquivalentTo(new[] { 5, 6, 7, 8 }));
		var after8 = Targeting.Narrow(moves, 0, 8);
		Assert.That(Targeting.Options(after8, 1), Is.EquivalentTo(new[] { 5, 6, 7 }));
		var after5 = Targeting.Narrow(after8, 1, 5);
		Assert.That(Targeting.Options(after5, 2), Is.EquivalentTo(new[] { 6, 7 }));
		var last = Targeting.Narrow(after5, 2, 7);
		Assert.That(last, Has.Count.EqualTo(1));
		Assert.That(((ActivateAbilityAction)last[0]).TapIds, Is.EquivalentTo(new[] { 5, 7, 8 }));
	}

	[Test]
	public void SacrificeOrPay_PickDifferentNumbers_AndAreListed()
	{
		List<GameAction> moves =
		[
			new CastSpellAction
			{
				PlayerId = 1,
				CardId = 10,
				SacrificeId = 7,
			},
			new CastSpellAction { PlayerId = 1, CardId = 10 },
		];

		Assert.That(Targeting.NextIndex(moves, 0), Is.EqualTo(-1));
	}

	[Test]
	public void SilenceTheEcho_ThroughTheEngine_IsTwoWays_ThenClicks()
	{
		var m = new MagicTableManager(FraDecks.All["White-Black"], FraDecks.All["Red-Green"], 5);
		m.AutoplayTo(9); // seed 5: your main phase
		while (m.StepAi() is not null) { }
		foreach (
			var (name, zone) in new[]
			{
				("Rank Rat", ZoneType.Battlefield),
				("Forest", ZoneType.Battlefield),
				("Forest", ZoneType.Battlefield),
				("Silence the Echo", ZoneType.Hand),
			}
		)
		{
			while (!m.CanSandbox && m.StepAi() is not null) { }
			m.Sandbox(m.HumanId, CardRegistry.Get(name), zone);
		}
		while (m.StepAi() is not null) { }
		var s = m.State;
		var silence = s.CardsIn(m.HumanId, ZoneType.Hand)
			.Single(c => c.Name == "Silence the Echo")
			.Id;
		var moves = s.DistinctMoves(s.LegalActions()).Where(a => s.IsMoveFor(a, silence));

		var plays = Targeting.Plays(s, moves);
		Assert.That(plays, Has.Count.EqualTo(1), "one play, cast two ways");
		Assert.That(
			Targeting.NextIndex(plays[0], 0),
			Is.EqualTo(-1),
			"the ways pick different numbers"
		);
		var ways = Targeting.Ways(s, plays[0]);

		Assert.That(
			ways.Select(w => w.Label),
			Is.EquivalentTo(
				new[]
				{
					"Cast Silence the Echo, sacrificing",
					"Cast Silence the Echo, paying instead",
				}
			)
		);
		var sacrificing = ways.Single(w => w.Label.EndsWith("sacrificing")).Moves;
		Assert.That(
			Targeting.NextIndex(sacrificing, 0),
			Is.EqualTo(0),
			"then the target is clicked"
		);
		Assert.That(Targeting.What(sacrificing[0], 1), Is.EqualTo("what to sacrifice"));
	}

	[Test]
	public void AModalSpell_ListsEveryMode_TheImpossibleOneGreyedWithWhy()
	{
		var m = new MagicTableManager(FraDecks.All["White-Black"], FraDecks.All["Red-Green"], 5);
		m.AutoplayTo(3);
		while (m.StepAi() is not null) { }
		foreach (
			var (name, zone) in new[]
			{
				("Plains", ZoneType.Battlefield),
				("Plains", ZoneType.Battlefield),
				("Surgical Precision", ZoneType.Hand),
			}
		)
		{
			while (!m.CanSandbox && m.StepAi() is not null) { }
			m.Sandbox(m.HumanId, CardRegistry.Get(name), zone);
		}
		while (m.StepAi() is not null) { }
		var s = m.State;
		var precision = s.CardsIn(m.HumanId, ZoneType.Hand)
			.Single(c => c.Name == "Surgical Precision")
			.Id;
		var spell = s.GetCard(precision).GetComponent<SpellEffectsComponent>()!;
		Assert.That(
			spell.Modes[0].Targets[0].Candidates(s, m.HumanId, precision), // the script's own spec
			Is.Empty,
			"precondition: no creature the destroy mode could target"
		);
		var moves = s.DistinctMoves(s.LegalActions())
			.Where(a => s.IsMoveFor(a, precision))
			.ToList();
		Assert.That(moves, Is.Not.Empty, "precondition: castable");

		var modes = Targeting.Modes(s, precision, moves)!;

		Assert.That(
			modes,
			Has.Count.EqualTo(spell.Modes.Count),
			"every mode, not just the legal one"
		);
		Assert.That(modes[0].Moves, Is.Empty);
		Assert.That(modes[0].Label, Does.Contain("(no legal targets)"));
		Assert.That(modes[1].Moves, Is.Not.Empty);
		Assert.That(modes[1].Label, Is.EqualTo(Describe.Modes(spell.Modes, [1])));
	}

	[Test]
	public void WaysThatDifferOtherwise_AreListedInFull_NotAsPaying()
	{
		var m = new MagicTableManager(FraDecks.All["White-Black"], FraDecks.All["Red-Green"], 5);
		var (s, card) = (m.State, m.State.CardsIn(m.HumanId, ZoneType.Hand).First().Id);
		List<GameAction> moves =
		[
			new CastSpellAction
			{
				PlayerId = m.HumanId,
				CardId = card,
				Targets = [5],
			},
			new CastSpellAction
			{
				PlayerId = m.HumanId,
				CardId = card,
				Targets = [5, 6],
			},
		];

		var ways = Targeting.Ways(s, moves);

		Assert.That(ways, Has.Count.EqualTo(2));
		Assert.That(ways.Select(w => w.Label), Has.None.Contains("paying"));
	}

	[Test]
	public void SacrificeOrPay_IsListedOncePerWay_NotPerTargetAndSacrifice()
	{
		var m = new MagicTableManager(FraDecks.All["White-Black"], FraDecks.All["Red-Green"], 5);
		var (s, card) = (m.State, m.State.CardsIn(m.HumanId, ZoneType.Hand).First().Id);
		GameAction Silence(int target, int sacrifice = 0) =>
			new CastSpellAction
			{
				PlayerId = m.HumanId,
				CardId = card,
				Targets = [target],
				SacrificeId = sacrifice,
			};
		List<GameAction> moves =
		[
			Silence(5, 7),
			Silence(5, 8),
			Silence(6, 7),
			Silence(5),
			Silence(6),
		];

		var ways = Targeting.Ways(s, moves);

		Assert.That(ways, Has.Count.EqualTo(2));
		Assert.That(ways[0].Label, Does.EndWith(", sacrificing"));
		Assert.That(ways[0].Moves, Has.Count.EqualTo(3));
		Assert.That(ways[1].Label, Does.EndWith(", paying instead"));
	}

	[Test]
	public void ASingleLegalTarget_IsStillClicked_ASpellWithoutTargetsIsNot()
	{
		Assert.That(Targeting.NextIndex([Cast(5)], 0), Is.EqualTo(0));
		Assert.That(Targeting.NextIndex([Cast()], 0), Is.EqualTo(-1));
	}

	[Test]
	public void MovesThatDifferByTargetCount_AreListedNotClicked()
	{
		List<GameAction> moves = [Cast(5), Cast(6, 7)]; // the first target differs, yet the counts do too

		Assert.That(Targeting.NextIndex(moves, 0), Is.EqualTo(-1));
	}

	[Test]
	public void ASameFirstTarget_IsClickedToo_ThenTheSecond()
	{
		List<GameAction> moves = [Cast(5, 7), Cast(5, 8)];

		Assert.That(Targeting.NextIndex(moves, 0), Is.EqualTo(0));
		Assert.That(Targeting.NextIndex(Targeting.Narrow(moves, 0, 5), 1), Is.EqualTo(1));
		Assert.That(moves.Select(Targeting.PicksOf).All(t => t.Count == 2));
	}
}
