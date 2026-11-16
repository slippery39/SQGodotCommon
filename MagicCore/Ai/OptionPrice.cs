using System.Collections.Concurrent;
using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Limited;
using MagicCore.Sets;

namespace MagicCore.Ai;

/// <summary>
/// A held ANSWER priced by what it is likely to meet (story AI-26.4) — the search's side of
/// <see cref="OptionReport"/>, OFF by default (<c>BeamSearchAi.Settings.OptionPrice</c>, <c>beam+optionprice</c>).
/// Shayne: "the price the AI puts on it by using it early. Cards which don't get much better when we wait, are priced
/// cheaply … Cards which do get better are priced more expensive." An answer is a card whose target is chosen as it is
/// cast and that gains from some creature of theirs (Lightning Bolt, Extended Absence, a Pacifism Aura).
///
/// The answers in our hand are priced TOGETHER, replacing their held prices: what they make, on average over sampled
/// futures, of the creatures on their board and the ones they are likely to play — the best answer on the best
/// target, the next on the next (<see cref="Assign"/>) — less a charge for the mana they will cost then. So the cast
/// line gives up the price and gains the board; the hold line keeps it.
/// - What they are likely to play: sample AI sealed decks sharing the colours they have shown (<see cref="TheirLikely"/>).
/// - What an answer gains from a creature: measured once per pair, in <see cref="CardValues"/>' fixture, and kept
///   (<see cref="Gain"/>) — the card really cast at the creature, so what it can hit and kill is the engine's answer.
/// - Read at the search's root (the futures, the turns ahead), then one sum per distinct hand-and-board.
/// ponytail: a gain is the creature on a neutral board — today's clocks, counters and Auras on it are not counted;
/// an enters trigger's target is not an answer; the first use of a set samples its decks (~10 s) and the first sight
/// of an answer measures it against every likely creature (a hitch on the table — warm both at start-up if this
/// becomes the default); no time discount on a later target; the assignment is greedy.
/// </summary>
public static class OptionPrice
{
	/// <summary>AI sealed decks sampled per set for "what they probably play" — seeds no match or suite uses.</summary>
	public const int SampleDecks = 200;

	/// <summary>Futures the search prices a hand over — one fixed seed, the same for every line of a search.</summary>
	public const int Futures = 32;

	/// <summary>
	/// The most turns ahead that are priced: the sooner of the two clocks, 1 to this — 8, where the old option price
	/// stops at 4 (<see cref="Evaluator.MaxOptionTurns"/>). Chosen on the three-Bolt position, which Shayne holds "not
	/// even close": at 4 the third Bolt often meets no creature at all (findings, AI-26.2).
	/// </summary>
	public const int MaxTurns = 8;

	// ===== what they probably play =====

	static readonly ConcurrentDictionary<string, Lazy<SealedDeck[]>> sampled = new();

	// Lazy: a GetOrAdd factory runs once per thread that gets there first, and this one is ~10 s
	static SealedDeck[] DecksOf(string set) =>
		sampled
			.GetOrAdd(
				set,
				_ =>
					new(() =>
					{
						var decks = new SealedDeck[SampleDecks];
						Parallel.For(
							0,
							SampleDecks,
							i => decks[i] = SealedBuilder.Opponent(set, 5000 + i)
						);
						return decks;
					})
			)
			.Value;

	/// <summary>The opponent's likely creatures: the sample decks that share the colours they have shown, and their creatures by name.</summary>
	internal sealed record Likely(
		string Set,
		List<ManaColor> Shown,
		int Share,
		SealedDeck[] All,
		List<SealedDeck> Fit,
		Dictionary<string, Card> Creatures,
		List<Card> KnownInHand,
		int Unseen
	);

	internal static Likely TheirLikely(GameState s, int me)
	{
		var them = s.OpponentOf(me);
		var set =
			AllCards(s, them)
				.Concat(AllCards(s, me))
				.Select(c => c.Set)
				.Where(x => x is not null && CardValues.TableSets.Contains(x))
				.GroupBy(x => x)
				.OrderByDescending(g => g.Count())
				.FirstOrDefault()
				?.Key ?? "FRA";
		// a land shows the colours it makes - unless it makes more than two (any colour), which shows none of theirs
		IEnumerable<ManaColor> Shows(Card c) =>
			!c.Is(CardType.Land) ? c.Colors
			: ManaAbilities.ColorsOf(s, c).Distinct().ToList() is { Count: <= 2 } made ? made
			: [];
		var shown = s.CardsIn(them, ZoneType.Battlefield)
			.SelectMany(Shows)
			.Concat(
				new[] { ZoneType.Graveyard, ZoneType.Exile }
					.SelectMany(z => s.CardsIn(them, z))
					.Concat(
						s.CardsIn(them, ZoneType.Hand)
							.Where(c => c.KnownTo.HasFlag(Knowers.Opponent))
					)
					.SelectMany(c => c.Colors)
			)
			.Where(c => c != ManaColor.Colorless)
			.Distinct()
			.ToList();
		var all = DecksOf(set);
		// the decks sharing the most of the colours shown - all of them, or one fewer while under ten decks do
		var share = shown.Count;
		List<SealedDeck> Sharing(int n) => [.. all.Where(d => shown.Count(d.Colors.Contains) >= n)];
		while (share > 0 && Sharing(share).Count < 10)
			share--;
		var fit = Sharing(share);
		var creatures = fit.SelectMany(d => d.Main)
			.Distinct()
			.Select(CardRegistry.Get)
			.Where(d => d.Data.TypeLine.Contains("Creature"))
			.ToDictionary(d => d.Data.Name, d => CardFactory.Create(d, them));
		var knownInHand = s.CardsIn(them, ZoneType.Hand)
			.Where(c => c.KnownTo.HasFlag(Knowers.Opponent) && c.Is(CardType.Creature))
			.ToList();
		foreach (var c in knownInHand)
			creatures.TryAdd(c.Name, c);
		// a legend they already have isn't coming again: a second copy is the legend rule's, not a target
		foreach (
			var c in s.CardsIn(them, ZoneType.Battlefield).Where(c => c.Is(Supertype.Legendary))
		)
			creatures.Remove(c.Name);
		return new(
			set,
			shown,
			share,
			all,
			fit,
			creatures,
			[.. knownInHand.Where(c => creatures.ContainsKey(c.Name))],
			s.CardsIn(them, ZoneType.Hand).Count(c => !c.KnownTo.HasFlag(Knowers.Opponent))
		);
	}

	/// <summary>The turns ahead that are priced: the sooner of the two clocks on the board, 1 to <see cref="MaxTurns"/>.</summary>
	internal static int TurnsAhead(GameState s, int me) =>
		Math.Clamp(
			Math.Min(
				Evaluator.TurnsToKill(s, s.OpponentOf(me), me),
				Evaluator.TurnsToKill(s, me, s.OpponentOf(me))
			),
			1,
			MaxTurns
		);

	/// <summary>
	/// <paramref name="count"/> futures from one fixed seed: the creatures they play — of the cards they hold unseen
	/// and draw in <paramref name="turns"/> turns, from a deck that fits, those they have the mana for — and the
	/// answers we draw, from our own list (<paramref name="isAnswer"/>).
	/// </summary>
	internal static List<(List<string> Creatures, List<string> Answers)> SampleFutures(
		GameState s,
		int me,
		Likely likely,
		int turns,
		int count,
		Func<string, bool> isAnswer
	)
	{
		var them = s.OpponentOf(me);
		var theirLands = s.CardsIn(them, ZoneType.Battlefield).Count(c => c.Is(CardType.Land));
		var unseen = likely.Unseen;
		var budget = Enumerable.Range(1, turns).Sum(i => theirLands + i);
		var library = s.CardsIn(me, ZoneType.Library).Select(c => c.Name).ToList();
		var rng = new Random(1);
		var futures = new List<(List<string>, List<string>)>();
		for (var f = 0; f < count; f++)
		{
			var deck = likely
				.Fit[rng.Next(likely.Fit.Count)]
				.Main.OrderBy(_ => rng.Next())
				.Take(unseen + turns);
			// the creatures we know they hold come first, and spend their mana like any other
			var (spent, comes) = (0, new List<string>());
			foreach (var name in likely.KnownInHand.Select(c => c.Name).Concat(deck))
				if (
					likely.Creatures.TryGetValue(name, out var c)
					&& spent + c.ManaCost.ManaValue <= budget
				)
				{
					spent += c.ManaCost.ManaValue;
					comes.Add(name);
				}
			futures.Add((comes, [.. library.OrderBy(_ => rng.Next()).Take(turns).Where(isAnswer)]));
		}
		return futures;
	}

	static IEnumerable<Card> AllCards(GameState s, int player) =>
		new[]
		{
			ZoneType.Battlefield,
			ZoneType.Hand,
			ZoneType.Library,
			ZoneType.Graveyard,
			ZoneType.Exile,
		}.SelectMany(z => s.CardsIn(player, z));

	/// <summary>
	/// The most the answers make of the targets: gains[a][t] is answer a's gain on target t, each answer used once and
	/// each target answered once, an answer left over taking its floor (its use with no creature: burn at the face).
	/// Greedy by the biggest gain first.
	/// </summary>
	public static double Assign(IReadOnlyList<double[]> gains, IReadOnlyList<double> floors)
	{
		var pairs = new List<(double Gain, int A, int T)>();
		for (var a = 0; a < gains.Count; a++)
		for (var t = 0; t < gains[a].Length; t++)
			if (gains[a][t] > floors[a])
				pairs.Add((gains[a][t], a, t));
		var (usedA, usedT, total) = (new HashSet<int>(), new HashSet<int>(), 0.0);
		foreach (var (gain, a, t) in pairs.OrderByDescending(p => p.Gain))
			if (!usedA.Contains(a) && !usedT.Contains(t))
			{
				usedA.Add(a);
				usedT.Add(t);
				total += gain;
			}
		return total
			+ Enumerable.Range(0, gains.Count).Where(a => !usedA.Contains(a)).Sum(a => floors[a]);
	}

	// ===== what an answer gains from a creature: measured once, kept =====

	static readonly ConcurrentDictionary<(string Answer, string Creature), Lazy<double>> gains =
		new();

	static readonly ConcurrentDictionary<string, Lazy<double>> floors = new();

	static readonly Lazy<(GameState State, int Me)> fixture = new(() => CardValues.Fixture(7));

	/// <summary>
	/// What casting <paramref name="answer"/> at <paramref name="creature"/> gains — the evaluation after, settled,
	/// less before, the card's own flat price added back — in the card-value fixture (our lands for it, both creature
	/// ladders), the creature put on their side. 0 when it can't be cast at it or gains nothing. Without the threat
	/// term: the fixture's ladders have no clock worth reading. By name: measured once.
	/// </summary>
	public static double Gain(Card answer, Card creature) =>
		gains
			.GetOrAdd(
				(answer.Name, creature.Name),
				_ =>
					new(
						() =>
							Measured(
								answer,
								creature,
								(s, cast, target) => cast.Targets.Contains(target)
							)
					)
			)
			.Value;

	/// <summary>What <paramref name="answer"/> gains cast with no creature of theirs targeted (burn at the face) — its use when nothing better comes.</summary>
	public static double Floor(Card answer) =>
		floors
			.GetOrAdd(
				answer.Name,
				_ =>
					new(
						() =>
							Measured(
								answer,
								null,
								(s, cast, _) =>
									!cast.Targets.Any(id =>
										id != 0 && s.HasObject(id) && s.IsOnBattlefield(id)
									)
							)
					)
			)
			.Value;

	static double Measured(
		Card answer,
		Card? creature,
		Func<GameState, CastSpellAction, int, bool> counts
	)
	{
		// the price being built must not read itself: no root while a gain is measured. And a kept gain is the same
		// whoever asks first: exactly these terms off whatever that search has off, and its engine steps counted for
		// nobody (a position's "cost" would otherwise depend on which thread met the card first)
		var outer = root.Value;
		root.Value = null;
		try
		{
			return MagicRules
				.CountingSteps(
					() =>
						Evaluator.WithOnlyOff(
							Evaluator.Terms.HoldOption
								| Evaluator.Terms.Threat
								| Evaluator.Terms.LandUnlock,
							() => Evaluator.WithByUse(0, Measure)
						)
				)
				.Result;

			// the creature on a neutral board: the fixture's own ladder is never what holds it off
			double Measure()
			{
				{
					var (s, me) = fixture.Value;
					var them = s.OpponentOf(me);
					foreach (var land in CardValues.LandsFor(answer.ManaCost))
						s = s.AddObject(
							CardFactory.Create(CardRegistry.Get(land), me),
							s.ZoneId(me, ZoneType.Battlefield)
						).GameState;
					var target = 0;
					if (creature is not null)
					{
						(s, var put) = s.AddObject(
							creature.Fresh() with
							{
								OwnerId = them,
								ControllerId = them,
							},
							s.ZoneId(them, ZoneType.Battlefield)
						);
						target = put.Id;
					}
					(s, var held) = s.AddObject(
						answer.Fresh() with
						{
							OwnerId = me,
							ControllerId = me,
						},
						s.ZoneId(me, ZoneType.Hand)
					);
					var before = Evaluator.Evaluate(s, me);
					return s.DistinctMoves(s.LegalActions())
						.OfType<CastSpellAction>()
						.Where(c => c.CardId == held.Id && counts(s, c, target))
						.Select(c =>
							Evaluator.Evaluate(Lookahead.Settle(s.Act(c)), me)
							- before
							+ Evaluator.FlatHandCard
						)
						.Append(0)
						.Max();
				}
			}
		}
		finally
		{
			root.Value = outer;
		}
	}

	// ===== the search's root =====

	/// <summary>What one search reads once, at its root: who is priced, how far ahead, the futures — each made on first use.</summary>
	sealed class Root
	{
		public readonly int Me;

		public readonly int Turns;

		public readonly ConcurrentDictionary<string, double> Memo = new();

		readonly Lazy<Likely> likely;

		readonly Lazy<Dictionary<string, Card>> ours;

		readonly Lazy<List<(List<string> Creatures, List<string> Answers)>> futures;

		readonly Lazy<double> drawWants;

		readonly ConcurrentDictionary<string, bool> answers = new();

		public Root(GameState s, int me)
		{
			Me = me;
			Turns = TurnsAhead(s, me);
			likely = new(() => TheirLikely(s, me));
			// our own list is known: the targeted cards in our hand and library, one of each name
			ours = new(
				() =>
					s.CardsIn(me, ZoneType.Library)
						.Concat(s.CardsIn(me, ZoneType.Hand))
						.Where(c => c.Name != Hidden.UnknownCard && CastGains.IsOption(s, c))
						.GroupBy(c => c.Name)
						.ToDictionary(g => g.Key, g => g.First())
			);
			futures = new(
				() =>
					SampleFutures(
						s,
						me,
						Likely,
						Turns,
						OptionPrice.Futures,
						name => ours.Value.TryGetValue(name, out var c) && Answers(c)
					)
			);
			// what our draws will want of our mana over the turns ahead: the library's spells at their costs
			drawWants = new(() =>
			{
				var library = s.CardsIn(me, ZoneType.Library).ToList();
				return library.Count == 0
					? 0
					: Turns
						* (double)
							library.Where(c => !c.Is(CardType.Land)).Sum(c => c.ManaCost.ManaValue)
						/ library.Count;
			});
		}

		public Likely Likely => likely.Value;

		public Card Ours(string name) => ours.Value[name];

		public List<(List<string> Creatures, List<string> Answers)> Futures => futures.Value;

		public double DrawWants => drawWants.Value;

		/// <summary>An answer: it gains more from some creature they are likely to play than from none.</summary>
		public bool Answers(Card c) =>
			answers.GetOrAdd(c.Name, _ => Likely.Creatures.Values.Any(t => Gain(c, t) > Floor(c)));
	}

	static readonly AsyncLocal<Root?> root = new();

	/// <summary><paramref name="work"/> with <paramref name="me"/>'s answers priced from <paramref name="s"/> — or as before, when <paramref name="on"/> is false.</summary>
	public static T WithRoot<T>(bool on, GameState s, int me, Func<T> work)
	{
		var outer = root.Value;
		root.Value = on ? new Root(s, me) : null;
		try
		{
			return work();
		}
		finally
		{
			root.Value = outer;
		}
	}

	/// <summary>Is <paramref name="player"/>'s hand priced here?</summary>
	internal static bool Active(int player) => root.Value is { } r && r.Me == player;

	/// <summary>An answer in the priced hand: its target chosen as it is cast, and some likely creature of theirs worth it.</summary>
	internal static bool IsAnswer(GameState s, Card c) =>
		c.Name != Hidden.UnknownCard && CastGains.IsOption(s, c) && root.Value!.Answers(c);

	/// <summary>
	/// What <paramref name="answers"/> — <paramref name="player"/>'s answers in hand — are worth together: what they
	/// add, averaged over the futures, to what the answers we draw would make of their creatures on the board and to
	/// come — each less its own mana later (<see cref="Evaluator.RecastPerMana"/> a mana, by how busy our mana will be
	/// with the rest of the hand and our draws — Shayne: "the cheaper it is, the more likely you will find a fit for it
	/// in your curve").
	/// </summary>
	internal static double Value(GameState s, int player, IReadOnlyList<Card> answers)
	{
		var r = root.Value!;
		var board = s.CardsIn(s.OpponentOf(player), ZoneType.Battlefield)
			.Where(c => c.Is(CardType.Creature))
			.ToList();
		// a creature of theirs that ours hold off is worth that share as a target too (Evaluator.HeldOff)
		var heldOff = Evaluator.HeldOff(s, s.OpponentOf(player));
		double Share(Card t) => heldOff?.Contains(t.Id) == true ? Evaluator.HeldOffShare : 1;
		// how busy our mana will be with the REST of the hand and our draws - never with the answers themselves, so
		// one more answer never makes another dearer to cast (the evaluator's terms are monotonic)
		var lands = s.CardsIn(player, ZoneType.Battlefield).Count(c => c.Is(CardType.Land));
		var rest = s.CardsIn(player, ZoneType.Hand)
			.Where(c => !c.Is(CardType.Land) && !answers.Any(a => a.Id == c.Id))
			.Sum(c => c.ManaCost.ManaValue);
		var busy = Math.Round(
			Math.Min(1, (rest + r.DrawWants) / (r.Turns * Math.Max(1, lands))),
			2
		);
		double Charge(Card a) => Evaluator.RecastPerMana * a.ManaCost.ManaValue * busy;
		var key =
			string.Join(',', answers.Select(c => c.Name).Order(StringComparer.Ordinal))
			+ '|'
			+ string.Join(
				',',
				board
					.Select(c => Share(c) < 1 ? c.Name + "~" : c.Name)
					.Order(StringComparer.Ordinal)
			)
			+ '|'
			+ busy;
		return r.Memo.GetOrAdd(
			key,
			_ =>
				r.Futures.Average(f =>
				{
					// each answer's gains and floor less its own mana, never below nothing
					double Made(IReadOnlyList<Card> list) =>
						Assign(
							[
								.. list.Select(a =>
									board
										.Select(t => Gain(a, t) * Share(t))
										.Concat(
											f.Creatures.Select(n => Gain(a, r.Likely.Creatures[n]))
										)
										.Select(g => g - Charge(a))
										.ToArray()
								),
							],
							[.. list.Select(a => Math.Max(0, Floor(a) - Charge(a)))]
						);
					// what the hand's answers ADD to what the answers we draw would make anyway - so the last one
					// in hand is not also credited with those still to come
					var drawn = f.Answers.Select(r.Ours).ToList();
					return Made([.. answers, .. drawn]) - Made(drawn);
				})
		);
	}
}
