using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Godot;
using ImmutableGameObjects;
using KinCore;
using KinCore.Party;

namespace KinGame;

/// <summary>
/// **THE COMPANION GAME — the first playable slice (2026-09-23).** One battle: your companions on
/// the bottom row, foes on the top, a combined hand of their cards. Built beside `KinBoard` rather
/// than into it; the lane/unit game still runs from `kin_board.tscn`.
///
/// **Reads `PartyState`, decides nothing.** Where an attack lands, whether a step is legal and what
/// ending the turn costs all come from KinCore; this only draws them and forwards clicks and drops.
///
/// `-- --scenario=N` picks a scenario (0-based) at start; the buttons switch between them live.
/// </summary>
public partial class KinPartyBoard : Node2D
{
	private GameState _state;
	private int _scenario;

	/// <summary>The companion clicked last, whose move targets are lit. 0 = none.</summary>
	private int _selectedAllyId;

	private readonly KinPartyCell[] _foeCells = new KinPartyCell[PartyBattle.Spaces];
	private readonly KinPartyCell[] _allyCells = new KinPartyCell[PartyBattle.Spaces];

	private KinHandView _hand;
	private Label _title;
	private Label _subtitle;
	private Label _energy;
	private Label _hint;
	private Label _log;
	private Button _endTurn;
	private readonly List<string> _logLines = new();
	private CanvasLayer _layer;

	/// <summary>Full-rect, never takes a click: where floating numbers live so no container clips them.</summary>
	private Control _overlay;

	/// <summary>The hand card being hovered or dragged, whose owner is lit on the board. 0 = none.</summary>
	private int _focusCardId;

	/// <summary>`--focus=N` only: stands in for a hover a capture cannot make.</summary>
	private int _captureFocusId;

	public override void _Ready()
	{
		KinAnimator.LoadConfiguredSpeed();
		BuildUi();

		foreach (var arg in OS.GetCmdlineUserArgs())
		{
			if (
				arg.StartsWith("--scenario=")
				&& int.TryParse(arg["--scenario=".Length..], out var n)
			)
				_scenario = Mathf.Clamp(n, 0, PartyContent.Scenarios.Count - 1);

			// `-- --click-space=3,4` sends REAL left clicks to those spaces of your row, in order,
			// through the viewport — every mouse filter included. A capture cannot click, and a
			// shortcut that skips the click once "verified" a move no player could make.
			if (arg.StartsWith("--click-space="))
			{
				var delay = 0.6;
				foreach (var part in arg["--click-space=".Length..].Split(','))
					if (int.TryParse(part, out var space))
					{
						GetTree().CreateTimer(delay).Timeout += () => ClickSpace(space);
						delay += 0.4;
					}
			}

			// Capture-only. `--focus=2` lights the owner of the third card in hand as a hover would —
			// a capture cannot hover (synthetic motion does not drive physics picking), so this checks
			// what the highlight LOOKS like, not that hovering triggers it. `--play=2` plays that card
			// through the same path a drop takes; `--end-turn` then ends the turn.
			if (arg.StartsWith("--focus=") && int.TryParse(arg["--focus=".Length..], out var focus))
				GetTree().CreateTimer(0.5).Timeout += () => _captureFocusId = HandCard(focus);
			if (arg.StartsWith("--play=") && int.TryParse(arg["--play=".Length..], out var play))
				GetTree().CreateTimer(0.5).Timeout += () =>
					Report(TryPlay(HandCard(play), null) ?? "played");
			if (arg == "--end-turn")
				GetTree().CreateTimer(1.6).Timeout += OnEndTurn;
		}

		StartScenario(_scenario);
	}

	private int HandCard(int index) => _state.CardsIn(ZoneType.Hand).ElementAt(index).Id;

	private void ClickSpace(int space)
	{
		var at = _allyCells[space].Root.GetGlobalRect().GetCenter();
		foreach (var pressed in new[] { true, false })
			GetViewport()
				.PushInput(
					new InputEventMouseButton
					{
						ButtonIndex = MouseButton.Left,
						Pressed = pressed,
						Position = at,
						GlobalPosition = at,
					},
					true
				);
	}

	private void StartScenario(int index)
	{
		_scenario = index;
		_selectedAllyId = 0;
		_logLines.Clear();
		_state = PartyBattleFactory.Create(
			PartyContent.Scenarios[index],
			(int)GD.RandRange(1, 9999)
		);
		Render(ImmutableList<GameEvent>.Empty);
	}

	// ===== Input — every rule question goes to the engine

	private void Apply(GameAction action)
	{
		var validation = action.ValidateAdd(_state);
		if (!validation.IsValid)
		{
			Report(validation.Reason);
			return;
		}

		var (state, events) = _state.AddAction(action).ProcessAllActions();
		_state = state;
		_hint.Text = HowToPlay;
		Render(events);
	}

	private const string HowToPlay =
		"Drag a card onto your row — its owner acts. Click a companion, then a lit space, to move.";

	/// <summary>Returns null when the card played, or the engine's refusal — the hand shows it.</summary>
	private string TryPlay(int cardId, int? space)
	{
		var action = new PlayPartyCardAction { CardId = cardId, Space = space ?? -1 };
		var validation = action.ValidateAdd(_state);
		if (!validation.IsValid)
			return validation.Reason;

		Apply(action);
		return null;
	}

	/// <summary>Which of YOUR spaces a point is over. The foe row is never a drop target.</summary>
	/// <summary>A drop on EITHER row names that column — a push is naturally aimed at the foe row.</summary>
	private int? DropSpaceAt(Vector2 point)
	{
		if (SpaceAt(point) is { } mine)
			return mine;
		for (var i = 0; i < PartyBattle.Spaces; i++)
			if (_foeCells[i].Root.GetGlobalRect().HasPoint(point))
				return i;
		return null;
	}

	private int? SpaceAt(Vector2 point)
	{
		for (var i = 0; i < PartyBattle.Spaces; i++)
			if (_allyCells[i].Root.GetGlobalRect().HasPoint(point))
				return i;
		return null;
	}

	/// <summary>
	/// **Click a companion to select it; click an empty space beside it to move there.** Read here,
	/// after the GUI and after a hand card has claimed its own click, so nothing on the board has to
	/// catch the mouse. A press only, so the release that ends a card drag is never read as a click.
	/// </summary>
	public override void _UnhandledInput(InputEvent @event)
	{
		if (
			@event
			is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click
		)
			return;
		if (_state is null || Common.Cards.CardUIManager.DraggingCard is not null)
			return;
		if (SpaceAt(click.Position) is not { } space)
			return;

		GetViewport().SetInputAsHandled();

		if (_state.AllyAt(space) is { } ally)
		{
			_selectedAllyId = _selectedAllyId == ally.Id ? 0 : ally.Id;
			Render(ImmutableList<GameEvent>.Empty);
			if (_selectedAllyId != 0)
				Report(
					$"{ally.Name}: {ally.PassiveRule} "
						+ (
							ally.MoveReadyIn == 0
								? "Click a lit space to move."
								: $"Moves again in {ally.MoveReadyIn} turn{(ally.MoveReadyIn == 1 ? "" : "s")}."
						)
				);
			return;
		}

		if (_selectedAllyId == 0)
		{
			Report("Click one of your companions first, then an empty space beside it.");
			return;
		}

		var moving = _selectedAllyId;
		_selectedAllyId = 0;
		Apply(new MoveAllyAction { AllyId = moving, Space = space });
	}

	private void OnEndTurn()
	{
		_selectedAllyId = 0;
		Apply(new EndPartyTurnAction());
	}

	// ===== Rendering — a full repaint from state, every time

	/// <summary>
	/// **Lights the owner of the card under the cursor**, hovered or dragged — the playtest played a
	/// card for Bramble believing it was Pike's. Repaints the rows only, and only when the card
	/// changes, so the hand is never re-synced under a drag.
	/// </summary>
	public override void _Process(double delta)
	{
		if (_state is null)
			return;

		var ui =
			Common.Cards.CardUIManager.DraggingCard
			?? Common.Cards.CardUIManager.CurrentHoveredCard;
		var id = int.TryParse(ui?.Id, out var parsed) ? parsed : _captureFocusId;
		if (id == _focusCardId)
			return;

		_focusCardId = id;
		RenderRows();
	}

	private void Render(ImmutableList<GameEvent> events)
	{
		var party = _state.GetParty();
		RenderRows();

		_title.Text = party.IsOver
			? (party.Won ? "VICTORY" : "DEFEAT") + $" — {party.Name.ToUpperInvariant()}"
			: $"{party.Name.ToUpperInvariant()}   ·   TURN {party.TurnNumber}";

		var down = _state.Allies().Where(a => a.IsKnockedOut).Select(a => a.Name).ToList();
		_subtitle.Text =
			party.Description
			+ (
				down.Count > 0
					? $"   Knocked out: {string.Join(", ", down)} — their cards are dead."
					: ""
			);

		_energy.Text = $"ENERGY {party.Energy}/{party.MaxEnergy}";
		_endTurn.Disabled = party.IsOver;

		foreach (var line in events.Select(Describe).Where(l => l is not null))
			Log(line);

		_hand.Sync(_state.CardsIn(ZoneType.Hand).ToList(), party.Energy);
		Animate(events);
	}

	private void RenderRows()
	{
		var forecast = _state.HpLostIfTurnEndsNow();

		// Which of your spaces each foe's intent hits, and by whom — straight from the engine.
		var threats = new Dictionary<int, List<string>>();
		foreach (var foe in _state.LivingFoes())
		foreach (var space in _state.IntentTargets(foe))
		{
			if (!threats.TryGetValue(space, out var list))
				threats[space] = list = new List<string>();
			list.Add($"{foe.Name} {foe.Current.Amount}");
		}

		var selected =
			_selectedAllyId != 0
			&& _state.GetObject(_selectedAllyId) is Ally { IsKnockedOut: false } s
				? s
				: null;

		var focus =
			_focusCardId != 0
			&& _state.HasObject(_focusCardId)
			&& _state.GetObject(_focusCardId) is KinCard card
				? card
				: null;

		// **Where the card under the cursor can be dropped — asked of the ENGINE, space by space**, so
		// the lit spaces can never disagree with what a drop will do. A step lands on YOUR row; a push
		// or a swap is about where a FOE ends up, so it lights the foe row.
		var drops = new HashSet<int>();
		if (focus is not null && focus.NeedsASpace())
			for (var i = 0; i < PartyBattle.Spaces; i++)
				if (
					new PlayPartyCardAction { CardId = focus.Id, Space = i }
						.ValidateAdd(_state)
						.IsValid
				)
					drops.Add(i);
		var onFoeRow = focus is not null && !focus.Effects.Any(e => e.Template is StepAction);

		for (var i = 0; i < PartyBattle.Spaces; i++)
		{
			RenderFoe(i, onFoeRow && drops.Contains(i) ? focus : null);
			RenderAlly(
				i,
				threats.GetValueOrDefault(i),
				forecast,
				selected,
				focus,
				!onFoeRow && drops.Contains(i)
			);
		}
	}

	private void RenderFoe(int space, KinCard dropHere)
	{
		var cell = _foeCells[space];
		if (_state.FoeAt(space) is not { } foe)
		{
			if (dropHere is not null)
				cell.Show(
					"DROP HERE",
					null,
					"",
					"",
					$"{dropHere.Name.ToUpperInvariant()} it here",
					"",
					KinPalette.EmptySlot,
					KinPalette.Gold
				);
			else
				cell.ShowEmpty();
			return;
		}

		var intent = foe.Current;
		var says = intent.Kind switch
		{
			IntentType.Attack when intent.Homing => $"{intent.Name} {intent.Amount} → weakest",
			IntentType.Attack => $"{intent.Name} {intent.Amount}  ({intent.Offsets.Count} wide)",
			IntentType.Block => $"{intent.Name}: +{intent.Amount} block",
			IntentType.Move => $"{intent.Name}: step {(intent.Amount < 0 ? "left" : "right")}",
			_ => intent.Name,
		};

		var attacks = intent.Kind == IntentType.Attack;
		cell.Show(
			foe.Name.ToUpperInvariant(),
			Art(foe.Name, hostile: true),
			$"HP {foe.Hp}/{foe.MaxHp}" + (foe.Block > 0 ? $"  ·  BLOCK {foe.Block}" : ""),
			attacks ? "" : says.ToUpperInvariant(),
			dropHere is not null ? $"▼ {dropHere.Name.ToUpperInvariant()} HERE"
				: foe.OffBalance > 0 ? $"OFF-BALANCE +{foe.OffBalance}"
				: "",
			attacks ? says.ToUpperInvariant() : "",
			KinArt.EnemyGround,
			dropHere is not null ? KinPalette.Gold
				: attacks ? KinPalette.Red
				: null
		);
	}

	private void RenderAlly(
		int space,
		List<string> threats,
		ImmutableDictionary<int, int> forecast,
		Ally selected,
		KinCard focus,
		bool canDropHere
	)
	{
		var cell = _allyCells[space];
		var threatText = threats is null ? "" : "▼ " + string.Join(", ", threats);
		var canStepHere =
			selected is { MoveReadyIn: 0 } && _state.StepRefusal(selected, space) is null;

		// The card under the cursor, and who plays it.
		var actor = focus is null ? null : _state.Owner(focus);

		if (_state.AllyAt(space) is not { } ally)
		{
			if (canDropHere)
				cell.Show(
					"DROP HERE",
					null,
					"",
					"",
					$"{actor.Name} steps here",
					threatText,
					KinPalette.EmptySlot,
					KinPalette.Gold
				);
			else if (canStepHere)
				cell.Show(
					"MOVE HERE",
					null,
					"",
					"",
					"click to step",
					threatText,
					KinPalette.EmptySlot,
					KinPalette.Gold
				);
			else if (threats is not null)
				cell.Show("", null, "", "", "", threatText, KinPalette.EmptySlot, KinPalette.Red);
			else
				cell.ShowEmpty();
			return;
		}

		var loses = forecast.GetValueOrDefault(ally.Id);
		var acts = actor?.Id == ally.Id;
		cell.Show(
			ally.Name.ToUpperInvariant(),
			Art(ally.Name, hostile: false),
			// FOUR lines at most over the art — six covered Pike to the ears. Speed is not printed: its
			// only effect is the move line below it ("moves in 2"), and clicking states the rules.
			$"HP {ally.Hp}/{ally.MaxHp} · POW {ally.Power}"
				+ (ally.Block > 0 ? $" · BLOCK {ally.Block}" : ""),
			"",
			acts ? $"▲ PLAYS {focus.Name.ToUpperInvariant()}"
				: ally.MoveReadyIn == 0 ? "MOVE READY"
				: $"moves in {ally.MoveReadyIn}",
			loses > 0 ? $"▼ −{loses} HP if turn ends"
				: threats is not null ? "▼ blocked"
				: "",
			KinPalette.Companion(ally.Name),
			acts || ally.Id == selected?.Id ? KinPalette.Gold
				: loses > 0 ? KinPalette.Red
				: null
		);

		// The passive, with its live number when a card or a step has raised it this turn.
		cell.Passive =
			ally.Momentum > 0 ? $"MOMENTUM: next hit +{ally.Momentum}"
			: ally.BonusThorns > 0 ? $"THORNS {ally.TotalThorns} this turn"
			: ally.Passive;
	}

	/// <summary>A companion's figure is drawn in its own colour, matching its cards.</summary>
	private static Texture2D Art(string name, bool hostile) =>
		KinArt.Drawing(name)
		?? KinArt.Figure(
			hostile ? KinArt.ColourFor(name) : KinPalette.Companion(name).Lightened(0.45f),
			hostile
		);

	/// <summary>
	/// **What just happened, told one beat at a time** — the playtest could not see what a card
	/// did. The card's name rises off the companion that played it, then each number off the thing
	/// it happened to, staggered so the foes' turn reads in the order they acted.
	/// </summary>
	private void Animate(ImmutableList<GameEvent> events)
	{
		var beat = KinAnimator.Instant ? 0 : 0.28 / KinAnimator.Speed;
		var delay = 0.0;

		foreach (var e in events)
		{
			System.Action play = e switch
			{
				CardPlayedEvent played when _state.GetObject(played.CardId) is KinCard card => () =>
				{
					var cell = AllyCell(_state.Owner(card).Id);
					KinAnimator.Pop(cell);
					KinAnimator.Float(
						_overlay,
						cell,
						card.Name.ToUpperInvariant(),
						KinPalette.Gold
					);
				},
				FoeHitEvent hit => () =>
					Struck(_foeCells[((Foe)_state.GetObject(hit.FoeId)).Space].Root, hit.Damage),
				AllyHitEvent hit => () =>
				{
					Struck(AllyCell(hit.AllyId), hit.Damage);
					if (hit.Damage > 0)
						KinAnimator.Shake(_layer, 6f);
				},
				BlockGainedEvent block => () =>
					KinAnimator.Float(
						_overlay,
						AllyCell(block.AllyId),
						$"+{block.Amount} BLOCK",
						KinPalette.Bone
					),
				AllyMovedEvent moved => () => KinAnimator.Pop(_allyCells[moved.To].Root),
				FoeMovedEvent moved => () =>
				{
					KinAnimator.Pop(_foeCells[moved.To].Root);
					KinAnimator.Float(
						_overlay,
						_foeCells[moved.To].Root,
						moved.To < moved.From ? "◀" : "▶",
						KinPalette.Bone
					);
				},
				_ => null,
			};

			if (play is null)
				continue;

			if (delay <= 0)
				play();
			else
				GetTree().CreateTimer(delay).Timeout += play;
			delay += beat;
		}
	}

	/// <summary>A hit: the cell flashes, and the damage — or BLOCKED — rises off it.</summary>
	private void Struck(Control cell, int damage)
	{
		KinAnimator.Flash(cell, new Color(1.6f, 0.7f, 0.7f));
		KinAnimator.Float(
			_overlay,
			cell,
			damage > 0 ? $"−{damage}" : "BLOCKED",
			damage > 0 ? KinPalette.Red : KinPalette.Bone
		);
	}

	private Control AllyCell(int allyId) => _allyCells[((Ally)_state.GetObject(allyId)).Space].Root;

	private string Describe(GameEvent e) =>
		e switch
		{
			AllyHitEvent hit => $"{hit.By} hits {Who(hit.AllyId)} for {hit.Damage}"
				+ (hit.Blocked > 0 ? $" ({hit.Blocked} blocked)" : ""),
			AllyKnockedOutEvent ko => $"{Who(ko.AllyId)} is knocked out!",
			FoeHitEvent hit => $"{Who(hit.FoeId)} takes {hit.Damage}"
				+ (hit.Blocked > 0 ? $" ({hit.Blocked} blocked)" : ""),
			CardPlayedEvent played => $"Played {played.CardName}",
			FoeMovedEvent moved =>
				$"{Who(moved.FoeId)} is pushed {(moved.To < moved.From ? "left" : "right")}",
			PartyBattleEndedEvent end => end.Won
				? "Every foe is down. VICTORY."
				: "Every companion is down. DEFEAT.",
			_ => null,
		};

	private string Who(int id) => _state.GetObject(id).Name;

	private void Report(string text)
	{
		_hint.Text = text;
		KinAnimator.Pop(_hint);
	}

	private void Log(string line)
	{
		_logLines.Add(line);
		if (_logLines.Count > 6)
			_logLines.RemoveAt(0);
		_log.Text = string.Join("\n", _logLines);
	}

	// ===== Layout

	private void BuildUi()
	{
		var layer = _layer = new CanvasLayer();
		AddChild(layer);

		var ground = new ColorRect { Color = KinPalette.Navy };
		ground.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		layer.AddChild(ground);

		var margin = new MarginContainer();
		margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		margin.AddThemeConstantOverride("margin_left", 32);
		margin.AddThemeConstantOverride("margin_right", 32);
		margin.AddThemeConstantOverride("margin_top", 16);
		margin.AddThemeConstantOverride("margin_bottom", 16);
		layer.AddChild(margin);

		var column = new VBoxContainer();
		column.AddThemeConstantOverride("separation", 8);
		margin.AddChild(column);

		column.AddChild(BuildBanner());
		column.AddChild(BuildRow(_foeCells));
		column.AddChild(
			KinPalette.Text("▲ your attacks fire straight up their column", 16, KinPalette.Bone)
		);
		column.AddChild(BuildRow(_allyCells));
		column.AddChild(BuildStatus());
		column.AddChild(new Control { CustomMinimumSize = new Vector2(0, KinHandView.BandHeight) });

		// The log sits to the right of the rows, where the board has room.
		_log = KinPalette.Text("", 16, KinPalette.Bone, HorizontalAlignment.Left);
		_log.Position = new Vector2(1560, 110);
		_log.Size = new Vector2(340, 300);
		_log.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		_log.Modulate = new Color(1, 1, 1, 0.8f);
		layer.AddChild(_log);

		var canvas = GetViewportRect().Size;
		_hand = new KinHandView(
			layer,
			new Vector2(canvas.X / 2, canvas.Y - KinHandView.BandHeight / 2f),
			DropSpaceAt,
			TryPlay,
			Report
		);

		_overlay = new Control();
		_overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		layer.AddChild(_overlay);

		MakeTransparentToMouse(layer);
	}

	private Control BuildBanner()
	{
		var panel = new PanelContainer();
		panel.AddThemeStyleboxOverride("panel", KinPalette.Box(KinPalette.Slate));

		var across = new HBoxContainer();
		across.AddThemeConstantOverride("separation", 16);
		panel.AddChild(across);

		var text = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		_title = KinPalette.Text("", 30, KinPalette.Bone, HorizontalAlignment.Left);
		_subtitle = KinPalette.Text("", 18, KinPalette.Bone, HorizontalAlignment.Left);
		_subtitle.Modulate = new Color(1, 1, 1, 0.75f);
		text.AddChild(_title);
		text.AddChild(_subtitle);
		across.AddChild(text);

		for (var i = 0; i < PartyContent.Scenarios.Count; i++)
		{
			var index = i;
			var button = new Button { Text = PartyContent.Scenarios[i].Name.ToUpperInvariant() };
			button.AddThemeFontSizeOverride("font_size", 18);
			button.Pressed += () => StartScenario(index);
			across.AddChild(button);
		}

		var menu = new Button { Text = "MENU" };
		menu.AddThemeFontSizeOverride("font_size", 18);
		menu.Pressed += () => Project.GameManager.Instance.GoToMainMenu();
		across.AddChild(menu);

		return panel;
	}

	/// <summary>Esc goes back to the main menu, as the MENU button does.</summary>
	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventKey { Pressed: true, Keycode: Key.Escape })
			Project.GameManager.Instance.GoToMainMenu();
	}

	private static Control BuildRow(KinPartyCell[] cells)
	{
		var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		row.AddThemeConstantOverride("separation", 14);
		for (var i = 0; i < cells.Length; i++)
		{
			cells[i] = new KinPartyCell();
			row.AddChild(cells[i].Root);
		}
		return row;
	}

	private Control BuildStatus()
	{
		var panel = new PanelContainer();
		panel.AddThemeStyleboxOverride("panel", KinPalette.Box(KinPalette.Slate));

		var across = new HBoxContainer();
		across.AddThemeConstantOverride("separation", 24);
		panel.AddChild(across);

		_energy = KinPalette.Text("", 24, KinPalette.Gold);
		_hint = KinPalette.Text(HowToPlay, 18, KinPalette.Bone, HorizontalAlignment.Left);
		_hint.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

		_endTurn = new Button { Text = "END TURN" };
		_endTurn.AddThemeFontSizeOverride("font_size", 22);
		_endTurn.Pressed += OnEndTurn;

		across.AddChild(_energy);
		across.AddChild(_hint);
		across.AddChild(_endTurn);
		return panel;
	}

	/// <summary>Every Control but a Button steps out of the mouse's way — card hover needs it.</summary>
	private static void MakeTransparentToMouse(Node node)
	{
		foreach (var child in node.GetChildren())
		{
			if (child is Control control and not Button)
				control.MouseFilter = Control.MouseFilterEnum.Ignore;
			MakeTransparentToMouse(child);
		}
	}
}
