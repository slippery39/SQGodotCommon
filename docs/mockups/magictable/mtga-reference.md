# MagicTable — the MTGA reference (TBL-4.4)

For Shayne to mark: each row **copy**, **skip** or **later**, with a note if it should work differently. The marked
rows become TBL-4's visual requirements (the `/match-mockup` skill's step A.1) and TBL-4.5's animation list. Gathered
2026-11-12 from the guides below; rows marked *(from memory)* are Claude's recollection of Arena, to confirm.

**Best evidence would be your own:** 4–6 screenshots from your Arena games (a busy board, a spell on the stack with a
target, combat with blocks, the opponent's turn) saved here as `arena-<what>.png`. The public guides are mostly from
2018–2020 and show layout, not motion.

Sources: [Hipsters of the Coast — the in-match screen](https://www.hipstersofthecoast.com/2018/03/how-to-play-mtg-arena-the-in-match-screen/)
(annotated screenshots of each part), [MTGAZone — playing a match](https://mtgazone.com/playing-a-match/),
[ASAP guide to MTGA — playing a match](https://sites.google.com/view/asap-bg-to-mtga/playing-a-match),
[MTGGoldfish — a first look](https://www.mtggoldfish.com/articles/magic-the-gathering-arena-a-first-look).

## Layout

| # | MTGA | Mark |
|---|---|---|
| L1 | You at the bottom, the opponent at the top, mirrored | |
| L2 | Your lands in the row nearest your hand; creatures nearer the centre line | |
| L3 | Artifacts and enchantments to the right of your lands; planeswalkers at the far right, above the pass button | |
| L4 | Your hand fanned across the bottom, half off the screen; hover lifts a card fully into view | |
| L5 | Avatar with the life total in the middle of each side, the phase strip beside it | |
| L6 | Library, graveyard and exile beside the hand, at the left; opponent's mirrored at the top | |
| L7 | The opponent's hand as card backs at the top (known cards face up — TBL-6 already) | |
| L8 | Keywords as icons on the lower frame of a card in play; "…" when there are more | |
| L9 | Damage marked shown as lowered toughness (MTGAZone notes this misleads: damage doesn't lower toughness) | |
| L10 | A changed cost shown on the card in hand with a sparkle | |

## Turn, priority and the main button

| # | MTGA | Mark |
|---|---|---|
| T1 | The phase ladder by each portrait; click a step to set a stop | |
| T2 | One main button bottom right whose label says what pressing it does: *Next*, *To Combat*, *Attack (N)*, *No Blocks*, *Resolve*, *End Turn* *(from memory)* | |
| T3 | Whose turn it is shown by the active player's side lighting up *(from memory)* | |
| T4 | Full control as a toggle (stop at every priority) | |
| T5 | A timer: the hourglass, extensions as a number beside it | |

## The stack

| # | MTGA | Mark |
|---|---|---|
| S1 | The stack at the right edge, its top item largest; hover an item to see its targets, right-click for the full card | |
| S2 | A large stack opens into a full view in the middle of the screen | |
| S3 | The stack can be hidden with a small arrow when it covers the planeswalker area | |
| S4 | A spell that can't be countered has a red aura | |

## Targeting, combat and payment

| # | MTGA | Mark |
|---|---|---|
| C1 | An arrow from a spell or ability to its target while choosing, and while it's on the stack *(from memory)* | |
| C2 | Attackers slide forward when declared; a line from each blocker to what it blocks *(from memory)* | |
| C3 | Hover the defending player to see which attackers are unblocked | |
| C4 | Damage order / several blockers: click the number on each to set the order | |
| C5 | Dragging a spell over the battlefield highlights the lands the auto-tap will use (TBL-8 already) | |
| C6 | The mana pool above your portrait when mana floats; click a symbol to spend it | |

## Motion — what makes it clear something happened (TBL-4.5)

MTGA uses "visual cues, sound effects and intention lines" to show why things happen; MTGO shows less motion but
keeps a text log. Shayne's bar: MTGO's level is enough.

| # | Animation | Mark |
|---|---|---|
| A1 | A cast: the card travels from the hand (or their hand's back) to the stack | |
| A2 | Resolving: the card travels from the stack to its place on the battlefield, or to the graveyard | |
| A3 | Entering: a short settle or flash where it lands | |
| A4 | Tapping: the card turns sideways over a beat, not a snap | |
| A5 | Combat damage: a number floats off each creature and player hit; a lethal one dims, then goes to the graveyard | |
| A6 | Life change: the life total counts down or up, coloured | |
| A7 | A trigger: the source card pulses as its ability goes on the stack | |
| A8 | Draw: the card slides from the library into the hand | |
| A9 | Each animation a short beat (~0.3–0.5 s), queued so two never overlap; faster when you're not involved | |

## MTGO's strengths worth keeping (TBL-4's "best of MTGO")

| # | MTGO | Mark |
|---|---|---|
| O1 | A readable game log, one line per event, always available | |
| O2 | The stack as a list with each item's full text | |
| O3 | Stops on any step, set ahead, for both players' turns | |
| O4 | Everything explicit: no hidden auto-yields | |

## Our table today, for comparison

The current layout and its rules are in `.claude/rules/magic-table.md`. A side-by-side capture of today's table goes
here when TBL-4.1's interview runs (a capture needs a quiet machine — the cube lane is building tonight).
