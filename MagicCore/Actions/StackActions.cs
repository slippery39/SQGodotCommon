using System.Collections.Immutable;
using ImmutableGameObjects;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Turns;

namespace MagicCore.Actions;

/// <summary>What was decided while casting, carried by the card while it is on the stack.</summary>
public record OnStackComponent(int X) : GameComponent;

/// <summary>
/// CR 601.2, Arena-style: the card goes on the stack, the cost is paid by
/// <see cref="ManaPayment"/>, and the caster keeps priority (CR 117.3c).
/// </summary>
public record CastSpellAction : GameAction
{
	public int PlayerId { get; init; }
	public int CardId { get; init; }
	public int X { get; init; }

	public override ValidationResult ValidateAdd(GameState s)
	{
		if (Priority.Holds(s, PlayerId) is { IsValid: false } noPriority)
			return noPriority;
		if (!s.IsIn(CardId, PlayerId, ZoneType.Hand))
			return ValidationResult.Invalid("Not in your hand");
		var card = s.GetCard(CardId);
		if (card.Is(CardType.Land))
			return ValidationResult.Invalid("Lands are played, not cast");
		if (!CanCastNow(s, card))
			return ValidationResult.Invalid("Sorcery speed: your main phase with an empty stack");
		if (X < 0 || (X > 0 && !card.ManaCost.HasX))
			return ValidationResult.Invalid("Invalid X");
		if (ManaPayment.Find(s, PlayerId, card.ManaCost, X) is null)
			return ValidationResult.Invalid("Cannot pay the cost");
		return ValidationResult.Valid;
	}

	bool CanCastNow(GameState s, Card card)
	{
		if (card.Is(CardType.Instant) || card.Has("Flash"))
			return true;
		var game = s.GetGame();
		return game.ActivePlayerId == PlayerId
			&& game.Step is Step.PreCombatMain or Step.PostCombatMain
			&& s.StackIsEmpty();
	}

	public override ActionResult Execute(GameState s)
	{
		var card = s.GetCard(CardId);
		s = ManaPayment.Pay(s, PlayerId, ManaPayment.Find(s, PlayerId, card.ManaCost, X)!);
		s = s.MoveCard(CardId, s.GetWellKnownId(MagicKeys.Stack), toTop: true); // index 0 is the top
		s = s.UpdateObject(CardId, s.GetCard(CardId).WithComponent(new OnStackComponent(X)));
		return new ActionResult(Priority.Retain(s, PlayerId));
	}
}

public static class StackResolution
{
	/// <summary>
	/// CR 608: the top object resolves. A permanent spell enters under its controller; an instant
	/// or sorcery runs its effects and goes to its owner's graveyard. Then the active player gets
	/// priority (CR 117.3b).
	/// </summary>
	public static GameState ResolveTop(GameState s)
	{
		var stackId = s.GetWellKnownId(MagicKeys.Stack);
		var card = s.GetCard(s.GetChildrenIds(stackId).First());
		s = s.WithGame(s.GetGame() with { PriorityPlayerId = 0 });

		if (!card.Is(CardType.Instant | CardType.Sorcery))
		{
			s = s.EnterBattlefield(card.Id, card.ControllerId);
			return Priority.Retain(s, s.GetGame().ActivePlayerId);
		}

		var effects = card.GetComponent<SpellEffectsComponent>()?.Effects ?? [];
		return s.SpawnAction(
			new PipelineAction
			{
				Steps = [.. effects, new FinishSpellAction { CardId = card.Id }],
				PipelineContext = ImmutableDictionary<string, object>
					.Empty.Add(ContextKeys.Controller, card.ControllerId)
					.Add(ContextKeys.X, card.GetComponent<OnStackComponent>()!.X),
			}
		);
	}
}

public record FinishSpellAction : GameAction
{
	public int CardId { get; init; }

	public override ActionResult Execute(GameState s)
	{
		var card = s.GetCard(CardId);
		s = s.MoveCard(CardId, s.ZoneId(card.OwnerId, ZoneType.Graveyard));
		return new ActionResult(Priority.Retain(s, s.GetGame().ActivePlayerId));
	}
}
