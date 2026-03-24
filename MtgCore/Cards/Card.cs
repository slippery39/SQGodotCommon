using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Base record for all cards. Cards are always children of a Zone in GameState.
/// Effects describe what the card does when it resolves — pure data, no behavior.
/// </summary>
public record Card : GameObject
{
	public int ManaCost { get; init; }
	public int OwnerId { get; init; }
	public int ControllerId { get; init; }

	public ImmutableList<CardEffect> Effects { get; init; } = ImmutableList<CardEffect>.Empty;
}

/// <summary>
/// A spell that resolves immediately and goes to the graveyard.
/// Instants can be cast any time you have priority.
/// Sorceries are the same but restricted to your main phase — modeled
/// via game rules rather than a separate subtype for now.
/// </summary>
public record InstantCard : Card;

/// <summary>
/// A permanent that enters the battlefield and stays until destroyed or removed.
/// </summary>
public record CreatureCard : Card
{
	public int Power { get; init; }
	public int Toughness { get; init; }
	public int Damage { get; init; } = 0;
	public bool IsTapped { get; init; } = false;
	public bool HasSummoningSickness { get; init; } = true;
}
