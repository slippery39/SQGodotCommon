using System.Collections.Immutable;
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

	private readonly PanelContainer[] _enemySlots = new PanelContainer[DoomBattle.LaneCount];
	private readonly Label[] _enemyLabels = new Label[DoomBattle.LaneCount];
	private readonly Label[] _telegraphLabels = new Label[DoomBattle.LaneCount];
	private readonly PanelContainer[] _unitSlots = new PanelContainer[DoomBattle.LaneCount];
	private readonly Label[] _unitLabels = new Label[DoomBattle.LaneCount];

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

		_endTurnButton.Disabled = battle.IsOver;
		if (battle.IsOver)
			_endTurnButton.Text = battle.PlayerIsDead ? "YOU DIED" : "OPPONENT DOWN";
	}

	private void RenderLane(int lane, Opponent opponent)
	{
		var enemy = _state.EnemyInLane(lane);
		_enemyLabels[lane].Text = enemy is null
			? "-"
			: $"{enemy.Name}\n{enemy.IntentAmount} atk    {enemy.Health} hp";
		_enemySlots[lane]
			.AddThemeStyleboxOverride(
				"panel",
				enemy is null
					? DoomPalette.Box(DoomPalette.EmptySlot, DoomPalette.Slate)
					: DoomPalette.Box(DoomPalette.Slate, DoomPalette.Red)
			);

		// The summon telegraph: ONE symbol in the lane it is coming to. The delay between the
		// announcement and the body landing is what keeps the Opponent reachable at all, so this
		// marker is load-bearing rather than polish — see DoomUI.md. The player only acts on WHICH
		// lane is closing, so it carries no stats.
		_telegraphLabels[lane].Visible = opponent.NextSummon is { } summon && summon.Lane == lane;

		var card = _state.UnitInLane(lane);
		var isCompanion = card?.HasComponent<CompanionComponent>() == true;

		_unitLabels[lane].Text = card is null
			? "-"
			: $"{card.Name}\n{card.Unit().Power} atk    {card.Unit().RemainingToughness} hp";
		_unitLabels[lane]
			.AddThemeColorOverride("font_color", isCompanion ? DoomPalette.Gold : DoomPalette.Bone);
		_unitSlots[lane]
			.AddThemeStyleboxOverride(
				"panel",
				card is null ? DoomPalette.Box(DoomPalette.EmptySlot, DoomPalette.Slate)
					: isCompanion ? DoomPalette.Box(DoomPalette.Slate, DoomPalette.Gold, 3)
					: DoomPalette.Box(DoomPalette.Slate, DoomPalette.Bone)
			);
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
		column.AddChild(BuildLaneRow(_enemySlots, _enemyLabels, _telegraphLabels));
		column.AddChild(BuildLaneRow(_unitSlots, _unitLabels, null));
		column.AddChild(BuildStatusStrip());
		column.AddChild(BuildFooter());
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

		rows.AddChild(stack);
		return rows;
	}

	private Control BuildLaneRow(PanelContainer[] slots, Label[] labels, Label[] telegraphs)
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 10);

		for (var lane = 0; lane < DoomBattle.LaneCount; lane++)
		{
			var slot = new PanelContainer
			{
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
				CustomMinimumSize = new Vector2(0, 110),
			};

			var cell = new VBoxContainer();
			var label = DoomPalette.Text("-", 18, DoomPalette.Bone);
			label.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
			label.VerticalAlignment = VerticalAlignment.Center;
			cell.AddChild(label);

			if (telegraphs is not null)
			{
				var telegraph = DoomPalette.Text("INCOMING", 14, DoomPalette.Gold);
				telegraph.Visible = false;
				telegraphs[lane] = telegraph;
				cell.AddChild(telegraph);
			}

			slot.AddChild(cell);
			row.AddChild(slot);

			slots[lane] = slot;
			labels[lane] = label;
		}

		return row;
	}

	private Control BuildStatusStrip()
	{
		var panel = new PanelContainer();
		panel.AddThemeStyleboxOverride("panel", DoomPalette.Box(DoomPalette.Slate));
		_statusLabel = DoomPalette.Text("", 20, DoomPalette.Bone);
		panel.AddChild(_statusLabel);
		return panel;
	}

	private Control BuildFooter()
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 16);
		row.SizeFlagsVertical = Control.SizeFlags.ExpandFill;

		var logPanel = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		logPanel.AddThemeStyleboxOverride("panel", DoomPalette.Box(DoomPalette.EmptySlot));
		_logLabel = DoomPalette.Text("", 16, DoomPalette.Bone, HorizontalAlignment.Left);
		_logLabel.VerticalAlignment = VerticalAlignment.Top;
		logPanel.AddChild(_logLabel);
		row.AddChild(logPanel);

		_endTurnButton = new Button { Text = "END TURN", CustomMinimumSize = new Vector2(220, 0) };
		_endTurnButton.Pressed += OnEndTurn;
		row.AddChild(_endTurnButton);

		return row;
	}
}
