using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore.Cards.Builders;
using static MtgCore.Cards.Builders.TargetBuilder;

namespace MtgCore;

/// <summary>
/// Theme 5 — Angels and Demons. The set's top end, and the reanimation package's payoff.
///
/// Every card here obeys the 6-8 rate bar: it must effectively end the game on resolution.
/// A body alone is only a clock in a no-blocker combat model, and these are also the cards
/// most often cheated into play, so their value is front-loaded into the ETB. Nothing here
/// pays off "next upkeep" — a recurring drawback or a delayed reward would make them dead.
///
/// Angels reward Humans; Demons feed on discard and sacrifice.
///
/// Batches 1 and 6: 25 cards.
/// </summary>
public static class HollowmereAngelsDemons
{
	public static IReadOnlyList<Card> Cards { get; } =
		[
			// Lethal on resolution in a Human deck: the team grows and gains evasion at once.
			CardFactory
				.Creature("Archangel of Vigils", manaCost: 7, power: 6, toughness: 6)
				.WithSubtype(Hollowmere.Angel)
				.WithFlying()
				.WithLifelink()
				.WithEtbTrigger(
					"Vigil Unbroken",
					eb =>
						eb.WithAction(
								new AddModifierAction
								{
									PowerBonus = 2,
									ToughnessBonus = 2,
									Duration = ModifierDuration.Permanent,
								},
								TargetingStrategy.AllValid(
									new IsSubtypeSpecification { Subtype = Hollowmere.Human }.And(
										TargetSpecification.CreatureControlledByYou()
									)
								)
							)
							.WithAction(
								new GrantKeywordAction { GrantsFlying = true },
								AllValid().AllYourCreatures()
							)
				)
				.Build(),
			// Lifelink is what stops this being a worse Griselbrand — it refuels the life it
			// spends, so it draws into the winning turn instead of just being a big flier.
			new()
			{
				Name = "Abyssal Tyrant",
				ManaCost = 8,
				Subtypes = ImmutableHashSet.Create(
					StringComparer.OrdinalIgnoreCase,
					Hollowmere.Demon
				),
				Components = ImmutableArray.Create<GameComponent>(
					new PermanentComponent(),
					new CreatureComponent
					{
						Power = 7,
						Toughness = 7,
						HasFlying = true,
						HasLifelink = true,
					},
					new ActivatedAbilityComponent
					{
						Name = "Bargain",
						ManaCost = 0,
						MaxActivationsPerTurn = 0, // unlimited — the life total is the limiter
						AdditionalCosts = ImmutableList.Create<AdditionalCost>(
							new LifeAdditionalCost { Amount = 7 }
						),
						Effect = new CardEffect
						{
							TargetingStrategy = TargetingStrategy.Self(),
							ActionTemplate = new DrawCardsAction { Amount = 7 },
						},
					}
				),
			},
			// Two-for-one on resolution. Replaces a version whose upkeep sacrifice was pure
			// downside: with no blockers there is nothing to convert the sacrifice into.
			CardFactory
				.Creature("Hollow Vow Tyrant", manaCost: 6, power: 6, toughness: 6)
				.WithSubtype(Hollowmere.Demon)
				.WithFlying()
				.WithDeathtouch()
				.WithEtbTrigger(
					"Collect the Vow",
					eb =>
						eb.WithAction(
								new DestroyCreatureAction(),
								TargetingStrategy.SingleTarget(
									TargetSpecification.OpponentCreatures()
								)
							)
							.WithOpponentDiscard()
				)
				.Build(),
			// The cheap Angel that bridges to the Human deck — a reanimation target you are
			// also happy to hard-cast.
			CardFactory
				.Creature("Chapel Seraph", manaCost: 5, power: 4, toughness: 4)
				.WithSubtype(Hollowmere.Angel)
				.WithFlying()
				.WithEtbTrigger(
					"Sanctify",
					eb => eb.WithCreateTokens(HollowmereTokens.Human(), count: 2)
				)
				.Build(),
			// ===== BATCH 6: ANGELS =====
			// The cheap Angel, so the tribe is castable rather than only reanimatable.
			CardFactory
				.Creature("Voice of the Chapel", manaCost: 3, power: 3, toughness: 3)
				.WithSubtype(Hollowmere.Angel)
				.WithFlying()
				.Build(),
			// A defensive Angel — Taunt plus Flying holds both attack angles at once.
			CardFactory
				.Creature("Herald of the Chapel", manaCost: 4, power: 3, toughness: 5)
				.WithSubtype(Hollowmere.Angel)
				.WithFlying()
				.WithTaunt()
				.Build(),
			// The efficient evasive lifelinker that stabilises a race.
			CardFactory
				.Creature("Chapel Guardian Angel", manaCost: 4, power: 3, toughness: 4)
				.WithSubtype(Hollowmere.Angel)
				.WithFlying()
				.WithLifelink()
				.Build(),
			// Removal on a flier — the five-drop two-for-one.
			CardFactory
				.Creature("Angel of Broken Vigils", manaCost: 5, power: 4, toughness: 4)
				.WithSubtype(Hollowmere.Angel)
				.WithFlying()
				.WithEtbTrigger(
					"Break the Vigil",
					eb => eb.WithDestroy().WithTarget(Single().OpponentCreatures())
				)
				.Build(),
			// Bridges Angels into the go-wide themes.
			CardFactory
				.Creature("Angel of the Drowned Choir", manaCost: 5, power: 3, toughness: 4)
				.WithSubtype(Hollowmere.Angel)
				.WithFlying()
				.WithEtbTrigger(
					"Lead the Choir",
					eb => eb.WithCreateTokens(HollowmereTokens.Spirit(), count: 2)
				)
				.Build(),
			// Stabilises against the aggressive decks on the turn it lands.
			CardFactory
				.Creature("Seraph of the Silt", manaCost: 5, power: 4, toughness: 4)
				.WithSubtype(Hollowmere.Angel)
				.WithFlying()
				.WithLifelink()
				.WithEtbTrigger("Cleanse the Silt", eb => eb.WithLifeGain(4))
				.Build(),
			// Reanimation on the tribe's own body — an Angel that finds another.
			CardFactory
				.Creature("Angel of Second Rites", manaCost: 6, power: 5, toughness: 5)
				.WithSubtype(Hollowmere.Angel)
				.WithFlying()
				.WithEtbTrigger("Second Rites", eb => eb.WithReanimate())
				.Build(),
			// Wins on resolution in a Human deck, which is the six-drop bar.
			CardFactory
				.Creature("Archangel of Hollowmere", manaCost: 6, power: 5, toughness: 5)
				.WithSubtype(Hollowmere.Angel)
				.WithFlying()
				.WithLifelink()
				.WithEtbTrigger(
					"Gather the Faithful",
					eb =>
						eb.WithCreateTokens(HollowmereTokens.Human(), count: 3)
							.WithGrantKeyword(haste: true)
							.WithTarget(AllValid().AllYourCreatures())
				)
				.Build(),
			// The top of the curve: a permanent board-wide anthem that ends the game.
			CardFactory
				.Creature("Guardian of the Last Vigil", manaCost: 7, power: 6, toughness: 6)
				.WithSubtype(Hollowmere.Angel)
				.WithFlying()
				.WithLifelink()
				.WithEtbTrigger(
					"The Last Vigil",
					eb =>
						eb.WithAction(
							new AddModifierAction
							{
								PowerBonus = 2,
								ToughnessBonus = 2,
								Duration = ModifierDuration.Permanent,
							},
							AllValid().AllYourCreatures()
						)
				)
				.Build(),
			// The sweeper the go-wide decks have to play around.
			CardFactory
				.Spell("Angelic Reckoning", manaCost: 5)
				.WithDamage(4)
				.WithTarget(AllValid().OpponentCreatures())
				.Build(),
			// Evasion and a pump — the Angel deck's combat trick.
			CardFactory
				.Spell("Wings of the Vigil", manaCost: 2)
				.WithBoost(2, 2)
				.WithTarget(Single().YourCreatures())
				.WithGrantKeyword(flying: true)
				.WithTarget(Single().YourCreatures())
				.WithFlashback(4)
				.Build(),
			// ===== BATCH 6: DEMONS =====
			// The cheap Demon. Slightly under rate on toughness rather than carrying a
			// recurring drawback, which would be pure downside with no blockers.
			CardFactory
				.Creature("Fiend of the Silt", manaCost: 4, power: 4, toughness: 4)
				.WithSubtype(Hollowmere.Demon)
				.WithFlying()
				.Build(),
			// Card selection on a body, for the Demon decks built on discard.
			CardFactory
				.Creature("Infernal Bargainer", manaCost: 4, power: 3, toughness: 3)
				.WithSubtype(Hollowmere.Demon)
				.WithFlying()
				.WithEtbTrigger("Strike a Bargain", eb => eb.WithDiscard().WithDraw(2))
				.Build(),
			// Raw card advantage at a life cost the Vampire and Demon decks can pay.
			CardFactory.Spell("Demonic Bargain", manaCost: 3).WithDraw(3).WithLoseLife(3).Build(),
			// Edict on a flier — answers the hexproof and recursion threats targeting cannot.
			CardFactory
				.Creature("Demon of the Drowned Vow", manaCost: 5, power: 5, toughness: 5)
				.WithSubtype(Hollowmere.Demon)
				.WithFlying()
				.WithEtbTrigger(
					"Collect the Drowned Vow",
					eb => eb.WithDestroy().WithTarget(Single().OpponentCreatures())
				)
				.Build(),
			// A big trampling flier — pure clock at five.
			CardFactory
				.Creature("Persecutor of Hollowmere", manaCost: 5, power: 5, toughness: 5)
				.WithSubtype(Hollowmere.Demon)
				.WithFlying()
				.WithTrample()
				.Build(),
			// Turns every death into reach, in a format where creatures die constantly.
			CardFactory
				.Creature("Harvester of Souls", manaCost: 6, power: 5, toughness: 5)
				.WithSubtype(Hollowmere.Demon)
				.WithFlying()
				.WithTriggeredAbility(
					"Harvest",
					TriggerConditions.OnAnyCreatureDies(),
					eb =>
						eb.WithAction(
							new DrainLifeAction
							{
								Amount = 1,
								TargetOpponent = true,
								PlayerIdContextKey = ContextKeys.CastingPlayerId,
							},
							TargetingStrategy.NoTarget()
						)
				)
				.Build(),
			// Strips their hand and threatens lethal — a two-for-one on a huge body.
			CardFactory
				.Creature("Vow-Breaker Fiend", manaCost: 6, power: 6, toughness: 6)
				.WithSubtype(Hollowmere.Demon)
				.WithFlying()
				.WithDeathtouch()
				.WithEtbTrigger("Break the Vow", eb => eb.WithOpponentDiscard(2))
				.Build(),
			// A pure beater for the reanimation decks.
			CardFactory
				.Creature("Bloodgorged Fiend", manaCost: 6, power: 6, toughness: 5)
				.WithSubtype(Hollowmere.Demon)
				.WithFlying()
				.WithTrample()
				.Build(),
			// Two removal spells stapled to a 6/6 flier — the seven-drop bar.
			CardFactory
				.Creature("Reaper from the Mere", manaCost: 7, power: 6, toughness: 6)
				.WithSubtype(Hollowmere.Demon)
				.WithFlying()
				.WithEtbTrigger(
					"Reap the Mere",
					eb =>
						eb.WithDestroy()
							.WithTarget(Single().OpponentCreatures())
							.WithDestroy()
							.WithTarget(Single().OpponentCreatures())
				)
				.Build(),
			// The set's largest body, and an eight-point life swing on arrival.
			CardFactory
				.Creature("The Drowned Archfiend", manaCost: 8, power: 8, toughness: 8)
				.WithSubtype(Hollowmere.Demon)
				.WithFlying()
				.WithTrample()
				.WithLifelink()
				.WithEtbTrigger(
					"Drown the Living",
					eb =>
						eb.WithAction(
							new DrainLifeAction
							{
								Amount = 4,
								TargetOpponent = true,
								PlayerIdContextKey = ContextKeys.CastingPlayerId,
							},
							TargetingStrategy.NoTarget()
						)
				)
				.Build(),
		];
}
