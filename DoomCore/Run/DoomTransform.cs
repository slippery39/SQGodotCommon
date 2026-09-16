using System.Collections.Immutable;

namespace DoomCore;

/// <summary>
/// WHICH deck entries a permanent doom acts on, read off the firing that just landed.
/// </summary>
public enum FiringRead
{
	/// <summary>Units on the Field when it fired. What Nuclear reads.</summary>
	Standing,

	/// <summary>
	/// Units that died since the previous firing. What Zombie reads. A LIST, not a set — a card
	/// that cycles back out of Discard and dies twice pays twice.
	/// </summary>
	Died,

	/// <summary>
	/// Units that died on the turn the doom landed. The narrow window, and the one a doom that
	/// MINTS cards wants: `Died` grows with the countdown and the board width, so on a five-lane
	/// board it hands out several times as much.
	/// </summary>
	DiedThisTurn,

	/// <summary>Units committed to the Field at any point this battle.</summary>
	Summoned,

	/// <summary>
	/// Deck entries never committed this battle. The complement of <see cref="Summoned"/>, and the
	/// one read that is computed from the deck rather than recorded on the firing.
	/// </summary>
	NeverSummoned,
}

/// <summary>WHAT it does to them.</summary>
public enum TransformVerb
{
	/// <summary>Adds one copy of <see cref="DoomTransform.Template"/> per entry read.</summary>
	AddCopies,

	/// <summary>Rewrites the entries read — deltas, absolute sets, and a tag.</summary>
	Modify,

	/// <summary>Adds a fresh copy of each entry read, with a new RunCardId.</summary>
	Duplicate,

	/// <summary>Removes the entries read from the deck entirely.</summary>
	Delete,
}

/// <summary>
/// One permanent doom's fallout, AS DATA.
///
/// **This is what stops a new permanent apocalypse being a new function.** Zombie and Nuclear were
/// each a hand-written `(run, firing) -> run` method, and a themed act wants a dozen more of them.
/// They are the same shape underneath: take a set of deck entries off the firing, do one thing to
/// them. A read and a verb covers both, and covers Famine, Grey Goo and the rest without code.
///
/// It stays OUTSIDE `GameState` and is not a `GameAction`, because the run deck it rewrites lives
/// outside GameState too — that boundary is the whole reason this file is separate from
/// `DoomEffect`. See DoomJam.md's engine findings.
///
/// **Order matters within a scenario's list.** Delete-then-Duplicate is not the same as
/// Duplicate-then-Delete: duplicates are minted with new ids, so a Delete that runs afterwards
/// would find them outside the read it was given and take them straight back out.
/// </summary>
public record DoomTransform
{
	public FiringRead Reads { get; init; } = FiringRead.Standing;
	public TransformVerb Does { get; init; } = TransformVerb.Modify;

	/// <summary>
	/// Units only, which is what every apocalypse so far has meant. Flood's permanent version took
	/// the living and left the rites alone; a Famine that ate your rites would need this false.
	/// </summary>
	public bool UnitsOnly { get; init; } = true;

	/// <summary>What <see cref="TransformVerb.AddCopies"/> mints. Ignored by every other verb.</summary>
	public RunCard Template { get; init; } = new();

	public int PowerDelta { get; init; }
	public int ToughnessDelta { get; init; }
	public int CostDelta { get; init; }

	/// <summary>Absolute overrides, applied after the deltas. This is how assimilation is written.</summary>
	public int? SetPower { get; init; }
	public int? SetToughness { get; init; }
	public int? SetCost { get; init; }

	/// <summary>
	/// One result per this many entries read, for <see cref="TransformVerb.AddCopies"/> and
	/// <see cref="TransformVerb.Delete"/>. Two means every second death pays.
	///
	/// **The rate limit on any doom that adds or removes cards.** A stat buff has a natural ceiling
	/// — a unit can only get so big before the board stops caring — but minting and deleting do
	/// not, and a band fires its doom about ten times.
	/// </summary>
	public int PerN { get; init; } = 1;

	/// <summary>Mark left on every entry modified. Empty leaves the tags alone.</summary>
	public string Tag { get; init; } = "";

	/// <summary>Shown on the doom preview and in the content dump. Keep it true to the data.</summary>
	public string Text { get; init; } = "";

	/// <summary>The run card ids this points at, for one firing against one deck.</summary>
	public IEnumerable<int> Select(Run run, DoomFiring firing)
	{
		var ids = Reads switch
		{
			FiringRead.Standing => firing.OnFieldRunCardIds.AsEnumerable(),
			FiringRead.Died => firing.DiedRunCardIds,
			FiringRead.DiedThisTurn => firing.DiedThisTurnRunCardIds,
			FiringRead.Summoned => firing.SummonedRunCardIds,
			FiringRead.NeverSummoned => run
				.Deck.Where(c => !firing.SummonedRunCardIds.Contains(c.RunCardId))
				.Select(c => c.RunCardId),
			_ => throw new ArgumentOutOfRangeException(
				nameof(Reads),
				$"No rule for reading {Reads}. A read with no case would select nothing, and an "
					+ "apocalypse that touched nobody looks exactly like one that worked."
			),
		};

		if (!UnitsOnly)
			return ids;

		var units = run.Deck.Where(c => c.IsUnit).Select(c => c.RunCardId).ToImmutableHashSet();
		return ids.Where(units.Contains);
	}

	/// <summary>Applies this one transform to the run. Pure; the caller chains them in order.</summary>
	public Run Apply(Run run, DoomFiring firing)
	{
		var selected = Select(run, firing).ToList();
		if (selected.Count == 0)
			return run;

		switch (Does)
		{
			case TransformVerb.AddCopies:
			{
				var count = selected.Count / Math.Max(1, PerN);
				return count == 0 ? run : run.WithCards(Enumerable.Repeat(Template, count));
			}

			case TransformVerb.Duplicate:
			{
				var set = selected.ToImmutableHashSet();
				return run.WithCards(run.Deck.Where(c => set.Contains(c.RunCardId)).ToList());
			}

			case TransformVerb.Delete:
			{
				// Deck order, so which cards go is deterministic and a run replays exactly.
				var set = selected.Take(selected.Count / Math.Max(1, PerN)).ToImmutableHashSet();
				if (set.IsEmpty)
					return run;

				return run with
				{
					Deck = run.Deck.RemoveAll(c => set.Contains(c.RunCardId)),
				};
			}

			case TransformVerb.Modify:
			{
				var set = selected.ToImmutableHashSet();
				return run with
				{
					Deck =
					[
						.. run.Deck.Select(card =>
							!set.Contains(card.RunCardId) ? card : Rewrite(card)
						),
					],
				};
			}

			default:
				throw new ArgumentOutOfRangeException(
					nameof(Does),
					$"No rule for {Does}. A verb with no case would do nothing at all."
				);
		}
	}

	private RunCard Rewrite(RunCard card) =>
		card with
		{
			Power = Math.Max(0, SetPower ?? card.Power + PowerDelta),
			Toughness = Math.Max(1, SetToughness ?? card.Toughness + ToughnessDelta),
			Cost = Math.Max(0, SetCost ?? card.Cost + CostDelta),
			Tags = Tag.Length == 0 ? card.Tags : card.Tags.Add(Tag),
		};
}
