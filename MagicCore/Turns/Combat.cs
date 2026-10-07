using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;

namespace MagicCore.Turns;

/// <summary>
/// Combat (CR 506–511). Declarations are one choice PER CREATURE — "attack or not", "block whom or
/// not" — so a decision is linear in creatures instead of one pick among every subset.
/// </summary>
public static class Combat
{
	public static int Defender(GameState s) => s.OpponentOf(s.GetGame().ActivePlayerId);

	public static IEnumerable<Card> Attackers(GameState s) =>
		s.CardsIn(s.GetGame().ActivePlayerId, ZoneType.Battlefield).Where(c => c.AttackingId != 0);

	public static IEnumerable<Card> BlockersOf(GameState s, int attackerId) =>
		s.CardsIn(Defender(s), ZoneType.Battlefield).Where(c => c.BlockingId == attackerId);

	/// <summary>CR 302.6: under its controller's control continuously since their most recent turn began.</summary>
	public static bool IsSummoningSick(GameState s, Card card)
	{
		var game = s.GetGame();
		var mostRecentTurn =
			game.ActivePlayerId == card.ControllerId ? game.TurnNumber : game.TurnNumber - 1;
		return card.ControlledSinceTurn >= mostRecentTurn;
	}

	public static bool CanAttack(GameState s, Card c) =>
		c.Is(CardType.Creature)
		&& !c.IsTapped
		&& !c.Has("Defender")
		&& (c.Has("Haste") || !IsSummoningSick(s, c));

	public static bool CanBlock(Card blocker, Card attacker) =>
		blocker.Is(CardType.Creature)
		&& !blocker.IsTapped
		&& (!attacker.Has("Flying") || blocker.Has("Flying") || blocker.Has("Reach"));

	public static GameState BeginDeclareAttackers(GameState s)
	{
		var active = s.GetGame().ActivePlayerId;
		var candidates = s.CardsIn(active, ZoneType.Battlefield)
			.Where(c => CanAttack(s, c))
			.ToList();
		if (candidates.Count == 0)
			return TurnStructure.GivePriority(s, active);
		return s.SpawnAction(
			new PipelineAction
			{
				Steps =
				[
					.. candidates.Select(c => new DeclareAttackerChoice(active, c.Id)),
					new FinishAttackersAction(),
				],
			}
		);
	}

	public static GameState BeginDeclareBlockers(GameState s)
	{
		var attackers = Attackers(s).ToList();
		if (attackers.Count == 0)
			return TurnStructure.EndStep(s); // CR 508.8: no attackers, no blockers or damage steps
		var defender = Defender(s);
		var candidates = s.CardsIn(defender, ZoneType.Battlefield)
			.Where(b => attackers.Any(a => CanBlock(b, a)))
			.ToList();
		if (candidates.Count == 0)
			return TurnStructure.GivePriority(s, s.GetGame().ActivePlayerId);
		return s.SpawnAction(
			new PipelineAction
			{
				Steps =
				[
					.. candidates.Select(c => new DeclareBlockerChoice(defender, c.Id)),
					new FinishBlockersAction(),
				],
			}
		);
	}

	public static bool AnyFirstStrike(GameState s) =>
		Combatants(s).Any(c => c.Has("First strike") || c.Has("Double strike"));

	static IEnumerable<Card> Combatants(GameState s) =>
		Attackers(s)
			.Concat(s.CardsIn(Defender(s), ZoneType.Battlefield).Where(c => c.BlockingId != 0));

	/// <summary>
	/// CR 510: every combatant that deals damage in this step deals it at once — assignments are
	/// computed from the state before any of it lands.
	///
	/// ponytail: the attacker's controller does not choose the split among blockers. Each blocker
	/// in declaration order takes lethal; the excess goes to the player with trample, else onto the
	/// first blocker. Make it a choice when the AI can use one.
	/// </summary>
	public static GameState DealDamage(GameState s, bool firstStrikeStep)
	{
		bool Deals(Card c) =>
			c.Power > 0
			&& (
				firstStrikeStep
					? c.Has("First strike") || c.Has("Double strike")
					: !c.Has("First strike") || c.Has("Double strike")
			);

		var hits = new List<(Card Source, int TargetId, int Amount)>();
		foreach (var attacker in Attackers(s).Where(Deals))
		{
			if (!attacker.IsBlocked)
			{
				hits.Add((attacker, attacker.AttackingId, attacker.Power));
				continue;
			}
			var remaining = attacker.Power;
			var blockers = BlockersOf(s, attacker.Id).ToList();
			foreach (var blocker in blockers)
			{
				var lethal = attacker.Has("Deathtouch")
					? 1
					: Math.Max(1, blocker.Toughness - blocker.Damage);
				var amount = Math.Min(remaining, lethal);
				hits.Add((attacker, blocker.Id, amount));
				remaining -= amount;
			}
			if (remaining > 0 && attacker.Has("Trample"))
				hits.Add((attacker, attacker.AttackingId, remaining));
			else if (remaining > 0 && blockers.Count > 0)
				hits.Add((attacker, blockers[0].Id, remaining));
		}
		foreach (
			var blocker in s.CardsIn(Defender(s), ZoneType.Battlefield)
				.Where(c => c.BlockingId != 0 && Deals(c))
		)
			if (s.GetCard(blocker.BlockingId).AttackingId != 0)
				hits.Add((blocker, blocker.BlockingId, blocker.Power));

		foreach (var (source, targetId, amount) in hits.Where(h => h.Amount > 0))
		{
			if (s.GetObject(targetId) is MagicPlayer player)
				s = s.WithPlayer(player with { Life = player.Life - amount });
			else
			{
				var target = s.GetCard(targetId);
				s = s.UpdateObject(
					targetId,
					target with
					{
						Damage = target.Damage + amount,
						DamagedByDeathtouch =
							target.DamagedByDeathtouch || source.Has("Deathtouch"),
					}
				);
			}
			if (source.Has("Lifelink"))
			{
				var controller = s.GetPlayer(source.ControllerId);
				s = s.WithPlayer(controller with { Life = controller.Life + amount });
			}
		}
		return s;
	}

	/// <summary>CR 511.3: everything leaves combat as the end of combat step ends.</summary>
	public static GameState EndCombat(GameState s) =>
		ForEachPermanent(
			s,
			c => c.AttackingId != 0 || c.BlockingId != 0 || c.IsBlocked,
			c => c with { AttackingId = 0, BlockingId = 0, IsBlocked = false }
		);

	/// <summary>CR 514.2: damage wears off in cleanup.</summary>
	public static GameState RemoveDamage(GameState s) =>
		ForEachPermanent(
			s,
			c => c.Damage > 0 || c.DamagedByDeathtouch,
			c => c with { Damage = 0, DamagedByDeathtouch = false }
		);

	static GameState ForEachPermanent(GameState s, Func<Card, bool> where, Func<Card, Card> update)
	{
		foreach (var id in new[] { s.Player1Id(), s.Player2Id() })
		foreach (var card in s.CardsIn(id, ZoneType.Battlefield).Where(where).ToList())
			s = s.UpdateObject(card.Id, update(card));
		return s;
	}
}
