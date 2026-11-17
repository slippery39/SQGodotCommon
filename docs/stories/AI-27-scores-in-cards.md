---
id: AI-27
title: An evaluator pricing pass — one consistent theory, scored in cards, instead of piecemeal fixes
status: backlog
area: AI
created: 2026-11-16
---

## Why

**Reshaped 2026-11-17 (Shayne).** "I think we might need an evaliuator pricing pass and please note what we found
here, where instead of fixing everything peacemeal one by one, we actually try to come up with a better way for the
evaluator to score that is more consistent and less finicky." And: "thats what that story should focus on, is sort of
combining all the issues we have had into an easier to understand theory that spans across our whole system."

His theory, in his words (2026-11-17):
- **One unit:** "perhaps we need to normalize our scoring by 'cards' that is a value of +1 means you are now up a card
  from before."
- **Drawing is never punished, cards valued once seen:** "we also have to make sure that drawing a random card is not
  punished (since drawing a random card will always get you 1 card deeper in your deck), but that cards are properly
  evaluated once they have been drawn (i.e. a random card should always be worth a card... a 1/1 vanilla creature on
  turn 10, is usually not worth much even if it was the card you drawn.)"
- **Deploying is not punished:** "We have had some bugs where not deploying cards is punished, because we might lose
  that card to a line later on (which makes no sense, a deployed resource is usually better than a non deployed
  resource), so like if I play a 3/2 and it trades with the opposing 3/2, both of us are neutral on cards, which is
  usually fine we should not choose to hold our 3/2 in our hand because that might happen."
- **Card advantage counts whatever the cards are:** "if I play a draw 2 and I draw 2 lands, even if it is the late game
  and the lands are not that valuable, I am still up 2 cards from a total gameplay perspective. Otherwise I would have
  spent the next 2 turns drawing those lands anyways and being in a much worse spot. It shouldn't matter that my draws
  happened to be lands but just the fact that I got to draw 2 cards deeper in the deck."
- **Drawing now beats drawing later:** "drawing a card gets 1 carc closer in your deck. Waiting to draw a card doesn't
  really give you any additional value since you will be a card deeper in your deck either way."
- **A bounce gives no card:** "the value is in the opponent having to spend mana to deploy their card again. It does
  not 'give a card back'".
- **Life against a card:** 5 life paid at 20 is about a card; a card spent to gain life needs 8+; life is worth more
  the lower it is (Decisions, 2026-11-17).

Claude's reading of the theory (for him to correct): a position is scored as cards up, both players counted alike —
cards in hand, cards' worth on the board (what each is DOING, against an average card), library depth, life converted
at his rate, mana spent as tempo. A drawn card is an average card at the moment it is drawn; seeing it is a land is no
loss, because the library left is better for it (the evaluator sees "a land in hand" and never "and my next draws got
better" — the missing half). A trade is −1 each, so casting never scores below holding the same card. Loyalty that
buys a draw is worth at most that draw now. A bounce costs them the recast, and no card.

**What pointed here — fixed one by one, each in its own units** (the evaluator prices "a card" in at least six places:
a held card at half its measured value, an unseen card 2.65, the flat 1.2, a walker's future draws at the unseen price,
the option price, a bounced creature as itself less its recast):
- AI-26: a Bolt on a vanilla 1/1 (the 1/1 at 4.5, "not even close"); the held-off share on the board counted a wall
  twice and jumped when the wall died (Jace −1 for −3, Divining Duelist in its own combat — AI-26.16, option A).
- AI-26.16: removal gains measured without the held values made a bounce worth more than a kill.
- AI-28.2: a token sacrificed at the end step priced as a 2/1 kept, so the line that made it lost.
- AI-28.3: a Clue (a flat 2.0) lost to 4 life at 21 (3.5).
- This story's trial, unseen cards at full value (2026-11-17): three Jace −3 draws became passes and four removal
  spells were held (Prophesied End, Fatehold Charm's bounce, Vraska's Final Mercy, Academic Ascent) — Shayne: "The fact
  you described these alone tells me that somehting is way off in our evaluator." Not traced: the causes are guesses.

**Earlier (2026-11-16), the story as first opened:**

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

- 2026-11-17, Shayne (asked, from AI-28.3's Clue, what a card is worth against life): "At 20 life, I would say 5 life is worth it to pay to draw a card as a baseline, but depending on the
  matchup it could be higher or lower. Obviously at lower life totals, this does not sustain. But also there is a
  difference between paying life to draw a card, and spending a card to gain life. In general spending a card to gain
  life is priced much higher, usually needing a lot of life (8+) before its actually worth an entire card and even then
  its situational. Obviously this also changes based on current life (more life is generally worth more the lower life
  total you are)".
  Claude, measured against today's evaluator: its life curve already has his shape — below 20 a point costs 1.0, above
  20 it gains 0.5, so 5 life paid at 20 is 5.0 and 8–10 life gained is 4.0–5.0 — IF a card is ~5. But an unseen card
  is 2.65 to it (an unseen spell 4.38; held cards at half their measured value, lands mixed in): it values 5 life at
  ~2 cards. So by his rule the gap is on the CARD side, not life's.

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

**Handoff (2026-11-17, end of session).** The story is now the pricing pass (Why). Nothing of it is built, and the
default evaluator is unchanged (held cards at 0.5; `beam-held=1.0` was a match name only). The trial switch
`beam+unseenfull` was **removed before commit**: `/pre-commit` found it priced cards on TWO scales — a card we can't see
at 1.0, a card we know at 0.5 — so every seen-against-unseen comparison was skewed: a surveil binning good known spells
for a blind draw, a reveal or bounce lowering their hand, the option price measured on the other scale. So the trial's
odd plays (Jace −3 kept, removal held) may be ITS artifacts as much as the old evaluator's — one more case for one
consistent scale. Start with:
1. **An inventory:** every place the evaluator prices a card, a body, life, mana, loyalty, a token, a draw — its formula,
   its unit, the story that set it. The six above are the start. Read-only.
2. **Where a seen card meets an unseen one** — the surveil's keep-or-bin (`BeamSearchAi`, `held.Of(name) < held.Unknown`),
   a known card in their hand (`Evaluator`, the known-hand branch), our known top card against a blind draw, the option
   price's own held values (`OptionPrice.Measured`) — each must use the same scale, or a rescale breaks it.
3. **His principles as tests** before any rescale — each true whatever the card price: drawing now ≥ holding the
   loyalty; a bounce costs the recast and gives no card; a draw-2 that hits lands is still +2; casting a 3/2 that
   trades never scores below holding it.
4. **Then a proposal to him:** the one theory, in cards, and how each term maps onto it — discussed before code.
The five draw-or-board positions (findings, "AI-27") wait for his read.


Nothing started. AI-26.8 (where the vanilla 1/1 rule lives once the search prices options) is this story's first
piece: in the search the rule can only be the evaluator's price of a 1/1.

## Log

- 2026-11-17: Shayne: "Yeah lets try raising the value of a card towards 5 points, this might just be a raw value
  thing." Tried with the existing held fraction (`beam-held=X`; an unseen card is LandShare × 0.3 + the rest × the
  average measured spell × X). Measured, nothing changed in the code:

  | held fraction | unseen card | #364 (Raffi Musiker: life or a Clue) | suite changed | oracle agrees | engine steps |
  |---|---|---|---|---|---|
  | 0.5 (default) | 2.65 | gains 2 life (7.93 / 6.43) | — | 76.0% | 20 132 |
  | 0.75 | ~3.9 | gains 2 life (6.82 / 6.50) | 18 of 288 | 75.0% | 19 575 |
  | 1.0 | ~5.2 | **investigates** (7.65 / 5.70) | 32 of 288 | 70.5% | 18 792 |

  At 1.0: 9 of the 32 now pass where they played (Jace −3 thrice, Mindseeker Oculus twice), 5 play where they passed,
  18 play something else. AI-1 (2026-10-16) measured 1.0 hoarding and losing 26–46 — before most of today's search.
  **The match** (`--match=beam-held=1.0,beam --until-clear`, 600 games, 155 min): **47.5% ± 1.4pp (285-315)**,
  undecided at the cap (no 5pp difference; the estimate about −2.5pp). Per game it cast as many spells (9.8 against
  9.7) and played as many lands (7.5 / 7.4) — **not hoarding**. By deck: Red-Green 130-70, White-Black 103-97,
  White-Blue Jace 52-148; 9 463 engine steps a decision against 9 947. Shayne's reading: drawing over-valued against
  board presence ("I could see the AI maybe choosing to play card draw spells over getting board presence") — what the
  spells are, not how many; to test by raising unseen cards (draws) and cards in hand apart.

- 2026-11-16: created from Shayne's question during AI-26.
