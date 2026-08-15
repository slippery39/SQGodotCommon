using MtgCore.Cards.Builders;
using static MtgCore.Cards.Builders.TargetBuilder;

namespace MtgCore;

/// <summary>
/// Theme 10 â€” Vampire Tribal. Lifedrain and sacrifice; the tribe that profits from creatures
/// dying, which makes it the natural home for the set's deathtouch.
///
/// Deathtouch is concentrated here and kept rare â€” with no blockers it turns every attack
/// into a favourable trade, so it goes on small bodies where the trade is the whole point.
///
/// Lifelink is the tribe's signature rather than deathtouch. In a no-blocker format the only
/// way to survive a faster clock is to gain life while attacking, which is what lets Vampires
/// race the go-wide decks instead of losing to them.
///
/// Batches 1 and 5: 20 cards.
/// </summary>
public static class HollowmereVampires
{
	public static IReadOnlyList<Card> Cards { get; } =
		[
			// The drain engine. Every creature that dies â€” including the opponent's, and
			// including tokens â€” is reach, which is what makes the go-wide themes fear it.
			CardFactory
				.Creature("Blood Artist", manaCost: 2, power: 0, toughness: 1)
				.WithSubtype(Hollowmere.Vampire)
				.WithTriggeredAbility(
					"Toast the Fallen",
					TriggerConditions.OnAnyCreatureDies(),
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
			// A repeatable token maker on an evasive body â€” the tribe's mana sink and its
			// best reanimation target below the Angels.
			CardFactory
				.Creature("Bloodline Keeper", manaCost: 4, power: 3, toughness: 3)
				.WithSubtype(Hollowmere.Vampire)
				.WithFlying()
				.WithActivatedAbility(
					"Convene the Bloodline",
					manaCost: 1,
					effect: eb => eb.WithCreateTokens(HollowmereTokens.Vampire())
				)
				.Build(),
			// Deathtouch plus lifelink plus flying: attacks profitably into anything, which
			// is the ceiling for a three-drop and the reason there are so few of these.
			CardFactory
				.Creature("Nighthawk Penitent", manaCost: 3, power: 2, toughness: 3)
				.WithSubtype(Hollowmere.Vampire)
				.WithFlying()
				.WithLifelink()
				.WithDeathtouch()
				.Build(),
			// ===== BATCH 5 =====
			// The one-drop that lets the tribe race from turn one.
			CardFactory
				.Creature("Vampire Cutthroat", manaCost: 1, power: 1, toughness: 1)
				.WithSubtype(Hollowmere.Vampire)
				.WithLifelink()
				.Build(),
			// Punishes the go-wide decks the tribe is otherwise weakest against.
			CardFactory
				.Creature("Blood Seeker", manaCost: 1, power: 1, toughness: 1)
				.WithSubtype(Hollowmere.Vampire)
				.WithTriggeredAbility(
					"Scent of Blood",
					new EventTriggerCondition
					{
						EventTypeName = EventTypeNames.CreatureEnteredBattlefield,
						Filter = new IsControlledByOpponentSpecification(),
					},
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
			// Grows every time it connects â€” with no blockers, that is most turns.
			CardFactory
				.Creature("Bloodcrazed Neonate", manaCost: 2, power: 2, toughness: 1)
				.WithSubtype(Hollowmere.Vampire)
				.WithTriggeredAbility(
					"Blood Frenzy",
					new EventTriggerCondition
					{
						EventTypeName = EventTypeNames.CombatDamageDealtToPlayer,
						Filter = new IsSourceCardSpecification(),
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
			// A clean aggressive two-drop with the tribe's keyword.
			CardFactory
				.Creature("Vampire Lacerator", manaCost: 2, power: 2, toughness: 2)
				.WithSubtype(Hollowmere.Vampire)
				.WithLifelink()
				.Build(),
			// The sacrifice outlet â€” turns a board about to be swept into a lethal attack.
			CardFactory
				.Creature("Bloodthrone Vampire", manaCost: 2, power: 2, toughness: 2)
				.WithSubtype(Hollowmere.Vampire)
				.WithActivatedAbility(
					"Drain the Thrall",
					manaCost: 0,
					effect: eb =>
						eb.WithAction(
							new AddModifierAction
							{
								PowerBonus = 2,
								ToughnessBonus = 2,
								Duration = ModifierDuration.UntilEndOfTurn,
								TargetContextKey = ContextKeys.SourceCardId,
							},
							TargetingStrategy.NoTarget()
						),
					costs: cb => cb.Sacrifice(TargetSpecification.CreatureControlledByYou())
				)
				.Build(),
			// The tribal lord.
			CardFactory
				.Creature("Stromkirk Captain", manaCost: 3, power: 2, toughness: 2)
				.WithSubtype(Hollowmere.Vampire)
				.WithComponent(
					new StaticPTBoostAbility
					{
						PowerBonus = 1,
						ToughnessBonus = 1,
						Filter = new IsSubtypeSpecification { Subtype = Hollowmere.Vampire }.And(
							TargetSpecification.OtherCreaturesYouControl()
						),
					}
				)
				.WithComponent(
					new StaticGrantKeywordAbility
					{
						GrantsLifelink = true,
						Filter = new IsSubtypeSpecification { Subtype = Hollowmere.Vampire }.And(
							TargetSpecification.OtherCreaturesYouControl()
						),
					}
				)
				.Build(),
			// Evasive damage â€” the tribe's clock when the ground stalls.
			CardFactory
				.Creature("Vampire Interloper", manaCost: 3, power: 3, toughness: 3)
				.WithSubtype(Hollowmere.Vampire)
				.WithFlying()
				.Build(),
			// Life swing on a body, which is how this tribe stabilises.
			CardFactory
				.Creature("Markov Patrician", manaCost: 3, power: 2, toughness: 2)
				.WithSubtype(Hollowmere.Vampire)
				.WithLifelink()
				.WithTriggeredAbility(
					"Taste of the Hunt",
					TriggerConditions.OnSelfAttacks(),
					eb => eb.WithLifeGain(2)
				)
				.Build(),
			// Removal and reach in one card, with a second use.
			CardFactory
				.Spell("Sanguine Rite", manaCost: 3)
				.WithAction(
					new DrainLifeAction
					{
						Amount = 3,
						TargetOpponent = true,
						PlayerIdContextKey = ContextKeys.CastingPlayerId,
					},
					TargetingStrategy.NoTarget()
				)
				.WithFlashback(5)
				.Build(),
			// Grows off every death on either side â€” a slow inevitability engine.
			CardFactory
				.Creature("Stromkirk Bloodthief", manaCost: 4, power: 3, toughness: 3)
				.WithSubtype(Hollowmere.Vampire)
				.WithTriggeredAbility(
					"Feast on the Fallen",
					TriggerConditions.OnAnyCreatureDies(),
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
			// The evasive drain engine â€” Blood Artist's bigger sibling.
			CardFactory
				.Creature("Falkenrath Noble", manaCost: 4, power: 2, toughness: 2)
				.WithSubtype(Hollowmere.Vampire)
				.WithFlying()
				.WithTriggeredAbility(
					"Noble's Due",
					TriggerConditions.OnAnyCreatureDies(),
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
			// The tribe's link to the graveyard theme.
			CardFactory
				.Creature("Bloodline Necromancer", manaCost: 4, power: 3, toughness: 3)
				.WithSubtype(Hollowmere.Vampire)
				.WithSubtype(Hollowmere.Wizard)
				.WithEtbTrigger("Call the Bloodline", eb => eb.WithAutoReanimate())
				.Build(),
			// Two evasive lifelinking bodies from one card.
			CardFactory
				.Creature("Olivia of the Mere", manaCost: 5, power: 4, toughness: 4)
				.WithSubtype(Hollowmere.Vampire)
				.WithFlying()
				.WithEtbTrigger(
					"Sire the Brood",
					eb => eb.WithCreateTokens(HollowmereTokens.Vampire(), count: 2)
				)
				.Build(),
			// A resilient evasive threat that races almost anything.
			CardFactory
				.Creature("Bloodlord of Hollowmere", manaCost: 5, power: 4, toughness: 5)
				.WithSubtype(Hollowmere.Vampire)
				.WithFlying()
				.WithLifelink()
				.Build(),
			// The tribe's top end: attacks profitably into any board and swings the race.
			CardFactory
				.Creature("Elder of the Blood Court", manaCost: 6, power: 5, toughness: 5)
				.WithSubtype(Hollowmere.Vampire)
				.WithFlying()
				.WithLifelink()
				.WithDeathtouch()
				.Build(),
			// A cheap trick that turns a trade into a blowout.
			CardFactory
				.Spell("Feast of Blood", manaCost: 2)
				.WithGrantKeyword(lifelink: true, deathtouch: true)
				.WithTarget(Single().YourCreatures())
				.WithFlashback(4)
				.Build(),
			// Mass lifelink â€” the payoff that ends a race the turn it resolves.
			CardFactory
				.Spell("Night of the Long Thirst", manaCost: 4)
				.WithGrantKeyword(lifelink: true)
				.WithTarget(AllValid().AllYourCreatures())
				.WithBoost(1, 1)
				.WithTarget(AllValid().AllYourCreatures())
				.Build(),
		];
}
