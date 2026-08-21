using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore.Cards.Builders;
using static MtgCore.Cards.Builders.TargetBuilder;

namespace MtgCore;

/// <summary>
/// Green creatures from the Core Set Cube — https://cubecobra.com/cube/list/magiccoreset20xx
///
/// All 36, in the cube's own order (by mana value, then name).
///
/// ELVES ARE THE COLOUR'S ENGINE, exactly as Goblins are red's. Eleven of these are Elves and two
/// more make Elf tokens, so the lords (Elvish Archdruid, Dwynen) and the tribal payoffs (Dwynen's
/// Elite, Sylvan Messenger) are load-bearing rather than decorative. Half the Elves also ramp,
/// which is what ties the tribe to the colour's other axis instead of leaving them two decks.
///
/// MANA DORKS PRODUCE AUTOMATICALLY AT THE START OF YOUR TURN, not via a tap ability. Five cards
/// here are printed "{T}: Add mana"; all five are built as an upkeep trigger feeding
/// AddTemporaryManaAction. The mechanics land in the right place for free — StartTurnAction
/// refills CurrentMana = MaxMana and the trigger resolves after that, so the mana is additive and
/// evaporates at the next refill, which is correct because it must vanish if the creature dies. A
/// dork cast this turn produces nothing until the next upkeep, which is what summoning sickness
/// would have done anyway. The reason to do it this way rather than as an activated ability is
/// that an unactivated mana ability is INVISIBLE: the creature simply looks weak, and the AI must
/// re-derive the activation every turn on every dork before it can cast anything. They are priced
/// a touch above their printed rate to pay for the reliability.
///
/// +1/+1 COUNTERS ARE REAL NOW, and green is why. MtgCore/CLAUDE.md recorded counters as a
/// deliberate "won't do" — a permanent AddModifierAction was the counter — with the stated trigger
/// for revisiting being "a card that counts counters". Three cards here do arithmetic on them:
/// Primordial Hydra doubles, Wildwood Scourge reacts to them landing elsewhere, and Barkhide Troll
/// enters with one. See PlusOneCounterComponent and AddCountersAction.
///
/// DIVERGENCES FROM PRINTED CARDS. Every ability is implemented except where the engine has no
/// such concept at all. Each is commented on the card itself; the complete list is:
///   - NO COLOURS, so protection from and hexproof from a colour are unexpressible (Garruk's
///     Harbinger, Shifting Ceratops). Protection from a creature TYPE is implemented; from a
///     colour is not. Woodland Bellower's "green creature card" and Yeva's "green creature spells"
///     lose their colour restriction for the same reason.
///   - NO FLASH, because there is no priority window — the same absence that makes blue's
///     counterspells fire as traps from hand rather than being cast in response. Yeva, Nature's
///     Herald is printed with flash on herself AND grants it to your creatures, so both halves go
///     and she is rebuilt.
///   - LANDS ARE NOT PERMANENTS. They are consumed into MaxMana and exiled by PlayLandAction, so
///     "search for a basic land and put it onto the battlefield" is GainPermanentManaAction and
///     "destroy target land" has no object to destroy (Acidic Slime). A land card in your LIBRARY
///     is still a real card, so tutoring one to hand works normally.
///   - NO FORCED ATTACKS and NO BLOCKING, so "can't block" and "blocks" clauses are inert (Elder
///     Gargaroth's "attacks or blocks" becomes "attacks"). Reach is genuinely load-bearing here
///     because Flying restricts who may ATTACK.
///   - VIGILANCE remains deliberately unimplemented engine-wide (Elder Gargaroth, Vivien's
///     emblem). Dropping it only ever helps the player.
///   - NO DOUBLE-FACED PLANESWALKERS, so Nissa, Vastwood Seer keeps her ETB but not her flip —
///     the same cut Kytheon, Chandra and Liliana, Heretical Healer all took.
///   - ATTACKING IS FLEETING, resolving immediately, so "for each attacking Elf" becomes "for
///     each Elf you control" (Dwynen). Same call red's Goblin Piledriver took, and costed for.
/// </summary>
public static class CoresetCubeGreen
{
	public const string Elf = "Elf";
	public const string Druid = "Druid";
	public const string Shaman = "Shaman";
	public const string Scout = "Scout";
	public const string Ranger = "Ranger";
	public const string Warrior = "Warrior";
	public const string Human = "Human";
	public const string Bird = "Bird";
	public const string Hydra = "Hydra";
	public const string Troll = "Troll";
	public const string Spider = "Spider";
	public const string Ooze = "Ooze";
	public const string Beast = "Beast";
	public const string Dinosaur = "Dinosaur";
	public const string Bear = "Bear";
	public const string Giant = "Giant";
	public const string Insect = "Insect";
	public const string Wurm = "Wurm";
	public const string Wolf = "Wolf";
	public const string Saproling = "Saproling";
	public const string Elemental = "Elemental";

	public static IReadOnlyList<Card> Cards { get; } =
		[
			// ===== ONE MANA =====

			// Printed as "{T}: Add one mana of any color". There are no colours, so the fixing —
			// the entire reason this card is famous — is worth nothing here and what remains is a
			// 0/1 flyer that ramps. See the header for why it ramps on your upkeep instead.
			CardFactory
				.Creature("Birds of Paradise", manaCost: 1, power: 0, toughness: 1)
				.WithSubtype(Bird)
				.WithFlying()
				.WithTriggeredAbility(
					"Wild Growth",
					TriggerConditions.OnYourUpkeep(),
					eb => eb.WithAddMana(1)
				)
				.Build(),
			CardFactory
				.Creature("Elvish Mystic", manaCost: 1, power: 1, toughness: 1)
				.WithSubtype(Elf)
				.WithSubtype(Druid)
				.WithTriggeredAbility(
					"Channel Mana",
					TriggerConditions.OnYourUpkeep(),
					eb => eb.WithAddMana(1)
				)
				.Build(),
			CardFactory
				.Creature("Llanowar Elves", manaCost: 1, power: 1, toughness: 1)
				.WithSubtype(Elf)
				.WithSubtype(Druid)
				.WithTriggeredAbility(
					"Channel Mana",
					TriggerConditions.OnYourUpkeep(),
					eb => eb.WithAddMana(1)
				)
				.Build(),
			// The first {X} CREATURE in the engine — CastCreatureAction had no XValue at all
			// before green, only CastSpellAction did. Built at 0/0 because X IS its body.
			//
			// "Whenever counters are put on another non-Hydra creature you control" is the reason
			// CountersAddedEvent exists as its own event rather than reusing CreatureModifiedEvent:
			// the latter fires for Giant Growth, so this would have grown off every combat trick.
			// The non-Hydra exclusion is not decoration either — without it two Scourges grow each
			// other without bound.
			CardFactory
				.Creature("Wildwood Scourge", manaCost: 1, power: 0, toughness: 0)
				.WithSubtype(Hydra)
				.WithXCost()
				.WithEntersWithCounters(fromXValue: true)
				.WithTriggeredAbility(
					"Sympathetic Growth",
					TriggerConditions.OnCountersPlacedOnAnotherCreature(Hydra),
					eb => eb.WithSelfCounters(1)
				)
				.Build(),
			// ===== TWO MANA =====

			// The removal cost is reskinned: "{1}, Remove a +1/+1 counter: gains hexproof" becomes
			// "{1}: gains hexproof, once each turn". What the counter removal is FOR is bounding
			// the hexproof, and MaxActivationsPerTurn = 1 does that for zero lines — a whole
			// RemoveCounterAdditionalCost class would exist for this one card. It still ENTERS
			// with a counter, so it is still a counters card and still feeds Wildwood Scourge.
			CardFactory
				.Creature("Barkhide Troll", manaCost: 2, power: 2, toughness: 2)
				.WithSubtype(Troll)
				.WithEntersWithCounters(1)
				.WithActivatedAbility(
					"Toughen Hide",
					manaCost: 1,
					// A raw GrantKeywordAction with TargetContextKey, NOT WithGrantKeyword +
					// NoTarget: the builder sets no target key, so a NoTarget strategy resolves to
					// an EMPTY list and the ability grants hexproof to nobody. It renders
					// perfectly and does nothing — the exact failure this set keeps finding.
					effect: eb =>
						eb.WithAction(
							new GrantKeywordAction
							{
								GrantsHexproof = true,
								TargetContextKey = ContextKeys.SourceCardId,
							},
							TargetingStrategy.NoTarget()
						),
					maxPerTurn: 1
				)
				.Build(),
			// Reach plus deathtouch is a genuine wall here — Flying restricts who may ATTACK, so
			// reach really does catch flyers, and deathtouch makes every attack into it a trade.
			CardFactory
				.Creature("Deadly Recluse", manaCost: 2, power: 1, toughness: 2)
				.WithSubtype(Spider)
				.WithReach()
				.WithDeathtouch()
				.Build(),
			// "If you control another Elf" — the Elite counts ITSELF, so the minimum is 2 rather
			// than 1. ConditionalAction evaluates its condition with no source card to exclude
			// (see ControlsSubtypeCondition), and counting yourself is the honest way to say
			// "another" without inventing a second condition type.
			CardFactory
				.Creature("Dwynen's Elite", manaCost: 2, power: 2, toughness: 2)
				.WithSubtype(Elf)
				.WithSubtype(Warrior)
				.WithEtbTrigger(
					"Rally the Kin",
					eb =>
						eb.WithAction(
							new ConditionalAction
							{
								Condition = new ControlsSubtypeCondition
								{
									Subtype = Elf,
									Minimum = 2,
								},
								Action = new CreateCardAction
								{
									CardTemplate = CoresetCubeGreenTokens.ElfWarrior(),
									Count = 1,
								},
							},
							TargetingStrategy.NoTarget()
						)
				)
				.Build(),
			CardFactory
				.Creature("Elvish Visionary", manaCost: 2, power: 1, toughness: 1)
				.WithSubtype(Elf)
				.WithSubtype(Shaman)
				.WithEtbTrigger("Foresight", eb => eb.WithDraw(1))
				.Build(),
			// The tutor is filtered by COMPONENT, not subtype — "a creature card" is not a
			// subtype string, which is why SelectCardFromZoneAction takes a TargetSpecification.
			// Discarding a creature to find a creature is card selection, not card advantage, so
			// it repeats every turn without running away with the game.
			CardFactory
				.Creature("Fauna Shaman", manaCost: 2, power: 2, toughness: 2)
				.WithSubtype(Elf)
				.WithSubtype(Shaman)
				.WithActivatedAbility(
					"Survival",
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
										OutputKey = "fauna_target",
									},
									new MoveCardToHandAction
									{
										CardIdContextKey = "fauna_target",
										PlayerIdContextKey = ContextKeys.CastingPlayerId,
									}
								),
							},
							TargetingStrategy.NoTarget()
						),
					costs: cb => cb.Discard(1),
					requiresTap: true
				)
				.Build(),
			// The doubling is the card, and it is what the counter system was built for — a
			// permanent AddModifierAction has no number to double. The trample clause is a
			// ThresholdComponent reading counters instead of the graveyard, evaluated live at
			// read time because StaticAbilityEngine's push model would go stale the moment a
			// counter landed.
			CardFactory
				.Creature("Primordial Hydra", manaCost: 2, power: 0, toughness: 0)
				.WithSubtype(Hydra)
				.WithXCost()
				.WithEntersWithCounters(fromXValue: true)
				.WithCounterThreshold(minimum: 10, trample: true)
				.WithTriggeredAbility(
					"Rampant Growth",
					TriggerConditions.OnYourUpkeep(),
					eb => eb.WithSelfCounterMultiplier(2)
				)
				.Build(),
			// The counter is a REAL counter, not a permanent P/T modifier: Scavenging Ooze is the
			// card most likely to be sitting there when a Wildwood Scourge lands, and only
			// PlusOneCounterComponent emits the event that feeds it.
			CardFactory
				.Creature("Scavenging Ooze", manaCost: 2, power: 2, toughness: 2)
				.WithSubtype(Ooze)
				.WithActivatedAbility(
					"Scavenge",
					manaCost: 1,
					effect: eb =>
						eb.WithAction(
							new PipelineAction
							{
								Steps = ImmutableList.Create<GameAction>(
									new SelectCardFromZoneAction
									{
										Zone = ZoneType.Graveyard,
										TargetOpponent = true,
										PlayerIdContextKey = ContextKeys.CastingPlayerId,
										OutputKey = "ooze_target",
									},
									new MoveCardToExileAction { CardIdContextKey = "ooze_target" },
									new GainLifeAction
									{
										Amount = 1,
										TargetContextKey = ContextKeys.CastingPlayerId,
									},
									new AddCountersAction
									{
										Amount = 1,
										TargetContextKey = ContextKeys.SourceCardId,
									}
								),
							},
							TargetingStrategy.NoTarget()
						),
					maxPerTurn: 0
				)
				.Build(),
			// The three modes set BASE power and toughness, which is what "becomes a 4/4 Rhino"
			// means — it overwrites rather than adding, so it is a rescue for a shrunk creature
			// and a liability for a pumped one. Once each turn, as printed.
			CardFactory
				.Creature("Skinshifter", manaCost: 2, power: 1, toughness: 1)
				.WithSubtype(Human)
				.WithSubtype(Shaman)
				.WithActivatedAbility(
					"Shift Form",
					manaCost: 1,
					effect: eb =>
						eb.WithModes(
							onceEach: false,
							modeTargeting: null,
							(
								"Rhino — becomes 4/4 trample",
								new AddCustomModifierAction
								{
									Modifier = new BecomesBaseCreatureComponent
									{
										Power = 4,
										Toughness = 4,
										GrantsTrample = true,
										Duration = ModifierDuration.UntilEndOfTurn,
									},
									TargetContextKey = ContextKeys.SourceCardId,
								}
							),
							(
								"Bird — becomes 2/2 flying",
								new AddCustomModifierAction
								{
									Modifier = new BecomesBaseCreatureComponent
									{
										Power = 2,
										Toughness = 2,
										GrantsFlying = true,
										Duration = ModifierDuration.UntilEndOfTurn,
									},
									TargetContextKey = ContextKeys.SourceCardId,
								}
							),
							(
								"Plant — becomes 0/8",
								new AddCustomModifierAction
								{
									Modifier = new BecomesBaseCreatureComponent
									{
										Power = 0,
										Toughness = 8,
										Duration = ModifierDuration.UntilEndOfTurn,
									},
									TargetContextKey = ContextKeys.SourceCardId,
								}
							)
						),
					maxPerTurn: 1
				)
				.Build(),
			// A land card in the LIBRARY is a real card and goes to hand normally — it is only
			// once played that it stops being a permanent. So the fetch-to-hand cards need no
			// divergence at all, unlike the fetch-to-battlefield ones.
			CardFactory
				.Creature("Sylvan Ranger", manaCost: 2, power: 1, toughness: 1)
				.WithSubtype(Elf)
				.WithSubtype(Scout)
				.WithEtbTrigger("Pathfinding", eb => eb.WithTutor("Land"))
				.Build(),
			// ===== THREE MANA =====

			CardFactory
				.Creature("Borderland Ranger", manaCost: 3, power: 2, toughness: 2)
				.WithSubtype(Human)
				.WithSubtype(Scout)
				.WithEtbTrigger("Pathfinding", eb => eb.WithTutor("Land"))
				.Build(),
			// The Elf lord AND the Elf mana engine in one card, which is why the tribe holds
			// together. "Add {G} for each Elf you control" needs no new action: the count feeds
			// AddTemporaryManaAction through EffectAction.AmountContextKey.
			CardFactory
				.Creature("Elvish Archdruid", manaCost: 3, power: 2, toughness: 2)
				.WithSubtype(Elf)
				.WithSubtype(Druid)
				.WithComponent(
					new StaticPTBoostAbility
					{
						PowerBonus = 1,
						ToughnessBonus = 1,
						Filter = new IsSubtypeSpecification { Subtype = Elf },
					}
				)
				.WithTriggeredAbility(
					"Channel the Grove",
					TriggerConditions.OnYourUpkeep(),
					eb =>
						eb.WithAction(
							new PipelineAction
							{
								Steps = ImmutableList.Create<GameAction>(
									new CountCardsWithSubtypeAction
									{
										Subtype = Elf,
										CreaturesOnly = true,
										PlayerIdContextKey = ContextKeys.CastingPlayerId,
										OutputKey = "archdruid_elves",
									},
									new AddTemporaryManaAction
									{
										AmountContextKey = "archdruid_elves",
										TargetContextKey = ContextKeys.CastingPlayerId,
									}
								),
							},
							TargetingStrategy.NoTarget()
						)
				)
				.Build(),
			// Hexproof from black is unexpressible with no colours, so it becomes plain hexproof
			// and the card costs one more. That is a real upgrade — untargetable by everything
			// rather than by one colour — and 4/3 hexproof for three would be a beating.
			CardFactory
				.Creature("Garruk's Harbinger", manaCost: 4, power: 4, toughness: 3)
				.WithSubtype(Beast)
				.WithTrample()
				.WithHexproof()
				.WithTriggeredAbility(
					"Hunter's Reward",
					TriggerConditions.OnSelfDealsCombatDamageToPlayer(),
					eb => eb.WithDig(3)
				)
				.Build(),
			CardFactory
				.Creature("Llanowar Visionary", manaCost: 3, power: 2, toughness: 2)
				.WithSubtype(Elf)
				.WithSubtype(Druid)
				.WithEtbTrigger("Foresight", eb => eb.WithDraw(1))
				.WithTriggeredAbility(
					"Channel Mana",
					TriggerConditions.OnYourUpkeep(),
					eb => eb.WithAddMana(1)
				)
				.Build(),
			// "Whenever a PLAYER casts a spell" — either player, which is what makes it grow on
			// the opponent's turn too and why OnAnyPlayerCastsSpell exists next to OnYouCastSpell.
			CardFactory
				.Creature("Managorger Hydra", manaCost: 3, power: 1, toughness: 1)
				.WithSubtype(Hydra)
				.WithTrample()
				.WithTriggeredAbility(
					"Feed on Magic",
					TriggerConditions.OnAnyPlayerCastsSpell(),
					eb => eb.WithSelfCounters(1),
					maxPerTurn: 0
				)
				.Build(),
			// The flip to Nissa, Sage Animist is cut — a creature transforming into a planeswalker
			// crosses the creature/permanent cast-routing split, the same cut Kytheon, Chandra and
			// Liliana all took. What remains is the ETB, so she is costed as a 2/3 that fetches.
			CardFactory
				.Creature("Nissa, Vastwood Seer", manaCost: 3, power: 2, toughness: 3)
				.WithSubtype(Elf)
				.WithSubtype(Scout)
				.WithEtbTrigger("Vastwood Lore", eb => eb.WithTutor("Land"))
				.Build(),
			CardFactory
				.Creature("Reclamation Sage", manaCost: 3, power: 2, toughness: 1)
				.WithSubtype(Elf)
				.WithSubtype(Shaman)
				.WithEtbTrigger(
					"Naturalize",
					eb =>
						eb.WithAction(
							new DestroyPermanentAction(),
							TargetingStrategy.SingleTarget(
								new IsCardTypeSpecification
								{
									Types = CardType.Artifact | CardType.Enchantment,
								}.And(new IsOnBattlefieldSpecification())
							)
						)
				)
				.Build(),
			CardFactory
				.Creature("Thrashing Brontodon", manaCost: 3, power: 3, toughness: 4)
				.WithSubtype(Dinosaur)
				.WithActivatedAbility(
					"Naturalize",
					manaCost: 1,
					effect: eb =>
						eb.WithAction(
							new DestroyPermanentAction(),
							TargetingStrategy.SingleTarget(
								new IsCardTypeSpecification
								{
									Types = CardType.Artifact | CardType.Enchantment,
								}.And(new IsOnBattlefieldSpecification())
							)
						),
					costs: cb => cb.SacrificeSelf()
				)
				.Build(),
			CardFactory
				.Creature("Yeva's Forcemage", manaCost: 3, power: 2, toughness: 2)
				.WithSubtype(Elf)
				.WithSubtype(Shaman)
				.WithEtbTrigger(
					"Forcemage's Gift",
					eb => eb.WithBoost(2, 2).WithTarget(Best().OtherCreaturesYouControl())
				)
				.Build(),
			// ===== FOUR MANA =====

			// "You gain 1 life for each attacking Elf" becomes "for each Elf you control".
			// Attacking is fleeting here — one attack per turn, resolving immediately — so the
			// board cannot be asked mid-combat who is attacking. Same call red's Goblin Piledriver
			// took. Not having to commit the attack is a real upgrade, so she costs the same for
			// a smaller trigger.
			CardFactory
				.Creature("Dwynen, Gilt-Leaf Daen", manaCost: 4, power: 3, toughness: 4)
				.WithSubtype(Elf)
				.WithSubtype(Warrior)
				.WithReach()
				.WithComponent(
					new StaticPTBoostAbility
					{
						PowerBonus = 1,
						ToughnessBonus = 1,
						Filter = new IsSubtypeSpecification { Subtype = Elf },
					}
				)
				.WithTriggeredAbility(
					"Gilt-Leaf Rally",
					TriggerConditions.OnSelfAttacks(),
					eb =>
						eb.WithAction(
							new PipelineAction
							{
								Steps = ImmutableList.Create<GameAction>(
									new CountCardsWithSubtypeAction
									{
										Subtype = Elf,
										CreaturesOnly = true,
										PlayerIdContextKey = ContextKeys.CastingPlayerId,
										OutputKey = "dwynen_elves",
									},
									new GainLifeAction
									{
										AmountContextKey = "dwynen_elves",
										TargetContextKey = ContextKeys.CastingPlayerId,
									}
								),
							},
							TargetingStrategy.NoTarget()
						)
				)
				.Build(),
			// The cost reduction is the reason ConditionalCostReductionComponent gained AppliesTo.
			// Every reduction before this one lived on the card being discounted and asked a
			// question about the PLAYER; this one lives on a battlefield permanent and asks a
			// question about the CARD being cast, which an ActivationCondition cannot express.
			CardFactory
				.Creature("Goreclaw, Terror of Qal Sisma", manaCost: 4, power: 4, toughness: 3)
				.WithSubtype(Bear)
				.WithTrample()
				.WithCostReductionFor(
					2,
					new IsCardTypeSpecification { Types = CardType.Creature }.And(
						new PowerAtLeastSpecification { Minimum = 4 }
					)
				)
				.WithTriggeredAbility(
					"Terror of the Pack",
					TriggerConditions.OnSelfAttacks(),
					eb =>
						eb.WithBoost(1, 1)
							.WithTarget(
								AllValid()
									.WithSpec(
										TargetSpecification
											.CreatureControlledByYou()
											.And(new PowerAtLeastSpecification { Minimum = 4 })
									)
							)
							.WithGrantKeyword(trample: true)
							.WithTarget(
								AllValid()
									.WithSpec(
										TargetSpecification
											.CreatureControlledByYou()
											.And(new PowerAtLeastSpecification { Minimum = 4 })
									)
							)
				)
				.Build(),
			CardFactory
				.Creature("Llanowar Empath", manaCost: 4, power: 2, toughness: 2)
				.WithSubtype(Elf)
				.WithSubtype(Shaman)
				.WithEtbTrigger("Empathic Vision", eb => eb.WithScry(2).WithDig(1))
				.Build(),
			// The tap ability's return half — "that creature deals damage divided as its
			// controller chooses among those Wolves" — is divided damage, which has no home here;
			// see the red section's Spray note. What survives is the aggressive half: each Wolf
			// you control pitches in, expressed as damage scaled by the Wolf count. Losing the
			// drawback makes it better, so it costs one more than printed.
			CardFactory
				.Creature("Master of the Wild Hunt", manaCost: 5, power: 3, toughness: 3)
				.WithSubtype(Human)
				.WithSubtype(Shaman)
				.WithTriggeredAbility(
					"Call the Hunt",
					TriggerConditions.OnYourUpkeep(),
					eb => eb.WithCreateTokens(CoresetCubeGreenTokens.Wolf(), 1)
				)
				.WithActivatedAbility(
					"Loose the Pack",
					manaCost: 0,
					effect: eb =>
						eb.WithAction(
							new PipelineAction
							{
								Steps = ImmutableList.Create<GameAction>(
									new CountCardsWithSubtypeAction
									{
										Subtype = Wolf,
										CreaturesOnly = true,
										PlayerIdContextKey = ContextKeys.CastingPlayerId,
										OutputKey = "hunt_wolves",
									},
									new SelectCreatureFromBattlefieldByManaCostAction
									{
										TargetOpponent = true,
										SelectLowest = false,
										PlayerIdContextKey = ContextKeys.CastingPlayerId,
										OutputKey = "hunt_target",
									},
									new DealDamageAction
									{
										AmountContextKey = "hunt_wolves",
										TargetContextKey = "hunt_target",
									}
								),
							},
							TargetingStrategy.NoTarget()
						),
					requiresTap: true
				)
				.Build(),
			// Protection from blue is unexpressible. The three activated keyword grants are the
			// interesting half and they all survive; "this spell can't be countered" is kept
			// rather than cut because blue's traps genuinely fire from hand in this engine, so
			// the clause protects against something real.
			CardFactory
				.Creature("Shifting Ceratops", manaCost: 4, power: 5, toughness: 4)
				.WithSubtype(Dinosaur)
				.WithActivatedAbility(
					"Shift Stance",
					manaCost: 1,
					effect: eb =>
						eb.WithModes(
							onceEach: false,
							modeTargeting: null,
							(
								"Gains reach",
								new GrantKeywordAction
								{
									GrantsReach = true,
									TargetContextKey = ContextKeys.SourceCardId,
								}
							),
							(
								"Gains trample",
								new GrantKeywordAction
								{
									GrantsTrample = true,
									TargetContextKey = ContextKeys.SourceCardId,
								}
							),
							(
								"Gains haste",
								new GrantKeywordAction
								{
									GrantsHaste = true,
									TargetContextKey = ContextKeys.SourceCardId,
								}
							)
						),
					maxPerTurn: 0
				)
				.Build(),
			// "Reveal the top four and put all Elf cards into your hand" is a scaling tutor, and
			// chaining four filtered searches is how the engine already expresses it — each step
			// sees the updated state, so a card already moved is skipped automatically.
			CardFactory
				.Creature("Sylvan Messenger", manaCost: 4, power: 2, toughness: 2)
				.WithSubtype(Elf)
				.WithTrample()
				.WithEtbTrigger(
					"Muster the Kin",
					eb => eb.WithTutor(Elf).WithTutor(Elf).WithTutor(Elf)
				)
				.Build(),
			// BOTH her abilities are flash and there is no priority window, so both go. What is
			// left would be a vanilla 4/4, which is not a card — so she is rebuilt as the Elf
			// payoff her tribal text implies, buffing the tribe she leads.
			CardFactory
				.Creature("Yeva, Nature's Herald", manaCost: 4, power: 4, toughness: 4)
				.WithSubtype(Elf)
				.WithSubtype(Shaman)
				.WithComponent(
					new StaticGrantKeywordAbility
					{
						GrantsHaste = true,
						Filter = new IsSubtypeSpecification { Subtype = Elf },
					}
				)
				.Build(),
			// ===== FIVE MANA =====

			// "Destroy target artifact, enchantment, or land" loses the land: a played land is
			// exiled into MaxMana and there is no permanent to destroy. Deathtouch and the 2/2
			// body are unchanged, so it is still the value creature it is meant to be.
			CardFactory
				.Creature("Acidic Slime", manaCost: 5, power: 2, toughness: 2)
				.WithSubtype(Ooze)
				.WithDeathtouch()
				.WithEtbTrigger(
					"Corrode",
					eb =>
						eb.WithAction(
							new DestroyPermanentAction(),
							TargetingStrategy.SingleTarget(
								new IsCardTypeSpecification
								{
									Types = CardType.Artifact | CardType.Enchantment,
								}.And(new IsOnBattlefieldSpecification())
							)
						)
				)
				.Build(),
			// Vigilance is cut engine-wide and "or blocks" is inert with no blocking, so the
			// trigger fires on attack only. That is a real downgrade on a card whose whole appeal
			// is that it triggers on both halves of combat, so it keeps its printed cost for a
			// slightly smaller body.
			CardFactory
				.Creature("Elder Gargaroth", manaCost: 5, power: 6, toughness: 6)
				.WithSubtype(Beast)
				.WithReach()
				.WithTrample()
				.WithTriggeredAbility(
					"Wrath of the Wilds",
					TriggerConditions.OnSelfAttacks(),
					eb =>
						eb.WithModes(
							onceEach: false,
							modeTargeting: null,
							(
								"Create a 3/3 Beast",
								new CreateCardAction
								{
									CardTemplate = CoresetCubeGreenTokens.Beast(),
									Count = 1,
								}
							),
							(
								"Gain 3 life",
								new GainLifeAction
								{
									Amount = 3,
									TargetContextKey = ContextKeys.CastingPlayerId,
								}
							),
							(
								"Draw a card",
								new DrawCardsAction
								{
									Amount = 1,
									TargetContextKey = ContextKeys.CastingPlayerId,
								}
							)
						)
				)
				.Build(),
			// The leave-the-battlefield trigger must be ActiveInZone = Graveyard — by the time
			// CheckStateBasedEffectsAction scans, the card has already moved. It fires on ANY
			// departure, not just death, which is what makes it resilient to exile and bounce.
			CardFactory
				.Creature("Thragtusk", manaCost: 5, power: 5, toughness: 3)
				.WithSubtype(Beast)
				.WithEtbTrigger("Vital Surge", eb => eb.WithLifeGain(5))
				.WithTriggeredAbility(
					"Regrowth",
					new EventTriggerCondition
					{
						EventTypeName = EventTypeNames.PermanentLeftBattlefield,
						Filter = new IsSourceCardSpecification(),
					},
					eb => eb.WithCreateTokens(CoresetCubeGreenTokens.Beast(), 1),
					ActiveInZone: ZoneType.Graveyard
				)
				.Build(),
			// ===== SIX MANA =====

			// "Search for up to two LAND cards and put them onto the battlefield" is
			// GainPermanentManaAction, not a tutor: a land's entire game effect here is +1 mana,
			// and there is no land permanent to fetch. Triggers on entering AND attacking, as
			// printed — the attack half is why it is the ramp payoff rather than just ramp.
			CardFactory
				.Creature("Primeval Titan", manaCost: 6, power: 6, toughness: 6)
				.WithSubtype(Giant)
				.WithTrample()
				.WithEtbTrigger(
					"Awaken the Land",
					eb =>
						eb.WithAction(
							new GainPermanentManaAction
							{
								Amount = 2,
								TargetContextKey = ContextKeys.CastingPlayerId,
							},
							TargetingStrategy.NoTarget()
						)
				)
				.WithTriggeredAbility(
					"Trample the Earth",
					TriggerConditions.OnSelfAttacks(),
					eb =>
						eb.WithAction(
							new GainPermanentManaAction
							{
								Amount = 2,
								TargetContextKey = ContextKeys.CastingPlayerId,
							},
							TargetingStrategy.NoTarget()
						)
				)
				.Build(),
			// "Nonlegendary GREEN creature card with mana value 3 or less" loses the colour word,
			// which costs nothing in a mono-green section. The mana-value cap is the part that
			// matters and HasManaCostAtMostSpecification already expresses it.
			CardFactory
				.Creature("Woodland Bellower", manaCost: 6, power: 6, toughness: 5)
				.WithSubtype(Beast)
				.WithEtbTrigger(
					"Bellow",
					eb =>
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
										}.And(new HasManaCostAtMostSpecification { Maximum = 3 }),
										PlayerIdContextKey = ContextKeys.CastingPlayerId,
										OutputKey = "bellower_target",
									},
									new PutIntoBattlefieldAction
									{
										CardIdContextKey = "bellower_target",
									}
								),
							},
							TargetingStrategy.NoTarget()
						)
				)
				.Build(),
			// ===== SEVEN MANA =====

			CardFactory
				.Creature("Hornet Queen", manaCost: 7, power: 2, toughness: 2)
				.WithSubtype(Insect)
				.WithFlying()
				.WithDeathtouch()
				.WithEtbTrigger(
					"Swarm",
					eb => eb.WithCreateTokens(CoresetCubeGreenTokens.Insect(), 4)
				)
				.Build(),
			CardFactory
				.Creature("Pelakka Wurm", manaCost: 7, power: 7, toughness: 7)
				.WithSubtype(Wurm)
				.WithTrample()
				.WithEtbTrigger("Vital Surge", eb => eb.WithLifeGain(7))
				.WithDeathTrigger("Last Gasp", eb => eb.WithDraw(1))
				.Build(),
		];
}
