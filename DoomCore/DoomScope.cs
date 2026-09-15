namespace DoomCore;

/// <summary>
/// What a doom scenario is allowed to change. **A fixed property of the scenario's design**, never
/// a tier of the same idea — variety comes from having many scenarios, not from re-scoping three.
///
/// Scope is the difficulty curve: early floors draw from <see cref="Battle"/> scenarios, which are
/// inconveniences gone when the fight is, and later floors from <see cref="Permanent"/> ones, which
/// leave marks on the run. `StarterContent.PlayableOn` does that gating.
///
/// It also decides which hook a scenario belongs in, and **a scenario in the wrong hook silently
/// does nothing** — the failure this codebase keeps rediscovering. Both hooks therefore throw when
/// handed the other scope rather than no-opping.
/// </summary>
public enum DoomScope
{
	/// <summary>
	/// Changes the battle only — board, hand, draw pile. Nothing survives the fight.
	///
	/// **Exempt from "bargain, not tax."** That rule exists because a run deck that only degrades is
	/// a misery engine; nothing carries forward here, so a battle doom can be a pure obstacle. That
	/// exemption is what makes early-game content cheap to write.
	/// </summary>
	Battle,

	/// <summary>
	/// Rewrites the run deck, forever. **Must be a bargain** — it converts one resource into
	/// another and a greedy line must exist.
	/// </summary>
	Permanent,
}
