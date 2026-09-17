using System.Collections.Immutable;
using DoomCore;
using ImmutableGameObjects;

namespace DoomCore.Tests;

/// <summary>
/// The doom fires repeatedly, and each firing reads the board as it was AT THAT MOMENT.
///
/// These guard the reason <see cref="DoomFiring"/> exists at all. A transform applied once at the
/// end, or one reading a cumulative list, both look correct until a battle runs long.
/// </summary>
public class RecurringDoomTests
{
	private static (GameState State, ImmutableList<GameEvent> Events) Do(
		GameState state,
		GameAction action
	) => state.AddAction(action).ProcessAllActions();

	private static Enemy Killer(int lane, int attack) =>
		new()
		{
			Name = "Crusher",
			Health = 500,
			MaxHealth = 500,
			Intent = IntentKind.Attack,
			IntentAmount = attack,
			Lane = lane,
		};

	/// <summary>
	/// A death is paid for ONCE, by the firing that read it. If `DiedRunCardIds` were left on the
	/// battle instead of consumed, every later firing would mint a Zombie for the same corpse and a
	/// long battle would end in an exponential pile of them.
	/// </summary>
	[Test]
	public void ADeathIsPaidForByExactlyOneFiring()
	{
		var run = new Run { Life = 200, MaxLife = 200 }.WithCards(
			[
				new RunCard
				{
					Name = "Fragile",
					Cost = 0,
					IsUnit = true,
					Power = 0,
					Toughness = 1,
				},
			]
		);

		// Countdown 1, so the doom fires at the end of every turn.
		var (state, _) = run.StartBattle(
			DoomScenario.Zombie,
			countdown: 1,
			[Killer(lane: 0, attack: 9)],
			opponentHealth: 500
		);

		// Turn 1: stand Fragile in front of the Crusher. It dies.
		var fragile = state.CardsIn(ZoneType.Hand).First(c => c.Name == "Fragile");
		(state, _) = Do(state, new PlayCardAction { CardId = fragile.Id, Lane = 0 });
		(state, _) = Do(state, new EndTurnAction());

		// Turns 2 and 3: play nothing, so nothing dies.
		(state, _) = Do(state, new EndTurnAction());
		(state, _) = Do(state, new EndTurnAction());

		var firings = state.GetBattle().Firings;
		Assert.That(firings, Has.Count.EqualTo(3), "one firing per turn at countdown 1");
		Assert.That(firings[0].DiedRunCardIds, Has.Count.EqualTo(1), "it died before firing 1");
		Assert.That(firings[1].DiedRunCardIds, Is.Empty, "and must not be paid for again");
		Assert.That(firings[2].DiedRunCardIds, Is.Empty);

		var after = run.AfterBattle(state);

		Assert.That(
			after.Deck.Count(c => c.Name == "Zombie"),
			Is.EqualTo(1),
			"one corpse, one Zombie, however many apocalypses passed over it"
		);
	}

	/// <summary>
	/// Nuclear reads what was STANDING. Two firings over two different boards must irradiate each
	/// board as it was — not the final board twice, which is what a single end-of-battle read gives.
	/// </summary>
	[Test]
	public void EachFiringIrradiatesTheBoardItActuallySaw()
	{
		var run = new Run { Life = 200, MaxLife = 200 }.WithCards(
			[
				new RunCard
				{
					Name = "First",
					Cost = 0,
					IsUnit = true,
					Power = 0,
					Toughness = 9,
				},
				new RunCard
				{
					Name = "Second",
					Cost = 0,
					IsUnit = true,
					Power = 0,
					Toughness = 9,
				},
			]
		);

		var (state, _) = run.StartBattle(
			DoomScenario.Nuclear,
			countdown: 1,
			[],
			opponentHealth: 500
		);

		// Firing 1 sees only First.
		var first = state.CardsIn(ZoneType.Hand).First(c => c.Name == "First");
		(state, _) = Do(state, new PlayCardAction { CardId = first.Id, Lane = 0 });
		(state, _) = Do(state, new EndTurnAction());

		// Firing 2 sees both.
		var second = state.CardsIn(ZoneType.Hand).First(c => c.Name == "Second");
		(state, _) = Do(state, new PlayCardAction { CardId = second.Id, Lane = 1 });
		(state, _) = Do(state, new EndTurnAction());

		var firings = state.GetBattle().Firings;
		Assert.That(firings[0].OnFieldRunCardIds, Has.Count.EqualTo(1));
		Assert.That(
			firings[1].OnFieldRunCardIds,
			Has.Count.EqualTo(1),
			"**Combat v3**: the second firing sees what you committed on ITS turn, not an "
				+ "accumulated pile. First withdrew at the end of turn 1 and was not standing."
		);
		Assert.That(
			firings[0].OnFieldRunCardIds,
			Is.Not.EqualTo(firings[1].OnFieldRunCardIds),
			"and the two firings read different boards, which is why DoomFiring exists"
		);

		var after = run.AfterBattle(state);

		// Each stood through exactly one firing, so each took the buff once. Before v3 First was
		// still standing at the second firing and took it twice — the thing that changed is what a
		// board IS, not how firings are counted.
		// The COUNT is what this test is about, so the amount is read rather than restated.
		var buff = DoomTransforms.IrradiatedBuff;
		Assert.That(after.Deck.Single(c => c.Name == "First").Toughness, Is.EqualTo(9 + buff));
		Assert.That(after.Deck.Single(c => c.Name == "Second").Toughness, Is.EqualTo(9 + buff));
	}

	/// <summary>
	/// **The guard on the whole of Combat v3, and it will not announce itself if it breaks.**
	///
	/// `EndTurnAction` SPAWNS `ResolveDoomAction` rather than resolving it inline, and the spawn
	/// queue is FIFO. So withdrawing units anywhere inside `EndTurnAction.Execute` would empty the
	/// field BEFORE the apocalypse read it — and all six scenarios that read `FiringRead.Standing`
	/// would quietly do nothing while looking exactly like scenarios that worked.
	///
	/// If this ever fails, do not fix the assertion: `WithdrawUnitsAction` has been moved ahead of
	/// the doom.
	/// </summary>
	[Test]
	public void ADoomFiringSeesTheBoardYouCommitted()
	{
		var run = new Run { Life = 200, MaxLife = 200 }.WithCards(
			[
				new RunCard
				{
					Name = "Committed",
					Cost = 0,
					IsUnit = true,
					Power = 0,
					Toughness = 9,
				},
			]
		);

		var (state, _) = run.StartBattle(
			DoomScenario.Nuclear,
			countdown: 1,
			[],
			opponentHealth: 500
		);

		var card = state.CardsIn(ZoneType.Hand).First(c => c.Name == "Committed");
		(state, _) = Do(state, new PlayCardAction { CardId = card.Id, Lane = 0 });
		(state, _) = Do(state, new EndTurnAction());

		var firing = state.GetBattle().Firings.Single();

		Assert.That(
			firing.OnFieldRunCardIds,
			Is.Not.Empty,
			"the apocalypse read an EMPTY board — withdrawal is running before the doom"
		);
		Assert.That(firing.OnFieldRunCardIds, Does.Contain(card.RunCardId));

		// And it really landed on the deck, not just on the record of what it saw.
		var after = run.AfterBattle(state);
		Assert.That(
			after.Deck.Single(c => c.Name == "Committed").Toughness,
			Is.EqualTo(9 + DoomTransforms.IrradiatedBuff)
		);
	}

	/// <summary>
	/// A unit the APOCALYPSE killed died, and was not quietly swept away as a withdrawal.
	///
	/// The dead are cleared early in `EndTurnAction`, and the doom resolves after that — so a unit
	/// the firing killed is still standing when `WithdrawUnitsAction` runs. Withdrawing it would
	/// move it to Discard as a survivor: no `OnDeath`, nothing in `DiedRunCardIds`, and Zombie
	/// paid nothing for a corpse it is owed. Hence the second clear-the-dead pass.
	/// </summary>
	[Test]
	public void AUnitTheApocalypseKilledCountsAsADeath()
	{
		var run = new Run { Life = 200, MaxLife = 200 }.WithCards(
			[
				new RunCard
				{
					Name = "Frail",
					Cost = 0,
					IsUnit = true,
					Power = 0,
					Toughness = 1,
				},
			]
		);

		// Ashfall chips every unit you hold when it lands, which is lethal to a 0/1.
		var (state, _) = run.StartBattle(
			DoomScenario.Ashfall,
			countdown: 1,
			[],
			opponentHealth: 500
		);

		var card = state.CardsIn(ZoneType.Hand).First(c => c.Name == "Frail");
		(state, _) = Do(state, new PlayCardAction { CardId = card.Id, Lane = 0 });
		(state, var events) = Do(state, new EndTurnAction());

		Assert.That(
			events.OfType<UnitDiedEvent>().Any(e => e.CardId == card.Id),
			Is.True,
			"the apocalypse killed it, so it DIED — it did not withdraw"
		);
		Assert.That(state.GetBattle().DiedRunCardIds, Does.Contain(card.RunCardId));
	}

	/// <summary>
	/// The dial says what the NEXT firing would do, not what the recorded ones already did — and it
	/// uses the same capture the real firing uses, so it cannot disagree with the apocalypse.
	/// </summary>
	[Test]
	public void ThePreviewStillDescribesTheNextFiringAfterOneHasAlreadyLanded()
	{
		var run = new Run { Life = 200, MaxLife = 200 }.WithCards(
			[
				new RunCard
				{
					Name = "Exposed",
					Cost = 0,
					IsUnit = true,
					Power = 0,
					Toughness = 9,
				},
			]
		);

		var (state, _) = run.StartBattle(
			DoomScenario.Nuclear,
			countdown: 1,
			[],
			opponentHealth: 500
		);

		var card = state.CardsIn(ZoneType.Hand).First(c => c.Name == "Exposed");
		(state, _) = Do(state, new PlayCardAction { CardId = card.Id, Lane = 0 });
		(state, _) = Do(state, new EndTurnAction());

		Assert.That(state.GetBattle().DoomsFired, Is.EqualTo(1), "one has landed");

		// **Combat v3: the board is empty at the start of a turn, so the dial has to be asked
		// about a board you have actually committed.** Re-place the card before previewing — the
		// preview answers "what would the next firing do to what is standing NOW", and what is
		// standing now is whatever you just played.
		var replayed = state.CardsIn(ZoneType.Hand).First(c => c.Name == "Exposed");
		(state, _) = Do(state, new PlayCardAction { CardId = replayed.Id, Lane = 0 });

		var preview = DoomPreviewer.Preview(run, state);

		Assert.That(
			preview.Changed,
			Has.Count.EqualTo(1),
			"the dial still answers 'what would the next one do', not 'what did the last one do'"
		);
		Assert.That(preview.Summary, Does.Contain("1 changed"));
	}
}
