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
		card.Is(CardType.Instant)
		|| s.Has(card, "Flash")
		|| card.HasComponent<CastAsThoughItHadFlash>(); // Necromancy

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

	/// <summary>
	/// Sink into Stupor // Soporific Springs: a modal double-faced card played as its back face, a land (CR 712.12) — judged
	/// and entering as that face (Card.ModalBackFace), front face up again once it leaves.
	/// </summary>
	public bool BackFace { get; init; }

	/// <summary>The land this plays: the card, or its modal back face.</summary>
	Card? Land(GameState s) =>
		s.HasObject(CardId) && s.GetObject(CardId) is Card card
			? BackFace
				? card.ModalBackFace()
				: card
			: null;

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
		if (Land(s) is not { } land || !Rules.Playable.CanPlayLandFromHere(s, land, PlayerId))
			return ValidationResult.Invalid(
				"Not a land in your hand, or one you may play from exile"
			);
		return ValidationResult.Valid;
	}

	public override ActionResult Execute(GameState s)
	{
		// The Holodeck: "once during each of your turns" — a land played from the graveyard uses it.
		var permission = Rules.Playable.ZonePermission(s, Land(s)!, PlayerId);
		if (permission is { Rule.OncePerTurn: true, Holder: var holder })
			s = s.UpdateObject(
				holder.Id,
				holder with
				{
					PermissionUsedOn = s.GetGame().TurnNumber,
				}
			);
		var from = ((Zone)s.GetObject(s.GetParent(CardId)!.Value)).ZoneType;
		s = s.EnterBattlefield(
			CardId,
			PlayerId,
			gains: permission?.Rule.Gains, // Serra Paragon's gift
			transformed: BackFace
		);
		s = s.Tally(PlayerId, t => t with { LandsPlayed = t.LandsPlayed + 1 });
		s = s.Raise(new LandPlayedEvent(PlayerId, CardId, from));
		return new ActionResult(Priority.Retain(s, PlayerId));
	}
}

/// <summary>
/// Abhorrent Oculus' manifest dread: "Turn it face up any time for its mana cost if it's a creature card." A special
/// action (CR 116.2b): any time you have priority, no stack — its mana cost paid, it is what it was (Card.TurnedFaceUp),
/// the same object: no "enters". Offered for a face-down permanent you control whose card is a creature card.
/// </summary>
public record TurnFaceUpAction : GameAction
{
	public int PlayerId { get; init; }
	public int CardId { get; init; }

	/// <summary>The face-down permanent's card, if this player may turn it face up — null otherwise.</summary>
	public static Card? CardOf(GameState s, int playerId, int cardId) =>
		s.HasObject(cardId)
		&& s.IsIn(cardId, playerId, ZoneType.Battlefield)
		&& s.GetCard(cardId).GetComponent<FaceDown>() is { Original: var card }
		&& card.Is(CardType.Creature)
			? card
			: null;

	// ponytail: a Phyrexian symbol is paid with its colour, never 2 life (PayMana's ceiling), and a creature card with
	// no mana cost turns up free rather than never (CR 118.6) — none in the cube's sets; a move per life paid when one is.
	static ManaCost Cost(Card card) => card.ManaCost.PayingLifeFor(0);

	public override ValidationResult ValidateAdd(GameState s)
	{
		if (Priority.Holds(s, PlayerId) is { IsValid: false } noPriority)
			return noPriority;
		if (CardOf(s, PlayerId, CardId) is not { } card)
			return ValidationResult.Invalid("Not a face-down creature card you control");
		return ManaPayment.Find(s, PlayerId, Cost(card), 0) is null
			? ValidationResult.Invalid("Can't pay its mana cost")
			: ValidationResult.Valid;
	}

	public override ActionResult Execute(GameState s)
	{
		var card = CardOf(s, PlayerId, CardId)!;
		s = ManaPayment.Pay(s, PlayerId, ManaPayment.Find(s, PlayerId, Cost(card), 0)!);
		s = s.UpdateObject(CardId, s.GetCard(CardId).TurnedFaceUp());
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

	/// <summary>
	/// Rain of Filth's gained "Sacrifice this land: Add {B}." — set, this activates its mana ability without {T} (a move
	/// in LegalActions, as Phyrexian Tower's), on a tapped land too; never the solver's.
	/// </summary>
	public bool NoTap { get; init; }

	/// <summary>
	/// Orcish Lumberjack: "three mana in any combination of {R} and/or {G}" — how many of the units are the ability's
	/// other colour (ManaAbilityComponent.OrColor); the rest are Color.
	/// </summary>
	public int OfTheOther { get; init; }

	/// <summary>
	/// What tapping it makes, for this colour: its yield, else what its unrestricted abilities make — Delighted
	/// Halfling's {C}, beside its any colour for legendary spells.
	/// </summary>
	ManaAbilities.Yield YieldFor(GameState s) =>
		ManaAbilities.YieldOf(s, s.GetCard(CardId)) is var all
		&& !all.Units.IsEmpty
		&& all.Units[0].Contains(Color)
			? all
			: ManaAbilities.YieldOf(s, s.GetCard(CardId), (null, null));

	ManaAbilityComponent? Chosen(GameState s) =>
		ManaAbilities
			.AbilitiesOf(s, s.GetCard(CardId))
			.FirstOrDefault(m => m.ChoosesCost || m.NoTap);

	public override ValidationResult ValidateAdd(GameState s)
	{
		if (Priority.Holds(s, PlayerId) is { IsValid: false } noPriority)
			return noPriority;
		if (
			Turns.Combat.LockedDown(
				s,
				PlayerId,
				s.HasObject(CardId) ? s.GetObject(CardId) as Card : null,
				manaAbility: true
			)
		)
			return ValidationResult.Invalid(
				"No mana abilities now: Sen Triplets, Clarion Conqueror"
			);
		if (SacrificeId != 0 || NoTap)
			return
				s.IsIn(CardId, PlayerId, ZoneType.Battlefield)
				&& Chosen(s) is { Cost: null } ability // ponytail: a mana cost beside a picked one isn't paid here — no card has both
				&& ability.NoTap == NoTap
				&& (ability.AnyOneColor || ability.Color == Color)
				&& OfTheOther >= 0
				&& OfTheOther
					<= (
						ability.OrColor is null
							? 0
							: ability.Amount.Value(s, Conditions.Of(s.GetCard(CardId)))
					)
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
		var yield = YieldFor(s);
		if (yield.Units.IsEmpty || !yield.Units[0].Contains(Color))
			return ValidationResult.Invalid($"Cannot produce {Color}");
		return ValidationResult.Valid;
	}

	/// <summary>Makes its whole yield: the chosen colour, all of it for Loot; Molten Tide's extra {R}.</summary>
	public override ActionResult Execute(GameState s)
	{
		if (SacrificeId != 0 || NoTap)
			return new ActionResult(Priority.Retain(WithChosenCost(s), PlayerId));
		var yield = YieldFor(s);
		s = ManaPayment.RaiseActivated(s, PlayerId, CardId);
		s = s.Tap(CardId);
		s = ManaAbilities.PayAsUsed(s, PlayerId, s.GetCard(CardId), Color);
		var player = s.GetPlayer(PlayerId);
		var pool = player.ManaPool;
		foreach (var unit in yield.Units.Select((u, i) => (Colors: u, First: i == 0)))
		{
			// a unit of one colour is that colour (Badgermole Cub's additional {G}), whatever was chosen
			var color =
				unit.First || (yield.SameColor && unit.Colors.Length > 1) ? Color : unit.Colors[0];
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
		// Badgermole Cub: "whenever you tap a creature for mana" — only if this ability taps it (Orcish Lumberjack's
		// "{T}, Sacrifice a Forest"; never "Sacrifice this: Add …"), judged before the costs are paid
		ManaColor[] extra = ability.Costs.Any(c => c is TapSelfCost)
			? [.. ManaAbilities.ExtraFor(s, s.GetCard(CardId))]
			: [];
		s = ManaPayment.RaiseActivated(s, PlayerId, CardId);
		foreach (var cost in ability.Costs.OrderByDescending(c => c.Taps))
			s = cost.Pay(s, PlayerId, CardId, cost.Slot == CostSlot.Sacrifice ? SacrificeId : 0);
		var player = s.GetPlayer(PlayerId);
		var pool = Enumerable
			.Range(0, n)
			.Aggregate(
				player.ManaPool,
				(p, i) =>
				{
					var color = i < n - OfTheOther ? Color : ability.OrColor ?? Color;
					return ability.Restriction == ManaRestriction.None
						? p.Add(color)
						: p with
						{
							Restricted = p.Restricted.Add(
								new RestrictedMana(color, ability.Restriction)
							),
						};
				}
			);
		return s.WithPlayer(
			player with
			{
				ManaPool = extra.Aggregate(pool, (p, color) => p.Add(color)),
			}
		);
	}
}
