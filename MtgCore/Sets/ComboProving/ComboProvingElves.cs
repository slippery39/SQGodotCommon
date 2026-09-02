using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore.Cards.Builders;
using static MtgCore.Cards.Builders.TargetBuilder;

namespace MtgCore;

/// <summary>
/// **Package 2 — a mana engine that is not rituals.** Five Elves, no single one of which wins, and
/// no infinite loop anywhere in the package.
///
/// **What is being tested is harder than package 1 and deliberately so.** Storm was already
/// discoverable because `ProbeManaProfit` measures net mana, so a ritual reads as supply for a
/// spells-cast demand. An Elf engine is mana-positive only in AGGREGATE — one Elf taps for one
/// mana, which is break-even at best; the engine is the SECOND, THIRD and FOURTH Elf making the
/// first one better. There is no single card whose profile says "ritual". The open question is
/// whether the builder can assemble a critical-mass engine when no individual piece looks like one.
///
/// **Prediction, recorded before the run** (per the handoff's rule that a run should be read against
/// a prediction rather than interpreted afterwards): the tribe itself will be found — an Elf demand
/// is one narrow filter with a big supplier set, which is the shape mutation already stumbles into.
/// What is genuinely uncertain is whether the PAYOFFS end up in the deck, because two of them are
/// invisible to the harvester by construction (see below).
///
/// ### Measured: all four Elf payoffs are discoverable, including the count-based ones
///
/// **A prediction written here first was wrong, and the correction is worth keeping.** The guess was
/// that "add mana equal to the number of Elves you control" declares nothing readable, because
/// `CountCardsWithSubtypeAction { Subtype = "Elf" }` is a plain string rather than a
/// `TargetSpecification` on a `Filter` property. That is only half the harvest rule: `PoolFeatures`
/// documents at the top of the file that **a `string` property named `Subtype` always means "cards
/// of this subtype qualify"**, and lifts it into an `IsSubtypeSpecification` — which is how
/// Dragonstorm's `SelectCardFromLibraryAction{Subtype = Dragon}` has always been found.
///
/// Measured on DES, every payoff builds a core:
///
///     Wirewood Conduit    IsSubtypeSpecification{Elf}            support 17, payoff 6
///     Timberwatch Elder   IsSubtypeSpecification{Elf}            support 17, payoff 6
///     Wirewood Symbiont   And{Elf, ControlledByYou}              support 23
///     Wirewood Herald     EventTriggerCondition{...Filter=Elf}   support 23
///     Hoofthunder Colossus  no demand, no core
///
/// The Colossus having none is CORRECT — it counts creatures generally and names no filter, so it
/// is a payoff for the board rather than for the tribe.
///
/// **The package does split across two demand objects**, and that is the known near-duplicate-
/// concept problem `MtgSimulator/CLAUDE.md` already records for LIFT-ranked selection: bare
/// `Elf` and `Elf ∧ ControlledByYou` mean nearly the same thing and dedupe separately. It is not
/// worth collapsing by dropping the control clause — readying an OPPONENT's Elf is not what the
/// card should do — so the Elf archetype is reachable as two cores rather than one.
///
/// ### Cover 10 is doing real work here
///
/// Every card in this package is a 1/1 utility creature, and in a blockerless game those die to
/// anything. `CoverTurns` is the defensive counterpart to Taunt and exists precisely so a small
/// body with a real ability can survive to use it — without it the engine never gets a second turn
/// and the package would measure removal rather than assembly. It burns down on its controller's
/// turn and is spent immediately if the creature attacks, so it buys setup, not immunity.
///
/// ### No loop, deliberately
///
/// The untapper readies a target Elf, and the mana Elf taps for mana — so four untappers give at
/// most four extra activations. Nothing readies the untappers themselves. Package 1 is where the
/// unbounded loop lives; this one is a critical-mass engine, which is a different question and
/// should stay a different question. `LoopDetectorTests` sweeps cover it either way.
/// </summary>
public static class ComboProvingElves
{
	public const string Elf = "Elf";

	/// <summary>Every Elf here is a fragile 1/1 that has to survive a turn to do anything.</summary>
	private static CreatureCardBuilder ElfBody(string name, int manaCost) =>
		CardFactory
			.Creature(name, manaCost: manaCost, power: 1, toughness: 1)
			.WithSubtype(Elf)
			.WithCover(10);

	public static IReadOnlyList<Card> Cards { get; } =
		[
			// ===== The mana engine itself =====
			// "Exhaust: add mana equal to the number of Elves you control." Invisible to the
			// harvester as a payoff (the Elf count is a string), a strong supplier of the tribe.
			ElfBody("Wirewood Conduit", manaCost: 2)
				.WithActivatedAbility(
					"Channel",
					manaCost: 0,
					effect: eb =>
						eb.WithAction(
							new PipelineAction
							{
								Steps = ImmutableList.Create<GameAction>(
									new CountCardsWithSubtypeAction
									{
										Subtype = Elf,
										CreaturesOnly = true,
										PlayerIdContextKey = ContextKeys.CastingPlayerId,
										OutputKey = "elf_count",
									},
									new AddTemporaryManaAction
									{
										AmountContextKey = "elf_count",
										TargetContextKey = ContextKeys.CastingPlayerId,
									}
								),
							},
							TargetingStrategy.NoTarget()
						),
					requiresTap: true,
					// **Unlimited per turn, because the TAP is the limit and readying is the
					// resource.** At the default of 1 the untapper would be a blank: readying a
					// creature that has already spent its one activation buys nothing, and the
					// whole package would quietly stop being an engine.
					//
					// This does NOT open a loop, and the reason is conservation rather than a cap:
					// every activation exhausts its own source and readies at most one creature, so
					// total available activations never increases. Readiness can be moved around the
					// board; it cannot be created. `LoopDetectorTests` sweeps confirm it.
					maxPerTurn: 0
				)
				.Build(),
			// The untapper. "Target ELF you control" rather than any creature: it is what makes the
			// archetype visible to `DeckCore.For` at all (see the class comment), and it keeps the
			// card honest — this is an Elf payoff, not a generic combo enabler.
			ElfBody("Wirewood Symbiont", manaCost: 1)
				.WithActivatedAbility(
					"Rouse",
					manaCost: 0,
					effect: eb =>
						eb.WithAction(
							new UnexhaustCreatureAction(),
							Single()
								.WithSpec(
									new IsSubtypeSpecification { Subtype = Elf }.And(
										new IsControlledByYouSpecification()
									)
								)
						),
					requiresTap: true,
					// **Unlimited per turn, because the TAP is the limit and readying is the
					// resource.** At the default of 1 the untapper would be a blank: readying a
					// creature that has already spent its one activation buys nothing, and the
					// whole package would quietly stop being an engine.
					//
					// This does NOT open a loop, and the reason is conservation rather than a cap:
					// every activation exhausts its own source and readies at most one creature, so
					// total available activations never increases. Readiness can be moved around the
					// board; it cannot be created. `LoopDetectorTests` sweeps confirm it.
					maxPerTurn: 0
				)
				.Build(),
			// The card engine. Its trigger Filter IS a TargetSpecification on a demand property, so
			// this one is harvested cleanly — the most discoverable card in the package.
			ElfBody("Wirewood Herald", manaCost: 1)
				.WithTriggeredAbility(
					"Elvish Insight",
					new EventTriggerCondition
					{
						EventTypeName = EventTypeNames.CreatureEnteredBattlefield,
						Filter = new IsSubtypeSpecification { Subtype = Elf }.And(
							new IsControlledByYouSpecification()
						),
					},
					eb => eb.WithDraw(1)
				)
				.Build(),
			// Timberwatch: "Exhaust: target Elf you control gets +X/+X, where X is the number of
			// Elves you control." Narrowed from the printed "target creature" so the buff stays
			// inside the tribe and the targeting filter names Elf.
			ElfBody("Timberwatch Elder", manaCost: 2)
				.WithActivatedAbility(
					"Rally",
					manaCost: 0,
					effect: eb =>
						eb.WithAction(
							new PipelineAction
							{
								Steps = ImmutableList.Create<GameAction>(
									new CountCardsWithSubtypeAction
									{
										Subtype = Elf,
										CreaturesOnly = true,
										PlayerIdContextKey = ContextKeys.CastingPlayerId,
										OutputKey = "rally_count",
										CountedIdsOutputKey = "rally_targets",
									},
									new AddModifierAction
									{
										PowerBonusContextKey = "rally_count",
										ToughnessBonusContextKey = "rally_count",
										Duration = ModifierDuration.UntilEndOfTurn,
										TargetContextKey = "rally_targets",
									}
								),
							},
							TargetingStrategy.NoTarget()
						),
					requiresTap: true,
					// **Unlimited per turn, because the TAP is the limit and readying is the
					// resource.** At the default of 1 the untapper would be a blank: readying a
					// creature that has already spent its one activation buys nothing, and the
					// whole package would quietly stop being an engine.
					//
					// This does NOT open a loop, and the reason is conservation rather than a cap:
					// every activation exhausts its own source and readies at most one creature, so
					// total available activations never increases. Readiness can be moved around the
					// board; it cannot be created. `LoopDetectorTests` sweeps confirm it.
					maxPerTurn: 0
				)
				.Build(),
			// ===== The payoff that converts the board into a win =====
			// Craterhoof. Ready every creature you control, they gain haste and trample, and each
			// gets +X/+X where X is how many you control.
			//
			// **The mass-buff shape is Overwhelming Stampede's, and it has to be**: mass targeting
			// cannot reach inside a `PipelineAction`, so the number and the target list must both
			// come out of one scan. `CountCardsWithSubtypeAction.CountedIdsOutputKey` was added for
			// exactly this — counting is what an Elf board needs, where greatest-power (Stampede's
			// number) reads +1/+1 off a field of 1/1s and does nothing.
			//
			// Readying matters here in a way it would not in real Magic: the Elves have been tapped
			// for mana to cast this, so without it the team that pays for the card cannot attack.
			CardFactory
				.Creature("Hoofthunder Colossus", manaCost: 7, power: 5, toughness: 5)
				.WithSubtype("Beast")
				.WithTrample()
				.WithEtbTrigger(
					"Overrun",
					eb =>
						eb.WithAction(
							new PipelineAction
							{
								Steps = ImmutableList.Create<GameAction>(
									new CountCardsWithSubtypeAction
									{
										CreaturesOnly = true,
										PlayerIdContextKey = ContextKeys.CastingPlayerId,
										OutputKey = "hoof_count",
										CountedIdsOutputKey = "hoof_targets",
									},
									new UnexhaustCreatureAction
									{
										TargetContextKey = "hoof_targets",
									},
									new AddModifierAction
									{
										PowerBonusContextKey = "hoof_count",
										ToughnessBonusContextKey = "hoof_count",
										Duration = ModifierDuration.UntilEndOfTurn,
										TargetContextKey = "hoof_targets",
									},
									new GrantKeywordAction
									{
										GrantsHaste = true,
										GrantsTrample = true,
										Duration = ModifierDuration.UntilEndOfTurn,
										TargetContextKey = "hoof_targets",
									}
								),
							},
							TargetingStrategy.NoTarget()
						)
				)
				.Build(),
		];
}
