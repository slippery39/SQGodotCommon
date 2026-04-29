using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Casts a spell from a player's hand.
///
/// ValidateAdd confirms the card exists, is in the player's hand,
/// is controlled by the casting player, has a SpellComponent,
/// has valid targets, and that the player has enough mana.
///
/// Execute moves the card to the stack, spends mana, and spawns ResolveSpellAction.
/// </summary>
public record CastSpellAction : GameAction
{
	public int CardId { get; init; }
	public int CastingPlayerId { get; init; }

	public ImmutableDictionary<int, ImmutableList<int>> TargetIds { get; init; } =
		ImmutableDictionary<int, ImmutableList<int>>.Empty;

	/// <summary>
	/// Payment selections for selection-based additional costs (sacrifice, discard).
	/// Key = index into card.AdditionalCastCosts. Resource costs (life) need no entry here.
	/// </summary>
	public ImmutableDictionary<int, ImmutableList<int>> AdditionalCostPayments { get; init; } =
		ImmutableDictionary<int, ImmutableList<int>>.Empty;

	public override ValidationResult ValidateAdd(GameState gameState)
	{
		if (!gameState.HasObject(CardId))
			return ValidationResult.Invalid($"Card {CardId} does not exist");

		var card = gameState.GetObject(CardId) as Card;
		if (card == null)
			return ValidationResult.Invalid($"Object {CardId} is not a card");

		if (card.ControllerId != CastingPlayerId)
			return ValidationResult.Invalid("You do not control this card");

		var handId = gameState.GetPlayerZoneId(CastingPlayerId, ZoneType.Hand);
		if (gameState.GetCardZoneId(CardId) != handId)
			return ValidationResult.Invalid("Card is not in your hand");

		var spellComponent = card.GetComponent<SpellComponent>();
		if (spellComponent == null)
			return ValidationResult.Invalid("Card is not a spell");

		var player = gameState.GetPlayer(CastingPlayerId);
		if (player.CurrentMana < card.ManaCost)
			return ValidationResult.Invalid(
				$"Not enough mana (have {player.CurrentMana}, need {card.ManaCost})"
			);

		var costsResult = ValidateAdditionalCosts(gameState, card);
		if (!costsResult.IsValid)
			return costsResult;

		return ValidateTargets(gameState, spellComponent);
	}

	public override ActionResult Execute(GameState gameState)
	{
		var card = (Card)gameState.GetObject(CardId);
		var state = PayAdditionalCosts(gameState, card);

		var player = state.GetPlayer(CastingPlayerId);
		state = state.UpdateObject(
			CastingPlayerId,
			player with
			{
				CurrentMana = player.CurrentMana - card.ManaCost,
			}
		);
		state = state.MoveObject(CardId, state.GetStackId());

		var game = state.TryGetGame();
		if (game != null)
			state = state.UpdateObject(
				game.Id,
				game with
				{
					SpellsCastThisTurn = game.SpellsCastThisTurn + 1,
				}
			);

		var castEvent = new SpellCastEvent { CardId = CardId, CastingPlayerId = CastingPlayerId };
		state = state with { PendingGameEvents = state.PendingGameEvents.Add(castEvent) };

		return new ActionResult(
			state.SpawnAction(
				new ResolveSpellAction
				{
					CardId = CardId,
					CastingPlayerId = CastingPlayerId,
					TargetIds = TargetIds,
				}
			)
		).WithEvent(castEvent);
	}

	private ValidationResult ValidateAdditionalCosts(GameState gameState, Card card)
	{
		for (int i = 0; i < card.AdditionalCastCosts.Count; i++)
		{
			var cost = card.AdditionalCastCosts[i];
			var paymentIds =
				cost.RequiresSelection && AdditionalCostPayments.TryGetValue(i, out var ids)
					? ids
					: ImmutableList<int>.Empty;
			var result = cost.Validate(gameState, CastingPlayerId, CardId, paymentIds);
			if (!result.IsValid)
				return result;
		}
		return ValidationResult.Valid;
	}

	private ValidationResult ValidateTargets(GameState gameState, SpellComponent spellComponent)
	{
		var context = new TargetingContext
		{
			GameState = gameState,
			SourceCardId = CardId,
			CastingPlayerId = CastingPlayerId,
		};

		for (int i = 0; i < spellComponent.Effects.Count; i++)
		{
			var effect = spellComponent.Effects[i];
			if (!effect.TargetingStrategy.RequiresUserSelection)
				continue;

			if (!TargetIds.TryGetValue(i, out var targets))
				return ValidationResult.Invalid($"No targets provided for effect {i}");

			if (!effect.TargetingStrategy.ValidateTargets(targets, context))
				return ValidationResult.Invalid($"Invalid targets for effect {i}");
		}
		return ValidationResult.Valid;
	}

	private GameState PayAdditionalCosts(GameState state, Card card)
	{
		for (int i = 0; i < card.AdditionalCastCosts.Count; i++)
		{
			var cost = card.AdditionalCastCosts[i];
			var paymentIds =
				cost.RequiresSelection && AdditionalCostPayments.TryGetValue(i, out var ids)
					? ids
					: ImmutableList<int>.Empty;
			state = cost.Pay(state, CastingPlayerId, CardId, paymentIds);
		}
		return state;
	}
}
