namespace ImmutableGameObjects;

/// <summary>
/// A single option the player can choose from.
/// </summary>
public record ChoiceOption
{
	public int Id { get; init; } // The ID of the thing being chosen (card, enemy, etc.)
	public string DisplayText { get; init; } = "";
	public bool IsEnabled { get; init; } = true;
}
