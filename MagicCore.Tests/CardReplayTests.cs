using MagicCore.Cards;
using MagicCore.Sets;

namespace MagicCore.Tests;

/// <summary>
/// Every scripted card, replayed against its recording (GoldenCardReplay.txt, see GoldenReplay) in
/// short games built around it — so a change to one card's behaviour fails THAT card. RandomGameTests
/// is the broad net, but a card two scripts use is played there about once in 150 games.
///
/// Explicit, as it takes ~90 s: run it at every refactor gate.
///   dotnet test MagicCore.Tests --filter "FullyQualifiedName~CardReplayTests"
/// </summary>
[Explicit("~90 s: run at a refactor gate")]
public class CardReplayTests
{
	const int Seeds = 4;
	const int LastTurn = 16;

	static readonly GoldenReplay Replays = new("GoldenCardReplay.txt");

	[OneTimeTearDown]
	public void SaveReplays() => Replays.WriteIfUpdating();

	static IEnumerable<string> Cards => CardRegistry.Scripted.Order(StringComparer.Ordinal);

	static readonly List<CardDefinition> Spells =
	[
		.. Cards.Select(CardRegistry.Get).Where(c => !c.Data.TypeLine.Contains("Land")),
	];

	[TestCaseSource(nameof(Cards))]
	public void PlaysAsRecorded(string card)
	{
		var exercised = false;
		for (var seed = 1; seed <= Seeds; seed++)
		{
			var mix = (int)Mix(card, seed);
			var s = MagicRules.NewGame(Deck(card, mix), Deck(card, mix + 1), seed);
			var trace = new GoldenReplay.Trace();
			s = RandomGameTests.PlayRandomly(
				s,
				seed,
				trace,
				everyPath: true,
				lastTurn: LastTurn,
				onMove: (st, move) =>
					exercised |=
						MagicRules.CardOf(move) is var id and not 0 && st.GetObject(id).Name == card
			);
			Replays.Check(card, seed, trace, s);
		}
		if (!exercised)
			TestContext.Progress.WriteLine($"not exercised: {card}");
	}

	/// <summary>
	/// CardProbeTests' shape: three of the card, ten other scripted cards, basics of every colour. The
	/// ten are ranked by a hash of their own name, so a card added to the set changes only the decks
	/// it ranks into — the rest of the file stays put and a regenerated diff stays readable.
	/// </summary>
	static List<CardDefinition> Deck(string card, int seed) =>
		[
			.. Enumerable.Repeat(CardRegistry.Get(card), 3),
			.. Spells.OrderBy(c => Mix(c.Data.Name, seed)).Take(10),
			.. new[] { "Plains", "Island", "Swamp", "Mountain", "Forest" }.SelectMany(l =>
				Enumerable.Repeat(TestGames.Card(l), 5)
			),
		];

	/// <summary>FNV-1a with a final avalanche: stable across runs, unlike string.GetHashCode.</summary>
	static uint Mix(string text, int seed)
	{
		var h = 2166136261u ^ (uint)seed;
		foreach (var ch in text)
			h = (h ^ ch) * 16777619u;
		h ^= h >> 15;
		h *= 0x2c1b3c6du;
		return h ^ (h >> 12);
	}
}
