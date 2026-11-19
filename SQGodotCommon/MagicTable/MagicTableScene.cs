using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ImmutableGameObjects;
using MagicCore;
using MagicCore.Actions;
using MagicCore.Ai;
using MagicCore.Cards;
using MagicCore.Limited;
using MagicCore.Rules;
using MagicCore.Sets;
using MagicCore.Sets.Fra;
using MagicCore.Turns;
using MtgGame;

namespace MagicTable;

/// <summary>
/// The real-rules Magic table, Arena's layout in the Arcane look (MtgVisualDesign.md). Everything
/// is redrawn from <see cref="MagicTableManager.State"/> on each change. Click a card to play it,
/// then click its targets; a choice about objects on the table is answered by clicking them
/// (<see cref="BoardChoice"/>), anything else in the <see cref="ChoicePanel"/>. Space passes, Esc
/// leaves; the AI moves on a timer.
/// Debug args (<c>-GameArgs</c>): <c>--seed=N</c>, <c>--decks=A,B</c> (FraDecks names or Arena export files, with <c>--set=TRK</c> for TRK lists),
/// <c>--autoplay=N</c>, <c>--ai-vs-ai</c>, <c>--put=Name:Zone</c>, <c>--resume=FILE</c> with <c>--resume-at=N</c>,
/// <c>--show-hand</c> (their hand face up), <c>--full-ai</c> (the search uncapped), <c>--beat=S</c> (the animation beat in seconds, 0 for none — MagicTableScene.Motion.cs).
/// </summary>
public partial class MagicTableScene : Control
{
	/// <summary>Sizes here are in the 1920x1080 design space (project.godot), shown at 2/3 in the 1280x720 window.</summary>
	private const int TextSize = 22;

	// The layout's numbers, read by BuildLayout AND BoardScale — the scale must know the room.
	private const int Margin = 12;
	private const int Gap = 8;
	private const int LeftWidth = 375;
	private const int RightWidth = 430;

	private MagicTableManager _m = null!;

	/// <summary>The game whose result a run was told (<see cref="TableSetup.Finished"/>) — told once.</summary>
	private MagicTableManager? _reportedGame;
	private readonly PlayerPlate _oppPlate = new();
	private readonly PlayerPlate _myPlate = new();
	private readonly ZoneViewer _viewer = new();
	private (int Player, ZoneType Zone)? _viewing; // the zone open in the viewer, redrawn with the table
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

	// The combat row (TBL-4.6): in the middle, only while there is combat; edged red.
	private readonly HBoxContainer _combatRow =
		new() { Alignment = BoxContainer.AlignmentMode.Center };
	private readonly PanelContainer _combatPanel = new() { Visible = false };
	private readonly TargetArrows _arrows = new(); // the stack's targets (TBL-4.5)

	// Your hand: Common's fan (TBL-3), a Node2D placed over this space — the column reserves the room,
	// it can't contain a Node2D. Ignores the mouse: card hover is physics picking, which any Control
	// that stops the mouse above it blocks (godot-frontend.md).
	private MagicHandView _hand = null!;
	private readonly Control _handSpace =
		new()
		{
			CustomMinimumSize = new Vector2(0, CardViews.HandSize.Y + 12),
			MouseFilter = MouseFilterEnum.Ignore,
		};
	private readonly PhaseBar _phases = new();

	// A hand card being dragged, and the lands paying for it would tap (TBL-8.2).
	private int _previewFor;
	private IReadOnlySet<int> _pays = new HashSet<int>();
	private readonly VBoxContainer _stack = new();
	private readonly VBoxContainer _buttons = new();
	private readonly ScrollContainer _buttonsScroll;

	/// <summary>The most room the action column's buttons take before they scroll (a long list of plays or picks).</summary>
	private const float MaxButtonsHeight = 520;

	// The developer controls, folded away (TBL-4.2): undo, sandbox, full control, the AI's pace.
	private readonly MenuButton _tools =
		new()
		{
			Text = "Tools",
			Flat = false,
			FocusMode = FocusModeEnum.None,
		};

	private enum Tool
	{
		Undo,
		Sandbox,
		FullControl,
		Pause,
		ShowHand,
		Note,
		FullAi,
		Step,
		Inspector,
		Speed = 10, // + the index into Speeds
		Beat = 20, // + the index into Beats (TBL-4.5)
	}

	private bool _paused;

	// Their hand face up (Shayne, 2026-11-13: watching a game, "to see what they have in hand and determine whether
	// or not they are making reasonable plays"). A viewer's switch, not a rule: Tools, or --show-hand.
	private bool _showHand = OS.GetCmdlineUserArgs().Contains("--show-hand");

	// A right-click on the table (not on a card — that zooms it): Concede, confirmed first (TBL-9.3).
	private readonly PopupMenu _context = new();
	private readonly ConfirmationDialog _concede =
		new()
		{
			Title = "Concede",
			DialogText = "Concede this game? It counts as a loss.",
			OkButtonText = "Concede",
		};

	// Each side's half of the battlefield: a dark wash, edged in gold on its player's turn (TBL-4.2).
	private readonly PanelContainer _oppBoard = new() { SizeFlagsVertical = SizeFlags.ExpandFill };
	private readonly PanelContainer _myBoard = new() { SizeFlagsVertical = SizeFlags.ExpandFill };

	// "Your turn" / "AI's turn", shown as each turn begins, then fading.
	private readonly PanelContainer _banner =
		new() { MouseFilter = MouseFilterEnum.Ignore, Modulate = Colors.Transparent };
	private int _bannerTurn;

	// A held card, big (TBL-4.2): Arena's zoom, and a phone's only way to read a card.
	private readonly TextureRect _zoom =
		new()
		{
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			CustomMinimumSize = new Vector2(630, 880),
			MouseFilter = MouseFilterEnum.Ignore,
			Visible = false,
		};
	private readonly RichTextLabel _log =
		new()
		{
			ScrollFollowing = true,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			BbcodeEnabled = false,
		};
	private readonly ChoicePanel _choice = new() { CustomMinimumSize = new Vector2(930, 0) };
	private readonly SandboxPanel _sandbox = new();
	private readonly AiInspector _inspector = new(); // F6 (AI-14.4)
	private MagicTableManager.Inspection? _inspected;
	private readonly Timer _timer = new() { WaitTime = 0.5, Autostart = true };
	private static readonly (string Label, double Seconds)[] Speeds =
	[
		("Fast", 0.1),
		("Quick", 0.25),
		("Normal", 0.5),
		("Slow", 1.0),
		("Very slow", 2.0),
	];

	// What the table is asking, beyond the engine's own state. Any input (Do) clears the first two.
	// A clicked card's plays when it has several, with what the list asks ("Choose a play:",
	// "X — choose a mode") — one value, so a list can never show another's title.
	private (string Title, List<(string Label, List<GameAction> Moves)> Items)? _plays;
	private (List<GameAction> Candidates, int Index)? _targeting; // a play waiting for a target click
	private BoardChoice? _boardChoice; // the pending choice, when it is answered on the table

	// The state last drawn. GetPendingChoice() builds a NEW choice object on every call, so "is this
	// still the same question?" compares the immutable state, which changes only when something happens.
	private GameState? _shownState;
	private bool _shownAnimating; // drawn mid-beat, the question unasked: the same state is a NEW question after
	private readonly List<int> _picked = new(); // a multi board choice's picks so far
	private HashSet<int> _onTable = new(); // what can be clicked where it is, this redraw
	private CombatDeclaration? _combat; // your attacks or blocks, clicked on the table before submitting

	public MagicTableScene() => _buttonsScroll = Scrolling(_buttons, SizeFlags.Fill);

	public override void _Ready()
	{
		MtgUiStyles.AddBackdrop(this);
		BuildLayout();
		MouseFilter = MouseFilterEnum.Ignore; // the full-screen root would swallow the cards' picking
		_hand = new MagicHandView(
			this,
			DropOnTable,
			ObjectClicked,
			Preview,
			t => ShowBanner(t, 36),
			() => _choice.Visible || _viewer.Visible || _sandbox.Visible || _inspector.Visible
		);
		// ItemRectChanged, not Resized: the container MOVES the space into place after the first draw.
		_handSpace.ItemRectChanged += PlaceHand;
		_phases.StopsChanged += stops => Do(() => _m.Stops = stops);
		_phases.RowsChanged += Refresh;
		_choice.HiddenChanged += Refresh;

		// The AI moves on the timer; Pause stops it, Step makes one move, the speed sets the beat. It decides in the
		// background (TBL-2.22) - _Process plays the move once made - so the table stays live while it thinks.
		_timer.Timeout += () =>
		{
			if (!_paused && _onScreen is null) // the AI waits for the table to catch up
				_m.BeginAi();
		};
		AddChild(_timer);
		// --beat=S: an animation beat of S seconds (0: none) — Tools sets it while playing (TBL-4.5)
		if (
			double.TryParse(
				Arg("beat"),
				System.Globalization.NumberStyles.Float,
				System.Globalization.CultureInfo.InvariantCulture,
				out var beat
			)
		)
			_beat = beat;
		BuildTools();
		BuildContextMenu();
		Action<string?> zoom = name =>
		{
			_zoom.Visible = name is not null;
			if (name is not null)
				_zoom.Texture = MagicArt.Full(name);
		};
		CardViews.Zoom = zoom;
		// Only if still ours: a scene changing to another table adds the new one before this one goes.
		TreeExiting += () =>
		{
			if (CardViews.Zoom == zoom)
				CardViews.Zoom = null;
		};
		_sandbox.Put += (seat, name, zone) =>
			Do(
				() =>
					_m.Sandbox(seat == 0 ? _m.HumanId : _m.OpponentId, CardRegistry.Get(name), zone)
			);
		_sandbox.Previewed += Preview;

		// A record to resume (TBL-5.7): --resume=FILE, or picked on the deck select — once; New game is new.
		var resume = Arg("resume") is { } file
			? GameRecord.FromJson(File.ReadAllText(file))
			: TableSetup.Current?.Resume;
		// --resume-at=N: only its first N moves, to stop on a position (a capture of a blocked attack).
		if (resume is not null && int.TryParse(Arg("resume-at"), out var upTo))
			resume = resume.Truncate(upTo);
		if (TableSetup.Current?.Resume is not null)
			TableSetup.Current = TableSetup.Current with { Resume = null };
		_m = resume is not null
			? MagicTableManager.Resume(resume, UseSearch, RecordsDir)
			: NewGame(
				int.TryParse(Arg("seed"), out var seed)
					? seed
					: TableSetup.Current?.Seed ?? Random.Shared.Next(1, int.MaxValue),
				Stops.Arena
			);
		_m.FullSearch = OS.GetCmdlineUserArgs().Contains("--full-ai"); // before --autoplay plays with it
		if (int.TryParse(Arg("autoplay"), out var turn))
			_m.AutoplayTo(turn);
		// --put=Name:Zone (repeatable): the sandbox from the command line, into your seat. Without
		// --autoplay the game is at the mulligan, where nothing can be put — both keep first.
		var puts = OS.GetCmdlineUserArgs().Where(a => a.StartsWith("--put=")).ToList();
		if (puts.Count > 0 && !_m.CanSandbox)
			_m.AutoplayTo(1);
		foreach (var put in puts)
		{
			var (name, zone) = put["--put=".Length..].Split(':') switch
			{
				[var n, var z] => (n, Enum.Parse<ZoneType>(z, ignoreCase: true)),
				[var n] => (n, ZoneType.Hand),
				_ => throw new ArgumentException($"Expected --put=Name:Zone, got {put}"),
			};
			// A put's trigger may hand the AI a choice (Rank Rat: they discard) — let it answer first.
			while (!_m.CanSandbox && _m.AiToAct)
				_m.StepAi();
			if (_m.CanSandbox)
				_m.Sandbox(_m.HumanId, CardRegistry.Get(name), zone);
			else
				GD.PushWarning(
					$"{put} skipped: a choice of yours is pending — answer it, then use the Sandbox panel"
				);
		}
		if (OS.GetCmdlineUserArgs().Contains("--inspector")) // F6 open from the start: it follows each search
			OpenInspector();
		Refresh();
	}

	private static string? Arg(string name) =>
		OS.GetCmdlineUserArgs()
			.FirstOrDefault(a => a.StartsWith($"--{name}="))
			?[(name.Length + 3)..];

	/// <summary>
	/// Decks, AI-vs-AI and which AI from the deck select (<see cref="TableSetup"/>), the debug args overriding
	/// (the search unless --greedy);
	/// --autoplay only at startup, so New game starts at turn 0.
	/// </summary>
	private static MagicTableManager NewGame(int seed, Stops stops)
	{
		var setup = TableSetup.Current;
		var decks = Arg("decks")?.Split(',').Select(DeckArg).ToList();
		if (decks is { Count: not 2 })
			throw new ArgumentException(
				"--decks takes two decks: yours,theirs (FraDecks names or Arena export files)"
			);
		return new MagicTableManager(
			decks?[0].Main ?? setup?.Mine ?? FraDecks.All["White-Black"],
			decks?[1].Main ?? setup?.Theirs ?? FraDecks.All["Red-Green"],
			seed,
			OS.GetCmdlineUserArgs().Contains("--ai-vs-ai") || setup?.AiVsAi == true,
			UseSearch,
			decks is null ? setup?.MySideboard : decks[0].Sideboard,
			decks is null ? setup?.TheirSideboard : decks[1].Sideboard
		)
		{
			RecordTo = RecordsDir,
			Stops = stops,
		};
	}

	/// <summary>
	/// One deck of <c>--decks=A,B</c>: a FraDecks name, or an Arena export file (relative to the solution root, e.g.
	/// <c>sim_results/builds/RB.txt</c>) read as a sealed run reads it — the main deck, the rest of its pool outside the
	/// game — in the set of <c>--set=</c> (FRA by default).
	/// </summary>
	private static SealedBuildMatch.Build DeckArg(string name) =>
		FraDecks.All.TryGetValue(name, out var deck)
			? new(name, deck, [])
			: SealedBuildMatch.FromExport(
				Path.GetFileNameWithoutExtension(name),
				File.ReadAllText(
					Path.IsPathRooted(name)
						? name
						: Path.GetFullPath(ProjectSettings.GlobalizePath("res://../" + name))
				),
				DeckSelectScene.LimitedSet
			);

	private static bool UseSearch =>
		!OS.GetCmdlineUserArgs().Contains("--greedy") && TableSetup.Current?.Search != false;

	/// <summary>The solution root, beside the Godot project — untracked, like sim_results/ (story TBL-5).</summary>
	public static string RecordsDir =>
		Path.GetFullPath(ProjectSettings.GlobalizePath("res://../game_records"));

	// Any release ends a zoom: a redraw during the hold frees the held card, and its own release never comes.
	public override void _Input(InputEvent @event)
	{
		_hand.Track(@event);
		if (
			@event is InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true } right
			&& !_m.IsOver
			&& !OnACard()
		)
			// a popup is placed in WINDOW pixels — as _Input's event already is (a capture showed the
			// screen transform applied twice: the menu at 2/3 of the click)
			_context.Popup(new Rect2I((Vector2I)right.Position, Vector2I.Zero));
		if (@event is InputEventMouseButton { Pressed: false })
			_zoom.Visible = false;
	}

	/// <summary>
	/// Arena's tap preview: while a spell is dragged out of the hand, the lands it would tap glow. Asked of
	/// <see cref="CastSpellAction.PaymentPreview"/> — the very call the cast pays with, so it can't differ.
	/// </summary>
	public override void _Process(double delta)
	{
		// The AI's background decision, once made (TBL-2.22): a pass shows nothing, so the next decision starts at once
		// and the table redraws when a move shows or the turn comes back to you - as the timer's old loop did.
		if (!_paused && _onScreen is null && _m.AiDecided)
		{
			_m.Animate = _beat > 0;
			if (_m.FinishAi() is null && _m.AiToAct)
				_m.BeginAi();
			else
				Do(() => { });
		}
		var dragged =
			Common.Cards.CardUIManager.DraggingCard is { Id: { } id } && int.TryParse(id, out var n)
				? n
				: 0;
		if (dragged == _previewFor)
			return;
		_previewFor = dragged;
		var s = _m.State;
		_pays =
			dragged == 0
				? new HashSet<int>()
				: Moves()
					.OfType<CastSpellAction>()
					.Where(a => s.IsMoveFor(a, dragged))
					.Select(a => a.PaymentPreview(s))
					.FirstOrDefault(p => p is not null)
					?.Where(p => p.LandId != 0 && !p.Floats)
					.Select(p => p.LandId)
					.ToHashSet() ?? new HashSet<int>();
		Refresh();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is not InputEventKey { Pressed: true, Echo: false } key)
			return;
		if (key.Keycode == Key.F6)
		{
			if (_inspector.Visible)
				_inspector.Visible = false;
			else
				OpenInspector();
		}
		else if (key.Keycode == Key.Escape)
		{
			if (_inspector.Visible)
				_inspector.Visible = false;
			else if (_viewing is not null)
			{
				_viewing = null;
				Refresh();
			}
			else if (_sandbox.Visible)
				_sandbox.Visible = false;
			else if (_plays is not null || _targeting is not null)
				Do(() => { }); // Esc cancels a play first, like Arena
			else
				Project.GameManager.Instance.ChangeScene(
					TableSetup.Current?.Back ?? "res://MagicTable/DeckSelectScene.tscn"
				);
		}
		else if (
			key.Keycode == Key.Space
			&& _targeting is null
			&& Moves().OfType<PassPriorityAction>().FirstOrDefault() is { } pass
		)
			Do(() => _m.Act(pass));
		else if (key.Keycode == Key.Z && key.CtrlPressed)
			Do(_m.Undo);
		else if (key.Keycode == Key.N && !key.CtrlPressed)
			OpenNote();
		// P pauses the game — the AI and the animations; in AI vs AI Space does too (it has nothing to pass there)
		else if (key.Keycode == Key.P || key.Keycode == Key.Space && _m.AiVsAi)
			TogglePause();
	}

	/// <summary>Every input goes through here: apply it, then redraw.</summary>
	private void Do(Action change)
	{
		_plays = null;
		_targeting = null;
		_m.Animate = _beat > 0; // before the change: the first move made is shown too
		change();
		QueueBeats();
		if (_onScreen is null) // the beats redraw as they play
			Refresh();
	}

	/// <summary>The human's distinct moves right now; none while a choice is pending or it is not their turn to decide.</summary>
	private IReadOnlyList<GameAction> Moves() =>
		_onScreen is null && _m.HumanToAct && !_m.State.IsWaitingForChoice
			? _m.State.DistinctMoves(_m.State.LegalActions())
			: [];

	/// <summary>Everything that can be clicked where it is: permanents, your hand, the stack, the players.</summary>
	private HashSet<int> OnTable(GameState s) =>
		[
			.. s.CardsIn(_m.HumanId, ZoneType.Battlefield).Select(c => c.Id),
			.. s.CardsIn(_m.OpponentId, ZoneType.Battlefield).Select(c => c.Id),
			.. s.CardsIn(_m.HumanId, ZoneType.Hand).Select(c => c.Id),
			.. s.GetChildrenIds(s.GetWellKnownId(MagicKeys.Stack)),
			_m.HumanId,
			_m.OpponentId,
		];

	private void Refresh()
	{
		var s = _onScreen ?? _m.State;
		var game = s.GetGame();
		var moves = Moves();
		// A prepared creature → its castable copy, from the legal moves themselves (TBL-2.7): wherever
		// the copy is, whoever owns it, if the engine lets you cast it, its creature takes the click.
		_preparedCopies = moves
			.Select(MagicRules.CardOf)
			.Where(id => id != 0)
			.Distinct()
			.Select(s.GetCard)
			.Select(c => (Copy: c.Id, Creature: Prepare.CreatureOf(s, c)))
			.Where(x => x.Creature is not null)
			.DistinctBy(x => x.Creature!.Id)
			.ToDictionary(x => x.Creature!.Id, x => x.Copy);

		var choice = _m.HumanToAct && _onScreen is null ? s.GetPendingChoice() : null;
		var sameQuestion = ReferenceEquals(s, _shownState) && !_shownAnimating;
		if (!sameQuestion)
			_picked.Clear();
		_onTable = OnTable(s);
		// Combat is declared on the table as a whole (Arena), not one creature's question at a time.
		var declaring = choice is DeclareAttackerChoice or DeclareBlockerChoice;
		// A new declaration (another turn's, or one Undo went back to) starts clean.
		_combat =
			!declaring ? null
			: sameQuestion && _combat is not null ? _combat
			: new CombatDeclaration(choice is DeclareAttackerChoice);
		// "You may cast it" mid-resolution (Common Goal off The Caretaker): cast as from the hand - click the
		// targets - never a menu of every combination (TBL-2.16). Always restarted, so Esc can't strand it.
		var casting = choice is ChooseCastChoice;
		if (choice is ChooseCastChoice how && _targeting is null && _plays is null)
			StartCast(s, how);
		_boardChoice =
			choice is null || declaring || casting ? null : BoardChoice.For(s, choice, _onTable);
		if (choice is null || declaring || casting || _boardChoice is not null)
			_choice.Close();
		else if (!sameQuestion)
			_choice.Show(s, choice, Preview, ids => Do(() => _m.Choose(ids)));
		_shownState = s;
		_shownAnimating = _onScreen is not null;

		var clickable = Clickable();
		var about = OnItsCreature(
			s,
			_boardChoice?.AboutId
				?? (_targeting is (var candidates, _) ? MagicRules.CardOf(candidates[0]) : 0)
		);
		Glow GlowOf(int id, Card? c) =>
			_combat is { } combat ? combat.GlowOf(id, c, clickable)
			: _pays.Contains(id) ? Glow.Pays
			: _picked.Contains(id) ? Glow.Selected
			: clickable.Contains(id) ? Glow.Choosable
			: id == about ? Glow.About
			: c?.AttackingId > 0 ? Glow.Attacking
			: c?.BlockingId > 0 ? Glow.Blocking
			: c is not null
			&& _targeting is null
			&& (moves.Any(a => s.IsMoveFor(a, id)) || _preparedCopies.ContainsKey(id))
				? Glow.Playable
			: Glow.None;

		var active = game.ActivePlayerId;
		_oppBoard.AddThemeStyleboxOverride("panel", CardViews.Board(active == _m.OpponentId));
		_myBoard.AddThemeStyleboxOverride("panel", CardViews.Board(active == _m.HumanId));
		if (game.TurnNumber != _bannerTurn && game.TurnNumber > 0)
		{
			_bannerTurn = game.TurnNumber;
			ShowBanner(
				active == _m.HumanId && !_m.AiVsAi
					? "Your turn"
					: $"{s.GetPlayer(active).Name}'s turn"
			);
		}
		_header.Text =
			$"Seed {_m.Seed} · Turn {game.TurnNumber} · {(game.ActivePlayerId == _m.HumanId && !_m.AiVsAi ? "your" : $"{s.GetPlayer(game.ActivePlayerId).Name}'s")} turn";
		foreach (
			var (plate, player) in new[] { (_oppPlate, _m.OpponentId), (_myPlate, _m.HumanId) }
		)
			plate.Show(
				s,
				player,
				GlowOf(player, null),
				Preview,
				zone =>
				{
					_viewing = (player, zone);
					Refresh();
				},
				_m.HumanId,
				active: player == active,
				showHand: _showHand
			);

		// Auras and Equipment sit under their host — on its side of the table, whoever controls them.
		var attached = s.CardsIn(_m.HumanId, ZoneType.Battlefield)
			.Concat(s.CardsIn(_m.OpponentId, ZoneType.Battlefield))
			.Select(a => (Card: a, Host: Attachments.HostOf(s, a)))
			.Where(x => x.Host is not null)
			.ToLookup(x => x.Host!.Id, x => x.Card);
		RememberColumns(s);
		_phases.Show(_m, s); // first: the scale reads its height
		var scale = BoardScale(s);
		Fill(_oppLands, s, _m.OpponentId, lands: true, attached, c => GlowOf(c.Id, c), scale);
		Fill(_oppField, s, _m.OpponentId, lands: false, attached, c => GlowOf(c.Id, c), scale);
		Fill(_myField, s, _m.HumanId, lands: false, attached, c => GlowOf(c.Id, c), scale);
		Fill(_myLands, s, _m.HumanId, lands: true, attached, c => GlowOf(c.Id, c), scale);
		FillCombat(s, attached, c => GlowOf(c.Id, c), scale);

		var hand = s.CardsIn(_m.HumanId, ZoneType.Hand).ToList();
		// Anything castable from elsewhere (flashback, impulse exile) joins the hand row — but a
		// prepared copy is cast from its creature (TBL-2.7: in the hand it confused Shayne).
		var elsewhere = moves
			.Select(MagicRules.CardOf)
			.Where(id => id != 0 && !s.IsOnBattlefield(id) && hand.All(c => c.Id != id))
			.Distinct()
			.Select(s.GetCard)
			.Where(c => OnItsCreature(s, c.Id) == c.Id);
		_hand.Sync(
			[
				.. hand.Concat(elsewhere)
					.Select(c =>
						(
							c.Id,
							MagicArt.ImageName(s, c),
							GlowOf(c.Id, c),
							s.GetParent(c.Id) is int parent
							&& s.GetObject(parent) is Zone { ZoneType: not ZoneType.Hand } zone
								? zone.ZoneType.ToString().ToLowerInvariant()
								: null
						)
					),
			]
		);

		ShowStack(s, id => GlowOf(id, null));
		// The plates are where a player is targeted; the arrows find everything by its tag.
		CardViews.Tag(_oppPlate, _m.OpponentId);
		CardViews.Tag(_myPlate, _m.HumanId);
		_arrows.Pairs =
		[
			.. s.GetChildren(s.GetWellKnownId(MagicKeys.Stack))
				.SelectMany(o => (
						o switch
						{
							StackAbility a => a.Targets,
							_ => o.GetComponent<OnStackComponent>()?.Targets ?? [],
						}
					).Where(t => t != 0 && t != o.Id).Distinct().Select(t => (o.Id, t))),
		];
		ShowButtons(s, moves);
		_buttonsScroll.CustomMinimumSize = new Vector2(
			0,
			Mathf.Min(_buttons.GetCombinedMinimumSize().Y, MaxButtonsHeight)
		);
		var tools = _tools.GetPopup();
		tools.SetItemChecked(tools.GetItemIndex((int)Tool.FullControl), _m.Stops.FullControl);
		tools.SetItemChecked(tools.GetItemIndex((int)Tool.ShowHand), _showHand);
		tools.SetItemChecked(tools.GetItemIndex((int)Tool.FullAi), _m.FullSearch);
		tools.SetItemDisabled(tools.GetItemIndex((int)Tool.FullAi), !_m.Search);
		tools.SetItemDisabled(tools.GetItemIndex((int)Tool.Undo), !_m.CanUndo);
		tools.SetItemDisabled(tools.GetItemIndex((int)Tool.Step), !_m.AiToAct);
		ShowPaused();
		_sandbox.Allow(_m.CanSandbox);
		_log.Text = string.Join("\n", _m.Log.TakeLast(200));
		if (_inspector.Visible && !ReferenceEquals(_inspected, _m.LastSearch))
			OpenInspector(); // the AI searched again: follow it

		if (_viewing is (var viewPlayer, var viewZone))
			_viewer.Show(
				s,
				$"{s.GetPlayer(viewPlayer).Name} — {viewZone}",
				s.CardsIn(viewPlayer, viewZone),
				c => GlowOf(c.Id, c),
				Preview,
				id =>
				{
					_viewing = null; // out of the way of the targets and choices that follow
					ObjectClicked(id);
				},
				() =>
				{
					_viewing = null;
					Refresh();
				},
				_m.HumanId
			);
		else
			_viewer.Close();
	}

	/// <summary>What a click may pick right now: a target, or an object the board choice offers.</summary>
	private IReadOnlySet<int> Clickable() =>
		_combat is { } combat ? combat.Clickable(_m.State, _m.State.GetPendingChoice()!)
		: _targeting is (var candidates, var index)
			? Targeting.Options(candidates, index).ToHashSet()
		: _boardChoice?.Clickable ?? new HashSet<int>();

	/// <summary>One router for every click on an object — a card, a player plate, a stack row.</summary>
	private void ObjectClicked(int id)
	{
		var s = _m.State;
		if (_combat is { } combat)
		{
			combat.Click(s, s.GetPendingChoice()!, id);
			Refresh();
		}
		else if (_targeting is (var candidates, var index))
		{
			if (Targeting.Options(candidates, index).Contains(id))
				Play(Targeting.Narrow(candidates, index, id), index + 1);
		}
		else if (_boardChoice is { } board)
		{
			if (!board.Clickable.Contains(id) && board.Buttons.All(b => b.Id != id))
				return;
			if (!board.Multi)
				Do(() => _m.Choose([id]));
			else
			{
				if (!_picked.Remove(id))
					_picked.Add(id);
				Refresh();
			}
		}
		else if (s.HasObject(id) && s.GetObject(id) is Card)
		{
			var all = Moves();
			var mine = all.Where(a => s.IsMoveFor(a, id)).ToList();
			// A prepared creature casts its spell's copy (TBL-2.7). With nothing else to do, the click
			// IS a click on the copy — so a modal copy still asks for its mode below.
			if (_preparedCopies.TryGetValue(id, out var copy))
			{
				var spell = all.Where(a => s.IsMoveFor(a, copy)).ToList();
				if (mine.Count == 0 && !s.TapOptions(_m.HumanId, id).Any())
					(id, mine) = (copy, spell);
				else
					mine.AddRange(spell);
			}
			// A modal spell asks for its mode first — every mode shown, the impossible ones greyed.
			if (Targeting.Modes(s, id, mine) is { } modes)
			{
				var others = Targeting.Plays(s, mine.Where(a => a is not CastSpellAction));
				_plays = (
					$"{s.GetCard(id).Name} — choose a mode{(others.Count > 0 ? " or a play" : "")}",
					[.. modes, .. others.Select(p => (PlayLabel(s, p), p))]
				);
				Refresh();
				return;
			}
			var plays = Targeting.Plays(s, mine);
			// Arena's manual tapping: clicking your land (or mana creature) floats its mana.
			plays.AddRange(s.TapOptions(_m.HumanId, id).Select(t => new List<GameAction> { t }));
			if (plays.Count == 1)
				Play(plays[0], 0);
			else if (plays.Count > 1)
			{
				_plays = ("Choose a play:", plays.Select(p => (PlayLabel(s, p), p)).ToList());
				Refresh();
			}
		}
	}

	/// <summary>One move left: make it. Several told apart by a target: wait for that click. Otherwise list them.</summary>
	private void Play(List<GameAction> candidates, int from)
	{
		// X first, on a spinner, before any pick is clicked (TBL-9.1)
		if (from == 0 && Targeting.Xs(candidates).Count > 1)
		{
			_plays = ("Choose X:", [(PlayLabel(_m.State, candidates), candidates)]);
			_targeting = null;
			Refresh();
			return;
		}
		var index = Targeting.NextIndex(candidates, from);
		if (index < 0 && candidates.Count == 1)
		{
			// The last pick of a "may cast it": answer the choice with that cast, not a move of its own
			if (
				_m.State.GetPendingChoice() is ChooseCastChoice how
				&& candidates[0] is CastSpellAction cast
			)
				Do(() => _m.Choose([how.Casts.IndexOf(cast) + 1]));
			else
				Do(() => _m.Act(candidates[0]));
			return;
		}
		// Not told apart by clicks (sacrifice or pay): choose the way, then click its picks.
		_plays = index < 0 ? ("Choose a play:", Targeting.Ways(_m.State, candidates)) : null;
		_targeting = index < 0 ? null : (candidates, index);
		Refresh();
	}

	/// <summary>
	/// A pending <see cref="ChooseCastChoice"/> as the hand's cast: its casts narrowed by clicking their
	/// targets, a list only where clicks can't tell them apart (modes, "sacrifice or pay").
	/// </summary>
	private void StartCast(GameState s, ChooseCastChoice how)
	{
		List<GameAction> casts = [.. how.Casts];
		var index = Targeting.NextIndex(casts, 0);
		if (index >= 0)
			_targeting = (casts, index);
		else
			_plays = ("Cast it how?", Targeting.Ways(s, casts));
	}

	/// <summary>The way out of a "may cast it" once clicked into: cast nothing. Otherwise the plain cancel, which just clears.</summary>
	private void AddCancel()
	{
		if (_m.State.GetPendingChoice() is ChooseCastChoice)
			AddButton("Don't cast it  (Esc)", () => _m.Choose([]));
		else
			AddButton("Cancel  (Esc)", () => { });
	}

	private void PlaceHand()
	{
		_hand.Place(_handSpace.GetGlobalRect());
	}

	/// <summary>
	/// A card dragged out of the hand onto the table: the same router as a click (TBL-3). Says why
	/// when there is nothing it could do — a drop that silently slides back tells the player nothing.
	/// </summary>
	private string? DropOnTable(int id)
	{
		var s = _m.State;
		if (
			!Clickable().Contains(id)
			&& (_targeting is not null || !Moves().Any(a => s.IsMoveFor(a, id)))
		)
			return _m.HumanToAct
				? $"{s.GetCard(id).Name} can't be played now"
				: "Not your move — the AI is deciding";
		ObjectClicked(id);
		return null;
	}

	// ponytail: a creature that has its own plays lists a modal copy's casts one per mode combination,
	// not through the mode picker — pick the copy first if a modal prepare spell meets such a creature.
	private Dictionary<int, int> _preparedCopies = new();

	/// <summary>A prepared copy is shown by its creature; anything else by itself.</summary>
	private static int OnItsCreature(GameState s, int id) =>
		s.HasObject(id)
		&& s.GetObject(id) is Card { IsCopy: true } copy
		&& Prepare.CreatureOf(s, copy) is { } creature
			? creature.Id
			: id;

	/// <summary>A play's button: in full when there is nothing left to click, its picks left out otherwise.</summary>
	private static string PlayLabel(GameState s, List<GameAction> play) =>
		Targeting.Xs(play).Count > 1 ? Targeting.Label(s, Targeting.WithX(play[0], 0)) // X on its spinner
		: play.Count == 1 && Targeting.PicksOf(play[0]).Count == 0 ? Describe.Action(s, play[0])
		: Targeting.Label(s, play[0]);

	private void Fill(
		HFlowContainer row,
		GameState s,
		int player,
		bool lands,
		ILookup<int, Card> attached,
		Func<Card, Glow> glow,
		float scale
	)
	{
		CardViews.Clear(row);
		// fixed by the screen: rows that follow the scale bounce (TBL-2.18); a row that still overflows
		// scrolls inside it, never pushing the hand off the screen (TBL-2.19)
		((Control)row.GetParent()).CustomMinimumSize = new Vector2(0, _rowHeight);
		var onSomething = attached.SelectMany(g => g).Select(a => a.Id).ToHashSet();
		var combatants = Combatants();
		var cards = s.CardsIn(player, ZoneType.Battlefield)
			.Where(c =>
				c.Is(CardType.Land) == lands
				&& !onSomething.Contains(c.Id)
				&& !combatants.Contains(c.Id) // in the combat row (FillCombat)
			);
		// Identical lands stack, as on Arena: everything the crop shows is the same.
		foreach (
			var group in cards.GroupBy(c =>
				lands && !attached.Contains(c.Id) ? $"{CardViews.Looks(s, c)}|{glow(c)}" : $"{c.Id}"
			)
		)
		{
			row.AddChild(
				group.Count() > 1 // a land stack: every land in it (Permanent tags its one card)
					? CardViews.Tag(
						PermanentView(s, group.First(), group.Count(), attached, glow, scale),
						[.. group.Select(c => c.Id)]
					)
					: PermanentView(s, group.First(), 1, attached, glow, scale)
			);
		}

		// Phased out (The City on the Edge of Forever): still shown in its row, dimmed and badged — nothing
		// can be done with it, so a click does nothing. What was attached shows beside it, dimmed too.
		foreach (
			var c in s.CardsIn(player, ZoneType.PhasedOut).Where(c => c.Is(CardType.Land) == lands)
		)
			row.AddChild(CardViews.Permanent(s, c, 1, glow(c), Preview, () => { }, scale));
	}

	/// <summary>
	/// The combat row (TBL-4.6, Shayne: "attackers was hard to follow"): in the middle, only while there is combat.
	/// Each attacker takes a column with the creatures blocking it — MTGO's pairing (TBL-4.3) — the opponent's side
	/// on top, yours below, so who attacks and who blocks whom reads at a glance, apart from the rest of the board.
	/// </summary>
	private void FillCombat(
		GameState s,
		ILookup<int, Card> attached,
		Func<Card, Glow> glow,
		float scale
	)
	{
		CardViews.Clear(_combatRow);
		_combatPanel.Visible = _shownColumns.Count > 0;
		// its two rows' height: a ScrollContainer reports none of its content's, and the band collapsed to a line
		((Control)_combatRow.GetParent()).CustomMinimumSize = new Vector2(0, 2 * _rowHeight + 4);
		foreach (var (attacker, blockers) in _shownColumns)
		{
			var column = new VBoxContainer();
			column.AddThemeConstantOverride("separation", 4);
			foreach (var side in new[] { _m.OpponentId, _m.HumanId })
			{
				var half = new HBoxContainer
				{
					Alignment = BoxContainer.AlignmentMode.Center,
					CustomMinimumSize = new Vector2(0, _rowHeight),
				};
				half.AddThemeConstantOverride("separation", 6);
				foreach (var id in blockers.Prepend(attacker))
					if (s.IsOnBattlefield(id) && s.GetCard(id) is { } c && c.ControllerId == side)
						half.AddChild(PermanentView(s, c, 1, attached, glow, scale));
				column.AddChild(half);
			}
			_combatRow.AddChild(column);
		}
	}

	/// <summary>Who is in the combat row: every attacker and blocker of the declaration (<see cref="_shownColumns"/>).</summary>
	private HashSet<int> Combatants() =>
		[.. _shownColumns.SelectMany(g => g.Blockers.Prepend(g.Attacker))];

	/// <summary>
	/// The columns as shown, made once a redraw (<see cref="RememberColumns"/>): the declaration's (<see cref="_columns"/>),
	/// with the blocks you have clicked but not yet submitted in them too (TBL-2.20, Shayne: "The creatures should move
	/// into the combat area showing that they are going to block after I have chosen them") — shown only, never
	/// remembered, so one you take back goes home.
	/// </summary>
	private List<(int Attacker, List<int> Blockers)> _shownColumns = [];

	private List<(int Attacker, List<int> Blockers)> ShownColumns() =>
		_combat is { Attacking: false } blocking && blocking.Declared.Count > 0
			?
			[
				.. _columns.Select(g =>
					(
						g.Attacker,
						g.Blockers.Union(
								blocking
									.Declared.Where(d => d.Value == g.Attacker)
									.Select(d => d.Key)
							)
							.ToList()
					)
				),
			]
			: _columns;

	/// <summary>A permanent as a view, with what is attached to it under it.</summary>
	private Control PermanentView(
		GameState s,
		Card c,
		int count,
		ILookup<int, Card> attached,
		Func<Card, Glow> glow,
		float scale
	)
	{
		var view = CardViews.Permanent(
			s,
			c,
			count,
			glow(c),
			Preview,
			() => ObjectClicked(c.Id),
			scale
		);
		if (!attached.Contains(c.Id))
			return view;
		var stack = new VBoxContainer();
		stack.AddThemeConstantOverride("separation", 2);
		stack.AddChild(view);
		AddAttachments(c.Id);
		return stack;

		// An Aura on an Equipment on a creature: each strip followed by what is on it.
		void AddAttachments(int hostId)
		{
			foreach (var a in attached[hostId])
			{
				stack.AddChild(
					CardViews.Attachment(a, glow(a), Preview, () => ObjectClicked(a.Id))
				);
				AddAttachments(a.Id);
			}
		}
	}

	/// <summary>
	/// The combat's columns, from the DECLARATION (TBL-2.17): each attacker with every creature that blocked
	/// it, in the order first seen, kept while any attacker is left. A creature that leaves mid-combat
	/// drops out of its column but the columns don't re-form, so attackers and blockers still meet.
	/// </summary>
	private void RememberColumns(GameState s)
	{
		var live = Combat
			.Attackers(s)
			.Select(a => (a.Id, Blockers: Combat.BlockersOf(s, a.Id).Select(b => b.Id).ToList()))
			.ToList();
		var turn = s.GetGame().TurnNumber;
		if (live.Count == 0 || turn != _columnsTurn)
			_columns = [];
		_columnsTurn = turn;
		foreach (var (id, blockers) in live)
		{
			var i = _columns.FindIndex(c => c.Attacker == id);
			if (i < 0)
				_columns.Add((id, blockers));
			else
				_columns[i] = (id, _columns[i].Blockers.Union(blockers).ToList());
		}
		_shownColumns = ShownColumns();
	}

	private List<(int Attacker, List<int> Blockers)> _columns = [];
	private int _columnsTurn;

	private void ShowStack(GameState s, Func<int, Glow> glow)
	{
		CardViews.Clear(_stack);
		var title = InfoLabel();
		title.Text = "Stack (top first)";
		_stack.AddChild(title);
		foreach (var obj in s.GetChildren(s.GetWellKnownId(MagicKeys.Stack)))
		{
			var (name, text) = obj switch
			{
				Card spell => (MagicArt.ImageName(s, spell), spell.Name),
				StackAbility ability => (ability.Source.Name, $"{ability.Source.Name} — ability"),
				_ => (obj.Name, obj.Name),
			};
			var who = s.GetPlayer(s.StackObjectControllerId(obj.Id)).Name;
			var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
			row.AddChild(CardViews.Thumb(name, 84));
			var label = InfoLabel();
			label.Text = $"{text} ({who})";
			label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			label.MouseFilter = MouseFilterEnum.Ignore;
			row.AddChild(label);
			var plate = new PanelContainer();
			plate.AddThemeStyleboxOverride("panel", CardViews.Outline(glow(obj.Id)));
			plate.AddChild(row);
			var id = obj.Id;
			CardViews.Hook(plate, name, Preview, () => ObjectClicked(id));
			_stack.AddChild(CardViews.Tag(plate, id));
		}
	}

	private void ShowButtons(GameState s, IReadOnlyList<GameAction> moves)
	{
		CardViews.Clear(_buttons);
		var winner = _m.WinnerId;
		if (_m.IsOver && _onScreen is null)
		{
			_status.Text =
				winner == 0 ? "Draw."
				: winner == _m.HumanId ? "You win!"
				: "The AI wins.";
			if (TableSetup.Current?.Finished is { } finished)
			{
				// a run's game (a draft's): reported once, and no replay into the run
				if (_reportedGame != _m)
					finished(winner == _m.HumanId);
				_reportedGame = _m;
				AddButton(
					"Back to deck building",
					() => Project.GameManager.Instance.ChangeScene(TableSetup.Current.Back)
				);
				return;
			}
			AddButton("Replay this seed", () => _m = _m.Replay());
			AddButton(
				"New game",
				() =>
				{
					var full = _m.FullSearch;
					_m = NewGame(Random.Shared.Next(1, int.MaxValue), _m.Stops);
					_m.FullSearch = full;
				}
			);
			return;
		}
		if (_choice.Hidden) // put away to see the board (TBL-2.21): the choice still waits
		{
			_status.Text = "A choice is waiting";
			AddButton("Show the choice", _choice.Unhide);
			return;
		}
		if (_targeting is (var candidates, var index))
		{
			_status.Text =
				$"{Targeting.Label(s, candidates[0])} — choose {Targeting.What(candidates[0], index)}";
			// A target not on the table (a card in a graveyard) is a button instead; 0 is
			// "no target" for an optional one ("up to one target creature").
			foreach (
				var id in Targeting.Options(candidates, index).Where(i => !_onTable.Contains(i))
			)
				AddButton(
					id == 0 ? "No target" : s.GetObject(id).Name,
					() => ObjectClicked(id),
					redraw: false,
					option: true
				);
			AddCancel();
			return;
		}
		if (_plays is var (title, items))
		{
			_status.Text = title;
			foreach (var (label, play) in items)
				if (Targeting.Xs(play) is { Count: > 1 } xs)
					AddXPicker(label, play, xs);
				else
					AddButton(label, () => Play(play, 0), redraw: false, option: true).Disabled =
						play.Count == 0; // a mode that can't be chosen now: shown, greyed
			AddCancel();
			return;
		}
		if (_combat is { } combat)
		{
			_status.Text = combat.Status(s);
			var count = combat.Declared.Count;
			AddButton(
				count > 0 ? $"{(combat.Attacking ? "Attack" : "Block")} ({count})"
					: combat.Attacking ? "No attacks"
					: "No blocks",
				() => combat.Refused = _m.DeclareCombat(combat.Declared.ToDictionary())
			);
			if (combat.Attacking && count < MagicCore.Turns.Combat.AttackCandidates(s).Count)
				AddButton(
					"All attack",
					() =>
					{
						combat.AllAttack(s);
						Refresh();
					},
					redraw: false
				);
			return;
		}
		if (_boardChoice is { } board)
		{
			var choice = board.Choice;
			_status.Text =
				$"{Describe.Question(s, choice)}{(board.Multi ? $" ({BoardChoice.Range(choice)})" : "")}";
			foreach (var option in board.Buttons)
				AddButton(
					option.DisplayText,
					board.Multi ? () => ObjectClicked(option.Id) : () => _m.Choose([option.Id]),
					redraw: !board.Multi,
					option: true,
					picked: _picked.Contains(option.Id)
				);
			if (board.Multi)
			{
				var count = _picked.Count;
				var submit = AddButton($"Submit ({count})", () => _m.Choose(_picked.ToList()));
				submit.Disabled = count < choice.MinChoices || count > choice.MaxChoices;
			}
			return;
		}
		_status.Text =
			_m.HumanToAct && _onScreen is null
				? s.IsWaitingForChoice
					? "Your choice."
					: $"Your priority — {s.GetGame().Step}"
				: "AI is thinking…";
		foreach (var move in moves.Where(a => MagicRules.CardOf(a) == 0))
			AddButton(
				move is PassPriorityAction ? $"{PassLabel(s)}  (Space)" : Describe.Action(s, move),
				() => _m.Act(move)
			);
	}

	/// <summary>
	/// What passing leads to, as Arena's button says it (TBL-4.2) — so a pass is never made thinking
	/// it is the other player's turn. ponytail: by the step alone, not by your stops; with stops that
	/// skip combat, "To combat" passes further than it says — read Stops if that misleads.
	/// </summary>
	private string PassLabel(GameState s)
	{
		var game = s.GetGame();
		return !s.StackIsEmpty() ? "Resolve"
			: game.ActivePlayerId != _m.HumanId ? "Pass"
			: game.Step switch
			{
				Step.PreCombatMain => "To combat",
				Step.PostCombatMain or Step.End => "End turn",
				_ => "Next",
			};
	}

	private Tween? _bannerTween;

	private void ShowBanner(string text, int size = 64)
	{
		CardViews.Clear(_banner);
		_banner.AddChild(MtgUiStyles.Title(text, size));
		_bannerTween?.Kill(); // a fade-out still running would hide the new turn's banner
		var tween = _bannerTween = CreateTween();
		tween.TweenProperty(_banner, "modulate", Colors.White, 0.2);
		tween.TweenInterval(0.9);
		tween.TweenProperty(_banner, "modulate", Colors.Transparent, 0.6);
	}

	private void OpenInspector()
	{
		_inspected = _m.LastSearch;
		_inspector.Show(_inspected, _m.ExplainLastSearch, () => _inspector.Visible = false);
	}

	// A note for Claude (TBL-10.3): the AI waits while it is written, the position saved with it (GameNotes).
	private readonly ConfirmationDialog _note =
		new()
		{
			Title = "Note — for Claude to analyse",
			OkButtonText = "Save note",
			MinSize = new Vector2I(900, 420),
		};
	private readonly TextEdit _noteText =
		new()
		{
			PlaceholderText = "What looks wrong, or worth a look, at this moment?",
			WrapMode = TextEdit.LineWrappingMode.Boundary,
		};
	private bool _pausedBeforeNote;

	private void TogglePause()
	{
		_paused = !_paused;
		ShowPaused();
	}

	/// <summary>The pause on show — set at once, as a redraw waits while beats play.</summary>
	private void ShowPaused()
	{
		var tools = _tools.GetPopup();
		tools.SetItemChecked(tools.GetItemIndex((int)Tool.Pause), _paused);
		_tools.Text = _paused ? "Tools · paused" : "Tools";
	}

	private void OpenNote()
	{
		if (_note.Visible)
			return;
		_pausedBeforeNote = _paused;
		_paused = true;
		ShowPaused();
		_noteText.Text = "";
		_note.PopupCentered();
		_noteText.GrabFocus();
	}

	private void CloseNote(bool save)
	{
		_paused = _pausedBeforeNote;
		if (save && _noteText.Text.Trim() is { Length: > 0 } text)
			ShowBanner(
				GameNotes.Write(_m, text, Shown().State, Shown().Moves) is { } path
					? $"Note saved — {System.IO.Path.GetFileName(path)}"
					: "Not saved: this game keeps no record yet",
				28
			);
		Refresh();
	}

	private void BuildContextMenu()
	{
		_noteText.AddThemeFontSizeOverride("font_size", TextSize);
		_noteText.SizeFlagsVertical = SizeFlags.ExpandFill;
		_note.AddChild(_noteText);
		_note.GetOkButton().AddThemeFontSizeOverride("font_size", TextSize);
		_note.GetCancelButton().AddThemeFontSizeOverride("font_size", TextSize);
		_note.Confirmed += () => CloseNote(save: true);
		_note.Canceled += () => CloseNote(save: false);
		AddChild(_note);
		_context.AddThemeFontSizeOverride("font_size", TextSize);
		_context.AddItem("Concede", 0);
		_context.IdPressed += _ => _concede.PopupCentered();
		AddChild(_context);
		_concede.GetLabel().AddThemeFontSizeOverride("font_size", TextSize);
		_concede.GetOkButton().AddThemeFontSizeOverride("font_size", TextSize);
		_concede.GetCancelButton().AddThemeFontSizeOverride("font_size", TextSize);
		_concede.Confirmed += () => Do(_m.Concede);
		AddChild(_concede);
	}

	/// <summary>Is the pointer on a card — one on the table (<see cref="CardViews.Hook"/>) or in the hand?</summary>
	private bool OnACard()
	{
		if (Common.Cards.CardUIManager.CurrentHoveredCard is not null)
			return true;
		for (Node? n = GetViewport().GuiGetHoveredControl(); n is not null; n = n.GetParent())
			if (n.HasMeta(CardViews.CardMeta))
				return true;
		return false;
	}

	private void BuildTools()
	{
		var menu = _tools.GetPopup();
		menu.AddThemeFontSizeOverride("font_size", TextSize);
		menu.AddItem("Undo  (Ctrl+Z)", (int)Tool.Undo);
		menu.AddItem("Sandbox", (int)Tool.Sandbox);
		menu.AddSeparator();
		menu.AddCheckItem("Full control", (int)Tool.FullControl);
		menu.AddCheckItem("Pause  (P)", (int)Tool.Pause);
		menu.AddCheckItem("Show their hand", (int)Tool.ShowHand);
		menu.AddItem("Write a note  (N)", (int)Tool.Note);
		menu.AddCheckItem("Full AI (no 1 s cap)", (int)Tool.FullAi);
		menu.AddItem("Step the AI", (int)Tool.Step);
		menu.AddItem("AI inspector  (F6)", (int)Tool.Inspector);
		menu.AddSeparator("AI speed");
		for (var i = 0; i < Speeds.Length; i++)
			menu.AddRadioCheckItem(Speeds[i].Label, (int)Tool.Speed + i);
		menu.SetItemChecked(menu.GetItemIndex((int)Tool.Speed + 2), true);
		menu.AddSeparator("Animation");
		for (var i = 0; i < Beats.Length; i++)
		{
			menu.AddRadioCheckItem(Beats[i].Label, (int)Tool.Beat + i);
			menu.SetItemChecked(menu.GetItemIndex((int)Tool.Beat + i), Beats[i].Seconds == _beat);
		}
		menu.IdPressed += id =>
		{
			switch ((Tool)id)
			{
				case Tool.Undo:
					Do(_m.Undo);
					break;
				case Tool.Sandbox:
					_sandbox.Visible = !_sandbox.Visible;
					break;
				case Tool.FullControl:
					Do(() => _m.Stops = _m.Stops with { FullControl = !_m.Stops.FullControl });
					break;
				case Tool.Pause:
					TogglePause();
					break;
				case Tool.FullAi:
					_m.FullSearch = !_m.FullSearch;
					Refresh();
					break;
				case Tool.Note:
					OpenNote();
					break;
				case Tool.ShowHand:
					_showHand = !_showHand;
					if (_onScreen is null) // mid-beat, the next beat redraws
						Refresh();
					break;
				case Tool.Inspector:
					OpenInspector();
					break;
				case Tool.Step: // paused: a decision made in the background is played first
					if (_m.AiDecided)
						Do(() => _m.FinishAi());
					else if (_m.AiToAct && !_m.Thinking)
						Do(() => _m.StepAi());
					break;
				case >= Tool.Beat:
					var beat = (int)id - (int)Tool.Beat;
					_beat = Beats[beat].Seconds;
					for (var i = 0; i < Beats.Length; i++)
						menu.SetItemChecked(menu.GetItemIndex((int)Tool.Beat + i), i == beat);
					break;
				default:
					var speed = (int)id - (int)Tool.Speed;
					_timer.WaitTime = Speeds[speed].Seconds;
					for (var i = 0; i < Speeds.Length; i++)
						menu.SetItemChecked(menu.GetItemIndex((int)Tool.Speed + i), i == speed);
					break;
			}
		};
	}

	/// <summary>A button in the action column. Through Do (clears any play in progress) unless it continues one.</summary>
	/// <param name="option">One of a list (plays, targets, a choice's options): the plain plate in a
	/// smaller font. Otherwise a main action (Pass, Attack, Submit) in the big oxblood style.</param>
	/// <param name="picked">An option of a multi-pick choice that is picked: the selected plate.</param>
	/// <summary>
	/// X on a spinner (TBL-9.1, Shayne: one button per X was "terrible past a few"): − and + step it, the
	/// box takes a typed number, bounded by the legal X values (a typed gap goes to the nearest legal one);
	/// the play's button makes it with that X. Starts at the most X can be.
	/// </summary>
	private void AddXPicker(string label, List<GameAction> play, List<int> xs)
	{
		var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		row.AddThemeConstantOverride("separation", 8);
		var spin = new SpinBox
		{
			MinValue = xs[0],
			MaxValue = xs[^1],
			Step = 1,
			Rounded = true,
			Value = xs[^1],
			Alignment = HorizontalAlignment.Center,
			CustomMinimumSize = new Vector2(110, 0),
		};
		spin.GetLineEdit().AddThemeFontSizeOverride("font_size", TextSize + 4);
		// its own tiny arrows: the − and + beside it do that
		foreach (
			var arrow in new[]
			{
				"updown",
				"up",
				"up_hover",
				"up_pressed",
				"up_disabled",
				"down",
				"down_hover",
				"down_pressed",
				"down_disabled",
			}
		)
			spin.AddThemeIconOverride(arrow, new ImageTexture());
		spin.AddThemeConstantOverride("buttons_width", 0);
		spin.GetLineEdit().AddThemeStyleboxOverride("normal", MtgUiStyles.DarkPanel());
		var x = SealedScene.Text("X =", TextSize + 2);
		x.VerticalAlignment = VerticalAlignment.Center;
		row.AddChild(x);
		row.AddChild(SealedScene.Choice("−", () => spin.Value -= 1));
		row.AddChild(spin);
		row.AddChild(SealedScene.Choice("+", () => spin.Value += 1));
		_buttons.AddChild(row);
		var go = AddButton(
			$"{label}  (X={xs[^1]})",
			() => Play(Targeting.WithChosenX(play, (int)spin.Value), 0),
			redraw: false,
			option: true
		);
		spin.ValueChanged += value => go.Text = $"{label}  (X={(int)value})";
	}

	private Button AddButton(
		string text,
		Action act,
		bool redraw = true,
		bool option = false,
		bool picked = false
	)
	{
		// Wrapped, never wider than the column: a long label ("Cast X → Y, sacrificing Z") once grew
		// the button off the screen and hid which button did what.
		var button = new Button
		{
			Text = text,
			FocusMode = FocusModeEnum.None,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			CustomMinimumSize = new Vector2(1, 0),
		};
		if (option)
		{
			MtgUiStyles.StyleChoice(button, selected: picked);
			button.AddThemeFontSizeOverride("font_size", TextSize);
		}
		else
			MtgUiStyles.StylePrimary(button, 28);
		button.Pressed += redraw ? () => Do(act) : act;
		_buttons.AddChild(button);
		return button;
	}

	private void Preview(string? cardName)
	{
		if (cardName is not null)
			_preview.Texture = MagicArt.Full(cardName);
	}

	private void BuildLayout()
	{
		var margin = new MarginContainer();
		margin.SetAnchorsPreset(LayoutPreset.FullRect);
		foreach (var side in new[] { "left", "right", "top", "bottom" })
			margin.AddThemeConstantOverride($"margin_{side}", Margin);
		AddChild(margin);
		var columns = new HBoxContainer();
		columns.AddThemeConstantOverride("separation", Gap);
		margin.AddChild(columns);

		// A player plate is a target too: "deal 3 damage to any target", "Attack the AI".
		var left = Column(LeftWidth);
		_oppPlate.Clicked += () => ObjectClicked(_m.OpponentId);
		_myPlate.Clicked += () => ObjectClicked(_m.HumanId);
		left.AddChild(_oppPlate);
		left.AddChild(_preview);
		left.AddChild(_myPlate);
		columns.AddChild(left);

		var center = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		center.AddThemeConstantOverride("separation", 4);
		// The two battlefields take the slack, so lands sit at the edges and the hand at the bottom.
		_oppField.SizeFlagsVertical = _myField.SizeFlagsVertical = SizeFlags.ExpandFill;
		center.AddChild(Half(_oppBoard, _oppLands, _oppField));
		center.AddChild(_phases);
		var band = CardViews.Board(active: false);
		band.BorderColor = new Color("d0583a"); // Glow.Attacking's red
		band.SetBorderWidthAll(3);
		_combatPanel.AddThemeStyleboxOverride("panel", band);
		_combatRow.AddThemeConstantOverride("separation", 16);
		_combatPanel.AddChild(Scrolling(_combatRow, SizeFlags.Fill));
		center.AddChild(_combatPanel);
		center.AddChild(Half(_myBoard, _myField, _myLands));
		center.AddChild(_handSpace);
		columns.AddChild(center);

		var right = Column(RightWidth);
		right.AddChild(Plate(_header));
		right.AddChild(Plate(_stack));
		right.AddChild(Plate(_status));
		right.AddChild(_buttonsScroll);
		_log.AddThemeFontSizeOverride("normal_font_size", TextSize - 2);
		_tools.AddThemeFontSizeOverride("font_size", TextSize);
		MtgUiStyles.StyleChoice(_tools, selected: false);
		right.AddChild(_tools);
		var logPlate = Plate(_log);
		logPlate.SizeFlagsVertical = SizeFlags.ExpandFill;
		right.AddChild(logPlate);
		columns.AddChild(right);

		_banner.AddThemeStyleboxOverride("panel", MtgUiStyles.DarkPanel());
		// Later children draw on top: a pending choice above an open zone viewer.
		foreach (
			var panel in new Control[] { _banner, _viewer, _sandbox, _choice, _inspector, _zoom }
		)
		{
			// Above the hand: Hand2D gives each card a ZIndex, which beats tree order (godot-frontend.md).
			var overlay = new CenterContainer
			{
				MouseFilter = MouseFilterEnum.Ignore,
				ZIndex = 300,
			};
			overlay.SetAnchorsPreset(LayoutPreset.FullRect);
			overlay.AddChild(panel);
			AddChild(overlay);
		}
		_arrows.Rects = () => Rects(Nodes());
		AddChild(_arrows);
	}

	/// <summary>
	/// The permanents' size (TBL-4.2: a few cards sat small in an empty centre): as big as a half of
	/// the board holds two rows, at most 1.5×, and never so wide that the fullest row would wrap —
	/// counted as if every card were tapped (sideways). The room comes from the
	/// SCREEN, never from the board's own size, which grows with its cards (that fed back: the cards
	/// grew until the hand left the screen). Counted as the rows are built: a land stack is one card, a
	/// combat's columns are in both creature rows, and attachments stack under their host (TBL-2.19).
	/// </summary>
	/// <summary>Down to here a crowded board shrinks its cards (TBL-2.10: 10+ creatures wrapped the row and pushed the hand off the screen); past it the row scrolls.</summary>
	private const float MinBoardScale = 0.5f;

	/// <summary>Every permanent row's height: a half of the board's room, from the screen alone, so rows never move as cards come and go.</summary>
	private float _rowHeight = CardViews.PermanentSize.Y;

	private float BoardScale(GameState s)
	{
		var screen = GetViewportRect().Size - new Vector2(2 * Margin, 2 * Margin);
		var w = screen.X - LeftWidth - RightWidth - 2 * Gap - 8; // less the half's own margins
		// Two rows a half, and while there is combat the combat row's two more (TBL-4.6): every row the same
		// height, less each panel's margins (12) and the gaps between them.
		var combat = _shownColumns.Count > 0;
		var (rows, panels) = combat ? (6, 3) : (4, 2);
		var room = screen.Y - _phases.GetCombinedMinimumSize().Y - (CardViews.HandSize.Y + 12) - 12;
		var rowHeight = (room - 12 - 12 * panels - (combat ? 8 : 0)) / rows; // the band's border and its gap
		// The combat row's columns side by side, each as wide as its wider side — counted here, or it wraps (TBL-2.19).
		var combatants = Combatants();
		var columns = _shownColumns.Sum(g => Math.Max(1, g.Blockers.Count));
		var fullest = new[] { _m.HumanId, _m.OpponentId }
			.SelectMany(p => s.CardsIn(p, ZoneType.Battlefield).GroupBy(c => c.Is(CardType.Land)))
			.Select(g =>
				g.Key
					? g.Select(c => CardViews.Looks(s, c)).Distinct().Count() // identical lands stack (Fill); a glow may split one
					: g.Count(c => !combatants.Contains(c.Id))
			)
			.Append(columns)
			.DefaultIfEmpty(1)
			.Max();
		// Auras and Equipment stack under their host's card, at their own height — an Aura on an Equipment
		// too, so count them by the card at the bottom of the chain (TBL-2.19).
		Card Root(Card c) => Attachments.HostOf(s, c) is { } host ? Root(host) : c;
		var strips = new[] { _m.HumanId, _m.OpponentId }
			.SelectMany(p => s.CardsIn(p, ZoneType.Battlefield))
			.Where(c => Attachments.HostOf(s, c) is not null)
			.GroupBy(c => Root(c).Id)
			.Select(g => g.Count())
			.DefaultIfEmpty(0)
			.Max();
		var size = CardViews.PermanentSize;
		_rowHeight = rowHeight;
		var byHeight = (rowHeight - strips * CardViews.AttachmentHeight) / size.Y;
		var byWidth = w / (fullest * (size.Y + 6));
		return Mathf.Clamp(Mathf.Min(byHeight, byWidth), MinBoardScale, 1.5f);
	}

	private static PanelContainer Half(PanelContainer board, params Control[] rows)
	{
		var column = new VBoxContainer();
		column.AddThemeConstantOverride("separation", 4);
		foreach (var row in rows)
			column.AddChild(Scrolling(row, row.SizeFlagsVertical));
		board.AddChild(column);
		return board;
	}

	/// <summary>
	/// A ScrollContainer reports no height of its own content, so whatever grows inside one — a row past
	/// MinBoardScale, a long list of buttons — scrolls there instead of growing the table off the screen
	/// (TBL-2.19: a combat's columns wrapped a row, and two Equipment under one creature, and the hand left).
	/// </summary>
	private static ScrollContainer Scrolling(Control content, SizeFlags vertical)
	{
		var scroll = new ScrollContainer
		{
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			SizeFlagsVertical = vertical,
			MouseFilter = MouseFilterEnum.Pass, // as the rows were: a Stop here would block the hand's card picking
		};
		content.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		scroll.AddChild(content);
		return scroll;
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

	// ponytail: past ~20 permanents on a side (below MinBoardScale) a row wraps and scrolls in its place;
	// stack identical permanents if a real board ever gets there.
	private static HFlowContainer Row()
	{
		// No height of its own: its scroll holds _rowHeight (Fill). An unscaled card's height here overflowed every
		// row once six shared the screen (TBL-4.6) — each row scrolled.
		var row = new HFlowContainer { Alignment = FlowContainer.AlignmentMode.Center };
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
