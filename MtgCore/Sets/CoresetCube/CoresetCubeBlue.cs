using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore.Cards.Builders;
using static MtgCore.Cards.Builders.TargetBuilder;

namespace MtgCore;

/// <summary>
/// Blue creatures from the Core Set Cube — https://cubecobra.com/cube/list/magiccoreset20xx
///
/// All 28, in the cube's own order (by mana value, then name).
///
/// WALLS. Fog Bank and Wall of Frost are pure blockers, and this engine has no blocking. Taunt is
/// its blocker substitute, so both become Taunt creatures at their printed stats. Fog Bank also
/// prevents all combat damage to itself, which as a plain Taunt creature would make it an
/// unremovable roadblock that every attack is compelled into forever — with no way to go wide,
/// the opponent would simply have no answer. It therefore carries TauntUntilAttackedComponent and
/// drops Taunt after soaking one attack each turn, which is exactly what one blocker does.
///
/// FREEZE. "Doesn't untap during its controller's next untap step" is CreatureComponent.
/// FrozenTurns; "for as long as you control this" is FrozenBySourceId. Both build on the Exhaust
/// mechanic added for white.
///
/// OTHER DIVERGENCES, each also commented on its card:
///   - "Can't be blocked" and islandwalk are blank — there is no blocking, and lands are not
///     battlefield permanents. Those clauses become Taunt-ignoring or a small stat bump.
///   - Jace, Vryn's Prodigy keeps its looter half; the flip to a planeswalker crosses the
///     creature/permanent cast-routing split, the same cut Kytheon took in white.
/// </summary>
public static class CoresetCubeBlue
{
	public const string Wizard = "Wizard";
	public const string Human = "Human";
	public const string Merfolk = "Merfolk";
	public const string Rogue = "Rogue";
	public const string Illusion = "Illusion";
	public const string Spirit = "Spirit";
	public const string Elemental = "Elemental";
	public const string Wall = "Wall";
	public const string Djinn = "Djinn";
	public const string Sphinx = "Sphinx";
	public const string Giant = "Giant";
	public const string Angel = "Angel";
	public const string Siren = "Siren";
	public const string Monk = "Monk";

	public static IReadOnlyList<Card> Cards { get; } =
		[
			// ===== ONE MANA =====

			// The graveyard threshold reuses ThresholdComponent, which is live-evaluated — it has
			// to be, since a graveyard changes with no battlefield event to re-stamp on.
			CardFactory
				.Creature("Jace's Phantasm", manaCost: 1, power: 1, toughness: 1)
				.WithSubtype(Illusion)
				.WithFlying()
				.WithComponent(
					new ThresholdComponent
					{
						Minimum = 10,
						PowerBonus = 4,
						ToughnessBonus = 4,
						Duration = ModifierDuration.Permanent,
					}
				)
				.Build(),
			// Flash is dropped — it needs a priority window, the same gap counterspells route
			// around. The mana sink is the half that matters.
			CardFactory
				.Creature("Spectral Sailor", manaCost: 1, power: 1, toughness: 1)
				.WithSubtype(Spirit)
				.WithFlying()
				.WithActivatedAbility(
					"Sailor's Tale",
					manaCost: 4,
					effect: eb => eb.WithDraw(1).WithTarget(TargetingStrategy.Self()),
					maxPerTurn: 0
				)
				.Build(),
			// ===== TWO MANA =====

			// Defender + "prevent all combat damage to and by it" = an unkillable Taunt wall,
			// which is unanswerable here. TauntUntilAttackedComponent makes it soak exactly one
			// attack per turn and then step aside. See the class header.
			CardFactory
				.Creature("Fog Bank", manaCost: 2, power: 0, toughness: 2)
				.WithSubtype(Wall)
				.WithFlying()
				.WithTaunt()
				.WithComponent(new TauntUntilAttackedComponent())
				.WithComponent(new PreventsCombatDamageComponent())
				.Build(),
			// Printed as "return target TAPPED creature an opponent controls". Gating on
			// exhaustion made it a blank card in play: creatures here only become exhausted from
			// a tapper, because attacking does not tap — so the clause that is live in real Magic
			// is almost never satisfiable in this engine. Bounces any opposing creature instead.
			CardFactory
				.Creature("Harbinger of the Tides", manaCost: 2, power: 2, toughness: 2)
				.WithSubtype(Merfolk)
				.WithSubtype(Wizard)
				.WithEtbTrigger(
					"Tidal Return",
					eb => eb.WithBounce().WithTarget(Random().OpponentCreatures())
				)
				.Build(),
			// The flip to Jace, Telepath Unbound is cut — creature to planeswalker crosses the
			// cast-routing split. The looter is the half that plays.
			CardFactory
				.Creature("Jace, Vryn's Prodigy", manaCost: 2, power: 0, toughness: 2)
				.WithSubtype(Human)
				.WithSubtype(Wizard)
				.WithActivatedAbility(
					"Loot",
					manaCost: 0,
					effect: eb =>
						eb.WithDraw(1).WithTarget(TargetingStrategy.Self()).WithDiscard(1),
					requiresTap: true
				)
				.Build(),
			CardFactory
				.Creature("Jeskai Elder", manaCost: 2, power: 1, toughness: 2)
				.WithSubtype(Human)
				.WithSubtype(Monk)
				.WithTriggeredAbility(
					"Prowess",
					TriggerConditions.OnYouCastSpell(),
					eb => eb.WithProwessBuff()
				)
				.WithTriggeredAbility(
					"Elder's Insight",
					TriggerConditions.OnSelfDealsCombatDamageToPlayer(),
					eb => eb.WithDraw(1).WithTarget(TargetingStrategy.Self()).WithDiscard(1)
				)
				.Build(),
			CardFactory
				.Creature("Merfolk Looter", manaCost: 2, power: 1, toughness: 1)
				.WithSubtype(Merfolk)
				.WithSubtype(Rogue)
				.WithActivatedAbility(
					"Loot",
					manaCost: 0,
					effect: eb =>
						eb.WithDraw(1).WithTarget(TargetingStrategy.Self()).WithDiscard(1),
					requiresTap: true
				)
				.Build(),
			CardFactory
				.Creature("Mystic Archaeologist", manaCost: 2, power: 2, toughness: 1)
				.WithSubtype(Human)
				.WithSubtype(Wizard)
				.WithActivatedAbility(
					"Dig Deep",
					manaCost: 5,
					effect: eb => eb.WithDraw(2).WithTarget(TargetingStrategy.Self()),
					maxPerTurn: 0
				)
				.Build(),
			// Copies the biggest creature on the board. Base 0/0, so with an empty board it dies
			// to the zero-toughness rule — exactly as the printed card does.
			CardFactory
				.Creature("Phantasmal Image", manaCost: 2, power: 0, toughness: 0)
				.WithSubtype(Illusion)
				.WithComponent(new CopyOnEnterComponent { RemainsIllusion = true })
				.Build(),
			// ===== THREE MANA =====

			CardFactory
				.Creature("Aether Adept", manaCost: 3, power: 2, toughness: 2)
				.WithSubtype(Human)
				.WithSubtype(Wizard)
				.WithEtbTrigger(
					"Displace",
					eb => eb.WithBounce().WithTarget(Single().OpponentCreatures())
				)
				.Build(),
			CardFactory
				.Creature("Barrin, Tolarian Archmage", manaCost: 3, power: 2, toughness: 2)
				.WithSubtype(Human)
				.WithSubtype(Wizard)
				.WithEtbTrigger(
					"Recall",
					eb =>
						eb.WithBounce()
							.WithTarget(
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
				)
				.Build(),
			// "X = the number of +1/+1 counters" is read as effective power instead. A permanent
			// modifier IS the counter here, and Skulker's power is always 1 + counters, so the
			// number is identical with no counter subsystem.
			CardFactory
				.Creature("Chasm Skulker", manaCost: 3, power: 1, toughness: 1)
				.WithSubtype("Squid")
				.WithSubtype("Horror")
				.WithTriggeredAbility(
					"Ink Swell",
					TriggerConditions.OnYouDraw(),
					eb => eb.WithSelfBuff(1, 1)
				)
				.WithDeathTrigger(
					"Ink Cloud",
					eb =>
						eb.WithAction(
							new CreateTokensPerPowerAction
							{
								CardTemplate = CoresetCubeBlueTokens.Squid(),
								Offset = -1,
							},
							TargetingStrategy.NoTarget()
						)
				)
				.Build(),
			CardFactory
				.Creature("Cloudkin Seer", manaCost: 3, power: 2, toughness: 1)
				.WithSubtype(Elemental)
				.WithSubtype(Wizard)
				.WithFlying()
				.WithEtbTrigger(
					"Insight",
					eb => eb.WithDraw(1).WithTarget(TargetingStrategy.Self())
				)
				.Build(),
			CardFactory
				.Creature("Frost Lynx", manaCost: 3, power: 2, toughness: 2)
				.WithSubtype(Elemental)
				.WithSubtype("Cat")
				.WithEtbTrigger("Frostbite", eb => eb.WithFreeze(1))
				.Build(),
			CardFactory
				.Creature("Illusory Angel", manaCost: 3, power: 4, toughness: 4)
				.WithSubtype(Angel)
				.WithSubtype(Illusion)
				.WithFlying()
				.WithCastRestriction(new RequiresSpellsCastThisTurnRestriction { Minimum = 1 })
				.Build(),
			// "Can't be blocked" is blank; Jhessian Thief keeps prowess and the damage trigger,
			// which is what the card is played for.
			CardFactory
				.Creature("Jhessian Thief", manaCost: 3, power: 1, toughness: 3)
				.WithSubtype(Human)
				.WithSubtype(Rogue)
				.WithTriggeredAbility(
					"Prowess",
					TriggerConditions.OnYouCastSpell(),
					eb => eb.WithProwessBuff()
				)
				.WithTriggeredAbility(
					"Pickpocket",
					TriggerConditions.OnSelfDealsCombatDamageToPlayer(),
					eb => eb.WithDraw(1).WithTarget(TargetingStrategy.Self())
				)
				.Build(),
			CardFactory
				.Creature("Mistral Singer", manaCost: 3, power: 2, toughness: 2)
				.WithSubtype(Siren)
				.WithFlying()
				.WithTriggeredAbility(
					"Prowess",
					TriggerConditions.OnYouCastSpell(),
					eb => eb.WithProwessBuff()
				)
				.Build(),
			// A 0/7 Taunt wall is a genuinely strong roadblock here. Unlike Fog Bank it can be
			// killed by attacking into it, so it keeps Taunt permanently.
			CardFactory
				.Creature("Wall of Frost", manaCost: 3, power: 0, toughness: 7)
				.WithSubtype(Wall)
				.WithTaunt()
				// "Whenever Wall of Frost blocks a creature, that creature doesn't untap during
				// its controller's next untap step." Blocking is Taunt here, so it becomes
				// "whenever a creature attacks this". It previously listened for ANY attack and
				// froze EVERY creature an opponent controlled — a three-mana one-sided Frost
				// Breath every single turn, triggered by attacks it had nothing to do with.
				.WithTriggeredAbility(
					"Numbing Cold",
					new AttackedThisCardCondition(),
					eb =>
						eb.WithAction(
							new ExhaustCreatureAction
							{
								FreezeTurns = 1,
								TargetContextKey = ContextKeys.TriggerSubjectId,
							},
							TargetingStrategy.NoTarget()
						)
				)
				.Build(),
			// ===== FOUR MANA =====

			CardFactory
				.Creature("Clone", manaCost: 4, power: 0, toughness: 0)
				.WithSubtype("Shapeshifter")
				.WithComponent(new CopyOnEnterComponent())
				.Build(),
			// "For as long as you control this" is a source-linked freeze: killing the Geists
			// releases the creature, which is what makes the card answerable.
			CardFactory
				.Creature("Dungeon Geists", manaCost: 4, power: 3, toughness: 3)
				.WithSubtype(Spirit)
				.WithFlying()
				.WithEtbTrigger("Imprison", eb => eb.WithFreeze(1, whileSourceRemains: true))
				.Build(),
			CardFactory
				.Creature("Talrand, Sky Summoner", manaCost: 4, power: 2, toughness: 2)
				.WithSubtype(Merfolk)
				.WithSubtype(Wizard)
				.WithTriggeredAbility(
					"Summon Drake",
					TriggerConditions.OnYouCastSpell(),
					eb => eb.WithCreateTokens(CoresetCubeBlueTokens.Drake())
				)
				.Build(),
			// The unblockable ability is blank, so the Thopters are the whole card.
			CardFactory
				.Creature("Whirler Rogue", manaCost: 4, power: 2, toughness: 2)
				.WithSubtype(Human)
				.WithSubtype(Rogue)
				.WithEtbTrigger(
					"Thopter Escort",
					eb => eb.WithCreateTokens(CoresetCubeBlueTokens.Thopter(), count: 2)
				)
				.Build(),
			// ===== FIVE MANA AND UP =====

			CardFactory
				.Creature("Cavalier of Gales", manaCost: 5, power: 5, toughness: 5)
				.WithSubtype(Elemental)
				.WithSubtype("Knight")
				.WithFlying()
				.WithEtbTrigger(
					"Gale Insight",
					eb => eb.WithDraw(3).WithTarget(TargetingStrategy.Self()).WithDiscard(2)
				)
				.WithDeathTrigger("Return to the Winds", eb => eb.WithScry(2))
				.Build(),
			CardFactory
				.Creature("Soulblade Djinn", manaCost: 5, power: 4, toughness: 3)
				.WithSubtype(Djinn)
				.WithFlying()
				.WithTriggeredAbility(
					"Shared Prowess",
					TriggerConditions.OnYouCastSpell(),
					eb => eb.WithBoost(1, 1).WithTarget(AllValid().AllYourCreatures())
				)
				.Build(),
			CardFactory
				.Creature("Stormwing Entity", manaCost: 5, power: 3, toughness: 3)
				.WithSubtype(Elemental)
				.WithFlying()
				.WithCostReduction(3, new CastSpellThisTurnCondition())
				.WithTriggeredAbility(
					"Prowess",
					TriggerConditions.OnYouCastSpell(),
					eb => eb.WithProwessBuff()
				)
				.WithEtbTrigger("Storm Sight", eb => eb.WithScry(2))
				.Build(),
			// The ward ("counter that spell unless {2}") is cut: targeting is not interceptable,
			// and hand traps are a different mechanism. The freeze half is the body of the card.
			CardFactory
				.Creature("Frost Titan", manaCost: 6, power: 6, toughness: 6)
				.WithSubtype(Giant)
				.WithEtbTrigger("Deep Freeze", eb => eb.WithFreeze(1))
				.WithTriggeredAbility(
					"Glacial Advance",
					TriggerConditions.OnSelfAttacks(),
					eb => eb.WithFreeze(1)
				)
				.Build(),
			CardFactory
				.Creature("Agent of Treachery", manaCost: 7, power: 2, toughness: 3)
				.WithSubtype(Human)
				.WithSubtype(Rogue)
				.WithEtbTrigger("Treachery", eb => eb.WithGainControl())
				.WithTriggeredAbility(
					"Spoils of Betrayal",
					new EventTriggerCondition { EventTypeName = EventTypeNames.TurnEnded },
					eb => eb.WithDraw(3).WithTarget(TargetingStrategy.Self()),
					maxPerTurn: 1
				)
				.Build(),
			// "An opponent separates them into two piles" cannot be expressed — the choice system
			// has no opponent-made partition. Drawing two of five keeps the shape and the rate.
			CardFactory
				.Creature("Sphinx of Uthuun", manaCost: 7, power: 5, toughness: 6)
				.WithSubtype(Sphinx)
				.WithFlying()
				.WithEtbTrigger("Fateful Vision", eb => eb.WithDig(5).WithDig(5))
				.Build(),
		];
}
