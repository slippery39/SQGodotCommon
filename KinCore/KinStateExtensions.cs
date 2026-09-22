using System.Collections.Immutable;
using ImmutableGameObjects;

namespace KinCore;

/// <summary>
/// The API boundary. Presentation layers (KinConsole, the Godot front end) read and drive a
/// battle through here and never construct actions for game flow themselves.
/// </summary>
public static class KinStateExtensions
{
	public static KinBattle GetBattle(this GameState s) =>
		(KinBattle)s.GetObject(s.GetWellKnownId(KinObjectKeys.Battle));

	public static KinPlayer GetPlayer(this GameState s) =>
		(KinPlayer)s.GetObject(s.GetWellKnownId(KinObjectKeys.Player));

	public static Opponent GetOpponent(this GameState s) =>
		(Opponent)s.GetObject(s.GetWellKnownId(KinObjectKeys.Opponent));

	public static int ZoneId(this GameState s, ZoneType zone) =>
		s.GetWellKnownId(
			zone switch
			{
				ZoneType.Draw => KinObjectKeys.Draw,
				ZoneType.Hand => KinObjectKeys.Hand,
				ZoneType.Discard => KinObjectKeys.Discard,
				ZoneType.Field => KinObjectKeys.Field,
				ZoneType.Enemies => KinObjectKeys.Enemies,
				ZoneType.Exhausted => KinObjectKeys.Exhausted,
				ZoneType.Taken => KinObjectKeys.Taken,
				_ => throw new ArgumentOutOfRangeException(nameof(zone)),
			}
		);

	public static IEnumerable<KinCard> CardsIn(this GameState s, ZoneType zone) =>
		s.GetChildren(s.ZoneId(zone)).OfType<KinCard>();

	public static IEnumerable<Enemy> LivingEnemies(this GameState s) =>
		s.GetChildren(s.ZoneId(ZoneType.Enemies)).OfType<Enemy>().Where(e => !e.IsDead);

	/// <summary>Units on the Field. This is what a doom scenario reads each time it fires.</summary>
	public static IEnumerable<KinCard> Units(this GameState s) =>
		s.CardsIn(ZoneType.Field).Where(c => c.HasComponent<UnitComponent>());

	public static UnitComponent Unit(this KinCard card) =>
		card.GetComponent<UnitComponent>()
		?? throw new InvalidOperationException($"{card.Name} is not a unit");

	/// <summary>
	/// Your living unit in a lane, or null. At most one — <see cref="PlayCardAction"/> refuses to
	/// play into an occupied lane, so anything reading a lane can assume a single occupant.
	///
	/// Dead units are excluded rather than removed: a unit that died this turn is still on the
	/// Field until <c>ClearTheDead</c> runs, and it must not soak another lane's attack.
	/// </summary>
	public static KinCard? UnitInLane(this GameState s, int lane) =>
		s.Units().FirstOrDefault(c => !c.Unit().IsDead && c.Unit().Lane == lane);

	/// <summary>The living enemy in a lane, or null.</summary>
	public static Enemy? EnemyInLane(this GameState s, int lane) =>
		s.LivingEnemies().FirstOrDefault(e => e.Lane == lane);

	/// <summary>Lanes with no living unit of yours. These are what an enemy attack lands through.</summary>
	public static IEnumerable<int> OpenLanes(this GameState s) =>
		Enumerable.Range(0, KinBattle.LaneCount).Where(l => s.UnitInLane(l) is null);

	/// <summary>
	/// Lanes you hold with something a Devour card is allowed to eat — so, not the companion's.
	/// `PlayCardAction` refuses to build over the companion, and offering that lane would be
	/// offering an invalid move.
	/// </summary>
	public static IEnumerable<int> HeldLanes(this GameState s) =>
		Enumerable
			.Range(0, KinBattle.LaneCount)
			.Where(l => s.UnitInLane(l) is { } held && !held.HasComponent<CompanionComponent>());

	/// <summary>
	/// **Ends the battle if somebody just died, and it is the ONE account of a battle ending.**
	///
	/// This used to live inline in `EndTurnAction` and nowhere else, which meant a battle could
	/// only end when a turn ended. That was invisible while all damage came from lanes trading —
	/// and then direct-damage cards arrived, and killing the Opponent with one left the fight
	/// running until the player pressed End Turn on a corpse. **Found by playing, not by a test.**
	///
	/// So it is called from `DealDamageAction` too, which is the single place every mid-turn point
	/// of damage passes through — player, Opponent, enemy and unit alike. Guarding there rather
	/// than in each card is what stops the next direct-damage card reopening this.
	///
	/// **Idempotent**: a battle already over is left exactly as it is, so calling it after every
	/// packet of damage costs nothing and cannot double-fire the events.
	///
	/// **Order is a rule, not a preference.** The player's death is checked FIRST, so a packet that
	/// would kill both is a loss — the run ending outranks winning the battle.
	/// </summary>
	public static (GameState State, ImmutableList<GameEvent> Events) SettleBattleEnd(
		this GameState s
	)
	{
		var battle = s.GetBattle();
		if (battle.IsOver)
			return (s, ImmutableList<GameEvent>.Empty);

		if (s.GetPlayer().Life <= 0)
			return (
				s.UpdateObject(battle.Id, battle with { IsOver = true, PlayerIsDead = true }),
				[new PlayerDiedEvent()]
			);

		if (s.GetOpponent().IsDead)
			return (
				s.UpdateObject(battle.Id, battle with { IsOver = true, OpponentDefeated = true }),
				[new OpponentDefeatedEvent()]
			);

		return (s, ImmutableList<GameEvent>.Empty);
	}

	/// <summary>
	/// Starts the battle: opening hand drawn, turn 1 begun. Single entry point for every
	/// presentation layer — nobody constructs StartBattleAction directly.
	/// </summary>
	public static (GameState State, ImmutableList<GameEvent> Events) BeginBattle(
		this GameState s,
		int openingHand = 5
	) => s.AddAction(new StartBattleAction { OpeningHandSize = openingHand }).ProcessAllActions();
}
