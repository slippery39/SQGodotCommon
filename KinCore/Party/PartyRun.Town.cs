namespace KinCore.Party;

/// <summary>
/// **The town, as a map of buildings** (`KinMapPlan.md` §3, §7): the hospital, the shop, the pen,
/// the leader's hall (from the second town on) and the gate. **Healing is no longer free on
/// arrival** — the hospital sells it, so it competes with cards for the same gold (the
/// decided "what makes a town a choice"). The shop, pen, hall and gate use the run's existing verbs.
/// </summary>
public partial record PartyRun
{
	/// <summary>A guess (exploring): about one wild win's gold and a bit.</summary>
	public const int HospitalPrice = 25;

	/// <summary>This town's buildings.</summary>
	public TownMap Town => PartyTowns.For(RegionIndex, Region.Name);

	/// <summary>Anyone on the team or the bench below their max.</summary>
	public bool AnyoneHurt => Team.Any(m => m.Hp < m.MaxHp);

	/// <summary>Why the hospital will not heal — or null if it will.</summary>
	public string? CannotHeal =>
		Phase != RunPhase.Town ? "Not in a town"
		: !AnyoneHurt ? "Everyone is already well"
		: Gold < HospitalPrice ? $"Healing costs {HospitalPrice} gold"
		: null;

	/// <summary>**The hospital**: the team and the bench to full, for gold. Refused changes nothing.</summary>
	public PartyRun HealAtHospital() =>
		CannotHeal is not null
			? this
			: this with
			{
				Gold = Gold - HospitalPrice,
				Team = Heal(Team, 1),
			};
}
