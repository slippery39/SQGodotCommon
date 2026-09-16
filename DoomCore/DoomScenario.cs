namespace DoomCore;

/// <summary>
/// Which apocalypse fires, over and over, for as long as a battle lasts. **It does not end one** —
/// only a death does. Every scenario is the same mechanic — read the board when it fires, then
/// change something — so adding one is data, not code.
///
/// Each has a SCOPE (see <see cref="DoomScope"/>), fixed at design time, which decides both what it
/// may touch and which hook implements it: `DoomBattleEffects` or `DoomTransforms`.
///
/// **PERMANENT scenarios must be a BARGAIN, never a pure tax**: they convert one resource into
/// another, because a run deck that only ever gets worse is a misery engine and makes escalating
/// enemies unbalanceable. Battle-scope scenarios are exempt — nothing carries forward, so they can
/// be pure obstacles. See DoomJam.md.
/// </summary>
public enum DoomScenario
{
	/// <summary>No doom. Test scaffolding only — a real battle always has one.</summary>
	None = 0,

	/// <summary>Reads what DIED. Deaths return as 2/2 Zombies: quantity bought with deck space.</summary>
	Zombie,

	/// <summary>Reads what was LEFT ON THE FIELD. Those become Irradiated: +4/+4, lose 2 life when drawn.</summary>
	Nuclear,

	/// <summary>
	/// BATTLE scope. Reads what is STANDING and washes it all to Discard — you keep the cards and
	/// lose the board. It used to delete never-summoned units from the run deck; permanent removal
	/// caused more trouble than it was worth. See DoomJam.md.
	/// </summary>
	Flood,

	/// <summary>Reads what you SACRIFICED. Sacrificed units return as life — the doom you want at 6 HP.</summary>
	Rapture,

	/// <summary>
	/// BATTLE scope. Burns everything still standing and everyone behind it.
	///
	/// **Added as data alone** — an entry here and an entry in `ScenarioLibrary`, with no hook case,
	/// no scope row and no countdown row. That is the whole point of scenarios being content.
	/// </summary>
	Ashfall,
}
