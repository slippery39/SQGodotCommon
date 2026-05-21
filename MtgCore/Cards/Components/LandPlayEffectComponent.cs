using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Carries an immediate effect that fires when the land card is played or put into play.
/// PlayLandAction and PutLandIntoPlayAction spawn a ResolveEffectAction for this effect
/// after processing mana and moving the card to exile.
/// Used by Glimmervoid (gain 2 life) and Bounceland (return a land from exile to hand).
/// </summary>
public record LandPlayEffectComponent : GameComponent
{
	public CardEffect Effect { get; init; } = new();
}
