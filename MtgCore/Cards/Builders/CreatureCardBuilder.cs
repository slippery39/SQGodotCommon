using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore.Cards.Builders;

/// <summary>
/// Fluent builder for creature cards. Produces a Card with PermanentComponent +
/// CreatureComponent, plus any abilities added via the With* methods.
/// </summary>
public class CreatureCardBuilder
{
	private readonly string _name;
	private readonly int _manaCost;
	private readonly int _power;
	private readonly int _toughness;

	private readonly List<string> _subtypes = new();
	private readonly List<AdditionalCost> _castCosts = new();
	private readonly List<GameComponent> _extraComponents = new();

	private bool _hasHaste;
	private bool _hasFlying;
	private bool _hasTaunt;
	private bool _hasReach;
	private bool _hasLifelink;
	private bool _hasTrample;
	private bool _hasDoubleStrike;
	private bool _hasDeathtouch;
	private CardType _extraTypes = CardType.None;
	private bool _hasFirstStrike;
	private bool _hasIndestructible;
	private bool _hasShroud;
	private bool _hasHexproof;

	internal CreatureCardBuilder(string name, int manaCost, int power, int toughness)
	{
		_name = name;
		_manaCost = manaCost;
		_power = power;
		_toughness = toughness;
	}

	// ===== SUBTYPES =====

	public CreatureCardBuilder WithSubtype(string subtype)
	{
		_subtypes.Add(subtype);
		return this;
	}

	// ===== CAST COSTS =====

	public CreatureCardBuilder WithSacrificeSubtypeCost(string subtype, int count = 1)
	{
		_castCosts.Add(
			new SacrificeAdditionalCost
			{
				Filter = new IsSubtypeSpecification { Subtype = subtype },
				Count = count,
			}
		);
		return this;
	}

	// ===== KEYWORDS =====

	public CreatureCardBuilder WithHaste()
	{
		_hasHaste = true;
		return this;
	}

	public CreatureCardBuilder WithFlying()
	{
		_hasFlying = true;
		return this;
	}

	public CreatureCardBuilder WithTaunt()
	{
		_hasTaunt = true;
		return this;
	}

	public CreatureCardBuilder WithReach()
	{
		_hasReach = true;
		return this;
	}

	public CreatureCardBuilder WithLifelink()
	{
		_hasLifelink = true;
		return this;
	}

	public CreatureCardBuilder WithTrample()
	{
		_hasTrample = true;
		return this;
	}

	public CreatureCardBuilder WithDoubleStrike()
	{
		_hasDoubleStrike = true;
		return this;
	}

	public CreatureCardBuilder WithDeathtouch()
	{
		_hasDeathtouch = true;
		return this;
	}

	/// <summary>
	/// First strike — deals combat damage before creatures without it, so an attack into a
	/// creature it can kill takes no damage back. Strong in this engine's no-blocker combat;
	/// see AttackAction.ApplyCreatureVsCreature.
	/// </summary>
	public CreatureCardBuilder WithFirstStrike()
	{
		_hasFirstStrike = true;
		return this;
	}

	/// <summary>Damage and "destroy" effects do not kill it. Zero toughness still does.</summary>
	public CreatureCardBuilder WithIndestructible()
	{
		_hasIndestructible = true;
		return this;
	}

	public CreatureCardBuilder WithShroud()
	{
		_hasShroud = true;
		return this;
	}

	public CreatureCardBuilder WithHexproof()
	{
		_hasHexproof = true;
		return this;
	}

	/// <summary>
	/// Exalted — "whenever a creature you control attacks alone, that creature gets +1/+1 until
	/// end of turn." Instances stack; pass count &gt; 1 for a card with multiple.
	/// </summary>
	public CreatureCardBuilder WithExalted(int count = 1)
	{
		_extraComponents.Add(new ExaltedComponent { Count = count });
		return this;
	}

	/// <summary>
	/// Protection from one or more creature types. Colour protection is not possible — this
	/// engine has no colours — so subtype protection is the whole of protection here.
	/// </summary>
	public CreatureCardBuilder WithProtectionFrom(params string[] subtypes)
	{
		_extraComponents.Add(
			new ProtectionFromSubtypeComponent
			{
				Subtypes = subtypes.ToImmutableHashSet(StringComparer.OrdinalIgnoreCase),
			}
		);
		return this;
	}

	/// <summary>
	/// "As long as your life total is at least <paramref name="minimum"/>, this gets +X/+Y."
	/// Stamped Permanent so StartTurnAction's cleanup does not strip it.
	/// </summary>
	public CreatureCardBuilder WithLifeTotalBonus(int power, int toughness, int minimum = 25)
	{
		_extraComponents.Add(
			new LifeTotalComponent
			{
				Minimum = minimum,
				PowerBonus = power,
				ToughnessBonus = toughness,
				Duration = ModifierDuration.Permanent,
			}
		);
		return this;
	}

	/// <summary>
	/// P/T equal to the number of creatures you control — the */* templating. Build the card
	/// with base power/toughness 0 and add this.
	/// </summary>
	public CreatureCardBuilder WithPowerEqualToCreatureCount(
		int powerPer = 1,
		int toughnessPer = 1,
		bool countsSelf = true
	)
	{
		_extraComponents.Add(
			new CreatureCountComponent
			{
				PowerPerCreature = powerPer,
				ToughnessPerCreature = toughnessPer,
				CountsSelf = countsSelf,
				Duration = ModifierDuration.Permanent,
			}
		);
		return this;
	}

	/// <summary>"Noncreature spells cost {amount} more to cast" — taxes both players.</summary>
	public CreatureCardBuilder WithSpellTax(int amount = 1, bool nonCreatureOnly = true)
	{
		_extraComponents.Add(
			new SpellTaxComponent { Amount = amount, NonCreatureOnly = nonCreatureOnly }
		);
		return this;
	}

	/// <summary>
	/// "If you would gain life, gain that much plus <paramref name="amount"/> instead."
	/// A replacement effect, not a trigger — a trigger version would loop forever.
	/// </summary>
	public CreatureCardBuilder WithLifeGainBonus(int amount = 1)
	{
		_extraComponents.Add(new LifeGainBonusComponent { Amount = amount });
		return this;
	}

	/// <summary>Restricts when this card may be cast at all (e.g. Serra Avenger).</summary>
	public CreatureCardBuilder WithCastRestriction(CastRestrictionComponent restriction)
	{
		_extraComponents.Add(restriction);
		return this;
	}

	/// <summary>
	/// "This spell costs {amount} less to cast if …" — Stormwing Entity. Applied by CostEngine
	/// alongside affinity and convoke, so the reductions compose rather than fight.
	/// </summary>
	public CreatureCardBuilder WithCostReduction(int amount, ActivationCondition condition)
	{
		_extraComponents.Add(
			new ConditionalCostReductionComponent { Amount = amount, Condition = condition }
		);
		return this;
	}

	/// <summary>
	/// Threshold — while the controller's graveyard holds at least <paramref name="minimum"/>
	/// cards, this creature gets the given bonus. Stamped Permanent so StartTurnAction's
	/// end-of-turn cleanup does not strip it.
	/// </summary>
	public CreatureCardBuilder WithThreshold(
		int power,
		int toughness,
		int minimum = 7,
		bool flying = false,
		bool taunt = false,
		bool lifelink = false,
		bool trample = false,
		bool deathtouch = false
	)
	{
		_extraComponents.Add(
			new ThresholdComponent
			{
				Minimum = minimum,
				PowerBonus = power,
				ToughnessBonus = toughness,
				GrantsFlying = flying,
				GrantsTaunt = taunt,
				GrantsLifelink = lifelink,
				GrantsTrample = trample,
				GrantsDeathtouch = deathtouch,
				Duration = ModifierDuration.Permanent,
			}
		);
		return this;
	}

	/// <summary>
	/// Graveyard recursion (Gravecrawler / unearth): castable from the graveyard for
	/// <paramref name="manaCost"/>. Unlike spell flashback the creature is not exiled
	/// afterwards, so it can be recurred every time it dies.
	/// </summary>
	public CreatureCardBuilder WithGraveyardRecursion(int manaCost)
	{
		_extraComponents.Add(new FlashbackComponent { FlashbackManaCost = manaCost });
		return this;
	}

	// ===== ABILITIES =====

	public CreatureCardBuilder WithActivatedAbility(
		string name,
		int manaCost,
		Action<SpellCardBuilder> effect,
		Action<CreatureCostBuilder>? costs = null,
		ActivationCondition? condition = null,
		bool requiresTap = false,
		int maxPerTurn = 1
	)
	{
		var effectBuilder = new SpellCardBuilder("_", 0);
		effect(effectBuilder);

		var costBuilder = new CreatureCostBuilder();
		costs?.Invoke(costBuilder);

		var effects = effectBuilder.BuildEffects();
		if (effects.Count == 0)
			throw new InvalidOperationException($"Activated ability '{name}' has no effect.");

		_extraComponents.Add(
			new ActivatedAbilityComponent
			{
				Name = name,
				ManaCost = manaCost,
				AdditionalCosts = costBuilder.Build(),
				Effects = effects,
				Condition = condition,
				RequiresTap = requiresTap,
				MaxActivationsPerTurn = maxPerTurn,
			}
		);
		return this;
	}

	/// <summary>
	/// Shorthand for WithTriggeredAbility using OnSelfEntersBattlefield() as the condition.
	/// Use this for "when ~ enters the battlefield" abilities.
	/// </summary>
	public CreatureCardBuilder WithEtbTrigger(string name, Action<SpellCardBuilder> effect) =>
		WithTriggeredAbility(name, TriggerConditions.OnSelfEntersBattlefield(), effect);

	/// <summary>
	/// "When this dies, ...". Sets ActiveInZone = Graveyard, which is mandatory for death
	/// triggers: by the time CheckStateBasedEffectsAction scans for triggers the card has
	/// already moved to the graveyard, so a Battlefield-scoped trigger silently never fires.
	/// </summary>
	public CreatureCardBuilder WithDeathTrigger(string name, Action<SpellCardBuilder> effect) =>
		WithTriggeredAbility(
			name,
			TriggerConditions.OnSelfDies(),
			effect,
			ActiveInZone: ZoneType.Graveyard
		);

	/// <param name="maxTriggers">
	/// Lifetime firing cap; 0 = unlimited. Use 1 for renown ("if it isn't renowned").
	/// </param>
	/// <param name="maxPerTurn">
	/// Per-turn firing cap; 0 = unlimited. Independent of <paramref name="maxTriggers"/> —
	/// "once each turn" and "once ever" are different card text.
	/// </param>
	public CreatureCardBuilder WithTriggeredAbility(
		string name,
		TriggerCondition condition,
		Action<SpellCardBuilder> effect,
		ZoneType ActiveInZone = ZoneType.Battlefield,
		int maxTriggers = 0,
		int maxPerTurn = 0
	)
	{
		var effectBuilder = new SpellCardBuilder("_", 0);
		effect(effectBuilder);

		var effects = effectBuilder.BuildEffects();
		if (effects.Count == 0)
			throw new InvalidOperationException($"Triggered ability '{name}' has no effect.");

		_extraComponents.Add(
			new TriggeredAbilityComponent
			{
				Name = name,
				Condition = condition,
				ActiveInZone = ActiveInZone,
				// A user-select strategy inside a trigger resolves to no targets and the effect
				// silently does nothing — see TriggerTargeting.
				Effects = TriggerTargeting.MakeResolvable(effects),
				MaxTriggers = maxTriggers,
				MaxTriggersPerTurn = maxPerTurn,
			}
		);
		return this;
	}

	/// <summary>
	/// Renown N — "when this deals combat damage to a player, if it isn't renowned, put N
	/// +1/+1 counters on it and it becomes renowned."
	///
	/// "Isn't renowned" is MaxTriggers = 1: a lifetime cap, not a per-turn one. The counters are
	/// a permanent P/T modifier, which is what a +1/+1 counter is in this engine.
	/// </summary>
	public CreatureCardBuilder WithRenown(int amount = 1) =>
		WithTriggeredAbility(
			$"Renown {amount}",
			TriggerConditions.OnSelfDealsCombatDamageToPlayer(),
			eb =>
				eb.WithAction(
					new AddModifierAction
					{
						PowerBonus = amount,
						ToughnessBonus = amount,
						Duration = ModifierDuration.Permanent,
						TargetContextKey = ContextKeys.SourceCardId,
					},
					TargetingStrategy.NoTarget()
				),
			maxTriggers: 1
		);

	public CreatureCardBuilder WithComponent(GameComponent component)
	{
		_extraComponents.Add(component);
		return this;
	}

	/// <summary>
	/// Adds types beyond Creature — for artifact creatures and enchantment creatures.
	/// Creature is always included; this is additive.
	/// </summary>
	public CreatureCardBuilder WithTypes(CardType types)
	{
		_extraTypes |= types;
		return this;
	}

	// ===== BUILD =====

	public Card Build()
	{
		var creature = new CreatureComponent
		{
			Power = _power,
			Toughness = _toughness,
			HasHaste = _hasHaste,
			HasFlying = _hasFlying,
			HasTaunt = _hasTaunt,
			HasReach = _hasReach,
			HasLifelink = _hasLifelink,
			HasTrample = _hasTrample,
			HasDoubleStrike = _hasDoubleStrike,
			HasDeathtouch = _hasDeathtouch,
			HasFirstStrike = _hasFirstStrike,
			HasIndestructible = _hasIndestructible,
			HasShroud = _hasShroud,
			HasHexproof = _hasHexproof,
		};

		var components = ImmutableArray
			.Create<GameComponent>(new PermanentComponent(), creature)
			.AddRange(_extraComponents);

		return new Card
		{
			Name = _name,
			ManaCost = _manaCost,
			Types = CardType.Creature | _extraTypes,
			AdditionalCastCosts = _castCosts.ToImmutableList(),
			Subtypes = _subtypes.ToImmutableHashSet(StringComparer.OrdinalIgnoreCase),
			Components = components,
		};
	}
}

/// <summary>
/// Builds the AdditionalCost list for an activated ability.
/// </summary>
public class CreatureCostBuilder
{
	private readonly List<AdditionalCost> _costs = new();

	public CreatureCostBuilder SacrificeSubtype(string subtype, int count = 1)
	{
		_costs.Add(
			new SacrificeAdditionalCost
			{
				Filter = new IsSubtypeSpecification { Subtype = subtype },
				Count = count,
			}
		);
		return this;
	}

	public CreatureCostBuilder Sacrifice(TargetSpecification? filter = null, int count = 1)
	{
		_costs.Add(new SacrificeAdditionalCost { Filter = filter, Count = count });
		return this;
	}

	/// <summary>
	/// "Sacrifice this creature" as part of the cost — Hanged Executioner, Lena.
	/// IsSourceCardSpecification restricts the payment to the card owning the ability.
	/// </summary>
	public CreatureCostBuilder SacrificeSelf()
	{
		_costs.Add(new SacrificeAdditionalCost { Filter = new IsSourceCardSpecification() });
		return this;
	}

	/// <summary>"Discard a card" as part of the cost — Seasoned Hallowblade.</summary>
	public CreatureCostBuilder Discard(int count = 1)
	{
		_costs.Add(new DiscardAdditionalCost { Count = count });
		return this;
	}

	internal ImmutableList<AdditionalCost> Build() => _costs.ToImmutableList();
}
