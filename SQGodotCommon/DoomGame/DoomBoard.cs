using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using DoomCore;
using Godot;
using ImmutableGameObjects;

namespace DoomGame;

/// <summary>
/// The battle screen. **Presentation only** — see DoomUI.md.
///
/// Every number here is READ from DoomStateExtensions and never computed. A UI that works out a
/// game fact for itself is a second rules engine, and it will drift from the first. If something
/// cannot be read, the fix is a reader in DoomCore, not arithmetic in this file.
///
/// Built in code rather than as a .tscn because the layout is a contract, and a contract is easier
/// to review as text than as a scene diff.
/// </summary>
public partial class DoomBoard : Node2D
{
	private const int Seed = 42;

	/// <summary>How much log to keep. Enough to watch a turn resolve, not enough to grow forever.</summary>
	private const int LogLines = 40;

	private Run _run;
	private GameState _state;

	private Label _scenarioLabel;
	private Label _descriptionLabel;
	private Label _opponentHealthLabel;
	private ProgressBar _opponentHealthBar;
	private Label _floorLabel;
	private Label _lifeLabel;
	private Label _turnLabel;
	private HBoxContainer _energyPips;
	private Label _logLabel;
	private ScrollContainer _logScroll;
	private readonly List<string> _log = [];
	private Button _endTurnButton;
	private DoomHandView _hand;
	private DoomIntermission _intermission;
	private Label _doomFlash;

	/// <summary>Stops AfterBattle being applied twice: Render runs on every action, IsOver latches.</summary>
	private bool _battleResolved;

	private readonly DoomLaneCell[] _enemyLanes = new DoomLaneCell[DoomBattle.LaneCount];
	private readonly DoomLaneCell[] _unitLanes = new DoomLaneCell[DoomBattle.LaneCount];

	public override void _Ready()
	{
		BuildUi();
		StartRun();
	}

	private void StartRun()
	{
		_run = StarterContent.NewRun(Seed);
		StartBattleOnCurrentFloor();
	}

	/// <summary>
	/// Begins the battle for whatever floor the run is on. The run outlives the battle — deck, life,
	/// floor and companion all persist — so this is the only thing a new floor needs.
	/// </summary>
	private void StartBattleOnCurrentFloor()
	{
		_battleResolved = false;
		_intermission.Hide();

		// Not every floor is a battle. The front end asks the SAME content function the simulator
		// does — if these two ever disagree about what a floor is, every measured number is about
		// a game nobody plays.
		if (StarterContent.FloorKindFor(_run.Floor) == FloorKind.Rest)
		{
			var rested = _run.Rest(StarterContent.RestHealFor(_run.MaxLife));
			_intermission.ShowRest(_run, rested);
			_run = rested;
			return;
		}

		var scenario = StarterContent.ScenarioFor(Seed, _run.Floor);

		var (state, events) = _run.StartBattle(
			scenario,
			StarterContent.CountdownFor(scenario),
			StarterContent.EnemiesFor(_run.Floor, Seed),
			opponent: StarterContent.OpponentFor(_run.Floor, Seed)
		);

		_state = state;
		Render(events);
	}

	/// <summary>
	/// The battle ended, so the RUN takes over. Until this existed a battle stopped and that was
	/// that: no next floor, no transform, no companion mark — the apocalypses are meant to BE the
	/// power curve, and they were landing on a deck nobody ever played again.
	/// </summary>
	private void ResolveBattle()
	{
		_battleResolved = true;

		var battle = _state.GetBattle();
		if (battle.PlayerIsDead)
		{
			_intermission.ShowRunOver(_run.AfterBattle(_state));
			return;
		}

		var before = _run;
		var after = _run.AfterBattle(_state);
		_run = after;

		if (after.IsOver)
		{
			_intermission.ShowRunOver(after);
			return;
		}

		_intermission.ShowFloorCleared(before, after, battle.DoomsFired);
		_intermission.OfferRewards(
			StarterContent.RewardsFor(Seed, after.Floor),
			StarterContent.ScenarioFor(Seed, after.Floor),
			after.Floor
		);
	}

	/// <summary>
	/// Adds the chosen card to the RUN deck and descends. The run owns the deck, so the card is
	/// there for every battle after this one — it does not join the fight that just ended.
	/// </summary>
	private void TakeReward(RunCard card)
	{
		_run = _run.WithCard(card);
		StartBattleOnCurrentFloor();
	}

	private void OnEndTurn()
	{
		if (_state.GetBattle().IsOver)
			return;

		var (state, events) = _state.AddAction(new EndTurnAction()).ProcessAllActions();
		_state = state;
		Render(events);
	}

	/// <summary>
	/// Which of YOUR lane slots a dropped card landed on, or null if it missed.
	///
	/// Hit-tested against the slot rectangles rather than through Area2D drop targets: the lanes are
	/// laid out by containers, so their rects are already the truth, and physics bodies would have
	/// to be kept in step with them on every resize. The hand lives in the same CanvasLayer as the
	/// board, so a card's global position and a slot's global rect are in the same space.
	/// </summary>
	private int? LaneAt(Vector2 point)
	{
		for (var lane = 0; lane < DoomBattle.LaneCount; lane++)
			if (_unitLanes[lane].Root.GetGlobalRect().HasPoint(point))
				return lane;

		return null;
	}

	/// <summary>
	/// Plays a card into a lane. Returns null when it worked, or the ENGINE'S refusal text when it
	/// did not — this never decides legality for itself, and never invents a message. The console
	/// prints the same strings: "Lane 2 is already held by Ash", "Not enough energy for Bulwark".
	/// </summary>
	private string TryPlay(int cardId, int? lane)
	{
		if (_state.GetBattle().IsOver)
			return "the battle is over";

		// Only a body needs a lane. Asking the engine keeps this from being a second opinion about
		// what a unit is.
		var isUnit =
			_state.HasObject(cardId)
			&& _state.GetObject(cardId) is DoomCard card
			&& card.HasComponent<UnitComponent>();

		if (isUnit && lane is null)
			return "drop a unit on one of your lanes";

		var action = new PlayCardAction { CardId = cardId, Lane = lane ?? 0 };

		var validation = action.ValidateAdd(_state);
		if (!validation.IsValid)
			return validation.Reason;

		var (state, events) = _state.AddAction(action).ProcessAllActions();
		_state = state;
		Render(events);
		return null;
	}

	// ===== Rendering =====

	/// <summary>
	/// Redraws every region from current state. Deliberately a full repaint rather than patching
	/// individual nodes per event: a battle is five lanes and a handful of numbers, so the cheap
	/// thing and the correct thing are the same thing, and no region can be left showing something
	/// stale.
	/// </summary>
	private void Render(ImmutableList<GameEvent> events)
	{
		var battle = _state.GetBattle();
		var player = _state.GetPlayer();
		var opponent = _state.GetOpponent();

		var turnWord = battle.CountdownRemaining == 1 ? "TURN" : "TURNS";
		_scenarioLabel.Text =
			$"{battle.Scenario.ToString().ToUpperInvariant()} — {battle.CountdownRemaining} {turnWord}";
		_descriptionLabel.Text = StarterContent.DescriptionFor(battle.Scenario);

		// Clamped for DISPLAY only. Overkill leaves real health negative, which is correct in the
		// engine and reads as a bug on a health bar: the last hit showed "-1 / 26".
		var shown = System.Math.Max(opponent.Health, 0);
		_opponentHealthBar.MaxValue = opponent.MaxHealth;
		_opponentHealthBar.Value = shown;
		_opponentHealthLabel.Text = $"{shown} / {opponent.MaxHealth}";

		for (var lane = 0; lane < DoomBattle.LaneCount; lane++)
			RenderLane(lane, opponent);

		_floorLabel.Text = $"FLOOR {_run.Floor}";
		_lifeLabel.Text = $"{player.Life} / {player.MaxLife}";
		_turnLabel.Text = $"TURN {battle.TurnNumber}";
		RenderEnergy(player);

		RenderLog(events);
		_hand.Sync(_state.CardsIn(ZoneType.Hand).ToList(), player.Energy);

		// Set on BOTH branches. Setting it only when the battle ends left "OPPONENT DOWN" on the
		// button for the whole of the next floor.
		_endTurnButton.Disabled = battle.IsOver;
		if (battle.IsOver)
		{
			_endTurnButton.Text = battle.PlayerIsDead ? "YOU DIED" : "OPPONENT DOWN";
			if (!_battleResolved)
				ResolveBattle();
		}
		else
		{
			_endTurnButton.Text = "END TURN";
		}
	}

	private void RenderLane(int lane, Opponent opponent)
	{
		var enemy = _state.EnemyInLane(lane);
		if (enemy is null)
			_enemyLanes[lane].ShowEmpty();
		else
			_enemyLanes[lane].ShowEnemy(enemy);

		// The summon telegraph: ONE marker in the lane it is coming to. The delay between the
		// announcement and the body landing is what keeps the Opponent reachable at all, so this is
		// load-bearing rather than polish — see DoomUI.md. The player only acts on WHICH lane is
		// closing, so it carries no stats.
		_enemyLanes[lane]
			.SetTelegraph(opponent.NextSummon is { } summon && summon.Lane == lane);

		var card = _state.UnitInLane(lane);
		if (card is null)
			_unitLanes[lane].ShowEmpty();
		else
			_unitLanes[lane].ShowUnit(card, card.HasComponent<CompanionComponent>());
	}

	/// <summary>
	/// The battle log. It exists because of a bug the tests could not find: OpponentDamagedEvent was
	/// raised and never rendered, so the Opponent's health fell with nothing on screen saying why.
	/// **Every event gets a line until the animation that replaces it exists.**
	/// </summary>
	private void RenderLog(ImmutableList<GameEvent> events)
	{
		foreach (var e in events)
		{
			var line = e switch
			{
				PlayerDamagedEvent d => $"took {d.Amount} - {d.LifeRemaining} life left",
				UnitDiedEvent u => $"{u.CardName} died",
				EnemyDiedEvent x => $"{x.EnemyName} is dead",
				IrradiatedDrawnEvent i => $"drawing {i.CardName} cost 1 life",
				EnemySummonedEvent s => $"{s.EnemyName} drops into L{s.Lane}",
				EnemyTelegraphedEvent g => $"they are bringing up {g.EnemyName} for L{g.Lane}",
				OpponentDamagedEvent o => $"hit the Opponent for {o.Amount}",
				OpponentDefeatedEvent => "THE OPPONENT IS DOWN",
				DoomResolvedEvent d => FlashDoom(d),
				PlayerDiedEvent => "*** YOU DIED ***",
				_ => null,
			};

			if (line is not null)
				Report(line);
		}
	}

	/// <summary>
	/// The apocalypse gets the screen for a moment.
	///
	/// A player fought and won an entire battle and could not say afterwards whether the doom had
	/// fired at all. It was announced by one line in a log that is hidden by default, and the
	/// countdown banner simply reset. **This is the centrepiece of the game and it was happening in
	/// silence.**
	/// </summary>
	private string FlashDoom(DoomResolvedEvent fired)
	{
		var name = fired.Scenario.ToString().ToUpperInvariant();

		_doomFlash.Text = $"{name} LANDS";
		_doomFlash.Modulate = Colors.White;
		_doomFlash.Visible = true;

		CreateTween().TweenProperty(_doomFlash, "modulate:a", 0f, 2.2f).SetDelay(0.8);

		return $"*** {name} LANDS (#{fired.FiringNumber}) ***";
	}

	// ===== Layout =====

	private void BuildUi()
	{
		var layer = new CanvasLayer();
		AddChild(layer);

		var root = new ColorRect { Color = DoomPalette.Navy };
		root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		layer.AddChild(root);

		// Between the ground colour and the content, so it sits behind the board and shows down the
		// sides. Added to the layer rather than inside the ColorRect so the draw order is explicit.
		layer.AddChild(BuildBackdrop(GetViewportRect().Size));

		var margin = new MarginContainer();
		margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		margin.AddThemeConstantOverride("margin_left", 32);
		margin.AddThemeConstantOverride("margin_right", 32);
		margin.AddThemeConstantOverride("margin_top", 18);
		margin.AddThemeConstantOverride("margin_bottom", 18);
		layer.AddChild(margin);

		var column = new VBoxContainer();
		column.AddThemeConstantOverride("separation", 10);
		margin.AddChild(column);

		column.AddChild(BuildBanner());
		column.AddChild(BuildOpponent());
		column.AddChild(BuildLaneRow(_enemyLanes, showsTelegraph: true));
		column.AddChild(BuildLaneRow(_unitLanes, showsTelegraph: false));
		column.AddChild(BuildStatusStrip());
		column.AddChild(BuildFooter());

		// The fan is a Node2D and draws where it is told, so the column reserves the space rather
		// than containing it. Added to the same CanvasLayer so that a dragged card's global
		// position and a lane slot's global rect share one coordinate space — see LaneAt.
		var handSpace = new Control { CustomMinimumSize = new Vector2(0, DoomHandView.BandHeight) };
		column.AddChild(handSpace);

		// The BASE canvas, not the window: stretch mode is canvas_items, so this is 1920x1080 and
		// the window scales it down. Placing the fan against the window size puts it off-screen.
		var canvas = GetViewportRect().Size;
		_hand = new DoomHandView(
			layer,
			new Vector2(canvas.X / 2, canvas.Y - DoomHandView.BandHeight / 2f),
			LaneAt,
			TryPlay,
			Report
		);

		// LAST, and after every board Control exists. Godot runs GUI picking BEFORE physics
		// picking, so any Control with MouseFilter.Stop under the cursor swallows the click and the
		// cards' Area2D never sees it. No mouse_entered means CardUIManager never learns the card is
		// hoverable, and the drag cannot start. The full-screen ground ColorRect alone did that
		// everywhere on the board.
		//
		// Every Control here is presentation: the cards are Node2D, and the only thing on this
		// screen that wants a click is the End Turn button. So everything else steps out of the way.
		_doomFlash = DoomPalette.Text("", 88, DoomPalette.Red);
		_doomFlash.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_doomFlash.VerticalAlignment = VerticalAlignment.Center;
		_doomFlash.Visible = false;
		layer.AddChild(_doomFlash);

		_intermission = new DoomIntermission(layer, StartBattleOnCurrentFloor, TakeReward);

		MakeTransparentToMouse(layer);
	}

	private static void MakeTransparentToMouse(Node node)
	{
		foreach (var child in node.GetChildren())
		{
			if (child is Control control and not Button)
				control.MouseFilter = Control.MouseFilterEnum.Ignore;

			MakeTransparentToMouse(child);
		}
	}

	/// <summary>
	/// A drowned city, in flat layers, behind everything.
	///
	/// Polygons rather than a generated texture: a 1920x1080 image would be two million pixels of
	/// per-pixel loop to draw a dozen rectangles. It is mostly hidden behind the content column and
	/// shows down the sides, which is where the reference puts it too.
	///
	/// This is also the PERSPECTIVE SHIFT hook, if that sub-theme survives — swap the skyline per
	/// apocalypse and the world behind the board changes with the doom.
	/// </summary>
	private static float Horizon(Vector2 canvas) => canvas.Y * 0.66f;

	private static Node2D BuildBackdrop(Vector2 canvas)
	{
		var backdrop = new Node2D();
		var skyline = Color.FromHtml("#192533");
		var water = Color.FromHtml("#18242F");

		var rng = new System.Random(7);
		for (float x = -40; x < canvas.X + 40; )
		{
			var width = rng.Next(70, 190);
			var height = rng.Next(70, 200);
			var top = Horizon(canvas) - height;

			backdrop.AddChild(
				new Polygon2D
				{
					Color = skyline,
					Polygon =
					[
						new Vector2(x, top),
						new Vector2(x + width, top),
						new Vector2(x + width, Horizon(canvas)),
						new Vector2(x, Horizon(canvas)),
					],
				}
			);

			x += width + rng.Next(6, 26);
		}

		backdrop.AddChild(
			new Polygon2D
			{
				Color = water,
				Polygon =
				[
					new Vector2(0, Horizon(canvas)),
					new Vector2(canvas.X, Horizon(canvas)),
					new Vector2(canvas.X, canvas.Y),
					new Vector2(0, canvas.Y),
				],
			}
		);

		// A few flat wave lines, because an unbroken block of colour does not read as water.
		for (var i = 0; i < 5; i++)
		{
			var y = canvas.Y * (0.60f + i * 0.08f);
			backdrop.AddChild(
				new Line2D
				{
					DefaultColor = skyline,
					Width = 3,
					Points =
					[
						new Vector2(canvas.X * (0.04f + i * 0.02f), y),
						new Vector2(canvas.X * (0.30f - i * 0.02f), y),
					],
				}
			);
			backdrop.AddChild(
				new Line2D
				{
					DefaultColor = skyline,
					Width = 3,
					Points =
					[
						new Vector2(canvas.X * (0.70f + i * 0.02f), y),
						new Vector2(canvas.X * (0.96f - i * 0.02f), y),
					],
				}
			);
		}

		return backdrop;
	}

	private Control BuildBanner()
	{
		var panel = new PanelContainer();
		panel.AddThemeStyleboxOverride("panel", DoomPalette.Box(DoomPalette.Slate));

		var rows = new VBoxContainer();
		_scenarioLabel = DoomPalette.Text("", 38, DoomPalette.Bone, HorizontalAlignment.Left);
		_descriptionLabel = DoomPalette.Text("", 18, DoomPalette.Bone, HorizontalAlignment.Left);
		_descriptionLabel.Modulate = new Color(1, 1, 1, 0.7f);

		rows.AddChild(_scenarioLabel);
		rows.AddChild(_descriptionLabel);
		panel.AddChild(rows);
		return panel;
	}

	private Control BuildOpponent()
	{
		var rows = new VBoxContainer();
		rows.AddThemeConstantOverride("separation", 4);

		// The thing you are trying to kill, given a body. It was the words "THE OPPONENT" over a
		// bar, which is the one place on this screen that had no picture of what it described.
		rows.AddChild(
			new TextureRect
			{
				Texture = DoomArt.Hooded(),
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				CustomMinimumSize = new Vector2(0, 78),
			}
		);

		var stack = new PanelContainer();
		stack.AddThemeStyleboxOverride("panel", DoomPalette.Box(DoomPalette.Navy));

		_opponentHealthBar = new ProgressBar
		{
			ShowPercentage = false,
			CustomMinimumSize = new Vector2(0, 30),
		};
		_opponentHealthBar.AddThemeStyleboxOverride(
			"background",
			DoomPalette.Box(DoomPalette.EmptySlot)
		);
		_opponentHealthBar.AddThemeStyleboxOverride("fill", DoomPalette.Box(DoomPalette.Red));
		stack.AddChild(_opponentHealthBar);

		_opponentHealthLabel = DoomPalette.Text("", 18, DoomPalette.Bone);
		stack.AddChild(_opponentHealthLabel);

		rows.AddChild(Centred(stack));
		return rows;
	}

	private static Control BuildLaneRow(DoomLaneCell[] cells, bool showsTelegraph)
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", DoomLaneCell.Gap);

		for (var lane = 0; lane < DoomBattle.LaneCount; lane++)
		{
			cells[lane] = new DoomLaneCell(showsTelegraph);
			row.AddChild(cells[lane].Root);
		}

		return Centred(row);
	}

	/// <summary>
	/// Centres a band on the board's content column instead of letting it span the viewport.
	///
	/// The banner is the only thing that runs edge to edge. Everything the player reads during a
	/// turn sits in one column the width of the lanes, so the eye has a single place to be.
	/// </summary>
	private static Control Centred(Control inner)
	{
		inner.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
		inner.CustomMinimumSize = new Vector2(DoomLaneCell.RowWidth, inner.CustomMinimumSize.Y);

		var centre = new CenterContainer();
		centre.AddChild(inner);
		return centre;
	}

	/// <summary>
	/// Floor, life, energy, turn — as discs where a disc means something, and text where it does not.
	/// Energy is spent and refilled every turn, so it is shown as pips you can count rather than a
	/// fraction you have to read.
	/// </summary>
	private Control BuildStatusStrip()
	{
		var panel = new PanelContainer();
		panel.AddThemeStyleboxOverride("panel", DoomPalette.Box(DoomPalette.Slate));

		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 18);
		row.Alignment = BoxContainer.AlignmentMode.Center;

		_floorLabel = DoomPalette.Text("", 20, DoomPalette.Bone);
		row.AddChild(_floorLabel);

		var (lifePip, lifeLabel) = DoomPalette.Pip(DoomPalette.Red, 18);
		_lifeLabel = lifeLabel;
		row.AddChild(lifePip);

		_energyPips = new HBoxContainer();
		_energyPips.AddThemeConstantOverride("separation", 6);
		row.AddChild(_energyPips);

		_turnLabel = DoomPalette.Text("", 20, DoomPalette.Bone);
		row.AddChild(_turnLabel);

		panel.AddChild(row);
		return Centred(panel);
	}

	/// <summary>
	/// One gold disc per point of MAX energy, dimmed once spent — so what you have left and what you
	/// started with are the same picture. Rebuilt rather than tweened: max energy can change.
	/// </summary>
	private void RenderEnergy(DoomPlayer player)
	{
		foreach (var child in _energyPips.GetChildren())
		{
			_energyPips.RemoveChild(child);
			child.QueueFree();
		}

		for (var i = 0; i < player.MaxEnergy; i++)
		{
			var spent = i >= player.Energy;
			var dot = new Panel { CustomMinimumSize = new Vector2(20, 20) };

			var box = DoomPalette.Box(
				spent ? DoomPalette.EmptySlot : DoomPalette.Gold,
				spent ? DoomPalette.Gold : DoomPalette.Gold,
				spent ? 2 : 0
			);
			box.CornerRadiusTopLeft = box.CornerRadiusTopRight = 10;
			box.CornerRadiusBottomLeft = box.CornerRadiusBottomRight = 10;
			dot.AddThemeStyleboxOverride("panel", box);

			_energyPips.AddChild(dot);
		}
	}

	/// <summary>
	/// End Turn, and a debug log behind F3.
	///
	/// The log exists because OpponentDamagedEvent was once raised and never rendered: the
	/// Opponent's health fell with nothing on screen saying why, and every state assertion passed.
	/// It is a debugging tool rather than part of the game's face, so it stays hidden until asked
	/// for. Animations will replace it, and then it can go.
	/// </summary>
	private Control BuildFooter()
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 16);

		// A Label grows its OWN minimum size with its content, so an append-only log pushed this row
		// taller every turn and stretched the End Turn button with it. Fixed height, text scrolls
		// inside it, and the line count is capped.
		_logScroll = new ScrollContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(0, 80),
			Visible = false,
		};

		var logPanel = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		logPanel.AddThemeStyleboxOverride("panel", DoomPalette.Box(DoomPalette.EmptySlot));
		_logLabel = DoomPalette.Text("", 15, DoomPalette.Bone, HorizontalAlignment.Left);
		_logLabel.VerticalAlignment = VerticalAlignment.Top;
		logPanel.AddChild(_logLabel);
		_logScroll.AddChild(logPanel);
		row.AddChild(_logScroll);

		// Holds the button's place whether or not the log is showing, so F3 never moves the one
		// control the player actually uses.
		row.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

		_endTurnButton = new Button { Text = "END TURN", CustomMinimumSize = new Vector2(260, 80) };
		_endTurnButton.AddThemeFontSizeOverride("font_size", 24);
		_endTurnButton.Pressed += OnEndTurn;
		row.AddChild(_endTurnButton);

		return Centred(row);
	}

	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventKey { Pressed: true, Keycode: Key.F3 })
			_logScroll.Visible = !_logScroll.Visible;
	}

	/// <summary>Newest first, and CAPPED. An unbounded log is a memory leak with a user interface.</summary>
	private void Report(string line)
	{
		_log.Insert(0, line);
		if (_log.Count > LogLines)
			_log.RemoveRange(LogLines, _log.Count - LogLines);

		_logLabel.Text = string.Join("\n", _log);
	}
}
