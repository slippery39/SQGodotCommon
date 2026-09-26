using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Godot;
using ImmutableGameObjects;
using KinCore;
using KinCore.Party;

namespace KinGame;

/// <summary>
/// **THE COMPANION GAME — THE RELAY (2026-09-25).** One battle: your line and theirs facing each
/// other on the field (`KinRelayField`), the fronts meeting in the middle. At END TURN the lines act
/// in steps from the back, both sides at once; your hand is the trainer's, played ON a monster or a
/// foe. Built beside `KinBoard`; the lane/unit game still runs from `kin_board.tscn`.
///
/// **Reads `PartyState`, decides nothing.** Who a move lands on, who acts when, whether a drop is
/// legal and what ending the turn costs all come from KinCore.
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

	/// <summary>The practice scenarios, as one dropdown: a row of buttons pushed END TURN off screen at seven.</summary>
	private OptionButton _practicePicker;

	/// <summary>The monster clicked last — its rule is shown; in deploy, the next place clicked takes it. 0 = none.</summary>
	private int _selectedAllyId;

	/// <summary>DEPLOY: the monster pressed and being dragged to a new place. 0 = none.</summary>
	private int _heldId;

	private KinRelayField _field;

	private KinHandView _hand;

	/// <summary>MTG's choice panel, reused as it is (see <see cref="Settle"/>).</summary>
	private MtgGame.ChoicePanel _choice;
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
								? PartyBattle.MaxLine + int.Parse(parts[1][1..])
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
			// Capture-only: `--fight` presses FIGHT before any `--play`, `--focus` or `--snare`
			// (practice scenarios open deploying, and deploy refuses them all).
			if (arg == "--fight")
				GetTree().CreateTimer(0.3).Timeout += OnEndTurn;
		}

		_practice |= Practice;
		Practice = false;
		_practicePicker.Visible = _practice;

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
					if (screen is "find" or "deep" or "gym" or "gymfight")
					{
						Change(r => r.ChooseArea(0) with { StopIndex = screen == "find" ? 2 : 3 });
						if (screen.StartsWith("gym"))
							Change(r => r.SkipDeep());
						if (screen == "gymfight")
							NextBattle();
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
			.Range(0, PartyBattle.MaxLine * 2)
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
			Space = drop % PartyBattle.MaxLine,
			FoeRow = drop >= PartyBattle.MaxLine,
		};

	private void ClickSpace(int space)
	{
		var at = _field.CentreOf(space);
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
		_practicePicker.Selected = index;
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
		Settle(state, events);
	}

	/// <summary>
	/// **After the engine runs: render, or ask.** A card that pauses on a choice ("discard a card")
	/// shows MTG's `ChoicePanel` — it depends only on the engine's `ChoiceOption`, so KIN uses it
	/// as it is. Its backdrop blocks the board until the choice is answered.
	/// </summary>
	private void Settle(GameState state, ImmutableList<GameEvent> events)
	{
		_state = state;
		_hint.Text = HowToPlay;
		Render(events);

		if (_state.GetPendingChoice() is { } choice)
		{
			_choice.ShowChoice(choice.Prompt, choice.Options, choice.MinChoices, choice.MaxChoices);
			return;
		}

		if (_run is not null && _state.GetParty().IsOver)
			GetTree().CreateTimer(1.4).Timeout += BattleOver;
	}

	private void OnChoiceConfirmed(ImmutableList<int> chosen)
	{
		var (state, events) = _state.ResolveChoice(chosen);
		Settle(state, events);
	}

	private const string HowToPlay =
		"Drag a card onto a monster (or a foe). At END TURN the lines act from the back, both sides "
		+ "at once — the fronts clash last.";

	/// <summary>The cell the last card was dropped on — the card's name rises off it.</summary>
	private Control _lastDrop;

	/// <summary>Returns null when the card played, or the engine's refusal — the hand shows it.</summary>
	private string TryPlay(int cardId, int? drop)
	{
		// Nowhere takes it: say the ENGINE's reason (in deploy, "order your line first"), not a guess.
		if (drop is null)
			return Play(cardId, 0).ValidateAdd(_state).Reason ?? "Drop it on a monster or a foe";

		var action = Play(cardId, drop.Value);
		var validation = action.ValidateAdd(_state);
		if (!validation.IsValid)
			return validation.Reason;

		_lastDrop = _field.At(_state, drop.Value);
		Apply(action);
		return null;
	}

	/// <summary>Which place of EITHER line a point is over, in the drop numbering (theirs = 5–9).</summary>
	private int? DropSpaceAt(Vector2 point) => _field.DropAt(point);

	private int? FoeSpaceAt(Vector2 point) =>
		_field.DropAt(point) is { } d && d >= PartyBattle.MaxLine ? d - PartyBattle.MaxLine : null;

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

	private int? SpaceAt(Vector2 point) =>
		_field.DropAt(point) is { } d && d < PartyBattle.MaxLine ? d : null;

	/// <summary>
	/// **Click a monster to read its rule. In DEPLOY, press one and drag it to a place in your line**
	/// — or click it, then the place (a capture cannot drag). Read here, after the GUI and after a hand
	/// card has claimed its own click, so nothing on the board has to catch the mouse.
	/// </summary>
	public override void _UnhandledInput(InputEvent @event)
	{
		if (
			_state is null
			|| _screens.IsShowing
			|| Common.Cards.CardUIManager.DraggingCard is not null
		)
			return;

		if (_heldId != 0)
		{
			Carry(@event);
			return;
		}

		if (
			@event
			is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click
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

		// DEPLOY (R2), by clicks: a monster picked, then the place it should stand.
		if (
			_state.GetParty().Deploying
			&& Selected() is { } placing
			&& space != placing.Position
			&& space < _state.LivingAllies().Count()
		)
		{
			_selectedAllyId = 0;
			Apply(new DeployMoveAction { AllyId = placing.Id, To = space });
			return;
		}

		if (_state.AllyAt(space) is { } ally)
		{
			// Picked up: the release decides — another place moves it, the same place is a click.
			if (_state.GetParty().Deploying)
			{
				_heldId = ally.Id;
				RenderRows();
				return;
			}

			_selectedAllyId = _selectedAllyId == ally.Id ? 0 : ally.Id;
			Render(ImmutableList<GameEvent>.Empty);
			if (_selectedAllyId != 0)
				Report($"{ally.Name}: {ally.PassiveRule}");
			return;
		}

		Report("Click one of your monsters to read it.");
	}

	/// <summary>DEPLOY: the held monster follows the mouse; let go over a place in your line, it goes there.</summary>
	private void Carry(InputEvent @event)
	{
		if (@event is InputEventMouseMotion motion)
		{
			_field.Follow(_heldId, motion.Position);
			return;
		}
		if (
			@event is not InputEventMouseButton { Pressed: false, ButtonIndex: MouseButton.Left } up
		)
			return;

		GetViewport().SetInputAsHandled();
		var held = (Ally)_state.GetObject(_heldId);
		_heldId = 0;
		if (
			SpaceAt(up.Position) is { } to
			&& to != held.Position
			&& to < _state.LivingAllies().Count()
		)
		{
			_selectedAllyId = 0;
			Apply(new DeployMoveAction { AllyId = held.Id, To = to });
			return;
		}

		_selectedAllyId = _selectedAllyId == held.Id ? 0 : held.Id;
		RenderRows();
		Report(
			_selectedAllyId != 0
				? $"{held.Name}: {held.PassiveRule} Click a place in your line to move it there."
				: HowToPlay
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
		Apply(_state.GetParty().Deploying ? new BeginFightAction() : new EndPartyTurnAction());
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
				: _field.CreatureAt(_state, GetViewport().GetMousePosition())
					?? (
						_captureInspect is { } n && _field.CreatureIn(_state, n) is { } c
							? (c, _field.ViewOf(c.Id))
							: null
					);

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

	private void Render(ImmutableList<GameEvent> events)
	{
		var party = _state.GetParty();
		RenderRows(settleAfter: null);

		_title.Text = party.IsOver
			? (party.Won ? "VICTORY" : "DEFEAT") + $" — {party.Name.ToUpperInvariant()}"
			: $"{party.Name.ToUpperInvariant()}   ·   TURN {party.TurnNumber}";

		var down = _state.Allies().Where(a => a.IsKnockedOut).Select(a => a.Name).ToList();
		var bench = _state.BenchedAllies().Select(a => $"{a.Name} {a.Hp}/{a.MaxHp}").ToList();
		_subtitle.Text =
			party.Description
			+ (down.Count > 0 ? $"   Knocked out: {string.Join(", ", down)}." : "")
			+ (bench.Count > 0 ? $"   Bench: {string.Join(", ", bench)}." : "");

		// Borrowed energy (Surge) is a cost you pay later — it must be visible now.
		_energy.Text =
			$"ENERGY {party.Energy}/{party.MaxEnergy}"
			+ (party.EnergyDebt > 0 ? $"  −{party.EnergyDebt} NEXT TURN" : "");
		_snare.Text = $"SNARE ×{party.Snares}";
		_snare.Disabled = party.IsOver || party.Snares == 0;
		_endTurn.Disabled = party.IsOver;
		_endTurn.Text = party.Deploying ? "FIGHT" : "END TURN";
		if (party.Deploying)
			_hint.Text =
				"DEPLOY: drag your monsters into order — the front stands nearest the middle. Then FIGHT.";

		foreach (var line in events.Select(Describe).Where(l => l is not null))
			Log(line);

		// The badge shows what the card costs NOW (`CostOf`: Scrap Hammer after discards).
		_hand.Sync(
			[.. _state.CardsIn(ZoneType.Hand).Select(c => c with { Cost = _state.CostOf(c) })],
			party.Energy
		);
		_field.Settle(Animate(events));
	}

	/// <summary>
	/// **The field, drawn from state.** `settleAfter` null leaves the line where it stands (Render
	/// slides it after the turn's blows); otherwise the line settles after that long.
	/// </summary>
	private void RenderRows(double? settleAfter = 0)
	{
		// **The order badge**: who acts when at the end of the turn, straight from the engine.
		var order = _state
			.ActingOrder()
			.Select((c, i) => (c.Id, i))
			.ToDictionary(p => p.Id, p => p.i + 1);

		var focus =
			_focusCardId != 0
			&& _state.HasObject(_focusCardId)
			&& _state.GetObject(_focusCardId) is KinCard card
				? card
				: null;

		// **Where the card under the cursor can be dropped — asked of the ENGINE, place by place, on
		// both lines**, so the lit places can never disagree with what a drop will do.
		var drops = new HashSet<int>();
		if (focus is not null)
			for (var d = 0; d < PartyBattle.MaxLine * 2; d++)
				if (Play(focus.Id, d).ValidateAdd(_state).IsValid)
					drops.Add(d);

		_field.Render(
			_state,
			new FieldContext(
				_state.ForecastIfTurnEndsNow().Hp,
				order,
				drops,
				focus,
				_snaring,
				_selectedAllyId,
				_heldId
			)
		);
		if (settleAfter is { } delay)
			_field.Settle(delay);
	}

	/// <summary>
	/// **What just happened, told one beat at a time** — the playtest could not see what a card
	/// did. The card's name rises off the companion that played it, then each number off the thing
	/// it happened to, staggered so the foes' turn reads in the order they acted.
	/// </summary>
	/// <returns>How long the beats run — the line closes up after them.</returns>
	private double Animate(ImmutableList<GameEvent> events)
	{
		var beat = KinAnimator.Instant ? 0 : 0.28 / KinAnimator.Speed;
		var delay = 0.0;
		var swinging = 0;

		foreach (var e in events)
		{
			// A blow lunges its attacker once, however many it lands on (a Sweep hits them all).
			var attacker = e switch
			{
				FoeHitEvent { AttackerId: > 0 } f => f.AttackerId,
				AllyHitEvent { AttackerId: > 0 } a => a.AttackerId,
				_ => 0,
			};
			var lunge = attacker != 0 && attacker != swinging;
			if (attacker != 0)
				swinging = attacker;

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
					var cell = _field.ViewOf(caught.FoeId);
					KinAnimator.Pop(cell);
					KinAnimator.Float(_overlay, cell, "CAUGHT!", KinPalette.Gold);
				},
				CardStolenEvent stolen => () =>
					KinAnimator.Float(
						_overlay,
						_field.ViewOf(stolen.FoeId),
						$"STOLE {stolen.CardName.ToUpperInvariant()}",
						KinPalette.Red
					),
				FoeStaggeredEvent staggered => () =>
					KinAnimator.Float(
						_overlay,
						_field.ViewOf(staggered.FoeId),
						"STAGGERED",
						KinPalette.Bone
					),
				FoeHitEvent hit => () =>
				{
					if (lunge)
						_field.Lunge(_state, hit.AttackerId);
					Struck(_field.ViewOf(hit.FoeId), hit.Damage);
				},
				AllyHitEvent hit => () =>
				{
					if (lunge)
						_field.Lunge(_state, hit.AttackerId);
					Struck(_field.ViewOf(hit.AllyId), hit.Damage);
					if (hit.Damage > 0)
						KinAnimator.Shake(_layer, 6f);
				},
				BlockGainedEvent block => () =>
					KinAnimator.Float(
						_overlay,
						_field.ViewOf(block.AllyId),
						$"+{block.Amount} BLOCK",
						KinPalette.Bone
					),
				AllySwappedInEvent swap => () =>
				{
					var cell = _field.ViewOf(swap.AllyId);
					KinAnimator.Pop(cell);
					KinAnimator.Float(_overlay, cell, "IN!", KinPalette.Gold);
				},
				FoeMovedEvent moved => () =>
					KinAnimator.Float(
						_overlay,
						_field.ViewOf(moved.FoeId),
						"SWAPPED",
						KinPalette.Bone
					),
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
		return delay;
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

	private string Describe(GameEvent e) =>
		e switch
		{
			AllyHitEvent hit => $"{hit.By} hits {Who(hit.AllyId)} for {hit.Damage}"
				+ (hit.Blocked > 0 ? $" ({hit.Blocked} blocked)" : ""),
			AllyKnockedOutEvent ko => $"{Who(ko.AllyId)} is knocked out!",
			FoeHitEvent hit => $"{Who(hit.FoeId)} takes {hit.Damage}"
				+ (hit.Blocked > 0 ? $" ({hit.Blocked} blocked)" : ""),
			CardPlayedEvent played => $"Played {played.CardName}",
			FoeMovedEvent moved => $"{Who(moved.FoeId)} is moved to place {moved.To + 1}",
			FoeStaggeredEvent staggered => $"{Who(staggered.FoeId)} is staggered",
			CardStolenEvent stolen => $"{Who(stolen.FoeId)} steals your {stolen.CardName}",
			CardDiscardedEvent discarded => $"Discarded {Who(discarded.CardId)}",
			FoeCaughtEvent caught => $"Caught the {Who(caught.FoeId)}!",
			AllySwappedInEvent swap => $"{Who(swap.AllyId)} steps in for {Who(swap.ForAllyId)}",
			PartyBattleEndedEvent end => end.Won ? "VICTORY." : "DEFEAT.",
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
		_field = new KinRelayField();
		// Centred in the room between the banner and the status strip, both ways.
		var centred = new CenterContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
		centred.AddChild(_field.Root);
		column.AddChild(centred);
		column.AddChild(BuildStatus());
		column.AddChild(new Control { CustomMinimumSize = new Vector2(0, KinHandView.BandHeight) });

		// The log sits to the right of the rows, where the board has room.
		_log = KinPalette.Text("", 16, KinPalette.Bone, HorizontalAlignment.Left);
		_log.Position = new Vector2(1560, 110);
		_log.Size = new Vector2(340, 300);
		_log.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		_log.Modulate = new Color(1, 1, 1, 0.8f);
		layer.AddChild(_log);

		_choice = new MtgGame.ChoicePanel();
		AddChild(_choice);
		_choice.Confirmed += OnChoiceConfirmed;
		// Its panel has no background under KIN's theme — the options floated over the board.
		foreach (var panel in _choice.FindChildren("*", nameof(PanelContainer), true, false))
			((PanelContainer)panel).AddThemeStyleboxOverride(
				"panel",
				KinPalette.Box(KinPalette.Navy, KinPalette.Slate)
			);

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

		_practicePicker = new OptionButton();
		_practicePicker.AddThemeFontSizeOverride("font_size", 18);
		for (var i = 0; i < PartyContent.Scenarios.Count; i++)
			_practicePicker.AddItem(PartyContent.Scenarios[i].Name.ToUpperInvariant(), i);
		_practicePicker.ItemSelected += index => StartScenario((int)index);
		across.AddChild(_practicePicker);

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
		// Wraps, with a minimum width of 1: unwrapped, a long hint was as wide as its text and pushed
		// END TURN — and the whole column with it — off the screen.
		_hint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		_hint.CustomMinimumSize = new Vector2(1, 0);

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
