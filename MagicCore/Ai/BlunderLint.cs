using System.Text;
using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Effects;
using MagicCore.Sets.Fra;
using MagicCore.Turns;

namespace MagicCore.Ai;

/// <summary>
/// AI-17.7, the blunder lint (Shayne, 2026-11-08): AI-vs-AI games in which every decision is checked for a
/// named, OBVIOUS mistake and counted, whatever the game's result. A lint, not a weight - it never changes a
/// decision. Each checker's exact rule is its comment; the self-test is running an older build
/// (<c>beam+nolanddrop</c>) and seeing the class the fix removed come back.
/// </summary>
public static class BlunderLint
{
	public const string PassedWithLand = "pass-with-land";
	public const string AttackIntoBigger = "attack-into-bigger";
	public const string WastedRemoval = "wasted-removal";
	public const string FaceBurn = "face-burn";
	public const string FlashOnOwnTurn = "flash-on-own-turn";

	public sealed record Finding(string Class, int Seed, int Move, int Turn, string Text);

	public static (string Report, IReadOnlyList<Finding> Findings) Run(
		int games,
		int firstSeed,
		string ai,
		int? only = null
	)
	{
		var decks = FraDecks.All.Keys.ToList();
		var found = new List<Finding>[games];
		var done = 0;
		var clock = System.Diagnostics.Stopwatch.StartNew();
		Parallel.For(
			0,
			games,
			g =>
			{
				if (only is { } o && firstSeed + g != o)
				{
					found[g] = [];
					return;
				}
				found[g] = Game(
					firstSeed + g,
					ai,
					FraDecks.All[decks[g % decks.Count]],
					FraDecks.All[decks[(g + 1) % decks.Count]]
				);
				var n = Interlocked.Increment(ref done);
				Console.Error.WriteLine(
					$"lint: {n}/{games} games, {clock.Elapsed.TotalMinutes:0.0} min"
				);
			}
		);
		var all = found.SelectMany(f => f).ToList();
		var md = new StringBuilder(
			$"# Blunder lint: {games} games, seeds {firstSeed}.., AI `{ai}`\n\n"
		);
		md.Append("| class | count | games with one |\n|---|---|---|\n");
		foreach (
			var c in new[]
			{
				PassedWithLand,
				AttackIntoBigger,
				WastedRemoval,
				FaceBurn,
				FlashOnOwnTurn,
			}
		)
			md.Append(
				$"| {c} | {all.Count(f => f.Class == c)} | {all.Where(f => f.Class == c).Select(f => f.Seed).Distinct().Count()} |\n"
			);
		foreach (var f in all.OrderBy(f => f.Class).ThenBy(f => f.Seed).ThenBy(f => f.Move))
			md.Append($"- {f.Class}: seed {f.Seed}, move #{f.Move}, turn {f.Turn}: {f.Text}\n");
		return (md.ToString(), all);
	}

	static List<Finding> Game(
		int seed,
		string ai,
		IReadOnlyList<CardDefinition> deck1,
		IReadOnlyList<CardDefinition> deck2
	)
	{
		var found = new List<Finding>();
		var s = MagicRules.NewGame(deck1, deck2, seed);
		var first = s.DecidingPlayerId();
		var ais = new[] { first, s.OpponentOf(first) }.ToDictionary(
			p => p,
			p => AiMatch.Create(ai, p, seed, searchThreads: 1)
		);
		var burns =
			new List<(
				int Card,
				int Who,
				int OppLife,
				int Move,
				int Turn,
				bool Creature,
				string Text
			)>();
		for (var move = 0; move < 4_000 && !s.IsOver(); move++)
		{
			var who = s.DecidingPlayerId();
			var decision = ais[who].Decide(s);
			var turn = s.GetGame().TurnNumber;
			void Add(string c, string text) => found.Add(new(c, seed, move, turn, text));

			// face burn resolved: the damage is the life the opponent lost once the stack is empty again
			if (s.StackIsEmpty() && burns.Count > 0)
			{
				foreach (var b in burns)
				{
					var lost = b.OppLife - s.GetPlayer(s.OpponentOf(b.Who)).Life;
					// 5. A damage spell sent at the opponent's face that did not kill them. A one-shot spell
					// spent on a few points of life; a creature target legal at the cast is noted.
					if (s.GetPlayer(s.OpponentOf(b.Who)).Life > 0)
						found.Add(
							new(
								FaceBurn,
								seed,
								b.Move,
								b.Turn,
								$"{b.Text}: {lost} damage, them {b.OppLife} -> {s.GetPlayer(s.OpponentOf(b.Who)).Life}"
									+ (b.Creature ? " (a creature was a legal target)" : "")
							)
						);
				}
				burns.Clear();
			}

			switch (decision.Action)
			{
				// 1. Passing priority in its own postcombat main phase, stack empty, with a land play legal.
				case PassPriorityAction
					when s.GetGame().Step == Step.PostCombatMain
						&& s.GetGame().ActivePlayerId == who
						&& s.StackIsEmpty()
						&& s.LegalActions().Any(a => a is PlayLandAction l && l.PlayerId == who):
					Add(PassedWithLand, "passed the second main phase with a land to play");
					break;
				case CastSpellAction cast when s.HasObject(cast.CardId):
					CheckCast(s, who, cast, move, turn, Add, burns);
					break;
			}

			// 5b. The same for a permanent whose enters-trigger is only damage (Ajani's Anguish): the target is a
			// choice made as the trigger goes on the stack, the source card named in it.
			if (
				s.GetPendingChoice() is ChooseTargetChoice tc
				&& decision.Answer is [var picked]
				&& picked == s.OpponentOf(who)
				&& s.HasObject(tc.SourceId)
				&& s.GetObject(tc.SourceId) is Card src
				&& s.AbilitiesOf<TriggeredAbilityComponent>(src)
					.Where(t => t.Targets.Count > 0)
					.ToList()
					is { Count: > 0 } trig
				&& trig.All(t => t.Effects.All(e => e is DealDamage))
			)
				burns.Add(
					(
						src.Id,
						who,
						s.GetPlayer(picked).Life,
						move,
						turn,
						tc.Spec.Candidates(s, who, src.Id)
							.Any(id =>
								s.GetObject(id) is Card c
								&& c.ControllerId != who
								&& c.Is(CardType.Creature)
							),
						$"{src.Name} trigger at their face{Held(s, who)}"
					)
				);

			// 2. A whole attack that is a pure downside, judged once declared (see PureDownside).
			if (
				s.GetGame().Step == Step.DeclareAttackers
				&& decision.State.GetGame().Step != Step.DeclareAttackers
				&& PureDownside(decision.State, who) is { } why
			)
				Add(AttackIntoBigger, why);

			s = decision.State;
		}
		return found;
	}

	/// <summary>What the caster held, and how many lands are left in its library - did it have a follow-up?</summary>
	static string Held(GameState s, int who) =>
		$" [hand: {string.Join(", ", s.CardsIn(who, ZoneType.Hand).Select(c => c.Name))}; library {s.CardsIn(who, ZoneType.Library).Count()} cards, {s.CardsIn(who, ZoneType.Library).Count(c => c.Is(CardType.Land))} lands; their board {s.CardsIn(s.OpponentOf(who), ZoneType.Battlefield).Count(c => c.Is(CardType.Creature))} creatures]";

	static void CheckCast(
		GameState s,
		int who,
		CastSpellAction cast,
		int move,
		int turn,
		Action<string, string> add,
		List<(int, int, int, int, int, bool, string)> burns
	)
	{
		var card = s.GetCard(cast.CardId);
		// 6. A flash creature cast in its caster's own main phase with the stack empty - it could have waited
		// for the opponent's turn (end step, a block, in response). Cast from the hand only.
		// No exemption for "wants it to attack": cast at the end of their turn it attacks just the same (Shayne, 2026-11-09).
		if (
			card.Is(CardType.Creature)
			&& s.Has(card, "Flash")
			&& s.GetGame().ActivePlayerId == who
			&& s.GetGame().Step is Step.PreCombatMain or Step.PostCombatMain
			&& s.StackIsEmpty()
			&& s.IsIn(card.Id, who, ZoneType.Hand)
		)
			add(FlashOnOwnTurn, $"{card.Name} (flash) cast in its own main phase");
		var effects = card.GetComponent<SpellEffectsComponent>()?.Effects ?? [];
		var removal = effects.Any(e => e is DealDamage or Destroy or Exile);
		var targets = cast.Targets.Where(s.HasObject).Select(s.GetObject).ToList();
		// 3a. A removal spell aimed only at its own caster's permanents (the target of a cast is chosen, so
		// this is the AI pointing a kill at itself).
		if (
			removal
			&& targets.Count > 0
			&& targets.All(t => t is Card c && c.ControllerId == who)
			&& !card.Is(CardType.Creature)
		)
			add(
				WastedRemoval,
				$"{card.Name} aimed at its own {string.Join(", ", targets.Select(t => t.Name))}"
			);
		// 5. Damage at the opponent's face: judged once it resolves (see Game).
		if (
			(card.Is(CardType.Instant) || card.Is(CardType.Sorcery))
			&& effects.Any(e => e is DealDamage)
			&& effects.All(e => e is DealDamage) // No Admittance also empowers Jace: not pure burn
			&& targets.Any(t => t is MagicPlayer p && p.Id != who)
		)
		{
			var creature = s.LegalActions()
				.OfType<CastSpellAction>()
				.Any(a =>
					a.CardId == cast.CardId
					&& a.Targets.Any(id =>
						s.HasObject(id)
						&& s.GetObject(id) is Card t
						&& t.ControllerId != who
						&& t.Is(CardType.Creature)
					)
				);
			burns.Add(
				(
					cast.CardId,
					who,
					s.GetPlayer(s.OpponentOf(who)).Life,
					move,
					turn,
					creature,
					$"{card.Name}{(cast.X > 0 ? $" (X={cast.X})" : "")} at their face{Held(s, who)}"
				)
			);
		}
	}

	/// <summary>
	/// 2. A declared attack that gives a creature away for little (Shayne, 2026-11-09: alpha strikes that kill or deal
	/// decent damage are fine; the mistake is a pure downside). Fires when some attacker would be eaten - an untapped
	/// creature of the defender could block it, kill it and survive, and it has no first/double strike, deathtouch,
	/// indestructible or attack trigger - AND the defender can hold the whole attack to at most <see cref="Minimal"/>
	/// damage by blocking the biggest attackers it can (menace attackers counted as unblocked), AND the attacker's
	/// controller has no instant in hand with an untapped land to back it.
	/// </summary>
	const int Minimal = 2;

	static string? PureDownside(GameState s, int who)
	{
		var attackers = Combat.Attackers(s).ToList();
		var blockers = s.CardsIn(s.OpponentOf(who), ZoneType.Battlefield)
			.Where(b => !b.IsTapped)
			.ToList();
		var trick =
			s.CardsIn(who, ZoneType.Hand).Any(c => c.Is(CardType.Instant) || s.Has(c, "Flash"))
			&& s.CardsIn(who, ZoneType.Battlefield).Any(c => c.Is(CardType.Land) && !c.IsTapped);
		if (attackers.Count == 0 || trick)
			return null;
		var eaten = attackers
			.Where(a =>
				!new[]
				{
					"Menace",
					"First strike",
					"Double strike",
					"Deathtouch",
					"Indestructible",
				}.Any(k => s.Has(a, k))
				&& !s.AbilitiesOf<TriggeredAbilityComponent>(a).Any()
				&& blockers.FirstOrDefault(b =>
					Combat.CanBlock(s, b, a)
					&& s.Power(b) >= s.Toughness(a) - a.Damage
					&& s.Toughness(b) - b.Damage > s.Power(a)
				)
					is not null
			)
			.ToList();
		if (eaten.Count == 0)
			return null;
		// the defender's best hold: each blocker stops the biggest attacker it can block (one blocker per attacker)
		var open = attackers.Where(a => !s.Has(a, "Menace")).OrderByDescending(s.Power).ToList();
		var free = blockers.ToList();
		var through = attackers.Where(a => s.Has(a, "Menace")).Sum(s.Power);
		foreach (var a in open)
		{
			var b = free.FirstOrDefault(b => Combat.CanBlock(s, b, a));
			if (b is null)
				through += s.Power(a);
			else
				free.Remove(b);
		}
		return through <= Minimal
			? $"{string.Join(", ", eaten.Select(a => $"{a.Name} {s.Power(a)}/{s.Toughness(a)}"))} eaten; the attack of {attackers.Count} can be held to {through} damage"
			: null;
	}
}
