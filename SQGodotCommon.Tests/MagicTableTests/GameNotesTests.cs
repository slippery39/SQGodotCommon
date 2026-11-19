using System;
using System.IO;
using System.Linq;
using MagicCore;
using MagicCore.Ai;
using MagicCore.Sets.Fra;
using MagicTable;
using NUnit.Framework;

namespace SQGodotCommon.Tests;

public class GameNotesTests
{
	/// <summary>
	/// TBL-10.3: notes go beside the record, one header then each note — and the move a note names reopens THAT
	/// position: the record cut to it and resumed is where the note was written. Without that a note points nowhere.
	/// A note on a position the table still shows, behind the game, lists no move after it.
	/// </summary>
	[Test]
	public void ANote_IsWrittenBesideTheRecord_AndItsMoveReopensThePosition()
	{
		var dir = Path.Combine(Path.GetTempPath(), $"notes-{Guid.NewGuid():N}");
		try
		{
			var m = new MagicTableManager(
				FraDecks.All["White-Black"],
				FraDecks.All["Red-Green"],
				1,
				aiVsAi: true,
				search: false
			)
			{
				RecordTo = dir,
			};
			for (var i = 0; i < 60 && !m.IsOver; i++)
				m.StepAi();

			var path = GameNotes.Write(m, "First: why did it hold that card?");
			var moves = m.Record.Moves.Count;
			var named = m
				.State.CardsIn(m.HumanId, ZoneType.Hand)
				.Concat(m.State.CardsIn(m.HumanId, ZoneType.Battlefield))
				.First()
				.Name;
			var position = MoveTokens.Offers(m.State) + MoveTokens.Ending(m.State);
			m.StepAi();
			GameNotes.Write(m, "Second note");

			Assert.That(path, Is.EqualTo(Path.ChangeExtension(m.RecordFile!, ".notes.md")));
			var text = File.ReadAllText(path!);
			Assert.Multiple(() =>
			{
				Assert.That(text.Split("# Notes").Length, Is.EqualTo(2), "one header");
				Assert.That(text, Does.Contain("First: why did it hold that card?"));
				Assert.That(text, Does.Contain("Second note"));
				Assert.That(text, Does.Contain($"'--resume-at={moves}'"));
				Assert.That(text, Does.Contain($"## Move {moves} "));
				Assert.That(text, Does.Contain(named), "the cards, by name");
			});

			// A note on a position still on screen while the game ran ahead: its move, its lines, nothing after it.
			var k = moves - 10;
			GameNotes.Write(m, "Behind", m.Record.Before(k + 1), k);
			var behind = File.ReadAllText(path!).Split("## Move")[^1];
			Assert.That(behind, Does.StartWith($" {k} ").And.Contain("Behind"));
			Assert.That(behind, Does.Not.Contain($"- #{k + 1} "), "no move after the position");

			var record = GameRecord.FromJson(File.ReadAllText(m.RecordFile!)).Truncate(moves);
			var reopened = MagicTableManager.Resume(record, search: false);
			Assert.That(
				MoveTokens.Offers(reopened.State) + MoveTokens.Ending(reopened.State),
				Is.EqualTo(position)
			);
		}
		finally
		{
			if (Directory.Exists(dir))
				Directory.Delete(dir, recursive: true);
		}
	}

	[Test]
	public void AGameWithNoRecord_SavesNoNote() =>
		Assert.That(
			GameNotes.Write(
				new MagicTableManager(
					FraDecks.All["White-Black"],
					FraDecks.All["Red-Green"],
					1,
					aiVsAi: true,
					search: false
				),
				"lost"
			),
			Is.Null
		);
}
