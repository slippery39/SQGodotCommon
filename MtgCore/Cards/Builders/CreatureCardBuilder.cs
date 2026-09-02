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
	private int _coverTurns;
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

	/// <summary>
	/// COVER N — can't be attacked for N of your turns, or until it attacks. See
	/// CreatureComponent.CoverTurns. Use it to let a low-toughness utility creature live long
	/// enough to do its job, instead of over-statting it into a wall.
	/// </summary>
	public CreatureCardBuilder WithCover(int turns = 1)
	{
		_coverTurns = turns;
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
	/// <param name="subtype">
	/// Counts only this creature type — "+2/+0 for each other Goblin you control"
	/// (Goblin Piledriver). Empty counts every creature.
	/// </param>
	public CreatureCardBuilder WithPowerEqualToCreatureCount(
		int powerPer = 1,
		int toughnessPer = 1,
		bool countsSelf = true,
		string subtype = ""
	)
	{
		_extraComponents.Add(
			new CreatureCountComponent
			{
				PowerPerCreature = powerPer,
				ToughnessPerCreature = toughnessPer,
				CountsSelf = countsSelf,
				Subtype = subtype,
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
	/// A cost reduction this creature grants to OTHER spells you cast — Goreclaw's "creature
	/// spells you cast with power 4 or greater cost {2} less".
	///
	/// Distinct from WithCostReduction, which discounts the card it sits on. This one lives on a
	/// battlefield permanent and asks a question about the card being cast, which is why it takes
	/// a TargetSpecification rather than an ActivationCondition.
	/// </summary>
	public CreatureCardBuilder WithCostReductionFor(int amount, TargetSpecification appliesTo)
	{
		_extraComponents.Add(
			new ConditionalCostReductionComponent { Amount = amount, AppliesTo = appliesTo }
		);
		return this;
	}

	/// <summary>
	/// "This creature enters with N +1/+1 counters on it."
	///
	/// Pass fromXValue: true for an {X} Hydra — the counters become the X paid to cast it, so the
	/// card is built at base 0/0 and X is its whole body. That requires XCostComponent as well;
	/// use WithXCost() alongside this.
	/// </summary>
	public CreatureCardBuilder WithEntersWithCounters(int count = 1, bool fromXValue = false)
	{
		_extraComponents.Add(
			new EntersWithCountersComponent { Count = count, FromXValue = fromXValue }
		);
		return this;
	}

	/// <summary>
	/// An {X} cost on a creature spell. The chosen X lives on CastCreatureAction, not on the card,
	/// so two copies can be cast for different X.
	/// </summary>
	public CreatureCardBuilder WithXCost(int multiplier = 1)
	{
		_extraComponents.Add(new XCostComponent { Multiplier = multiplier });
		return this;
	}

	/// <summary>
	/// A keyword granted while this creature has at least <paramref name="minimum"/> +1/+1
	/// counters on it — Primordial Hydra's "has trample as long as it has ten or more".
	///
	/// Counted live at read time, not stamped: StaticAbilityEngine is a push model that re-stamps
	/// only on ETB/LTB, so a counter-gated keyword would go stale the moment a counter was added.
	/// </summary>
	public CreatureCardBuilder WithCounterThreshold(
		int minimum,
		bool trample = false,
		bool flying = false,
		bool taunt = false,
		bool lifelink = false,
		bool deathtouch = false
	)
	{
		_extraComponents.Add(
			new ThresholdComponent
			{
				CountSource = ThresholdSource.PlusOneCounters,
				Minimum = minimum,
				PowerBonus = 0,
				ToughnessBonus = 0,
				GrantsTrample = trample,
				GrantsFlying = flying,
				GrantsTaunt = taunt,
				GrantsLifelink = lifelink,
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
			CoverTurns = _coverTurns,
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

	/// <summary>
	/// "Discard a card" as part of the cost — Seasoned Hallowblade. Pass a subtype for
	/// "discard a land card" (Molten Vortex).
	/// </summary>
	public CreatureCostBuilder Discard(int count = 1, string subtype = "")
	{
		_costs.Add(SpellCardBuilder.DiscardCost(count, subtype));
		return this;
	}

	/// <summary>
	/// "Remove a +1/+1 counter from this creature" as part of the cost — Walking Ballista.
	///
	/// Distinct from <see cref="RemoveCounterAdditionalCost"/>, which spends CHARGE counters. See
	/// <see cref="RemovePlusOneCounterAdditionalCost"/> for why the two must not be merged, and for
	/// why this has to be a cost rather than an effect.
	/// </summary>
	public CreatureCostBuilder RemovePlusOneCounter(int count = 1)
	{
		_costs.Add(new RemovePlusOneCounterAdditionalCost { Count = count });
		return this;
	}

	/// <summary>
	/// "Exile N cards from your graveyard" as part of the cost — Grim Lavamancer. What makes a
	/// repeatable ability self-limiting: it consumes a finite resource, so the loop has a floor
	/// and graveyard hate is live against it.
	/// </summary>
	public CreatureCostBuilder ExileFromGraveyard(int count = 1, TargetSpecification? filter = null)
	{
		_costs.Add(new ExileFromGraveyardAdditionalCost { Count = count, Filter = filter });
		return this;
	}

	/// <summary>
	/// "Pay N life" as part of the cost — Vilis, Cruel Sadist. Black's signature resource:
	/// life is a cost here, not just a total, which is what makes its card advantage cheap.
	/// LifeAdditionalCost had no builder wrapper at all before this.
	/// </summary>
	public CreatureCostBuilder PayLife(int amount)
	{
		_costs.Add(new LifeAdditionalCost { Amount = amount });
		return this;
	}

	/// <summary>
	/// "Remove a gold counter from this artifact" — Dragon's Hoard. Spends charge counters off the
	/// SOURCE, which is what bounds an otherwise mana-only repeatable ability.
	///
	/// Not a selection cost: a counter is fungible, so there is nothing for the player to choose.
	/// See RemoveCounterAdditionalCost.
	/// </summary>
	public CreatureCostBuilder RemoveCounter(string kind = "charge", int count = 1)
	{
		_costs.Add(new RemoveCounterAdditionalCost { Kind = kind, Count = count });
		return this;
	}

	internal ImmutableList<AdditionalCost> Build() => _costs.ToImmutableList();
}
