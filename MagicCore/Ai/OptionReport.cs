using System.Text;
using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Limited;
using MagicCore.Sets;

namespace MagicCore.Ai;

/// <summary>
/// A REPORT, not a price the AI uses yet (story AI-26.2): what each targeted card in a hand is worth cast NOW against
/// what it is likely to be worth LATER — Shayne: "a balance between how much value we get from it now, vs how much we
/// get from it if we hold it for later". It prices the TARGET, not the card (Lightning Bolt on a vanilla 1/1 at 20
/// life: the 1/1 is worth little, what they will play next is worth more):
/// - NOW: the card's best cast in this position — the evaluation after, settled, less before (<see cref="CastGains"/>'s
///   offer, on the real board).
/// - LATER: the same cast against each creature the opponent is likely to play — the creatures of sample AI sealed
///   decks that fit the colours they have shown (<see cref="SealedBuilder.Opponent"/>), each put onto their real
///   battlefield in turn. Card-agnostic: a card that can't hit or kill a creature gains nothing from it.
/// - HOW MANY ANSWERS: over sampled futures (the cards they hold unseen and draw, the creatures among them they have
///   the mana for; our own draws from our known list), the best answer takes the best target and so on. A card's
///   held price is what the hand makes with it less what it makes without it — the spare one of three is worth the
///   third-best target (Shayne: "Yes that is correct").
/// - MANA: Shayne: "its more about tempo because of the wasted mana … the cheaper it is, the more likely you will
///   find a fit for it in your curve". Holding is charged the mana left idle now, and its cost later by how busy our
///   mana will be with the rest of the hand.
/// ponytail: no time discount on a later target; the assignment is greedy, not optimal; a card that can't be cast at
/// this moment isn't priced; a target chosen by an enters trigger (not as the card is cast) isn't attributed; their
/// future creatures are creatures only (no planeswalkers), each priced alone on today's board. Each is a line to add
/// when Shayne's reading of the numbers shows it matters.
/// </summary>
public static class OptionReport
{
	/// <summary>Futures sampled per report, from one fixed seed: the same position gives the same report.</summary>
	public const int Futures = 400;

	/// <summary>
	/// A vanilla 1/1 as a share of a card (Shayne, 2026-11-16): "in general a 1/1 is not worth a full card. Kind of
	/// like how surveil is worth 0.33 of a card. A vanilla 1/1 is usually about 0.33 to 0.5 of a card, but it depends
	/// on the format and is obviously context dependant." The middle of his range - chosen, not measured. The
	/// evaluator's body for it (2 + 1.5 + 1 = 4.5) is about 0.5 of <see cref="ACard"/>, the top of it.
	/// ponytail: a vanilla 1/1 only, as he said it - a scale for other small bodies is his to give.
	/// </summary>
	public const double VanillaOneOneShare = 0.4;

	/// <summary>A card's worth once cast, in the evaluator's points: the average measured card (<see cref="CardValues"/>).</summary>
	public static double ACard => aCard.Value;

	static readonly Lazy<double> aCard =
		new(() => CardValues.Fra.Values.Where(e => e.NotMeasured is null).Average(e => e.Value));

	/// <summary>A 1/1 creature with no keyword and no ability of its own, as it stands on the board.</summary>
	static bool IsVanillaOneOne(GameState s, Card c) =>
		c.Is(CardType.Creature)
		&& s.Power(c) == 1
		&& s.Toughness(c) == 1
		&& c.Keywords.IsEmpty
		&& !c.Components.Any(x =>
			x
				is Effects.TriggeredAbilityComponent
					or Effects.ActivatedAbilityComponent
					or Effects.StaticEffectComponent
					or Effects.ManaAbilityComponent
		);

	sealed record Answer(
		string Name,
		Card Card,
		bool InHand,
		double Floor,
		Dictionary<int, double> Board,
		Dictionary<string, double> Pool
	);

	/// <summary>
	/// The report for <paramref name="me"/>, who is deciding in <paramref name="s"/>. <paramref name="turns"/>: how
	/// many turns ahead are priced, where the default is the sooner clock, 1 to <see cref="OptionPrice.MaxTurns"/>.
	/// </summary>
	public static string Of(GameState s, int me, int? turns = null) =>
		Evaluator.Without(Evaluator.Terms.HoldOption, () => Build(s, me, turns));

	static string Build(GameState s, int me, int? ahead)
	{
		var them = s.OpponentOf(me);
		var held = HeldValues.Fra(BeamSearchAi.Settings.HeldFraction);
		var game = s.GetGame();
		var turns = ahead ?? OptionPrice.TurnsAhead(s, me);
		var text = new StringBuilder();

		// ----- what they probably play: sample sealed decks that fit the colours they have shown (the search's own) -----
		var likely = OptionPrice.TheirLikely(s, me);
		var (set, shown, share, all, fit, creatures, _, unseen) = likely;
		var fitNote =
			share < shown.Count ? $" (on {share} of those colours: under ten fit them all)" : "";
		var pool = creatures.Keys.Order(StringComparer.Ordinal).ToList();
		var copies = fit.SelectMany(d => d.Main)
			.Where(creatures.ContainsKey)
			.GroupBy(n => n)
			.ToDictionary(g => g.Key, g => g.Count());

		// ----- our answers: targeted cards in hand, and those still in our library (our own list is known) -----
		var hand = s.CardsIn(me, ZoneType.Hand).Where(c => CastGains.IsOption(s, c)).ToList();
		var library = s.CardsIn(me, ZoneType.Library).ToList();
		var names = hand.Concat(library.Where(c => CastGains.IsOption(s, c)))
			.Select(c => c.Name)
			.Distinct()
			.ToList();
		var answers = new Dictionary<string, Answer>();
		var notPriced = new List<string>();
		foreach (var name in names)
		{
			var inHand = hand.FirstOrDefault(c => c.Name == name);
			var card = inHand ?? library.First(c => c.Name == name);
			// a library card is priced as though it were in hand now
			var start = inHand is not null ? s : Put(s, me, card, ZoneType.Hand).State;
			var (floor, board, cast) = Gains(start, me, name, held);
			if (!cast)
			{
				if (inHand is not null)
					notPriced.Add(name);
				continue;
			}
			var gains = new double[pool.Count];
			Parallel.For(
				0,
				pool.Count,
				i =>
				{
					var (withIt, id) = Put(start, them, creatures[pool[i]], ZoneType.Battlefield);
					gains[i] = Math.Max(
						0,
						Gains(withIt, me, name, held).Board.GetValueOrDefault(id)
					);
				}
			);
			// a card that gains from no target, here or likely, answers nothing (a vanilla instant, a draw spell)
			if (board.Values.Concat(gains).Any(g => g > floor))
				answers[name] = new(
					name,
					card,
					inHand is not null,
					floor,
					board,
					pool.Select((n, i) => (n, gains[i])).ToDictionary(x => x.n, x => x.Item2)
				);
		}

		// ----- the futures: their new creatures, our new answers -----
		var theirLands = s.CardsIn(them, ZoneType.Battlefield).Count(c => c.Is(CardType.Land));
		var futures = OptionPrice.SampleFutures(s, me, likely, turns, Futures, answers.ContainsKey);
		var boardTargets = answers
			.Values.Where(a => a.InHand)
			.SelectMany(a => a.Board.Keys)
			.Distinct()
			.ToList();

		// what a hand of answers makes over the futures; `without`: a board target already answered
		double Worth(IReadOnlyList<string> inHand, int without = 0) =>
			futures.Average(f =>
			{
				var list = inHand.Concat(f.Answers).Select(n => answers[n]).ToList();
				var board = boardTargets.Where(id => id != without).ToList();
				return OptionPrice.Assign(
					[
						.. list.Select(a =>
							board
								.Select(id => a.Board.GetValueOrDefault(id))
								.Concat(f.Creatures.Select(n => a.Pool.GetValueOrDefault(n)))
								.ToArray()
						),
					],
					[.. list.Select(a => a.Floor)]
				);
			});

		// ----- the report -----
		var mine = hand.Where(c => answers.ContainsKey(c.Name)).Select(c => c.Name).ToList();
		text.AppendLine(
			$"# Options for player {me}: turn {game.TurnNumber}, life {s.GetPlayer(me).Life} against {s.GetPlayer(them).Life}, {turns} turn(s) priced"
		);
		text.AppendLine(
			$"They have shown {(shown.Count == 0 ? "no colour" : string.Join(", ", shown))}: {fit.Count} of {all.Length} sample {set} sealed decks fit{fitNote}."
		);
		text.AppendLine(
			$"They hold {unseen} unseen card(s) and draw {turns}, with {theirLands} land(s): {futures.Average(f => (double)f.Creatures.Count):0.0} new creature(s) they can cast, on average."
		);
		var inLibrary = library.Count(c => answers.ContainsKey(c.Name));
		text.AppendLine(
			$"Our answers: {mine.Count} in hand, {inLibrary} more in the {library.Count}-card library ({futures.Average(f => (double)f.Answers.Count):0.00} drawn in {turns} turn(s), on average)."
		);
		text.AppendLine(
			$"A card is worth {ACard:0.0} (the average measured card); their vanilla 1/1 as a target {VanillaOneOneShare} of it, {VanillaOneOneShare * ACard:0.0}, plus what its attack costs us."
		);
		if (notPriced.Count > 0)
			text.AppendLine(
				$"Not priced — can't be cast at this moment: {string.Join(", ", notPriced.Distinct())}."
			);

		var hold = Worth(mine);
		foreach (var name in mine.Distinct())
		{
			var a = answers[name];
			var rest = mine.ToList();
			rest.Remove(name);
			var best = a
				.Board.Where(kv => kv.Value > a.Floor)
				.OrderByDescending(kv => kv.Value)
				.FirstOrDefault();
			var now = Math.Max(a.Floor, best.Value);
			var castNow = now + Worth(rest, best.Key);
			var count = mine.Count(n => n == name);
			text.AppendLine();
			text.AppendLine(
				$"## {name} {a.Card.ManaCost} ({(Priority.AtInstantSpeed(s, a.Card) ? "instant speed" : "sorcery speed")}){(count > 1 ? $", {count} in hand" : "")}"
			);
			text.AppendLine(
				$"Now: {now:0.0} — {(best.Key == 0 ? "with no target on the board worth more" : Label(s, best.Key))}"
					+ (
						a.Board.Count(kv => kv.Value > a.Floor) > 1
							? "; others: "
								+ string.Join(
									", ",
									a.Board.Where(kv => kv.Key != best.Key && kv.Value > a.Floor)
										.OrderByDescending(kv => kv.Value)
										.Take(4)
										.Select(kv => $"{Label(s, kv.Key)} {kv.Value:0.0}")
								)
							: ""
					)
					+ (a.Floor > 0 ? $"; with no creature targeted {a.Floor:0.0}" : "")
			);
			var slots = pool.Sum(n => copies.GetValueOrDefault(n));
			double Share(Func<string, bool> which) =>
				slots == 0
					? 0
					: 100.0 * pool.Where(which).Sum(n => copies.GetValueOrDefault(n)) / slots;
			text.AppendLine(
				$"Their likely creatures ({pool.Count} names): it gains nothing from {Share(n => a.Pool[n] <= a.Floor):0}% of them, more than now from {Share(n => a.Pool[n] > now):0}%."
			);
			foreach (
				var n in pool.Where(n => a.Pool[n] > a.Floor)
					.OrderByDescending(n => a.Pool[n])
					.Take(8)
			)
				text.AppendLine(
					$"    {a.Pool[n], 6:0.0}  {Label(creatures[n])}, in {100.0 * fit.Count(d => d.Main.Contains(n)) / fit.Count:0}% of the decks"
				);
			text.AppendLine($"Later, as our only answer: {Worth([name]):0.0}");
			text.AppendLine(
				$"Held, in this hand ({mine.Count} answer(s)): {hold - Worth(rest):0.0} — what the hand makes with it, less without it"
			);
			var upgrade = hold - castNow;
			text.AppendLine($"Waiting adds, by targets alone: {upgrade:+0.0;-0.0}");

			// ----- mana: idle now if held, and its cost later by how busy our mana is -----
			var cost = a.Card.ManaCost.ManaValue;
			var lands = s.CardsIn(me, ZoneType.Battlefield)
				.Where(c => c.Is(CardType.Land))
				.ToList();
			var open = lands.Count(c => !c.IsTapped);
			var others = s.CardsIn(me, ZoneType.Hand)
				.Where(c => !c.Is(CardType.Land) && c.Id != hand.First(h => h.Name == name).Id)
				.Select(c => c.ManaCost.ManaValue)
				.ToList();
			var ourTurn = game.ActivePlayerId == me && !Priority.AtInstantSpeed(s, a.Card);
			// the most of our open mana the other cards could use this turn (every subset: a hand is a few cards)
			var used = Enumerable
				.Range(0, 1 << Math.Min(others.Count, 12))
				.Select(mask => others.Where((_, i) => (mask >> i & 1) == 1).Sum())
				.Where(sum => sum <= open)
				.Max();
			var idle = ourTurn ? Math.Min(cost, open - used) : 0;
			var spells = library.Where(c => !c.Is(CardType.Land)).ToList();
			var wants =
				others.Sum()
				+ (
					library.Count == 0
						? 0
						: turns * (double)spells.Sum(c => c.ManaCost.ManaValue) / library.Count
				);
			var busy = lands.Count == 0 ? 1 : Math.Min(1, wants / (turns * lands.Count));
			var charge = Evaluator.RecastPerMana * (idle + cost * busy);
			text.AppendLine(
				$"Mana: holding leaves {idle} idle now; later its {cost} mana is wanted elsewhere {100 * busy:0}% (the rest of the hand and our draws want {wants:0.0} of {turns * lands.Count} mana) — charge {charge:0.0} at {Evaluator.RecastPerMana} a mana"
			);
			var net = upgrade - charge;
			text.AppendLine(net > 0 ? $"Net: HOLD by {net:0.0}" : $"Net: CAST NOW by {-net:0.0}");
		}
		if (mine.Count == 0)
			text.AppendLine("No targeted card in hand that can be cast at this moment.");
		// what the search itself would put on these answers (beam+optionprice): the same futures idea, its gains
		// measured once on a neutral board instead of on this one
		var (priced, price) = OptionPrice.WithRoot(
			true,
			s,
			me,
			() =>
			{
				var list = s.CardsIn(me, ZoneType.Hand)
					.Where(c => OptionPrice.IsAnswer(s, c))
					.ToList();
				return (list.Count, list.Count == 0 ? 0 : OptionPrice.Value(s, me, list));
			}
		);
		text.AppendLine();
		text.AppendLine(
			priced == 0
				? "The search (beam+optionprice) prices no card in this hand as an answer."
				: $"The search (beam+optionprice) prices this hand's {priced} answer(s) together at {price:0.0}, where their flat prices are {priced * Evaluator.FlatHandCard:0.0}."
		);
		return text.ToString();
	}

	/// <summary>What casting the card named gains now: by each permanent it can target, and with none (<c>Floor</c>).</summary>
	static (double Floor, Dictionary<int, double> Board, bool Cast) Gains(
		GameState s,
		int me,
		string name,
		HeldValues held
	)
	{
		// settled like the cast: mid-combat, Settle plays the damage, so both are read at the same moment
		var before = Evaluator.Evaluate(Lookahead.Settle(s), me, held);
		var (floor, board, any) = (0.0, new Dictionary<int, double>(), false);
		foreach (
			var cast in s.AiMoves(s.LegalActions())
				.OfType<CastSpellAction>()
				.Where(c => s.IsIn(c.CardId, me, ZoneType.Hand) && s.GetCard(c.CardId).Name == name)
		)
		{
			any = true;
			var after = Lookahead.Settle(s.Act(cast));
			var gain = Evaluator.Evaluate(after, me, held) - before + held.Of(name);
			var hit = cast
				.Targets.Where(id => id != 0 && s.HasObject(id) && s.IsOnBattlefield(id))
				.ToList();
			if (hit.Count == 0)
				floor = Math.Max(floor, gain);
			foreach (var id in hit)
			{
				// their vanilla 1/1 it removed: a share of a card, not the evaluator's body (Shayne's rule)
				var worth =
					s.GetObject(id) is Card t
					&& t.ControllerId != me
					&& IsVanillaOneOne(s, t)
					&& !(after.HasObject(id) && after.IsOnBattlefield(id))
						? gain - Evaluator.ValueOf(s, t) + VanillaOneOneShare * ACard
						: gain;
				board[id] = Math.Max(board.GetValueOrDefault(id, double.MinValue), worth);
			}
		}
		return (floor, board, any);
	}

	/// <summary>A copy of the card straight into a zone, outside the rules (as <see cref="CardValues"/>' fixture): nothing triggers.</summary>
	static (GameState State, int Id) Put(GameState s, int player, Card card, ZoneType zone)
	{
		var (next, obj) = s.AddObject(card, s.ZoneId(player, zone));
		return (next, obj.Id);
	}

	static string Label(GameState s, int id) =>
		s.GetObject(id) is Card c
			? c.Is(CardType.Creature)
				? $"{c.Name} {s.Power(c)}/{s.Toughness(c)}"
				: c.Name
			: "a player";

	static string Label(Card c) => $"{c.Name} {c.ManaCost} {c.OwnPower}/{c.OwnToughness}";
}
