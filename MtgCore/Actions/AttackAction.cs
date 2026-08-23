using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Resolves a creature attacking either an opponent player or one of their creatures.
///
/// Writes to GameState.PendingGameEvents:
///   - CreatureAttackedEvent when the attack is declared
///   - CreatureDestroyedEvent for any creature that dies from combat damage
///
/// These events are consumed by CheckStateBasedEffectsAction after resolution
/// to evaluate triggered abilities.
/// </summary>
public record AttackAction : GameAction
{
	public int AttackerId { get; init; }
	public int TargetId { get; init; }
	public int AttackingPlayerId { get; init; }

	public override ValidationResult ValidateAdd(GameState gameState)
	{
		var attackerResult = ValidateAttacker(gameState);
		if (!attackerResult.IsValid)
			return attackerResult;

		return ValidateTarget(gameState);
	}

	private ValidationResult ValidateAttacker(GameState gameState)
	{
		if (!gameState.HasObject(AttackerId))
			return ValidationResult.Invalid($"Attacker {AttackerId} does not exist");

		var attacker = gameState.GetObject(AttackerId) as Card;
		if (attacker == null)
			return ValidationResult.Invalid("Attacker is not a card");

		if (attacker.ControllerId != AttackingPlayerId)
			return ValidationResult.Invalid("You do not control the attacker");

		if (!attacker.HasComponent<CreatureComponent>())
			return ValidationResult.Invalid("Attacker is not a creature");

		var creature = attacker.GetComponent<CreatureComponent>()!;

		if (creature.HasSummoningSickness && !gameState.GetEffectiveHaste(AttackerId))
			return ValidationResult.Invalid("Creature has summoning sickness and cannot attack");

		if (creature.HasAttacked)
			return ValidationResult.Invalid("Creature has already attacked this turn");

		if (creature.IsExhausted)
			return ValidationResult.Invalid("Creature is exhausted and cannot attack");

		// Pacifism and friends. Distinct from exhaustion, which clears every turn.
		if (gameState.GetEffectiveStats(AttackerId).CantAttack)
			return ValidationResult.Invalid("This creature can't attack");

		var attackerZone = gameState.GetCardZone(AttackerId);
		if (attackerZone.ZoneType != ZoneType.Battlefield)
			return ValidationResult.Invalid("Attacker is not on the battlefield");

		return ValidationResult.Valid;
	}

	private ValidationResult ValidateTarget(GameState gameState)
	{
		if (!gameState.HasObject(TargetId))
			return ValidationResult.Invalid($"Target {TargetId} does not exist");

		var targetObj = gameState.GetObject(TargetId);
		int defendingPlayerId;

		if (targetObj is MtgPlayer targetPlayer)
		{
			if (targetPlayer.Id == AttackingPlayerId)
				return ValidationResult.Invalid("Cannot attack yourself");
			defendingPlayerId = targetPlayer.Id;
		}
		else if (targetObj is Card targetCard)
		{
			var isPlaneswalker = targetCard.HasComponent<PlaneswalkerComponent>();

			if (!targetCard.HasComponent<CreatureComponent>() && !isPlaneswalker)
				return ValidationResult.Invalid("Target card is not a creature or planeswalker");

			var targetZone = gameState.GetCardZone(TargetId);
			if (targetZone.ZoneType != ZoneType.Battlefield)
				return ValidationResult.Invalid("Target is not on the battlefield");

			if (targetCard.ControllerId == AttackingPlayerId)
				return ValidationResult.Invalid("Cannot attack your own permanent");

			// A planeswalker has no Flying, so CanReach does not apply — but Taunt still does,
			// which is what stops a Taunt creature being ignored in favour of the walker behind it.
			if (isPlaneswalker)
				return ValidateTauntConstraint(gameState, targetCard.ControllerId);

			if (IsCovered(targetCard))
				return ValidationResult.Invalid(
					"This creature is under Cover and can't be attacked"
				);

			if (
				!CanReach(
					gameState.GetEffectiveStats(AttackerId),
					gameState.GetEffectiveStats(TargetId)
				)
			)
				return ValidationResult.Invalid(
					"Only a creature with Flying or Reach can attack a creature with Flying"
				);

			defendingPlayerId = targetCard.ControllerId;
		}
		else
		{
			return ValidationResult.Invalid("Target must be a player or creature");
		}

		return ValidateTauntConstraint(gameState, defendingPlayerId);
	}

	/// <summary>
	/// Flying: a creature with Flying can only be attacked by a creature with Flying or Reach.
	///
	/// This is what makes Flying worth anything in a combat model with no blockers. Without it
	/// the keyword does nothing but bypass Taunt, which is blank whenever the defender has no
	/// Taunt creature — so a card paying for Flying was usually paying for nothing. Reach is
	/// the intended answer, and is therefore worth real card text.
	/// </summary>
	private static bool CanReach(CreatureStats attacker, CreatureStats target) =>
		!target.HasFlying || attacker.HasFlying || attacker.HasReach;

	/// <summary>
	/// Cover N: this creature cannot be attacked at all. Read off the raw CreatureComponent
	/// rather than CreatureStats because it is a countdown, not a keyword — see
	/// CreatureComponent.CoverTurns.
	/// </summary>
	private static bool IsCovered(Card card) =>
		card.GetComponent<CreatureComponent>()?.CoverTurns > 0;

	/// <summary>
	/// Enforces the Taunt rule: if the defending player has Taunt creatures, the attacker
	/// must attack one of them — unless the attacker has Flying and none of the Taunt
	/// creatures have Flying or Reach (in which case the aerial attacker flies over).
	/// </summary>
	private ValidationResult ValidateTauntConstraint(GameState gameState, int defendingPlayerId)
	{
		var opponentBattlefieldId = gameState.GetPlayerZoneId(
			defendingPlayerId,
			ZoneType.Battlefield
		);

		var attackerStats = gameState.GetEffectiveStats(AttackerId);

		// Single pass: collect only taunt creatures — avoids allocating stats for non-taunt creatures
		// and eliminates the three LINQ chains (Where+Select+ToList, two Where+Select+ToHashSet).
		//
		// Only Taunt creatures this attacker could actually attack constrain it. Otherwise a
		// Flying Taunt creature would forbid every ground creature from attacking at all: Taunt
		// would compel an attack that the Flying rule simultaneously forbids.
		List<(int Id, bool HasFlying, bool HasReach)>? tauntCreatures = null;
		foreach (var c in gameState.GetCardsInZone(opponentBattlefieldId))
		{
			if (c.HasComponent<CreatureComponent>())
			{
				var stats = gameState.GetEffectiveStats(c.Id);
				if (!stats.HasTaunt)
					continue;
				if (!CanReach(attackerStats, stats))
					continue;
				// Exactly the reason CanReach is filtered here: Taunt may only compel an attack
				// that is otherwise LEGAL. A creature with both Taunt and Cover would otherwise
				// forbid every attack on its controller — Taunt compelling the one target that
				// Cover simultaneously forbids — which locks combat rather than shaping it.
				if (IsCovered(c))
					continue;
				tauntCreatures ??= [];
				tauntCreatures.Add((c.Id, stats.HasFlying, stats.HasReach));
				continue;
			}

			// A PLANESWALKER can be taunting too — Gideon Jura's "+2: creatures attack Gideon if
			// able" is the whole defensive half of the card, and this scan only ever looked at
			// creatures, so the grant sat on him doing nothing. He is already a legal attack
			// target; this is what makes him a compulsory one.
			//
			// Taunt reaches a walker through the ordinary GrantKeywordAction stamp rather than a
			// bespoke component: an UntilEndOfTurn grant is cleared by StartTurnAction for its
			// controller only, so one applied on your turn lasts exactly through the opponent's
			// next turn — which is what the printed card says, for free.
			if (!c.HasComponent<PlaneswalkerComponent>())
				continue;
			if (!c.GetComponents<AppliedKeywordComponent>().Any(k => k.GrantsTaunt))
				continue;

			// No flying and no reach: a walker cannot obstruct a flier, matching how a
			// ground-bound Taunt creature is flown over rather than blocking the sky.
			tauntCreatures ??= [];
			tauntCreatures.Add((c.Id, false, false));
		}

		if (tauntCreatures is null)
			return ValidationResult.Valid;

		if (attackerStats.HasFlying)
		{
			var hasObstructingTaunt = false;
			var targetIsObstructing = false;
			foreach (var (id, hasFlying, hasReach) in tauntCreatures)
			{
				if (hasFlying || hasReach)
				{
					hasObstructingTaunt = true;
					if (id == TargetId)
						targetIsObstructing = true;
				}
			}

			if (!hasObstructingTaunt)
				return ValidationResult.Valid;
			if (targetIsObstructing)
				return ValidationResult.Valid;
			return ValidationResult.Invalid(
				"A Taunt creature with Flying or Reach must be attacked first"
			);
		}

		foreach (var (id, _, _) in tauntCreatures)
			if (id == TargetId)
				return ValidationResult.Valid;

		return ValidationResult.Invalid("A Taunt creature must be attacked first");
	}

	public override ActionResult Execute(GameState gameState)
	{
		var attacker = (Card)gameState.GetObject(AttackerId);
		var attackerCreature = attacker.GetComponent<CreatureComponent>()!;
		var targetObj = gameState.GetObject(TargetId);

		// Exalted is resolved BEFORE HasAttacked is set, because "attacks alone" is decided by
		// whether any OTHER creature has already attacked this turn.
		var exalted = CountExaltedIfAttackingAlone(gameState);

		// Attacking spends Cover outright — "or until it attacks". The creature is hiding or it is
		// fighting, never both, and without this Cover would be free upside on an aggressive
		// creature instead of protection bought by staying home.
		var state = gameState.UpdateObject(
			AttackerId,
			attacker.WithComponentReplaced(
				attackerCreature with
				{
					HasAttacked = true,
					CoverTurns = 0,
				}
			)
		);

		if (exalted > 0)
		{
			// Added, not replaced — a combat trick already on this creature must survive.
			var stamped = (Card)state.GetObject(AttackerId);
			state = state.UpdateObject(
				AttackerId,
				stamped with
				{
					Components = stamped.Components.Add(
						new StaticPowerToughnessModifier
						{
							PowerBonus = exalted,
							ToughnessBonus = exalted,
							Duration = ModifierDuration.UntilEndOfTurn,
							SourceCardId = AttackerId,
						}
					),
				}
			);
		}

		// Mark the DEFENDER as having been attacked. Fog Bank's Taunt lapses on this, so it must
		// be stamped before any damage or death resolves — a wall that dies to the attack it
		// soaked still counts as having soaked one.
		if (
			state.GetObject(TargetId) is Card defenderCard
			&& defenderCard.GetComponent<CreatureComponent>() is { } defenderCreature
			&& !defenderCreature.WasAttackedThisTurn
		)
			state = state.UpdateObject(
				TargetId,
				defenderCard.WithComponentReplaced(
					defenderCreature with
					{
						WasAttackedThisTurn = true,
					}
				)
			);

		var attackedEvent = new CreatureAttackedEvent
		{
			CreatureId = AttackerId,
			AttackingPlayerId = AttackingPlayerId,
			TargetId = TargetId,
		};
		state = state with { PendingGameEvents = state.PendingGameEvents.Add(attackedEvent) };
		var events = ImmutableList.Create<GameEvent>(attackedEvent);

		// Read stats AFTER the exalted stamp so the bonus is part of this combat.
		var attackerStats = state.GetEffectiveStats(AttackerId);
		var strikeCount = attackerStats.HasDoubleStrike ? 2 : 1;
		// Clamped at 0 for damage purposes. A creature shrunk below 0 power by Sensory Deprivation
		// deals no damage — it does NOT heal what it attacks, which is what a raw negative did to
		// life totals and to marked damage alike.
		var power = Math.Max(0, attackerStats.Power);

		if (targetObj is MtgPlayer targetPlayer)
		{
			var totalDamage = power * strikeCount;
			var (s, e) = ApplyDamageToPlayerStrikes(state, targetPlayer.Id, power, strikeCount);
			state = s;
			events = events.AddRange(e);

			if (attackerStats.HasLifelink)
				state = state.SpawnAction(
					new GainLifeAction
					{
						Amount = totalDamage,
						TargetIds = ImmutableList.Create(AttackingPlayerId),
					}
				);

			return new ActionResult(state) { Events = events };
		}

		// Attacking a planeswalker: loyalty absorbs the damage and nothing strikes back. Lifelink
		// still applies — damage was dealt.
		if (targetObj is Card walkerCard && walkerCard.HasComponent<PlaneswalkerComponent>())
		{
			var totalToWalker = power * strikeCount;
			state = state.DamagePlaneswalker(walkerCard.Id, totalToWalker);

			if (attackerStats.HasLifelink)
				state = state.SpawnAction(
					new GainLifeAction
					{
						Amount = totalToWalker,
						TargetIds = ImmutableList.Create(AttackingPlayerId),
					}
				);

			return new ActionResult(state) { Events = events };
		}

		if (targetObj is Card targetCard)
		{
			var targetStats = state.GetEffectiveStats(targetCard.Id);

			var (s, e) = ApplyCreatureVsCreature(state, targetCard, power, strikeCount);
			state = s;
			events = events.AddRange(e);

			if (attackerStats.HasLifelink)
				state = state.SpawnAction(
					new GainLifeAction
					{
						Amount = power * strikeCount,
						TargetIds = ImmutableList.Create(AttackingPlayerId),
					}
				);

			if (targetStats.HasLifelink)
				state = state.SpawnAction(
					new GainLifeAction
					{
						Amount = Math.Max(0, targetStats.Power),
						TargetIds = ImmutableList.Create(targetCard.ControllerId),
					}
				);

			if (attackerStats.HasTrample)
			{
				var excess = Math.Max(0, power * strikeCount - targetStats.Toughness);
				if (excess > 0)
				{
					var (s2, e2) = ApplyDamageToPlayer(
						state,
						AttackerId,
						(MtgPlayer)state.GetObject(targetCard.ControllerId),
						excess
					);
					state = s2;
					events = events.AddRange(e2);
				}
			}

			return new ActionResult(state) { Events = events };
		}

		return new ActionResult(state) { Events = events };
	}

	private (GameState, ImmutableList<GameEvent>) ApplyDamageToPlayerStrikes(
		GameState state,
		int targetPlayerId,
		int power,
		int strikeCount
	)
	{
		var events = ImmutableList<GameEvent>.Empty;
		for (int i = 0; i < strikeCount; i++)
		{
			if (!state.HasObject(targetPlayerId))
				break;
			var (newState, newEvents) = ApplyDamageToPlayer(
				state,
				AttackerId,
				(MtgPlayer)state.GetObject(targetPlayerId),
				power
			);
			state = newState;
			events = events.AddRange(newEvents);
		}
		return (state, events);
	}

	/// <summary>
	/// Resolves an attack into a creature. Damage is normally simultaneous, so both sides can
	/// die in the exchange.
	///
	/// FIRST STRIKE breaks the simultaneity: the striking side's damage resolves first, and if
	/// it kills the other creature, no damage comes back. With no blockers in this engine that
	/// makes first strike a genuinely premium keyword — every attack into a creature it can kill
	/// becomes a free trade — which is why it costs real card text. Double strike implies first
	/// strike (CreatureStats.StrikesFirst).
	///
	/// If BOTH sides strike first, neither gains anything and damage is simultaneous again,
	/// matching the real rule.
	/// </summary>
	private (GameState, ImmutableList<GameEvent>) ApplyCreatureVsCreature(
		GameState state,
		Card targetCard,
		int power,
		int strikeCount
	)
	{
		// All combat properties are read before any damage, so a creature that dies in this
		// exchange still deals its own damage back where the rules say it should.
		var attackerStats = state.GetEffectiveStats(AttackerId);
		var defenderStats = state.GetEffectiveStats(targetCard.Id);
		var attackerHasDeathtouch = attackerStats.HasDeathtouch;
		var defenderHasDeathtouch = defenderStats.HasDeathtouch;
		// Clamped for the same reason as the attacker's — see Execute.
		var defenderPower = Math.Max(0, defenderStats.Power);

		var attackerStrikesFirst = attackerStats.StrikesFirst;
		var defenderStrikesFirst = defenderStats.StrikesFirst;

		// Defender strikes first and attacker doesn't: the defender's damage lands first and
		// may kill the attacker before it deals any.
		if (defenderStrikesFirst && !attackerStrikesFirst)
		{
			var (s1, e1) = ApplyDamageToCreature(
				state,
				(Card)state.GetObject(AttackerId),
				defenderPower,
				defenderHasDeathtouch
			);

			// Attacker died to the first strike — it never deals its damage.
			if (IsInGraveyard(s1, AttackerId))
				return (s1, e1);

			var (s2, e2) = ApplyDamageToCreature(
				s1,
				(Card)s1.GetObject(targetCard.Id),
				power * strikeCount,
				attackerHasDeathtouch
			);
			return (s2, e1.AddRange(e2));
		}

		var (state2, targetEvents) = ApplyDamageToCreature(
			state,
			targetCard,
			power * strikeCount,
			attackerHasDeathtouch
		);
		var events = targetEvents;

		// Attacker strikes first and the defender is dead: nothing comes back.
		if (attackerStrikesFirst && !defenderStrikesFirst && IsInGraveyard(state2, targetCard.Id))
			return (state2, events);

		var currentAttacker = state2.HasObject(AttackerId)
			? (Card)state2.GetObject(AttackerId)
			: null;

		if (currentAttacker == null)
			return (state2, events);

		var (state3, attackerEvents) = ApplyDamageToCreature(
			state2,
			currentAttacker,
			defenderPower,
			defenderHasDeathtouch
		);
		return (state3, events.AddRange(attackerEvents));
	}

	/// <summary>
	/// A creature that died in combat is moved to its owner's graveyard rather than removed
	/// from the game state, so "did it die?" is a zone check, not an existence check.
	/// </summary>
	private static bool IsInGraveyard(GameState state, int cardId) =>
		state.HasObject(cardId) && state.GetCardZone(cardId).ZoneType == ZoneType.Graveyard;

	/// <summary>
	/// Total exalted instances the attacking player controls, or 0 if this creature is not
	/// attacking alone.
	///
	/// "Attacks alone" is well defined even without a declare-attackers step: no OTHER creature
	/// its controller owns has attacked yet this turn. Must be called before HasAttacked is set
	/// on the attacker.
	///
	/// Instances are counted, not merely detected, because Sublime Archangel grants exalted to
	/// every other creature you control and each instance triggers separately.
	/// </summary>
	private int CountExaltedIfAttackingAlone(GameState state)
	{
		var battlefieldId = state.GetPlayerZoneId(AttackingPlayerId, ZoneType.Battlefield);
		if (battlefieldId == 0)
			return 0;

		var total = 0;

		foreach (var card in state.GetCardsInZone(battlefieldId))
		{
			if (card.ControllerId != AttackingPlayerId)
				continue;

			var creature = card.GetComponent<CreatureComponent>();
			if (creature == null)
				continue;

			if (card.Id != AttackerId && creature.HasAttacked)
				return 0;

			total += card.GetComponent<ExaltedComponent>()?.Count ?? 0;

			foreach (var applied in card.GetComponents<AppliedKeywordComponent>())
				if (applied.GrantsExalted)
					total++;
		}

		return total;
	}

	private static (GameState, ImmutableList<GameEvent>) ApplyDamageToPlayer(
		GameState state,
		int attackerId,
		MtgPlayer player,
		int amount
	)
	{
		// Combat damage NEVER went through the replacement engine, so damage prevention simply did
		// not apply to attacks — the one kind of damage this game is mostly made of. Safe Passage
		// could not do the thing it exists to do. DealDamageAction had always done this; combat was
		// just never wired in.
		amount = state.ApplyReplacements(ReplaceableEvent.DamageToPlayer, player.Id, amount);
		if (amount <= 0)
			return (state, ImmutableList<GameEvent>.Empty);

		// LifeLostThisTurn was not updated here either, so the most common life loss in the game —
		// being attacked — was invisible to every payoff that reads it: bloodthirst, Chandra's
		// Phoenix, Knight of the Ebon Legion.
		var updated = player with
		{
			Life = player.Life - amount,
			LifeLostThisTurn = player.LifeLostThisTurn + amount,
		};
		var newState = state.UpdateObject(player.Id, updated);

		var combatEvent = new CombatDamageDealtToPlayerEvent
		{
			AttackerId = attackerId,
			DefendingPlayerId = player.Id,
			Amount = amount,
		};
		newState = newState with
		{
			PendingGameEvents = newState.PendingGameEvents.Add(combatEvent),
		};

		var events = ImmutableList.Create<GameEvent>(
			new PlayerDamagedEvent { PlayerId = player.Id, Amount = amount },
			combatEvent
		);
		return (newState, events);
	}

	private (GameState, ImmutableList<GameEvent>) ApplyDamageToCreature(
		GameState state,
		Card card,
		int amount,
		bool fromDeathtouch = false
	)
	{
		// Protection from the damage source's creature type: the damage is not dealt at all,
		// so no marked damage and no CreatureDamagedEvent. The other combatant in this exchange
		// is the source — for damage to the defender that is the attacker, and for the return
		// damage it is the defender.
		var sourceId = card.Id == AttackerId ? TargetId : AttackerId;
		if (state.IsProtectedFrom(card.Id, sourceId))
			return (state, ImmutableList<GameEvent>.Empty);

		// Fog Bank prevents combat damage in BOTH directions, so the check covers the creature
		// taking the damage and the one dealing it.
		if (
			card.HasComponent<PreventsCombatDamageComponent>()
			|| (state.GetObject(sourceId) as Card)?.HasComponent<PreventsCombatDamageComponent>()
				== true
		)
			return (state, ImmutableList<GameEvent>.Empty);

		// Same omission as the player path: prevention covers "you AND creatures you control", and
		// the creature half was equally unreachable in combat.
		amount = state.ApplyReplacements(
			ReplaceableEvent.DamageToCreature,
			card.ControllerId,
			amount,
			card.Id
		);
		if (amount <= 0)
			return (state, ImmutableList<GameEvent>.Empty);

		var creature = card.GetComponent<CreatureComponent>()!;
		var newDamage = creature.Damage + amount;
		var events = ImmutableList<GameEvent>.Empty;

		// Use effective toughness — accounts for modifiers
		var updatedCard = card.WithComponentReplaced(creature with { Damage = newDamage });
		state = state.UpdateObject(card.Id, updatedCard);

		if (state.IsLethalDamage(card.Id, newDamage, fromDeathtouch))
		{
			var leftEvent = new PermanentLeftBattlefieldEvent
			{
				CardId = card.Id,
				OwnerId = card.OwnerId,
			};
			state = state with { PendingGameEvents = state.PendingGameEvents.Add(leftEvent) };

			var graveyardId = state.GetPlayerZoneId(card.OwnerId, ZoneType.Graveyard);
			state = state.MoveCardTracked(card.Id, graveyardId);

			var destroyedEvent = new CreatureDestroyedEvent { CreatureId = card.Id };
			events = events.Add(destroyedEvent);
			state = state with { PendingGameEvents = state.PendingGameEvents.Add(destroyedEvent) };
		}
		else
		{
			// Must reach PendingGameEvents, not just the caller-visible log — see the matching
			// comment in DealDamageAction. Combat is the damage source Brash Taunter's Taunt is
			// designed to attract, so this is the half that matters most for it.
			var damagedEvent = new CreatureDamagedEvent { CreatureId = card.Id, Amount = amount };
			events = events.Add(damagedEvent);
			state = state with { PendingGameEvents = state.PendingGameEvents.Add(damagedEvent) };
		}

		return (state, events);
	}
}
