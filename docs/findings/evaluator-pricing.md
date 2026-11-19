# The evaluator's prices — inventory, principles, and the three-ledger proposal (AI-27)

Written 2026-11-17 against `mtg-real` at 98d1a0f2, for Shayne to read and correct before any code changes.
Read-only: nothing in the AI changed. The principle tests are `MagicCore.Tests/Ai/PricingPrincipleTests.cs`.

A **point** is the evaluator's raw unit. For scale: the average measured card is 8.5 points (`OptionReport.ACard`),
but the search prices an unseen card at **2.65** (theirs) or **2.54** (our draw, the land-or-not blend), and an
unseen spell at **4.38**. That spread is most of what follows.

## 1. Inventory — every place a price is set

Label: **V-now** value, immediate · **V-time** value over time · **T** tempo · **O** option · **mixed**.
"Held" means the search's default: a card's measured value × 0.5 (`HeldValues`, fraction 0.5).

### Cards in hand

| # | What | Formula | Points | Set by | Label |
|---|---|---|---|---|---|
| H1 | A non-land card, nothing measured | `FlatHandCard` | 1.2 | original | V-now |
| H2 | A land in hand | `LandInHand` | 0.3 | original | mixed (card + mana to come) |
| H3 | A land that unlocks nothing | `LandSurplus` | 0.05 | AI-9.1 | V-now |
| H4 | Our card, measured | 0.5 × its `CardValues` entry, never below 1.2 | a 3/2 vanilla: 4.25 | AI-1 | **mixed** — the 0.5 is in effect the tempo charge for holding (§3) |
| H5 | Our card short on mana or colour | H4 × 0.6 per source short (generic past the first; every unmakeable pip) | ×0.6, ×0.36 | AI-17.21, AI-9.9 | T (castability) inside V |
| H6 | Our held instant / flash card | expected best of its offers over 1–4 turns (`CastGains.ExpectedBest`, `OptionTurns`) | per table | AI-13, AI-13.4 | O |
| H7 | Our targeted sorcery-speed card | its offers' average, one turn | per table | AI-13.10 | O ("a turn of tempo") |
| H8 | Our answers (targeted, gain from a likely creature) | 1.2 each + their joint value over 32 sampled futures, ≤ 8 turns, less 0.5 × mana value × how busy our mana is | per position | AI-26.4 | O, with a T charge |
| H9 | Their creature that ours hold off, as a target | × 0.4 | — | AI-26.12/16 | O |
| H10 | Their unseen card (land or not) | 17/40 × 0.3 + 23/40 × the FRA table's average held spell | 2.65 | AI-1, AI-9.5 | V-now |
| H11 | Their card we know: a creature | max(held, body − 0.5 × mana value) | a 3/3 for 3: 8.0 | AI-13.4 | **mixed** — the recast is tempo inside a card price |
| H12 | Their card we know: a land / a spell | 0.3 / its held price | — | AI-9.5 | V-now |
| H13 | Our unseen draw in the look-ahead (`Hidden.UnknownCard`) | the FRA table's average held spell, never short, never an option | 4.38 | AI-20.3 | V-now |
| H14 | Our hand over its maximum, our turn | the cheapest cards count 0 | — | AI-13.13 | V-now |
| H15 | A card exiled "you may play it", made inside the search | 0.8 × H10, each further one × 0.5 | 2.12, 1.06 … | AI-17.16 | V-now |
| H16 | A graveyard card that returns itself | its hand price − 0.5 × the return's mana | — | AI-17.6 | V-time, T charge |

### The board

| # | What | Formula | Points | Set by | Label |
|---|---|---|---|---|---|
| B1 | A creature | 2 + 1.5 × power + toughness + 0.8 a keyword (lasting stats, damage off toughness) | 1/1 4.5 · 2/2 7.0 · 3/2 8.5 · 3/3 9.5 · 7/2 14.5 | original, AI-5.3 | **mixed** — the card (the flat 2) and the mana in it (the stats) |
| B2 | A creature gone at the next end step | 0 | — | AI-28.2 | V-now |
| B3 | A token creature | as B1 | — | — | mixed |
| B4 | Lands in play | 1.5 each to 7, 0.4 beyond | — | original | **T** (mana capacity) |
| B5 | Other permanents (a Clue, an Aura, an engine) | 2.0 flat | — | original | V-time, unpriced |
| B6 | A Spacecraft below its threshold | 2 + charge share × its creature's body | — | MC-5.2 | V-time |
| B7 | A planeswalker | best sequence of its abilities over ≤ 5 turns, each turn × 0.6, while its loyalty outlasts their attacks; floor 2; + 0.1 a counter | — | AI-9.3 (AI-8.2 before) | V-time |
| B8 | Damage it soaks | 0.5 × the life price of each point | — | AI-9.3 | V-time (life) |
| B9 | Marked damage | in POTENTIAL only, share of toughness | tie-break | AI-19.22 | O |

### Life and the clock

| # | What | Formula | Points | Set by | Label |
|---|---|---|---|---|---|
| L1 | Life | 1.0 a point to 20, 0.5 above; below 6 one more a point and (6 − life)² ÷ 2; 0 is −50 | 5 lost at 20: 5.0 · 8 gained: 4.0 | original, AI-17.6 | V-now (by his rule) |
| L2 | The clock (both ways) | 0.5 × 20 × power through blockers ÷ their life | a 2/2 unblocked at 4 life: 5.0 | AI-5.2, AI-8.4, AI-17.9 | **T** — board turned into a race |
| L3 | A game won / lost | ±10 000, + a tanh tie-break of the board (cap 10), × 0.95 a turn | — | AI-17.6 | — |

### Effects (`Evaluator.Effect`, read by walkers, emblems and nothing else)

| # | Effect | Price | Label |
|---|---|---|---|
| E1 | Draw N | N × H10 (2.65) — **the blend with lands, not H13** | V-now |
| E2 | Discard N (ours or theirs) | N × H10 — **even of a hand we can see** | V-now |
| E3 | Surveil / scry N | N × 0.33 × H10 | V-time |
| E4 | Exile the top, you may cast it | 0.8 × H10 | V-now |
| E5 | "If …" | 0.5 × its effects | — |
| E6 | Damage to a player / life gained | the life price | V-now |
| E7 | Damage that kills / −X/−X that kills | the creature's B1 | V-now |
| E8 | Bounce | B1 − max(held, B1 − 0.5 × mana value); a token all of B1 | **T** dressed as V |
| E9 | Stun counters | power × N × our life price | T |
| E10 | +1/+1 counter | 2.5 each | V-now |
| E11 | Mana, untap lands | the land price of that many more lands (restricted × 0.5) | T |
| E12 | Edict | their cheapest permanent (creatures by B1, others 2) | V-now |
| E13 | Emblem | anthem as counters on our creatures; a trigger over 5 turns at × 0.6 | V-time |
| E14 | Pending surveil on the stack | 0.33 × H10 each | V-time |

### The search's own prices and rules

| # | What | Value | Set by | Label |
|---|---|---|---|---|
| S1 | Rollout: 2 turns + their counter-attack, scored by the evaluator at the horizon, undiscounted | — | AI-16, AI-17.6 | (implicit T, §3) |
| S2 | Our rollout plays the land and **at most one spell a turn** (`ActionsPerTurn = 1`) | — | — | (implicit T) |
| S3 | Their rollout plays only cards we KNOW they hold — their unseen hand is never cast | — | AI-10.4 | (implicit T) |
| S4 | POTENTIAL tie-break: our untapped mana sources and creatures + marked-damage share, within 0.1 | count | AI-13.11 | O ("mana kept up") |
| S5 | Pass bias 0.01; waiting wins a tie for an instant-speed move (`WaitOnTies`) | — | AI-13.12 | O |
| S6 | Tap abilities kept for their end step (`LastChance`) | rule | AI-26.5 | O |
| S7 | Face burn held until it kills within two turns (`SaveBurn`); never pass with a land drop (`LandDrop`) | rules | AI-17.6, 2026-11-08 | O / T |
| S8 | `Lookahead.Settle`'s choices (a discard, a sacrifice, "you may") — **no held values**: every card 1.2, their hand 1.2 each | — | AI-17.6 | V-now, its own scale |
| S9 | The card-value table itself: each card cast into a fixed board with **flat** prices and **libraries of basic lands**, to the turn's end, no attacks | — | AI-1 | V-now — a draw spell measured as lands drawn (0.3 each), engines and walkers' later turns unseen |
| S10 | Cast-gain offers: an evaluation gain + the card's held price, GreedyAi self-play | — | AI-13.4 | O |
| S11 | Four horizons: options 4 turns, the option price 8, a walker 5, the rollout 2 + 1 | — | — | — |

### Where a seen card meets an unseen one

| # | Comparison | The two prices |
|---|---|---|
| X1 | A card of theirs we come to know (a bounce, a reveal) against their unseen card | H11/H12 against 2.65 — measured: known cards average **3.76** (+42%), so revealing their hand or bouncing lowers our score by ~1.1 a card before anything else |
| X2 | Our card once drawn against the unseen draw | H4–H8 (short-discounted, options) against H13 4.38 (never discounted) — measured: **1.52 against 2.54** (below) |
| X3 | A look-ahead line that draws a card we know (a surveil's keep) against one that draws blind | its held price against H13 |
| X4 | A surveil's keep-or-bin | lands by rule (unlocks or not); spells branched; the old rule `held.Of < held.Unknown` is off |
| X5 | Walker and effect draws (E1) against the search's own drawn card | 2.65 against the land-or-not blend (2.54 here; it moves with our lands and deck) |
| X6 | A discard of a card we can see (E2) | priced as an unseen card |
| X7 | The option price's gains | measured with the default held values; a bounced creature as H11 |
| X8 | `Settle`'s choices (S8) against everything else | 1.2 a card against the held scale |
| X9 | The 6th unseen draw of one look-ahead | a real card, by id (`Hidden.UnknownDraws = 5`) |

**Six "cards"** live side by side: 1.2 (flat, Settle, the fixture), 2.65 (their unseen card, every effect's draw),
~2.5 (our draw, blended), 4.38 (our unseen spell), 4.25 and up (a known card at half its measured value), 8.5 (the
average measured card, the option report's unit). A rescale that moves one breaks every comparison against another.

## 2. His principles as tests

Each is a relation that must hold whatever the prices — "a card" is the evaluator's own price of drawing an unseen
card now, so no number is restated. Positions start from empty hands in our first main phase, 20 life each.

| Principle (his words) | Test | Today |
|---|---|---|
| "drawing a random card is not punished" | an unseen card, and a seen land, each score above not drawing — on 0 and on 8 Forests | **pass** |
| "I am still up 2 cards" (a draw-2 that hits two lands, late) | 8 Forests in play, empty hand: two seen Forests drawn ≈ two unseen cards (±25%) | **fail** — 0.10 against 5.09 |
| "cards are properly evaluated once they have been drawn" — ours | 3 Forests in play: the average over our library of each card seen in hand ≈ an unseen draw (±10%) | **fail** — 1.52 against 2.54 |
| — theirs | the average over their library of each card known in their hand ≈ their unseen card | **fail** — 3.76 against 2.65 |
| "they draw a card" costs what ours gains | their unseen draw against ours (±5%) | **pass** — 2.65 against 2.54, 4% apart |
| "Waiting to draw a card doesn't really give you any additional value" | a Jace token ("−1: Surveil 1", "−3: Draw a card") at 3 having drawn, against at 6 not | **pass** |
| — a Clue ("{2}, Sacrifice: draw a card") | cracked, against kept | **pass** |
| "still 'digging' through your deck" (added after his answers) | a Jace token at 6 on three Forests, the look-ahead's land version: the land its −3 draws is worth about a card, and the −3 beats banking | **fail** — the land 0.05 against a card 2.54; the −3 6.85 against banking 7.64 |
| "the opponent having to spend mana to deploy their card again. It does not 'give a card back'" | their creature bounced gains less than killed; a 1/1 for {5} and a 5/5 for {5} gain the same bounced, clock aside | **pass** (with the clock the 5/5's bounce gains 2.0 more: the attack it misses — tempo, rightly) |
| "we should not choose to hold our 3/2" | a vanilla 3/2 for {2}{G} cast, or traded with theirs, never below holding it; a trade is neutral — at held 0.5, 0.75 and 1.0 | **pass** at all three (at 1.0 only by the clock: their 3/2 unanswered) |
| "5 life is worth it to pay to draw a card"; "a card spent to gain life … 8+" | 5 lost at 20 within 0.75–1.33 cards; 7 gained under a card; 10 gained a card or more | **fail** — 5 lost is 1.96 cards, 7 gained 1.38 |

The four that fail are `[Explicit]` — the target, not a regression; the test adapter skips them even when a filter
names them, so remove the attribute to run them.

**Why ours fails, X2:** in the test's mixed-colour deck on Forests, a seen card is mostly colour-short (× 0.6 —
Repurposed Enforcer, {1}{W}, 4.25 held, 2.55 in hand); with the short discounts off, seen is 2.86 against 2.54. So
two gaps: an unseen draw is **never** discounted for mana or colour, a seen one is; and the unseen draw is the whole
FRA table's average (4.38) where this deck's spells average 4.83. Seeing a card should not change its expected worth;
today it does, both ways.

**Why life fails:** against his rule the gap is the card, not life — 5 life is 5.0 points and an unseen card 2.54.
By the unseen-spell price (4.38) his rule nearly holds (5 life 1.14 cards, 8 gained 0.91).

## 3. The proposal — three ledgers (for discussion; nothing built)

### The ledgers, each in its own unit

**VALUE — cards.** Both players counted alike; +1 is a card up.
- A card in hand is **1 card** of count. What it *is* moves it around 1 by its quality `q` (its measured worth over
  its deck's average), normalised so a deck's cards average exactly 1 — ours from our list, theirs from the decks
  `OptionPrice` already samples for "what they probably play". An unseen card is 1 by definition. That alone fixes
  X1, X2, X5 and X8: one scale, and seeing a card changes nothing on average.
- **The library**, the missing half. A draw-2 that hits lands is +2 because "I would have spent the next 2 turns
  drawing those lands anyways": being 2 cards deeper means my next draws are cards 3 and 4. So the score counts
  what I hold **and the draws still to come in the game's length** (D, from the clocks): a land seen makes the rest
  of my library better, and the two exactly cancel on average. Drawing then can never score below not drawing, a
  draw-2 of lands is +2 whenever the game lasts two more draws, and "now beats later" falls out (the same card,
  usable sooner).
- **A permanent** is the cards' worth of what it does: a body on his scale (~0.4 a point of power and toughness, power
  past what toughness protects discounted — the open AI-27.1 question), a walker or engine as what it makes over its
  lifetime (B7, already shaped this way), a Clue as the card it will draw less nothing.
- **Life** by his rate: 5 paid at 20 ≈ a card; gains worth less than losses; dearer the lower it is (L1's shape).

**TEMPO — mana.** Mana deployed against mana negated.
- Their mana negated: a kill or bounce costs them the removed permanent's mana value; ours spent on it is charged
  by the line itself (the mana that line did not put on the board). Bolt on a 4-drop +3, Divination −3, a bounce
  + their recast with no card (E8, H11 become pure tempo).
- The race: the clock (L2) *is* tempo turned into life — it stays, as tempo's.
- Lands in play (B4) are tempo capacity.

**OPTIONS — cards.** What holding is worth: the expected best use later, less the use now, never below 0 — over the
turns the game has left. H6–H9, S4–S6 and `WaitOnTies` are all this. A vanilla creature has none, so it is cast; an
answer waiting for its target keeps its premium. Holding's cost is tempo (mana unspent this turn), which the tempo
ledger now charges — so the 0.5 held fraction (H4), today's hidden tempo charge, goes to 1.0.

### Combining: one exchange rate, from the game's expected length

`score = value + r(L) × tempo + options`, where L is the game's expected length in turns from the two clocks the
evaluator already computes (`TurnsToKill`, as `OptionTurns` and `OptionPrice.TurnsAhead` read today). Short race:
tempo dear, cards cheap ("who's the beatdown"); long game: the reverse. Also from L, and only from it: every "over
time" horizon — a walker's lifetime, the options' turns, the draws still to come, the option price's futures —
replacing today's four (S11). Recommendation: start with a mana at about a third of a card in an average game (an
average spell costs ~3), scaled by length, and let positions and a match set it.

### Not counting the look-ahead's tempo twice

The rollout already turns some tempo into the score: a board deployed this turn attacks and blocks for two turns and
the counter-attack, and that damage lands in life (S1). It does not see (S2, S3): our second spell in a turn, their
unseen hand ever being cast — so the mana a bounce makes them re-spend never crowds anything out — or anything past
the horizon. So the tempo ledger is read **at the horizon only, for what is still standing there** (mana on the
board, their known cards still to recast) — never for damage already dealt, which is already life. The clock term
is the one overlap to watch: it forecasts the race from the horizon on, which is tempo; the body price must then
stop carrying the mana in it (B1's stats), or a creature is paid for twice.

### Every term, mapped

| Today | Becomes |
|---|---|
| H1, H4, H10, H12, H13, E1, E2, E4, S8 | VALUE: 1 card × quality, one scale |
| H2, H3, H5, H14 | VALUE quality (a surplus land, a card we can't cast in time) |
| H6–H9, B9, S4–S7, S10 | OPTIONS |
| H11, E8 | TEMPO (their recast), VALUE keeps the card |
| H15, E3, E14 | VALUE: a share of a card |
| H16, B5–B8, E10, E13 | VALUE over time, over L |
| B1, B3 | VALUE (what it does) + the stats' mana to TEMPO — the split to settle |
| B2 | VALUE 0 |
| B4, E9, E11, L2 | TEMPO |
| L1, E6, E7, E12 | VALUE |
| L3, S1–S3 | unchanged — the search |

### Order, once agreed (each step behind a switch, positions then the suite then a match)

1. One card scale (VALUE's count and quality) — the failing seen/unseen tests turn green; nothing else moves.
2. The library's draws to come — the draw-2 test.
3. Bodies on his scale; life against the card.
4. The tempo ledger and r(L); the held fraction to 1.0 with it, never before (AI-1: 1.0 alone hoarded).
5. One horizon L for every over-time term.

## Questions for Shayne

1. A card's quality once seen: normalised to its own deck's average (a bad card in a great deck is below 1) — right?
2. The library term: a draw-2 is +2 "whenever the game lasts two more draws" — or always +2, whatever the length?
3. Tempo of a token (mana value 0): the mana a vanilla of its body costs, or none?
4. Lands in play: tempo only, or also cards (a land is a card drawn)?
5. The exchange rate: a third of a card a mana at an average length — your feel?
