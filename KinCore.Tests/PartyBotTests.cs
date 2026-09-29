using ImmutableGameObjects;
using KinCore;
using KinCore.Party;

namespace KinCore.Tests;

/// <summary>
/// **The sim's bot FIRES** — it plays a card, finds a combo, throws a Snare, and a whole run
/// finishes. An inert bot would report a win rate of nothing and look like a balance finding.
/// </summary>
public class PartyBotTests
{
	private static readonly Intent Idle = new() { Name = "Idle", Kind = IntentType.Block };

	private static Intent Hit(int amount) =>
		new()
		{
			Name = "Hit",
			Kind = IntentType.Attack,
			Amount = amount,
		};

	private static PartyCompanion Mon(params Intent[] moves) => new("Mon", 20, 0, [.. moves]);

	private static Foe Foe(int position, int hp, params Intent[] pattern) =>
		new()
		{
			Name = "Foe",
			Hp = hp,
			MaxHp = hp,
			Position = position,
			Pattern = [.. pattern],
		};

	private static KinCard Card(string name, CardStep step) =>
		new()
		{
			Name = name,
			Cost = 1,
			Effects = [new KinEffect { Template = step, Text = name }],
		};

	private static GameState Battle(PartyCompanion mon, KinCard[] deck, params Foe[] foes) =>
		PartyBattleFactory.Create(
			new PartyScenario("Test", "", [new(mon, 0)], [.. foes], [.. deck], [])
		);

	[Test]
	public void ItPlaysACardThatDealsDamage()
	{
		var s = Battle(
			Mon(Idle),
			[Card("Strike", new StrikeAction { Amount = 5 })],
			Foe(0, 50, Idle)
		);

		s = PartyBot.PlayTurn(s);

		Assert.That(s.LivingFoes().Single().Hp, Is.EqualTo(45));
	}

	[Test]
	public void ItFindsATwoPlayComboThatNeitherPlayWinsAlone()
	{
		// Rally alone deals nothing (its monster idles); Strike alone leaves the foe at 3. Both win.
		var s = Battle(
			Mon(Idle),
			[
				Card("Rally", new PowerAction { Amount = 3 }),
				Card("Strike", new StrikeAction { Amount = 3 }),
			],
			Foe(0, 6, Idle)
		);

		s = PartyBot.PlayTurn(s);

		Assert.That(s.GetParty().IsOver && s.GetParty().Won, Is.True);
	}

	[Test]
	public void ARealRunFinishes()
	{
		var run = PartySim.PlayRun(PartyContent.Pike, seed: 7);

		Assert.That(run.Battles, Is.GreaterThan(0));
		Assert.That(run.End, Is.Not.EqualTo(RunEnd.Stalled));
	}
}
