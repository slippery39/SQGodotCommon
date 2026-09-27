using System.Collections.Immutable;
using ImmutableGameObjects;

namespace KinCore.Party;

/// <summary>
/// **Plays a card ON a place in a line** — one of your monsters or one of theirs, as the card's steps
/// say. No card belongs to a monster: the one it is dropped on is the one it acts on.
/// </summary>
public record PlayPartyCardAction : GameAction
{
	public int CardId { get; init; }

	/// <summary>The POSITION it was dropped on, in the line <see cref="FoeRow"/> names.</summary>
	public int Space { get; init; } = -1;

	/// <summary>Dropped on THEIR line, not yours.</summary>
	public bool FoeRow { get; init; }

	public override ValidationResult ValidateAdd(GameState s)
	{
		var party = s.GetParty();
		if (party.IsOver)
			return ValidationResult.Invalid("The battle is over");
		if (party.Deploying)
			return ValidationResult.Invalid(PartyDeploy.Waiting);

		if (!s.HasObject(CardId) || s.GetParent(CardId) != s.ZoneId(ZoneType.Hand))
			return ValidationResult.Invalid("That card is not in your hand");

		var card = (KinCard)s.GetObject(CardId);
		if (card.Effects.IsEmpty)
			return ValidationResult.Invalid($"{card.Name} does nothing");

		if (party.Energy < s.CostOf(card))
			return ValidationResult.Invalid($"Not enough energy for {card.Name}");

		// Each step says why this place will not do — the board asks place by place to light the
		// legal ones, so the lit targets and the drop can never disagree.
		foreach (var effect in card.Effects)
			if (effect.Template is CardStep step && step.Refusal(s, Space, FoeRow) is { } refusal)
				return ValidationResult.Invalid(refusal);

		return ValidationResult.Valid;
	}

	public override ActionResult Execute(GameState s)
	{
		var card = (KinCard)s.GetObject(CardId);
		var party = s.GetParty();

		var cost = s.CostOf(card);
		s = s.UpdateObject(
			party.Id,
			party with
			{
				Energy = party.Energy - cost,
				NextCardFree = false,
				CardsPlayedThisTurn = party.CardsPlayedThisTurn + 1,
				XPaid = card.HasComponent<SpendsAllEnergy>() ? cost : party.XPaid,
			}
		);

		// **It leaves the hand at once, onto the battle — in no zone — while it resolves**, so a
		// choice from the hand ("discard a card") cannot pick the card being played. It is
		// discarded LAST: discarded first, a draw could reshuffle it straight back into the hand.
		s = s.MoveObject(CardId, party.Id);
		if (card.IsSpell())
			s = s.StageEvent(new SpellPlayedEvent());
		s = s.SpawnActions(
			card.Effects.Select(e =>
					e.Template is CardStep step
						? step with
						{
							Space = Space,
							FoeRow = FoeRow,
						}
						: e.Template
				)
				.Append(new DiscardPlayedCardAction { CardId = CardId })
		);

		return new ActionResult(s).WithEvent(
			new CardPlayedEvent
			{
				CardId = CardId,
				CardName = card.Name,
				EnergySpent = card.Cost,
			}
		);
	}
}

/// <summary>
/// **Throw a Snare at a foe: it is caught** — out of the line at once, counted as beaten, and it
/// joins the run with its own cycle if the battle is won. Their FRONT only, a third of its HP or
/// less, never a boss. An ITEM, not a card: Snares never dilute the deck.
/// </summary>
public record UseSnareAction : GameAction
{
	public const int Cost = 1;

	public int FoeId { get; init; }

	public override ValidationResult ValidateAdd(GameState s)
	{
		if (s.GetParty().IsOver)
			return ValidationResult.Invalid("The battle is over");
		if (s.GetParty().Deploying)
			return ValidationResult.Invalid(PartyDeploy.Waiting);
		if (
			!s.HasObject(FoeId)
			|| s.GetObject(FoeId) is not Foe { IsDead: false, Caught: false } foe
		)
			return ValidationResult.Invalid("Throw it at a foe");
		return s.CatchRefusal(foe) is { } refusal
			? ValidationResult.Invalid(refusal)
			: ValidationResult.Valid;
	}

	public override ActionResult Execute(GameState s)
	{
		var party = s.GetParty();
		s = s.UpdateObject(
			party.Id,
			party with
			{
				Energy = party.Energy - Cost,
				Snares = party.Snares - 1,
			}
		);
		s = s.UpdateObject(FoeId, (Foe)s.GetObject(FoeId) with { Caught = true });
		s = s.ReturnStolen(FoeId);
		return new ActionResult(s).WithEvent(new FoeCaughtEvent { FoeId = FoeId });
	}
}

/// <summary>The played card leaves the hand once everything it does has resolved.</summary>
public record DiscardPlayedCardAction : GameAction
{
	public int CardId { get; init; }

	public override ActionResult Execute(GameState s) =>
		new(s.MoveObject(CardId, s.ZoneId(ZoneType.Discard)));
}

/// <summary>
/// **Ends your turn: THE RELAY.** The lines act in STEPS, back to front, BOTH SIDES AT ONCE (R3):
/// everything at the furthest-back position still to act goes together, each aiming from the state
/// at the start of the step; then the step SETTLES (faints, the bench, the lines closing up). Then
/// the next furthest back. A creature that acted early (Hasten) or arrived mid-round sits it out.
/// </summary>
public record EndPartyTurnAction : GameAction
{
	public override ValidationResult ValidateAdd(GameState s) =>
		s.GetParty().IsOver ? ValidationResult.Invalid("The battle is over")
		: s.GetParty().Deploying ? ValidationResult.Invalid(PartyDeploy.Waiting)
		: ValidationResult.Valid;

	public override ActionResult Execute(GameState s)
	{
		foreach (var id in s.GetChildrenIds(s.ZoneId(ZoneType.Hand)).ToList())
			s = s.MoveObject(id, s.ZoneId(ZoneType.Discard));

		var ending = s.GetParty();
		s = s.UpdateObject(ending.Id, ending with { EndingTurn = true });

		// Who acts this round is fixed now; WHEN is by position, re-read each step — a token summoned
		// at the front pushes everyone back, so a loop over positions would skip whoever moved.
		var toAct = s.ActingOrder().Select(c => c.Id).ToHashSet();
		var events = ImmutableList<GameEvent>.Empty;
		ImmutableList<GameEvent> more;

		while (!s.GetParty().IsOver)
		{
			var waiting = s.LivingAllies()
				.Cast<Creature>()
				.Concat(s.LivingFoes())
				.Where(c => toAct.Contains(c.Id))
				.ToList();
			if (waiting.Count == 0)
				break;

			// Within a step, braces and other set-up land before blows — on BOTH sides, so a step stays
			// symmetric: a same-step Brace meets the blow it braced for, whoever's it is.
			var back = waiting.Max(c => c.Position);
			var step = waiting
				.Where(c => c.Position == back)
				.OrderBy(c => c.Current.Kind == IntentType.Attack ? 1 : 0)
				.ThenBy(c => c is Ally ? 0 : 1)
				.Select(c => (c.Id, Targets: s.IntentTargets(c)))
				.ToList();

			foreach (var (id, targets) in step)
			{
				toAct.Remove(id);
				if (s.GetObject(id) is Foe { Staggered: true } staggered)
				{
					s = s.UpdateObject(
						id,
						staggered with
						{
							Staggered = false,
							PatternIndex = staggered.PatternIndex + 1,
						}
					);
					events = events.Add(new FoeStaggeredEvent { FoeId = id });
					continue;
				}

				(s, more) = PartyState.Act(s, id, targets);
				events = events.AddRange(more);
			}

			(s, more) = s.Settle();
			events = events.AddRange(more);
		}

		if (s.GetParty().IsOver)
			return new ActionResult(s).WithEvents(events);

		var party = s.GetParty();
		s = s.UpdateObject(party.Id, party with { TurnNumber = party.TurnNumber + 1 });
		s = s.SpawnAction(new StartPartyTurnAction());
		return new ActionResult(s).WithEvents(events);
	}
}

/// <summary>Refills energy, drops your Block and this turn's buffs, fades tokens, draws five.</summary>
public record StartPartyTurnAction : GameAction
{
	public const int HandSize = 5;

	public override ActionResult Execute(GameState s)
	{
		var party = s.GetParty();
		// **The Glowmoth**: a monster left unhit last turn brings energy. Turn 1 had no last turn.
		var unhit =
			party.TurnNumber == 1
				? 0
				: s.LivingAllies()
					.Where(a => !a.WasHit)
					.SelectMany(a => a.GetComponents<EnergyIfUnhit>())
					.Sum(e => e.Amount);

		s = s.UpdateObject(
			party.Id,
			party with
			{
				Energy = Math.Max(0, party.MaxEnergy - party.EnergyDebt + unhit),
				EnergyDebt = 0,
				NextCardFree = false,
				CardsPlayedThisTurn = 0,
				FoesDefeatedThisTurn = 0,
				EndingTurn = false,
				AlliesActedThisRound = 0,
				DiscardedThisTurn = 0,
				SpellDamageThisTurn = 0,
				SpellsSplash = false,
				LastSpell = null,
			}
		);

		foreach (var ally in s.Allies().ToList())
			s = s.UpdateObject(
				ally.Id,
				ally with
				{
					Block = 0,
					BonusThorns = 0,
					BonusPower = 0,
					WasHit = false,
					HasActed = false,
				}
			);

		foreach (var foe in s.LivingFoes().Where(f => f.OffBalance > 0).ToList())
			s = s.UpdateObject(foe.Id, foe with { OffBalance = 0 });

		foreach (var id in s.Allies().Select(a => a.Id).Concat(s.LivingFoes().Select(f => f.Id)))
			s = s.ResetTriggers(id);

		s = PartySummon.Fade(s);

		(s, _) = StartTurnAction.DrawCards(s, HandSize);
		return new ActionResult(s).WithEvent(
			new TurnStartedEvent { TurnNumber = party.TurnNumber }
		);
	}
}

// ===== What a card does. Each is a template on the card; the drop is filled in on play.

/// <summary>
/// A step of a card, done to whatever it was dropped on. <see cref="PlayPartyCardAction"/> fills the
/// drop in. **By default a card is dropped on one of YOUR monsters** — override
/// <see cref="Refusal"/> for a card aimed at the foes.
/// </summary>
public abstract record CardStep : GameAction
{
	/// <summary>The POSITION it was dropped on, in the line <see cref="FoeRow"/> names.</summary>
	public int Space { get; init; } = -1;

	public bool FoeRow { get; init; }

	/// <summary>Why the card cannot be dropped there, or null if it can.</summary>
	public virtual string? Refusal(GameState s, int space, bool foeRow) =>
		!foeRow && s.AllyAt(space) is not null ? null : "Drop it on one of your monsters";

	/// <summary>The monster it was dropped on.</summary>
	protected Ally Target(GameState s) => s.AllyAt(Space)!;

	protected static ActionResult Update(GameState s, Ally ally) =>
		new(s.UpdateObject(ally.Id, ally));
}

/// <summary>Block for the monster it is dropped on — Guard.</summary>
public record GuardAction : CardStep
{
	public int Amount { get; init; }

	public override ActionResult Execute(GameState s)
	{
		var ally = Target(s);
		s = s.UpdateObject(ally.Id, ally with { Block = ally.Block + Amount });
		return new ActionResult(s).WithEvent(
			new BlockGainedEvent { AllyId = ally.Id, Amount = Amount }
		);
	}
}

/// <summary>Thorns until your next turn starts — Thornhide. On Bramble it stacks on her own.</summary>
public record ThornsAction : CardStep
{
	public int Amount { get; init; }

	public override ActionResult Execute(GameState s) =>
		Update(s, Target(s) with { BonusThorns = Target(s).BonusThorns + Amount });
}

/// <summary>Power until your next turn starts — Rally. It lands when the monster's attack does.</summary>
public record PowerAction : CardStep
{
	public int Amount { get; init; }

	public override ActionResult Execute(GameState s) =>
		Update(s, Target(s) with { BonusPower = Target(s).BonusPower + Amount });
}

/// <summary>
/// **The monster plays its move NOW instead of at the end of the turn** — Hasten. It sits out its
/// step, and it counts for the relay (a FINISHER behind it sees it).
/// </summary>
public record HastenAction : CardStep
{
	public override string? Refusal(GameState s, int space, bool foeRow) =>
		base.Refusal(s, space, foeRow)
		?? (s.AllyAt(space)!.HasActed ? $"{s.AllyAt(space)!.Name} has already acted" : null);

	public override ActionResult Execute(GameState s)
	{
		var ally = Target(s);
		var (after, events) = PartyState.Act(s, ally.Id, s.IntentTargets(ally));
		after = after.UpdateObject(
			ally.Id,
			(Ally)after.GetObject(ally.Id) with
			{
				HasActed = true,
			}
		);
		return new ActionResult(after).WithEvents(events);
	}
}

/// <summary>The monster strikes NOW, on top of its move — the deck's damage. Their front, unless aimed.</summary>
public record StrikeAction : CardStep
{
	public int Amount { get; init; }
	public Aim Aim { get; init; }

	/// <summary>Page Storm: +1 for each card in your hand (the card played has already left it).</summary>
	public bool PlusCardsInHand { get; init; }

	/// <summary>Unleash: + this much for each energy its X paid.</summary>
	public int PerX { get; init; }

	public override ActionResult Execute(GameState s)
	{
		var ally = Target(s);
		var (after, events) = PartyState.AttackFoes(
			s,
			ally,
			Amount
				+ (PlusCardsInHand ? s.CardsIn(ZoneType.Hand).Count() : 0)
				+ PerX * s.GetParty().XPaid,
			PartyState.AimAt(s, ally, Aim)
		);
		return new ActionResult(after).WithEvents(events);
	}
}

public record DrawAction : CardStep
{
	public int Count { get; init; } = 1;

	/// <summary>Reads how many from the pipeline instead — "draw that many" (MtgCore's AmountContextKey).</summary>
	public string CountKey { get; init; } = "";

	public override ActionResult Execute(GameState s)
	{
		var count = CountKey.Length > 0 ? GetInput(CountKey, 0) : Count;
		var before = s.CardsIn(ZoneType.Hand).Count();
		(s, _) = StartTurnAction.DrawCards(s, count);
		var drawn = s.CardsIn(ZoneType.Hand).Count() - before;
		return new ActionResult(
			drawn > 0 ? s.StageEvent(new CardsDrawnEvent { Count = drawn }) : s
		);
	}
}

/// <summary>**Dropped on a FOE: it loses its next move** — Stagger. Its cycle still advances.</summary>
public record StaggerAction : CardStep
{
	public override string? Refusal(GameState s, int space, bool foeRow) =>
		foeRow && s.FoeAt(space) is not null ? null : "Drop it on a foe";

	public override ActionResult Execute(GameState s) =>
		s.FoeAt(Space) is { } foe
			? new ActionResult(s.UpdateObject(foe.Id, foe with { Staggered = true }))
			: new ActionResult(s);
}

// ===== The line verbs — the only way to reorder in a fight (R7).

/// <summary>**Swap the monster it is dropped on with the one ahead of it** — Hold the Line.</summary>
public record SwapAction : CardStep
{
	/// <summary>
	/// **The solo mode** (Shayne, 2026-09-27: "dead cards with one monster"): with nobody ahead to
	/// swap with — at the front, or alone — it gains this much Block instead, so the card is never dead.
	/// </summary>
	public int AloneBlock { get; init; }

	public override string? Refusal(GameState s, int space, bool foeRow) =>
		base.Refusal(s, space, foeRow)
		?? (space == 0 && AloneBlock == 0 ? "It is already at the front" : null);

	public override ActionResult Execute(GameState s)
	{
		if (Space > 0)
			return new(s.MoveInLine(Target(s).Id, Space - 1));
		var ally = Target(s);
		s = s.UpdateObject(ally.Id, ally with { Block = ally.Block + AloneBlock });
		return new ActionResult(s).WithEvent(
			new BlockGainedEvent { AllyId = ally.Id, Amount = AloneBlock }
		);
	}
}

/// <summary>
/// **Send the monster it is dropped on to the FRONT** — Charge. Already there, it stays: the card's
/// other steps (Charge's Power) still count.
/// </summary>
public record RallyAction : CardStep
{
	public override ActionResult Execute(GameState s) => new(s.MoveInLine(Target(s).Id, 0));
}

/// <summary>**Send the monster it is dropped on to the BACK.**</summary>
public record RetreatAction : CardStep
{
	public override string? Refusal(GameState s, int space, bool foeRow) =>
		base.Refusal(s, space, foeRow)
		?? (space == s.LivingAllies().Count() - 1 ? "It is already at the back" : null);

	public override ActionResult Execute(GameState s) =>
		new(s.MoveInLine(Target(s).Id, PartyBattle.MaxLine));
}

/// <summary>
/// **Dropped on their line: their front two swap** — Gust. The foes it moves are Off-Balance while
/// Gale stands.
/// </summary>
public record GustAction : CardStep
{
	/// <summary>
	/// **The solo mode**: against a LONE foe there is nothing to swap, so the gust hits it for this
	/// much instead — the card is never dead in a one-foe fight.
	/// </summary>
	public int AloneDamage { get; init; }

	public override string? Refusal(GameState s, int space, bool foeRow) =>
		!foeRow ? "Drop it on their line"
		: s.LivingFoes().Count() < 2 && AloneDamage == 0 ? "It needs two foes to swap"
		: null;

	public override ActionResult Execute(GameState s)
	{
		if (s.LivingFoes().Count() < 2)
		{
			if (s.LivingFoes().FirstOrDefault() is not { } lone)
				return new(s);
			var (hit, hitEvents) = PartyState.HitFoe(s, lone, AloneDamage);
			return new ActionResult(hit).WithEvents(hitEvents);
		}
		var (after, events) = PartyState.Shove(s, foes: true);
		return new ActionResult(after).WithEvents(events);
	}
}

// ===== Events — the board animates from these; nothing reads them for rules.

public record AllyHitEvent : GameEvent
{
	public int AllyId { get; init; }
	public int Damage { get; init; }
	public int Blocked { get; init; }
	public string By { get; init; } = "";

	/// <summary>The creature whose blow it was — the board lunges it. 0 = no body swung (Thorns, a spell).</summary>
	public int AttackerId { get; init; }
}

public record AllyKnockedOutEvent : GameEvent
{
	public int AllyId { get; init; }
}

public record BlockGainedEvent : GameEvent
{
	public int AllyId { get; init; }
	public int Amount { get; init; }
}

public record FoeMovedEvent : GameEvent
{
	public int FoeId { get; init; }
	public int From { get; init; }
	public int To { get; init; }
}

public record FoeHitEvent : GameEvent
{
	public int FoeId { get; init; }
	public int Damage { get; init; }
	public int Blocked { get; init; }

	/// <summary>The monster whose blow it was — the board lunges it. 0 = no body swung (a spell, Thorns).</summary>
	public int AttackerId { get; init; }
}

/// <summary>A benched monster joined the back of the line for a fallen one.</summary>
public record AllySwappedInEvent : GameEvent
{
	public int AllyId { get; init; }
	public int ForAllyId { get; init; }
}

public record FoeCaughtEvent : GameEvent
{
	public int FoeId { get; init; }
}

public record CardStolenEvent : GameEvent
{
	public int FoeId { get; init; }
	public string CardName { get; init; } = "";
}

public record FoeStaggeredEvent : GameEvent
{
	public int FoeId { get; init; }
}

public record PartyBattleEndedEvent : GameEvent
{
	public bool Won { get; init; }
}
