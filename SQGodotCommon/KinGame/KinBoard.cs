using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Godot;
using ImmutableGameObjects;
using KinCore;

namespace KinGame;

/// <summary>
/// The battle screen. **Presentation only** — see KinUI.md.
///
/// Every number here is READ from KinStateExtensions and never computed. A UI that works out a
/// game fact for itself is a second rules engine, and it will drift from the first. If something
/// cannot be read, the fix is a reader in KinCore, not arithmetic in this file.
///
/// Built in code rather than as a .tscn because the layout is a contract, and a contract is easier
/// to review as text than as a scene diff.
/// </summary>
public partial class KinBoard : Node2D
{
	/// <summary>
	/// Rolled once per run, and SHOWN on the theme-select screen so a run can be named and replayed.
	///
	/// It was a const 42, which made every playthrough of the build byte-identical: same Opponents,
	/// same traits, same three cards offered on every floor. The schedule is meant to be the fixed
	/// part; the enemies and the rewards are what the seed is for.
	///
	/// Kept small so `RewardsFor`'s `seed * 104729` stays a long way from overflowing.
	/// </summary>
	private int _seed;

	/// <summary>How much log to keep. Enough to watch a turn resolve, not enough to grow forever.</summary>
	private const int LogLines = 40;

	private Run _run;
	private GameState _state;

	private Label _actLabel;
	private TextureRect _opponentFigure;
	private Label _descriptionLabel;
	private Label _opponentHealthLabel;
	private ProgressBar _opponentHealthBar;
	private Label _floorLabel;
	private Label _lifeLabel;
	private Control _lifePip;
	private CanvasLayer _layer;
	private Control _overlay;

	/// <summary>
	/// Who was standing in each lane at the LAST repaint, by object id.
	///
	/// The board animates a lane by comparing this to what is there now: gained a body, pop it;
	/// lost one, flash it. The alternative was putting a `Lane` on `UnitDiedEvent` and
	/// `EnemyDiedEvent` purely to serve the front end, and a death event does not otherwise need to
	/// know where it happened.
	///
	/// **This is not the UI computing a game fact.** It decides nothing and nothing reads it but the
	/// animator; it is bookkeeping about what this screen drew last time, which is the one kind of
	/// state a view is entitled to keep.
	/// </summary>
	private readonly int[] _lastEnemyInLane = new int[KinBattle.LaneCount];
	private readonly int[] _lastUnitInLane = new int[KinBattle.LaneCount];
	private Label _turnLabel;
	private HBoxContainer _energyPips;
	private Label _logLabel;
	private ScrollContainer _logScroll;
	private readonly List<string> _log = [];
	private Button _endTurnButton;
	private KinHandView _hand;
	private KinIntermission _intermission;
	private KinCompanionSelect _companionSelect;
	private KinShop _shop;
	private KinCardInspector _inspector;

	/// <summary>Stops AfterBattle being applied twice: Render runs on every action, IsOver latches.</summary>
	private bool _battleResolved;

	private readonly KinLaneCell[] _enemyLanes = new KinLaneCell[KinBattle.LaneCount];
	private readonly KinLaneCell[] _unitLanes = new KinLaneCell[KinBattle.LaneCount];

	public override void _Ready()
	{
		// A run you cannot name is a run you cannot report a bug about, so the seed is rolled here
		// and shown on the status strip — it used to live on the theme-select screen, which is gone.
		// `-- --seed=N` pins it, so a capture of a particular board can be taken twice.
		_seed = int.TryParse(UserArg("--seed="), out var pinned)
			? pinned
			: (int)GD.RandRange(1, 9999);
		KinAnimator.LoadConfiguredSpeed();

		BuildUi();

		// **The game just starts.** There used to be a screen here asking which act to walk into,
		// and chaining the acts made that choice vanish — a run is all three, in a fixed order — so
		// it had been showing a decision that did nothing.
		//
		// `--autostart` went with it. It existed only because `--write-movie` cannot click a button,
		// so a capture without it was a picture of that menu; there is nothing left to skip. The
		// capture flags below still do real work.
		{
			StartRun();

			// `-- --autoturn` ends a turn on a timer. **Animation cannot be verified
			// from a still board**: nothing moves until state changes, so every capture of a fresh
			// battle shows a settled screen and proves nothing. This drives real turns through the
			// real engine so a capture catches damage numbers, pops and flashes mid-flight.
			// `-- --reward` opens the reward screen on the opening position. The screen
			// is only reachable by WINNING a floor, which a capture cannot do, so without this the
			// one screen where a card is the whole decision is the one screen never looked at.
			if (System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--reward") >= 0)
			{
				_hand.SetVisible(false);

				_intermission.ShowFloorCleared(_run, _run);

				// **Forced to a floor that actually offers upgrades**, the same way `--shop` forces
				// gold and a big deck: a capture's job is the fullest case the screen has to draw,
				// and floor 1 is never an upgrade floor so the row would never appear in a capture.
				_intermission.OfferUpgrades(
					StarterContent.UpgradesFor(_seed, StarterContent.FloorsPerUpgrade)
				);
				_intermission.OfferRewards(
					StarterContent.RewardsFor(_run.Theme, _seed, _run.Floor),
					_run.Floor
				);

				return;
			}

			// `-- --shop` opens the shop on the opening position. Like `--reward`, the
			// screen is otherwise only reachable by playing to a shop floor, which a capture cannot
			// do — so without this the layout would ship having never been looked at.
			//
			// Gold and a deck are forced to the WORST case the screen has to draw: enough to afford
			// everything, and a deck large enough to test the removal grid's sizing.
			if (System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--shop") >= 0)
			{
				_hand.SetVisible(false);
				_run = _run with { Gold = 400 };

				foreach (var card in StarterContent.RewardPool(_run.Theme))
					_run = _run.WithCard(card).WithCard(card);

				_shop.Show(
					StarterContent.ShopFor(_run.Theme, _seed, _run.Floor, _run.CardsRemoved),
					removing: System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--remove") >= 0
				);

				return;
			}

			if (System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--autoturn") >= 0)
			{
				var ticker = new Timer { WaitTime = 1.6, Autostart = true };
				ticker.Timeout += OnEndTurn;
				AddChild(ticker);
			}
		}
	}

	/// <summary>
	/// Starts the run. **A run is ALL THREE ACTS, in the fixed order in `ActMap.Order`** — the theme
	/// is derived from the floor you are standing on, so there has been nothing to choose since the
	/// acts were chained.
	///
	/// `KinThemeSelect` was deleted rather than left showing a choice that did nothing — an act
	/// picker stopped meaning anything once a run became all three acts in a fixed order. The note
	/// it left behind said the replacement should pick the COMPANION, and
	/// <see cref="KinCompanionSelect"/> is that.
	///
	/// **A capture cannot click, and EVERY capture flag needs `_run` to already exist** — `--reward`,
	/// `--shop` and `--autoturn` all read it on the line after this returns. So any user argument at
	/// all means a scripted run: take the default companion and start. Checking only the flag that
	/// happened to be tested would leave the other three dereferencing a null run.
	///
	/// This is the same job `--autostart` used to do for the act picker.
	/// </summary>
	private void StartRun()
	{
		if (OS.GetCmdlineUserArgs().Length > 0)
		{
			BeginRunWith(CaptureCompanion(), CaptureFloor());
			return;
		}

		_hand.SetVisible(false);
		_companionSelect.Show();
	}

	/// <summary>Takes the choice and starts the first floor. The only way past the select screen.</summary>
	private void BeginRunWith(Companion companion, int floor = 1)
	{
		_companionSelect.Hide();
		_run = StarterContent.NewRun(_seed, companion) with { Floor = floor };
		StartBattleOnCurrentFloor();
	}

	/// <summary>
	/// `-- --companion=Pike` plays a capture as someone other than the starter. **A capture cannot
	/// click the select screen**, so without this the archetype starters are the one thing no
	/// screenshot could show. An unknown name falls back to the starter and says so in the log.
	/// </summary>
	private static Companion CaptureCompanion()
	{
		var name = UserArg("--companion=");
		if (name is null)
			return StarterContent.StarterCompanion;

		var found = StarterContent.Roster.FirstOrDefault(c =>
			c.Name.Equals(name, System.StringComparison.OrdinalIgnoreCase)
		);
		if (found is null)
			GD.PushWarning($"--companion={name}: no such companion, using the starter");

		return found ?? StarterContent.StarterCompanion;
	}

	/// <summary>
	/// `-- --floor=6` opens on floor 6. **Floor 1 fields no enemy with a trait**, so a capture of the
	/// opening position could never show a Flier, a Razorback or a Flail Knight on the board — the
	/// same reason `--reward` forces an upgrade floor.
	/// </summary>
	private static int CaptureFloor() =>
		int.TryParse(UserArg("--floor="), out var floor) && floor >= 1 ? floor : 1;

	private static string UserArg(string prefix) =>
		OS.GetCmdlineUserArgs()
			.FirstOrDefault(a => a.StartsWith(prefix, System.StringComparison.Ordinal))
			?[prefix.Length..];

	/// <summary>
	/// Begins the battle for whatever floor the run is on. The run outlives the battle — deck, life,
	/// floor and companion all persist — so this is the only thing a new floor needs.
	/// </summary>
	private void StartBattleOnCurrentFloor()
	{
		_battleResolved = false;
		_intermission.Hide();

		// Hidden here as well as on the way out of the shop: this is the one path every floor goes
		// through, so a screen left up by any route is taken down by this one.
		_shop?.Hide();
		_hand?.SetVisible(true);

		// A new battle is a new board, not a change to the old one. Without this every lane of the
		// opening position pops in and the last floor's dead bodies flash on a field they were
		// never on — the animator would be describing a transition that did not happen.
		_firstPaint = true;
		System.Array.Clear(_lastEnemyInLane);
		System.Array.Clear(_lastUnitInLane);

		// Not every floor is a battle. The front end asks the SAME content function the simulator
		// does — if these two ever disagree about what a floor is, every measured number is about
		// a game nobody plays.
		var kind = StarterContent.FloorKindFor(_run.Floor);

		if (kind == FloorKind.Rest)
		{
			var rested = _run.Rest(StarterContent.RestHealFor(_run.MaxLife));
			_intermission.ShowRest(_run, rested);
			_run = rested;
			return;
		}

		if (kind == FloorKind.Shop)
		{
			_hand.SetVisible(false);
			_shop.Show(StarterContent.ShopFor(_run.Theme, _seed, _run.Floor, _run.CardsRemoved));
			return;
		}

		var (state, events) = _run.StartBattle(
			StarterContent.EnemiesFor(_run.Floor, _seed),
			opponent: StarterContent.OpponentFor(_run.Floor, _seed)
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

		// The hand is not yours to play between floors, and it drew over the intermission.
		_hand.SetVisible(false);

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

		_intermission.ShowFloorCleared(before, after);

		// Mirrors RunSimulator: the offers are for the floor you are about to walk into, not the
		// one just cleared. `UpgradesFor` returns nothing on a floor that offers none, so the
		// frequency dial lives in content and this screen never has to know it.
		_intermission.OfferUpgrades(StarterContent.UpgradesFor(_seed, after.Floor));
		_intermission.OfferRewards(
			StarterContent.RewardsFor(after.Theme, _seed, after.Floor),
			after.Floor
		);
	}

	/// <summary>
	/// Buys a card. **The RUN decides whether it can be afforded**, not this screen — the same rule
	/// that keeps the reward screen from computing a deck diff of its own.
	/// </summary>
	private void BuyCard(RunCard card)
	{
		var offer = StarterContent.ShopFor(_run.Theme, _seed, _run.Floor, _run.CardsRemoved);
		_run = _run.BuyCard(card, offer.CardPrice);
	}

	/// <summary>
	/// Takes a card out of the run for good — the first thing other than an apocalypse that can.
	/// `Run.RemoveCard` refuses below `Run.MinDeckSize`, so the floor is enforced where the rule
	/// lives rather than by the button being greyed out.
	/// </summary>
	private void RemoveCard(int runCardId)
	{
		var offer = StarterContent.ShopFor(_run.Theme, _seed, _run.Floor, _run.CardsRemoved);
		_run = _run.RemoveCard(runCardId, offer.RemovalPrice);
	}

	private void BuyHeal()
	{
		var offer = StarterContent.ShopFor(_run.Theme, _seed, _run.Floor, _run.CardsRemoved);
		_run = _run.BuyHeal(offer.HealPrice, offer.HealAmount);
	}

	/// <summary>Walks out of the shop and onto the next floor.</summary>
	private void LeaveShop()
	{
		_shop.Hide();
		_run = _run with { Floor = _run.Floor + 1 };
		StartBattleOnCurrentFloor();
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

	/// <summary>
	/// Takes a companion upgrade. **Does NOT advance the floor** — unlike a card, which closes the
	/// screen, this is one of two independent decisions on it and DESCEND is the way down.
	/// </summary>
	private void TakeUpgrade(CompanionUpgrade upgrade) => _run = _run.WithCompanionUpgrade(upgrade);

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
		for (var lane = 0; lane < KinBattle.LaneCount; lane++)
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

		// A body needs a lane — and so does a rite that READS a lane, since the lane it lands on is
		// what it acts upon. Asking `KinCardFace` keeps this from being a second opinion about
		// which cards those are; the rule itself lives in `KinTargeting`.
		var needsLane =
			_state.HasObject(cardId)
			&& _state.GetObject(cardId) is KinCard card
			&& KinCardFace.NeedsALane(card);

		if (needsLane && lane is null)
			return "drop this on one of your lanes";

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

		// The band the doom clock used to own. It says WHERE you are now — which act, which floor
		// of it — because that is the only thing left on this screen the board itself cannot show.
		_actLabel.Text = KinPalette.Caps(ThemeLibrary.Of(_run.Theme).Name);
		_descriptionLabel.Text = $"Floor {_run.Floor}";

		// Clamped for DISPLAY only. Overkill leaves real health negative, which is correct in the
		// engine and reads as a bug on a health bar: the last hit showed "-1 / 26".
		// Each Opponent gets its own drawing where one has been made, and the generated hood where
		// none has. `The Choir` and `The Last Warden` are still on the fallback.
		_opponentFigure.Texture = KinArt.Drawing(opponent.Name) ?? KinArt.Hooded();

		var shown = System.Math.Max(opponent.Health, 0);
		_opponentHealthBar.MaxValue = opponent.MaxHealth;
		_opponentHealthBar.Value = shown;
		_opponentHealthLabel.Text = $"{shown} / {opponent.MaxHealth}";

		for (var lane = 0; lane < KinBattle.LaneCount; lane++)
			RenderLane(lane, opponent);

		AnimateEvents(events);

		_floorLabel.Text = $"FLOOR {_run.Floor}   ACT {_run.ActIndex + 1}   SEED {_seed}";
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

		// LAST. Everything above it asks "is this the opening position", and the answer has to stay
		// true for the whole of that repaint.
		_firstPaint = false;
	}

	private void RenderLane(int lane, Opponent opponent)
	{
		var enemy = _state.EnemyInLane(lane);
		if (enemy is null)
			_enemyLanes[lane].ShowEmpty();
		else
			_enemyLanes[lane].ShowEnemy(enemy);

		AnimateLane(_enemyLanes[lane].Root, _lastEnemyInLane, lane, enemy?.Id ?? 0);

		// The summon telegraph: ONE marker in the lane it is coming to. The delay between the
		// announcement and the body landing is what keeps the Opponent reachable at all, so this is
		// load-bearing rather than polish — see KinUI.md. The player only acts on WHICH lane is
		// closing, so it carries no stats.
		_enemyLanes[lane]
			.SetTelegraph(opponent.NextSummon is { } summon && summon.Lane == lane);

		var card = _state.UnitInLane(lane);
		if (card is null)
			_unitLanes[lane].ShowEmpty();
		else
			_unitLanes[lane].ShowUnit(card, card.HasComponent<CompanionComponent>());

		AnimateLane(_unitLanes[lane].Root, _lastUnitInLane, lane, card?.Id ?? 0);
	}

	/// <summary>
	/// Marks a lane that changed hands, and records who holds it now.
	///
	/// A body ARRIVING pops; a body LEAVING flashes red. The same two cases cover a card you played,
	/// an enemy the Opponent dropped in, a unit that died and an enemy you killed — which is why
	/// this is driven by occupancy rather than by four separate events.
	/// </summary>
	private void AnimateLane(Control cell, int[] previous, int lane, int occupant)
	{
		var before = previous[lane];
		previous[lane] = occupant;

		// First paint of a battle. Everything would "arrive" at once and the board would jitter its
		// way in, so the opening position is simply drawn.
		if (_firstPaint)
			return;

		if (occupant != 0 && occupant != before)
			KinAnimator.Pop(cell);
		else if (occupant == 0 && before != 0)
			KinAnimator.Flash(cell, KinPalette.Red);
	}

	private bool _firstPaint = true;

	/// <summary>
	/// The numbers that moved, floated off the thing they came out of.
	///
	/// Lane changes are NOT handled here — see <see cref="AnimateLane"/>. What is left is the
	/// arithmetic a player would otherwise have to notice by watching a bar: damage to the
	/// Opponent, damage to you, and life coming back.
	/// </summary>
	private void AnimateEvents(ImmutableList<GameEvent> events)
	{
		if (KinAnimator.Instant || _firstPaint)
			return;

		// **Damage is SUMMED over the batch, not floated per event.**
		//
		// `EndTurnAction` damages the player once per attacking enemy lane and the Opponent once per
		// open lane, so a single End Turn can raise five PlayerDamagedEvents and five
		// OpponentDamagedEvents. Floated one each, they spawn at the same point in the same frame
		// and overlap into an unreadable smear — which is exactly what the playtest reported: the
		// numbers bunch up and you cannot see what you dealt.
		//
		// It also fixes the shake looking random. Each Shake captured the layer's CURRENT offset as
		// its home, so the second one in a frame captured a home that the first had already moved,
		// and they fought: sometimes a jolt, sometimes nothing, sometimes a board left off-centre.
		// One total, one number, one shake.
		var dealt = events.OfType<OpponentDamagedEvent>().Sum(e => e.Amount);
		var taken = events.OfType<PlayerDamagedEvent>().Sum(e => e.Amount);
		var healed = events.OfType<LifeGainedEvent>().Sum(e => e.Amount);

		if (dealt > 0)
		{
			KinAnimator.Float(_overlay, _opponentHealthBar, $"-{dealt}", KinPalette.Bone);
			KinAnimator.Flash(_opponentHealthBar, new Color(1.9f, 1.9f, 1.9f));
		}

		// Losing life gets the shake as well as the number. It is the only counter in the game that
		// cannot be rebuilt, so it is the one that never scrolls past quietly.
		if (taken > 0)
		{
			KinAnimator.Float(_overlay, _lifePip, $"-{taken}", KinPalette.Red);
			KinAnimator.Shake(_layer);
		}

		if (healed > 0)
			KinAnimator.Float(_overlay, _lifePip, $"+{healed}", KinPalette.Bone);
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
				EnemySummonedEvent s => $"{s.EnemyName} drops into L{s.Lane}",
				EnemyTelegraphedEvent g => $"they are bringing up {g.EnemyName} for L{g.Lane}",
				OpponentDamagedEvent o => $"hit the Opponent for {o.Amount}",
				OpponentDefeatedEvent => "THE OPPONENT IS DOWN",
				// The two halves of a taking. Nothing in content takes today; the lines stay
				// because an effect that changes the board and says nothing is the one thing
				// KinUI.md forbids outright.
				CardsTakenEvent t when t.Count > 0 => t.Count == 1
					? $"1 card taken for {t.Turns} turns"
					: $"{t.Count} cards taken for {t.Turns} turns",
				CardsReturnedEvent r => r.Count == 1
					? "1 taken card comes back"
					: $"{r.Count} taken cards come back",
				PlayerDiedEvent => "*** YOU DIED ***",
				_ => null,
			};

			if (line is not null)
				Report(line);
		}
	}

	// ===== Layout =====

	private void BuildUi()
	{
		var layer = new CanvasLayer();
		_layer = layer;
		AddChild(layer);

		var root = new ColorRect { Color = KinPalette.Navy };
		root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		layer.AddChild(root);

		// Between the ground colour and the content, so it sits behind the board and shows down the
		// sides. Added to the layer rather than inside the ColorRect so the draw order is explicit.
		layer.AddChild(BuildBackdrop());

		var margin = new MarginContainer();
		margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		margin.AddThemeConstantOverride("margin_left", 32);
		margin.AddThemeConstantOverride("margin_right", 32);
		margin.AddThemeConstantOverride("margin_top", 18);
		margin.AddThemeConstantOverride("margin_bottom", 18);
		layer.AddChild(margin);

		var column = new VBoxContainer();

		// 6, not 10. The bands came to 1096 of 1080 once the Opponent and the lane frame grew;
		// four gaps at four pixels each is the cheapest 16px on the board. `WarnIfColumnOverflows`
		// is what found it, and is what will find the next one.
		column.AddThemeConstantOverride("separation", 6);
		margin.AddChild(column);

		column.AddChild(BuildBanner());
		column.AddChild(BuildOpponent());
		column.AddChild(BuildLaneGrid());
		column.AddChild(BuildStatusStrip());

		// The fan is a Node2D and draws where it is told, so the column reserves the space rather
		// than containing it. Added to the same CanvasLayer so that a dragged card's global
		// position and a lane slot's global rect share one coordinate space — see LaneAt.
		var handSpace = new Control { CustomMinimumSize = new Vector2(0, KinHandView.BandHeight) };
		column.AddChild(handSpace);

		// The BASE canvas, not the window: stretch mode is canvas_items, so this is 1920x1080 and
		// the window scales it down. Placing the fan against the window size puts it off-screen.
		var canvas = GetViewportRect().Size;
		_hand = new KinHandView(
			layer,
			new Vector2(canvas.X / 2, canvas.Y - KinHandView.BandHeight / 2f),
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
		layer.AddChild(BuildLogOverlay());

		// Floating damage numbers live here: a full-rect layer above the board that never takes a
		// click. Parenting them to the thing they describe would let a container clip them the
		// moment they rose past its edge.
		_overlay = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
		_overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		layer.AddChild(_overlay);

		_inspector = new KinCardInspector(layer, new Vector2(1500, 740));
		_intermission = new KinIntermission(
			layer,
			StartBattleOnCurrentFloor,
			TakeReward,
			TakeUpgrade
		);
		_companionSelect = new KinCompanionSelect(layer, c => BeginRunWith(c));

		// **Reads the run through a callback rather than being handed a copy.** A shop that held
		// its own Run would be a second account of the gold and the deck, and it would drift from
		// the one the game actually uses after the first purchase.
		_shop = new KinShop(layer, () => _run, BuyCard, RemoveCard, BuyHeal, LeaveShop);

		MakeTransparentToMouse(layer);
		WarnIfColumnOverflows(column);
	}

	/// <summary>
	/// Shouts if the bands no longer fit the canvas.
	///
	/// **This has now gone wrong three times in one session**, and every time the symptom was the
	/// same and told you nothing: the hand quietly slid under the status strip, because the fan is
	/// positioned against the bottom of the screen while the column grows downward from the top.
	/// Nothing errors, nothing logs, and the band that actually grew is not the band that looks
	/// broken — the last time it was the Opponent's hood, four regions away.
	///
	/// Deferred a frame because a Control has no size until the container has laid it out.
	/// </summary>
	private void WarnIfColumnOverflows(Control column)
	{
		Callable
			.From(() =>
			{
				var canvas = GetViewportRect().Size.Y;

				// The column ALREADY contains the reserved hand space as its last child, so adding
				// BandHeight again counts the fan twice — the first version of this check reported
				// 1456 of 1080 for a board that was sixteen pixels over.
				var used = column.Size.Y;

				if (used > canvas)
					GD.PushWarning(
						$"KinBoard: the bands need {used:0}px of {canvas:0}. The hand will be "
							+ $"{used - canvas:0}px under the status strip. Shrink a band."
					);
			})
			.CallDeferred();
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
	/// The drowned city behind the board.
	///
	/// **A painted image now, not eighty lines of polygons.** The procedural version existed because
	/// there was no art; it drew a dozen rectangles and five wave lines and was mostly hidden behind
	/// the content column anyway. The real thing is one file.
	///
	/// `KeepAspectCovered` rather than `Stretch`: the image is 16:9 and the window may not be, and a
	/// backdrop that squashes is worse than one that crops. The crop is safe because the composition
	/// deliberately keeps its subject in the outer thirds — see docs/mockups/backdrop-prompt.md.
	///
	/// Falls back to the flat ground colour if nobody has dropped an image in, which is also what a
	/// missing `--headless --import` looks like.
	/// </summary>
	private static Control BuildBackdrop()
	{
		var backdrop = new TextureRect
		{
			Texture = KinArt.Backdrop,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};

		backdrop.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		return backdrop;
	}

	private Control BuildBanner()
	{
		var panel = new PanelContainer();
		panel.AddThemeStyleboxOverride("panel", KinPalette.Box(KinPalette.Slate));

		var across = new HBoxContainer();
		across.AddThemeConstantOverride("separation", 24);

		var rows = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		_actLabel = KinPalette.Text("", 40, KinPalette.Bone, HorizontalAlignment.Left);
		_descriptionLabel = KinPalette.Text("", 24, KinPalette.Bone, HorizontalAlignment.Left);
		_descriptionLabel.Modulate = new Color(1, 1, 1, 0.75f);

		rows.AddChild(_actLabel);
		rows.AddChild(_descriptionLabel);
		across.AddChild(rows);

		panel.AddChild(across);
		return panel;
	}

	/// <summary>
	/// The thing you are trying to kill: a hood standing behind its own health bar.
	///
	/// **The bar is a centred pill, not a full-width slab.** Spanning the board it was the loudest
	/// object on screen — a solid red bar the width of five lanes, for a number that changes a few
	/// times a turn — and it flattened the hood into a decoration sitting above a wall. Sized to
	/// roughly a third of the board it reads as *this creature's* health, which is what it is.
	///
	/// The figure OVERLAPS the bar, by a negative separation. That is the mockup's arrangement and
	/// it is doing real work: the two are one object, and stacked with a gap they were two.
	/// </summary>
	private Control BuildOpponent()
	{
		var rows = new VBoxContainer();
		// Enough overlap that the two read as one object, not so much that the hood becomes a sliver
		// poking over a bar — at -20 against a 104px figure it had all but disappeared.
		rows.AddThemeConstantOverride("separation", -16);

		// It was the words "THE OPPONENT" over a bar — the one thing on this screen with no picture
		// of what it described.
		// The texture is set in Render, not here: BuildUi runs before there IS an Opponent, and each
		// one is a different thing wearing the same hood.
		_opponentFigure = new TextureRect
		{
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			CustomMinimumSize = new Vector2(0, 120),
		};
		rows.AddChild(_opponentFigure);

		var stack = new PanelContainer { CustomMinimumSize = new Vector2(OpponentBarWidth, 0) };
		stack.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;

		_opponentHealthBar = new ProgressBar
		{
			ShowPercentage = false,
			CustomMinimumSize = new Vector2(OpponentBarWidth, 40),
		};
		_opponentHealthBar.AddThemeStyleboxOverride(
			"background",
			Pill(KinPalette.EmptySlot, KinPalette.Navy)
		);
		_opponentHealthBar.AddThemeStyleboxOverride("fill", Pill(KinPalette.Red, KinPalette.Red));
		stack.AddChild(_opponentHealthBar);

		_opponentHealthLabel = KinPalette.Text("", 24, KinPalette.Bone);
		stack.AddChild(_opponentHealthLabel);

		// **NOT `Centred`.** That helper forces its child to the lane row's width, which is exactly
		// the full-width slab this rework exists to undo — it would have quietly stretched the pill
		// straight back to 1548 and made the change look like it had not been applied.
		var centre = new CenterContainer();
		centre.AddChild(stack);
		rows.AddChild(centre);

		return rows;
	}

	/// <summary>Roughly a third of the board — the mockup's proportion, and enough for "120 / 120".</summary>
	private const int OpponentBarWidth = 560;

	/// <summary>A fully rounded bar end. A pill, not a rectangle with soft corners.</summary>
	private static StyleBoxFlat Pill(Color fill, Color border)
	{
		var box = KinPalette.Box(fill, border, 3);
		box.CornerRadiusTopLeft = box.CornerRadiusBottomLeft = 20;
		box.CornerRadiusTopRight = box.CornerRadiusBottomRight = 20;
		box.ContentMarginLeft = box.ContentMarginRight = 0;
		return box;
	}

	/// <summary>
	/// Both rows inside one panel.
	///
	/// The mockup frames the ten slots as a single object rather than as two loose rows, and that is
	/// the correct reading of the game: your row and their row are one board, and a lane is a COLUMN
	/// through both. Framed together, the eye travels up and down a lane; framed apart, it travels
	/// along each row and has to do the pairing itself.
	/// </summary>
	private Control BuildLaneGrid()
	{
		var panel = new PanelContainer();

		var box = KinPalette.Box(new Color(0, 0, 0, 0.22f), KinPalette.Slate, 2);
		box.CornerRadiusTopLeft = box.CornerRadiusTopRight = 14;
		box.CornerRadiusBottomLeft = box.CornerRadiusBottomRight = 14;
		box.ContentMarginLeft = box.ContentMarginRight = 14;
		box.ContentMarginTop = box.ContentMarginBottom = 6;
		panel.AddThemeStyleboxOverride("panel", box);

		var rows = new VBoxContainer();
		rows.AddThemeConstantOverride("separation", 10);
		rows.AddChild(BuildLaneRow(_enemyLanes, showsTelegraph: true));
		rows.AddChild(BuildLaneRow(_unitLanes, showsTelegraph: false));
		panel.AddChild(rows);

		return Centred(panel);
	}

	private static Control BuildLaneRow(KinLaneCell[] cells, bool showsTelegraph)
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", KinLaneCell.Gap);

		for (var lane = 0; lane < KinBattle.LaneCount; lane++)
		{
			cells[lane] = new KinLaneCell(showsTelegraph);
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
		inner.CustomMinimumSize = new Vector2(KinLaneCell.RowWidth, inner.CustomMinimumSize.Y);

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
		panel.AddThemeStyleboxOverride("panel", KinPalette.Box(KinPalette.Slate));

		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 18);

		_floorLabel = KinPalette.Text("", 24, KinPalette.Bone);
		row.AddChild(_floorLabel);

		var (lifePip, lifeLabel) = KinPalette.Pip(KinPalette.Red, 24);
		_lifeLabel = lifeLabel;
		_lifePip = lifePip;
		row.AddChild(lifePip);

		_energyPips = new HBoxContainer();
		_energyPips.AddThemeConstantOverride("separation", 6);
		row.AddChild(_energyPips);

		_turnLabel = KinPalette.Text("", 24, KinPalette.Bone);
		row.AddChild(_turnLabel);

		// **End Turn lives here now.** It had a band of its own, 80px tall and otherwise empty, and
		// the column was already 50px over the 1080 canvas because of it — which is why the hand
		// was pressed against the bottom edge with its stat badges half off-screen. The one control
		// the player clicks belongs beside the state it acts on, not in a row by itself.
		row.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

		_endTurnButton = new Button { Text = "END TURN", CustomMinimumSize = new Vector2(240, 62) };
		_endTurnButton.AddThemeFontSizeOverride("font_size", 26);
		_endTurnButton.Pressed += OnEndTurn;
		row.AddChild(_endTurnButton);

		panel.AddChild(row);
		return Centred(panel);
	}

	/// <summary>
	/// One gold disc per point of MAX energy, dimmed once spent — so what you have left and what you
	/// started with are the same picture. Rebuilt rather than tweened: max energy can change.
	/// </summary>
	private void RenderEnergy(KinPlayer player)
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

			var box = KinPalette.Box(
				spent ? KinPalette.EmptySlot : KinPalette.Gold,
				spent ? KinPalette.Gold : KinPalette.Gold,
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
	private Control BuildLogOverlay()
	{
		// An OVERLAY, not a band. As a row in the column it cost 80px of a budget that was already
		// over, for a panel that is hidden in normal play — a debugging tool was charging the game
		// rent. Anchored to a corner it costs nothing until F3 asks for it.
		//
		// A Label grows its OWN minimum size with its content, so an append-only log stretched
		// whatever contained it. Fixed height, text scrolls inside it, line count capped.
		_logScroll = new ScrollContainer
		{
			CustomMinimumSize = new Vector2(620, 240),
			Visible = false,
		};
		_logScroll.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
		_logScroll.Position = new Vector2(32, 120);

		var logPanel = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		logPanel.AddThemeStyleboxOverride("panel", KinPalette.Box(KinPalette.EmptySlot));
		_logLabel = KinPalette.Text("", 17, KinPalette.Bone, HorizontalAlignment.Left);
		_logLabel.VerticalAlignment = VerticalAlignment.Top;
		logPanel.AddChild(_logLabel);
		_logScroll.AddChild(logPanel);

		return _logScroll;
	}

	/// <summary>
	/// Keeps the keyword panel pointed at whatever the cursor is over.
	///
	/// Per frame, because that is the rate `CardUIManager` recomputes the hovered card at — hooking
	/// a signal instead would mean a second opinion about which of several overlapping cards is the
	/// one being read. The panel itself does nothing when the answer has not changed.
	/// </summary>
	public override void _Process(double delta)
	{
		if (_state is null || _inspector is null)
			return;

		_inspector.Follow(_state.CardsIn(ZoneType.Hand).ToDictionary(c => c.Id.ToString()));
	}

	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventKey { Pressed: true, Keycode: Key.F3 })
			_logScroll.Visible = !_logScroll.Visible;

		// F4 cycles the animation speed live. The configured value in ProjectSettings is the one
		// that ships; this is for deciding what that value should BE, which cannot be done by
		// reasoning about it — you have to watch a turn at each speed.
		if (@event is InputEventKey { Pressed: true, Keycode: Key.F4 })
		{
			KinAnimator.Speed = KinAnimator.Speed switch
			{
				>= 2f => 0f,
				>= 1f => 2f,
				> 0f => 1f,
				_ => 1f,
			};

			Report(
				KinAnimator.Instant ? "animation OFF" : $"animation speed {KinAnimator.Speed:0.##}x"
			);
		}
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
