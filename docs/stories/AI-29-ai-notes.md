---
id: AI-29
title: AI notes — specific evaluators for troublesome effects, called by the general one
status: backlog
area: AI
created: 2026-11-18
---

## Why

Shayne, 2026-11-18 (during AI-27): "making a completely generic AI that plays everything correctly without any
additional input from us would be a dream, but obviously the amount of complexity and edge cases in magic makes this
quite complicated. It seems to be quite hard to find a single evaluator system that can work for every single card in
the game. I'm wondering if maybe, even though we have stated the opposite up until this point, it would be worth it to
add 'AI Notes' for cards or abilities that are troublesome … And i'm saying notes, but in reality these could just be
like evaluator functions used for specific effects or cards that we write … Have a general evaluator, and then specific
eveluators that can be called by the general evaluator … So we would basically be doing our general eveluator, + a forge
style how to play certain cards but not necessarily on every single card."

His examples, in his words (2026-11-18):
- **Flash:** "prefer playing at end of opponents turn OR if extra value would be gained by playing it before then (like
  being able to block and kill a high value creature, or being able to block a creature for free, or in the case of
  dvine duelist being able to tap a creature to prevent some damage etc..)"
- **Looting:** "we could provide a procedural evaluator for determining which cards to discard (i.e. excess lands,
  spells we can't currently cast, low value spells etc..) instead of the AI lookahead trying to determine that all by
  itself."
- **Bounce (Unsummon):** "evaluate the baseline of the card, and say only use this in non desperation, if you will get
  more than the baseline value of the card, or it will help you win the game quickly etc.."
- **Yoshimaru, Scrappy Stray** ("When Yoshimaru enters, another target creature you control fights up to one target
  creature an opponent controls"): edge it toward casting with a creature that fights something.
"Now it sounds easy because Im saying this all in english, so the implementation is something we would need to figure
out."

A candidate from the same session (AI-9.10, 2026-11-18): land sequencing - Konstrari Annex ("enters tapped unless you
control a planeswalker") played while a Jace token makes it enter untapped, before Rockfall Vale ("unless you control
two or more other lands"), whose condition holds from then on anyway. The general evaluator can't see it: the look-ahead
plays the Annex tapped inside its own turns, where its one spell a turn fits the mana left.
Shayne (2026-11-18): "do we need to look ahead for this? Is there no way to determine which lands would come into play
untapped in the current situation vs next turn and sequence the lands that way? Like the lookahead seems to be causing
too much issues, even though in theory it should help."

What pointed here (AI-27): seed 2 #16 — Divining Duelist cast in our main phase to loot away our next land drop, where
Mindseeker Oculus now and Duelist held for their turn was the play; the look-ahead never plays our cards in their turn.

Claude's proposed shape (2026-11-18, for him to correct): notes attach to an EFFECT or mechanic (flash, "enters,
fight", loot, bounce), not a card name — one note covers every card using that piece (MC-6's compose rule); a card-name
note only for a truly unique card. Three kinds: when to play it, how to answer its choice, what holding it is worth.
They live in the AI code, never on the cards (the rules engine stays AI-free, game state serializable). A note tilts a
price rather than forbidding a move, so the search can still override it. Each is measured like any term: a position
from his table notes as its test, an off switch, the suite and a match. The rules already scattered through the AI are
notes in all but name (burn waits for a kill, never pass with a land drop, surveil lands binned by rule, Nyota's number,
Kai Winn's name, tap abilities at their last chance, waiting wins a tie) and can move into the same home.

## Requirements

Not agreed — to shape with Shayne.

## Decisions

- 2026-11-18, Shayne: noted, not started — "I want to finish what we have in this session first."

## Tasks

- [ ] AI-29.1 The shape agreed: where a note lives, how the general evaluator calls it, its three kinds
- [ ] AI-29.2 The pilot: flash (his rule above), on seed 2 #16

## Next

Not started. Read the Why (his rules, verbatim) and AI-27's Log for seed 2 #16. Start with AI-29.1 — a discussion.

## Log

- 2026-11-18: created from Shayne's question during AI-27.
