using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore.Cards.Builders;
using static MtgCore.Cards.Builders.TargetBuilder;

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
/// Batches 1 and 3: 20 cards — 17 double-faced, plus 3 support cards that reward the theme's
/// real cost, which is the turns you spend not casting spells.
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
			// ===== BATCH 3 =====
			Werewolf(
				dayName: "Mere-Road Vagrant",
				nightName: "Gaunt Howler",
				manaCost: 1,
				dayPower: 1,
				dayToughness: 2,
				nightPower: 2,
				nightToughness: 3,
				extraDaySubtype: Hollowmere.Rogue
			),
			Werewolf(
				dayName: "Chapel Bell-Warden",
				nightName: "Bell-Torn Beast",
				manaCost: 2,
				dayPower: 1,
				dayToughness: 3,
				nightPower: 4,
				nightToughness: 3,
				dayTaunt: true,
				extraDaySubtype: Hollowmere.Cleric
			),
			Werewolf(
				dayName: "Hollowmere Poacher",
				nightName: "Silt-Slick Prowler",
				manaCost: 2,
				dayPower: 2,
				dayToughness: 2,
				nightPower: 4,
				nightToughness: 4,
				extraDaySubtype: Hollowmere.Rogue
			),
			Werewolf(
				dayName: "Grim-Faced Reeve",
				nightName: "Reeve of Broken Oaths",
				manaCost: 2,
				dayPower: 2,
				dayToughness: 1,
				nightPower: 3,
				nightToughness: 3,
				nightTrample: true,
				extraDaySubtype: Hollowmere.Soldier
			),
			Werewolf(
				dayName: "Ashwood Herbalist",
				nightName: "Ashwood Ripper",
				manaCost: 2,
				dayPower: 1,
				dayToughness: 2,
				nightPower: 3,
				nightToughness: 3,
				dayLifelink: true,
				nightLifelink: true,
				extraDaySubtype: Hollowmere.Cleric
			),
			Werewolf(
				dayName: "Moonlit Sentry",
				nightName: "Sentry of the Long Howl",
				manaCost: 3,
				dayPower: 2,
				dayToughness: 4,
				nightPower: 5,
				nightToughness: 4,
				dayTaunt: true,
				nightTaunt: true,
				extraDaySubtype: Hollowmere.Soldier
			),
			Werewolf(
				dayName: "Village Rabblerouser",
				nightName: "Rabid Rabblerouser",
				manaCost: 3,
				dayPower: 3,
				dayToughness: 2,
				nightPower: 5,
				nightToughness: 3,
				nightHaste: true
			),
			Werewolf(
				dayName: "Mere-Bank Tracker",
				nightName: "Tracker of Cold Scents",
				manaCost: 3,
				dayPower: 2,
				dayToughness: 3,
				nightPower: 4,
				nightToughness: 4,
				nightDeathtouch: true,
				extraDaySubtype: Hollowmere.Rogue
			),
			Werewolf(
				dayName: "Cloistered Novice",
				nightName: "Unhallowed Novice",
				manaCost: 3,
				dayPower: 3,
				dayToughness: 3,
				nightPower: 5,
				nightToughness: 5,
				extraDaySubtype: Hollowmere.Cleric
			),
			Werewolf(
				dayName: "Hollowmere Houndmaster",
				nightName: "Master of the Pack",
				manaCost: 4,
				dayPower: 4,
				dayToughness: 3,
				nightPower: 6,
				nightToughness: 4,
				nightTrample: true
			),
			Werewolf(
				dayName: "Watcher at the Ford",
				nightName: "Ford-Drowned Horror",
				manaCost: 4,
				dayPower: 2,
				dayToughness: 5,
				nightPower: 5,
				nightToughness: 6,
				dayTaunt: true,
				nightTaunt: true,
				extraDaySubtype: Hollowmere.Soldier
			),
			Werewolf(
				dayName: "Blood-Moon Acolyte",
				nightName: "Blood-Moon Devourer",
				manaCost: 4,
				dayPower: 3,
				dayToughness: 4,
				nightPower: 6,
				nightToughness: 5,
				nightLifelink: true,
				extraDaySubtype: Hollowmere.Cleric
			),
			Werewolf(
				dayName: "Elder of the Long Night",
				nightName: "Terror of the Long Night",
				manaCost: 5,
				dayPower: 4,
				dayToughness: 4,
				nightPower: 7,
				nightToughness: 6,
				nightTrample: true,
				nightHaste: true
			),
			Werewolf(
				dayName: "Hollowmere Lycanthrope",
				nightName: "Scourge of Hollowmere",
				manaCost: 6,
				dayPower: 5,
				dayToughness: 5,
				nightPower: 8,
				nightToughness: 7,
				nightTrample: true,
				nightDeathtouch: true
			),
			// ===== WEREWOLF SUPPORT =====
			// The tribal anthem. Werewolves have no lord on a body, because a Werewolf that
			// dies to removal takes its night face with it — the anthem is the safer home.
			CardFactory
				.Creature("Howlpack Alpha", manaCost: 4, power: 3, toughness: 3)
				.WithSubtype(Hollowmere.Werewolf)
				.WithComponent(
					new StaticPTBoostAbility
					{
						PowerBonus = 1,
						ToughnessBonus = 1,
						Filter = new IsSubtypeSpecification { Subtype = Hollowmere.Werewolf }.And(
							TargetSpecification.OtherCreaturesYouControl()
						),
					}
				)
				.WithComponent(
					new StaticGrantKeywordAbility
					{
						GrantsTrample = true,
						Filter = new IsSubtypeSpecification { Subtype = Hollowmere.Werewolf }.And(
							TargetSpecification.OtherCreaturesYouControl()
						),
					}
				)
				.Build(),
			// Pays you for the theme's real cost — the turns you spend not casting spells.
			CardFactory
				.Creature("Keeper of the Quiet Hour", manaCost: 3, power: 2, toughness: 3)
				.WithSubtype(Hollowmere.Human)
				.WithSubtype(Hollowmere.Cleric)
				.WithTriggeredAbility(
					"Reward the Silence",
					new SpellsCastLastTurnCondition { Maximum = 0 },
					eb => eb.WithDraw(1)
				)
				.Build(),
			// A one-sided answer: your werewolves are already flipped, theirs are not.
			CardFactory
				.Spell("Moonrise", manaCost: 2)
				.WithGrantKeyword(trample: true, haste: true)
				.WithTarget(AllValid().AllYourCreatures())
				.Build(),
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
		bool dayLifelink = false,
		bool dayTaunt = false,
		bool dayFlying = false,
		bool nightTrample = false,
		bool nightTaunt = false,
		bool nightDeathtouch = false,
		bool nightLifelink = false,
		bool nightHaste = false,
		bool nightFlying = false,
		string? extraDaySubtype = null
	)
	{
		// The day face is a Human, which is what makes every werewolf double as Human tribal
		// density — a Werewolf deck is a Human deck that deliberately runs fewer spells.
		var daySubtypes = ImmutableHashSet.Create(
			StringComparer.OrdinalIgnoreCase,
			Hollowmere.Human,
			Hollowmere.Werewolf
		);
		if (extraDaySubtype != null)
			daySubtypes = daySubtypes.Add(extraDaySubtype);
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
				HasLifelink = nightLifelink,
				HasHaste = nightHaste,
				HasFlying = nightFlying,
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
					HasLifelink = dayLifelink,
					HasTaunt = dayTaunt,
					HasFlying = dayFlying,
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
