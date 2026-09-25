using System.Collections.Immutable;
using ImmutableGameObjects;

namespace KinCore.Party;

/// <summary>
/// What ending the turn now would cost: each creature's HP lost (by id), and the health you and the
/// gym leader would lose.
/// </summary>
public record Forecast(ImmutableDictionary<int, int> Hp, int Trainer, int Leader);

/// <summary>
/// The API boundary for the companion game. The Godot board reads everything through here and
/// never computes a game fact itself — including where an attack will land and who acts when.
/// </summary>
public static class PartyState
{
	public const string BattleKey = "PartyBattle";

	public static PartyBattle GetParty(this GameState s) =>
		(PartyBattle)s.GetObject(s.GetWellKnownId(BattleKey));

	/// <summary>Every monster of yours, knocked out or not, in board order.</summary>
	public static IEnumerable<Ally> Allies(this GameState s) =>
		s.GetChildren(s.GetWellKnownId(BattleKey)).OfType<Ally>().OrderBy(a => a.Space);

	/// <summary>Your monsters ON THE BOARD and standing — not fainted, not on the bench.</summary>
	public static IEnumerable<Ally> LivingAllies(this GameState s) =>
		s.Allies().Where(a => !a.IsKnockedOut && !a.Benched);

	/// <summary>
	/// **A monster's own deck joins the draw pile** — at the start of a battle it fights, or when it
	/// steps in from the bench. Until then its cards wait under it, in no zone.
	/// </summary>
	public static GameState DeployDeck(this GameState s, int allyId)
	{
		foreach (var id in s.GetChildrenIds(allyId).ToList())
			s = s.MoveObject(id, s.ZoneId(ZoneType.Draw));
		return s;
	}

	/// <summary>
	/// **When a monster faints, its cards leave every zone** and wait under it again — no dead
	/// draws. Faints happen at END of turn, after the hand is discarded, so none leaves a hand.
	/// </summary>
	public static GameState WithdrawDeck(this GameState s, int allyId)
	{
		foreach (var zone in new[] { ZoneType.Draw, ZoneType.Hand, ZoneType.Discard })
		foreach (var card in s.CardsIn(zone).Where(c => c.OwnerId == allyId).ToList())
			s = s.MoveObject(card.Id, allyId);
		return s;
	}

	/// <summary>
	/// **What a card costs to play NOW — the one place a cost is adjusted** (MtgCore's CostEngine:
	/// every reduction and tax goes through here, floored at 0). Paying, validating and the hand's
	/// cost badge all read it, so they cannot disagree.
	/// </summary>
	public static int CostOf(this GameState s, KinCard card)
	{
		var party = s.GetParty();
		if (party.NextCardFree)
			return 0;
		if (card.HasComponent<SpendsAllEnergy>())
			return party.Energy;

		var cost = card.Cost;
		if (card.GetComponent<CostReduction>() is { } reduction)
			cost -= reduction.PerDiscardThisTurn * party.DiscardedThisTurn;

		// The first card each turn: monsters' discounts and foes' taxes, from both sides of the board.
		var first =
			party.CardsPlayedThisTurn == 0
				? s.LivingAllies()
					.Cast<Creature>()
					.Concat(s.LivingFoes())
					.SelectMany(c => c.GetComponents<FirstCardCost>())
					.Select(f => f.Amount)
					.ToList()
				: [];

		// MtgCore's order: reductions first, floored, THEN taxes — a card cut to 0 still pays a tax.
		cost = Math.Max(0, cost + first.Where(a => a < 0).Sum());
		return Math.Max(0, cost + first.Where(a => a > 0).Sum());
	}

	/// <summary>
	/// **A thief's haul goes back to your discard pile** when it is beaten or caught. A card whose
	/// monster has fainted goes back under it instead — no dead draws.
	/// </summary>
	public static GameState ReturnStolen(this GameState s, int foeId)
	{
		foreach (var card in s.GetChildren(foeId).OfType<KinCard>().ToList())
			s = s.MoveObject(
				card.Id,
				card.OwnerId != 0 && ((Ally)s.GetObject(card.OwnerId)).IsKnockedOut
					? card.OwnerId
					: s.ZoneId(ZoneType.Discard)
			);
		return s;
	}

	/// <summary>The bench, in the order it steps in.</summary>
	public static IEnumerable<Ally> BenchedAllies(this GameState s) =>
		s.Allies().Where(a => a.Benched && !a.IsKnockedOut).OrderBy(a => a.Slot);

	/// <summary>Every foe still standing — not beaten, not caught — left to right.</summary>
	public static IEnumerable<Foe> LivingFoes(this GameState s) =>
		s.GetChildren(s.GetWellKnownId(BattleKey))
			.OfType<Foe>()
			.Where(f => !f.IsDead && !f.Caught)
			.OrderBy(f => f.Space);

	/// <summary>Every foe a Snare took this battle — they join the run if it is won.</summary>
	public static IEnumerable<Foe> CaughtFoes(this GameState s) =>
		s.GetChildren(s.GetWellKnownId(BattleKey)).OfType<Foe>().Where(f => f.Caught);

	/// <summary>**The HP at or below which a foe can be caught: a third of its max.**</summary>
	public static int CatchAt(this Foe foe) => foe.MaxHp / 3;

	/// <summary>
	/// **Why a Snare cannot take this foe now, or null if it can.** Shared by the throw and by the
	/// board, which lights the foes a Snare would take.
	/// </summary>
	public static string? CatchRefusal(this GameState s, Foe foe)
	{
		var party = s.GetParty();
		if (party.Snares <= 0)
			return "You have no Snares left";
		if (!foe.Catchable)
			return $"The {foe.Name} cannot be caught";
		if (foe.Hp > foe.CatchAt())
			return $"Weaken the {foe.Name} first: {foe.CatchAt()} HP or less";
		if (party.Energy < UseSnareAction.Cost)
			return "Not enough energy for a Snare";
		return null;
	}

	/// <summary>A knocked-out monster leaves the row, so its space is free.</summary>
	public static Ally? AllyAt(this GameState s, int space) =>
		s.LivingAllies().FirstOrDefault(a => a.Space == space);

	public static Foe? FoeAt(this GameState s, int space) =>
		s.LivingFoes().FirstOrDefault(f => f.Space == space);

	/// <summary>
	/// **Who acts at the end of the turn, in order**: fastest first, both sides interleaved; on a tie
	/// yours go first, then left to right. The order badge on the board is this list.
	/// </summary>
	public static ImmutableList<Creature> ActingOrder(this GameState s) =>
		[
			.. s.LivingAllies()
				.Cast<Creature>()
				.Concat(s.LivingFoes())
				.OrderByDescending(c => c.Speed)
				.ThenBy(c => c is Ally ? 0 : 1)
				.ThenBy(c => c.Space),
		];

	/// <summary>
	/// **Why this monster cannot step to that space, or null if it can.** One step, to a space next
	/// to it; a monster standing there swaps with it. Shared by the click and the board's lit spaces.
	/// </summary>
	public static string? StepRefusal(this GameState s, Ally ally, int space)
	{
		if (ally.StepsLeft <= 0)
			return $"{ally.Name} has already stepped this turn";

		if (space < 0 || space >= PartyBattle.Spaces)
			return $"There is no space {space}";

		if (Math.Abs(space - ally.Space) != 1)
			return $"{ally.Name} can only step to a space next to it";

		return null;
	}

	/// <summary>
	/// **The spaces on the OTHER row this creature's move will hit.** The telegraph the board draws
	/// and the attack that resolves both come from here, so what you see is what happens.
	///
	/// A shape is fixed columns, so it can be empty — that is a dodged attack, or a monster standing
	/// in the wrong column. Homing picks the lowest-HP creature on the other side (leftmost on a tie).
	/// </summary>
	public static ImmutableList<int> IntentTargets(this GameState s, Creature creature)
	{
		var intent = creature.Current;
		if (intent.Kind != IntentType.Attack)
			return [];

		if (intent.Homing)
		{
			IEnumerable<Creature> others = creature is Ally ? s.LivingFoes() : s.LivingAllies();
			// A Decoy draws every homing attack to itself.
			if (others.FirstOrDefault(c => c.HasComponent<Lure>()) is { } lure)
				return [lure.Space];
			var weakest = others.OrderBy(c => c.Hp).ThenBy(c => c.Space).FirstOrDefault();
			return weakest is null ? [] : [weakest.Space];
		}

		return
		[
			.. intent
				.Offsets.Select(o => creature.Space + o)
				.Where(c => c >= 0 && c < PartyBattle.Spaces),
		];
	}

	/// <summary>
	/// **What ending the turn now would cost** — each creature's HP, yours AND the foes', and the two
	/// trainers' health — played out on a throwaway copy by the rules themselves, never a sum the UI
	/// adds up.
	/// </summary>
	public static Forecast ForecastIfTurnEndsNow(this GameState s)
	{
		var party = s.GetParty();
		if (party.IsOver)
			return new Forecast(ImmutableDictionary<int, int>.Empty, 0, 0);

		var (after, _) = s.AddAction(new EndPartyTurnAction()).ProcessAllActions();
		var then = after.GetParty();
		return new Forecast(
			s.LivingAllies()
				.Cast<Creature>()
				.Concat(s.LivingFoes())
				.ToImmutableDictionary(c => c.Id, c => c.Hp - ((Creature)after.GetObject(c.Id)).Hp),
			party.TrainerHp - then.TrainerHp,
			party.LeaderHp - then.LeaderHp
		);
	}

	/// <summary>
	/// **This foe's attack will land on no monster, so it will hit YOU** — as things stand now. The
	/// telegraph says so ("→ YOU"); where it truly lands is decided when it resolves.
	/// </summary>
	public static bool AimsAtTrainer(this GameState s, Foe foe) =>
		foe.Current.Kind == IntentType.Attack
		&& !foe.Staggered
		&& !s.IntentTargets(foe).Any(space => s.AllyAt(space) is not null);

	/// <summary>**This monster's attack will land on no foe, so it will hit the gym LEADER.**</summary>
	public static bool AimsAtLeader(this GameState s, Ally ally) =>
		s.GetParty().LeaderHp > 0
		&& ally.Current.Kind == IntentType.Attack
		&& !s.IntentTargets(ally).Any(space => s.FoeAt(space) is not null);

	/// <summary>
	/// **CAPTURE HARNESS ONLY — never called in play.** Ends the battle as a win or a loss, so a
	/// capture can reach the screens that follow a battle without playing one. Every foe (on a win)
	/// or monster (on a loss) goes to 0 HP, so what reads the result reads real HP.
	/// </summary>
	public static GameState DebugEndBattle(this GameState s, bool won)
	{
		if (won)
			foreach (var foe in s.LivingFoes().ToList())
				s = s.UpdateObject(foe.Id, foe with { Hp = 0 });
		else
			foreach (var ally in s.LivingAllies().ToList())
				s = s.UpdateObject(ally.Id, ally with { Hp = 0 });

		var party = s.GetParty();
		return s.UpdateObject(party.Id, party with { IsOver = true, Won = won });
	}

	/// <summary>
	/// **A creature plays its current move**, then its cycle advances. The end of the turn calls this
	/// for each creature in <see cref="ActingOrder"/>; Hasten calls it early. `targets` were fixed by
	/// the caller from <see cref="IntentTargets"/> — the telegraph — so nothing re-aims mid-turn.
	/// </summary>
	internal static (GameState, ImmutableList<GameEvent>) Act(
		GameState s,
		int creatureId,
		ImmutableList<int> targets
	)
	{
		var events = ImmutableList<GameEvent>.Empty;

		// A foe's Block lasts through your turn and drops when it next acts.
		if (s.GetObject(creatureId) is Foe { Block: > 0 } braced)
			s = s.UpdateObject(creatureId, braced with { Block = 0 });

		var creature = (Creature)s.GetObject(creatureId);
		var intent = creature.Current;
		ImmutableList<GameEvent> more;

		switch (intent.Kind)
		{
			case IntentType.Attack when creature is Ally ally:
				(s, more) = AttackFoes(s, ally, intent.Amount, targets);
				events = events.AddRange(more);
				break;

			case IntentType.Attack:
				var landed = false;
				foreach (var space in targets)
				{
					if (s.GetParty().IsOver || ((Foe)s.GetObject(creatureId)).IsDead)
						break;
					if (s.AllyAt(space) is not { } victim)
						continue;

					landed = true;
					var overflow = intent.Amount - victim.Block - victim.Hp;

					(s, more) = HitAlly(s, victim, intent.Amount, creature.Name);
					events = events.AddRange(more);

					// **TRAMPLE**: what fells the monster and more, the rest goes through to you.
					if (overflow > 0 && creature.HasComponent<Trample>() && !s.GetParty().IsOver)
					{
						(s, more) = HitTrainer(s, overflow, creature.Name);
						events = events.AddRange(more);
					}

					// **Thorns: attacking this monster hurts**, blocked or not.
					if (victim.TotalThorns > 0 && !s.GetParty().IsOver)
					{
						(s, more) = HitFoe(s, (Foe)s.GetObject(creatureId), victim.TotalThorns);
						events = events.AddRange(more);
					}
				}

				// **An attack that lands on no monster hits the trainer** — once, whatever its shape.
				if (!landed && !s.GetParty().IsOver && !((Foe)s.GetObject(creatureId)).IsDead)
				{
					(s, more) = HitTrainer(s, intent.Amount, creature.Name);
					events = events.AddRange(more);
				}

				// **THIEF: then it takes the top card of your draw pile.** An empty pile is not reshuffled.
				if (
					intent.Steals
					&& !s.GetParty().IsOver
					&& !((Foe)s.GetObject(creatureId)).IsDead
					&& s.CardsIn(ZoneType.Draw).FirstOrDefault() is { } top
				)
				{
					s = s.MoveObject(top.Id, creatureId);
					events = events.Add(
						new CardStolenEvent { FoeId = creatureId, CardName = top.Name }
					);
				}
				break;

			case IntentType.Block:
				s = s.UpdateObject(
					creatureId,
					creature with
					{
						Block = creature.Block + intent.Amount,
					}
				);
				if (creature is Ally && intent.Amount > 0)
					events = events.Add(
						new BlockGainedEvent { AllyId = creatureId, Amount = intent.Amount }
					);
				break;

			case IntentType.Move:
				// A caught Wisp still drifts — on your row now, and only into an empty space.
				var to = creature.Space + intent.Amount;
				var taken = creature is Ally ? s.AllyAt(to) is not null : s.FoeAt(to) is not null;
				if (to >= 0 && to < PartyBattle.Spaces && !taken)
					s = s.UpdateObject(creatureId, creature with { Space = to });
				break;

			case IntentType.Push when creature is Ally && s.FoeAt(creature.Space) is { } ahead:
				var dest = ahead.Space + intent.Amount;
				if (dest >= 0 && dest < PartyBattle.Spaces && s.FoeAt(dest) is null)
					(s, more) = Push(s, ahead, dest);
				else
					more = [];
				events = events.AddRange(more);
				break;
		}

		// **Summon: a token into the nearest empty space on its own row** — yours, or a wild brood.
		if (intent is { Kind: IntentType.Summon, Summons: { } brood })
		{
			var ally = creature is Ally;
			if (PartySummon.NearestEmpty(s, creature.Space, ally) is { } at)
				s = ally
					? PartySummon.SummonAlly(s, brood, at)
					: PartySummon.SummonFoe(s, brood, at);
		}

		// **Echo: your last spell again**, where it was dropped. Only an ally has spells to echo.
		if (
			intent.Kind == IntentType.Echo
			&& creature is Ally
			&& s.GetParty().LastSpell is { } spell
		)
		{
			var echoed = spell.Execute(s);
			s = echoed.GameState;
			events = events.AddRange(echoed.Events);
		}

		// The cycle advances even for a creature that died mid-move to Thorns — it is gone anyway.
		var acted = (Creature)s.GetObject(creatureId);
		s = s.UpdateObject(creatureId, acted with { PatternIndex = acted.PatternIndex + 1 });
		return (s, events);
	}

	/// <summary>
	/// **One of your monsters attacks the given foe-row spaces** — its move, or a card's strike.
	/// Momentum rides on the whole attack and is spent by it, hit or miss.
	/// </summary>
	internal static (GameState, ImmutableList<GameEvent>) AttackFoes(
		GameState s,
		Ally ally,
		int amount,
		IEnumerable<int> spaces
	)
	{
		var damage = ally.AttackFor(amount);
		if (ally.Momentum > 0)
			s = s.UpdateObject(ally.Id, ally with { Momentum = 0 });

		var events = ImmutableList<GameEvent>.Empty;
		var landed = false;
		foreach (var space in spaces)
		{
			if (s.GetParty().IsOver || s.FoeAt(space) is not { } foe)
				continue;

			landed = true;
			var overflow = damage + foe.OffBalance - foe.Block - foe.Hp;
			ImmutableList<GameEvent> hit;
			(s, hit) = HitFoe(s, foe, damage);
			events = events.AddRange(hit);

			// **TRAMPLE**, yours: what fells a foe and more carries on into a random other foe.
			if (overflow > 0 && ally.HasComponent<Trample>() && !s.GetParty().IsOver)
			{
				var others = s.LivingFoes().ToList();
				if (others.Count > 0)
				{
					var rng = new Random(s.RngSeed);
					var next = others[rng.Next(others.Count)];
					(s, hit) = HitFoe(s with { RngSeed = rng.Next() }, next, overflow);
					events = events.AddRange(hit);
				}
			}
		}

		// The mirror of the trainer rule: a swing that finds no foe hits the gym LEADER, if there is one.
		if (!landed && s.GetParty() is { IsOver: false, LeaderHp: > 0 })
		{
			ImmutableList<GameEvent> hit;
			(s, hit) = HitLeader(s, damage, ally.Name);
			events = events.AddRange(hit);
		}
		return (s, events);
	}

	/// <summary>
	/// A foe moved to an empty space — by Gust the card or Gale's move. **Off-Balance** while any
	/// monster with the passive stands.
	/// </summary>
	internal static (GameState, ImmutableList<GameEvent>) Push(GameState s, Foe foe, int to)
	{
		var unbalance = s.LivingAllies().Select(a => a.Unbalances).DefaultIfEmpty(0).Max();
		s = s.UpdateObject(
			foe.Id,
			foe with
			{
				Space = to,
				OffBalance = Math.Max(foe.OffBalance, unbalance),
			}
		);
		return (
			s,
			[
				new FoeMovedEvent
				{
					FoeId = foe.Id,
					From = foe.Space,
					To = to,
				},
			]
		);
	}

	/// <summary>
	/// **CAPTURE HARNESS ONLY — never called in play.** Drops the foe in that space to the HP a Snare
	/// can take, so a capture can show catching without playing the fight down to it.
	/// </summary>
	public static GameState DebugWeaken(this GameState s, int space) =>
		s.FoeAt(space) is { } foe ? s.UpdateObject(foe.Id, foe with { Hp = foe.CatchAt() }) : s;

	/// <summary>Damage to a monster: Block first, then HP. Ends the battle when the last one falls.</summary>
	internal static (GameState, ImmutableList<GameEvent>) HitAlly(
		GameState s,
		Ally ally,
		int amount,
		string by
	)
	{
		var blocked = Math.Min(amount, ally.Block);
		var hit = ally with
		{
			WasHit = true,
			Block = ally.Block - blocked,
			Hp = Math.Max(0, ally.Hp - (amount - blocked)),
		};
		s = s.UpdateObject(ally.Id, hit);

		ImmutableList<GameEvent> events =
		[
			new AllyHitEvent
			{
				AllyId = ally.Id,
				Damage = amount - blocked,
				Blocked = blocked,
				By = by,
			},
		];
		if (hit.IsKnockedOut && hit.FadesIn > 0)
		{
			// A token has no deck and no bench behind it: it is simply gone, with its faint effect.
			events = events.Add(new AllyKnockedOutEvent { AllyId = ally.Id });
			s = PartySummon.TokenFainted(s, hit);
		}
		else if (hit.IsKnockedOut)
		{
			events = events.Add(new AllyKnockedOutEvent { AllyId = ally.Id });
			s = s.WithdrawDeck(ally.Id);

			// **The bench steps in**, into the fainted monster's space. It arrives after this turn's
			// order was fixed, so it acts from next turn — but it can be hit where it stands.
			if (s.BenchedAllies().FirstOrDefault() is { } sub)
			{
				s = s.UpdateObject(sub.Id, sub with { Benched = false, Space = ally.Space });
				s = KinRng.ShuffleZone(s.DeployDeck(sub.Id), s.ZoneId(ZoneType.Draw));
				events = events.Add(
					new AllySwappedInEvent { AllyId = sub.Id, ForAllyId = ally.Id }
				);
			}
		}

		// Tokens never keep a battle alive — only real monsters do.
		if (!s.LivingAllies().Any(a => a.FadesIn == 0))
		{
			var party = s.GetParty();
			s = s.UpdateObject(party.Id, party with { IsOver = true, Won = false });
			events = events.Add(new PartyBattleEndedEvent { Won = false });
		}

		return (s, events);
	}

	/// <summary>Damage to YOU. At 0 the battle is lost — and with it the run.</summary>
	internal static (GameState, ImmutableList<GameEvent>) HitTrainer(
		GameState s,
		int amount,
		string by
	)
	{
		var party = s.GetParty();
		var hp = Math.Max(0, party.TrainerHp - amount);
		s = s.UpdateObject(party.Id, party with { TrainerHp = hp });

		ImmutableList<GameEvent> events = [new TrainerHitEvent { Damage = amount, By = by }];
		if (hp == 0)
		{
			s = s.UpdateObject(party.Id, s.GetParty() with { IsOver = true, Won = false });
			events = events.Add(new PartyBattleEndedEvent { Won = false });
		}
		return (s, events);
	}

	/// <summary>Damage to the gym LEADER. At 0 the gym is won, whatever of its creatures still stand.</summary>
	internal static (GameState, ImmutableList<GameEvent>) HitLeader(
		GameState s,
		int amount,
		string by
	)
	{
		var party = s.GetParty();
		var hp = Math.Max(0, party.LeaderHp - amount);
		s = s.UpdateObject(party.Id, party with { LeaderHp = hp });

		ImmutableList<GameEvent> events = [new LeaderHitEvent { Damage = amount, By = by }];
		if (hp == 0)
		{
			s = s.UpdateObject(party.Id, s.GetParty() with { IsOver = true, Won = true });
			events = events.Add(new PartyBattleEndedEvent { Won = true });
		}
		return (s, events);
	}

	/// <summary>Damage to a foe: Block first, then HP. Ends the battle when the last one falls.</summary>
	internal static (GameState, ImmutableList<GameEvent>) HitFoe(GameState s, Foe foe, int amount)
	{
		// Off-Balance rides on every hit, whoever lands it — Pike's jab and Bramble's Thorns alike.
		amount += foe.OffBalance;

		var blocked = Math.Min(amount, foe.Block);
		var hit = foe with
		{
			Block = foe.Block - blocked,
			Hp = Math.Max(0, foe.Hp - (amount - blocked)),
		};
		s = s.UpdateObject(foe.Id, hit);
		if (hit.IsDead)
		{
			s = s.ReturnStolen(foe.Id);
			var party = s.GetParty();
			if (!party.EndingTurn)
				s = s.UpdateObject(
					party.Id,
					party with
					{
						FoesDefeatedThisTurn = party.FoesDefeatedThisTurn + 1,
					}
				);
			s = s.StageEvent(
				new FoeDefeatedEvent { FoeId = foe.Id, DuringYourTurn = !party.EndingTurn }
			);
		}

		ImmutableList<GameEvent> events =
		[
			new FoeHitEvent
			{
				FoeId = foe.Id,
				Damage = amount - blocked,
				Blocked = blocked,
			},
		];

		if (!s.LivingFoes().Any())
		{
			var party = s.GetParty();
			s = s.UpdateObject(party.Id, party with { IsOver = true, Won = true });
			events = events.Add(new PartyBattleEndedEvent { Won = true });
		}

		return (s, events);
	}
}
