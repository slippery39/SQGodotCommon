using System;
using Common.Cards;
using Godot;

namespace KinGame;

/// <summary>
/// Makes a card face TAKE ON THE FIRST PRESS, on a phone as well as a mouse.
///
/// **`CardUI2D.Clicked` fires only while the card is the HOVERED card, and a finger has no hover.**
/// With a mouse that costs nothing — the pointer is already over the card when you press it. On a
/// touchscreen the first tap only registers the hover, so every click-a-card screen needed TWO
/// taps, and the second one worked even slightly off the card because the card was still marked as
/// hovered. Found by playing the Android build, not by a test.
///
/// The hand never had this bug because a card there is DRAGGED and a drag begins on the press. Only
/// the screens that CLICK a card were affected — the reward offer, and both of the shop's (buying,
/// and removing).
///
/// **It lives here rather than in each screen** because there are three call sites and there will
/// be more: every future screen that offers a card to click would otherwise ship two-tap on a
/// phone and nobody would notice on a desktop.
///
/// **Wired onto the INSTANCE, never into the scene.** `Common/Cards/2D` is MtgGame's too, and this
/// project must stay a clean no-op for it.
/// </summary>
public static class KinCardTap
{
	/// <summary>
	/// Calls <paramref name="take"/> on the first press of this card.
	///
	/// **ONE route, deliberately.** This first shipped wiring the area press AND `Clicked`, with a
	/// latch to stop the two firing twice for one mouse press — which is a guard invented to cover
	/// a second wire that did not need to exist. The area alone serves both inputs:
	/// `InputEventMouseButton` is the mouse and `InputEventScreenTouch` is the finger.
	///
	/// Measured on the scene rather than assumed, because the whole fix rests on it:
	/// `HoverArea.input_pickable` is **true** and its shape is **232x315**, which is the card. So
	/// the area's coverage IS the clickable region — the same region that lights the hover
	/// highlight, which is what tells the player what they are about to press.
	///
	/// `monitoring = false` on that area is not a problem: it governs detecting other areas and
	/// bodies, not input picking.
	///
	/// Throws if the area is missing, rather than silently wiring nothing and shipping a card that
	/// cannot be taken at all.
	/// </summary>
	public static void OnFirstPress(CardUI2D ui, Action take)
	{
		if (ui.FindChild("HoverArea", true, false) is not Area2D area)
			throw new InvalidOperationException(
				"CardUI2D has no HoverArea, so nothing would be clickable. The card scene changed "
					+ "under KinCardTap."
			);

		area.InputEvent += (_, e, _) =>
		{
			if (
				e
				is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }
					or InputEventScreenTouch { Pressed: true }
			)
				take();
		};
	}
}
