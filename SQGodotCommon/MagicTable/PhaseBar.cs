using System;
using MagicCore;
using MtgGame;

namespace MagicTable;

/// <summary>
/// Arena's phase ladder: the active player's steps, the current one lit (TBL-4.2: one row, not
/// two — the other player's waits behind "Stops"). Each step is also a stop toggle — clicking it
/// flips whether you get priority there; a stop shows as a gold underline. "Stops" shows both
/// players' rows, to set the stops of the turn that isn't on.
/// </summary>
public partial class PhaseBar : VBoxContainer
{
	private static readonly (Step Step, string Label)[] Steps =
	{
		(Step.Upkeep, "Upkeep"),
		(Step.Draw, "Draw"),
		(Step.PreCombatMain, "Main 1"),
		(Step.BeginCombat, "Combat"),
		(Step.DeclareAttackers, "Attack"),
		(Step.DeclareBlockers, "Block"),
		(Step.CombatDamage, "Damage"),
		(Step.EndCombat, "End Combat"),
		(Step.PostCombatMain, "Main 2"),
		(Step.End, "End"),
	};

	/// <summary>Raised with the new stops when a toggle is clicked.</summary>
	public event Action<Stops>? StopsChanged;

	/// <summary>Raised when "Stops" adds or removes a row — the table redraws, since the board is sized around this bar.</summary>
	public event Action? RowsChanged;

	public PhaseBar()
	{
		AddThemeConstantOverride("separation", 1);
	}

	private bool _bothRows;

	public void Show(MagicTableManager m)
	{
		CardViews.Clear(this);
		var game = m.State.GetGame();
		var activeIsMine = game.ActivePlayerId == m.HumanId;
		foreach (var mine in new[] { false, true })
		{
			var active = activeIsMine == mine;
			if (!active && !_bothRows)
				continue;
			var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
			row.AddThemeConstantOverride("separation", 2);
			AddChild(row);
			row.AddChild(Label(m.State.GetPlayer(mine ? m.HumanId : m.OpponentId).Name, 60));
			foreach (var (step, label) in Steps)
			{
				var current =
					active
					&& (
						game.Step == step
						|| (step == Step.CombatDamage && game.Step == Step.FirstStrikeDamage)
					);
				row.AddChild(Toggle(m.Stops, mine, step, label, current));
			}
			if (active)
				row.AddChild(StopsButton(m));
		}
	}

	private Button StopsButton(MagicTableManager m)
	{
		var button = new Button
		{
			Text = "Stops",
			ToggleMode = true,
			ButtonPressed = _bothRows,
			FocusMode = FocusModeEnum.None,
			TooltipText = "Show both players' steps, to set where you stop in either turn",
		};
		button.AddThemeFontSizeOverride("font_size", 18);
		MtgUiStyles.StyleChoice(button, selected: _bothRows);
		button.Toggled += on =>
		{
			_bothRows = on;
			RowsChanged?.Invoke();
		};
		return button;
	}

	private Button Toggle(Stops stops, bool mine, Step step, string label, bool current)
	{
		var set = mine ? stops.Mine : stops.Theirs;
		var on = set.Contains(step);
		var button = new Button
		{
			Text = label,
			CustomMinimumSize = new Vector2(86, 33),
			TooltipText =
				$"{(on ? "Stop" : "No stop")} in {(mine ? "your" : "the AI's")} {label} — click to toggle",
			FocusMode = FocusModeEnum.None,
		};
		button.AddThemeFontSizeOverride("font_size", 18);
		var style = new StyleBoxFlat
		{
			BgColor = current ? new Color("946536") : new Color(0.1f, 0.06f, 0.03f, 0.75f),
			BorderColor = MtgUiStyles.GoldBorder,
			BorderWidthBottom = on ? 3 : 0,
		};
		style.SetCornerRadiusAll(3);
		foreach (var state in new[] { "normal", "hover", "pressed" })
			button.AddThemeStyleboxOverride(state, style);
		button.AddThemeColorOverride("font_color", current ? Colors.White : new Color("c8ae7f"));
		button.Pressed += () =>
		{
			var next = on ? set.Remove(step) : set.Add(step);
			StopsChanged?.Invoke(mine ? stops with { Mine = next } : stops with { Theirs = next });
		};
		return button;
	}

	private static Label Label(string text, int width)
	{
		var label = new Label { Text = text, CustomMinimumSize = new Vector2(width, 0) };
		label.AddThemeFontSizeOverride("font_size", 20);
		label.AddThemeColorOverride("font_color", new Color("f4e29b"));
		return label;
	}
}
