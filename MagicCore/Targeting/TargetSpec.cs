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
	Permanent, // narrowed by AnyOfTypes / NotTypes: "noncreature, nonland permanent"
	SpellOrCreature, // a spell on the stack, or a creature on the battlefield
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

	/// <summary>For <see cref="TargetKind.Permanent"/>: "artifact or enchantment". None means any type.</summary>
	public CardType AnyOfTypes { get; init; }

	/// <summary>"Noncreature, nonland": none of these types.</summary>
	public CardType NotTypes { get; init; }

	/// <summary>"With power 2 or less".</summary>
	public int MaxPower { get; init; } = int.MaxValue;

	/// <summary>"With toughness 4 or greater".</summary>
	public int MinToughness { get; init; }

	/// <summary>"Target creature with flying".</summary>
	public string? WithKeyword { get; init; }

	/// <summary>Gallia, Mabel: "target creature that entered this turn".</summary>
	public bool EnteredThisTurn { get; init; }

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
			Kind == TargetKind.SpellOrCreature
			&& s.GetObject(targetId) is Card spell
			&& OnStack(s, targetId)
		)
			return targetId != sourceId && Filters(s, spell); // a spell cannot target itself (CR 115.5)
		if (
			Kind == TargetKind.Player
			|| s.GetObject(targetId) is not Card card
			|| !s.IsOnBattlefield(targetId)
		)
			return false;

		var typeOk = Kind switch
		{
			TargetKind.Creature or TargetKind.SpellOrCreature => card.Is(CardType.Creature),
			TargetKind.Permanent => AnyOfTypes == CardType.None || card.Is(AnyOfTypes),
			_ => card.Is(CardType.Creature | CardType.Planeswalker),
		};
		return typeOk && !(Another && targetId == sourceId) && Filters(s, card);
	}

	bool Filters(GameState s, Card card) =>
		!(Nonlegendary && card.Is(Supertype.Legendary))
		&& (AnyOfColors.IsEmpty || card.Colors.Any(AnyOfColors.Contains))
		&& (NotTypes == CardType.None || !card.Is(NotTypes))
		&& (MinToughness == 0 || s.Toughness(card) >= MinToughness)
		&& (MaxPower == int.MaxValue || s.Power(card) <= MaxPower)
		&& (WithKeyword is null || s.Has(card, WithKeyword))
		&& (!EnteredThisTurn || card.EnteredOnTurn == s.GetGame().TurnNumber);

	static bool OnStack(GameState s, int id) =>
		s.GetParent(id) == s.GetWellKnownId(MagicKeys.Stack);

	public IEnumerable<int> Candidates(GameState s, int controllerId, int sourceId)
	{
		var players = new[] { s.Player1Id(), s.Player2Id() };
		var permanents = players
			.SelectMany(p => s.CardsIn(p, ZoneType.Battlefield))
			.Select(c => c.Id);
		var spells =
			Kind == TargetKind.SpellOrCreature
				? s.GetChildrenIds(s.GetWellKnownId(MagicKeys.Stack))
				: [];
		return players
			.Concat(permanents)
			.Concat(spells)
			.Where(id => IsLegal(s, controllerId, sourceId, id));
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
