using System.Collections.Immutable;
using ImmutableGameObjects;

namespace DoomCore;

/// <summary>
/// What the apocalypse would do to your deck if it landed RIGHT NOW.
///
/// **Certainty is permission to show the player everything.** The tension here is a race, not a
/// surprise: the doom is coming on a known clock and you are deciding whether to take it or outrun
/// it. Hiding what it would do buys nothing and only stops the player making that choice. This is
/// what the GO SPINNY dial renders, and what the console prints every turn.
/// </summary>
public record DoomPreview
{
	public DoomScenario Scenario { get; init; }
	public int TurnsRemaining { get; init; }

	public ImmutableList<RunCard> Added { get; init; } = ImmutableList<RunCard>.Empty;
	public ImmutableList<RunCard> Removed { get; init; } = ImmutableList<RunCard>.Empty;
	public ImmutableList<DoomPreviewChange> Changed { get; init; } =
		ImmutableList<DoomPreviewChange>.Empty;

	/// <summary>False when the scenario has no transform yet — say so rather than showing nothing.</summary>
	public bool IsAvailable { get; init; } = true;

	/// <summary>
	/// False for a <see cref="DoomScope.Battle"/> scenario, which changes this fight and nothing
	/// else. The dial must SAY so: a battle doom leaves the deck untouched, and a preview that just
	/// reported "nothing would change" would be indistinguishable from a broken one.
	/// </summary>
	public bool IsPermanent { get; init; } = true;

	/// <summary>
	/// Units the apocalypse would take off the board. Battle-scope scenarios only.
	///
	/// Computed by running the real effect and diffing the FIELD, so it needs no knowledge of
	/// whether a scenario sweeps, takes or kills — which is what has kept it honest through two
	/// rewrites of what Flood does.
	/// </summary>
	public int UnitsSwept { get; init; }

	public string Flavour { get; init; } = "";

	/// <summary>One line, safe to print anywhere. Counts are computed, never hand-written.</summary>
	public string Summary =>
		!IsAvailable ? $"{Scenario}: no transform implemented"
		: !IsPermanent ? $"{Scenario}: {UnitsSwept} taken off the board — nothing permanent"
		: Added.IsEmpty && Removed.IsEmpty && Changed.IsEmpty ? $"{Scenario}: nothing would change"
		: $"{Scenario}: "
			+ string.Join(
				", ",
				new[]
				{
					Added.IsEmpty ? null : $"+{Added.Count} gained",
					Removed.IsEmpty ? null : $"-{Removed.Count} LOST",
					Changed.IsEmpty ? null : $"{Changed.Count} changed",
				}.Where(part => part is not null)
			);
}

public record DoomPreviewChange
{
	public RunCard Before { get; init; } = new();
	public RunCard After { get; init; } = new();
}

public static class DoomPreviewer
{
	/// <summary>
	/// Runs the REAL transform speculatively and diffs the decks.
	///
	/// It deliberately does not describe each scenario in its own words — a second, hand-written
	/// account of what Flood does would drift from Flood the moment either changed, and the player
	/// would be playing around a lie. Every future scenario is previewable for free because of this.
	///
	/// It applies a HYPOTHETICAL firing captured from the board right now, not the firings already
	/// recorded on the battle. Those have happened; the dial answers "what would the NEXT one do".
	/// Both sides use <see cref="DoomFiring.Capture"/>, so the preview cannot disagree with the
	/// apocalypse it is predicting.
	/// </summary>
	public static DoomPreview Preview(Run run, GameState battle)
	{
		var turnsRemaining = battle.GetBattle().CountdownRemaining;
		var scenario = battle.GetBattle().Scenario;

		// A battle-scope apocalypse touches no deck, so a deck diff would report "nothing would
		// change" — true, and useless. Diff the BOARD instead, by running the real effect. Same
		// rule as below: never write a second account of what a scenario does.
		if (StarterContent.ScopeOf(scenario) == DoomScope.Battle)
		{
			var standing = battle.Units().Count();
			var left = DoomBattleEffects.Apply(battle, scenario).State.Units().Count();

			return new DoomPreview
			{
				Scenario = scenario,
				TurnsRemaining = turnsRemaining,
				Flavour = Flavour(scenario),
				IsPermanent = false,
				UnitsSwept = standing - left,
			};
		}

		Run hypothetical;
		try
		{
			hypothetical = DoomTransforms.ApplyFiring(
				run,
				DoomFiring.Capture(battle, battle.GetBattle().TurnNumber)
			);
		}
		catch (NotSupportedException)
		{
			return new DoomPreview
			{
				Scenario = scenario,
				TurnsRemaining = turnsRemaining,
				IsAvailable = false,
			};
		}

		var before = run.Deck.ToImmutableDictionary(c => c.RunCardId);
		var after = hypothetical.Deck.ToImmutableDictionary(c => c.RunCardId);

		return new DoomPreview
		{
			Scenario = scenario,
			TurnsRemaining = turnsRemaining,
			Flavour = Flavour(scenario),
			Removed = before.Values.Where(c => !after.ContainsKey(c.RunCardId)).ToImmutableList(),
			Added = after.Values.Where(c => !before.ContainsKey(c.RunCardId)).ToImmutableList(),
			Changed = before
				.Values.Where(c => after.TryGetValue(c.RunCardId, out var a) && a != c)
				.Select(c => new DoomPreviewChange { Before = c, After = after[c.RunCardId] })
				.ToImmutableList(),
		};
	}

	/// <summary>
	/// The scenario's one-line description. Lives in `StarterContent` beside the rest of the
	/// per-scenario content, because the battle banner shows it too and two copies of a string the
	/// player reads is one copy too many.
	/// </summary>
	private static string Flavour(DoomScenario scenario) => StarterContent.DescriptionFor(scenario);
}
