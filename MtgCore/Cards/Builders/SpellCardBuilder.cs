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
	private readonly List<GameComponent> _extraComponents = new();
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

	/// <summary>
	/// Mill — move cards from the top of a library to the graveyard. Defaults to milling
	/// yourself (self-mill is the enabler half of the graveyard theme); override with
	/// .WithTarget(Single().Opponent()) for an opposing mill.
	/// </summary>
	public SpellCardBuilder WithMill(int amount)
	{
		FlushPending();
		_pendingAction = new MillAction { Amount = amount };
		_pendingTargeting = TargetingStrategy.Self();
		return this;
	}

	/// <summary>
	/// Reanimation — put a creature card from your graveyard onto the battlefield.
	/// </summary>
	public SpellCardBuilder WithReanimate()
	{
		FlushPending();
		_pendingAction = new PutIntoBattlefieldAction();
		_pendingTargeting = TargetingStrategy.SingleTarget(
			new IsCreatureInOwnGraveyardSpecification()
		);
		return this;
	}

	/// <summary>
	/// Return a creature card from your graveyard to your hand. Slower than reanimation but
	/// not restricted to creatures you can afford to cheat in.
	/// </summary>
	public SpellCardBuilder WithReturnCreatureFromGraveyard()
	{
		FlushPending();
		_pendingAction = new ReturnToHandAction();
		_pendingTargeting = TargetingStrategy.SingleTarget(
			new IsCreatureInOwnGraveyardSpecification()
		);
		return this;
	}

	/// <summary>
	/// Return an instant or sorcery card from your graveyard to your hand.
	/// </summary>
	public SpellCardBuilder WithReturnSpellFromGraveyard()
	{
		FlushPending();
		_pendingAction = new ReturnToHandAction();
		_pendingTargeting = TargetingStrategy.SingleTarget(
			new IsInstantOrSorceryInOwnGraveyardSpecification()
		);
		return this;
	}

	/// <summary>
	/// Grant keywords for a duration. The effect-driven counterpart to a lord's static grant —
	/// used for combat tricks and one-shot team pumps.
	/// </summary>
	public SpellCardBuilder WithGrantKeyword(
		bool flying = false,
		bool haste = false,
		bool taunt = false,
		bool lifelink = false,
		bool trample = false,
		bool deathtouch = false,
		ModifierDuration duration = ModifierDuration.UntilEndOfTurn
	)
	{
		FlushPending();
		_pendingAction = new GrantKeywordAction
		{
			GrantsFlying = flying,
			GrantsHaste = haste,
			GrantsTaunt = taunt,
			GrantsLifelink = lifelink,
			GrantsTrample = trample,
			GrantsDeathtouch = deathtouch,
			Duration = duration,
		};
		_pendingTargeting = TargetingStrategy.SingleTarget(
			TargetSpecification.CreatureControlledByYou()
		);
		return this;
	}

	/// <summary>
	/// Target opponent discards <paramref name="count"/> cards at random.
	///
	/// Always sets PlayerIdContextKey: DiscardRandomCardAction resolves the discarding player
	/// from context and silently no-ops when that key is missing, which is invisible in a card
	/// definition. Use this rather than constructing the action by hand.
	/// </summary>
	public SpellCardBuilder WithOpponentDiscard(int count = 1)
	{
		FlushPending();

		GameAction Discard() =>
			new DiscardRandomCardAction
			{
				TargetOpponent = true,
				PlayerIdContextKey = ContextKeys.CastingPlayerId,
			};

		_pendingAction =
			count <= 1
				? Discard()
				: new PipelineAction
				{
					Steps = Enumerable.Range(0, count).Select(_ => Discard()).ToImmutableList(),
				};
		_pendingTargeting = TargetingStrategy.NoTarget();
		return this;
	}

	/// <summary>
	/// Discard a card you choose from your hand — the outlet half of the discard theme.
	///
	/// Excludes the source card. Targets are chosen at cast time, while the spell is still in
	/// hand, so without this a looting spell could select itself as its own discard.
	/// </summary>
	public SpellCardBuilder WithDiscard(int count = 1)
	{
		FlushPending();
		_pendingAction = new DiscardCardsAction();
		_pendingTargeting = TargetingStrategy.SingleTarget(
			new IsInHandSpecification().And(new IsNotSelfSpecification())
		) with
		{
			MinTargets = count,
			MaxTargets = count,
		};
		return this;
	}

	/// <summary>
	/// Exile cards from a graveyard — the format's answer to the graveyard theme.
	/// </summary>
	public SpellCardBuilder WithExileFromGraveyard(bool opponent = true)
	{
		FlushPending();
		_pendingAction = new PipelineAction
		{
			Steps = ImmutableList.Create<GameAction>(
				new SelectCardFromZoneAction
				{
					Zone = ZoneType.Graveyard,
					TargetOpponent = opponent,
					PlayerIdContextKey = ContextKeys.CastingPlayerId,
					OutputKey = "gy_hate_target",
				},
				new MoveCardToExileAction { CardIdContextKey = "gy_hate_target" }
			),
		};
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

	public SpellCardBuilder WithComponent(GameComponent component)
	{
		_extraComponents.Add(component);
		return this;
	}

	// ===== BUILD =====

	public Card Build()
	{
		FlushPending();
		var components = ImmutableArray.CreateBuilder<GameComponent>();
		components.Add(
			new SpellComponent { Effects = _effects.ToImmutableList(), HasStorm = _hasStorm }
		);
		if (_flashbackManaCost.HasValue)
			components.Add(new FlashbackComponent { FlashbackManaCost = _flashbackManaCost.Value });
		foreach (var extra in _extraComponents)
			components.Add(extra);
		return new Card
		{
			Name = _name,
			ManaCost = _manaCost,
			AdditionalCastCosts = _castCosts.ToImmutableList(),
			Components = components.ToImmutable(),
		};
	}
}
