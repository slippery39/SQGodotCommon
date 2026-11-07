using ImmutableGameObjects;
using MagicCore.Cards;
using MagicCore.Effects;
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

	/// <summary>
	/// "Any time you could cast a sorcery" (CR 307.1): your turn, a main phase, an empty stack. The ONE
	/// timing rule for sorceries, lands, loyalty abilities and "activate only as a sorcery".
	/// </summary>
	public static bool AtSorcerySpeed(GameState s, int playerId)
	{
		var game = s.GetGame();
		return game.ActivePlayerId == playerId
			&& game.Step is Step.PreCombatMain or Step.PostCombatMain
			&& s.StackIsEmpty();
	}

	/// <summary>A spell castable whenever its controller has priority: an instant, or one with flash (CR 702.8). The ONE read of it.</summary>
	public static bool AtInstantSpeed(GameState s, Card card) =>
		card.Is(CardType.Instant) || s.Has(card, "Flash");

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
		if (!Priority.AtSorcerySpeed(s, PlayerId))
			return ValidationResult.Invalid(
				"Lands are played in your main phase with an empty stack"
			);
		var tally = s.GetPlayer(PlayerId).ThisTurn;
		if (tally.LandsPlayed >= s.LandDrops(PlayerId))
			return ValidationResult.Invalid("Already played a land this turn");
		if (
			!s.HasObject(CardId)
			|| s.GetObject(CardId) is not Card land
			|| !Rules.Playable.CanPlayLandFromHere(s, land, PlayerId)
		)
			return ValidationResult.Invalid(
				"Not a land in your hand, or one you may play from exile"
			);
		return ValidationResult.Valid;
	}

	public override ActionResult Execute(GameState s)
	{
		// The Holodeck: "once during each of your turns" — a land played from the graveyard uses it.
		if (Rules.Playable.GraveyardPermission(s, s.GetCard(CardId), PlayerId) is { } permission)
			s = s.UpdateObject(
				permission.Id,
				permission with
				{
					PermissionUsedOn = s.GetGame().TurnNumber,
				}
			);
		var from = ((Zone)s.GetObject(s.GetParent(CardId)!.Value)).ZoneType;
		s = s.EnterBattlefield(CardId, PlayerId);
		s = s.Tally(PlayerId, t => t with { LandsPlayed = t.LandsPlayed + 1 });
		s = s.Raise(new LandPlayedEvent(PlayerId, CardId, from));
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

	/// <summary>
	/// Phyrexian Tower: "{T}, Sacrifice a creature: Add {B}{B}" — the creature sacrificed; set, this
	/// activates its mana ability whose cost picks one (a move in LegalActions), not its plain yield.
	/// </summary>
	public int SacrificeId { get; init; }

	ManaAbilityComponent? Chosen(GameState s) =>
		ManaAbilities.AbilitiesOf(s, s.GetCard(CardId)).FirstOrDefault(m => m.ChoosesCost);

	public override ValidationResult ValidateAdd(GameState s)
	{
		if (Priority.Holds(s, PlayerId) is { IsValid: false } noPriority)
			return noPriority;
		if (Turns.Combat.LockedDown(s, PlayerId, manaAbility: true))
			return ValidationResult.Invalid("No mana abilities now: Sen Triplets");
		if (SacrificeId != 0)
			return
				s.IsIn(CardId, PlayerId, ZoneType.Battlefield)
				&& Chosen(s) is { Cost: null } ability // ponytail: a mana cost beside a picked one isn't paid here — no card has both
				&& (ability.AnyOneColor || ability.Color == Color)
				&& ability.Costs.All(c => c.CanPay(s, PlayerId, CardId))
				&& CostList
					.Options(ability.Costs, CostSlot.Sacrifice, s, PlayerId, CardId)
					.Contains(SacrificeId)
				? ValidationResult.Valid
				: ValidationResult.Invalid("That mana ability can't be activated so");
		if (
			!s.IsIn(CardId, PlayerId, ZoneType.Battlefield)
			|| !ManaAbilities.CanTap(s, s.GetCard(CardId))
		)
			return ValidationResult.Invalid(
				"Not a permanent you control that can tap for mana now"
			);
		var yield = ManaAbilities.YieldOf(s, s.GetCard(CardId));
		if (yield.Units.IsEmpty || !yield.Units[0].Contains(Color))
			return ValidationResult.Invalid($"Cannot produce {Color}");
		return ValidationResult.Valid;
	}

	/// <summary>Makes its whole yield: the chosen colour, all of it for Loot; Molten Tide's extra {R}.</summary>
	public override ActionResult Execute(GameState s)
	{
		if (SacrificeId != 0)
			return new ActionResult(Priority.Retain(WithChosenCost(s), PlayerId));
		var yield = ManaAbilities.YieldOf(s, s.GetCard(CardId));
		s = ManaPayment.RaiseActivated(s, PlayerId, CardId);
		s = s.Tap(CardId);
		var player = s.GetPlayer(PlayerId);
		var pool = player.ManaPool;
		foreach (var unit in yield.Units.Select((u, i) => (Colors: u, First: i == 0)))
		{
			var color = unit.First || yield.SameColor ? Color : unit.Colors[0];
			// Restricted mana floats as restricted (ManaPool.Restricted) — what it may pay for stays with it.
			pool =
				yield.Restriction == Effects.ManaRestriction.None
					? pool.Add(color)
					: pool with
					{
						Restricted = pool.Restricted.Add(
							new RestrictedMana(color, yield.Restriction)
						),
					};
		}
		s = s.WithPlayer(player with { ManaPool = pool });
		if (yield.Sacrificed)
			s = s.Sacrifice(CardId); // Treasure: "{T}, Sacrifice this"
		return new ActionResult(Priority.Retain(s, PlayerId));
	}

	/// <summary>Pays its costs in order (the {T}, then the creature), then adds its mana to the pool.</summary>
	GameState WithChosenCost(GameState s)
	{
		var ability = Chosen(s)!;
		var n = Math.Max(0, ability.Amount.Value(s, Conditions.Of(s.GetCard(CardId))));
		s = ManaPayment.RaiseActivated(s, PlayerId, CardId);
		foreach (var cost in ability.Costs.OrderByDescending(c => c.Taps))
			s = cost.Pay(s, PlayerId, CardId, cost.Slot == CostSlot.Sacrifice ? SacrificeId : 0);
		var player = s.GetPlayer(PlayerId);
		var pool = Enumerable
			.Range(0, n)
			.Aggregate(
				player.ManaPool,
				(p, _) =>
					ability.Restriction == ManaRestriction.None
						? p.Add(Color)
						: p with
						{
							Restricted = p.Restricted.Add(
								new RestrictedMana(Color, ability.Restriction)
							),
						}
			);
		return s.WithPlayer(player with { ManaPool = pool });
	}
}
