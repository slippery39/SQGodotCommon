using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// "Exile it at end of turn" — a permanent that exists for one turn only.
///
/// The engine's stand-in for "becomes a creature until end of turn". A planeswalker cannot BE a
/// creature here (the creature/permanent routing split is decided at cast time), so Gideon Jura's
/// 0 makes a hasty token instead and takes it away again when the turn ends. Same visible outcome
/// — one turn of a 6/6 attacking — without crossing that boundary.
///
/// Swept by EndTurnAction on BOTH battlefields rather than only the active player's. The token is
/// made and used on its controller's turn, so either sweep would do today; doing both means a
/// token created off a trigger on the opponent's turn cannot linger for a full extra round.
///
/// Exile rather than sacrifice, so nothing treats it as a death: a token that "died" would feed
/// every death payoff on the board once a turn, for free.
/// </summary>
public record ExileAtEndOfTurnComponent : GameComponent;
