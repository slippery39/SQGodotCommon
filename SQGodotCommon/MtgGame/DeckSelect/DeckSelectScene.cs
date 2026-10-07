using System.Collections.Generic;
using MtgSimulator;
using Project;

namespace MtgGame;

public partial class DeckSelectScene : Control
{
	private DeckChoice? _playerChoice;
	private DeckChoice? _opponentChoice;

	private readonly List<Button> _playerButtons = new();
	private readonly List<Button> _opponentButtons = new();
	private Button _startButton = null!;

	private Label _playerTagline = null!;
	private Label _opponentTagline = null!;

	/// Deck buttons per row in each side's grid. One column of fourteen pushed Start Battle off
	/// the bottom of a 720p window — the screen could not start a game.
	private const int GridColumns = 2;

	public override void _Ready()
	{
		MtgUiStyles.AddBackdrop(this);

		var root = new VBoxContainer();
		root.SetAnchorsPreset(LayoutPreset.FullRect);
		root.OffsetTop = 24;
		root.AddThemeConstantOverride("separation", 20);
		AddChild(root);

		root.AddChild(MtgUiStyles.Title("Choose Your Decks"));

		var columns = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		columns.AddThemeConstantOverride("separation", 72);
		root.AddChild(columns);
		columns.AddChild(BuildColumn("Your Deck", isPlayer: true));
		columns.AddChild(BuildColumn("Opponent's Deck", isPlayer: false));

		// Pinned bottom-right, where END TURN lives on the board: never pushed off by the list.
		_startButton = new Button { Text = "Start Battle", Disabled = true };
		_startButton.SetAnchorsPreset(LayoutPreset.BottomRight);
		_startButton.OffsetLeft = -360;
		_startButton.OffsetTop = -110;
		_startButton.OffsetRight = -24;
		_startButton.OffsetBottom = -30;
		MtgUiStyles.StylePrimary(_startButton, fontSize: 32);
		_startButton.Pressed += OnStartBattle;
		AddChild(_startButton);
	}

	private VBoxContainer BuildColumn(string heading, bool isPlayer)
	{
		var col = new VBoxContainer();
		col.AddThemeConstantOverride("separation", 12);

		col.AddChild(MtgUiStyles.Title(heading, 30));

		var grid = new GridContainer { Columns = GridColumns };
		grid.AddThemeConstantOverride("h_separation", 10);
		grid.AddThemeConstantOverride("v_separation", 10);
		col.AddChild(grid);

		var tagline = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			CustomMinimumSize = new Vector2(1, 56),
		};
		tagline.AddThemeFontSizeOverride("font_size", 22);
		tagline.AddThemeColorOverride("font_color", new Color("f2e6c8"));
		col.AddChild(tagline);
		if (isPlayer)
			_playerTagline = tagline;
		else
			_opponentTagline = tagline;

		var buttonList = isPlayer ? _playerButtons : _opponentButtons;
		foreach (var deck in DeckRegistry.All)
			AddDeckOption(
				grid,
				buttonList,
				deck.Name,
				DeckTagline(deck.Name),
				new DeckChoice.Premade(deck.Name),
				isPlayer
			);
		AddDeckOption(
			grid,
			buttonList,
			"Random Premade",
			"Mystery deck — revealed at game start",
			new DeckChoice.RandomPremade(),
			isPlayer
		);
		AddDeckOption(
			grid,
			buttonList,
			"Randomized",
			"40 random cards from the full card pool",
			new DeckChoice.Randomized(),
			isPlayer
		);
		return col;
	}

	private void AddDeckOption(
		GridContainer parent,
		List<Button> buttonList,
		string name,
		string tagline,
		DeckChoice choice,
		bool isPlayer
	)
	{
		var btn = new Button { Text = name, CustomMinimumSize = new Vector2(250, 52) };
		btn.AddThemeFontSizeOverride("font_size", 22);
		MtgUiStyles.StyleChoice(btn, selected: false);

		btn.Pressed += () =>
		{
			if (isPlayer)
			{
				_playerChoice = choice;
				HighlightSelection(_playerButtons, btn);
				_playerTagline.Text = tagline;
			}
			else
			{
				_opponentChoice = choice;
				HighlightSelection(_opponentButtons, btn);
				_opponentTagline.Text = tagline;
			}
			UpdateStartButton();
		};

		buttonList.Add(btn);
		parent.AddChild(btn);
	}

	private static void HighlightSelection(List<Button> buttons, Button selected)
	{
		foreach (var b in buttons)
			MtgUiStyles.StyleChoice(b, selected: b == selected);
	}

	private void UpdateStartButton() =>
		_startButton.Disabled = _playerChoice == null || _opponentChoice == null;

	private void OnStartBattle()
	{
		if (_playerChoice == null || _opponentChoice == null)
			return;

		var setup = new DeckSetupData(_playerChoice, _opponentChoice);
		GameManager.Instance.RegisterService(setup);
		GameManager.Instance.ChangeScene("res://MtgGame/MtgGameScene.tscn");
	}

	private static string DeckTagline(string deckName) =>
		deckName switch
		{
			"Zoo" => "RGW aggro — fast creatures + burn",
			"Goblins" => "Red tribal — haste, lords, token flood",
			"Dragonstorm" => "Combo — ritual ramp into Dragonstorm",
			_ => string.Empty,
		};
}
