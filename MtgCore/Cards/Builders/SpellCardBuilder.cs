using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore.Cards.Builders;

/// <summary>
/// Fluent builder for instant/sorcery cards and for effect lists in activated/triggered
/// abilities. Each .With[Action]() call opens a pending CardEffect with a sensible default
/// targeting; .WithTarget() overrides that targeting. The next action call or .Build()
/// flushes the pending effect into the effects list.
/// </summary>
public class SpellCardBuilder
{
	private readonly string _name;
	private readonly int _manaCost;
	private readonly List<AdditionalCost> _castCosts = new();
	private readonly List<CardEffect> _effects = new();
	private bool _hasStorm;
	private int? _flashbackManaCost;

	private GameAction? _pendingAction;
	private TargetingStrategy? _pendingTargeting;

	internal SpellCardBuilder(string name, int manaCost)
	{
		_name = name;
		_manaCost = manaCost;
	}

	// ===== CAST COSTS =====

	public SpellCardBuilder WithSacrificeSubtypeCost(string subtype, int count = 1)
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

	public SpellCardBuilder WithSacrificeCost(TargetSpecification? filter = null, int count = 1)
	{
		_castCosts.Add(new SacrificeAdditionalCost { Filter = filter, Count = count });
		return this;
	}

	public SpellCardBuilder WithStorm()
	{
		_hasStorm = true;
		return this;
	}

	public SpellCardBuilder WithFlashback(int manaCost)
	{
		_flashbackManaCost = manaCost;
		return this;
	}

	// ===== EFFECT ACTIONS =====

	public SpellCardBuilder WithDamage(int amount)
	{
		FlushPending();
		_pendingAction = new DealDamageAction { Amount = amount };
		_pendingTargeting = TargetingStrategy.SingleTarget(
			TargetSpecification.PlayersOrCreatures()
		);
		return this;
	}

	public SpellCardBuilder WithLifeGain(int amount)
	{
		FlushPending();
		_pendingAction = new GainLifeAction { Amount = amount };
		_pendingTargeting = TargetingStrategy.Self();
		return this;
	}

	public SpellCardBuilder WithDraw(int amount)
	{
		FlushPending();
		_pendingAction = new DrawCardsAction { Amount = amount };
		_pendingTargeting = TargetingStrategy.Self();
		return this;
	}

	public SpellCardBuilder WithLoseLife(int amount)
	{
		FlushPending();
		_pendingAction = new LoseLifeAction { Amount = amount };
		_pendingTargeting = TargetingStrategy.Self();
		return this;
	}

	public SpellCardBuilder WithDestroy()
	{
		FlushPending();
		_pendingAction = new DestroyCreatureAction();
		_pendingTargeting = TargetingStrategy.SingleTarget(TargetSpecification.OpponentCreatures());
		return this;
	}

	public SpellCardBuilder WithExile()
	{
		FlushPending();
		_pendingAction = new ExileAction();
		_pendingTargeting = TargetingStrategy.SingleTarget(
			TargetSpecification.PlayersOrCreatures()
		);
		return this;
	}

	public SpellCardBuilder WithAddMana(int amount)
	{
		FlushPending();
		_pendingAction = new AddTemporaryManaAction { Amount = amount };
		_pendingTargeting = TargetingStrategy.Self();
		return this;
	}

	public SpellCardBuilder WithBoost(
		int power,
		int toughness,
		ModifierDuration duration = ModifierDuration.UntilEndOfTurn
	)
	{
		FlushPending();
		_pendingAction = new AddModifierAction
		{
			PowerBonus = power,
			ToughnessBonus = toughness,
			Duration = duration,
		};
		_pendingTargeting = TargetingStrategy.SingleTarget(
			TargetSpecification.CreatureControlledByYou()
		);
		return this;
	}

	public SpellCardBuilder WithCreateTokens(Card template, int count = 1)
	{
		FlushPending();
		_pendingAction = new CreateCardAction { CardTemplate = template, Count = count };
		_pendingTargeting = TargetingStrategy.NoTarget();
		return this;
	}

	public SpellCardBuilder WithGiveFlashback()
	{
		FlushPending();
		_pendingAction = new GiveFlashbackAction();
		_pendingTargeting = TargetingStrategy.RandomTarget(
			new IsInstantOrSorceryInOwnGraveyardSpecification()
		);
		return this;
	}

	public SpellCardBuilder WithAction(GameAction action, TargetingStrategy targeting)
	{
		FlushPending();
		_pendingAction = action;
		_pendingTargeting = targeting;
		return this;
	}

	// ===== TARGETING OVERRIDE =====

	public SpellCardBuilder WithTarget(TargetingStrategy targeting)
	{
		_pendingTargeting = targeting;
		return this;
	}

	public SpellCardBuilder NoTarget()
	{
		_pendingTargeting = TargetingStrategy.NoTarget();
		return this;
	}

	// ===== INTERNAL HELPERS =====

	private void FlushPending()
	{
		if (_pendingAction == null)
			return;
		_effects.Add(
			new CardEffect
			{
				ActionTemplate = _pendingAction,
				TargetingStrategy = _pendingTargeting!,
			}
		);
		_pendingAction = null;
		_pendingTargeting = null;
	}

	internal ImmutableList<CardEffect> BuildEffects()
	{
		FlushPending();
		return _effects.ToImmutableList();
	}

	// ===== BUILD =====

	public Card Build()
	{
		FlushPending();
		var components = ImmutableList.CreateBuilder<GameComponent>();
		components.Add(
			new SpellComponent { Effects = _effects.ToImmutableList(), HasStorm = _hasStorm }
		);
		if (_flashbackManaCost.HasValue)
			components.Add(new FlashbackComponent { FlashbackManaCost = _flashbackManaCost.Value });
		return new Card
		{
			Name = _name,
			ManaCost = _manaCost,
			AdditionalCastCosts = _castCosts.ToImmutableList(),
			Components = components.ToImmutable(),
		};
	}
}
