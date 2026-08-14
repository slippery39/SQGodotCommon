using MtgCore.Cards.Builders;
using static MtgCore.Cards.Builders.TargetBuilder;

namespace MtgCore;

/// <summary>
/// Theme 6 — Go-Wide Spirits. Token flood plus pump.
///
/// Rates are deliberately below their paper equivalents. With no blockers every token
/// connects with the opponent's face, so a squad of 1/1 fliers is closer to a squad of
/// unblockable creatures — and an anthem multiplies that across the whole board. Spirits
/// overlap heavily into Spells (cast triggers make them) and Graveyard (flashback makes
/// them twice).
///
/// Batches 1 and 6: 25 cards.
/// </summary>
public static class HollowmereSpirits
{
	public static IReadOnlyList<Card> Cards { get; } =
		[
			// Four evasive bodies across two casts — the archetype's defining card.
			CardFactory
				.Spell("Lingering Souls", manaCost: 3)
				.WithCreateTokens(HollowmereTokens.Spirit(), count: 2)
				.WithFlashback(4)
				.Build(),
			// The lord. Hexproof protects the whole board from the format's removal, which
			// is why the body is a bare 2/2 for three.
			CardFactory
				.Creature("Drogskol Captain", manaCost: 3, power: 2, toughness: 2)
				.WithSubtype(Hollowmere.Spirit)
				.WithFlying()
				.WithComponent(
					new StaticPTBoostAbility
					{
						PowerBonus = 1,
						ToughnessBonus = 1,
						Filter = new IsSubtypeSpecification { Subtype = Hollowmere.Spirit }.And(
							TargetSpecification.OtherCreaturesYouControl()
						),
					}
				)
				.WithComponent(
					new StaticGrantKeywordAbility
					{
						GrantsHexproof = true,
						Filter = new IsSubtypeSpecification { Subtype = Hollowmere.Spirit }.And(
							TargetSpecification.OtherCreaturesYouControl()
						),
					}
				)
				.Build(),
			// A repeatable token engine that doubles as the deck's mana sink.
			CardFactory
				.Creature("Chapel Bellringer", manaCost: 4, power: 2, toughness: 4)
				.WithSubtype(Hollowmere.Spirit)
				.WithTaunt()
				.WithActivatedAbility(
					"Toll the Bell",
					manaCost: 2,
					effect: eb => eb.WithCreateTokens(HollowmereTokens.Spirit())
				)
				.Build(),
			// The payoff: a one-shot team pump that ends a stalled board. Priced at five
			// because go-wide plus a global pump is close to lethal on its own.
			CardFactory
				.Spell("Vigil of the Drowned", manaCost: 5)
				.WithAction(
					new AddModifierAction
					{
						PowerBonus = 2,
						ToughnessBonus = 1,
						Duration = ModifierDuration.UntilEndOfTurn,
					},
					AllValid().AllYourCreatures()
				)
				.WithCreateTokens(HollowmereTokens.Spirit(), count: 2)
				.Build(),
			// ===== BATCH 6 =====
			// The evasive one-drop the archetype opens on.
			CardFactory
				.Creature("Mausoleum Wanderer", manaCost: 1, power: 1, toughness: 1)
				.WithSubtype(Hollowmere.Spirit)
				.WithFlying()
				.Build(),
			// A token for one mana — raw width, which is what this deck converts into damage.
			CardFactory
				.Spell("Chapel Candle", manaCost: 1)
				.WithCreateTokens(HollowmereTokens.Spirit())
				.WithFlashback(3)
				.Build(),
			// A trick that doubles as evasion, and comes back.
			CardFactory
				.Spell("Silent Vigil", manaCost: 1)
				.WithBoost(1, 1)
				.WithTarget(Single().YourCreatures())
				.WithGrantKeyword(flying: true)
				.WithTarget(Single().YourCreatures())
				.WithFlashback(3)
				.Build(),
			// The defensive flier — Taunt plus Flying blunts both attack angles.
			CardFactory
				.Creature("Drogskol Shieldmate", manaCost: 2, power: 1, toughness: 3)
				.WithSubtype(Hollowmere.Spirit)
				.WithFlying()
				.WithTaunt()
				.Build(),
			// Two evasive bodies for two mana — the archetype's density spell.
			CardFactory
				.Spell("Spectral Reserves", manaCost: 2)
				.WithCreateTokens(HollowmereTokens.Spirit(), count: 2)
				.Build(),
			// A clean two-drop flier.
			CardFactory
				.Creature("Wisp-Lit Sentry", manaCost: 2, power: 2, toughness: 2)
				.WithSubtype(Hollowmere.Spirit)
				.WithFlying()
				.Build(),
			// Haste on the tribe turns a token flood into immediate damage.
			CardFactory
				.Creature("Rattlechains", manaCost: 2, power: 2, toughness: 1)
				.WithSubtype(Hollowmere.Spirit)
				.WithFlying()
				.WithComponent(
					new StaticGrantKeywordAbility
					{
						GrantsHaste = true,
						Filter = new IsSubtypeSpecification { Subtype = Hollowmere.Spirit }.And(
							TargetSpecification.OtherCreaturesYouControl()
						),
					}
				)
				.Build(),
			// Three bodies on one card. The marquee go-wide spell.
			CardFactory
				.Spell("Spectral Procession", manaCost: 3)
				.WithCreateTokens(HollowmereTokens.Spirit(), count: 3)
				.Build(),
			// Grows with the flood rather than pumping it — harder to answer than an anthem.
			CardFactory
				.Creature("Hollowmere Choirmaster", manaCost: 3, power: 2, toughness: 2)
				.WithSubtype(Hollowmere.Spirit)
				.WithFlying()
				.WithTriggeredAbility(
					"Swell the Choir",
					new EventTriggerCondition
					{
						EventTypeName = EventTypeNames.CreatureEnteredBattlefield,
						Filter = new IsSubtypeSpecification { Subtype = Hollowmere.Spirit }
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
			// An efficient evasive clock.
			CardFactory
				.Creature("Niblis of the Mere", manaCost: 3, power: 3, toughness: 3)
				.WithSubtype(Hollowmere.Spirit)
				.WithFlying()
				.Build(),
			// A body plus a body, on the ground so it holds a Taunt slot.
			CardFactory
				.Creature("Spirit Shepherd", manaCost: 3, power: 2, toughness: 3)
				.WithSubtype(Hollowmere.Spirit)
				.WithEtbTrigger(
					"Guide the Lost",
					eb => eb.WithCreateTokens(HollowmereTokens.Spirit())
				)
				.Build(),
			// Mass evasion — turns any stalled board into lethal, in any deck.
			CardFactory
				.Spell("Ghostly Chorus", manaCost: 3)
				.WithGrantKeyword(flying: true)
				.WithTarget(AllValid().AllYourCreatures())
				.WithFlashback(5)
				.Build(),
			// Prowess on an evasive body, bridging Spirits into the Spells theme.
			CardFactory
				.Creature("Niblis of Frost", manaCost: 4, power: 3, toughness: 3)
				.WithSubtype(Hollowmere.Spirit)
				.WithFlying()
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
			// A permanent anthem on a spell, so removal cannot undo it.
			CardFactory
				.Spell("Drogskol Anthem", manaCost: 4)
				.WithAction(
					new AddModifierAction
					{
						PowerBonus = 1,
						ToughnessBonus = 1,
						Duration = ModifierDuration.Permanent,
					},
					AllValid().AllYourCreatures()
				)
				.Build(),
			// Three bodies twice. Deliberately a bigger burst than Lingering Souls rather than
			// a costlier one — at 2 tokens it was strictly worse than Souls in the same set,
			// which is the exact failure HollowmereRateTests now guards against.
			CardFactory
				.Spell("Windswept Chorus", manaCost: 4)
				.WithCreateTokens(HollowmereTokens.Spirit(), count: 3)
				.WithFlashback(6)
				.Build(),
			// Four power of evasion across two bodies.
			CardFactory
				.Creature("Choir-Honored Abbess", manaCost: 4, power: 2, toughness: 2)
				.WithSubtype(Hollowmere.Spirit)
				.WithFlying()
				.WithEtbTrigger(
					"Consecrate the Choir",
					eb => eb.WithCreateTokens(HollowmereTokens.Spirit(), count: 2)
				)
				.Build(),
			// Rebuilds through a sweeper, which is what this archetype fears most.
			CardFactory
				.Creature("Chorus of the Drowned Bell", manaCost: 5, power: 3, toughness: 4)
				.WithSubtype(Hollowmere.Spirit)
				.WithFlying()
				.WithTriggeredAbility(
					"Answer the Bell",
					TriggerConditions.OnAnyCreatureDies(),
					eb => eb.WithCreateTokens(HollowmereTokens.Spirit())
				)
				.Build(),
			// Scales off a graveyard full of dead Spirits — the theme's graveyard crossover.
			CardFactory
				.Spell("Spectral Tide", manaCost: 5)
				.WithCreateTokensPerCard(HollowmereTokens.Spirit(), Hollowmere.Spirit)
				.Build(),
			// The finisher: width, evasion and a pump in one card.
			CardFactory
				.Creature("Geist of the Deep Mere", manaCost: 6, power: 5, toughness: 5)
				.WithSubtype(Hollowmere.Spirit)
				.WithFlying()
				.WithEtbTrigger(
					"Summon the Drowned Choir",
					eb =>
						eb.WithCreateTokens(HollowmereTokens.Spirit(), count: 3)
							.WithGrantKeyword(haste: true)
							.WithTarget(AllValid().AllYourCreatures())
				)
				.Build(),
			// A cheap pump that rewards having gone wide.
			CardFactory
				.Spell("Mere-Chapel Bells", manaCost: 2)
				.WithBoost(2, 0)
				.WithTarget(AllValid().AllYourCreatures())
				.WithFlashback(4)
				.Build(),
			// Recursion for the archetype's best creature, on a Spirit body.
			CardFactory
				.Creature("Keeper of the Silent Choir", manaCost: 4, power: 2, toughness: 4)
				.WithSubtype(Hollowmere.Spirit)
				.WithFlying()
				.WithEtbTrigger("Recall the Choir", eb => eb.WithReturnCreatureFromGraveyard())
				.Build(),
		];
}
