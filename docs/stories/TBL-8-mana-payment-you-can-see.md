---
id: TBL-8
title: Mana payment you can trust and see — a smarter auto-tap, and Arena's tap preview
status: done
area: TBL
created: 2026-11-08
---

## Why

Shayne's TRK sealed session (2026-11-08): the auto-tapper tapped all his black sources while a black card
was still in his hand. He thinks it was because one land had an ability, though it could have tapped for
colourless just as well. A payment he can't see in advance is one he can't trust; Arena shows which lands
a spell will tap while the card is dragged.

## Requirements

- [ ] Auto-pay leaves the sources of a colour the hand still needs, when another payment is as good
- [ ] While a card is dragged (and on hover of a clickable spell/ability), the lands it would tap are marked
- [ ] The preview is the payment that will be made — one function, never a second copy for display

## Decisions

- 2026-11-08, Shayne: implement Arena's drag preview. Not started; the misclick fix (TBL-2.8) goes first.

Open questions:
- (answered 2026-11-08: the land was Unexplored Territory.)

## Tasks

- [x] TBL-8.1 The all-Swamp tap (Shayne, 2026-11-08: Unexplored Territory — {T}: {C}, {1},{T},sacrifice: a
      basic — on the battlefield, {1}{B} in hand, and the auto-pay tapped the Swamps). Cause: `Solve` sorts
      sources by how many colours they make, and counted {C} as one, so Territory tied with a Swamp and
      battlefield order decided. {C} no longer counts: a colourless land pays first. `ManaTests`
      `AColourlessLand_PaysTheGeneric_BeforeABasicDoes` (red without the fix, checked). One golden replay
      moved (`EveryScriptedCard 29`, a different land tapped from step ~400) and was re-recorded. NOT done: keeping the colours the REST of the hand
      needs when two basics are equal (the `ponytail:` note on `ManaPayment`) — a Plains/Swamp split with
      {W} and {B} cards in hand still picks by order
- [x] TBL-8.2 The preview (Arena's): while a spell is dragged out of the hand, the lands it would tap glow amber
      (`Glow.Pays`), and stop when it is dropped. The table asks `CastSpellAction.PaymentPreview` — the one
      function validation and the cast itself now call (`FindPayment`), so a preview can't differ from what is
      paid. Polled in `MagicTableScene._Process` from Common's `CardUIManager.DraggingCard`. Captured through
      a real drag: Raffi ({2}{B}) over three Swamps, a fourth, and Unexplored Territory — the Territory and two
      Swamps glow, the other two don't. Only a cast, not an activated ability; floating mana isn't shown. Not
      pinned by a test (Godot input)

## Next

Done. Left over, by choice: the payment keeps no colour for the rest of the hand when two basics are equal (see TBL-8.1); a preview of an activated ability's cost.

## Log

- 2026-11-08: noted from Shayne's playtest.
