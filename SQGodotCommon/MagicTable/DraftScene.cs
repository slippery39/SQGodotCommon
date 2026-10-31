using System.Linq;
using MtgGame;

namespace MagicTable;

/// <summary>
/// FRA Quick Draft, the Arena way (story LIM-2.3): the pack in the middle as full cards — click one to
/// take it, every bot picking at the same time; your picks on the right by colour, each in curve order,
/// with the curve above them; the card under the mouse on the left. When the last pack is empty your
/// picks go to the Sealed screen's deck builder (<see cref="SealedScene.OpenDraft"/>). Every rule is
/// <see cref="DraftSession"/>'s; this only draws it. Debug args (<c>-GameArgs</c>): <c>--seed=N</c>;
/// <c>--autopick</c> drafts the first card of every pack and goes on to the deck building.
/// </summary>
public partial class DraftScene : Control
{
	private const float TileWidth = 180;

	/// <summary>The draft in progress — kept when you leave and come back, replaced by New draft.</summary>
	private static DraftSession? _session;

	/// <summary>The names of <see cref="SealedScene.Order"/>'s groups: Arena's pool order.</summary>
	private static readonly string[] Groups =
	[
		"White",
		"Blue",
		"Black",
		"Red",
		"Green",
		"Multicolour",
		"Colourless",
		"Lands",
	];

	/// <summary>The command line's --seed and --autopick are for the first visit only, not every New draft.</summary>
	private static bool _argsRead;

	private readonly Label _title = MtgUiStyles.Title("", 34);
	private readonly Label _where = MtgUiStyles.Title("", 28);
	private readonly Label _picksTitle = MtgUiStyles.Title("", 28);
	private readonly Label _curve = SealedScene.Text("", 18);
	private readonly HFlowContainer _pack = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
	private readonly VBoxContainer _picks = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
	private readonly TextureRect _preview =
		new()
		{
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			CustomMinimumSize = new Vector2(375, 523),
		};
	private readonly LineEdit _seed =
		new() { PlaceholderText = "random", CustomMinimumSize = new Vector2(140, 0) };

	private DraftSession S => _session!;

	public override void _Ready()
	{
		var args = _argsRead ? [] : OS.GetCmdlineUserArgs();
		_argsRead = true;
		_session ??= new DraftSession(
			args.FirstOrDefault(a => a.StartsWith("--seed="))?["--seed=".Length..] is { } arg
			&& int.TryParse(arg, out var seed)
				? seed
				: System.Random.Shared.Next(1, 1_000_000)
		);
		// --autopick: take the first card of every pack — straight to the deck building (captures)
		if (args.Contains("--autopick"))
			while (!S.IsComplete)
				S.Pick(0);
		if (S.IsComplete) // the draft is done: its deck building (New draft starts again there)
		{
			// deferred: ChangeScene adds to the root, which a _Ready can't (godot-frontend.md)
			Callable.From(ToBuilder).CallDeferred();
			return;
		}
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
		columns.AddChild(PackColumn());
		columns.AddChild(PicksColumn());
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
		left.AddChild(_where);
		left.AddChild(_preview);
		var seedRow = new HBoxContainer();
		seedRow.AddThemeConstantOverride("separation", 8);
		seedRow.AddChild(SealedScene.Text("Seed"));
		_seed.AddThemeFontSizeOverride("font_size", 22);
		seedRow.AddChild(_seed);
		seedRow.AddChild(
			SealedScene.Choice(
				"New draft",
				() =>
				{
					_session = new DraftSession(
						int.TryParse(_seed.Text.Trim(), out var seed)
							? seed
							: System.Random.Shared.Next(1, 1_000_000)
					);
					Redraw();
				}
			)
		);
		left.AddChild(seedRow);
		return left;
	}

	private Control PackColumn()
	{
		var scroll = new ScrollContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
		};
		_pack.AddThemeConstantOverride("h_separation", 8);
		_pack.AddThemeConstantOverride("v_separation", 8);
		scroll.AddChild(_pack);
		return scroll;
	}

	private Control PicksColumn()
	{
		var right = new VBoxContainer { CustomMinimumSize = new Vector2(400, 0) };
		right.AddThemeConstantOverride("separation", 8);
		right.AddChild(_picksTitle);
		_curve.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		_curve.CustomMinimumSize = new Vector2(1, 0);
		right.AddChild(_curve);
		var scroll = new ScrollContainer
		{
			SizeFlagsVertical = SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
		};
		_picks.AddThemeConstantOverride("separation", 4);
		scroll.AddChild(_picks);
		right.AddChild(scroll);
		return right;
	}

	private void Redraw()
	{
		if (S.IsComplete)
		{
			ToBuilder();
			return;
		}
		_title.Text = $"Draft · seed {S.Seed}";
		_where.Text = S.Where;
		if (_preview.Texture is null) // something to read before the first hover
			Preview(S.Pack[0]);

		CardViews.Clear(_pack);
		// shown in Arena's colour order; a click takes that card's place in the pack
		foreach (
			var (card, index) in S
				.Pack.Select((c, i) => (c, i))
				.OrderBy(x => SealedScene.Order(x.c))
				.ThenBy(x => x.c)
		)
			_pack.AddChild(Tile(card, index));

		var spells = S.Picks.Where(c => !IsLand(c)).ToList();
		_picksTitle.Text = $"Your picks  {S.Picks.Count}";
		_curve.Text =
			"Curve: "
			+ string.Join(
				"  ",
				spells
					.GroupBy(c => System.Math.Min(6, (int)SealedScene.Data(c).Cmc))
					.OrderBy(g => g.Key)
					.Select(g => $"{(g.Key == 6 ? "6+" : g.Key.ToString())}: {g.Count()}")
			);
		_curve.Visible = spells.Count > 0;
		CardViews.Clear(_picks);
		foreach (
			var group in S
				.Picks.OrderBy(SealedScene.Order)
				.ThenBy(c => c)
				.GroupBy(c => SealedScene.Order(c).Item1)
		)
		{
			_picks.AddChild(SealedScene.Text($"{Groups[group.Key]}  {group.Count()}", 20));
			foreach (var card in group)
				_picks.AddChild(PickRow(card));
		}
	}

	/// <summary>A card of the pack: the full card; hover previews it, a click takes it.</summary>
	private Control Tile(string card, int index)
	{
		var tile = new TextureRect
		{
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, // before Texture (godot-frontend.md)
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			Texture = MagicArt.Full(MagicArt.Key(card, SealedSession.Set)),
			CustomMinimumSize = new Vector2(TileWidth, TileWidth * 1.395f),
			TooltipText = SealedSession.Playable(card)
				? ""
				: "Not playable yet — its ability isn't scripted",
		};
		if (!SealedSession.Playable(card)) // as the deck builder shows it: it won't take it
			tile.Modulate = new Color(0.45f, 0.45f, 0.45f);
		CardViews.Hook(
			tile,
			card,
			Preview,
			() =>
			{
				S.Pick(index);
				Redraw();
			}
		);
		return tile;
	}

	/// <summary>A pick: its cost and name, as the deck list shows them; hover previews it.</summary>
	private Control PickRow(string card)
	{
		var row = new PanelContainer();
		row.AddThemeStyleboxOverride("panel", MtgUiStyles.ButtonNormal());
		var label = SealedScene.Text(
			$"{SealedScene.Data(card).ManaCost?.Split(" // ")[0]}  {card.Split(" // ")[0]}",
			18
		);
		label.MouseFilter = MouseFilterEnum.Ignore;
		label.ClipText = true;
		label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
		label.CustomMinimumSize = new Vector2(1, 0);
		row.AddChild(label);
		CardViews.Hook(row, card, Preview, onClick: null);
		return row;
	}

	private void Preview(string? card)
	{
		if (card is not null)
			_preview.Texture = MagicArt.Full(MagicArt.Key(card, SealedSession.Set));
	}

	/// <summary>The next visit starts a fresh draft — the deck builder's New draft.</summary>
	public static void Reset() => _session = null;

	private static void ToBuilder()
	{
		SealedScene.OpenDraft(_session!);
		Project.GameManager.Instance.ChangeScene("res://MagicTable/SealedScene.tscn");
	}

	private static bool IsLand(string card) => SealedScene.Data(card).TypeLine.Contains("Land");
}
