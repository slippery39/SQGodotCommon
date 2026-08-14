using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore.Cards.Builders;
using static MtgCore.Cards.Builders.TargetBuilder;

namespace MtgCore;

/// <summary>
/// Theme 2 — Discard Bonuses. Outlets that turn dead cards into graveyard fuel, plus the
/// payoffs that make discarding a benefit.
///
/// Madness is modelled as "when you discard this, ⟨effect⟩" — a graveyard-active trigger on
/// CardDiscardedEvent — because casting a discarded card needs a priority window this engine
/// does not have. The effect therefore costs no mana, so these are priced as free effects,
/// not as a discount on the card's face cost.
///
/// Because a madness trigger fires for free, madness creatures are costed as though the
/// discard route were their real cost: they are priced at what you would pay to cast them,
/// with a body slightly under rate, so discarding is a tempo win rather than a free spell.
///
/// Batches 1-2: 30 cards.
/// </summary>
public static class HollowmereDiscard
{
	public static IReadOnlyList<Card> Cards { get; } =
		[
			// The archetypal outlet. Cheap, refuels, and comes back once.
			CardFactory
				.Spell("Faithless Looting", manaCost: 1)
				.WithDraw(2)
				.WithDiscard()
				.WithFlashback(3)
				.Build(),
			// Madness. Free 3 damage when discarded, or a real removal spell when cast.
			CardFactory
				.Spell("Fiery Temper", manaCost: 2)
				.WithDamage(3)
				.WithTarget(Single().PlayersOrCreatures())
				.WithComponent(Madness(Drain(3)))
				.Build(),
			// Grows every time you loot. A 3-drop that must do two things, and does.
			CardFactory
				.Creature("Asylum Chronicler", manaCost: 3, power: 2, toughness: 3)
				.WithSubtype(Hollowmere.Human)
				.WithSubtype(Hollowmere.Wizard)
				.WithTriggeredAbility(
					"Record the Madness",
					new EventTriggerCondition
					{
						EventTypeName = EventTypeNames.CardDiscarded,
						Filter = new IsControlledByYouSpecification(),
					},
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
			// Madness on a body: discarding it puts it straight onto the battlefield, so the
			// looting outlets in this theme are never truly discarding it away.
			CardFactory
				.Creature("Shrieking Penitent", manaCost: 4, power: 4, toughness: 3)
				.WithSubtype(Hollowmere.Horror)
				.WithComponent(Madness(PutSelfOntoBattlefield()))
				.Build(),
			// Rummaging that nets a card. The discard is the point, not the cost.
			CardFactory.Spell("Cathartic Reunion", manaCost: 1).WithDiscard().WithDraw(3).Build(),
			// ===== BATCH 2 =====
			// The free repeatable outlet. Every discard deck wants one on turn one.
			CardFactory
				.Creature("Insolent Neonate", manaCost: 1, power: 1, toughness: 1)
				.WithSubtype(Hollowmere.Vampire)
				.WithActivatedAbility(
					"Rummage",
					manaCost: 0,
					effect: eb => eb.WithDiscard().WithDraw(1)
				)
				.Build(),
			// Cheap card filtering.
			CardFactory.Spell("Wild Guess", manaCost: 1).WithDiscard().WithDraw(2).Build(),
			// Madness on a one-drop body: discarding it is strictly better than casting it.
			CardFactory
				.Creature("Twitching Ghoul", manaCost: 1, power: 1, toughness: 1)
				.WithSubtype(Hollowmere.Zombie)
				.WithComponent(Madness(PutSelfOntoBattlefield()))
				.Build(),
			// Targeted hand attack — the format's answer to a reanimation combo.
			CardFactory
				.Spell("Whispered Betrayal", manaCost: 2)
				.WithOpponentDiscard()
				.WithLoseLife(2)
				.Build(),
			// Madness drain. Free three-point swing when discarded.
			CardFactory
				.Spell("Alms of the Vein", manaCost: 2)
				.WithAction(
					new DrainLifeAction
					{
						Amount = 3,
						TargetOpponent = true,
						PlayerIdContextKey = ContextKeys.CastingPlayerId,
					},
					TargetingStrategy.NoTarget()
				)
				.WithComponent(Madness(Drain(3)))
				.Build(),
			// A 3/1 for two that only costs a discard — the tempo payoff of the theme.
			CardFactory
				.Creature("Bloodmad Vampire", manaCost: 3, power: 3, toughness: 1)
				.WithSubtype(Hollowmere.Vampire)
				.WithComponent(Madness(PutSelfOntoBattlefield()))
				.Build(),
			// Rewards the outlet rather than the discarded card.
			CardFactory
				.Creature("Gnawing Hunger", manaCost: 2, power: 2, toughness: 2)
				.WithSubtype(Hollowmere.Vampire)
				.WithTriggeredAbility(
					"Feed",
					OnYouDiscard(),
					eb =>
						eb.WithAction(
							new AddModifierAction
							{
								PowerBonus = 1,
								ToughnessBonus = 0,
								Duration = ModifierDuration.Permanent,
								TargetContextKey = ContextKeys.SourceCardId,
							},
							TargetingStrategy.NoTarget()
						)
				)
				.Build(),
			// Draw-two attached to a body, at the cost of a card you did not want.
			CardFactory.Spell("Tormenting Voice", manaCost: 2).WithDiscard().WithDraw(3).Build(),
			// A recurring discard outlet that also fills the graveyard twice over.
			CardFactory
				.Creature("Rummaging Wight", manaCost: 3, power: 2, toughness: 2)
				.WithSubtype(Hollowmere.Zombie)
				.WithActivatedAbility(
					"Paw Through the Silt",
					manaCost: 1,
					effect: eb => eb.WithDiscard().WithMill(2)
				)
				.Build(),
			// A 4/3 haste for three, if you are willing to discard it.
			CardFactory
				.Creature("Incorrigible Youths", manaCost: 4, power: 4, toughness: 3)
				.WithSubtype(Hollowmere.Human)
				.WithHaste()
				.WithComponent(Madness(PutSelfOntoBattlefield()))
				.Build(),
			// Turns every discard into life, which is what lets the theme race.
			CardFactory
				.Creature("Asylum Visitor", manaCost: 3, power: 3, toughness: 1)
				.WithSubtype(Hollowmere.Vampire)
				.WithTriggeredAbility(
					"Sate",
					OnYouDiscard(),
					eb =>
						eb.WithAction(
							new DrainLifeAction
							{
								Amount = 1,
								TargetOpponent = true,
								PlayerIdContextKey = ContextKeys.CastingPlayerId,
							},
							TargetingStrategy.NoTarget()
						)
				)
				.Build(),
			// A discard outlet that pumps itself — the aggressive shell's mana sink.
			CardFactory
				.Creature("Olivia's Dragoon", manaCost: 2, power: 1, toughness: 2)
				.WithSubtype(Hollowmere.Human)
				.WithSubtype(Hollowmere.Rogue)
				.WithActivatedAbility(
					"Goad the Beast",
					manaCost: 0,
					effect: eb =>
						eb.WithDiscard()
							.WithAction(
								new AddModifierAction
								{
									PowerBonus = 2,
									ToughnessBonus = 0,
									Duration = ModifierDuration.UntilEndOfTurn,
									TargetContextKey = ContextKeys.SourceCardId,
								},
								TargetingStrategy.NoTarget()
							)
				)
				.Build(),
			// Removal with a madness half — flexible, which is what a cube wants.
			CardFactory
				.Spell("Avacyn's Judgment", manaCost: 4)
				.WithDamage(4)
				.WithTarget(Single().PlayersOrCreatures())
				.WithComponent(Madness(Drain(4)))
				.Build(),
			// Strips two and leaves a body. The bar for a four-drop is a two-for-one.
			CardFactory
				.Creature("Hollowmere Inquisitor", manaCost: 4, power: 3, toughness: 3)
				.WithSubtype(Hollowmere.Human)
				.WithSubtype(Hollowmere.Cleric)
				.WithEtbTrigger("Interrogate", eb => eb.WithOpponentDiscard(2))
				.Build(),
			// Makes a Spirit every time you loot, bridging discard into go-wide.
			CardFactory
				.Creature("Hollowmere Confessor", manaCost: 4, power: 3, toughness: 4)
				.WithSubtype(Hollowmere.Human)
				.WithSubtype(Hollowmere.Cleric)
				.WithTriggeredAbility(
					"Absolve",
					OnYouDiscard(),
					eb => eb.WithCreateTokens(HollowmereTokens.Spirit())
				)
				.Build(),
			// The Demon payoff: every discard sweeps their board a little further.
			CardFactory
				.Creature("Archfiend of the Mere", manaCost: 5, power: 5, toughness: 4)
				.WithSubtype(Hollowmere.Demon)
				.WithFlying()
				.WithTriggeredAbility(
					"Choking Ash",
					OnYouDiscard(),
					eb => eb.WithDamage(1).WithTarget(AllValid().OpponentCreatures())
				)
				.Build(),
			// Recurring hand attack — cheap, and comes back.
			CardFactory
				.Spell("Raven's Crime", manaCost: 1)
				.WithOpponentDiscard()
				.WithFlashback(2)
				.Build(),
			// Symmetric-looking but one-sided: you are the one with madness cards.
			CardFactory
				.Spell("Riddle of the Mere", manaCost: 3)
				.WithOpponentDiscard(2)
				.WithDraw(1)
				.Build(),
			// A looter on an evasive body: repeatable, and hard to answer.
			CardFactory
				.Creature("Mere-Drifter Wisp", manaCost: 3, power: 1, toughness: 2)
				.WithSubtype(Hollowmere.Spirit)
				.WithFlying()
				.WithActivatedAbility(
					"Drift and Discard",
					manaCost: 0,
					effect: eb => eb.WithDiscard().WithDraw(1)
				)
				.Build(),
			// The madness top-end: a 6/6 flier for the price of a discard.
			CardFactory
				.Creature("Ravenous Bloodseeker", manaCost: 6, power: 6, toughness: 6)
				.WithSubtype(Hollowmere.Demon)
				.WithFlying()
				.WithComponent(Madness(PutSelfOntoBattlefield()))
				.Build(),
			// Discard as a cost rather than a drawback: a hard-cast bargain.
			CardFactory
				.Spell("Bargain at the Crossroads", manaCost: 2)
				.WithDiscard()
				.WithReanimate()
				.Build(),
			// The outlet that also mills — two enablers stapled together.
			CardFactory
				.Spell("Silt-Stained Ledger", manaCost: 2)
				.WithDiscard()
				.WithMill(3)
				.WithDraw(2)
				.Build(),
			// Turns a dead card into a real threat, and the discarded card into fuel.
			CardFactory
				.Creature("Ghoulish Bargainer", manaCost: 3, power: 2, toughness: 3)
				.WithSubtype(Hollowmere.Zombie)
				.WithEtbTrigger(
					"Strike the Bargain",
					eb => eb.WithDiscard().WithReturnCreatureFromGraveyard()
				)
				.Build(),
			// A madness spell that answers the go-wide decks.
			CardFactory
				.Spell("Ashen Reckoning", manaCost: 4)
				.WithDamage(2)
				.WithTarget(AllValid().OpponentCreatures())
				.WithComponent(Madness(Drain(2)))
				.Build(),
			// Pays you for the whole theme at once: a Spirit and a card per discard.
			CardFactory
				.Creature("Keeper of Broken Vows", manaCost: 5, power: 3, toughness: 4)
				.WithSubtype(Hollowmere.Human)
				.WithSubtype(Hollowmere.Wizard)
				.WithTriggeredAbility(
					"Keep the Vow",
					OnYouDiscard(),
					eb => eb.WithDraw(1).WithLoseLife(1)
				)
				.Build(),
		];

	// ===== SHARED TRIGGER PIECES =====
	//
	// Written once here rather than repeated per card: the ActiveInZone = Graveyard on a
	// madness trigger is mandatory (the card is already in the graveyard when it fires) and
	// silently does nothing if forgotten.

	private static TriggeredAbilityComponent Madness(CardEffect effect) =>
		new()
		{
			Name = "Madness",
			ActiveInZone = ZoneType.Graveyard,
			Condition = new EventTriggerCondition
			{
				EventTypeName = EventTypeNames.CardDiscarded,
				Filter = new IsSourceCardSpecification(),
			},
			Effect = effect,
		};

	/// "Whenever you discard a card" — fires for any card its controller discards.
	private static EventTriggerCondition OnYouDiscard() =>
		new()
		{
			EventTypeName = EventTypeNames.CardDiscarded,
			Filter = new IsControlledByYouSpecification(),
		};

	private static CardEffect PutSelfOntoBattlefield() =>
		new()
		{
			TargetingStrategy = TargetingStrategy.NoTarget(),
			ActionTemplate = new PutIntoBattlefieldAction
			{
				CardIdContextKey = ContextKeys.SourceCardId,
			},
		};

	private static CardEffect Drain(int amount) =>
		new()
		{
			TargetingStrategy = TargetingStrategy.NoTarget(),
			ActionTemplate = new DrainLifeAction
			{
				Amount = amount,
				TargetOpponent = true,
				PlayerIdContextKey = ContextKeys.CastingPlayerId,
			},
		};
}
