using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Marker: this card sits in its owner's exile zone but may still be played this turn —
/// "exile the top card of your library. You may play it this turn" (Abbot of Keral Keep,
/// Chandra, Pyromaster).
///
/// MtgActionGenerator offers a card carrying this exactly as it would a hand card. The three
/// cast actions accept it as an alternative to "card is in your hand" via
/// MtgGameStateExtensions.IsInCastableZone. EndTurnAction strips it from the active player's
/// exile zone when their turn ends, which is what makes it "this turn" and not "forever."
/// </summary>
public record ExiledPlayableComponent : GameComponent { }
