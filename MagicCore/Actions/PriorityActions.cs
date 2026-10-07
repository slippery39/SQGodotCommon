using ImmutableGameObjects;
using MagicCore.Cards;
using MagicCore.Turns;

namespace MagicCore.Actions;

/// <summary>
/// Legality lives in ValidateAdd and nowhere else: <see cref="MagicRules.LegalActions"/>
/// enumerates candidates and keeps the ones that validate, so the generator cannot drift from
/// the rules.
/// </summary>
static class Priority
{
	public static ValidationResult Holds(GameState s, int playerId) =>
		// PriorityPlayerId is 0 while nobody holds priority — a 0 caller must not match it.
		playerId != 0
		&& !s.IsOver()
		&& !s.IsWaitingForChoice
		&& s.GetGame().PriorityPlayerId == playerId
			? ValidationResult.Valid
			: ValidationResult.Invalid("Player does not have priority");

	/// <summary>CR 117.3c: a player who acts gets priority back, and the pass count restarts.</summary>
	public static GameState Retain(GameState s, int playerId) =>
		TurnStructure.GivePriority(
			s.WithGame(s.GetGame() with { PassesInSuccession = 0 }),
			playerId
		);
}

public record PassPriorityAction : GameAction
{
	public int PlayerId { get; init; }

	public override ValidationResult ValidateAdd(GameState s) => Priority.Holds(s, PlayerId);

	public override ActionResult Execute(GameState s)
	{
		var game = s.GetGame();
		var passes = game.PassesInSuccession + 1;

		// CR 117.4: all players passed in succession — resolve the top object, or end the step.
		if (passes >= 2)
			return new ActionResult(
				s.StackIsEmpty() ? TurnStructure.EndStep(s) : StackResolution.ResolveTop(s)
			);

		s = s.WithGame(game with { PassesInSuccession = passes });
		return new ActionResult(TurnStructure.GivePriority(s, s.OpponentOf(PlayerId)));
	}
}

/// <summary>A special action (CR 305.1): no stack, main phase, empty stack, your turn, once per turn.</summary>
public record PlayLandAction : GameAction
{
	public int PlayerId { get; init; }
	public int CardId { get; init; }

	public override ValidationResult ValidateAdd(GameState s)
	{
		if (Priority.Holds(s, PlayerId) is { IsValid: false } noPriority)
			return noPriority;
		var game = s.GetGame();
		if (game.ActivePlayerId != PlayerId)
			return ValidationResult.Invalid("Not your turn");
		if (game.Step is not (Step.PreCombatMain or Step.PostCombatMain) || !s.StackIsEmpty())
			return ValidationResult.Invalid("Lands are played in a main phase with an empty stack");
		if (s.GetPlayer(PlayerId).LandsPlayedThisTurn >= 1)
			return ValidationResult.Invalid("Already played a land this turn");
		if (!s.IsIn(CardId, PlayerId, ZoneType.Hand) || !s.GetCard(CardId).Is(CardType.Land))
			return ValidationResult.Invalid("Not a land in your hand");
		return ValidationResult.Valid;
	}

	public override ActionResult Execute(GameState s)
	{
		s = s.EnterBattlefield(CardId, PlayerId);
		var player = s.GetPlayer(PlayerId);
		s = s.WithPlayer(player with { LandsPlayedThisTurn = player.LandsPlayedThisTurn + 1 });
		return new ActionResult(Priority.Retain(s, PlayerId));
	}
}

/// <summary>
/// Activates a land's mana ability. Only basic land types' intrinsic abilities so far (CR 305.6);
/// nonbasic lands get hand-written abilities with their cards.
///
/// Not offered by LegalActions: tapping with nothing to spend on is pure branching for the AI.
/// Casting will pay costs by tapping through this same action.
/// </summary>
public record TapForManaAction : GameAction
{
	public int PlayerId { get; init; }
	public int CardId { get; init; }
	public ManaColor Color { get; init; }

	public override ValidationResult ValidateAdd(GameState s)
	{
		if (Priority.Holds(s, PlayerId) is { IsValid: false } noPriority)
			return noPriority;
		if (!s.IsIn(CardId, PlayerId, ZoneType.Battlefield) || s.GetCard(CardId).IsTapped)
			return ValidationResult.Invalid("Not an untapped permanent you control");
		if (!ManaAbilities.ColorsOf(s.GetCard(CardId)).Contains(Color))
			return ValidationResult.Invalid($"Cannot produce {Color}");
		return ValidationResult.Valid;
	}

	public override ActionResult Execute(GameState s)
	{
		s = s.UpdateObject(CardId, s.GetCard(CardId) with { IsTapped = true });
		var player = s.GetPlayer(PlayerId);
		s = s.WithPlayer(player with { ManaPool = player.ManaPool.Add(Color) });
		return new ActionResult(Priority.Retain(s, PlayerId));
	}
}

public static class ManaAbilities
{
	public static IEnumerable<ManaColor> ColorsOf(Card card)
	{
		foreach (var subtype in card.Subtypes)
		{
			switch (subtype)
			{
				case "Plains":
					yield return ManaColor.White;
					break;
				case "Island":
					yield return ManaColor.Blue;
					break;
				case "Swamp":
					yield return ManaColor.Black;
					break;
				case "Mountain":
					yield return ManaColor.Red;
					break;
				case "Forest":
					yield return ManaColor.Green;
					break;
			}
		}
	}
}
