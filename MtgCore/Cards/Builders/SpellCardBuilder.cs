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
	private CardType _types = CardType.None;

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

	/// <summary>
	/// Discard as an additional cost to cast, rather than as an effect. Unlike WithDiscard,
	/// this makes the spell uncastable with an empty hand, and the card is gone before the
	/// spell resolves.
	///
	/// Use for "as an additional cost, discard a card"; use WithDiscard for a discard that is
	/// part of the effect ("draw 4, then discard 1").
	/// </summary>
	/// <param name="subtype">
	/// Restricts what may be pitched — "as an additional cost, discard a land card" (Magmatic
	/// Insight). Empty means any card.
	/// </param>
	public SpellCardBuilder WithDiscardCost(int count = 1, string subtype = "")
	{
		_castCosts.Add(DiscardCost(count, subtype));
		return this;
	}

	/// <summary>Shared by both discard-cost builders so the filter and its wording cannot drift.</summary>
	internal static DiscardAdditionalCost DiscardCost(int count, string subtype) =>
		new()
		{
			Count = count,
			Filter = string.IsNullOrEmpty(subtype)
				? null
				: new IsSubtypeSpecification { Subtype = subtype },
			FilterDescription = string.IsNullOrEmpty(subtype) ? "" : subtype.ToLowerInvariant(),
		};

	/// <summary>
	/// "As an additional cost, pay N life." Distinct from WithLoseLife, which is an effect:
	/// this makes the spell uncastable at or below N life, and is paid before it resolves.
	/// </summary>
	public SpellCardBuilder WithLifeCost(int amount)
	{
		_castCosts.Add(new LifeAdditionalCost { Amount = amount });
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

	/// <summary>
	/// "Exile the top card of your library. You may play it this turn." See
	/// ExiledPlayableComponent for how the card stays castable until the turn ends.
	/// </summary>
	public SpellCardBuilder WithImpulseDraw()
	{
		FlushPending();
		_pendingAction = new ExileTopCardPlayableAction();
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
	public SpellCardBuilder WithReanimate(bool fromAnyGraveyard = false)
	{
		FlushPending();
		_pendingAction = new PutIntoBattlefieldAction();
		_pendingTargeting = TargetingStrategy.SingleTarget(
			fromAnyGraveyard
				? new IsCreatureInAnyGraveyardSpecification()
				: new IsCreatureInOwnGraveyardSpecification()
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
	/// Create one token per matching card in a zone — "create a Zombie for each Zombie in your
	/// graveyard". The Krenko pattern: count into pipeline context, then create that many.
	///
	/// Leave <paramref name="subtype"/> empty to count every card in the zone.
	/// </summary>
	public SpellCardBuilder WithCreateTokensPerCard(
		Card template,
		string subtype,
		ZoneType zone = ZoneType.Graveyard,
		string countKey = "token_scale_count",
		bool creaturesOnly = false
	)
	{
		FlushPending();
		_pendingAction = new PipelineAction
		{
			Steps = ImmutableList.Create<GameAction>(
				new CountCardsWithSubtypeAction
				{
					Subtype = subtype,
					Zone = zone,
					OutputKey = countKey,
					CreaturesOnly = creaturesOnly,
					PlayerIdContextKey = ContextKeys.CastingPlayerId,
				},
				new CreateCardAction { CardTemplate = template, CountInputKey = countKey }
			),
		};
		_pendingTargeting = TargetingStrategy.NoTarget();
		return this;
	}

	/// <summary>
	/// Weaken — -X/-X to an opposing creature. Kills anything whose toughness reaches zero,
	/// which CheckStateBasedEffectsAction enforces, so this is removal that scales with the
	/// target rather than a flat "destroy". Doubles as a combat trick against a big attacker.
	/// </summary>
	public SpellCardBuilder WithWeaken(int power, int toughness)
	{
		FlushPending();
		_pendingAction = new AddModifierAction
		{
			PowerBonus = -power,
			ToughnessBonus = -toughness,
			Duration = ModifierDuration.UntilEndOfTurn,
		};
		_pendingTargeting = TargetingStrategy.SingleTarget(TargetSpecification.OpponentCreatures());
		return this;
	}

	/// <summary>
	/// Bounce — return an opposing permanent to its owner's hand. Answers what destroy cannot
	/// (a recurring threat comes back as a card to re-cast, not a card in the graveyard),
	/// which matters in a set this full of graveyard recursion.
	/// </summary>
	public SpellCardBuilder WithBounce()
	{
		FlushPending();
		_pendingAction = new ReturnToHandAction();
		_pendingTargeting = TargetingStrategy.SingleTarget(TargetSpecification.OpponentCreatures());
		return this;
	}

	/// <summary>
	/// Fight — this creature and a target creature deal damage to each other. Removal that
	/// costs no card but risks the fighter, and it reaches flyers a ground creature could
	/// never attack.
	/// </summary>
	public SpellCardBuilder WithFight()
	{
		FlushPending();
		_pendingAction = new FightAction();
		_pendingTargeting = TargetingStrategy.SingleTarget(TargetSpecification.OpponentCreatures());
		return this;
	}

	/// <summary>
	/// Edict — the opponent loses a creature chosen by mana cost rather than by targeting,
	/// so it answers Hexproof and Shroud, which nothing else in the set can touch.
	/// </summary>
	public SpellCardBuilder WithEdict(bool takeBiggest = true)
	{
		FlushPending();
		_pendingAction = new PipelineAction
		{
			Steps = ImmutableList.Create<GameAction>(
				new SelectCreatureFromBattlefieldByManaCostAction
				{
					TargetOpponent = true,
					SelectLowest = !takeBiggest,
					PlayerIdContextKey = ContextKeys.CastingPlayerId,
					OutputKey = "edict_target",
				},
				new DestroyCreatureAction { TargetContextKey = "edict_target" }
			),
		};
		_pendingTargeting = TargetingStrategy.NoTarget();
		return this;
	}

	/// <summary>
	/// Drain — target opponent loses N life and you gain N. Black's core verb, and the reason
	/// this exists rather than chaining WithLoseLife + WithLifeGain: those are two targeted
	/// effects, and inside a triggered ability TargetingStrategy.NoTarget() resolves to an empty
	/// target list that ResolveEffectAction writes over any hardcoded TargetIds, so the loss
	/// half silently hits nobody. DrainLifeAction derives both players from context instead,
	/// which is the only shape that works from a trigger.
	///
	/// It was hand-rolled at roughly ten call sites in Hollowmere before this, each one an
	/// opportunity to forget PlayerIdContextKey and get a card that does nothing.
	/// </summary>
	public SpellCardBuilder WithDrain(int amount)
	{
		FlushPending();
		_pendingAction = new DrainLifeAction
		{
			Amount = amount,
			TargetOpponent = true,
			PlayerIdContextKey = ContextKeys.CastingPlayerId,
		};
		_pendingTargeting = TargetingStrategy.NoTarget();
		return this;
	}

	/// <summary>
	/// Each player sacrifices a creature — a symmetric edict (Fleshbag Marauder, Smallpox).
	///
	/// Both halves take the CHEAPEST creature, not the biggest: a real edict lets each player
	/// choose, and each would keep their bomb. Taking the biggest would make a symmetric edict
	/// strictly better for whoever cast it, which is the opposite of what the card says.
	///
	/// excludeSubtype skips creatures of that type on both sides — Call to the Grave's
	/// "non-Zombie creature", the clause that makes it a build-around rather than a liability.
	/// </summary>
	public SpellCardBuilder WithSymmetricEdict(string excludeSubtype = "")
	{
		FlushPending();

		IEnumerable<GameAction> Half(bool opponent, string key) =>
			[
				new SelectCreatureFromBattlefieldByManaCostAction
				{
					TargetOpponent = opponent,
					SelectLowest = true,
					ExcludeSubtype = excludeSubtype,
					PlayerIdContextKey = ContextKeys.CastingPlayerId,
					OutputKey = key,
				},
				new DestroyCreatureAction { TargetContextKey = key },
			];

		_pendingAction = new PipelineAction
		{
			Steps = [.. Half(true, "symmetric_edict_them"), .. Half(false, "symmetric_edict_you")],
		};
		_pendingTargeting = TargetingStrategy.NoTarget();
		return this;
	}

	/// <summary>
	/// Target opponent reveals their hand and you choose a card to discard — the chosen half of
	/// the discard theme, as distinct from WithOpponentDiscard's random one.
	///
	/// "You choose" becomes "take their most expensive non-land card", the same deterministic
	/// stand-in for a choice used everywhere else in this engine. That gap matters: random
	/// discard off an eight-card hand is a coin flip, while choosing is real disruption, and
	/// cards are costed on which one they are.
	/// </summary>
	public SpellCardBuilder WithChosenDiscard(int count = 1)
	{
		FlushPending();

		IEnumerable<GameAction> One(int index) =>
			[
				new SelectCardFromHandByManaCostAction
				{
					TargetOpponent = true,
					SelectLowest = false,
					PlayerIdContextKey = ContextKeys.CastingPlayerId,
					OutputKey = $"chosen_discard_{index}",
				},
				new DiscardCardsAction { TargetContextKey = $"chosen_discard_{index}" },
			];

		_pendingAction = new PipelineAction
		{
			Steps = [.. Enumerable.Range(0, count).SelectMany(One)],
		};
		_pendingTargeting = TargetingStrategy.NoTarget();
		return this;
	}

	/// <summary>
	/// Tutor — search your library for a card and put it in your hand. With no subtype this is
	/// an unrestricted tutor (Grim Tutor, Dark Petition); with one it is a synergy fetch.
	///
	/// Consistency, which is what makes a synergy deck function rather than flood.
	/// </summary>
	public SpellCardBuilder WithTutor(string subtype = "")
	{
		FlushPending();
		_pendingAction = new PipelineAction
		{
			Steps = ImmutableList.Create<GameAction>(
				new SelectCardFromLibraryAction
				{
					Subtype = subtype,
					// Unrestricted search must rank; library order is random, so without this an
					// unfiltered tutor would just hand you the top card of your library.
					SelectBestByManaCost = string.IsNullOrEmpty(subtype),
					OutputKey = "tutor_target",
					PlayerIdContextKey = ContextKeys.CastingPlayerId,
				},
				new MoveCardToHandAction
				{
					CardIdContextKey = "tutor_target",
					PlayerIdContextKey = ContextKeys.CastingPlayerId,
				}
			),
		};
		_pendingTargeting = TargetingStrategy.NoTarget();
		return this;
	}

	/// <summary>
	/// Dig — look at the top N cards and put one in your hand. Card selection rather than card
	/// advantage, which is a different axis from Draw and lets a deck find its payoff.
	/// </summary>
	public SpellCardBuilder WithDig(int amount)
	{
		FlushPending();
		_pendingAction = new LookAtTopCardsAction
		{
			Amount = amount,
			PlayerIdContextKey = ContextKeys.CastingPlayerId,
		};
		_pendingTargeting = TargetingStrategy.NoTarget();
		return this;
	}

	/// <summary>
	/// Scry N — look at the top N cards of your library and put any of them on the bottom.
	///
	/// The choice is real: MinChoices is 0, so keeping everything on top is legal. Bottoming
	/// unconditionally would not be scry — it is strictly worse than doing nothing whenever the
	/// top card is one you wanted.
	/// </summary>
	public SpellCardBuilder WithScry(int amount = 1)
	{
		FlushPending();
		_pendingAction = new PipelineAction
		{
			Steps = ImmutableList.Create<GameAction>(
				new SelectTopCardsToBottomAction
				{
					Prompt = $"Scry {amount} — choose any to put on the bottom",
					Amount = amount,
					MinChoices = 0,
					MaxChoices = amount,
					OutputKey = "scry_to_bottom",
				},
				new MoveCardToBottomOfLibraryAction
				{
					CardIdsContextKey = "scry_to_bottom",
					PlayerIdContextKey = ContextKeys.CastingPlayerId,
				}
			),
		};
		_pendingTargeting = TargetingStrategy.NoTarget();
		return this;
	}

	/// <summary>
	/// Prevent damage for the rest of the turn — Safe Passage, Harm's Way. Stamped on the
	/// player, since a one-shot spell has no permanent to live on.
	/// </summary>
	public SpellCardBuilder WithDamagePrevention(
		bool preventAll = true,
		int amount = 2,
		bool includeCreatures = true
	)
	{
		FlushPending();
		_pendingAction = new PreventDamageAction
		{
			PreventAll = preventAll,
			Amount = amount,
			PreventsCreatureDamage = includeCreatures,
		};
		_pendingTargeting = TargetingStrategy.Self();
		return this;
	}

	/// <summary>
	/// Wraps the next effect in an intervening-if clause, checked at resolution —
	/// "if you have less life than an opponent, you gain 6 life".
	/// </summary>
	public SpellCardBuilder WithConditionalAction(ActivationCondition condition, GameAction action)
	{
		FlushPending();
		_pendingAction = new ConditionalAction { Condition = condition, Action = action };
		_pendingTargeting = TargetingStrategy.NoTarget();
		return this;
	}

	/// <summary>
	/// "Choose one —". Each mode is a display name and the action it runs.
	/// </summary>
	public SpellCardBuilder WithModes(params (string Name, GameAction Action)[] modes) =>
		WithModes(onceEach: false, modes);

	/// <summary>
	/// Modal, with onceEach giving "choose one that hasn't been chosen" — a mode is struck off
	/// the list permanently once taken (Demonic Pact).
	///
	/// The two flags below must move together: excluding without recording never excludes, and
	/// recording without excluding is dead state. Exposed as one parameter so a card cannot set
	/// half of it.
	/// </summary>
	public SpellCardBuilder WithModes(
		bool onceEach,
		params (string Name, GameAction Action)[] modes
	)
	{
		FlushPending();
		_pendingAction = new PipelineAction
		{
			Steps = ImmutableList.Create<GameAction>(
				new SelectModeAction
				{
					Prompt = onceEach ? "Choose one that hasn't been chosen" : "Choose one",
					ModeNames = modes.Select(m => m.Name).ToImmutableList(),
					MinChoices = 1,
					MaxChoices = 1,
					OutputKey = "chosen_mode",
					ExcludeAlreadyChosen = onceEach,
				},
				new ApplyChosenModeAction
				{
					Modes = modes.Select(m => m.Action).ToImmutableList(),
					ModeContextKey = "chosen_mode",
					RecordChoice = onceEach,
				}
			),
		};
		_pendingTargeting = TargetingStrategy.NoTarget();
		return this;
	}

	/// <summary>
	/// Adds {X} to the mana cost. The effect reads the chosen X from pipeline context under
	/// ContextKeys.XValue — an X spell whose effect ignores it is just an overpriced spell.
	/// </summary>
	public SpellCardBuilder WithXCost(int multiplier = 1)
	{
		_extraComponents.Add(new XCostComponent { Multiplier = multiplier });
		return this;
	}

	/// <summary>
	/// Convoke — costs {1} less per ready creature you control, and exhausts exactly that many
	/// when cast. See ConvokeComponent for why the choice of which creatures is automatic.
	/// </summary>
	public SpellCardBuilder WithConvoke()
	{
		_extraComponents.Add(new ConvokeComponent());
		return this;
	}

	/// <summary>
	/// Freeze — exhaust the target and keep it tapped through <paramref name="turns"/> further
	/// untap steps. turns: 0 is a plain tapper (WithExhaust); 1 is "doesn't untap during its
	/// controller's next untap step".
	/// </summary>
	public SpellCardBuilder WithFreeze(int turns = 1, bool whileSourceRemains = false)
	{
		FlushPending();
		_pendingAction = new ExhaustCreatureAction
		{
			FreezeTurns = turns,
			FreezeWhileSourceRemains = whileSourceRemains,
		};
		_pendingTargeting = TargetingStrategy.SingleTarget(TargetSpecification.OpponentCreatures());
		return this;
	}

	/// <summary>
	/// "Until end of turn, target creature loses all abilities and becomes a 1/1."
	/// Stamped UntilEndOfTurn so the normal turn cleanup removes it.
	/// </summary>
	public SpellCardBuilder WithBecomesVanilla(int power = 1, int toughness = 1)
	{
		FlushPending();
		_pendingAction = new AddCustomModifierAction
		{
			Modifier = new BecomesBaseCreatureComponent
			{
				Power = power,
				Toughness = toughness,
				Duration = ModifierDuration.UntilEndOfTurn,
			},
		};
		_pendingTargeting = TargetingStrategy.SingleTarget(TargetSpecification.OpponentCreatures());
		return this;
	}

	/// <summary>"Take an extra turn after this one."</summary>
	public SpellCardBuilder WithExtraTurn(int turns = 1)
	{
		FlushPending();
		_pendingAction = new TakeExtraTurnAction { Turns = turns };
		_pendingTargeting = TargetingStrategy.NoTarget();
		return this;
	}

	/// <summary>"Gain control of target permanent."</summary>
	public SpellCardBuilder WithGainControl()
	{
		FlushPending();
		_pendingAction = new GainControlAction();
		_pendingTargeting = TargetingStrategy.SingleTarget(TargetSpecification.OpponentCreatures());
		return this;
	}

	/// <summary>"This spell costs {amount} less to cast if …".</summary>
	public SpellCardBuilder WithCostReduction(int amount, ActivationCondition condition)
	{
		_extraComponents.Add(
			new ConditionalCostReductionComponent { Amount = amount, Condition = condition }
		);
		return this;
	}

	/// <summary>
	/// A counterspell. This card is never cast — it fires automatically from hand when the
	/// opponent casts a matching spell and you left its cost unspent. See CounterTrapComponent.
	///
	/// A trap needs no effect of its own, so it does not go through the usual effect pipeline;
	/// DrawOnCounter covers the one card (Bone to Ash) that riders an effect onto the counter.
	/// </summary>
	public SpellCardBuilder AsCounterTrap(
		CardType targetTypes = CardType.AnySpell | CardType.AnyPermanent,
		CardType excludeTypes = CardType.None,
		int manaTax = 0,
		bool taxAllRemaining = false,
		bool exileInstead = false,
		bool returnToHandInstead = false,
		int drawOnCounter = 0
	)
	{
		_extraComponents.Add(
			new CounterTrapComponent
			{
				TargetTypes = targetTypes,
				ExcludeTypes = excludeTypes,
				ManaTax = manaTax,
				TaxAllRemaining = taxAllRemaining,
				ExileInstead = exileInstead,
				ReturnToHandInstead = returnToHandInstead,
				DrawOnCounter = drawOnCounter,
			}
		);
		return this;
	}

	// ===== TRIGGER-SAFE VARIANTS =====
	//
	// A triggered ability spawns ResolveEffectAction with no TargetIds, so a UserSelect
	// targeting strategy resolves to an EMPTY target list and the effect silently does
	// nothing. Every one of these picks its target itself, so it works from a trigger.
	// HollowmereCardBugTests.TriggeredAbilities_DoNotUseUserSelectTargeting enforces the rule.

	/// Reanimates the first creature found in your graveyard.
	public SpellCardBuilder WithAutoReanimate() =>
		WithGraveyardPipeline(
			new IsCreatureInOwnGraveyardSpecification(),
			key => new PutIntoBattlefieldAction { CardIdContextKey = key },
			"auto_reanimate"
		);

	/// Returns the first creature found in your graveyard to your hand.
	public SpellCardBuilder WithAutoReturnCreature() =>
		WithGraveyardPipeline(
			new IsCreatureInOwnGraveyardSpecification(),
			key => new MoveCardToHandAction
			{
				CardIdContextKey = key,
				PlayerIdContextKey = ContextKeys.CastingPlayerId,
			},
			"auto_return_creature"
		);

	/// Returns the first instant or sorcery found in your graveyard to your hand.
	public SpellCardBuilder WithAutoReturnSpell() =>
		WithGraveyardPipeline(
			new IsInstantOrSorceryInOwnGraveyardSpecification(),
			key => new MoveCardToHandAction
			{
				CardIdContextKey = key,
				PlayerIdContextKey = ContextKeys.CastingPlayerId,
			},
			"auto_return_spell"
		);

	/// Destroys the opponent's most expensive creature. Same shape as WithEdict.
	public SpellCardBuilder WithAutoDestroy() => WithEdict();

	/// Damages the opponent's most expensive creature.
	public SpellCardBuilder WithAutoDamage(int amount) =>
		WithBiggestOpposingCreaturePipeline(
			key => new DealDamageAction { Amount = amount, TargetContextKey = key },
			"auto_damage"
		);

	/// Shrinks the opponent's most expensive creature, killing it if toughness hits zero.
	public SpellCardBuilder WithAutoWeaken(int power, int toughness) =>
		WithBiggestOpposingCreaturePipeline(
			key => new AddModifierAction
			{
				PowerBonus = -power,
				ToughnessBonus = -toughness,
				Duration = ModifierDuration.UntilEndOfTurn,
				TargetContextKey = key,
			},
			"auto_weaken"
		);

	/// Fights the opponent's most expensive creature.
	public SpellCardBuilder WithAutoFight() =>
		WithBiggestOpposingCreaturePipeline(
			key => new FightAction { TargetContextKey = key },
			"auto_fight"
		);

	private SpellCardBuilder WithGraveyardPipeline(
		TargetSpecification filter,
		Func<string, GameAction> follow,
		string key
	)
	{
		FlushPending();
		_pendingAction = new PipelineAction
		{
			Steps = ImmutableList.Create(
				new SelectCardFromZoneAction
				{
					Zone = ZoneType.Graveyard,
					Filter = filter,
					PlayerIdContextKey = ContextKeys.CastingPlayerId,
					OutputKey = key,
				},
				follow(key)
			),
		};
		_pendingTargeting = TargetingStrategy.NoTarget();
		return this;
	}

	private SpellCardBuilder WithBiggestOpposingCreaturePipeline(
		Func<string, GameAction> follow,
		string key
	)
	{
		FlushPending();
		_pendingAction = new PipelineAction
		{
			Steps = ImmutableList.Create(
				new SelectCreatureFromBattlefieldByManaCostAction
				{
					TargetOpponent = true,
					SelectLowest = false,
					PlayerIdContextKey = ContextKeys.CastingPlayerId,
					OutputKey = key,
				},
				follow(key)
			),
		};
		_pendingTargeting = TargetingStrategy.NoTarget();
		return this;
	}

	/// <summary>
	/// Prowess: +1/+1 until end of turn to the card whose ability is resolving.
	///
	/// Targets via ContextKeys.SourceCardId rather than a targeting strategy, because the
	/// buff must land on the trigger's own source. Pair with a SpellCast trigger.
	/// </summary>
	public SpellCardBuilder WithProwessBuff(int power = 1, int toughness = 1)
	{
		FlushPending();
		_pendingAction = new AddModifierAction
		{
			PowerBonus = power,
			ToughnessBonus = toughness,
			Duration = ModifierDuration.UntilEndOfTurn,
			TargetContextKey = ContextKeys.SourceCardId,
		};
		_pendingTargeting = TargetingStrategy.NoTarget();
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
		bool reach = false,
		bool shroud = false,
		bool hexproof = false,
		bool firstStrike = false,
		bool doubleStrike = false,
		bool indestructible = false,
		bool exalted = false,
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
			GrantsReach = reach,
			GrantsShroud = shroud,
			GrantsHexproof = hexproof,
			GrantsFirstStrike = firstStrike,
			GrantsDoubleStrike = doubleStrike,
			GrantsIndestructible = indestructible,
			GrantsExalted = exalted,
			Duration = duration,
		};
		_pendingTargeting = TargetingStrategy.SingleTarget(
			TargetSpecification.CreatureControlledByYou()
		);
		return this;
	}

	/// <summary>
	/// A permanent +X/+X on the card running this effect — "put a +1/+1 counter on this
	/// creature" (Ajani's Pridemate, Gideon's Avenger, renown).
	///
	/// A permanent AddModifierAction IS this engine's +1/+1 counter. TargetContextKey =
	/// SourceCardId is what makes it hit the source rather than needing a target, which matters
	/// because ResolveEffectAction overwrites hardcoded TargetIds on a NoTarget strategy.
	/// </summary>
	public SpellCardBuilder WithSelfBuff(int power = 1, int toughness = 1)
	{
		FlushPending();
		_pendingAction = new AddModifierAction
		{
			PowerBonus = power,
			ToughnessBonus = toughness,
			Duration = ModifierDuration.Permanent,
			TargetContextKey = ContextKeys.SourceCardId,
		};
		_pendingTargeting = TargetingStrategy.NoTarget();
		return this;
	}

	/// <summary>
	/// Exhaust target creature — "tap target creature". Defaults to an opponent's creature,
	/// which is what every tapper in the cube wants; override with .WithTarget(...) otherwise.
	/// </summary>
	public SpellCardBuilder WithExhaust()
	{
		FlushPending();
		_pendingAction = new ExhaustCreatureAction();
		_pendingTargeting = TargetingStrategy.SingleTarget(TargetSpecification.OpponentCreatures());
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
	/// Modelled as a ChoiceAction at resolution, not as cast-time targeting. Cast-time targets
	/// are locked in before the spell resolves, so "draw 4, then discard 1" would have you
	/// choosing the discard out of your pre-draw hand — you could never pitch a card you just
	/// drew, which is the whole point of a looter.
	///
	/// Choosing at resolution also removes the self-discard hazard for free: the spell has
	/// already moved to the stack by then, so it can never be its own discard.
	/// </summary>
	public SpellCardBuilder WithDiscard(int count = 1)
	{
		FlushPending();
		_pendingAction = new PipelineAction
		{
			Steps = ImmutableList.Create<GameAction>(
				new SelectCardsFromHandAction
				{
					Prompt =
						count == 1
							? "Choose a card to discard"
							: $"Choose {count} cards to discard",
					MinChoices = count,
					MaxChoices = count,
					OutputKey = ContextKeys.SelectedCardIds,
				},
				new DiscardCardsAction { TargetContextKey = ContextKeys.SelectedCardIds }
			),
		};
		_pendingTargeting = TargetingStrategy.NoTarget();
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
			Types = _types,
			AdditionalCastCosts = _castCosts.ToImmutableList(),
			Components = components.ToImmutable(),
		};
	}

	/// <summary>
	/// Declares the card's types. Left None, Card.EffectiveTypes derives Instant|Sorcery, which
	/// is why every pre-existing spell keeps working without an edit.
	/// </summary>
	public SpellCardBuilder WithTypes(CardType types)
	{
		_types = types;
		return this;
	}
}
