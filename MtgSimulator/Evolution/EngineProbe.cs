using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Reflection;
using ImmutableGameObjects;
using MtgCore;

namespace MtgSimulator;

/// <summary>
/// **Did the deck's payoff go off with its own support already deployed?**
///
/// This is the fitness `Goldfish` was supposed to be and is not. The goldfish measures "how fast
/// can this deck deal 20 to an inert opponent", which aggro wins outright — measured on the
/// interpolation from assembled Storm to a pile of good cards, DISMANTLING Storm made it goldfish
/// *faster* (5.0 -> 4.0). Against an opponent that does nothing, the quickest kill is cheap
/// creatures attacking; a combo deck has to assemble first, so the instrument rewarded exactly the
/// thing the search already converges on without help.
///
/// What separates an engine from a pile is not speed, it is **execution**: the payoff resolved,
/// and when it did, the deck had already deployed the cards that make it worth resolving. Tendrils
/// with a storm count. Reanimate with a fatty in the graveyard. Frogmite behind four artifacts.
/// A pile of the format's best individual cards contains no payoff for any distinctive demand at
/// all, so it reads **zero** here where it read *best* on the goldfish.
///
/// **It names no archetype.** Payoffs and enablers both come out of <see cref="PoolFeatures"/> —
/// the cards in this deck that ASK a demand, and the cards that ANSWER it — so a synergy nobody
/// has thought of is measured the day its cards exist.
///
/// The reading is a diagnostic and an EXPLORATION signal, never a substitute for a win rate. The
/// standing evidence is that maximum-density goblins scored 24.4% against the AI's half-goblin
/// deck at 55.0%: features generate proposals, the win rate judges them.
/// </summary>
public sealed record EngineProbe(
	string Concept,
	ImmutableHashSet<string> Payoffs,
	ImmutableHashSet<string> Enablers
)
{
	/// <summary>
	/// One game's answer. <paramref name="Depth"/> is how many distinct enablers were already
	/// deployed the best time a payoff executed; <paramref name="Payoffs"/> is how many payoffs
	/// executed at all.
	///
	/// **The two are separate on purpose.** Depth 0 with payoffs 0 means the deck never cast the
	/// card it is built around; depth 0 with payoffs 3 means it cast it three times into an empty
	/// board. Those are different failures — the first is a mana or curve problem, the second says
	/// the engine is not assembling — and collapsing them into one number is how the goldfish
	/// managed to look correct.
	/// </summary>
	public readonly record struct Reading(int Depth, int Turn, int Payoffs)
	{
		public bool Assembled => Depth > 0;
	}

	public static readonly Reading Nothing = new(0, 0, 0);

	public bool IsUsable => !Payoffs.IsEmpty && !Enablers.IsEmpty;

	/// <summary>
	/// The payoff/enabler split for one demand.
	///
	/// **Payoffs come from the DECK, enablers from the POOL, and that asymmetry is the whole
	/// reason a control deck can be compared against a concept deck.** Enablers were deck-derived
	/// first, and it made the lift measurement vacuous: a control deck holds different cards, so
	/// under a deck-derived enabler set it has no enablers at all, scores depth 0 by construction,
	/// and lift comes out equal to depth. Measured, 18 of 43 controls scored exactly 0.0 — the
	/// column ranked identically to the one it existed to correct.
	///
	/// Pool-derived, "depth" means *how many cards answering this demand did the deck deploy*,
	/// which is a question any deck can be asked. A good-stuff pile full of creatures now scores
	/// properly against "a creature entered the battlefield", which is exactly what should sink
	/// that concept's lift.
	///
	/// A card may legitimately be BOTH (a Goblin lord asks for Goblins and is one). It is kept in
	/// both sets, and <see cref="Read"/> credits a payoff before deploying it, so a card never
	/// counts as its own support — the same rule <see cref="PoolFeatures.Satisfaction"/> applies.
	/// </summary>
	/// <summary>
	/// The probe for a whole <see cref="DeckCore"/> — the payoff slot against every support slot.
	///
	/// Keeps the same asymmetry <see cref="FromConcept"/> documents below, and for the same reason:
	/// payoffs are scoped to the DECK so the good-stuff control can be given the same ones, while
	/// enablers stay POOL-wide so "depth" means *how many demand-answering cards did this deck
	/// deploy*, which is a question any deck can be asked.
	///
	/// **Support slots are unioned, and that is a known ceiling.** A Dragonstorm deck deploying
	/// five rituals and no Dragon reads depth 5, which is the `Satisfaction`-vs-`WeakestSatisfaction`
	/// trap in a new place. It is much weaker here than it was for `SeedConcept` decks, because a
	/// core-built deck cannot be MISSING a slot — `DeckCore.Satisfy` fills every one to its floor —
	/// so what remains is a draw-consistency question rather than a construction one, and
	/// `EngineCandidate.DeadInDeck` reports it off `WeakestSatisfaction` either way.
	/// ponytail: union depth; per-slot readings need `Goldfish` to carry several probes per game.
	/// </summary>
	public static EngineProbe FromCore(DeckCore core, Decklist deck) =>
		new(
			core.Name,
			[
				.. deck.Spells.Keys.Where(n =>
					core.Slots.Any(s => s.IsIdentity && s.Cards.Contains(n))
				),
			],
			[.. core.Slots.Where(s => !s.IsIdentity).SelectMany(s => s.Cards)]
		);

	public static EngineProbe FromConcept(PoolFeatures features, int demandIndex, Decklist deck) =>
		new(
			features.Describe(demandIndex),
			[.. deck.Spells.Keys.Where(n => features.DemandsOf(n).Contains(demandIndex))],
			[.. features.SuppliersOf(demandIndex)]
		);

	/// <summary>
	/// Walks one game's event log once and reports the best assembly it saw.
	///
	/// Cheap by construction — one pass, two set lookups per event, no state reconstruction — so
	/// it can be extracted inside a parallel batch body without the log ever leaving it. Holding
	/// logs across a 28 000-game batch took the median game from 2.8s to 7.5s; see
	/// `MetagameEvolver.PlayBatch`.
	/// </summary>
	public Reading Read(IReadOnlyList<GameEvent> events, IReadOnlyDictionary<int, string> names)
	{
		var deployed = new HashSet<int>();
		var turnStarts = 0;
		var best = Nothing;
		var executed = 0;

		foreach (var e in events)
		{
			if (e is TurnStartedEvent)
			{
				turnStarts++;
				continue;
			}

			var cardId = CardIdOf(e);
			if (cardId == 0 || !names.TryGetValue(cardId, out var name))
				continue;

			// Credited BEFORE this card is deployed, so a card that is both payoff and enabler
			// never counts itself. Another copy of it deployed earlier still counts, which is
			// correct — four Goblin Chieftains genuinely do support each other.
			if (IsExecution(e) && Payoffs.Contains(name))
			{
				executed++;
				if (deployed.Count > best.Depth)
					best = best with { Depth = deployed.Count, Turn = TurnOf(turnStarts) };
			}

			// Distinct card INSTANCES, so one enabler that is cast, resolves and hits the
			// graveyard is one enabler and not three.
			if (Enablers.Contains(name))
				deployed.Add(cardId);
		}

		return best with
		{
			Payoffs = executed,
		};
	}

	/// <summary>
	/// Median depth, median turn, and the share of games that assembled at all.
	///
	/// **Median for the turn, mean for nothing.** A game that never assembles reports turn 0, and
	/// averaging those in would make a deck that assembles half the time on turn 4 look faster
	/// than one that assembles every game on turn 5.
	///
	/// **DEPTH IS MEDIANED OVER ASSEMBLED GAMES TOO, and it was not.** The argument above is about
	/// turn, and the same argument applies to depth — a game that never assembled contributes a
	/// meaningless 0 — but the filter was only applied to one of them. The consequence was
	/// arithmetic rather than subtle: below 50% assembly the median is taken over a majority of
	/// zeroes, so **`MedianDepth` was forced to exactly 0**, and since `Lift = depth - controlDepth`
	/// LIFT went to 0 with it.
	///
	/// Measured on a real DES report before the fix, across all 42 engines: **22 of 22 with
	/// assembly under 50% read depth exactly 0, and 0 of 20 above it did.** That is a perfect split
	/// on the median's own threshold, not a tendency.
	///
	/// It mattered most for exactly the decks mode 7 exists to find: a combo assembles rarely by
	/// nature, so LIFT — the ranking column — was structurally incapable of scoring one. The
	/// planted two-card Twin combo in CMB read `depth 0.0, LIFT 0.0` while holding 8 payoffs and 8
	/// enablers with no dead cards.
	///
	/// Rate is unaffected and remains over ALL games, which is what makes "assembles 20% of the
	/// time, but deeply when it does" a readable pair rather than one averaged number.
	/// </summary>
	public static (double MedianDepth, double MedianTurn, double AssemblyRate) Summarise(
		IReadOnlyList<Reading> readings
	)
	{
		if (readings.Count == 0)
			return (0, 0, 0);

		var assembled = readings.Where(r => r.Assembled).ToList();
		var rate = (double)assembled.Count / readings.Count;

		return (
			assembled.Count == 0 ? 0 : Median(assembled.Select(r => (double)r.Depth)),
			assembled.Count == 0 ? 0 : Median(assembled.Select(r => (double)r.Turn)),
			rate
		);
	}

	private static double Median(IEnumerable<double> values)
	{
		var sorted = values.Order().ToList();
		if (sorted.Count == 0)
			return 0;
		return sorted.Count % 2 == 1
			? sorted[sorted.Count / 2]
			: (sorted[sorted.Count / 2 - 1] + sorted[sorted.Count / 2]) / 2.0;
	}

	/// <summary>
	/// A turn here is a full round — `EndTurnAction` advances `TurnNumber` only when player 2 ends
	/// theirs — so two `TurnStartedEvent`s make one turn, and player 1 starts.
	/// </summary>
	private static int TurnOf(int turnStarts) => Math.Max(1, (turnStarts + 1) / 2);

	/// <summary>
	/// The card DID something, as opposed to merely existing somewhere.
	///
	/// **This is the one place a short list of event types is unavoidable, and the reason is worth
	/// stating.** Deployment can be read generically — any event naming a card is that card
	/// happening, and being milled or discarded is a perfectly good way for a reanimation target
	/// to be deployed. Execution cannot: a Tendrils pitched to Faithless Looting produces a
	/// `CardDiscardedEvent` carrying its id, and counting that as "the payoff went off" would make
	/// a deck that discards its own combo piece score as though it had won with them.
	///
	/// Resolution and entering play are the two ways a card's text happens here.
	/// </summary>
	/// <remarks>
	/// **`AbilityActivatedEvent` is the third way, and leaving it out made every activated-ability
	/// engine unmeasurable.** Resolution and entering play cover a spell and a permanent; a creature
	/// whose engine is its ability does its work at neither. Execution was therefore credited when
	/// the creature was CAST — before the ability could ever be used — so a planted two-card combo
	/// that activates a copier twenty times in a turn scored one execution, at the moment the copier
	/// landed with nothing yet deployed.
	///
	/// It does not weaken the closed-list rule this method exists for. That rule is about not
	/// counting a card being THROWN AWAY as the card going off (a Tendrils pitched to Faithless
	/// Looting emits an event carrying its id). Activating an ability is unambiguously the card's
	/// text happening, which is exactly what the list is supposed to contain.
	/// </remarks>
	private static bool IsExecution(GameEvent e) =>
		e
			is SpellResolvedEvent
				or CreatureEnteredBattlefieldEvent
				or PermanentEnteredBattlefieldEvent
				or AbilityActivatedEvent;

	/// <summary>
	/// The card an event is about, by reflection on the property name.
	///
	/// Same positional rule <see cref="PoolFeatures"/> harvests demands with, and it holds for the
	/// same reason: the engine names this consistently. An event about a card carries `CardId`,
	/// and an event carrying `CardId` is about a card. An event type added tomorrow is read the
	/// day it is emitted, with nothing here to update.
	/// </summary>
	private static int CardIdOf(GameEvent e) =>
		CardIdProperty
			.GetOrAdd(
				e.GetType(),
				t => t.GetProperty("CardId", BindingFlags.Public | BindingFlags.Instance)
			)
			?.GetValue(e)
			is int id
			? id
			: 0;

	/// Concurrent because `Read` is called from inside parallel game batches.
	private static readonly ConcurrentDictionary<Type, PropertyInfo?> CardIdProperty = new();
}
