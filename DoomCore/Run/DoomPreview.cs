using System.Collections.Immutable;
using ImmutableGameObjects;

namespace DoomCore;

/// <summary>
/// What the apocalypse would do to your deck if it landed RIGHT NOW.
///
/// **Certainty is permission to show the player everything.** The tension in this game is
/// inevitability, not surprise — the doom cannot be prevented, so hiding it buys nothing and only
/// stops the player from playing around it. This is what the GO SPINNY countdown dial renders, and
/// what the console prints every turn.
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

	public string Flavour { get; init; } = "";

	/// <summary>One line, safe to print anywhere. Counts are computed, never hand-written.</summary>
	public string Summary =>
		!IsAvailable ? $"{Scenario}: no transform implemented"
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
	/// </summary>
	public static DoomPreview Preview(Run run, GameState battle)
	{
		var turnsRemaining = battle.GetBattle().CountdownRemaining;
		var scenario = battle.GetBattle().Scenario;

		Run hypothetical;
		try
		{
			hypothetical = DoomTransforms.Apply(run, battle);
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
	/// Flavour only — never mechanics. Anything a player needs in order to decide comes from the
	/// diff above, so this text going stale can mislead about tone but never about rules.
	/// </summary>
	private static string Flavour(DoomScenario scenario) =>
		scenario switch
		{
			DoomScenario.Zombie => "The dead do not stay where you leave them.",
			DoomScenario.Nuclear => "What stands in the open will be changed by it.",
			DoomScenario.Flood => "Only what you carried aboard goes with you.",
			DoomScenario.Rapture => "What you give up is not lost.",
			_ => "",
		};
}
