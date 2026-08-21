using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore.Cards.Builders;
using static MtgCore.Cards.Builders.TargetBuilder;

namespace MtgCore;

/// <summary>
/// Green non-creature permanents from the Core Set Cube — 4 enchantments (three of them Auras),
/// 1 enchantment engine, 1 artifact Equipment, 3 planeswalkers.
///
/// LANDS ARE WHERE THIS SECTION DIVERGES MOST. Six green cards treat lands as battlefield
/// permanents and this engine deliberately does not have them — PlayLandAction consumes a land
/// into MaxMana and exiles it, so there is nothing to enchant, untap or animate. Three of those
/// six are here, and all three are reskinned onto mana rather than cut, following Knight of the
/// White Orchid and Simic Growth Chamber:
///   - Gift of Paradise stops being an Aura and becomes an enchantment that gains life and adds
///     a permanent mana, which is exactly what enchanting a land did.
///   - Garruk Wildspeaker's "+1: Untap two target lands" becomes "+1: Add 2 mana this turn" —
///     untapping a land IS a burst of mana here.
///   - Nissa, Worldwaker animates lands in all three abilities, so she is rebuilt around the
///     4/4 trample Elemental the animation produces, plus the mana the lands represent.
///
/// DIVERGENCES FROM PRINTED CARDS:
///   - NO FLASH (no priority window), so Feral Invocation loses it and costs one less.
///   - NO OPENING-HAND PERMANENTS. Leyline of Vitality's "if this is in your opening hand you may
///     begin the game with it on the battlefield" has no mulligan or pre-game step to hook into.
///     Dropped; the rest of the card is a real enchantment and it is costed as one.
///   - NO END-STEP TRIGGERS and no "activated abilities can't be activated", so Arachnus Web keeps
///     only "enchanted creature can't attack". Losing its own self-destruct clause makes it
///     strictly better than printed, so it costs one more.
///   - NO ABILITY COPYING or ability granting beyond keywords, and NO "exile at end of turn".
///   - VIGILANCE is unimplemented engine-wide, so Vivien Reid's emblem grants the two keywords
///     that exist.
///   - "CAN'T BE BLOCKED BY MORE THAN ONE CREATURE" (Wolfrider's Saddle) is inert with no
///     blocking, and is dropped silently like every other blocking clause in the cube.
/// </summary>
public static class CoresetCubeGreenPermanents
{
	public static IReadOnlyList<Card> Cards { get; } =
		[
			// ===== ENCHANTMENTS =====

			// The trample half is the whole card, and AsAura could not grant it until green —
			// EquippedBoostComponent has always carried GrantsTrample but the builder never
			// exposed it, so this would have been a silent +2/+0.
			//
			// "When this Aura is put into a graveyard from the battlefield, return it to its
			// owner's hand" is what makes Rancor a recurring threat rather than card
			// disadvantage. It fires from the graveyard, which is mandatory: by the time
			// CheckStateBasedEffectsAction scans, the Aura has already moved there.
			CardFactory
				.Enchantment("Rancor", manaCost: 1)
				.AsAura(
					powerBonus: 2,
					toughnessBonus: 0,
					trample: true,
					targeting: Single().YourCreatures()
				)
				.WithTriggeredAbility(
					"Undying Rancor",
					new EventTriggerCondition
					{
						EventTypeName = EventTypeNames.PermanentLeftBattlefield,
						Filter = new IsSourceCardSpecification(),
					},
					eb =>
						eb.WithAction(
							new MoveCardToHandAction
							{
								CardIdContextKey = ContextKeys.SourceCardId,
								PlayerIdContextKey = ContextKeys.CastingPlayerId,
							},
							TargetingStrategy.NoTarget()
						),
					activeInZone: ZoneType.Graveyard
				)
				.Build(),
			// A repeatable sacrifice outlet that converts a creature into a better creature. The
			// tutor is filtered by component rather than subtype — "a creature card" is not a
			// subtype string. Bounded by needing a creature to eat, so it needs no per-turn cap.
			CardFactory
				.Enchantment("Evolutionary Leap", manaCost: 2)
				.WithActivatedAbility(
					"Evolve",
					manaCost: 1,
					effect: eb =>
						eb.WithAction(
							new PipelineAction
							{
								Steps = ImmutableList.Create<GameAction>(
									new SelectCardFromZoneAction
									{
										Zone = ZoneType.Library,
										Filter = new IsCardTypeSpecification
										{
											Types = CardType.Creature,
										},
										PlayerIdContextKey = ContextKeys.CastingPlayerId,
										OutputKey = "leap_target",
									},
									new MoveCardToHandAction
									{
										CardIdContextKey = "leap_target",
										PlayerIdContextKey = ContextKeys.CastingPlayerId,
									}
								),
							},
							TargetingStrategy.NoTarget()
						),
					costs: cb => cb.Sacrifice(TargetSpecification.CreatureControlledByYou()),
					maxPerTurn: 0
				)
				.Build(),
			// "At the beginning of the end step, if enchanted creature's power is 4 or greater,
			// destroy this Aura" is cut — there is no end step, and a power-gated self-destruct
			// would need a conditional trigger reading the ENCHANTED creature rather than the
			// controller. "Its activated abilities can't be activated" is cut too. Both cuts make
			// the card strictly better, so it costs one more than printed.
			CardFactory
				.Enchantment("Arachnus Web", manaCost: 4)
				.AsAura(preventsAttacking: true, targeting: Single().OpponentCreatures())
				.Build(),
			// Flash cut — no priority window. That is a real loss on a combat trick, so it costs
			// one less than printed.
			CardFactory
				.Enchantment("Feral Invocation", manaCost: 2)
				.AsAura(powerBonus: 2, toughnessBonus: 2, targeting: Single().YourCreatures())
				.Build(),
			// RESKINNED OFF LANDS. "Enchant land … enchanted land has '{T}: Add two mana'" has
			// nothing to enchant: a played land is already exiled into MaxMana. The card's actual
			// game effect was "gain 3 life and add a permanent mana", and that is what it now is.
			// GainPermanentManaAction raises MaxMana AND CurrentMana, which is what a land does.
			CardFactory
				.Enchantment("Gift of Paradise", manaCost: 3)
				.WithEtbTrigger(
					"Gift of the Wilds",
					eb =>
						eb.WithLifeGain(3)
							.WithAction(
								new GainPermanentManaAction
								{
									Amount = 1,
									TargetContextKey = ContextKeys.CastingPlayerId,
								},
								TargetingStrategy.NoTarget()
							)
				)
				.Build(),
			// "Reveal the top card; if it's a land you may put it onto the battlefield" becomes a
			// straight upkeep ramp — there is no land permanent, so the reveal has nothing to
			// decide. Capped at one per turn as printed.
			CardFactory
				.Enchantment("Into the Wilds", manaCost: 4)
				.WithTriggeredAbility(
					"Growth of the Wilds",
					TriggerConditions.OnYourUpkeep(),
					eb =>
						eb.WithAction(
							new GainPermanentManaAction
							{
								Amount = 1,
								TargetContextKey = ContextKeys.CastingPlayerId,
							},
							TargetingStrategy.NoTarget()
						),
					maxPerTurn: 1
				)
				.Build(),
			// The opening-hand clause is cut — there is no mulligan or pre-game step. maxPerTurn
			// is deliberately 0 (unlimited): the token this does NOT make means there is no
			// self-trigger loop, unlike red's Flameshadow Conjuring, which hung the engine.
			CardFactory
				.Enchantment("Leyline of Vitality", manaCost: 3)
				.WithStaticBoost(0, 1, TargetSpecification.CreatureControlledByYou())
				.WithTriggeredAbility(
					"Verdant Vigour",
					TriggerConditions.OnCreatureYouControlEnters(),
					eb => eb.WithLifeGain(1),
					maxPerTurn: 0
				)
				.Build(),
			// ===== ARTIFACT =====

			// "Can't be blocked by more than one creature" is inert with no blocking and is
			// dropped. The ETB makes a Wolf; attaching to it in the same resolution would need
			// the token's id threaded out of CreateCardAction, so the equip ability does the
			// attaching instead — which is one turn slower and is why it costs one less.
			CardFactory
				.Artifact("Wolfrider's Saddle", manaCost: 3)
				.WithComponent(new EquipmentComponent { PowerBonus = 1, ToughnessBonus = 1 })
				.WithEtbTrigger(
					"Saddle Up",
					eb => eb.WithCreateTokens(CoresetCubeGreenTokens.Wolf(), 1)
				)
				.WithActivatedAbility(
					"Equip",
					manaCost: 3,
					effect: eb =>
						eb.WithAction(new AttachEquipmentAction(), Single().YourCreatures())
				)
				.Build(),
			// ===== PLANESWALKERS =====

			// "+1: Untap two target lands" becomes "+1: Add 2 mana this turn". Untapping lands IS
			// a mana burst here, and AddTemporaryManaAction raises CurrentMana only, so it
			// evaporates at the next refill exactly as the untap would have.
			CardFactory
				.Planeswalker("Garruk Wildspeaker", manaCost: 4)
				.WithLoyalty(3)
				.WithLoyaltyAbility("+1: Add 2 mana this turn", 1, eb => eb.WithAddMana(2))
				.WithLoyaltyAbility(
					"-1: Create a 3/3 Beast",
					-1,
					eb => eb.WithCreateTokens(CoresetCubeGreenTokens.Beast(), 1)
				)
				.WithLoyaltyAbility(
					"-4: Creatures you control get +3/+3 and gain trample",
					-4,
					eb =>
						eb.WithBoost(3, 3)
							.WithTarget(AllValid().AllYourCreatures())
							.WithGrantKeyword(trample: true)
							.WithTarget(AllValid().AllYourCreatures())
				)
				.Build(),
			// REBUILT AROUND WHAT SURVIVES. All three printed abilities animate lands into 4/4
			// trample Elementals, and there are no land permanents — so the Elemental becomes a
			// token and the lands become the mana they actually are. The ultimate keeps its shape:
			// a pile of lands that are also a pile of creatures.
			CardFactory
				.Planeswalker("Nissa, Worldwaker", manaCost: 5)
				.WithLoyalty(3)
				.WithLoyaltyAbility(
					"+1: Create a 4/4 Elemental with trample",
					1,
					eb => eb.WithCreateTokens(CoresetCubeGreenTokens.Elemental(), 1)
				)
				.WithLoyaltyAbility("+1: Add 4 mana this turn", 1, eb => eb.WithAddMana(4))
				.WithLoyaltyAbility(
					"-7: Gain 3 mana and create three 4/4 Elementals with trample",
					-7,
					eb =>
						eb.WithAction(
								new GainPermanentManaAction
								{
									Amount = 3,
									TargetContextKey = ContextKeys.CastingPlayerId,
								},
								TargetingStrategy.NoTarget()
							)
							.WithCreateTokens(CoresetCubeGreenTokens.Elemental(), 3)
				)
				.Build(),
			// The -3 keeps all three printed targets: artifact, enchantment, or creature with
			// flying. HasFlyingSpecification exists (red's Earthquake needed it), so the flying
			// clause is real rather than widened into unconditional creature removal.
			CardFactory
				.Planeswalker("Vivien Reid", manaCost: 5)
				.WithLoyalty(4)
				.WithLoyaltyAbility(
					"+1: Look at the top four cards; take one",
					1,
					eb => eb.WithDig(4)
				)
				.WithLoyaltyAbility(
					"-3: Destroy target artifact, enchantment or flyer",
					-3,
					eb =>
						eb.WithAction(
							new DestroyPermanentAction(),
							TargetingStrategy.SingleTarget(
								new IsOnBattlefieldSpecification().And(
									new IsCardTypeSpecification
									{
										Types = CardType.Artifact | CardType.Enchantment,
									}.Or(
										new IsCreatureSpecification().And(
											new HasFlyingSpecification()
										)
									)
								)
							)
						)
				)
				.WithLoyaltyAbility(
					"-8: You get an emblem that pumps your creatures",
					-8,
					eb =>
						eb.WithAction(
							new GrantEmblemAction
							{
								Emblem = new Emblem
								{
									// Vigilance is unimplemented engine-wide, so the emblem grants
									// the two keywords that exist. It re-applies each upkeep
									// because an emblem is a trigger, not a static ability — a
									// permanent anthem would need StaticAbilityEngine and an
									// emblem has no battlefield permanent to hang one on.
									Name = "Vivien's Wilds",
									Condition = TriggerConditions.OnYourUpkeep(),
									Effect = new CardEffect
									{
										TargetingStrategy = TargetingStrategy.AllValid(
											TargetSpecification.CreatureControlledByYou()
										),
										ActionTemplate = new AddModifierAction
										{
											PowerBonus = 2,
											ToughnessBonus = 2,
											Duration = ModifierDuration.UntilEndOfTurn,
										},
									},
								},
							},
							TargetingStrategy.Self()
						)
				)
				.Build(),
		];
}
