using System.Collections.Immutable;
using ImmutableGameObjects;

namespace KinCore.Party;

/// <summary>What ending the turn now would cost: each creature's HP lost, by id.</summary>
public record Forecast(ImmutableDictionary<int, int> Hp);

/// <summary>
/// The API boundary for the companion game. The Godot board reads everything through here and
/// never computes a game fact itself — including who a move lands on and who acts when.
/// </summary>
public static class PartyState
{
	public const string BattleKey = "PartyBattle";

	public static PartyBattle GetParty(this GameState s) =>
		(PartyBattle)s.GetObject(s.GetWellKnownId(BattleKey));

	/// <summary>Every monster of yours, fallen, benched or standing, by slot.</summary>
	public static IEnumerable<Ally> Allies(this GameState s) =>
		s.GetChildren(s.GetWellKnownId(BattleKey)).OfType<Ally>().OrderBy(a => a.Slot);

	/// <summary>**Your line**: standing and in it, front (0) first.</summary>
	public static IEnumerable<Ally> LivingAllies(this GameState s) =>
		s.GetChildren(s.GetWellKnownId(BattleKey))
			.OfType<Ally>()
			.Where(a => !a.IsKnockedOut && !a.Benched && a.Position >= 0)
			.OrderBy(a => a.Position);

	/// <summary>The bench, in the order it joins the line.</summary>
	public static IEnumerable<Ally> BenchedAllies(this GameState s) =>
		s.Allies().Where(a => a.Benched && !a.IsKnockedOut);

	/// <summary>**Their line**: standing, not caught, front (0) first.</summary>
	public static IEnumerable<Foe> LivingFoes(this GameState s) =>
		s.GetChildren(s.GetWellKnownId(BattleKey))
			.OfType<Foe>()
			.Where(f => !f.IsDead && !f.Caught && f.Position >= 0)
			.OrderBy(f => f.Position);

	/// <summary>Every foe a Snare took this battle — they join the run if it is won.</summary>
	public static IEnumerable<Foe> CaughtFoes(this GameState s) =>
		s.GetChildren(s.GetWellKnownId(BattleKey)).OfType<Foe>().Where(f => f.Caught);

	public static Ally? AllyAt(this GameState s, int position) =>
		s.LivingAllies().FirstOrDefault(a => a.Position == position);

	public static Foe? FoeAt(this GameState s, int position) =>
		s.LivingFoes().FirstOrDefault(f => f.Position == position);

	/// <summary>The other side's line, as seen by this creature.</summary>
	public static ImmutableList<Creature> Opponents(this GameState s, Creature c) =>
		[.. c is Ally ? s.LivingFoes().Cast<Creature>() : s.LivingAllies()];

	/// <summary>This creature's own line.</summary>
	public static ImmutableList<Creature> OwnLine(this GameState s, Creature c) =>
		[.. c is Ally ? s.LivingAllies().Cast<Creature>() : s.LivingFoes()];

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
		// KIN TOTEM: your first card each turn is free.
		if (party.CardsPlayedThisTurn == 0 && party.Relics.Contains(Relic.KinTotem))
			return 0;
		if (card.HasComponent<SpendsAllEnergy>())
			return party.Energy;

		var cost = card.Cost;
		if (card.GetComponent<CostReduction>() is { } reduction)
			cost -= reduction.PerDiscardThisTurn * party.DiscardedThisTurn;

		// The first card each turn: monsters' discounts and foes' taxes, from both lines.
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
	/// **A thief's haul goes back to your discard pile** when it is beaten or caught.
	/// </summary>
	public static GameState ReturnStolen(this GameState s, int foeId)
	{
		foreach (var card in s.GetChildren(foeId).OfType<KinCard>().ToList())
			s = s.MoveObject(card.Id, s.ZoneId(ZoneType.Discard));
		return s;
	}

	/// <summary>
	/// **Whether this foe could EVER be yours**: catchable, and of your run's family or colourless
	/// (`KinFamiliesPlan.md`, round 2). A practice fight (no family) catches anything.
	/// </summary>
	public static bool IsYourKind(this GameState s, Foe foe)
	{
		var family = s.GetParty().Family;
		return foe.Catchable
			&& (family == Family.None || foe.Family == Family.None || foe.Family == family);
	}

	/// <summary>**The HP at or below which a foe can be caught: a third of its max.**</summary>
	public static int CatchAt(this Foe foe) => foe.MaxHp / 3;

	/// <summary>
	/// **Why a Snare cannot take this foe now, or null if it can.** Shared by the throw and by the
	/// board, which lights the foe a Snare would take. **Only their FRONT** (Shayne, 2026-09-25):
	/// catching a back-liner means pulling it forward first.
	/// </summary>
	public static string? CatchRefusal(this GameState s, Foe foe)
	{
		var party = s.GetParty();
		if (party.Snares <= 0)
			return "You have no Snares left";
		if (!foe.Catchable)
			return $"The {foe.Name} cannot be caught";
		if (!s.IsYourKind(foe))
			return $"Not your family: you catch {party.Family} and colourless monsters";
		if (foe.Position != 0)
			return "A Snare only reaches their front";
		if (foe.Hp > foe.CatchAt())
			return $"Weaken the {foe.Name} first: {foe.CatchAt()} HP or less";
		if (party.Energy < UseSnareAction.Cost)
			return "Not enough energy for a Snare";
		return null;
	}

	/// <summary>
	/// **The steps of the end of the turn, back to front, BOTH LINES AT ONCE** (R3): each step is the
	/// creatures standing at one position, yours first. A monster that acted early (Hasten) sits its
	/// step out. The board's order badges are this list — as things stand now.
	/// </summary>
	public static ImmutableList<ImmutableList<Creature>> ActingSteps(this GameState s)
	{
		var creatures = s.LivingAllies()
			.Where(a => !a.HasActed)
			.Cast<Creature>()
			.Concat(s.LivingFoes())
			.ToList();
		return
		[
			.. creatures
				.GroupBy(c => c.Position)
				.OrderByDescending(g => g.Key)
				.Select(g => g.OrderBy(c => c is Ally ? 0 : 1).ToImmutableList()),
		];
	}

	/// <summary>Every creature in the order of its step — for the badges.</summary>
	public static ImmutableList<Creature> ActingOrder(this GameState s) =>
		[.. s.ActingSteps().SelectMany(step => step)];

	/// <summary>
	/// **Who this creature's move lands on, by id** — its <see cref="Aim"/> read against the lines as
	/// they stand. The telegraph and the resolution both come from here, so what you see is what
	/// happens. A Decoy (<see cref="Lure"/>) draws BACK and HUNT attacks to itself.
	/// </summary>
	public static ImmutableList<int> IntentTargets(this GameState s, Creature creature)
	{
		var intent = creature.Current;
		switch (intent.Kind)
		{
			case IntentType.Block when intent.Target == Aim.Ahead:
				return
				[
					.. s.OwnLine(creature)
						.Where(c => c.Position == creature.Position - 1)
						.Select(c => c.Id),
				];
			case IntentType.Attack:
				return AimAt(s, creature, intent.Target);
			default:
				return [];
		}
	}

	/// <summary>The creatures an aim picks out of the other line (or the one ahead, for AHEAD).</summary>
	public static ImmutableList<int> AimAt(GameState s, Creature from, Aim aim)
	{
		if (aim == Aim.Ahead)
			return
			[
				.. s.OwnLine(from).Where(c => c.Position == from.Position - 1).Select(c => c.Id),
			];

		var line = s.Opponents(from);
		if (line.IsEmpty)
			return [];
		if (
			aim is Aim.Back or Aim.Hunt
			&& line.FirstOrDefault(c => c.HasComponent<Lure>()) is { } lure
		)
			return [lure.Id];

		return aim switch
		{
			Aim.Back => [line[^1].Id],
			Aim.Pierce => [.. line.Take(2).Select(c => c.Id)],
			Aim.Sweep => [.. line.Select(c => c.Id)],
			Aim.Hunt => [line.OrderBy(c => c.Hp).ThenBy(c => c.Position).First().Id],
			_ => [line[0].Id],
		};
	}

	/// <summary>
	/// **What ending the turn now would cost** — each creature's HP, yours AND the foes' — played out
	/// on a throwaway copy by the rules themselves, never a sum the UI adds up.
	/// </summary>
	public static Forecast ForecastIfTurnEndsNow(this GameState s)
	{
		if (s.GetParty().IsOver)
			return new Forecast(ImmutableDictionary<int, int>.Empty);

		// While DEPLOYING, the forecast is the first round in the order you have set — exactly what
		// you are choosing the order by.
		if (s.GetParty().Deploying)
			s = s.AddAction(new BeginFightAction()).ProcessAllActions().State;
		var (after, _) = s.AddAction(new EndPartyTurnAction()).ProcessAllActions();
		return new Forecast(
			s.LivingAllies()
				.Cast<Creature>()
				.Concat(s.LivingFoes())
				.ToImmutableDictionary(c => c.Id, c => c.Hp - ((Creature)after.GetObject(c.Id)).Hp)
		);
	}

	/// <summary>
	/// **CAPTURE HARNESS ONLY — never called in play.** Ends the battle as a win or a loss, so a
	/// capture can reach the screens that follow a battle without playing one.
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

	/// <summary>**CAPTURE HARNESS ONLY.** Drops the foe at that position to the HP a Snare can take.</summary>
	public static GameState DebugWeaken(this GameState s, int position) =>
		s.FoeAt(position) is { } foe ? s.UpdateObject(foe.Id, foe with { Hp = foe.CatchAt() }) : s;

	// ===== Lines

	/// <summary>
	/// **Moves a creature to a new place in its line**, everyone else keeping their order around it —
	/// Rally (0), Retreat (the back), a Swap, a Move intent. Clamped to the line.
	/// </summary>
	public static GameState MoveInLine(this GameState s, int creatureId, int to)
	{
		var creature = (Creature)s.GetObject(creatureId);
		var line = s.OwnLine(creature).Where(c => c.Id != creatureId).ToList();
		line.Insert(Math.Clamp(to, 0, line.Count), creature);
		return Renumber(s, line);
	}

	/// <summary>Swaps the front two of a line — Gust, and a foe's Shove.</summary>
	public static GameState SwapFrontTwo(this GameState s, bool foes)
	{
		var line = foes ? s.LivingFoes().Cast<Creature>().ToList() : [.. s.LivingAllies()];
		if (line.Count < 2)
			return s;
		(line[0], line[1]) = (line[1], line[0]);
		return Renumber(s, line);
	}

	/// <summary>A token arrives at the FRONT of its line (R11); everyone else steps back.</summary>
	public static GameState InsertAtFront(GameState s, Creature arriving, bool foes)
	{
		var line = foes ? s.LivingFoes().Cast<Creature>().ToList() : [.. s.LivingAllies()];
		(s, var added) = s.AddObject(arriving, s.GetWellKnownId(BattleKey));
		line.Insert(0, added);
		return Renumber(s, line);
	}

	private static GameState Renumber(GameState s, IReadOnlyList<Creature> line)
	{
		for (var i = 0; i < line.Count; i++)
			if (((Creature)s.GetObject(line[i].Id)) is var c && c.Position != i)
				s = s.UpdateObject(c.Id, c with { Position = i });
		return s;
	}

	/// <summary>
	/// **SETTLE — MtgCore's state-based effects for a line.** Hits only deal damage; this is where the
	/// fallen and the caught LEAVE their line, the first benched monster joins at the BACK for each of
	/// your real monsters that fell, the lines close up, and the battle is won or lost. It runs after
	/// every card (the post-processor) and after every STEP of the end of the turn, which is what
	/// makes a step simultaneous: nobody's faint changes a line until the step is over. Idempotent.
	/// </summary>
	public static (GameState, ImmutableList<GameEvent>) Settle(this GameState s)
	{
		var events = ImmutableList<GameEvent>.Empty;
		var root = s.GetWellKnownId(BattleKey);

		foreach (
			var foe in s.GetChildren(root)
				.OfType<Foe>()
				.Where(f => (f.IsDead || f.Caught) && f.Position >= 0)
				.ToList()
		)
			s = s.UpdateObject(foe.Id, foe with { Position = -1 });

		var fallen = s.GetChildren(root)
			.OfType<Ally>()
			.Where(a => a.IsKnockedOut && a.Position >= 0)
			.ToList();
		foreach (var ally in fallen)
			s = s.UpdateObject(ally.Id, ally with { Position = -1 });

		s = Renumber(s, [.. s.LivingAllies()]);
		s = Renumber(s, [.. s.LivingFoes()]);

		// A token has no bench behind it; a real monster is replaced at the back.
		foreach (var ally in fallen.Where(a => a.FadesIn == 0))
		{
			if (s.BenchedAllies().FirstOrDefault() is not { } sub)
				break;
			s = s.UpdateObject(
				sub.Id,
				sub with
				{
					Benched = false,
					Position = s.LivingAllies().Count(),
				}
			);
			events = events.Add(new AllySwappedInEvent { AllyId = sub.Id, ForAllyId = ally.Id });
		}

		var party = s.GetParty();
		if (party.IsOver)
			return (s, events);

		// Tokens never keep a battle alive — only real monsters do.
		var lost = !s.LivingAllies().Any(a => a.FadesIn == 0);
		var won = !s.LivingFoes().Any();
		if (won || lost)
		{
			s = s.UpdateObject(party.Id, party with { IsOver = true, Won = won && !lost });
			events = events.Add(new PartyBattleEndedEvent { Won = won && !lost });
		}
		return (s, events);
	}

	// ===== Acting

	/// <summary>
	/// **A creature plays its current move**, then its cycle advances. The end of the turn calls this
	/// for every creature in a step; Hasten calls it early. `targets` were chosen at the START of the
	/// step (R3), so a creature that falls mid-step still lands its blow and nothing re-aims.
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
				(s, more) = AttackAllies(s, (Foe)creature, intent, targets);
				events = events.AddRange(more);
				break;

			case IntentType.Block:
				foreach (var id in intent.Target == Aim.Ahead ? targets : [creatureId])
				{
					var shielded = (Creature)s.GetObject(id);
					s = s.UpdateObject(
						id,
						shielded with
						{
							Block = shielded.Block + intent.Amount,
						}
					);
					if (shielded is Ally && intent.Amount > 0)
						events = events.Add(
							new BlockGainedEvent { AllyId = id, Amount = intent.Amount }
						);
				}
				break;

			case IntentType.Move:
				s = s.MoveInLine(creatureId, creature.Position + intent.Amount);
				break;

			case IntentType.Shove:
				(s, more) = Shove(s, foes: creature is Ally);
				events = events.AddRange(more);
				break;

			// **Summon: a token at the FRONT of its own line** — yours, or a wild brood.
			case IntentType.Summon when intent.Summons is { } brood:
				s =
					creature is Ally
						? PartySummon.SummonAlly(s, brood)
						: PartySummon.SummonFoe(s, brood);
				break;

			case IntentType.Pull when creature is Foe:
				s = PartyBosses.PullBackToFront(s);
				break;

			// **Echo: your last spell again**, on the same foe. Only an ally has spells to echo.
			case IntentType.Echo when creature is Ally && s.GetParty().LastSpell is { } spell:
				var echoed = spell.Execute(s);
				s = echoed.GameState;
				events = events.AddRange(echoed.Events);
				break;
		}

		var acted = (Creature)s.GetObject(creatureId);
		s = s.UpdateObject(creatureId, acted with { PatternIndex = acted.PatternIndex + 1 });
		if (acted is Ally)
		{
			var party = s.GetParty();
			s = s.UpdateObject(
				party.Id,
				party with
				{
					AlliesActedThisRound = party.AlliesActedThisRound + 1,
				}
			);
		}
		return (s, events);
	}

	/// <summary>
	/// **One of your monsters attacks these foes** — its move, or a card's strike. FINISHER rides on
	/// the whole attack: the relay's count of monsters that acted before it this round. A target that
	/// already fell is a wasted blow.
	/// </summary>
	internal static (GameState, ImmutableList<GameEvent>) AttackFoes(
		GameState s,
		Ally ally,
		int amount,
		IEnumerable<int> targets
	)
	{
		var damage =
			ally.AttackFor(amount)
			+ ally.FinisherPerAlly * s.GetParty().AlliesActedThisRound
			+ ally.GetComponents<KindleFinisher>().Sum(k => k.PerKindle) * s.GetParty().Kindle;
		var events = ImmutableList<GameEvent>.Empty;
		foreach (var id in targets)
		{
			if (s.GetObject(id) is not Foe { IsDead: false, Caught: false } foe)
				continue;

			var overflow = damage + foe.OffBalance - foe.Block - foe.Hp;
			ImmutableList<GameEvent> hit;
			(s, hit) = HitFoe(s, foe, damage, ally.Id);
			events = events.AddRange(hit);

			// **TRAMPLE**, yours: what fells a foe and more carries on into the one behind it.
			if (overflow > 0 && ally.HasComponent<Trample>() && Behind(s, foe) is Foe next)
			{
				(s, hit) = HitFoe(s, next, overflow);
				events = events.AddRange(hit);
			}
		}
		return (s, events);
	}

	/// <summary>A foe's attack on your monsters: Block, Thorns back, and TRAMPLE into the one behind.</summary>
	private static (GameState, ImmutableList<GameEvent>) AttackAllies(
		GameState s,
		Foe attacker,
		Intent intent,
		IEnumerable<int> targets
	)
	{
		var events = ImmutableList<GameEvent>.Empty;
		ImmutableList<GameEvent> more;
		foreach (var id in targets)
		{
			if (s.GetObject(id) is not Ally { IsKnockedOut: false } victim)
				continue;

			var overflow = intent.Amount - victim.Block - victim.Hp;
			(s, more) = HitAlly(s, victim, intent.Amount, attacker.Name, attacker.Id);
			events = events.AddRange(more);

			if (overflow > 0 && attacker.HasComponent<Trample>() && Behind(s, victim) is Ally next)
			{
				(s, more) = HitAlly(s, next, overflow, attacker.Name);
				events = events.AddRange(more);
			}

			// **Thorns: attacking this monster hurts**, blocked or not. THORNWALL adds its Block, as
			// it stood when the blow came in.
			var thorns = victim.TotalThorns + (victim.HasComponent<Thornwall>() ? victim.Block : 0);
			if (thorns > 0)
			{
				events = events.Add(new ThornsEvent { FoeId = attacker.Id, Damage = thorns });
				(s, more) = HitFoe(s, (Foe)s.GetObject(attacker.Id), thorns);
				events = events.AddRange(more);
			}
		}

		// **THIEF: then it takes the top card of your draw pile.** An empty pile is not reshuffled.
		if (intent.Steals && s.CardsIn(ZoneType.Draw).FirstOrDefault() is { } top)
		{
			s = s.MoveObject(top.Id, attacker.Id);
			events = events.Add(new CardStolenEvent { FoeId = attacker.Id, CardName = top.Name });
		}
		return (s, events);
	}

	/// <summary>The creature standing right behind this one in its line, if any.</summary>
	private static Creature? Behind(GameState s, Creature c) =>
		s.OwnLine(c).FirstOrDefault(o => o.Position == c.Position + 1 && !o.IsDown);

	/// <summary>
	/// **The other line's front two swap.** When YOU do it, the moved foes are Off-Balance while a
	/// monster with the passive stands.
	/// </summary>
	internal static (GameState, ImmutableList<GameEvent>) Shove(GameState s, bool foes)
	{
		var before = foes ? s.LivingFoes().Take(2).ToList() : [];
		s = s.SwapFrontTwo(foes);
		if (!foes || before.Count < 2)
			return (s, []);

		var unbalance = s.LivingAllies().Select(a => a.Unbalances).DefaultIfEmpty(0).Max();
		var events = ImmutableList<GameEvent>.Empty;
		foreach (var moved in before)
		{
			var now = (Foe)s.GetObject(moved.Id);
			s = s.UpdateObject(
				now.Id,
				now with
				{
					OffBalance = Math.Max(now.OffBalance, unbalance),
				}
			);
			events = events.Add(
				new FoeMovedEvent
				{
					FoeId = now.Id,
					From = moved.Position,
					To = now.Position,
				}
			);
		}
		return (s, events);
	}

	/// <summary>Damage to a monster: Block first, then HP. Falling is settled later (<see cref="Settle"/>).</summary>
	internal static (GameState, ImmutableList<GameEvent>) HitAlly(
		GameState s,
		Ally ally,
		int amount,
		string by,
		int byId = 0
	)
	{
		var blocked = Math.Min(amount, ally.Block);
		var hit = ally with
		{
			WasHit = true,
			Block = ally.Block - blocked,
			Rooted = Math.Min(ally.Rooted, ally.Block - blocked),
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
				AttackerId = byId,
			},
		];
		if (hit.IsKnockedOut && !ally.IsKnockedOut)
		{
			events = events.Add(new AllyKnockedOutEvent { AllyId = ally.Id });
			if (hit.FadesIn > 0)
				s = PartySummon.TokenFainted(s, hit);
		}
		return (s, events);
	}

	/// <summary>Damage to a foe: Block first, then HP. Falling is settled later (<see cref="Settle"/>).</summary>
	internal static (GameState, ImmutableList<GameEvent>) HitFoe(
		GameState s,
		Foe foe,
		int amount,
		int byId = 0
	)
	{
		// Off-Balance rides on every hit, whoever lands it — Pike's jab and Bramble's Thorns alike.
		// SHELL then asks whether the whole blow was big enough to matter.
		amount = PartyBosses.ThroughShell(foe, amount + foe.OffBalance);

		var blocked = Math.Min(amount, foe.Block);
		var hit = foe with
		{
			Block = foe.Block - blocked,
			Hp = Math.Max(0, foe.Hp - (amount - blocked)),
		};
		s = s.UpdateObject(foe.Id, hit);
		if (hit.IsDead && !foe.IsDead)
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

		// A PHASE fires on the hit that crosses it — after the hit's own event, so it reads in order.
		(s, var phased) = PartyBosses.CheckPhase(s, foe.Id);
		return (
			s,
			[
				new FoeHitEvent
				{
					FoeId = foe.Id,
					Damage = amount - blocked,
					Blocked = blocked,
					AttackerId = byId,
				},
				.. phased,
			]
		);
	}
}
