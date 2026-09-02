using System.Text;

namespace MtgSimulator;

/// <summary>
/// One proposal the evolver considered: what it changed, what that did to the win rate, and
/// whether it stuck.
/// </summary>
/// <param name="ParentRate">
/// The parent's rate in the SAME generation, never the previous one. Common random numbers make
/// this a paired comparison — the parent and all its mutants played identical opponents on
/// identical shuffles — so `Delta` is the mutation's effect with shuffle and search variance
/// cancelled. Compared against a previous generation's number it would be neither paired nor
/// meaningful.
/// </param>
/// <param name="Outcome">
/// Why the proposal ended up where it did. `Rejected` and `TooSimilar` are different failures and
/// collapsing them would hide a field pinned by its diversity floor rather than by fitness.
/// </param>
public sealed record MutationRow(
	int Generation,
	int Slot,
	string Deck,
	string Added,
	string Removed,
	double ParentRate,
	double Rate,
	int Games,
	string Outcome
)
{
	public double Delta => Rate - ParentRate;
}

/// <summary>
/// **Every mutation proposed, kept so the search can be read rather than guessed at.**
///
/// The standing complaint the evolver could not answer is which cards it TRIED. A final decklist
/// shows what survived, and "Wirewood Conduit is not in the elf deck" has at least two causes with
/// opposite fixes — never proposed (a selection-heuristic problem) or proposed, played and cut
/// (a survivability problem). Nothing in the reports distinguished them, so both were argued from
/// the fill rule instead of measured.
///
/// Rejected proposals are recorded too, and they are the more informative half: an accepted list
/// only says what worked, while the rejects say what the mutator keeps reaching for and failing
/// with — and whether it is failing on fitness or on the diversity floor.
/// </summary>
public sealed class MutationLog
{
	public const string Accepted = "accepted";
	public const string Rejected = "rejected";
	public const string TooSimilar = "too-similar";
	public const string Reseeded = "reseeded";

	/// <summary>
	/// `Mutate` had a budget and returned nothing legal.
	///
	/// **This is the outcome that had to be INFERRED once, and inferring it is how the finding
	/// nearly went unnoticed.** A silent null is indistinguishable from a deck that was offered
	/// improvements and refused them, and those are opposite diagnoses.
	///
	/// Measured on a 10-deck DES run: engine slots with core pools of 129 and 54 cards produced 32
	/// and 33 real proposals on a full budget, while pools of 2 and 5 produced 3 and 2 — and both of
	/// the latter finished NON-VIABLE, having been frozen at their seed for the whole run.
	///
	/// **Core pool size is not the only cause and the other one is not identified.** A non-engine
	/// slot, with no pool lock at all, was measured at 0 real proposals against 6 dry in the same
	/// family of runs. Do not read this outcome as "the pool lock did it" — that is the prediction-
	/// before-measurement trap this project keeps paying for. It is an observable.
	/// </summary>
	public const string NoProposal = "no-proposal";

	private readonly List<MutationRow> _rows = [];

	private sealed class Tally
	{
		public int Proposed;
		public int Accepted;
		public double SumDelta;
		public int FirstGen = int.MaxValue;
		public int LastGen;
	}

	public IReadOnlyList<MutationRow> Rows => _rows;

	public void Add(MutationRow row) => _rows.Add(row);

	/// <summary>
	/// Cards a proposal adds and removes, as two display strings.
	///
	/// Counts are included because a mutation is often a RECOUNT — 3x to 4x of a card already in
	/// the deck — and rendering that as an empty diff would make the most common operator invisible.
	/// </summary>
	public static (string Added, string Removed) Diff(Decklist parent, Decklist child)
	{
		var added = new List<string>();
		var removed = new List<string>();

		foreach (
			var name in parent.Spells.Keys.Union(child.Spells.Keys).Order(StringComparer.Ordinal)
		)
		{
			var delta = child.CopiesOf(name) - parent.CopiesOf(name);
			if (delta > 0)
				added.Add($"{delta}x {name}");
			else if (delta < 0)
				removed.Add($"{-delta}x {name}");
		}

		if (child.Lands != parent.Lands)
		{
			var delta = child.Lands - parent.Lands;
			(delta > 0 ? added : removed).Add($"{Math.Abs(delta)}x Land");
		}

		return (string.Join(" + ", added), string.Join(" + ", removed));
	}

	/// <summary>
	/// Per-card: how often the search reached for it, and what happened when it did.
	///
	/// **`MeanDelta` is over PROPOSALS, not over accepted ones.** Averaging only the accepted rows
	/// would report every card as positive by construction — acceptance is conditioned on beating
	/// the parent — which is the same selection artifact this project already records for the
	/// gauntlet and for card values measured inside the decks that played them.
	/// </summary>
	public IReadOnlyList<(
		string Card,
		int Proposed,
		int Accepted,
		double MeanDelta,
		int FirstGen,
		int LastGen
	)> ByCard()
	{
		var rows = new Dictionary<string, Tally>(StringComparer.Ordinal);

		foreach (var row in _rows)
		{
			foreach (var name in NamesIn(row.Added))
			{
				var cur = rows.GetValueOrDefault(name) ?? new Tally();
				cur.Proposed++;
				cur.Accepted += row.Outcome == Accepted ? 1 : 0;
				cur.SumDelta += row.Delta;
				cur.FirstGen = Math.Min(cur.FirstGen, row.Generation);
				cur.LastGen = Math.Max(cur.LastGen, row.Generation);
				rows[name] = cur;
			}
		}

		return rows.Select(kv =>
				(
					kv.Key,
					kv.Value.Proposed,
					kv.Value.Accepted,
					kv.Value.Proposed == 0 ? 0 : kv.Value.SumDelta / kv.Value.Proposed,
					kv.Value.FirstGen,
					kv.Value.LastGen
				)
			)
			.OrderByDescending(r => r.Proposed)
			.ThenBy(r => r.Key, StringComparer.Ordinal)
			.ToList();
	}

	/// "2x Wirewood Conduit + 1x Land" -> the card names, dropping the synthetic land entry.
	private static IEnumerable<string> NamesIn(string diff) =>
		diff.Split(" + ", StringSplitOptions.RemoveEmptyEntries)
			.Select(part => part[(part.IndexOf(' ') + 1)..])
			.Where(name => !string.Equals(name, "Land", StringComparison.Ordinal));

	/// <summary>
	/// The whole log as CSV, because the per-card question is a filter and a spreadsheet answers it
	/// better than any fixed report — "when did Wirewood Conduit enter and leave slot 3" is one sort.
	/// </summary>
	public string ToCsv()
	{
		var sb = new StringBuilder(
			"Generation,Slot,Deck,Added,Removed,ParentRate,Rate,Delta,Games,Outcome\n"
		);
		foreach (var r in _rows)
			sb.AppendLine(
				$"{r.Generation},{r.Slot},{Escape(r.Deck)},{Escape(r.Added)},{Escape(r.Removed)},"
					+ $"{r.ParentRate:F4},{r.Rate:F4},{r.Delta:F4},{r.Games},{r.Outcome}"
			);
		return sb.ToString();
	}

	/// Card names contain commas — "Krenko, Mob Boss" — which is why this exists at all.
	private static string Escape(string field) =>
		field.Contains(',') || field.Contains('"') ? $"\"{field.Replace("\"", "\"\"")}\"" : field;

	public static string PathFor(string setCode) =>
		Path.Combine(
			"sim_results",
			$"mutations_{setCode.ToLowerInvariant()}_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
		);

	public string Save(string setCode)
	{
		var path = PathFor(setCode);
		var dir = Path.GetDirectoryName(path);
		if (!string.IsNullOrEmpty(dir))
			Directory.CreateDirectory(dir);
		File.WriteAllText(path, ToCsv());
		return path;
	}
}
