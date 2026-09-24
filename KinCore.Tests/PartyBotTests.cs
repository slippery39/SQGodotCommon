using ImmutableGameObjects;
using KinCore;
using KinCore.Party;

namespace KinCore.Tests;

/// <summary>
/// **The sim's bot FIRES** — it steps out of a blow, plays a card, throws a Snare, and a whole run
/// finishes. An inert bot would report a win rate of nothing and look like a balance finding.
/// </summary>
public class PartyBotTests
{
	private static Intent Hit(int amount) =>
		new()
		{
			Name = "Hit",
			Kind = IntentType.Attack,
			Amount = amount,
		};

	private static PartyCompanion Mon(params Intent[] moves) => new("Mon", 20, 0, 1, [.. moves]);

	private static Foe Foe(int space, int hp, params Intent[] pattern) =>
		new()
		{
			Name = "Foe",
			Hp = hp,
			MaxHp = hp,
			Speed = 3,
			Space = space,
			Pattern = [.. pattern],
		};

	private static GameState Battle(
		PartyCompanion mon,
		int space,
		KinCard[] deck,
		params Foe[] foes
	) =>
		PartyBattleFactory.Create(
			new PartyScenario("Test", "", [new(mon, space)], [.. foes], [.. deck], [], Snares: 1)
		);

	[Test]
	public void ItStepsIntoAFoesColumnToHitIt()
	{
		var s = Battle(
			Mon(Hit(5)),
			2,
			[],
			Foe(3, 50, new Intent { Name = "Idle", Kind = IntentType.Block })
		);

		s = PartyBot.PlayTurn(s);

		Assert.That(s.LivingFoes().Single().Hp, Is.EqualTo(45));
	}

	[Test]
	public void ItFindsATwoPlayComboThatNeitherPlayWinsAlone()
	{
		// Its free step is spent, so reaching the foe takes Dash THEN a step — Dash alone gains nothing.
		var dash = new KinCard
		{
			Name = "Dash",
			Cost = 0,
			Effects = [new KinEffect { Template = new DashAction(), Text = "Dash" }],
		};
		var idle = new Intent { Name = "Idle", Kind = IntentType.Block };
		var s = Battle(Mon(Hit(5)), 2, [dash], Foe(3, 50, idle));
		var mon = s.LivingAllies().Single();
		s = s.UpdateObject(mon.Id, mon with { StepsLeft = 0 });

		s = PartyBot.PlayTurn(s);

		Assert.That(s.LivingFoes().Single().Hp, Is.EqualTo(45));
	}

	[Test]
	public void ItLeavesACatchableFoeAliveRatherThanKillIt()
	{
		// Snareable at 10 of 30. Its hit (5) would kill a foe at 4 — the bot holds it back instead. A
		// second foe, so the kill is not a WIN (a win outranks any catch, rightly).
		var idle = new Intent { Name = "Idle", Kind = IntentType.Block };
		var s = Battle(Mon(Hit(5)), 2, [], Foe(2, 30, idle), Foe(4, 50, idle));
		var foe = s.LivingFoes().First();
		s = s.UpdateObject(foe.Id, foe with { Hp = 4 });
		s = s.UpdateObject(s.GetParty().Id, s.GetParty() with { Energy = 0 });

		s = PartyBot.PlayTurn(s);

		Assert.That(s.LivingFoes().First().Hp, Is.EqualTo(4), "it stepped out of line");
	}

	[Test]
	public void ItThrowsASnareAtAWeakFoe()
	{
		var s = Battle(Mon(Hit(1)), 2, [], Foe(2, 30, Hit(1)));
		var foe = s.LivingFoes().Single();
		s = s.UpdateObject(foe.Id, foe with { Hp = 5 });

		s = PartyBot.PlayTurn(s);

		Assert.That(s.CaughtFoes(), Is.Not.Empty);
	}

	[Test]
	public void ARealRunFinishes()
	{
		var run = PartySim.PlayRun(PartyContent.Pike, seed: 7);

		Assert.That(run.Battles, Is.GreaterThan(0));
		Assert.That(run.End, Is.Not.EqualTo(RunEnd.Stalled));
	}
}
