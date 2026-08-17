using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore.Cards.Builders;
using static MtgCore.Cards.Builders.TargetBuilder;

namespace MtgCore;

/// <summary>
/// Blue instants and sorceries from the Core Set Cube — 18 instants and 11 sorceries.
/// https://cubecobra.com/cube/list/magiccoreset20xx
///
/// COUNTERSPELLS ARE TRAPS. Six of these are never cast: they sit in hand and fire automatically
/// when the opponent casts a matching spell, paid for with mana you chose not to spend. This
/// engine has a stack but no priority — the non-active player never acts on your turn — so a
/// counterspell cannot be cast in response to anything. See CounterTrapComponent for the full
/// design; the short version is that holding the card and leaving the mana up IS the decision.
///
/// OTHER DIVERGENCES, each also commented on its card:
///   - No colours, so Aether Gust's "red or green" and every protection-from-a-colour clause is
///     unexpressible.
///   - Combat resolves instantly, so nothing is ever observably "attacking" — Aetherspouts and
///     Condemn-style cards target any creature instead.
///   - Talent of the Telepath cannot cast from an opponent's library; there is no path for one
///     player to cast another's cards.
/// </summary>
public static class CoresetCubeBlueSpells
{
	public static IReadOnlyList<Card> Cards { get; } =
		[
			// ===== INSTANTS =====

			// {X}{U}: a trap is never cast, so there is no moment to choose X. The tax becomes
			// whatever mana the trapper still had after paying for the trap — spending everything
			// you held up is a fair reading of X.
			CardFactory
				.Instant("Clash of Wills", manaCost: 1)
				.AsCounterTrap(taxAllRemaining: true)
				.Build(),
			CardFactory
				.Instant("Opt", manaCost: 1)
				.WithScry(1)
				.WithDraw(1)
				.WithTarget(TargetingStrategy.Self())
				.Build(),
			CardFactory
				.Instant("Unsummon", manaCost: 1)
				.WithBounce()
				.WithTarget(Single().OpponentCreatures())
				.Build(),
			// "Red or green" is unexpressible — no colours. Kept as top-of-library removal, which
			// is the card's actual function.
			CardFactory
				.Instant("Aether Gust", manaCost: 2)
				.WithAction(
					new MoveCardToTopOfLibraryAction(),
					TargetingStrategy.SingleTarget(TargetSpecification.OpponentCreatures())
				)
				.Build(),
			CardFactory
				.Instant("Disperse", manaCost: 2)
				.WithBounce()
				.WithTarget(
					Single()
						.WithSpec(
							new IsNotCardTypeSpecification { Types = CardType.Land }
								.And(new IsOnBattlefieldSpecification())
								.And(new IsControlledByOpponentSpecification())
						)
				)
				.Build(),
			CardFactory
				.Instant("Essence Scatter", manaCost: 2)
				.AsCounterTrap(targetTypes: CardType.Creature)
				.Build(),
			CardFactory.Instant("Mana Leak", manaCost: 2).AsCounterTrap(manaTax: 3).Build(),
			CardFactory
				.Instant("Negate", manaCost: 2)
				.AsCounterTrap(excludeTypes: CardType.Creature)
				.Build(),
			// Spell mastery is finally expressible: CardType makes "instant and/or sorcery cards
			// in your graveyard" a real question. Without it, the freeze is a plain tap.
			CardFactory
				.Instant("Send to Sleep", manaCost: 2)
				.WithExhaust()
				.WithTarget(AllValid().OpponentCreatures())
				.WithConditionalAction(
					new SpellMasteryCondition(),
					new ExhaustCreatureAction { FreezeTurns = 1 }
				)
				.Build(),
			CardFactory.Instant("Turn to Frog", manaCost: 2).WithBecomesVanilla(1, 1).Build(),
			// The "return target spell" half is the trap; returning a creature is the other mode.
			// Modelled as the trap, since that is the harder half to reach any other way.
			CardFactory
				.Instant("Unsubstantiate", manaCost: 2)
				.AsCounterTrap(returnToHandInstead: true)
				.Build(),
			CardFactory.Instant("Dissipate", manaCost: 3).AsCounterTrap(exileInstead: true).Build(),
			CardFactory
				.Instant("Frost Breath", manaCost: 3)
				.WithFreeze(1)
				.WithTarget(AllValid().OpponentCreatures())
				.Build(),
			CardFactory
				.Instant("Polymorphist's Jest", manaCost: 3)
				.WithAction(
					new AddCustomModifierAction
					{
						Modifier = new BecomesBaseCreatureComponent
						{
							Power = 1,
							Toughness = 1,
							Duration = ModifierDuration.UntilEndOfTurn,
						},
					},
					TargetingStrategy.AllValid(TargetSpecification.OpponentCreatures())
				)
				.Build(),
			CardFactory
				.Instant("Uncomfortable Chill", manaCost: 3)
				.WithWeaken(2, 0)
				.WithTarget(AllValid().OpponentCreatures())
				.WithDraw(1)
				.WithTarget(TargetingStrategy.Self())
				.Build(),
			CardFactory
				.Instant("Bone to Ash", manaCost: 4)
				.AsCounterTrap(targetTypes: CardType.Creature, drawOnCounter: 1)
				.Build(),
			CardFactory
				.Instant("Rain of Revelation", manaCost: 4)
				.WithDraw(3)
				.WithTarget(TargetingStrategy.Self())
				.WithDiscard(1)
				.Build(),
			// "For each ATTACKING creature" — nothing is ever observably attacking, since combat
			// resolves in a single action. Hits every creature the opponent controls instead.
			CardFactory
				.Instant("Aetherspouts", manaCost: 5)
				.WithAction(
					new MoveCardToTopOfLibraryAction(),
					TargetingStrategy.AllValid(TargetSpecification.OpponentCreatures())
				)
				.Build(),
			// ===== SORCERIES =====

			// "Put them back in any order" needs a reorder step the choice system has no shape
			// for. Scry 3 keeps the selection without the ordering.
			CardFactory
				.Sorcery("Ponder", manaCost: 1)
				.WithScry(3)
				.WithDraw(1)
				.WithTarget(TargetingStrategy.Self())
				.Build(),
			CardFactory
				.Sorcery("Preordain", manaCost: 1)
				.WithScry(2)
				.WithDraw(1)
				.WithTarget(TargetingStrategy.Self())
				.Build(),
			CardFactory
				.Sorcery("Void Snare", manaCost: 1)
				.WithBounce()
				.WithTarget(
					Single()
						.WithSpec(
							new IsNotCardTypeSpecification { Types = CardType.Land }
								.And(new IsOnBattlefieldSpecification())
								.And(new IsControlledByOpponentSpecification())
						)
				)
				.Build(),
			// X spell: the chosen X reaches the effect through ContextKeys.XValue.
			CardFactory
				.Sorcery("Mind Spring", manaCost: 2)
				.WithXCost()
				.WithAction(
					new DrawCardsAction
					{
						AmountContextKey = ContextKeys.XValue,
						TargetContextKey = ContextKeys.CastingPlayerId,
					},
					TargetingStrategy.NoTarget()
				)
				.Build(),
			CardFactory
				.Sorcery("Anchor to the Aether", manaCost: 3)
				.WithAction(
					new MoveCardToTopOfLibraryAction(),
					TargetingStrategy.SingleTarget(TargetSpecification.OpponentCreatures())
				)
				.WithScry(1)
				.Build(),
			CardFactory
				.Sorcery("Winged Words", manaCost: 3)
				.WithCostReduction(1, new ControlsFlyingCreatureCondition())
				.WithDraw(2)
				.WithTarget(TargetingStrategy.Self())
				.Build(),
			CardFactory.Sorcery("Drawn from Dreams", manaCost: 4).WithDig(7).WithDig(7).Build(),
			CardFactory
				.Sorcery("Sleep", manaCost: 4)
				.WithFreeze(1)
				.WithTarget(AllValid().OpponentCreatures())
				.Build(),
			// Casting from an opponent's library has no path — no player can cast another's
			// cards. Reskinned to the mill half plus card draw, which is the same tempo swing.
			CardFactory
				.Sorcery("Talent of the Telepath", manaCost: 4)
				.WithMill(7)
				.WithTarget(Single().Opponent())
				.WithConditionalAction(
					new SpellMasteryCondition(),
					new DrawCardsAction { Amount = 2 }
				)
				.Build(),
			CardFactory
				.Sorcery("Talrand's Invocation", manaCost: 4)
				.WithCreateTokens(CoresetCubeBlueTokens.Drake(), count: 2)
				.Build(),
			CardFactory.Sorcery("Time Warp", manaCost: 5).WithExtraTurn().Build(),
		];
}
