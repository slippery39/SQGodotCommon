using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Picks one card out of a list of revealed card IDs and writes it to OutputKey — the missing
/// middle of "look at the top N cards, put one into your hand".
///
/// LookAtTopCardsAction is a pure query: it writes IDs into pipeline context and stops. Nothing
/// consumed them, so EVERY dig card in the cube revealed cards and then did nothing — Track Down
/// was a two-mana scry with no cantrip, Llanowar Empath was a vanilla 2/2, and Drawn from Dreams
/// and Search the Parish were blank. The reveal step needs a chooser and a mover; this is the
/// chooser.
///
/// SELECTION IS BEST-BY-MANA-COST, non-land, matching SelectCardFromLibraryAction.SelectBestByManaCost
/// and Clone's "copy the biggest thing". A dig is a player choice the engine has to stand in for,
/// and taking the first revealed card would make the whole mechanic "draw the top card" — which is
/// exactly the trap that rule was written for.
///
/// Filter narrows what may be taken ("reveal a creature or land card"). When nothing matches,
/// OutputKey is written as 0 and the downstream mover no-ops, which is the correct outcome for a
/// dig that whiffed.
/// </summary>
public record SelectFromRevealedAction : GameAction
{
	public string RevealedIdsContextKey { get; init; } = ContextKeys.TopCardIds;
	public string OutputKey { get; init; } = "";
	public TargetSpecification? Filter { get; init; }

	/// <summary>
	/// Allow a land to be taken. ON by default, unlike the other "best card" pickers.
	///
	/// It was off, copied from SelectCardFromLibraryAction where excluding lands is right: that
	/// one searches the WHOLE library, so a land is never the card you were hunting for. A dig
	/// looks at one to four cards and says "put one into your hand" — the player takes the land
	/// when the land is what is there.
	///
	/// Off, the dig matched nothing whenever every revealed card was a land, wrote 0, and the
	/// mover no-opped: Track Down simply did not draw about a third of the time, silently and at
	/// full price. Every dig card in the cube shared it. The existing test stocked a library of
	/// four creatures and no lands, so it passed throughout.
	///
	/// Safe because selection is MaxBy(ManaCost) and a land costs 0: it can only ever win when
	/// nothing else was revealed, which is precisely when it should.
	/// </summary>
	public bool AllowLands { get; init; } = true;

	public override ActionResult Execute(GameState gameState)
	{
		if (string.IsNullOrEmpty(OutputKey))
			return new ActionResult(gameState);

		var revealed = GetInput<ImmutableList<int>>(
			RevealedIdsContextKey,
			ImmutableList<int>.Empty
		);

		var castingPlayerId = GetInput<int>(ContextKeys.CastingPlayerId, 0);
		var context = new TargetingContext
		{
			GameState = gameState,
			SourceCardId = GetInput<int>(ContextKeys.SourceCardId, 0),
			CastingPlayerId = castingPlayerId,
			IsNonTargeted = true,
		};

		var best = revealed
			.Where(gameState.HasObject)
			.Select(id => gameState.GetObject(id) as Card)
			.Where(c => c != null)
			.Where(c => AllowLands || !c!.HasSubtype("Land"))
			.Where(c => Filter == null || Filter.IsSatisfiedBy(c!.Id, context))
			.MaxBy(c => c!.ManaCost);

		return new ActionResult(gameState).WithOutput(OutputKey, best?.Id ?? 0);
	}
}
