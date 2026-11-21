---
id: AI-34
title: Playing around the opponent's cards — removal, tricks and a look-ahead that bets on one future
status: backlog
area: AI
created: 2026-11-20
---

## Why

The search plays the opponent as one fixed policy, so a choice can rest on one imagined reply, and nothing of theirs
that is unseen (a removal spell, a trick) changes what we commit. Shayne (2026-11-20): "we need to get the AI making the
basic decisions well before we even think of implementing a way to play around tricks" — so this waits, and gathers
the positions that need it.

## Requirements

- [ ] Not shaped yet: the cases below first, then what "playing around" means here.

## Decisions

- 2026-11-20, Shayne: not now; basic decisions first (AI-33). Positions that need it are noted here and left.

## Cases

1. **Seed 3 #16 of the position suite** (White-Blue Jace vs White-Black, turn 6, 3 lands, nothing on our board; they have
   a tapped Rank Rat 1/1 and an untapped Sanctum Lurker 3/2 that keeps their Jace token at 0 loyalty and grants it
   "[+2]: deals 1 damage to each opponent and you gain 1 life"). Since AI-33.4's two-attack clock the search casts
   Repurposed Enforcer (3/2 for 2) over Mindseeker Oculus (2/1, "empower Jace 4", then Jace's −3 draws). **Shayne:
   the Oculus** — "you immediatley get to draw a acard, get a blocker for their 3/2, and if they have a removal spell you
   would rahter your 2/1 die than your 3/2". Why it didn't: its Enforcer future had the simulated opponent trade the
   Lurker for the Enforcer on turn 8 (taking their Jace's +2 with it), 3.54 against the Oculus's 1.82; under the old clock
   that opponent didn't block and the Oculus won (1.07 against 0.31). One guessed block decided it, and the removal
   that would rather hit the 2/1 isn't modelled. Shayne: "not too worried about this play" - noted, not fixed.
2. **The Sarcophagus** (AI-19, 2026-11-11): "When swinging for the win, you have to account for, can I win through them
   removing one of my creatures?"

## Tasks

(none yet)

## Next

Nothing until AI-33's attack decisions are sound. Add cases as they come.

## Log

- 2026-11-20: opened from AI-33.4's suite read - case 1, with AI-19's Sarcophagus as case 2.
