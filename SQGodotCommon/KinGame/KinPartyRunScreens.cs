using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using KinCore;
using KinCore.Party;

namespace KinGame;

/// <summary>
/// **THE RUN's screens, over the battle board** (KinJam.md "THE RUN"): choose a starter, between
/// battles (HP, who revived, who joined, the rest, a reward), and the run's end. Presentation only —
/// every fact comes from `PartyRun` and its `RunReport`.
///
/// **Built of Buttons, and the board hides the hand while it shows.** Card hover is physics picking
/// and the fan's cards carry their own ZIndex, so a hand left visible draws over any overlay.
/// </summary>
public sealed class KinPartyRunScreens
{
	private readonly ColorRect _root;
	private readonly VBoxContainer _column;

	public bool IsShowing => _root.Visible;

	/// <summary>What each starter WANTS from the board — the one line that tells them apart.</summary>
	private static readonly Dictionary<string, string> Wants =
		new()
		{
			["Bramble"] = "Wants to be HIT",
			["Pike"] = "Wants never to be where the hit lands",
			["Gale"] = "Wants the FOES where it chooses",
		};

	public KinPartyRunScreens(Node parent)
	{
		_root = new ColorRect { Color = new Color(KinPalette.Navy, 0.96f), Visible = false };
		_root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_root.MouseFilter = Control.MouseFilterEnum.Stop; // nothing under it takes a click
		parent.AddChild(_root);

		var centre = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		centre.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_root.AddChild(centre);

		_column = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		_column.AddThemeConstantOverride("separation", 18);
		centre.AddChild(_column);
	}

	public void Hide() => _root.Visible = false;

	// ===== The three screens

	public void ShowStarters(Action<PartyCompanion> choose)
	{
		Begin("CHOOSE YOUR STARTER", "The other two join after your first two wins.");

		var row = Row();
		foreach (var companion in PartyContent.Roster)
			row.AddChild(
				Tile(
					KinPalette.Companion(companion.Name),
					KinArt.Drawing(companion.Name),
					companion.Name.ToUpperInvariant(),
					[
						Wants.GetValueOrDefault(companion.Name, ""),
						companion.Passive,
						companion.PassiveRule,
						$"HP {companion.Hp} · POW {companion.Power} · SPD {companion.Speed}",
					],
					new Vector2(320, 500),
					() => choose(companion)
				)
			);
	}

	public void ShowBetween(
		PartyRun run,
		RunReport report,
		string beaten,
		Action<RewardCard> take,
		Action skip
	)
	{
		Begin(
			$"VICTORY — {beaten.ToUpperInvariant()}",
			$"Next: battle {run.Battle + 1} of {run.Encounters.Count}, {run.Next.Name}"
		);

		var news = new List<string>();
		foreach (var name in report.Revived)
			news.Add($"{name} was knocked out, and is back at a quarter HP.");
		if (report.Joined is { } joined)
			news.Add($"{joined} joins the team!");
		if (report.Rested)
			news.Add(
				$"The team rests before the last battle: +{(int)(PartyRun.RestHeal * 100)}% HP."
			);
		foreach (var line in news)
			_column.AddChild(Label(line, 24, KinPalette.Gold));

		_column.AddChild(
			Label(
				string.Join(
					"     ",
					run.Team.Select(m =>
						$"{m.Companion.Name.ToUpperInvariant()} {m.Hp}/{m.Companion.Hp}"
					)
				),
				24,
				KinPalette.Bone
			)
		);

		_column.AddChild(Label("TAKE A CARD", 28, KinPalette.Bone));
		var row = Row();
		foreach (var reward in run.RewardOffer())
			row.AddChild(
				Tile(
					KinPalette.Companion(reward.Companion),
					KinArt.Drawing(reward.Companion),
					$"{reward.Card.Name.ToUpperInvariant()}  ({reward.Card.Cost})",
					[
						string.Join(" ", KinRulesText.Lines(reward.Card)),
						$"for {reward.Companion.ToUpperInvariant()}",
					],
					new Vector2(280, 380),
					() => take(reward)
				)
			);

		var buttons = Row();
		buttons.AddChild(Button("SKIP", skip));
	}

	public void ShowOver(PartyRun run, Action newRun, Action menu)
	{
		Begin(
			run.IsWon ? "THE RUN IS WON" : "DEFEAT",
			run.IsWon
				? $"All {run.Encounters.Count} battles, and the Old Tusker with them."
				: $"The team fell in battle {run.Battle + 1} of {run.Encounters.Count}: {run.Next.Name}."
		);

		var buttons = Row();
		buttons.AddChild(Button("NEW RUN", newRun));
		buttons.AddChild(Button("MENU", menu));
	}

	// ===== Pieces

	private void Begin(string title, string subtitle)
	{
		foreach (var child in _column.GetChildren())
			child.QueueFree();

		_column.AddChild(Label(title, 48, KinPalette.Bone));
		_column.AddChild(Label(subtitle, 22, new Color(KinPalette.Bone, 0.8f)));
		_root.Visible = true;
	}

	private HBoxContainer Row()
	{
		var row = new HBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		row.AddThemeConstantOverride("separation", 24);
		_column.AddChild(row);
		return row;
	}

	private static Label Label(string text, int size, Color colour)
	{
		var label = KinPalette.Text(text, size, colour);
		label.MouseFilter = Control.MouseFilterEnum.Ignore;
		return label;
	}

	private static Button Button(string text, Action pressed)
	{
		var button = new Button { Text = text, CustomMinimumSize = new Vector2(200, 56) };
		button.AddThemeFontSizeOverride("font_size", 24);
		button.Pressed += pressed;
		return button;
	}

	/// <summary>
	/// A big clickable tile: the companion's colour, its portrait, a title and a few lines. A Button
	/// with ignoring children, so the whole tile takes the click.
	/// </summary>
	private static Button Tile(
		Color fill,
		Texture2D art,
		string title,
		string[] lines,
		Vector2 size,
		Action pressed
	)
	{
		var tile = new Button { CustomMinimumSize = size };
		tile.AddThemeStyleboxOverride("normal", KinPalette.Box(fill, KinPalette.Bone, 2));
		tile.AddThemeStyleboxOverride(
			"hover",
			KinPalette.Box(fill.Lightened(0.12f), KinPalette.Gold, 5)
		);
		tile.AddThemeStyleboxOverride(
			"pressed",
			KinPalette.Box(fill.Darkened(0.1f), KinPalette.Gold, 5)
		);
		tile.AddThemeStyleboxOverride("focus", KinPalette.Box(fill, KinPalette.Gold, 5));
		tile.Pressed += pressed;

		var column = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		column.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		column.OffsetLeft = 12;
		column.OffsetRight = -12;
		column.OffsetTop = 10;
		column.OffsetBottom = -10;
		column.AddThemeConstantOverride("separation", 6);
		tile.AddChild(column);

		// Every line wraps, the title included: an unwrapped label's minimum width is its whole line,
		// and "WHIRLING STRIKE (2)" dragged the column wider than the tile.
		var heading = Label(title, 26, KinPalette.Bone);
		heading.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		heading.CustomMinimumSize = new Vector2(1, 0);
		column.AddChild(heading);
		column.AddChild(
			new TextureRect
			{
				Texture = art,
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				CustomMinimumSize = new Vector2(0, size.Y * 0.4f),
				MouseFilter = Control.MouseFilterEnum.Ignore,
			}
		);
		foreach (var line in lines.Where(l => l.Length > 0))
		{
			var label = Label(line, 20, KinPalette.Bone);
			label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			label.CustomMinimumSize = new Vector2(1, 0);
			column.AddChild(label);
		}

		return tile;
	}
}
