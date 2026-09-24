using System.Collections.Immutable;
using ImmutableGameObjects;

namespace KinCore.Party;

/// <summary>A monster between battles: who it is and the HP it carries.</summary>
public record RunCompanion(PartyCompanion Companion, int Hp);

/// <summary>What happened between two battles, for the screen to tell.</summary>
public record RunReport(
	ImmutableList<string> Revived,
	ImmutableList<string> Caught,
	ImmutableList<string> ToBench,
	bool Rested
);

/// <summary>
/// **THE RUN** (KinJam.md "THE RUN" and "CATCHING"): five battles in a row. You start with ONE
/// monster and **catch the rest** — a foe Snared in a battle you win joins with the HP it was caught
/// at and its own cycle. Up to three fight; the rest wait on the BENCH, and you choose who fights
/// between battles. HP carries over; a knocked-out monster revives at a quarter of its max; one rest
/// before the last battle heals 30% of max; after each win you take one of three cards.
///
/// **Lives OUTSIDE GameState**, like the lane game's `Run`: a battle is built from it, played, and
/// read back into it. Plain records only — the Serialization Rule holds here too.
/// </summary>
public record PartyRun
{
	public const double RestHeal = 0.3;

	/// <summary>How many fight. The rest wait on the bench.</summary>
	public const int TeamSize = 3;

	/// <summary>Snares a run starts with. Towns will sell more; until then, these are all you get.</summary>
	public const int StartingSnares = 3;

	/// <summary>Who fights, in board order.</summary>
	public ImmutableList<RunCompanion> Team { get; init; } = [];

	/// <summary>**The bench**: caught monsters beyond the three that fight. Swapped in between battles.</summary>
	public ImmutableList<RunCompanion> Bench { get; init; } = [];

	public ImmutableList<Encounter> Encounters { get; init; } = [];

	/// <summary>**The trainer's deck** — one for the run, whoever is on the team.</summary>
	public ImmutableList<KinCard> Deck { get; init; } = [];

	public ImmutableList<KinCard> Rewards { get; init; } = [];

	public int Snares { get; init; }

	/// <summary>The index of the NEXT battle. Equal to the encounter count once the run is won.</summary>
	public int Battle { get; init; }

	public int Seed { get; init; }
	public bool Lost { get; init; }

	public bool IsWon => !Lost && Battle >= Encounters.Count;
	public bool IsOver => Lost || IsWon;
	public Encounter Next => Encounters[Battle];

	/// <summary>A new run with one starter. Everyone else is caught.</summary>
	public static PartyRun Start(
		PartyCompanion starter,
		int seed,
		ImmutableList<Encounter>? encounters = null,
		ImmutableList<KinCard>? deck = null,
		ImmutableList<KinCard>? rewards = null
	) =>
		new()
		{
			Team = [new RunCompanion(starter, starter.Hp)],
			Encounters = encounters ?? PartyContent.Encounters,
			Deck = deck ?? PartyContent.StarterDeck,
			Rewards = rewards ?? PartyContent.Rewards,
			Snares = StartingSnares,
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

	/// <summary>
	/// **A caught foe as a monster of yours: exactly what it had.** Its cycle, its Speed, its max HP.
	/// Power 0, because a foe's move amounts are already its whole damage.
	/// </summary>
	public static PartyCompanion FromFoe(Foe foe) =>
		new(foe.Name, foe.MaxHp, Power: 0, foe.Speed, foe.Pattern);

	/// <summary>The next battle, with every monster at the HP the run left it, and the run's Snares.</summary>
	public GameState StartBattle()
	{
		var spaces = Formation(Team.Count);
		var scenario = new PartyScenario(
			Next.Name,
			$"Battle {Battle + 1} of {Encounters.Count}",
			[.. Team.Select((m, i) => new PlacedCompanion(m.Companion, spaces[i], m.Hp))],
			Next.Foes,
			Deck,
			[],
			Snares
		);
		return PartyBattleFactory.Create(scenario, Seed + Battle * 101);
	}

	/// <summary>
	/// **Reads a finished battle back into the run.** A loss ends it. A win carries HP over — a
	/// knocked-out monster revives at a quarter of its max — keeps the Snares left, and every caught
	/// foe joins: the team if there is room, else the bench. Before the last battle the team rests.
	/// </summary>
	public (PartyRun Run, RunReport Report) AfterBattle(GameState finished)
	{
		var party = finished.GetParty();
		if (!party.IsOver)
			throw new InvalidOperationException("The battle is not over");

		if (!party.Won)
			return (this with { Lost = true }, new RunReport([], [], [], false));

		var revived = ImmutableList<string>.Empty;
		var team = Team.Select(
				(member, slot) =>
				{
					var ally = finished.Allies().Single(a => a.Slot == slot);
					if (!ally.IsKnockedOut)
						return member with { Hp = ally.Hp };

					revived = revived.Add(ally.Name);
					return member with { Hp = (int)Math.Ceiling(ally.MaxHp / 4.0) };
				}
			)
			.ToImmutableList();

		var run = this with { Team = team, Battle = Battle + 1, Snares = party.Snares };

		var caught = ImmutableList<string>.Empty;
		var toBench = ImmutableList<string>.Empty;
		foreach (var foe in finished.CaughtFoes())
		{
			var joining = new RunCompanion(FromFoe(foe), foe.Hp);
			caught = caught.Add(foe.Name);
			if (run.Team.Count < TeamSize)
				run = run with { Team = run.Team.Add(joining) };
			else
			{
				run = run with { Bench = run.Bench.Add(joining) };
				toBench = toBench.Add(foe.Name);
			}
		}

		var rested = run.Battle == Encounters.Count - 1;
		if (rested)
			run = run with { Team = Rest(run.Team), Bench = Rest(run.Bench) };

		return (run, new RunReport(revived, caught, toBench, rested));
	}

	private static ImmutableList<RunCompanion> Rest(ImmutableList<RunCompanion> monsters) =>
		[
			.. monsters.Select(m =>
				m with
				{
					Hp = Math.Min(
						m.Companion.Hp,
						m.Hp + (int)Math.Ceiling(m.Companion.Hp * RestHeal)
					),
				}
			),
		];

	/// <summary>**A benched monster takes a team member's place**, and the team member sits down.</summary>
	public PartyRun Swap(int teamIndex, int benchIndex) =>
		this with
		{
			Team = Team.SetItem(teamIndex, Bench[benchIndex]),
			Bench = Bench.SetItem(benchIndex, Team[teamIndex]),
		};

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
