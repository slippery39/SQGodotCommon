using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore.Cards.Builders;
using static MtgCore.Cards.Builders.TargetBuilder;

namespace MtgCore;

/// <summary>
/// White enchantments, equipment and planeswalkers from the Core Set Cube.
/// https://cubecobra.com/cube/list/magiccoreset20xx
///
/// AURAS ride the equipment attachment rails (EquipmentComponent.IsAura) rather than a parallel
/// system. They attach via an ETB trigger instead of at cast time; with no priority window
/// between casting and resolving, the two are observationally identical here.
///
/// PLANESWALKERS use PlaneswalkerComponent + loyalty abilities. The once-per-turn limit is per
/// WALKER, shared across all of its abilities, which is why it lives on the component rather
/// than on each ability.
///
/// DIVERGENCES beyond the colour and combat-timing ones listed in CoresetCubeWhiteSpells:
///   - Path of Bravery's anthem is unconditional. StaticAbilityEngine is a push model that only
///     re-stamps on ETB/LTB, so an anthem gated on a life total would go stale the moment
///     anyone took damage. Noted in DesignNotes.md.
///   - "Prevent all but 1 damage" emblems become flat prevention; damage has no per-source
///     identity to except.
/// </summary>
public static class CoresetCubeWhitePermanents
{
	public static IReadOnlyList<Card> Cards { get; } =
		[
			// ===== ENCHANTMENTS =====

			// The archetypal aura. 0/0 bonuses — its whole text is the can't-attack flag, which
			// is deliberately NOT permanent exhaustion: exhaustion clears every turn.
			CardFactory
				.Enchantment("Pacifism", manaCost: 2)
				.AsAura(
					preventsAttacking: true,
					targeting: TargetingStrategy.SingleTarget(
						TargetSpecification.OpponentCreatures()
					)
				)
				.Build(),
			// The "you may pay {W}" is dropped — there is no optional-cost prompt on a trigger —
			// so the token is free but capped at once per turn to keep the rate honest.
			CardFactory
				.Enchantment("Spirit Bonds", manaCost: 2)
				.WithTriggeredAbility(
					"Bind Spirit",
					TriggerConditions.OnCreatureYouControlEnters(),
					eb => eb.WithCreateTokens(CoresetCubeTokens.Spirit()),
					maxPerTurn: 1
				)
				.WithActivatedAbility(
					"Spirit Ward",
					manaCost: 2,
					effect: eb =>
						eb.WithGrantKeyword(indestructible: true)
							.WithTarget(Single().YourCreatures()),
					costs: c => c.SacrificeSubtype("Spirit"),
					maxPerTurn: 0
				)
				.Build(),
			// "Protection from creatures" would mean untargetable and undamageable by every
			// creature. Hexproof covers the targeting half; the combat half is dropped, since
			// blanket damage immunity from all creatures has no expression here.
			CardFactory
				.Enchantment("Spirit Mantle", manaCost: 2)
				.AsAura(
					powerBonus: 1,
					toughnessBonus: 1,
					hexproof: true,
					targeting: TargetingStrategy.SingleTarget(
						TargetSpecification.CreatureControlledByYou()
					)
				)
				.Build(),
			// Linked exile: the Ring remembers what it exiled and hands it back when it leaves.
			// The return trigger MUST be graveyard-active — by the time the leave is scanned the
			// Ring has already moved, so a battlefield-scoped trigger never fires.
			CardFactory
				.Enchantment("Oblivion Ring", manaCost: 3)
				.WithEtbTrigger(
					"Banish",
					eb =>
						eb.WithAction(
							new ExileLinkedAction(),
							TargetingStrategy.SingleTarget(
								new IsNotCardTypeSpecification { Types = CardType.Land }
									.And(new IsOnBattlefieldSpecification())
									.And(new IsControlledByOpponentSpecification())
							)
						)
				)
				.WithTriggeredAbility(
					"Release",
					new EventTriggerCondition
					{
						EventTypeName = EventTypeNames.PermanentLeftBattlefield,
						Filter = new IsSourceCardSpecification(),
					},
					eb =>
						eb.WithAction(new ReturnLinkedExileAction(), TargetingStrategy.NoTarget()),
					activeInZone: ZoneType.Graveyard
				)
				.Build(),
			// The anthem is unconditional — see the class header. The attack trigger is real.
			CardFactory
				.Enchantment("Path of Bravery", manaCost: 3)
				.WithStaticBoost(1, 1, TargetSpecification.CreatureControlledByYou())
				.WithTriggeredAbility(
					"Rally the Faithful",
					TriggerConditions.OnAnyCreatureAttacks(),
					eb => eb.WithLifeGain(1).WithTarget(TargetingStrategy.Self())
				)
				.Build(),
			// A real aura with keyword grants, and it bounces back when its host dies.
			CardFactory
				.Enchantment("Angelic Destiny", manaCost: 4)
				.AsAura(
					powerBonus: 4,
					toughnessBonus: 4,
					flying: true,
					firstStrike: true,
					targeting: TargetingStrategy.SingleTarget(
						TargetSpecification.CreatureControlledByYou()
					)
				)
				.WithTriggeredAbility(
					"Destiny Returns",
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
			// "Activated abilities can't be activated" is dropped — there is no per-permanent
			// ability lock. The can't-attack half and the life gain are both real.
			CardFactory
				.Enchantment("Faith's Fetters", manaCost: 4)
				.AsAura(
					preventsAttacking: true,
					targeting: TargetingStrategy.SingleTarget(
						TargetSpecification.OpponentCreatures()
					)
				)
				.WithEtbTrigger(
					"Fetters",
					eb => eb.WithLifeGain(4).WithTarget(TargetingStrategy.Self())
				)
				.Build(),
			CardFactory
				.Enchantment("Glorious Anthem", manaCost: 3)
				.WithStaticBoost(1, 1, TargetSpecification.CreatureControlledByYou())
				.Build(),
			// ===== EQUIPMENT =====

			// Makes its own creature to hold it. The equip ability stays repeatable so it can be
			// moved once the Soldier dies.
			CardFactory
				.Artifact("Ancestral Blade", manaCost: 2)
				.WithComponent(new EquipmentComponent { PowerBonus = 1, ToughnessBonus = 1 })
				.WithEtbTrigger("Forge", eb => eb.WithCreateTokens(CoresetCubeTokens.Soldier()))
				.WithActivatedAbility(
					"Equip",
					manaCost: 1,
					effect: eb =>
						eb.WithAction(
							new AttachEquipmentAction(),
							TargetingStrategy.SingleTarget(
								TargetSpecification.CreatureControlledByYou()
							)
						),
					maxPerTurn: 0
				)
				.Build(),
			// ===== PLANESWALKERS =====

			CardFactory
				.Planeswalker("Ajani, Caller of the Pride", manaCost: 3)
				.WithLoyalty(4)
				.WithLoyaltyAbility(
					"+1: Put a +1/+1 counter on up to one target creature",
					1,
					eb =>
						eb.WithBoost(1, 1, ModifierDuration.Permanent)
							.WithTarget(Single().YourCreatures())
				)
				.WithLoyaltyAbility(
					"-3: Target creature gains flying and double strike until end of turn",
					-3,
					eb =>
						eb.WithGrantKeyword(flying: true, doubleStrike: true)
							.WithTarget(Single().YourCreatures())
				)
				// "X 2/2 Cats where X is your life total" would be ~20 bodies and hang the board.
				// Capped at 4, which is still a game-ending ultimate at this power level.
				.WithLoyaltyAbility(
					"-8: Create four 2/2 white Cat creature tokens",
					-8,
					eb => eb.WithCreateTokens(CoresetCubeTokens.Cat(), count: 4)
				)
				.Build(),
			CardFactory
				.Planeswalker("Ajani Steadfast", manaCost: 4)
				.WithLoyalty(4)
				.WithLoyaltyAbility(
					"+1: Up to one target creature gets +1/+1 and gains first strike and lifelink",
					1,
					eb =>
						eb.WithGrantKeyword(firstStrike: true, lifelink: true)
							.WithTarget(Single().YourCreatures())
				)
				.WithLoyaltyAbility(
					"-2: Put a +1/+1 counter on each creature you control",
					-2,
					eb =>
						eb.WithBoost(1, 1, ModifierDuration.Permanent)
							.WithTarget(AllValid().AllYourCreatures())
				)
				// The printed emblem prevents all but 1 damage from each source. Damage has no
				// per-source identity here, so this prevents 1 damage per instance instead.
				.WithLoyaltyAbility(
					"-7: You get an emblem that prevents damage",
					-7,
					eb =>
						eb.WithAction(
							new GrantEmblemAction
							{
								Emblem = new Emblem
								{
									Name = "Ajani's Aegis",
									Condition = TriggerConditions.OnYourUpkeep(),
									Effect = new CardEffect
									{
										TargetingStrategy = TargetingStrategy.Self(),
										ActionTemplate = new PreventDamageAction
										{
											PreventAll = false,
											Amount = 1,
										},
									},
								},
							},
							TargetingStrategy.Self()
						)
				)
				.Build(),
			CardFactory
				.Planeswalker("Gideon Jura", manaCost: 5)
				.WithLoyalty(6)
				// "Creatures attack Gideon if able" needs a forced-attack rule the engine has no
				// concept of. Reskinned to the defensive role: exhaust the opponent's board so
				// it cannot attack at all next turn.
				.WithLoyaltyAbility(
					"+2: Exhaust each creature an opponent controls",
					2,
					eb => eb.WithExhaust().WithTarget(AllValid().OpponentCreatures())
				)
				.WithLoyaltyAbility(
					"-2: Destroy target exhausted creature",
					-2,
					eb =>
						eb.WithAction(
							new DestroyPermanentAction(),
							TargetingStrategy.SingleTarget(
								TargetSpecification
									.OpponentCreatures()
									.And(new IsExhaustedSpecification())
							)
						)
				)
				// "Becomes a 6/6 creature that's still a planeswalker" needs a permanent to hold
				// both component sets and survive the swap. Reskinned to a damage burst, which is
				// what the ability does in practice.
				.WithLoyaltyAbility(
					"0: Deal 6 damage to target creature",
					0,
					eb => eb.WithDamage(6).WithTarget(Single().OpponentCreatures())
				)
				.Build(),
			CardFactory
				.Planeswalker("Basri Ket", manaCost: 3)
				.WithLoyalty(3)
				.WithLoyaltyAbility(
					"+1: Put a +1/+1 counter on up to one target creature; it gains indestructible",
					1,
					eb =>
						eb.WithBoost(1, 1, ModifierDuration.Permanent)
							.WithTarget(Single().YourCreatures())
							.WithGrantKeyword(indestructible: true)
							.WithTarget(Single().YourCreatures())
				)
				// The printed -2 makes tokens tapped AND attacking, which needs an attack that
				// can be joined mid-combat. Combat resolves instantly here, so the tokens simply
				// arrive ready to attack.
				.WithLoyaltyAbility(
					"-2: Create two 1/1 white Soldier creature tokens",
					-2,
					eb => eb.WithCreateTokens(CoresetCubeTokens.Soldier(), count: 2)
				)
				.WithLoyaltyAbility(
					"-6: You get an emblem that reinforces your army each turn",
					-6,
					eb =>
						eb.WithAction(
							new GrantEmblemAction
							{
								Emblem = new Emblem
								{
									Name = "Basri's Muster",
									Condition = TriggerConditions.OnYourUpkeep(),
									Effect = new CardEffect
									{
										TargetingStrategy = TargetingStrategy.NoTarget(),
										ActionTemplate = new CreateCardAction
										{
											CardTemplate = CoresetCubeTokens.Soldier(),
											Count = 1,
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
