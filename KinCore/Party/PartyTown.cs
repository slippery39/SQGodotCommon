using System.Collections.Immutable;

namespace KinCore.Party;

/// <summary>What a building in a town is for (`KinMapPlan.md` §3).</summary>
public enum BuildingKind
{
	/// <summary>Heals the team and the bench — for gold.</summary>
	Hospital,

	/// <summary>Cards, and taking a card out of the deck.</summary>
	Shop,

	/// <summary>The road out, onto the route — and its boss at the end.</summary>
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
/// data" starts with the same streets; a town differs by its name.
/// </summary>
public static class PartyTowns
{
	public static TownMap For(int regionIndex, string regionName) =>
		new(
			[
				new(BuildingKind.Hospital, "Hospital", 0.22, 0.34),
				new(BuildingKind.Shop, "Shop", 0.5, 0.26),
				new(BuildingKind.Gate, "Town Gate", 0.8, 0.74),
			]
		);
}
