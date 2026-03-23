namespace ImmutableGameObjects;

/// <summary>
/// Result of validating an action. Contains whether the action is valid
/// and an optional reason if it is not.
/// </summary>
public record ValidationResult
{
	public bool IsValid { get; init; }
	public string Reason { get; init; } = "";

	public static readonly ValidationResult Valid = new() { IsValid = true };

	public static ValidationResult Invalid(string reason) =>
		new() { IsValid = false, Reason = reason };
}
