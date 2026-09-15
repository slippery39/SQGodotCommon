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

	private Run _run;
	private GameState _state;

	private Label _scenarioLabel;
	private Label _descriptionLabel;
	private Label _opponentHealthLabel;
	private ProgressBar _opponentHealthBar;
	private Label _statusLabel;
	private Label _logLabel;
	private Button _endTurnButton;
	private DoomHandView _hand;

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
		var scenario = StarterContent.ScenarioFor(Seed, _run.Floor);

		var (state, events) = _run.StartBattle(
			scenario,
			StarterContent.CountdownFor(scenario),
			StarterContent.EnemiesFor(_run.Floor),
			opponentHealth: StarterContent.OpponentHealthFor(_run.Floor)
		);

		_state = state;
		Render(events);
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
	private string TryPlay(int cardId, int lane)
	{
		if (_state.GetBattle().IsOver)
			return "the battle is over";

		var action = new PlayCardAction { CardId = cardId, Lane = lane };

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

		_opponentHealthBar.MaxValue = opponent.MaxHealth;
		_opponentHealthBar.Value = opponent.Health;
		_opponentHealthLabel.Text = $"{opponent.Health} / {opponent.MaxHealth}";

		for (var lane = 0; lane < DoomBattle.LaneCount; lane++)
			RenderLane(lane, opponent);

		_statusLabel.Text =
			$"FLOOR {_run.Floor}     LIFE {player.Life} / {player.MaxLife}     "
			+ $"ENERGY {player.Energy} / {player.MaxEnergy}     TURN {battle.TurnNumber}";

		RenderLog(events);
		_hand.Sync(_state.CardsIn(ZoneType.Hand).ToList(), player.Energy);

		_endTurnButton.Disabled = battle.IsOver;
		if (battle.IsOver)
			_endTurnButton.Text = battle.PlayerIsDead ? "YOU DIED" : "OPPONENT DOWN";
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
				DoomResolvedEvent d => $"*** {d.Scenario.ToString().ToUpperInvariant()} LANDS ***",
				PlayerDiedEvent => "*** YOU DIED ***",
				_ => null,
			};

			if (line is not null)
				_logLabel.Text = line + "\n" + _logLabel.Text;
		}
	}

	// ===== Layout =====

	private void BuildUi()
	{
		var layer = new CanvasLayer();
		AddChild(layer);

		var root = new ColorRect { Color = DoomPalette.Navy };
		root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		layer.AddChild(root);

		var margin = new MarginContainer();
		margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		margin.AddThemeConstantOverride("margin_left", 32);
		margin.AddThemeConstantOverride("margin_right", 32);
		margin.AddThemeConstantOverride("margin_top", 24);
		margin.AddThemeConstantOverride("margin_bottom", 24);
		root.AddChild(margin);

		var column = new VBoxContainer();
		column.AddThemeConstantOverride("separation", 14);
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
			message => _logLabel.Text = message + "\n" + _logLabel.Text
		);
	}

	private Control BuildBanner()
	{
		var panel = new PanelContainer();
		panel.AddThemeStyleboxOverride("panel", DoomPalette.Box(DoomPalette.Slate));

		var rows = new VBoxContainer();
		_scenarioLabel = DoomPalette.Text("", 44, DoomPalette.Bone, HorizontalAlignment.Left);
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
		rows.AddChild(DoomPalette.Text("THE OPPONENT", 16, DoomPalette.Bone));

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

	private Control BuildStatusStrip()
	{
		var panel = new PanelContainer();
		panel.AddThemeStyleboxOverride("panel", DoomPalette.Box(DoomPalette.Slate));
		_statusLabel = DoomPalette.Text("", 20, DoomPalette.Bone);
		panel.AddChild(_statusLabel);
		return Centred(panel);
	}

	private Control BuildFooter()
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 16);
		// NOT ExpandFill on the row: the log would eat every spare pixel and push the fan off the
		// bottom of the screen, which is exactly what it did.
		var logPanel = new PanelContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(0, 130),
		};
		logPanel.AddThemeStyleboxOverride("panel", DoomPalette.Box(DoomPalette.EmptySlot));
		_logLabel = DoomPalette.Text("", 16, DoomPalette.Bone, HorizontalAlignment.Left);
		_logLabel.VerticalAlignment = VerticalAlignment.Top;
		logPanel.AddChild(_logLabel);
		row.AddChild(logPanel);

		_endTurnButton = new Button
		{
			Text = "END TURN",
			CustomMinimumSize = new Vector2(260, 130),
		};
		_endTurnButton.AddThemeFontSizeOverride("font_size", 24);
		_endTurnButton.Pressed += OnEndTurn;
		row.AddChild(_endTurnButton);

		return Centred(row);
	}
}
