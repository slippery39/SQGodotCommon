using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// "Search your library for a card" — as a REAL choice, offering every legal card in the
/// searching player's library and writing the pick to OutputKey.
///
/// The deliberate counterpart to SelectCardFromLibraryAction, which resolves the same sentence by
/// ranking on mana cost and handing the result over. That auto-pick is the right default and stays
/// the default: for most searches the engine's answer is the answer the player would have given
/// (Simic Growth Chamber returning a land — you rarely care which), and turning every search into a
/// prompt would be worse to play, not better.
///
/// It is wrong precisely where the CHOICE is what the card charges for. Grim Tutor costs three mana
/// AND three life to fetch any card in the deck; picking the most expensive one instead makes it a
/// worse Sign in Blood, and the rendered text still promised "search your library for a card". Use
/// this action when the selection is the card's whole point; use the auto-picker otherwise.
///
/// MinChoices is 1, but GameState.ResolveChoice clamps it to the option count — so an empty or
/// fully-filtered library resolves to "fail to find" (OutputKey is left unset and the downstream
/// mover no-ops) rather than wedging the pipeline on a choice with nothing to choose.
/// </summary>
public record SearchLibraryAction : ChoiceAction
{
	/// <summary>Whose library is searched. Falls back to ContextKeys.CastingPlayerId.</summary>
	public int PlayerId { get; init; }

	/// <summary>Restricts the search to a subtype ("Goblin"). Empty searches every card.</summary>
	public string Subtype { get; init; } = "";

	/// <summary>
	/// Further restriction ("a creature card with mana value 3 or less"). Must be zone-agnostic:
	/// IsCreatureSpecification is battlefield-only and silently matches nothing in a library —
	/// use IsCardTypeSpecification instead.
	/// </summary>
	public TargetSpecification? Filter { get; init; }

	/// <summary>The player doing the searching decides — see ChoiceAction.GetDecidingPlayerId.</summary>
	public override int GetDecidingPlayerId(
		GameState gameState,
		ImmutableDictionary<string, object> pipelineContext
	) => ResolvePlayerId(pipelineContext);

	private int ResolvePlayerId(ImmutableDictionary<string, object> pipelineContext) =>
		PlayerId != 0 ? PlayerId
		: pipelineContext.TryGetValue(ContextKeys.CastingPlayerId, out var id) ? (int)id
		: 0;

	public override ImmutableList<ChoiceOption> GetOptions(
		GameState gameState,
		ImmutableDictionary<string, object> pipelineContext
	)
	{
		var playerId = ResolvePlayerId(pipelineContext);
		if (playerId == 0)
			return ImmutableList<ChoiceOption>.Empty;

		var context = new TargetingContext
		{
			GameState = gameState,
			CastingPlayerId = playerId,
			SourceCardId = pipelineContext.TryGetValue(ContextKeys.SourceCardId, out var src)
				? (int)src
				: 0,
			IsNonTargeted = true,
		};

		var libraryId = gameState.GetPlayerZoneId(playerId, ZoneType.Library);

		return gameState
			.GetCardsInZone(libraryId)
			.Where(c => string.IsNullOrEmpty(Subtype) || c.HasSubtype(Subtype))
			.Where(c => Filter == null || Filter.IsSatisfiedBy(c.Id, context))
			// A library is shuffled, so its order carries no information and would present the
			// player with an arbitrary list. Sorting makes the same search read the same way twice.
			.OrderBy(c => c.ManaCost)
			.ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
			.Select(c => new ChoiceOption { Id = c.Id, DisplayText = $"{c.Name} ({c.ManaCost})" })
			.ToImmutableList();
	}
}
