using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// "You may have this creature enter as a copy of any creature on the battlefield."
/// — Clone, Phantasmal Image.
///
/// Applied by PutIntoBattlefieldAction's ETB ceremony, which is the single place every creature
/// reaches the battlefield through — cast, created or reanimated — so a cloned token and a cast
/// Clone behave identically.
///
/// WHICH creature is copied is decided here rather than by the player: the highest effective
/// power on either battlefield. A real ChoiceAction would be more faithful, but it would have to
/// pause the pipeline in the middle of the ETB ceremony, and "copy the biggest thing" is what the
/// choice almost always is. Revisit if a card makes the choice interesting.
///
/// The copy takes the target's Components, Subtypes and Types but keeps its own Id, OwnerId and
/// ControllerId — a Clone of your opponent's creature is still yours.
/// </summary>
public record CopyOnEnterComponent : GameComponent
{
	/// <summary>
	/// Phantasmal Image stays an Illusion on top of whatever it copies, and gains
	/// "when this becomes the target of a spell or ability, sacrifice it".
	/// </summary>
	public bool RemainsIllusion { get; init; } = false;
}
