using MtgCore.Cards.Builders;
using static MtgCore.Cards.Builders.TargetBuilder;

namespace MtgCore;

/// <summary>
/// Theme 4 â€” Mill. Mostly self-mill, which is the enabler half of the graveyard theme;
/// opposing mill exists but is a slower clock than combat damage in a 40-card format, so it
/// is always attached to a body or a cantrip rather than sold as a win condition on its own.
///
/// Opposing mill is real here in a way it usually is not: decks are 40 cards and games run
/// long enough that 8-10 cards is a meaningful chunk. It is still never sold as a standalone
/// win condition â€” every opposing-mill card also draws, kills, or leaves a body, so drafting
/// them is never a trap.
///
/// Self-mill stays capped at 5 per card for the decking reason in the Graveyard theme header.
///
/// Batches 1 and 4: 25 cards.
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
			// Deathtouch keeps a 1-drop enabler relevant on turn 10 â€” otherwise it rots in hand.
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
				.WithDeathTrigger("Reclaim", eb => eb.WithAutoReturnCreature())
				.Build(),
			// The opposing-mill card, priced as a cantrip because milling out is not a
			// realistic clock here â€” it is graveyard denial and a trigger enabler.
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
			// ===== BATCH 4 =====
			// The cheapest enabler with a body attached.
			CardFactory
				.Creature("Silt Crawler", manaCost: 1, power: 1, toughness: 1)
				.WithSubtype(Hollowmere.Horror)
				.WithEtbTrigger("Stir the Silt", eb => eb.WithMill(2))
				.Build(),
			// Pure enabler at one mana, for the decks that need the graveyard fast.
			CardFactory.Spell("Skim the Surface", manaCost: 1).WithMill(4).WithDraw(1).Build(),
			// A second landfall miller, so the archetype has redundancy.
			CardFactory
				.Creature("Shoal Scarab", manaCost: 2, power: 1, toughness: 3)
				.WithSubtype(Hollowmere.Insect)
				.WithTriggeredAbility(
					"Stir the Shallows",
					TriggerConditions.OnLandfall(),
					eb => eb.WithMill(2)
				)
				.Build(),
			// A body that fills the yard on the way down.
			CardFactory
				.Creature("Silt Golem", manaCost: 2, power: 2, toughness: 2)
				.WithSubtype(Hollowmere.Horror)
				.WithEtbTrigger("Churn", eb => eb.WithMill(3))
				.Build(),
			// Opposing mill that is never a dead card, because it replaces itself.
			CardFactory
				.Spell("Grasp of the Mere", manaCost: 2)
				.WithMill(6)
				.WithTarget(Single().Opponent())
				.WithDraw(1)
				.Build(),
			// Self-mill with a second use â€” two graveyard-fills from one card.
			CardFactory
				.Spell("Sunken Archives", manaCost: 2)
				.WithMill(4)
				.WithDraw(1)
				.WithFlashback(5)
				.Build(),
			// Mill into a Spirit: the enabler that also builds a board.
			CardFactory
				.Spell("Dregs of the Mere", manaCost: 2)
				.WithMill(3)
				.WithCreateTokens(HollowmereTokens.Spirit())
				.Build(),
			// Enabler and payoff in one card, which is the theme's whole design.
			CardFactory
				.Creature("Mere-Drowned Scribe", manaCost: 3, power: 2, toughness: 3)
				.WithSubtype(Hollowmere.Zombie)
				.WithSubtype(Hollowmere.Wizard)
				.WithEtbTrigger("Drown the Records", eb => eb.WithMill(4))
				.WithThreshold(power: 2, toughness: 2)
				.Build(),
			// Symmetric on paper, one-sided in practice â€” you are the one with the payoffs.
			CardFactory
				.Spell("Flood the Vaults", manaCost: 3)
				.WithMill(5)
				.WithTarget(AllValid().Players())
				.Build(),
			// A repeatable mill outlet â€” the safe way to reach a deep graveyard.
			CardFactory
				.Creature("Silt-Choked Oracle", manaCost: 3, power: 2, toughness: 2)
				.WithSubtype(Hollowmere.Human)
				.WithSubtype(Hollowmere.Wizard)
				.WithActivatedAbility("Consult the Silt", manaCost: 2, effect: eb => eb.WithMill(3))
				.Build(),
			// The real opposing-mill clock, still stapled to a card.
			CardFactory
				.Spell("Tidal Erasure", manaCost: 3)
				.WithMill(8)
				.WithTarget(Single().Opponent())
				.WithDraw(1)
				.Build(),
			// Grows every time a card of yours hits the yard. The theme's marquee payoff.
			CardFactory
				.Creature("Mere-Fed Horror", manaCost: 3, power: 2, toughness: 2)
				.WithSubtype(Hollowmere.Horror)
				.WithTriggeredAbility(
					"Gorge",
					OnYouMill(),
					eb =>
						eb.WithAction(
							new AddModifierAction
							{
								PowerBonus = 1,
								ToughnessBonus = 1,
								Duration = ModifierDuration.Permanent,
								TargetContextKey = ContextKeys.SourceCardId,
							},
							TargetingStrategy.NoTarget()
						)
				)
				.Build(),
			// A defensive body that enables. Taunt is the only way to blunt an attack.
			CardFactory
				.Creature("Deep Mere Lurker", manaCost: 4, power: 3, toughness: 5)
				.WithSubtype(Hollowmere.Horror)
				.WithTaunt()
				.WithEtbTrigger("Drag Down", eb => eb.WithMill(4))
				.Build(),
			// Mills a quarter of their deck and draws â€” a real threat to a slow deck.
			CardFactory
				.Spell("Drown the Archive", manaCost: 2)
				.WithMill(10)
				.WithTarget(Single().Opponent())
				.WithDraw(1)
				.Build(),
			// Turns the mill plan into a life buffer against the aggressive decks.
			CardFactory
				.Creature("Silt-Wreathed Scholar", manaCost: 4, power: 3, toughness: 3)
				.WithSubtype(Hollowmere.Human)
				.WithSubtype(Hollowmere.Wizard)
				.WithTriggeredAbility("Study the Drowned", OnYouMill(), eb => eb.WithLifeGain(1))
				.Build(),
			// Mill plus a board â€” the four-drop two-for-one.
			CardFactory
				.Spell("Sunken Chorus", manaCost: 4)
				.WithMill(5)
				.WithCreateTokens(HollowmereTokens.Spirit(), count: 2)
				.Build(),
			// Big symmetric mill for the decks built to profit from it.
			CardFactory
				.Spell("The Mere Swallows All", manaCost: 5)
				.WithMill(8)
				.WithTarget(AllValid().Players())
				.WithDraw(1)
				.Build(),
			// Fills the yard and immediately cashes it in.
			CardFactory
				.Creature("Abyssal Dredger", manaCost: 5, power: 4, toughness: 4)
				.WithSubtype(Hollowmere.Zombie)
				.WithEtbTrigger("Dredge the Depths", eb => eb.WithMill(5).WithAutoReturnCreature())
				.Build(),
			// Threshold at the top of the mill curve â€” this theme turns it on fastest.
			CardFactory
				.Creature("Silt-Gorged Wurm", manaCost: 5, power: 4, toughness: 4)
				.WithSubtype(Hollowmere.Horror)
				.WithTrample()
				.WithThreshold(power: 3, toughness: 3)
				.Build(),
			// The reanimation target the mill deck is digging toward.
			CardFactory
				.Creature("Leviathan of the Deep Mere", manaCost: 6, power: 7, toughness: 7)
				.WithSubtype(Hollowmere.Horror)
				.WithTrample()
				.WithEtbTrigger("Swallow the Shore", eb => eb.WithMill(5))
				.Build(),
		];

	/// "Whenever a card of yours is milled" â€” the subject of CardMilledEvent is the card,
	/// so this filter reads as "one of your cards", not "you milled someone".
	private static EventTriggerCondition OnYouMill() =>
		new()
		{
			EventTypeName = EventTypeNames.CardMilled,
			Filter = new IsControlledByYouSpecification(),
		};
}
