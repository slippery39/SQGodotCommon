using System.Collections.Immutable;
using ImmutableGameObjects;

namespace KinCore.Party;

/// <summary>A monster between battles: who it is and the HP it carries.</summary>
public record RunCompanion(PartyCompanion Companion, int Hp);

/// <summary>What happened between two battles, for the screen to tell.</summary>
public record RunReport(ImmutableList<string> Revived, string? Joined, bool Rested);

/// <summary>
/// **THE RUN, v1** (KinJam.md "THE RUN"): five battles in a row. You start with ONE companion and
/// the others join after battles 1 and 2; HP carries over; a knocked-out companion revives at a
/// quarter of its max; one rest before the last battle heals 30% of max; after each win you take one
/// of three cards into the trainer's deck.
///
/// **Lives OUTSIDE GameState**, like the lane game's `Run`: a battle is built from it, played, and
/// read back into it. Plain records only — the Serialization Rule holds here too.
/// </summary>
public record PartyRun
{
	public const int JoinAfterBattles = 2;
	public const double RestHeal = 0.3;

	public ImmutableList<RunCompanion> Team { get; init; } = [];

	/// <summary>Companions still to join, in order. One joins after each of the first two wins.</summary>
	public ImmutableList<PartyCompanion> Waiting { get; init; } = [];

	public ImmutableList<Encounter> Encounters { get; init; } = [];

	/// <summary>**The trainer's deck** — one for the run, whoever is on the team.</summary>
	public ImmutableList<KinCard> Deck { get; init; } = [];

	public ImmutableList<KinCard> Rewards { get; init; } = [];

	/// <summary>The index of the NEXT battle. Equal to the encounter count once the run is won.</summary>
	public int Battle { get; init; }

	public int Seed { get; init; }
	public bool Lost { get; init; }

	public bool IsWon => !Lost && Battle >= Encounters.Count;
	public bool IsOver => Lost || IsWon;
	public Encounter Next => Encounters[Battle];

	/// <summary>A new run with one starter. The rest of the roster waits, in roster order.</summary>
	public static PartyRun Start(
		PartyCompanion starter,
		int seed,
		ImmutableList<PartyCompanion>? roster = null,
		ImmutableList<Encounter>? encounters = null,
		ImmutableList<KinCard>? deck = null,
		ImmutableList<KinCard>? rewards = null
	) =>
		new()
		{
			Team = [new RunCompanion(starter, starter.Hp)],
			Waiting = [.. (roster ?? PartyContent.Roster).Where(c => c.Name != starter.Name)],
			Encounters = encounters ?? PartyContent.Encounters,
			Deck = deck ?? PartyContent.StarterDeck,
			Rewards = rewards ?? PartyContent.Rewards,
			Seed = seed,
		};

	/// <summary>
	/// **Where a team of N stands**: one in the middle, two either side of it, three spread out — so
	/// every size has a space to step into.
	/// </summary>
	public static ImmutableList<int> Formation(int size) =>
		size switch
		{
			1 => [2],
			2 => [1, 3],
			_ => [0, 2, 4],
		};

	/// <summary>The next battle, with every companion at the HP the run left it.</summary>
	public GameState StartBattle()
	{
		var spaces = Formation(Team.Count);
		var scenario = new PartyScenario(
			Next.Name,
			$"Battle {Battle + 1} of {Encounters.Count}",
			[.. Team.Select((m, i) => new PlacedCompanion(m.Companion, spaces[i], m.Hp))],
			Next.Foes,
			Deck,
			[]
		);
		return PartyBattleFactory.Create(scenario, Seed + Battle * 101);
	}

	/// <summary>
	/// **Reads a finished battle back into the run.** A loss ends it. A win carries HP over — a
	/// knocked-out companion revives at a quarter of its max — then the next companion joins (after
	/// the first two wins), and before the last battle the team rests.
	/// </summary>
	public (PartyRun Run, RunReport Report) AfterBattle(GameState finished)
	{
		var party = finished.GetParty();
		if (!party.IsOver)
			throw new InvalidOperationException("The battle is not over");

		if (!party.Won)
			return (this with { Lost = true }, new RunReport([], null, false));

		var revived = ImmutableList<string>.Empty;
		var team = Team.Select(member =>
			{
				var ally = finished.Allies().Single(a => a.Name == member.Companion.Name);
				if (!ally.IsKnockedOut)
					return member with { Hp = ally.Hp };

				revived = revived.Add(ally.Name);
				return member with { Hp = (int)Math.Ceiling(ally.MaxHp / 4.0) };
			})
			.ToImmutableList();

		var run = this with { Team = team, Battle = Battle + 1 };

		string? joined = null;
		if (run.Battle <= JoinAfterBattles && run.Waiting.Count > 0)
		{
			var newcomer = run.Waiting[0];
			joined = newcomer.Name;
			run = run with
			{
				Team = run.Team.Add(new RunCompanion(newcomer, newcomer.Hp)),
				Waiting = run.Waiting.RemoveAt(0),
			};
		}

		var rested = run.Battle == Encounters.Count - 1;
		if (rested)
			run = run with
			{
				Team =
				[
					.. run.Team.Select(m =>
						m with
						{
							Hp = Math.Min(
								m.Companion.Hp,
								m.Hp + (int)Math.Ceiling(m.Companion.Hp * RestHeal)
							),
						}
					),
				],
			};

		return (run, new RunReport(revived, joined, rested));
	}

	/// <summary>
	/// **Three cards to choose from** — deterministic per run and battle, so a seed replays.
	/// </summary>
	public ImmutableList<KinCard> RewardOffer()
	{
		var rng = new Random(Seed * 31 + Battle);
		return [.. Rewards.OrderBy(_ => rng.Next()).Take(3)];
	}

	/// <summary>The chosen card joins the deck for the rest of the run.</summary>
	public PartyRun Take(KinCard reward) => this with { Deck = Deck.Add(reward) };
}
