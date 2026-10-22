using ImmutableGameObjects;
using MagicCore.Actions;
using MagicCore.Ai;
using MagicCore.Sets.Fra;

namespace MagicCore.Tests.Ai;

/// <summary>
/// Game records (story TBL-5): every transition kept, a replay that rebuilds every position, and a
/// record that says so when it no longer replays — never a different game in silence.
/// </summary>
public class GameRecordTests
{
	/// <summary>
	/// A game recorded as the table records it — every transition: forced passes as "rule", the rest
	/// as "ai" — with a manual tap and a sandbox placement along the way, so every token kind is in it.
	/// THE model loop: each move is applied FROM ITS TOKEN, never from the state a decision returned,
	/// so what was recorded is what was played (PositionSuite's rule).
	/// </summary>
	static (GameRecord Record, GameState Last) PlayRecorded(int seed)
	{
		var record = GameRecord.New(
			seed,
			FraDecks.All["White-Black"],
			FraDecks.All["Red-Green"],
			("You", "AI"),
			sideboard1: [FraCards.Get("Last Gasp"), FraCards.Get("Rank Rat")]
		);
		var s = record.Start();
		var ais = new Dictionary<int, IAi>
		{
			[s.Player1Id()] = new GreedyAi(s.Player1Id()),
			[s.Player2Id()] = new GreedyAi(s.Player2Id()),
		};
		var (tapped, put) = (false, false);
		for (var i = 0; i < 5000 && !s.IsOver(); i++)
		{
			if (s.LegalActions() is [PassPriorityAction pass])
			{
				Record(MoveTokens.Token(s, pass, null), "rule");
				continue;
			}
			var who = s.DecidingPlayerId();
			if (!put && s.GetGame().TurnNumber >= 3 && Sandbox.CanPut(s))
			{
				Record(MoveTokens.Sandbox(who, "Last Gasp", ZoneType.Hand), "sandbox");
				put = true;
				continue;
			}
			var tap = s.GetPendingChoice() is null
				? s.CardsIn(who, ZoneType.Battlefield)
					// a land that can't make white (colour 0) — so a dropped colour can't replay as a legal tap
					.Where(c => !s.TapOptions(who, c.Id).Any(t => t.Color == ManaColor.White))
					.SelectMany(c => s.TapOptions(who, c.Id))
					.FirstOrDefault()
				: null;
			if (!tapped && s.GetGame().TurnNumber >= 4 && tap is not null)
			{
				Record(MoveTokens.Token(s, tap, null), "you");
				tapped = true;
				continue;
			}
			var decision = ais[who].Decide(s);
			Record(MoveTokens.Token(s, decision), "ai", decision.What);
		}

		void Record(string token, string kind, string said = "")
		{
			record = record.Add(s, token, kind, said);
			s = MoveTokens.Apply(s, token);
		}
		Assert.That(tapped && put, "the game reached a tap and a sandbox placement");
		return (s.IsOver() ? record.Ended(s) : record, s);
	}

	[Test]
	public void ARecordedGame_ReplaysEveryPosition_ToTheSameEnd()
	{
		var (record, last) = PlayRecorded(seed: 4);
		var (before, end) = record.Replay();

		Assert.That(before, Has.Count.EqualTo(record.Moves.Count));
		Assert.That(MoveTokens.Ending(end), Is.EqualTo(MoveTokens.Ending(last)));
		Assert.That(MoveTokens.Offers(end), Is.EqualTo(MoveTokens.Offers(last)));
		Assert.That(
			record.Moves.Select(m => m.Kind).Distinct(),
			Is.SupersetOf(new[] { "rule", "ai", "you", "sandbox" })
		);
	}

	[Test]
	public void ATranscript_SaysEveryMoveButAForcedPass_ThenHowItEnded()
	{
		var (record, last) = PlayRecorded(seed: 4);
		Assert.That(last.IsOver(), "precondition");

		var lines = record.Transcript();

		Assert.That(lines, Has.Count.EqualTo(record.Moves.Count(m => m.Kind != "rule") + 1));
		Assert.That(lines, Has.One.Contains("[sandbox] Put Last Gasp into Hand"));
		Assert.That(
			lines[^1],
			Is.EqualTo(
				$"{last.GetPlayer(last.WinnerId()).Name} won on turn {last.GetGame().TurnNumber}."
			)
		);
		Assert.That(record.Transcript(passes: true), Has.Count.EqualTo(record.Moves.Count + 1));
	}

	[Test]
	public void ATranscriptsMoveNumber_IsThePositionBeforeThatMove()
	{
		var (record, _) = PlayRecorded(seed: 4);
		var line = record.Transcript().Single(l => l.Contains("[sandbox]"));
		var at = int.Parse(line[1..line.IndexOf(' ')]);

		Assert.That(record.Moves[at - 1].Kind, Is.EqualTo("sandbox"));
		Assert.That(MoveTokens.Offers(record.Before(at)), Is.EqualTo(record.Moves[at - 1].Offers));
		Assert.Throws<ArgumentOutOfRangeException>(() => record.Before(record.Moves.Count + 1));
	}

	/// <summary>
	/// <c>Records/Sample.json</c>: a headless table game (GreedyAi, seed 5, the stand-in to turn 6),
	/// kept with <c>--keep=Sample --at=110</c> — so the embedding is checked, and the test that printed
	/// (its control: GreedyAi still casts Samut there) runs. Stale with the engine like any kept record.
	/// </summary>
	[Test]
	public void TheSampleKeptRecord_IsEmbedded_AndItsPrintedControlHolds()
	{
		Assert.That(KeptRecords.Names, Does.Contain("Sample"), "the csproj embeds Records/*.json");
		var s = KeptRecords.Before("Sample", 110);
		var choice = MoveTokens.Token(s, new GreedyAi(s.DecidingPlayerId()).Decide(s));

		Assert.That(MoveTokens.Same(choice, KeptRecords.Get("Sample").Moves[109].Token));
	}

	/// <summary>A kept record (an AI test case, TBL-5.5) the engine no longer replays fails HERE by name.</summary>
	[Test]
	public void EveryKeptRecord_StillReplays()
	{
		foreach (var name in KeptRecords.Names)
			Assert.DoesNotThrow(
				() => KeptRecords.Get(name).Replay(),
				$"{name}: stale — keep it again from a new game, or drop it with its test"
			);
	}

	[Test]
	public void ARecord_SurvivesItsFile()
	{
		var (record, _) = PlayRecorded(seed: 5);
		var read = GameRecord.FromJson(record.ToJson());
		Assert.That(read.ToJson(), Is.EqualTo(record.ToJson()));
		Assert.That(
			MoveTokens.Ending(read.Replay().Last),
			Is.EqualTo(MoveTokens.Ending(record.Replay().Last))
		);
	}

	[Test]
	public void APositionThatOffersSomethingElse_IsStale_NamingTheMove()
	{
		var (record, _) = PlayRecorded(seed: 6);
		var broken = record with
		{
			Moves = record.Moves.SetItem(10, record.Moves[10] with { Offers = "00000000" }),
		};
		var e = Assert.Throws<MoveTokens.StaleException>(() => broken.Replay());
		Assert.That(e!.Message, Does.StartWith("move 11"));
	}

	[Test]
	public void AGameThatEndsDifferently_IsStale() =>
		Assert.Throws<MoveTokens.StaleException>(
			() => (PlayRecorded(seed: 7).Record with { End = "00000000" }).Replay()
		);

	[Test]
	public void ATokenThatWontParse_IsStale_NotACrash()
	{
		var s = PlayRecorded(seed: 9).Record.Start();
		foreach (var token in new[] { "t41", "s5:Hand", "s5:Nowhere:Last Gasp", "x", "a999" })
			Assert.Throws<MoveTokens.StaleException>(() => MoveTokens.Apply(s, token), token);
	}

	[Test]
	public void ADeckCardThatNoLongerExists_IsStale_NotACrash()
	{
		var record = PlayRecorded(seed: 9).Record;
		Assert.Throws<MoveTokens.StaleException>(
			() => (record with { Deck2 = record.Deck2.SetItem(0, "No Such Card") }).Replay()
		);
	}

	// Into EITHER seat, whoever holds priority — so the description names whose zone.
	[Test]
	public void ASandboxMove_SaysWhatItPut_AndWhere()
	{
		var s = PlayRecorded(seed: 9).Record.Start();
		var token = MoveTokens.Token(
			s,
			new SandboxPutAction(s.Player2Id(), FraCards.Get("Last Gasp"), ZoneType.Hand),
			null
		);
		Assert.That(
			MoveTokens.Describe(s, token),
			Is.EqualTo($"Put Last Gasp into Hand ({s.GetPlayer(s.Player2Id()).Name})")
		);
	}

	[Test]
	public void AnUndo_Truncates_AndTheRecordReplaysToThatPosition()
	{
		var (record, _) = PlayRecorded(seed: 8);
		var undone = record.Truncate(100);
		Assert.That(undone.Moves, Has.Count.EqualTo(100));
		Assert.That(MoveTokens.Offers(undone.Replay().Last), Is.EqualTo(record.Moves[100].Offers));
	}
}
