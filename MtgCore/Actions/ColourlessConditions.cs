using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// "If you control an artifact named Scepter of Empires" — the Core Set Cube's Empires trio
/// (Crown, Scepter and Throne of Empires), each of which does something bigger when the other two
/// are on the battlefield beside it.
///
/// Deliberately built rather than cut. Name-matching is MISSING from this engine, not structurally
/// absent the way colour and blocking are, and the set's own rule is to build the mechanic when a
/// card needs something the engine lacks. It is also the whole point of those three cards.
///
/// Mirrors ControlsSubtypeCondition exactly, including the Minimum field, so a future "you control
/// two creatures named X" needs no second type. Name comparison is case-insensitive to match
/// Card.HasSubtype's comparer.
/// </summary>
public record ControlsCardNamedCondition : ActivationCondition
{
	public string CardName { get; init; } = "";
	public int Minimum { get; init; } = 1;

	public override bool IsSatisfied(GameState state, int cardId, int playerId)
	{
		if (playerId == 0 || string.IsNullOrEmpty(CardName))
			return false;

		var battlefieldId = state.GetPlayerZoneId(playerId, ZoneType.Battlefield);
		if (battlefieldId == 0)
			return false;

		return state
				.GetCardsInZone(battlefieldId)
				.Count(c =>
					c.ControllerId == playerId
					&& string.Equals(c.Name, CardName, StringComparison.OrdinalIgnoreCase)
				) >= Minimum;
	}

	public override string Describe() => $"you control {CardName}";
}

/// <summary>
/// "You control artifacts named X and Y" — the Empires clause needs two names at once, and
/// ActivationCondition has no And combinator of its own.
///
/// A list rather than a nested pair, because all three Empires cards ask about exactly two other
/// names and a nested And would read worse than the card does.
/// </summary>
public record ControlsAllCardsNamedCondition : ActivationCondition
{
	public string FirstName { get; init; } = "";
	public string SecondName { get; init; } = "";

	public override bool IsSatisfied(GameState state, int cardId, int playerId) =>
		new ControlsCardNamedCondition { CardName = FirstName }.IsSatisfied(state, cardId, playerId)
		&& new ControlsCardNamedCondition { CardName = SecondName }.IsSatisfied(
			state,
			cardId,
			playerId
		);

	public override string Describe() => $"you control {FirstName} and {SecondName}";
}

/// <summary>
/// "Activate only if you control no creatures." — Haunted Plate Mail.
///
/// The card's own limiter on animating itself, and the reason AnimateAction does not need to apply
/// summoning sickness: a permanent that animates only on an empty board cannot be a surprise
/// attacker off the top of a curve. Counts creatures the player CONTROLS, not owns, so a stolen
/// creature blocks the animation exactly as one of your own would.
/// </summary>
public record ControlsNoCreaturesCondition : ActivationCondition
{
	public override bool IsSatisfied(GameState state, int cardId, int playerId)
	{
		if (playerId == 0)
			return false;

		var battlefieldId = state.GetPlayerZoneId(playerId, ZoneType.Battlefield);
		if (battlefieldId == 0)
			return true;

		return !state
			.GetCardsInZone(battlefieldId)
			.Any(c => c.ControllerId == playerId && c.HasComponent<CreatureComponent>());
	}

	public override string Describe() => "you control no creatures";
}
