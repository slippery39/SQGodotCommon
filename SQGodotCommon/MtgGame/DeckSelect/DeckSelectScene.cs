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

	private static readonly Color SelectedBg = new(0.45f, 0.34f, 0.06f, 1f);
	private static readonly Color SelectedBorder = new(1f, 0.85f, 0.30f, 1f);

	public override void _Ready()
	{
		var bg = new ColorRect { Color = MtgUiStyles.DarkBg };
		bg.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(bg);

		var center = new CenterContainer();
		center.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(center);

		var root = new VBoxContainer();
		root.AddThemeConstantOverride("separation", 32);
		center.AddChild(root);

		var title = new Label { Text = "Choose Your Decks" };
		title.HorizontalAlignment = HorizontalAlignment.Center;
		title.AddThemeFontSizeOverride("font_size", 48);
		title.Modulate = MtgUiStyles.GoldBorder;
		root.AddChild(title);

		var columns = new HBoxContainer();
		columns.AddThemeConstantOverride("separation", 48);
		root.AddChild(columns);

		columns.AddChild(BuildColumn("Your Deck", isPlayer: true));

		var sep = new VSeparator();
		sep.CustomMinimumSize = new Vector2(2, 0);
		columns.AddChild(sep);

		columns.AddChild(BuildColumn("Opponent's Deck", isPlayer: false));

		var startCenter = new CenterContainer();
		root.AddChild(startCenter);

		_startButton = new Button { Text = "Start Battle", Disabled = true };
		_startButton.CustomMinimumSize = new Vector2(300, 56);
		_startButton.AddThemeFontSizeOverride("font_size", 22);
		ApplyButtonStyle(_startButton, selected: false);
		_startButton.Pressed += OnStartBattle;
		startCenter.AddChild(_startButton);
	}

	private VBoxContainer BuildColumn(string heading, bool isPlayer)
	{
		var col = new VBoxContainer();
		col.AddThemeConstantOverride("separation", 12);
		col.CustomMinimumSize = new Vector2(290, 0);

		var label = new Label { Text = heading };
		label.HorizontalAlignment = HorizontalAlignment.Center;
		label.AddThemeFontSizeOverride("font_size", 22);
		label.Modulate = MtgUiStyles.GoldBorder;
		col.AddChild(label);

		var buttonList = isPlayer ? _playerButtons : _opponentButtons;

		foreach (var deck in DeckRegistry.All)
		{
			var choice = new DeckChoice.Premade(deck.Name);
			AddDeckOption(col, buttonList, deck.Name, DeckTagline(deck.Name), choice, isPlayer);
		}

		AddDeckOption(
			col,
			buttonList,
			"Random Premade",
			"Mystery deck — revealed at game start",
			new DeckChoice.RandomPremade(),
			isPlayer
		);

		AddDeckOption(
			col,
			buttonList,
			"Randomized",
			"40 random cards from the full card pool",
			new DeckChoice.Randomized(),
			isPlayer
		);

		return col;
	}

	private void AddDeckOption(
		VBoxContainer parent,
		List<Button> buttonList,
		string name,
		string tagline,
		DeckChoice choice,
		bool isPlayer
	)
	{
		var btn = new Button { Text = name };
		btn.CustomMinimumSize = new Vector2(290, 44);
		btn.AddThemeFontSizeOverride("font_size", 18);
		ApplyButtonStyle(btn, selected: false);

		btn.Pressed += () =>
		{
			if (isPlayer)
			{
				_playerChoice = choice;
				HighlightSelection(_playerButtons, btn);
			}
			else
			{
				_opponentChoice = choice;
				HighlightSelection(_opponentButtons, btn);
			}
			UpdateStartButton();
		};

		buttonList.Add(btn);
		parent.AddChild(btn);

		if (tagline.Length > 0)
		{
			var sub = new Label { Text = tagline };
			sub.HorizontalAlignment = HorizontalAlignment.Center;
			sub.AddThemeFontSizeOverride("font_size", 11);
			sub.Modulate = new Color(0.7f, 0.7f, 0.7f, 1f);
			parent.AddChild(sub);
		}
	}

	private static void HighlightSelection(List<Button> buttons, Button selected)
	{
		foreach (var b in buttons)
			ApplyButtonStyle(b, selected: b == selected);
	}

	private void UpdateStartButton()
	{
		var ready = _playerChoice != null && _opponentChoice != null;
		_startButton.Disabled = !ready;
		ApplyButtonStyle(_startButton, selected: ready);
	}

	private void OnStartBattle()
	{
		if (_playerChoice == null || _opponentChoice == null)
			return;

		var setup = new DeckSetupData(_playerChoice, _opponentChoice);
		GameManager.Instance.RegisterService(setup);
		GameManager.Instance.ChangeScene("res://MtgGame/MtgGameScene.tscn");
	}

	private static void ApplyButtonStyle(Button btn, bool selected)
	{
		if (selected)
		{
			var s = new StyleBoxFlat();
			s.BgColor = SelectedBg;
			s.SetBorderWidthAll(2);
			s.BorderColor = SelectedBorder;
			s.CornerRadiusTopLeft =
				s.CornerRadiusTopRight =
				s.CornerRadiusBottomLeft =
				s.CornerRadiusBottomRight =
					4;
			btn.AddThemeStyleboxOverride("normal", s);
			btn.AddThemeStyleboxOverride("hover", s);
			btn.AddThemeStyleboxOverride("pressed", s);
			btn.AddThemeStyleboxOverride("disabled", MtgUiStyles.ButtonDisabled());
		}
		else
		{
			btn.AddThemeStyleboxOverride("normal", MtgUiStyles.ButtonNormal());
			btn.AddThemeStyleboxOverride("hover", MtgUiStyles.ButtonHover());
			btn.AddThemeStyleboxOverride("pressed", MtgUiStyles.ButtonNormal());
			btn.AddThemeStyleboxOverride("disabled", MtgUiStyles.ButtonDisabled());
		}
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
