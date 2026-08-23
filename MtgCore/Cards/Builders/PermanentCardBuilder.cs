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
				// See TriggerTargeting — user-select inside a trigger silently does nothing.
				Effects = TriggerTargeting.MakeResolvable(effects),
				MaxTriggers = maxTriggers,
				MaxTriggersPerTurn = maxPerTurn,
			}
		);
		return this;
	}

	/// <summary>
	/// Makes this card an Aura that chooses what it enchants when it is CAST.
	///
	/// It used to attach through an ETB trigger, on the reasoning that nothing can respond
	/// between cast and resolution so the timing was identical. Playtesting proved that wrong for
	/// a reason unrelated to timing: an ETB trigger cannot make the spell illegal, so Pacifism
	/// and Aether Tunnel were castable with no creature on the board, resolved, found nothing to
	/// attach to, and sat there inert forever. See AuraTargetComponent.
	///
	/// Pass 0/0 bonuses for an aura that only grants keywords or only shuts a creature down.
	/// </summary>
	/// <remarks>
	/// trample/reach/deathtouch were missing from this list while EquippedBoostComponent has
	/// always carried them, so an Aura granting any of the three rendered and behaved as a bare
	/// P/T buff — Rancor is +2/+0 AND trample, and the trample half would simply have vanished.
	/// </remarks>
	public PermanentCardBuilder AsAura(
		int powerBonus = 0,
		int toughnessBonus = 0,
		bool flying = false,
		bool firstStrike = false,
		bool lifelink = false,
		bool indestructible = false,
		bool hexproof = false,
		bool preventsAttacking = false,
		bool trample = false,
		bool reach = false,
		bool deathtouch = false,
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
					GrantsTrample = trample,
					GrantsReach = reach,
					GrantsDeathtouch = deathtouch,
					PreventsAttacking = preventsAttacking,
					Duration = ModifierDuration.Permanent,
				},
			}
		);

		_extraComponents.Add(
			new AuraTargetComponent
			{
				Targeting =
					targeting ?? TargetingStrategy.SingleTarget(TargetSpecification.Creatures()),
			}
		);

		return this;
	}

	/// <summary>
	/// The equip ability: "{N}: Attach to target creature you control."
	///
	/// ONCE PER TURN, which is a deliberate divergence from the printed rule. Real MTG allows
	/// unlimited equips; this engine cannot afford them, and the reason is not a card-balance one.
	///
	/// Measured: with Swiftfoot Boots at equip {0}, the AI moves the boots back and forth between
	/// two creatures until GameRunner's 200-action-per-turn limit ends the game as a draw. In a
	/// 14 000-game run **28.7% of games with Boots on the board drew**, against a 0.7% base rate,
	/// and Boots was the permanent on board in 12 of 12 sampled action-limit games.
	///
	/// IT IS NOT A TIE-BREAK PROBLEM, and two AI-side fixes were built and reverted before that was
	/// established. Instrumenting the search shows the equip scoring **74.1 against 72.7 for ending
	/// the turn** — and moving the boots straight back scores +1.4 again. The evaluator genuinely
	/// rates both directions of the same oscillation as an improvement, so no "prefer to stop on a
	/// tie" rule can ever catch it; there is no tie. The underlying evaluator defect is recorded in
	/// DesignNotes.md and is a separate, larger piece of work.
	///
	/// VERIFIED: ActionLimitReached draws went 86 -> 0 over 7 000 games with this cap in place, and
	/// the base win rate moved to exactly 50.0% (a draw-free run). Two earlier runs appeared to show
	/// the cap failing; both were measuring a stale binary that never contained it.
	///
	/// A per-turn cap bounds the loop unconditionally, whatever the evaluator believes. The cost is
	/// small and known: the printed reason for unlimited equips is moving a sword off a dying
	/// creature, and once per turn still allows that. Raising this to 0 (unlimited) reopens the
	/// draws — do not, without first fixing the evaluator and re-measuring.
	///
	/// THE CURRENT WEARER IS EXCLUDED AS A TARGET, and that exclusion is what stops a free equip
	/// hanging the game. Re-attaching to the creature already wearing it is a perfect no-op:
	/// AttachEquipmentAction strips the boost and re-stamps an identical one, so the resulting
	/// state is byte-identical. Real MTG allows it and no player has ever done it on purpose.
	///
	/// It was invisible while every equip cost mana, because mana bounded the loop. Dropping
	/// Swiftfoot Boots to equip {0} removed the bound and **38.5% of games with Boots on the board
	/// ended in a draw** against a 1.5% base rate — the AI re-equipped until GameRunner's
	/// 200-action-per-turn limit ended the game. See MultiTurnBeamSearchAiStrategy.PickBestNode:
	/// EndTurn must EXCEED the best other action to be chosen, so a zero-value action beats ending
	/// the turn forever.
	///
	/// The exclusion lives in the TARGETING SPEC rather than in AttachEquipmentAction.ValidateAdd
	/// because the spec is the single thing both MtgActionGenerator and ActivateAbilityAction run.
	/// A guard in the action alone would leave the generator offering an action that then fails —
	/// the "the AI can do it and I can't" split this codebase keeps rediscovering.
	/// </summary>
	public PermanentCardBuilder WithEquip(int manaCost) =>
		WithActivatedAbility(
			"Equip",
			manaCost,
			effect: eb =>
				eb.WithAction(
					new AttachEquipmentAction(),
					TargetingStrategy.SingleTarget(
						TargetSpecification
							.CreatureControlledByYou()
							.And(new IsEquippedBySourceSpecification().Not())
					)
				),
			maxPerTurn: 1
		);

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

	/// <summary>
	/// A static keyword grant to every permanent matching <paramref name="filter"/> — Akroma's
	/// Memorial.
	///
	/// The machinery already worked from a non-creature source: CheckStateBasedEffectsAction
	/// routes PermanentEnteredBattlefieldEvent into StaticAbilityEngine alongside the creature
	/// event. Only the builder method was missing, so every existing StaticGrantKeywordAbility in
	/// the repo happens to sit on a creature.
	///
	/// Vigilance is absent from the parameter list because it is absent from the engine — see the
	/// deliberate non-implementation in MtgCore/CLAUDE.md.
	/// </summary>
	public PermanentCardBuilder WithStaticGrantKeyword(
		TargetSpecification filter,
		bool flying = false,
		bool firstStrike = false,
		bool doubleStrike = false,
		bool trample = false,
		bool haste = false,
		bool lifelink = false,
		bool deathtouch = false,
		bool reach = false,
		bool taunt = false,
		bool indestructible = false,
		bool shroud = false,
		bool hexproof = false
	)
	{
		_extraComponents.Add(
			new StaticGrantKeywordAbility
			{
				Filter = filter,
				GrantsFlying = flying,
				GrantsFirstStrike = firstStrike,
				GrantsDoubleStrike = doubleStrike,
				GrantsTrample = trample,
				GrantsHaste = haste,
				GrantsLifelink = lifelink,
				GrantsDeathtouch = deathtouch,
				GrantsReach = reach,
				GrantsTaunt = taunt,
				GrantsIndestructible = indestructible,
				GrantsShroud = shroud,
				GrantsHexproof = hexproof,
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
