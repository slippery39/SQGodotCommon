using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ImmutableGameObjects;
using MagicCore;
using MagicCore.Actions;
using MagicCore.Cards;

namespace MagicTable;

/// <summary>
/// Shayne's notes on a game, for Claude to analyse later (TBL-10.3; Shayne, 2026-11-13: "so I can write notes for you
/// to analyze later on"). Each note is appended to <c>&lt;record&gt;.notes.md</c> beside the game's record: his text,
/// the exact position (the record and its move count — reopened with <c>--resume-at</c>), the board as names, the
/// last log lines, and the AI's last search. Markdown, so either of us can read it; the record itself is untouched
/// (Claude's call, Shayne's leave: Undo cuts a record short and it is rewritten every move). Plain C#. Tested.
/// </summary>
public static class GameNotes
{
	/// <summary>Appends a note at the game's current position; returns the notes file, or null when the game keeps no record.</summary>
	public static string? Write(MagicTableManager m, string text, DateTime? at = null)
	{
		if (m.RecordFile is not { } record)
			return null;
		var path = Path.ChangeExtension(record, ".notes.md");
		var fresh = !File.Exists(path);
		using var file = File.AppendText(path);
		if (fresh)
			file.Write(Header(m, record));
		file.Write(Note(m, record, text, at ?? DateTime.Now));
		return path;
	}

	private static string Header(MagicTableManager m, string record) =>
		$"# Notes — {Path.GetFileName(record)}\n\n"
		+ $"Seed {m.Seed} · {m.State.GetPlayer(m.HumanId).Name} (seat 1) v {m.State.GetPlayer(m.OpponentId).Name}"
		+ $"{(m.AiVsAi ? " · AI vs AI" : "")}\n";

	private static string Note(MagicTableManager m, string record, string text, DateTime at)
	{
		var s = m.State;
		var game = s.GetGame();
		var moves = m.Record.Moves.Count;
		var lines = new List<string>
		{
			"",
			$"## Move {moves} · turn {game.TurnNumber}, {s.GetPlayer(game.ActivePlayerId).Name}'s {game.Step} · {at:yyyy-MM-dd HH:mm}",
			"",
			text.Trim(),
			"",
			$"Reopen: `./Run-Godot.ps1 MagicTable/MagicTableScene.tscn -GameArgs '--resume={record}','--resume-at={moves}','--show-hand'`",
			"",
		};
		foreach (var player in new[] { m.OpponentId, m.HumanId })
			lines.AddRange(Side(s, player));
		var stack = s.GetChildren(s.GetWellKnownId(MagicKeys.Stack)).ToList();
		if (stack.Count > 0)
			lines.Add(
				"- stack (top first): "
					+ string.Join(
						"; ",
						stack.Select(o =>
							$"{(o is StackAbility a ? $"{a.Source.Name} — ability" : o.Name)} ({s.GetPlayer(s.StackObjectControllerId(o.Id)).Name})"
						)
					)
			);
		lines.Add("");
		lines.Add("Last moves:");
		lines.AddRange(m.Log.TakeLast(8).Select(l => $"- {l}"));
		if (m.LastSearch is { } search)
		{
			lines.Add("");
			lines.Add(
				$"The AI's last search (turn {search.Turn}, {search.Who} chose {search.Chose}):"
			);
			lines.AddRange(
				search.Ranked.Take(6).Select((r, i) => $"{i + 1}. {r.Move} — {r.Score:0.00}")
			);
		}
		return string.Join("\n", lines) + "\n";
	}

	/// <summary>A player's side as names: life and counts, the hand, the battlefield (stats, tapped, counters).</summary>
	private static IEnumerable<string> Side(GameState s, int player)
	{
		var p = s.GetPlayer(player);
		string Count(ZoneType z) => $"{s.CardsIn(player, z).Count()}";
		yield return $"**{p.Name}** — life {p.Life}, library {Count(ZoneType.Library)}, graveyard {Count(ZoneType.Graveyard)}, exile {Count(ZoneType.Exile)}";
		yield return $"- hand: {Names(s.CardsIn(player, ZoneType.Hand).Select(c => c.Name))}";
		yield return $"- battlefield: {Names(s.CardsIn(player, ZoneType.Battlefield).Select(c => Permanent(s, c)))}";
	}

	private static string Permanent(GameState s, Card c)
	{
		var parts = new List<string>();
		if (c.Is(CardType.Creature))
			parts.Add($"{s.Power(c)}/{s.Toughness(c) - c.Damage}");
		if (c.Is(CardType.Planeswalker))
			parts.Add($"loyalty {c.Loyalty}");
		if (c.PlusOneCounters > 0)
			parts.Add($"+1/+1 ×{c.PlusOneCounters}");
		if (c.IsTapped)
			parts.Add("tapped");
		if (c.IsToken)
			parts.Add("token");
		return parts.Count == 0 ? c.Name : $"{c.Name} ({string.Join(", ", parts)})";
	}

	/// <summary>Names in order, a repeat counted: "Swamp ×2, Rank Rat".</summary>
	private static string Names(IEnumerable<string> names) =>
		names.ToList() is { Count: > 0 } all
			? string.Join(
				", ",
				all.GroupBy(n => n).Select(g => g.Count() > 1 ? $"{g.Key} ×{g.Count()}" : g.Key)
			)
			: "—";
}
