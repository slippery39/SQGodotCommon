using System.Collections.Immutable;
using ImmutableGameObjects;
using MtgCore.Cards.Builders;
using static MtgCore.Cards.Builders.TargetBuilder;

namespace MtgCore;

/// <summary>
/// The Core Set Cube's multicolour non-creatures — two sorceries and one planeswalker.
/// https://cubecobra.com/cube/list/magiccoreset20xx
///
/// A short file because the multicolour section is overwhelmingly creatures: 27 of its 30 nonland
/// cards. See CoresetCubeMulticolour's header for why the colour pairs are flavour rather than
/// mechanics here, and why these are costed as monocolour cards of the same mana value.
///
/// DIVERGENCES beyond the colour and blocking cuts listed in CoresetCubeBlack:
///   - LOYALTY COSTS ARE FIXED, so Garruk's "-8" ultimate keeps its cost but has to change what it
///     does; see below.
///   - NO "TAPPED AND ATTACKING" TOKENS, so Heroic Reinforcements grants haste instead — the same
///     substitution Skyknight Vanguard makes.
/// </summary>
public static class CoresetCubeMulticolourSpells
{
	public static IReadOnlyList<Card> Cards { get; } =
		[
			// ===== SORCERIES =====

			// Printed: "Create an X/X blue and red Weird creature token, where X is the number of
			// instant and sorcery cards in your graveyard. Then you may return an instant or
			// sorcery card from your graveyard to your hand. Exile Experimental Overload."
			//
			// THE TOKEN IS A LIVE X/X, NOT A SNAPSHOT. Nothing in the engine can create a token
			// with a runtime-computed P/T, so instead the Weird is a 0/0 carrying the same
			// GraveyardCountComponent Enigma Drake uses. That is not a workaround — it is closer to
			// what the card wants than a fixed token would be, because the Weird keeps growing as
			// more spells reach the yard.
			//
			// Note AffectsToughness is TRUE on the token and FALSE on the Drake: a 0/0 with a
			// power-only modifier dies to the zero-toughness rule the moment it arrives.
			//
			// "Exile this" is WithFlashback's exile-after-resolution in reverse and is dropped: the
			// self-exile exists in real Magic to stop the card recurring itself, and nothing here
			// returns a sorcery to hand except the card's own second half, which cannot target
			// itself while it is still resolving.
			CardFactory
				.Sorcery("Experimental Overload", manaCost: 4)
				.WithCreateTokens(CoresetCubeMulticolourTokens.Weird())
				.WithAutoReturnSpell()
				.Build(),
			// Printed: "Create two 1/1 white Soldier creature tokens. Until end of turn, creatures
			// you control get +1/+1 and gain haste."
			//
			// THE TOKENS CANNOT BE BUFFED BY THE SAME SPELL THAT MAKES THEM, and that is an engine
			// constraint rather than a card decision: ResolveEffectAction resolves EVERY effect's
			// targets up front, before any of them executes, so an AllValid list is fixed while the
			// tokens are still unmade. Buffing first and creating second changes nothing — the
			// tokens are absent from the list either way.
			//
			// So the two Soldiers arrive with haste BUILT IN and the team buff covers the rest of
			// the board. The only divergence from printed is that the tokens are 1/1 rather than
			// 2/2 for the turn; the "attack the turn they arrive" half, which is the reason the
			// card is a finisher, is intact.
			//
			// WithTarget also binds only the PENDING effect, so it has to follow each one. Chaining
			// both and putting a single WithTarget at the end left the +1/+1 on WithBoost's
			// default — a single chosen creature — while only the haste went team-wide.
			CardFactory
				.Sorcery("Heroic Reinforcements", manaCost: 4)
				.WithCreateTokens(CoresetCubeTokens.HastySoldier(), 2)
				.WithBoost(1, 1)
				.WithTarget(AllValid().AllYourCreatures())
				.WithGrantKeyword(haste: true)
				.WithTarget(AllValid().AllYourCreatures())
				.Build(),
			// ===== PLANESWALKER =====

			// Printed: "+1: Destroy another target planeswalker. +1: Create a 3/3 black Beast with
			// deathtouch. -3: Destroy target creature. You gain life equal to its toughness.
			// -8: Target opponent gets an emblem with 'Whenever a creature attacks you, it gets
			// +5/+5 and gains trample until end of turn.'"
			//
			// The first three are faithful. The -3's "life equal to its toughness" becomes a flat
			// 3 — reading the destroyed creature's toughness needs a count action that does not
			// exist, and 3 is the median toughness of a creature worth spending a -3 on.
			//
			// THE ULTIMATE IS INVERTED, and deliberately. Printed, it gives the OPPONENT an emblem
			// that buffs creatures attacking THEM — Garruk is punishing you for having attacked
			// him, and it only reads as an ultimate because it is your opponent's board it wrecks
			// when you eventually kill them. Emblems here are player-owned and always fire, and
			// there is no "creature attacks you" emblem shape that helps the caster, so this
			// becomes what an eight-loyalty ultimate should be: your creatures get the buff.
			CardFactory
				.Planeswalker("Garruk, Apex Predator", manaCost: 7)
				.WithLoyalty(5)
				.WithLoyaltyAbility(
					"+1: Destroy another target planeswalker",
					1,
					eb =>
						eb.WithAction(
							new DestroyPermanentAction(),
							Single().WithSpec(new IsPlaneswalkerSpecification())
						)
				)
				.WithLoyaltyAbility(
					"+1: Create a 3/3 Beast with deathtouch",
					1,
					eb => eb.WithCreateTokens(CoresetCubeMulticolourTokens.Beast())
				)
				.WithLoyaltyAbility(
					"-3: Destroy target creature. You gain 3 life",
					-3,
					eb =>
						eb.WithDestroy()
							.WithTarget(Single().OpponentCreatures())
							.WithAction(
								new GainLifeAction
								{
									Amount = 3,
									TargetContextKey = ContextKeys.CastingPlayerId,
								},
								TargetingStrategy.NoTarget()
							)
				)
				.WithLoyaltyAbility(
					"-8: Your creatures get +5/+5 and gain trample",
					-8,
					eb =>
						eb.WithBoost(5, 5)
							.WithTarget(AllValid().AllYourCreatures())
							.WithGrantKeyword(trample: true)
							.WithTarget(AllValid().AllYourCreatures())
				)
				.Build(),
		];
}
