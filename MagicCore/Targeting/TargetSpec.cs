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
	CardInYourGraveyard, // narrowed by AnyOfTypes / MaxManaValue: "target creature card in your graveyard"
	CardInAnyGraveyard, // Afterthought Sentry: "target card from a graveyard"
	Spell, // a spell on the stack, narrowed by AnyOfTypes (+ OrSupertype) and the usual filters
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

	/// <summary>Fateshaper Aspirant: "target legendary card". None means any.</summary>
	public Supertype WithSupertype { get; init; }

	/// <summary>Icy Reception: "creature OR legendary spell" — widens AnyOfTypes; never narrows.</summary>
	public Supertype OrSupertype { get; init; }

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

	/// <summary>Hexhaven Dueling Arena: "target creature that attacked this turn".</summary>
	public bool AttackedThisTurn { get; init; }

	/// <summary>"With mana value 3 or less" — a card in a graveyard or a permanent.</summary>
	public int MaxManaValue { get; init; } = int.MaxValue;

	/// <summary>Your Fate Ends Here: "with mana value 3 or greater".</summary>
	public int MinManaValue { get; init; }

	/// <summary>The second of "one or two target creatures": optional, and not the first one again.</summary>
	public bool NotThePrevious { get; init; }

	/// <param name="targeting">False for a restriction that is not targeting — an Aura's "enchant",
	/// an attachment choice: hexproof then doesn't matter (CR 702.11b names targets only).</param>
	public bool IsLegal(
		GameState s,
		int controllerId,
		int sourceId,
		int targetId,
		bool targeting = true
	)
	{
		if (!s.HasObject(targetId))
			return false;
		if (Kind is TargetKind.CardInYourGraveyard or TargetKind.CardInAnyGraveyard)
			return s.GetObject(targetId) is Card inGraveyard
				&& s.IsIn(
					targetId,
					Kind == TargetKind.CardInYourGraveyard ? controllerId : inGraveyard.OwnerId,
					ZoneType.Graveyard
				)
				&& targetId != sourceId
				&& (AnyOfTypes == CardType.None || inGraveyard.Is(AnyOfTypes))
				&& inGraveyard.ManaCost.ManaValue <= MaxManaValue
				&& inGraveyard.ManaCost.ManaValue >= MinManaValue
				&& (WithSupertype == Supertype.None || inGraveyard.Is(WithSupertype));
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
		if (Kind == TargetKind.Spell)
			return s.GetObject(targetId) is Card stackSpell
				&& OnStack(s, targetId)
				&& targetId != sourceId // a spell cannot target itself (CR 115.5)
				&& (
					AnyOfTypes == CardType.None
					|| stackSpell.Is(AnyOfTypes)
					|| (OrSupertype != Supertype.None && stackSpell.Is(OrSupertype))
				)
				&& Filters(s, stackSpell);
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
		// Hexproof (CR 702.11b): not a legal target for an opponent's spell or ability. Read through
		// s.Has, the one effective-keyword answer — MtgCore once asked a second question here and
		// every equipment-granted hexproof protected nothing. Mass effects never target, so they
		// pass. The same test also filters "a creature you control" for sacrifices — always yours,
		// so hexproof never bites there.
		return typeOk
			&& !(Another && targetId == sourceId)
			&& Filters(s, card)
			&& !(targeting && card.ControllerId != controllerId && s.Has(card, "Hexproof"));
	}

	bool Filters(GameState s, Card card) =>
		!(Nonlegendary && card.Is(Supertype.Legendary))
		&& (WithSupertype == Supertype.None || card.Is(WithSupertype))
		&& (AnyOfColors.IsEmpty || card.Colors.Any(AnyOfColors.Contains))
		&& (NotTypes == CardType.None || !card.Is(NotTypes))
		&& (MinToughness == 0 || s.Toughness(card) >= MinToughness)
		&& (MaxPower == int.MaxValue || s.Power(card) <= MaxPower)
		&& (WithKeyword is null || s.Has(card, WithKeyword))
		&& (!EnteredThisTurn || card.EnteredOnTurn == s.GetGame().TurnNumber)
		&& (!AttackedThisTurn || card.AttackedOnTurn == s.GetGame().TurnNumber)
		&& ManaValue(card) is var mv
		&& mv <= MaxManaValue
		&& mv >= MinManaValue;

	/// <summary>On the stack, X counts (CR 202.3e); anywhere else it is 0.</summary>
	static int ManaValue(Card card) =>
		card.ManaCost.ManaValue + (card.GetComponent<Actions.OnStackComponent>()?.X ?? 0);

	static bool OnStack(GameState s, int id) =>
		s.GetParent(id) == s.GetWellKnownId(MagicKeys.Stack);

	public IEnumerable<int> Candidates(GameState s, int controllerId, int sourceId)
	{
		var players = new[] { s.Player1Id(), s.Player2Id() };
		var permanents = players
			.SelectMany(p => s.CardsIn(p, ZoneType.Battlefield))
			.Select(c => c.Id);
		var spells = Kind is TargetKind.SpellOrCreature or TargetKind.Spell
			? s.GetChildrenIds(s.GetWellKnownId(MagicKeys.Stack))
			: [];
		var graveyard = Kind switch
		{
			TargetKind.CardInYourGraveyard => s.CardsIn(controllerId, ZoneType.Graveyard)
				.Select(c => c.Id),
			TargetKind.CardInAnyGraveyard => players
				.SelectMany(p => s.CardsIn(p, ZoneType.Graveyard))
				.Select(c => c.Id),
			_ => [],
		};
		return players
			.Concat(permanents)
			.Concat(spells)
			.Concat(graveyard)
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
			combos = combos
				.SelectMany(c => options.Select(o => c.Add(o)))
				// "One or two targets": a pair is one play in either order, so keep one ordering.
				.Where(c => !spec.NotThePrevious || c.Length < 2 || c[^1] == 0 || c[^1] > c[^2])
				.ToList();
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
							&& !(spec.NotThePrevious && i > 0 && targets[i] == targets[i - 1])
			)
			.All(ok => ok);
}
