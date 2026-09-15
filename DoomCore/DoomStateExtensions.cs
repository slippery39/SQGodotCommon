using ImmutableGameObjects;

namespace DoomCore;

/// <summary>
/// The API boundary. Presentation layers (DoomConsole, the Godot front end) read and drive a
/// battle through here and never construct actions for game flow themselves.
/// </summary>
public static class DoomStateExtensions
{
	public static DoomBattle GetBattle(this GameState s) =>
		(DoomBattle)s.GetObject(s.GetWellKnownId(DoomObjectKeys.Battle));

	public static DoomPlayer GetPlayer(this GameState s) =>
		(DoomPlayer)s.GetObject(s.GetWellKnownId(DoomObjectKeys.Player));

	public static Opponent GetOpponent(this GameState s) =>
		(Opponent)s.GetObject(s.GetWellKnownId(DoomObjectKeys.Opponent));

	public static int ZoneId(this GameState s, ZoneType zone) =>
		s.GetWellKnownId(
			zone switch
			{
				ZoneType.Draw => DoomObjectKeys.Draw,
				ZoneType.Hand => DoomObjectKeys.Hand,
				ZoneType.Discard => DoomObjectKeys.Discard,
				ZoneType.Field => DoomObjectKeys.Field,
				ZoneType.Enemies => DoomObjectKeys.Enemies,
				_ => throw new ArgumentOutOfRangeException(nameof(zone)),
			}
		);

	public static IEnumerable<DoomCard> CardsIn(this GameState s, ZoneType zone) =>
		s.GetChildren(s.ZoneId(zone)).OfType<DoomCard>();

	public static IEnumerable<Enemy> LivingEnemies(this GameState s) =>
		s.GetChildren(s.ZoneId(ZoneType.Enemies)).OfType<Enemy>().Where(e => !e.IsDead);

	/// <summary>Units on the Field. This is what a doom scenario reads at countdown 0.</summary>
	public static IEnumerable<DoomCard> Units(this GameState s) =>
		s.CardsIn(ZoneType.Field).Where(c => c.HasComponent<UnitComponent>());

	public static UnitComponent Unit(this DoomCard card) =>
		card.GetComponent<UnitComponent>()
		?? throw new InvalidOperationException($"{card.Name} is not a unit");

	/// <summary>
	/// Your living unit in a lane, or null. At most one — <see cref="PlayCardAction"/> refuses to
	/// play into an occupied lane, so anything reading a lane can assume a single occupant.
	///
	/// Dead units are excluded rather than removed: a unit that died this turn is still on the
	/// Field until <c>ClearTheDead</c> runs, and it must not soak another lane's attack.
	/// </summary>
	public static DoomCard? UnitInLane(this GameState s, int lane) =>
		s.Units().FirstOrDefault(c => !c.Unit().IsDead && c.Unit().Lane == lane);

	/// <summary>The living enemy in a lane, or null.</summary>
	public static Enemy? EnemyInLane(this GameState s, int lane) =>
		s.LivingEnemies().FirstOrDefault(e => e.Lane == lane);

	/// <summary>Lanes with no living unit of yours. These are what an enemy attack lands through.</summary>
	public static IEnumerable<int> OpenLanes(this GameState s) =>
		Enumerable.Range(0, DoomBattle.LaneCount).Where(l => s.UnitInLane(l) is null);

	/// <summary>
	/// Starts the battle: opening hand drawn, turn 1 begun. Single entry point for every
	/// presentation layer — nobody constructs StartBattleAction directly.
	/// </summary>
	public static (
		GameState State,
		System.Collections.Immutable.ImmutableList<GameEvent> Events
	) BeginBattle(this GameState s, int openingHand = 5) =>
		s.AddAction(new StartBattleAction { OpeningHandSize = openingHand }).ProcessAllActions();
}
