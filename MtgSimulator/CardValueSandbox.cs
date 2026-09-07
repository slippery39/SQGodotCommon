using System.Text.Json;
using ImmutableGameObjects;
using MtgCore;
using MtgCore.Cards.Builders;

namespace MtgSimulator;

/// <summary>
/// What a card is worth ONCE YOU CAN CAST IT, measured by casting it into a fixed position and
/// rolling the game forward.
///
/// **This is a different axis from the trained draft model and the two are expected to disagree.**
/// `DraftTrainingData` measures how much a card raises a 40-card deck's win rate, which is
/// dominated by castability, curve and consistency — so a 2/2 for 1 posts a high rate partly
/// BECAUSE it is always castable. That number cannot answer "at eight lands, do I tutor the 2/2 or
/// the 6/6 trample", because it is one scalar per card and the question is a function of context.
/// This sandbox holds castability constant and measures the other half. Draft picking keeps using
/// the win rates; nothing here replaces them.
///
/// The live AI already knows its own lands, so castability belongs to the caller, not here.
///
/// Two runs per card, differing only in <see cref="OpponentSimulationMode"/>:
///
/// | Run | Mode | Measures |
/// |---|---|---|
/// | <see cref="CardValue.Value"/> | `PassTurn` — dummies never attack | value when it resolves unopposed |
/// | <see cref="CardValue.StressValue"/> | `BoardOnly` — dummies loop every attack | value under pressure |
/// | <see cref="CardValue.Fragility"/> | the difference | how much the card needs a quiet board |
///
/// Fragility is the answer to "a 1/1 with a free 5-damage ability is overvalued by a sandbox that
/// never lets the opponent kill it" — such a card shows a large gap and its headline number is
/// therefore known to be unreliable, rather than merely suspected of it.
///
/// Both runs come out of the enum the search already has, so neither needed an engine change.
///
/// **Known limitation: the board is symmetric, so a symmetric effect prices at exactly zero.** A
/// one-sided sweeper and a Wrath both read as ~0 here. Same limitation `EndTurnCostSweep` carries;
/// an asymmetric fixture is the instrument for that family, and it would move every other card's
/// number too.
/// </summary>
public static class CardValueSandbox
{
	/// <summary>
	/// Mana granted above the card's own cost, so an {X} card has something to spend on X.
	///
	/// The generator enumerates X ASCENDING, so taking the first cast action gives X = 0 and puts
	/// the Hydras onto the battlefield as 0/0s that the zero-toughness rule kills on arrival. That
	/// artifact was the top of `EndTurnCostSweep`'s outlier list before it was fixed; the cast
	/// below takes the HIGHEST X within this budget for the same reason.
	/// </summary>
	private const int XBudget = 3;

	private const int ChoiceDrainCap = 100;

	/// <summary>
	/// How many of a card's cast actions get rolled out. The generator emits one per legal target,
	/// so a "any target" spell against this fixture produces ~12 (five creatures a side plus two
	/// players). Bounds the cost of a wide targeter without truncating a normal one.
	/// </summary>
	private const int MaxCastCandidates = 16;

	/// <summary>
	/// Mana cost of the inert filler stuffed into libraries and hands.
	///
	/// **It must be plausible, not merely out of the way.** Filler carries no components, so it is
	/// uncastable (`AddSpellAction` returns on a null `SpellComponent`) and contributes only a
	/// constant hand count — but any card that READS a mana value reads this one. 99 made Dark
	/// Tutelage drain 99 and report as the worst card in the cube by four orders of magnitude.
	/// </summary>
	private const int FillerManaCost = 3;

	private const int FillerHandSize = 4;
	private const int LibraryFiller = 30;

	/// <summary>
	/// Vanilla creatures given to EACH side. A ladder rather than N copies of one body so a card
	/// that cares about size — "destroy target creature with power 4 or greater", a fight spell,
	/// a −3/−3 — finds both a legal and an illegal target and has to choose.
	///
	/// **Both sides, not just the opponent.** Removal needs their board, but auras, equipment,
	/// mass pumps and fight spells need ours, and a card with no legal target measures as a blank.
	/// Symmetric on purpose: creature count, power and race pressure are all scored as differences,
	/// so an equal board cancels out of the baseline — see the symmetric-effect limitation above.
	///
	/// Guesswork until the first table is read. That is what it is a constant for.
	/// </summary>
	private static readonly int[] DummyLadder = [1, 2, 3, 4, 5];

	/// <summary>
	/// Half-turns rolled after the cast. Longer than the search's production default of 2, because
	/// this runs offline and the whole point is catching value that accrues over time — an upkeep
	/// trigger contributes nothing at a 2-half-turn horizon.
	/// </summary>
	public const int DefaultLookaheadTurns = 4;

	/// <summary>
	/// Cards stocked into every one of player 1's zones so a demand is ANSWERED, plus the storm
	/// count if one is asked for.
	///
	/// **Every zone, deliberately, because dispatching per demand kind is the mechanic-to-meaning
	/// table this project refuses to write.** A subtype filter reads the battlefield, a reanimation
	/// spec reads the graveyard, Dragonstorm's fetch reads the library, and a rummage cost reads the
	/// hand. Putting the suppliers in all four satisfies whichever it turns out to be, with nothing
	/// here having to know which.
	///
	/// <paramref name="StormCount"/> is the one exception and it is the same exception
	/// <see cref="PoolFeatures"/> already makes: "a spell was cast" is an event in a turn, not a
	/// card in a zone, so there is nothing to stock and the counter is set directly.
	/// </summary>
	/// <param name="BattlefieldOnly">
	/// Put the stock on the battlefield and nowhere else.
	///
	/// **The four-zone default is right for a DEMAND and wrong for a DECK.** Leverage does not know
	/// which zone a demand reads, so it stocks all of them; the cost is the documented artifact where
	/// two copies in the hand and library dilute both and auras price negative. A deck context is a
	/// BOARD — "what does this card do alongside the deck it lives in" — so diluting the hand with
	/// eight copies of other spells measures the dilution rather than the card.
	/// </param>
	private sealed record Stocking(
		string Key,
		IReadOnlyList<Card> Cards,
		int StormCount,
		bool BattlefieldOnly = false
	);

	private const int StockedCopies = 2;

	public static IReadOnlyList<CardValue> Measure(
		IReadOnlyList<Card> cards,
		int lookaheadTurns = DefaultLookaheadTurns,
		int seed = 7
	)
	{
		// Keyed by mana level as well as mode: the fixture's mana tracks the card's own cost, so
		// the baseline a card is scored against must be the one at ITS level.
		var controls =
			new Dictionary<(int Mana, OpponentSimulationMode Mode, string Stock), float>();
		var results = new List<CardValue>(cards.Count);

		foreach (var card in cards)
		{
			// A land is mana, and the evaluator already prices mana at weight 2.0. Casting one here
			// would measure that term, not the card.
			if (card.HasSubtype("Land"))
			{
				results.Add(new CardValue(card.Name, card.ManaCost, 0f, 0f, "land"));
				continue;
			}

			var mana = card.ManaCost + XBudget;
			var calm = MeasureOne(
				card,
				mana,
				OpponentSimulationMode.PassTurn,
				lookaheadTurns,
				seed,
				controls
			);
			if (calm.Error != null)
			{
				results.Add(new CardValue(card.Name, card.ManaCost, 0f, 0f, calm.Error));
				continue;
			}

			var stress = MeasureOne(
				card,
				mana,
				OpponentSimulationMode.BoardOnly,
				lookaheadTurns,
				seed,
				controls
			);
			if (stress.Error != null)
			{
				results.Add(new CardValue(card.Name, card.ManaCost, 0f, 0f, stress.Error));
				continue;
			}

			results.Add(new CardValue(card.Name, card.ManaCost, calm.Value, stress.Value, null));
		}

		return results;
	}

	/// <summary>
	/// **How much better than the AVERAGE candidate is this card, in the deck it would live in?**
	///
	/// The question deckbuilding actually asks is never "is this card good" but *"why would I add
	/// this card instead of another one"* — value is relative by nature, because a deck slot has an
	/// opportunity cost. `ConstructedValues.CardDelta` answers a different question honestly and
	/// precisely: how a card does in a RANDOM deck. A synergy piece is defined by being weak alone
	/// and strong in context, so no amount of data fixes that mismatch — measured on DES, Wirewood
	/// Conduit reads 51.6% in random decks and is never played, while the hand-built elf deck holding
	/// it beats the builder's own elf deck 65-35.
	///
	/// Two properties make this scale-free, and both are inherited rather than invented:
	///
	/// - **Per-mana controls.** <see cref="MeasureOne"/> already charges each card against a control
	///   at ITS OWN mana level, so "N turns have elapsed" is subtracted before anything is compared.
	///   That is what stops an eight-drop winning merely for costing eight — the horizon bias is a
	///   BASELINE problem, not a lookahead problem, and the baseline was already right.
	/// - **A fixed replacement level, not the argmax alternative.** Values are reported against the
	///   MEDIAN of the measured population. Ranking each card against "the next best other card"
	///   would be O(n²), unstable (one strong addition moves every other value) and non-transitive.
	///   Against a median it is O(n), comparable, and cacheable — the WAR construction, and it
	///   rescales automatically when the pool's power level moves.
	///
	/// **This proposes, it does not judge.** It exists to say which cards are worth trying in a
	/// slot; whether the resulting deck is good is still a win-rate question, and this must never
	/// become a fitness function — that is the rule that keeps `ArchetypeChallenge`'s maximum-density
	/// goblin deck (24.4%) from being what the search optimises toward.
	/// </summary>
	/// <param name="context">
	/// The cards that shape the board — in practice the deck being filled, or the core slot's own
	/// pool. Stocked onto the BATTLEFIELD ONLY; see <see cref="Stocking.BattlefieldOnly"/>.
	/// </param>
	public static IReadOnlyList<ContextValue> MeasureInContext(
		IReadOnlyList<Card> candidates,
		IReadOnlyList<Card> context,
		int lookaheadTurns = DefaultLookaheadTurns,
		int seed = 7
	)
	{
		var controls =
			new Dictionary<(int Mana, OpponentSimulationMode Mode, string Stock), float>();

		// Ordinal-sorted names rather than a hash: the control cache is keyed on this, and a
		// randomised hash is the bug this project has now found three times.
		var stocking = new Stocking(
			string.Join("|", context.Select(c => c.Name).Order(StringComparer.Ordinal)),
			context,
			StormCount: 0,
			BattlefieldOnly: true
		);

		var raw = new List<(string Name, float Value, string? Error)>(candidates.Count);
		foreach (var card in candidates)
		{
			if (card.HasSubtype("Land"))
			{
				raw.Add((card.Name, 0f, "land"));
				continue;
			}

			var (value, error) = MeasureOne(
				card,
				card.ManaCost + XBudget,
				OpponentSimulationMode.BoardOnly,
				lookaheadTurns,
				seed,
				controls,
				stocking
			);
			raw.Add((card.Name, value, error));
		}

		// The median is taken over MEASURED cards only. Folding an unmeasurable card in as 0 would
		// drag the baseline toward zero and quietly inflate everything else.
		var measured = raw.Where(r => r.Error is null).Select(r => r.Value).Order().ToList();
		if (measured.Count == 0)
			return [.. raw.Select(r => new ContextValue(r.Name, r.Value, 0f, r.Error))];

		var median =
			measured.Count % 2 == 1
				? measured[measured.Count / 2]
				: (measured[measured.Count / 2 - 1] + measured[measured.Count / 2]) / 2f;

		return
		[
			.. raw.Select(r => new ContextValue(
				r.Name,
				r.Value,
				r.Error is null ? r.Value - median : 0f,
				r.Error
			)),
		];
	}

	/// <summary>
	/// **How much is this card worth WITH its demands answered, against a bare board?**
	///
	/// This is the signal that says "combo payoff" without anyone naming an archetype, and it is the
	/// ranking `EngineDiscovery` needs: `DeckCore.For` generates a core for every card that asks
	/// anything, which on ALL is 203 cores and 50 distinct — far more than a report can be read as,
	/// and most of them are one broad demand ("a creature entered") that no deck has to be built
	/// around.
	///
	/// Supplier count cannot separate them, because it cannot tell a broad demand with a real payoff
	/// from a broad demand with a fake one. Leverage can:
	///
	/// | Card | bare | supplied | leverage |
	/// |---|---|---|---|
	/// | a good-stuff creature | high | high | **~0** — it does the same thing either way |
	/// | an aura that targets a creature | high | high | **~0** |
	/// | Dragonstorm | ~0, a blank | large | **large** |
	///
	/// **Both arms subtract their OWN control**, so the extra permanents a stocking puts on the
	/// board cancel out and what is left is the card's own gain from being supported.
	///
	/// One arm only (`PassTurn`). Fragility is a separate axis and doubling the cost to measure it
	/// here would be answering a question nobody asked of this table.
	/// </summary>
	public static IReadOnlyList<CardLeverage> MeasureLeverage(
		IReadOnlyList<string> payoffs,
		PoolFeatures features,
		IReadOnlyDictionary<string, Card> pool,
		int suppliersPerDemand = 2,
		int lookaheadTurns = DefaultLookaheadTurns,
		int seed = 7,
		int selfActionsPerTurn = LeverageSelfActions
	)
	{
		var controls = new Dictionary<(int, OpponentSimulationMode, string), float>();
		var results = new List<CardLeverage>(payoffs.Count);

		foreach (var name in payoffs)
		{
			if (!pool.TryGetValue(name, out var card))
				continue;

			var demands = features.DemandsOf(name).Where(features.Informative).ToList();
			if (demands.Count == 0)
			{
				results.Add(new CardLeverage(name, 0f, 0f, "asks nothing answerable"));
				continue;
			}

			var stock = new List<Card>();
			var storm = 0;
			foreach (var d in demands)
			{
				if (features.Demands[d] is PoolFeatures.SpellsCastDemand s)
				{
					storm = Math.Max(storm, s.Minimum);
					continue;
				}
				// Best supply first, and never the payoff itself — a card must not answer its own
				// demand, the same rule `Satisfaction` and `EngineProbe.Read` apply.
				stock.AddRange(
					features
						.SuppliersOf(d)
						.Where(n => !string.Equals(n, name, StringComparison.Ordinal))
						.Take(suppliersPerDemand)
						.Where(pool.ContainsKey)
						.Select(n => pool[n])
				);
			}

			if (stock.Count == 0 && storm == 0)
			{
				results.Add(new CardLeverage(name, 0f, 0f, "nothing in the pool to stock"));
				continue;
			}

			var mana = card.ManaCost + XBudget;
			var bare = MeasureOne(
				card,
				mana,
				OpponentSimulationMode.PassTurn,
				lookaheadTurns,
				seed,
				controls,
				selfActionsPerTurn: selfActionsPerTurn
			);
			var supplied = MeasureOne(
				card,
				mana,
				OpponentSimulationMode.PassTurn,
				lookaheadTurns,
				seed,
				controls,
				new Stocking(name, stock, storm),
				selfActionsPerTurn
			);

			results.Add(
				new CardLeverage(name, bare.Value, supplied.Value, Unmeasured(bare.Error, supplied.Error))
			);
		}

		return results;
	}

	/// The one cast failure that carries information rather than reporting a broken measurement.
	private const string NoLegalCast = "no legal cast action";

	/// <summary>
	/// Non-land actions the rollout may take per simulated turn **when measuring leverage**.
	///
	/// **A budget of 1 cannot represent a combo, and that is why Splinter Twin measured as an
	/// ordinary creature.** `PlayGreedyTurn` plays a land and then this many actions; at 1, an
	/// unbounded loop turns over once per turn, so "infinite tokens" prices as "one token a turn".
	/// Measured on the stocked fixture for a copier with its Illusionists in play:
	///
	/// | selfActionsPerTurn | leverage |
	/// |---|---|
	/// | 1 | 7.60 |
	/// | 4 | 11.40 |
	/// | 8 | 35.40 |
	/// | 16 | 71.40 |
	///
	/// **The monotone climb with no plateau IS the combo signature** — a card with a fixed effect
	/// flattens out. Note the control climbs too (97 → 282 over that sweep), so this is not a free
	/// gift to combo cards: it re-baselines both arms and only a card that keeps producing gains.
	///
	/// **And raising it does NOT work, which is why this is 1. Measured, on DES, 5 payoffs:**
	///
	/// | budget | measured | Kilnmother Vess (Twin) leverage |
	/// |---|---|---|
	/// | 1 | 5/5 | 0.00 |
	/// | 2 | 5/5 | 2.67 |
	/// | 3 | **2/5** | 11.50, but three cards lost to terminals |
	/// | 4 | **0/5** | — |
	/// | 8 | **0/5** | — |
	///
	/// Over the whole 43-engine DES report at budget 8: **0 of 43 measured**, against 43 of 44 at
	/// budget 1. The fixture simply DECIDES ITSELF — give the player more actions per turn and it
	/// kills the opponent inside the lookahead for every card, not only for combos, and
	/// `IsDecisive` then excludes the lot. More simulation cannot isolate a combo here; it just
	/// ends the game.
	///
	/// Budget 2 is the only setting that both measures and registers Twin at all, and 2.67 against
	/// Zombie Apocalypse's 45.87 does not change its rank in any useful way — while sitting one
	/// step from a cliff. A default must not be parked next to a catastrophic failure mode.
	///
	/// **The knob is kept because the sweep is the evidence**, not because anything should raise it.
	/// Telling a combo from a fixed effect wants `LoopDetector`, which already finds this exact
	/// combo, rather than a longer rollout.
	///
	/// **Leverage only.** <see cref="Measure"/> keeps the production default of 1 so
	/// `card_values_*.json` — which feeds the live AI's `ResolveChoice` — does not move for a
	/// change to an offline ranking.
	/// </summary>
	private const int LeverageSelfActions = 1;

	/// <summary>
	/// **A card that cannot be CAST without its support is not unmeasurable — it is bare zero.**
	///
	/// The bare arm plays the card into an empty fixture. A reanimation spell targets a creature in
	/// your graveyard, so with an empty graveyard it has no legal target and no legal cast action,
	/// and the arm fails. Marking the whole measurement unmeasured then threw away a perfectly good
	/// SUPPLIED number and dropped the card into tier 1 of <c>BlankFirstKey</c>, below every card
	/// whose bare arm happened to be castable.
	///
	/// **That inverted the ranking this key exists to produce.** `bare = 0` is the signature the
	/// sort is built to promote — "a blank until assembled" — and a card that literally cannot be
	/// cast alone is the purest instance of it. Measured on DES: Raise the Sunken (supplied 17.60,
	/// Lift 16, assembly 100%) and Necromantic Summons (supplied 23.50) were both discarded this
	/// way, while Zombie Apocalypse — bare 0.00 too, but castable with no Zombies because it simply
	/// does nothing — measured fine and ranked first. The discriminator between rank 1 and rank 24
	/// was whether the card is castable with no targets, which is a rules technicality and says
	/// nothing about the archetype.
	///
	/// **A failed SUPPLIED arm is still fatal**, and that asymmetry is the whole rule: without it
	/// there is no supplied number, so there is no leverage to report.
	/// </summary>
	internal static string? Unmeasured(string? bareError, string? suppliedError) =>
		suppliedError is not null ? suppliedError
		: bareError is null or NoLegalCast ? null
		: bareError;

	private static (float Value, string? Error) MeasureOne(
		Card card,
		int mana,
		OpponentSimulationMode mode,
		int lookaheadTurns,
		int seed,
		Dictionary<(int, OpponentSimulationMode, string), float> controls,
		Stocking? stocking = null,
		int selfActionsPerTurn = 1
	)
	{
		var (state, ids) = Table(mana, stocking);
		var ai = new MultiTurnBeamSearchAiStrategy(
			ids,
			lookaheadTurns: lookaheadTurns,
			opponentMode: mode,
			rng: new Random(seed),
			selfActionsPerTurn: selfActionsPerTurn
		);

		// The counterfactual. Without it the number is "N turns elapsed" PLUS the card, and the
		// elapsed part swamps the signal — it is the same for every card at this mana level, which
		// is exactly why it is worth computing once and subtracting.
		// **Each stocking gets its OWN control, and that is what makes leverage a real difference.**
		// A stocked fixture has more permanents, so it scores higher before the card is cast; charge
		// the stocked arm against the bare control and the leverage number would be the board.
		// **The budget is part of the key.** A control is "this fixture, played out by THIS rollout",
		// and the rollout's action budget changes it by a lot — 97.10 at 1 against 281.67 at 16 on
		// the Twin fixture. Two budgets sharing one cache would charge one arm against the other's
		// baseline. No caller mixes budgets within a call today; this makes that safe rather than
		// merely true.
		var stockKey = $"{stocking?.Key ?? ""}|{selfActionsPerTurn}";
		if (!controls.TryGetValue((mana, mode, stockKey), out var control))
		{
			try
			{
				control = ai.ScoreAfterCompletingTurn(state, ids.Player1Id);
			}
			catch
			{
				return (0f, "control threw");
			}

			// **A decided CONTROL poisons every card measured against it, and reports nothing.**
			// The subject's own score is checked for decisiveness below and excluded by name; the
			// control was not, so a fixture that resolves itself inside the lookahead produced a
			// baseline of ±9000 and every value became `score - 9000`. Measured while sweeping the
			// rollout budget: Raise the Sunken came back at **-8018.06 with no error at all**,
			// which reads as a real measurement of a catastrophic card.
			//
			// Guarded here rather than clamped: if the fixture decides the game without the card
			// in it, there is no counterfactual to subtract and the honest answer is that this
			// measurement did not happen.
			if (StateEvaluator.IsDecisive(control))
				return (0f, "control reached a terminal inside the lookahead");

			controls[(mana, mode, stockKey)] = control;
		}

		Card subject;
		(state, subject) = state.AddObject(
			card with
			{
				OwnerId = ids.Player1Id,
				ControllerId = ids.Player1Id,
			},
			parentId: ids.Player1HandId
		);

		// CAST from hand rather than injected onto the battlefield. Casting is what runs the ETB
		// ceremony — entry counters, copy effects, ETB triggers — and injection skips all of it,
		// which made five cards arrive as literal 0/0s in the sweep this fixture is modelled on.
		List<GameAction> candidates;
		try
		{
			var all = MtgActionGenerator
				.GetLegalActions(state, ids, ids.Player1Id)
				.Where(a => CardIdOf(a) == subject.Id)
				.Where(a => XOf(a) <= XBudget)
				.ToList();

			if (all.Count == 0)
				return (0f, NoLegalCast);

			// The generator emits ONE ACTION PER TARGET, so taking any single one picks a target
			// arbitrarily — and `PlayersOrCreatures` includes the caster's own face. That made every
			// burn spell in the cube measure as a blank: Lightning Bolt came back at -1.66, which is
			// the cost of the card leaving hand and nothing else. The card is worth what it is worth
			// PLAYED WELL, so every target is rolled out and the best is kept, which is what the
			// real search does.
			var maxX = all.Max(XOf);
			candidates = all.Where(a => XOf(a) == maxX).Take(MaxCastCandidates).ToList();
		}
		catch
		{
			return (0f, "threw while generating casts");
		}

		var best = float.MinValue;
		var lastError = NoLegalCast;

		foreach (var candidate in candidates)
		{
			GameState cast;
			try
			{
				cast = Apply(state, candidate, ai, ids.Player1Id);
			}
			catch
			{
				lastError = "threw while casting";
				continue;
			}

			// A spell legitimately leaves for the graveyard and a permanent legitimately stays on
			// the battlefield, so "did it reach the battlefield" is the wrong question for half the
			// set. Still in hand means the cast never happened and anything scored now is fixture.
			if (cast.GetCardsInZone(ids.Player1HandId).Any(c => c.Id == subject.Id))
			{
				lastError = "still in hand after casting";
				continue;
			}

			float score;
			try
			{
				score = ai.ScoreAfterCompletingTurn(cast, ids.Player1Id);
			}
			catch
			{
				lastError = "threw during rollout";
				continue;
			}

			// A rollout that reaches a win returns a DISCOUNTED terminal — ~9000 against a board
			// score of ~80 — so one such card sets the scale for the whole table and every real
			// value collapses into the bottom 1% of it. Sublime Archangel did exactly this at
			// 8971.70, two orders of magnitude clear of second place.
			//
			// Excluded rather than clamped, and reported by name: winning inside the lookahead says
			// the FIXTURE is decided, not that the card is worth 9000. Never compare against
			// WinScore directly here — IsDecisive tests the threshold that survives the discount.
			if (StateEvaluator.IsDecisive(score))
			{
				lastError = "reached a terminal inside the lookahead";
				continue;
			}

			if (score > best)
				best = score;
		}

		return best == float.MinValue ? (0f, lastError) : (best - control, null);
	}

	/// <summary>
	/// A fixed position with real mana at <paramref name="mana"/>, stocked libraries, a hand, and
	/// the dummy ladder on both sides.
	///
	/// <c>Create()</c> rather than <c>CreateForTesting()</c>: the 99-mana fixture decouples
	/// affordability from cost, which is right when mana is meant to cancel and wrong here, where
	/// it is the variable being held constant.
	/// </summary>
	private static (GameState State, MtgGameIds Ids) Table(int mana, Stocking? stocking = null)
	{
		var (state, ids) = MtgGameFactory.Create();
		state = state.WithoutDeckingLoss();

		var seats = new[]
		{
			(ids.Player1Id, ids.Player1LibraryId, ids.Player1BattlefieldId),
			(ids.Player2Id, ids.Player2LibraryId, ids.Player2BattlefieldId),
		};

		foreach (var (pid, lib, battlefield) in seats)
		{
			var player = state.GetPlayer(pid);
			state = state.UpdateObject(pid, player with { MaxMana = mana, CurrentMana = mana });

			// Library filler: turns draw, and a decking loss would swamp the signal.
			for (var i = 0; i < LibraryFiller; i++)
				(state, _) = state.AddObject(Filler(pid), parentId: lib);

			foreach (var n in DummyLadder)
			{
				var dummy = CardFactory
					.Creature($"Dummy {n}/{n}", manaCost: n, power: n, toughness: n)
					.Build();
				var body = dummy.GetComponent<CreatureComponent>()!;
				// Not summoning sick, or the BoardOnly stress run has nothing to attack with and
				// both arms measure the same thing.
				dummy = (Card)
					dummy.WithComponentReplaced(body with { HasSummoningSickness = false });
				(state, _) = state.AddObject(
					dummy with
					{
						OwnerId = pid,
						ControllerId = pid,
					},
					parentId: battlefield
				);
			}
		}

		// A hand, or discard and rummage costs cannot be paid and that whole family measures as
		// uncastable. Non-land so CardsInHandWeight counts them.
		for (var i = 0; i < FillerHandSize; i++)
			(state, _) = state.AddObject(Filler(ids.Player1Id), parentId: ids.Player1HandId);

		if (stocking is not null)
		{
			int[] zones = stocking.BattlefieldOnly
				? [ids.Player1BattlefieldId]
				:
				[
					ids.Player1BattlefieldId,
					ids.Player1GraveyardId,
					ids.Player1LibraryId,
					ids.Player1HandId,
				];

			foreach (var zone in zones.Where(z => z != 0))
			foreach (var card in stocking.Cards)
				for (var i = 0; i < StockedCopies; i++)
					(state, _) = state.AddObject(
						card with
						{
							OwnerId = ids.Player1Id,
							ControllerId = ids.Player1Id,
						},
						parentId: zone
					);

			// Set rather than stocked: see `Stocking`. `CastSpellAction` increments this before
			// resolution, so a card with storm sees count + 1, exactly as it would in a real turn.
			if (stocking.StormCount > 0 && state.TryGetGame() is { } game)
				state = state.UpdateObject(
					game.Id,
					game with
					{
						SpellsCastThisTurn = stocking.StormCount,
					}
				);
		}

		return (state, ids);
	}

	private static Card Filler(int ownerId) =>
		new()
		{
			Name = "F",
			ManaCost = FillerManaCost,
			OwnerId = ownerId,
			ControllerId = ownerId,
		};

	/// <summary>
	/// Applies an action and drains every choice it raises, so what gets rolled out is a position
	/// rather than a half-finished resolution.
	///
	/// <c>ProcessAllActions</c> deliberately STOPS at a <c>ChoiceAction</c> — the AI resolves
	/// choices, not the action loop — so a diagnostic that calls it alone leaves any card with a
	/// scry, a discard or a mode frozen mid-pipeline, and every such card measures as inert. That
	/// has cost this project twice already (`GreenLowWinRateAuditTests`, `EndTurnCostSweep`).
	/// </summary>
	private static GameState Apply(GameState state, GameAction action, IAiStrategy ai, int playerId)
	{
		var (next, ok) = state.TryAddAction(action);
		if (!ok)
			return state;

		(next, _) = next.ProcessAllActions();

		for (var i = 0; i < ChoiceDrainCap && next.IsWaitingForChoice; i++)
		{
			var choice = next.GetPendingChoice();
			if (choice == null)
				break;
			// AS ITS OWNER. Resolving our own trigger from the opponent's perspective is a real bug
			// this project has already shipped once — see ResolveAllChoices.
			var owner = next.GetPendingChoiceDecidingPlayerId();
			(next, _) = next.ResolveChoice(
				ai.ResolveChoice(next, choice, owner == 0 ? playerId : owner)
			);
		}

		return next;
	}

	private static int CardIdOf(GameAction action) =>
		action switch
		{
			CastCreatureAction c => c.CardId,
			CastPermanentAction p => p.CardId,
			CastSpellAction s => s.CardId,
			_ => -1,
		};

	private static int XOf(GameAction action) =>
		action switch
		{
			CastCreatureAction c => c.XValue,
			CastSpellAction s => s.XValue,
			_ => 0,
		};

	/// <summary>
	/// Sits beside the trained draft models rather than replacing them — the two files answer
	/// different questions and both are read.
	/// </summary>
	public static string PathFor(string setCode) =>
		Path.Combine("sim_results", $"card_values_{setCode.ToLowerInvariant()}.json");

	public static void Save(string path, IReadOnlyList<CardValue> values)
	{
		var dir = Path.GetDirectoryName(path);
		if (!string.IsNullOrEmpty(dir))
			Directory.CreateDirectory(dir);
		File.WriteAllText(
			path,
			JsonSerializer.Serialize(values, new JsonSerializerOptions { WriteIndented = true })
		);
	}

	public static IReadOnlyList<CardValue> Load(string path) => FromJson(File.ReadAllText(path));

	/// <summary>
	/// Parses from a string rather than a path, because System.IO cannot read a <c>res://</c> path
	/// inside an exported Godot build. Same reason <c>DraftTrainingStore.FromJson</c> exists.
	/// </summary>
	public static IReadOnlyList<CardValue> FromJson(string json) =>
		JsonSerializer.Deserialize<List<CardValue>>(json) ?? [];

	/// <summary>
	/// Name → value, for <see cref="WeightedStateEvaluator.CardValues"/>.
	///
	/// <paramref name="underPressure"/> picks which arm. **Both are kept and neither is "the"
	/// value**: on a passive board the AI should want the biggest threat, and on a dangerous one it
	/// should want the answer — Day of Judgment reads −35.10 in the quiet arm and +15.40 in the
	/// pressure arm, and both are correct for the board they describe.
	///
	/// Choosing per-evaluation from the race-pressure term is the intended end state and is NOT
	/// built yet: which arm helps at all is unmeasured, and a runtime switch between two tables is
	/// complexity that should follow the measurement rather than precede it. Until then this is a
	/// harness parameter, so the two can be swept against each other.
	///
	/// Unmeasured cards are omitted rather than zeroed — the evaluator skips a name it cannot find,
	/// which is "exactly average", where a stored 0 would be indistinguishable from a real zero.
	/// </summary>
	public static Dictionary<string, float> Lookup(
		IReadOnlyList<CardValue> values,
		bool underPressure
	) =>
		values
			.Where(v => v.WasMeasured)
			.ToDictionary(
				v => v.Name,
				v => underPressure ? v.StressValue : v.Value,
				StringComparer.Ordinal
			);
}

/// <summary>
/// One card's sandbox measurement. <see cref="NotMeasured"/> is null for a real result and
/// otherwise names why the card was skipped — reported rather than dropped, because a run that
/// measured forty cards and a run that measured four hundred otherwise produce output of exactly
/// the same shape.
/// </summary>
public sealed record CardValue(
	string Name,
	int ManaCost,
	float Value,
	float StressValue,
	string? NotMeasured
)
{
	/// <summary>
	/// How much of the card's value evaporates when the opponent is allowed to attack. A large gap
	/// means <see cref="Value"/> is describing a board the card will rarely be left alone on.
	/// </summary>
	public float Fragility => Value - StressValue;

	public bool WasMeasured => NotMeasured == null;
}

/// <summary>
/// What a payoff is worth with its demands answered, against the same card cast into a bare board.
///
/// **Near zero is the common and correct answer** — most of any pool is good-stuff cards that do
/// the same thing whatever else is in play. A large positive is the signature of a combo payoff:
/// a card that is a blank until its conditions are met.
///
/// A large NEGATIVE is worth reading rather than discarding. It means the stocked board made the
/// card worse, which is either a genuine anti-synergy or — more likely at first — a fixture
/// artifact, since stocking puts cards into four zones at once and some of those placements are
/// nonsense for the card in question.
/// </summary>
/// <param name="Raw">Score in the context, already net of a control at this card's mana level.</param>
/// <param name="OverAverage">
/// <paramref name="Raw"/> minus the median of every measured candidate — "how much better than the
/// average card competing for this slot". The number the fill should rank on; positive means worth
/// trying, and the scale moves with the pool's power level rather than against a fixed constant.
/// </param>
public sealed record ContextValue(string Name, float Raw, float OverAverage, string? NotMeasured);

public sealed record CardLeverage(string Name, float Bare, float Supplied, string? NotMeasured)
{
	public float Leverage => Supplied - Bare;

	public bool WasMeasured => NotMeasured == null;
}
