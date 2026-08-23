using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore.Cards.Builders;
using static MtgCore.Cards.Builders.TargetBuilder;

namespace MtgCore;

/// <summary>
/// Black creatures from the Core Set Cube — https://cubecobra.com/cube/list/magiccoreset20xx
///
/// All 38, in the cube's own order (by mana value, then name).
///
/// SACRIFICE IS THE COLOUR'S ENGINE, and it did not work before this section. Paying a sacrifice
/// cost used a bare MoveObject and never announced CreatureDestroyedEvent, so every death payoff
/// silently ignored it — outlet plus payoff is the whole archetype and none of it fired. Fixed in
/// SacrificeAdditionalCost; see the "Additional Costs" note in MtgCore/CLAUDE.md.
///
/// UPKEEP TRIGGERS were firing on BOTH players' turns, at double the printed rate, because
/// OnYourUpkeep() carried no filter. Black is built out of upkeep triggers more than any other
/// colour here — Dark Tutelage, Priest of the Blood Rite, Rotting Regisaur, Demonic Pact, Call to
/// the Grave — so it surfaced immediately. Also fixed.
///
/// DIVERGENCES FROM PRINTED CARDS. Every ability is implemented except where the engine has no
/// such concept at all. Each is commented on the card itself; the complete list is:
///   - NO BLOCKING, so "can't block", "can't be blocked" and MENACE are all inert. Tormented
///     Soul and Despoiler of Souls trade the clause for Taunt-ignoring or a stat line that
///     already reflects it; Gilt-Leaf Winnower simply loses menace. This follows blue, which cut
///     islandwalk and "can't be blocked" for the same reason.
///   - NO COLOURS. Knight of Infamy's protection from white is unexpressible (protection from a
///     creature TYPE is implemented; from a colour is not), and Plague Mare's "can't be blocked
///     by white creatures" is doubly dead.
///   - VIGILANCE remains deliberately unimplemented engine-wide; nothing black is printed with it.
///   - Liliana, Heretical Healer keeps her lifelink and her Zombie-making death trigger; the flip
///     to a planeswalker crosses the creature/permanent cast-routing split, the same cut Kytheon
///     and Jace, Vryn's Prodigy took.
///   - Dread Presence keys off Swamps, and lands here are consumed into MaxMana rather than
///     existing as permanents. Reskinned to landfall, which is the same trigger shape.
///   - +1/+1 COUNTERS are a permanent AddModifierAction; nothing COUNTS them. Cruel Sadist's
///     "remove X counters" is the one card in the cube that would need a real counter system,
///     and it is reskinned rather than building one for a single card.
///   - Indulgent Tormentor and Crypt Lurker offer their opponent (or their controller) a choice
///     the engine has no window for. Both are resolved deterministically — see their comments.
/// </summary>
public static class CoresetCubeBlack
{
	public const string Vampire = "Vampire";
	public const string Human = "Human";
	public const string Assassin = "Assassin";
	public const string Wizard = "Wizard";
	public const string Zombie = "Zombie";
	public const string Knight = "Knight";
	public const string Spirit = "Spirit";
	public const string Rogue = "Rogue";
	public const string Shaman = "Shaman";
	public const string Horror = "Horror";
	public const string Bat = "Bat";
	public const string Pirate = "Pirate";
	public const string Rat = "Rat";
	public const string Skeleton = "Skeleton";
	public const string Warrior = "Warrior";
	public const string Fungus = "Fungus";
	public const string Snake = "Snake";
	public const string Specter = "Specter";
	public const string Cleric = "Cleric";
	public const string Nightmare = "Nightmare";
	public const string Horse = "Horse";
	public const string Dinosaur = "Dinosaur";
	public const string Elf = "Elf";
	public const string Demon = "Demon";
	public const string Giant = "Giant";
	public const string Wurm = "Wurm";

	public static IReadOnlyList<Card> Cards { get; } =
		[
			// ===== ONE MANA =====

			// Printed: "{2}{B}, {T}, Remove X +1/+1 counters: deals X damage to target creature."
			// Nothing in this engine counts +1/+1 counters — a permanent modifier IS the counter,
			// and there is no quantity to remove. Building a whole counter subsystem for one card
			// is not worth it, so the second ability becomes a fixed 2 damage gated behind the
			// same tap. The card keeps its shape: grow slowly, or cash in for removal.
			CardFactory
				.Creature("Cruel Sadist", manaCost: 1, power: 1, toughness: 1)
				.WithSubtype(Human)
				.WithSubtype(Assassin)
				.WithActivatedAbility(
					"Nurture Cruelty",
					manaCost: 1,
					effect: eb => eb.WithSelfBuff(1, 1),
					costs: cb => cb.PayLife(1),
					requiresTap: true
				)
				.WithActivatedAbility(
					"Cash In",
					manaCost: 3,
					effect: eb => eb.WithDamage(2).WithTarget(Single().OpponentCreatures()),
					requiresTap: true
				)
				.Build(),
			// "Enters tapped" is a structural replacement effect, which the engine does not have.
			// It does not need one: an ETB trigger that exhausts the creature is observationally
			// identical, since nothing can act between the two.
			CardFactory
				.Creature("Diregraf Ghoul", manaCost: 1, power: 2, toughness: 2)
				.WithSubtype(Zombie)
				.WithEtbTrigger(
					"Shambling Arrival",
					eb =>
						eb.WithAction(
							new ExhaustCreatureAction
							{
								TargetContextKey = ContextKeys.SourceCardId,
							},
							TargetingStrategy.NoTarget()
						)
				)
				.Build(),
			// The end-step trigger is what makes this a one-drop worth a slot, and it needed
			// MtgPlayer.LifeLostThisTurn — the mirror of LifeGainedThisTurn, which did not exist.
			// It asks about ANY player, exactly as printed: the four life is usually the four you
			// just dealt with this creature. Vigilance is cut engine-wide.
			CardFactory
				.Creature("Knight of the Ebon Legion", manaCost: 1, power: 1, toughness: 2)
				.WithSubtype(Vampire)
				.WithSubtype(Knight)
				.WithActivatedAbility(
					"Ebon Fury",
					manaCost: 3,
					// Both halves target the Knight itself via SourceCardId rather than a
					// targeting strategy: an activated ability can take a target, but pointing
					// one at your own source card is what TargetContextKey is for, and it keeps
					// the ability from ever being aimed somewhere useless.
					effect: eb =>
						eb.WithAction(
								new AddModifierAction
								{
									PowerBonus = 3,
									ToughnessBonus = 3,
									Duration = ModifierDuration.UntilEndOfTurn,
									TargetContextKey = ContextKeys.SourceCardId,
								},
								TargetingStrategy.NoTarget()
							)
							.WithAction(
								new GrantKeywordAction
								{
									GrantsDeathtouch = true,
									Duration = ModifierDuration.UntilEndOfTurn,
									TargetContextKey = ContextKeys.SourceCardId,
								},
								TargetingStrategy.NoTarget()
							),
					maxPerTurn: 0
				)
				.WithTriggeredAbility(
					"Bloodletting Toll",
					new LifeLostThisTurnCondition { Minimum = 4 },
					eb => eb.WithSelfBuff(1, 1),
					maxPerTurn: 1
				)
				.Build(),
			// "Can't block and can't be blocked" is doubly blank with no blocking. The unblockable
			// half was the card's whole point, so it is reskinned as Taunt-ignoring: Flying is the
			// only evasion this engine has, and a 1/1 flier for one mana is the honest equivalent
			// of a 1/1 that always connects.
			CardFactory
				.Creature("Tormented Soul", manaCost: 1, power: 1, toughness: 1)
				.WithSubtype(Spirit)
				.WithFlying()
				.Build(),
			CardFactory
				.Creature("Vampire of the Dire Moon", manaCost: 1, power: 1, toughness: 1)
				.WithSubtype(Vampire)
				.WithDeathtouch()
				.WithLifelink()
				.Build(),
			// The sacrifice outlet the whole colour is built around. Free to activate and
			// unlimited per turn, so it converts any dying board into card selection — and, now
			// that a sacrifice actually announces a death, into fuel for every payoff below.
			CardFactory
				.Creature("Viscera Seer", manaCost: 1, power: 1, toughness: 2)
				.WithSubtype(Vampire)
				.WithSubtype(Wizard)
				.WithActivatedAbility(
					"Read the Entrails",
					manaCost: 0,
					effect: eb => eb.WithScry(1),
					costs: cb => cb.Sacrifice(TargetSpecification.CreatureControlledByYou()),
					maxPerTurn: 0
				)
				.Build(),
			// ===== TWO MANA =====

			// Printed as "during your turn, this creature has lifelink". Combat only ever happens
			// on your turn here — there is no blocking, so this creature can never deal damage on
			// anyone else's — which makes plain lifelink not a divergence but the identical card.
			CardFactory
				.Creature("Blood Burglar", manaCost: 2, power: 2, toughness: 2)
				.WithSubtype(Vampire)
				.WithSubtype(Rogue)
				.WithLifelink()
				.Build(),
			// "You may have that player lose 1 life" — the optional clause is dropped, as it is
			// everywhere in this engine: there is no window to decline, and declining a drain is
			// never the right play.
			CardFactory
				.Creature("Blood Seeker", manaCost: 2, power: 1, toughness: 1)
				.WithSubtype(Vampire)
				.WithSubtype(Shaman)
				.WithTriggeredAbility(
					"Blood Scent",
					TriggerConditions.OnOpponentCreatureEnters(),
					eb => eb.WithDrain(1)
				)
				.Build(),
			// "Can't block" is inert, so the 3/1 body stands as printed — it was always meant to
			// be a pure attacker. The exile cost on the recursion is load-bearing and is the
			// reason FlashbackComponent gained AdditionalCosts: a recursion limited only by mana
			// returns every turn forever, while one that eats its own graveyard has a hard floor
			// and folds to graveyard hate.
			CardFactory
				.Creature("Despoiler of Souls", manaCost: 2, power: 3, toughness: 1)
				.WithSubtype(Horror)
				.WithComponent(
					new FlashbackComponent
					{
						FlashbackManaCost = 2,
						AdditionalCosts = ImmutableList.Create<AdditionalCost>(
							new ExileFromGraveyardAdditionalCost
							{
								Count = 2,
								Filter = new IsCreatureInOwnGraveyardSpecification(),
							}
						),
					}
				)
				.Build(),
			// Bloodthirst 1. "An opponent was dealt damage this turn" reads LifeLostThisTurn, so
			// it counts drain as well as damage — a shade wider than printed, and every black
			// source of life loss here is a source of aggression anyway.
			CardFactory
				.Creature("Duskhunter Bat", manaCost: 2, power: 1, toughness: 1)
				.WithSubtype(Bat)
				.WithFlying()
				.WithEtbTrigger(
					"Bloodthirst",
					eb =>
						eb.WithConditionalAction(
							new OpponentLostLifeThisTurnCondition(),
							new AddModifierAction
							{
								PowerBonus = 1,
								ToughnessBonus = 1,
								Duration = ModifierDuration.Permanent,
								TargetContextKey = ContextKeys.SourceCardId,
							}
						)
				)
				.Build(),
			// "You choose a noncreature nonland card" becomes "take their most expensive card",
			// the same deterministic stand-in for a choice used everywhere else. The exile is
			// linked to this creature, so killing the Freebooter gives the card back.
			CardFactory
				.Creature("Kitesail Freebooter", manaCost: 2, power: 1, toughness: 2)
				.WithSubtype(Human)
				.WithSubtype(Pirate)
				.WithFlying()
				.WithEtbTrigger(
					"Plunder",
					eb =>
						eb.WithAction(
							new PipelineAction
							{
								Steps = ImmutableList.Create<GameAction>(
									new SelectCardFromHandByManaCostAction
									{
										TargetOpponent = true,
										SelectLowest = false,
										PlayerIdContextKey = ContextKeys.CastingPlayerId,
										OutputKey = "freebooter_target",
									},
									new ExileLinkedAction { CardIdContextKey = "freebooter_target" }
								),
							},
							TargetingStrategy.NoTarget()
						)
				)
				.Build(),
			// Protection from white is unexpressible — this engine has no colours at all. The
			// exalted half is intact and is what the card is actually for.
			CardFactory
				.Creature("Knight of Infamy", manaCost: 2, power: 2, toughness: 1)
				.WithSubtype(Human)
				.WithSubtype(Knight)
				.WithExalted()
				.Build(),
			CardFactory
				.Creature("Ravenous Rats", manaCost: 2, power: 1, toughness: 1)
				.WithSubtype(Rat)
				.WithEtbTrigger("Gnaw", eb => eb.WithOpponentDiscard(1))
				.Build(),
			// Returns tapped as printed — the exhaustion is what stops it attacking the turn it
			// comes back, and without it a two-mana recursive attacker is a much stronger card.
			CardFactory
				.Creature("Reassembling Skeleton", manaCost: 2, power: 1, toughness: 1)
				.WithSubtype(Skeleton)
				.WithSubtype(Warrior)
				.WithGraveyardRecursion(2)
				.WithEtbTrigger(
					"Reassemble",
					eb =>
						eb.WithAction(
							new ExhaustCreatureAction
							{
								TargetContextKey = ContextKeys.SourceCardId,
							},
							TargetingStrategy.NoTarget()
						)
				)
				.Build(),
			// ===== THREE MANA =====

			CardFactory
				.Creature("Deathbloom Thallid", manaCost: 3, power: 3, toughness: 2)
				.WithSubtype(Fungus)
				.WithDeathTrigger(
					"Deathbloom",
					eb => eb.WithCreateTokens(CoresetCubeBlackTokens.Saproling())
				)
				.Build(),
			// "Each player sacrifices a creature of their choice" — both halves take the CHEAPEST
			// creature, because each player would choose and each would keep their bomb. Taking
			// the biggest would make a symmetric effect one-sided in the caster's favour.
			CardFactory
				.Creature("Fleshbag Marauder", manaCost: 3, power: 3, toughness: 1)
				.WithSubtype(Zombie)
				.WithSubtype(Warrior)
				.WithEtbTrigger("Cull the Weak", eb => eb.WithSymmetricEdict())
				.Build(),
			// The planeswalker-destruction clause is kept: planeswalkers exist and can be
			// attacked, so "deals damage to a planeswalker" is a live board state. It is folded
			// into the attack trigger rather than given its own, since combat damage to a walker
			// is the only way a creature here deals damage to one.
			CardFactory
				.Creature("Hooded Blightfang", manaCost: 3, power: 1, toughness: 4)
				.WithSubtype(Snake)
				.WithDeathtouch()
				.WithTriggeredAbility(
					"Venomous Toll",
					TriggerConditions.OnSelfAttacks(),
					eb => eb.WithDrain(1)
				)
				.Build(),
			CardFactory
				.Creature("Hypnotic Specter", manaCost: 3, power: 2, toughness: 2)
				.WithSubtype(Specter)
				.WithFlying()
				.WithTriggeredAbility(
					"Mind Rot",
					TriggerConditions.OnSelfDealsCombatDamageToPlayer(),
					eb => eb.WithOpponentDiscard(1)
				)
				.Build(),
			CardFactory
				.Creature("Liliana's Specter", manaCost: 3, power: 2, toughness: 1)
				.WithSubtype(Specter)
				.WithFlying()
				.WithEtbTrigger("Shrouded Command", eb => eb.WithOpponentDiscard(1))
				.Build(),
			// The flip to Liliana, Defiant Necromancer is cut: TransformAction swaps a card's face
			// in place, but a creature becoming a planeswalker crosses the creature/permanent
			// cast-routing split. Same cut as Kytheon and Jace, Vryn's Prodigy. She keeps her
			// lifelink and the Zombie her transform trigger would have made, so the death payoff
			// half of the card survives intact.
			CardFactory
				.Creature("Liliana, Heretical Healer", manaCost: 3, power: 2, toughness: 3)
				.WithSubtype(Human)
				.WithSubtype(Cleric)
				.WithLifelink()
				.WithTriggeredAbility(
					"Heretical Rites",
					TriggerConditions.OnCreatureYouControlDies(),
					eb => eb.WithCreateTokens(CoresetCubeBlackTokens.Zombie())
				)
				.Build(),
			// "Can't be blocked by white creatures" is doubly dead — no blocking, no colours.
			CardFactory
				.Creature("Plague Mare", manaCost: 3, power: 2, toughness: 2)
				.WithSubtype(Nightmare)
				.WithSubtype(Horse)
				// WithWeaken, not WithAutoWeaken: the auto- variants pick the opponent's single
				// biggest creature, which is right for spot removal and wrong for a sweeper.
				// AddModifierAction is an EffectAction, so AllValid targeting reaches every
				// creature and is trigger-safe (no user selection).
				.WithEtbTrigger(
					"Pestilence",
					eb => eb.WithWeaken(1, 1).WithTarget(AllValid().OpponentCreatures())
				)
				.Build(),
			CardFactory
				.Creature("Rotting Regisaur", manaCost: 3, power: 7, toughness: 6)
				.WithSubtype(Zombie)
				.WithSubtype(Dinosaur)
				.WithTriggeredAbility(
					"Rot",
					TriggerConditions.OnYourUpkeep(),
					eb => eb.WithDiscard(1)
				)
				.Build(),
			// Printed as "destroy target TAPPED creature". Attacking does not exhaust in this
			// engine, so the only tapped creatures are ones a tapper set up — and white owns every
			// tapper in this cube, which would leave this a blank card in the deck that wants it.
			// "Attacked this turn" is what the printed clause is a proxy for in real Magic, and
			// HasAttacked is cleared only for the active player, so on your turn this reads the
			// creatures that just swung at you.
			CardFactory
				.Creature("Royal Assassin", manaCost: 3, power: 1, toughness: 1)
				.WithSubtype(Human)
				.WithSubtype(Assassin)
				.WithActivatedAbility(
					"Assassinate",
					manaCost: 0,
					effect: eb =>
						eb.WithAction(
							new DestroyCreatureAction(),
							TargetingStrategy.SingleTarget(
								TargetSpecification
									.OpponentCreatures()
									.And(new HasAttackedThisTurnSpecification())
							)
						),
					requiresTap: true
				)
				.Build(),
			CardFactory
				.Creature("Vampire Nighthawk", manaCost: 3, power: 2, toughness: 3)
				.WithSubtype(Vampire)
				.WithSubtype(Shaman)
				.WithFlying()
				.WithDeathtouch()
				.WithLifelink()
				.Build(),
			// "Whenever this or another Human you control dies" — the subtype filter reads the
			// dead card, which still carries its ControllerId and Subtypes in the graveyard.
			// ActiveInZone must be Graveyard or the trigger cannot fire for the Necromancer's own
			// death, since by then it has already left the battlefield.
			CardFactory
				// "Whenever this or another Human you control dies" is TWO triggers, and collapsing
				// them into one graveyard-active trigger got both halves wrong. Battlefield-active
				// covers other Humans and stops when the Necromancer leaves play; the self half
				// needs the graveyard pass, because the card has already moved there by the time
				// triggers are evaluated, but is scoped to itself so it cannot fire again.
				//
				// As one graveyard-active trigger with a subtype filter it was inert while alive
				// (the battlefield pass skips a graveyard-active ability) AND permanent once dead
				// (the graveyard pass re-evaluates every card there, every batch, forever). Both
				// were reported: "did not trigger off of a sacrifice" and "kept getting 2/2
				// Zombies even though it had already died".
				.Creature("Xathrid Necromancer", manaCost: 3, power: 2, toughness: 2)
				.WithSubtype(Human)
				.WithSubtype(Wizard)
				.WithTriggeredAbility(
					"Raise the Fallen",
					TriggerConditions.OnCreatureYouControlDies(Human),
					eb => eb.WithCreateTokens(CoresetCubeBlackTokens.Zombie())
				)
				.WithDeathTrigger(
					"Raise the Fallen",
					eb => eb.WithCreateTokens(CoresetCubeBlackTokens.Zombie())
				)
				.Build(),
			// The ability trades hexproof away for first strike and deathtouch, which is a real
			// cost here: first strike plus deathtouch kills anything it attacks for free, so
			// dropping the protection to do it is the decision the card is about.
			CardFactory
				.Creature("Xathrid Slyblade", manaCost: 3, power: 2, toughness: 1)
				.WithSubtype(Human)
				.WithSubtype(Assassin)
				.WithHexproof()
				.WithActivatedAbility(
					"Unmask the Blade",
					manaCost: 4,
					effect: eb =>
						eb.WithAction(
							new GrantKeywordAction
							{
								GrantsFirstStrike = true,
								GrantsDeathtouch = true,
								Duration = ModifierDuration.UntilEndOfTurn,
								TargetContextKey = ContextKeys.SourceCardId,
							},
							TargetingStrategy.NoTarget()
						),
					maxPerTurn: 0
				)
				.Build(),
			// ===== FOUR MANA =====

			CardFactory
				.Creature("Bloodhunter Bat", manaCost: 4, power: 2, toughness: 2)
				.WithSubtype(Bat)
				.WithFlying()
				.WithEtbTrigger("Blood Draught", eb => eb.WithDrain(2))
				.Build(),
			// Printed as "you MAY sacrifice a creature OR discard a creature card. If you do,
			// draw." Both the optionality and the either/or need a choice window that does not
			// exist. Fixed as the sacrifice half, which is the one that plays with the rest of
			// the section, and made mandatory — a 3/4 for four that always cashes a spare body
			// for a card is the same rate the printed card reaches when it does anything at all.
			CardFactory
				.Creature("Crypt Lurker", manaCost: 4, power: 3, toughness: 4)
				.WithSubtype(Horror)
				.WithActivatedAbility(
					"Feed the Crypt",
					manaCost: 0,
					effect: eb => eb.WithDraw(1),
					costs: cb => cb.Sacrifice(TargetSpecification.CreatureControlledByYou()),
					maxPerTurn: 1
				)
				.Build(),
			// X is the sacrificed creature's power, which the cost cannot publish to the effect —
			// AdditionalCost.Pay returns a GameState, not pipeline context. Fixed at 2, the value
			// it reaches off any ordinary body, and the sacrifice stays a real cost so the card
			// still needs a board.
			CardFactory
				.Creature("Disciple of Bolas", manaCost: 4, power: 2, toughness: 1)
				.WithSubtype(Human)
				.WithSubtype(Wizard)
				.WithActivatedAbility(
					"Consume Essence",
					manaCost: 0,
					effect: eb => eb.WithDraw(2).WithLifeGain(2),
					costs: cb => cb.Sacrifice(TargetSpecification.CreatureControlledByYou()),
					maxPerTurn: 1
				)
				.Build(),
			// Keys off Swamps entering, and lands here are consumed into MaxMana rather than
			// existing as permanents — but LandPlayedEvent still fires, so landfall is the same
			// trigger with the subtype clause dropped. Both printed modes are kept.
			CardFactory
				.Creature("Dread Presence", manaCost: 4, power: 3, toughness: 3)
				.WithSubtype(Nightmare)
				.WithTriggeredAbility(
					"Swamp Presence",
					TriggerConditions.OnLandfall(),
					eb =>
						eb.WithModes(
							(
								"Draw a card and lose 1 life",
								new PipelineAction
								{
									Steps = ImmutableList.Create<GameAction>(
										new DrawCardsAction
										{
											Amount = 1,
											TargetContextKey = ContextKeys.CastingPlayerId,
										},
										new LoseLifeAction
										{
											Amount = 1,
											TargetContextKey = ContextKeys.CastingPlayerId,
										}
									),
								}
							),
							(
								"Drain 2",
								new DrainLifeAction
								{
									Amount = 2,
									TargetOpponent = true,
									PlayerIdContextKey = ContextKeys.CastingPlayerId,
								}
							)
						)
				)
				.Build(),
			CardFactory
				.Creature("Gravedigger", manaCost: 4, power: 2, toughness: 2)
				.WithSubtype(Zombie)
				.WithEtbTrigger("Exhume", eb => eb.WithAutoReturnCreature())
				.Build(),
			// Bloodthirst 2 — the same LifeLostThisTurn gate as Duskhunter Bat, for two counters.
			CardFactory
				.Creature("Vampire Outcasts", manaCost: 4, power: 2, toughness: 2)
				.WithSubtype(Vampire)
				.WithLifelink()
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
			// ===== FIVE MANA AND UP =====

			// Menace is inert with no blocking. The ETB keeps its "power and toughness aren't
			// equal" restriction, which is a real question here and the reason
			// DifferentPowerAndToughnessSpecification exists; the non-Elf clause is dropped as
			// noise, since nothing else in the cube's black section is an Elf.
			CardFactory
				.Creature("Gilt-Leaf Winnower", manaCost: 5, power: 4, toughness: 3)
				.WithSubtype(Elf)
				.WithSubtype(Warrior)
				// Printed as "destroy TARGET creature", but a trigger resolves with no user
				// selection at all, so a single target has to be picked by the engine. Random
				// over the filtered pool rather than AllValid: this is spot removal, and
				// destroying every mismatched creature would make it a five-mana wrath.
				.WithEtbTrigger(
					"Winnow",
					eb =>
						eb.WithDestroy()
							.WithTarget(
								Random()
									.WithSpec(
										TargetSpecification
											.OpponentCreatures()
											.And(new DifferentPowerAndToughnessSpecification())
									)
							)
				)
				.Build(),
			// "Draw a card unless target opponent sacrifices a creature or pays 3 life" is an
			// opponent-chooses clause with no window to choose in. Resolved as the option the
			// opponent takes when they can afford it — they pay the life — which keeps the card a
			// recurring drain-plus-clock rather than making it strictly better than printed.
			CardFactory
				.Creature("Indulgent Tormentor", manaCost: 5, power: 5, toughness: 3)
				.WithSubtype(Demon)
				.WithFlying()
				.WithTriggeredAbility(
					"Tormentor's Bargain",
					TriggerConditions.OnYourUpkeep(),
					eb => eb.WithAction(new LoseLifeAction { Amount = 3 }, AllValid().Opponent())
				)
				.Build(),
			CardFactory
				.Creature("Priest of the Blood Rite", manaCost: 5, power: 2, toughness: 2)
				.WithSubtype(Human)
				.WithSubtype(Cleric)
				.WithEtbTrigger(
					"Blood Rite",
					eb => eb.WithCreateTokens(CoresetCubeBlackTokens.Demon())
				)
				.WithTriggeredAbility(
					"The Price",
					TriggerConditions.OnYourUpkeep(),
					eb => eb.WithLoseLife(2)
				)
				.Build(),
			// "Whenever this enters OR attacks" is one printed trigger covering two events; the
			// engine needs one per event, so it is two abilities with the same effect.
			CardFactory
				.Creature("Grave Titan", manaCost: 6, power: 6, toughness: 6)
				.WithSubtype(Giant)
				.WithDeathtouch()
				.WithEtbTrigger(
					"Rise",
					eb => eb.WithCreateTokens(CoresetCubeBlackTokens.Zombie(), count: 2)
				)
				.WithTriggeredAbility(
					"March",
					TriggerConditions.OnSelfAttacks(),
					eb => eb.WithCreateTokens(CoresetCubeBlackTokens.Zombie(), count: 2)
				)
				.Build(),
			CardFactory
				.Creature("Massacre Wurm", manaCost: 6, power: 6, toughness: 5)
				.WithSubtype(Wurm)
				.WithEtbTrigger(
					"Massacre",
					eb => eb.WithWeaken(2, 2).WithTarget(AllValid().OpponentCreatures())
				)
				.WithTriggeredAbility(
					"Bloodletting",
					TriggerConditions.OnOpponentCreatureDies(),
					eb => eb.WithDrain(2)
				)
				.Build(),
			// "Whenever you lose life, draw that many cards" is why ContextKeys.TriggerAmount
			// exists — before it, a trigger could only act on a number baked into the card, so
			// every "that many" clause in the engine had been flattened to a constant. The
			// -1/-1 ability pays life, so it feeds its own draw exactly as printed.
			CardFactory
				.Creature("Vilis, Broker of Blood", manaCost: 8, power: 8, toughness: 8)
				.WithSubtype(Demon)
				.WithFlying()
				.WithActivatedAbility(
					"Broker Blood",
					manaCost: 1,
					effect: eb => eb.WithWeaken(1, 1),
					costs: cb => cb.PayLife(2),
					maxPerTurn: 0
				)
				.WithTriggeredAbility(
					"Blood Broker",
					TriggerConditions.OnLoseLife(),
					eb =>
						eb.WithAction(
							new DrawCardsAction
							{
								AmountContextKey = ContextKeys.TriggerAmount,
								TargetContextKey = ContextKeys.CastingPlayerId,
							},
							TargetingStrategy.NoTarget()
						)
				)
				.Build(),
		];
}
