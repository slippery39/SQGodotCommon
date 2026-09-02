using MtgCore.Cards.Builders;
using static MtgCore.Cards.Builders.TargetBuilder;

namespace MtgCore;

/// <summary>
/// **Package 5 — Sanguine Bond + Exquisite Blood: two cards that literally win the game.**
///
/// Each half is a two-mana enchantment that does nothing on its own worth a card. Together, ANY
/// life gain at all drains the opponent from 20 to 0 in one resolution.
///
/// ### It terminates, and that was verified rather than assumed
///
/// The pair is a self-feeding trigger — you gain life, so they lose that much, so you gain that
/// much — which is precisely the shape `GameState.MaxActionsPerResolution` (10 000) exists to catch;
/// its message reads *"a trigger that produces the event it triggers on"*. A resolution that throws
/// becomes `GameEndReason.UnhandledException`, which every trainer EXCLUDES, so a throwing combo is
/// invisible rather than dominant.
///
/// Measured in `LifeDrainLoopTests`, gaining ONE life with both out:
///
///     opponent 0, you 41, opponent lost: True
///
/// Twenty iterations, terminating on its own far under the cap, with the loss recorded by the
/// state-based check. **The cascade stops because the opponent is dead, not because a guard fired.**
/// Re-run that test before touching either card; a change that made the loop stop terminating would
/// convert this from the set's cleanest win condition into an excluded game with no error.
///
/// ### The trigger targeting trap this package is the poster child for
///
/// The Bond's first draft used `Single().Opponent()` and drained **zero** while looking entirely
/// correct on the card. `TargetSelectionMode.UserSelect` is filled during a cast action's targeting
/// step, and a trigger has no such step, so the list resolves empty and the effect hits nobody.
/// `Random()` is the trigger-safe form and is deterministic wherever there is one legal target,
/// which is every "target opponent" clause in a two-player game. See MtgCore/CLAUDE.md.
///
/// ### Measured: the pair is HALF discoverable, which is the sharpest result in the set
///
/// The prediction was that neither half would harvest a demand, since neither carries a card filter
/// and there is no "lifegain" `TargetSpecification` anywhere in the engine. **Half wrong.**
///
///     Covenant of Thorns     1 demand — EventTriggerCondition{PlayerGainedLife}
///                            CORE: 10x [12 lifegain cards]
///     Sanguine Reciprocity   0 demands, no core
///
/// A TRIGGER CONDITION is itself a demand — `ProbeTriggers` plays every pool card and records which
/// triggers it fires, so "whenever you gain life" is answered by the twelve cards that gain life.
/// The Covenant is therefore a normal, findable payoff with a real support slot.
///
/// **SUPERSEDED — the pair is now discoverable, and BOTH halves of the recorded diagnosis were
/// wrong.** Kept because the correction is worth more than the original claim.
///
/// It read: *"its trigger fires on an OPPONENT losing life, and the probe plays cards solo — nothing
/// it can do makes the opponent lose life in that fixture, so the demand has no suppliers and is
/// dropped."* Two errors in one sentence:
///
/// 1. **The demand was never harvested at all**, so it had no suppliers because it did not exist.
///    `PoolFeatures.IsObjectReferential` recurses into a trigger's `Filter`, and
///    `IsControlledByOpponentSpecification` was disqualifying — a rule correct for *targeting*
///    ("target creature an opponent controls" is answered by nothing in the placement fixture) and
///    wrong for a trigger, which is asked against real events instead.
/// 2. **Solo probing is a real limitation, but a separate one.** Fixing it needed
///    `ProbeChainedTriggers`, a second pass that replays a card alongside a supplier of a demand it
///    asks — Covenant fires only after something gains you life, so the chain has to be two deep.
///
/// Measured on DES: Reciprocity's demand now has four suppliers, and **Covenant of Thorns is
/// reachable only through the chained pass** — disabling it leaves the three cards that drain on
/// their own. Each fix alone changes nothing.
///
/// The boundary this package was built to mark still stands and is now crossed:
/// **conjunction-building joins a card to cards that ANSWER it, and these two are joined by one
/// PRODUCING what the other CONSUMES**, with no filter relating them.
///
/// **The lesson is the one section 5 of the handoff is entirely about**: a prediction written down
/// before it was measured became something two later sessions reasoned FROM. Re-derive from the
/// dump, not from this comment.
/// </summary>
public static class ComboProvingDrain
{
	public static IReadOnlyList<Card> Cards { get; } =
		[
			// "Whenever you gain life, target opponent loses that much life."
			// ContextKeys.TriggerAmount carries the event's number, which is what makes "that much"
			// expressible rather than a flat constant.
			CardFactory
				.Enchantment("Covenant of Thorns", manaCost: 2)
				.WithComponent(
					new TriggeredAbilityComponent
					{
						Name = "Covenant",
						Condition = TriggerConditions.OnGainLife(),
						Effect = new CardEffect
						{
							TargetingStrategy = Random().Opponent(),
							ActionTemplate = new LoseLifeAction
							{
								AmountContextKey = ContextKeys.TriggerAmount,
							},
						},
					}
				)
				.Build(),
			// "Whenever an opponent loses life, you gain that much life."
			//
			// IsControlledByOpponentSpecification rather than TriggerConditions.OnLoseLife(), which
			// filters on YOUR life loss — the wrong half, and a mistake that would make the pair
			// inert while each card still read correctly.
			CardFactory
				.Enchantment("Sanguine Reciprocity", manaCost: 2)
				.WithComponent(
					new TriggeredAbilityComponent
					{
						Name = "Reciprocity",
						Condition = new EventTriggerCondition
						{
							EventTypeName = EventTypeNames.PlayerLostLife,
							Filter = new IsControlledByOpponentSpecification(),
						},
						Effect = new CardEffect
						{
							TargetingStrategy = TargetingStrategy.NoTarget(),
							ActionTemplate = new GainLifeAction
							{
								AmountContextKey = ContextKeys.TriggerAmount,
								TargetContextKey = ContextKeys.CastingPlayerId,
							},
						},
					}
				)
				.Build(),
			// A cheap, unremarkable life-gain outlet so the pair has something to ignite it that is
			// not already in HLM/CSC. Deliberately a weak card on its own — the point is that a deck
			// wanting it is a deck that already holds both halves.
			CardFactory
				.Creature("Almsgiver Acolyte", manaCost: 1, power: 1, toughness: 1)
				.WithSubtype("Cleric")
				.WithEtbTrigger(
					"Alms",
					eb => eb.WithLifeGain(2).WithTarget(TargetingStrategy.Self())
				)
				.Build(),
		];
}
