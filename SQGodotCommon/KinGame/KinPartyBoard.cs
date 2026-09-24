using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Godot;
using ImmutableGameObjects;
using KinCore;
using KinCore.Party;

namespace KinGame;

/// <summary>
/// **THE COMPANION GAME — AUTO-BATTLE v1 (2026-09-24).** One battle: your monsters on the bottom
/// row, foes on the top. Every creature plays its own telegraphed cycle at the end of the turn, in
/// Speed order; your hand is the trainer's, played ON a monster or a foe. Built beside `KinBoard`;
/// the lane/unit game still runs from `kin_board.tscn`.
///
/// **Reads `PartyState`, decides nothing.** Where an attack lands, who acts when, whether a step or
/// a drop is legal and what ending the turn costs all come from KinCore.
///
/// `-- --scenario=N` picks a scenario (0-based) at start; the buttons switch between them live.
/// </summary>
public partial class KinPartyBoard : Node2D
{
	private GameState _state;
	private int _scenario;

	/// <summary>
	/// **Set by the main menu's PRACTICE** before the scene loads: the fixed scenarios, with their
	/// buttons, instead of a run. Any `--scenario=` flag means practice too. Read once, then cleared.
	/// </summary>
	public static bool Practice { get; set; }

	private bool _practice;

	/// <summary>THE RUN (KinJam.md). Null in practice.</summary>
	private PartyRun _run;

	private KinPartyRunScreens _screens;
	private readonly List<Button> _practiceButtons = new();

	/// <summary>The monster clicked last, whose step targets are lit. 0 = none.</summary>
	private int _selectedAllyId;

	private readonly KinPartyCell[] _foeCells = new KinPartyCell[PartyBattle.Spaces];
	private readonly KinPartyCell[] _allyCells = new KinPartyCell[PartyBattle.Spaces];

	private KinHandView _hand;
	private Label _title;
	private Label _subtitle;
	private Label _energy;

	/// <summary>The Snare item: press it, then click a foe. Armed = the next foe click throws it.</summary>
	private Button _snare;

	private bool _snaring;
	private Label _hint;
	private Label _log;
	private Button _endTurn;
	private readonly List<string> _logLines = new();
	private CanvasLayer _layer;

	/// <summary>Full-rect, never takes a click: where floating numbers live so no container clips them.</summary>
	private Control _overlay;

	/// <summary>The hand card being hovered or dragged, whose legal drops are lit. 0 = none.</summary>
	private int _focusCardId;

	/// <summary>`--focus=N` only: stands in for a hover a capture cannot make.</summary>
	private int _captureFocusId;

	private KinPartyInspector _inspector;

	/// <summary>The creature the inspector shows, and the state it was drawn from. 0 = hidden.</summary>
	private int _inspectedId;

	private GameState _inspectedState;

	/// <summary>`--inspect=N` only: hovers a cell (0–4 yours, 5–9 the foe's) for a capture.</summary>
	private int? _captureInspect;

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
			{
				_scenario = Mathf.Clamp(n, 0, PartyContent.Scenarios.Count - 1);
				_practice = true;
			}

			// Capture-only, for the run's screens: `--starter=1` skips choosing (Roster[1]),
			// `--screen=between|over` then ends the first battle through `DebugEndBattle` so the
			// screen after it can be captured without playing a whole battle.
			if (
				arg.StartsWith("--starter=") && int.TryParse(arg["--starter=".Length..], out var st)
			)
				_captureStarter = st;
			if (arg.StartsWith("--screen="))
				_captureScreen = arg["--screen=".Length..];

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

			// Capture-only. `--focus=2` lights where the third card in hand can go, as a hover would —
			// a capture cannot hover (synthetic motion does not drive physics picking), so this checks
			// what the highlight LOOKS like, not that hovering triggers it. `--play=2` plays that card
			// on the first space that takes it (`--play=2@3` your space 3, `--play=2@f3` the foe's)
			// through the same path a drop takes; `--end-turn` then ends the turn.
			if (arg.StartsWith("--focus=") && int.TryParse(arg["--focus=".Length..], out var focus))
				GetTree().CreateTimer(0.5).Timeout += () => _captureFocusId = HandCard(focus);
			if (arg.StartsWith("--play="))
			{
				var parts = arg["--play=".Length..].Split('@');
				if (int.TryParse(parts[0], out var play))
					GetTree().CreateTimer(0.5).Timeout += () =>
					{
						var card = HandCard(play);
						int? at =
							parts.Length < 2 ? FirstDrop(card)
							: parts[1].StartsWith('f')
								? PartyBattle.Spaces + int.Parse(parts[1][1..])
							: int.Parse(parts[1]);
						Report(TryPlay(card, at) ?? "played");
					};
			}
			if (
				arg.StartsWith("--inspect=")
				&& int.TryParse(arg["--inspect=".Length..], out var inspect)
			)
				_captureInspect = inspect;
			// Capture-only: `--snare=3` weakens the foe in space 3 to catchable (`DebugWeaken`) and
			// arms the Snare, to capture what an armed Snare lights.
			if (arg.StartsWith("--snare=") && int.TryParse(arg["--snare=".Length..], out var weak))
				GetTree().CreateTimer(0.4).Timeout += () =>
				{
					_state = _state.DebugWeaken(weak);
					Render(ImmutableList<GameEvent>.Empty);
					OnSnare();
				};
			if (arg == "--end-turn")
				GetTree().CreateTimer(1.6).Timeout += OnEndTurn;
		}

		_practice |= Practice;
		Practice = false;
		foreach (var button in _practiceButtons)
			button.Visible = _practice;

		if (_practice)
			StartScenario(_scenario);
		else if (_captureStarter is { } starter)
		{
			// Capture-only: `--screen=areas` leaves the town; `find|deep|gym` jump along the first
			// area's trail; `between|over` fight its first stop and end it through `DebugEndBattle`.
			BeginRun(PartyContent.Roster[starter]);
			if (_captureScreen is { } screen)
				GetTree().CreateTimer(0.5).Timeout += () =>
				{
					Change(r => r.LeaveTown());
					if (screen == "areas")
						return;
					if (screen is "find" or "deep" or "gym")
					{
						Change(r => r.ChooseArea(0) with { StopIndex = screen == "find" ? 2 : 3 });
						if (screen == "gym")
							Change(r => r.SkipDeep());
						return;
					}
					Change(r => r.ChooseArea(0));
					_state = _state.DebugEndBattle(won: screen == "between");
					BattleOver();
				};
		}
		else
			ShowStarters();
	}

	private int? _captureStarter;
	private string _captureScreen;

	// ===== THE RUN

	private void ShowStarters()
	{
		_hand.SetVisible(false);
		_screens.ShowStarters(BeginRun);
	}

	private void BeginRun(PartyCompanion starter)
	{
		_run = PartyRun.Start(starter, (int)GD.RandRange(1, 9999));
		Continue();
	}

	/// <summary>A change to the run from a screen, then whatever comes next.</summary>
	private void Change(System.Func<PartyRun, PartyRun> change)
	{
		_run = change(_run);
		Continue();
	}

	/// <summary>
	/// **Where the run is decides what shows** (`PartyRun.Phase`): the town, the choice of area, the
	/// next stop on the trail — a fight, a find or the deeper path — the gym, or the end.
	/// </summary>
	private void Continue()
	{
		_hand.SetVisible(false);
		switch (_run.Phase)
		{
			case RunPhase.Town:
				_screens.ShowTown(_run, Change);
				break;
			case RunPhase.ChooseArea:
				_screens.ShowAreas(_run, area => Change(r => r.ChooseArea(area)));
				break;
			case RunPhase.Trail when _run.CurrentStop.Kind == StopKind.Battle:
				NextBattle();
				break;
			case RunPhase.Trail when _run.CurrentStop.Kind == StopKind.Find:
				_screens.ShowFind(_run, () => Change(r => r.TakeFind()));
				break;
			case RunPhase.Trail:
				_screens.ShowDeep(_run, NextBattle, () => Change(r => r.SkipDeep()));
				break;
			case RunPhase.Gym:
				_screens.ShowGym(_run, NextBattle);
				break;
			default:
				_screens.ShowOver(
					_run,
					ShowStarters,
					() => Project.GameManager.Instance.GoToMainMenu()
				);
				break;
		}
	}

	private void NextBattle()
	{
		_screens.Hide();
		_hand.SetVisible(true);
		_selectedAllyId = 0;
		_logLines.Clear();
		_state = _run.StartBattle();
		Render(ImmutableList<GameEvent>.Empty);
	}

	/// <summary>
	/// **A run battle has ended: read it into the run and show what comes next.** Called after a
	/// pause so the last blows finish animating before the screen covers them.
	/// </summary>
	private void BattleOver()
	{
		if (_run is null || _screens.IsShowing)
			return;

		var beaten = _state.GetParty().Name;
		(_run, var report) = _run.AfterBattle(_state);
		_hand.SetVisible(false);

		if (_run.IsOver)
			Continue();
		else
			ShowBetween(report, beaten);
	}

	/// <summary>Redrawn after every bench swap, so the team shown is always the team that fights.</summary>
	private void ShowBetween(RunReport report, string beaten) =>
		_screens.ShowBetween(
			_run,
			report,
			beaten,
			reward => Change(r => r.Take(reward)),
			Continue,
			(team, bench) =>
			{
				_run = _run.Swap(team, bench);
				ShowBetween(report, beaten);
			}
		);

	private int HandCard(int index) => _state.CardsIn(ZoneType.Hand).ElementAt(index).Id;

	/// <summary>The first drop the engine accepts for this card, in the board's drop numbering.</summary>
	private int? FirstDrop(int cardId) =>
		Enumerable
			.Range(0, PartyBattle.Spaces * 2)
			.Cast<int?>()
			.FirstOrDefault(d => Play(cardId, d.Value).ValidateAdd(_state).IsValid);

	/// <summary>
	/// **A drop, in the board's numbering: 0–4 is your row, 5–9 the foe's.** The hand only passes an
	/// int, so the board folds the row into it and unfolds it here.
	/// </summary>
	private static PlayPartyCardAction Play(int cardId, int drop) =>
		new()
		{
			CardId = cardId,
			Space = drop % PartyBattle.Spaces,
			FoeRow = drop >= PartyBattle.Spaces,
		};

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

		if (_run is not null && _state.GetParty().IsOver)
			GetTree().CreateTimer(1.4).Timeout += BattleOver;
	}

	private const string HowToPlay =
		"Drag a card onto a monster (or a foe). Click a monster, then a lit space, to step. "
		+ "Everyone acts when you END TURN, in the order shown.";

	/// <summary>The cell the last card was dropped on — the card's name rises off it.</summary>
	private Control _lastDrop;

	/// <summary>Returns null when the card played, or the engine's refusal — the hand shows it.</summary>
	private string TryPlay(int cardId, int? drop)
	{
		if (drop is null)
			return "Drop it on a monster or a foe";

		var action = Play(cardId, drop.Value);
		var validation = action.ValidateAdd(_state);
		if (!validation.IsValid)
			return validation.Reason;

		_lastDrop = (action.FoeRow ? _foeCells : _allyCells)[action.Space].Root;
		Apply(action);
		return null;
	}

	/// <summary>Which space of EITHER row a point is over, in the drop numbering (foe row = 5–9).</summary>
	private int? DropSpaceAt(Vector2 point)
	{
		if (SpaceAt(point) is { } mine)
			return mine;
		for (var i = 0; i < PartyBattle.Spaces; i++)
			if (_foeCells[i].Root.GetGlobalRect().HasPoint(point))
				return PartyBattle.Spaces + i;
		return null;
	}

	private int? FoeSpaceAt(Vector2 point)
	{
		for (var i = 0; i < PartyBattle.Spaces; i++)
			if (_foeCells[i].Root.GetGlobalRect().HasPoint(point))
				return i;
		return null;
	}

	private void OnSnare()
	{
		_selectedAllyId = 0;
		_snaring = !_snaring;
		Report(
			_snaring
				? "Click a lit foe to catch it — a third of its HP or less. Click anywhere else to put the Snare away."
				: HowToPlay
		);
		RenderRows();
	}

	private int? SpaceAt(Vector2 point)
	{
		for (var i = 0; i < PartyBattle.Spaces; i++)
			if (_allyCells[i].Root.GetGlobalRect().HasPoint(point))
				return i;
		return null;
	}

	/// <summary>
	/// **Click a monster to select it; click a space beside it to step there** — onto an ally, and the
	/// two swap. Read here, after the GUI and after a hand card has claimed its own click, so nothing
	/// on the board has to catch the mouse. A press only, so the release that ends a card drag is
	/// never read as a click.
	/// </summary>
	public override void _UnhandledInput(InputEvent @event)
	{
		if (
			@event
			is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click
		)
			return;
		if (
			_state is null
			|| _screens.IsShowing
			|| Common.Cards.CardUIManager.DraggingCard is not null
		)
			return;

		// **An armed Snare takes the next click**: a foe is a throw, anywhere else puts it away.
		if (_snaring)
		{
			GetViewport().SetInputAsHandled();
			_snaring = false;
			if (FoeSpaceAt(click.Position) is { } at && _state.FoeAt(at) is { } target)
				Apply(new UseSnareAction { FoeId = target.Id });
			else
			{
				_hint.Text = HowToPlay;
				RenderRows();
			}
			return;
		}

		if (SpaceAt(click.Position) is not { } space)
			return;

		GetViewport().SetInputAsHandled();

		if (
			Selected() is { } moving
			&& space != moving.Space
			&& _state.StepRefusal(moving, space) is null
		)
		{
			_selectedAllyId = 0;
			Apply(new MoveAllyAction { AllyId = moving.Id, Space = space });
			return;
		}

		if (_state.AllyAt(space) is { } ally)
		{
			_selectedAllyId = _selectedAllyId == ally.Id ? 0 : ally.Id;
			Render(ImmutableList<GameEvent>.Empty);
			if (_selectedAllyId != 0)
				Report(
					$"{ally.Name}: {ally.PassiveRule} "
						+ (
							ally.StepsLeft > 0
								? "Click a lit space to step — onto an ally to swap."
								: "It has stepped this turn."
						)
				);
			return;
		}

		Report(
			Selected() is { } s
				? _state.StepRefusal(s, space)
				: "Click one of your monsters first, then a space beside it."
		);
	}

	private Ally Selected() =>
		_selectedAllyId != 0 && _state.GetObject(_selectedAllyId) is Ally { IsKnockedOut: false } a
			? a
			: null;

	private void OnEndTurn()
	{
		_selectedAllyId = 0;
		_snaring = false;
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
		UpdateInspector(cardInPlay: ui is not null);

		var id = int.TryParse(ui?.Id, out var parsed) ? parsed : _captureFocusId;
		if (id == _focusCardId)
			return;

		_focusCardId = id;
		RenderRows();
	}

	/// <summary>
	/// **The creature under the mouse gets the inspector** — its stats, passive and whole cycle. Not
	/// while a card is hovered or dragged (the lit drops matter more then) or a run screen shows.
	/// Redrawn only when the creature or the state changes.
	/// </summary>
	private void UpdateInspector(bool cardInPlay)
	{
		_inspector.Fit();

		var hovered =
			cardInPlay || _screens.IsShowing
				? null
				: CreatureAt(GetViewport().GetMousePosition())
					?? (_captureInspect is { } n ? CreatureInCell(n) : null);

		if (hovered is null)
		{
			_inspector.Hide();
			_inspectedId = 0;
			return;
		}
		if (hovered.Value.Creature.Id == _inspectedId && _inspectedState == _state)
			return;

		var (creature, cell) = hovered.Value;
		_inspectedId = creature.Id;
		_inspectedState = _state;
		var order = _state.ActingOrder().FindIndex(c => c.Id == creature.Id) + 1;
		_inspector.Show(creature, order, cell.GetGlobalRect(), GetViewportRect().Size);
	}

	private (Creature Creature, Control Cell)? CreatureAt(Vector2 point)
	{
		for (var d = 0; d < PartyBattle.Spaces * 2; d++)
			if (CreatureInCell(d) is { } found && found.Cell.GetGlobalRect().HasPoint(point))
				return found;
		return null;
	}

	/// <summary>The creature in a cell, in the drop numbering: 0–4 your row, 5–9 the foe's.</summary>
	private (Creature Creature, Control Cell)? CreatureInCell(int d)
	{
		var space = d % PartyBattle.Spaces;
		Creature creature = d < PartyBattle.Spaces ? _state.AllyAt(space) : _state.FoeAt(space);
		return creature is null
			? null
			: (creature, (d < PartyBattle.Spaces ? _allyCells : _foeCells)[space].Root);
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
			+ (down.Count > 0 ? $"   Knocked out: {string.Join(", ", down)}." : "");

		_energy.Text = $"ENERGY {party.Energy}/{party.MaxEnergy}";
		_snare.Text = $"SNARE ×{party.Snares}";
		_snare.Disabled = party.IsOver || party.Snares == 0;
		_endTurn.Disabled = party.IsOver;

		foreach (var line in events.Select(Describe).Where(l => l is not null))
			Log(line);

		_hand.Sync(_state.CardsIn(ZoneType.Hand).ToList(), party.Energy);
		Animate(events);
	}

	private void RenderRows()
	{
		var forecast = _state.HpLostIfTurnEndsNow();

		// **The order badge**: who acts when at the end of the turn, straight from the engine.
		var order = _state
			.ActingOrder()
			.Select((c, i) => (c.Id, i))
			.ToDictionary(p => p.Id, p => p.i + 1);

		// Which of your spaces each foe's move hits, and by whom — straight from the engine.
		var threats = new Dictionary<int, List<string>>();
		foreach (var foe in _state.LivingFoes().Where(f => !f.Staggered))
		foreach (var space in _state.IntentTargets(foe))
		{
			if (!threats.TryGetValue(space, out var list))
				threats[space] = list = new List<string>();
			list.Add($"{foe.Name} {foe.Current.Amount}");
		}

		var focus =
			_focusCardId != 0
			&& _state.HasObject(_focusCardId)
			&& _state.GetObject(_focusCardId) is KinCard card
				? card
				: null;

		// **Where the card under the cursor can be dropped — asked of the ENGINE, space by space, on
		// both rows**, so the lit spaces can never disagree with what a drop will do.
		var drops = new HashSet<int>();
		if (focus is not null)
			for (var d = 0; d < PartyBattle.Spaces * 2; d++)
				if (Play(focus.Id, d).ValidateAdd(_state).IsValid)
					drops.Add(d);

		for (var i = 0; i < PartyBattle.Spaces; i++)
		{
			RenderFoe(i, drops.Contains(PartyBattle.Spaces + i) ? focus : null, order, forecast);
			RenderAlly(
				i,
				threats.GetValueOrDefault(i),
				forecast,
				order,
				drops.Contains(i) ? focus : null
			);
		}
	}

	private void RenderFoe(
		int space,
		KinCard dropHere,
		Dictionary<int, int> order,
		ImmutableDictionary<int, int> forecast
	)
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
					$"{dropHere.Name.ToUpperInvariant()} a foe in here",
					"",
					KinPalette.EmptySlot,
					KinPalette.Gold
				);
			else
				cell.ShowEmpty();
			return;
		}

		var catchable = foe.Catchable && foe.Hp <= foe.CatchAt();
		var throwHere = _snaring && _state.CatchRefusal(foe) is null;

		var intent = foe.Current;
		var says = foe.Staggered
			? "STAGGERED — loses this move"
			: KinPartyCell.Says(intent, intent.Amount).ToUpperInvariant();
		var attacks = intent.Kind == IntentType.Attack && !foe.Staggered;
		var loses = forecast.GetValueOrDefault(foe.Id);

		cell.Show(
			$"{order.GetValueOrDefault(foe.Id)} · {foe.Name.ToUpperInvariant()}",
			Art(foe.Name, hostile: true),
			$"HP {foe.Hp}/{foe.MaxHp}"
				+ (foe.Block > 0 ? $" · BLOCK {foe.Block}" : "")
				+ (catchable ? " · ◆ CATCH" : ""),
			attacks ? "" : says,
			throwHere ? "◆ SNARE IT HERE"
				: dropHere is not null ? $"▼ {dropHere.Name.ToUpperInvariant()} HERE"
				: loses > 0
					? $"▲ −{loses} HP if turn ends"
						+ (foe.OffBalance > 0 ? $" (off-balance +{foe.OffBalance})" : "")
				: foe.OffBalance > 0 ? $"OFF-BALANCE +{foe.OffBalance}"
				: "",
			attacks ? says : "",
			KinArt.EnemyGround,
			dropHere is not null || throwHere ? KinPalette.Gold
				: attacks ? KinPalette.Red
				: null
		);
	}

	private void RenderAlly(
		int space,
		List<string> threats,
		ImmutableDictionary<int, int> forecast,
		Dictionary<int, int> order,
		KinCard dropHere
	)
	{
		var cell = _allyCells[space];
		var threatText = threats is null ? "" : "▼ " + string.Join(", ", threats);
		var selected = Selected();
		var canStepHere =
			selected is not null
			&& selected.Space != space
			&& _state.StepRefusal(selected, space) is null;

		if (_state.AllyAt(space) is not { } ally)
		{
			if (canStepHere)
				cell.Show(
					"STEP HERE",
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
		var next = ally.Current;
		cell.Show(
			(ally.HasActed ? "" : $"{order.GetValueOrDefault(ally.Id)} · ")
				+ ally.Name.ToUpperInvariant(),
			Art(ally.Name, hostile: false),
			// FOUR lines at most over the art — six covered Pike to the ears.
			$"HP {ally.Hp}/{ally.MaxHp} · POW {ally.Power + ally.BonusPower}"
				+ (ally.Block > 0 ? $" · BLOCK {ally.Block}" : ""),
			ally.HasActed
				? "ACTED THIS TURN"
				: "▲ "
					+ KinPartyCell
						.Says(
							next,
							next.Kind == IntentType.Attack
								? ally.AttackFor(next.Amount)
								: next.Amount
						)
						.ToUpperInvariant(),
			dropHere is not null ? $"▲ {dropHere.Name.ToUpperInvariant()} HERE"
				: canStepHere ? "▲ SWAP HERE"
				: "",
			loses > 0 ? $"▼ −{loses} HP if turn ends"
				: threats is not null ? "▼ blocked"
				: "",
			KinPalette.Companion(ally.Name),
			dropHere is not null || canStepHere || ally.Id == selected?.Id ? KinPalette.Gold
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
				CardPlayedEvent played when _lastDrop is { } cell => () =>
				{
					KinAnimator.Pop(cell);
					KinAnimator.Float(
						_overlay,
						cell,
						played.CardName.ToUpperInvariant(),
						KinPalette.Gold
					);
				},
				FoeCaughtEvent caught => () =>
				{
					var cell = _foeCells[((Foe)_state.GetObject(caught.FoeId)).Space].Root;
					KinAnimator.Pop(cell);
					KinAnimator.Float(_overlay, cell, "CAUGHT!", KinPalette.Gold);
				},
				FoeStaggeredEvent staggered => () =>
					KinAnimator.Float(
						_overlay,
						_foeCells[((Foe)_state.GetObject(staggered.FoeId)).Space].Root,
						"STAGGERED",
						KinPalette.Bone
					),
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
			FoeStaggeredEvent staggered => $"{Who(staggered.FoeId)} is staggered",
			FoeCaughtEvent caught => $"Caught the {Who(caught.FoeId)}!",
			PartyBattleEndedEvent end => end.Won
				? "Every foe is down. VICTORY."
				: "Every monster is down. DEFEAT.",
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

		// AFTER the pass above, which would switch its click-blocking ground off.
		_screens = new KinPartyRunScreens(layer);
		_inspector = new KinPartyInspector(layer);
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
			_practiceButtons.Add(button);
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

		_snare = new Button
		{
			Text = "SNARE",
			TooltipText = "Catch a foe at a third of its HP or less. Costs 1 energy.",
		};
		_snare.AddThemeFontSizeOverride("font_size", 22);
		_snare.Pressed += OnSnare;

		across.AddChild(_energy);
		across.AddChild(_snare);
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
