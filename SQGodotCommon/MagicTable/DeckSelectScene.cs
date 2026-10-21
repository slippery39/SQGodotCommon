using System.Collections.Generic;
using System.Linq;
using MagicCore.Cards;
using MagicCore.Sets.Fra;
using MtgGame;

namespace MagicTable;

/// <summary>What the deck select hands the table: plain data, read by the table as it starts.</summary>
public sealed record TableSetup(
	IReadOnlyList<CardDefinition> Mine,
	IReadOnlyList<CardDefinition> Theirs,
	int? Seed,
	bool AiVsAi,
	bool Search
)
{
	/// <summary>The last setup chosen — New game and Replay keep it.</summary>
	public static TableSetup? Current { get; set; }

	/// <summary>Each seat's cards outside the game — sealed's unplayed pool (Extrapolate the Impossible).</summary>
	public IReadOnlyList<CardDefinition> MySideboard { get; init; } = [];

	public IReadOnlyList<CardDefinition> TheirSideboard { get; init; } = [];

	/// <summary>Where Escape from the table goes: the screen the game was set up on.</summary>
	public string Back { get; init; } = "res://MagicTable/DeckSelectScene.tscn";
}

/// <summary>
/// Pick both seats' decks — a built-in FRA deck, or an Arena export pasted in (what you'd
/// queue with) — a seed to replay, and whether the AI plays both seats. Start is off until both
/// decks read cleanly; every problem is named.
/// </summary>
public partial class DeckSelectScene : Control
{
	private const string Paste = "Paste an Arena deck";
	private const string MenuScene = "res://Project/MainMenu/main_menu.tscn";
	private readonly Seat _mine = new("You", "White-Black");
	private readonly Seat _theirs = new("AI", "Red-Green");
	private readonly LineEdit _seed =
		new() { PlaceholderText = "random", CustomMinimumSize = new Vector2(240, 0) };
	private readonly CheckBox _aiVsAi = new() { Text = "AI plays both seats (watch)" };
	private readonly CheckBox _search =
		new() { Text = "Search AI (stronger, ~1 s a move)", ButtonPressed = true };
	private readonly Button _start = new() { Text = "Start", FocusMode = FocusModeEnum.None };

	public override void _Ready()
	{
		MtgUiStyles.AddBackdrop(this);
		var margin = new MarginContainer();
		margin.SetAnchorsPreset(LayoutPreset.FullRect);
		foreach (var side in new[] { "left", "right", "top", "bottom" })
			margin.AddThemeConstantOverride($"margin_{side}", 60);
		AddChild(margin);
		var column = new VBoxContainer();
		column.AddThemeConstantOverride("separation", 24);
		margin.AddChild(column);
		column.AddChild(MtgUiStyles.Title("Reality Fracture — Playtest", 60));

		var seats = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
		seats.AddThemeConstantOverride("separation", 40);
		foreach (var seat in new[] { _mine, _theirs })
		{
			seat.Changed += Validate;
			seats.AddChild(seat.Build());
		}
		column.AddChild(seats);

		var bottom = new HBoxContainer();
		bottom.AddThemeConstantOverride("separation", 24);
		var seedLabel = Text("Seed");
		seedLabel.AutowrapMode = TextServer.AutowrapMode.Off; // in an HBox a wrapping label shrinks to one letter a line
		bottom.AddChild(seedLabel);
		_seed.AddThemeFontSizeOverride("font_size", 24);
		bottom.AddChild(_seed);
		_aiVsAi.AddThemeFontSizeOverride("font_size", 24);
		bottom.AddChild(_aiVsAi);
		_search.AddThemeFontSizeOverride("font_size", 24);
		bottom.AddChild(_search);
		bottom.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
		var sealedButton = new Button { Text = "Sealed", FocusMode = FocusModeEnum.None };
		MtgUiStyles.StylePrimary(sealedButton, 34);
		sealedButton.CustomMinimumSize = new Vector2(240, 0);
		sealedButton.Pressed += () =>
			Project.GameManager.Instance.ChangeScene("res://MagicTable/SealedScene.tscn");
		bottom.AddChild(sealedButton);
		MtgUiStyles.StylePrimary(_start, 34);
		_start.CustomMinimumSize = new Vector2(300, 0);
		_start.Pressed += Start;
		bottom.AddChild(_start);
		column.AddChild(bottom);
		Validate();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
			Project.GameManager.Instance.ChangeScene(MenuScene);
	}

	private void Validate() => _start.Disabled = _mine.Deck is null || _theirs.Deck is null;

	private void Start()
	{
		TableSetup.Current = new TableSetup(
			_mine.Deck!,
			_theirs.Deck!,
			int.TryParse(_seed.Text.Trim(), out var seed) ? seed : null,
			_aiVsAi.ButtonPressed,
			_search.ButtonPressed
		);
		Project.GameManager.Instance.ChangeScene("res://MagicTable/MagicTableScene.tscn");
	}

	private static Label Text(string text, int size = 24)
	{
		var label = new Label
		{
			Text = text,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			CustomMinimumSize = new Vector2(1, 0),
		};
		label.AddThemeFontSizeOverride("font_size", size);
		label.AddThemeColorOverride("font_color", new Color("f2e6c8"));
		return label;
	}

	/// <summary>One seat: the built-in decks and "paste an Arena deck", with what was read.</summary>
	private sealed class Seat(string who, string initial)
	{
		private string _choice = initial;
		private readonly List<Button> _buttons = new();
		private readonly TextEdit _paste =
			new()
			{
				PlaceholderText = "Deck\n4 Last Gasp (FRA) 96\n17 Swamp (FRA) 283\n…",
				CustomMinimumSize = new Vector2(0, 260),
				Visible = false,
			};
		private readonly Label _report = Text("");

		public event System.Action? Changed;

		/// <summary>The chosen deck; null while a paste doesn't read.</summary>
		public IReadOnlyList<CardDefinition>? Deck { get; private set; }

		public Control Build()
		{
			var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
			column.AddThemeConstantOverride("separation", 10);
			column.AddChild(MtgUiStyles.Title(who, 40));
			foreach (var name in FraDecks.All.Keys.Append(Paste))
			{
				var button = new Button
				{
					Text = name,
					FocusMode = FocusModeEnum.None,
					CustomMinimumSize = new Vector2(0, 54),
				};
				button.AddThemeFontSizeOverride("font_size", 24);
				button.Pressed += () => Choose(name);
				_buttons.Add(button);
				column.AddChild(button);
			}
			_paste.AddThemeFontSizeOverride("font_size", 20);
			_paste.TextChanged += Read;
			column.AddChild(_paste);
			column.AddChild(_report);
			Choose(_choice);
			return column;
		}

		private void Choose(string name)
		{
			_choice = name;
			foreach (var button in _buttons)
				MtgUiStyles.StyleChoice(button, selected: button.Text == name);
			_paste.Visible = name == Paste;
			Read();
		}

		private void Read()
		{
			if (_choice != Paste)
			{
				Deck = FraDecks.All[_choice];
				_report.Text = $"{Deck.Count} cards";
			}
			else
			{
				var result = ArenaDeck.Parse(_paste.Text);
				Deck = result.Ok ? result.Deck : null;
				_report.Text = result.Ok
					? $"{result.Deck.Count} cards read"
					: string.Join("\n", result.Problems);
			}
			Changed?.Invoke();
		}
	}
}
