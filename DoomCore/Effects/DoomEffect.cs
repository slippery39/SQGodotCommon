using System.Collections.Immutable;
using ImmutableGameObjects;

namespace DoomCore;

/// <summary>
/// WHEN an effect fires. A holder can carry several, each with a different trigger.
/// </summary>
public enum EffectTrigger
{
	/// <summary>The card was played. Units resolve this as they enter the lane.</summary>
	OnPlay = 0,

	/// <summary>The thing holding this died — your unit, or an enemy.</summary>
	OnDeath,

	/// <summary>Start of your turn, before you draw.</summary>
	OnTurnStart,

	/// <summary>End of your turn, after the lanes have resolved.</summary>
	OnTurnEnd,

	/// <summary>An apocalypse just landed.</summary>
	OnDoomFires,
}

/// <summary>
/// WHAT an effect points at. **Rules, not choices** — there is no targeting anywhere in this game,
/// so a target is something the board can answer on its own at the moment the effect fires.
///
/// This enum is what stands in for MtgCore's 523 lines of `TargetingStrategy` and
/// `TargetSpecifications`. Those exist to let a player pick; nothing here ever asks.
/// </summary>
public enum DoomTarget
{
	/// <summary>Nothing. For effects that read the board themselves.</summary>
	None = 0,
	Self,
	Player,
	Opponent,
	AllEnemies,

	/// <summary>
	/// The enemy sharing the source's lane, if there is one.
	///
	/// **For a rite, "the source's lane" is the lane you dropped it on** — see
	/// <see cref="DoomTargeting"/>. A rite played into a lane is this game's targeted card, and it
	/// needs no targeting UI because the drop IS the choice.
	/// </summary>
	EnemyInSourceLane,

	/// <summary>
	/// YOUR unit in the source's lane. For a unit that is itself; for a rite it is whatever you
	/// dropped the rite on — a buff, an extra strike, or a sacrifice.
	/// </summary>
	UnitInSourceLane,

	/// <summary>Every unit you have on the field, companion included.</summary>
	YourUnits,

	/// <summary>
	/// Your units in the lanes either side of the source. **The spatial axis, and it was free.**
	///
	/// Lane choice is the only decision this game has, and until this existed it was very nearly
	/// arbitrary: any open lane was as good as any other, so "which lane" was a question with no
	/// wrong answer. An effect that reads the lanes NEXT DOOR turns the board into a shape you
	/// arrange rather than five interchangeable slots.
	/// </summary>
	YourUnitsInAdjacentLanes,

	/// <summary>The enemies in the lanes either side of the source. Splash, from your side.</summary>
	EnemiesInAdjacentLanes,
}

/// <summary>
/// One effect on one holder: when it fires, what it points at, and what it does.
///
/// **This is MtgCore's `CardEffect` with the targeting half collapsed to an enum.** There it pairs
/// a `TargetingStrategy` with a `GameAction` template; the engine resolves targets, injects the ids
/// and spawns. Same three steps here — see <see cref="ResolveEffectsAction"/>.
///
/// **Nothing about this is card-specific.** A `DoomCard`, an `Enemy`, an `Opponent` and eventually a
/// doom scenario can all carry a list of these, and the same `DealDamageAction` serves all of them.
/// That is the whole point of putting it here rather than on the card.
///
/// Serializable by construction: a trigger, an enum and a `GameAction` record. **No delegates** —
/// see the Serialization Rule in CLAUDE.md.
/// </summary>
public record DoomEffect
{
	public EffectTrigger Trigger { get; init; } = EffectTrigger.OnPlay;
	public DoomTarget Target { get; init; } = DoomTarget.None;

	/// <summary>
	/// The action to spawn, holding its own fixed data (Amount and so on). Target ids are injected
	/// at resolution, so one template serves every holder that declares it.
	/// </summary>
	public GameAction Template { get; init; } = null!;

	/// <summary>Shown on the card. Written by hand, so keep it true to the template.</summary>
	public string Text { get; init; } = "";
}

/// <summary>Anything that can carry effects — a card, an enemy, an opponent.</summary>
public interface IHasEffects
{
	ImmutableList<DoomEffect> Effects { get; }
}
