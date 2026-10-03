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

	/// <summary>The monster clicked last — its rule is shown. 0 = none.</summary>
	private int _selectedAllyId;

	private KinRelayField _field;

	private KinHandView _hand;

	/// <summary>MTG's choice panel, reused as it is (see <see cref="Settle"/>).</summary>
	private MtgGame.ChoicePanel _choice;
	private Label _title;
	private Label _subtitle;
	private Label _energy;
	private Label _energyNote;

	/// <summary>The team's SPELL POWER and your auras, over the orb — hidden when there are none.</summary>
	private Label _spellPower;

	/// <summary>Spell Power's symbol, left of its number.</summary>
	private TextureRect _spellIcon;

	/// <summary>The auras in play: a symbol and a short name each, over the Spell Power.</summary>
	private VBoxContainer _auras;

	private Label _hint;
	private Button _endTurn;
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
						if (TryPlay(card, at) is { } refused)
							Report(refused);
					};
			}
			if (
				arg.StartsWith("--inspect=")
				&& int.TryParse(arg["--inspect=".Length..], out var inspect)
			)
				_captureInspect = inspect;
			if (arg == "--end-turn")
				GetTree().CreateTimer(1.6).Timeout += OnEndTurn;
			// Capture-only: `--mouse=x,y` stands in for the cursor (canvas pixels), so a tip can be seen.
			if (
				arg.StartsWith("--mouse=")
				&& arg["--mouse=".Length..].Split(',') is [var mx, var my]
			)
			{
				var at = _captureMouse = new Vector2(float.Parse(mx), float.Parse(my));
				// One real motion event too, so a native tooltip (the run screens) can open.
				GetTree().CreateTimer(0.9).Timeout += () =>
					GetViewport()
						.PushInput(
							new InputEventMouseMotion
							{
								Position = at.Value,
								GlobalPosition = at.Value,
							},
							true
						);
			}
			if (
				arg.StartsWith("--hover-card=")
				&& int.TryParse(arg["--hover-card=".Length..], out var hc)
			)
				_captureHoverCard = hc;
			// Capture-only: `--howto` opens the ? panel.
			if (arg == "--howto")
				GetTree().CreateTimer(0.6).Timeout += ToggleHowTo;
		}

		_practice |= Practice;
		Practice = false;
		_practicePicker.Visible = _practice;

		if (_practice)
			StartScenario(_scenario);
		else if (_captureStarter is { } starter)
		{
			// Capture-only: `--screen=route` sets out onto the first route, `route2` walks one place
			// further; `town2` is the second town; `boss` stands at the route's end (placed, not walked);
			// `between|over` fight the route's first place and end it through `DebugEndBattle`.
			// `trio` shows the rolled team and its REROLL; every other screen skips past it.
			BeginRun(PartyContent.Families[starter], showTrio: _captureScreen == "trio");
			if (_captureScreen is { } screen)
				GetTree().CreateTimer(0.5).Timeout += () =>
				{
					if (screen == "trio")
						return;
					if (screen is "spring" or "spring-full")
					{
						// Placed on the route's first spring, not walked: its choice waits.
						// `spring-full` adds the family's whole pool first — the upgrade row that overflows.
						Change(r =>
						{
							var route = r.EnterRoute();
							var spring = route.Route!.Nodes.First(n => n.Kind == NodeKind.Rest);
							if (screen == "spring-full")
								route = route with
								{
									Deck = route.Deck.AddRange(
										PartyContent.Rewards.Where(c => c.Family == route.Family)
									),
								};
							return route with { NodeId = spring.Id };
						});
						return;
					}
					if (screen == "town2")
					{
						Change(r => r with { RegionIndex = 1 });
						return;
					}
					if (screen is "boss" or "relics" or "monsters")
					{
						Change(r =>
						{
							var route = r.EnterRoute();
							return route with { NodeId = route.Route!.End.Id };
						});
						// `relics` wins the boss fight through the capture hook: the real flow after.
						if (screen is "relics" or "monsters")
						{
							_state = _state.DebugEndBattle(won: true);
							BattleOver();
						}
						// `monsters` skips the relic, to show the boss's monster pick.
						if (screen == "monsters")
						{
							_run = _run with { RelicChoice = [] };
							ShowPrizes(new RunReport([], 0), _run.Boss.Name);
						}
						return;
					}
					if (screen is "hospital" or "shop" or "pen")
					{
						// A hurt team, so the hospital has something to sell.
						Change(r =>
							r with
							{
								Team = [.. r.Team.Select(m => m with { Hp = m.Hp / 2 })],
							}
						);
						OpenBuilding(System.Enum.Parse<BuildingKind>(screen, ignoreCase: true));
						return;
					}
					Change(r => r.EnterRoute());
					if (screen == "route")
						return;
					if (screen == "route2")
					{
						var find = _run.Route!.Nodes.FirstOrDefault(n => !n.IsFight && n.Row > 0);
						Change(r =>
							r with
							{
								NodeId = find?.Id ?? 0,
								Cleared = [.. r.Cleared, find?.Id ?? 0],
							}
						);
						return;
					}
					// `evolvedfight`: the whole team in its evolved forms (the ★ and the art fallback).
					if (screen == "evolvedfight")
						_run = _run with
						{
							Team =
							[
								.. _run.Team.Select(m => new RunCompanion(
									m.Companion.EvolvesInto ?? m.Companion,
									m.Hp
								)),
							],
						};
					Change(r => r.MoveTo(r.Route!.Next(0).First().Id));
					if (screen is "routefight" or "evolvedfight")
						return;
					_state = _state.DebugEndBattle(won: screen == "between");
					BattleOver();
				};
		}
		else
			ShowStarters();
	}

	private KinRouteMap _route;
	private KinTownMap _town;

	/// <summary>
	/// The building open in town, or null on the town's map. Screen state, not run state: which
	/// door you are standing in changes nothing the run must remember.
	/// </summary>
	private BuildingKind? _building;

	private int? _captureStarter;
	private string _captureScreen;

	// ===== THE RUN

	private void ShowStarters()
	{
		_hand.SetVisible(false);
		_screens.ShowFamilies(family => BeginRun(family));
	}

	private void BeginRun(Family family, bool showTrio = true)
	{
		_run = PartyRun.Start(family, (int)GD.RandRange(1, 9999));
		if (showTrio)
			ShowTrio();
		else
			Continue();
	}

	/// <summary>The rolled three: REROLL once, or BEGIN the run.</summary>
	private void ShowTrio() =>
		_screens.ShowTrio(
			_run,
			() =>
			{
				_run = _run.Reroll();
				ShowTrio();
			},
			Continue
		);

	/// <summary>A change to the run from a screen, then whatever comes next.</summary>
	private void Change(System.Func<PartyRun, PartyRun> change)
	{
		_run = change(_run);
		Continue();
	}

	/// <summary>
	/// **Where the run is decides what shows** (`PartyRun.Phase`): the town's map — or the building
	/// you walked into — the route map, or the fight on the place you just walked to, or the end.
	/// </summary>
	private void Continue()
	{
		_hand.SetVisible(false);
		_route.Hide();
		_town.Hide();
		switch (_run.Phase)
		{
			case RunPhase.Town when _building is { } inside:
				_screens.ShowBuilding(
					_run,
					inside,
					Change,
					() =>
					{
						_building = null;
						Continue();
					}
				);
				break;
			case RunPhase.Town:
				_screens.Hide();
				_town.Show(_run, OpenBuilding);
				break;
			case RunPhase.Route when !_run.HereIsCleared && _run.Here.IsFight:
				NextBattle();
				break;
			// A SPRING: heal, or upgrade a card (round 4).
			case RunPhase.Route when _run.AtSpring:
				_screens.ShowSpring(
					_run,
					() => Change(r => r.HealAtSpring()),
					index => Change(r => r.UpgradeAtSpring(index))
				);
				break;
			case RunPhase.Route:
				_screens.Hide();
				_route.Show(_run, id => Change(r => r.MoveTo(id)));
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

	/// <summary>Into a building — or, through the gate, out onto the route.</summary>
	private void OpenBuilding(BuildingKind kind)
	{
		if (kind == BuildingKind.Gate)
		{
			Change(r => r.EnterRoute());
			return;
		}
		_building = kind;
		Continue();
	}

	private void NextBattle()
	{
		_building = null;
		_screens.Hide();
		_route.Hide();
		_town.Hide();
		_hand.SetVisible(true);
		_selectedAllyId = 0;
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
			ShowPrizes(report, beaten);
	}

	/// <summary>
	/// **A boss's prizes, each on its own screen, then the usual rewards**: a boss relic, then (the
	/// first two bosses) a monster — round 4: monsters come from bosses. Any fight else goes straight
	/// to the rewards.
	/// </summary>
	private void ShowPrizes(RunReport report, string beaten)
	{
		if (!_run.RelicChoice.IsEmpty)
			ShowRelicChoice(report, beaten);
		else if (!_run.Evolvable.IsEmpty)
			_screens.ShowEvolution(
				_run,
				index =>
				{
					_run = _run.Evolve(index);
					ShowPrizes(report, beaten);
				}
			);
		else
			ShowBetween(report, beaten);
	}

	/// <summary>
	/// **After a boss: one of three BOSS RELICS, on its own screen** — then the usual rewards. Its
	/// own screen because the victory screen is already full (it pushed SKIP off once).
	/// </summary>
	private void ShowRelicChoice(RunReport report, string beaten) =>
		_screens.ShowRelicChoice(
			_run,
			beaten,
			relic =>
			{
				_run = _run.ChooseRelic(relic);
				ShowPrizes(report, beaten);
			},
			() =>
			{
				_run = _run with { RelicChoice = [] };
				ShowPrizes(report, beaten);
			}
		);

	private void ShowBetween(RunReport report, string beaten) =>
		_screens.ShowBetween(_run, report, beaten, reward => Change(r => r.Take(reward)), Continue);

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
		_hint.Text = "";
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

	/// <summary>
	/// **The team's Spell Power and the auras in play, as symbols** — a swirl and a number; an aura's
	/// mark and its name (the declutter pass, 2026-09-30).
	/// </summary>
	private void ShowTeamStatus()
	{
		var spell = _state.SpellBonus();
		_spellIcon.Visible = _spellPower.Visible = spell > 0;
		_spellPower.Text = spell.ToString();

		foreach (var child in _auras.GetChildren())
			child.QueueFree();
		foreach (var aura in _state.Auras())
		{
			var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
			row.AddThemeConstantOverride("separation", 6);
			row.AddChild(
				new TextureRect
				{
					ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
					StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
					Texture = KinArt.AuraIcon,
					Modulate = KinPalette.Gold,
					CustomMinimumSize = new Vector2(30, 30),
					MouseFilter = Control.MouseFilterEnum.Ignore,
				}
			);
			row.AddChild(Outlined(aura.Name.ToUpperInvariant(), 18, KinPalette.Bone));
			_auras.AddChild(row);
		}
	}

	/// <summary>How to play — the ? panel's lines (the declutter pass: no always-on hint line).</summary>
	private static readonly string[] HowToPlay =
	[
		"Drag a card onto its target. A card with no target plays anywhere on the field.",
		"The first attack card on each monster each turn also fires its bonus.",
		"END TURN: the foes act, from the back. A foe's badge shows its next move.",
	];

	/// <summary>The cell the last card was dropped on — it pops.</summary>
	private Control _lastDrop;

	/// <summary>Returns null when the card played, or the engine's refusal — the hand shows it.</summary>
	private string TryPlay(int cardId, int? drop)
	{
		// **No target needed: it plays where it lands** (MTG's `SpellNeedsTargets` false → cast on drop).
		if (_state.GetObject(cardId) is KinCard { } untargeted && !untargeted.NeedsTarget())
		{
			var free = new PlayPartyCardAction { CardId = cardId };
			if (free.ValidateAdd(_state) is { IsValid: false } refused)
				return refused.Reason;
			_lastDrop = drop is { } at ? _field.At(_state, at) : null;
			Apply(free);
			return null;
		}

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

	private int? SpaceAt(Vector2 point) =>
		_field.DropAt(point) is { } d && d < PartyBattle.MaxLine ? d : null;

	/// <summary>
	/// **Click a monster to read its rule** — monsters are not dragged in a fight; the order is set in
	/// town (2026-10-02). Read here, after the GUI and after a hand card has claimed its own click, so
	/// nothing on the board has to catch the mouse.
	/// </summary>
	public override void _UnhandledInput(InputEvent @event)
	{
		if (
			_state is null
			|| _screens.IsShowing
			|| Common.Cards.CardUIManager.DraggingCard is not null
		)
			return;

		if (
			@event
			is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click
		)
			return;

		if (SpaceAt(click.Position) is not { } space)
			return;

		GetViewport().SetInputAsHandled();

		if (_state.AllyAt(space) is { } ally)
		{
			_selectedAllyId = _selectedAllyId == ally.Id ? 0 : ally.Id;
			Render(ImmutableList<GameEvent>.Empty);
			if (_selectedAllyId != 0)
				Report($"{ally.Name}: {ally.PassiveRule}");
			return;
		}

		Report("Click one of your monsters to read it.");
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
		UpdateInspector(cardInPlay: ui is not null);
		UpdateTip(
			Common.Cards.CardUIManager.DraggingCard is not null
				? null
				: Common.Cards.CardUIManager.CurrentHoveredCard
					?? (_captureHoverCard is { } n && n < _hand.Cards.Count ? _hand.Cards[n] : null)
		);

		PreviewAttack(Common.Cards.CardUIManager.DraggingCard);

		var id = int.TryParse(ui?.Id, out var parsed) ? parsed : _captureFocusId;
		if (id == _focusCardId)
			return;

		_focusCardId = id;
		RenderRows();
	}

	private (Common.Cards.CardUI2D Ui, KinCard Card, int? Total) _attackShown;

	/// <summary>
	/// **A dragged attack card shows what it would hit for, over the monster under it** — and its own
	/// words again anywhere else. Rewritten only when the number changes.
	/// </summary>
	private void PreviewAttack(Common.Cards.CardUI2D dragging)
	{
		var card =
			int.TryParse(dragging?.Id, out var id)
			&& _state.HasObject(id)
			&& _state.GetObject(id) is KinCard c
			&& c.Effects.Any(e => e.Template is StrikeAction)
				? c
				: null;
		int? total =
			card is not null && SpaceAt(Mouse) is { } space
				? _state.AttackPreview(card, space)
				: null;
		if (dragging == _attackShown.Ui && total == _attackShown.Total)
			return;

		// Put the words back only on an ATTACK the old face still shows: a non-attack was never
		// rewritten, and a played card's face is recycled for whatever the hand drew next.
		if (
			_attackShown is { Ui: { } old, Card: { } shown }
			&& old != dragging
			&& IsInstanceValid(old)
			&& old.Id == shown.Id.ToString()
		)
			KinCardFace.ShowAttack(old, shown, null);
		if (card is not null)
			KinCardFace.ShowAttack(dragging, card, total);
		_attackShown = (dragging, card, total);
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
		_inspector.Show(
			creature,
			StepOf(creature.Id),
			cell.GetGlobalRect(),
			GetViewportRect().Size
		);
	}

	// ===== Tips — what a symbol means, on hover (Shayne, 2026-10-01)

	private PanelContainer _tip;
	private RichTextLabel _tipText;

	/// <summary>`--hover-card=N` only: hand card N stands in for a hovered card.</summary>
	private int? _captureHoverCard;

	/// <summary>`--mouse=x,y` only: the cursor a capture stands in for, so a hover can be seen.</summary>
	private Vector2? _captureMouse;

	private Vector2 Mouse => _captureMouse ?? GetViewport().GetMousePosition();

	private void BuildTip(CanvasLayer layer)
	{
		_tip = new PanelContainer
		{
			Visible = false,
			ZIndex = 250,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		_tip.AddThemeStyleboxOverride("panel", KinPalette.Box(KinPalette.Navy, KinPalette.Gold, 2));
		_tipText = new RichTextLabel
		{
			BbcodeEnabled = true,
			FitContent = true,
			ScrollActive = false,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			CustomMinimumSize = new Vector2(380, 0),
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		_tipText.AddThemeFontSizeOverride("normal_font_size", 22);
		_tipText.AddThemeFontSizeOverride("bold_font_size", 22);
		_tipText.AddThemeColorOverride("default_color", KinPalette.Bone);
		_tip.AddChild(_tipText);
		layer.AddChild(_tip);
	}

	/// <summary>
	/// **The tip for what is under the cursor**: a hovered CARD says its rules and each symbol on it;
	/// a creature's ICON says what it is (and hides the inspector, which would cover it); the Spell
	/// Power and aura marks say theirs. Nothing under the cursor, no tip.
	/// </summary>
	private void UpdateTip(Common.Cards.CardUI2D hoveredCard)
	{
		string text = null;
		Vector2 at;
		if (
			hoveredCard is not null
			&& int.TryParse(hoveredCard.Id, out var cardId)
			&& _state.HasObject(cardId)
			&& _state.GetObject(cardId) is KinCard card
		)
		{
			text = CardTip(card);
			// Above the lifted card, beside it: the card itself must stay readable.
			at = hoveredCard.GetGlobalTransformWithCanvas().Origin + new Vector2(140, -560);
		}
		else
		{
			at = Mouse + new Vector2(24, 24);
			if (!_screens.IsShowing)
				text = _field.TipAt(_state, Mouse) ?? TeamStatusTip(Mouse);
			if (text is not null)
				_inspector.Hide();
		}

		_tip.Visible = text is not null;
		if (text is null)
			return;
		if (_tipText.Text != text)
			_tipText.Text = text;
		_tip.ResetSize();
		var canvas = GetViewportRect().Size;
		_tip.Position = new Vector2(
			Mathf.Clamp(at.X, 8, canvas.X - _tip.Size.X - 8),
			Mathf.Clamp(at.Y, 8, canvas.Y - _tip.Size.Y - 8)
		);
	}

	/// <summary>A card's rules in words, then each symbol it uses and what it means.</summary>
	private static string CardTip(KinCard card)
	{
		var lines = new List<string>
		{
			$"[b]{card.Name.ToUpperInvariant()}[/b]",
			KinCardFace.RulesTextFor(card),
		};
		foreach (var symbol in KinSymbols.Of(card))
			lines.Add($"{KinSymbols.Img(symbol)} [b]{symbol.Name}[/b]: {symbol.Meaning}");
		return string.Join("\n", lines);
	}

	/// <summary>The Spell Power and aura marks over the orb, explained.</summary>
	private string TeamStatusTip(Vector2 mouse)
	{
		if (
			_spellIcon.Visible
			&& (
				_spellIcon.GetGlobalRect().HasPoint(mouse)
				|| _spellPower.GetGlobalRect().HasPoint(mouse)
			)
		)
			return $"Spell Power {_state.SpellBonus()}: {KinSymbols.SpellPower.Meaning}";
		var auras = _state.Auras().ToList();
		var rows = _auras.GetChildren().OfType<Control>().ToList();
		for (var i = 0; i < Mathf.Min(rows.Count, auras.Count); i++)
			if (rows[i].GetGlobalRect().HasPoint(mouse))
				return $"{auras[i].Name}: {KinSymbols.Aura.Meaning}";
		return null;
	}

	private void Render(ImmutableList<GameEvent> events)
	{
		var party = _state.GetParty();
		RenderRows(settleAfter: null);

		// One short line (the declutter pass): the fight and the turn. The field shows the rest.
		_title.Text = party.IsOver
			? (party.Won ? "VICTORY" : "DEFEAT") + $" — {party.Name.ToUpperInvariant()}"
			: $"TURN {party.TurnNumber}   ·   {party.Name.ToUpperInvariant()}";
		_subtitle.Text = "";

		// Borrowed energy (Surge) is a cost you pay later — it must be visible now.
		_energy.Text = $"{party.Energy}/{party.MaxEnergy}";
		_energyNote.Text = party.EnergyDebt > 0 ? $"−{party.EnergyDebt} NEXT TURN" : "";
		// Spell Power is the TEAM's total, and the auras are rules on your side (Ember, round 4).
		ShowTeamStatus();
		_energyNote.LabelSettings.FontColor =
			party.EnergyDebt > 0 ? KinPalette.Red.Lightened(0.3f) : KinPalette.Bone;
		_endTurn.Disabled = party.IsOver;

		// The badge shows what the card costs NOW (`CostOf`: Scrap Hammer after discards).
		_hand.Sync(
			[.. _state.CardsIn(ZoneType.Hand).Select(c => c with { Cost = _state.CostOf(c) })],
			party.Energy,
			// LIVE: a spell's numbers with the Spell Power already in.
			c => KinCardFace.For(c, _state.SpellBonus()),
			_state.SpellBonus()
		);
		_field.Settle(Animate(events));
	}

	private Dictionary<int, int> Steps() =>
		_state
			.ActingSteps()
			.SelectMany((step, i) => step.Select(c => (c.Id, Step: i + 1)))
			.ToDictionary(p => p.Id, p => p.Step);

	private int StepOf(int creatureId) => Steps().GetValueOrDefault(creatureId);

	/// <summary>
	/// **The field, drawn from state.** `settleAfter` null leaves the line where it stands (Render
	/// slides it after the turn's blows); otherwise the line settles after that long.
	/// </summary>
	private void RenderRows(double? settleAfter = 0)
	{
		// **The step badge**: which step of the end of the turn each creature acts in, straight from
		// the engine. Both sides act AT ONCE within a step, so a pair shares its number — the flat
		// 1–6 order this replaced made simultaneous blows look sequential.
		var steps = Steps();

		var focus =
			_focusCardId != 0
			&& _state.HasObject(_focusCardId)
			&& _state.GetObject(_focusCardId) is KinCard card
				? card
				: null;

		// **Where the card under the cursor can be dropped — asked of the ENGINE, place by place, on
		// both lines**, so the lit places can never disagree with what a drop will do.
		// A card that needs NO target lights nothing: it plays wherever it is dropped (MTG's rule).
		var drops = new HashSet<int>();
		if (focus is not null && focus.NeedsTarget())
		{
			for (var d = 0; d < PartyBattle.MaxLine * 2; d++)
				if (Play(focus.Id, d).ValidateAdd(_state).IsValid)
					drops.Add(d);
		}

		_field.Render(
			_state,
			new FieldContext(
				_state.ForecastIfTurnEndsNow().Hp,
				steps,
				drops,
				focus,
				_selectedAllyId
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
				// A pop on what it landed on — no name rising off it: you just played it (2026-10-02).
				CardPlayedEvent when _lastDrop is { } cell => () => KinAnimator.Pop(cell),
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
						$"+{block.Amount}",
						KinPalette.Bone,
						KinArt.GuardIcon
					),
				// **The engines, seen firing** (2026-09-28: the playtest could not tell Ember ever did).
				SpellPowerGainedEvent gained => () =>
					KinAnimator.Float(
						_overlay,
						_spellPower,
						$"+{gained.Amount}",
						KinPalette.Family(Family.Ember).Lightened(0.3f),
						KinArt.SpellPowerIcon
					),
				FoeBurnedEvent burned => () =>
				{
					var cell = _field.ViewOf(burned.FoeId);
					KinAnimator.Flash(cell, new Color(1.6f, 0.9f, 0.5f));
					KinAnimator.Float(
						_overlay,
						cell,
						$"−{burned.Damage}",
						KinPalette.Family(Family.Ember).Lightened(0.3f),
						KinArt.BurnIcon
					);
				},
				GrewEvent grew => () =>
					KinAnimator.Float(
						_overlay,
						_field.ViewOf(grew.AllyId),
						$"+{grew.Power}",
						KinPalette.Family(Family.Grove).Lightened(0.4f),
						KinArt.PowerIcon
					),
				FoePhaseEvent phase => () =>
				{
					var cell = _field.ViewOf(phase.FoeId);
					KinAnimator.Pop(cell);
					KinAnimator.Float(_overlay, cell, $"{phase.Name}!", KinPalette.Red);
				},
				ThornsEvent thorns => () =>
					KinAnimator.Float(
						_overlay,
						_field.ViewOf(thorns.FoeId),
						$"−{thorns.Damage}",
						KinPalette.Family(Family.Grove).Lightened(0.4f),
						KinArt.ThornsIcon
					),
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
			damage > 0 ? $"−{damage}" : "",
			damage > 0 ? KinPalette.Red : KinPalette.Bone,
			damage > 0 ? null : KinArt.GuardIcon
		);
	}

	private void Report(string text)
	{
		_hint.Text = text;
		KinAnimator.Pop(_hint);
	}

	// ===== Layout

	private void BuildUi()
	{
		var layer = _layer = new CanvasLayer();
		AddChild(layer);

		var ground = new ColorRect { Color = KinPalette.Navy };
		ground.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		layer.AddChild(ground);

		// **The stage** (style D): the lines stand on a painted ground, not in a navy void.
		// ponytail: one backdrop for every battle; per-region when there are more than one.
		if (KinArt.RegionBackdrop("greenwood") is { } backdrop)
		{
			var stage = new TextureRect
			{
				Texture = backdrop,
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
				MouseFilter = Control.MouseFilterEnum.Ignore,
				// Lightly dimmed: every word on it is outlined, and the mockup's stage is bright.
				Modulate = new Color(0.86f, 0.88f, 0.9f),
			};
			stage.SetAnchorsPreset(Control.LayoutPreset.FullRect);
			// Raised, so the backdrop's meadow is under the lines' feet rather than its mountains —
			// the stage is taller than the screen and the sky is what gets cropped.
			stage.OffsetTop = -StageLift;
			layer.AddChild(stage);
		}

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
		// Centred in the room between the banner and the hint, both ways.
		var centred = new CenterContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
		centred.AddChild(_field.Root);
		column.AddChild(centred);
		column.AddChild(BuildHint());
		column.AddChild(new Control { CustomMinimumSize = new Vector2(0, KinHandView.BandHeight) });
		BuildCorners(layer);

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
		_route = new KinRouteMap(layer);
		_town = new KinTownMap(layer);
		_inspector = new KinPartyInspector(layer);
		BuildTip(layer);
	}

	/// <summary>How far the backdrop is raised: ground at the feet line, sky cropped (tuned by capture).</summary>
	private const int StageLift = 380;

	/// <summary>A button in the kit (`KinUiKit`): textured, brightens on hover, dims when it cannot be pressed.</summary>
	private static void StyleButton(Button button, int fontSize, bool hex = false) =>
		KinUiKit.Style(button, fontSize, hex);

	private static Label Outlined(string text, int size, Color colour)
	{
		var label = KinPalette.Text(text, size, colour);
		label.LabelSettings = new LabelSettings
		{
			FontSize = size,
			FontColor = colour,
			OutlineSize = 6,
			OutlineColor = new Color(0.04f, 0.06f, 0.09f),
		};
		return label;
	}

	private Control BuildBanner()
	{
		var across = new HBoxContainer();
		across.AddThemeConstantOverride("separation", 16);

		// The region plate, top-left: as wide as its words, not the screen.
		var plate = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin };
		plate.AddThemeStyleboxOverride("panel", KinUiKit.Plate("bone"));
		var text = new VBoxContainer();
		text.AddThemeConstantOverride("separation", 0);
		_title = KinPalette.Text("", 28, KinPalette.Bone, HorizontalAlignment.Left);
		_subtitle = KinPalette.Text("", 18, KinPalette.Bone, HorizontalAlignment.Left);
		_subtitle.Modulate = new Color(1, 1, 1, 0.75f);
		text.AddChild(_title);
		text.AddChild(_subtitle);
		plate.AddChild(text);
		across.AddChild(plate);
		across.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

		_practicePicker = new OptionButton { SizeFlagsVertical = Control.SizeFlags.ShrinkBegin };
		StyleButton(_practicePicker, 18);
		for (var i = 0; i < PartyContent.Scenarios.Count; i++)
			_practicePicker.AddItem(PartyContent.Scenarios[i].Name.ToUpperInvariant(), i);
		_practicePicker.ItemSelected += index => StartScenario((int)index);
		across.AddChild(_practicePicker);

		// **?** — the how-to and what every symbol means (the declutter pass: no always-on hints).
		var help = new Button { Text = "?", SizeFlagsVertical = Control.SizeFlags.ShrinkBegin };
		StyleButton(help, 18);
		help.Pressed += ToggleHowTo;
		across.AddChild(help);

		var menu = new Button { Text = "MENU", SizeFlagsVertical = Control.SizeFlags.ShrinkBegin };
		StyleButton(menu, 18);
		menu.Pressed += () => Project.GameManager.Instance.GoToMainMenu();
		across.AddChild(menu);

		return across;
	}

	/// <summary>Esc closes the how-to if it is open; otherwise it goes back to the main menu.</summary>
	public override void _Input(InputEvent @event)
	{
		if (@event is not InputEventKey { Pressed: true, Keycode: Key.Escape })
			return;
		if (_howTo is { Visible: true })
			_howTo.Visible = false;
		else
			Project.GameManager.Instance.GoToMainMenu();
		GetViewport().SetInputAsHandled();
	}

	/// <summary>The how-to panel, built the first time it is asked for.</summary>
	private Control _howTo;

	private void ToggleHowTo()
	{
		_howTo ??= BuildHowTo();
		_howTo.Visible = !_howTo.Visible;
	}

	/// <summary>
	/// **HOW TO PLAY, and what every symbol means** — one screen, on the ? button. Everything the
	/// battle used to say all the time (the hint line, words on every creature) lives here once.
	/// A click anywhere closes it.
	/// </summary>
	private Control BuildHowTo()
	{
		var dim = new ColorRect { Color = new Color(0, 0, 0, 0.7f), ZIndex = 300 };
		dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		dim.GuiInput += e =>
		{
			if (e is InputEventMouseButton { Pressed: true })
				dim.Visible = false;
		};
		_layer.AddChild(dim);

		var centre = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		centre.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		dim.AddChild(centre);
		var panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		panel.AddThemeStyleboxOverride(
			"panel",
			KinPalette.Box(KinPalette.Navy, KinPalette.Gold, 3)
		);
		centre.AddChild(panel);
		var column = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		column.AddThemeConstantOverride("separation", 10);
		panel.AddChild(column);

		column.AddChild(Outlined("HOW TO PLAY", 36, KinPalette.Gold));
		foreach (var line in HowToPlay)
			column.AddChild(Outlined("•  " + line, 22, KinPalette.Bone));

		var grid = new GridContainer { Columns = 2, MouseFilter = Control.MouseFilterEnum.Ignore };
		grid.AddThemeConstantOverride("h_separation", 48);
		grid.AddThemeConstantOverride("v_separation", 8);
		column.AddChild(grid);
		foreach (
			var (_, icon, tint, meaning) in KinSymbols.Legend.Select(s =>
				(s, s.Icon, s.Tint, s.Line)
			)
		)
		{
			var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
			row.AddThemeConstantOverride("separation", 10);
			row.AddChild(
				new TextureRect
				{
					ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
					StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
					Texture = icon,
					Modulate = tint,
					CustomMinimumSize = new Vector2(34, 34),
					MouseFilter = Control.MouseFilterEnum.Ignore,
				}
			);
			var label = Outlined(meaning, 20, KinPalette.Bone);
			label.HorizontalAlignment = HorizontalAlignment.Left;
			row.AddChild(label);
			grid.AddChild(row);
		}
		column.AddChild(Outlined("Click anywhere to close.", 18, new Color(KinPalette.Bone, 0.7f)));
		dim.Visible = false;
		return dim;
	}

	/// <summary>
	/// **The hint — how to play, the deploy instruction, and the engine's refusal of a play** — as
	/// outlined words over the stage, where the status strip was. Wraps with a minimum width of 1:
	/// unwrapped, a long hint was as wide as its text and pushed the column off the screen.
	/// </summary>
	private Control BuildHint()
	{
		_hint = Outlined("", 20, KinPalette.Bone);
		_hint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		_hint.CustomMinimumSize = new Vector2(1, 0);
		return _hint;
	}

	/// <summary>
	/// **The corners of the hand band, as the mockup has them**: the energy ORB bottom-left, END TURN
	/// bottom-right. Placed on the 1920x1080 canvas by hand — they sit
	/// beside the fan, which no container lays out.
	/// </summary>
	private void BuildCorners(CanvasLayer layer)
	{
		var canvas = GetViewportRect().Size;
		const int orb = 150;

		// The orb is a picture (`Art/ui/orb.png`: a glossy sphere in a studded gold ring), not a
		// round Panel — the flat disc was the plainest thing on the screen.
		var disc = new TextureRect
		{
			Texture = KinArt.Drawing("ui/orb"),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.Scale,
			Position = new Vector2(38, canvas.Y - 272),
			Size = new Vector2(orb + 20, orb + 20),
		};
		layer.AddChild(disc);

		_energy = Outlined("", 48, KinPalette.Bone);
		_energy.Position = new Vector2(10, 40);
		_energy.Size = new Vector2(orb, 60);
		disc.AddChild(_energy);
		_spellIcon = new TextureRect
		{
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			Texture = KinArt.SpellPowerIcon,
			Modulate = Color.FromHtml("#FF9A3C"),
			Position = new Vector2(24, canvas.Y - 326),
			Size = new Vector2(52, 52),
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		layer.AddChild(_spellIcon);
		_auras = new VBoxContainer
		{
			Position = new Vector2(28, canvas.Y - 520),
			Size = new Vector2(220, 196),
			Alignment = BoxContainer.AlignmentMode.End,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		layer.AddChild(_auras);
		_spellPower = Outlined("", 38, Color.FromHtml("#FF9A3C"));
		_spellPower.HorizontalAlignment = HorizontalAlignment.Left;
		_spellPower.Position = new Vector2(80, canvas.Y - 324);
		_spellPower.Size = new Vector2(orb, 48);
		layer.AddChild(_spellPower);

		_energyNote = Outlined("ENERGY", 16, KinPalette.Bone);
		_energyNote.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		_energyNote.Position = new Vector2(20, 96);
		_energyNote.Size = new Vector2(orb - 20, 40);
		disc.AddChild(_energyNote);

		_endTurn = new Button
		{
			Text = "END TURN",
			Position = new Vector2(canvas.X - 48 - 300, canvas.Y - 190),
			Size = new Vector2(300, 96),
		};
		StyleButton(_endTurn, 36, hex: true);
		_endTurn.Pressed += OnEndTurn;
		layer.AddChild(_endTurn);
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
