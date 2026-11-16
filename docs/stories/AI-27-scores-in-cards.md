---
id: AI-27
title: Evaluator scores in cards — 1 point is a card, creature bodies priced on that scale
status: backlog
area: AI
created: 2026-11-16
---

## Why

Shayne, 2026-11-16, while pricing a vanilla 1/1 as a removal target (AI-26): "if we price power / toughness at about
0.4 of a card, i.e. a 1/1 is worth 0.4, a 2/2 is woth 0.8, Then we also have to discount cards that are high power
but low toughness (i.e. a 7/2 and a 5/2 are not that much different (since they both die to 2/2s and the same damage
based removal) But how do our evaluator scores currently work? I think we just set arbitrary numbers before right?
Would it make sense to make it more "normalized" so that a value of 1 = 1 card? That way it would be easier to
evaluate states from a human side (i.e. if I am +1 over my opponent, i am basically up a card from him). But that
also seems like a different story altogether, semi related to this specific scenario, but it still doesn't solve
options pricing."

**Today (read from `Evaluator.cs`, 2026-11-16).** Hand-chosen points, adjusted on positions and matches, consistent
with each other but anchored to nothing. An average measured card is 8.5 points (`OptionReport.ACard`: FRA 8.97,
TRK 8.17). A creature is 2 + 1.5 × power + toughness + 0.8 a keyword (`Evaluator.Body`): a vanilla 1/1 4.5 (0.53 of
a card), a 2/2 7.0 (0.82), a 3/3 9.5 (1.12), a 5/2 11.5 and a 7/2 14.5 (1.35 and 1.71). A land in play 1.5 (0.18), a
point of life 1.0 to 20 (0.12), a card in hand half its measured value.

Against his scale: the middle already agrees (a 2/2 is 0.82 for his 0.8); the flat 2 makes a 1/1 too dear (0.53 for
0.4) and big bodies too cheap; and power counts MORE than toughness, the opposite of his 7/2-against-5/2 point.

## Requirements

Not agreed — to shape with Shayne.

- [ ] A score reads in cards wherever a person reads it (the option report, the inspector, `--explain`).
- [ ] Creature bodies priced on his scale: about 0.4 of a card a point of power-and-toughness; power past what
      toughness can protect discounted (a 7/2 close to a 5/2).

## Decisions

- 2026-11-16, Shayne: a different story from option pricing (AI-26), "semi related".
- 2026-11-16, Shayne (AI-26): a vanilla 1/1, token or card, is 0.33 to 0.5 of a card; "for all other creatures it
  depends on the abilities it has."

Open questions:
- Show in cards only (divide by 8.5 when printing — no play changes), or rescale the evaluator itself (every
  constant, threshold, table and test number moves; no decision changes if done exactly)? Claude's lean: show first.
- The fragile discount: by what rule — power counted only up to some multiple of toughness?
- Lands and life in cards (0.18 and 0.12 today): are those his numbers too?

## Tasks

- [ ] AI-27.1 The interview: the open questions above
- [ ] AI-27.2 Scores shown in cards where a person reads them
- [ ] AI-27.3 The body on his scale behind a switch: positions, suite, match

## Next

Nothing started. AI-26.8 (where the vanilla 1/1 rule lives once the search prices options) is this story's first
piece: in the search the rule can only be the evaluator's price of a 1/1.

## Log

- 2026-11-16: created from Shayne's question during AI-26.
