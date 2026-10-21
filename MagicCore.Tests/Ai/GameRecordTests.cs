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
	public void ASandboxMove_SaysWhatItPut()
	{
		var s = PlayRecorded(seed: 9).Record.Start();
		var token = MoveTokens.Token(
			s,
			new SandboxPutAction(s.Player1Id(), FraCards.Get("Last Gasp"), ZoneType.Hand),
			null
		);
		Assert.That(MoveTokens.Describe(s, token), Is.EqualTo("[sandbox] Last Gasp into Hand"));
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
