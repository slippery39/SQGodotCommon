using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore.Cards.Builders;
using static MtgCore.Cards.Builders.TargetBuilder;

namespace MtgCore;

/// <summary>
/// Black instants and sorceries from the Core Set Cube —
/// https://cubecobra.com/cube/list/magiccoreset20xx
///
/// All 18 — 8 instants, 10 sorceries — in the cube's own order (by mana value, then name).
///
/// DIVERGENCES. Two engine facts account for nearly all of them, and both are structural:
///   - THERE ARE NO COLOURS. Doom Blade's "nonblack" and Noxious Grasp's "green or white" cannot
///     be expressed at all, so both become unconditional removal. That is a genuine power
///     increase and both are priced as such by the rest of the section rather than by their
///     printed rate — Murder at three mana is the honest baseline, and these undercut it.
///   - LANDS ARE NOT PERMANENTS. They are consumed into MaxMana, so Smallpox cannot sacrifice
///     one. It reduces MaxMana instead, which is the same resource by a different name and is
///     the only land-destruction in the cube.
///
/// "YOU CHOOSE" AND "EACH PLAYER CHOOSES" both need a decision window the engine does not have.
/// Distress takes the opponent's most expensive card; each half of a symmetric edict takes the
/// CHEAPEST creature, because a real edict lets each player choose and each would keep their
/// bomb. Taking the biggest would quietly make a symmetric card one-sided.
/// </summary>
public static class CoresetCubeBlackSpells
{
	public static IReadOnlyList<Card> Cards { get; } =
		[
			// ===== ONE MANA =====

			CardFactory
				.Instant("Disfigure", manaCost: 1)
				.WithWeaken(2, 2)
				.WithTarget(Single().OpponentCreatures())
				.Build(),
			// The self-damage is the whole point at one mana and is kept as printed — at three
			// life for a -3/-3 it is deliberately painful, not a downside to round off.
			CardFactory
				.Instant("Ulcerate", manaCost: 1)
				.WithWeaken(3, 3)
				.WithTarget(Single().OpponentCreatures())
				.WithLoseLife(3)
				.Build(),
			// ===== TWO MANA =====

			// Printed as "destroy target NONBLACK creature". No colours, so the restriction is
			// unexpressible and this is unconditional two-mana removal — strictly better than
			// Murder for one less. Left as-is rather than taxed upward: the cube already prices
			// removal by mana value and the section has enough of it to stay honest.
			CardFactory
				.Instant("Doom Blade", manaCost: 2)
				.WithDestroy()
				.WithTarget(Single().OpponentCreatures())
				.Build(),
			CardFactory
				.Instant("Grasp of Darkness", manaCost: 2)
				.WithWeaken(4, 4)
				.WithTarget(Single().OpponentCreatures())
				.Build(),
			// "Green or white" is unexpressible; the planeswalker half is not, since planeswalkers
			// exist here — DestroyPermanentAction covers both in one clause where the printed card
			// needed to name them separately.
			CardFactory
				.Instant("Noxious Grasp", manaCost: 2)
				.WithAction(
					new DestroyPermanentAction(),
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
				.WithLifeGain(1)
				.Build(),
			// "You choose a nonland card from it" — the choice becomes "take their most expensive
			// non-land card". The gap between this and a random discard is the whole card: random
			// off a full hand is a coin flip, choosing is real disruption.
			CardFactory.Sorcery("Distress", manaCost: 2).WithChosenDiscard().Build(),
			// Targets a player, so it can be pointed at yourself — the printed card can too, and
			// doing so is occasionally right when the two life is cheaper than the alternative.
			CardFactory
				.Sorcery("Sign in Blood", manaCost: 2)
				.WithDraw(2)
				.WithTarget(Single().Players())
				.WithLoseLife(2)
				.WithTarget(Single().Players())
				.Build(),
			// Land sacrifice becomes -1 MaxMana: lands are consumed into MaxMana rather than
			// existing as permanents, so that IS the land. Every clause is symmetric as printed,
			// which is what makes this a card you build around rather than just play.
			CardFactory
				.Sorcery("Smallpox", manaCost: 2)
				.WithAction(new LoseLifeAction { Amount = 1 }, AllValid().Players())
				.WithOpponentDiscard(1)
				.WithDiscard(1)
				.WithSymmetricEdict()
				.WithAction(new GainPermanentManaAction { Amount = -1 }, AllValid().Players())
				.Build(),
			// ===== THREE MANA =====

			CardFactory
				.Instant("Cower in Fear", manaCost: 3)
				.WithWeaken(1, 1)
				.WithTarget(AllValid().OpponentCreatures())
				.Build(),
			CardFactory
				.Instant("Murder", manaCost: 3)
				.WithDestroy()
				.WithTarget(Single().OpponentCreatures())
				.Build(),
			// The unrestricted search is why SelectCardFromLibraryAction gained
			// SelectBestByManaCost. Library order is random, so a first-match search with no
			// subtype filter is just "draw the top card" — this would have been a strictly worse
			// Sign in Blood, and nothing about it would have looked wrong.
			// The one search in the set where the PLAYER picks. Three mana and three life buy "any
			// card in your deck", so resolving it with the auto-picker's best-by-mana-cost made it
			// a worse Sign in Blood while the rules text still read "search your library for a
			// card". Every other search in the cube keeps the auto-picker — see WithSearchLibrary.
			CardFactory
				.Sorcery("Grim Tutor", manaCost: 1)
				.WithSearchLibrary()
				.WithLoseLife(3)
				.Build(),
			CardFactory
				.Sorcery("Read the Bones", manaCost: 3)
				.WithScry(2)
				.WithDraw(2)
				.WithLoseLife(2)
				.Build(),
			// ===== FOUR MANA =====

			// The sacrifice is a real cast-time cost, so this needs a board before it needs a
			// graveyard — and now that a sacrificed creature actually announces its death, the
			// cost feeds every death payoff in the section on the way through.
			CardFactory
				.Sorcery("Blood for Bones", manaCost: 3)
				.WithSacrificeCost(TargetSpecification.CreatureControlledByYou())
				.WithReanimate()
				.WithReturnCreatureFromGraveyard()
				.Build(),
			// Symmetric as printed. With no blocking a board wipe is the only way to undo a
			// board, which makes this one of the most important cards in the section.
			CardFactory
				.Sorcery("Languish", manaCost: 4)
				.WithWeaken(4, 4)
				.WithTarget(AllValid().Creatures())
				.Build(),
			// ===== FIVE MANA AND UP =====

			// Spell mastery adds {B}{B}{B}, which is AddTemporaryManaAction — it raises
			// CurrentMana only, so the ritual evaporates at end of turn exactly as the printed
			// mana would.
			CardFactory
				.Sorcery("Dark Petition", manaCost: 5)
				.WithTutor()
				.WithConditionalAction(
					new SpellMasteryCondition(),
					new AddTemporaryManaAction
					{
						Amount = 3,
						TargetContextKey = ContextKeys.CastingPlayerId,
					}
				)
				.Build(),
			// Spell mastery's "two additional +1/+1 counters" must land on the SAME creature the
			// reanimation chose. Both effects therefore carry the identical targeting strategy:
			// MtgActionGenerator fills one chosen target into every user-select effect on a
			// spell, which is exactly the pairing this needs. It is also why ConditionalAction
			// had to become an ITargetedAction — before that a conditional half could only ever
			// be NoTarget, and would have buffed nothing.
			CardFactory
				.Sorcery("Necromantic Summons", manaCost: 5)
				.WithReanimate(fromAnyGraveyard: true)
				.WithConditionalAction(
					new SpellMasteryCondition(),
					new AddModifierAction
					{
						PowerBonus = 2,
						ToughnessBonus = 2,
						Duration = ModifierDuration.Permanent,
					}
				)
				.WithTarget(
					TargetingStrategy.SingleTarget(new IsCreatureInAnyGraveyardSpecification())
				)
				.Build(),
			// Reanimates from EITHER graveyard, as printed — raiding the opponent's is what makes
			// this an answer to a board you are losing to rather than only a rebuy of your own.
			// Convoke means a wide board casts it several turns early.
			CardFactory
				.Sorcery("Endless Obedience", manaCost: 6)
				.WithConvoke()
				.WithReanimate(fromAnyGraveyard: true)
				.Build(),
			// Printed as -2/-0 on the victim's other creatures. With no blocking, -X/-0 only
			// reduces what they can attack for next turn, which is a real but modest tempo swing
			// — kept as printed rather than upgraded to a shrink that would kill.
			CardFactory
				.Instant("Public Execution", manaCost: 6)
				.WithDestroy()
				.WithTarget(Single().OpponentCreatures())
				.WithWeaken(2, 0)
				.WithTarget(AllValid().OpponentCreatures())
				.Build(),
		];
}
