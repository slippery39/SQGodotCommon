using System.Collections.Generic;
using MtgCore;

namespace MtgGame;

public abstract record DeckChoice
{
	public sealed record Premade(string Name) : DeckChoice;

	public sealed record RandomPremade : DeckChoice;

	public sealed record Randomized : DeckChoice;

	/// <summary>
	/// A drafted pool, in pick order. Holds the pool rather than a deck because
	/// <c>Draft.BuildDeck</c> stamps OwnerId/ControllerId and so must run per game — the same
	/// seat is Player 1 in some pairings and Player 2 in others.
	/// </summary>
	public sealed record Drafted(IReadOnlyList<Card> Pool) : DeckChoice;
}
