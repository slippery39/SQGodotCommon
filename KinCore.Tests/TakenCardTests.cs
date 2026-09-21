using System.Collections.Immutable;
using KinCore;
using ImmutableGameObjects;

namespace KinCore.Tests;

/// <summary>
/// `TakeCardsAction` — something takes the CARD, out of every deck, for a number of turns.
///
/// **These assert reachability, never that the effect was declared**, and that is the whole point
/// of them. The action shipped inside Flood, which had done precisely nothing since combat v3
/// landed: it washed the board to Discard, and v3 made the board wash itself to Discard at the end
/// of every turn. Nothing threw and no test failed while the scenario told the player it had taken
/// everything.
///
/// **The dooms are gone and this primitive is deliberately kept** — "a boss that takes your cards
/// for two turns" is the shape it was really for. It has no caller in content today, so these
/// tests are the only thing standing between it and silent rot. Drive it DIRECTLY: routing them
/// through whatever content happens to use it is how the no-op hid for a whole version.
/// </summary>
public class TakenCardTests
{
	private static (GameState State, ImmutableList<GameEvent> Events) Do(
		GameState state,
		GameAction action
	) => state.AddAction(action).ProcessAllActions();

	private static (GameState, int) AddUnit(GameState state, string name, int lane)
	{
		var card = new KinCard
		{
			Name = name,
			Cost = 0,
			RunCardId = 7,
		};
		card = (KinCard)
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
		KinBattleFactory.Create(opponentHealth: 500);

	/// <summary>Takes the named card, the way an effect would — by id, in one action.</summary>
	private static (GameState, ImmutableList<GameEvent>) Take(GameState state, int cardId) =>
		Do(state, new TakeCardsAction { Turns = 2, TargetIds = [cardId] });

	private static bool IsReachable(GameState state, string name) =>
		new[] { ZoneType.Draw, ZoneType.Hand, ZoneType.Discard, ZoneType.Field }.Any(zone =>
			state.CardsIn(zone).Any(c => c.Name == name)
		);

	[Test]
	public void TakingACardPullsItOutOfEveryDeck()
	{
		var state = Battle();
		(state, var cardId) = AddUnit(state, "Committed", lane: 1);
		(state, _) = state.BeginBattle();

		(state, var events) = Take(state, cardId);

		Assert.Multiple(() =>
		{
			Assert.That(
				IsReachable(state, "Committed"),
				Is.False,
				"taken means taken — not in draw, hand, discard or on the board. Washing it to "
					+ "Discard is what made this a no-op for a whole version."
			);
			Assert.That(state.CardsIn(ZoneType.Taken).Any(c => c.Name == "Committed"), Is.True);
			Assert.That(events.OfType<CardsTakenEvent>().Single().Count, Is.EqualTo(1));
		});
	}

	/// <summary>
	/// The half that makes it a cost rather than a deletion. An effect that removed cards outright
	/// could end a run on a ten-card deck — KinJam.md records that as the reason permanent removal
	/// was thrown out in the first place.
	/// </summary>
	[Test]
	public void ItComesBackWhenTheTurnsAreUp()
	{
		var state = Battle();
		(state, var cardId) = AddUnit(state, "Committed", lane: 1);
		(state, _) = state.BeginBattle();
		(state, _) = Take(state, cardId);

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
		(state, var cardId) = AddUnit(state, "Committed", lane: 1);
		(state, _) = state.BeginBattle();
		(state, _) = Take(state, cardId);

		for (var i = 0; i < 3; i++)
			(state, _) = Do(state, new EndTurnAction());

		var back = new[] { ZoneType.Draw, ZoneType.Hand, ZoneType.Discard }
			.SelectMany(state.CardsIn)
			.Single(c => c.Name == "Committed");

		Assert.That(
			back.HasComponent<TakenComponent>(),
			Is.False,
			"or a second take could not stamp it honestly"
		);
	}

	[Test]
	public void TheCompanionIsNeverTaken()
	{
		var run = StarterContent.NewRun();
		var (state, _) = run.StartBattle(enemies: [], opponentHealth: 500);

		// Aimed straight at it, which is the only honest way to assert the exemption.
		var companion = state.Units().Single(u => u.HasComponent<CompanionComponent>());
		(state, _) = Do(state, new TakeCardsAction { Turns = 2, TargetIds = [companion.Id] });

		Assert.That(
			state.Units().Any(u => u.HasComponent<CompanionComponent>()),
			Is.True,
			"nothing may take it — the rule lives on the action, not on whatever calls it"
		);
	}
}
