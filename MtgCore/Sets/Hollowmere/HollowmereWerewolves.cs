using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// Theme 9 — Werewolves. Humans that transform when the previous turn was quiet, and
/// transform back when it was busy.
///
/// Each card is one Card with a TransformComponent carrying the night face inline. Only one
/// face's components are live at a time, so the day face holds the "no spells cast last turn"
/// trigger and the night face holds the "two or more spells cast last turn" trigger — they
/// swap along with the stats.
///
/// The tension is real in this engine: the Spells theme wants to cast several cheap spells a
/// turn, which is exactly what keeps werewolves human. A Werewolf deck is a Human deck that
/// deliberately runs fewer spells.
///
/// Batch 1 contribution: 3 cards.
/// </summary>
public static class HollowmereWerewolves
{
	public static IReadOnlyList<Card> Cards { get; } =
		[
			Werewolf(
				dayName: "Village Messenger",
				nightName: "Moonrise Stalker",
				manaCost: 1,
				dayPower: 1,
				dayToughness: 1,
				nightPower: 3,
				nightToughness: 3,
				dayHaste: true,
				nightTrample: true
			),
			Werewolf(
				dayName: "Hollowmere Shepherd",
				nightName: "Fanged Shepherd",
				manaCost: 3,
				dayPower: 2,
				dayToughness: 3,
				nightPower: 5,
				nightToughness: 5,
				nightTaunt: true
			),
			Werewolf(
				dayName: "Lantern-Lit Trapper",
				nightName: "Throat-Ripper Lycanthrope",
				manaCost: 4,
				dayPower: 3,
				dayToughness: 3,
				nightPower: 6,
				nightToughness: 5,
				nightTrample: true,
				nightDeathtouch: true
			),
		];

	/// <summary>
	/// Builds a two-faced werewolf. Written as a helper because every werewolf differs only
	/// in names, stats, and keywords — the transform wiring is identical and error-prone to
	/// repeat (both faces need their own trigger, pointing at each other).
	/// </summary>
	private static Card Werewolf(
		string dayName,
		string nightName,
		int manaCost,
		int dayPower,
		int dayToughness,
		int nightPower,
		int nightToughness,
		bool dayHaste = false,
		bool nightTrample = false,
		bool nightTaunt = false,
		bool nightDeathtouch = false
	)
	{
		var daySubtypes = ImmutableHashSet.Create(
			StringComparer.OrdinalIgnoreCase,
			Hollowmere.Human,
			Hollowmere.Werewolf
		);
		var nightSubtypes = ImmutableHashSet.Create(
			StringComparer.OrdinalIgnoreCase,
			Hollowmere.Werewolf
		);

		// Night face: transforms back when the previous turn saw two or more spells.
		//
		// Deliberately carries NO TransformComponent. TransformAction rebuilds one pointing
		// back at the face it came from on every flip, so adding our own here would leave the
		// night face with two — and FirstOrDefault would pick ours, whose OtherFaceComponents
		// would have to be the day face, recursing forever.
		var nightComponents = ImmutableArray.Create<GameComponent>(
			new PermanentComponent(),
			new CreatureComponent
			{
				Power = nightPower,
				Toughness = nightToughness,
				HasTrample = nightTrample,
				HasTaunt = nightTaunt,
				HasDeathtouch = nightDeathtouch,
			},
			new TriggeredAbilityComponent
			{
				Name = "Return to Human Form",
				Condition = new SpellsCastLastTurnCondition { Minimum = 2 },
				Effect = TransformSelf(),
			}
		);

		return new Card
		{
			Name = dayName,
			ManaCost = manaCost,
			Subtypes = daySubtypes,
			Components = ImmutableArray.Create<GameComponent>(
				new PermanentComponent(),
				new CreatureComponent
				{
					Power = dayPower,
					Toughness = dayToughness,
					HasHaste = dayHaste,
				},
				new TriggeredAbilityComponent
				{
					Name = "Transform",
					Condition = new SpellsCastLastTurnCondition { Maximum = 0 },
					Effect = TransformSelf(),
				},
				new TransformComponent
				{
					OtherFaceName = nightName,
					OtherFaceSubtypes = nightSubtypes,
					OtherFaceComponents = nightComponents,
				}
			),
		};
	}

	private static CardEffect TransformSelf() =>
		new()
		{
			TargetingStrategy = TargetingStrategy.NoTarget(),
			ActionTemplate = new TransformAction { TargetContextKey = ContextKeys.SourceCardId },
		};
}
