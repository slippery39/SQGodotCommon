using System;
using System.Linq;
using System.Threading.Tasks;
using MtgSimulator;
using Project;

namespace MtgGame;

/// <summary>
/// Standings between rounds of a drafted round-robin. Owns no tournament state — that lives on
/// the <see cref="DraftTournament"/> service, because this scene is torn down every time the
/// human goes off to play their match.
/// </summary>
public partial class TournamentScene : Control
{
	private DraftTournament _tournament = null!;
	private VBoxContainer _root = null!;
	private Label _status = null!;
	private VBoxContainer _table = null!;
	private Button _action = null!;
	private Label _hint = null!;
	private DeckListPopup _deckList = null!;

	public override void _Ready()
	{
		_tournament = GameManager.Instance.GetService<DraftTournament>();
		BuildUi();
		_ = ResolvePendingRound();
	}

	/// <summary>
	/// The AI games for the round the human just played were started before they left this
	/// scene, so in practice they finished minutes ago and this returns immediately. The status
	/// line exists for the case where the human conceded in ten seconds.
	/// </summary>
	private async Task ResolvePendingRound()
	{
		try
		{
			if (_tournament.HasPendingRound)
			{
				_status.Text = "Simulating the other tables…";
				_action.Disabled = true;
				await _tournament.CompletePendingRoundAsync();
				_tournament.AdvanceRound();
			}
			Render();
		}
		catch (Exception e)
		{
			// An exception inside the simulation would otherwise vanish into an unobserved task
			// and leave the button dead with no explanation.
			GD.PushError($"Tournament round simulation failed: {e}");
			_status.Text = "Round simulation failed — see the Godot output log.";
		}
	}

	// ===== UI =====

	private void BuildUi()
	{
		var bg = new ColorRect { Color = MtgUiStyles.DarkBg };
		bg.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(bg);

		var center = new CenterContainer();
		center.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(center);

		_root = new VBoxContainer();
		_root.AddThemeConstantOverride("separation", 16);
		_root.CustomMinimumSize = new Vector2(560, 0);
		center.AddChild(_root);

		var title = new Label { Text = "Draft Standings" };
		title.HorizontalAlignment = HorizontalAlignment.Center;
		title.AddThemeFontSizeOverride("font_size", 44);
		title.Modulate = MtgUiStyles.GoldBorder;
		_root.AddChild(title);

		_status = new Label();
		_status.HorizontalAlignment = HorizontalAlignment.Center;
		_status.AddThemeFontSizeOverride("font_size", 16);
		_root.AddChild(_status);

		var panel = new PanelContainer();
		panel.AddThemeStyleboxOverride("panel", MtgUiStyles.DarkPanel(borderWidth: 2));
		_root.AddChild(panel);

		var inner = new MarginContainer();
		foreach (var side in new[] { "left", "right", "top", "bottom" })
			inner.AddThemeConstantOverride($"margin_{side}", 14);
		panel.AddChild(inner);

		_table = new VBoxContainer();
		_table.AddThemeConstantOverride("separation", 4);
		inner.AddChild(_table);

		_hint = new Label { Visible = false };
		_hint.HorizontalAlignment = HorizontalAlignment.Center;
		_hint.AddThemeFontSizeOverride("font_size", 13);
		_hint.Modulate = new Color(0.6f, 0.6f, 0.65f, 1f);
		_root.AddChild(_hint);

		var buttonRow = new CenterContainer();
		_root.AddChild(buttonRow);

		_deckList = new DeckListPopup();
		AddChild(_deckList);

		_action = new Button();
		_action.CustomMinimumSize = new Vector2(360, 52);
		_action.AddThemeFontSizeOverride("font_size", 20);
		_action.AddThemeStyleboxOverride("normal", MtgUiStyles.ButtonNormal());
		_action.AddThemeStyleboxOverride("hover", MtgUiStyles.ButtonHover());
		_action.AddThemeStyleboxOverride("pressed", MtgUiStyles.ButtonNormal());
		_action.AddThemeStyleboxOverride("disabled", MtgUiStyles.ButtonDisabled());
		buttonRow.AddChild(_action);
	}

	private void Render()
	{
		RenderTable();

		if (_tournament.IsComplete)
			RenderFinished();
		else
			RenderNextRound();
	}

	private void RenderTable()
	{
		foreach (var child in _table.GetChildren())
			child.QueueFree();

		AddRow("#", "Drafter", "W-L-D", "Win%", isHeader: true);

		var standings = _tournament.Standings();
		for (var rank = 0; rank < standings.Count; rank++)
		{
			var s = standings[rank];
			var name = s.IsHuman ? "You" : $"Bot {s.Seat}";
			var row = AddRow(
				$"{rank + 1}",
				name,
				$"{s.Wins}-{s.Losses}-{s.Draws}",
				s.Played == 0 ? "—" : $"{s.WinRate * 100:F0}%",
				isHeader: false,
				highlight: s.IsHuman
			);

			// Decklists open only once the event is over — mid-tournament they would be scouting
			// an opponent you have not played yet.
			if (!_tournament.IsComplete)
				continue;

			var seat = s.Seat;
			row.MouseFilter = MouseFilterEnum.Stop;
			row.MouseDefaultCursorShape = CursorShape.PointingHand;
			row.GuiInput += e =>
			{
				if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
					_deckList.ShowPool(name, _tournament.Pool(seat));
			};
		}
	}

	private HBoxContainer AddRow(
		string rank,
		string name,
		string record,
		string winRate,
		bool isHeader,
		bool highlight = false
	)
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 12);
		_table.AddChild(row);

		var widths = new[] { 40, 260, 110, 80 };
		var cells = new[] { rank, name, record, winRate };
		for (var i = 0; i < cells.Length; i++)
		{
			var label = new Label { Text = cells[i] };
			label.CustomMinimumSize = new Vector2(widths[i], 0);
			label.AddThemeFontSizeOverride("font_size", isHeader ? 14 : 18);
			label.Modulate =
				isHeader ? new Color(0.6f, 0.6f, 0.65f, 1f)
				: highlight ? MtgUiStyles.GoldBorder
				: Colors.White;
			// Labels must not eat the click, or the row never sees it.
			label.MouseFilter = MouseFilterEnum.Ignore;
			row.AddChild(label);
		}

		return row;
	}

	private void RenderNextRound()
	{
		var round = _tournament.Round;
		var (opponent, _) = _tournament.HumanPairing(round);

		_status.Text = $"Round {round + 1} of {_tournament.RoundCount}";
		_action.Disabled = false;
		_action.Text = $"Play Round {round + 1} — vs Bot {opponent}";
		_action.Pressed += () => StartHumanMatch(round, opponent);
	}

	private void StartHumanMatch(int round, int opponent)
	{
		// Started before leaving so the other tables resolve while the human plays.
		_tournament.StartRoundSim(round);

		GameManager.Instance.RegisterService(
			new DeckSetupData(
				new DeckChoice.Drafted(_tournament.Pool(_tournament.HumanSeat)),
				new DeckChoice.Drafted(_tournament.Pool(opponent))
			)
		);
		GameManager.Instance.ChangeScene("res://MtgGame/MtgGameScene.tscn");
	}

	private void RenderFinished()
	{
		var champion = _tournament.Standings()[0];
		_status.Text = champion.IsHuman
			? $"You win the draft at {champion.Wins}-{champion.Losses}!"
			: $"Bot {champion.Seat} wins the draft at {champion.Wins}-{champion.Losses}.";
		_status.AddThemeFontSizeOverride("font_size", 26);
		_status.Modulate = MtgUiStyles.GoldBorder;

		_hint.Text = "Click any drafter to see the deck they built";
		_hint.Visible = true;

		_action.Disabled = false;
		_action.Text = "Back to Main Menu";
		_action.Pressed += () =>
		{
			// Dropping the service matters: a stale tournament would otherwise send the next
			// ordinary game's game-over screen back to these standings.
			GameManager.Instance.RemoveService<DraftTournament>();
			GameManager.Instance.GoToMainMenu();
		};
	}
}
