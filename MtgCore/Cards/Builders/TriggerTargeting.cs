using System.Collections.Immutable;

namespace MtgCore.Cards.Builders;

/// <summary>
/// Makes a trigger's effects safe to resolve.
///
/// A triggered ability spawns ResolveEffectAction with NO TargetIds, so a user-select targeting
/// strategy inside a trigger resolves to an EMPTY target list and the effect silently does
/// nothing. Nothing throws, nothing logs, and the card simply never works.
///
/// This was found the expensive way: 19 Core Set Cube cards shipped with dead triggers —
/// Pegasus Courser, Frost Lynx, Dungeon Geists, Sun Titan, Oblivion Ring, Agent of Treachery and
/// more — because several builder verbs (WithExhaust, WithFreeze, WithBounce, WithGainControl)
/// reasonably DEFAULT to single-target for the spells that use them, and a trigger reusing the
/// verb inherited that default.
///
/// Fixing it at each call site would fix those 19 and leave the trap armed for the next card.
/// Every trigger goes through the builders, so downgrading here makes the bug unreachable:
/// UserSelect becomes Random, which picks one legal target instead of none.
///
/// Random rather than AllValid because these are "target X" effects — hitting everything would
/// silently make the card much stronger than printed. A card that genuinely wants every target
/// asks for AllValid explicitly, and that is preserved untouched.
/// </summary>
internal static class TriggerTargeting
{
	public static ImmutableList<CardEffect> MakeResolvable(ImmutableList<CardEffect> effects) =>
		effects.Select(MakeResolvable).ToImmutableList();

	private static CardEffect MakeResolvable(CardEffect effect)
	{
		if (!effect.TargetingStrategy.RequiresUserSelection)
			return effect;

		return effect with
		{
			TargetingStrategy = effect.TargetingStrategy with
			{
				SelectionMode = TargetSelectionMode.Random,
			},
		};
	}
}
