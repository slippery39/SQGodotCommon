using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Token templates for Hollowmere. Kept out of the set's card list — tokens are created at
/// runtime by CreateCardAction, never drawn or drafted.
///
/// Token rates are lower than their paper equivalents on purpose. Combat here has no
/// blockers, so every token connects with the opponent's face; a 1/1 flier is closer to an
/// unblockable 1/1 than to a paper 1/1.
/// </summary>
public static class HollowmereTokens
{
	/// The go-wide payload. Flying because Spirits bypass Taunt, which is the only
	/// defensive tool in this combat model.
	public static Card Spirit() =>
		new()
		{
			Name = "Spirit",
			Subtypes = ImmutableHashSet.Create(StringComparer.OrdinalIgnoreCase, "Spirit"),
			Components = ImmutableArray.Create<GameComponent>(
				new PermanentComponent(),
				new CreatureComponent
				{
					Power = 1,
					Toughness = 1,
					HasFlying = true,
				}
			),
		};

	/// Ground-bound and vanilla — Humans get their value from tribal payoffs, not the body.
	public static Card Human() =>
		new()
		{
			Name = "Human",
			Subtypes = ImmutableHashSet.Create(StringComparer.OrdinalIgnoreCase, "Human"),
			Components = ImmutableArray.Create<GameComponent>(
				new PermanentComponent(),
				new CreatureComponent { Power = 1, Toughness = 1 }
			),
		};

	/// Bigger than the other tokens because Zombie payoffs make fewer of them.
	public static Card Zombie() =>
		new()
		{
			Name = "Zombie",
			Subtypes = ImmutableHashSet.Create(StringComparer.OrdinalIgnoreCase, "Zombie"),
			Components = ImmutableArray.Create<GameComponent>(
				new PermanentComponent(),
				new CreatureComponent { Power = 2, Toughness = 2 }
			),
		};

	/// Lifelink rather than flying — Vampires want the life swing, and it gives the
	/// aggressive decks a way to race through a faster clock.
	public static Card Vampire() =>
		new()
		{
			Name = "Vampire",
			Subtypes = ImmutableHashSet.Create(StringComparer.OrdinalIgnoreCase, "Vampire"),
			Components = ImmutableArray.Create<GameComponent>(
				new PermanentComponent(),
				new CreatureComponent
				{
					Power = 1,
					Toughness = 1,
					HasLifelink = true,
				}
			),
		};
}
