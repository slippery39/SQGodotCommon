using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore.Cards.Builders;
using static MtgCore.Cards.Builders.TargetBuilder;

namespace MtgCore;

/// <summary>
/// **Package 3 — reanimator, as a three-card VALUE combo rather than a loop.** Cheat an eight-drop
/// into play on turn two off a one-mana spell.
///
/// **What is being tested is different from packages 1 and 2, and that is the point.** This is the
/// one archetype the builder already demonstrably finds: `MtgSimulator/CLAUDE.md` records a working
/// reanimator core (458 declarative creatures against 42 causal enablers) and the causal split was
/// built for exactly it. So this package is not asking "can the archetype be found" — it is asking
/// a sharper question: **given a payoff that is enormously better than every other reanimation
/// target, does the builder pick it, and does it play the ENABLERS that get it there?**
///
/// That distinction is the whole reanimator problem. A deck of Reanimate plus fat creatures with no
/// discard outlet and no self-mill assembles nothing, and it is what the mode built before causal
/// supply existed.
///
/// **DES supplies most of the package already, which is deliberate.** Faithless Looting is in HLM
/// and the graveyard theme there is deep in self-mill and recursion, so the enabler half is mostly
/// pre-existing and the run measures selection rather than availability. What DES does NOT have is
/// a tutor that puts a creature straight into the graveyard — **Entomb is Legacy-only, so it is
/// absent from HLM+CSC+CMB entirely** — and it has no reanimation spell cheap enough to be a turn-two
/// play. Those two, plus the payoff, are what this file adds.
///
/// ### Names
///
/// None of these reuse a printed name from HLM or CSC. Calling the tutor "Entomb" would have
/// collided with the Legacy pool, where `SetRegistry` resolves last-registered-wins — CMB registers
/// last, so it would have silently replaced Legacy's Entomb in the ALL union. `ComboProvingTests`
/// asserts the whole set is collision-free.
///
/// ### What should be discoverable, and the one thing to watch
///
/// The reanimation spell carries `IsCreatureInOwnGraveyardSpecification` on its targeting strategy,
/// which is graveyard-scoped and therefore already a demand — this is the exact case
/// `IsDeckScoped` was introduced for. The tutor should register as a CAUSAL supplier of that demand
/// via the movement rule, since resolving it puts a card into the graveyard.
///
/// **Watch the tutor specifically.** The causal-supply gate added this session requires a blind
/// mover to be more likely than not to move a matching card: `1 - (1 - density)^moved >= 0.5`. The
/// tutor moves exactly one card, so it is credited only while creatures are at least half the pool.
/// That is comfortable on DES and it is the pool-density-versus-deck-density ceiling already marked
/// `ponytail:` in `PoolFeatures` — a reanimator DECK is far more than half creatures, and the rule
/// cannot see the deck. If the tutor ever stops reading as an enabler, this is why.
/// </summary>
public static class ComboProvingReanimator
{
	public static IReadOnlyList<Card> Cards { get; } =
		[
			// Entomb. The enabler the format is missing, and the one that turns reanimator from a
			// deck that hopes to discard a fatty into one that finds it.
			//
			// **IsCardTypeSpecification, never IsCreatureSpecification.** The latter is
			// battlefield-only — it enforces shroud and hexproof, which it can only do against a
			// permanent — so as a library filter it matches NOTHING and the card silently searches
			// and finds nothing. CLAUDE.md records four shipped cards that were blank for exactly
			// this reason.
			CardFactory
				.Sorcery("Consign to Rot", manaCost: 1)
				.WithAction(
					new PipelineAction
					{
						Steps = ImmutableList.Create<GameAction>(
							new SelectCardFromZoneAction
							{
								Zone = ZoneType.Library,
								Filter = new IsCardTypeSpecification { Types = CardType.Creature },
								PlayerIdContextKey = ContextKeys.CastingPlayerId,
								OutputKey = "entomb_target",
							},
							new MoveCardToGraveyardAction { CardIdContextKey = "entomb_target" }
						),
					},
					TargetingStrategy.NoTarget()
				)
				.Build(),
			// Reanimate at one mana. The payoff below costs eight, so this is a seven-mana discount
			// on turn two — pushed far past cube rate, which is what the set is for.
			CardFactory
				.Sorcery("Raise the Sunken", manaCost: 1)
				.WithReanimate()
				.WithTarget(Single().CreatureInYourGraveyard())
				.Build(),
			// The payoff. Eight mana is unreachable by casting in any deck this format can build, so
			// the card exists only to be cheated in — which is what makes it a combo piece rather
			// than a big creature. Drawing seven is the "huge benefit" half: it does not win on the
			// spot, it converts one enabler into a whole new hand.
			//
			// Taunt matters more here than it looks in a blockerless game: it forces attacks into a
			// 7/7 lifelinker, so resolving it stabilises as well as refuels.
			CardFactory
				.Creature("Aurex, the Sevenfold", manaCost: 8, power: 7, toughness: 7)
				.WithSubtype("Horror")
				.WithFlying()
				.WithLifelink()
				.WithTrample()
				.WithTaunt()
				.WithEtbTrigger("Sevenfold", eb => eb.WithDraw(7))
				.Build(),
			// **A second, cheaper target, and it is a control rather than filler.** The user's
			// prediction was that a real reanimator deck plays backup targets; with only one
			// eight-drop in the set, a deck holding just it cannot be told apart from a deck that
			// found the archetype but drew badly. This gives the builder something to choose
			// BETWEEN, so "it played two fatties" becomes a readable result.
			CardFactory
				.Creature("Sunken Colossus", manaCost: 7, power: 6, toughness: 6)
				.WithSubtype("Horror")
				.WithTrample()
				.WithTaunt()
				.WithLifelink()
				.WithLifeGainBonus(10)
				.WithEtbTrigger("", eb => eb.WithDraw(2))
				.WithEtbTrigger("Dredge", eb => eb.WithMill(4).WithTarget(TargetingStrategy.Self()))
				.Build(),
		];
}
