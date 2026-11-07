using System.Collections.Generic;
using System.IO;
using System.Linq;
using MagicCore.Ai;
using MagicCore.Cards;
using MagicCore.Sets;
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

	/// <summary>A recorded game to resume (TBL-5.7): the table makes its moves again, once.</summary>
	public GameRecord? Resume { get; init; }

	/// <summary>Where Escape from the table goes: the screen the game was set up on.</summary>
	public string Back { get; init; } = "res://MagicTable/DeckSelectScene.tscn";

	/// <summary>
	/// A game of a run (a draft's, LIM-2.3): told once, as the game ends, whether you won — and the
	/// table then offers only the way back, so a game can't be replayed into the run.
	/// </summary>
	public System.Action<bool>? Finished { get; init; }
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

	/// <summary>
	/// The set Sealed and Draft open (MC-5.4) — chosen here, kept while the game runs. <c>--set=TRK</c> on
	/// the command line starts on it (captures of the Sealed and Draft screens run them directly).
	/// </summary>
	public static string LimitedSet { get; private set; } =
		OS.GetCmdlineUserArgs()
			.FirstOrDefault(a => a.StartsWith("--set="))
			?["--set=".Length..].ToUpperInvariant()
			is { } set
		&& SealedSession.Sets.Any(s => s.Code == set)
			? set
			: SealedSession.Sets[0].Code;

	private static string SetText() =>
		$"Set: {SealedSession.Sets.First(s => s.Code == LimitedSet).Name}";

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
		column.AddChild(bottom);
		// The buttons get their own row: beside the options, the row outgrew the screen (2026-10-23).
		var actions = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
		actions.AddThemeConstantOverride("separation", 24);
		var resumeButton = new Button { Text = "Resume a game", FocusMode = FocusModeEnum.None };
		MtgUiStyles.StylePrimary(resumeButton, 34);
		resumeButton.Pressed += ChooseRecord;
		actions.AddChild(resumeButton);
		// one button that steps through the sets: a popup list's rect is in window pixels (magic-table rules)
		var setButton = new Button { Text = SetText(), FocusMode = FocusModeEnum.None };
		MtgUiStyles.StylePrimary(setButton, 34);
		setButton.Pressed += () =>
		{
			var i = SealedSession.Sets.ToList().FindIndex(s => s.Code == LimitedSet);
			LimitedSet = SealedSession.Sets[(i + 1) % SealedSession.Sets.Count].Code;
			setButton.Text = SetText();
		};
		actions.AddChild(setButton);
		var sealedButton = new Button { Text = "Sealed", FocusMode = FocusModeEnum.None };
		MtgUiStyles.StylePrimary(sealedButton, 34);
		sealedButton.CustomMinimumSize = new Vector2(240, 0);
		sealedButton.Pressed += () =>
		{
			SealedScene.OpenSealed();
			Project.GameManager.Instance.ChangeScene("res://MagicTable/SealedScene.tscn");
		};
		actions.AddChild(sealedButton);
		var draftButton = new Button { Text = "Draft", FocusMode = FocusModeEnum.None };
		MtgUiStyles.StylePrimary(draftButton, 34);
		draftButton.CustomMinimumSize = new Vector2(240, 0);
		draftButton.Pressed += () =>
			Project.GameManager.Instance.ChangeScene("res://MagicTable/DraftScene.tscn");
		actions.AddChild(draftButton);
		MtgUiStyles.StylePrimary(_start, 34);
		_start.CustomMinimumSize = new Vector2(300, 0);
		_start.Pressed += Start;
		actions.AddChild(_start);
		column.AddChild(actions);
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

	/// <summary>Pick a file from game_records/ — every table game is recorded there (TBL-5).</summary>
	private void ChooseRecord()
	{
		var dialog = new FileDialog
		{
			FileMode = FileDialog.FileModeEnum.OpenFile,
			Access = FileDialog.AccessEnum.Filesystem,
			CurrentDir = MagicTableScene.RecordsDir,
			Filters = ["*.json ; Game records"],
			Title = "Resume a recorded game — then Undo back to the play you want to see again",
		};
		dialog.FileSelected += Resume;
		dialog.VisibilityChanged += () =>
		{
			if (!dialog.Visible)
				dialog.QueueFree();
		};
		AddChild(dialog);
		dialog.PopupCentered(new Vector2I(1100, 700));
	}

	private void Resume(string path)
	{
		try
		{
			var record = GameRecord.FromJson(File.ReadAllText(path));
			record.CheckFormat();
			TableSetup.Current = new TableSetup(
				[.. record.Deck1.Select(CardRegistry.Get)],
				[.. record.Deck2.Select(CardRegistry.Get)],
				record.Seed,
				record.About.GetValueOrDefault("Player1") != "human",
				_search.ButtonPressed
			)
			{
				MySideboard = [.. record.Sideboard1.Select(CardRegistry.Get)],
				TheirSideboard = [.. record.Sideboard2.Select(CardRegistry.Get)],
				Resume = record,
			};
			Project.GameManager.Instance.ChangeScene("res://MagicTable/MagicTableScene.tscn");
		}
		catch (System.Exception e)
			when (e
					is IOException
						or System.UnauthorizedAccessException
						or System.Text.Json.JsonException
						or KeyNotFoundException
						or MoveTokens.StaleException
			)
		{
			var problem = new AcceptDialog
			{
				Title = "Can't resume",
				DialogText =
					e is KeyNotFoundException
						? $"A card in it no longer exists ({e.Message})"
						: e.Message,
			};
			problem.VisibilityChanged += () =>
			{
				if (!problem.Visible)
					problem.QueueFree();
			};
			AddChild(problem);
			problem.PopupCentered();
		}
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
