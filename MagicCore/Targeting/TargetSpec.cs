using System.Collections.Immutable;
using ImmutableGameObjects;
using MagicCore.Cards;

namespace MagicCore.Targeting;

public enum TargetKind
{
	Creature,
	CreatureOrPlaneswalker,
	AnyTarget, // creature, planeswalker or player (CR 115.4)
	Player,
}

public enum Whose
{
	Any,
	You,
	Opponent,
}

/// <summary>
/// What one "target" word allows. Checked when chosen AND again on resolution (CR 608.2b) —
/// the single legality test for both, so the two can never disagree.
/// </summary>
public record TargetSpec(TargetKind Kind, Whose Whose = Whose.Any)
{
	/// <summary>"Up to one target" — choosing nothing (id 0) is legal.</summary>
	public bool Optional { get; init; }

	/// <summary>"Another target" — not the source itself.</summary>
	public bool Another { get; init; }

	public bool Nonlegendary { get; init; }

	/// <summary>"That's blue or red" — empty means any colour.</summary>
	public ImmutableArray<ManaColor> AnyOfColors { get; init; } = [];

	public bool IsLegal(GameState s, int controllerId, int sourceId, int targetId)
	{
		if (!s.HasObject(targetId))
			return false;
		var whoseOk = (Whose, s.GetObject(targetId)) switch
		{
			(Whose.Any, _) => true,
			(Whose.You, MagicPlayer p) => p.Id == controllerId,
			(Whose.Opponent, MagicPlayer p) => p.Id != controllerId,
			(Whose.You, Card c) => c.ControllerId == controllerId,
			(Whose.Opponent, Card c) => c.ControllerId != controllerId,
			_ => false,
		};
		if (!whoseOk)
			return false;
		if (s.GetObject(targetId) is MagicPlayer)
			return Kind is TargetKind.AnyTarget or TargetKind.Player;
		if (
			Kind == TargetKind.Player
			|| s.GetObject(targetId) is not Card card
			|| !s.IsOnBattlefield(targetId)
		)
			return false;

		var typeOk = Kind switch
		{
			TargetKind.Creature => card.Is(CardType.Creature),
			_ => card.Is(CardType.Creature | CardType.Planeswalker),
		};
		return typeOk
			&& !(Another && targetId == sourceId)
			&& !(Nonlegendary && card.Is(Supertype.Legendary))
			&& (AnyOfColors.IsEmpty || card.Colors.Any(AnyOfColors.Contains));
	}

	public IEnumerable<int> Candidates(GameState s, int controllerId, int sourceId)
	{
		var players = new[] { s.Player1Id(), s.Player2Id() };
		var permanents = players
			.SelectMany(p => s.CardsIn(p, ZoneType.Battlefield))
			.Select(c => c.Id);
		return players.Concat(permanents).Where(id => IsLegal(s, controllerId, sourceId, id));
	}

	/// <summary>Every legal assignment for a list of target words — one id per word, 0 for an unused "up to one".</summary>
	public static IEnumerable<ImmutableArray<int>> Combinations(
		GameState s,
		IReadOnlyList<TargetSpec> specs,
		int controllerId,
		int sourceId
	)
	{
		IEnumerable<ImmutableArray<int>> combos = [ImmutableArray<int>.Empty];
		foreach (var spec in specs)
		{
			var options = spec.Candidates(s, controllerId, sourceId).ToList();
			if (spec.Optional)
				options.Add(0);
			combos = combos.SelectMany(c => options.Select(o => c.Add(o))).ToList();
		}
		return combos;
	}

	/// <summary>Chosen targets fit the specs: one per word, each legal, 0 only where optional.</summary>
	public static bool AreLegal(
		GameState s,
		IReadOnlyList<TargetSpec> specs,
		ImmutableArray<int> targets,
		int controllerId,
		int sourceId
	) =>
		targets.Length == specs.Count
		&& specs
			.Select(
				(spec, i) =>
					targets[i] == 0
						? spec.Optional
						: spec.IsLegal(s, controllerId, sourceId, targets[i])
			)
			.All(ok => ok);
}
