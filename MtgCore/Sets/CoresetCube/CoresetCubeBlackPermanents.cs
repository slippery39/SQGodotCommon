using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore.Cards.Builders;
using static MtgCore.Cards.Builders.TargetBuilder;

namespace MtgCore;

/// <summary>
/// Black non-creature permanents from the Core Set Cube —
/// https://cubecobra.com/cube/list/magiccoreset20xx
///
/// All 11 — 5 enchantments, 4 Auras, 2 planeswalkers — in the cube's own order.
///
/// UPKEEP IS THIS FILE'S MECHANIC, and it was broken before this section. OnYourUpkeep() carried
/// no filter, and TurnStartedEvent's subject is the player whose turn began, so every upkeep
/// trigger in the engine fired on BOTH turns at double the printed rate. Dark Tutelage, Demonic
/// Pact and Call to the Grave are all clocks — running one at double speed is the difference
/// between a build-around and a card that kills you. Fixed in TriggerConditions.
///
/// AURAS ride the equipment attachment rails (EquipmentComponent.IsAura), as in white and blue:
/// they attach via an ETB trigger rather than at cast time, which with no priority window is
/// observationally identical.
///
/// DIVERGENCES beyond the colour and blocking cuts listed in CoresetCubeBlack:
///   - Crippling Blight's "can't block" is inert, so it is a bare -1/-1 Aura. That is a weak
///     card and is left weak rather than inflated: the cube is allowed to contain filler.
///   - Demonic Pact keeps all four modes INCLUDING "you lose the game", which is the entire
///     card. It needed ChosenModesComponent (a mode struck off permanently once taken) and
///     SetLifeTotalAction with Amount = 0 — the state-based loss check already owns everything
///     that follows from hitting zero, so there is no second way to lose.
///   - Sorin Markov's -7 controls a player for a turn. There is no concept of playing someone
///     else's turn, so it becomes the closest thing the engine can express to the same
///     game-ending swing.
///   - Liliana Vess's -8 reanimates from ALL graveyards, as printed, via ReanimateManyAction.
/// </summary>
public static class CoresetCubeBlackPermanents
{
	public static IReadOnlyList<Card> Cards { get; } =
		[
			// ===== ENCHANTMENTS =====

			// "Can't block" is inert with no blocking, leaving a plain -1/-1 Aura. Deliberately
			// not upgraded to compensate — a one-mana Aura that shrinks a creature permanently is
			// a real if minor effect, and padding filler to look playable distorts the draft.
			CardFactory
				.Enchantment("Crippling Blight", manaCost: 1)
				.AsAura(
					powerBonus: -1,
					toughnessBonus: -1,
					targeting: TargetingStrategy.SingleTarget(
						TargetSpecification.OpponentCreatures()
					)
				)
				.Build(),
			// The scry trigger fires on ANY creature dying, either side's — with the sacrifice
			// fix in place, this finally sees a sacrificed creature too, which is most of what it
			// is meant to trigger on.
			CardFactory
				.Enchantment("Shadows of the Past", manaCost: 2)
				.WithTriggeredAbility(
					"Echoes",
					TriggerConditions.OnAnyCreatureDies(),
					eb => eb.WithScry(1)
				)
				.WithActivatedAbility(
					"Drain the Past",
					manaCost: 5,
					effect: eb => eb.WithDrain(2),
					condition: new CreaturesInGraveyardCondition { Minimum = 4 },
					maxPerTurn: 0
				)
				.Build(),
			// "Lose life equal to its mana value" needed no new code: RevealTopCardAction already
			// publishes ContextKeys.RevealedCardManaCost (built for Dark Confidant) and
			// LoseLifeAction already reads AmountContextKey. The card is a genuine clock — the
			// life loss scales with your own curve, so a deck full of bombs pays for them twice.
			CardFactory
				.Enchantment("Dark Tutelage", manaCost: 3)
				.WithTriggeredAbility(
					"Tutelage",
					TriggerConditions.OnYourUpkeep(),
					eb =>
						eb.WithAction(
							new PipelineAction
							{
								Steps = ImmutableList.Create<GameAction>(
									new RevealTopCardAction
									{
										PlayerIdContextKey = ContextKeys.CastingPlayerId,
									},
									new MoveCardToHandAction
									{
										CardIdContextKey = ContextKeys.RevealedCardId,
										PlayerIdContextKey = ContextKeys.CastingPlayerId,
									},
									new LoseLifeAction
									{
										AmountContextKey = ContextKeys.RevealedCardManaCost,
										TargetContextKey = ContextKeys.CastingPlayerId,
									}
								),
							},
							TargetingStrategy.NoTarget()
						)
				)
				.Build(),
			// Every mode kept, including the fourth. onceEach strikes a mode off permanently once
			// taken, so this is a four-turn clock that ends the game — which is the card. Without
			// the exclusion it would just be a repeating value engine with a mode you never pick.
			CardFactory
				.Enchantment("Demonic Pact", manaCost: 4)
				.WithTriggeredAbility(
					"The Pact",
					TriggerConditions.OnYourUpkeep(),
					eb =>
						eb.WithModes(
							onceEach: true,
							(
								"Deal 4 damage and gain 4 life",
								new DrainLifeAction
								{
									Amount = 4,
									TargetOpponent = true,
									PlayerIdContextKey = ContextKeys.CastingPlayerId,
								}
							),
							(
								"Target opponent discards two cards",
								new PipelineAction
								{
									Steps = ImmutableList.Create<GameAction>(
										new DiscardRandomCardAction
										{
											TargetOpponent = true,
											PlayerIdContextKey = ContextKeys.CastingPlayerId,
										},
										new DiscardRandomCardAction
										{
											TargetOpponent = true,
											PlayerIdContextKey = ContextKeys.CastingPlayerId,
										}
									),
								}
							),
							(
								"Draw two cards",
								new DrawCardsAction
								{
									Amount = 2,
									TargetContextKey = ContextKeys.CastingPlayerId,
								}
							),
							(
								"You lose the game",
								new SetLifeTotalAction
								{
									Amount = 0,
									TargetContextKey = ContextKeys.CastingPlayerId,
								}
							)
						)
				)
				.Build(),
			// "Whenever a creature attacks you or a planeswalker you control" — with no blocking
			// there is no "attacks you" distinct from "attacks", so filtering the attacker to an
			// opponent's creature is the entire clause.
			CardFactory
				.Enchantment("Blood Reckoning", manaCost: 4)
				.WithTriggeredAbility(
					"Reckoning",
					TriggerConditions.OnCreatureAttacksYou(),
					eb => eb.WithAction(new LoseLifeAction { Amount = 1 }, AllValid().Opponent())
				)
				.Build(),
			// Printed as "at the beginning of EACH player's upkeep, THAT player sacrifices".
			// One symmetric edict on your own upkeep is the same effect on a slower clock: both
			// players lose a creature per round either way, and it halves the trigger traffic.
			// The self-sacrifice clause is dropped — it checks "no creatures on the battlefield",
			// which this card actively prevents from ever being true.
			CardFactory
				.Enchantment("Call to the Grave", manaCost: 5)
				.WithTriggeredAbility(
					"The Call",
					TriggerConditions.OnYourUpkeep(),
					eb => eb.WithSymmetricEdict(CoresetCubeBlack.Zombie)
				)
				.Build(),
			// ===== AURAS =====

			// The upkeep drain fires on the ENCHANTED creature's controller's turn, which is what
			// OnOpponentUpkeep exists for. Pointing it at OnYourUpkeep would have the victim
			// losing life on your turn instead — same total, wrong timing, and it would stack
			// oddly with your own upkeep triggers.
			CardFactory
				.Enchantment("Stab Wound", manaCost: 3)
				.AsAura(
					powerBonus: -2,
					toughnessBonus: -2,
					targeting: TargetingStrategy.SingleTarget(
						TargetSpecification.OpponentCreatures()
					)
				)
				.WithTriggeredAbility(
					"Festering Wound",
					TriggerConditions.OnOpponentUpkeep(),
					eb => eb.WithAction(new LoseLifeAction { Amount = 2 }, AllValid().Opponent())
				)
				.Build(),
			// "Is a Demon in addition to its other types" is dropped — nothing in the cube cares
			// about Demon as a tribe. The graveyard recast is kept with both its costs, which is
			// what makes this a recurring threat rather than a vanilla Aura.
			CardFactory
				.Enchantment("Demonic Embrace", manaCost: 3)
				.AsAura(
					powerBonus: 3,
					toughnessBonus: 1,
					flying: true,
					targeting: TargetingStrategy.SingleTarget(
						TargetSpecification.CreatureControlledByYou()
					)
				)
				.WithComponent(
					new FlashbackComponent
					{
						FlashbackManaCost = 3,
						AdditionalCosts = ImmutableList.Create<AdditionalCost>(
							new LifeAdditionalCost { Amount = 3 },
							new DiscardAdditionalCost { Count = 1 }
						),
					}
				)
				.Build(),
			CardFactory
				.Enchantment("Mark of the Vampire", manaCost: 4)
				.AsAura(
					powerBonus: 2,
					toughnessBonus: 2,
					lifelink: true,
					targeting: TargetingStrategy.SingleTarget(
						TargetSpecification.CreatureControlledByYou()
					)
				)
				.Build(),
			// ===== PLANESWALKERS =====

			// The -2 tutors to the TOP of the library rather than to hand, which is the printed
			// card and a real cost: you spend your next draw on it. SelectBestByManaCost is what
			// makes an unrestricted search mean anything — library order is random, so a
			// first-match search would just have found whatever was already on top.
			CardFactory
				.Planeswalker("Liliana Vess", manaCost: 5)
				.WithLoyalty(5)
				.WithLoyaltyAbility(
					"+1: Target player discards a card",
					1,
					eb => eb.WithOpponentDiscard(1)
				)
				.WithLoyaltyAbility(
					"-2: Search your library, put that card on top",
					-2,
					eb =>
						eb.WithAction(
							new PipelineAction
							{
								Steps = ImmutableList.Create<GameAction>(
									new SelectCardFromLibraryAction
									{
										SelectBestByManaCost = true,
										OutputKey = "vess_tutor",
										PlayerIdContextKey = ContextKeys.CastingPlayerId,
									},
									new PutOnLibraryAction
									{
										TargetContextKey = "vess_tutor",
										Bottom = false,
									}
								),
							},
							TargetingStrategy.NoTarget()
						)
				)
				.WithLoyaltyAbility(
					"-8: Return all creatures from all graveyards under your control",
					-8,
					eb =>
						eb.WithAction(
							new ReanimateManyAction { Count = 99, FromAllGraveyards = true },
							TargetingStrategy.NoTarget()
						)
				)
				.Build(),
			// The +2 and -3 are both faithful; -3 is why SetLifeTotalAction exists, and it is
			// deliberately not routed through the replacement engine, since a life-gain bonus
			// must not turn "becomes 10" into "becomes 11".
			//
			// The -7 controls a player for a turn. There is no concept of playing an opponent's
			// turn — no priority, no way to hand the action generator to the other seat — so it
			// becomes the outcome that ultimate actually buys: their board is emptied and they
			// take a turn's worth of damage they cannot answer.
			CardFactory
				.Planeswalker("Sorin Markov", manaCost: 6)
				.WithLoyalty(4)
				.WithLoyaltyAbility(
					"+2: Deal 2 damage to any target and gain 2 life",
					2,
					eb => eb.WithDrain(2)
				)
				.WithLoyaltyAbility(
					"-3: Target opponent's life total becomes 10",
					-3,
					eb => eb.WithAction(new SetLifeTotalAction { Amount = 10 }, Single().Opponent())
				)
				.WithLoyaltyAbility(
					"-7: Destroy all creatures an opponent controls",
					-7,
					eb => eb.WithDestroy().WithTarget(AllValid().OpponentCreatures())
				)
				.Build(),
		];
}
