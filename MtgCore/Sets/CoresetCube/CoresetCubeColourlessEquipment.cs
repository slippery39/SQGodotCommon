using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore.Cards.Builders;
using static MtgCore.Cards.Builders.TargetBuilder;

namespace MtgCore;

/// <summary>
/// Equipment from the Core Set Cube — https://cubecobra.com/cube/list/magiccoreset20xx
///
/// All 14, in the cube's own order (by mana value, then name). This is far and away the biggest
/// concentration of Equipment in the project; before it, the whole engine had three.
///
/// THE FIVE RINGS ARE THE REASON IsEquippedBySourceSpecification EXISTS. Each is printed as a
/// keyword grant plus "at the beginning of your upkeep, put a +1/+1 counter on equipped creature if
/// it's [colour]". The colour gate is unreachable, and with it gone the five would be near-identical
/// vanilla equipment — which the Hollowmere rate rule forbids outright, and which would be five
/// wasted slots in a 450-card cube. Keeping the upkeep counter is what makes them distinct, and
/// keeping it requires an attachment to be able to name its own wearer from its own trigger.
/// TargetingContext already carried SourceCardId; nothing else was needed.
///
/// EQUIP IS maxPerTurn: 0 THROUGHOUT. A repeatable equip is the printed rule, and the two existing
/// Equipment in the cube disagree with each other about it (Ancestral Blade passes 0, Wolfrider's
/// Saddle omits it and is silently once-per-turn). Moving a sword off a dying creature is most of
/// what Equipment does, so it is repeatable here without exception.
///
/// EQUIP COSTS ARE THE PRINTED ONES, and the cast costs mostly are too. Equipment is stronger here
/// than in real Magic for a structural reason worth stating: with no blockers, a creature carrying a
/// sword attacks unopposed every turn, and there is no combat-trick blowout to punish over-investing
/// in one body. Removal is the only answer, so the equip cost is the tempo tax that keeps them fair.
///
/// DIVERGENCES beyond the colour and blocking cuts listed in CoresetCubeBlack:
///   - VIGILANCE is deliberately unimplemented — Ring of Thune and Sword of Vengeance both lose it.
///     Dropping a keyword only ever helps the player, so neither is costed down for it.
///   - NO BLOCKING, so "can't be blocked" is meaningless — Whispersilk Cloak keeps only shroud.
///   - NO REGENERATION, so Ring of Xathrid needs a different keyword entirely.
///   - NO UNTAP EFFECT and artifacts never exhaust, so Manifold Key's whole first ability is
///     doubly unreachable; see CoresetCubeColourlessArtifacts, where it lives.
///   - ANIMATION EXISTS NOW (AnimateAction), so Haunted Plate Mail keeps its 4/4 mode — see below
///     for the one clause it still cannot have.
/// </summary>
public static class CoresetCubeColourlessEquipment
{
	public static IReadOnlyList<Card> Cards { get; } =
		[
			// ===== ONE =====

			// Printed: "Equipped creature gets +1/+1. Equip {1}." Faithful.
			Equipment("Short Sword", manaCost: 1, equipCost: 1, power: 1, toughness: 1),
			// ===== TWO =====

			// Printed: "Equipped creature gets +1/+1 and has deathtouch. Equip {2}."
			// Faithful. Deathtouch on an unblockable attacker is a premium keyword here — every
			// attack into a creature becomes a favourable trade — so the printed equip {2} stays.
			Equipment(
				"Gorgon Flail",
				manaCost: 2,
				equipCost: 2,
				power: 1,
				toughness: 1,
				deathtouch: true
			),
			// Printed: "Equipped creature has hexproof and haste. Equip {1}." Faithful.
			Equipment("Swiftfoot Boots", manaCost: 2, equipCost: 1, hexproof: true, haste: true),
			// ===== THE RINGS =====
			// All five: keyword grant, plus an upkeep +1/+1 counter on the wearer, equip {1}. The
			// printed colour gate on the counter is cut, which makes the counter unconditional and
			// each Ring meaningfully stronger than printed — so each costs one more than its
			// printed {2}. The keyword is what tells them apart, and it is chosen to match the
			// colour the printed card cared about.

			// Printed: "{2}: Equipped creature gains hexproof until end of turn."
			// The activated grant becomes a static one — a one-shot pump-for-mana is a decision the
			// AI has to re-derive every turn, and a permanent hexproof on a growing creature is the
			// blue Ring's actual role.
			Ring("Ring of Evos Isle", hexproof: true),
			// Printed: "Equipped creature has trample." Faithful keyword.
			Ring("Ring of Kalonia", trample: true),
			// Printed: "Equipped creature has vigilance." Vigilance is unimplemented, so this takes
			// first strike instead — the white Ring should reward attacking into a board, and first
			// strike on a creature that grows every upkeep is exactly that.
			Ring("Ring of Thune", firstStrike: true),
			// Printed: "Equipped creature has haste." Faithful keyword.
			Ring("Ring of Valkas", haste: true),
			// Printed: "{2}: Regenerate equipped creature." Regeneration does not exist and would
			// need a structural replacement ("the next time it would be destroyed"). Indestructible
			// is the nearest thing the engine has and is strictly better, so this Ring is the
			// expensive one of the five.
			Ring("Ring of Xathrid", indestructible: true, manaCost: 4),
			// Printed: "Legendary. Equipped creature gets +1/+1. Whenever equipped creature attacks,
			// you may search your library for a basic land card, put it onto the battlefield tapped,
			// then shuffle. Equip {2}."
			//
			// Faithful, and the second card in the cube to need IsEquippedBySourceSpecification —
			// here as a trigger FILTER rather than as an effect target, which is the same question
			// asked from the other side. "You may" is mandatory; ramp is never unwanted.
			// Legendary is not modelled anywhere in this engine.
			CardFactory
				.Artifact("Sword of the Animist", manaCost: 2)
				.WithSubtype("Equipment")
				.WithComponent(new EquipmentComponent { PowerBonus = 1, ToughnessBonus = 1 })
				.WithTriggeredAbility(
					"Trailblaze",
					new EventTriggerCondition
					{
						EventTypeName = EventTypeNames.CreatureAttacked,
						Filter = new IsEquippedBySourceSpecification(),
					},
					eb =>
						eb.WithAction(
							new GainPermanentManaAction { Amount = 1, Deferred = true },
							TargetingStrategy.Self()
						)
				)
				.WithEquip(2)
				.Build(),
			// ===== THREE =====

			// Printed: "Equipped creature has double strike. Equip {2}." Faithful.
			Equipment("Fireshrieker", manaCost: 3, equipCost: 2, doubleStrike: true),
			// Printed: "Equipped creature gets +3/+0. Equip {3}." Faithful.
			Equipment("Greatsword", manaCost: 3, equipCost: 3, power: 3),
			// Printed: "Equipped creature gets +2/+0 and has first strike, vigilance, trample, and
			// haste. Equip {3}." Vigilance is cut; the other three are faithful.
			Equipment(
				"Sword of Vengeance",
				manaCost: 3,
				equipCost: 3,
				power: 2,
				firstStrike: true,
				trample: true,
				haste: true
			),
			// Printed: "Equipped creature can't be blocked and has shroud. Equip {2}."
			// "Can't be blocked" is meaningless with no blocking. Shroud is the half that plays,
			// and it is a real one — it protects the creature from the opponent's removal, which is
			// the only thing that answers an equipped attacker here. Costed down by one for the
			// loss, and note shroud cuts BOTH ways: you cannot target your own creature either, so
			// this cannot be stacked with a second aura or a combat trick.
			Equipment("Whispersilk Cloak", manaCost: 2, equipCost: 2, shroud: true),
			// ===== FOUR =====

			// Printed: "Equipped creature gets +4/+4. {0}: Until end of turn, this permanent becomes
			// a 4/4 Spirit artifact creature that's no longer an Equipment. Activate only if you
			// control no creatures. Equip {4}."
			//
			// The animation is REAL, via AnimateAction — this is the card that motivated building
			// it. The one clause dropped is "that's no longer an Equipment", which is unreachable
			// rather than merely hard: the ability can only be activated while you control no
			// creatures, so the Plate Mail cannot be attached to anything at the time.
			//
			// It CAN equip itself once animated, and that is left alone deliberately: it costs {4}
			// and a whole turn to reach an 8/8, on a board where you controlled nothing, and the
			// AnimateAction wears off at end of turn while the equip cost does not come back.
			CardFactory
				.Artifact("Haunted Plate Mail", manaCost: 4)
				.WithSubtype("Equipment")
				.WithComponent(new EquipmentComponent { PowerBonus = 4, ToughnessBonus = 4 })
				.WithActivatedAbility(
					"Animate",
					manaCost: 0,
					effect: eb =>
						eb.WithAction(
							new AnimateAction
							{
								Power = 4,
								Toughness = 4,
								Subtype = "Spirit",
								TargetContextKey = ContextKeys.SourceCardId,
							},
							TargetingStrategy.NoTarget()
						),
					condition: new ControlsNoCreaturesCondition()
				)
				.WithEquip(4)
				.Build(),
		];

	/// <summary>
	/// A plain Equipment: stat bonus, keyword grants, repeatable equip. Fourteen cards differing
	/// only in their numbers and their keyword list is exactly what a helper is for, and writing
	/// each one out longhand is fourteen chances to forget maxPerTurn or the Equipment subtype.
	/// </summary>
	private static Card Equipment(
		string name,
		int manaCost,
		int equipCost,
		int power = 0,
		int toughness = 0,
		bool firstStrike = false,
		bool doubleStrike = false,
		bool deathtouch = false,
		bool trample = false,
		bool haste = false,
		bool hexproof = false,
		bool shroud = false,
		bool indestructible = false
	) =>
		CardFactory
			.Artifact(name, manaCost)
			.WithSubtype("Equipment")
			.WithComponent(
				new EquipmentComponent
				{
					PowerBonus = power,
					ToughnessBonus = toughness,
					// The keyword grants live on the boost stamped onto the creature, not on the
					// EquipmentComponent — GetEffectiveStats reads them in their own pass, because
					// a Permanent-duration AppliedKeywordComponent is owned by StaticAbilityEngine
					// and would be stripped out from under an attachment.
					CustomBoostTemplate = new EquippedBoostComponent
					{
						PowerBonus = power,
						ToughnessBonus = toughness,
						GrantsFirstStrike = firstStrike,
						GrantsDoubleStrike = doubleStrike,
						GrantsDeathtouch = deathtouch,
						GrantsTrample = trample,
						GrantsHaste = haste,
						GrantsHexproof = hexproof,
						GrantsShroud = shroud,
						GrantsIndestructible = indestructible,
						Duration = ModifierDuration.Permanent,
					},
				}
			)
			.WithEquip(equipCost)
			.Build();

	/// <summary>
	/// One of the five Rings: a keyword grant plus an unconditional +1/+1 counter on the wearer
	/// each of your upkeeps, equip {1}.
	///
	/// The upkeep trigger is the whole reason these are not five copies of Short Sword, and it is
	/// the reason IsEquippedBySourceSpecification exists — see the file header.
	/// </summary>
	private static Card Ring(
		string name,
		int manaCost = 3,
		bool firstStrike = false,
		bool trample = false,
		bool haste = false,
		bool hexproof = false,
		bool indestructible = false
	) =>
		CardFactory
			.Artifact(name, manaCost)
			.WithSubtype("Equipment")
			.WithComponent(
				new EquipmentComponent
				{
					CustomBoostTemplate = new EquippedBoostComponent
					{
						GrantsFirstStrike = firstStrike,
						GrantsTrample = trample,
						GrantsHaste = haste,
						GrantsHexproof = hexproof,
						GrantsIndestructible = indestructible,
						Duration = ModifierDuration.Permanent,
					},
				}
			)
			.WithTriggeredAbility(
				"Attune",
				TriggerConditions.OnYourUpkeep(),
				eb =>
					eb.WithAction(
						new AddCountersAction { Amount = 1 },
						TargetingStrategy.AllValid(new IsEquippedBySourceSpecification())
					)
			)
			.WithEquip(1)
			.Build();
}
