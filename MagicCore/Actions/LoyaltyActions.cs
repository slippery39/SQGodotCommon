using System.Collections.Immutable;
using ImmutableGameObjects;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Targeting;
using MagicCore.Turns;

namespace MagicCore.Actions;

public static class Loyalty
{
	/// <summary>
	/// A planeswalker's loyalty abilities: its own, then those its controller's permanents grant it
	/// ("Planeswalkers you control have …"). The ONE list — activation indexes into it.
	/// </summary>
	public static ImmutableList<LoyaltyAbilityComponent> AbilitiesOf(GameState s, Card walker) =>
		walker.Is(CardType.Planeswalker) && !s.HasLostAllAbilities(walker) // granted ones go too
			?
			[
				.. s.AbilitiesOf<LoyaltyAbilityComponent>(walker),
				.. s.CardsIn(walker.ControllerId, ZoneType.Battlefield)
					.SelectMany(s.AbilitiesOf<GrantsLoyaltyAbility>)
					.Select(g => g.Ability),
			]
			: [];

	/// <summary>
	/// The ONE place loyalty counters are added or removed — activation costs, damage, effects.
	/// Never below 0: removing counters removes only those there (CR 120.3c), which matters when
	/// Sanctum Lurker keeps a walker alive at 0.
	/// </summary>
	/// <param name="putterId">Who puts them — Inspired Tethermage's "you"; the walker's controller if 0.</param>
	public static GameState Change(GameState s, int walkerId, int delta, int putterId = 0)
	{
		var walker = s.GetCard(walkerId);
		s = s.PutCounters(walkerId, CounterKinds.Loyalty, delta); // Brad Boimler applies there
		return delta > 0
			? s.Raise(new LoyaltyCountersPutEvent(putterId != 0 ? putterId : walker.ControllerId))
			: s;
	}

	public static string CostText(int cost) =>
		cost > 0 ? $"+{cost}"
		: cost == 0 ? "0"
		: $"−{-cost}";
}

/// <summary>
/// CR 606: activate a loyalty ability — your main phase, empty stack, once per turn per
/// planeswalker. The cost is paid in loyalty counters; a minus cost needs that many.
/// </summary>
public record ActivateLoyaltyAction : GameAction
{
	public int PlayerId { get; init; }
	public int CardId { get; init; }
	public int AbilityIndex { get; init; }
	public ImmutableArray<int> Targets { get; init; } = [];

	/// <summary>The X of a "−X" ability; 0 otherwise.</summary>
	public int X { get; init; }

	int CostOf(LoyaltyAbilityComponent ability) => ability.MinusX ? -X : ability.Cost;

	public override ValidationResult ValidateAdd(GameState s)
	{
		if (Priority.Holds(s, PlayerId) is { IsValid: false } noPriority)
			return noPriority;
		if (!s.IsIn(CardId, PlayerId, ZoneType.Battlefield))
			return ValidationResult.Invalid("Not a planeswalker you control");
		var walker = s.GetCard(CardId);
		if (Loyalty.AbilitiesOf(s, walker).ElementAtOrDefault(AbilityIndex) is not { } ability)
			return ValidationResult.Invalid("No such loyalty ability");
		var game = s.GetGame();
		var instantSpeedJace =
			walker.Subtypes.Contains("Jace")
			&& s.ThisTurnEffects<Effects.JaceLoyaltyAtInstantSpeed>(PlayerId).Any();
		if (!instantSpeedJace && !Priority.AtSorcerySpeed(s, PlayerId))
			return ValidationResult.Invalid("Loyalty abilities are sorcery speed");
		if (Turns.Combat.LockedDown(s, PlayerId)) // split second, Yuriko, Sen Triplets
			return ValidationResult.Invalid("No abilities now: combat lockdown or split second");
		if (walker.LoyaltyActivatedOnTurn == game.TurnNumber)
			return ValidationResult.Invalid(
				"Already activated a loyalty ability of this planeswalker this turn"
			);
		if (X < 0 || (X > 0 && !ability.MinusX))
			return ValidationResult.Invalid("Invalid X");
		if (walker.Loyalty + CostOf(ability) < 0)
			return ValidationResult.Invalid("Not enough loyalty");
		if (ability.OnlyIf is { } onlyIf && !onlyIf.Holds(s, Conditions.Of(walker)))
			return ValidationResult.Invalid("Its condition isn't met");
		if (!TargetSpec.AreLegal(s, ability.Targets, Targets, PlayerId, CardId))
			return ValidationResult.Invalid("Illegal targets");
		return ValidationResult.Valid;
	}

	public override ActionResult Execute(GameState s)
	{
		var walker = s.GetCard(CardId);
		var ability = Loyalty.AbilitiesOf(s, walker)[AbilityIndex];
		var cost = CostOf(ability);
		s = Loyalty.Change(s, CardId, cost, PlayerId);
		walker = s.GetCard(CardId) with { LoyaltyActivatedOnTurn = s.GetGame().TurnNumber };
		s = s.UpdateObject(CardId, walker)
			.Tally(
				PlayerId,
				t => t with { LoyaltyAbilitiesActivated = t.LoyaltyAbilitiesActivated + 1 }
			)
			.Raise(new LoyaltyActivatedEvent(PlayerId, cost))
			.Raise(new AbilityActivatedEvent(PlayerId, walker));
		s = StackResolution.Push(
			s,
			new StackAbility
			{
				Name =
					$"{walker.Name} [{(ability.MinusX ? $"−{X}" : Loyalty.CostText(ability.Cost))}]",
				SourceId = CardId,
				Source = walker,
				ControllerId = PlayerId,
				Effects = ability.Effects,
				Specs = ability.Targets,
				Targets = Targets,
				X = X,
			}
		);
		return new ActionResult(Priority.Retain(s, PlayerId));
	}
}
