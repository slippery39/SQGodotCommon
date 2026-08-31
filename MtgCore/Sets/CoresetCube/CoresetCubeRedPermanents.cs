using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore.Cards.Builders;
using static MtgCore.Cards.Builders.TargetBuilder;

namespace MtgCore;

/// <summary>
/// Red non-creature permanents from the Core Set Cube — 4 enchantments (one an Aura),
/// 1 artifact, 4 planeswalkers.
///
/// THE PLANESWALKERS ARE THE REASON RED WENT FIRST-CLASS. Four of the nine cards here are Chandras
/// or Sarkhan, and until this section no spell in the engine could damage a planeswalker at all —
/// they were answerable only by attacking them. Fixed alongside the red creatures; see
/// "Planeswalkers" in MtgCore/CLAUDE.md.
///
/// DIVERGENCES FROM PRINTED CARDS:
///   - NO ABILITY GRANTING. Burning Anger gives the enchanted creature an activated ability, and
///     nothing here can grant one. Reskinned onto the Aura itself — see its comment.
///   - NO TOKEN COPYING of an arbitrary permanent, and no "exile it at end of turn". Flameshadow
///     Conjuring is rebuilt around the part that survives, which is the hasty extra body.
///   - NO ABILITY COPYING, so Chandra's Regulator's "copy that loyalty ability" is unexpressible.
///   - LOYALTY COSTS ARE FIXED, so Chandra Nalaar's "-X" has no home; it becomes a fixed -3.
///   - "That creature can't block this turn" (Chandra, Pyromaster's +1) is inert with no blocking.
///   - NO CASTING FROM THE LIBRARY, so Chandra, Heart of Fire's emblem is reskinned to a recurring
///     burn trigger, which is the same "you win from here" role.
/// </summary>
public static class CoresetCubeRedPermanents
{
	public static IReadOnlyList<Card> Cards { get; } =
		[
			// ===== ENCHANTMENTS =====

			// WAS a sacrifice outlet: "{0}, sacrifice a creature: deal 1 damage". It measured at
			// the bottom of the model twice over, and the reason was never the rate — the AI is
			// offered exactly ONE sacrifice payment, and until SacrificeAdditionalCost was sorted
			// worst-first that payment was its best creature, which StateEvaluator correctly
			// refuses forever. Even sorted, paying a whole creature for 1 damage is a trade the
			// search will almost never take, so the card stayed a near-blank enchantment.
			//
			// Rebuilt as a PAYOFF rather than an outlet: it now rewards deaths the deck was going
			// to suffer anyway instead of asking the AI to manufacture them. That removes the
			// decision the AI was bad at and keeps the card's identity — a board of dying tokens
			// still converts into reach, it just no longer costs an activation to do it.
			//
			// Random targeting because a death trigger has no targeting window; same shape as the
			// divided-damage cards in this section.
			CardFactory
				.Enchantment("Barrage of Expendables", manaCost: 2)
				.WithTriggeredAbility(
					"Expend",
					TriggerConditions.OnCreatureYouControlDies(),
					eb =>
						eb.WithDamage(1)
							.WithTarget(
								TargetingStrategy.RandomTarget(
									TargetSpecification.OpponentOrOpponentCreatures()
								)
							)
				)
				.Build(),
			// "Discard a land card" is a genuine cost here, not a reskin: a land in hand is a real
			// card and pitching one gives up a mana drop. This and Magmatic Insight are why
			// DiscardAdditionalCost gained a Filter.
			CardFactory
				// Damage raised 2 -> 3 as a balance probe. The card reads well — it turns flood into
				// reach — but measured near the bottom of the model, and it is not obvious whether
				// that is the rate or the AI. If 3 does not move it, the rate was never the problem
				// and the next place to look is how the search values discarding a land: the
				// evaluator counts non-land cards in hand only, so pitching a land costs it nothing
				// on paper and the damage should already look free.
				.Enchantment("Molten Vortex", manaCost: 1)
				.WithActivatedAbility(
					"Vent",
					manaCost: 0,
					effect: eb =>
						eb.WithDamage(3).WithTarget(Single().OpponentOrOpponentCreatures()),
					costs: cb => cb.Discard(1, "Land"),
					maxPerTurn: 1
				)
				.Build(),
			// Printed: "whenever a nontoken creature enters under your control, you may pay {R};
			// if you do, create a token copy of it with haste, then exile it at end of turn."
			// Three separate things the engine lacks — copying an arbitrary permanent into a
			// token, an optional cost on a trigger, and a delayed exile. What survives is the part
			// that actually plays: every creature you cast brings a hasty friend. A fixed 3/1
			// Elemental stands in for the copy, so the effect no longer scales with what entered.
			// maxPerTurn IS LOAD-BEARING AND ITS ABSENCE HUNG THE ENGINE. The printed card says
			// "whenever a NONTOKEN creature enters"; nothing here can express "nontoken", so
			// without a cap the token this makes is itself a creature entering, which re-triggers
			// the ability, forever. Not a slow game — ProcessAllActions never returns, because the
			// per-turn action limit lives in GameRunner rather than in the engine. It wedged
			// training threads permanently.
			CardFactory
				.Enchantment("Flameshadow Conjuring", manaCost: 4)
				.WithTriggeredAbility(
					"Conjure",
					TriggerConditions.OnCreatureYouControlEnters(),
					eb => eb.WithCreateTokens(CoresetCubeRedTokens.HastyElemental(), 1),
					maxPerTurn: 1
				)
				.Build(),
			// Printed: "enchanted creature has '{T}: deals damage equal to its power to any
			// target'". Granting an ACTIVATED ability is not something any card here can do, so
			// the ability lives on the Aura instead. That loses the scaling with the creature's
			// power and the tap cost, so it is capped at once per turn to keep it honest, and the
			// Aura still buffs its host so enchanting something is still the point.
			CardFactory
				.Enchantment("Burning Anger", manaCost: 5)
				.AsAura(powerBonus: 2, toughnessBonus: 0, targeting: Single().YourCreatures())
				.WithActivatedAbility(
					"Channel Anger",
					manaCost: 0,
					effect: eb =>
						eb.WithDamage(3).WithTarget(Single().OpponentOrOpponentCreatures()),
					maxPerTurn: 1
				)
				.Build(),
			// ===== ARTIFACT =====

			// Printed as a looter plus "whenever you activate a loyalty ability of a Chandra, you
			// may pay {1} to copy it". Copying an ability does not exist, and building it for one
			// card in the cube is not worth it — the looting half is the half that plays, and it
			// fixes red's worst problem, which is flooding out with a hand of lands.
			CardFactory
				.Artifact("Chandra's Regulator", manaCost: 2)
				.WithActivatedAbility(
					"Regulate",
					manaCost: 2,
					effect: eb => eb.WithDiscard(1).WithDraw(2).WithTarget(TargetingStrategy.Self())
				)
				.Build(),
			// ===== PLANESWALKERS =====

			// "+1: 1 damage to target player and 1 damage to up to one target creature that player
			// controls. That creature can't block this turn." The can't-block half is inert. The 0
			// is the impulse draw the mechanic was built for.
			CardFactory
				.Planeswalker("Chandra, Pyromaster", manaCost: 4)
				.WithLoyalty(4)
				.WithLoyaltyAbility(
					"+1: Deal 1 damage to target opponent and 1 to a creature they control",
					1,
					eb =>
						eb.WithDamage(1)
							.WithTarget(AllValid().Opponent())
							.WithDamage(1)
							.WithTarget(Random().OpponentCreatures())
				)
				.WithLoyaltyAbility("0: Impulse draw", 0, eb => eb.WithImpulseDraw())
				.WithLoyaltyAbility(
					"-7: Exile the top five cards; you may play them this turn",
					-7,
					eb =>
					{
						// Printed as ten. Five is the same effect at a scale the rest of the game
						// can keep up with — and every one of them expires at end of turn, so the
						// tenth card was never going to be cast anyway.
						for (var i = 0; i < 5; i++)
							eb.WithImpulseDraw();
					}
				)
				.Build(),
			// The -X becomes a fixed -3: loyalty costs are ints on the ability, and an X-cost
			// loyalty ability would need the cast-time X machinery that lives on CastSpellAction.
			CardFactory
				.Planeswalker("Chandra Nalaar", manaCost: 5)
				.WithLoyalty(6)
				.WithLoyaltyAbility(
					"+1: Deal 1 damage to target opponent",
					1,
					eb => eb.WithDamage(1).WithTarget(AllValid().Opponent())
				)
				.WithLoyaltyAbility(
					"-3: Deal 4 damage to target creature",
					-3,
					eb => eb.WithDamage(4).WithTarget(Single().OpponentCreatures())
				)
				.WithLoyaltyAbility(
					"-8: Deal 10 damage to target opponent and each creature they control",
					-8,
					eb =>
						eb.WithDamage(10)
							.WithTarget(AllValid().Opponent())
							.WithDamage(10)
							.WithTarget(AllValid().OpponentCreatures())
				)
				.Build(),
			// Printed +1 is "discard your hand, then exile the top three cards; you may play them
			// this turn". The discard-your-hand cost is dropped — there is no "discard your whole
			// hand" verb and a count-based one would be wrong on an empty hand — so the impulse is
			// cut from three to two to pay for losing the drawback.
			CardFactory
				.Planeswalker("Chandra, Heart of Fire", manaCost: 5)
				.WithLoyalty(4)
				.WithLoyaltyAbility(
					"+1: Exile the top two cards; you may play them this turn",
					1,
					eb =>
					{
						eb.WithImpulseDraw();
						eb.WithImpulseDraw();
					}
				)
				.WithLoyaltyAbility(
					"-2: Deal 2 damage to any target",
					-2,
					eb => eb.WithDamage(2).WithTarget(Single().PlayersOrCreatures())
				)
				.WithLoyaltyAbility(
					"-7: You get an emblem that burns each upkeep",
					-7,
					eb =>
						eb.WithAction(
							// Printed emblem is "you may cast spells from the top of your library",
							// and there is no path for casting from the library. A recurring burn
							// trigger fills the same role: the game ends shortly after this.
							new GrantEmblemAction
							{
								Emblem = new Emblem
								{
									Name = "Chandra's Fury",
									Condition = TriggerConditions.OnYourUpkeep(),
									Effect = new CardEffect
									{
										TargetingStrategy = TargetingStrategy.AllValid(
											new IsPlayerSpecification().And(
												new IsControlledByOpponentSpecification()
											)
										),
										ActionTemplate = new DealDamageAction { Amount = 3 },
									},
								},
							},
							TargetingStrategy.Self()
						)
				)
				.Build(),
			// The +1's "spend this only on Dragon spells" is unexpressible — mana here is colourless
			// and untyped — so it is plain ramp. AddTemporaryManaAction raises CurrentMana only, so
			// it evaporates next turn and stays a "cast something big NOW" button rather than ramp.
			CardFactory
				.Planeswalker("Sarkhan, Fireblood", manaCost: 3)
				.WithLoyalty(3)
				.WithLoyaltyAbility("+1: Add 2 mana", 1, eb => eb.WithAddMana(2))
				.WithLoyaltyAbility(
					"-3: Discard a card, then draw two cards",
					-3,
					eb => eb.WithDiscard(1).WithDraw(2).WithTarget(TargetingStrategy.Self())
				)
				.WithLoyaltyAbility(
					"-7: Create three 4/4 Dragons with flying",
					-7,
					eb => eb.WithCreateTokens(CoresetCubeRedTokens.Dragon(), 3)
				)
				.Build(),
		];
}
