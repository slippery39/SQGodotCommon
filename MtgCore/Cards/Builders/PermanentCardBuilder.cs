using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore.Cards.Builders;

/// <summary>
/// Fluent builder for non-creature permanents: artifacts, enchantments, auras and planeswalkers.
///
/// Before this, every artifact and enchantment was hand-constructed in CardLibrary with a raw
/// component array and a magic "Artifact" subtype string. This is deliberately a separate class
/// from CreatureCardBuilder rather than a shared base: CreatureCardBuilder is depended on by
/// several hundred existing cards, and re-parenting it to extract four small methods is a much
/// riskier change than duplicating them.
/// </summary>
public class PermanentCardBuilder
{
	private readonly string _name;
	private readonly int _manaCost;
	private readonly CardType _types;

	private readonly List<string> _subtypes = new();
	private readonly List<AdditionalCost> _castCosts = new();
	private readonly List<GameComponent> _extraComponents = new();

	private int _startingLoyalty;

	internal PermanentCardBuilder(string name, int manaCost, CardType types)
	{
		_name = name;
		_manaCost = manaCost;
		_types = types;

		// The type is also recorded as a subtype string, because AffinityComponent and several
		// existing targeting specs still ask HasSubtype("Artifact"). Keeping both in sync here
		// means no card has to know which of the two a given consumer uses.
		if (types.HasFlag(CardType.Artifact))
			_subtypes.Add("Artifact");
		if (types.HasFlag(CardType.Enchantment))
			_subtypes.Add("Enchantment");
		if (types.HasFlag(CardType.Planeswalker))
			_subtypes.Add("Planeswalker");
	}

	public PermanentCardBuilder WithSubtype(string subtype)
	{
		_subtypes.Add(subtype);
		return this;
	}

	public PermanentCardBuilder WithComponent(GameComponent component)
	{
		_extraComponents.Add(component);
		return this;
	}

	/// <summary>Starting loyalty. Planeswalkers only.</summary>
	public PermanentCardBuilder WithLoyalty(int loyalty)
	{
		_startingLoyalty = loyalty;
		return this;
	}

	/// <summary>
	/// A loyalty ability. <paramref name="loyaltyCost"/> is positive for "+1", negative for
	/// "-3", zero for "0". Only one loyalty ability per planeswalker may be used each turn —
	/// that limit lives on PlaneswalkerComponent, not here, so all of a walker's abilities
	/// share it.
	/// </summary>
	public PermanentCardBuilder WithLoyaltyAbility(
		string name,
		int loyaltyCost,
		Action<SpellCardBuilder> effect
	)
	{
		var effectBuilder = new SpellCardBuilder("_", 0);
		effect(effectBuilder);

		var effects = effectBuilder.BuildEffects();
		if (effects.Count == 0)
			throw new InvalidOperationException($"Loyalty ability '{name}' has no effect.");

		_extraComponents.Add(
			new ActivatedAbilityComponent
			{
				Name = name,
				ManaCost = 0,
				Effects = effects,
				IsLoyaltyAbility = true,
				LoyaltyCost = loyaltyCost,
				// The real cap is per-planeswalker and lives on PlaneswalkerComponent; leaving
				// this unlimited stops the per-ability counter from double-restricting.
				MaxActivationsPerTurn = 0,
			}
		);
		return this;
	}

	public PermanentCardBuilder WithActivatedAbility(
		string name,
		int manaCost,
		Action<SpellCardBuilder> effect,
		Action<CreatureCostBuilder>? costs = null,
		ActivationCondition? condition = null,
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
				MaxActivationsPerTurn = maxPerTurn,
			}
		);
		return this;
	}

	/// <summary>
	/// "When this enters the battlefield, ...". Uses OnSelfEntersBattlefieldAsNonCreature,
	/// which listens for PermanentEnteredBattlefield — a non-creature permanent never fires
	/// CreatureEnteredBattlefield, so the creature version silently never triggers.
	/// </summary>
	public PermanentCardBuilder WithEtbTrigger(string name, Action<SpellCardBuilder> effect) =>
		WithTriggeredAbility(
			name,
			TriggerConditions.OnSelfEntersBattlefieldAsNonCreature(),
			effect
		);

	public PermanentCardBuilder WithTriggeredAbility(
		string name,
		TriggerCondition condition,
		Action<SpellCardBuilder> effect,
		ZoneType activeInZone = ZoneType.Battlefield,
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
				ActiveInZone = activeInZone,
				Effects = effects,
				MaxTriggers = maxTriggers,
				MaxTriggersPerTurn = maxPerTurn,
			}
		);
		return this;
	}

	/// <summary>
	/// Makes this card an Aura that attaches to a creature when it enters the battlefield.
	///
	/// Attachment happens via an ETB trigger rather than at cast time. Real MTG chooses the
	/// aura's target as the spell is cast, but nothing in this engine can respond between cast
	/// and resolution, so the two are observationally identical — and the trigger route needs no
	/// new casting plumbing.
	///
	/// Pass 0/0 bonuses for an aura that only grants keywords or only shuts a creature down.
	/// </summary>
	public PermanentCardBuilder AsAura(
		int powerBonus = 0,
		int toughnessBonus = 0,
		bool flying = false,
		bool firstStrike = false,
		bool lifelink = false,
		bool indestructible = false,
		bool hexproof = false,
		bool preventsAttacking = false,
		TargetingStrategy? targeting = null
	)
	{
		_extraComponents.Add(
			new EquipmentComponent
			{
				IsAura = true,
				PowerBonus = powerBonus,
				ToughnessBonus = toughnessBonus,
				CustomBoostTemplate = new EquippedBoostComponent
				{
					PowerBonus = powerBonus,
					ToughnessBonus = toughnessBonus,
					GrantsFlying = flying,
					GrantsFirstStrike = firstStrike,
					GrantsLifelink = lifelink,
					GrantsIndestructible = indestructible,
					GrantsHexproof = hexproof,
					PreventsAttacking = preventsAttacking,
					Duration = ModifierDuration.Permanent,
				},
			}
		);

		return WithTriggeredAbility(
			"Enchant",
			TriggerConditions.OnSelfEntersBattlefieldAsNonCreature(),
			eb =>
				eb.WithAction(
					new AttachEquipmentAction(),
					targeting ?? TargetingStrategy.SingleTarget(TargetSpecification.Creatures())
				)
		);
	}

	/// <summary>An anthem — a static P/T boost to permanents matching <paramref name="filter"/>.</summary>
	public PermanentCardBuilder WithStaticBoost(
		int power,
		int toughness,
		TargetSpecification filter
	)
	{
		_extraComponents.Add(
			new StaticPTBoostAbility
			{
				PowerBonus = power,
				ToughnessBonus = toughness,
				Filter = filter,
			}
		);
		return this;
	}

	public Card Build()
	{
		var components = ImmutableArray.CreateBuilder<GameComponent>();
		components.Add(new PermanentComponent());

		if (_types.HasFlag(CardType.Planeswalker))
			components.Add(
				new PlaneswalkerComponent
				{
					StartingLoyalty = _startingLoyalty,
					Loyalty = _startingLoyalty,
				}
			);

		foreach (var extra in _extraComponents)
			components.Add(extra);

		return new Card
		{
			Name = _name,
			ManaCost = _manaCost,
			Types = _types,
			AdditionalCastCosts = _castCosts.ToImmutableList(),
			Subtypes = _subtypes.ToImmutableHashSet(StringComparer.OrdinalIgnoreCase),
			Components = components.ToImmutable(),
		};
	}
}
