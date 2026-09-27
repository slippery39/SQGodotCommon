using System.Collections.Immutable;

namespace KinCore.Party;

/// <summary>What a building in a town is for (`KinMapPlan.md` §3).</summary>
public enum BuildingKind
{
	/// <summary>Heals the team and the bench — for gold.</summary>
	Hospital,

	/// <summary>Snares, cards, and taking a card out of the deck.</summary>
	Shop,

	/// <summary>The team and the bench: who fights next.</summary>
	Pen,

	/// <summary>The leader's hall: the town's leader fight (from the second town on).</summary>
	Hall,

	/// <summary>The road out, onto the next route — shut until the leader is beaten.</summary>
	Gate,
}

/// <summary>
/// **A building on a town's map**, placed by DATA: `X` and `Y` are 0–1 across and down the map, so
/// the screen places the sprite and its hotspot from here, never from pixels in a painting.
/// </summary>
public record Building(BuildingKind Kind, string Name, double X, double Y);

public record TownMap(ImmutableList<Building> Buildings);

/// <summary>
/// **The town of a region.** One layout for every town for now — the plan's "any town assembled from
/// data" starts with the same streets; a town differs by its name and whether it has a leader.
/// </summary>
public static class PartyTowns
{
	public static TownMap For(int regionIndex, string regionName) =>
		new(
			[
				new(BuildingKind.Hospital, "Hospital", 0.22, 0.34),
				new(BuildingKind.Shop, "Shop", 0.5, 0.26),
				new(BuildingKind.Pen, "The Pen", 0.25, 0.72),
				.. regionIndex >= 1
					? [new Building(BuildingKind.Hall, $"{regionName} Hall", 0.77, 0.3)]
					: ImmutableList<Building>.Empty,
				new(BuildingKind.Gate, "Town Gate", 0.8, 0.74),
			]
		);
}
