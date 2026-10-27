using System.Collections.Generic;
using System.Linq;
using MagicCore.Cards;
using MagicCore.Sets.Fra;
using MtgGame;

namespace MagicTable;

/// <summary>
/// Sealed deck building, the Arena way (story LIM-1): the pool's cards in the middle, filtered by
/// colour — click one into the deck; the deck as a list on the right — click a row back out; free
/// basics; the suggested decks (the AI's builds of your pool: two colours, a splash, a greedy splash —
/// LIM-3). Start plays it on the table against the AI's own build of ITS pool, a fresh one each Start.
/// Your pool is opened from a seed, or pasted from Arena. Every rule is <see cref="SealedSession"/>'s;
/// this only draws it. Debug args (<c>-GameArgs</c>): <c>--seed=N</c> opens that pool first; <c>--pool=FILE</c> an Arena pool export.
/// </summary>
public partial class SealedScene : Control
{
	private const float TileWidth = 150;
	private static readonly string[] Filters =
	[
		"All",
		"W",
		"U",
		"B",
		"R",
		"G",
		"Multi",
		"Colourless",
		"Lands",
	];

	/// <summary>The event in progress — kept when the table returns here, replaced by New pool.</summary>
	private static SealedSession? _session;

	private string _filter = "All";
	private readonly List<Button> _filterButtons = new();
	private readonly HFlowContainer _pool = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
	private readonly VBoxContainer _deckRows = new();
	private readonly Dictionary<string, Label> _basicCounts = new();
	private readonly Label _deckTitle = MtgUiStyles.Title("", 28);
	private readonly Label _title = MtgUiStyles.Title("", 34);
	private readonly Label _leftOut = Text("", 20);
	private readonly HFlowContainer _suggestRow = new();
	private readonly Label _pairTitle = Text("Close pairs — rating a card · spells at 1–3:", 16);
	private readonly HFlowContainer _pairRow = new();
	private readonly Label _suggested = Text("", 18);
	private readonly TextEdit _paste =
		new()
		{
			PlaceholderText = """
				In Arena's deck builder: Export, then paste here.

				Deck
				1 Card Name (FRA) 123
				…

				Sideboard
				…
				""",
			CustomMinimumSize = new Vector2(720, 440),
		};
	private readonly TextureRect _preview =
		new()
		{
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			CustomMinimumSize = new Vector2(375, 523),
		};
	private readonly LineEdit _seed =
		new() { PlaceholderText = "random", CustomMinimumSize = new Vector2(140, 0) };
	private readonly CheckBox _search =
		new() { Text = "Search AI (~1 s a move)", ButtonPressed = true };
	private readonly Button _start = new() { Text = "Start", FocusMode = FocusModeEnum.None };

	private SealedSession S => _session!;

	public override void _Ready()
	{
		// --pool=FILE: an Arena pool export, as if pasted (LIM-3.6's captures)
		if (
			_session is null
			&& OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--pool=")) is { } poolArg
		)
		{
			var file = poolArg["--pool=".Length..].Trim('"');
			var read = System.IO.File.Exists(file)
				? ArenaDeck.ReadPool(System.IO.File.ReadAllText(file))
				: null;
			if (read is { Pool.Count: > 0 })
				_session = new SealedSession(read.Pool, read.LeftOut);
			else
				GD.PushError(
					$"--pool={file}: no such file, or no FRA cards in it — a seed's pool instead"
				);
		}
		_session ??= new SealedSession(
			OS.GetCmdlineUserArgs()
				.FirstOrDefault(a => a.StartsWith("--seed="))
				?["--seed=".Length..]
				is { } arg
			&& int.TryParse(arg, out var seed)
				? seed
				: System.Random.Shared.Next(1, 1_000_000)
		);
		MtgUiStyles.AddBackdrop(this);
		var margin = new MarginContainer();
		margin.SetAnchorsPreset(LayoutPreset.FullRect);
		foreach (var side in new[] { "left", "right", "top", "bottom" })
			margin.AddThemeConstantOverride($"margin_{side}", 12);
		AddChild(margin);
		var columns = new HBoxContainer();
		columns.AddThemeConstantOverride("separation", 12);
		margin.AddChild(columns);

		columns.AddChild(LeftColumn());
		columns.AddChild(PoolColumn());
		columns.AddChild(DeckColumn());
		Redraw();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
			Project.GameManager.Instance.ChangeScene("res://MagicTable/DeckSelectScene.tscn");
	}

	private Control LeftColumn()
	{
		var left = new VBoxContainer { CustomMinimumSize = new Vector2(375, 0) };
		left.AddThemeConstantOverride("separation", 12);
		left.AddChild(_title);
		left.AddChild(_preview);

		var seedRow = new HBoxContainer();
		seedRow.AddThemeConstantOverride("separation", 8);
		seedRow.AddChild(Text("Seed"));
		_seed.AddThemeFontSizeOverride("font_size", 22);
		seedRow.AddChild(_seed);
		seedRow.AddChild(
			Choice(
				"New pool",
				() =>
				{
					_session = new SealedSession(
						int.TryParse(_seed.Text.Trim(), out var seed)
							? seed
							: System.Random.Shared.Next(1, 1_000_000)
					);
					Redraw();
				}
			)
		);
		left.AddChild(seedRow);

		var dialog = new AcceptDialog
		{
			Title = "Your Arena pool — the deck AND sideboard",
			OkButtonText = "Use this pool",
		};
		_paste.AddThemeFontSizeOverride("font_size", 18);
		dialog.AddChild(_paste);
		dialog.AddCancelButton("Cancel");
		dialog.Confirmed += UsePastedPool;
		AddChild(dialog);
		left.AddChild(Choice("Paste Arena pool", () => dialog.PopupCentered()));

		// every other pair within 1.5 points of the best, for Shayne to judge (LIM-3.6); a click adds
		// its builds to the suggestions. Here, not under them: the deck list needs that column's height
		left.AddChild(_pairTitle);
		_pairRow.AddThemeConstantOverride("h_separation", 6);
		_pairRow.AddThemeConstantOverride("v_separation", 6);
		left.AddChild(_pairRow);
		return left;
	}

	/// <summary>Your real pool (LIM-1.7). Nothing readable keeps the pool you had, and says why.</summary>
	private void UsePastedPool()
	{
		var read = ArenaDeck.ReadPool(_paste.Text);
		if (read.Pool.Count == 0)
		{
			_leftOut.Text = "No FRA cards found in the paste — the pool is unchanged.";
			_leftOut.Visible = true;
			return;
		}
		_session = new SealedSession(read.Pool, read.LeftOut);
		Redraw();
	}

	private Control PoolColumn()
	{
		var center = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		center.AddThemeConstantOverride("separation", 8);
		var filters = new HBoxContainer();
		filters.AddThemeConstantOverride("separation", 6);
		foreach (var filter in Filters)
		{
			var button = Choice(
				filter,
				() =>
				{
					_filter = filter;
					Redraw();
				}
			);
			_filterButtons.Add(button);
			filters.AddChild(button);
		}
		center.AddChild(filters);
		_leftOut.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		_leftOut.CustomMinimumSize = new Vector2(1, 0);
		center.AddChild(_leftOut);
		var scroll = new ScrollContainer
		{
			SizeFlagsVertical = SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
		};
		_pool.AddThemeConstantOverride("h_separation", 6);
		_pool.AddThemeConstantOverride("v_separation", 6);
		scroll.AddChild(_pool);
		center.AddChild(scroll);
		return center;
	}

	private Control DeckColumn()
	{
		var right = new VBoxContainer { CustomMinimumSize = new Vector2(400, 0) };
		right.AddThemeConstantOverride("separation", 8);
		right.AddChild(_deckTitle);
		var scroll = new ScrollContainer
		{
			SizeFlagsVertical = SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
		};
		_deckRows.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		scroll.AddChild(_deckRows);
		right.AddChild(scroll);

		foreach (var basic in SealedSession.BasicNames)
		{
			var row = new HBoxContainer();
			var name = Text(basic);
			name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			row.AddChild(name);
			row.AddChild(Choice("−", () => Basic(basic, -1)));
			var count = Text("0");
			count.CustomMinimumSize = new Vector2(44, 0);
			count.HorizontalAlignment = HorizontalAlignment.Center;
			_basicCounts[basic] = count;
			row.AddChild(count);
			row.AddChild(Choice("+", () => Basic(basic, +1)));
			right.AddChild(row);
		}

		// a row that wraps: a pool may offer up to three suggestions, and nothing runs off the screen
		_suggestRow.AddThemeConstantOverride("h_separation", 8);
		_suggestRow.AddThemeConstantOverride("v_separation", 8);
		right.AddChild(_suggestRow);
		_suggested.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		_suggested.CustomMinimumSize = new Vector2(1, 0);
		right.AddChild(_suggested);
		_search.AddThemeFontSizeOverride("font_size", 22);
		right.AddChild(_search);
		MtgUiStyles.StylePrimary(_start, 34);
		_start.Pressed += () =>
		{
			// never your pool's own seed: that would open your pool for the AI too
			var gameSeed = System.Random.Shared.Next(1, 1_000_000);
			if (gameSeed == S.Seed)
				gameSeed++;
			TableSetup.Current = S.ToSetup(_search.ButtonPressed, gameSeed);
			Project.GameManager.Instance.ChangeScene("res://MagicTable/MagicTableScene.tscn");
		};
		right.AddChild(_start);
		return right;
	}

	private void Basic(string basic, int delta) =>
		Act(() => S.SetBasics(basic, S.Basics(basic) + delta));

	private void Act(System.Action act)
	{
		act();
		Redraw();
	}

	private void Redraw()
	{
		_title.Text = S.Seed is { } seed ? $"Sealed · seed {seed}" : "Sealed · your pool";
		_leftOut.Text =
			S.LeftOut.Count == 0
				? ""
				: "Left out (not Reality Fracture cards, or unreadable): "
					+ string.Join(
						", ",
						S.LeftOut.GroupBy(c => c)
							.Select(g => g.Count() > 1 ? $"{g.Count()}× {g.Key}" : g.Key)
					);
		_leftOut.Visible = _leftOut.Text.Length > 0;
		foreach (var button in _filterButtons)
			MtgUiStyles.StyleChoice(button, selected: button.Text == _filter);

		CardViews.Clear(_pool);
		foreach (var card in S.Sideboard.Where(Shows).OrderBy(Order).ThenBy(c => c))
			_pool.AddChild(Tile(card));

		CardViews.Clear(_deckRows);
		foreach (
			var group in S
				.FromPool.GroupBy(c => c)
				.OrderBy(g => Data(g.Key).TypeLine.Contains("Land")) // lands last, as on Arena
				.ThenBy(g => Data(g.Key).Cmc)
				.ThenBy(g => g.Key)
		)
			_deckRows.AddChild(DeckRow(group.Key, group.Count()));

		foreach (var (basic, label) in _basicCounts)
			label.Text = S.Basics(basic).ToString();
		CardViews.Clear(_suggestRow);
		for (var i = 0; i < S.Suggestions.Count; i++)
		{
			var which = i;
			var button = Choice(S.Suggestions[i].Button, () => Act(() => S.Suggest(which)));
			button.TooltipText = S.Suggestions[i].Label + "\n" + S.OnTimeText(i);
			_suggestRow.AddChild(button);
		}
		_suggestRow.AddChild(Choice("Clear", () => Act(S.Clear)));
		CardViews.Clear(_pairRow);
		for (var i = 0; i < S.ClosePairs.Count; i++)
		{
			var which = i;
			var button = Choice(
				SealedSession.PairText(S.ClosePairs[i]),
				() => Act(() => S.ShowPair(which))
			);
			button.CustomMinimumSize = new Vector2(0, 32);
			button.AddThemeFontSizeOverride("font_size", 16);
			MtgUiStyles.StyleChoice(button, selected: S.ShownPair == i);
			_pairRow.AddChild(button);
		}
		_pairTitle.Visible = S.ClosePairs.Count > 0;
		_suggested.Text = S.SuggestedIndex is { } from
			? $"Started from: {S.Suggestions[from].Label} — as suggested, {S.OnTimeText(from)}"
			: "";
		_suggested.Visible = _suggested.Text.Length > 0;

		var lands = S.Deck.Count(c => Data(c).TypeLine.Contains("Land"));
		_deckTitle.Text = $"Deck  {S.Count - lands} + {lands} lands = {S.Count}";
		_start.Disabled = !S.Ready;
	}

	/// <summary>A card of the pool: the full card; hover previews it, a click puts it in the deck.</summary>
	private Control Tile(string card)
	{
		var tile = new TextureRect
		{
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, // before Texture (godot-frontend.md)
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			Texture = MagicArt.Full(card),
			CustomMinimumSize = new Vector2(TileWidth, TileWidth * 1.395f),
			TooltipText = SealedSession.Playable(card)
				? ""
				: "Not playable yet — its ability isn't scripted",
		};
		if (!SealedSession.Playable(card))
			tile.Modulate = new Color(0.45f, 0.45f, 0.45f);
		CardViews.Hook(tile, card, Preview, () => Act(() => S.Add(card)));
		return tile;
	}

	/// <summary>
	/// A deck row: count, cost, name; hover previews, a click takes one copy back out. ponytail: the
	/// cost is its printed text ("{1}{W/U}") — draw pips (a RichTextLabel with the mana icons) if it
	/// reads poorly.
	/// </summary>
	private Control DeckRow(string card, int copies)
	{
		var row = new PanelContainer();
		row.AddThemeStyleboxOverride("panel", MtgUiStyles.ButtonNormal());
		var label = Text(
			$"{copies}×  {Data(card).ManaCost?.Split(" // ")[0]}  {card.Split(" // ")[0]}",
			20
		);
		label.MouseFilter = MouseFilterEnum.Ignore;
		// a long row (Karn's five twobrid pips) must not widen the column: clip it, the preview reads it
		label.ClipText = true;
		label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
		label.CustomMinimumSize = new Vector2(1, 0);
		row.AddChild(label);
		CardViews.Hook(row, card, Preview, () => Act(() => S.Remove(card)));
		return row;
	}

	private void Preview(string? card)
	{
		if (card is not null)
			_preview.Texture = MagicArt.Full(card);
	}

	private bool Shows(string card)
	{
		var data = Data(card);
		var land = data.TypeLine.Contains("Land");
		var colors = data.Colors ?? [];
		return _filter switch
		{
			"All" => true,
			"Lands" => land,
			"Colourless" => !land && colors.Length == 0,
			"Multi" => colors.Length > 1,
			_ => colors.Length == 1 && colors[0] == _filter,
		};
	}

	/// <summary>Arena's pool order: white, blue, black, red, green, multicolour, colourless, lands — then cost.</summary>
	private static (int, double) Order(string card)
	{
		var data = Data(card);
		var colors = data.Colors ?? [];
		var group =
			data.TypeLine.Contains("Land") ? 7
			: colors.Length > 1 ? 5
			: colors.Length == 0 ? 6
			: "WUBRG".IndexOf(colors[0], System.StringComparison.Ordinal);
		return (group, data.Cmc);
	}

	private static CardData Data(string card) => FraCards.Get(card).Data;

	private static Button Choice(string text, System.Action pressed)
	{
		var button = new Button
		{
			Text = text,
			FocusMode = FocusModeEnum.None,
			CustomMinimumSize = new Vector2(0, 44),
		};
		button.AddThemeFontSizeOverride("font_size", 22);
		MtgUiStyles.StyleChoice(button, selected: false);
		button.Pressed += pressed;
		return button;
	}

	private static Label Text(string text, int size = 22)
	{
		var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.Off };
		label.AddThemeFontSizeOverride("font_size", size);
		label.AddThemeColorOverride("font_color", new Color("f2e6c8"));
		return label;
	}
}
