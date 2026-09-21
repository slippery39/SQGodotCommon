using System.Collections.Immutable;
using DoomCore;
using ImmutableGameObjects;

namespace DoomCore.Tests;

/// <summary>
/// Flood, rebuilt — the apocalypse takes the CARD for two turns.
///
/// **These exist because Flood shipped in v1 and did nothing from the day combat v3 landed.** It
/// washed the board to Discard; v3 made the board wash itself to Discard every turn. Nothing threw,
/// no test failed, and the scenario went on telling the player it had taken everything. So the
/// assertions here are about a card being UNREACHABLE and then coming BACK, never about the effect
/// having been declared.
/// </summary>
public class TakenCardTests
{
	private static (GameState State, ImmutableList<GameEvent> Events) Do(
		GameState state,
		GameAction action
	) => state.AddAction(action).ProcessAllActions();

	private static (GameState, int) AddUnit(GameState state, string name, int lane)
	{
		var card = new DoomCard
		{
			Name = name,
			Cost = 0,
			RunCardId = 7,
		};
		card = (DoomCard)
			card.WithComponent(
				new UnitComponent
				{
					Power = 2,
					Toughness = 9,
					Lane = lane,
				}
			);

		var (s, added) = state.AddObject(card, state.ZoneId(ZoneType.Field));
		return (s, added.Id);
	}

	private static GameState Battle() =>
		DoomBattleFactory.Create(DoomScenario.Flood, countdown: 1, opponentHealth: 500);

	private static bool IsReachable(GameState state, string name) =>
		new[] { ZoneType.Draw, ZoneType.Hand, ZoneType.Discard, ZoneType.Field }.Any(zone =>
			state.CardsIn(zone).Any(c => c.Name == name)
		);

	[Test]
	public void AFiringTakesTheStandingCardOutOfEveryDeck()
	{
		var state = Battle();
		(state, _) = AddUnit(state, "Committed", lane: 1);
		(state, _) = state.BeginBattle();

		(state, var events) = Do(state, new EndTurnAction());

		Assert.Multiple(() =>
		{
			Assert.That(
				IsReachable(state, "Committed"),
				Is.False,
				"taken means taken — not in draw, hand, discard or on the board. Washing it to "
					+ "Discard is what made this doom a no-op for a whole version."
			);
			Assert.That(state.CardsIn(ZoneType.Taken).Any(c => c.Name == "Committed"), Is.True);
			Assert.That(events.OfType<CardsTakenEvent>().Single().Count, Is.EqualTo(1));
		});
	}

	/// <summary>
	/// The half that makes it a cost rather than a deletion. A doom that removed cards outright
	/// could end a run on a ten-card deck — DoomJam.md records that as the reason permanent removal
	/// was thrown out in the first place.
	/// </summary>
	[Test]
	public void ItComesBackWhenTheTurnsAreUp()
	{
		var state = Battle();
		(state, _) = AddUnit(state, "Committed", lane: 1);
		(state, _) = state.BeginBattle();
		(state, _) = Do(state, new EndTurnAction());

		var takenOnTurn = state.GetBattle().TurnNumber;

		(state, _) = Do(state, new EndTurnAction());
		Assert.That(
			IsReachable(state, "Committed"),
			Is.False,
			$"still gone the turn after it was taken (turn {takenOnTurn})"
		);

		(state, var events) = Do(state, new EndTurnAction());

		Assert.Multiple(() =>
		{
			// **Reachable, not specifically in Discard.** It returns to the discard pile, and a
			// battle whose draw pile has run dry reshuffles that pile immediately — so the card
			// this test follows is usually drawn into the hand on the very turn it comes back.
			// Asserting the pile rather than the reachability would fail on a true return.
			Assert.That(
				IsReachable(state, "Committed"),
				Is.True,
				"back in circulation, two turns on"
			);
			Assert.That(state.CardsIn(ZoneType.Taken), Is.Empty);
			Assert.That(events.OfType<CardsReturnedEvent>().Any(), Is.True);
		});
	}

	[Test]
	public void ATakenCardCarriesNoReturnDateOnceItIsBack()
	{
		var state = Battle();
		(state, _) = AddUnit(state, "Committed", lane: 1);
		(state, _) = state.BeginBattle();

		for (var i = 0; i < 3; i++)
			(state, _) = Do(state, new EndTurnAction());

		var back = new[] { ZoneType.Draw, ZoneType.Hand, ZoneType.Discard }
			.SelectMany(state.CardsIn)
			.Single(c => c.Name == "Committed");

		Assert.That(
			back.HasComponent<TakenComponent>(),
			Is.False,
			"or a second firing could not stamp it honestly"
		);
	}

	[Test]
	public void TheCompanionIsNeverTaken()
	{
		var run = StarterContent.NewRun();
		var (state, _) = run.StartBattle(
			DoomScenario.Flood,
			countdown: 1,
			enemies: [],
			opponentHealth: 500
		);

		(state, _) = Do(state, new EndTurnAction());

		Assert.That(
			state.Units().Any(u => u.HasComponent<CompanionComponent>()),
			Is.True,
			"no doom may touch it, and Flood is where that rule was written"
		);
	}
}
