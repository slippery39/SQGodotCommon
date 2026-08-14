using MtgCore.Cards.Builders;
using static MtgCore.Cards.Builders.TargetBuilder;

namespace MtgCore;

/// <summary>
/// Theme 4 — Mill. Mostly self-mill, which is the enabler half of the graveyard theme;
/// opposing mill exists but is a slower clock than combat damage in a 40-card format, so it
/// is always attached to a body or a cantrip rather than sold as a win condition on its own.
///
/// Batch 1 contribution: 5 cards.
/// </summary>
public static class HollowmereMill
{
	public static IReadOnlyList<Card> Cards { get; } =
		[
			// The cantrip. Fills the graveyard and replaces itself, so it is never a dead draw.
			CardFactory
				.Spell("Thought Scour", manaCost: 1)
				.WithMill(2)
				.WithTarget(Single().Players())
				.WithDraw(1)
				.Build(),
			// Deathtouch keeps a 1-drop enabler relevant on turn 10 — otherwise it rots in hand.
			CardFactory
				.Creature("Cryptwalk Scarab", manaCost: 1, power: 1, toughness: 2)
				.WithSubtype(Hollowmere.Insect)
				.WithDeathtouch()
				.WithTriggeredAbility(
					"Burrow Deeper",
					TriggerConditions.OnLandfall(),
					eb => eb.WithMill(3)
				)
				.Build(),
			// Self-mill into recursion. A symmetric "each player mills" did nothing for its
			// own controller, which is why the mill is one-sided and feeds a return.
			CardFactory
				.Creature("Tome Dredger", manaCost: 4, power: 4, toughness: 4)
				.WithSubtype(Hollowmere.Zombie)
				.WithEtbTrigger("Dredge the Archive", eb => eb.WithMill(4))
				.WithDeathTrigger("Reclaim", eb => eb.WithReturnCreatureFromGraveyard())
				.Build(),
			// The opposing-mill card, priced as a cantrip because milling out is not a
			// realistic clock here — it is graveyard denial and a trigger enabler.
			CardFactory
				.Spell("Dreadwaters", manaCost: 2)
				.WithMill(5)
				.WithTarget(Single().Opponent())
				.WithDraw(1)
				.Build(),
			// Bulk self-mill with flashback: the deep-graveyard enabler that turns on
			// threshold on both halves.
			CardFactory
				.Spell("Drown in the Mere", manaCost: 2)
				.WithMill(5)
				.WithFlashback(4)
				.Build(),
		];
}
