using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore.Cards.Builders;
using static MtgCore.Cards.Builders.TargetBuilder;

namespace MtgCore;

/// <summary>
/// Theme 1 — Graveyard Matters. The set's spine: threshold, flashback, reanimation, and
/// creatures that scale off graveyard size. Most other themes overlap into this one.
///
/// Self-mill is capped at 5 per card. In a 40-card deck a player draws roughly 20 cards over a
/// normal game, so stacking large self-mill effects can deck the caster — LibraryEmptyEvent is
/// a loss condition. Repeatable small mill is the safer way to build a deep graveyard.
///
/// Batches 1-2: 35 cards of a planned 60.
/// </summary>
public static class HollowmereGraveyard
{
	public static IReadOnlyList<Card> Cards { get; } =
		[
			// Mills and replaces itself, so the mill is upside rather than the whole card.
			CardFactory
				.Creature("Grave Scholar", manaCost: 2, power: 3, toughness: 2)
				.WithSubtype(Hollowmere.Human)
				.WithSubtype(Hollowmere.Wizard)
				.WithEtbTrigger(
					"Dredge the Shallows",
					eb =>
						eb.WithAction(
							new PipelineAction
							{
								Steps = ImmutableList.Create<GameAction>(
									new MillAction
									{
										Amount = 3,
										TargetContextKey = ContextKeys.CastingPlayerId,
									},
									new SelectCardFromZoneAction
									{
										Zone = ZoneType.Graveyard,
										Filter =
											new IsInstantOrSorceryInOwnGraveyardSpecification(),
										PlayerIdContextKey = ContextKeys.CastingPlayerId,
										OutputKey = "scholar_target",
									},
									new MoveCardToHandAction
									{
										CardIdContextKey = "scholar_target",
										PlayerIdContextKey = ContextKeys.CastingPlayerId,
									}
								),
							},
							TargetingStrategy.NoTarget()
						)
				)
				.Build(),
			// Tarmogoyf's rate is the whole appeal — at a higher cost this is a worse vanilla.
			new()
			{
				Name = "Splinterbone Horror",
				ManaCost = 2,
				Subtypes = ImmutableHashSet.Create(
					StringComparer.OrdinalIgnoreCase,
					Hollowmere.Zombie,
					Hollowmere.Horror
				),
				Components = ImmutableArray.Create<GameComponent>(
					new PermanentComponent(),
					new CreatureComponent { Power = 0, Toughness = 1 },
					new GraveyardCountComponent { Duration = ModifierDuration.Permanent }
				),
			},
			// The threshold payoff at common rate: fine early, a real threat once online.
			CardFactory
				.Creature("Nightfall Reveler", manaCost: 3, power: 2, toughness: 2)
				.WithSubtype(Hollowmere.Human)
				.WithThreshold(power: 3, toughness: 3, flying: true)
				.Build(),
			// The zone-dependent static. Flying is the strongest keyword here because it is
			// the only clean way past a Taunt blocker-substitute.
			new()
			{
				Name = "Wonder of the Drowned",
				ManaCost = 4,
				Subtypes = ImmutableHashSet.Create(
					StringComparer.OrdinalIgnoreCase,
					Hollowmere.Spirit
				),
				Components = ImmutableArray.Create<GameComponent>(
					new PermanentComponent(),
					new CreatureComponent { Power = 2, Toughness = 2 },
					new StaticGrantKeywordAbility
					{
						GrantsFlying = true,
						ActiveInZone = ZoneType.Graveyard,
						Filter = new IsOnBattlefieldSpecification()
							.And(new IsCreatureSpecification())
							.And(new IsControlledByYouSpecification()),
					}
				),
			},
			// Reanimation at a rate that lets it target the set's 6-8 drops.
			CardFactory
				.Spell("Ghoulcaller's Bargain", manaCost: 3)
				.WithReanimate()
				.WithFlashback(6)
				.Build(),
			// Cheap self-mill plus a body later. Two cards' worth of value in one slot.
			CardFactory.Spell("Grim Excavation", manaCost: 1).WithMill(4).WithFlashback(3).Build(),
			// Threshold on a defensive body. Taunt matters — without it a big butt does nothing.
			CardFactory
				.Creature("Cairn Warden", manaCost: 3, power: 1, toughness: 4)
				.WithSubtype(Hollowmere.Spirit)
				.WithTaunt()
				.WithThreshold(power: 4, toughness: 2)
				.Build(),
			// Recursion engine. Returning to hand rather than the battlefield keeps it fair.
			CardFactory
				.Creature("Sexton of the Drowned Chapel", manaCost: 4, power: 3, toughness: 3)
				.WithSubtype(Hollowmere.Human)
				.WithSubtype(Hollowmere.Cleric)
				.WithEtbTrigger("Exhume the Faithful", eb => eb.WithReturnCreatureFromGraveyard())
				.Build(),
			// Payoff for a full graveyard that also fills it. Draws two at threshold.
			CardFactory
				.Spell("Rites of the Sunken Choir", manaCost: 3)
				.WithMill(3)
				.WithDraw(2)
				.Build(),
			// A 1-drop that is never dead: mills early, becomes a threat late.
			CardFactory
				.Creature("Drowned Acolyte", manaCost: 1, power: 1, toughness: 2)
				.WithSubtype(Hollowmere.Human)
				.WithSubtype(Hollowmere.Cleric)
				.WithEtbTrigger("Wade In", eb => eb.WithMill(2))
				.WithThreshold(power: 2, toughness: 1)
				.Build(),
			// ===== BATCH 2 =====
			// One-drop threshold. Trades up all game once the graveyard is stocked.
			CardFactory
				.Creature("Cryptside Scavenger", manaCost: 1, power: 1, toughness: 1)
				.WithSubtype(Hollowmere.Human)
				.WithSubtype(Hollowmere.Rogue)
				.WithThreshold(power: 2, toughness: 2)
				.Build(),
			// Cheap recursion that comes back for a second creature.
			CardFactory
				.Spell("Ashen Rite", manaCost: 1)
				.WithReturnCreatureFromGraveyard()
				.WithFlashback(3)
				.Build(),
			// Efficient reanimation with no flashback — the cheap, one-shot version.
			CardFactory.Spell("Exhume the Mere", manaCost: 2).WithReanimate().Build(),
			// Threshold plus trample: the graveyard deck's way through a Taunt wall.
			CardFactory
				.Creature("Mire Prowler", manaCost: 2, power: 2, toughness: 2)
				.WithSubtype(Hollowmere.Zombie)
				.WithThreshold(power: 2, toughness: 2, trample: true)
				.Build(),
			// Self-mill plus recursion in one card — the theme's engine at two mana.
			CardFactory
				.Spell("Corpse Harvest", manaCost: 2)
				.WithMill(3)
				.WithReturnCreatureFromGraveyard()
				.Build(),
			// Recurs itself to hand, so it is never truly answered by removal.
			CardFactory
				.Creature("Silt-Choked Wanderer", manaCost: 2, power: 2, toughness: 1)
				.WithSubtype(Hollowmere.Spirit)
				.WithDeathTrigger(
					"Drift Back",
					eb =>
						eb.WithAction(
							new ReturnToHandAction { TargetContextKey = ContextKeys.SourceCardId },
							TargetingStrategy.NoTarget()
						)
				)
				.Build(),
			// Bulk self-mill with a second use. Capped at 5 — see the class header.
			CardFactory.Spell("Unhallowed Rite", manaCost: 2).WithMill(5).WithFlashback(4).Build(),
			// A wall that stops being a wall. Taunt is the only defensive tool here.
			CardFactory
				.Creature("Cairnwatch Sentinel", manaCost: 2, power: 0, toughness: 4)
				.WithSubtype(Hollowmere.Spirit)
				.WithTaunt()
				.WithThreshold(power: 4, toughness: 0)
				.Build(),
			// Recurring body: dies, comes back, dies again. The set's sacrifice fodder.
			CardFactory
				.Creature("Bone Shambler", manaCost: 3, power: 2, toughness: 2)
				.WithSubtype(Hollowmere.Zombie)
				.WithGraveyardRecursion(manaCost: 3)
				.Build(),
			// Anger: haste from the graveyard. The second zone-dependent static in the set,
			// and the one that makes reanimated fatties attack the turn they arrive.
			new()
			{
				Name = "Fury of the Mere",
				ManaCost = 4,
				Subtypes = ImmutableHashSet.Create(
					StringComparer.OrdinalIgnoreCase,
					Hollowmere.Horror
				),
				Components = ImmutableArray.Create<GameComponent>(
					new PermanentComponent(),
					new CreatureComponent { Power = 3, Toughness = 3 },
					new StaticGrantKeywordAbility
					{
						GrantsHaste = true,
						ActiveInZone = ZoneType.Graveyard,
						Filter = new IsOnBattlefieldSpecification()
							.And(new IsCreatureSpecification())
							.And(new IsControlledByYouSpecification()),
					}
				),
			},
			// A graveyard-active lord: Spirits get bigger while this sits in the yard.
			new()
			{
				Name = "Dirge-Singer of the Mere",
				ManaCost = 3,
				Subtypes = ImmutableHashSet.Create(
					StringComparer.OrdinalIgnoreCase,
					Hollowmere.Spirit
				),
				Components = ImmutableArray.Create<GameComponent>(
					new PermanentComponent(),
					new CreatureComponent { Power = 2, Toughness = 2 },
					new StaticPTBoostAbility
					{
						PowerBonus = 1,
						ToughnessBonus = 1,
						ActiveInZone = ZoneType.Graveyard,
						Filter = new IsSubtypeSpecification { Subtype = Hollowmere.Spirit }
							.And(new IsOnBattlefieldSpecification())
							.And(new IsControlledByYouSpecification()),
					}
				),
			},
			// Repeatable recursion on a body — a mana sink that never runs out of gas.
			CardFactory
				.Creature("Hollowmere Necromancer", manaCost: 3, power: 2, toughness: 2)
				.WithSubtype(Hollowmere.Human)
				.WithSubtype(Hollowmere.Wizard)
				.WithActivatedAbility(
					"Call the Drowned",
					manaCost: 3,
					effect: eb => eb.WithReturnCreatureFromGraveyard()
				)
				.Build(),
			// Removal that also advances the plan — the two-for-one bar for three mana.
			CardFactory
				.Spell("Rite of Rot", manaCost: 3)
				.WithDestroy()
				.WithTarget(Single().OpponentCreatures())
				.WithMill(3)
				.Build(),
			// Mills on arrival and grows into a threat. Enabler and payoff in one slot.
			CardFactory
				.Creature("Sepulchral Warden", manaCost: 3, power: 2, toughness: 3)
				.WithSubtype(Hollowmere.Spirit)
				.WithEtbTrigger("Silt the Vault", eb => eb.WithMill(3))
				.WithThreshold(power: 2, toughness: 2)
				.Build(),
			// Reanimation that attacks immediately, which is what makes it lethal rather
			// than merely large in a format with no blockers.
			CardFactory
				.Spell("Second Burial", manaCost: 3)
				.WithReanimate()
				.WithGrantKeyword(haste: true)
				.Build(),
			// Card advantage stapled to the enabler. The Graveyard deck's draw spell.
			CardFactory
				.Spell("Whispers from the Mere", manaCost: 4)
				.WithDraw(3)
				.WithMill(3)
				.Build(),
			// Graveyard hate on a body — maindeckable because everyone uses their yard.
			CardFactory
				.Creature("Graveyard Trespasser", manaCost: 4, power: 3, toughness: 3)
				.WithSubtype(Hollowmere.Werewolf)
				.WithEtbTrigger("Desecrate", eb => eb.WithExileFromGraveyard().WithLifeGain(2))
				.Build(),
			// Big threshold body with evasion — the top of the theme's own curve.
			CardFactory
				.Creature("Mausoleum Warden", manaCost: 5, power: 3, toughness: 5)
				.WithSubtype(Hollowmere.Spirit)
				.WithTaunt()
				.WithThreshold(power: 2, toughness: 2, lifelink: true)
				.Build(),
			// The premium reanimation target: huge, tramples, and refills the yard on arrival.
			CardFactory
				.Creature("Sepulchral Leviathan", manaCost: 6, power: 6, toughness: 6)
				.WithSubtype(Hollowmere.Horror)
				.WithTrample()
				.WithEtbTrigger("Silt Surge", eb => eb.WithMill(5))
				.Build(),
			// Returns two creatures on resolution — the 7-drop bar is "wins if unanswered".
			CardFactory
				.Creature("Choir of the Drowned", manaCost: 7, power: 5, toughness: 5)
				.WithSubtype(Hollowmere.Spirit)
				.WithFlying()
				.WithEtbTrigger(
					"Raise the Choir",
					eb =>
						eb.WithReturnCreatureFromGraveyard()
							.WithCreateTokens(HollowmereTokens.Spirit(), count: 2)
				)
				.Build(),
			// Repeatable mill on a cheap body — the safe way to build a deep graveyard
			// without the decking risk of a large one-shot self-mill.
			CardFactory
				.Creature("Silt-Sifter", manaCost: 2, power: 1, toughness: 3)
				.WithSubtype(Hollowmere.Human)
				.WithSubtype(Hollowmere.Wizard)
				.WithActivatedAbility("Sift the Silt", manaCost: 1, effect: eb => eb.WithMill(2))
				.Build(),
			// Removal priced off the graveyard: cheap once the yard is stocked.
			CardFactory
				.Spell("Drag Under", manaCost: 3)
				.WithDamage(3)
				.WithTarget(Single().OpponentCreatures())
				.WithFlashback(5)
				.Build(),
			// Lifegain that scales with the theme, keeping the aggressive decks honest.
			CardFactory
				.Creature("Mere-Tender Acolyte", manaCost: 2, power: 1, toughness: 3)
				.WithSubtype(Hollowmere.Human)
				.WithSubtype(Hollowmere.Cleric)
				.WithLifelink()
				.WithThreshold(power: 2, toughness: 1)
				.Build(),
			// Mass reanimation at the very top. Deliberately the set's only one.
			CardFactory
				.Spell("The Mere Gives Up Its Dead", manaCost: 6)
				.WithAction(new PutIntoBattlefieldAction(), AllValid().CreaturesInYourGraveyard())
				.Build(),
			// A cantrip that turns on threshold and finds the card you need.
			CardFactory
				.Spell("Consult the Drowned", manaCost: 2)
				.WithMill(3)
				.WithDraw(1)
				.WithFlashback(4)
				.Build(),
			// ===== BATCH 3 =====
			// Threshold at the top of the curve, where the payoff should be largest.
			CardFactory
				.Creature("Silt-Wreathed Colossus", manaCost: 5, power: 4, toughness: 4)
				.WithSubtype(Hollowmere.Horror)
				.WithThreshold(power: 4, toughness: 4, trample: true)
				.Build(),
			// A recurring flier — the graveyard deck's evasive clock.
			CardFactory
				.Creature("Drowned Revenant", manaCost: 4, power: 3, toughness: 2)
				.WithSubtype(Hollowmere.Spirit)
				.WithFlying()
				.WithGraveyardRecursion(manaCost: 5)
				.Build(),
			// Cheap interaction that also stocks the yard.
			CardFactory
				.Spell("Silt in the Lungs", manaCost: 1)
				.WithDamage(2)
				.WithTarget(Single().OpponentCreatures())
				.WithMill(2)
				.Build(),
			// The graveyard deck's card-advantage engine on a stick.
			CardFactory
				.Creature("Keeper of the Ossuary", manaCost: 4, power: 2, toughness: 4)
				.WithSubtype(Hollowmere.Human)
				.WithSubtype(Hollowmere.Cleric)
				.WithTaunt()
				.WithTriggeredAbility(
					"Tally the Bones",
					TriggerConditions.OnYourUpkeep(),
					eb => eb.WithMill(1).WithLifeGain(1)
				)
				.Build(),
			// Sacrifice outlet: converts a dying board into graveyard depth and reach.
			CardFactory
				.Creature("Ossuary Warden", manaCost: 3, power: 2, toughness: 3)
				.WithSubtype(Hollowmere.Zombie)
				.WithActivatedAbility(
					"Feed the Ossuary",
					manaCost: 0,
					effect: eb => eb.WithDraw(1).WithLoseLife(1),
					costs: cb => cb.Sacrifice(TargetSpecification.CreatureControlledByYou())
				)
				.Build(),
			// Punishes the opponent's graveyard while filling yours.
			CardFactory
				.Spell("Silt the Vaults", manaCost: 3)
				.WithExileFromGraveyard()
				.WithMill(3)
				.WithDraw(1)
				.Build(),
			// Reanimation that dodges sorcery-speed removal by rebuilding immediately.
			CardFactory
				.Spell("Dredge the Deep Mere", manaCost: 5)
				.WithReanimate()
				.WithCreateTokens(HollowmereTokens.Spirit(), count: 2)
				.Build(),
			// A threshold one-drop with evasion — relevant on turn one and turn twelve.
			CardFactory
				.Creature("Mere-Wisp", manaCost: 1, power: 1, toughness: 1)
				.WithSubtype(Hollowmere.Spirit)
				.WithFlying()
				.WithThreshold(power: 2, toughness: 1)
				.Build(),
			// ===== BATCH 4 =====
			// Reanimation on a body — the effect the whole top-end is built around.
			CardFactory
				.Creature("Hollowmere Gravecaller", manaCost: 5, power: 3, toughness: 3)
				.WithSubtype(Hollowmere.Human)
				.WithSubtype(Hollowmere.Wizard)
				.WithEtbTrigger("Call Them Up", eb => eb.WithReanimate())
				.Build(),
			// A cheap threshold flier for the aggressive graveyard decks.
			CardFactory
				.Creature("Silt-Veiled Shade", manaCost: 2, power: 1, toughness: 2)
				.WithSubtype(Hollowmere.Spirit)
				.WithFlying()
				.WithThreshold(power: 2, toughness: 2)
				.Build(),
			// Sacrifice plus recursion: converts a board into a rebuilt one.
			CardFactory
				.Spell("Rite of Second Drowning", manaCost: 3)
				.WithSacrificeCost(TargetSpecification.CreatureControlledByYou())
				.WithReanimate()
				.WithDraw(1)
				.Build(),
			// Punishes the mirror while advancing your own plan.
			CardFactory
				.Creature("Ossuary Scavenger", manaCost: 3, power: 3, toughness: 2)
				.WithSubtype(Hollowmere.Zombie)
				.WithEtbTrigger("Pick the Bones", eb => eb.WithExileFromGraveyard().WithMill(2))
				.Build(),
			// Recurring lifelink — the graveyard deck's answer to an aggressive start.
			CardFactory
				.Creature("Mere-Drowned Penitent", manaCost: 3, power: 2, toughness: 2)
				.WithSubtype(Hollowmere.Spirit)
				.WithLifelink()
				.WithGraveyardRecursion(manaCost: 4)
				.Build(),
		];
}
