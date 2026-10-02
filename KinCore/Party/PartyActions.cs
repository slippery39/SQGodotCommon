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
		var steps = card
			.Effects.Select(e =>
				e.Template is CardStep step
					? step with
					{
						Space = Space,
						FoeRow = FoeRow,
					}
					: e.Template
			)
			.ToList();
		if (card.IsSpell())
		{
			// **EMBER**: the chain count rises first — Chain Lightning and the Nth-spell rules read it —
			// then ECHO, Fan the Flames and SPELLWEAVER (`PartyEmber.SpellCast`).
			var now = s.GetParty();
			s = s.UpdateObject(now.Id, now with { SpellsThisTurn = now.SpellsThisTurn + 1 });
			(s, var extra) = PartyEmber.SpellCast(s);
			s = s.StageEvent(new SpellPlayedEvent());
			steps = [.. Enumerable.Range(0, 1 + extra).SelectMany(_ => steps)];
		}
		s = s.SpawnActions(steps.Append(new DiscardPlayedCardAction { CardId = CardId }));

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

		// **BURN ticks as the foes' turn begins** — before any of them acts (`PartyEmber`).
		(s, var burned) = PartyEmber.BurnTick(s);
		(s, var fell) = s.Settle();

		// Who acts this round is fixed now; WHEN is by position, re-read each step — a token summoned
		// at the front pushes everyone back, so a loop over positions would skip whoever moved.
		var toAct = s.ActingOrder().Select(c => c.Id).ToHashSet();
		var events = burned.AddRange(fell);
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
		// **BANK** (Ironhorn): some of the energy left unspent carries over. Turn 1 has none to carry.
		var banked =
			party.TurnNumber == 1
				? 0
				: Math.Min(
					party.Energy,
					s.LivingAllies().SelectMany(a => a.GetComponents<Bank>()).Sum(b => b.Most)
				);
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
				Energy = Math.Max(
					0,
					party.MaxEnergy - party.EnergyDebt + unhit + party.EnergyNextTurn + banked
				),
				EnergyDebt = 0,
				EnergyNextTurn = 0,
				TurnSpellPower = 0,
				SpellDiscount = 0,
				SpellsTwice = 0,
				// INNER FIRE: every turn it holds, the fight's Spell Power climbs.
				FightSpellPower =
					party.FightSpellPower + party.GetComponents<InnerFireAura>().Count(),
				NextCardFree = false,
				CardsPlayedThisTurn = 0,
				FoesDefeatedThisTurn = 0,
				EndingTurn = false,
				AlliesActedThisRound = 0,
				DiscardedThisTurn = 0,
				SpellDamageThisTurn = 0,
				SpellsSplash = false,
				LastSpell = null,
				SpellsThisTurn = 0,
			}
		);

		foreach (var ally in s.Allies().ToList())
			s = s.UpdateObject(
				ally.Id,
				ally with
				{
					BonusThorns = 0,
					BonusPower = 0,
					AttackedThisTurn = false,
					WasHit = false,
					HasActed = false,
				}
			);

		// ENRAGE sharpens every attack after the first round (PartyBosses).
		if (party.TurnNumber > 1)
			s = PartyBosses.RoundStarts(s);

		// Block drops to what is ROOTED; WILD HEART grows your monsters (PartyFamilies).
		(s, var grew) = PartyFamilies.TurnStart(s, firstTurn: party.TurnNumber == 1);

		foreach (var foe in s.LivingFoes().Where(f => f.OffBalance > 0).ToList())
			s = s.UpdateObject(foe.Id, foe with { OffBalance = 0 });

		foreach (var id in s.Allies().Select(a => a.Id).Concat(s.LivingFoes().Select(f => f.Id)))
			s = s.ResetTriggers(id);

		s = PartySummon.Fade(s);

		(s, _) = StartTurnAction.DrawCards(s, HandSize + s.GetParty().DrawBonus);
		return new ActionResult(s)
			.WithEvent(new TurnStartedEvent { TurnNumber = party.TurnNumber })
			.WithEvents(grew);
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

	/// <summary>
	/// **Does this step act on what the card is dropped on?** MtgCore's
	/// `TargetingStrategy.RequiresUserSelection` (Shayne, 2026-09-30). A card with no step that does
	/// is played wherever it is dropped on the field (`PartyState.NeedsTarget`); only steps that
	/// target are asked about the place.
	/// </summary>
	public virtual bool NeedsTarget => true;

	/// <summary>
	/// Why the card cannot be dropped there, or null if it can. An untargeted step ignores the place
	/// and says only why it cannot happen at all ("your line is full").
	/// </summary>
	public virtual string? Refusal(GameState s, int space, bool foeRow) =>
		!NeedsTarget || (!foeRow && s.AllyAt(space) is not null)
			? null
			: "Drop it on one of your monsters";

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

/// <summary>
/// **THORNS** — until your next turn starts (big numbers), or `ForFight` (small ones); on the creature
/// it is dropped on, or `AllLine` every creature on your line. SPORECAP (Hushcap) adds to every card.
/// </summary>
public record ThornsAction : CardStep
{
	public int Amount { get; init; }
	public bool ForFight { get; init; }
	public bool AllLine { get; init; }

	public override bool NeedsTarget => !AllLine;

	public override ActionResult Execute(GameState s)
	{
		var amount = Amount + PartyGrove.Sporecap(s);
		foreach (var ally in AllLine ? s.LivingAllies().ToList() : [Target(s)])
			s = s.UpdateObject(
				ally.Id,
				ForFight
					? ally with
					{
						Thorns = ally.Thorns + amount,
					}
					: ally with
					{
						BonusThorns = ally.BonusThorns + amount,
					}
			);
		return new(s);
	}
}

/// <summary>Power until your next turn starts — Rally. It lands when the monster's attack does.</summary>
public record PowerAction : CardStep
{
	public int Amount { get; init; }

	public override ActionResult Execute(GameState s) =>
		Update(s, Target(s) with { BonusPower = Target(s).BonusPower + Amount });
}

/// <summary>
/// **An ATTACK card: the monster it is played on strikes NOW** — the only way a monster attacks (round
/// 4). Their front, unless aimed. The first on each monster each turn fires its bonus.
/// </summary>
public record StrikeAction : CardStep
{
	public int Amount { get; init; }
	public Aim Aim { get; init; }

	/// <summary>Page Storm: +1 for each card in your hand (the card played has already left it).</summary>
	public bool PlusCardsInHand { get; init; }

	/// <summary>Unleash: + this much for each energy its X paid.</summary>
	public int PerX { get; init; }

	/// <summary>Bark Slam: + this much for each point of its Block — READ, never spent (Grove).</summary>
	public int PerBlock { get; init; }

	/// <summary>Thorn Lash: + this much for each of its Thorns.</summary>
	public int PerThorns { get; init; }

	/// <summary>What this attack would land for, before Block — the card's live number on a drag.</summary>
	public int Damage(GameState s)
	{
		var (bonused, extra, _, _) = PartyMonsters.Attacks(s, Target(s));
		var ally = (Ally)bonused.GetObject(Target(s).Id);
		return PartyState.AttackDamage(bonused, ally, Total(bonused, ally, extra));
	}

	private int Total(GameState s, Ally ally, int extra) =>
		Amount
		+ extra
		+ (PlusCardsInHand ? s.CardsIn(ZoneType.Hand).Count() : 0)
		+ PerX * s.GetParty().XPaid
		+ PerBlock * ally.Block
		+ PerThorns * ally.TotalThorns;

	public override ActionResult Execute(GameState s)
	{
		// **The FIRST attack on this monster this turn fires its bonus** (round 4, `PartyMonsters`).
		var (bonused, extra, bonus, burn) = PartyMonsters.Attacks(s, Target(s));
		var ally = (Ally)bonused.GetObject(Target(s).Id);
		var targets = PartyState.AimAt(bonused, ally, Aim);
		var (after, events) = PartyState.AttackFoes(
			bonused,
			ally,
			Total(bonused, ally, extra),
			targets
		);
		// A first-attack BURN (Cinder Newt) lands on whoever the blow hit.
		foreach (var id in targets)
			if (burn > 0 && after.GetObject(id) is Foe { IsDead: false } hit)
				after = after.UpdateObject(id, hit with { Burn = hit.Burn + burn });
		return new ActionResult(after).WithEvents(bonus.AddRange(events));
	}
}

public record DrawAction : CardStep
{
	public int Count { get; init; } = 1;

	/// <summary>
	/// **A draw targets nothing** — so Ember Dart (deal to a foe, then draw) goes where its damage
	/// goes, and Flicker plays anywhere (playtest, 2026-09-30).
	/// </summary>
	public override bool NeedsTarget => false;

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

	/// <summary>It always moves their FRONT two — nothing to aim.</summary>
	public override bool NeedsTarget => false;

	public override string? Refusal(GameState s, int space, bool foeRow) =>
		s.LivingFoes().Count() < 2 && AloneDamage == 0 ? "It needs two foes to swap" : null;

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

/// <summary>A foe struck a monster with Thorns (or under THORNMAIL) and takes this much back.</summary>
public record ThornsEvent : GameEvent
{
	public int FoeId { get; init; }
	public int Damage { get; init; }
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
