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
	/// Calls <paramref name="take"/> at most once, on the first press of this card.
	///
	/// Both routes are wired — the hover area's own input, which is what works under a finger, and
	/// `Clicked`, which is what a mouse has always used. On a desktop both can fire for a single
	/// press, so the latch is not defensive: without it a reward would be taken twice, putting two
	/// cards in the deck and advancing the floor twice.
	/// </summary>
	public static void OnFirstPress(CardUI2D ui, Action take)
	{
		var claimed = false;

		void Once()
		{
			if (claimed)
				return;

			claimed = true;
			take();
		}

		if (ui.FindChild("HoverArea", true, false) is Area2D area)
		{
			area.InputEvent += (_, e, _) =>
			{
				if (
					e
					is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }
						or InputEventScreenTouch { Pressed: true }
				)
					Once();
			};
		}

		ui.Clicked += _ => Once();
	}
}
