using System.Collections.Generic;
using System.IO;
using System.Linq;
using MagicCore.Cards;
using MagicCore.Sets;
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
	private static readonly string[] Rarities = ["Common", "Uncommon", "Rare", "Mythic"];
	private static readonly string[] ColorFilters = ["W", "U", "B", "R", "G", "Colourless"]; // toggled together, as Arena's (LIM-5.2)

	/// <summary>The event in progress — kept when the table returns here, replaced by New pool.</summary>
	private static SealedSession? _session;

	/// <summary>Where a pasted pool was saved for the console's <c>--compare</c> (LIM-3.4); null for a seed's pool.</summary>
	private static string? _poolFile;

	/// <summary>A draft's deck building (LIM-2.3): its picks are the pool, and Start plays its run's next bot.</summary>
	private static DraftSession? _draft;

	/// <summary>The drafted pool, built here — the same screen as sealed (Shayne, 2026-10-29).</summary>
	public static void OpenDraft(DraftSession draft)
	{
		_draft = draft;
		_session = draft.Builder;
		_poolFile = null;
	}

	/// <summary>Sealed again, from the deck select: a draft's pool is never a sealed pool, nor another set's.</summary>
	public static void OpenSealed()
	{
		if (_draft is null && _session?.Set == DeckSelectScene.LimitedSet)
			return;
		(_draft, _session, _poolFile) = (null, null, null);
	}

	private readonly Label _simNote = Text("", 16);

	// The pool's filters (LIM-1.10): any of the picked colours, Colourless among them (several at once), a kind
	// that excludes colours (multicolour, lands), and any of the picked rarities; all must hold.
	private readonly HashSet<string> _colors = new();
	private string _kind = "All";
	private readonly HashSet<string> _rarities = new();

	// LIM-4: the pool narrowed by what you type — name, type line, rules text, keywords. The deck list is never filtered.
	private readonly LineEdit _query =
		new()
		{
			PlaceholderText = "Search name or text…",
			ClearButtonEnabled = true,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
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
				? ArenaDeck.ReadPool(System.IO.File.ReadAllText(file), DeckSelectScene.LimitedSet)
				: null;
			if (read is { Pool.Count: > 0 })
			{
				_session = new SealedSession(read.Pool, read.LeftOut, DeckSelectScene.LimitedSet);
				_poolFile = Path.GetFullPath(file);
			}
			else
				GD.PushError(
					$"--pool={file}: no such file, or no {DeckSelectScene.LimitedSet} cards in it — a seed's pool instead"
				);
		}
		_session ??= new SealedSession(
			OS.GetCmdlineUserArgs()
				.FirstOrDefault(a => a.StartsWith("--seed="))
				?["--seed=".Length..]
				is { } arg
			&& int.TryParse(arg, out var seed)
				? seed
				: System.Random.Shared.Next(1, 1_000_000),
			DeckSelectScene.LimitedSet
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
							: System.Random.Shared.Next(1, 1_000_000),
						S.Set
					);
					Redraw();
				}
			)
		);
		left.AddChild(seedRow);
		seedRow.Visible = _draft is null; // a draft's pool is its picks
		var newDraft = Choice(
			"New draft",
			() =>
			{
				DraftScene.Reset();
				Project.GameManager.Instance.ChangeScene("res://MagicTable/DraftScene.tscn");
			}
		);
		newDraft.Visible = _draft is not null;
		left.AddChild(newDraft);

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
		var paste = Choice("Paste Arena pool", () => dialog.PopupCentered());
		paste.Visible = _draft is null;
		left.AddChild(paste);

		// every build offered here, compared by AI-vs-AI sims in the console (LIM-3.4) — hours, so not here
		var sim = Choice("Copy sim command", CopySimCommand);
		sim.Visible = _draft is null; // --compare reads a seed or a file; a draft's pool is neither
		left.AddChild(sim);
		_simNote.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		_simNote.CustomMinimumSize = new Vector2(1, 0);
		_simNote.Visible = false;
		left.AddChild(_simNote);

		// every other pair within 1.5 points of the best, for Shayne to judge (LIM-3.6); a click adds
		// its builds to the suggestions. Here, not under them: the deck list needs that column's height.
		// It scrolls: a taller column pushes Start off the screen (nine pairs did, before the cap of 4)
		left.AddChild(_pairTitle);
		_pairRow.AddThemeConstantOverride("h_separation", 6);
		_pairRow.AddThemeConstantOverride("v_separation", 6);
		_pairRow.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		var pairs = new ScrollContainer
		{
			SizeFlagsVertical = SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
		};
		pairs.AddChild(_pairRow);
		left.AddChild(pairs);
		return left;
	}

	/// <summary>Your real pool (LIM-1.7). Nothing readable keeps the pool you had, and says why.</summary>
	private void UsePastedPool()
	{
		var read = ArenaDeck.ReadPool(_paste.Text, S.Set);
		if (read.Pool.Count == 0)
		{
			_leftOut.Text = $"No {S.Set} cards found in the paste — the pool is unchanged.";
			_leftOut.Visible = true;
			return;
		}
		// saved for the console's --compare, beside game_records/ (untracked) — before the session
		// changes, so the file named is always this pool's
		string? file = null;
		try
		{
			var dir = Path.GetFullPath(ProjectSettings.GlobalizePath("res://../sealed_pools"));
			Directory.CreateDirectory(dir);
			file = Path.Combine(dir, $"{System.DateTime.Now:yyyy-MM-dd_HHmmss}.txt");
			File.WriteAllText(file, _paste.Text);
		}
		catch (IOException e)
		{
			GD.PushError($"sealed_pools/: {e.Message}");
			file = null;
		}
		_session = new SealedSession(read.Pool, read.LeftOut, S.Set);
		_poolFile = file;
		_simNote.Visible = false;
		Redraw();
	}

	/// <summary>The console command that compares every build offered for this pool, onto the clipboard.</summary>
	private void CopySimCommand()
	{
		if (S.Seed is null && _poolFile is null)
			_simNote.Text =
				"This pasted pool couldn't be saved to sealed_pools/ — no command to copy.";
		else
		{
			var (command, note) = S.SimCommand(_poolFile);
			DisplayServer.ClipboardSet(command);
			_simNote.Text = $"Copied — run it from the solution root. {note}";
		}
		_simNote.Visible = true;
	}

	private Control PoolColumn()
	{
		var center = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		center.AddThemeConstantOverride("separation", 8);
		var filters = new HBoxContainer();
		filters.AddThemeConstantOverride("separation", 6);
		foreach (var filter in Filters)
		{
			var button = Choice(filter, () => PickFilter(filter));
			_filterButtons.Add(button);
			filters.AddChild(button);
		}
		center.AddChild(filters);
		var rarities = new HBoxContainer();
		rarities.AddThemeConstantOverride("separation", 6);
		foreach (var rarity in Rarities)
		{
			var button = Choice(
				rarity,
				() =>
				{
					if (!_rarities.Remove(rarity))
						_rarities.Add(rarity);
					Redraw();
				}
			);
			_filterButtons.Add(button);
			rarities.AddChild(button);
		}
		_query.AddThemeFontSizeOverride("font_size", 22);
		_query.AddThemeStyleboxOverride("normal", MtgUiStyles.DarkPanel()); // the theme draws a LineEdit with no box
		_query.TextChanged += _ => Redraw();
		rarities.AddChild(_query);
		center.AddChild(rarities);
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
			TableSetup.Current =
				_draft?.ToSetup(_search.ButtonPressed, gameSeed)
				?? S.ToSetup(_search.ButtonPressed, gameSeed);
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
		_title.Text =
			_draft is not null ? $"Draft {S.Set} · seed {_draft.Seed}\n{_draft.RunText}" // two lines: one widened the column
			: S.Seed is { } seed ? $"Sealed {S.Set} · seed {seed}"
			: $"Sealed {S.Set} · your pool";
		_leftOut.Text =
			S.LeftOut.Count == 0
				? ""
				: $"Left out (not {SealedSession.Sets.First(x => x.Code == S.Set).Name} cards, or unreadable): "
					+ string.Join(
						", ",
						S.LeftOut.GroupBy(c => c)
							.Select(g => g.Count() > 1 ? $"{g.Count()}× {g.Key}" : g.Key)
					);
		_leftOut.Visible = _leftOut.Text.Length > 0;
		foreach (var button in _filterButtons)
			MtgUiStyles.StyleChoice(button, selected: FilterOn(button.Text));

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
		_start.Disabled = !S.Ready || _draft is { RunOver: true };
	}

	/// <summary>A card of the pool: the full card; hover previews it, a click puts it in the deck.</summary>
	private Control Tile(string card)
	{
		var tile = new TextureRect
		{
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, // before Texture (godot-frontend.md)
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			Texture = MagicArt.Full(MagicArt.Key(card, S.Set)),
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

	/// <summary>A deck row: count, name, cost; hover previews, a click takes one copy back out.</summary>
	private Control DeckRow(string card, int copies)
	{
		var row = CardRow(card, $"{copies}×  ", 20);
		CardViews.Hook(row, card, Preview, () => Act(() => S.Remove(card)));
		return row;
	}

	/// <summary>
	/// A card as a list row, as Arena's: the name, then its cost in mana symbols at the right (LIM-5.1) — the
	/// deck list and the draft's picks. The name clips, never the cost: a long row must not widen the column.
	/// </summary>
	internal static PanelContainer CardRow(string card, string prefix, int size)
	{
		var row = new PanelContainer();
		row.AddThemeStyleboxOverride("panel", MtgUiStyles.ButtonNormal());
		var line = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
		line.AddThemeConstantOverride("separation", 8);
		var name = Text($"{prefix}{card.Split(" // ")[0]}", size);
		name.MouseFilter = MouseFilterEnum.Ignore;
		name.ClipText = true;
		name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
		name.CustomMinimumSize = new Vector2(1, 0);
		name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		line.AddChild(name);
		var cost = new RichTextLabel
		{
			BbcodeEnabled = true,
			FitContent = true,
			AutowrapMode = TextServer.AutowrapMode.Off,
			ScrollActive = false,
			MouseFilter = MouseFilterEnum.Ignore,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			Text = ManaSymbols.Bbcode(Data(card).ManaCost?.Split(" // ")[0], size),
		};
		line.AddChild(cost);
		row.AddChild(line);
		return row;
	}

	private void Preview(string? card)
	{
		if (card is not null)
			_preview.Texture = MagicArt.Full(MagicArt.Key(card, S.Set));
	}

	/// <summary>A colour toggles (several at once); a kind replaces the colours; All clears every filter.</summary>
	private void PickFilter(string filter)
	{
		if (filter == "All")
		{
			_colors.Clear();
			_rarities.Clear();
			_kind = "All";
			_query.Text = "";
		}
		else if (ColorFilters.Contains(filter))
		{
			_kind = "All";
			if (!_colors.Remove(filter))
				_colors.Add(filter);
		}
		else
		{
			_colors.Clear();
			_kind = _kind == filter ? "All" : filter;
		}
		Redraw();
	}

	private bool FilterOn(string button) =>
		button == "All"
			? _colors.Count + _rarities.Count == 0 && _kind == "All" && _query.Text.Trim() == ""
		: Rarities.Contains(button) ? _rarities.Contains(button)
		: ColorFilters.Contains(button) ? _colors.Contains(button)
		: _kind == button;

	private bool Shows(string card)
	{
		var data = Data(card);
		var land = data.TypeLine.Contains("Land");
		var colors = data.Colors ?? [];
		var kind = _kind switch
		{
			"Lands" => land,
			"Multi" => colors.Length > 1,
			_ => true,
		};
		return kind
			&& SealedSession.InColours(data, _colors)
			&& (_rarities.Count == 0 || _rarities.Contains(RarityOf(card)))
			&& SealedSession.Matches(data, _query.Text);
	}

	private Dictionary<string, string>? _rarityOf;
	private string? _raritySet;

	/// <summary>"Common" … "Mythic" from the set's printings; a card it doesn't print (a Special Guest) counts as Mythic (Shayne, 2026-11-08).</summary>
	private string RarityOf(string card)
	{
		if (_raritySet != S.Set || _rarityOf is null)
		{
			_raritySet = S.Set;
			_rarityOf = CardDataSet
				.Load(S.Set)
				.GroupBy(p => p.Card.Name)
				.ToDictionary(g => g.Key, g => Capitalised(g.First().Rarity));
		}
		return _rarityOf.GetValueOrDefault(card, "Mythic");
	}

	private static string Capitalised(string word) =>
		word.Length == 0 ? word : char.ToUpperInvariant(word[0]) + word[1..];

	/// <summary>Arena's pool order: white, blue, black, red, green, multicolour, colourless, lands — then cost.</summary>
	internal static (int, double) Order(string card)
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

	internal static CardData Data(string card) => CardRegistry.Get(card).Data;

	internal static Button Choice(string text, System.Action pressed)
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

	internal static Label Text(string text, int size = 22)
	{
		var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.Off };
		label.AddThemeFontSizeOverride("font_size", size);
		label.AddThemeColorOverride("font_color", new Color("f2e6c8"));
		return label;
	}
}
