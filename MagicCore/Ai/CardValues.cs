using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Sets;

namespace MagicCore.Ai;

/// <summary>
/// What a card is worth once it can be cast, MEASURED: cast it into a fixed position, let it resolve,
/// and compare the position with the same one without the card. Card-agnostic — it reads outcomes,
/// never card text — so every scripted card is priced by the very rules that play it. MtgSimulator's
/// CardValueSandbox, ported minimal (story AI-1): a held card is worth a fraction of this.
///
/// **This turn's effect, not a rollout — measured, not assumed (2026-10-16).** MtgSimulator rolls
/// four turns forward, and on MtgCore (no blocking) that worked. Here, with a full ladder on each side,
/// the rollout's combat swamps the card: both boards attack and block until they are gone, so Rampart
/// Hunter (a 3/3 deathtouch) measured EXACTLY the control at every horizon from one to four turns, and
/// Artifist Acumen (first strike and a card) −11 to −19. So the card is cast and the turn played out
/// with NO attacks — end-step effects and until-end-of-turn pumps settle (Face Yourself's copies are
/// sacrificed; counted alive it read 47.5) — and the position is evaluated as the next turn begins.
/// The cost: value that accrues later (a planeswalker's next activations, an upkeep trigger) is not
/// counted — which only makes the AI readier to CAST such a card, never to hoard it.
///
/// The fixture: our first main phase, on EACH side a ladder of vanilla creatures, 1/1 to 5/5 — so
/// removal finds targets of every size, and auras, pumps and equipment find bodies — and both libraries
/// basic lands. Each card gets its own lands, untapped: a basic for every coloured pip, then generic
/// plus <see cref="XBudget"/> spread over the five basics — so an X spell has three to spend, not
/// whatever a fixed land count allows (MtgSimulator's rule). The control has the same lands. A card is
/// cast every legal way (each target, mode and X, up to <see cref="MaxCasts"/>); its best line counts.
/// A {C} pip can't be paid (no colourless sources) — such a card is reported not measured.
///
/// Our own choices on the way (targets of a trigger, "you may", "up to N") get GreedyAi's answer —
/// each option tried — so optional value is measured; the opponent's get the cheap answer.
///
/// Known limits: the board is symmetric, so a symmetric effect (a sweeper) prices near zero (as
/// MtgSimulator's); "its controller draws a card" costs only a land's worth, the libraries being lands;
/// and the land count follows the card's cost, so effects that count lands (domain, ramp) are measured
/// from different bases card to card.
/// </summary>
public static class CardValues
{
	/// <summary>One card's measured value, or why it has none (then it keeps the flat hand value).</summary>
	public sealed record Entry(string Name, double Value, string? NotMeasured = null);

	/// <summary>A safety cap only: a spell with two targets over two ladders has ~25 lines, all tried —
	/// a cap of 16 cut exactly the best ones, the generator listing our small creatures first.</summary>
	const int MaxCasts = 256;

	/// <summary>Mana beyond the card's own cost, so an {X} card has something to spend on X.</summary>
	public const int XBudget = 3;

	static readonly int[] Ladder = [1, 2, 3, 4, 5];

	static readonly string[] Basics = ["Plains", "Island", "Swamp", "Mountain", "Forest"];

	// ===== The tracked table: MagicCore/Sets/FRA.values.json, embedded like FRA.json =====

	static readonly JsonSerializerOptions Json =
		new()
		{
			WriteIndented = true,
			NewLine = "\n", // the same table on every machine (.gitattributes pins it to LF too)
			DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
			Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // "Ajani's Anguish", not escaped as '
		};

	static string TableFile(string set) => $"{set}.values.json";

	/// <summary>Where the table is written, from the solution root. GENERATED — never hand-edit it:
	/// <c>dotnet run --project MagicConsole -- --card-values</c>, and a test fails while it is stale.</summary>
	public static string TablePath => TablePathFor("FRA");

	/// <summary>Where a set's table is written (FRA and TRK: Star Trek's cards were flat 1.20 in hand until AI-17.15 measured them).</summary>
	public static string TablePathFor(string set) =>
		Path.Combine("MagicCore", "Sets", TableFile(set));

	/// <summary>The sets with a tracked table.</summary>
	public static readonly string[] TableSets = ["FRA", "TRK"];

	/// <summary>Every FRA card that plays, measured, in name order — what the table holds.</summary>
	public static IReadOnlyList<Entry> MeasureFra() => MeasureSet("FRA");

	/// <summary>Every card of the set that plays, measured, in name order.</summary>
	public static IReadOnlyList<Entry> MeasureSet(string set) =>
		Measure(
			[
				.. CardRegistry
					.Printings(set)
					.Select(p => p.Card.Name)
					.Distinct()
					.Where(CardRegistry.IsImplemented)
					.Order(StringComparer.Ordinal)
					.Select(CardRegistry.Get),
			]
		);

	/// <summary>The table's text: values to two decimals (so a diff shows real moves only), −0 as 0.</summary>
	public static string ToJson(IReadOnlyList<Entry> entries) =>
		JsonSerializer.Serialize(
			entries.Select(e => e with { Value = Math.Round(e.Value, 2) + 0.0 }),
			Json
		) + "\n";

	/// <summary>The embedded table's text, or null if it was never generated.</summary>
	public static string? TableText(string set = "FRA") => CardDataSet.ResourceText(TableFile(set));

	/// <summary>
	/// The tables of every set (FRA, then TRK for a name not in FRA) by card name, parsed ONCE — the AI looks values up inside its evaluation. Empty if
	/// the table was never generated.
	/// </summary>
	public static IReadOnlyDictionary<string, Entry> Fra => fra.Value;

	static readonly Lazy<IReadOnlyDictionary<string, Entry>> fra =
		new(() =>
		{
			var all = new Dictionary<string, Entry>();
			foreach (var set in TableSets)
				if (TableText(set) is { } text)
					foreach (var e in JsonSerializer.Deserialize<List<Entry>>(text)!)
						all.TryAdd(e.Name, e);
			return all;
		});

	// ===== Measuring =====

	/// <summary>
	/// Each card's measured value. Without the evaluator's threat term (Shayne, AI-5.2): a card's worth,
	/// not a position's clock — the fixture's creature ladder would otherwise give almost any card a flat
	/// bonus for the attacker it leaves unblocked. And without the six keywords AI-5.3 added: a turn with
	/// no attacks can't show what haste or reach does, so it would only echo their weight back.
	/// </summary>
	public static IReadOnlyList<Entry> Measure(IReadOnlyList<CardDefinition> cards, int seed = 7) =>
		Evaluator.Without(
			Evaluator.Terms.Threat | Evaluator.Terms.MoreKeywords | Evaluator.Terms.LandUnlock,
			() =>
			{
				var (fixture, me) = Fixture(seed);
				var results = new Entry[cards.Count];
				Parallel.For(0, cards.Count, i => results[i] = MeasureOne(cards[i], fixture, me));
				return (IReadOnlyList<Entry>)results;
			}
		);

	static Entry MeasureOne(CardDefinition card, GameState fixture, int me)
	{
		var name = card.Data.Name;
		if (card.Data.TypeLine.Contains("Land"))
			return new(name, 0, "a land — mana, which the evaluator already prices");
		var withLands = fixture;
		foreach (var land in LandsFor(CardFactory.Create(card, me).ManaCost))
			(withLands, _) = Put(withLands, me, CardRegistry.Get(land), ZoneType.Battlefield);
		var control = Evaluator.Evaluate(ThroughTheTurn(withLands, me), me);
		var (s, id) = Put(withLands, me, card, ZoneType.Hand);
		// Casts only: an activation from the hand (landcycling) is not the card's value once cast.
		var casts = s.DistinctMoves(s.LegalActions())
			.Where(a => a is CastSpellAction && s.IsMoveFor(a, id))
			.Take(MaxCasts)
			.ToList();
		if (casts.Count == 0)
			return new(name, 0, "can't be cast in the fixture");
		var best = casts.Max(a => Evaluator.Evaluate(ThroughTheTurn(s.Act(a), me), me));
		// A decided game is worth ±10000 against board scores of ~100: it says the FIXTURE is decided,
		// not what the card is worth (MtgSimulator's Sublime Archangel set its whole table's scale).
		return Math.Abs(best) >= BeamSearchAi.WinThreshold
			? new(name, 0, "decides the fixture")
			: new(name, best - control);
	}

	/// <summary>A basic per coloured pip (a hybrid's first colour), then generic plus XBudget round the five basics.</summary>
	static IEnumerable<string> LandsFor(ManaCost cost)
	{
		var colours = cost
			.Symbols.Where(x =>
				x.Kind is SymbolKind.Colored or SymbolKind.Hybrid or SymbolKind.TwoBrid
			)
			.Where(x => x.Color != ManaColor.Colorless)
			.Select(x => Basics[(int)x.Color])
			.ToList();
		var generic = cost.ManaValue - colours.Count + XBudget;
		return colours.Concat(
			Enumerable.Range(0, Math.Max(0, generic)).Select(i => Basics[i % Basics.Length])
		);
	}

	/// <summary>
	/// The rest of the turn with NO attacks, until the next turn begins — end-step effects and pumps
	/// settled. Priority is passed; our choices get GreedyAi's answer (attacks excepted: none), the
	/// opponent's the cheap one.
	/// </summary>
	static GameState ThroughTheTurn(GameState s, int me)
	{
		var ours = new GreedyAi(me);
		var turn = s.GetGame().TurnNumber;
		for (var i = 0; i < 500 && !s.IsOver() && s.GetGame().TurnNumber == turn; i++)
			s = s.GetPendingChoice() switch
			{
				null => s.Act(new PassPriorityAction { PlayerId = s.DecidingPlayerId() }),
				DeclareAttackerChoice none => s.Choose(Lookahead.QuickAnswer(s, none)),
				var choice when s.DecidingPlayerId() == me => s.Choose(ours.Answer(s, choice)),
				var choice => s.Choose(Lookahead.QuickAnswer(s, choice)),
			};
		return s;
	}

	/// <summary>The fixed position: our first main, both ladders, empty hands, libraries of basics.</summary>
	static (GameState State, int Me) Fixture(int seed)
	{
		var library = Basics.SelectMany(b => Enumerable.Repeat(CardRegistry.Get(b), 8)).ToList();
		var s = MagicRules.NewGame(library, library, seed);
		var me = s.GetGame().StartingPlayerId;
		bool Ready(GameState x) =>
			x.GetGame().Step == Step.PreCombatMain && x.DecidingPlayerId() == me;
		for (var i = 0; i < 200 && !Ready(s); i++)
			s = s.GetPendingChoice() is { } choice
				? s.Choose(choice.Options.Take(choice.MinChoices).Select(o => o.Id))
				: s.Act(
					s.GetGame().Step == Step.PreGame
						? new KeepHandAction { PlayerId = s.DecidingPlayerId() }
						: new PassPriorityAction { PlayerId = s.DecidingPlayerId() }
				);

		if (!Ready(s))
			throw new InvalidOperationException(
				"The card-value fixture never reached our first main phase."
			);
		foreach (var p in new[] { s.Player1Id(), s.Player2Id() })
		{
			foreach (var card in s.CardsIn(p, ZoneType.Hand).ToList())
				s = s.MoveCard(card.Id, s.ZoneId(p, ZoneType.Library));
			foreach (var size in Ladder)
				(s, _) = Put(s, p, LadderCreature(size), ZoneType.Battlefield);
		}
		return (s, me);
	}

	static CardDefinition LadderCreature(int size) =>
		CardDefinition.Vanilla(
			new CardData(
				$"Ladder {size}/{size}",
				"normal",
				$"{{{size}}}",
				size,
				"Creature — Sandbox",
				null,
				$"{size}",
				$"{size}",
				null,
				null,
				[],
				null
			)
		);

	/// <summary>A fresh card straight into a zone, outside the rules: untapped, no summoning sickness.</summary>
	static (GameState State, int CardId) Put(
		GameState s,
		int player,
		CardDefinition card,
		ZoneType zone
	)
	{
		var (next, obj) = s.AddObject(CardFactory.Create(card, player), s.ZoneId(player, zone));
		return (next, obj.Id);
	}
}
