using System.Collections.Immutable;
using ImmutableGameObjects;

namespace KinCore.Party;

/// <summary>
/// Plays a card: its OWNER does what it says. `Space` is where it was dropped — only a card that
/// moves you reads it, and it must be an empty space next to the owner.
/// </summary>
public record PlayPartyCardAction : GameAction
{
	public int CardId { get; init; }
	public int Space { get; init; } = -1;

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

		var owner = s.Owner(card);
		if (owner.IsKnockedOut)
			return ValidationResult.Invalid($"{owner.Name} is knocked out");

		if (party.Energy < card.Cost)
			return ValidationResult.Invalid($"Not enough energy for {card.Name}");

		// Each step of the card that is played ON a space says why this space will not do. Read
		// from where things stand NOW, so a card whose space step comes after a move would be judged
		// from before the move — none does yet.
		foreach (var effect in card.Effects)
			if (
				effect.Template is CardStep { NeedsSpace: true } needs
				&& needs.SpaceRefusal(s, owner, Space) is { } refusal
			)
				return ValidationResult.Invalid(refusal);

		return ValidationResult.Valid;
	}

	public override ActionResult Execute(GameState s)
	{
		var card = (KinCard)s.GetObject(CardId);
		var owner = s.Owner(card);
		var party = s.GetParty();

		s = s.UpdateObject(party.Id, party with { Energy = party.Energy - card.Cost });

		// In card order, so Lunge steps BEFORE it strikes — it attacks from where it lands. **The card
		// is discarded LAST**: discarded first, Feint's draw reshuffled Discard into an empty Draw
		// pile and drew Feint straight back (found in play).
		s = s.SpawnActions(
			card.Effects.Select(e =>
					e.Template is CardStep step
						? step with
						{
							AllyId = owner.Id,
							Space = Space,
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

/// <summary>The played card leaves the hand once everything it does has resolved.</summary>
public record DiscardPlayedCardAction : GameAction
{
	public int CardId { get; init; }

	public override ActionResult Execute(GameState s) =>
		new(s.MoveObject(CardId, s.ZoneId(ZoneType.Discard)));
}

/// <summary>
/// **The free move: one step into an adjacent empty space, then a cooldown set by Speed.** Speed 3
/// moves every turn, 2 every other turn, 1 every third — so where you stand is planned, not
/// re-chosen every turn. Cards that move you ignore this cooldown.
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
			return ValidationResult.Invalid("That companion cannot move");

		if (ally.MoveReadyIn > 0)
			return ValidationResult.Invalid(
				$"{ally.Name} can move again in {ally.MoveReadyIn} turn{(ally.MoveReadyIn == 1 ? "" : "s")}"
			);

		return s.StepRefusal(ally, Space) is { } refusal
			? ValidationResult.Invalid(refusal)
			: ValidationResult.Valid;
	}

	public override ActionResult Execute(GameState s)
	{
		var ally = (Ally)s.GetObject(AllyId);
		s = s.UpdateObject(
			AllyId,
			ally.SteppedTo(Space) with
			{
				MoveReadyIn = CooldownAfterMove(ally.Speed),
			}
		);
		return new ActionResult(s).WithEvent(
			new AllyMovedEvent
			{
				AllyId = AllyId,
				From = ally.Space,
				To = Space,
			}
		);
	}

	/// <summary>Counted down once per turn start: Speed 3 → ready next turn, Speed 1 → in three.</summary>
	public static int CooldownAfterMove(int speed) => 4 - Math.Clamp(speed, 1, 3);
}

/// <summary>
/// Ends your turn: the hand is discarded, every foe does what it telegraphed, left to right, and
/// your next turn starts.
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

		// **Targets are fixed BEFORE anyone acts**, from the same function the board draws the
		// telegraph with. Otherwise a homing attack could re-aim after an earlier foe's hit changed
		// who has the lowest HP, and the board would have shown the wrong target.
		var plan = s.LivingFoes().Select(f => (f.Id, Targets: s.IntentTargets(f))).ToList();

		var events = ImmutableList<GameEvent>.Empty;
		foreach (var (foeId, targets) in plan)
		{
			// Thorns can kill a foe — the last one, even — before it gets to act.
			if (s.GetParty().IsOver)
				break;
			if (((Foe)s.GetObject(foeId)).IsDead)
				continue;

			// A foe's Block lasts through your turn and drops when it next acts.
			s = s.UpdateObject(foeId, Current(s, foeId) with { Block = 0 });
			var foe = Current(s, foeId);
			var intent = foe.Current;

			switch (intent.Kind)
			{
				case IntentType.Attack:
					foreach (var space in targets)
						if (s.AllyAt(space) is { } ally)
						{
							ImmutableList<GameEvent> hit;
							(s, hit) = PartyState.HitAlly(s, ally, intent.Amount, foe.Name);
							events = events.AddRange(hit);

							// **Thorns: attacking this companion hurts**, blocked or not.
							if (
								ally.TotalThorns > 0
								&& Current(s, foeId) is { IsDead: false } struck
							)
							{
								(s, hit) = PartyState.HitFoe(s, struck, ally.TotalThorns);
								events = events.AddRange(hit);
							}
						}
					break;

				case IntentType.Block:
					s = s.UpdateObject(foeId, Current(s, foeId) with { Block = intent.Amount });
					break;

				case IntentType.Move:
					var to = foe.Space + intent.Amount;
					if (to >= 0 && to < PartyBattle.Spaces && s.FoeAt(to) is null)
						s = s.UpdateObject(foeId, Current(s, foeId) with { Space = to });
					break;
			}

			var acted = Current(s, foeId);
			s = s.UpdateObject(foeId, acted with { PatternIndex = acted.PatternIndex + 1 });

			if (s.GetParty().IsOver)
				return new ActionResult(s).WithEvents(events);

			if (!s.LivingAllies().Any())
			{
				var lost = s.GetParty();
				s = s.UpdateObject(lost.Id, lost with { IsOver = true, Won = false });
				return new ActionResult(s).WithEvents(
					events.Add(new PartyBattleEndedEvent { Won = false })
				);
			}
		}

		var party = s.GetParty();
		s = s.UpdateObject(party.Id, party with { TurnNumber = party.TurnNumber + 1 });
		s = s.SpawnAction(new StartPartyTurnAction());
		return new ActionResult(s).WithEvents(events);
	}

	private static Foe Current(GameState s, int foeId) => (Foe)s.GetObject(foeId);
}

/// <summary>Refills energy, drops your Block and Draw Fire, ticks move cooldowns, draws five.</summary>
public record StartPartyTurnAction : GameAction
{
	public const int HandSize = 5;

	public override ActionResult Execute(GameState s)
	{
		var party = s.GetParty();
		s = s.UpdateObject(party.Id, party with { Energy = party.MaxEnergy, DrawFireAllyId = 0 });

		foreach (var ally in s.Allies().ToList())
			s = s.UpdateObject(
				ally.Id,
				ally with
				{
					Block = 0,
					BonusThorns = 0,
					Momentum = 0,
					MoveReadyIn = Math.Max(0, ally.MoveReadyIn - 1),
				}
			);

		foreach (var foe in s.LivingFoes().Where(f => f.OffBalance > 0).ToList())
			s = s.UpdateObject(foe.Id, foe with { OffBalance = 0 });

		(s, _) = StartTurnAction.DrawCards(s, HandSize);
		return new ActionResult(s).WithEvent(
			new TurnStartedEvent { TurnNumber = party.TurnNumber }
		);
	}
}

// ===== What a card does. Each is a template on the card; the owner and drop space are filled in on play.

/// <summary>A step of a card, done BY its owner. <see cref="PlayPartyCardAction"/> fills both ids in.</summary>
public abstract record CardStep : GameAction
{
	public int AllyId { get; init; }
	public int Space { get; init; } = -1;

	/// <summary>True for a step that is played ON a space — a step, a push, a swap.</summary>
	public virtual bool NeedsSpace => false;

	/// <summary>Why the card cannot be dropped on that space, or null if it can.</summary>
	public virtual string? SpaceRefusal(GameState s, Ally owner, int space) => null;

	protected Ally Owner(GameState s) => (Ally)s.GetObject(AllyId);

	/// <summary>A foe this companion moved: Off-Balance, if the companion has the passive.</summary>
	protected static Foe Unbalanced(Foe foe, Ally by) =>
		foe with
		{
			OffBalance = Math.Max(foe.OffBalance, by.Unbalances),
		};
}

/// <summary>
/// **Push the foe AHEAD one column — to the space the card was dropped on.** Every intent is a shape
/// anchored on the foe's column, so this re-aims its attack. With <see cref="Collision"/>, a push
/// into another foe does not move it: both take that much instead (Slam).
/// </summary>
public record PushAction : CardStep
{
	public int Collision { get; init; }

	public override bool NeedsSpace => true;

	public override string? SpaceRefusal(GameState s, Ally owner, int space)
	{
		if (s.FoeAt(owner.Space) is not { } foe)
			return $"There is no foe ahead of {owner.Name}";
		if (space < 0 || space >= PartyBattle.Spaces || Math.Abs(space - owner.Space) != 1)
			return $"Drop it one column left or right of the {foe.Name}";
		if (Collision == 0 && s.FoeAt(space) is { } other)
			return $"The {other.Name} is in the way";
		return null;
	}

	public override ActionResult Execute(GameState s)
	{
		var owner = Owner(s);
		if (s.FoeAt(owner.Space) is not { } foe)
			return new ActionResult(s);

		if (s.FoeAt(Space) is { } other)
		{
			// Slam: it is shoved into the other and neither moves.
			s = s.UpdateObject(foe.Id, Unbalanced(foe, owner));
			var events = ImmutableList<GameEvent>.Empty;
			foreach (var id in new[] { foe.Id, other.Id })
				if (!s.GetParty().IsOver && (Foe)s.GetObject(id) is { IsDead: false } struck)
				{
					ImmutableList<GameEvent> hit;
					(s, hit) = PartyState.HitFoe(s, struck, Collision);
					events = events.AddRange(hit);
				}
			return new ActionResult(s).WithEvents(events);
		}

		s = s.UpdateObject(foe.Id, Unbalanced(foe, owner) with { Space = Space });
		return new ActionResult(s).WithEvent(
			new FoeMovedEvent
			{
				FoeId = foe.Id,
				From = foe.Space,
				To = Space,
			}
		);
	}
}

/// <summary>
/// **Swap the foe ahead with the foe beside it** — dropped on that foe's column. Re-aims two attacks
/// at once, and both are Off-Balance.
/// </summary>
public record SwapAction : CardStep
{
	/// <summary>Damage to BOTH swapped foes, after the swap — Cyclone.</summary>
	public int Damage { get; init; }

	public override bool NeedsSpace => true;

	public override string? SpaceRefusal(GameState s, Ally owner, int space)
	{
		if (s.FoeAt(owner.Space) is not { } foe)
			return $"There is no foe ahead of {owner.Name}";
		if (Math.Abs(space - owner.Space) != 1 || s.FoeAt(space) is null)
			return $"Drop it on a foe beside the {foe.Name}";
		return null;
	}

	public override ActionResult Execute(GameState s)
	{
		var owner = Owner(s);
		if (s.FoeAt(owner.Space) is not { } foe || s.FoeAt(Space) is not { } other)
			return new ActionResult(s);

		s = s.UpdateObject(foe.Id, Unbalanced(foe, owner) with { Space = other.Space });
		s = s.UpdateObject(other.Id, Unbalanced(other, owner) with { Space = foe.Space });
		ImmutableList<GameEvent> events =
		[
			new FoeMovedEvent
			{
				FoeId = foe.Id,
				From = foe.Space,
				To = other.Space,
			},
			new FoeMovedEvent
			{
				FoeId = other.Id,
				From = other.Space,
				To = foe.Space,
			},
		];

		// Cyclone: the two are flung past each other and both take the hit — Off-Balance already on.
		if (Damage > 0)
			foreach (var id in new[] { foe.Id, other.Id })
				if (!s.GetParty().IsOver && (Foe)s.GetObject(id) is { IsDead: false } struck)
				{
					ImmutableList<GameEvent> hit;
					(s, hit) = PartyState.HitFoe(s, struck, Damage);
					events = events.AddRange(hit);
				}

		return new ActionResult(s).WithEvents(events);
	}
}

/// <summary>
/// **The foe ahead is Off-Balance without being moved** — Downdraft: set up a hit on a foe you want
/// to leave exactly where it is.
/// </summary>
public record UnbalanceAction : CardStep
{
	public override ActionResult Execute(GameState s)
	{
		var owner = Owner(s);
		return s.FoeAt(owner.Space) is { } foe
			? new ActionResult(s.UpdateObject(foe.Id, Unbalanced(foe, owner)))
			: new ActionResult(s);
	}
}

/// <summary>Momentum without a step — Quickstep's second half.</summary>
public record MomentumAction : CardStep
{
	public int Amount { get; init; }

	public override ActionResult Execute(GameState s)
	{
		var owner = Owner(s);
		return new ActionResult(
			s.UpdateObject(owner.Id, owner with { Momentum = owner.Momentum + Amount })
		);
	}
}

/// <summary>
/// **Attacks fire straight ahead** from the owner's column: [0] is the foe opposite, [-1, 0, 1]
/// sweeps it and both beside it. No foe in a column hit = that part misses.
/// </summary>
public record StrikeAction : CardStep
{
	public int Amount { get; init; }
	public bool AddPower { get; init; } = true;
	public ImmutableList<int> Offsets { get; init; } = [0];

	/// <summary>Adds the owner's current Block — Retaliate: the Wall hits as hard as it is braced.</summary>
	public bool AddBlock { get; init; }

	/// <summary>Double against a foe with no foe beside it — Flank: pick off the straggler.</summary>
	public bool DoubleIfAlone { get; init; }

	/// <summary>Adds the owner's Thorns — Briar Burst: Bramble cashes in what she has built up.</summary>
	public bool AddThorns { get; init; }

	/// <summary>Goes straight through a foe's Block — Pierce: the answer to a Preen.</summary>
	public bool IgnoreBlock { get; init; }

	public override ActionResult Execute(GameState s)
	{
		var owner = Owner(s);

		// Momentum rides on the whole attack — every column a sweep hits — and is spent by it, hit
		// or miss. "Your next attack" means the next one you make, not the next one that connects.
		var damage =
			Amount
			+ (AddPower ? owner.Power : 0)
			+ (AddBlock ? owner.Block : 0)
			+ (AddThorns ? owner.TotalThorns : 0)
			+ owner.Momentum;
		if (owner.Momentum > 0)
			s = s.UpdateObject(owner.Id, owner with { Momentum = 0 });

		var events = ImmutableList<GameEvent>.Empty;
		foreach (var offset in Offsets)
		{
			if (s.GetParty().IsOver || s.FoeAt(owner.Space + offset) is not { } foe)
				continue;

			var alone = s.FoeAt(foe.Space - 1) is null && s.FoeAt(foe.Space + 1) is null;

			ImmutableList<GameEvent> hit;
			(s, hit) = PartyState.HitFoe(
				s,
				foe,
				DoubleIfAlone && alone ? damage * 2 : damage,
				IgnoreBlock
			);
			events = events.AddRange(hit);
		}

		return new ActionResult(s).WithEvents(events);
	}
}

/// <summary>Thorns for the owner until your next turn starts — Thornhide.</summary>
public record ThornsAction : CardStep
{
	public int Amount { get; init; }

	public override ActionResult Execute(GameState s)
	{
		var owner = Owner(s);
		return new ActionResult(
			s.UpdateObject(owner.Id, owner with { BonusThorns = owner.BonusThorns + Amount })
		);
	}
}

/// <summary>Block for the owner — and, with <see cref="AndBeside"/>, for the companions next to it.</summary>
public record GuardAction : CardStep
{
	public int Amount { get; init; }
	public bool AndBeside { get; init; }

	public override ActionResult Execute(GameState s)
	{
		var owner = Owner(s);
		var guarded = s.LivingAllies()
			.Where(a => a.Id == owner.Id || (AndBeside && Math.Abs(a.Space - owner.Space) == 1))
			.ToList();

		foreach (var ally in guarded)
			s = s.UpdateObject(ally.Id, ally with { Block = ally.Block + Amount });

		return new ActionResult(s).WithEvents(
			[.. guarded.Select(a => new BlockGainedEvent { AllyId = a.Id, Amount = Amount })]
		);
	}
}

/// <summary>Moves the owner to the space the card was dropped on. Ignores the move cooldown.</summary>
public record StepAction : CardStep
{
	public override bool NeedsSpace => true;

	public override string? SpaceRefusal(GameState s, Ally owner, int space) =>
		s.StepRefusal(owner, space);

	public override ActionResult Execute(GameState s)
	{
		var owner = Owner(s);
		s = s.UpdateObject(owner.Id, owner.SteppedTo(Space));
		return new ActionResult(s).WithEvent(
			new AllyMovedEvent
			{
				AllyId = owner.Id,
				From = owner.Space,
				To = Space,
			}
		);
	}
}

public record DrawAction : CardStep
{
	public int Count { get; init; } = 1;

	public override ActionResult Execute(GameState s)
	{
		(s, _) = StartTurnAction.DrawCards(s, Count);
		return new ActionResult(s);
	}
}

/// <summary>This turn, single-target attacks on a companion beside the owner hit the owner.</summary>
public record DrawFireAction : CardStep
{
	public override ActionResult Execute(GameState s)
	{
		var party = s.GetParty();
		return new ActionResult(s.UpdateObject(party.Id, party with { DrawFireAllyId = AllyId }));
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

public record PartyBattleEndedEvent : GameEvent
{
	public bool Won { get; init; }
}
