using System.Collections.Immutable;
using ImmutableGameObjects;

namespace MtgCore;

/// <summary>
/// "Until end of turn, this permanent becomes a 4/4 Spirit artifact creature" — Haunted Plate Mail,
/// and the animation primitive generally (Ensoul Artifact, manlands, the Gods).
///
/// THE ONLY WAY A CreatureComponent REACHES A PERMANENT ALREADY IN PLAY. Before this, the component
/// was created in exactly two places — CreatureCardBuilder.Build and PutIntoBattlefieldAction — and
/// AddModifierAction, AddCustomModifierAction and AddCountersAction all silently `continue` on a
/// non-creature. BecomesBaseCreatureComponent does not help: it is a PowerToughnessModifier that
/// reshapes an EXISTING creature by cancelling its printed stats, and on a card with no body its
/// bonus is computed against zero and read by nobody.
///
/// THREE THINGS THIS HAS TO GET RIGHT, and each is silent when wrong:
///
/// 1. SUMMONING SICKNESS IS NOT APPLIED. Real MTG lets an animated permanent attack if it has been
///    under your control since the turn began, which is the normal case by a wide margin. Erring
///    the other way would make every animation a do-nothing on the turn you paid for it. Cards that
///    need a limiter carry their own — Haunted Plate Mail's is "activate only if you control no
///    creatures".
///
/// 2. STATIC ABILITIES ARE RE-APPLIED BY HAND. StaticAbilityEngine is a push model driven by
///    CreatureEnteredBattlefieldEvent, so without this an anthem simply misses the new creature.
///    Staging that event instead would be worse than the bug: every ETB payoff on the board —
///    Corpse Knight, Soul Warden — would fire for a permanent that did not enter anything. So
///    ProcessPermanentEntered is called DIRECTLY and no event is staged.
///
/// 3. IT MUST BE UNDONE, and not here. See EndTurnAction.RevertAnimatedPermanents: a
///    CreatureComponent is not a PowerToughnessModifier, so ClearEndOfTurnModifiers will not
///    remove it, and StartTurnAction's per-component loop runs for the ACTIVE player only, which
///    would leave the animation alive through the opponent's whole turn.
/// </summary>
public record AnimateAction : EffectAction
{
	public int Power { get; init; } = 1;
	public int Toughness { get; init; } = 1;

	/// <summary>Creature subtype gained while animated, e.g. "Spirit". Removed on revert.</summary>
	public string Subtype { get; init; } = "";

	public bool HasFlying { get; init; }
	public bool HasHaste { get; init; }

	// ponytail: no DetachEquipment flag. Haunted Plate Mail's "that's no longer an Equipment" is
	// unreachable — it can only animate while you control no creatures, so it cannot be attached to
	// anything at the time. Add one when a card can animate an attached Equipment.

	public override ActionResult Execute(GameState gameState)
	{
		var state = gameState;
		var game = state.TryGetGame();
		if (game == null)
			return new ActionResult(gameState);

		foreach (var targetId in ResolveTargetIds())
		{
			if (!state.HasObject(targetId) || state.GetObject(targetId) is not Card card)
				continue;

			// Already a creature — animating twice would stack a second body and, worse,
			// double-stamp every anthem, because StampEffect unconditionally Adds.
			if (card.HasComponent<CreatureComponent>())
				continue;

			if (!card.HasComponent<PermanentComponent>())
				continue;

			var battlefieldId = state.GetPlayerZoneId(card.ControllerId, ZoneType.Battlefield);
			if (state.GetParent(targetId) != battlefieldId)
				continue;

			var subtypes = string.IsNullOrEmpty(Subtype)
				? card.Subtypes
				: card.Subtypes.Add(Subtype);

			state = state.UpdateObject(
				targetId,
				card with
				{
					Types = card.EffectiveTypes | CardType.Creature,
					Subtypes = subtypes,
					Components = card
						.Components.Add(
							new CreatureComponent
							{
								Power = Power,
								Toughness = Toughness,
								HasSummoningSickness = false,
								HasFlying = HasFlying,
								HasHaste = HasHaste,
							}
						)
						.Add(new AnimatedUntilEndOfTurnComponent { AddedSubtype = Subtype }),
				}
			);

			state = StaticAbilityEngine.ProcessPermanentEntered(state, targetId, game.Id);
		}

		return new ActionResult(state);
	}
}

/// <summary>
/// Marks a permanent whose creature body was granted by AnimateAction and expires at end of turn.
///
/// AddedSubtype records what to take back off, so reverting cannot strip a subtype the card was
/// printed with — animating an artifact Equipment into a Spirit and back must leave "Artifact" and
/// "Equipment" exactly where they were.
/// </summary>
public record AnimatedUntilEndOfTurnComponent : GameComponent
{
	public string AddedSubtype { get; init; } = "";
}
