using System.Collections.Immutable;
using ImmutableGameObjects;
using MagicCore.Cards;
using MagicCore.Filters;

namespace MagicCore.Targeting;

/// <summary>Where a target word's candidates come from — the zone a filter never names.</summary>
public enum From
{
	Players,
	Battlefield,
	Stack, // spells: an ability on the stack is not a card
	YourGraveyard,
	AnyGraveyard, // Afterthought Sentry: "target card from a graveyard"
	Ability, // Sublime Epiphany: "target activated or triggered ability" — on the stack, not a card
}

public enum Whose
{
	Any,
	You,
	Opponent,
}

public static class WhoseRules
{
	/// <summary>THE "you / an opponent" test — a filter's controller and a trigger's "who did it" both ask here.</summary>
	public static bool Includes(this Whose whose, int playerId, int controllerId) =>
		whose switch
		{
			Whose.You => playerId == controllerId,
			Whose.Opponent => playerId != 0 && playerId != controllerId,
			_ => true,
		};
}

/// <summary>
/// What one "target" word allows: a zone, a filter (which objects there), and the targeting rules.
/// Checked when chosen AND again on resolution (CR 608.2b) — the single legality test for both, so
/// the two can never disagree.
/// </summary>
public record TargetSpec(From From, Filter? Filter = null)
{
	/// <summary>"Up to one target" — choosing nothing (id 0) is legal.</summary>
	public bool Optional { get; init; }

	/// <summary>The second of "one or two target creatures": optional, and not the first one again.</summary>
	public bool NotThePrevious { get; init; }

	/// <summary>
	/// Dominion Infiltration: "Choose X target creatures your opponents control" — this word X times, X as
	/// cast, each a different object: one cast per SET, never per order (StackResolution.TextOf).
	/// </summary>
	public bool PerX { get; init; }

	/// <summary>
	/// Fatehold Charm: "target spell or creature" — also legal where this one says, in its own zone.
	/// Only the outer spec's Optional and NotThePrevious are read; set them there.
	/// </summary>
	public TargetSpec? Or { get; init; }

	/// <summary>
	/// Highly Illogical: "Counter target spell with mana value 2 or less. If this spell was kicked,
	/// instead counter target spell." — the target word as it reads when kicked (StackResolution.TextOf).
	/// </summary>
	public TargetSpec? IfKicked { get; init; }

	/// <summary>"Target creature" — on the battlefield.</summary>
	public static readonly TargetSpec Creature = Permanent(Is.Creature);
	public static readonly TargetSpec CreatureOrPlaneswalker = Permanent(
		Is.Creature | Is.Planeswalker
	);

	/// <summary>"Target [filter]" on the battlefield; no filter is "target permanent".</summary>
	public static TargetSpec Permanent(Filter? filter = null) => new(From.Battlefield, filter);

	public static TargetSpec Player(Filter? filter = null) => new(From.Players, filter);

	/// <summary>"Target spell" — never the spell itself (CR 115.5).</summary>
	public static TargetSpec Spell(Filter? filter = null) => new(From.Stack, filter);

	/// <summary>"Target activated or triggered ability" — Is.TriggeredAbility narrows it.</summary>
	public static TargetSpec Ability(Filter? filter = null) => new(From.Ability, filter);

	public static TargetSpec InYourGraveyard(Filter? filter = null) =>
		new(From.YourGraveyard, filter);

	public static TargetSpec InAnyGraveyard(Filter? filter = null) =>
		new(From.AnyGraveyard, filter);

	/// <summary>"Any target" (CR 115.4): a player, creature or planeswalker — the filter narrows each.</summary>
	public static TargetSpec AnyTarget(Filter? filter = null) =>
		Player(filter) with
		{
			Or = Permanent(
				filter is null
					? Is.Creature | Is.Planeswalker
					: (Is.Creature | Is.Planeswalker) & filter
			),
		};

	/// <param name="targeting">False for a restriction that is not targeting — an Aura's "enchant",
	/// an attachment choice: hexproof then doesn't matter (CR 702.11b names targets only).</param>
	public bool IsLegal(
		GameState s,
		int controllerId,
		int sourceId,
		int targetId,
		bool targeting = true
	) =>
		s.HasObject(targetId)
		&& (
			Allows(s, s.GetObject(targetId), controllerId, sourceId, targeting)
			|| (Or?.IsLegal(s, controllerId, sourceId, targetId, targeting) ?? false)
		);

	bool Allows(GameState s, GameObject o, int controllerId, int sourceId, bool targeting)
	{
		var inZone = From switch
		{
			From.Players => o is MagicPlayer,
			From.Battlefield => o is Card && s.IsOnBattlefield(o.Id),
			// Off the battlefield a spell or ability never targets its own card (CR 115.5).
			From.Stack => o is Card && OnStack(s, o.Id) && o.Id != sourceId,
			From.Ability => o is Actions.StackAbility && OnStack(s, o.Id),
			From.YourGraveyard => o is Card
				&& s.IsIn(o.Id, controllerId, ZoneType.Graveyard)
				&& o.Id != sourceId,
			_ => o is Card c && s.IsIn(o.Id, c.OwnerId, ZoneType.Graveyard) && o.Id != sourceId,
		};
		// Hexproof (CR 702.11b): not a legal target for an opponent's spell or ability. Read through
		// s.Has, the one effective-keyword answer — MtgCore once asked a second question here and
		// every equipment-granted hexproof protected nothing. Mass effects never target, so they pass.
		return inZone
			&& (Filter?.Matches(s, o, new FilterContext(controllerId, sourceId)) ?? true)
			&& !(
				targeting
				&& From == From.Battlefield
				&& o is Card card
				&& card.ControllerId != controllerId
				&& s.Has(card, "Hexproof")
			)
			&& !(
				targeting
				&& From == From.Battlefield
				&& o is Card target
				&& FromARestrictedSource(s, target, sourceId)
			)
			// The Needs of the Many: "You gain hexproof" — a player, not a legal target for an opponent.
			&& !(
				targeting
				&& o is MagicPlayer player
				&& player.Id != controllerId
				&& s.ThisTurnEffects<Effects.YouHaveHexproof>(player.Id).Any()
			);
	}

	/// <summary>Species 8472: an ability (its source a permanent, not a spell on the stack) from a source its filter names.</summary>
	static bool FromARestrictedSource(GameState s, Card target, int sourceId) =>
		target.HasComponent<Effects.CantBeTargetedByAbilitiesFrom>()
		&& s.IsOnBattlefield(sourceId)
		&& s.AbilitiesOf<Effects.CantBeTargetedByAbilitiesFrom>(target)
			.Any(r =>
				r.Sources.Matches(s, s.GetCard(sourceId), new(target.ControllerId, target.Id))
			);

	bool Covers(From zone) => From == zone || (Or?.Covers(zone) ?? false);

	static bool OnStack(GameState s, int id) =>
		s.GetParent(id) == s.GetWellKnownId(MagicKeys.Stack);

	/// <summary>Every legal choice, in one fixed order: players, permanents (player 1's first), stack, graveyards.</summary>
	public IEnumerable<int> Candidates(GameState s, int controllerId, int sourceId)
	{
		var players = new[] { s.Player1Id(), s.Player2Id() };
		IEnumerable<int> ids = Covers(From.Players) ? players : [];
		if (Covers(From.Battlefield))
			ids = ids.Concat(
				players.SelectMany(p => s.CardsIn(p, ZoneType.Battlefield)).Select(c => c.Id)
			);
		if (Covers(From.Stack) || Covers(From.Ability)) // one pass: "triggered ability or colorless spell" covers both
			ids = ids.Concat(s.GetChildrenIds(s.GetWellKnownId(MagicKeys.Stack)));
		if (Covers(From.AnyGraveyard))
			ids = ids.Concat(
				players.SelectMany(p => s.CardsIn(p, ZoneType.Graveyard)).Select(c => c.Id)
			);
		else if (Covers(From.YourGraveyard))
			ids = ids.Concat(s.CardsIn(controllerId, ZoneType.Graveyard).Select(c => c.Id));
		return ids.Where(id => IsLegal(s, controllerId, sourceId, id));
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

	/// <summary>Chosen targets fit the specs: one per word, each legal, 0 only where optional — a
	/// NotThePrevious word never repeats one before it (X targets are all different).</summary>
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
							&& !(spec.NotThePrevious && targets[..i].Contains(targets[i]))
			)
			.All(ok => ok);
}
