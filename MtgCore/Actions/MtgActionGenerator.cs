using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Generates all legal actions for the active player in the current game state.
///
/// This is the single shared source of legal action generation used by both
/// the console and the simulator. Never duplicate this logic in presentation layers.
///
/// Performance notes:
///   - Hand is scanned once, categorizing each card in a single pass
///   - Zone IDs are resolved once at the top and reused throughout
///   - TryAddAction is still called for creature/spell actions since their
///     ValidateAdd checks mana and targeting which are non-trivial to inline
///   - Attack validation is inlined since the checks are simple flag reads
/// </summary>
public static class MtgActionGenerator
{
	/// <param name="deduplicateAttackers">
	/// Collapses strategically-identical attackers to one representative action. Correct for AI
	/// search, where it prevents exponential blow-up with many identical tokens; wrong for a
	/// human, who needs every creature to be individually attackable. Presentation layers pass
	/// false.
	/// </param>
	public static List<GameAction> GetLegalActions(
		GameState state,
		MtgGameIds ids,
		int playerId,
		bool deduplicateAttackers = true
	)
	{
		var actions = new List<GameAction>();
		var opponentId = playerId == ids.Player1Id ? ids.Player2Id : ids.Player1Id;

		var handId = playerId == ids.Player1Id ? ids.Player1HandId : ids.Player2HandId;
		var graveyardId =
			playerId == ids.Player1Id ? ids.Player1GraveyardId : ids.Player2GraveyardId;
		var battlefieldId =
			playerId == ids.Player1Id ? ids.Player1BattlefieldId : ids.Player2BattlefieldId;
		var opponentBattlefieldId =
			playerId == ids.Player1Id ? ids.Player2BattlefieldId : ids.Player1BattlefieldId;

		AddHandActions(state, playerId, handId, actions);
		AddGraveyardFlashbackActions(state, playerId, graveyardId, actions);
		AddAttackActions(
			state,
			playerId,
			opponentId,
			battlefieldId,
			opponentBattlefieldId,
			actions,
			deduplicateAttackers
		);
		AddAbilityActions(state, playerId, battlefieldId, actions);
		actions.Add(
			new EndTurnAction
			{
				GameId = ids.GameId,
				Player1Id = ids.Player1Id,
				Player2Id = ids.Player2Id,
			}
		);

		return actions;
	}

	/// <summary>
	/// Generates all legal actions for the given player using well-known IDs from GameState,
	/// without needing the obsolete MtgGameIds struct.
	/// </summary>
	/// <param name="deduplicateAttackers">See the overload above — pass false for a human UI.</param>
	public static List<GameAction> GetLegalActions(
		GameState state,
		int playerId,
		bool deduplicateAttackers = true
	)
	{
		var player1Id = state.GetWellKnownId(MtgObjectKeys.Player1);
		var player2Id = state.GetWellKnownId(MtgObjectKeys.Player2);
		var isPlayer1 = playerId == player1Id;
		var opponentId = isPlayer1 ? player2Id : player1Id;

		var handId = state.GetWellKnownId(
			isPlayer1 ? MtgObjectKeys.Player1Hand : MtgObjectKeys.Player2Hand
		);
		var battlefieldId = state.GetWellKnownId(
			isPlayer1 ? MtgObjectKeys.Player1Battlefield : MtgObjectKeys.Player2Battlefield
		);
		var opponentBattlefieldId = state.GetWellKnownId(
			isPlayer1 ? MtgObjectKeys.Player2Battlefield : MtgObjectKeys.Player1Battlefield
		);
		var gameId = state.GetWellKnownId(MtgObjectKeys.Game);

		var graveyardId = state.GetWellKnownId(
			isPlayer1 ? MtgObjectKeys.Player1Graveyard : MtgObjectKeys.Player2Graveyard
		);

		var actions = new List<GameAction>();
		AddHandActions(state, playerId, handId, actions);
		AddGraveyardFlashbackActions(state, playerId, graveyardId, actions);
		AddAttackActions(
			state,
			playerId,
			opponentId,
			battlefieldId,
			opponentBattlefieldId,
			actions,
			deduplicateAttackers
		);
		AddAbilityActions(state, playerId, battlefieldId, actions);
		actions.Add(
			new EndTurnAction
			{
				GameId = gameId,
				Player1Id = player1Id,
				Player2Id = player2Id,
			}
		);
		return actions;
	}

	// ===== HAND =====

	private static void AddHandActions(
		GameState state,
		int playerId,
		int handId,
		List<GameAction> actions
	)
	{
		foreach (var card in state.GetCardsInZone(handId))
			AddCastableCardAction(state, playerId, card, actions);

		// Impulse draw: a card exiled by ExileTopCardPlayableAction ("you may play it this turn")
		// is offered exactly like a hand card. IsInCastableZone (checked by all three cast
		// actions) is what actually allows the resulting action to validate; this is only the
		// generator's half of the same rule.
		//
		// Driven by MtgGame.PlayableExiledIds rather than by scanning the exile zone. This method
		// runs on every legal-action generation — thousands of times per AI move once rollouts are
		// counted — and exile only ever grows, because every played land is moved there. Scanning
		// it meant the cost of impulse draw was paid by every deck, on every decision, rising with
		// the turn number, even in games where no such card existed.
		//
		// The ids are a hint: re-check existence, the marker, and the zone, so a stale entry is
		// skipped rather than conjuring an action for a card that has moved on. Ordered by id so
		// action order stays deterministic — ImmutableHashSet enumeration order is not a contract.
		var game = state.TryGetGame();
		if (game == null || game.PlayableExiledIds.IsEmpty)
			return;

		var exileId = state.GetPlayerZoneId(playerId, ZoneType.Exile);
		foreach (var cardId in game.PlayableExiledIds.OrderBy(id => id))
		{
			if (!state.HasObject(cardId) || state.GetObject(cardId) is not Card card)
				continue;
			if (!card.HasComponent<ExiledPlayableComponent>())
				continue;
			if (state.GetParent(cardId) != exileId)
				continue;

			AddCastableCardAction(state, playerId, card, actions);
		}
	}

	private static void AddCastableCardAction(
		GameState state,
		int playerId,
		Card card,
		List<GameAction> actions
	)
	{
		// Lands bypass the cost system — no mana cost, no additional costs
		if (card.HasSubtype("Land"))
		{
			AddLandAction(state, playerId, card, actions);
			return;
		}

		// Counter traps fire from hand by themselves and are never cast. Offering one would
		// be offering a blank spell at full price.
		if (card.HasComponent<CounterTrapComponent>())
			return;

		var costPayments = BuildAdditionalCostPayments(
			state,
			playerId,
			card.Id,
			card.AdditionalCastCosts
		);
		if (costPayments == null)
			return;

		if (card.HasComponent<CreatureComponent>())
		{
			AddCreatureAction(state, playerId, card, costPayments, actions);
		}
		else if (card.HasComponent<PermanentComponent>())
		{
			AddPermanentAction(state, playerId, card, costPayments, actions);
		}
		else
		{
			AddSpellAction(state, playerId, card, costPayments, actions);
		}
	}

	private static void AddLandAction(
		GameState state,
		int playerId,
		Card card,
		List<GameAction> actions
	)
	{
		var action = new PlayLandAction { CardId = card.Id, CastingPlayerId = playerId };
		if (state.TryAddAction(action).Success)
			actions.Add(action);
	}

	private static void AddCreatureAction(
		GameState state,
		int playerId,
		Card card,
		ImmutableDictionary<int, ImmutableList<int>> costPayments,
		List<GameAction> actions
	)
	{
		// An {X} creature (the Hydras) is offered once per affordable X, exactly as an X spell is.
		// AffordableXValues returns [0] for everything else, so this costs non-X creatures nothing.
		foreach (var x in AffordableXValues(state, card, playerId))
		{
			var action = new CastCreatureAction
			{
				CardId = card.Id,
				CastingPlayerId = playerId,
				AdditionalCostPayments = costPayments,
				XValue = x,
			};
			if (state.TryAddAction(action).Success)
				actions.Add(action);
		}
	}

	private static void AddPermanentAction(
		GameState state,
		int playerId,
		Card card,
		ImmutableDictionary<int, ImmutableList<int>> costPayments,
		List<GameAction> actions
	)
	{
		// An Aura is offered once per legal target, exactly as a targeted spell is — that is what
		// gives the human the normal targeting prompt and lets the AI choose what to enchant.
		var aura = card.GetComponent<AuraTargetComponent>();
		if (aura != null)
		{
			var context = new TargetingContext
			{
				GameState = state,
				SourceCardId = card.Id,
				CastingPlayerId = playerId,
			};

			foreach (var target in aura.Targeting.GetValidTargets(context))
			{
				var auraAction = new CastPermanentAction
				{
					CardId = card.Id,
					CastingPlayerId = playerId,
					AdditionalCostPayments = costPayments,
					TargetIds = ImmutableList.Create(target),
				};
				if (state.TryAddAction(auraAction).Success)
					actions.Add(auraAction);
			}
			return;
		}

		var action = new CastPermanentAction
		{
			CardId = card.Id,
			CastingPlayerId = playerId,
			AdditionalCostPayments = costPayments,
		};
		if (state.TryAddAction(action).Success)
			actions.Add(action);
	}

	private static void AddSpellAction(
		GameState state,
		int playerId,
		Card card,
		ImmutableDictionary<int, ImmutableList<int>> costPayments,
		List<GameAction> actions
	)
	{
		var spell = card.GetComponent<SpellComponent>();
		if (spell == null)
			return;

		var effectIndex = spell.Effects.FindIndex(e => e.TargetingStrategy.RequiresUserSelection);
		if (effectIndex >= 0)
		{
			AddTargetedSpellAction(state, playerId, card, costPayments, effectIndex, actions);
			return;
		}

		// An X spell is offered once per affordable X. Offering only X=0 would make every
		// X spell look like a blank card to the AI and give a human no way to choose.
		foreach (var x in AffordableXValues(state, card, playerId))
		{
			var castAction = new CastSpellAction
			{
				CardId = card.Id,
				CastingPlayerId = playerId,
				AdditionalCostPayments = costPayments,
				TargetIds = ImmutableDictionary<int, ImmutableList<int>>.Empty,
				XValue = x,
			};
			if (state.TryAddAction(castAction).Success)
				actions.Add(castAction);
		}
	}

	/// <summary>
	/// Every X the player could pay for, or just {0} for a card with no {X} in its cost.
	///
	/// Capped so a huge mana pool cannot explode the action count — beyond a handful, larger X
	/// is almost always strictly better anyway, so the cap costs the AI nothing real.
	/// </summary>
	private static IEnumerable<int> AffordableXValues(GameState state, Card card, int playerId)
	{
		if (!card.HasComponent<XCostComponent>())
			return [0];

		var available = state.GetPlayer(playerId).CurrentMana;
		var maxX = 0;
		while (
			maxX < MaxXChoices && state.ComputeEffectiveCost(card, playerId, maxX + 1) <= available
		)
			maxX++;

		return Enumerable.Range(0, maxX + 1);
	}

	private const int MaxXChoices = 8;

	/// <remarks>
	/// TargetIds is keyed by EFFECT INDEX — CastSpellAction.ValidateTargets looks the targets up
	/// under the index of the effect that needs them. Keying everything under 0 silently made any
	/// spell whose targeted effect was not its first effect impossible to cast, for the AI and
	/// the UI alike, because validation then found no targets for that effect.
	/// </remarks>
	private static void AddTargetedSpellAction(
		GameState state,
		int playerId,
		Card card,
		ImmutableDictionary<int, ImmutableList<int>> costPayments,
		int effectIndex,
		List<GameAction> actions
	)
	{
		var effects = card.GetComponent<SpellComponent>()!.Effects;
		var targetedEffect = effects[effectIndex];
		var context = new TargetingContext
		{
			GameState = state,
			SourceCardId = card.Id,
			CastingPlayerId = playerId,
		};
		var validTargets = targetedEffect.TargetingStrategy.GetValidTargets(context);

		// Every effect that needs a target gets the SAME chosen one. A card like Feat of
		// Resistance reads "put a +1/+1 counter on target creature you control. It gains
		// hexproof" — one target, two effects. Filling only the first index left the second
		// effect with an empty target list, so it silently did nothing and, because
		// ValidateAdd then found no targets for it, the whole spell was never offered at all.
		var targetedIndices = new List<int>();
		for (int i = 0; i < effects.Count; i++)
			if (effects[i].TargetingStrategy.RequiresUserSelection)
				targetedIndices.Add(i);

		// A TARGETED X spell was previously only ever offered at X=0 — this loop was missing here
		// while AddSpellAction had it, so Primal Might's whole cost was unreachable and the card
		// looked blank. AffordableXValues returns [0] for a non-X spell, so nothing else changes.
		foreach (var target in validTargets)
		{
			var targetMap = ImmutableDictionary<int, ImmutableList<int>>.Empty;
			foreach (var index in targetedIndices)
				targetMap = targetMap.Add(index, ImmutableList.Create(target));

			foreach (var x in AffordableXValues(state, card, playerId))
			{
				var castAction = new CastSpellAction
				{
					CardId = card.Id,
					CastingPlayerId = playerId,
					AdditionalCostPayments = costPayments,
					TargetIds = targetMap,
					XValue = x,
				};
				if (state.TryAddAction(castAction).Success)
				{
					actions.Add(castAction);
				}
			}
		}
	}

	// ===== GRAVEYARD (FLASHBACK) =====

	private static void AddGraveyardFlashbackActions(
		GameState state,
		int playerId,
		int graveyardId,
		List<GameAction> actions
	)
	{
		foreach (var card in state.GetCardsInZone(graveyardId))
		{
			var flashback = card.GetComponent<FlashbackComponent>();
			if (flashback == null)
				continue;

			// Flashback can carry costs beyond mana (Despoiler of Souls exiles two other
			// creature cards). Null means they cannot be paid, so the card is not offered —
			// same contract as the hand path.
			var costPayments = BuildAdditionalCostPayments(
				state,
				playerId,
				card.Id,
				flashback.AdditionalCosts
			);
			if (costPayments == null)
				continue;

			var spell = card.GetComponent<SpellComponent>();

			// Creatures with FlashbackComponent are graveyard recursion (Gravecrawler).
			// They carry no SpellComponent and so have no targets to enumerate.
			if (spell == null)
			{
				if (!card.HasComponent<CreatureComponent>())
					continue;

				var recurAction = new CastFromGraveyardAction
				{
					CardId = card.Id,
					CastingPlayerId = playerId,
					TargetIds = ImmutableDictionary<int, ImmutableList<int>>.Empty,
					AdditionalCostPayments = costPayments,
				};
				if (state.TryAddAction(recurAction).Success)
					actions.Add(recurAction);
				continue;
			}

			var effectIndex = spell.Effects.FindIndex(e =>
				e.TargetingStrategy.RequiresUserSelection
			);
			if (effectIndex >= 0)
			{
				AddTargetedFlashbackAction(
					state,
					playerId,
					card,
					effectIndex,
					costPayments,
					actions
				);
			}
			else
			{
				var castAction = new CastFromGraveyardAction
				{
					CardId = card.Id,
					CastingPlayerId = playerId,
					TargetIds = ImmutableDictionary<int, ImmutableList<int>>.Empty,
					AdditionalCostPayments = costPayments,
				};
				if (state.TryAddAction(castAction).Success)
					actions.Add(castAction);
			}
		}
	}

	/// <remarks>See AddTargetedSpellAction — TargetIds is keyed by effect index, not by 0.</remarks>
	private static void AddTargetedFlashbackAction(
		GameState state,
		int playerId,
		Card card,
		int effectIndex,
		ImmutableDictionary<int, ImmutableList<int>> costPayments,
		List<GameAction> actions
	)
	{
		var targetedEffect = card.GetComponent<SpellComponent>()!.Effects[effectIndex];
		var context = new TargetingContext
		{
			GameState = state,
			SourceCardId = card.Id,
			CastingPlayerId = playerId,
		};
		var validTargets = targetedEffect.TargetingStrategy.GetValidTargets(context);

		foreach (var target in validTargets)
		{
			var castAction = new CastFromGraveyardAction
			{
				CardId = card.Id,
				CastingPlayerId = playerId,
				TargetIds = ImmutableDictionary<int, ImmutableList<int>>.Empty.Add(
					effectIndex,
					ImmutableList.Create(target)
				),
				AdditionalCostPayments = costPayments,
			};
			if (state.TryAddAction(castAction).Success)
				actions.Add(castAction);
		}
	}

	// ===== ATTACKS =====

	private static void AddAttackActions(
		GameState state,
		int playerId,
		int opponentId,
		int battlefieldId,
		int opponentBattlefieldId,
		List<GameAction> actions,
		bool deduplicateAttackers
	)
	{
		// Planeswalkers are legal attack targets alongside creatures and the player itself.
		var targetCards = state
			.GetCardsInZone(opponentBattlefieldId)
			.Where(c =>
				c.HasComponent<CreatureComponent>() || c.HasComponent<PlaneswalkerComponent>()
			)
			.ToList();

		// Collapse interchangeable DEFENDERS, the mirror of the attacker dedup below. Attacking
		// one of twenty identical Goblin tokens is the same decision twenty times over, and the
		// two dimensions multiply: without this, deduplicating attackers alone still leaves
		// (signatures x every individual token) actions, each of which the AI pays a full
		// rollout to score.
		//
		// Safe because identical signatures also share legality: what makes an attack legal is
		// reach (Flying/Reach, both in the signature) and the Taunt constraint, which is a
		// question about the whole board and so answers the same for either twin. Same caveat as
		// AttackerSignature — two same-named creatures differing only by an Aura that grants a
		// triggered ability would collapse wrongly; no card does that today.
		if (deduplicateAttackers && targetCards.Count > 1)
		{
			var seenDefenders = new HashSet<DefenderSignature>();
			targetCards = targetCards.Where(c => seenDefenders.Add(SignatureOf(c))).ToList();
		}

		// Everything about a defender that changes what attacking it does: how much damage
		// comes back, whether it survives, whether it can legally be attacked, and what its
		// controller gains. A planeswalker contributes its loyalty instead of combat stats.
		// Local rather than a private method because DefenderSignature is file-local.
		DefenderSignature SignatureOf(Card card)
		{
			var loyalty = card.GetComponent<PlaneswalkerComponent>()?.Loyalty ?? 0;
			var creature = card.GetComponent<CreatureComponent>();
			if (creature == null)
				return new DefenderSignature(card.Name, Loyalty: loyalty);

			// GetEffectiveStats already folds TauntUntilAttacked's WasAttackedThisTurn into
			// HasTaunt, so the conditional-taunt case needs no separate field here.
			var stats = state.GetEffectiveStats(card.Id);
			return new DefenderSignature(
				card.Name,
				stats.Power,
				stats.Toughness,
				creature.Damage,
				stats.HasFirstStrike,
				stats.HasDoubleStrike,
				stats.HasDeathtouch,
				stats.HasIndestructible,
				stats.HasLifelink,
				stats.HasTaunt,
				stats.HasFlying,
				stats.HasReach,
				card.HasComponent<PreventsCombatDamageComponent>(),
				loyalty
			);
		}

		var attackTargets = targetCards.Select(c => c.Id).Prepend(opponentId).ToList();

		// Deduplicate by (target, attacker signature): two creatures with the same name,
		// effective P/T, current damage, and combat-relevant abilities produce identical
		// game outcomes when attacking the same target, so only one representative is needed.
		//
		// This is an AI search optimisation and must be OFF for a human: it suppresses the
		// duplicate's actions entirely, so a player holding two copies of the same creature
		// finds the second one simply unclickable.
		var seen = deduplicateAttackers
			? new HashSet<(int targetId, AttackerSignature sig)>()
			: null;

		foreach (var attacker in state.GetCardsInZone(battlefieldId))
		{
			var creature = attacker.GetComponent<CreatureComponent>();
			if (creature == null || creature.HasAttacked || creature.IsExhausted)
				continue;
			if (creature.HasSummoningSickness && !state.GetEffectiveHaste(attacker.Id))
				continue;

			var stats = state.GetEffectiveStats(attacker.Id);
			var sig = new AttackerSignature(
				attacker.Name,
				stats.Power,
				stats.Toughness,
				creature.Damage,
				stats.HasFlying,
				stats.HasTrample,
				stats.HasDoubleStrike,
				stats.HasLifelink,
				stats.HasFirstStrike,
				stats.HasIndestructible,
				stats.HasDeathtouch
			);

			foreach (var targetId in attackTargets)
			{
				if (seen != null && !seen.Add((targetId, sig)))
					continue;

				var attack = new AttackAction
				{
					AttackerId = attacker.Id,
					TargetId = targetId,
					AttackingPlayerId = playerId,
				};
				if (state.TryAddAction(attack).Success)
					actions.Add(attack);
			}
		}
	}

	// ===== ACTIVATED ABILITIES =====

	private static void AddAbilityActions(
		GameState state,
		int playerId,
		int battlefieldId,
		List<GameAction> actions
	)
	{
		foreach (var card in state.GetCardsInZone(battlefieldId))
		{
			var abilities = card.GetComponents<ActivatedAbilityComponent>().ToList();
			for (int i = 0; i < abilities.Count; i++)
			{
				var ab = abilities[i];
				if (ab.MaxActivationsPerTurn > 0 && ab.ActivationCount >= ab.MaxActivationsPerTurn)
					continue;

				var costPayments = BuildAdditionalCostPayments(
					state,
					playerId,
					card.Id,
					abilities[i].AdditionalCosts
				);
				if (costPayments == null)
					continue;

				var abilityAction = BuildAbilityAction(
					state,
					playerId,
					card.Id,
					i,
					abilities[i],
					costPayments
				);
				if (abilityAction != null && state.TryAddAction(abilityAction).Success)
					actions.Add(abilityAction);
			}
		}
	}

	private static ActivateAbilityAction? BuildAbilityAction(
		GameState state,
		int playerId,
		int cardId,
		int abilityIndex,
		ActivatedAbilityComponent ability,
		ImmutableDictionary<int, ImmutableList<int>> costPayments
	)
	{
		var needsTarget = ability.TargetedEffect?.TargetingStrategy.RequiresUserSelection == true;
		if (needsTarget)
		{
			var context = new TargetingContext
			{
				GameState = state,
				SourceCardId = cardId,
				CastingPlayerId = playerId,
			};
			var validTargets = ability.TargetedEffect!.TargetingStrategy.GetValidTargets(context);

			// A loyalty ability may always be activated, even with nothing to target. Every
			// planeswalker's plus ability reads "up to one target creature", and requiring a
			// target made Ajani, Caller of the Pride offer NO abilities at all on an empty
			// board — so it could never build loyalty toward its ultimate, which is the whole
			// point of a plus ability. The effect simply does nothing; the loyalty still moves.
			if (validTargets.Count == 0 && ability.IsLoyaltyAbility)
				return new ActivateAbilityAction
				{
					CardId = cardId,
					ActivatingPlayerId = playerId,
					AbilityIndex = abilityIndex,
					AdditionalCostPayments = costPayments,
					TargetIds = ImmutableList<int>.Empty,
				};

			if (validTargets.Count == 0)
				return null;

			return new ActivateAbilityAction
			{
				CardId = cardId,
				ActivatingPlayerId = playerId,
				AbilityIndex = abilityIndex,
				AdditionalCostPayments = costPayments,
				TargetIds = ImmutableList.Create(validTargets[0]),
			};
		}

		return new ActivateAbilityAction
		{
			CardId = cardId,
			ActivatingPlayerId = playerId,
			AbilityIndex = abilityIndex,
			AdditionalCostPayments = costPayments,
			TargetIds = ImmutableList<int>.Empty,
		};
	}

	// ===== ADDITIONAL COST HELPERS =====

	/// <summary>
	/// Builds the AdditionalCostPayments dictionary for a cast/activate action.
	/// Returns null if any selection cost has no valid payments (card cannot be played).
	/// Picks the first valid payment for each selection cost — sufficient for AI use.
	/// </summary>
	private static ImmutableDictionary<int, ImmutableList<int>>? BuildAdditionalCostPayments(
		GameState state,
		int playerId,
		int sourceCardId,
		ImmutableList<AdditionalCost> costs
	)
	{
		var payments = ImmutableDictionary<int, ImmutableList<int>>.Empty;
		for (int i = 0; i < costs.Count; i++)
		{
			if (!costs[i].RequiresSelection)
				continue;

			var needed = costs[i].RequiredPaymentCount;
			var validPayments = costs[i].GetValidPayments(state, playerId, sourceCardId);
			if (validPayments.Count < needed)
				return null;

			payments = payments.Add(i, validPayments.Take(needed).ToImmutableList());
		}
		return payments;
	}
}

/// <summary>
/// Identifies a unique attacker profile for deduplication in AddAttackActions.
/// Two creatures with the same signature produce identical outcomes when attacking
/// the same target, so only one representative action is generated per (target, sig) pair.
/// </summary>
/// <summary>
/// The fields that determine an attack's outcome. Two attackers with the same signature
/// attacking the same target produce strategically identical states, so the AI only needs one.
///
/// Every combat-relevant keyword MUST appear here. Omitting one silently merges two creatures
/// that fight differently — a first striker deduped against a vanilla creature of the same size
/// would hide the trade that only one of them wins.
/// </summary>
/// The defender-side counterpart to AttackerSignature. Defaults let a planeswalker be described
/// by name and loyalty alone.
file record struct DefenderSignature(
	string Name,
	int Power = 0,
	int Toughness = 0,
	int Damage = 0,
	bool HasFirstStrike = false,
	bool HasDoubleStrike = false,
	bool HasDeathtouch = false,
	bool HasIndestructible = false,
	bool HasLifelink = false,
	bool HasTaunt = false,
	bool HasFlying = false,
	bool HasReach = false,
	bool PreventsCombatDamage = false,
	int Loyalty = 0
);

file record struct AttackerSignature(
	string Name,
	int Power,
	int Toughness,
	int Damage,
	bool HasFlying,
	bool HasTrample,
	bool HasDoubleStrike,
	bool HasLifelink,
	bool HasFirstStrike,
	bool HasIndestructible,
	bool HasDeathtouch
);
