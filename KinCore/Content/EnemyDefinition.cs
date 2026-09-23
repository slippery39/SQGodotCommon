using System.Collections.Immutable;

namespace KinCore;

/// <summary>
/// An enemy as CONTENT — the definition, not an instance in a battle. The same relationship
/// <see cref="RunCard"/> has to a <see cref="KinCard"/>.
///
/// **Behaviour is data.** An enemy that hurts you when it dies, or bleeds you every turn, is a
/// <see cref="KinEffect"/> in this record — not a branch in `EndTurnAction`. Adding one needs no
/// engine change, which is the whole point: enemies become something to iterate on rather than
/// something to code.
/// </summary>
public record EnemyDefinition
{
	public string Name { get; init; } = "";
	public string Description { get; init; } = "";
	public int Health { get; init; }

	/// <summary>Damage it telegraphs into its lane. Zero means it waits.</summary>
	public int Attack { get; init; }

	/// <summary>
	/// The earliest floor this may appear on. Same idea as `PlayableOn` for scenarios: the roster a
	/// floor draws from IS the difficulty curve, expressed as content rather than as a formula.
	/// </summary>
	public int MinFloor { get; init; } = 1;

	/// <summary>See <see cref="Enemy.Thorns"/>.</summary>
	public int Thorns { get; init; }

	/// <summary>See <see cref="Enemy.Strikes"/>. 1, or a definition that says nothing strikes zero times.</summary>
	public int Strikes { get; init; } = 1;

	/// <summary>See <see cref="Enemy.Flies"/>.</summary>
	public bool Flies { get; init; }

	public ImmutableList<KinEffect> Effects { get; init; } = ImmutableList<KinEffect>.Empty;

	public Enemy ToEnemy(int lane) =>
		new()
		{
			Name = Name,
			Description = Description,
			Health = Health,
			MaxHealth = Health,
			Intent = Attack > 0 ? IntentKind.Attack : IntentKind.Wait,
			IntentAmount = Attack,
			Lane = lane,
			Thorns = Thorns,
			Strikes = Strikes,
			Flies = Flies,
			Effects = Effects,
		};

	public PendingSummon ToSummon(int lane) =>
		new()
		{
			Name = Name,
			Health = Health,
			Attack = Attack,
			Lane = lane,
			Thorns = Thorns,
			Strikes = Strikes,
			Flies = Flies,
			Effects = Effects,
		};
}

/// <summary>
/// The thing you are trying to kill, as content.
///
/// Its own effects are the **per-battle inevitability lever** KinJam.md has wanted since dodging
/// stopped being a global rule: an Opponent that heals, or that cannot be finished while the
/// countdown runs, makes ONE fight a guaranteed apocalypse without retuning every other fight.
/// </summary>
public record OpponentDefinition
{
	public string Name { get; init; } = "The Opponent";
	public string Description { get; init; } = "";
	public int Health { get; init; }

	/// <summary>Turns between reinforcements. See <see cref="Opponent.SummonInterval"/>.</summary>
	public int SummonInterval { get; init; } = 3;

	/// <summary>What it puts back into an open lane. Its effects come with it.</summary>
	public EnemyDefinition Reinforcement { get; init; } = new();

	public int MinFloor { get; init; } = 1;

	public ImmutableList<KinEffect> Effects { get; init; } = ImmutableList<KinEffect>.Empty;
}
