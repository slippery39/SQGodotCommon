using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore.Cards.Builders;
using static MtgCore.Cards.Builders.TargetBuilder;

namespace MtgCore;

/// <summary>
/// Red creatures from the Core Set Cube — https://cubecobra.com/cube/list/magiccoreset20xx
///
/// All 38, in the cube's own order (by mana value, then name).
///
/// GOBLINS ARE THE COLOUR'S ENGINE. Fourteen of these are Goblins and seven cards make Goblin
/// tokens, so the tribal payoffs (Goblin Chieftain, Goblin Piledriver, Volley Veteran,
/// Siege-Gang Commander, Arms Dealer) are load-bearing rather than decorative. Note the deliberate
/// tension with the simulator: many identical tokens are exactly the case AttackerSignature
/// deduplication exists for — see the "Combat System" note in MtgCore/CLAUDE.md.
///
/// DAMAGE TO PLANESWALKERS DID NOT WORK before this section, in two independent and silent ways —
/// DealDamageAction had no planeswalker arm, and no targeting specification could name a walker.
/// Red is built out of "any target" burn more than any other colour here, so it surfaced at once.
/// Fixed in DealDamageAction and TargetSpecification.PlayersOrCreatures; see MtgCore/CLAUDE.md.
///
/// DIVIDED DAMAGE IS SPRAYED. "Deals N damage divided as you choose among any number of targets"
/// has no home in a targeting system that is either single-target or all-valid, so those cards
/// fire N independent 1-damage effects at random targets — Arcane Missiles — and cost one less
/// than printed to pay for the loss of aim. Three of the six affected cards are creatures
/// (Thundermaw Hellkite, Inferno Titan, Drakuseth); see DesignNotes.md for when to revisit it.
///
/// DIVERGENCES FROM PRINTED CARDS. Every ability is implemented except where the engine has no
/// such concept at all. Each is commented on the card itself; the complete list is:
///   - NO BLOCKING, so MENACE, "can't block" and "can't be blocked" are all inert. Boggart Brute
///     and Goblin Glory Chaser lose the clause outright; Frenzied Goblin's whole ability was
///     "target creature can't block", so it is reskinned. This follows blue and black, which cut
///     islandwalk and menace for the same reason.
///   - NO FORCED ATTACKS. "Attacks each turn if able" (Borderland Marauder, Goblin Rabblemaster)
///     has nothing to hook into — the engine never compels an attack. Dropping it only ever helps
///     the player, which is the same call vigilance got.
///   - NO COLOURS, so Goblin Piledriver's protection from blue is unexpressible (protection from
///     a creature TYPE is implemented; from a colour is not).
///   - Chandra, Fire of Kaladesh keeps her damage trigger but not her flip to a planeswalker —
///     that crosses the creature/permanent cast-routing split, the same cut Kytheon, Jace and
///     Liliana, Heretical Healer all took.
///   - "Attacking Goblins" become "Goblins you control" (Piledriver, Rabblemaster). Attacking is a
///     fleeting state here — one attack per turn, resolving immediately — so counting attackers
///     mid-combat is not a question the board can answer. Costed down accordingly, since not
///     having to commit the attack is a real upgrade.
///   - VIGILANCE remains deliberately unimplemented engine-wide; nothing red is printed with it.
/// </summary>
public static class CoresetCubeRed
{
	public const string Goblin = "Goblin";
	public const string Human = "Human";
	public const string Warrior = "Warrior";
	public const string Shaman = "Shaman";
	public const string Berserker = "Berserker";
	public const string Wizard = "Wizard";
	public const string Rogue = "Rogue";
	public const string Monk = "Monk";
	public const string Elemental = "Elemental";
	public const string Lizard = "Lizard";
	public const string Phoenix = "Phoenix";
	public const string Minotaur = "Minotaur";
	public const string Pirate = "Pirate";
	public const string Weird = "Weird";
	public const string Dragon = "Dragon";
	public const string Ogre = "Ogre";
	public const string Artificer = "Artificer";
	public const string Giant = "Giant";
	public const string Avatar = "Avatar";

	public static IReadOnlyList<Card> Cards { get; } =
		[
			// ===== ONE MANA =====

			// Printed: "Whenever this attacks, target creature can't block this turn." With no
			// blocking that is a blank line on a 1/1 for one mana. Reskinned to the same INTENT —
			// clearing a path — via Taunt suppression, which is this engine's only "your creature
			// cannot stop mine" lever: exhausting a blocker takes it out of the way for a turn.
			CardFactory
				.Creature("Frenzied Goblin", manaCost: 1, power: 1, toughness: 1)
				.WithSubtype(Goblin)
				.WithSubtype(Berserker)
				.WithTriggeredAbility(
					"Frenzied Charge",
					TriggerConditions.OnSelfAttacks(),
					eb => eb.WithExhaust().WithTarget(Random().OpponentCreatures())
				)
				.Build(),
			CardFactory
				.Creature("Goblin Arsonist", manaCost: 1, power: 1, toughness: 1)
				.WithSubtype(Goblin)
				.WithSubtype(Shaman)
				.WithDeathTrigger(
					"Final Spark",
					eb => eb.WithDamage(1).WithTarget(Random().OpponentOrOpponentCreatures())
				)
				.Build(),
			// Printed as "{R}: gains flying until end of turn". Flying is real evasion here — it
			// gates who may attack it as well as what it may bypass — so a repeatable grant on a
			// one-drop is worth more than it reads.
			CardFactory
				.Creature("Goblin Balloon Brigade", manaCost: 1, power: 1, toughness: 1)
				.WithSubtype(Goblin)
				.WithSubtype(Warrior)
				.WithActivatedAbility(
					"Take Flight",
					manaCost: 1,
					effect: eb =>
						eb.WithAction(
							new GrantKeywordAction
							{
								GrantsFlying = true,
								Duration = ModifierDuration.UntilEndOfTurn,
								TargetContextKey = ContextKeys.SourceCardId,
							},
							TargetingStrategy.NoTarget()
						),
					maxPerTurn: 0
				)
				.Build(),
			CardFactory
				.Creature("Goblin Fireslinger", manaCost: 1, power: 1, toughness: 1)
				.WithSubtype(Goblin)
				.WithSubtype(Warrior)
				.WithActivatedAbility(
					"Pinger",
					manaCost: 0,
					effect: eb => eb.WithDamage(1).WithTarget(Single().Opponent()),
					requiresTap: true
				)
				.Build(),
			// Renown 1, then "as long as this is renowned it can't be blocked" — the second half is
			// inert. WithRenown sets MaxTriggers = 1, the LIFETIME cap, which is what "if it isn't
			// renowned" means; a per-turn cap would make it grow every turn instead of once ever.
			CardFactory
				.Creature("Goblin Glory Chaser", manaCost: 1, power: 1, toughness: 1)
				.WithSubtype(Goblin)
				.WithSubtype(Warrior)
				.WithRenown(1)
				.Build(),
			// The exile cost is the card, not a tax on it: unlimited repeatable 2 damage for one
			// mana would end games on its own, and eating the graveyard gives it a hard floor while
			// making it fold to graveyard hate.
			CardFactory
				.Creature("Grim Lavamancer", manaCost: 1, power: 1, toughness: 1)
				.WithSubtype(Human)
				.WithSubtype(Wizard)
				.WithActivatedAbility(
					"Flame Scour",
					manaCost: 1,
					effect: eb =>
						eb.WithDamage(2).WithTarget(Single().OpponentOrOpponentCreatures()),
					costs: cb => cb.ExileFromGraveyard(2),
					requiresTap: true,
					maxPerTurn: 0
				)
				.Build(),
			CardFactory
				.Creature("Scorch Spitter", manaCost: 1, power: 1, toughness: 1)
				.WithSubtype(Elemental)
				.WithSubtype(Lizard)
				.WithTriggeredAbility(
					"Searing Breath",
					TriggerConditions.OnSelfAttacks(),
					eb => eb.WithDamage(1).WithTarget(Single().Opponent())
				)
				.Build(),
			// ===== TWO MANA =====

			// The impulse-draw card the mechanic was built for. Prowess plus "exile the top card,
			// you may play it this turn" — see the "Impulse Draw" note in MtgCore/CLAUDE.md for why
			// the marker expires at the END of the exiling player's turn.
			CardFactory
				.Creature("Abbot of Keral Keep", manaCost: 2, power: 2, toughness: 1)
				.WithSubtype(Human)
				.WithSubtype(Monk)
				.WithEtbTrigger("Insight", eb => eb.WithImpulseDraw())
				.WithTriggeredAbility(
					"Prowess",
					TriggerConditions.OnYouCastSpell(),
					eb => eb.WithProwessBuff()
				)
				.Build(),
			// "Attacks each turn if able" is dropped — nothing here compels an attack.
			CardFactory
				.Creature("Borderland Marauder", manaCost: 2, power: 2, toughness: 2)
				.WithSubtype(Human)
				.WithSubtype(Warrior)
				.WithTriggeredAbility(
					"Battle Fury",
					TriggerConditions.OnSelfAttacks(),
					eb =>
						eb.WithAction(
							new AddModifierAction
							{
								PowerBonus = 2,
								ToughnessBonus = 0,
								Duration = ModifierDuration.UntilEndOfTurn,
								TargetContextKey = ContextKeys.SourceCardId,
							},
							TargetingStrategy.NoTarget()
						)
				)
				.Build(),
			// The haste half is the reason to play it: AddTemporaryManaAction raises CurrentMana
			// only, so the two mana evaporate next turn and this is strictly a "cheat something out
			// early" card rather than a ramp one.
			CardFactory
				.Creature("Generator Servant", manaCost: 2, power: 2, toughness: 1)
				.WithSubtype(Elemental)
				.WithActivatedAbility(
					"Overload",
					manaCost: 0,
					effect: eb =>
						eb.WithAddMana(2)
							.WithGrantKeyword(haste: true)
							.WithTarget(AllValid().AllYourCreatures()),
					costs: cb => cb.SacrificeSelf(),
					requiresTap: true
				)
				.Build(),
			CardFactory
				.Creature("Goblin Instigator", manaCost: 2, power: 1, toughness: 1)
				.WithSubtype(Goblin)
				.WithSubtype(Rogue)
				.WithEtbTrigger(
					"Rally",
					eb => eb.WithCreateTokens(CoresetCubeRedTokens.Goblin(), 1)
				)
				.Build(),
			// Printed: "+2/+0 for each other attacking Goblin", plus protection from blue. Neither
			// is expressible — attacking is not a queryable board state and there are no colours —
			// so it counts Goblins you control instead, live-evaluated so tokens entering mid-turn
			// are seen immediately. Base 1/2 rather than the printed 1/2 stays, but the bonus not
			// requiring an attack is an upgrade, so it earns its two mana on board presence alone.
			CardFactory
				.Creature("Goblin Piledriver", manaCost: 2, power: 1, toughness: 2)
				.WithSubtype(Goblin)
				.WithSubtype(Warrior)
				.WithPowerEqualToCreatureCount(
					powerPer: 2,
					toughnessPer: 0,
					countsSelf: false,
					subtype: Goblin
				)
				.Build(),
			// "Deals damage equal to its power" reads the SOURCE's power at resolution, which is
			// what makes the prowess trigger and the sacrifice ability combine.
			CardFactory
				.Creature("Heartfire Immolator", manaCost: 2, power: 2, toughness: 1)
				.WithSubtype(Human)
				.WithSubtype(Wizard)
				.WithTriggeredAbility(
					"Prowess",
					TriggerConditions.OnYouCastSpell(),
					eb => eb.WithProwessBuff()
				)
				.WithActivatedAbility(
					"Immolate",
					manaCost: 1,
					effect: eb => eb.WithDamage(2).WithTarget(Single().OpponentCreatures()),
					costs: cb => cb.SacrificeSelf()
				)
				.Build(),
			// Bloodthirst 2, reading LifeLostThisTurn — so it counts drain as well as damage, a
			// shade wider than printed. Menace is inert and dropped.
			CardFactory
				.Creature("Stormblood Berserker", manaCost: 2, power: 1, toughness: 1)
				.WithSubtype(Human)
				.WithSubtype(Berserker)
				.WithEtbTrigger(
					"Bloodthirst",
					eb =>
						eb.WithConditionalAction(
							new OpponentLostLifeThisTurnCondition(),
							new AddModifierAction
							{
								PowerBonus = 2,
								ToughnessBonus = 2,
								Duration = ModifierDuration.Permanent,
								TargetContextKey = ContextKeys.SourceCardId,
							}
						)
				)
				.Build(),
			// Printed with protection from a colour, which does not exist. First strike is the
			// closest thing red has to "wins the fight it picks", and in no-blocker combat it is a
			// premium keyword — see the First Strike note in MtgCore/CLAUDE.md.
			CardFactory
				.Creature("Unchained Berserker", manaCost: 2, power: 2, toughness: 2)
				.WithSubtype(Human)
				.WithSubtype(Berserker)
				.WithFirstStrike()
				.Build(),
			// OnYouCastSpell fires only from CastSpellAction and CastFromGraveyardAction, so it is
			// genuinely instant-and-sorcery-only — a creature or permanent cast does not feed it.
			CardFactory
				.Creature("Young Pyromancer", manaCost: 2, power: 2, toughness: 1)
				.WithSubtype(Human)
				.WithSubtype(Shaman)
				.WithTriggeredAbility(
					"Elemental Swarm",
					TriggerConditions.OnYouCastSpell(),
					eb => eb.WithCreateTokens(CoresetCubeRedTokens.Elemental(), 1)
				)
				.Build(),
			// ===== THREE MANA =====

			CardFactory
				.Creature("Arms Dealer", manaCost: 3, power: 2, toughness: 2)
				.WithSubtype(Goblin)
				.WithSubtype(Rogue)
				.WithActivatedAbility(
					"Fling Goblin",
					manaCost: 0,
					effect: eb => eb.WithDamage(2).WithTarget(Single().OpponentCreatures()),
					costs: cb => cb.SacrificeSubtype(Goblin),
					requiresTap: true
				)
				.Build(),
			// Menace is inert, and dropping it outright left a vanilla 3/2 for three with a
			// literally blank text box — below rate and the only card in the section with nothing
			// to read. Trample is the honest reskin: menace means "your blockers do not stop this
			// profitably", and trample is the one keyword here that still punishes a defender who
			// puts a body in the way.
			CardFactory
				.Creature("Boggart Brute", manaCost: 3, power: 3, toughness: 2)
				.WithSubtype(Goblin)
				.WithSubtype(Warrior)
				.WithTrample()
				.Build(),
			// The flip to a planeswalker is cut (creature/permanent routing split). What remains is
			// the damage trigger, which is the half that actually plays: a 2/2 that pings for its
			// power whenever you cast a spell.
			CardFactory
				.Creature("Chandra, Fire of Kaladesh", manaCost: 3, power: 2, toughness: 2)
				.WithSubtype(Human)
				.WithSubtype(Shaman)
				.WithTriggeredAbility(
					"Kindled Fury",
					TriggerConditions.OnYouCastSpell(),
					eb => eb.WithDamage(1).WithTarget(Single().Opponent())
				)
				.Build(),
			// Recursion keyed to the opponent losing life, which is red's most common board state.
			// ActiveInZone = Graveyard is mandatory: the card is already there when this fires.
			CardFactory
				.Creature("Chandra's Phoenix", manaCost: 3, power: 2, toughness: 2)
				.WithSubtype(Phoenix)
				.WithFlying()
				.WithHaste()
				.WithTriggeredAbility(
					"Rekindle",
					new LifeLostThisTurnCondition { Minimum = 3 },
					eb =>
						eb.WithAction(
							new MoveCardToHandAction
							{
								CardIdContextKey = ContextKeys.SourceCardId,
								PlayerIdContextKey = ContextKeys.CastingPlayerId,
							},
							TargetingStrategy.NoTarget()
						),
					ActiveInZone: ZoneType.Graveyard,
					maxPerTurn: 1
				)
				.Build(),
			// Printed as a discard outlet that pings each opponent. Menace dropped.
			CardFactory
				.Creature("Glint-Horn Buccaneer", manaCost: 3, power: 3, toughness: 4)
				.WithSubtype(Minotaur)
				.WithSubtype(Pirate)
				.WithActivatedAbility(
					"Raucous Bellow",
					manaCost: 2,
					effect: eb => eb.WithDamage(1).WithTarget(AllValid().OpponentCreatures()),
					costs: cb => cb.Discard(1),
					maxPerTurn: 0
				)
				.Build(),
			// The tribal lord. Haste on the whole team is what turns a board of Goblin tokens into
			// immediate damage, and it is why the token-makers are costed where they are.
			CardFactory
				// Haste is INTRINSIC here as well as granted. The printed card reads "Haste. Other
				// Goblin creatures you control get +1/+1 and have haste" — a static source never
				// applies to itself (StaticAbilityEngine.RegisterSourceAndApply skips it, which is
				// what makes "other" work at all), so without this line the Chieftain hands out a
				// keyword it does not have and cannot attack the turn it lands.
				.Creature("Goblin Chieftain", manaCost: 3, power: 2, toughness: 2)
				.WithSubtype(Goblin)
				.WithHaste()
				.WithComponent(
					new StaticPTBoostAbility
					{
						PowerBonus = 1,
						ToughnessBonus = 1,
						Filter = new IsSubtypeSpecification { Subtype = Goblin },
					}
				)
				.WithComponent(
					new StaticGrantKeywordAbility
					{
						GrantsHaste = true,
						Filter = new IsSubtypeSpecification { Subtype = Goblin },
					}
				)
				.Build(),
			// "Other Goblins attack each turn if able" is dropped with the rest of the forced
			// attacks, and "+1/+0 for each other attacking Goblin" becomes "for each other Goblin
			// you control" — see the header. The token is hasty, which is the printed card.
			CardFactory
				.Creature("Goblin Rabblemaster", manaCost: 3, power: 2, toughness: 2)
				.WithSubtype(Goblin)
				.WithSubtype(Warrior)
				.WithPowerEqualToCreatureCount(
					powerPer: 1,
					toughnessPer: 0,
					countsSelf: false,
					subtype: Goblin
				)
				.WithTriggeredAbility(
					"Rabble Rouser",
					TriggerConditions.OnYourUpkeep(),
					eb => eb.WithCreateTokens(CoresetCubeRedTokens.HastyGoblin(), 1)
				)
				.Build(),
			CardFactory
				.Creature("Guttersnipe", manaCost: 3, power: 2, toughness: 2)
				.WithSubtype(Goblin)
				.WithSubtype(Shaman)
				.WithTriggeredAbility(
					"Pyroclastic Rhetoric",
					TriggerConditions.OnYouCastSpell(),
					eb => eb.WithDamage(2).WithTarget(Single().Opponent())
				)
				.Build(),
			CardFactory
				.Creature("Rummaging Goblin", manaCost: 3, power: 2, toughness: 2)
				.WithSubtype(Goblin)
				.WithSubtype(Rogue)
				.WithActivatedAbility(
					"Rummage",
					manaCost: 0,
					effect: eb =>
						eb.WithDiscard(1).WithDraw(1).WithTarget(TargetingStrategy.Self()),
					requiresTap: true
				)
				.Build(),
			// Renown 1 plus a punisher trigger. The filter is the whole card: it must fire only on
			// an OPPONENT's noncreature spell, so it pairs IsControlledByOpponent with a type check
			// rather than using the bare SpellCast condition.
			CardFactory
				.Creature("Scab-Clan Berserker", manaCost: 3, power: 2, toughness: 2)
				.WithSubtype(Human)
				.WithSubtype(Berserker)
				.WithRenown(1)
				.WithTriggeredAbility(
					"Rage Tax",
					new EventTriggerCondition
					{
						EventTypeName = EventTypeNames.SpellCast,
						Filter = new IsControlledByOpponentSpecification(),
					},
					eb => eb.WithDamage(2).WithTarget(Single().Opponent())
				)
				.Build(),
			// A permanent buff, so it is this engine's +1/+1 counter — see the note on counters in
			// MtgCore/CLAUDE.md. Grows only on instants and sorceries, like every prowess payoff.
			CardFactory
				.Creature("Spellgorger Weird", manaCost: 3, power: 2, toughness: 2)
				.WithSubtype(Weird)
				.WithTriggeredAbility(
					"Gorge",
					TriggerConditions.OnYouCastSpell(),
					eb => eb.WithSelfBuff(1, 1)
				)
				.Build(),
			// ===== FOUR MANA =====

			// The discard clause is the real cost and is kept: a 4/4 flier for four that empties
			// your hand every turn is a genuine race card rather than a value one. The extra draw
			// is what pays for it.
			CardFactory
				.Creature("Avaricious Dragon", manaCost: 4, power: 4, toughness: 4)
				.WithSubtype(Dragon)
				.WithFlying()
				.WithTriggeredAbility(
					"Hoard",
					TriggerConditions.OnYourUpkeep(),
					eb => eb.WithDraw(1)
				)
				.WithTriggeredAbility(
					"Squander",
					new EventTriggerCondition
					{
						EventTypeName = EventTypeNames.TurnEnded,
						Filter = new IsControlledByYouSpecification(),
					},
					eb => eb.WithDiscard(1).WithTarget(TargetingStrategy.Self())
				)
				.Build(),
			// Doubles the Goblin board every activation. Unlimited per turn would be an infinite
			// loop with any sacrifice outlet, so it is once per turn and needs the tap.
			CardFactory
				.Creature("Krenko, Mob Boss", manaCost: 4, power: 3, toughness: 3)
				.WithSubtype(Goblin)
				.WithSubtype(Warrior)
				.WithActivatedAbility(
					"Mob Rule",
					manaCost: 0,
					effect: eb =>
						eb.WithCreateTokensPerCard(
							CoresetCubeRedTokens.Goblin(),
							subtype: Goblin,
							zone: ZoneType.Battlefield,
							countKey: "krenko_goblin_count"
						),
					requiresTap: true
				)
				.Build(),
			// Haste on arrival is the half that matters here: it turns every creature you play into
			// immediate damage, which is what the token strategies want.
			CardFactory
				.Creature("Ogre Battledriver", manaCost: 4, power: 3, toughness: 3)
				.WithSubtype(Ogre)
				.WithSubtype(Warrior)
				.WithTriggeredAbility(
					"Battle Cry",
					TriggerConditions.OnCreatureYouControlEnters(),
					// Both halves must land on the creature that ENTERED, which is what
					// TriggerSubjectId carries. A targeting strategy cannot see the event, so
					// Random() here would buff some other creature and leave the new arrival
					// without the haste that is the card's whole point — the same failure that
					// made Wall of Frost freeze the wrong creature. See "Reading the Triggering
					// Event" in MtgCore/CLAUDE.md.
					eb =>
						eb.WithAction(
								new AddModifierAction
								{
									PowerBonus = 2,
									ToughnessBonus = 0,
									Duration = ModifierDuration.UntilEndOfTurn,
									TargetContextKey = ContextKeys.TriggerSubjectId,
								},
								TargetingStrategy.NoTarget()
							)
							.WithAction(
								new GrantKeywordAction
								{
									GrantsHaste = true,
									Duration = ModifierDuration.UntilEndOfTurn,
									TargetContextKey = ContextKeys.TriggerSubjectId,
								},
								TargetingStrategy.NoTarget()
							)
				)
				.Build(),
			CardFactory
				.Creature("Pia and Kiran Nalaar", manaCost: 4, power: 2, toughness: 2)
				.WithSubtype(Human)
				.WithSubtype(Artificer)
				.WithEtbTrigger(
					"Thopter Escort",
					eb => eb.WithCreateTokens(CoresetCubeRedTokens.Thopter(), 2)
				)
				.WithActivatedAbility(
					"Scrap Cannon",
					manaCost: 2,
					effect: eb =>
						eb.WithDamage(2).WithTarget(Single().OpponentOrOpponentCreatures()),
					costs: cb => cb.SacrificeSubtype("Artifact"),
					maxPerTurn: 0
				)
				.Build(),
			// "Damage equal to the number of Goblins you control" — the payoff that makes a wide
			// Goblin board answer a single large creature.
			CardFactory
				.Creature("Volley Veteran", manaCost: 4, power: 3, toughness: 3)
				.WithSubtype(Goblin)
				.WithSubtype(Warrior)
				.WithEtbTrigger(
					"Volley",
					eb =>
						eb.WithAction(
							new PipelineAction
							{
								Steps = ImmutableList.Create<GameAction>(
									new CountCardsWithSubtypeAction
									{
										Subtype = Goblin,
										OutputKey = "volley_goblin_count",
										PlayerIdContextKey = ContextKeys.CastingPlayerId,
									},
									new SelectCreatureFromBattlefieldByManaCostAction
									{
										TargetOpponent = true,
										SelectLowest = false,
										PlayerIdContextKey = ContextKeys.CastingPlayerId,
										OutputKey = "volley_target",
									},
									new DealDamageAction
									{
										AmountContextKey = "volley_goblin_count",
										TargetContextKey = "volley_target",
									}
								),
							},
							TargetingStrategy.NoTarget()
						)
				)
				.Build(),
			// ===== FIVE MANA =====

			// Indestructible plus "whenever this is dealt damage, it deals that much to target
			// opponent". Taunt is what makes the damage half live — it forces attacks into a
			// creature that cannot die and reflects every point. TriggerAmount is what carries
			// "that much"; without it the clause would flatten to a constant.
			//
			// TauntUntilAttackedComponent for the same reason Fog Bank has it, and more sharply:
			// permanent Taunt on something that cannot die and PUNISHES being hit is not a
			// roadblock, it is a lock. Every attack was compelled into it, none of them killed it,
			// and each one burned the attacker — unanswerable without exile. It now soaks one
			// attack per turn and steps aside, which is what one blocker does.
			CardFactory
				.Creature("Brash Taunter", manaCost: 5, power: 1, toughness: 1)
				.WithSubtype(Goblin)
				.WithIndestructible()
				.WithTaunt()
				.WithComponent(new TauntUntilAttackedComponent())
				.WithTriggeredAbility(
					"Spite",
					new EventTriggerCondition
					{
						EventTypeName = EventTypeNames.CreatureDamaged,
						Filter = new IsSourceCardSpecification(),
					},
					eb =>
						eb.WithAction(
							// DealDamageAction rather than DrainLifeAction: the card deals damage,
							// and only an EffectAction can read "that much" off TriggerAmount.
							// AllValid over the opponent resolves to exactly one player, which is
							// the deterministic way to name them from inside a trigger.
							new DealDamageAction
							{
								AmountContextKey = ContextKeys.TriggerAmount,
							},
							AllValid().Opponent()
						)
				)
				.Build(),
			// Three Goblins on arrival plus a repeatable sacrifice outlet — the card the whole
			// Goblin section is building toward, and a reason SacrificeAdditionalCost had to
			// announce a real death (it did not, until black; see MtgCore/CLAUDE.md).
			CardFactory
				.Creature("Siege-Gang Commander", manaCost: 5, power: 2, toughness: 2)
				.WithSubtype(Goblin)
				.WithEtbTrigger(
					"Gang Up",
					eb => eb.WithCreateTokens(CoresetCubeRedTokens.Goblin(), 3)
				)
				.WithActivatedAbility(
					"Fling",
					manaCost: 2,
					effect: eb =>
						eb.WithDamage(2).WithTarget(Single().OpponentOrOpponentCreatures()),
					costs: cb => cb.SacrificeSubtype(Goblin),
					maxPerTurn: 0
				)
				.Build(),
			// Printed: "deals 1 damage to each creature with flying your opponents control, and
			// those creatures don't untap next turn." Both halves are kept — the freeze is a real
			// mechanic here — and it is the cleanest anti-flier card red has. Costs one less than
			// printed under the spray rule, since the damage is no longer aimed.
			CardFactory
				.Creature("Thundermaw Hellkite", manaCost: 5, power: 5, toughness: 5)
				.WithSubtype(Dragon)
				.WithFlying()
				.WithHaste()
				.WithEtbTrigger(
					"Downdraft",
					eb =>
						eb.WithDamage(1)
							.WithTarget(
								AllValid()
									.WithSpec(
										TargetSpecification
											.OpponentCreatures()
											.And(new HasFlyingSpecification())
									)
							)
							.WithFreeze(1)
							.WithTarget(
								AllValid()
									.WithSpec(
										TargetSpecification
											.OpponentCreatures()
											.And(new HasFlyingSpecification())
									)
							)
				)
				.Build(),
			// ===== SIX MANA =====

			// "Deals 3 damage divided as you choose" on ETB and on attack. Sprayed as three
			// independent 1-damage hits; costs one less than printed. The firebreathing is what
			// makes the body a threat on its own.
			CardFactory
				.Creature("Inferno Titan", manaCost: 6, power: 6, toughness: 6)
				.WithSubtype(Giant)
				.WithEtbTrigger("Eruption", eb => Spray(eb, 3))
				.WithTriggeredAbility(
					"Molten Charge",
					TriggerConditions.OnSelfAttacks(),
					eb => Spray(eb, 3)
				)
				.WithActivatedAbility(
					"Firebreathing",
					manaCost: 1,
					effect: eb =>
						eb.WithAction(
							new AddModifierAction
							{
								PowerBonus = 1,
								ToughnessBonus = 0,
								Duration = ModifierDuration.UntilEndOfTurn,
								TargetContextKey = ContextKeys.SourceCardId,
							},
							TargetingStrategy.NoTarget()
						),
					maxPerTurn: 0
				)
				.Build(),
			// The graveyard ability is the reason to run it over a plain fatty: it keeps threatening
			// from the bin. Impulse draw stands in for the printed "discard your hand, draw" shape,
			// which is the same "one more card, now" texture.
			CardFactory
				.Creature("Soul of Shandalar", manaCost: 6, power: 6, toughness: 5)
				.WithSubtype(Avatar)
				.WithEtbTrigger("Searing Soul", eb => Spray(eb, 3))
				.WithActivatedAbility(
					"Echo of Shandalar",
					manaCost: 4,
					effect: eb => eb.WithImpulseDraw(),
					maxPerTurn: 0
				)
				.Build(),
			// ===== SEVEN MANA =====

			// Printed: "deals 4 damage to any target and 3 damage to each of two other targets" on
			// attack. Seven sprayed points, costed one less. At seven mana it is the top of the
			// curve and is meant to end the game the turn after it lands.
			CardFactory
				.Creature("Drakuseth, Maw of Flames", manaCost: 7, power: 7, toughness: 7)
				.WithSubtype(Dragon)
				.WithFlying()
				.WithTriggeredAbility(
					"Maw of Flames",
					TriggerConditions.OnSelfAttacks(),
					eb => Spray(eb, 7)
				)
				.Build(),
		];

	/// <summary>
	/// "Deals N damage divided as you choose among any number of targets", as N independent
	/// 1-damage effects at random opposing targets — Arcane Missiles.
	///
	/// One helper rather than N chained calls per card so the shape cannot drift between the six
	/// cards that use it, and so the -1 costing rule has one thing to point at. See the header and
	/// DesignNotes.md for why this is a spray rather than a real partition.
	/// </summary>
	private static void Spray(SpellCardBuilder eb, int amount)
	{
		for (var i = 0; i < amount; i++)
			eb.WithDamage(1).WithTarget(Random().OpponentOrOpponentCreatures());
	}
}
