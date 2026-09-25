using ImmutableGameObjects;

namespace KinCore.Party;

/// <summary>
/// **The companion game's PostActionProcessor: fires the abilities of whatever is ACTIVE** — your
/// monsters on the board and standing (not benched, not fainted) and the living foes. The engine's
/// <see cref="Triggers.FireTriggers"/> does the rest; this only says who counts.
/// </summary>
public record FirePartyTriggersAction : GameAction
{
	public override bool IsPostProcessor => true;

	public override ActionResult Execute(GameState s)
	{
		if (s.PendingGameEvents.IsEmpty)
			return new(s);

		// Counted here, from the staged events, like MtgCore's CountCreatureDeaths: every discard
		// passes through this batch and none can bypass it.
		var discarded = s
			.PendingGameEvents.OfType<CardDiscardedEvent>()
			.Select(d => d.CardId)
			.ToList();
		if (discarded.Count > 0)
		{
			var party = s.GetParty();
			s = s.UpdateObject(
				party.Id,
				party with
				{
					DiscardedThisTurn = party.DiscardedThisTurn + discarded.Count,
				}
			);
		}

		// A discarded card is a source too — for its own TOSS.
		return new(
			s.FireTriggers(
				s.LivingAllies()
					.Select(a => a.Id)
					.Concat(s.LivingFoes().Select(f => f.Id))
					.Concat(discarded)
			)
		);
	}
}

/// <summary>
/// **A card drew cards during your turn.** Staged by <see cref="DrawAction"/> only: the hand dealt at
/// the start of a turn is not "drawing" for an ability.
/// </summary>
public record CardsDrawnEvent : GameEvent
{
	public int Count { get; init; }
}

public record OnCardsDrawn : TriggerRule
{
	public override bool Matches(GameEvent e, GameState s, int sourceId) => e is CardsDrawnEvent;
}

/// <summary>Energy for this turn.</summary>
public record GainEnergyAction : GameAction
{
	public int Amount { get; init; } = 1;

	public override ActionResult Execute(GameState s)
	{
		var party = s.GetParty();
		return new(s.UpdateObject(party.Id, party with { Energy = party.Energy + Amount }));
	}
}

/// <summary>**The creature holding the ability gains Block** — the Hoard Drake's hoard.</summary>
public record GainBlockAction : GameAction, ITriggerBound
{
	public int CreatureId { get; init; }
	public int Amount { get; init; }

	public GameAction Bind(int sourceId, GameEvent trigger) => this with { CreatureId = sourceId };

	public override ActionResult Execute(GameState s)
	{
		if (s.GetObject(CreatureId) is not Creature { IsDown: false } creature)
			return new(s);

		s = s.UpdateObject(CreatureId, creature with { Block = creature.Block + Amount });
		return creature is Ally
			? new ActionResult(s).WithEvent(
				new BlockGainedEvent { AllyId = CreatureId, Amount = Amount }
			)
			: new ActionResult(s);
	}
}

/// <summary>
/// **Damage to a random living foe** — MtgCore's `TargetingStrategy.Random`, drawn from the state's
/// seed so a replay picks the same foe.
/// </summary>
public record DamageRandomFoeAction : GameAction
{
	public int Amount { get; init; }

	public override ActionResult Execute(GameState s)
	{
		var foes = s.LivingFoes().ToList();
		if (foes.Count == 0 || s.GetParty().IsOver)
			return new(s);

		var rng = new Random(s.RngSeed);
		var foe = foes[rng.Next(foes.Count)];
		var (after, events) = PartyState.HitFoe(s with { RngSeed = rng.Next() }, foe, Amount);
		return new ActionResult(after).WithEvents(events);
	}
}
