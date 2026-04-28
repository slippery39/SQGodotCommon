namespace MtgSimulator;

public record PreconstructedGameResult
{
	public required GameResult GameResult { get; init; }
	public required string Player1DeckName { get; init; }
	public required string Player2DeckName { get; init; }

	/// <summary>
	/// True if Player 1 is on the play (goes first).
	/// </summary>
	public required bool Player1IsOnPlay { get; init; }

	public string OnPlayDeckName => Player1IsOnPlay ? Player1DeckName : Player2DeckName;
	public string OnDrawDeckName => Player1IsOnPlay ? Player2DeckName : Player1DeckName;

	public string? WinnerDeckName =>
		GameResult.IsDraw ? null
		: GameResult.IsPlayer1Win ? Player1DeckName
		: Player2DeckName;

	public string? LoserDeckName =>
		GameResult.IsDraw ? null
		: GameResult.IsPlayer1Win ? Player2DeckName
		: Player1DeckName;

	public bool DidOnPlayDeckWin => WinnerDeckName == OnPlayDeckName;
}
