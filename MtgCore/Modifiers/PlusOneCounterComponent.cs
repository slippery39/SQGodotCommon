using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// +1/+1 counters on a creature, as a single component whose Count is mutated in place.
///
/// MtgCore/CLAUDE.md long recorded counters as a deliberate "won't do" — a permanent
/// AddModifierAction IS the counter — with the stated trigger for revisiting being "a card that
/// counts counters". The Core Set Cube's green section is that trigger: Primordial Hydra DOUBLES
/// the number of counters on it, Wildwood Scourge reacts to counters being placed elsewhere, and
/// Barkhide Troll enters with one.
///
/// ONE COMPONENT WITH A COUNT, NOT N STAMPED MODIFIERS. "Double the counters" and "remove a
/// counter" both need a number, and asking "how many permanent P/T modifiers does this creature
/// have" answers the wrong question: Glorious Anthem's AppliedStaticPTBoost, an equipment's
/// EquippedBoostComponent and Unholy Strength's +2/+1 are all permanent and none of them are
/// counters. Doubling would double those too. A distinct type answers "how many counters"
/// unambiguously.
///
/// Duration is forced to Permanent in the constructor rather than left to each card definition.
/// Every other live-evaluated modifier here (ThresholdComponent, CreatureCountComponent,
/// LandsPlayedCountComponent) carries a comment begging callers to remember it, because
/// StartTurnAction strips anything UntilEndOfTurn. Counters are never temporary, so the type can
/// simply refuse to be built wrong.
///
/// CreatureEvaluator and HasPermanentPowerBonusSpecification need no changes — both already walk
/// every PowerToughnessModifier, so counters feed effective P/T and answer "did it have a counter
/// on it" (Basri's Lieutenant) for free.
///
/// ZONE CHANGES: counters are stripped when a card ENTERS the battlefield, in
/// PutIntoBattlefieldAction.ApplyEtbCeremony — deliberately not in MoveCardTracked's strip list.
/// See the comment there; the difference decides whether a death trigger can still see them.
/// </summary>
public record PlusOneCounterComponent : PowerToughnessModifier
{
	public PlusOneCounterComponent()
	{
		Duration = ModifierDuration.Permanent;
	}

	public int Count { get; init; } = 0;

	public override int GetPowerBonus(GameState state, int cardId) => Count;

	public override int GetToughnessBonus(GameState state, int cardId) => Count;
}
