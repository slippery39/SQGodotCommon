using System.Collections.Immutable;
using ImmutableGameObjects;

namespace KinCore.Party;

/// <summary>
/// **Plays a card ON a space** — one of your monsters, a foe, or an empty foe space, as the card's
/// steps say. No card belongs to a monster: the one it is dropped on is the one it acts on.
/// </summary>
public record PlayPartyCardAction : GameAction
{
	public int CardId { get; init; }
	public int Space { get; init; } = -1;

	/// <summary>Dropped on the FOE row, not yours.</summary>
	public bool FoeRow { get; init; }

	public override ValidationResult ValidateAdd(GameState s)
	{
		var party = s.GetParty();
		if (party.IsOver)
			return ValidationResult.Invalid("The battle is over");

		if (!s.HasObject(CardId) || s.GetParent(CardId) != s.ZoneId(ZoneType.Hand))
			return ValidationResult.Invalid("That card is not in your hand");

		var card = (KinCard)s.GetObject(CardId);
		if (card.Effects.IsEmpty)
			return ValidationResult.Invalid($"{card.Name} does nothing");

		if (party.Energy < s.CostOf(card))
			return ValidationResult.Invalid($"Not enough energy for {card.Name}");

		// Each step says why this space will not do — the board asks space by space to light the
		// legal ones, so the lit spaces and the drop can never disagree.
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
		// discarded LAST: discarded first, a draw could reshuffle it straight back into the hand
		// (found in play with Feint).
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
/// **Throw a Snare at a foe: it is caught** — off the board at once, counted as beaten, and it joins
/// the run with its own cycle if the battle is won. Only at a third of its HP or less, never a boss.
/// An ITEM, not a card: Snares never dilute the deck, and they are carried between battles.
/// </summary>
public record UseSnareAction : GameAction
{
	public const int Cost = 1;

	public int FoeId { get; init; }

	public override ValidationResult ValidateAdd(GameState s)
	{
		if (s.GetParty().IsOver)
			return ValidationResult.Invalid("The battle is over");
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

		ImmutableList<GameEvent> events = [new FoeCaughtEvent { FoeId = FoeId }];
		if (!s.LivingFoes().Any())
		{
			var won = s.GetParty();
			s = s.UpdateObject(won.Id, won with { IsOver = true, Won = true });
			events = events.Add(new PartyBattleEndedEvent { Won = true });
		}
		return new ActionResult(s).WithEvents(events);
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
/// **The free step: every monster, every turn, one space.** Position is AIM — every monster attacks
/// straight ahead — so it is never locked to a card. A step into an ally SWAPS the two (the first
/// run's lock: three in a row and nobody could move). Only the one stepping builds Momentum.
/// </summary>
public record MoveAllyAction : GameAction
{
	public int AllyId { get; init; }
	public int Space { get; init; }

	public override ValidationResult ValidateAdd(GameState s)
	{
		if (s.GetParty().IsOver)
			return ValidationResult.Invalid("The battle is over");

		if (s.GetObject(AllyId) is not Ally { IsKnockedOut: false } ally)
			return ValidationResult.Invalid("That monster cannot move");

		return s.StepRefusal(ally, Space) is { } refusal
			? ValidationResult.Invalid(refusal)
			: ValidationResult.Valid;
	}

	public override ActionResult Execute(GameState s)
	{
		var ally = (Ally)s.GetObject(AllyId);
		var events = ImmutableList<GameEvent>.Empty;

		if (s.AllyAt(Space) is { } other)
		{
			s = s.UpdateObject(other.Id, other with { Space = ally.Space });
			events = events.Add(
				new AllyMovedEvent
				{
					AllyId = other.Id,
					From = Space,
					To = ally.Space,
				}
			);
		}

		s = s.UpdateObject(
			AllyId,
			ally with
			{
				Space = Space,
				StepsLeft = ally.StepsLeft - 1,
				Momentum = ally.Momentum + ally.MomentumPerStep,
			}
		);
		return new ActionResult(s).WithEvents(
			events.Insert(
				0,
				new AllyMovedEvent
				{
					AllyId = AllyId,
					From = ally.Space,
					To = Space,
				}
			)
		);
	}
}

/// <summary>
/// **Ends your turn: every creature plays its move, one at a time, fastest first** — yours and the
/// foes' interleaved (<see cref="PartyState.ActingOrder"/>). Then your next turn starts.
/// </summary>
public record EndPartyTurnAction : GameAction
{
	public override ValidationResult ValidateAdd(GameState s) =>
		s.GetParty().IsOver
			? ValidationResult.Invalid("The battle is over")
			: ValidationResult.Valid;

	public override ActionResult Execute(GameState s)
	{
		foreach (var id in s.GetChildrenIds(s.ZoneId(ZoneType.Hand)).ToList())
			s = s.MoveObject(id, s.ZoneId(ZoneType.Discard));

		var ending = s.GetParty();
		s = s.UpdateObject(ending.Id, ending with { EndingTurn = true });

		// **Order and targets are fixed BEFORE anyone acts**, from the same functions the board draws
		// the badges and the telegraph with. Otherwise a homing attack could re-aim after an earlier
		// hit changed who has the lowest HP, and the board would have shown the wrong target.
		var plan = s.ActingOrder().Select(c => (c.Id, Targets: s.IntentTargets(c))).ToList();

		var events = ImmutableList<GameEvent>.Empty;
		foreach (var (id, targets) in plan)
		{
			if (s.GetParty().IsOver)
				return new ActionResult(s).WithEvents(events);

			switch (s.GetObject(id))
			{
				// Killed before its turn came — a faster monster got there first. That is Speed's job.
				case Creature { IsDown: true }:
				case Ally { HasActed: true }:
					continue;

				case Foe { Staggered: true } staggered:
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

			ImmutableList<GameEvent> acted;
			(s, acted) = PartyState.Act(s, id, targets);
			events = events.AddRange(acted);
		}

		if (s.GetParty().IsOver)
			return new ActionResult(s).WithEvents(events);

		var party = s.GetParty();
		s = s.UpdateObject(party.Id, party with { TurnNumber = party.TurnNumber + 1 });
		s = s.SpawnAction(new StartPartyTurnAction());
		return new ActionResult(s).WithEvents(events);
	}
}

/// <summary>Refills energy, drops your Block and this turn's buffs, gives every step back, draws five.</summary>
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
					Momentum = 0,
					WasHit = false,
					StepsLeft = 1,
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

// ===== What a card does. Each is a template on the card; the drop space is filled in on play.

/// <summary>
/// A step of a card, done to whatever it was dropped on. <see cref="PlayPartyCardAction"/> fills
/// the space in. **By default a card is dropped on one of YOUR monsters** — override
/// <see cref="Refusal"/> for a card aimed at the foes.
/// </summary>
public abstract record CardStep : GameAction
{
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

/// <summary>More steps this turn — Dash. On Pike every one of them is Momentum.</summary>
public record DashAction : CardStep
{
	public int Steps { get; init; } = 1;

	public override ActionResult Execute(GameState s) =>
		Update(s, Target(s) with { StepsLeft = Target(s).StepsLeft + Steps });
}

/// <summary>
/// **The monster plays its move NOW instead of at the end of the turn** — Hasten. Then it can still
/// step away: hit, then dodge. It does not act again at the end.
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

/// <summary>The monster strikes NOW, on top of its move — the deck's damage.</summary>
public record StrikeAction : CardStep
{
	public int Amount { get; init; }
	public ImmutableList<int> Offsets { get; init; } = [0];

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
			Offsets.Select(o => ally.Space + o)
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

/// <summary>
/// **Dropped on an EMPTY foe space: the foe beside it is pushed in** — Gust. Every attack is a shape
/// anchored on the foe's column, so this re-aims it — and lines it up with one of your monsters.
/// </summary>
public record PushAction : CardStep
{
	public override string? Refusal(GameState s, int space, bool foeRow)
	{
		if (!foeRow || space < 0 || space >= PartyBattle.Spaces)
			return "Drop it on an empty space beside a foe";
		if (s.FoeAt(space) is { } there)
			return $"The {there.Name} is standing there";

		var beside = Beside(s, space);
		return beside.Count switch
		{
			0 => "Drop it on an empty space beside a foe",
			1 => null,
			_ => "Two foes are beside that space — drop it where only one is",
		};
	}

	private static List<Foe> Beside(GameState s, int space) =>
		[.. new[] { space - 1, space + 1 }.Select(s.FoeAt).OfType<Foe>()];

	public override ActionResult Execute(GameState s)
	{
		var (after, events) = PartyState.Push(s, Beside(s, Space).Single(), Space);
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
}

public record AllyKnockedOutEvent : GameEvent
{
	public int AllyId { get; init; }
}

public record AllyMovedEvent : GameEvent
{
	public int AllyId { get; init; }
	public int From { get; init; }
	public int To { get; init; }
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
}

public record TrainerHitEvent : GameEvent
{
	public int Damage { get; init; }
	public string By { get; init; } = "";
}

public record LeaderHitEvent : GameEvent
{
	public int Damage { get; init; }
	public string By { get; init; } = "";
}

/// <summary>A benched monster stepped into a fainted one's space.</summary>
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
