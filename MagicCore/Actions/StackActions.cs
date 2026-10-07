using System.Collections.Immutable;
using ImmutableGameObjects;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Rules;
using MagicCore.Targeting;
using MagicCore.Turns;

namespace MagicCore.Actions;

/// <summary>What was decided while casting, carried by the card while it is on the stack.</summary>
public record OnStackComponent(int X, ImmutableArray<int> Targets) : GameComponent;

/// <summary>A triggered or activated ability on the stack (CR 113.7) — not a card.</summary>
public record StackAbility : GameObject
{
	public int SourceId { get; init; }

	/// <summary>The source as it was when the ability went on the stack — last-known information.</summary>
	public Card Source { get; init; } = null!;
	public int ControllerId { get; init; }
	public ImmutableList<GameAction> Effects { get; init; } = [];
	public ImmutableList<TargetSpec> Specs { get; init; } = [];
	public ImmutableArray<int> Targets { get; init; } = [];
	public int EventCardId { get; init; }
}

/// <summary>
/// CR 601.2, Arena-style: targets chosen, the card goes on the stack, the cost is paid by
/// <see cref="ManaPayment"/>, and the caster keeps priority (CR 117.3c).
/// </summary>
public record CastSpellAction : GameAction
{
	public int PlayerId { get; init; }
	public int CardId { get; init; }
	public int X { get; init; }
	public ImmutableArray<int> Targets { get; init; } = [];

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
		if (!TargetSpec.AreLegal(s, StackResolution.SpecsOf(card), Targets, PlayerId, CardId))
			return ValidationResult.Invalid("Illegal targets");
		if (ManaPayment.Find(s, PlayerId, Costs.Of(s, PlayerId, card), X) is null)
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
		s = ManaPayment.Pay(
			s,
			PlayerId,
			ManaPayment.Find(s, PlayerId, Costs.Of(s, PlayerId, card), X)!
		);
		s = s.MoveCard(CardId, s.GetWellKnownId(MagicKeys.Stack), toTop: true); // index 0 is the top
		s = s.UpdateObject(
			CardId,
			s.GetCard(CardId).WithComponent(new OnStackComponent(X, Targets))
		);
		s = s.Raise(new SpellCastEvent(CardId, PlayerId));
		return new ActionResult(Priority.Retain(s, PlayerId));
	}
}

/// <summary>"[Cost]: [effect]" — pays, taps if {T} is in the cost, and puts the ability on the stack.</summary>
public record ActivateAbilityAction : GameAction
{
	public int PlayerId { get; init; }
	public int CardId { get; init; }
	public int AbilityIndex { get; init; }
	public ImmutableArray<int> Targets { get; init; } = [];

	ActivatedAbilityComponent? Ability(GameState s) =>
		s.GetCard(CardId)
			.GetComponents<ActivatedAbilityComponent>()
			.ElementAtOrDefault(AbilityIndex);

	public override ValidationResult ValidateAdd(GameState s)
	{
		if (Priority.Holds(s, PlayerId) is { IsValid: false } noPriority)
			return noPriority;
		if (!s.IsIn(CardId, PlayerId, ZoneType.Battlefield) || Ability(s) is not { } ability)
			return ValidationResult.Invalid("No such ability on a permanent you control");
		var card = s.GetCard(CardId);
		if (ability.TapCost && !ManaAbilities.CanPayTapCost(s, card))
			return ValidationResult.Invalid("Cannot pay {T}");
		if (!TargetSpec.AreLegal(s, ability.Targets, Targets, PlayerId, CardId))
			return ValidationResult.Invalid("Illegal targets");
		if (
			ManaPayment.Find(s, PlayerId, ability.Cost, 0, excludeId: ability.TapCost ? CardId : 0)
			is null
		)
			return ValidationResult.Invalid("Cannot pay the cost");
		return ValidationResult.Valid;
	}

	public override ActionResult Execute(GameState s)
	{
		var ability = Ability(s)!;
		s = ManaPayment.Pay(
			s,
			PlayerId,
			ManaPayment.Find(s, PlayerId, ability.Cost, 0, ability.TapCost ? CardId : 0)!
		);
		if (ability.TapCost)
			s = s.UpdateObject(CardId, s.GetCard(CardId) with { IsTapped = true });
		s = StackResolution.Push(
			s,
			new StackAbility
			{
				Name = $"{s.GetCard(CardId).Name} ability",
				SourceId = CardId,
				Source = s.GetCard(CardId),
				ControllerId = PlayerId,
				Effects = ability.Effects,
				Specs = ability.Targets,
				Targets = Targets,
			}
		);
		return new ActionResult(Priority.Retain(s, PlayerId));
	}
}

public static class StackResolution
{
	public static ImmutableList<TargetSpec> SpecsOf(Card card) =>
		card.GetComponent<SpellEffectsComponent>()?.Targets ?? [];

	public static GameState Push(GameState s, StackAbility ability)
	{
		var stackId = s.GetWellKnownId(MagicKeys.Stack);
		(s, var added) = s.AddObject(ability, stackId);
		return s.MoveObjectToFront(added.Id, stackId);
	}

	/// <summary>
	/// CR 608: the top object resolves. A permanent spell enters under its controller. An instant,
	/// sorcery or ability runs its effects — unless every target it had is now illegal, when it
	/// does nothing (CR 608.2b). Targets that became illegal read as 0 to the effects. Then the
	/// active player gets priority (CR 117.3b).
	/// </summary>
	public static GameState ResolveTop(GameState s)
	{
		var stackId = s.GetWellKnownId(MagicKeys.Stack);
		var top = s.GetObject(s.GetChildrenIds(stackId).First());
		s = s.WithGame(s.GetGame() with { PriorityPlayerId = 0 });

		if (top is Card permanent && !permanent.Is(CardType.Instant | CardType.Sorcery))
		{
			s = s.EnterBattlefield(permanent.Id, permanent.ControllerId);
			return Priority.Retain(s, s.GetGame().ActivePlayerId);
		}

		var (controller, sourceId, snapshot, effects, specs, chosen, x, eventCard, finish) =
			top switch
			{
				Card spell => (
					spell.ControllerId,
					spell.Id,
					spell,
					spell.GetComponent<SpellEffectsComponent>()?.Effects ?? [],
					SpecsOf(spell),
					spell.GetComponent<OnStackComponent>()!.Targets,
					spell.GetComponent<OnStackComponent>()!.X,
					0,
					(GameAction)new FinishSpellAction { CardId = spell.Id }
				),
				StackAbility ability => (
					ability.ControllerId,
					ability.SourceId,
					ability.Source,
					ability.Effects,
					ability.Specs,
					ability.Targets,
					0,
					ability.EventCardId,
					new FinishAbilityAction { AbilityId = ability.Id }
				),
				_ => throw new InvalidOperationException(
					$"Unknown stack object {top.GetType().Name}"
				),
			};

		var legal = chosen
			.Select((id, i) => id != 0 && specs[i].IsLegal(s, controller, sourceId, id) ? id : 0)
			.ToImmutableArray();
		var fizzles = chosen.Any(id => id != 0) && legal.All(id => id == 0);

		return s.SpawnAction(
			new PipelineAction
			{
				Steps = fizzles ? [finish] : [.. effects, finish],
				PipelineContext = ImmutableDictionary<string, object>
					.Empty.Add(ContextKeys.Controller, controller)
					.Add(ContextKeys.Source, sourceId)
					.Add(ContextKeys.SourceSnapshot, snapshot)
					.Add(ContextKeys.X, x)
					.Add(ContextKeys.Targets, legal)
					.Add(ContextKeys.EventCard, eventCard),
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

public record FinishAbilityAction : GameAction
{
	public int AbilityId { get; init; }

	public override ActionResult Execute(GameState s) =>
		new(Priority.Retain(s.RemoveObject(AbilityId), s.GetGame().ActivePlayerId));
}
