namespace DoomCore;

/// <summary>
/// Which apocalypse ends a battle. Every scenario is the same mechanic — read the board at
/// countdown 0, apply a permanent transform to the RUN deck — so adding one is data, not code.
///
/// Every scenario must be a BARGAIN, never a pure tax: it converts one resource into another. A
/// deck that only ever gets worse is a misery engine, and it makes escalating enemies
/// unbalanceable. See DoomJam.md.
/// </summary>
public enum DoomScenario
{
	/// <summary>No doom. Test scaffolding only — a real battle always has one.</summary>
	None = 0,

	/// <summary>Reads what DIED. Deaths return as 1/1 Zombies: quantity bought with deck space.</summary>
	Zombie,

	/// <summary>Reads what was LEFT ON THE FIELD. Those become Irradiated: +2/+2, lose 1 life when drawn.</summary>
	Nuclear,

	/// <summary>Reads what you COMMITTED. Units summoned duplicate; units never summoned are removed.</summary>
	Flood,

	/// <summary>Reads what you SACRIFICED. Sacrificed units return as life — the doom you want at 6 HP.</summary>
	Rapture,
}
