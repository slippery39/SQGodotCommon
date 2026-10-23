using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ImmutableGameObjects;
using MagicCore;
using MagicCore.Actions;
using MagicCore.Ai;
using MagicCore.Cards;
using MagicCore.Rules;
using MagicCore.Sets.Fra;
using MtgGame;

namespace MagicTable;

/// <summary>
/// The real-rules Magic table, Arena's layout in the Arcane look (MtgVisualDesign.md). Everything
/// is redrawn from <see cref="MagicTableManager.State"/> on each change. Click a card to play it,
/// then click its targets; a choice about objects on the table is answered by clicking them
/// (<see cref="BoardChoice"/>), anything else in the <see cref="ChoicePanel"/>. Space passes, Esc
/// leaves; the AI moves on a timer.
/// Debug args (<c>-GameArgs</c>): <c>--seed=N</c>, <c>--decks=A,B</c> (FraDecks names),
/// <c>--autoplay=N</c>, <c>--ai-vs-ai</c>, <c>--put=Name:Zone</c>.
/// </summary>
public partial class MagicTableScene : Control
{
	/// <summary>Sizes here are in the 1920x1080 design space (project.godot), shown at 2/3 in the 1280x720 window.</summary>
	private const int TextSize = 22;

	private MagicTableManager _m = null!;
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
	private readonly ChoicePanel _choice = new() { CustomMinimumSize = new Vector2(930, 0) };
	private readonly SandboxPanel _sandbox = new();
	private readonly Timer _timer = new() { WaitTime = 0.5, Autostart = true };
	private readonly Button _pause =
		new()
		{
			Text = "Pause AI",
			ToggleMode = true,
			FocusMode = FocusModeEnum.None,
		};
	private readonly Button _step = new() { Text = "Step", FocusMode = FocusModeEnum.None };
	private readonly OptionButton _speed = new() { FocusMode = FocusModeEnum.None };
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
	private readonly List<int> _picked = new(); // a multi board choice's picks so far
	private HashSet<int> _onTable = new(); // what can be clicked where it is, this redraw
	private CombatDeclaration? _combat; // your attacks or blocks, clicked on the table before submitting

	public override void _Ready()
	{
		MtgUiStyles.AddBackdrop(this);
		BuildLayout();
		_phases.StopsChanged += stops => Do(() => _m.Stops = stops);
		_fullControl.Toggled += on => Do(() => _m.Stops = _m.Stops with { FullControl = on });
		_undo.Pressed += () => Do(_m.Undo);

		// The AI moves on the timer; Pause stops it, Step makes one move, the speed sets the beat.
		_timer.Timeout += () =>
		{
			if (!_pause.ButtonPressed && _m.AiToAct)
				Do(() => _m.StepAi());
		};
		AddChild(_timer);
		_pause.Toggled += _ => Refresh();
		_step.Pressed += () =>
		{
			if (_m.AiToAct)
				Do(() => _m.StepAi());
		};
		foreach (var (label, _) in Speeds)
			_speed.AddItem(label);
		_speed.Select(2);
		_speed.ItemSelected += i => _timer.WaitTime = Speeds[i].Seconds;
		_sandbox.Put += (seat, name, zone) =>
			Do(() => _m.Sandbox(seat == 0 ? _m.HumanId : _m.OpponentId, FraCards.Get(name), zone));
		_sandbox.Previewed += Preview;

		// A record to resume (TBL-5.7): --resume=FILE, or picked on the deck select — once; New game is new.
		var resume = Arg("resume") is { } file
			? GameRecord.FromJson(File.ReadAllText(file))
			: TableSetup.Current?.Resume;
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
				_m.Sandbox(_m.HumanId, FraCards.Get(name), zone);
			else
				GD.PushWarning(
					$"{put} skipped: a choice of yours is pending — answer it, then use the Sandbox panel"
				);
		}
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
		var decks = Arg("decks")?.Split(',');
		return new MagicTableManager(
			decks is null ? setup?.Mine ?? FraDecks.All["White-Black"] : FraDecks.All[decks[0]],
			decks is null ? setup?.Theirs ?? FraDecks.All["Red-Green"] : FraDecks.All[decks[1]],
			seed,
			OS.GetCmdlineUserArgs().Contains("--ai-vs-ai") || setup?.AiVsAi == true,
			UseSearch,
			decks is null ? setup?.MySideboard : null,
			decks is null ? setup?.TheirSideboard : null
		)
		{
			RecordTo = RecordsDir,
			Stops = stops,
		};
	}

	private static bool UseSearch =>
		!OS.GetCmdlineUserArgs().Contains("--greedy") && TableSetup.Current?.Search != false;

	/// <summary>The solution root, beside the Godot project — untracked, like sim_results/ (story TBL-5).</summary>
	public static string RecordsDir =>
		Path.GetFullPath(ProjectSettings.GlobalizePath("res://../game_records"));

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is not InputEventKey { Pressed: true, Echo: false } key)
			return;
		if (key.Keycode == Key.Escape)
		{
			if (_viewing is not null)
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
	}

	/// <summary>Every input goes through here: apply it, then redraw.</summary>
	private void Do(Action change)
	{
		_plays = null;
		_targeting = null;
		change();
		Refresh();
	}

	/// <summary>The human's distinct moves right now; none while a choice is pending or it is not their turn to decide.</summary>
	private IReadOnlyList<GameAction> Moves() =>
		_m.HumanToAct && !_m.State.IsWaitingForChoice
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
		var s = _m.State;
		var game = s.GetGame();
		var moves = Moves();

		var choice = _m.HumanToAct ? s.GetPendingChoice() : null;
		var sameQuestion = ReferenceEquals(s, _shownState);
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
		_boardChoice = choice is null || declaring ? null : BoardChoice.For(s, choice, _onTable);
		if (choice is null || declaring || _boardChoice is not null)
			_choice.Close();
		else if (!sameQuestion)
			_choice.Show(s, choice, Preview, ids => Do(() => _m.Choose(ids)));
		_shownState = s;

		var clickable = Clickable();
		var about =
			_boardChoice?.AboutId
			?? (_targeting is (var candidates, _) ? MagicRules.CardOf(candidates[0]) : 0);
		Glow GlowOf(int id, Card? c) =>
			_combat is { } combat ? combat.GlowOf(id, c, clickable)
			: _picked.Contains(id) ? Glow.Selected
			: clickable.Contains(id) ? Glow.Choosable
			: id == about ? Glow.About
			: c?.AttackingId > 0 ? Glow.Attacking
			: c?.BlockingId > 0 ? Glow.Blocking
			: c is not null && _targeting is null && moves.Any(a => s.IsMoveFor(a, id))
				? Glow.Playable
			: Glow.None;

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
				}
			);

		// Auras and Equipment sit under their host — on its side of the table, whoever controls them.
		var attached = s.CardsIn(_m.HumanId, ZoneType.Battlefield)
			.Concat(s.CardsIn(_m.OpponentId, ZoneType.Battlefield))
			.Select(a => (Card: a, Host: Attachments.HostOf(s, a)))
			.Where(x => x.Host is not null)
			.ToLookup(x => x.Host!.Id, x => x.Card);
		Fill(_oppLands, s, _m.OpponentId, lands: true, attached, c => GlowOf(c.Id, c));
		Fill(_oppField, s, _m.OpponentId, lands: false, attached, c => GlowOf(c.Id, c));
		Fill(_myField, s, _m.HumanId, lands: false, attached, c => GlowOf(c.Id, c));
		Fill(_myLands, s, _m.HumanId, lands: true, attached, c => GlowOf(c.Id, c));
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
			_hand.AddChild(
				CardViews.HandCard(
					c,
					GlowOf(c.Id, c),
					Preview,
					() => ObjectClicked(c.Id),
					from: c.IsCopy ? "prepared copy"
						: s.GetParent(c.Id) is int parent
						&& s.GetObject(parent) is Zone { ZoneType: not ZoneType.Hand } zone
							? zone.ZoneType.ToString().ToLowerInvariant()
						: null
				)
			);
		FitHand();

		ShowStack(s, id => GlowOf(id, null));
		ShowButtons(s, moves);
		_fullControl.SetPressedNoSignal(_m.Stops.FullControl);
		_undo.Disabled = !_m.CanUndo;
		_step.Disabled = !_m.AiToAct;
		_sandbox.Allow(_m.CanSandbox);
		_log.Text = string.Join("\n", _m.Log.TakeLast(200));

		if (_viewing is (var viewPlayer, var viewZone))
			_viewer.Show(
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
				}
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
			var mine = Moves().Where(a => s.IsMoveFor(a, id)).ToList();
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
		var index = Targeting.NextIndex(candidates, from);
		if (index < 0 && candidates.Count == 1)
		{
			Do(() => _m.Act(candidates[0]));
			return;
		}
		// Not told apart by clicks (sacrifice or pay): choose the way, then click its picks.
		_plays = index < 0 ? ("Choose a play:", Targeting.Ways(_m.State, candidates)) : null;
		_targeting = index < 0 ? null : (candidates, index);
		Refresh();
	}

	/// <summary>
	/// A hand too wide for the row overlaps, as on Arena, instead of hiding its last cards behind a
	/// scrollbar (8 cards did). The hover preview shows the whole card.
	/// </summary>
	private void FitHand()
	{
		var count = _hand.GetChildCount();
		var width = ((Control)_hand.GetParent()).Size.X;
		var room =
			count < 2 || width <= 0 ? 4 : (width - count * CardViews.HandSize.X) / (count - 1);
		_hand.AddThemeConstantOverride("separation", Mathf.Min(4, Mathf.FloorToInt(room))); // floor: a truncated overlap still clipped the last card
	}

	/// <summary>A play's button: in full when there is nothing left to click, its picks left out otherwise.</summary>
	private static string PlayLabel(GameState s, List<GameAction> play) =>
		play.Count == 1 && Targeting.PicksOf(play[0]).Count == 0
			? Describe.Action(s, play[0])
			: Targeting.Label(s, play[0]);

	private void Fill(
		HFlowContainer row,
		GameState s,
		int player,
		bool lands,
		ILookup<int, Card> attached,
		Func<Card, Glow> glow
	)
	{
		CardViews.Clear(row);
		var onSomething = attached.SelectMany(g => g).Select(a => a.Id).ToHashSet();
		var cards = s.CardsIn(player, ZoneType.Battlefield)
			.Where(c => c.Is(CardType.Land) == lands && !onSomething.Contains(c.Id));
		// Identical lands stack, as on Arena: everything the crop shows is the same.
		foreach (
			var group in cards.GroupBy(c =>
				lands && !attached.Contains(c.Id) ? $"{CardViews.Looks(s, c)}|{glow(c)}" : $"{c.Id}"
			)
		)
		{
			var c = group.First();
			var view = CardViews.Permanent(
				s,
				c,
				group.Count(),
				glow(c),
				Preview,
				() => ObjectClicked(c.Id)
			);
			if (!attached.Contains(c.Id))
			{
				row.AddChild(view);
				continue;
			}
			var stack = new VBoxContainer();
			stack.AddThemeConstantOverride("separation", 2);
			stack.AddChild(view);
			AddAttachments(stack, c.Id);
			row.AddChild(stack);
		}

		// An Aura on an Equipment on a creature: each strip followed by what is on it.
		void AddAttachments(VBoxContainer stack, int hostId)
		{
			foreach (var a in attached[hostId])
			{
				stack.AddChild(
					CardViews.Attachment(a, glow(a), Preview, () => ObjectClicked(a.Id))
				);
				AddAttachments(stack, a.Id);
			}
		}
	}

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
				Card spell => (spell.Name, spell.Name),
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
			_stack.AddChild(plate);
		}
	}

	private void ShowButtons(GameState s, IReadOnlyList<GameAction> moves)
	{
		CardViews.Clear(_buttons);
		var winner = s.WinnerId();
		if (_m.IsOver)
		{
			_status.Text =
				winner == 0 ? "Draw."
				: winner == _m.HumanId ? "You win!"
				: "The AI wins.";
			AddButton("Replay this seed", () => _m = _m.Replay());
			AddButton(
				"New game",
				() => _m = NewGame(Random.Shared.Next(1, int.MaxValue), _m.Stops)
			);
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
			AddButton("Cancel  (Esc)", () => { });
			return;
		}
		if (_plays is var (title, items))
		{
			_status.Text = title;
			foreach (var (label, play) in items)
				AddButton(label, () => Play(play, 0), redraw: false, option: true).Disabled =
					play.Count == 0; // a mode that can't be chosen now: shown, greyed
			AddButton("Cancel  (Esc)", () => { });
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
		_status.Text = _m.HumanToAct
			? s.IsWaitingForChoice
				? "Your choice."
				: $"Your priority — {s.GetGame().Step}"
			: "AI is thinking…";
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

	/// <summary>A button in the action column. Through Do (clears any play in progress) unless it continues one.</summary>
	/// <param name="option">One of a list (plays, targets, a choice's options): the plain plate in a
	/// smaller font. Otherwise a main action (Pass, Attack, Submit) in the big oxblood style.</param>
	/// <param name="picked">An option of a multi-pick choice that is picked: the selected plate.</param>
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
			margin.AddThemeConstantOverride($"margin_{side}", 12);
		AddChild(margin);
		var columns = new HBoxContainer();
		columns.AddThemeConstantOverride("separation", 8);
		margin.AddChild(columns);

		// A player plate is a target too: "deal 3 damage to any target", "Attack the AI".
		var left = Column(375);
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
		foreach (var part in new Control[] { _oppLands, _oppField, _phases, _myField, _myLands })
			center.AddChild(part);
		var handRow = new ScrollContainer
		{
			CustomMinimumSize = new Vector2(0, CardViews.HandSize.Y + 12),
			VerticalScrollMode = ScrollContainer.ScrollMode.Disabled,
			// ShowNever, not Disabled: Disabled makes the row as wide as the whole hand and pushes
			// the right column off the screen. The cards overlap to fit (FitHand), so it never scrolls.
			HorizontalScrollMode = ScrollContainer.ScrollMode.ShowNever,
		};
		handRow.AddChild(_hand);
		handRow.Resized += FitHand;
		center.AddChild(handRow);
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
		var sandbox = new Button { Text = "Sandbox", FocusMode = FocusModeEnum.None };
		sandbox.Pressed += () => _sandbox.Visible = !_sandbox.Visible;
		toggles.AddChild(sandbox);
		right.AddChild(toggles);
		// Watching the AI: pause it, step it, set its pace.
		var ai = new HBoxContainer();
		foreach (var control in new Control[] { _pause, _step, _speed, sandbox })
			control.AddThemeFontSizeOverride("font_size", TextSize);
		ai.AddChild(_pause);
		ai.AddChild(_step);
		ai.AddChild(_speed);
		right.AddChild(ai);
		var logPlate = Plate(_log);
		logPlate.SizeFlagsVertical = SizeFlags.ExpandFill;
		right.AddChild(logPlate);
		columns.AddChild(right);

		// Later children draw on top: a pending choice above an open zone viewer.
		foreach (var panel in new Control[] { _viewer, _sandbox, _choice })
		{
			var overlay = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
			overlay.SetAnchorsPreset(LayoutPreset.FullRect);
			overlay.AddChild(panel);
			AddChild(overlay);
		}
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
