using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore.Cards.Builders;
using static MtgCore.Cards.Builders.TargetBuilder;

namespace MtgCore;

/// <summary>
/// Theme 3 â€” Human Tribal. The set's aggressive backbone, and the tribe Angels reward.
/// Humans are also the most common creature type on cards belonging to other themes, so a
/// Human deck picks up density from Spells and Werewolves without trying.
///
/// Human count matters more than it looks: every werewolf's day face is a Human, so the
/// Werewolf theme silently doubles this tribe's density. Lords are costed with that in mind.
///
/// Batches 1 and 3: 30 cards.
/// </summary>
public static class HollowmereHumans
{
	public static IReadOnlyList<Card> Cards { get; } =
		[
			// The one-drop that rewards density. Grows on every subsequent Human.
			CardFactory
				.Creature("Champion of the Parish", manaCost: 1, power: 1, toughness: 1)
				.WithSubtype(Hollowmere.Human)
				.WithSubtype(Hollowmere.Soldier)
				.WithTriggeredAbility(
					"Rally the Parish",
					new EventTriggerCondition
					{
						EventTypeName = EventTypeNames.CreatureEnteredBattlefield,
						Filter = new IsSubtypeSpecification { Subtype = Hollowmere.Human }
							.And(new IsControlledByYouSpecification())
							.And(new IsNotSelfSpecification()),
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
			// The lord. Anthem effects are strong with no blockers, so the body stays small.
			CardFactory
				.Creature("Thraben Marshal", manaCost: 3, power: 2, toughness: 2)
				.WithSubtype(Hollowmere.Human)
				.WithSubtype(Hollowmere.Soldier)
				.WithComponent(
					new StaticPTBoostAbility
					{
						PowerBonus = 1,
						ToughnessBonus = 1,
						Filter = new IsSubtypeSpecification { Subtype = Hollowmere.Human }.And(
							TargetSpecification.OtherCreaturesYouControl()
						),
					}
				)
				.Build(),
			// Two bodies for two mana â€” the density enabler every tribal deck needs.
			CardFactory
				.Spell("Gather the Townsfolk", manaCost: 2)
				.WithCreateTokens(HollowmereTokens.Human(), count: 2)
				.Build(),
			// Bridges Humans into the graveyard theme: a Human that likes a full graveyard.
			CardFactory
				.Creature("Parish Gravedigger", manaCost: 3, power: 2, toughness: 3)
				.WithSubtype(Hollowmere.Human)
				.WithEtbTrigger("Unearth a Kinsman", eb => eb.WithAutoReturnCreature())
				.Build(),
			// Payoff at the top of the Human curve. Immediate board impact, per the rate bar.
			CardFactory
				.Creature("Hollowmere Militia Captain", manaCost: 5, power: 3, toughness: 3)
				.WithSubtype(Hollowmere.Human)
				.WithSubtype(Hollowmere.Soldier)
				.WithEtbTrigger(
					"Muster the Watch",
					eb => eb.WithCreateTokens(HollowmereTokens.Human(), count: 3)
				)
				.Build(),
			// ===== BATCH 3 =====
			// Two bodies from one card, and the second one flies. Bridges into Spirits.
			CardFactory
				.Creature("Doomed Traveler", manaCost: 1, power: 1, toughness: 1)
				.WithSubtype(Hollowmere.Human)
				.WithDeathTrigger(
					"Rest at Last",
					eb => eb.WithCreateTokens(HollowmereTokens.Spirit())
				)
				.Build(),
			// The aggressive one-drop. Nothing clever â€” the tribe needs a body on turn one.
			CardFactory
				.Creature("Parish Torchbearer", manaCost: 1, power: 2, toughness: 1)
				.WithSubtype(Hollowmere.Human)
				.WithSubtype(Hollowmere.Soldier)
				.Build(),
			// Keeps the aggressive decks honest without being a dead draw.
			CardFactory
				.Creature("Hollowmere Herbalist", manaCost: 1, power: 1, toughness: 2)
				.WithSubtype(Hollowmere.Human)
				.WithSubtype(Hollowmere.Cleric)
				.WithLifelink()
				.Build(),
			// Comes back, which makes it a repeatable Champion of the Parish trigger.
			CardFactory
				.Creature("Chapel Scavenger", manaCost: 2, power: 1, toughness: 1)
				.WithSubtype(Hollowmere.Human)
				.WithSubtype(Hollowmere.Rogue)
				.WithDeathTrigger(
					"Slip Away",
					eb =>
						eb.WithAction(
							new ReturnToHandAction { TargetContextKey = ContextKeys.SourceCardId },
							TargetingStrategy.NoTarget()
						)
				)
				.Build(),
			// Two Humans on one card at two mana â€” raw tribal density.
			CardFactory
				.Creature("Bellowing Sergeant", manaCost: 2, power: 2, toughness: 1)
				.WithSubtype(Hollowmere.Human)
				.WithSubtype(Hollowmere.Soldier)
				.WithEtbTrigger("Call to Arms", eb => eb.WithCreateTokens(HollowmereTokens.Human()))
				.Build(),
			// The defensive two-drop. Taunt is the only way to stop an attack in this engine.
			CardFactory
				.Creature("Vigilant Watcher", manaCost: 2, power: 1, toughness: 3)
				.WithSubtype(Hollowmere.Human)
				.WithSubtype(Hollowmere.Soldier)
				.WithTaunt()
				.Build(),
			// A mana sink that turns a stalled Human board into lethal.
			CardFactory
				.Creature("Village Ironsmith", manaCost: 2, power: 2, toughness: 2)
				.WithSubtype(Hollowmere.Human)
				.WithActivatedAbility(
					"Sharpen",
					manaCost: 1,
					effect: eb => eb.WithBoost(2, 0).WithTarget(Single().YourCreatures())
				)
				.Build(),
			// The Human that wants a full graveyard â€” this tribe's link to the set's spine.
			CardFactory
				.Creature("Gravewatch Sentry", manaCost: 2, power: 2, toughness: 2)
				.WithSubtype(Hollowmere.Human)
				.WithSubtype(Hollowmere.Soldier)
				.WithThreshold(power: 2, toughness: 2)
				.Build(),
			// Prowess on a Human body, bridging the tribe into the Spells theme.
			CardFactory
				.Creature("Hollowmere Apprentice", manaCost: 2, power: 1, toughness: 3)
				.WithSubtype(Hollowmere.Human)
				.WithSubtype(Hollowmere.Wizard)
				.WithTriggeredAbility(
					"Prowess",
					new EventTriggerCondition
					{
						EventTypeName = EventTypeNames.SpellCast,
						Filter = new IsControlledByYouSpecification(),
					},
					eb => eb.WithProwessBuff()
				)
				.Build(),
			// A second lord with a different axis, so two lords stack meaningfully.
			CardFactory
				.Creature("Mayor of Hollowmere", manaCost: 3, power: 2, toughness: 2)
				.WithSubtype(Hollowmere.Human)
				.WithComponent(
					new StaticPTBoostAbility
					{
						PowerBonus = 2,
						ToughnessBonus = 0,
						Filter = new IsSubtypeSpecification { Subtype = Hollowmere.Human }.And(
							TargetSpecification.OtherCreaturesYouControl()
						),
					}
				)
				.Build(),
			// A rescue effect â€” the payoff for the temporary keyword grants added in step 1.
			CardFactory
				.Creature("Parish Bell-Ringer", manaCost: 3, power: 2, toughness: 3)
				.WithSubtype(Hollowmere.Human)
				.WithSubtype(Hollowmere.Cleric)
				.WithEtbTrigger(
					"Sound the Alarm",
					eb => eb.WithGrantKeyword(taunt: true).WithTarget(AllValid().AllYourCreatures())
				)
				.Build(),
			// Three bodies in one card. The token payoff the tribe is built around.
			CardFactory
				.Spell("Hollowmere Muster", manaCost: 3)
				.WithCreateTokens(HollowmereTokens.Human(), count: 3)
				.Build(),
			// Removal on a Human body, gated behind having a tribe to sacrifice from.
			CardFactory
				.Creature("Devout Chaplain", manaCost: 3, power: 2, toughness: 2)
				.WithSubtype(Hollowmere.Human)
				.WithSubtype(Hollowmere.Cleric)
				.WithActivatedAbility(
					"Cast Out the Unclean",
					manaCost: 2,
					effect: eb => eb.WithAutoDestroy(),
					costs: cb => cb.SacrificeSubtype(Hollowmere.Human)
				)
				.Build(),
			// Efficient beater with reach through a stalled board.
			CardFactory
				.Creature("Chapel Guard", manaCost: 3, power: 3, toughness: 3)
				.WithSubtype(Hollowmere.Human)
				.WithSubtype(Hollowmere.Soldier)
				.WithLifelink()
				.Build(),
			// Recursion in tribal colours: finds a Human specifically, not just any creature.
			CardFactory
				.Creature("Parish Exhumer", manaCost: 3, power: 2, toughness: 2)
				.WithSubtype(Hollowmere.Human)
				.WithSubtype(Hollowmere.Cleric)
				.WithEtbTrigger(
					"Call the Congregation",
					eb =>
						eb.WithAction(
							new PipelineAction
							{
								Steps = ImmutableList.Create<GameAction>(
									new MillAction
									{
										Amount = 2,
										TargetContextKey = ContextKeys.CastingPlayerId,
									},
									new SelectCardFromZoneAction
									{
										Zone = ZoneType.Graveyard,
										Subtype = Hollowmere.Human,
										PlayerIdContextKey = ContextKeys.CastingPlayerId,
										OutputKey = "exhumer_target",
									},
									new MoveCardToHandAction
									{
										CardIdContextKey = "exhumer_target",
										PlayerIdContextKey = ContextKeys.CastingPlayerId,
									}
								),
							},
							TargetingStrategy.NoTarget()
						)
				)
				.Build(),
			// A cantrip on a body: the tribe's way to not flood out.
			CardFactory
				.Creature("Parish Cantor", manaCost: 3, power: 2, toughness: 2)
				.WithSubtype(Hollowmere.Human)
				.WithSubtype(Hollowmere.Wizard)
				.WithEtbTrigger("Sing the Roll", eb => eb.WithDraw(1))
				.Build(),
			// Permanently grows the team â€” the anthem that survives its own removal.
			CardFactory
				.Creature("Hollowmere Marshal", manaCost: 4, power: 3, toughness: 3)
				.WithSubtype(Hollowmere.Human)
				.WithSubtype(Hollowmere.Soldier)
				.WithEtbTrigger(
					"Drill the Watch",
					eb =>
						eb.WithAction(
							new AddModifierAction
							{
								PowerBonus = 1,
								ToughnessBonus = 1,
								Duration = ModifierDuration.Permanent,
							},
							TargetingStrategy.AllValid(
								new IsSubtypeSpecification { Subtype = Hollowmere.Human }.And(
									TargetSpecification.CreatureControlledByYou()
								)
							)
						)
				)
				.Build(),
			// Turns the tribe's width into a life buffer against the aggressive decks.
			CardFactory
				.Creature("Elder of the Parish", manaCost: 4, power: 2, toughness: 4)
				.WithSubtype(Hollowmere.Human)
				.WithSubtype(Hollowmere.Cleric)
				.WithTriggeredAbility(
					"Tend the Flock",
					new EventTriggerCondition
					{
						EventTypeName = EventTypeNames.CreatureEnteredBattlefield,
						Filter = new IsSubtypeSpecification { Subtype = Hollowmere.Human }.And(
							new IsControlledByYouSpecification()
						),
					},
					eb => eb.WithLifeGain(2)
				)
				.Build(),
			// Removal stapled to a body â€” the four-drop two-for-one bar.
			CardFactory
				.Creature("Hollowmere Bailiff", manaCost: 4, power: 3, toughness: 3)
				.WithSubtype(Hollowmere.Human)
				.WithSubtype(Hollowmere.Soldier)
				.WithEtbTrigger("Put Down", eb => eb.WithAutoDamage(3))
				.Build(),
			// A big trampling body for the tribe's top of curve.
			CardFactory
				.Creature("Riders of Hollowmere", manaCost: 4, power: 4, toughness: 4)
				.WithSubtype(Hollowmere.Human)
				.WithSubtype(Hollowmere.Soldier)
				.WithTrample()
				.Build(),
			// The alpha strike. In a no-blocker format a team pump with haste ends games.
			CardFactory
				.Creature("Parish Zealot", manaCost: 5, power: 4, toughness: 4)
				.WithSubtype(Hollowmere.Human)
				.WithEtbTrigger(
					"Righteous Fury",
					eb =>
						eb.WithAction(
								new AddModifierAction
								{
									PowerBonus = 1,
									ToughnessBonus = 1,
									Duration = ModifierDuration.UntilEndOfTurn,
								},
								AllValid().AllYourCreatures()
							)
							.WithGrantKeyword(haste: true)
							.WithTarget(AllValid().AllYourCreatures())
				)
				.Build(),
			// The defensive top-end, for the Human decks that want to go long.
			CardFactory
				.Creature("Cathedral Sanctifier", manaCost: 5, power: 4, toughness: 6)
				.WithSubtype(Hollowmere.Human)
				.WithSubtype(Hollowmere.Cleric)
				.WithTaunt()
				.WithLifelink()
				.Build(),
			// Wins on resolution in a tribal deck, which is the six-drop bar.
			CardFactory
				.Creature("Champion of the Chapel", manaCost: 6, power: 5, toughness: 5)
				.WithSubtype(Hollowmere.Human)
				.WithSubtype(Hollowmere.Soldier)
				.WithEtbTrigger(
					"Rally the Chapel",
					eb =>
						eb.WithCreateTokens(HollowmereTokens.Human(), count: 3)
							.WithGrantKeyword(haste: true)
							.WithTarget(AllValid().AllYourCreatures())
				)
				.Build(),
			// A cheap trick that protects the tribe's investment from a removal spell.
			CardFactory
				.Spell("Rally the Watch", manaCost: 2)
				.WithBoost(2, 2)
				.WithTarget(AllValid().AllYourCreatures())
				.WithFlashback(4)
				.Build(),
			// The tribal finisher spell â€” width plus evasion is lethal with no blockers.
			CardFactory
				.Spell("Hollowmere Procession", manaCost: 4)
				.WithCreateTokens(HollowmereTokens.Human(), count: 2)
				.WithCreateTokens(HollowmereTokens.Spirit(), count: 2)
				.Build(),
		];
}
