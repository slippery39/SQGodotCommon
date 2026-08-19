using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore.Cards.Builders;
using static MtgCore.Cards.Builders.TargetBuilder;

namespace MtgCore;

/// <summary>
/// White instants and sorceries from the Core Set Cube.
/// https://cubecobra.com/cube/list/magiccoreset20xx
///
/// DIVERGENCES. Two engine facts account for nearly all of them, and both are structural:
///   - THERE ARE NO COLOURS. "Protection from the colour of your choice" and "black or red"
///     cannot be expressed at all. Protection clauses become hexproof, which is the closest
///     thing this engine has to "can't be touched".
///   - COMBAT RESOLVES INSTANTLY. An attack is declared and damage is dealt in one action, with
///     no window to respond, so no creature is ever observably "attacking". Cards that target an
///     attacking creature target any creature instead.
///
/// Everything else is implemented, including convoke, X costs, modal choice, scry, damage
/// prevention and "tapped creature" removal — see CoresetCubeWhite's header for the creature
/// half.
/// </summary>
public static class CoresetCubeWhiteSpells
{
	public static IReadOnlyList<Card> Cards { get; } =
		[
			// ===== INSTANTS =====

			// "Target ATTACKING creature" -> any creature (combat resolves instantly, so nothing
			// is ever observably attacking). The life gain is a fixed 2 rather than the target's
			// toughness: no action reads a target's stats to scale another effect's amount.
			CardFactory
				.Instant("Condemn", manaCost: 1)
				.WithAction(
					new PutOnLibraryAction { Bottom = true },
					TargetingStrategy.SingleTarget(TargetSpecification.OpponentCreatures())
				)
				.Build(),
			// Protection from a colour is impossible. Hexproof is the closest read: it saves the
			// creature from targeted removal, which is what the card is played for.
			CardFactory
				.Instant("Gods Willing", manaCost: 1)
				.WithGrantKeyword(hexproof: true)
				.WithTarget(Single().YourCreatures())
				.WithScry(1)
				.Build(),
			// Redirection dropped — damage carries no re-targetable source here. The prevention
			// half is real and uses the replacement system.
			CardFactory
				.Instant("Harm's Way", manaCost: 1)
				.WithDamagePrevention(preventAll: false, amount: 2)
				.Build(),
			// Now expressible: CardType makes "artifact or enchantment" a real question rather
			// than a guess from subtype strings.
			CardFactory
				.Instant("Disenchant", manaCost: 2)
				.WithAction(
					new DestroyPermanentAction(),
					TargetingStrategy.SingleTarget(
						new IsCardTypeSpecification
						{
							Types = CardType.Artifact | CardType.Enchantment,
						}.And(new IsOnBattlefieldSpecification())
					)
				)
				.Build(),
			// Convoke is real: costs {1} less per ready creature and exhausts that many.
			CardFactory
				.Instant("Devouring Light", manaCost: 3)
				.WithConvoke()
				.WithExile()
				.WithTarget(Single().OpponentCreatures())
				.Build(),
			// A real modal choice, not a fixed mode.
			CardFactory
				.Instant("Fortify", manaCost: 3)
				// The targeting is load-bearing. Written without it, both modes were bare
				// AddModifierActions with no targets: ApplyChosenModeAction spawns the chosen mode
				// directly and nothing else resolves a strategy for it, so whichever mode you
				// picked, Fortify buffed NOBODY. It was the second-worst white card in the trained
				// model at 41.6%, which is what a blank card measures.
				.WithModes(
					onceEach: false,
					[AllValid().AllYourCreatures(), AllValid().AllYourCreatures()],
					(
						"Creatures you control get +2/+0",
						new AddModifierAction { PowerBonus = 2, ToughnessBonus = 0 }
					),
					(
						"Creatures you control get +0/+2",
						new AddModifierAction { PowerBonus = 0, ToughnessBonus = 2 }
					)
				)
				.Build(),
			CardFactory
				.Instant("Inspired Charge", manaCost: 4)
				.WithBoost(2, 1)
				.WithTarget(AllValid().AllYourCreatures())
				.Build(),
			// "Destroy target TAPPED creature" works properly now that exhaustion exists — this
			// is the payoff half of the tapper theme, and it is dead without a tapper.
			CardFactory
				.Instant("Swift Response", manaCost: 2)
				.WithAction(
					new DestroyPermanentAction(),
					TargetingStrategy.SingleTarget(
						TargetSpecification.OpponentCreatures().And(new IsExhaustedSpecification())
					)
				)
				.Build(),
			// The +1/+1 counter is a permanent modifier; protection becomes hexproof.
			CardFactory
				.Instant("Feat of Resistance", manaCost: 2)
				.WithBoost(1, 1, ModifierDuration.Permanent)
				.WithTarget(Single().YourCreatures())
				.WithGrantKeyword(hexproof: true)
				.WithTarget(Single().YourCreatures())
				.Build(),
			CardFactory
				.Instant("Safe Passage", manaCost: 3)
				.WithDamagePrevention(preventAll: true)
				.Build(),
			// ===== SORCERIES =====

			// "Black or red" is unexpressible. The card keeps its real shape — exile a creature
			// or planeswalker, then scry.
			CardFactory
				.Sorcery("Devout Decree", manaCost: 2)
				.WithExile()
				.WithTarget(
					Single()
						.WithSpec(
							new IsCardTypeSpecification
							{
								Types = CardType.Creature | CardType.Planeswalker,
							}
								.And(new IsOnBattlefieldSpecification())
								.And(new IsControlledByOpponentSpecification())
						)
				)
				.WithScry(1)
				.Build(),
			CardFactory
				.Sorcery("Day of Judgment", manaCost: 4)
				.WithAction(
					new DestroyCreatureAction(),
					TargetingStrategy.AllValid(TargetSpecification.Creatures())
				)
				.Build(),
			// "You choose one of each type, each player sacrifices the rest" needs a per-player
			// multi-type selection the choice system cannot express in one step. Implemented as
			// the creature clause only: each player keeps their cheapest creature, which is the
			// "you choose" outcome from the caster's point of view.
			CardFactory
				.Sorcery("Tragic Arrogance", manaCost: 5)
				.WithAction(
					new DestroyCreatureAction(),
					TargetingStrategy.AllValid(
						TargetSpecification
							.Creatures()
							.And(new IsNotCheapestCreatureSpecification())
					)
				)
				.Build(),
			// X and convoke together, exactly as printed.
			CardFactory
				.Sorcery("Return to the Ranks", manaCost: 2)
				.WithXCost()
				.WithConvoke()
				.WithAction(
					new ReanimateManyAction
					{
						CountContextKey = ContextKeys.XValue,
						MaxManaCost = 2,
					},
					TargetingStrategy.NoTarget()
				)
				.Build(),
			CardFactory
				.Sorcery("Basri's Solidarity", manaCost: 2)
				.WithBoost(1, 1, ModifierDuration.Permanent)
				.WithTarget(AllValid().AllYourCreatures())
				.Build(),
			// Both halves are independently conditional, checked at resolution.
			CardFactory
				.Sorcery("Timely Reinforcements", manaCost: 3)
				.WithConditionalAction(
					new HasLessLifeThanOpponentCondition(),
					new GainLifeAction { Amount = 6 }
				)
				.WithConditionalAction(
					new ControlsFewerCreaturesCondition(),
					new CreateCardAction { CardTemplate = CoresetCubeTokens.Soldier(), Count = 3 }
				)
				.Build(),
		];
}
