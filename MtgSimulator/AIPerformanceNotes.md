# AI Performance & Balance Notes

Issues and improvement ideas captured during simulation analysis. Not prioritised — revisit when simulation speed or AI quality becomes a blocker.

---

## Performance: Action Space Explosion

**Problem**

After fixing the attack-target bug (removed `break` in `MtgActionGenerator`), the legal action count for any turn is now:

```
(cards playable from hand) + (N_attackers × (1 + N_opponent_creatures)) + (activated abilities)
```

With a large board (10+ creatures per side) and 10+ cards in hand, a single turn can easily produce 100+ legal actions. At depth 3, the AI evaluates up to `100^3 = 1,000,000` state simulations. This is the primary cause of the ~4× slowdown observed after the fix.

---

## Potential Improvement 1: Alpha-Beta Style Pruning

**Idea**

The search is currently greedy best-first (not minimax), but the same pruning principle applies: once a candidate action scores at or above a threshold, skip evaluating the remaining siblings at that depth level. The win-score cutoff already does this for terminal states — it could be extended to a configurable "good enough" threshold for non-terminal states too.

A proper minimax with alpha-beta pruning would also let the AI model opponent responses, which would improve play quality significantly at the cost of increased complexity.

**Tradeoff**

Pure greedy search already skips entire subtrees once a winning action is found. Extending this to non-terminal thresholds risks missing a globally better action for a locally acceptable one. Needs careful tuning.

---

## Potential Improvement 2: Dynamic Depth Reduction

**Idea**

When `GetLegalActions` returns more than N actions (e.g. 20), drop the search depth from 3 to 1 for that decision. Keeps the AI responsive on complex boards at the cost of shallower lookahead exactly when the board is most interesting.

**Tradeoff**

The AI will make weaker decisions at high branching factor, which is arguably the most strategically important moment. Might cause noticeable play quality regression on large boards.

---

## Potential Improvement 3: Attack Target Prioritisation / Pre-Filtering

**Idea**

Rather than generating one `AttackAction` per attacker × target and letting the heuristic score sort them out, pre-rank opponent creatures by some threat metric before generating actions. Only generate attack options against the top K targets (e.g. the 2 highest-power creatures + the player), discarding the rest before the AI ever sees them.

Threat metric candidates:
- Opponent creature power (immediate damage threat)
- Power / toughness ratio (efficient attackers to trade into)
- Specific ability flags (once keyword abilities are modelled)

**Tradeoff**

Reducing options pre-emptively means the AI can never consider attacking a "low threat" creature even when it's strategically correct (e.g. killing a 0/1 that enables a combo). Accuracy loss depends on how the threat metric is calibrated.

---

## Potential Improvement 4: Phase-Ordered Action Splitting

**Idea**

Instead of presenting all legal actions simultaneously, restrict what category of action the AI can take at each "phase" within its turn:

1. **Play spells** (non-creature) first
2. **Declare attacks** second
3. **Play creatures** third

This reduces the branching factor at any one decision point since the AI only considers one category at a time. The game loop enforces ordering; the AI strategy just operates on a filtered action list.

**Tradeoff**

Fixes the action ordering, which is sometimes suboptimal. Examples where this hurts:
- Playing a creature *before* attacking could trigger Mentor of the Meek and draw a lethal spell
- Attacking *before* playing a pump spell misses the damage bonus

The accuracy cost is real and could skew win rates. Would need simulation comparison before/after to evaluate impact.

---

## Balance Note: Mentor of the Meek Draw Engine

Observed in flagged games: Player 1 accumulated 14–18 cards in hand by turn 8–9, with 10+ creatures on the battlefield. The draw loop fires as follows:

1. Player has Mentor of the Meek + mana + cheap creatures in hand
2. Plays a creature with power ≤ 2 → Mentor triggers → draws a card
3. Drawn card is another cheap creature → repeat

With enough mana and creatures in the deck this is effectively an unbounded draw engine within a single turn. Not a bug — this is how Mentor of the Meek works in real MTG — but the random deck construction means it can show up alongside multiple other draw-trigger creatures (Battlefield Scholar, War Drummer) which compound the effect.

Flagging to revisit when balance/card design becomes a focus.

---

## Balance Note: Craw Wurm Win Rate

Craw Wurm (6/4, high mana cost) showed an unexpectedly high win rate in early simulations. Likely explanation: the evaluator weights `TotalPowerWeight = 1.5` and `CreatureCountWeight = 3.0`, so a single large creature scores well even if it comes down late. Not necessarily a balance problem — could just be a reflection of the evaluator's current weights. Worth re-examining after any evaluator tuning.
