using System.Collections.Immutable;
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
		&& (!s.Has(c, "Defender") || s.Has(c, AttacksDespiteDefender))
		&& (s.Has(c, "Haste") || !IsSummoningSick(s, c));

	/// <summary>
	/// No spells or non-mana abilities now: Yuriko during combat, or a spell with split second on the
	/// stack (CR 702.61 — Samut gives it to its controller's instants and sorceries). Casting and
	/// activating both ask here; mana abilities and triggers never do.
	/// </summary>
	public static bool LockedDown(GameState s)
	{
		// Cheap questions first: this runs for every cast and activation the AI considers.
		var inCombat = s.GetGame().Step is >= Step.BeginCombat and <= Step.EndCombat;
		var spells = s.GetChildrenIds(s.GetWellKnownId(MagicKeys.Stack))
			.Select(s.GetObject)
			.OfType<Card>()
			.Where(c => c.Is(CardType.Instant | CardType.Sorcery))
			.ToList();
		if (!inCombat && spells.Count == 0)
			return false;
		var permanents = new[] { s.Player1Id(), s.Player2Id() }.SelectMany(p =>
			s.CardsIn(p, ZoneType.Battlefield)
		);
		if (inCombat && permanents.Any(s.HasAbility<Effects.NoSpellsOrAbilitiesDuringCombat>))
			return true;
		return spells.Any(spell =>
			s.CardsIn(spell.ControllerId, ZoneType.Battlefield)
				.Any(s.HasAbility<Effects.YourInstantsAndSorceriesHaveSplitSecond>)
		);
	}

	/// <summary>
	/// Surveillance Phantasm: "can attack as though it didn't have defender"; Ghalta: "creatures you
	/// control can …" — a keyword-like grant, like CantBeBlocked: a static's scope says who.
	/// </summary>
	public const string AttacksDespiteDefender = "Can attack as though it didn't have defender";

	/// <summary>"Can't be blocked" — granted by static abilities and until-end-of-turn effects alike.</summary>
	public const string CantBeBlocked = "Can't be blocked";

	public static bool CanBlock(GameState s, Card blocker, Card attacker) =>
		blocker.Is(CardType.Creature)
		&& !blocker.IsTapped
		&& !s.Has(attacker, CantBeBlocked)
		&& (!s.Has(attacker, "Flying") || s.Has(blocker, "Flying") || s.Has(blocker, "Reach"));

	/// <summary>The active player's creatures that may be declared as attackers — one choice each.</summary>
	public static List<Card> AttackCandidates(GameState s) =>
		s.CardsIn(s.GetGame().ActivePlayerId, ZoneType.Battlefield)
			.Where(c => CanAttack(s, c))
			.ToList();

	/// <summary>The defender's creatures that can block at least one attacker — one choice each.</summary>
	public static List<Card> BlockCandidates(GameState s)
	{
		var attackers = Attackers(s).ToList();
		return s.CardsIn(Defender(s), ZoneType.Battlefield)
			.Where(b => attackers.Any(a => CanBlock(s, b, a)))
			.ToList();
	}

	public static GameState BeginDeclareAttackers(GameState s)
	{
		var active = s.GetGame().ActivePlayerId;
		var candidates = AttackCandidates(s);
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
		var candidates = BlockCandidates(s);
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
		Combatants(s).Any(c => s.Has(c, "First strike") || s.Has(c, "Double strike"));

	static IEnumerable<Card> Combatants(GameState s) =>
		Attackers(s)
			.Concat(s.CardsIn(Defender(s), ZoneType.Battlefield).Where(c => c.BlockingId != 0));

	/// <summary>
	/// CR 510.1a: combat damage equal to its power — or its toughness, when that is greater and Ghalta
	/// says so; or, for Loot, as though a negative power were positive.
	/// </summary>
	public static int Assigns(GameState s, Card c)
	{
		var power = s.Power(c);
		if (
			s.Toughness(c) > power
			&& s.CardsIn(c.ControllerId, ZoneType.Battlefield)
				.Any(s.HasAbility<Effects.YourCreaturesAssignDamageByToughness>)
		)
			return s.Toughness(c);
		return power < 0 && s.HasAbility<Effects.AssignsDamageAsThoughPowerPositive>(c)
			? -power
			: power;
	}

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
		// Read once per combatant — every assignment is from the state before any damage lands.
		var assigned = new Dictionary<int, int>();
		int Assigned(Card c) =>
			assigned.TryGetValue(c.Id, out var n) ? n : assigned[c.Id] = Assigns(s, c);
		bool Deals(Card c) =>
			Assigned(c) > 0
			&& (
				firstStrikeStep
					? s.Has(c, "First strike") || s.Has(c, "Double strike")
					: !s.Has(c, "First strike") || s.Has(c, "Double strike")
			);

		var hits = new List<(Card Source, int TargetId, int Amount)>();
		foreach (var attacker in Attackers(s).Where(Deals))
		{
			if (!attacker.IsBlocked)
			{
				hits.Add((attacker, attacker.AttackingId, Assigned(attacker)));
				continue;
			}
			var remaining = Assigned(attacker);
			var blockers = BlockersOf(s, attacker.Id).ToList();
			foreach (var blocker in blockers)
			{
				var lethal = s.Has(attacker, "Deathtouch")
					? 1
					: Math.Max(1, s.Toughness(blocker) - blocker.Damage);
				var amount = Math.Min(remaining, lethal);
				hits.Add((attacker, blocker.Id, amount));
				remaining -= amount;
			}
			if (remaining > 0 && s.Has(attacker, "Trample"))
				hits.Add((attacker, attacker.AttackingId, remaining));
			else if (remaining > 0 && blockers.Count > 0)
				hits.Add((attacker, blockers[0].Id, remaining));
		}
		foreach (
			var blocker in s.CardsIn(Defender(s), ZoneType.Battlefield)
				.Where(c => c.BlockingId != 0 && Deals(c))
		)
			// Its attacker may be gone — a token that died to first strike no longer exists (CR 506.4).
			if (
				s.IsOnBattlefield(blocker.BlockingId)
				&& s.GetCard(blocker.BlockingId).AttackingId != 0
			)
				hits.Add((blocker, blocker.BlockingId, Assigned(blocker)));

		// Players never leave the game; a planeswalker token hit by first strike may already be gone.
		var players = hits.Select(h => h.TargetId)
			.Where(id => id == s.Player1Id() || id == s.Player2Id())
			.Distinct()
			.ToImmutableList();
		foreach (var (source, targetId, amount) in hits)
			s = Rules.Damage.Deal(s, source, targetId, amount, combat: true);
		return players.IsEmpty ? s : s.Raise(new CombatDamageToPlayersEvent(players));
	}

	/// <summary>CR 511.3: everything leaves combat as the end of combat step ends.</summary>
	public static GameState EndCombat(GameState s) =>
		ForEachPermanent(
			s,
			c => c.AttackingId != 0 || c.BlockingId != 0 || c.IsBlocked,
			c => c with { AttackingId = 0, BlockingId = 0, IsBlocked = false }
		);

	/// <summary>CR 514.2: damage wears off and "until end of turn" effects end, together, in cleanup.</summary>
	public static GameState CleanupPermanents(GameState s) =>
		ForEachPermanent(
			s,
			c =>
				c.Damage > 0
				|| c.DamagedByDeathtouch
				|| c.Components.Any(x => x is UntilEndOfTurn { UntilTurnOf: 0 })
				|| c.HasComponent<CopyUntilEndOfTurn>(),
			c =>
				(Card)(c.WithoutCopyEffect() with { Damage = 0, DamagedByDeathtouch = false }) with
				{
					Components = c.WithoutCopyEffect()
						.Components.RemoveAll(x => x is UntilEndOfTurn { UntilTurnOf: 0 }),
				}
		);

	static GameState ForEachPermanent(GameState s, Func<Card, bool> where, Func<Card, Card> update)
	{
		foreach (var id in new[] { s.Player1Id(), s.Player2Id() })
		foreach (var card in s.CardsIn(id, ZoneType.Battlefield).Where(where).ToList())
			s = s.UpdateObject(card.Id, update(card));
		return s;
	}
}
