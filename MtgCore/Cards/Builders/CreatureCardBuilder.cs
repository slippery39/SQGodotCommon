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
		Action<CreatureCostBuilder>? costs = null
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

	public CreatureCardBuilder WithTriggeredAbility(
		string name,
		TriggerCondition condition,
		Action<SpellCardBuilder> effect,
		ZoneType ActiveInZone = ZoneType.Battlefield
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
				Effects = effects,
			}
		);
		return this;
	}

	public CreatureCardBuilder WithComponent(GameComponent component)
	{
		_extraComponents.Add(component);
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
		};

		var components = ImmutableArray
			.Create<GameComponent>(new PermanentComponent(), creature)
			.AddRange(_extraComponents);

		return new Card
		{
			Name = _name,
			ManaCost = _manaCost,
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

	internal ImmutableList<AdditionalCost> Build() => _costs.ToImmutableList();
}
