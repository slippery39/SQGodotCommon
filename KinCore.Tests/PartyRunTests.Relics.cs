using ImmutableGameObjects;
using KinCore.Party;

namespace KinCore.Tests;

/// <summary>
/// **RELICS — an elite pays one, and each FIRES** (`PartyRelics`; `KinFamiliesPlan.md`, round 2).
/// Amounts are read from `PartyRelics`, never restated. Uses the run fixtures in `PartyRunTests.cs`.
/// </summary>
public partial class PartyRunTests
{
	private static PartyRun Holding(PartyRun run, params Relic[] relics) =>
		relics.Aggregate(run, (r, relic) => r.Gain(relic));

	/// <summary>A run's battle at its first fight, before FIGHT is pressed.</summary>
	private static GameState Dealt(PartyRun run) => OnFights(run, Fight(Foe("W"))).StartBattle();

	private static GameState Fought(GameState s) =>
		s.AddAction(new BeginFightAction()).ProcessAllActions().State;

	[Test]
	public void AnElitePaysARelicYouDoNotHold()
	{
		var run = OnRoute(Holding(Run(), Relic.Whetstone), Place(NodeKind.Elite, Fight(Foe("E"))))
			.MoveTo(1);

		(var after, var report) = WinNext(run);

		Assert.That(report.Relic, Is.Not.Null.And.Not.EqualTo(Relic.Whetstone));
		Assert.That(after.Relics, Has.Count.EqualTo(2).And.Contains(report.Relic!.Value));
	}

	[Test]
	public void WhetstoneLanternAndQuickBootsFireAtTheDeal()
	{
		var deck = Run() with { Deck = [.. Enumerable.Repeat(Wipe("Wipe"), 12)] };
		var plain = Dealt(deck);
		var held = Dealt(Holding(deck, Relic.Whetstone, Relic.Lantern, Relic.QuickBoots));

		Assert.That(
			held.Allies().Single().Power - plain.Allies().Single().Power,
			Is.EqualTo(PartyRelics.WhetstonePower)
		);
		Assert.That(
			held.GetParty().Energy - plain.GetParty().Energy,
			Is.EqualTo(PartyRelics.LanternEnergy)
		);
		Assert.That(
			held.CardsIn(ZoneType.Hand).Count() - plain.CardsIn(ZoneType.Hand).Count(),
			Is.EqualTo(PartyRelics.QuickBootsDraw)
		);
	}

	[Test]
	public void IronShellShieldsWhoeverIsInFrontWhenTheFightBegins()
	{
		var s = Fought(Dealt(Holding(WithTeam(Run(), A, B), Relic.IronShell)));

		Assert.That(s.AllyAt(0)!.Block, Is.EqualTo(PartyRelics.IronShellBlock));
		Assert.That(s.AllyAt(1)!.Block, Is.Zero);
	}

	[Test]
	public void FieldKitHealsAndLuckyCoinPaysMoreAfterAWin()
	{
		var hurt = Run() with { Team = [new RunCompanion(A, 10)] };

		var (plain, plainReport) = WinNext(OnFights(hurt, Fight(Foe("W"))));
		var (held, heldReport) = WinNext(
			OnFights(Holding(hurt, Relic.FieldKit, Relic.LuckyCoin), Fight(Foe("W")))
		);

		Assert.That(Hp(held, "A") - Hp(plain, "A"), Is.EqualTo(PartyRelics.FieldKitHeal));
		Assert.That(
			heldReport.Gold,
			Is.EqualTo((int)(plainReport.Gold * PartyRelics.LuckyCoinGold))
		);
	}

	[Test]
	public void SnarePouchPaysNowAndAtEveryTown()
	{
		var run = Run();
		var held = run.Gain(Relic.SnarePouch);
		Assert.That(held.Snares - run.Snares, Is.EqualTo(PartyRelics.SnarePouchNow));

		var (inTown, _) = WinNext(OnRoute(held).MoveTo(1));

		Assert.That(inTown.Phase, Is.EqualTo(RunPhase.Town));
		Assert.That(inTown.Snares - held.Snares, Is.EqualTo(1));
	}

	[Test]
	public void TrainersEyeOffersMoreCards()
	{
		var run = Run() with
		{
			Rewards = [Wipe("P1"), Wipe("P2"), Wipe("P3"), Wipe("P4"), Wipe("P5")],
		};

		Assert.That(run.RewardOffer(), Has.Count.EqualTo(3));
		Assert.That(
			run.Gain(Relic.TrainersEye).RewardOffer(),
			Has.Count.EqualTo(PartyRelics.TrainersEyeOffer)
		);
	}
}
