namespace MtgGame;

public abstract record DeckChoice
{
	public sealed record Premade(string Name) : DeckChoice;

	public sealed record RandomPremade : DeckChoice;

	public sealed record Randomized : DeckChoice;
}
