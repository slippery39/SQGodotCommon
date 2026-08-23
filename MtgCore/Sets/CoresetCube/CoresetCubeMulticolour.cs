using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore.Cards.Builders;
using static MtgCore.Cards.Builders.TargetBuilder;

namespace MtgCore;

/// <summary>
/// Multicolour creatures from the Core Set Cube — https://cubecobra.com/cube/list/magiccoreset20xx
///
/// All 27, grouped by the cube's own colour pairs and ordered by mana value within each.
///
/// THE COLOUR PAIRS ARE FLAVOUR, NOT MECHANICS, AND THAT IS THE WHOLE STORY OF THIS SECTION.
/// Cards have no colour in this engine, so a gold card is simply a card: nothing costs more to
/// cast for being two colours, no deck is punished for splashing, and the ten pairs impose no
/// deckbuilding constraint whatever. What survives is the DESIGN of each pair — the BG cards care
/// about creatures dying, the UW cards care about flying, the RW cards attack — and that is worth
/// keeping, because it is what gives a draft its archetypes even when the mana does not enforce
/// them. The pair headings below are for the reader, not the engine.
///
/// The practical consequence is that these 27 are costed as MONOCOLOUR cards of the same mana
/// value. A real gold card buys extra power with the difficulty of casting it; here that
/// difficulty does not exist, so a two-colour rate would make the whole section strictly better
/// than the five colour sections at every point on the curve.
///
/// DIVERGENCES beyond the colour and blocking cuts listed in CoresetCubeBlack:
///   - VIGILANCE is deliberately unimplemented — Citadel Castellan goes without.
///   - NO FLASH (no priority window), so Thunderclap Wyvern loses it and costs one less.
///   - NO MENACE (no blocking), so Brawl-Bash Ogre loses it.
///   - NO STRUCTURAL REPLACEMENT, so Possessed Skaab's "if it would die, exile it instead" is cut.
///   - NO "BEGINNING OF COMBAT" STEP and no optional triggers, so the three cards printed with
///     "at the beginning of combat on your turn, you MAY..." become activated abilities. That is
///     strictly better as a model than a mandatory trigger, which would force you to eat your own
///     board every turn — see Dire Fleet Warmonger.
///   - NO "TAPPED AND ATTACKING" TOKENS — attacking is atomic here — so Skyknight Vanguard's
///     Soldier arrives with haste instead.
///   - PLAYING LANDS OFF THE TOP IS REAL NOW (PlayFromLibraryTopComponent), so Radha keeps it.
/// </summary>
public static class CoresetCubeMulticolour
{
	public static IReadOnlyList<Card> Cards { get; } =
		[
			// ===== BLACK-GREEN — creatures dying is the payoff =====

			// Printed: "Reach. Deathtouch. Whenever another creature dies, each opponent loses 1
			// life." Faithful. The "another" matters: without IsNotSelfSpecification the Archer
			// drains once more on its own death, which is a real extra point of reach.
			CardFactory
				.Creature("Poison-Tip Archer", manaCost: 4, power: 2, toughness: 3)
				.WithSubtype("Elf")
				.WithReach()
				.WithDeathtouch()
				.WithTriggeredAbility(
					"Toxic Volley",
					new EventTriggerCondition
					{
						EventTypeName = EventTypeNames.CreatureDestroyed,
						Filter = new IsNotSelfSpecification(),
					},
					// WithLoseLife, not WithDrain: the printed card drains no life back, and the
					// difference across a long game is real. Random() rather than Single() because
					// TriggerTargeting downgrades user-select inside a trigger anyway, and there is
					// exactly one opponent to pick.
					eb => eb.WithLoseLife(1).WithTarget(Random().Opponent())
				)
				.Build(),
			// Printed: "At the beginning of your end step, if a creature died this turn, draw a
			// card."
			//
			// Faithful, and it needed no new TriggerCondition: the intervening "if" is a
			// ConditionalAction wrapping the existing CreatureDiedThisTurnCondition, which already
			// reads MtgGame.CreaturesDiedThisTurn and already counts EITHER player's creatures.
			CardFactory
				.Creature("Twinblade Assassins", manaCost: 5, power: 5, toughness: 4)
				.WithSubtype("Elf")
				.WithTriggeredAbility(
					"Blood Price",
					TriggerConditions.OnYourEndStep(),
					eb =>
						eb.WithConditionalAction(
								new CreatureDiedThisTurnCondition(),
								new DrawCardsAction
								{
									Amount = 1,
									TargetContextKey = ContextKeys.CastingPlayerId,
								}
							)
							.NoTarget()
				)
				.Build(),
			// ===== BLACK-RED — sacrifice your own creatures =====

			// Printed: "At the beginning of combat on your turn, you may sacrifice another
			// creature. If you do, this gets +2/+2 and gains trample until end of turn."
			//
			// An activated ability, because there is no combat step and no optional trigger. A
			// mandatory version would eat one of your creatures every single turn whether you
			// wanted it to or not, turning an upside into a liability — the sacrifice has to stay
			// a choice, and an activated ability is the only shape that keeps it one.
			CardFactory
				.Creature("Dire Fleet Warmonger", manaCost: 3, power: 3, toughness: 3)
				.WithSubtype("Orc")
				.WithActivatedAbility(
					"Blood Rite",
					manaCost: 0,
					// Written out rather than using WithSelfBuff, for two reasons that both matter.
					// WithSelfBuff is PERMANENT duration, and this pump is until end of turn, so it
					// would stack every activation into an unbounded creature. And WithTarget binds
					// only the pending effect, so a trailing one would leave the buff on its default
					// while the trample grant went to some other creature entirely.
					effect: eb =>
						eb.WithAction(
								new AddModifierAction
								{
									PowerBonus = 2,
									ToughnessBonus = 2,
									Duration = ModifierDuration.UntilEndOfTurn,
									TargetContextKey = ContextKeys.SourceCardId,
								},
								TargetingStrategy.NoTarget()
							)
							.WithAction(
								new GrantKeywordAction
								{
									GrantsTrample = true,
									Duration = ModifierDuration.UntilEndOfTurn,
									TargetContextKey = ContextKeys.SourceCardId,
								},
								TargetingStrategy.NoTarget()
							),
					costs: c => c.Sacrifice(TargetSpecification.OtherCreaturesYouControl())
				)
				.Build(),
			// Printed: "{1}, Sacrifice another creature: This deals 1 damage to any target."
			// Faithful — a sacrifice outlet as an activated ability is exactly what the engine
			// already models, and SacrificeAdditionalCost now announces a real death, so the
			// section's own death payoffs see it.
			CardFactory
				.Creature("Blazing Hellhound", manaCost: 4, power: 4, toughness: 3)
				.WithSubtype("Elemental")
				.WithActivatedAbility(
					"Devour",
					manaCost: 1,
					effect: eb =>
						eb.WithDamage(1).WithTarget(Single().OpponentOrOpponentCreatures()),
					costs: c => c.Sacrifice(TargetSpecification.OtherCreaturesYouControl()),
					maxPerTurn: 0
				)
				.Build(),
			// Printed: "Menace. Whenever this attacks, you may sacrifice another creature. If you
			// do, this gets +2/+2 until end of turn." Menace is cut; the rest becomes an activated
			// ability for the same reason as Dire Fleet Warmonger, and costs one less for the
			// keyword it lost.
			CardFactory
				.Creature("Brawl-Bash Ogre", manaCost: 3, power: 3, toughness: 3)
				.WithSubtype("Ogre")
				.WithActivatedAbility(
					"Brawl",
					manaCost: 0,
					// Until end of turn, not permanent — see Dire Fleet Warmonger for why WithSelfBuff
					// is the wrong tool here.
					effect: eb =>
						eb.WithAction(
							new AddModifierAction
							{
								PowerBonus = 2,
								ToughnessBonus = 2,
								Duration = ModifierDuration.UntilEndOfTurn,
								TargetContextKey = ContextKeys.SourceCardId,
							},
							TargetingStrategy.NoTarget()
						),
					costs: c => c.Sacrifice(TargetSpecification.OtherCreaturesYouControl())
				)
				.Build(),
			// ===== BLUE-BLACK — card selection and the graveyard =====

			// Printed: "{T}: Draw a card, then discard a card. {2}{U}{B},{T}, Sacrifice this:
			// Return target creature card from your graveyard to the battlefield."
			//
			// Faithful. RequiresTap works properly here because it IS a creature — the flag is a
			// no-op only on non-creature permanents — so the loot really costs the tap and cannot
			// be repeated within a turn.
			CardFactory
				.Creature("Obsessive Stitcher", manaCost: 3, power: 0, toughness: 3)
				.WithSubtype("Wizard")
				.WithActivatedAbility(
					"Stitch",
					manaCost: 0,
					effect: eb =>
						eb.WithDraw(1).WithDiscard(1).WithTarget(TargetingStrategy.Self()),
					requiresTap: true
				)
				.WithActivatedAbility(
					"Reanimate",
					manaCost: 4,
					effect: eb => eb.WithReanimate(),
					costs: c => c.SacrificeSelf()
				)
				.Build(),
			// Printed: "Deathtouch. Lifelink. Whenever this enters or deals combat damage to a
			// player, draw a card, then discard a card."
			//
			// Two triggered abilities rather than one, because the two events are unrelated and
			// TriggeredAbilityComponent takes a single condition. Deathtouch plus lifelink on a
			// no-blocker board is a genuinely premium pair — every attack it makes is a favourable
			// trade AND a life swing — so it is priced above its printed three.
			CardFactory
				.Creature("Tomebound Lich", manaCost: 4, power: 1, toughness: 3)
				.WithSubtype("Zombie")
				.WithDeathtouch()
				.WithLifelink()
				.WithEtbTrigger(
					"Bound Knowledge",
					eb => eb.WithDraw(1).WithDiscard(1).WithTarget(TargetingStrategy.Self())
				)
				.WithTriggeredAbility(
					"Bound Knowledge",
					TriggerConditions.OnSelfDealsCombatDamageToPlayer(),
					eb => eb.WithDraw(1).WithDiscard(1).WithTarget(TargetingStrategy.Self())
				)
				.Build(),
			// Printed: "When this enters, return target instant, sorcery, or creature card from
			// your graveyard to your hand. If this would die, exile it instead."
			//
			// The death-replacement is cut — structural replacement rewrites an action rather than
			// a number, which ReplacementModifierComponent explicitly does not cover (see
			// DesignNotes.md). It was a DRAWBACK on the printed card, denying you the recursion
			// twice, so losing it makes the Skaab better and it costs one more.
			//
			// The three-way "instant, sorcery, or creature" narrows to the spell half: a graveyard
			// deck wants the spell back, and the creature clause overlaps every reanimator in the
			// cube.
			CardFactory
				.Creature("Possessed Skaab", manaCost: 3, power: 3, toughness: 2)
				.WithSubtype("Zombie")
				.WithEtbTrigger("Dredge Up", eb => eb.WithAutoReturnSpell())
				.WithTaunt()
				.Build(),
			// ===== BLACK-WHITE — creatures entering, and life =====

			// Printed: "Whenever another creature you control enters, each opponent loses 1 life."
			// Faithful. WithDrain rather than WithLoseLife: inside a trigger NoTarget() overwrites
			// hardcoded TargetIds, so a loss-half aimed by hand hits nobody at all.
			CardFactory
				.Creature("Corpse Knight", manaCost: 2, power: 2, toughness: 2)
				.WithSubtype("Zombie")
				.WithTriggeredAbility(
					"Toll",
					new EventTriggerCondition
					{
						EventTypeName = EventTypeNames.CreatureEnteredBattlefield,
						Filter = new IsControlledByYouSpecification().And(
							new IsNotSelfSpecification()
						),
					},
					eb => eb.WithLoseLife(1).WithTarget(Random().Opponent())
				)
				.Build(),
			// Printed: "As long as you control an enchantment, this gets +1/+1 and has lifelink."
			//
			// FAITHFUL, and it is the card that showed the "conditional static abilities" deferral
			// is narrower than it reads. That deferral is about a conditional TEAM anthem, which
			// StaticAbilityEngine's push model cannot keep current. A condition on a SINGLE
			// creature buffing ITSELF needs no engine at all — it is a live-evaluated
			// PowerToughnessModifier, which is exactly what ThresholdComponent is. One new
			// ThresholdSource value and one case in IsActive, rather than a parallel component.
			CardFactory
				.Creature("Blood-Cursed Knight", manaCost: 3, power: 3, toughness: 2)
				.WithSubtype("Vampire")
				.WithComponent(
					new ThresholdComponent
					{
						CountSource = ThresholdSource.ControlledEnchantments,
						Minimum = 1,
						PowerBonus = 1,
						ToughnessBonus = 1,
						GrantsLifelink = true,
						Duration = ModifierDuration.Permanent,
					}
				)
				.Build(),
			// Printed: "Flying. Lifelink. At the beginning of your end step, if you gained 3 or
			// more life this turn, each opponent loses 3 life."
			//
			// Faithful. The intervening "if" is an AndTriggerCondition rather than a
			// ConditionalAction, because LifeGainedThisTurnCondition already exists as a
			// TriggerCondition (Resplendent Angel) — and note it fires on EACH end step by design,
			// so the OnYourEndStep half is what restricts this to your own.
			CardFactory
				.Creature("Indulging Patrician", manaCost: 3, power: 1, toughness: 4)
				.WithSubtype("Vampire")
				.WithFlying()
				.WithLifelink()
				.WithTriggeredAbility(
					"Sanguine Toast",
					new AndTriggerCondition
					{
						Conditions =
						[
							TriggerConditions.OnYourEndStep(),
							new LifeGainedThisTurnCondition { Minimum = 3 },
						],
					},
					eb => eb.WithLoseLife(3).WithTarget(Random().Opponent())
				)
				.Build(),
			// ===== GREEN-RED — mana and big bodies =====

			// Printed: "{T}: Add one mana of any color. {7},{T}, Sacrifice this: Create a 5/5 red
			// Dragon with flying." The mana half is an upkeep trigger like every other producer in
			// the cube; the Dragon is faithful.
			CardFactory
				.Creature("Draconic Disciple", manaCost: 3, power: 2, toughness: 2)
				.WithSubtype("Shaman")
				.WithTriggeredAbility(
					"Channel",
					TriggerConditions.OnYourUpkeep(),
					eb => eb.WithAddMana(1).WithTarget(TargetingStrategy.Self())
				)
				.WithActivatedAbility(
					"Call the Dragon",
					manaCost: 7,
					effect: eb => eb.WithCreateTokens(CoresetCubeRedTokens.Dragon()),
					costs: c => c.SacrificeSelf()
				)
				.Build(),
			// Printed: "During your turn, Radha has first strike. You may look at the top card of
			// your library any time, and you may play lands from the top of your library.
			// {4}{R}{G}: Radha gets +X/+X until end of turn, where X is the number of lands you
			// control."
			//
			// THE TOP-OF-LIBRARY CLAUSE IS REAL — PlayFromLibraryTopComponent widens
			// GameState.IsInCastableZone, the single predicate all four play actions consult, so
			// PlayLandAction reaches the top of the library with no change of its own. The Godot
			// board shows the card in hand, labelled, so a human can play it exactly as the AI can.
			//
			// "During your turn" is dropped from the first strike: a conditional static would go
			// stale under StaticAbilityEngine's push model, and unconditional first strike on a 3/3
			// is a fair rate at three. The pump reuses LandsPlayedCountComponent, already built for
			// Terravore.
			CardFactory
				.Creature("Radha, Heart of Keld", manaCost: 3, power: 3, toughness: 3)
				.WithSubtype("Elf")
				.WithFirstStrike()
				.WithComponent(new PlayFromLibraryTopComponent { Types = CardType.Land })
				.WithActivatedAbility(
					"Keldon Fury",
					manaCost: 6,
					effect: eb =>
						eb.WithAction(
							new AddCustomModifierAction
							{
								Modifier = new LandsPlayedCountComponent
								{
									Duration = ModifierDuration.UntilEndOfTurn,
								},
								TargetContextKey = ContextKeys.SourceCardId,
							},
							TargetingStrategy.NoTarget()
						)
				)
				.Build(),
			// Printed: "{T}: Add {G} for each creature with power 4 or greater you control.
			// {7}{R}: This deals damage equal to its power to target player or planeswalker."
			//
			// The scaling mana becomes a flat 1 on your upkeep: nothing counts creatures by power
			// into a pipeline amount, and a card whose mana output varies is the hardest kind for
			// the AI to plan around. The damage becomes a flat 4 — its printed power — for the same
			// reason, since "equal to its power" needs a count action that does not exist and the
			// Avenger's power only moves if something buffs it.
			CardFactory
				.Creature("Leafkin Avenger", manaCost: 4, power: 4, toughness: 3)
				.WithSubtype("Elemental")
				.WithTriggeredAbility(
					"Channel the Wild",
					TriggerConditions.OnYourUpkeep(),
					eb => eb.WithAddMana(1).WithTarget(TargetingStrategy.Self())
				)
				.WithActivatedAbility(
					"Elemental Blast",
					manaCost: 8,
					effect: eb => eb.WithDamage(4).WithTarget(Single().Opponent())
				)
				.Build(),
			// ===== GREEN-BLUE — counters and card flow =====

			// Printed: "Whenever you draw a card, put a +1/+1 counter on this creature."
			// Faithful. It works only because CardDrawnEvent reaches PendingGameEvents — it was
			// added to the caller-visible log alone for a long time, and no draw trigger in the
			// engine had ever fired.
			CardFactory
				.Creature("Lorescale Coatl", manaCost: 3, power: 2, toughness: 2)
				.WithSubtype("Snake")
				.WithTriggeredAbility(
					"Scale Up",
					TriggerConditions.OnYouDraw(),
					eb => eb.WithSelfCounters(1)
				)
				.Build(),
			// Printed: "Whenever this or another Elemental you control enters, look at the top card
			// of your library. If it's a land, you may put it onto the battlefield tapped. If you
			// don't, put it into your hand."
			//
			// The branch collapses. A land here is not a permanent — it is consumed into MaxMana —
			// so "put it onto the battlefield" and "put it into your hand" are, respectively, ramp
			// and a draw, and there is no way to test the revealed card's type mid-pipeline anyway
			// (ConditionalAction takes an ActivationCondition, which sees a player, not a context
			// key). Drawing is the branch that fires on most cards and is what the card does in
			// practice, so that is what it does here.
			//
			// The Elemental tribe is three cards across the whole cube, so the trigger widens to
			// any creature you control and the rate is set accordingly.
			CardFactory
				.Creature("Risen Reef", manaCost: 3, power: 1, toughness: 1)
				.WithSubtype("Elemental")
				.WithTriggeredAbility(
					"Tidal Knowledge",
					new EventTriggerCondition
					{
						EventTypeName = EventTypeNames.CreatureEnteredBattlefield,
						Filter = new IsControlledByYouSpecification().And(
							new IsNotSelfSpecification()
						),
					},
					eb => eb.WithDraw(1).WithTarget(TargetingStrategy.Self()),
					maxPerTurn: 1
				)
				.Build(),
			// Printed: "Flying. At the beginning of combat on your turn, you may pay {G}{U}. When
			// you do, put a +1/+1 counter on another target creature you control, and that creature
			// gains flying until end of turn."
			//
			// An activated ability, like the other two "may pay at the beginning of combat" cards.
			// Both effects share one targeting strategy, which is what makes the counter and the
			// flying land on the SAME creature — multi-effect targeting fills the one chosen target
			// into every user-select effect.
			CardFactory
				.Creature("Skyrider Patrol", manaCost: 4, power: 2, toughness: 3)
				.WithSubtype("Elf")
				.WithFlying()
				.WithActivatedAbility(
					"Lift",
					manaCost: 2,
					// Both effects carry the SAME strategy, which is what makes the counter and the
					// flying land on one creature: MtgActionGenerator fills the single chosen
					// target into every user-select effect on the ability.
					effect: eb =>
						eb.WithCounters(1)
							.WithTarget(Single().OtherCreaturesYouControl())
							.WithGrantKeyword(flying: true)
							.WithTarget(Single().OtherCreaturesYouControl())
				)
				.Build(),
			// ===== GREEN-WHITE — counters and going wide =====

			// Printed: "If one or more +1/+1 counters would be put on a creature you control, that
			// many plus one are put on it instead. When this dies, you gain life equal to its
			// power."
			//
			// FAITHFUL, and the counter half is a REPLACEMENT rather than a trigger, which is the
			// whole point: a trigger that adds counters in response to counters being added feeds
			// itself forever. Because CounterBonusComponent applies inside AddCountersAction,
			// exactly one CountersAddedEvent is emitted and it already carries the increased number.
			//
			// The death trigger is a flat 2 — its printed power — since "equal to its power" would
			// need to read the dead card's stats and the Mentor has no way to grow.
			CardFactory
				.Creature("Conclave Mentor", manaCost: 2, power: 2, toughness: 2)
				.WithSubtype("Centaur")
				.WithComponent(new CounterBonusComponent { Amount = 1 })
				.WithDeathTrigger(
					"Parting Gift",
					eb => eb.WithLifeGain(2).WithTarget(TargetingStrategy.Self())
				)
				.Build(),
			// Printed: "Vigilance. Renown 2." Vigilance is unimplemented. WithRenown sets
			// MaxTriggers = 1 — the LIFETIME cap, which is what "if it isn't renowned" means. A
			// per-turn cap would make it grow every single turn.
			CardFactory
				.Creature("Citadel Castellan", manaCost: 3, power: 2, toughness: 3)
				.WithSubtype("Knight")
				// Vigilance -> Taunt. A castellan holds the gate, which is what Taunt says here;
				// vigilance says nothing at all, so this card was paying a keyword tax for a blank.
				.WithTaunt()
				.WithRenown(2)
				.Build(),
			// Printed: "Ironroot Warlord's power is equal to the number of creatures you control.
			// {3}{G}{W}: Create a 1/1 white Soldier creature token."
			//
			// Faithful. Built at base 0/5 with WithPowerEqualToCreatureCount, which is the */N
			// templating — the modifier is live-evaluated, so the power tracks the board rather
			// than going stale, and the token ability feeds it.
			CardFactory
				.Creature("Ironroot Warlord", manaCost: 3, power: 0, toughness: 5)
				.WithSubtype("Treefolk")
				.WithPowerEqualToCreatureCount()
				.WithActivatedAbility(
					"Muster",
					manaCost: 5,
					effect: eb => eb.WithCreateTokens(CoresetCubeTokens.Soldier()),
					maxPerTurn: 0
				)
				.Build(),
			// ===== BLUE-RED — spells in the graveyard =====

			// Printed: "Flying. Haste." Faithful, and a clean rate at two.
			CardFactory
				.Creature("Lightning Stormkin", manaCost: 2, power: 2, toughness: 2)
				.WithSubtype("Elemental")
				.WithFlying()
				.WithHaste()
				.Build(),
			// Printed: "Flying. Enigma Drake's power is equal to the number of instant and sorcery
			// cards in your graveyard."
			//
			// Faithful, and the card that needed GraveyardCountComponent to grow a Types filter and
			// an AffectsToughness switch. AffectsToughness = false is what makes a */4 possible;
			// with it true the Drake would be a */4+X and quietly unkillable.
			//
			// Card.EffectiveTypes reports Instant|Sorcery for a spell built through CardFactory.Spell,
			// which declares no type and cannot tell the two apart. That is exactly the union this
			// card counts, so the derivation fallback is correct here rather than merely tolerated.
			CardFactory
				.Creature("Enigma Drake", manaCost: 3, power: 0, toughness: 4)
				.WithSubtype("Drake")
				.WithFlying()
				.WithComponent(
					new GraveyardCountComponent
					{
						Types = CardType.AnySpell,
						AffectsToughness = false,
						Duration = ModifierDuration.Permanent,
					}
				)
				.Build(),
			// ===== RED-WHITE — attacking =====

			// Printed: "Flying. Whenever this attacks, create a 1/1 white Soldier token that's
			// tapped and attacking."
			//
			// There is no "attacking" state for a token to arrive in — AttackAction declares,
			// resolves and applies damage in one atomic action, so a token created during it has
			// already missed the combat. It arrives with haste instead, which lets it attack on the
			// same turn under its own steam. That is a genuine downgrade (it can be answered before
			// it swings), so the Vanguard keeps its printed two.
			CardFactory
				.Creature("Skyknight Vanguard", manaCost: 2, power: 1, toughness: 2)
				.WithSubtype("Knight")
				.WithFlying()
				.WithTriggeredAbility(
					"Rally",
					TriggerConditions.OnSelfAttacks(),
					eb => eb.WithCreateTokens(CoresetCubeTokens.HastySoldier())
				)
				.Build(),
			// Printed: "Double strike." Faithful. Double strike is a premium keyword on a
			// no-blocker board — every attack into a creature it can kill is a free trade — so a
			// vanilla 2/2 with it is priced at three rather than its printed two.
			CardFactory
				.Creature("Iroas's Champion", manaCost: 3, power: 2, toughness: 2)
				.WithSubtype("Soldier")
				.WithDoubleStrike()
				.Build(),
			// ===== BLUE-WHITE — flying matters =====

			// Printed: "Flying. Creature spells with flying you cast cost {1} less to cast.
			// Whenever another creature you control with flying enters, this gets +1/+1 until end
			// of turn."
			//
			// Faithful. The discount is ConditionalCostReductionComponent.AppliesTo — a
			// specification asked about the CARD BEING CAST, which is the shape Goreclaw needed and
			// the only one an ActivationCondition cannot express, since a condition sees a player
			// and not a card. CostEngine scans the caster's battlefield only, so this never
			// discounts an opponent's spell.
			CardFactory
				.Creature("Watcher of the Spheres", manaCost: 2, power: 2, toughness: 2)
				.WithSubtype("Bird")
				.WithFlying()
				.WithCostReductionFor(
					1,
					new IsCardTypeSpecification { Types = CardType.Creature }.And(
						new HasFlyingSpecification()
					)
				)
				.WithTriggeredAbility(
					"Updraft",
					new EventTriggerCondition
					{
						EventTypeName = EventTypeNames.CreatureEnteredBattlefield,
						Filter = new IsControlledByYouSpecification()
							.And(new IsNotSelfSpecification())
							.And(new HasFlyingSpecification()),
					},
					eb =>
						eb.WithAction(
							new AddModifierAction
							{
								PowerBonus = 1,
								ToughnessBonus = 1,
								Duration = ModifierDuration.UntilEndOfTurn,
								TargetContextKey = ContextKeys.SourceCardId,
							},
							TargetingStrategy.NoTarget()
						)
				)
				.Build(),
			// Printed: "Flying. Other creatures you control with flying get +1/+1."
			//
			// Faithful, with one caveat worth stating because it is invisible: StaticAbilityEngine
			// is a push model that re-stamps only on ETB and LTB, so a creature that GAINS flying
			// after this anthem is already out will not pick the buff up until something else
			// enters or leaves. No card in the cube grants flying permanently to a creature already
			// in play, so nothing hits it today.
			CardFactory
				.Creature("Empyrean Eagle", manaCost: 3, power: 2, toughness: 3)
				.WithSubtype("Bird")
				.WithFlying()
				.WithComponent(
					new StaticPTBoostAbility
					{
						PowerBonus = 1,
						ToughnessBonus = 1,
						Filter = TargetSpecification
							.OtherCreaturesYouControl()
							.And(new HasFlyingSpecification()),
					}
				)
				.Build(),
			// Printed: "Flash. Flying. Other creatures you control with flying get +1/+1."
			// Flash needs a priority window the engine does not have, so it is cut and the Wyvern
			// costs one less — the same treatment Feral Invocation got in green.
			CardFactory
				.Creature("Thunderclap Wyvern", manaCost: 3, power: 2, toughness: 3)
				.WithSubtype("Drake")
				.WithFlying()
				.WithComponent(
					new StaticPTBoostAbility
					{
						PowerBonus = 1,
						ToughnessBonus = 1,
						Filter = TargetSpecification
							.OtherCreaturesYouControl()
							.And(new HasFlyingSpecification()),
					}
				)
				.Build(),
		];
}
