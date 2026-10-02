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

	/// <summary>A run's battle at its first fight, dealt and begun.</summary>
	private static GameState Dealt(PartyRun run) => OnFights(run, Fight(Foe("W"))).StartBattle();

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
		var s = Dealt(Holding(WithTeam(Run(), A, B), Relic.IronShell));

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

	// ===== A BOSS's prizes: half healed in the next town, and one of three BOSS relics

	[Test]
	public void ABossHealsYouInTheNextTownAndOffersThreeBossRelics()
	{
		var run = OnRoute(Run() with { Team = [new RunCompanion(A, 5)] }).MoveTo(1);

		var (town, _) = WinNext(run);

		Assert.That(town.Phase, Is.EqualTo(RunPhase.Town));
		var max = town.Team[0].MaxHp;
		Assert.That(
			Hp(town, "A"),
			Is.EqualTo(Math.Min(max, 5 + (int)Math.Ceiling(max * PartyRun.BossHeal))),
			"healed by half, not in full — the hospital has something to do"
		);
		Assert.That(town.RelicChoice, Has.Count.EqualTo(3));
		Assert.That(town.RelicChoice, Is.SubsetOf(PartyRelics.Boss));

		var pick = town.RelicChoice[1];
		var chosen = town.ChooseRelic(pick);
		Assert.That(chosen.Relics, Does.Contain(pick));
		Assert.That(chosen.RelicChoice, Is.Empty, "the other two are gone");
		Assert.That(town.EnterRoute().RelicChoice, Is.Empty, "and they do not follow you out");
	}

	[Test]
	public void AnEliteNeverPaysABossRelic()
	{
		Assert.That(PartyRelics.All.Intersect(PartyRelics.Boss), Is.Empty);
	}

	[Test]
	public void WarDrumAncientLensAndWarbandBannerFireEveryTurn()
	{
		var deck = Run() with { Deck = [.. Enumerable.Repeat(Wipe("Wipe"), 20)] };
		var plain = Dealt(deck);
		var held = Dealt(Holding(deck, Relic.WarDrum, Relic.AncientLens, Relic.WarbandBanner));

		Assert.That(
			held.Allies().Single().Power - plain.Allies().Single().Power,
			Is.EqualTo(PartyRelics.WarbandBannerPower)
		);
		plain = Do(plain, new EndPartyTurnAction());
		held = Do(held, new EndPartyTurnAction());
		Assert.That(
			held.GetParty().Energy - plain.GetParty().Energy,
			Is.EqualTo(PartyRelics.WarDrumEnergy),
			"a later turn's energy"
		);
		Assert.That(
			held.CardsIn(ZoneType.Hand).Count() - plain.CardsIn(ZoneType.Hand).Count(),
			Is.EqualTo(PartyRelics.AncientLensDraw),
			"a later turn's draw"
		);
	}

	[Test]
	public void KinTotemMakesTheFirstCardEachTurnFree()
	{
		var deck = Run() with { Deck = [Wipe("Wipe") with { Cost = 2 }] };
		var plain = Dealt(deck);
		var held = Dealt(Holding(deck, Relic.KinTotem));

		Assert.That(plain.CostOf(plain.CardsIn(ZoneType.Hand).Single()), Is.EqualTo(2));
		Assert.That(held.CostOf(held.CardsIn(ZoneType.Hand).Single()), Is.Zero);
	}
}
