using System.Collections.Immutable;

namespace DoomCore;

/// <summary>
/// The shape of an act, and the order the acts come in. **Both are data tables, on purpose.**
///
/// The layout below is a guess that wants playtesting, and the cheapest possible thing to change is
/// one array. Anything that computed floor kinds with arithmetic — `floor % 4 == 0`, as the old
/// `FloorKindFor` did — makes "put a shop on 5 and an event on 8" a puzzle instead of an edit.
///
/// **A run is now all three acts, in a fixed order.** Not randomised: the escalation is the story,
/// the same reason a theme's dooms run to a schedule rather than a roll.
/// </summary>
public static class ActMap
{
	/// <summary>
	/// What is on each floor of an act, floor 1 first. **Change this to change the act.**
	///
	/// Eight battles, the last of which is the boss. Twenty-four battles across a run, so a deck
	/// tops out near 34 cards against a 10-card starter even if you decline nothing.
	///
	/// The rest on floor 14 is the campfire before the boss and is not decoration: four unbroken
	/// battles at the hardest tier dropped clear rates to 22% when it was measured. Keep something
	/// restful immediately before the last floor.
	///
	/// **EVENT FLOORS ARE NOT IN HERE YET, and that is deliberate.** Events have no implementation,
	/// and a floor that silently behaves like a rest is exactly the no-op this codebase keeps
	/// rediscovering. The two floors they will take are battles until they exist, which makes the
	/// act slightly longer and entirely honest.
	/// </summary>
	public static readonly ImmutableArray<FloorKind> Layout =
	[
		FloorKind.Battle, // 1
		FloorKind.Battle, // 2
		FloorKind.Battle, // 3   <- Event, once events exist
		FloorKind.Battle, // 4
		FloorKind.Shop, //   5
		FloorKind.Battle, // 6
		FloorKind.Battle, // 7
		FloorKind.Battle, // 8   <- Event
		FloorKind.Battle, // 9
		FloorKind.Rest, //   10
		FloorKind.Battle, // 11
		FloorKind.Shop, //   12
		FloorKind.Battle, // 13  <- Event
		FloorKind.Rest, //   14  the campfire before the boss
		FloorKind.Battle, // 15  the boss
	];

	/// <summary>
	/// The acts, in the order a run walks them. **Fixed, not rolled.**
	///
	/// Ordered by measured difficulty and by escalation, which happen to agree: weather and
	/// judgement, then machines, then the dead getting up. The Rising is last because it is the
	/// hardest act and because The Last Host is the right thing to end on.
	/// </summary>
	public static readonly ImmutableArray<DoomTheme> Order =
	[
		DoomTheme.Reckoning,
		DoomTheme.LongEmergency,
		DoomTheme.Rising,
	];

	/// <summary>Floors in one act. Derived from the layout so the two can never disagree.</summary>
	public static int ActLength => Layout.Length;

	/// <summary>Floors in a whole run, all acts.</summary>
	public static int RunLength => ActLength * Order.Length;

	/// <summary>
	/// Which act a run-wide floor belongs to, 0-based. Floor 1 is act 0; floor
	/// <see cref="ActLength"/> + 1 is act 1.
	/// </summary>
	public static int ActIndexFor(int floor) =>
		Math.Clamp((floor - 1) / ActLength, 0, Order.Length - 1);

	/// <summary>The theme of the act a run-wide floor sits in.</summary>
	public static DoomTheme ThemeFor(int floor) => Order[ActIndexFor(floor)];

	/// <summary>
	/// The floor's position WITHIN its act, 1-based. This is what content should ask about —
	/// an act's apocalypse schedule and its boss are relative to the act, not to the run.
	/// </summary>
	public static int FloorInAct(int floor) => ((floor - 1) % ActLength) + 1;

	/// <summary>What is on this floor, read straight off <see cref="Layout"/>.</summary>
	public static FloorKind KindFor(int floor) => Layout[FloorInAct(floor) - 1];

	/// <summary>True on the last floor of an act — where the boss and the act's final doom live.</summary>
	public static bool IsBossFloor(int floor) => FloorInAct(floor) == ActLength;

	/// <summary>
	/// True when this floor ENDS an act and another one follows. The break where life is restored
	/// and the world changes over.
	/// </summary>
	public static bool IsActBreak(int floor) =>
		IsBossFloor(floor) && ActIndexFor(floor) < Order.Length - 1;
}
