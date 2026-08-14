using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Abstract base for static abilities on battlefield permanents.
/// A static ability applies a continuous effect to cards that satisfy its Filter.
///
/// Applied effects are stamped as components onto affected permanents by StaticAbilityEngine
/// when sources or targets enter/leave the battlefield. AffectedIds is the reverse index
/// used to find and remove those components when this source leaves play.
///
/// Filter is evaluated with SourceCardId set to the permanent that carries
/// the ability, so IsNotSelfSpecification correctly excludes the source card.
/// </summary>
public abstract record StaticAbilityComponent : GameComponent
{
	/// <summary>
	/// Determines which cards this static ability applies to.
	/// </summary>
	public TargetSpecification Filter { get; init; } = new AlwaysFalseSpecification();

	/// <summary>
	/// IDs of all permanents currently affected by this ability.
	/// Maintained by StaticAbilityEngine — used for O(k) cleanup when the source leaves play.
	/// </summary>
	public ImmutableHashSet<int> AffectedIds { get; init; } = ImmutableHashSet<int>.Empty;

	/// <summary>
	/// The zone the source card must be in for this ability to be active.
	/// Defaults to Battlefield, which leaves every pre-existing card unchanged.
	///
	/// Set to Graveyard for Wonder-style abilities ("while this is in your graveyard,
	/// creatures you control have Flying"). The engine registers and unregisters graveyard
	/// sources on CardEnteredGraveyardEvent / CardLeftGraveyardEvent, exactly mirroring how
	/// battlefield sources are handled on ETB/LTB.
	/// </summary>
	public ZoneType ActiveInZone { get; init; } = ZoneType.Battlefield;
}
