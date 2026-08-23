using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Searches a player's zone for the first card matching the given subtype and
/// writes its ID to OutputKey in pipeline context.
///
/// Follows the same pattern as SelectCardFromLibraryAction but targets any ZoneType
/// (typically ZoneType.Exile). Used by Simic Growth Chamber to find a land in the exile zone.
///
/// Player resolution:
///   - Set PlayerId directly, or
///   - Set PlayerIdContextKey to read the player ID from pipeline context (takes priority).
/// If TargetOpponent is true, derives the opponent from the resolved player ID.
/// If no matching card is found, OutputKey is set to 0 — downstream actions must handle 0.
/// </summary>
public record SelectCardFromZoneAction : GameAction
{
	public ZoneType Zone { get; init; }
	public string Subtype { get; init; } = "";

	/// <summary>
	/// Optional specification the card must also satisfy. Subtype alone cannot express
	/// "a creature card" or "an instant or sorcery", because those are identified by
	/// components rather than by a subtype string — use
	/// IsCreatureInOwnGraveyardSpecification / IsInstantOrSorceryInOwnGraveyardSpecification.
	///
	/// Evaluated with CastingPlayerId set to the player whose zone is being searched, so
	/// "own graveyard" specs resolve against that zone even when TargetOpponent is true.
	/// </summary>
	public TargetSpecification? Filter { get; init; } = null;

	public int PlayerId { get; init; } = 0;
	public string PlayerIdContextKey { get; init; } = "";
	public string OutputKey { get; init; } = "";
	public bool TargetOpponent { get; init; } = false;

	/// <summary>
	/// When true, excludes the card whose ID is in context under ContextKeys.SourceCardId.
	/// Use this when the source card itself has just entered the zone being searched
	/// (e.g. Simic Growth Chamber moves to exile then searches exile — without this flag it would
	/// find and return itself, producing a self-bounce loop).
	/// </summary>
	public bool ExcludeSourceCard { get; init; } = false;

	/// <summary>
	/// Bounds the search to cards costing STRICTLY LESS than the card whose id sits in context
	/// under this key. Empty means no bound.
	///
	/// The bound has to be dynamic: Evolutionary Leap replaces a creature that just died with a
	/// cheaper one, so the cap is whatever that creature cost and is not knowable at build time.
	/// HasManaCostAtMostSpecification takes a literal, which cannot express it.
	///
	/// A missing or unresolvable card id means NO bound rather than a bound of zero — a zero cap
	/// would silently match nothing and the card would read as a blank.
	/// </summary>
	public string MaxManaCostExclusiveFromCardContextKey { get; init; } = "";

	/// <summary>
	/// Picks the most expensive match instead of the first one. Mirrors
	/// SelectCardFromLibraryAction.SelectBestByManaCost and exists for the same reason: library
	/// order is random, so a first-match search is really "the top card of your library" and a
	/// tutor that finds a random creature is not a tutor.
	/// </summary>
	public bool SelectBestByManaCost { get; init; } = false;

	public override ActionResult Execute(GameState gameState)
	{
		var castingPlayerId = string.IsNullOrEmpty(PlayerIdContextKey)
			? PlayerId
			: GetInput<int>(PlayerIdContextKey, PlayerId);

		if (castingPlayerId == 0)
			return new ActionResult(gameState);

		var playerId = TargetOpponent ? GetOpponentId(gameState, castingPlayerId) : castingPlayerId;

		var excludeId = ExcludeSourceCard ? GetInput<int>(ContextKeys.SourceCardId, 0) : 0;

		var zoneId = gameState.GetPlayerZoneId(playerId, Zone);

		var filterContext = new TargetingContext
		{
			GameState = gameState,
			CastingPlayerId = playerId,
			SourceCardId = GetInput<int>(ContextKeys.SourceCardId, 0),
			IsNonTargeted = true,
		};

		// int.MaxValue means "no bound" — see MaxManaCostExclusiveFromCardContextKey. A missing
		// reference card must not collapse the search to nothing.
		var maxCostExclusive = int.MaxValue;
		if (!string.IsNullOrEmpty(MaxManaCostExclusiveFromCardContextKey))
		{
			var referenceId = GetInput<int>(MaxManaCostExclusiveFromCardContextKey, 0);
			if (referenceId != 0 && gameState.GetObject(referenceId) is Card reference)
				maxCostExclusive = reference.ManaCost;
		}

		var matches = gameState
			.GetCardsInZone(zoneId)
			.Where(c =>
				(string.IsNullOrEmpty(Subtype) || c.HasSubtype(Subtype))
				&& (excludeId == 0 || c.Id != excludeId)
				&& c.ManaCost < maxCostExclusive
				&& (Filter == null || Filter.IsSatisfiedBy(c.Id, filterContext))
			);

		// MaxBy ties on the first match, which is library order — random but deterministic for a
		// given shuffle, same rule the rest of the engine uses for engine-made choices.
		var candidate = SelectBestByManaCost
			? matches.MaxBy(c => c.ManaCost)
			: matches.FirstOrDefault();

		var foundId = candidate?.Id ?? 0;

		var result = new ActionResult(gameState);
		if (!string.IsNullOrEmpty(OutputKey))
			result = result.WithOutput(OutputKey, foundId);

		return result;
	}

	private static int GetOpponentId(GameState gameState, int castingPlayerId)
	{
		var p1Id = gameState.GetWellKnownId(MtgObjectKeys.Player1);
		return castingPlayerId == p1Id ? gameState.GetWellKnownId(MtgObjectKeys.Player2) : p1Id;
	}
}
