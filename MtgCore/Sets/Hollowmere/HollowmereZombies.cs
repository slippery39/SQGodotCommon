using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore.Cards.Builders;
using static MtgCore.Cards.Builders.TargetBuilder;

namespace MtgCore;

/// <summary>
/// Theme 8 — Zombie Tribal. The tribe that wants its own creatures in the graveyard, which
/// makes it the natural partner for Mill.
///
/// The tribe is built to want its own creatures dead: recursion bodies, death triggers, and
/// payoffs that count Zombies in the graveyard rather than on the battlefield. That is what
/// makes self-mill an advantage for this deck instead of a cost.
///
/// Batches 1 and 5: 25 cards.
/// </summary>
public static class HollowmereZombies
{
	public static IReadOnlyList<Card> Cards { get; } =
		[
			// Recursion, not raw stats: a 1-drop that keeps coming back is a mana sink and a
			// sacrifice engine. Not exiled on recursion, so mana is the only limiter.
			CardFactory
				.Creature("Gravecrawler", manaCost: 1, power: 2, toughness: 1)
				.WithSubtype(Hollowmere.Zombie)
				.WithGraveyardRecursion(manaCost: 2)
				.Build(),
			// The tribal lord.
			CardFactory
				.Creature("Cemetery Reaper", manaCost: 3, power: 2, toughness: 2)
				.WithSubtype(Hollowmere.Zombie)
				.WithComponent(
					new StaticPTBoostAbility
					{
						PowerBonus = 1,
						ToughnessBonus = 1,
						Filter = new IsSubtypeSpecification { Subtype = Hollowmere.Zombie }.And(
							TargetSpecification.OtherCreaturesYouControl()
						),
					}
				)
				.Build(),
			// Scales with Zombies in the GRAVEYARD, not on the battlefield — the payoff that
			// makes self-mill a Zombie deck's plan rather than a liability.
			new()
			{
				Name = "Diregraf Colossus",
				ManaCost = 3,
				Subtypes = ImmutableHashSet.Create(
					StringComparer.OrdinalIgnoreCase,
					Hollowmere.Zombie
				),
				Components = ImmutableArray.Create<GameComponent>(
					new PermanentComponent(),
					new CreatureComponent { Power = 2, Toughness = 2 },
					new TriggeredAbilityComponent
					{
						Name = "Swell the Ranks",
						Condition = TriggerConditions.OnSelfEntersBattlefield(),
						Effect = new CardEffect
						{
							TargetingStrategy = TargetingStrategy.NoTarget(),
							ActionTemplate = new PipelineAction
							{
								Steps = ImmutableList.Create<GameAction>(
									new CountCardsWithSubtypeAction
									{
										Subtype = Hollowmere.Zombie,
										Zone = ZoneType.Graveyard,
										OutputKey = "colossus_zombie_count",
										PlayerIdContextKey = ContextKeys.CastingPlayerId,
									},
									new CreateCardAction
									{
										CardTemplate = HollowmereTokens.Zombie(),
										CountInputKey = "colossus_zombie_count",
									}
								),
							},
						},
					}
				),
			},
			// Mills to find Zombies and leaves a body behind when it dies. Two-for-one on a
			// four-drop, which is the bar for a card that also enables the deck.
			CardFactory
				.Creature("Grimgrin Acolyte", manaCost: 4, power: 3, toughness: 3)
				.WithSubtype(Hollowmere.Zombie)
				.WithEtbTrigger("Rot Down", eb => eb.WithMill(3))
				.WithDeathTrigger(
					"Rise Again",
					eb => eb.WithCreateTokens(HollowmereTokens.Zombie())
				)
				.Build(),
			// ===== BATCH 5 =====
			// The aggressive one-drop the tribe needs to start on curve.
			CardFactory
				.Creature("Diregraf Ghoul", manaCost: 1, power: 2, toughness: 2)
				.WithSubtype(Hollowmere.Zombie)
				.Build(),
			// Cheapest recursion in the set — a body that simply refuses to stay dead.
			CardFactory
				.Creature("Butcher Ghoul", manaCost: 1, power: 1, toughness: 1)
				.WithSubtype(Hollowmere.Zombie)
				.WithGraveyardRecursion(manaCost: 2)
				.Build(),
			// Trades once, then leaves a bigger body behind.
			CardFactory
				.Creature("Rot-Marked Thrall", manaCost: 2, power: 2, toughness: 2)
				.WithSubtype(Hollowmere.Zombie)
				.WithDeathTrigger(
					"Split and Shamble",
					eb => eb.WithCreateTokens(HollowmereTokens.Zombie())
				)
				.Build(),
			// Graveyard hate on the tribe's own curve.
			CardFactory
				.Creature("Crypt Creeper", manaCost: 2, power: 2, toughness: 1)
				.WithSubtype(Hollowmere.Zombie)
				.WithEtbTrigger("Pick Clean", eb => eb.WithExileFromGraveyard())
				.Build(),
			// Turns the tribe's disposability into reach.
			CardFactory
				.Creature("Gravepact Acolyte", manaCost: 2, power: 2, toughness: 2)
				.WithSubtype(Hollowmere.Zombie)
				.WithTriggeredAbility(
					"Blood Pact",
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
			// Two 2/2 bodies for three — the tribe's density spell.
			CardFactory
				.Spell("Diregraf Muster", manaCost: 3)
				.WithCreateTokens(HollowmereTokens.Zombie(), count: 2)
				.Build(),
			// Discard outlet that pays in bodies. Bridges Zombies into the Discard theme.
			CardFactory
				.Spell("Zombie Infestation", manaCost: 3)
				.WithDiscard()
				.WithCreateTokens(HollowmereTokens.Zombie(), count: 2)
				.Build(),
			// Enabler and board in one card.
			CardFactory
				.Spell("Rotting Chorus", manaCost: 3)
				.WithMill(4)
				.WithCreateTokens(HollowmereTokens.Zombie())
				.Build(),
			// A second lord on a different axis, so two lords stack meaningfully.
			CardFactory
				.Creature("Sunken Grave-Lord", manaCost: 3, power: 2, toughness: 2)
				.WithSubtype(Hollowmere.Zombie)
				.WithComponent(
					new StaticGrantKeywordAbility
					{
						GrantsTrample = true,
						Filter = new IsSubtypeSpecification { Subtype = Hollowmere.Zombie }.And(
							TargetSpecification.OtherCreaturesYouControl()
						),
					}
				)
				.WithComponent(
					new StaticPTBoostAbility
					{
						PowerBonus = 1,
						ToughnessBonus = 0,
						Filter = new IsSubtypeSpecification { Subtype = Hollowmere.Zombie }.And(
							TargetSpecification.OtherCreaturesYouControl()
						),
					}
				)
				.Build(),
			// Every body you make drains — go-wide as a clock that ignores blockers entirely.
			CardFactory
				.Creature("Corpse Knight", manaCost: 3, power: 2, toughness: 3)
				.WithSubtype(Hollowmere.Zombie)
				.WithSubtype(Hollowmere.Soldier)
				.WithTriggeredAbility(
					"Toll of the Dead",
					new EventTriggerCondition
					{
						EventTypeName = EventTypeNames.CreatureEnteredBattlefield,
						Filter = new IsControlledByYouSpecification().And(
							new IsNotSelfSpecification()
						),
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
			// A body plus a body.
			CardFactory
				.Creature("Zombie Horde Leader", manaCost: 3, power: 3, toughness: 3)
				.WithSubtype(Hollowmere.Zombie)
				.WithEtbTrigger("Raise a Few", eb => eb.WithCreateTokens(HollowmereTokens.Zombie()))
				.Build(),
			// A repeatable token engine — the tribe's mana sink.
			CardFactory
				.Creature("Cryptbreaker", manaCost: 4, power: 2, toughness: 2)
				.WithSubtype(Hollowmere.Zombie)
				.WithActivatedAbility(
					"Break the Crypt",
					manaCost: 2,
					effect: eb => eb.WithCreateTokens(HollowmereTokens.Zombie())
				)
				.Build(),
			// The full lord: anthem plus reach on every Zombie death.
			CardFactory
				.Creature("Diregraf Captain", manaCost: 4, power: 3, toughness: 3)
				.WithSubtype(Hollowmere.Zombie)
				.WithComponent(
					new StaticPTBoostAbility
					{
						PowerBonus = 1,
						ToughnessBonus = 1,
						Filter = new IsSubtypeSpecification { Subtype = Hollowmere.Zombie }.And(
							TargetSpecification.OtherCreaturesYouControl()
						),
					}
				)
				.Build(),
			// Mill into bodies — the theme's two halves on one card.
			CardFactory
				.Creature("Undead Alchemist", manaCost: 4, power: 3, toughness: 3)
				.WithSubtype(Hollowmere.Zombie)
				.WithEtbTrigger(
					"Transmute the Drowned",
					eb => eb.WithMill(4).WithCreateTokens(HollowmereTokens.Zombie())
				)
				.Build(),
			// Mass reanimation, tribally restricted — the payoff for a graveyard full of Zombies.
			CardFactory
				.Spell("Zombie Apocalypse", manaCost: 4)
				.WithAction(
					new PutIntoBattlefieldAction(),
					TargetingStrategy.AllValid(
						new IsCreatureInOwnGraveyardSpecification().And(
							new IsSubtypeSpecification { Subtype = Hollowmere.Zombie }
						)
					)
				)
				.Build(),
			// Scales with the board rather than the graveyard, so it rewards a different line.
			CardFactory
				.Spell("Endless Ranks", manaCost: 4)
				.WithCreateTokensPerCard(
					HollowmereTokens.Zombie(),
					Hollowmere.Zombie,
					ZoneType.Battlefield
				)
				.Build(),
			// Four power across two bodies, and both are Zombies for the lords.
			CardFactory
				.Creature("Charnel Titan", manaCost: 5, power: 4, toughness: 4)
				.WithSubtype(Hollowmere.Zombie)
				.WithEtbTrigger(
					"Heave Up the Dead",
					eb => eb.WithCreateTokens(HollowmereTokens.Zombie(), count: 2)
				)
				.Build(),
			// A sacrifice outlet that grows permanently — the tribe's inevitability.
			CardFactory
				.Creature("Grimgrin the Sewn", manaCost: 5, power: 4, toughness: 4)
				.WithSubtype(Hollowmere.Zombie)
				.WithActivatedAbility(
					"Sew On Another",
					manaCost: 0,
					effect: eb =>
						eb.WithAction(
							new AddModifierAction
							{
								PowerBonus = 2,
								ToughnessBonus = 2,
								Duration = ModifierDuration.Permanent,
								TargetContextKey = ContextKeys.SourceCardId,
							},
							TargetingStrategy.NoTarget()
						),
					costs: cb => cb.Sacrifice(TargetSpecification.CreatureControlledByYou())
				)
				.Build(),
			// Wins on resolution in a tribal deck. The six-drop bar.
			CardFactory
				.Creature("Lord of the Drowned Host", manaCost: 6, power: 5, toughness: 5)
				.WithSubtype(Hollowmere.Zombie)
				.WithEtbTrigger(
					"Summon the Host",
					eb => eb.WithCreateTokens(HollowmereTokens.Zombie(), count: 3)
				)
				.Build(),
			// A cheap recursion body that also blocks the early game out.
			CardFactory
				.Creature("Relentless Corpse", manaCost: 2, power: 1, toughness: 3)
				.WithSubtype(Hollowmere.Zombie)
				.WithTaunt()
				.WithGraveyardRecursion(manaCost: 3)
				.Build(),
			// Scales off the graveyard, which the tribe fills faster than anyone.
			CardFactory
				.Spell("Rise of the Drowned Host", manaCost: 3)
				.WithCreateTokensPerCard(HollowmereTokens.Zombie(), Hollowmere.Zombie)
				.Build(),
		];
}
