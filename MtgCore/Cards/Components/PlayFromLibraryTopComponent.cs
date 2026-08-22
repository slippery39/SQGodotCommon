using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// "You may look at the top card of your library any time, and you may play lands from the top of
/// your library." — Radha, Heart of Keld. Also Courser of Kruphix, Oracle of Mul Daya, Future Sight.
///
/// A marker on a battlefield permanent its controller owns. Its whole job is to widen
/// GameState.IsInCastableZone, which is the SINGLE "can you play this from where it is" predicate
/// that all four play actions consult. That is why this needed no change to PlayLandAction, or to
/// any of the three cast actions: extending the predicate extends every one of them at once, the
/// same way impulse draw did.
///
/// Types is what may be played, not what may be looked at. Radha and Courser say lands only;
/// Future Sight is CardType.AnyPermanent | CardType.AnySpell — i.e. everything. Matching is
/// ANY-of, via Card.HasType.
///
/// SCOPE: the top card only, never a deeper one, and only the controller's own library.
/// </summary>
public record PlayFromLibraryTopComponent : GameComponent
{
	public CardType Types { get; init; } = CardType.Land;
}
