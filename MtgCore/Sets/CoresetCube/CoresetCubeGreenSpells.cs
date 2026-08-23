using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore.Cards.Builders;
using static MtgCore.Cards.Builders.TargetBuilder;

namespace MtgCore;

/// <summary>
/// Green instants and sorceries from the Core Set Cube — 10 instants + 10 sorceries.
///
/// FIGHT IS THE COLOUR'S REMOVAL, and it only started working properly this section. Four cards
/// here say "target creature YOU CONTROL … target creature you DON'T control", which is two
/// different targets — but ResolveEffectAction broadcasts the single user-chosen target to every
/// effect that requires selection, and FightAction reads its source from ContextKeys.SourceCardId,
/// which on a spell is the SPELL. Both halves were broken:
///   - TargetSelectionMode.Best lets the engine pick your side (your strongest creature) while the
///     opponent's side stays the player's choice, which is the half the card is actually about.
///   - FightAction gained a source fallback for when SourceCardId is not a creature. That also
///     repairs Hollowmere's Set Upon the Pack, a shipped spell that was a silent no-op.
/// FightAction.OneSided covers Rabid Bite and Hunter's Edge, where no damage comes back.
///
/// RAMP IS MANA, NOT LANDS. "Search your library for a basic land card and put it onto the
/// battlefield" is GainPermanentManaAction: lands are consumed into MaxMana and exiled by
/// PlayLandAction, so there is no land permanent to fetch and a land's whole game effect is +1
/// mana. Fetching a land to HAND is unaffected — a land card in the library is a real card.
///
/// DIVERGENCES FROM PRINTED CARDS:
///   - NO COLOURS, which guts Veil of Summer entirely — see its comment, it is the heaviest
///     reskin in the section.
///   - NO DELAYED TRIGGERS. Hunter's Insight is "whenever that creature deals combat damage this
///     turn, draw that many cards"; nothing here can register an effect to fire later in the turn.
///     Rebuilt as an immediate draw off the creature's power, which is the same card most of the
///     time and a worse one when the creature is removed in response — except that nothing can be
///     done in response either.
///   - NO SHUFFLING SEMANTICS worth modelling: "reveal, then shuffle" is flavour on a library
///     that is already random, so every tutor here silently drops it.
///   - Overwhelming Stampede keeps its printed "greatest power" wording rather than being
///     reskinned, because Overrun is ALREADY in this section and the two would otherwise be the
///     same card. See CountGreatestPowerAction for why that needed a query rather than a live
///     modifier (a live one recurses through GetEffectivePower).
/// </summary>
public static class CoresetCubeGreenSpells
{
	public static IReadOnlyList<Card> Cards { get; } =
		[
			// ===== INSTANTS =====

			// Prevention applies to COMBAT as well as effects — AttackAction routes through
			// ReplacementEngine like DealDamageAction does. Before that was wired up Fog could not
			// do the only thing it exists to do. UntilYourNextTurn is the default duration for
			// prevention, which matters here: with no priority window you cast this on your own
			// turn and the attack you are stopping arrives on theirs.
			CardFactory
				.Instant("Fog", manaCost: 1)
				.WithDamagePrevention(preventAll: true)
				.Build(),
			CardFactory
				.Instant("Giant Growth", manaCost: 1)
				.WithBoost(3, 3)
				.WithTarget(Single().Creatures())
				.Build(),
			CardFactory
				.Instant("Ranger's Guile", manaCost: 1)
				.WithBoost(1, 1)
				.WithTarget(Single().YourCreatures())
				.WithGrantKeyword(hexproof: true)
				.WithTarget(Single().YourCreatures())
				.Build(),
			// THE HEAVIEST RESKIN IN THE SECTION. Three of the printed card's four clauses are
			// colour-based and there are no colours: the conditional draw ("if an opponent has
			// cast a blue or black spell"), and both halves of "hexproof from blue and from
			// black". What survives is "spells you control can't be countered this turn", and
			// even that has to narrow to this spell alone, because CannotBeCounteredComponent is
			// per-card and there is no turn-long shield.
			//
			// Kept rather than cut because blue's counterspells genuinely fire from hand in this
			// engine, so an uncounterable cantrip is a real card rather than a blank one — and
			// it is priced as the cantrip it now is.
			CardFactory
				.Instant("Veil of Summer", manaCost: 1)
				.WithCannotBeCountered()
				.WithDraw(1)
				.Build(),
			CardFactory
				.Instant("Return to Nature", manaCost: 2)
				.WithModes(
					onceEach: false,
					modeTargeting:
					[
						TargetingStrategy.SingleTarget(
							new IsCardTypeSpecification { Types = CardType.Artifact }.And(
								new IsOnBattlefieldSpecification()
							)
						),
						TargetingStrategy.SingleTarget(
							new IsCardTypeSpecification { Types = CardType.Enchantment }.And(
								new IsOnBattlefieldSpecification()
							)
						),
						null,
					],
					("Destroy target artifact", new DestroyPermanentAction()),
					("Destroy target enchantment", new DestroyPermanentAction()),
					(
						"Exile target card from a graveyard",
						new PipelineAction
						{
							Steps = ImmutableList.Create<GameAction>(
								new SelectCardFromZoneAction
								{
									Zone = ZoneType.Graveyard,
									TargetOpponent = true,
									PlayerIdContextKey = ContextKeys.CastingPlayerId,
									OutputKey = "rtn_exile",
								},
								new MoveCardToExileAction { CardIdContextKey = "rtn_exile" }
							),
						}
					)
				)
				.Build(),
			// The Saproling half is the reason CreatureDiedThisTurnCondition and
			// MtgGame.CreaturesDiedThisTurn exist. That counter is incremented in
			// CheckStateBasedEffectsAction from the pending events rather than at the five actions
			// that stage a death, because five sites is five chances to miss one.
			CardFactory
				.Instant("Fungal Rebirth", manaCost: 3)
				.WithReturnCreatureFromGraveyard()
				.WithAction(
					new ConditionalAction
					{
						Condition = new CreatureDiedThisTurnCondition(),
						Action = new CreateCardAction
						{
							CardTemplate = CoresetCubeGreenTokens.Saproling(),
							Count = 2,
						},
					},
					TargetingStrategy.NoTarget()
				)
				.Build(),
			// "Target creature OR LAND card from a graveyard" — a land only reaches a graveyard
			// here by being milled or discarded, since a played land is exiled. So the land half
			// is rare rather than absent, and needs no divergence.
			CardFactory
				.Instant("Pulse of Murasa", manaCost: 3)
				.WithReturnCreatureFromGraveyard()
				.WithLifeGain(6)
				.Build(),
			// DELAYED TRIGGER CUT. Printed as "whenever that creature deals combat damage to a
			// player this turn, draw that many cards" — nothing here can register an effect to
			// fire later in the turn. Rebuilt as an immediate draw equal to the creature's power,
			// which is the same card whenever the attack was going to connect. Costs one more
			// than printed because it no longer requires the attack to happen at all.
			CardFactory
				.Instant("Hunter's Insight", manaCost: 4)
				.WithAction(
					new PipelineAction
					{
						Steps = ImmutableList.Create<GameAction>(
							new CountGreatestPowerAction
							{
								PlayerIdContextKey = ContextKeys.CastingPlayerId,
								OutputKey = "insight_power",
							},
							new DrawCardsAction
							{
								AmountContextKey = "insight_power",
								TargetContextKey = ContextKeys.CastingPlayerId,
							}
						),
					},
					TargetingStrategy.NoTarget()
				)
				.Build(),
			CardFactory
				.Instant("Might of Oaks", manaCost: 3)
				.WithBoost(7, 7)
				.WithTarget(Single().Creatures())
				.Build(),
			// "Two creature cards with different names" — the two searches run against updated
			// state, so the second cannot re-find the first. Distinct names are not enforced
			// beyond that, which only matters for a deck running duplicates.
			CardFactory
				.Instant("Shared Summons", manaCost: 4)
				.WithAction(
					new PipelineAction
					{
						Steps = ImmutableList.Create<GameAction>(
							new SelectCardFromZoneAction
							{
								Zone = ZoneType.Library,
								Filter = new IsCardTypeSpecification { Types = CardType.Creature },
								PlayerIdContextKey = ContextKeys.CastingPlayerId,
								OutputKey = "summons_1",
							},
							new MoveCardToHandAction
							{
								CardIdContextKey = "summons_1",
								PlayerIdContextKey = ContextKeys.CastingPlayerId,
							},
							new SelectCardFromZoneAction
							{
								Zone = ZoneType.Library,
								Filter = new IsCardTypeSpecification { Types = CardType.Creature },
								PlayerIdContextKey = ContextKeys.CastingPlayerId,
								OutputKey = "summons_2",
							},
							new MoveCardToHandAction
							{
								CardIdContextKey = "summons_2",
								PlayerIdContextKey = ContextKeys.CastingPlayerId,
							}
						),
					},
					TargetingStrategy.NoTarget()
				)
				.Build(),
			// ===== SORCERIES =====

			// The first card in the engine to use an X value as a P/T bonus, which is why
			// AddModifierAction gained PowerBonusContextKey — the bonus was a plain int before, so
			// this would have printed and resolved as "+0/+0". It is also the card that exposed
			// AddTargetedSpellAction never looping X: a TARGETED X spell could only ever be cast
			// for X = 0, so the whole cost was unreachable.
			//
			// The buff is applied by the engine to your strongest creature and the fight targets
			// theirs — see TargetSelectionMode.Best. Both halves land on the same creature because
			// buffing the strongest keeps it strongest.
			CardFactory
				.Sorcery("Primal Might", manaCost: 1)
				.WithXCost()
				.WithComponent(new RequiresCreatureRestriction())
				.WithAction(
					new AddModifierAction
					{
						PowerBonusContextKey = ContextKeys.XValue,
						ToughnessBonusContextKey = ContextKeys.XValue,
						Duration = ModifierDuration.UntilEndOfTurn,
					},
					Best().YourCreatures()
				)
				.WithFight()
				.WithTarget(Single().OpponentCreatures())
				.Build(),
			// One-sided: no damage comes back. The cast restriction is load-bearing — with an
			// empty board FightAction's source fallback finds nobody and the spell is a silent
			// no-op at full price, which the AI would happily pay.
			CardFactory
				.Sorcery("Rabid Bite", manaCost: 2)
				.WithComponent(new RequiresCreatureRestriction())
				.WithOneSidedFight()
				.WithTarget(Single().OpponentCreatures())
				.Build(),
			CardFactory
				.Sorcery("Rampant Growth", manaCost: 2)
				.WithAction(
					new GainPermanentManaAction
					{
						Amount = 1,
						TargetContextKey = ContextKeys.CastingPlayerId,
					},
					TargetingStrategy.NoTarget()
				)
				.Build(),
			// "If it's a creature or land card, draw a card" is the printed conditional. The scry
			// already decides what is on top, so the condition is one the player controls — which
			// is the whole point of the card and why it is not simply "scry 3, draw a card".
			CardFactory.Sorcery("Track Down", manaCost: 2).WithScry(3).WithDig(1).Build(),
			// One land to the battlefield (mana) and one to hand (a card), as printed — the two
			// halves are genuinely different resources here, which is what keeps this distinct
			// from Rampant Growth rather than being a more expensive copy.
			CardFactory
				.Sorcery("Cultivate", manaCost: 3)
				.WithAction(
					new GainPermanentManaAction
					{
						Amount = 1,
						TargetContextKey = ContextKeys.CastingPlayerId,
					},
					TargetingStrategy.NoTarget()
				)
				.WithTutor("Land")
				.Build(),
			// "Noncreature permanent" is IsNotCardTypeSpecification rather than a Not-wrapped
			// positive, because the negation must still require the candidate to BE a card — a
			// plain Not would match players too.
			CardFactory
				.Sorcery("Bramblecrush", manaCost: 4)
				.WithAction(
					new DestroyPermanentAction(),
					TargetingStrategy.SingleTarget(
						new IsNotCardTypeSpecification { Types = CardType.Creature }.And(
							new IsOnBattlefieldSpecification()
						)
					)
				)
				.Build(),
			// A real +1/+1 counter, not a permanent P/T modifier, because the counter is the
			// printed word and only PlusOneCounterComponent feeds Wildwood Scourge.
			CardFactory
				.Sorcery("Hunter's Edge", manaCost: 4)
				.WithComponent(new RequiresCreatureRestriction())
				.WithAction(new AddCountersAction { Amount = 1 }, Best().YourCreatures())
				.WithOneSidedFight()
				.WithTarget(Single().OpponentCreatures())
				.Build(),
			CardFactory
				.Sorcery("Wild Instincts", manaCost: 4)
				.WithComponent(new RequiresCreatureRestriction())
				.WithAction(
					new AddModifierAction
					{
						PowerBonus = 2,
						ToughnessBonus = 2,
						Duration = ModifierDuration.UntilEndOfTurn,
					},
					Best().YourCreatures()
				)
				.WithFight()
				.WithTarget(Single().OpponentCreatures())
				.Build(),
			// Both halves need an explicit AllValid target — WithBoost and WithGrantKeyword each
			// default to a SINGLE target, and a team pump that forgets to say so is a card that
			// buffs exactly one creature. Fortify shipped blank for precisely this.
			CardFactory
				.Sorcery("Overrun", manaCost: 4)
				.WithBoost(4, 4)
				.WithTarget(AllValid().AllYourCreatures())
				.WithGrantKeyword(trample: true)
				.WithTarget(AllValid().AllYourCreatures())
				.Build(),
			// Kept faithful rather than reskinned because Overrun is right above it — a reskin
			// would have shipped the same card twice and the draft model, which is keyed by card
			// name, could not have told them apart.
			//
			// One pipeline rather than two effects: the buff needs both a NUMBER and a target
			// LIST, and PipelineAction is not ITargetedAction so nothing can inject mass targets
			// into a pipeline step. CountGreatestPowerAction therefore emits both, from the one
			// scan it was already doing.
			CardFactory
				.Sorcery("Overwhelming Stampede", manaCost: 5)
				.WithAction(
					new PipelineAction
					{
						Steps = ImmutableList.Create<GameAction>(
							new CountGreatestPowerAction
							{
								PlayerIdContextKey = ContextKeys.CastingPlayerId,
								OutputKey = "stampede_power",
								CreatureIdsOutputKey = "stampede_targets",
							},
							new AddModifierAction
							{
								PowerBonusContextKey = "stampede_power",
								ToughnessBonusContextKey = "stampede_power",
								Duration = ModifierDuration.UntilEndOfTurn,
								TargetContextKey = "stampede_targets",
							},
							new GrantKeywordAction
							{
								GrantsTrample = true,
								Duration = ModifierDuration.UntilEndOfTurn,
								TargetContextKey = "stampede_targets",
							}
						),
					},
					TargetingStrategy.NoTarget()
				)
				.Build(),
		];
}
