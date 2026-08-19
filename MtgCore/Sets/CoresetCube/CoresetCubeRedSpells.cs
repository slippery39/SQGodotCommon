using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore.Cards.Builders;
using static MtgCore.Cards.Builders.TargetBuilder;

namespace MtgCore;

/// <summary>
/// Red instants and sorceries from the Core Set Cube — 12 instants + 8 sorceries.
///
/// BURN IS THE COLOUR, and it only started working this section: no spell in the engine could
/// target a planeswalker, and one handed damage anyway took zero. Both fixed alongside the red
/// creatures — see "Planeswalkers" in MtgCore/CLAUDE.md. Every "any target" burn spell here now
/// genuinely reads as any target.
///
/// DIVIDED DAMAGE IS SPRAYED. Cone of Flame and Flames of the Firebrand fire N independent
/// 1-damage effects at random opposing targets rather than apportioning a total, and cost one less
/// than printed to pay for the loss of aim. See DesignNotes.md.
///
/// DIVERGENCES FROM PRINTED CARDS:
///   - NO REGENERATION, so "it can't be regenerated" (Incinerate, Soul Sear) is inert reminder
///     text and is dropped silently — there is nothing to prevent.
///   - NO COLOURS, so Fry's "white or blue" restriction is unexpressible. It becomes unrestricted
///     removal at the same rate, which is a real upgrade, so it costs one more.
///   - "If it would die, exile it instead" (Scorching Dragonfire, Soul Sear) is a STRUCTURAL
///     replacement effect, which the engine deliberately does not have. See DesignNotes.md.
///   - NO BLOCKING, so "creatures can't block this turn" (Tectonic Rift) is inert, and lands are
///     consumed into MaxMana rather than existing as permanents, so "destroy target land" has no
///     object to destroy. Tectonic Rift loses both halves and is rebuilt — see its comment.
///   - "CAN'T BE COUNTERED" is kept rather than cut, unusually: blue's counterspell traps really
///     do fire from hand in this engine, so the clause protects against something real.
///     Banefire's is unconditional — see its comment.
/// </summary>
public static class CoresetCubeRedSpells
{
	public static IReadOnlyList<Card> Cards { get; } =
		[
			// ===== INSTANTS =====

			CardFactory
				.Instant("Lightning Bolt", manaCost: 1)
				.WithDamage(3)
				.WithTarget(Single().PlayersOrCreatures())
				.Build(),
			// Scry after the buff, so a combat trick also smooths the next draw. The scry is a real
			// choice (MinChoices 0) — bottoming unconditionally is strictly worse than declining.
			CardFactory
				.Instant("Titan's Strength", manaCost: 1)
				.WithBoost(3, 1)
				.WithTarget(Single().YourCreatures())
				.WithScry(1)
				.Build(),
			// Printed as "destroy target white or blue creature". There are no colours, so the
			// restriction cannot be expressed and this would otherwise be unconditional removal at
			// two mana — well under rate. Costs one more instead of pretending to be narrow.
			CardFactory
				.Instant("Fry", manaCost: 3)
				.WithDestroy()
				.WithTarget(Single().OpponentCreatures())
				.Build(),
			// "It can't be regenerated" is inert — no regeneration exists.
			CardFactory
				.Instant("Incinerate", manaCost: 2)
				.WithDamage(3)
				.WithTarget(Single().PlayersOrCreatures())
				.Build(),
			// The X damage is the whole card; the "if you control a creature with power 4+" rider
			// would need a board condition on a targeted effect, which no card shape here supports.
			// Dropped in favour of the X being clean, which is what makes it scale into the late
			// game the way the printed card does.
			CardFactory
				.Instant("Ravaging Blaze", manaCost: 2)
				.WithXCost()
				.WithAction(
					new DealDamageAction { AmountContextKey = ContextKeys.XValue },
					TargetingStrategy.SingleTarget(TargetSpecification.PlayersOrCreatures())
				)
				.Build(),
			// The exile-instead clause is a structural replacement the engine does not have.
			CardFactory
				.Instant("Scorching Dragonfire", manaCost: 2)
				.WithDamage(3)
				.WithTarget(Single().OpponentCreatures())
				.Build(),
			// Discard first, then draw two — a real rummage. WithDiscard chooses at RESOLUTION,
			// which matters: a cast-time selection would pick out of the pre-draw hand.
			CardFactory
				.Instant("Thrill of Possibility", manaCost: 2)
				.WithDiscard(1)
				.WithDraw(2)
				.WithTarget(TargetingStrategy.Self())
				.Build(),
			CardFactory
				.Instant("Soul Sear", manaCost: 3)
				.WithDamage(5)
				.WithTarget(Single().OpponentCreatures())
				.Build(),
			// "Attacking creatures get +2/+0", as a pump on your whole board.
			//
			// It was written as CreatureControlledByYou AND HasAttackedThisTurn, which reads
			// faithfully and is COMPLETELY BLANK: attacks resolve damage immediately here, so
			// before combat the spell has no legal target at all, and after combat the buff lands
			// on a creature that has already dealt its damage and cannot attack again. No blocking
			// means it cannot matter defensively either. The trained model measured it at -9.3pp,
			// the worst red card in the set, which is exactly right for a card that does nothing.
			//
			// That specification is correct for REMOVAL (Royal Assassin kills what attacked you,
			// on your turn) and wrong for a PUMP, because a pump always arrives after damage.
			CardFactory
				.Instant("Trumpet Blast", manaCost: 3)
				.WithBoost(2, 0)
				.WithTarget(AllValid().AllYourCreatures())
				.Build(),
			// Double strike implies first strike — ask CreatureStats.StrikesFirst, never
			// HasFirstStrike alone. In no-blocker combat this is close to "kill their creature and
			// take nothing back".
			CardFactory
				.Instant("Uncaged Fury", manaCost: 3)
				.WithBoost(1, 1)
				.WithTarget(Single().YourCreatures())
				.WithGrantKeyword(doubleStrike: true)
				.WithTarget(Single().YourCreatures())
				.Build(),
			// A fixed split, not a divided one: 4 to the creature and 2 to its controller are two
			// separate printed numbers, so this needs no spray and keeps its printed cost.
			CardFactory
				.Instant("Chandra's Outrage", manaCost: 4)
				.WithDamage(4)
				.WithTarget(Single().OpponentCreatures())
				.WithDamage(2)
				.WithTarget(AllValid().Opponent())
				.Build(),
			// Convoke: {1} less per ready creature, exhausting exactly that many. With a wide
			// Goblin board this is routinely free, which is the card.
			CardFactory
				.Instant("Stoke the Flames", manaCost: 4)
				.WithConvoke()
				.WithDamage(4)
				.WithTarget(Single().PlayersOrCreatures())
				.Build(),
			// ===== SORCERIES =====

			// Uncounterable UNCONDITIONALLY, not at X 5+ as printed. The chosen X lives on
			// CastSpellAction rather than on the card — two copies can be cast for different X —
			// so a component on the card cannot see it. Threading XValue into CounterTrapEngine
			// for one clause on one card is not worth it, and an X spell is usually cast big.
			CardFactory
				.Sorcery("Banefire", manaCost: 1)
				.WithXCost()
				.WithCannotBeCountered()
				.WithAction(
					new DealDamageAction { AmountContextKey = ContextKeys.XValue },
					TargetingStrategy.SingleTarget(TargetSpecification.PlayersOrCreatures())
				)
				.Build(),
			// "Each creature without flying and each player" — the flying exemption is the whole
			// card, and it is why HasFlyingSpecification exists. Without it this is a symmetric
			// sweeper with no way to build around it.
			CardFactory
				.Sorcery("Earthquake", manaCost: 1)
				.WithXCost()
				.WithAction(
					new DealDamageAction { AmountContextKey = ContextKeys.XValue },
					TargetingStrategy.AllValid(
						new IsCreatureSpecification().And(new HasFlyingSpecification().Not())
					)
				)
				.WithAction(
					new DealDamageAction { AmountContextKey = ContextKeys.XValue },
					TargetingStrategy.AllValid(new IsPlayerSpecification())
				)
				.Build(),
			// The land discard is a real cost, not a reskin: a land in hand is a genuine card here
			// and pitching one gives up a mana drop. It is why DiscardAdditionalCost gained Filter.
			CardFactory
				.Sorcery("Magmatic Insight", manaCost: 1)
				.WithDiscardCost(1, "Land")
				.WithDraw(2)
				.Build(),
			CardFactory
				.Sorcery("Krenko's Command", manaCost: 2)
				.WithCreateTokens(CoresetCubeRedTokens.Goblin(), 2)
				.Build(),
			// Spell mastery gates the uncounterable clause, exactly as printed — and unlike
			// Banefire it CAN be gated, because the condition reads the graveyard rather than a
			// value chosen at cast time.
			CardFactory
				.Sorcery("Exquisite Firecraft", manaCost: 3)
				.WithCannotBeCountered(new SpellMasteryCondition())
				.WithDamage(4)
				.WithTarget(Single().PlayersOrCreatures())
				.Build(),
			// Sprayed: 3 damage as three independent 1-damage hits, costing one less than printed.
			CardFactory
				.Sorcery("Flames of the Firebrand", manaCost: 2)
				.WithDamage(1)
				.WithTarget(Random().OpponentOrOpponentCreatures())
				.WithDamage(1)
				.WithTarget(Random().OpponentOrOpponentCreatures())
				.WithDamage(1)
				.WithTarget(Random().OpponentOrOpponentCreatures())
				.Build(),
			// Printed: "Destroy target land. Creatures can't block this turn." BOTH halves are
			// structurally absent — lands are consumed into MaxMana so there is no land permanent
			// to destroy, and there is no blocking to prevent. Rebuilt as the effect the card is
			// FOR: it is a finisher-enabler that clears the way, so it exhausts the opposing board
			// for a turn. Nothing of the printed card survives except its role.
			CardFactory
				.Sorcery("Tectonic Rift", manaCost: 4)
				.WithExhaust()
				.WithTarget(AllValid().OpponentCreatures())
				.Build(),
			// Printed: 1, 2 and 3 damage to three different targets. Sprayed as six independent
			// 1-damage hits, costing one less than printed.
			CardFactory
				.Sorcery("Cone of Flame", manaCost: 4)
				.WithDamage(1)
				.WithTarget(Random().OpponentOrOpponentCreatures())
				.WithDamage(1)
				.WithTarget(Random().OpponentOrOpponentCreatures())
				.WithDamage(1)
				.WithTarget(Random().OpponentOrOpponentCreatures())
				.WithDamage(1)
				.WithTarget(Random().OpponentOrOpponentCreatures())
				.WithDamage(1)
				.WithTarget(Random().OpponentOrOpponentCreatures())
				.WithDamage(1)
				.WithTarget(Random().OpponentOrOpponentCreatures())
				.Build(),
		];
}
