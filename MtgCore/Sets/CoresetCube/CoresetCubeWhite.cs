using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore.Cards.Builders;
using static MtgCore.Cards.Builders.TargetBuilder;

namespace MtgCore;

/// <summary>
/// White cards from the Core Set Cube — https://cubecobra.com/cube/list/magiccoreset20xx
///
/// All 38 white creatures, in the cube's own order (by mana value, then name).
///
/// Two engine facts drive every rate, exactly as in Hollowmere:
///   - Combat has no blockers, so a body alone is only a clock. First strike is unusually strong
///     here — an attack into a creature it can kill is a free trade — and is priced accordingly.
///   - Draft.BuildDeck takes the first 27 picks in PICK ORDER, not the best 27 by curve, so
///     expensive cards are a structural liability and must earn their slot.
///
/// DIVERGENCES FROM PRINTED CARDS. Every ability is implemented except where the engine has no
/// such concept at all. Each is commented on the card itself; the complete list is:
///   - VIGILANCE (8 cards) is unimplemented and deliberately deferred — with no blocking and a
///     one-attack-per-turn rule it has nothing to do, and how to reskin it is an open design
///     question. Cards printed with it simply lack it; adding it later is a one-line change.
///   - Kytheon's flip to a planeswalker: no planeswalkers yet.
///   - Grand Abolisher's "opponents can't cast during your turn": no priority system, so
///     opponents never act on your turn and the text is inert.
///   - Odric's "you choose which creatures block": there is no blocking.
///   - Protection from a COLOUR (Knight of Glory): this engine has no colours. Protection from a
///     creature TYPE is implemented and Baneslayer Angel keeps hers.
/// </summary>
public static class CoresetCubeWhite
{
	// Creature types, shared so a rename is one edit and a typo cannot silently break a lord.
	public const string Human = "Human";
	public const string Soldier = "Soldier";
	public const string Cleric = "Cleric";
	public const string Knight = "Knight";
	public const string Angel = "Angel";
	public const string Spirit = "Spirit";
	public const string Pegasus = "Pegasus";
	public const string Cat = "Cat";
	public const string Warrior = "Warrior";
	public const string Scout = "Scout";
	public const string Noble = "Noble";
	public const string Giant = "Giant";

	public static IReadOnlyList<Card> Cards { get; } =
		[
			// ===== ONE MANA =====

			// {T}: target ATTACKING creature gets +1/+1. RequiresTap makes the tap a real cost:
			// it exhausts, so this is once per turn and the body cannot also attack.
			CardFactory
				.Creature("Anointer of Champions", manaCost: 1, power: 1, toughness: 1)
				.WithSubtype(Human)
				.WithSubtype(Cleric)
				.WithActivatedAbility(
					"Anoint",
					manaCost: 0,
					effect: eb => eb.WithBoost(1, 1).WithTarget(Single().YourCreatures()),
					requiresTap: true
				)
				.Build(),
			// The archetypal tapper. {W}, {T}: exhaust target creature — which costs its
			// controller exactly one attack, since exhaustion clears on their own turn.
			CardFactory
				.Creature("Gideon's Lawkeeper", manaCost: 1, power: 1, toughness: 1)
				.WithSubtype(Human)
				.WithSubtype(Soldier)
				.WithActivatedAbility(
					"Lawkeep",
					manaCost: 1,
					effect: eb => eb.WithExhaust(),
					requiresTap: true
				)
				.Build(),
			// Flip side (Gideon, Battle-Forged) omitted — no planeswalkers. The indestructible
			// ability is the half that survives, and it is the half that matters in combat.
			CardFactory
				.Creature("Kytheon, Hero of Akros", manaCost: 1, power: 2, toughness: 1)
				.WithSubtype(Human)
				.WithSubtype(Soldier)
				.WithActivatedAbility(
					"Unbreakable",
					manaCost: 2,
					effect: eb =>
						eb.WithGrantKeyword(indestructible: true)
							.WithTarget(Single().YourCreatures())
				)
				.Build(),
			// The card that proved life-gain triggers had never fired — see A1 in the plan.
			CardFactory
				.Creature("Soul Warden", manaCost: 1, power: 1, toughness: 1)
				.WithSubtype(Human)
				.WithSubtype(Cleric)
				.WithTriggeredAbility(
					"Soul Tithe",
					TriggerConditions.OnAnotherCreatureEnters(),
					eb => eb.WithLifeGain(1).WithTarget(TargetingStrategy.Self())
				)
				.Build(),
			// Vigilance dropped. The life gate is real: LifeAboveStartingCondition reads
			// StartingLife rather than assuming 20.
			CardFactory
				.Creature("Speaker of the Heavens", manaCost: 1, power: 1, toughness: 1)
				.WithSubtype(Human)
				.WithSubtype(Cleric)
				.WithLifelink()
				// The textbook Cover card: a 1/1 whose entire value is a tap ability it has to
				// survive to use, gated behind a life total it needs turns to reach. Without Cover
				// the only way to make it live is to print it as a 1/3, which is the over-statting
				// Cover exists to avoid.
				.WithCover(1)
				.WithActivatedAbility(
					"Call the Host",
					manaCost: 0,
					effect: eb => eb.WithCreateTokens(CoresetCubeTokens.Angel()),
					condition: new LifeAboveStartingCondition { Amount = 7 },
					requiresTap: true
				)
				.Build(),
			// ===== TWO MANA =====

			// "Put a +1/+1 counter" is a permanent P/T modifier — that IS the counter here.
			CardFactory
				.Creature("Ajani's Pridemate", manaCost: 2, power: 2, toughness: 2)
				.WithSubtype(Cat)
				.WithSubtype(Soldier)
				.WithTriggeredAbility(
					"Pride",
					TriggerConditions.OnGainLife(),
					eb => eb.WithSelfBuff(1, 1)
				)
				.Build(),
			CardFactory
				.Creature("Fencing Ace", manaCost: 2, power: 1, toughness: 1)
				.WithSubtype(Human)
				.WithSubtype(Soldier)
				.WithDoubleStrike()
				.Build(),
			// Printed text is inert here: with no priority window opponents never act on your
			// turn. Reskinned to the protective role the card plays — it shields your board.
			CardFactory
				.Creature("Grand Abolisher", manaCost: 2, power: 2, toughness: 2)
				.WithSubtype(Human)
				.WithSubtype(Cleric)
				.WithComponent(
					new StaticGrantKeywordAbility
					{
						GrantsHexproof = true,
						Filter = TargetSpecification.OtherCreaturesYouControl(),
					}
				)
				.Build(),
			// "Creatures your opponents control enter tapped" — an ETB trigger that exhausts the
			// entering creature, so it cannot attack on its controller's next turn.
			CardFactory
				.Creature("Imposing Sovereign", manaCost: 2, power: 2, toughness: 1)
				.WithSubtype(Human)
				.WithSubtype(Noble)
				.WithTriggeredAbility(
					"Impose",
					new EventTriggerCondition
					{
						EventTypeName = EventTypeNames.CreatureEnteredBattlefield,
						Filter = new IsControlledByOpponentSpecification(),
					},
					eb => eb.WithExhaust().WithTarget(AllValid().OpponentCreatures())
				)
				.Build(),
			// Protection from black is impossible (no colours). First strike and exalted are both
			// real, and exalted is the half that actually shapes how you attack.
			CardFactory
				.Creature("Knight of Glory", manaCost: 2, power: 2, toughness: 1)
				.WithSubtype(Human)
				.WithSubtype(Knight)
				.WithFirstStrike()
				.WithExalted()
				.Build(),
			// "Search for a Plains and put it onto the battlefield" = +1 permanent mana: lands
			// here are consumed into MaxMana, so there is no Plains permanent to fetch.
			CardFactory
				.Creature("Knight of the White Orchid", manaCost: 2, power: 2, toughness: 2)
				.WithSubtype(Human)
				.WithSubtype(Knight)
				.WithFirstStrike()
				.WithTriggeredAbility(
					"White Orchid",
					new AndTriggerCondition
					{
						Conditions = ImmutableList.Create<TriggerCondition>(
							TriggerConditions.OnSelfEntersBattlefield(),
							new OpponentControlsMoreLandsCondition()
						),
					},
					eb =>
						eb.WithAction(
							new GainPermanentManaAction
							{
								Amount = 1,
								TargetContextKey = ContextKeys.CastingPlayerId,
							},
							TargetingStrategy.NoTarget()
						)
				)
				.Build(),
			// Discard a card,  gain indestructible.
			CardFactory
				.Creature("Seasoned Hallowblade", manaCost: 2, power: 3, toughness: 1)
				.WithSubtype(Human)
				.WithSubtype(Warrior)
				.WithActivatedAbility(
					"Hallowed Guard",
					manaCost: 0,
					effect: eb =>
						eb.WithGrantKeyword(indestructible: true)
							.WithTarget(Single().YourCreatures()),
					costs: c => c.Discard(1),
					maxPerTurn: 0
				)
				.Build(),
			// Vigilance dropped. The turn restriction is implemented — see the round-vs-turn note
			// on MinimumRoundCastRestriction.
			CardFactory
				.Creature("Serra Avenger", manaCost: 2, power: 3, toughness: 3)
				.WithSubtype(Angel)
				.WithFlying()
				.WithCastRestriction(new MinimumRoundCastRestriction { MinimumRound = 4 })
				.Build(),
			CardFactory
				.Creature("Stormfront Pegasus", manaCost: 2, power: 2, toughness: 1)
				.WithSubtype(Pegasus)
				.WithFlying()
				.Build(),
			// Vigilance dropped. Renown 1 is real: MaxTriggers = 1 is a LIFETIME cap, which is
			// what "if it isn't renowned" means — a per-turn cap would make it grow every turn.
			CardFactory
				.Creature("Topan Freeblade", manaCost: 2, power: 2, toughness: 2)
				.WithSubtype(Human)
				.WithSubtype(Soldier)
				.WithRenown(1)
				.Build(),
			// ===== THREE MANA =====

			// The life-gain clause is a REPLACEMENT effect, not a trigger. As a trigger it would
			// gain life in response to gaining life, forever.
			CardFactory
				.Creature("Angel of Vitality", manaCost: 3, power: 2, toughness: 2)
				.WithSubtype(Angel)
				.WithFlying()
				.WithLifeGainBonus(1)
				.WithLifeTotalBonus(2, 2, minimum: 25)
				.Build(),
			CardFactory
				.Creature("Attended Knight", manaCost: 3, power: 2, toughness: 2)
				.WithSubtype(Human)
				.WithSubtype(Knight)
				.WithFirstStrike()
				.WithEtbTrigger("Attendant", eb => eb.WithCreateTokens(CoresetCubeTokens.Soldier()))
				.Build(),
			// Printed */*. Power and toughness are ints here, so it is base 0/0 plus a
			// live-evaluated CreatureCountComponent — effective stats read correctly.
			CardFactory
				.Creature("Crusader of Odric", manaCost: 3, power: 0, toughness: 0)
				.WithSubtype(Human)
				.WithSubtype(Soldier)
				.WithPowerEqualToCreatureCount()
				.Build(),
			// "Becomes tapped" is "becomes exhausted" — the payoff half of the tapper theme.
			CardFactory
				.Creature("Gideon's Avenger", manaCost: 3, power: 2, toughness: 2)
				.WithSubtype(Human)
				.WithSubtype(Soldier)
				.WithTriggeredAbility(
					"Avenge",
					TriggerConditions.OnCreatureExhausted(),
					eb => eb.WithSelfBuff(1, 1)
				)
				.Build(),
			CardFactory
				.Creature("Hanged Executioner", manaCost: 3, power: 1, toughness: 1)
				.WithSubtype(Spirit)
				.WithFlying()
				.WithEtbTrigger(
					"Second Noose",
					eb => eb.WithCreateTokens(CoresetCubeTokens.Spirit())
				)
				.WithActivatedAbility(
					"Hang",
					manaCost: 4,
					effect: eb => eb.WithExile().WithTarget(Single().Creatures()),
					costs: c => c.SacrificeSelf()
				)
				.Build(),
			// Power 4 or greater is checked on EFFECTIVE power, so a pumped creature is a legal
			// target and a shrunk one is not.
			CardFactory
				.Creature("Intrepid Hero", manaCost: 3, power: 1, toughness: 1)
				.WithSubtype(Human)
				.WithSubtype(Soldier)
				.WithActivatedAbility(
					"Single Out",
					manaCost: 0,
					effect: eb =>
						eb.WithDestroy()
							.WithTarget(
								Single()
									.WithSpec(
										new PowerAtLeastSpecification { Minimum = 4 }.And(
											new IsControlledByOpponentSpecification()
										)
									)
							),
					requiresTap: true
				)
				.Build(),
			CardFactory
				.Creature("Master of Diversion", manaCost: 3, power: 2, toughness: 2)
				.WithSubtype(Human)
				.WithSubtype(Scout)
				.WithTriggeredAbility(
					"Divert",
					TriggerConditions.OnSelfAttacks(),
					eb => eb.WithExhaust()
				)
				.Build(),
			CardFactory
				.Creature("Pegasus Courser", manaCost: 3, power: 1, toughness: 3)
				.WithSubtype(Pegasus)
				.WithFlying()
				.WithTriggeredAbility(
					"Aerial Escort",
					TriggerConditions.OnSelfAttacks(),
					eb =>
						eb.WithGrantKeyword(flying: true)
							.WithTarget(Single().OtherCreaturesYouControl())
				)
				.Build(),
			// The 4/4 Angel token loses vigilance with everything else. LifeGainedThisTurn is
			// tracked on MtgPlayer; the trigger fires at end of turn, capped once per turn.
			CardFactory
				.Creature("Resplendent Angel", manaCost: 3, power: 3, toughness: 3)
				.WithSubtype(Angel)
				.WithFlying()
				.WithTriggeredAbility(
					"Resplendent Host",
					new LifeGainedThisTurnCondition { Minimum = 5 },
					eb => eb.WithCreateTokens(CoresetCubeTokens.Angel()),
					maxPerTurn: 1
				)
				.WithActivatedAbility(
					"Radiance",
					manaCost: 5,
					effect: eb => eb.WithBoost(2, 2).WithTarget(Single().YourCreatures())
				)
				.Build(),
			// Vigilance dropped; the death trigger is the real card.
			CardFactory
				.Creature("Steadfast Sentry", manaCost: 3, power: 3, toughness: 2)
				.WithSubtype(Human)
				.WithSubtype(Soldier)
				// Vigilance -> Taunt, the swap this whole batch is built on: vigilance is
				// unimplemented and blank, Taunt is the keyword that actually means "defends".
				.WithTaunt()
				.WithDeathTrigger(
					"Last Stand",
					eb =>
						eb.WithAction(
							new AddModifierAction
							{
								PowerBonus = 1,
								ToughnessBonus = 1,
								Duration = ModifierDuration.Permanent,
							},
							TargetingStrategy.SingleTarget(
								TargetSpecification.OtherCreaturesYouControl()
							)
						)
				)
				.Build(),
			CardFactory
				.Creature("Veteran Swordsmith", manaCost: 3, power: 3, toughness: 2)
				.WithSubtype(Human)
				.WithSubtype(Soldier)
				.WithComponent(
					new StaticPTBoostAbility
					{
						PowerBonus = 1,
						ToughnessBonus = 0,
						Filter = new IsSubtypeSpecification { Subtype = Soldier }.And(
							TargetSpecification.OtherCreaturesYouControl()
						),
					}
				)
				.Build(),
			// The tax hits both players, exactly as printed.
			CardFactory
				.Creature("Vryn Wingmare", manaCost: 2, power: 2, toughness: 1)
				.WithSubtype(Pegasus)
				.WithFlying()
				.WithSpellTax(1)
				.Build(),
			// ===== FOUR MANA =====

			CardFactory
				.Creature("Basri's Acolyte", manaCost: 4, power: 2, toughness: 3)
				.WithSubtype(Cat)
				.WithSubtype(Cleric)
				.WithLifelink()
				.WithEtbTrigger(
					"Blessing",
					eb =>
						eb.WithAction(
							new AddModifierAction
							{
								PowerBonus = 1,
								ToughnessBonus = 1,
								Duration = ModifierDuration.Permanent,
							},
							TargetingStrategy.SingleTarget(
								TargetSpecification.OtherCreaturesYouControl()
							)
						)
				)
				.Build(),
			// Vigilance and protection from multicolored both dropped (no colours). "If it had a
			// +1/+1 counter on it" is asked as "does it carry a permanent P/T bonus" — which is
			// exactly what a counter is here.
			CardFactory
				.Creature("Basri's Lieutenant", manaCost: 4, power: 3, toughness: 4)
				.WithSubtype(Human)
				.WithSubtype(Knight)
				// Vigilance -> Taunt. Four toughness is the statline that wants it, and the card
				// already protects the rest of the board on ETB.
				.WithTaunt()
				.WithEtbTrigger(
					"Knight's Charge",
					eb =>
						eb.WithAction(
							new AddModifierAction
							{
								PowerBonus = 1,
								ToughnessBonus = 1,
								Duration = ModifierDuration.Permanent,
							},
							TargetingStrategy.SingleTarget(
								TargetSpecification.OtherCreaturesYouControl()
							)
						)
				)
				// "Whenever this creature OR ANOTHER creature you control dies" is two triggers,
				// because the two halves need different ActiveInZone values: by the time the
				// Lieutenant's own death is scanned it has already moved to the graveyard, so a
				// Battlefield-scoped trigger would never see it. Neither is capped — the printed
				// card has no limit.
				.WithTriggeredAbility(
					"Fallen Champion",
					new EventTriggerCondition
					{
						EventTypeName = EventTypeNames.CreatureDestroyed,
						Filter = new IsControlledByYouSpecification()
							.And(new IsNotSelfSpecification())
							.And(new HasPermanentPowerBonusSpecification()),
					},
					eb => eb.WithCreateTokens(CoresetCubeTokens.Knight())
				)
				.WithTriggeredAbility(
					"Fallen Champion (Self)",
					new EventTriggerCondition
					{
						EventTypeName = EventTypeNames.CreatureDestroyed,
						Filter = new IsSourceCardSpecification().And(
							new HasPermanentPowerBonusSpecification()
						),
					},
					eb => eb.WithCreateTokens(CoresetCubeTokens.Knight()),
					ActiveInZone: ZoneType.Graveyard
				)
				.Build(),
			CardFactory
				.Creature("Gallant Cavalry", manaCost: 4, power: 2, toughness: 2)
				.WithSubtype(Human)
				.WithSubtype(Knight)
				.WithEtbTrigger(
					"Cavalry Charge",
					eb => eb.WithCreateTokens(CoresetCubeTokens.Knight())
				)
				.Build(),
			// Renown 2, once ever. The {W}: tap ability is the second half.
			CardFactory
				.Creature("Kytheon's Irregulars", manaCost: 4, power: 4, toughness: 3)
				.WithSubtype(Human)
				.WithSubtype(Soldier)
				.WithRenown(1)
				.WithActivatedAbility("Rough Justice", manaCost: 2, effect: eb => eb.WithExhaust())
				.Build(),
			// "You choose which creatures block" is blank — there is no blocking. First strike is
			// kept and the attack trigger becomes a squad buff, which is the same role.
			CardFactory
				.Creature("Odric, Master Tactician", manaCost: 4, power: 3, toughness: 4)
				.WithSubtype(Human)
				.WithSubtype(Soldier)
				.WithFirstStrike()
				.WithTriggeredAbility(
					"Master Tactics",
					TriggerConditions.OnSelfAttacks(),
					eb => eb.WithBoost(2, 0).WithTarget(Single().OtherCreaturesYouControl())
				)
				.Build(),
			// Grants exalted to the rest of the team via the static-ability push model, so
			// instances stack and a lone attacker gets +1/+1 for each.
			CardFactory
				.Creature("Sublime Archangel", manaCost: 4, power: 4, toughness: 3)
				.WithSubtype(Angel)
				.WithFlying()
				.WithExalted()
				.WithComponent(
					new StaticGrantKeywordAbility
					{
						GrantsExalted = true,
						Filter = TargetSpecification.OtherCreaturesYouControl(),
					}
				)
				.Build(),
			// ===== FIVE MANA AND UP =====

			// Life gain triggers now actually fire, which is the whole card.
			CardFactory
				.Creature("Archangel of Thune", manaCost: 5, power: 3, toughness: 4)
				.WithSubtype(Angel)
				.WithFlying()
				.WithLifelink()
				.WithTriggeredAbility(
					"Thune's Blessing",
					TriggerConditions.OnGainLife(),
					eb =>
						eb.WithAction(
							new AddModifierAction
							{
								PowerBonus = 1,
								ToughnessBonus = 1,
								Duration = ModifierDuration.Permanent,
							},
							TargetingStrategy.AllValid(
								TargetSpecification.CreatureControlledByYou()
							)
						)
				)
				.Build(),
			// Protection from Demons and Dragons is real — subtype protection is implementable
			// even though colour protection is not.
			CardFactory
				.Creature("Baneslayer Angel", manaCost: 5, power: 5, toughness: 5)
				.WithSubtype(Angel)
				.WithFlying()
				.WithFirstStrike()
				.WithLifelink()
				.WithProtectionFrom("Demon", "Dragon")
				.Build(),
			// Vigilance dropped; the anthem and the three bodies are the card.
			CardFactory
				.Creature("Captain of the Watch", manaCost: 6, power: 3, toughness: 3)
				.WithSubtype(Human)
				.WithSubtype(Soldier)
				// Vigilance -> Taunt. Printed, the captain grants vigilance to Soldiers; that half
				// is unreachable, so the Watch at least stands guard itself. A 3/3 body at six mana
				// needs the help.
				.WithTaunt()
				.WithComponent(
					new StaticPTBoostAbility
					{
						PowerBonus = 1,
						ToughnessBonus = 1,
						Filter = new IsSubtypeSpecification { Subtype = Soldier }.And(
							TargetSpecification.OtherCreaturesYouControl()
						),
					}
				)
				.WithEtbTrigger(
					"Muster the Watch",
					eb => eb.WithCreateTokens(CoresetCubeTokens.Soldier(), count: 3)
				)
				.Build(),
			// The power comparison on the sacrifice ability is implemented — a pumped Lena
			// protects more of the board.
			//
			// "For each NONTOKEN creature you control" counts every creature here: there is no
			// token marker on Card, so token-ness is unrepresentable. CreaturesOnly at least
			// stops artifacts and enchantments inflating the count.
			CardFactory
				.Creature("Lena, Selfless Champion", manaCost: 5, power: 3, toughness: 3)
				.WithSubtype(Human)
				.WithSubtype(Knight)
				.WithEtbTrigger(
					"Rally",
					eb =>
						eb.WithCreateTokensPerCard(
							CoresetCubeTokens.Soldier(),
							subtype: "",
							zone: ZoneType.Battlefield,
							creaturesOnly: true
						)
				)
				.WithActivatedAbility(
					"Selfless Sacrifice",
					manaCost: 0,
					effect: eb =>
						eb.WithGrantKeyword(indestructible: true)
							.WithTarget(
								AllValid()
									.WithSpec(
										TargetSpecification
											.CreatureControlledByYou()
											.And(new PowerLessThanSourceSpecification())
									)
							),
					costs: c => c.SacrificeSelf()
				)
				.Build(),
			// Vigilance dropped. "Return target permanent card with mana value 3 or less" uses
			// HasManaCostAtMostSpecification.
			CardFactory
				.Creature("Sun Titan", manaCost: 6, power: 6, toughness: 6)
				.WithSubtype(Giant)
				.WithTriggeredAbility(
					"Sunrise",
					TriggerConditions.OnSelfEntersBattlefield(),
					eb =>
						eb.WithReanimate()
							.WithTarget(
								Single()
									.WithSpec(
										new IsCreatureInOwnGraveyardSpecification().And(
											new HasManaCostAtMostSpecification { Maximum = 3 }
										)
									)
							)
				)
				.WithTriggeredAbility(
					"Sunrise (Attack)",
					TriggerConditions.OnSelfAttacks(),
					eb =>
						eb.WithReanimate()
							.WithTarget(
								Single()
									.WithSpec(
										new IsCreatureInOwnGraveyardSpecification().And(
											new HasManaCostAtMostSpecification { Maximum = 3 }
										)
									)
							)
				)
				.Build(),
		];
}
