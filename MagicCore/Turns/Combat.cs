using System.Collections.Immutable;
using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Targeting;

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

	/// <summary>
	/// CR 302.6: under its controller's control continuously since their most recent turn began — after an
	/// opponent's extra turn (H.M.S. Bounty) that is not the turn before; a state saved before TurnBeganOn
	/// existed reads the turn before.
	/// </summary>
	public static bool IsSummoningSick(GameState s, Card card)
	{
		var game = s.GetGame();
		var mostRecentTurn =
			game.ActivePlayerId == card.ControllerId ? game.TurnNumber
			: s.GetPlayer(card.ControllerId).TurnBeganOn is > 0 and var began ? began
			: game.TurnNumber - 1;
		return card.ControlledSinceTurn >= mostRecentTurn;
	}

	public static bool CanAttack(GameState s, Card c) =>
		c.Is(CardType.Creature)
		&& !c.IsTapped
		&& (!s.Has(c, "Defender") || s.Has(c, AttacksDespiteDefender))
		&& (s.Has(c, "Haste") || !IsSummoningSick(s, c))
		&& !s.ThisTurnEffects<Effects.CantAttackThisTurn>(c.ControllerId).Any(); // Peace in Our Time

	/// <summary>
	/// No spell or ability for this player now (<see cref="Effects.CantCastOrActivate"/>): Yuriko during
	/// combat, Sen Triplets' lock this turn, Voice of Victory, Archon of Emeria, Clarion Conqueror — or a spell
	/// with split second on the stack (CR 702.61 — Samut gives it to its controller's instants and sorceries).
	/// Casting and activating ask here; triggers never do. <paramref name="what"/>: the card being cast
	/// (<paramref name="spell"/>) or the source of the ability. <paramref name="manaAbility"/>: asked for a mana
	/// ability — only a lock with ManaAbilitiesToo stops one (Sen, the Conqueror: TapForManaAction, ManaPayment).
	/// </summary>
	public static bool LockedDown(
		GameState s,
		int playerId,
		Card? what = null,
		bool spell = false,
		bool manaAbility = false
	)
	{
		if (Stopped(Locks(s, playerId), s, what, spell, manaAbility))
			return true;
		if (manaAbility)
			return false; // split second leaves mana abilities alone
		var spells = s.GetChildrenIds(s.GetWellKnownId(MagicKeys.Stack))
			.Select(s.GetObject)
			.OfType<Card>()
			.Where(c => c.Is(CardType.Instant | CardType.Sorcery))
			.ToList();
		return spells.Any(onStack =>
			s.CardsIn(onStack.ControllerId, ZoneType.Battlefield)
				.Any(s.HasAbility<Effects.YourInstantsAndSorceriesHaveSplitSecond>)
		);
	}

	static readonly List<(Effects.CantCastOrActivate, Card)> NoLocks = [];

	/// <summary>
	/// The "can't cast or activate" rules that bind this player now, each with its holder: every permanent's and
	/// the player's own "this turn" ones, by whose it is (By), when (If) and how many spells in (SpellsEachTurn).
	/// What each stops is <see cref="Stopped"/> — a payment asks once and then of each source.
	/// </summary>
	public static IReadOnlyList<(Effects.CantCastOrActivate Rule, Card Holder)> Locks(
		GameState s,
		int playerId
	)
	{
		// This runs for every cast and activation the AI considers: with no lock in play (the board's flag,
		// found once per state) nothing is scanned or made.
		if (!Characteristics.RuleWork(s).Locks)
			return NoLocks;
		List<(Effects.CantCastOrActivate, Card)>? locks = null;
		foreach (
			var holder in s.CardsIn(playerId, ZoneType.Command)
				.Concat(s.CardsIn(s.Player1Id(), ZoneType.Battlefield))
				.Concat(s.CardsIn(s.Player2Id(), ZoneType.Battlefield))
		)
		foreach (var rule in s.AbilitiesOf<Effects.CantCastOrActivate>(holder))
			if (
				rule.By.Includes(playerId, holder.ControllerId)
				&& s.GetPlayer(playerId).ThisTurn.SpellsCast >= rule.SpellsEachTurn
				&& rule.If?.Holds(s, Effects.Conditions.Of(holder)) != false
			)
				(locks ??= []).Add((rule, holder));
		return locks ?? NoLocks;
	}

	/// <summary>
	/// Whether one of these locks stops this spell (the card cast) or this ability (its source; none: a
	/// player's mana from anywhere, which only a lock on every source stops).
	/// </summary>
	public static bool Stopped(
		IReadOnlyList<(Effects.CantCastOrActivate Rule, Card Holder)> locks,
		GameState s,
		Card? what,
		bool spell = false,
		bool manaAbility = false
	)
	{
		foreach (var (rule, holder) in locks)
			if (
				(spell ? rule.Spells : rule.Abilities)
				&& (rule.ManaAbilitiesToo || !manaAbility)
				&& (
					rule.Of is null
					|| what is not null
						&& (spell || s.IsOnBattlefield(what.Id))
						&& rule.Of.Matches(s, what, new(holder.ControllerId, holder.Id))
				)
			)
				return true;
		return false;
	}

	/// <summary>
	/// Surveillance Phantasm: "can attack as though it didn't have defender"; Ghalta: "creatures you
	/// control can …" — a keyword-like grant, like CantBeBlocked: a static's scope says who.
	/// </summary>
	public const string AttacksDespiteDefender = "Can attack as though it didn't have defender";

	/// <summary>"Can't be blocked" — granted by static abilities and until-end-of-turn effects alike.</summary>
	public const string CantBeBlocked = "Can't be blocked";

	/// <summary>
	/// "Can't block" — Cleveland Booker's Worm token (a static), La'An's "target creature can't block
	/// this turn" (until end of turn): a keyword-like grant, like CantBeBlocked.
	/// </summary>
	public const string CantBlock = "Can't block";

	/// <summary>
	/// Pet Sehlat: "This creature can't attack alone." — a keyword-like grant, judged on the whole
	/// declaration as it finishes (FinishAttackersAction): declared alone, it doesn't attack.
	/// ponytail: the table doesn't refuse that declaration (CR 508.1d) — TBL-7.
	/// </summary>
	public const string CantAttackAlone = "Can't attack alone";

	/// <summary>
	/// Goad (CR 701.15, Undercity's Arena: "Goad target creature"): "attacks each combat if able" — a keyword-like
	/// grant until the goader's next turn; DeclareAttackerChoice offers it no "Don't attack".
	/// </summary>
	public const string Goaded = "Goaded";

	/// <summary>
	/// Goaded — read from the effect's stamp, never <c>s.Has</c>: goad is an effect on the creature, not an ability it
	/// has, so losing all abilities doesn't end it.
	/// </summary>
	public static bool IsGoaded(Card c) =>
		c.Components.Any(x => x is UntilEndOfTurn { Keyword: Goaded });

	public static bool CanBlock(GameState s, Card blocker, Card attacker) =>
		blocker.Is(CardType.Creature)
		&& !blocker.IsTapped
		&& !s.Has(blocker, CantBlock)
		&& !s.Has(attacker, CantBeBlocked)
		&& (!s.Has(attacker, "Flying") || s.Has(blocker, "Flying") || s.Has(blocker, "Reach"))
		&& !s.AbilitiesOf<Effects.CantBeBlockedBy>(attacker)
			.Any(r => r.Blockers.Matches(s, blocker, new(attacker.ControllerId, attacker.Id)))
		&& s.AbilitiesOf<Effects.CanBlockOnly>(blocker)
			.All(r => r.Attackers.Matches(s, attacker, new(blocker.ControllerId, blocker.Id)))
		&& !Effects.Protection.Stops(s, attacker, blocker); // CR 702.16f

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

	static bool StrikesIn(GameState s, Card c, bool firstStrikeStep) =>
		firstStrikeStep
			? s.Has(c, "First strike") || s.Has(c, "Double strike")
			: !s.Has(c, "First strike") || s.Has(c, "Double strike");

	/// <summary>
	/// The damage step's first part (MC-7): each attacker dealing damage now that two or more creatures block
	/// has its damage divided by its controller (CR 510.1c — "divided as its controller chooses among them";
	/// Foundations, 2024, removed the damage assignment order and its lethal-first rule): one
	/// <see cref="AssignCombatDamageChoice"/> per blocker but the last, which takes the rest, then
	/// <see cref="DealCombatDamageAction"/>. Nothing to divide: the damage at once.
	/// </summary>
	public static GameState BeginDamage(GameState s, bool firstStrikeStep)
	{
		var asks = Attackers(s)
			.Where(a => a.IsBlocked && Assigns(s, a) > 0 && StrikesIn(s, a, firstStrikeStep))
			.SelectMany(a =>
				BlockersOf(s, a.Id)
					.SkipLast(1)
					.Select(b =>
						(GameAction)new AssignCombatDamageChoice(a.ControllerId, a.Id, b.Id)
					)
			)
			.ToList();
		if (asks.Count == 0)
			return TurnStructure.GivePriority(
				DealDamage(s, firstStrikeStep),
				s.GetGame().ActivePlayerId
			);
		return s.SpawnAction(
			new PipelineAction { Steps = [.. asks, new DealCombatDamageAction(firstStrikeStep)] }
		);
	}

	/// <summary>Lethal damage to a blocker (CR 702.19b): its toughness less damage marked, 1 from deathtouch.</summary>
	static int Lethal(GameState s, Card attacker, Card blocker) =>
		s.Has(attacker, "Deathtouch") ? 1 : Math.Max(1, s.Toughness(blocker) - blocker.Damage);

	/// <summary>
	/// The split nobody chose — and the AI's quick answer (<see cref="AssignCombatDamageChoice.Default"/>): each
	/// blocker in declaration order takes lethal; the rest goes to the player with trample, else onto the first.
	/// </summary>
	public static IReadOnlyList<(int TargetId, int Amount)> DefaultSplit(
		GameState s,
		Card attacker,
		int total
	)
	{
		var blockers = BlockersOf(s, attacker.Id).ToList();
		var shares = new List<(int TargetId, int Amount)>();
		var remaining = total;
		foreach (var blocker in blockers)
		{
			var amount = Math.Min(remaining, Lethal(s, attacker, blocker));
			shares.Add((blocker.Id, amount));
			remaining -= amount;
		}
		if (remaining > 0 && s.Has(attacker, "Trample"))
			shares.Add((attacker.AttackingId, remaining));
		else if (remaining > 0 && shares.Count > 0)
			shares[0] = (shares[0].TargetId, shares[0].Amount + remaining);
		return shares;
	}

	/// <summary>
	/// Where a blocked attacker's damage goes: as divided (<paramref name="split"/>, each blocker but the last),
	/// the last blocker the rest — or, with trample and lethal on every blocker, lethal and the rest to the
	/// player (CR 702.19c). One blocker, or no answers: <see cref="DefaultSplit"/>.
	/// ponytail: with trample the last blocker never takes more than lethal — overkilling it instead of the
	/// player is never wanted; offer it if a card makes it so.
	/// </summary>
	static IReadOnlyList<(int TargetId, int Amount)> Division(
		GameState s,
		Card attacker,
		int total,
		IReadOnlyDictionary<(int Attacker, int Blocker), int>? split
	)
	{
		var blockers = BlockersOf(s, attacker.Id).ToList();
		if (split is null || blockers.Count < 2 || !split.Keys.Any(k => k.Attacker == attacker.Id))
			return DefaultSplit(s, attacker, total);
		var shares = new List<(int TargetId, int Amount)>();
		var remaining = total;
		var allLethal = true;
		foreach (var blocker in blockers.SkipLast(1))
		{
			var amount = Math.Min(remaining, split.GetValueOrDefault((attacker.Id, blocker.Id)));
			shares.Add((blocker.Id, amount));
			remaining -= amount;
			allLethal &= amount >= Lethal(s, attacker, blocker);
		}
		var last = blockers[^1];
		var toLast =
			allLethal && s.Has(attacker, "Trample")
				? Math.Min(remaining, Lethal(s, attacker, last))
				: remaining;
		shares.Add((last.Id, toLast));
		if (remaining > toLast)
			shares.Add((attacker.AttackingId, remaining - toLast));
		return shares;
	}

	/// <summary>
	/// CR 510: every combatant that deals damage in this step deals it at once — assignments are
	/// computed from the state before any of it lands. <paramref name="split"/>: how each attacker's
	/// controller divided it among two or more blockers (<see cref="BeginDamage"/>).
	/// </summary>
	public static GameState DealDamage(
		GameState s,
		bool firstStrikeStep,
		IReadOnlyDictionary<(int Attacker, int Blocker), int>? split = null
	)
	{
		// Read once per combatant — every assignment is from the state before any damage lands.
		var assigned = new Dictionary<int, int>();
		int Assigned(Card c) =>
			assigned.TryGetValue(c.Id, out var n) ? n : assigned[c.Id] = Assigns(s, c);
		bool Deals(Card c) => Assigned(c) > 0 && StrikesIn(s, c, firstStrikeStep);

		var hits = new List<(Card Source, int TargetId, int Amount)>();
		foreach (var attacker in Attackers(s).Where(Deals))
		{
			if (!attacker.IsBlocked)
			{
				hits.Add((attacker, attacker.AttackingId, Assigned(attacker)));
				continue;
			}
			foreach (var (targetId, amount) in Division(s, attacker, Assigned(attacker), split))
				hits.Add((attacker, targetId, amount));
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
		var players = hits.Where(h => h.TargetId == s.Player1Id() || h.TargetId == s.Player2Id())
			.Where(h => !Rules.Damage.Prevented(s, h.TargetId, h.Source)) // The Needs of the Many: none was dealt
			.Select(h => h.TargetId)
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
			ReturnControl(s),
			c =>
				c.Damage > 0
				|| c.DamagedByDeathtouch
				|| c.Components.Any(x => x is UntilEndOfTurn { UntilTurnOf: 0 })
				|| c.HasComponent<CopyUntilEndOfTurn>(),
			c =>
				(Card)(
					c.WithoutCopyUntilEndOfTurn() with
					{
						Damage = 0,
						DamagedByDeathtouch = false,
					}
				) with
				{
					Components = c.WithoutCopyUntilEndOfTurn()
						.Components.RemoveAll(x => x is UntilEndOfTurn { UntilTurnOf: 0 }),
				}
		);

	/// <summary>
	/// Worf, Intelligence Contractor: control gained "until end of turn" ends (CR 514.2) — every such effect
	/// at once, so it goes back to whoever had it before the FIRST (the oldest stamp), not the last.
	/// </summary>
	static GameState ReturnControl(GameState s)
	{
		foreach (var id in new[] { s.Player1Id(), s.Player2Id() })
		foreach (var card in s.CardsIn(id, ZoneType.Battlefield).ToList())
			if (
				card.GetComponents<UntilEndOfTurn>()
					.FirstOrDefault(u => u is { UntilTurnOf: 0, ReturnControlTo: > 0 }) is
				{ } first
			)
				s = s.GainControl(card.Id, first.ReturnControlTo);
		return s;
	}

	static GameState ForEachPermanent(GameState s, Func<Card, bool> where, Func<Card, Card> update)
	{
		foreach (var id in new[] { s.Player1Id(), s.Player2Id() })
		foreach (var card in s.CardsIn(id, ZoneType.Battlefield).Where(where).ToList())
			s = s.UpdateObject(card.Id, update(card));
		return s;
	}
}
