using MtgCore.Cards.Builders;
using static MtgCore.Cards.Builders.TargetBuilder;

namespace MtgCore;

/// <summary>
/// **Package 1 — the untap/copy loop (Splinter Twin).** Four cards, two roles, and any copier plus
/// any untapper is an infinite combo.
///
/// The loop: exhaust a copier to make a token copy of an Illusionist → the token is a copy, so it
/// carries the same enters-the-battlefield trigger → that trigger readies the copier → activate
/// again. Every iteration adds a hasty body, so it converts to lethal rather than merely spinning.
///
/// **What is being tested:** the clearest possible two-card combo. The pieces are individually
/// weak — a 3-mana 1/1 and a 2-mana 1/4 — so nothing about their standalone rates suggests playing
/// them, and only the conjunction is worth anything. `DeckCore.For(copier)` should join "Illusionist
/// you control" into a core, and `LoopDetector`'s pair sweep should find the loop now that untap
/// exists. **If the builder cannot find this one, the problem is the builder, not the pool.**
///
/// ### Measured: cost was ONE barrier of two, and lowering it exposed the other
///
/// The copiers were originally 4 and 5 mana. At those costs mode 7 read `assem 20%` — the AI simply
/// had better plays and left them in hand. Dropping them to 3 and 4:
///
///     assem 20% -> 70%     the copier now gets cast
///     depth 1.0 -> 1.0     the LOOP still does not run
///     wins  8/10 -> 3/10   and the deck stopped winning
///
/// **Two things to read off that.** First, cost was real: castability was gating whether the payoff
/// ever hit the board. Second, it was not the whole story — `depth 1.0` means the ability is
/// activated at most once, because a running loop puts token copies onto the battlefield and those
/// tokens carry the untappers' names, so `EngineProbe` would count each one as another enabler
/// deployed and depth would climb with the iterations.
///
/// **The win-rate collapse is a CONFOUND, not a result.** Cheaper copiers lowered the deck's average
/// cost, so `DeckBuilder.LandsForConcept` cut the mana base from 19 lands to 16 — the deck did not
/// get worse at comboing, it got worse at casting things. Any further cost tuning here should check
/// the land count in the report before reading the win column.
///
/// **Why Illusionist and not "target creature you control":** the broad wording is answered by 400+
/// cards, the breadth gate discards it, and the archetype becomes invisible — a real combo the
/// builder is structurally unable to see. The narrow printed filter is what a core gets constructed
/// out of. See `ComboProving.Illusionist`.
///
/// **Two divergences from the printed cards, both deliberate and both making these STRONGER:**
///
/// - **No flash.** Deceiver Exarch and Pestermite are printed with flash so they are cast on the
///   opponent's turn. This engine has no priority window — the non-active player never acts — so
///   flash is unreachable, exactly as `CounterTrapComponent`'s notes record for counterspells. They
///   are plain sorcery-speed creatures here.
/// - **The tokens are permanent.** Real Kiki-Jiki and Splinter Twin sacrifice their copies at end
///   of turn, and nothing in this engine sweeps them — `EndTurnAction` has no such pass. See
///   `CreateTokenCopyAction.Count`.
///
/// The copiers are deliberately NOT Illusionists. `DeckCore.For` strips a payoff from its own
/// support slot, so a copier that was also its own combo target would blur the payoff/support split
/// the core is supposed to express.
/// </summary>
public static class ComboProvingTwin
{
	/// <summary>
	/// The untapper half. "Ready target EXHAUSTED creature you control" rather than any creature,
	/// which is precision rather than flavour: a trigger cannot ask a player to choose, so a broad
	/// target would be filled by `Random()` and would usually ready the wrong permanent — most often
	/// the token that just entered, which does nothing. The only exhausted creature during the loop
	/// is the copier that just paid its own tap cost, so the filter names it exactly.
	/// </summary>
	private static CreatureCardBuilder Untapper(string name, int power, int toughness) =>
		CardFactory
			.Creature(name, manaCost: 2, power: power, toughness: toughness)
			.WithSubtype(ComboProving.Illusionist)
			.WithEtbTrigger(
				"Ready",
				eb =>
					eb.WithAction(
						new UnexhaustCreatureAction(),
						// Random(), never Single(): a trigger has no cast-time targeting step, so a
						// UserSelect strategy resolves to an empty list and the ability silently
						// does nothing. The filter narrows it to one legal choice anyway.
						Random()
							.WithSpec(
								new IsExhaustedSpecification().And(
									new IsControlledByYouSpecification()
								)
							)
					)
			);

	/// <summary>
	/// The copier half. `maxPerTurn: 0` is unlimited and is what OPENS the loop —
	/// `ActivatedAbilityComponent.MaxActivationsPerTurn` defaults to 1, which is the engine's own
	/// guard against exactly this, and `LoopDetectorTests.TheEnginesOwnActivationCap_ClosesTheLoop`
	/// pins that the default is not a loop. Setting 0 here is the deliberate opposite, and it is the
	/// first real loop this engine has ever been able to express.
	/// </summary>
	private static CreatureCardBuilder Copier(
		string name,
		int manaCost,
		int power,
		int toughness
	) =>
		CardFactory
			.Creature(name, manaCost: manaCost, power: power, toughness: toughness)
			// **Haste, exactly as Kiki-Jiki is printed, and it is load-bearing rather than flavour.**
			// The ability costs a tap, and a summoning-sick creature cannot pay a tap cost — so
			// without haste the copier does nothing the turn it lands and the combo needs its
			// controller to untap with a 4-drop still alive. That is a different, far worse card,
			// and the difference is invisible from the card definition.
			.WithHaste()
			.WithActivatedAbility(
				"Twin",
				manaCost: 0,
				effect: eb =>
					eb.WithAction(
						new CreateTokenCopyAction(),
						// An ACTIVATED ability does get a targeting step — MtgActionGenerator fills
						// it — so Single() is correct here where it would be inert on a trigger.
						Single()
							.WithSpec(
								new IsSubtypeSpecification
								{
									Subtype = ComboProving.Illusionist,
								}.And(new IsControlledByYouSpecification())
							)
					),
				requiresTap: true,
				maxPerTurn: 0
			);

	public static IReadOnlyList<Card> Cards { get; } =
		[
			// 1/4 — statted to survive, since the combo needs it to still be there next turn.
			Untapper("Mirevale Deceiver", 1, 4).Build(),
			// The aggressive half: a 2/1 flyer is a real card on its own, which is the control for
			// "did the builder pick this because of the combo, or because it is simply playable?"
			Untapper("Tidebinder Sprite", 2, 1).WithFlying().Build(),
			Copier("Twinflame Artisan", manaCost: 3, power: 1, toughness: 1).Build(),
			Copier("Kilnmother Vess", manaCost: 4, power: 2, toughness: 2).Build(),
		];
}
