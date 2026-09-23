using System.Collections.Immutable;

namespace KinCore;

/// <summary>
/// **The archetype cards — one set per companion, so picking a companion declares a plan.**
///
/// See the design philosophy at the top of `KinJam.md`: every card should create a decision, and
/// the reward screen only has a question to ask if there is a plan to measure a card against. A
/// companion IS that plan. This file is the vertical slice — Bulwark (Bramble) and Face (Pike) —
/// and the other three archetypes land here the same way once the slice has been played.
///
/// **Generic fantasy names from the start** (see `KinSettingSketches.md`). None has authored art,
/// so each falls back to a generated figure — a known art debt, taken knowingly, and cheaper than
/// drawing cards whose numbers have not survived a playtest yet.
///
/// **Numbers are first guesses in scale with the pool** (1-drop ≈ 14 total stats, 2-drop 26-36,
/// less when the card carries a rule). `sim` decides them, per act.
/// </summary>
public static partial class StarterContent
{
	// ===== BULWARK — Bramble. Survive, and make hitting you expensive =====
	//
	// Thorns is blank in an open lane, so a Bulwark deck survives without closing and must draft
	// its own finisher — that is the trade, and Shieldwall Captain is the finisher it is offered.
	// Its natural victim is Flail Knight (every strike answered); its problem is the Harpy, which
	// goes over the wall.

	public static RunCard BriarSentinel =>
		Unit("Briar Sentinel", 1, 2, 12, "Grown into the gap, and it does not give.") with
		{
			Thorns = 6,
		};

	public static RunCard IronbarkTreant =>
		Unit("Ironbark Treant", 2, 4, 22, "Older than the road, and in the way of it.") with
		{
			Thorns = 10,
			Rarity = KinRarity.Uncommon,
		};

	/// <summary>
	/// **"Your units" includes the companion, and the companion keeps it for the whole battle** —
	/// ordinary units withdraw at end of turn, so on them it is a one-turn grant, but the
	/// companion never leaves. Cast every turn, it stacks on Bramble. That is a deliberate
	/// build-around, not an oversight; if it turns out to be too much, this is where to look.
	/// </summary>
	public static RunCard BarbedBanner =>
		Rite(
			"Barbed Banner",
			1,
			"Raised where the line has to hold.",
			OnPlay(KinTarget.YourUnits, new BuffAction { Thorns = 4 }, "your units gain Thorns 4")
		) with
		{
			Rarity = KinRarity.Uncommon,
		};

	/// <summary>
	/// **Order matters, and that is the decision it creates**: it grants Thorns to what is ALREADY
	/// standing either side of it, so it wants to be played after its neighbours, not before.
	/// </summary>
	public static RunCard HedgeWitch =>
		Unit(
			"Hedge Witch",
			2,
			6,
			12,
			"Knows which thorns to coax.",
			OnPlay(
				KinTarget.YourUnitsInAdjacentLanes,
				new BuffAction { Thorns = 5 },
				"Adjacent units gain Thorns 5"
			)
		) with
		{
			Rarity = KinRarity.Uncommon,
		};

	/// <summary>
	/// **The Bulwark finisher.** It reads the board at end of turn — after the lanes have
	/// resolved and the dead are cleared, before anything withdraws — so it pays for what
	/// SURVIVED, which is the whole archetype in one number.
	/// </summary>
	public static RunCard ShieldwallCaptain =>
		Unit(
			"Shieldwall Captain",
			2,
			8,
			14,
			"Counts the shields still up, and sends the bill.",
			EachTurn(
				KinTarget.Opponent,
				new DealDamageAction { Amount = 3, PerEach = CountOf.YourUnits },
				"end of turn: 3 to the Opponent per unit you hold"
			)
		) with
		{
			Rarity = KinRarity.Rare,
		};

	public static ImmutableArray<RunCard> BulwarkCards =>
		[BriarSentinel, IronbarkTreant, BarbedBanner, HedgeWitch, ShieldwallCaptain];

	// ===== FACE — Pike. Race the Opponent, and hit hard enough to go through =====
	//
	// Strikes is a hook, and Whetstone is the card that proves it: +3 power counts on EVERY strike,
	// so on a Blademaster it is +9. Its natural victim is the Harpy (killable, and a Face deck
	// kills); its problem is the Razorback, whose Thorns answer every one of those strikes.

	public static RunCard TwinbladeDuelist =>
		Unit("Twinblade Duelist", 2, 8, 10, "Two edges, one opinion.") with
		{
			Strikes = 2,
			Rarity = KinRarity.Uncommon,
		};

	/// <summary>
	/// **The glue, at 0.** A blank without a body worth doubling and absurd on the right one —
	/// which is what a build-around enabler is supposed to feel like.
	/// </summary>
	public static RunCard Whetstone =>
		Rite(
			"Whetstone",
			0,
			"Three strokes, and the edge sings.",
			OnPlay(
				KinTarget.UnitInSourceLane,
				new BuffAction { Power = 3, Strikes = 1 },
				"the unit in this lane gets +3/+0 and strikes once more"
			)
		);

	public static RunCard Berserker =>
		Unit("Berserker", 1, 10, 4, "Does not stop at the first one.") with
		{
			Breakthrough = true,
		};

	public static RunCard Blademaster =>
		Unit("Blademaster", 2, 6, 10, "Never the same cut twice. Or three times.") with
		{
			Strikes = 3,
			Rarity = KinRarity.Rare,
		};

	public static ImmutableArray<RunCard> FaceCards =>
		[TwinbladeDuelist, Whetstone, Berserker, Blademaster];
}
