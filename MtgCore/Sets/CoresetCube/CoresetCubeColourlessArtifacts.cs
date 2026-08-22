using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore.Cards.Builders;
using static MtgCore.Cards.Builders.TargetBuilder;

namespace MtgCore;

/// <summary>
/// Non-equipment artifacts from the Core Set Cube, plus its one colourless planeswalker —
/// https://cubecobra.com/cube/list/magiccoreset20xx
///
/// All 18 (17 artifacts + Ugin), in the cube's own order (by mana value, then name).
///
/// MANA ROCKS PRODUCE ON YOUR UPKEEP, NOT VIA A TAP ABILITY, exactly as the green dorks do. The
/// green rationale transfers whole: an unactivated mana ability is INVISIBLE — the creature or
/// artifact just looks weak, and the AI has to re-derive the activation every turn on every
/// producer before it can cast anything. StartTurnAction refills CurrentMana and the trigger
/// resolves after that, so the mana is additive and evaporates at the next refill, which is correct
/// because it must vanish if the rock is destroyed. A rock played this turn produces nothing until
/// the next upkeep — the same delay summoning sickness imposes on a dork. Price them slightly above
/// their printed rate for the reliability.
///
/// {T} ABILITIES ARE MaxActivationsPerTurn = 1, NOT RequiresTap. RequiresTap is a silent no-op on a
/// non-creature — ActivatedAbilityAction gates it on `creature != null` — and the once-per-turn cap
/// produces the same behaviour, correctly including "no summoning sickness for artifacts". The one
/// place they diverge is a permanent with TWO {T} abilities, where real Magic locks the second;
/// Dragon's Hoard and Meteorite are printed that way and both lose their second tap ability to the
/// cuts below, so nothing here depends on it. See DesignNotes.md.
///
/// THE EMPIRES TRIO IS BUILT, NOT CUT. Crown, Scepter and Throne each do something larger when the
/// other two are beside them, which needed ControlsAllCardsNamedCondition — fifteen lines mirroring
/// ControlsSubtypeCondition. Name-matching was MISSING from the engine, not structurally absent the
/// way colour and blocking are, and the set's rule is to build the mechanic in that case. The three
/// will almost never assemble in a 45-card pool, and that is fine: the base halves are playable on
/// their own and the payoff is the reason to read the card.
///
/// DIVERGENCES beyond the colour and blocking cuts listed in CoresetCubeBlack:
///   - NO UNTAP EFFECT, and artifacts never exhaust in the first place, so Manifold Key's "untap
///     another target artifact" is doubly unreachable — nothing outside StartTurnAction clears
///     IsExhausted, and IsExhausted lives on CreatureComponent.
///   - NO EXILED-WITH-THIS TRACKING, so Bag of Holding cannot return what it exiled.
///   - CHARGE COUNTERS EXIST NOW, so Dragon's Hoard keeps its gold counters — see below for the
///     half it does lose.
///   - NO BRANCHING ON A REVEALED CARD'S TYPE, so Druidic Satchel becomes modal.
///   - NO "ATTACKING CREATURE" STATE — attacking is atomic here, resolved inside one AttackAction —
///     so War Horn cannot anthem only the attackers.
///   - LOYALTY COSTS ARE FIXED, so Ugin's "-X" becomes a fixed -3, as Chandra Nalaar's did.
/// </summary>
public static class CoresetCubeColourlessArtifacts
{
	public static IReadOnlyList<Card> Cards { get; } =
		[
			// ===== ONE =====

			// Printed: "Whenever you discard a card, exile that card from your graveyard. {2},{T}:
			// Draw a card, then discard a card. {4},{T}, Sacrifice: Return all cards exiled with
			// this artifact to their owner's hand."
			//
			// The exile-and-return engine needs per-card provenance tracking that does not exist,
			// and the first clause on its own is pure self-graveyard-hate in a cube with Threshold
			// and flashback — a drawback with no payoff. What survives is the looting half plus a
			// bounded recursion: sacrifice it to buy back a creature. That keeps the card's actual
			// shape (a slow value engine that eventually cashes out) without the provenance.
			CardFactory
				.Artifact("Bag of Holding", manaCost: 1)
				.WithActivatedAbility(
					"Rummage",
					manaCost: 2,
					effect: eb => eb.WithDraw(1).WithDiscard(1).WithTarget(TargetingStrategy.Self())
				)
				.WithActivatedAbility(
					"Empty the Bag",
					manaCost: 4,
					effect: eb => eb.WithAutoReturnCreature(),
					costs: c => c.SacrificeSelf()
				)
				.Build(),
			// Printed: "{4},{T}, Exile this artifact: Exile target creature."
			// The exile-itself cost becomes SacrificeSelf — the only difference is which zone the
			// Effigy lands in, and nothing in the cube cares about an artifact in the graveyard
			// except artifact-death payoffs, which is a small upgrade the {4} already pays for.
			// Unconditional exile removal at one mana is why the activation is this expensive.
			CardFactory
				.Artifact("Brittle Effigy", manaCost: 1)
				.WithActivatedAbility(
					"Seal Away",
					manaCost: 4,
					effect: eb => eb.WithExile().WithTarget(Single().OpponentCreatures()),
					costs: c => c.SacrificeSelf()
				)
				.Build(),
			// Printed: "{1},{T}: Untap another target artifact. {3},{T}: Target creature can't be
			// blocked this turn."
			//
			// BOTH halves are unreachable. Untapping needs an effect that clears IsExhausted — only
			// StartTurnAction does — and it would do nothing anyway, because artifacts never
			// exhaust. "Can't be blocked" needs blocking. What is left is a one-mana artifact, so
			// it becomes the cube's cheapest affinity enabler with a small relevant ability:
			// exhausting a blocker-substitute is the closest live thing to "can't be blocked",
			// since Taunt is what stands in for a blocker here.
			CardFactory
				.Artifact("Manifold Key", manaCost: 1)
				.WithActivatedAbility(
					"Unlock",
					manaCost: 3,
					effect: eb => eb.WithExhaust().WithTarget(Single().OpponentCreatures())
				)
				.Build(),
			// ===== TWO =====

			// Printed: "{3},{T}: Tap target creature. Gain control of that creature instead if you
			// control artifacts named Scepter of Empires and Throne of Empires."
			//
			// Faithful, via ConditionalAction on ControlsAllCardsNamedCondition. Both halves resolve
			// — the exhaust always, the steal only with the set — so assembling the trio upgrades a
			// tempo ability into a Control Magic.
			CardFactory
				.Artifact("Crown of Empires", manaCost: 2)
				.WithActivatedAbility(
					"Command",
					manaCost: 3,
					effect: eb =>
						eb.WithExhaust()
							.WithConditionalAction(
								new ControlsAllCardsNamedCondition
								{
									FirstName = "Scepter of Empires",
									SecondName = "Throne of Empires",
								},
								new GainControlAction()
							)
							.WithTarget(Single().OpponentCreatures())
				)
				.Build(),
			// Printed: "{2},{T}, Sacrifice this artifact: It deals 2 damage to target creature."
			// Faithful.
			CardFactory
				.Artifact("Vial of Dragonfire", manaCost: 2)
				.WithActivatedAbility(
					"Ignite",
					manaCost: 2,
					effect: eb => eb.WithDamage(2).WithTarget(Single().OpponentCreatures()),
					costs: c => c.SacrificeSelf()
				)
				.Build(),
			// ===== THREE =====

			// Printed: "{3},{T}: Draw a card." Faithful.
			CardFactory
				.Artifact("Arcane Encyclopedia", manaCost: 3)
				.WithActivatedAbility(
					"Consult",
					manaCost: 3,
					effect: eb => eb.WithDraw(1).WithTarget(TargetingStrategy.Self())
				)
				.Build(),
			// Printed: "{1},{T}: Scry 2." Faithful. WithScry is a real choice with MinChoices = 0 —
			// bottoming a card unconditionally is strictly worse than doing nothing half the time.
			CardFactory
				.Artifact("Crystal Ball", manaCost: 3)
				.WithActivatedAbility(
					"Gaze",
					manaCost: 1,
					effect: eb => eb.WithScry(2).WithTarget(TargetingStrategy.Self())
				)
				.Build(),
			// Printed: "Whenever a Dragon you control enters, put a gold counter on this artifact.
			// {T}, Remove a gold counter: Draw a card. {T}: Add one mana of any color."
			//
			// The gold counters are REAL — ChargeCounterComponent plus RemoveCounterAdditionalCost,
			// the removal cost green deferred until a second card wanted to spend counters. This is
			// that card, and it is the one place in the cube where a counter is a currency rather
			// than a stat.
			//
			// TWO CHANGES. The Dragon trigger becomes any creature costing 5 or more — the cube has
			// exactly two Dragons outside this card, so a literal Dragon trigger would be blank in
			// almost every deck that drafted it. And the mana half becomes an upkeep trigger like
			// every other producer here, which also sidesteps the two-{T}-abilities problem, since
			// RequiresTap cannot lock the second one on a non-creature.
			CardFactory
				.Artifact("Dragon's Hoard", manaCost: 3)
				.WithTriggeredAbility(
					"Hoard",
					// Built inline rather than composed from TriggerConditions, because
					// EventTriggerCondition.Filter is already a TargetSpecification run against the
					// event's subject — here the creature that entered — so the extra clause goes
					// in the filter rather than needing a second TriggerCondition.
					//
					// HasManaCostAtLeastSpecification rather than Not(AtMost 4), which is the same
					// arithmetic: the negation inverts to true for a non-card, and MtgCardMapper
					// cannot describe it, so the entire restriction vanished from the card face and
					// the Hoard read as triggering on every creature.
					new EventTriggerCondition
					{
						EventTypeName = EventTypeNames.CreatureEnteredBattlefield,
						Filter = new IsControlledByYouSpecification().And(
							new HasManaCostAtLeastSpecification { Minimum = 5 }
						),
					},
					eb =>
						eb.WithAction(
							new AddChargeCountersAction
							{
								Kind = "gold",
								Amount = 1,
								TargetContextKey = ContextKeys.SourceCardId,
							},
							TargetingStrategy.NoTarget()
						)
				)
				.WithTriggeredAbility(
					"Wealth",
					TriggerConditions.OnYourUpkeep(),
					eb => eb.WithAddMana(1).WithTarget(TargetingStrategy.Self())
				)
				.WithActivatedAbility(
					"Spend the Hoard",
					manaCost: 0,
					effect: eb => eb.WithDraw(1).WithTarget(TargetingStrategy.Self()),
					costs: c => c.RemoveCounter("gold"),
					maxPerTurn: 0
				)
				.Build(),
			// Printed: "{2},{T}: Reveal the top card of your library. If it's a creature card,
			// create a 1/1 Saproling. If it's a land, put it onto the battlefield. Otherwise you
			// gain 2 life."
			//
			// Branching on a revealed card's TYPE mid-pipeline has no shape here: ConditionalAction
			// takes an ActivationCondition, which sees a player and not a pipeline context key.
			// Modal instead — you pick the mode rather than the library picking for you. That is
			// strictly better than printed and costs nothing to build (SelectModeAction already
			// downgrades UserSelect targeting to Random, so a mode CAN target), so the activation
			// costs one more than printed.
			CardFactory
				.Artifact("Druidic Satchel", manaCost: 3)
				.WithActivatedAbility(
					"Forage",
					manaCost: 3,
					effect: eb =>
						eb.WithModes(
							(
								"Create a 1/1 Saproling",
								new CreateCardAction
								{
									CardTemplate = CoresetCubeColourlessTokens.Saproling(),
									Count = 1,
								}
							),
							(
								"Add a land to your mana",
								new GainPermanentManaAction
								{
									Amount = 1,
									Deferred = true,
									TargetContextKey = ContextKeys.CastingPlayerId,
								}
							),
							(
								"Gain 2 life",
								new GainLifeAction
								{
									Amount = 2,
									TargetContextKey = ContextKeys.CastingPlayerId,
								}
							)
						)
				)
				.Build(),
			// Printed: "{T}: This artifact deals 1 damage to target player or planeswalker. It deals
			// 3 damage instead if you control artifacts named Crown of Empires and Throne of
			// Empires."
			//
			// Faithful. The two damage halves are separate effects rather than a replaced amount:
			// 1 always, plus 2 more with the set, which totals the printed 3.
			CardFactory
				.Artifact("Scepter of Empires", manaCost: 3)
				.WithActivatedAbility(
					"Rule",
					manaCost: 0,
					effect: eb =>
						eb.WithDamage(1)
							.WithConditionalAction(
								new ControlsAllCardsNamedCondition
								{
									FirstName = "Crown of Empires",
									SecondName = "Throne of Empires",
								},
								// No TargetContextKey: ConditionalAction forwards its own
								// TargetIds to the inner action, and multi-effect targeting fills
								// the one chosen target into every user-select effect on the
								// ability — so both damage halves hit the same thing.
								new DealDamageAction { Amount = 2 }
							)
							.WithTarget(Single().Opponent())
				)
				.Build(),
			// Printed: "Attacking creatures you control get +1/+0."
			//
			// There is no "attacking" state to read — AttackAction declares the attack, resolves
			// exalted and applies all damage in one atomic action, and the only persistent flag is
			// HasAttacked, which is set AFTER damage and so is always too late. A plain anthem
			// instead, which is what the card does in practice on a board with no blockers, and it
			// costs one more because it now also helps on defence.
			CardFactory
				.Artifact("War Horn", manaCost: 4)
				.WithStaticBoost(1, 0, TargetSpecification.CreatureControlledByYou())
				.Build(),
			// ===== FOUR =====

			// Printed: "{5},{T}, Exile this artifact: Exile all nonland permanents."
			//
			// Faithful, and the first sweeper in the project that reaches past creatures — every
			// mass targeting helper was creature-shaped until TargetBuilder.NonlandPermanents().
			// It exiles YOUR side too, including itself, which is what makes a symmetric wrath fair.
			CardFactory
				.Artifact("Perilous Vault", manaCost: 4)
				.WithActivatedAbility(
					"Purge",
					manaCost: 5,
					effect: eb => eb.WithAction(new ExileAction(), AllValid().NonlandPermanents()),
					costs: c => c.SacrificeSelf()
				)
				.Build(),
			// Printed: "{1},{T}: Create a 1/1 white Soldier creature token. Create five of those
			// tokens instead if you control artifacts named Crown of Empires and Scepter of
			// Empires."
			//
			// Faithful, with the same additive treatment as Scepter: one Soldier always, four more
			// with the set, totalling the printed five.
			CardFactory
				.Artifact("Throne of Empires", manaCost: 4)
				.WithActivatedAbility(
					"Muster",
					manaCost: 1,
					effect: eb =>
						eb.WithCreateTokens(CoresetCubeTokens.Soldier())
							.WithConditionalAction(
								new ControlsAllCardsNamedCondition
								{
									FirstName = "Crown of Empires",
									SecondName = "Scepter of Empires",
								},
								new CreateCardAction
								{
									CardTemplate = CoresetCubeTokens.Soldier(),
									Count = 4,
								}
							)
				)
				.Build(),
			// ===== FIVE =====

			// Printed: "{T}: Add three mana of any one color."
			// Three mana on your upkeep, every upkeep, per the file header. Priced at its printed
			// five: reliable and unactivated is a real upgrade over a tap ability, but it also
			// produces nothing on the turn it lands, which is the offsetting cost.
			CardFactory
				.Artifact("Gilded Lotus", manaCost: 5)
				.WithTriggeredAbility(
					"Bloom",
					TriggerConditions.OnYourUpkeep(),
					eb => eb.WithAddMana(3).WithTarget(TargetingStrategy.Self())
				)
				.Build(),
			// Printed: "When this artifact enters, it deals 2 damage to any target. {T}: Add one
			// mana of any color." Faithful, with the mana half on the upkeep.
			CardFactory
				.Artifact("Meteorite", manaCost: 5)
				.WithEtbTrigger(
					"Impact",
					eb => eb.WithDamage(2).WithTarget(Random().OpponentOrOpponentCreatures())
				)
				.WithTriggeredAbility(
					"Ore",
					TriggerConditions.OnYourUpkeep(),
					eb => eb.WithAddMana(1).WithTarget(TargetingStrategy.Self())
				)
				.Build(),
			// ===== SIX =====

			// Printed: "At the beginning of your upkeep, draw a card. {T}: This artifact deals 1
			// damage to any target." Faithful — two abilities, one triggered and one activated, so
			// the two-{T} problem does not arise.
			CardFactory
				.Artifact("Staff of Nin", manaCost: 6)
				.WithTriggeredAbility(
					"Foresight",
					TriggerConditions.OnYourUpkeep(),
					eb => eb.WithDraw(1).WithTarget(TargetingStrategy.Self())
				)
				.WithActivatedAbility(
					"Needle",
					manaCost: 0,
					effect: eb =>
						eb.WithDamage(1).WithTarget(Single().OpponentOrOpponentCreatures())
				)
				.Build(),
			// ===== SEVEN =====

			// Printed: "Legendary. Creatures you control have flying, first strike, vigilance,
			// trample, haste, and protection from black and from red."
			//
			// Four of the six keywords survive. Vigilance is unimplemented; protection from a COLOUR
			// is unreachable, since cards have no colour at all here (protection from a creature
			// TYPE is what exists). Still a seven-mana board-wide flying-and-haste anthem, which on
			// a no-blocker board is close to lethal on the turn it lands — the printed cost stands.
			CardFactory
				.Artifact("Akroma's Memorial", manaCost: 7)
				.WithStaticGrantKeyword(
					TargetSpecification.CreatureControlledByYou(),
					flying: true,
					firstStrike: true,
					trample: true,
					haste: true
				)
				.Build(),
			// ===== PLANESWALKER =====

			// Printed: "+2: Ugin deals 3 damage to any target. -X: Exile each permanent with mana
			// value X or less that's one or more colors. -10: You gain 7 life, draw seven cards,
			// then put up to seven permanent cards from your hand onto the battlefield."
			//
			// The +2 is faithful. The -X becomes a fixed -3, as Chandra Nalaar's did: loyalty costs
			// are fixed here, and the X is not knowable at build time. Its "that's one or more
			// colors" clause was Ugin's own asymmetry — it spared his colourless artifacts — and
			// with no colours there is nothing to spare, so the sweep is symmetric and hits your
			// side too. That is a real nerf to the card and the reason it is worth its eight mana
			// rather than being an auto-win.
			//
			// The -10 loses "put seven permanents from your hand onto the battlefield" — nothing
			// puts a permanent from hand into play, and building it for one ultimate on one card is
			// not worth it. Gain 7 and draw 7 is still a game-ending ultimate at ten loyalty.
			CardFactory
				.Planeswalker("Ugin, the Spirit Dragon", manaCost: 8)
				.WithLoyalty(7)
				.WithLoyaltyAbility(
					"+2: Ugin deals 3 damage to any target",
					2,
					eb => eb.WithDamage(3).WithTarget(Single().OpponentOrOpponentCreatures())
				)
				.WithLoyaltyAbility(
					"-3: Exile each nonland permanent with mana value 3 or less",
					-3,
					eb =>
						eb.WithAction(
							new ExileAction(),
							AllValid()
								.WithSpec(
									new IsCardTypeSpecification
									{
										Types = CardType.AnyPermanent & ~CardType.Land,
									}
										.And(new IsOnBattlefieldSpecification())
										.And(new HasManaCostAtMostSpecification { Maximum = 3 })
								)
						)
				)
				.WithLoyaltyAbility(
					"-10: You gain 7 life and draw seven cards",
					-10,
					eb => eb.WithLifeGain(7).WithDraw(7).WithTarget(TargetingStrategy.Self())
				)
				.Build(),
		];
}
