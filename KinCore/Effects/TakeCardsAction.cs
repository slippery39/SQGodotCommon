using System.Collections.Immutable;
using ImmutableGameObjects;

namespace KinCore;

/// <summary>
/// The turn a taken card comes back. A plain int on a component — **no timer, no scheduler, no
/// delegate.** The battle already counts turns, so "when" is a number you compare against it.
/// </summary>
public record TakenComponent : GameComponent
{
	public int ReturnsOnTurn { get; init; }
}

/// <summary>
/// Takes its targets OUT of the battle for a number of turns. They come back to Discard.
///
/// **This is Flood, rebuilt, and the reason is the biggest finding in the v3 handoff.** Flood used
/// to wash the board to Discard — and combat v3 made the board wash itself to Discard at the end of
/// every turn, so a doom that had shipped since v1 did precisely nothing while telling the player
/// it had taken everything. Two other scenarios were half-inert the same way.
///
/// **The fix is to take the CARD, not the body.** Under v3 the body was leaving regardless, so the
/// only thing left worth taking is the card itself: gone from every deck for two turns, not
/// drawable, not reshuffled, then handed back to Discard. That is a cost the player can see coming
/// and play around — commit less on the firing turn, or accept a thinner deck for two turns.
///
/// **The companion rides it out**, as it does every sweep: it is not a card and has nowhere to be
/// taken to. That rule lives here, with the action, rather than in whichever scenario takes.
/// </summary>
public record TakeCardsAction : EffectAction
{
	/// <summary>
	/// How many turns the cards are gone for. Two, on every scenario that uses it today.
	///
	/// Counted from the turn that is STARTING when the doom resolves — the countdown ticks and the
	/// turn number advances in `EndTurnAction` before the firing is spawned — so a card taken by a
	/// firing at the end of turn 4 is missing from turns 5 and 6 and is back for turn 7.
	/// </summary>
	public int Turns { get; init; } = 2;

	public override ActionResult Execute(GameState gameState)
	{
		var state = gameState;
		var takenZone = state.ZoneId(ZoneType.Taken);
		var returnsOn = state.GetBattle().TurnNumber + Turns;
		var count = 0;

		foreach (var id in TargetIds)
		{
			if (!state.HasObject(id) || state.GetObject(id) is not KinCard card)
				continue;

			if (card.HasComponent<CompanionComponent>())
				continue;

			state = state.UpdateObject(
				id,
				card.WithComponent(new TakenComponent { ReturnsOnTurn = returnsOn })
			);
			state = state.MoveObject(id, takenZone);
			count++;
		}

		return new ActionResult(state).WithEvent(
			new CardsTakenEvent { Count = count, Turns = Turns }
		);
	}

	/// <summary>
	/// Hands back everything whose turn has come. Called from <see cref="StartTurnAction"/>, before
	/// the hand is drawn, so a card that returns this turn can be drawn this turn.
	///
	/// **The component is stripped on the way out**, or the card would come back already carrying a
	/// return date and a second taking could not re-stamp it honestly.
	/// </summary>
	internal static (GameState, ImmutableList<GameEvent>) ReturnWhatIsDue(
		GameState state,
		ImmutableList<GameEvent> events
	)
	{
		var turn = state.GetBattle().TurnNumber;
		var discardId = state.ZoneId(ZoneType.Discard);
		var returned = 0;

		foreach (var card in state.CardsIn(ZoneType.Taken).ToList())
		{
			if (card.GetComponent<TakenComponent>() is not { } taken)
				continue;

			if (taken.ReturnsOnTurn > turn)
				continue;

			state = state.UpdateObject(card.Id, card.WithoutComponents<TakenComponent>());
			state = state.MoveObject(card.Id, discardId);
			returned++;
		}

		return (
			state,
			returned == 0 ? events : events.Add(new CardsReturnedEvent { Count = returned })
		);
	}
}
