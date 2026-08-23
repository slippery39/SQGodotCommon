using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore.Cards.Builders;
using static MtgCore.Cards.Builders.TargetBuilder;

namespace MtgCore;

/// <summary>
/// Artifact creatures from the Core Set Cube — https://cubecobra.com/cube/list/magiccoreset20xx
///
/// All 11, in the cube's own order (by mana value, then name).
///
/// EVERY CARD HERE DECLARES BOTH CardType.Artifact AND the "Artifact" SUBTYPE, and that is not
/// belt-and-braces. Card.EffectiveTypes short-circuits on declared Types, so a creature built with
/// only .WithSubtype("Artifact") answers HasType(CardType.Artifact) as FALSE — while CostEngine's
/// affinity count and SacrificeAdditionalCost read the subtype string and IsCardTypeSpecification
/// reads the flag. All five artifact creatures in the legacy CardLibrary have exactly that bug and
/// are invisible to half the engine. CreatureCardBuilder does not keep the two in sync the way
/// PermanentCardBuilder's constructor does, so it has to be done by hand, every time.
///
/// THE COLOURLESS SECTION IS THE ARTIFACT SECTION, and that makes this file load-bearing for
/// affinity: Frogmite, Myr Enforcer and Thoughtcast already exist and count artifact permanents,
/// so these eleven bodies are the cheapest artifacts in the cube and the ones that turn affinity
/// from a dead keyword into a deck.
///
/// DIVERGENCES beyond the colour and blocking cuts listed in CoresetCubeBlack:
///   - NO CHOSEN CREATURE TYPE and NO NAMING A CARD, so Adaptive Automaton and Phyrexian Revoker
///     are both reskinned. There is no runtime-choice state a specification can read:
///     ChosenModesComponent holds mode indices, IsSubtypeSpecification takes a build-time string,
///     and nothing anywhere can gate another player's activated abilities.
///   - NO COLOURS, so Diamond Knight's "spell of the chosen color" becomes any noncreature spell,
///     and Scuttlemutt loses its colour-changing half entirely.
///   - VIGILANCE is deliberately unimplemented, so Diamond Knight goes without and is costed for it.
///   - MANA PRODUCERS PRODUCE ON YOUR UPKEEP, never via a tap ability — Scuttlemutt here, and see
///     CoresetCubeColourlessArtifacts for the rocks. An unactivated mana ability is invisible: the
///     AI has to re-derive the activation every turn on every producer before it can cast anything.
/// </summary>
public static class CoresetCubeColourlessCreatures
{
	public static IReadOnlyList<Card> Cards { get; } =
		[
			// ===== ZERO =====

			// Printed: "{X}{X}. Enters with X +1/+1 counters. When it dies, create a 1/1 Thopter
			// with flying for each +1/+1 counter on it. {1},{T}: Put a +1/+1 counter on it."
			//
			// Faithful, and it is the cube's proof that counters survive into the graveyard:
			// CreateTokensPerPowerAction { UseCounters = true } reads the count off the dead card,
			// which works only because counters are stripped on ENTRY rather than on exit.
			CardFactory
				.Creature("Hangarback Walker", manaCost: 0, power: 0, toughness: 0)
				.WithTypes(CardType.Artifact)
				.WithSubtype("Artifact")
				.WithSubtype("Construct")
				.WithXCost(2)
				.WithEntersWithCounters(fromXValue: true)
				.WithActivatedAbility(
					"Assemble",
					manaCost: 1,
					effect: eb => eb.WithSelfCounters(1),
					requiresTap: true
				)
				.WithDeathTrigger(
					"Scatter Thopters",
					eb =>
						eb.WithAction(
							new CreateTokensPerPowerAction
							{
								CardTemplate = CoresetCubeColourlessTokens.Thopter(),
								UseCounters = true,
							},
							TargetingStrategy.NoTarget()
						)
				)
				.Build(),
			// ===== ONE =====

			// Printed: "{1},{T}: Put a +1/+1 counter on this creature." Faithful.
			CardFactory
				.Creature("Chronomaton", manaCost: 1, power: 1, toughness: 1)
				.WithTypes(CardType.Artifact)
				.WithSubtype("Artifact")
				.WithSubtype("Golem")
				.WithActivatedAbility(
					"Wind Up",
					manaCost: 1,
					effect: eb => eb.WithSelfCounters(1),
					requiresTap: true
				)
				.Build(),
			// ===== TWO =====

			// Printed: "As this enters, choose a nonland card name. Activated abilities of sources
			// with the chosen name can't be activated."
			//
			// RESKINNED. Naming a card needs runtime-choice state no specification can read, and
			// gating another player's activated abilities has no hook at all — ActivateAbilityAction
			// consults the ability, the card and the player, never a name registry. Rather than a
			// vanilla 2/1, it keeps a shape that plays like hate: it exhausts something on arrival.
			CardFactory
				.Creature("Phyrexian Revoker", manaCost: 2, power: 2, toughness: 1)
				.WithTypes(CardType.Artifact)
				.WithSubtype("Artifact")
				.WithSubtype("Horror")
				.WithEtbTrigger(
					"Revoke",
					eb => eb.WithExhaust().WithTarget(Random().OpponentCreatures())
				)
				.Build(),
			// ===== THREE =====

			// Printed: "As this enters, choose a creature type. This is that type. Other creatures
			// you control of the chosen type get +1/+1."
			//
			// RESKINNED to an untyped anthem, which is STRICTLY STRONGER than the printed card, so
			// the cost goes up by one to pay for it. The alternative — hardcoding one subtype —
			// would make it a lord for a tribe the cube may not support in any given draft, which
			// is a worse card AND a worse read.
			CardFactory
				.Creature("Adaptive Automaton", manaCost: 4, power: 2, toughness: 2)
				.WithTypes(CardType.Artifact)
				.WithSubtype("Artifact")
				.WithSubtype("Construct")
				.WithComponent(
					new StaticPTBoostAbility
					{
						PowerBonus = 1,
						ToughnessBonus = 1,
						Filter = TargetSpecification.OtherCreaturesYouControl(),
					}
				)
				.Build(),
			// Printed: "Vigilance. As this enters, choose a color. Whenever you cast a spell of the
			// chosen color, put a +1/+1 counter on this creature."
			//
			// Vigilance is cut (unimplemented) and the colour gate is cut (no colours), which would
			// leave a bare "whenever you cast a spell". It needs no narrowing clause, though:
			// SpellCastEvent is emitted only by CastSpellAction and CastFromGraveyardAction, so
			// OnYouCastSpell() already means "whenever you cast an instant or sorcery" —
			// CastCreatureAction and CastPermanentAction do not fire it. That is the same density
			// a single colour would roughly have given it, and it keeps this an artifact for a
			// spells deck rather than a free grower in every deck. Costed as the 1/1 it starts as.
			CardFactory
				.Creature("Diamond Knight", manaCost: 2, power: 1, toughness: 2)
				.WithTypes(CardType.Artifact)
				.WithSubtype("Artifact")
				.WithSubtype("Knight")
				// Vigilance -> Cover rather than Taunt: this is a grow-over-time card, so what it
				// needs is turns alive to accumulate counters, not to be attacked. Taunt would do
				// the exact opposite of what the card wants.
				.WithCover(1)
				.WithTriggeredAbility(
					"Attune",
					TriggerConditions.OnYouCastSpell(),
					eb => eb.WithSelfCounters(1)
				)
				.Build(),
			// Printed: "{T}: Add one mana of any color. {T}: Target creature becomes the color or
			// colors of your choice until end of turn."
			//
			// The second ability is unreachable — there are no colours — and the first becomes an
			// upkeep trigger like every other mana producer here. Note it produces from the turn
			// AFTER it lands, which is what summoning sickness would have done to the tap version.
			CardFactory
				.Creature("Scuttlemutt", manaCost: 3, power: 2, toughness: 2)
				.WithTypes(CardType.Artifact)
				.WithSubtype("Artifact")
				.WithSubtype("Scarecrow")
				.WithTriggeredAbility(
					"Brew",
					TriggerConditions.OnYourUpkeep(),
					eb => eb.WithAddMana(1).WithTarget(TargetingStrategy.Self())
				)
				.Build(),
			// Printed: "Flying. When this enters, draw a card." Faithful.
			CardFactory
				.Creature("Skyscanner", manaCost: 3, power: 1, toughness: 1)
				.WithTypes(CardType.Artifact)
				.WithSubtype("Artifact")
				.WithSubtype("Thopter")
				.WithFlying()
				.WithEtbTrigger("Scan", eb => eb.WithDraw(1).WithTarget(TargetingStrategy.Self()))
				.Build(),
			// ===== FOUR =====

			// Printed: "When this creature dies, you gain 3 life." Faithful.
			CardFactory
				.Creature("Guardian Automaton", manaCost: 4, power: 3, toughness: 3)
				.WithTypes(CardType.Artifact)
				.WithSubtype("Artifact")
				.WithSubtype("Construct")
				.WithDeathTrigger(
					"Failsafe",
					eb => eb.WithLifeGain(3).WithTarget(TargetingStrategy.Self())
				)
				.Build(),
			// Printed: "When this enters, you may search your library for a basic land card, put
			// that card onto the battlefield tapped, then shuffle. When this dies, you may draw a
			// card."
			//
			// The fetch is GainPermanentManaAction with Deferred = true: a land here is consumed
			// into MaxMana rather than existing as a permanent, and "tapped" is exactly the
			// difference between raising MaxMana alone and raising CurrentMana with it. "You may"
			// is mandatory, which only ever helps.
			CardFactory
				.Creature("Solemn Simulacrum", manaCost: 4, power: 2, toughness: 2)
				.WithTypes(CardType.Artifact)
				.WithSubtype("Artifact")
				.WithSubtype("Golem")
				.WithEtbTrigger(
					"Prospect",
					eb =>
						eb.WithAction(
							new GainPermanentManaAction { Amount = 1, Deferred = true },
							TargetingStrategy.Self()
						)
				)
				.WithDeathTrigger(
					"Mourn",
					eb => eb.WithDraw(1).WithTarget(TargetingStrategy.Self())
				)
				.Build(),
			// ===== FIVE =====

			// Printed: "When Golos enters, you may search your library for a land card, put that
			// card onto the battlefield tapped, then shuffle. {2}{W}{U}{B}{R}{G}: Exile the top
			// three cards of your library. You may play them this turn without paying their mana
			// costs."
			//
			// The five-colour activation cost becomes {7} — colourless mana is all there is, and
			// seven is what the printed cost totals. The impulse half is
			// ExileTopCardPlayableAction three times over; "without paying their mana costs" is cut
			// because ExiledPlayableComponent makes a card playable, not free, and a free triple
			// impulse off a five-drop is a different card entirely.
			CardFactory
				.Creature("Golos, Tireless Pilgrim", manaCost: 5, power: 3, toughness: 5)
				.WithTypes(CardType.Artifact)
				.WithSubtype("Artifact")
				.WithSubtype("Scout")
				.WithEtbTrigger(
					"Pilgrimage",
					eb =>
						eb.WithAction(
							new GainPermanentManaAction { Amount = 1, Deferred = true },
							TargetingStrategy.Self()
						)
				)
				.WithActivatedAbility(
					"Wanderlust",
					manaCost: 7,
					effect: eb =>
						eb.WithImpulseDraw()
							.WithImpulseDraw()
							.WithImpulseDraw()
							.WithTarget(TargetingStrategy.Self())
				)
				.Build(),
			// ===== SEVEN =====

			// Printed: "Flying. You can't lose the game and your opponents can't win the game."
			//
			// NARROWED from the printed card: this stops the LIFE loss only, and its controller
			// can still deck. A blanket "you can't lose" left an unanswered Angel with no way to
			// lose at all, so those games ran to the harness cutoff — 11.8% of games with it on
			// board were flagged draws against a 4.0% base rate. Decking is the one clock the
			// board cannot interact with, so keeping it live guarantees every game an ending.
			//
			// Otherwise unchanged: the check lives in CheckLossConditions, the one place a player
			// can lose, and it suppresses the OUTCOME rather than the cause — life still falls, so
			// killing the Angel collects the waiting loss on the very next state-based check.
			CardFactory
				.Creature("Platinum Angel", manaCost: 7, power: 4, toughness: 4)
				.WithTypes(CardType.Artifact)
				.WithSubtype("Artifact")
				.WithSubtype("Angel")
				.WithFlying()
				.WithComponent(new CannotLoseComponent())
				.Build(),
		];
}
