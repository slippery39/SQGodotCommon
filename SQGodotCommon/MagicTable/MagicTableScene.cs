using System;
using System.Collections.Generic;
using System.Linq;
using ImmutableGameObjects;
using MagicCore;
using MagicCore.Actions;
using MagicCore.Cards;
using MagicCore.Sets.Fra;
using MtgGame;

namespace MagicTable;

/// <summary>
/// The real-rules Magic table, Arena's layout in the Arcane look (MtgVisualDesign.md). Everything
/// is redrawn from <see cref="MagicTableManager.State"/> on each change. Click a card for its moves
/// (a menu when there are several), Space passes, Esc leaves; the AI moves on a timer.
/// Debug args (<c>-GameArgs</c>): <c>--seed=N</c>, <c>--decks=A,B</c> (FraDecks names),
/// <c>--autoplay=N</c>, <c>--ai-vs-ai</c>.
/// </summary>
public partial class MagicTableScene : Control
{
	private MagicTableManager _m = null!;
	private readonly Label _oppInfo = InfoLabel();
	private readonly Label _myInfo = InfoLabel();
	private readonly Label _header = InfoLabel();
	private readonly Label _status = InfoLabel();
	private readonly TextureRect _preview =
		new()
		{
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
	private readonly HFlowContainer _oppLands = Row();
	private readonly HFlowContainer _oppField = Row();
	private readonly HFlowContainer _myField = Row();
	private readonly HFlowContainer _myLands = Row();
	private readonly HBoxContainer _hand =
		new()
		{
			Alignment = BoxContainer.AlignmentMode.Center,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
	private readonly PhaseBar _phases = new();
	private readonly VBoxContainer _stack = new();
	private readonly VBoxContainer _buttons = new();
	private readonly CheckBox _fullControl =
		new() { Text = "Full control", FocusMode = FocusModeEnum.None };
	private readonly Button _undo = new() { Text = "Undo", FocusMode = FocusModeEnum.None };
	private readonly RichTextLabel _log =
		new()
		{
			ScrollFollowing = true,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			BbcodeEnabled = false,
		};

	/// <summary>Sizes here are in the 1920x1080 design space (project.godot), shown at 2/3 in the 1280x720 window.</summary>
	private const int TextSize = 22;
	private readonly ChoicePanel _choice = new() { CustomMinimumSize = new Vector2(930, 0) };
	private ImmutableGameObjects.ChoiceAction? _shownChoice;

	/// <summary>The clicked card's moves while it has several; cleared by any input.</summary>
	private List<GameAction>? _cardMoves;

	public override void _Ready()
	{
		MtgUiStyles.AddBackdrop(this);
		BuildLayout();
		_phases.StopsChanged += stops => Do(() => _m.Stops = stops);
		_fullControl.Toggled += on => Do(() => _m.Stops = _m.Stops with { FullControl = on });
		_undo.Pressed += () => Do(_m.Undo);

		var timer = new Timer { WaitTime = 0.5, Autostart = true };
		timer.Timeout += () =>
		{
			if (_m.AiToAct)
				Do(() => _m.StepAi());
		};
		AddChild(timer);

		_m = NewGame(
			int.TryParse(Arg("seed"), out var seed) ? seed : Random.Shared.Next(1, int.MaxValue),
			Stops.Arena
		);
		if (int.TryParse(Arg("autoplay"), out var turn))
			_m.AutoplayTo(turn);
		Refresh();
	}

	private static string? Arg(string name) =>
		OS.GetCmdlineUserArgs()
			.FirstOrDefault(a => a.StartsWith($"--{name}="))
			?[(name.Length + 3)..];

	/// <summary>Decks and AI-vs-AI from the args; --autoplay only at startup, so New game starts at turn 0.</summary>
	private static MagicTableManager NewGame(int seed, Stops stops)
	{
		var decks = (Arg("decks") ?? "White-Black,Red-Green").Split(',');
		return new MagicTableManager(
			FraDecks.All[decks[0]],
			FraDecks.All[decks[1]],
			seed,
			OS.GetCmdlineUserArgs().Contains("--ai-vs-ai")
		)
		{
			Stops = stops,
		};
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is not InputEventKey { Pressed: true, Echo: false } key)
			return;
		if (key.Keycode == Key.Escape)
			Project.GameManager.Instance.GoToMainMenu();
		else if (
			key.Keycode == Key.Space
			&& Moves().OfType<PassPriorityAction>().FirstOrDefault() is { } pass
		)
			Do(() => _m.Act(pass));
		else if (key.Keycode == Key.Z && key.CtrlPressed)
			Do(_m.Undo);
	}

	/// <summary>Every input goes through here: apply it, then redraw.</summary>
	private void Do(Action change)
	{
		_cardMoves = null;
		change();
		Refresh();
	}

	/// <summary>The human's distinct moves right now; none while a choice is pending or it is not their turn to decide.</summary>
	private IReadOnlyList<GameAction> Moves() =>
		_m.HumanToAct && !_m.State.IsWaitingForChoice
			? _m.State.DistinctMoves(_m.State.LegalActions())
			: [];

	private void Refresh()
	{
		var s = _m.State;
		var game = s.GetGame();
		var moves = Moves();
		Glow GlowOf(Card c) =>
			c.AttackingId != 0 ? Glow.Attacking
			: c.BlockingId != 0 ? Glow.Blocking
			: moves.Any(a => s.IsMoveFor(a, c.Id)) ? Glow.Playable
			: Glow.None;

		_header.Text =
			$"Seed {_m.Seed} · Turn {game.TurnNumber} · {(game.ActivePlayerId == _m.HumanId ? "your turn" : "AI's turn")}";
		_oppInfo.Text = Info(s, _m.OpponentId, "AI");
		_myInfo.Text = Info(s, _m.HumanId, _m.AiVsAi ? "AI 1 (bottom)" : "You");

		Fill(_oppLands, s, _m.OpponentId, lands: true, GlowOf);
		Fill(_oppField, s, _m.OpponentId, lands: false, GlowOf);
		Fill(_myField, s, _m.HumanId, lands: false, GlowOf);
		Fill(_myLands, s, _m.HumanId, lands: true, GlowOf);
		_phases.Show(_m);

		CardViews.Clear(_hand);
		var hand = s.CardsIn(_m.HumanId, ZoneType.Hand).ToList();
		// Anything castable from elsewhere (flashback, impulse exile, prepared copies) joins the hand row.
		var elsewhere = moves
			.Select(MagicRules.CardOf)
			.Where(id => id != 0 && !s.IsOnBattlefield(id) && hand.All(c => c.Id != id))
			.Distinct()
			.Select(s.GetCard);
		foreach (var c in hand.Concat(elsewhere))
			_hand.AddChild(CardViews.HandCard(c, GlowOf(c), Preview, () => Clicked(c)));

		ShowStack(s);
		ShowButtons(s, moves);
		_fullControl.SetPressedNoSignal(_m.Stops.FullControl);
		_undo.Disabled = !_m.CanUndo;
		_log.Text = string.Join("\n", _m.Log.TakeLast(200));

		var choice = _m.HumanToAct ? s.GetPendingChoice() : null;
		if (choice is null)
			_choice.Close();
		else if (!ReferenceEquals(choice, _shownChoice))
			_choice.Show(s, choice, Preview, ids => Do(() => _m.Choose(ids)));
		_shownChoice = choice;
	}

	private void Fill(
		HFlowContainer row,
		GameState s,
		int player,
		bool lands,
		Func<Card, Glow> glow
	)
	{
		CardViews.Clear(row);
		var cards = s.CardsIn(player, ZoneType.Battlefield)
			.Where(c => c.Is(CardType.Land) == lands);
		// Identical lands stack, as on Arena: everything the crop shows is the same.
		foreach (
			var group in cards.GroupBy(c =>
				lands ? $"{CardViews.Looks(s, c)}|{glow(c)}" : $"{c.Id}"
			)
		)
		{
			var c = group.First();
			row.AddChild(
				CardViews.Permanent(s, c, group.Count(), glow(c), Preview, () => Clicked(c))
			);
		}
	}

	private void ShowStack(GameState s)
	{
		CardViews.Clear(_stack);
		var title = InfoLabel();
		title.Text = "Stack (top first)";
		_stack.AddChild(title);
		foreach (var obj in s.GetChildren(s.GetWellKnownId(MagicKeys.Stack)))
		{
			var (name, text) = obj switch
			{
				Card spell => (spell.Name, spell.Name),
				StackAbility ability => (ability.Source.Name, $"{ability.Source.Name} — ability"),
				_ => (obj.Name, obj.Name),
			};
			var who = s.StackObjectControllerId(obj.Id) == _m.HumanId ? "you" : "AI";
			var row = new HBoxContainer();
			row.AddChild(CardViews.Thumb(name, 84));
			var label = InfoLabel();
			label.Text = $"{text} ({who})";
			label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			row.AddChild(label);
			CardViews.Hook(row, name, Preview, null);
			_stack.AddChild(row);
		}
	}

	private void ShowButtons(GameState s, IReadOnlyList<GameAction> moves)
	{
		CardViews.Clear(_buttons);
		var winner = s.WinnerId();
		_status.Text = _m.IsOver
			? winner == 0
				? "Draw."
				: winner == _m.HumanId
					? "You win!"
					: "The AI wins."
			: _m.HumanToAct
				? s.IsWaitingForChoice
					? "Your choice."
					: $"Your priority — {s.GetGame().Step}"
				: "AI is thinking…";

		if (_m.IsOver)
		{
			AddButton("Replay this seed", () => _m = _m.Replay());
			AddButton(
				"New game",
				() => _m = NewGame(Random.Shared.Next(1, int.MaxValue), _m.Stops)
			);
			return;
		}
		if (_cardMoves is not null)
		{
			_status.Text = "Choose a move:";
			foreach (var move in _cardMoves)
				AddButton(Describe.Action(s, move), () => _m.Act(move));
			AddButton("Cancel", () => { });
			return;
		}
		foreach (var move in moves.Where(a => MagicRules.CardOf(a) == 0))
			AddButton(
				move is PassPriorityAction
					? s.StackIsEmpty()
						? "Pass  (Space)"
						: "Resolve  (Space)"
					: Describe.Action(s, move),
				() => _m.Act(move)
			);
	}

	private void AddButton(string text, Action act)
	{
		var button = new Button { Text = text, FocusMode = FocusModeEnum.None };
		MtgUiStyles.StylePrimary(button, 28);
		button.Pressed += () => Do(act);
		_buttons.AddChild(button);
	}

	/// <summary>A card's moves: one acts at once; several are listed in the action column.</summary>
	private void Clicked(Card c)
	{
		var mine = Moves().Where(a => _m.State.IsMoveFor(a, c.Id)).ToList();
		if (mine.Count == 1)
			Do(() => _m.Act(mine[0]));
		else if (mine.Count > 1)
		{
			_cardMoves = mine;
			Refresh();
		}
	}

	private void Preview(string? cardName)
	{
		if (cardName is not null)
			_preview.Texture = MagicArt.Full(cardName);
	}

	private static string Info(GameState s, int player, string name)
	{
		var p = s.GetPlayer(player);
		int Count(ZoneType z) => s.CardsIn(player, z).Count();
		var pool = p.ManaPool;
		var floating = string.Join(
			" ",
			new[]
			{
				("W", pool.White),
				("U", pool.Blue),
				("B", pool.Black),
				("R", pool.Red),
				("G", pool.Green),
				("C", pool.Colorless),
			}
				.Where(x => x.Item2 > 0)
				.Select(x => $"{{{x.Item1}}}×{x.Item2}")
		);
		var priority = s.GetGame().PriorityPlayerId == player ? "  ◀ priority" : "";
		return $"{name} — life {p.Life}{priority}\n"
			+ $"hand {Count(ZoneType.Hand)} · library {Count(ZoneType.Library)}\n"
			+ $"graveyard {Count(ZoneType.Graveyard)} · exile {Count(ZoneType.Exile)} · command {Count(ZoneType.Command)}"
			+ (
				floating == "" && pool.Restricted.IsEmpty
					? ""
					: $"\nfloating {floating}{(pool.Restricted.IsEmpty ? "" : $" +{pool.Restricted.Count} restricted")}"
			);
	}

	private void BuildLayout()
	{
		var margin = new MarginContainer();
		margin.SetAnchorsPreset(LayoutPreset.FullRect);
		foreach (var side in new[] { "left", "right", "top", "bottom" })
			margin.AddThemeConstantOverride($"margin_{side}", 12);
		AddChild(margin);
		var columns = new HBoxContainer();
		columns.AddThemeConstantOverride("separation", 8);
		margin.AddChild(columns);

		var left = Column(375);
		left.AddChild(Plate(_oppInfo));
		left.AddChild(_preview);
		left.AddChild(Plate(_myInfo));
		columns.AddChild(left);

		var center = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		center.AddThemeConstantOverride("separation", 4);
		// The two battlefields take the slack, so lands sit at the edges and the hand at the bottom.
		_oppField.SizeFlagsVertical = _myField.SizeFlagsVertical = SizeFlags.ExpandFill;
		foreach (var part in new Control[] { _oppLands, _oppField, _phases, _myField, _myLands })
			center.AddChild(part);
		_hand.AddThemeConstantOverride("separation", 4);
		var handScroll = new ScrollContainer
		{
			CustomMinimumSize = new Vector2(0, CardViews.HandSize.Y + 12),
			VerticalScrollMode = ScrollContainer.ScrollMode.Disabled,
		};
		handScroll.AddChild(_hand);
		center.AddChild(handScroll);
		columns.AddChild(center);

		var right = Column(430);
		right.AddChild(Plate(_header));
		right.AddChild(Plate(_stack));
		right.AddChild(Plate(_status));
		right.AddChild(_buttons);
		var toggles = new HBoxContainer();
		_fullControl.AddThemeFontSizeOverride("font_size", TextSize);
		_undo.AddThemeFontSizeOverride("font_size", TextSize);
		_log.AddThemeFontSizeOverride("normal_font_size", TextSize - 2);
		toggles.AddChild(_fullControl);
		toggles.AddChild(_undo);
		right.AddChild(toggles);
		var logPlate = Plate(_log);
		logPlate.SizeFlagsVertical = SizeFlags.ExpandFill;
		right.AddChild(logPlate);
		columns.AddChild(right);

		var overlay = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
		overlay.SetAnchorsPreset(LayoutPreset.FullRect);
		overlay.AddChild(_choice);
		AddChild(overlay);
	}

	private static VBoxContainer Column(int width)
	{
		var column = new VBoxContainer { CustomMinimumSize = new Vector2(width, 0) };
		column.AddThemeConstantOverride("separation", 8);
		return column;
	}

	private static PanelContainer Plate(Control content)
	{
		var plate = new PanelContainer();
		plate.AddThemeStyleboxOverride("panel", MtgUiStyles.DarkPanel());
		plate.AddChild(content);
		return plate;
	}

	// ponytail: rows wrap (HFlow) when a side has many permanents, pushing the hand down; shrink
	// the cards to fit (MtgGame's FitRow) if a real board overflows.
	private static HFlowContainer Row()
	{
		var row = new HFlowContainer
		{
			CustomMinimumSize = new Vector2(0, CardViews.PermanentSize.Y),
			Alignment = FlowContainer.AlignmentMode.Center,
		};
		row.AddThemeConstantOverride("h_separation", 6);
		return row;
	}

	private static Label InfoLabel()
	{
		var label = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			CustomMinimumSize = new Vector2(1, 0),
		};
		label.AddThemeFontSizeOverride("font_size", TextSize);
		label.AddThemeColorOverride("font_color", new Color("f2e6c8"));
		return label;
	}
}
