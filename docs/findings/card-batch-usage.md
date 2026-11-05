# Card-batch usage

What each MagicCore card batch cost, to check that a workflow change saves usage (story MC-5: TRK is
the test of one fresh session per batch). Measured by `tools/usage_report.py`: **input-token
equivalents** (input 1, output 5, cache read 0.1, cache write 2 — as the API prices them), subagents
included. One batch = one session, so a row is the whole session.

**Add a row at the end of every batch, just before its commit:**
`python tools/usage_report.py --log "<task>: <what>, <N> cards"`. Cost per card = total / N.

**Baseline (FRA, 2026-10-10, before fresh sessions):** the rare batches cost ~3M each, eight in one
session (e24a44f6, 81.8M in all); 70% was cache reads of a ~442k-token median context. A fresh
session per batch was simulated to save 37–40%.

| Date | Batch | Session | Calls | Median context | Total |
|---|---|---|---|---|---|
| 2026-10-31 | MC-5.1: the TRK survey + plan, 0 cards | f711d95c | 61 | 184k | 2.0M |
| 2026-10-31 | MC-5.5: card registry across sets, TRK tokens, Planet duals, 10 cards | 498c3cea | 142 | 225k | 4.7M |
| 2026-10-31 | MC-5.3: batch 1 — tier 0-1 TRK commons and uncommons, 88 cards | 0065e5f8 | 125 | 254k | 5.0M |
| 2026-10-31 | MC-5.2 + MC-5.3 batch 2: Station, 12 cards | a4330530 | 116 | 248k | 4.4M |
| 2026-11-01 | MC-5.3: batch 3, the small TRK mechanics (beam me up, SNW, dilemma, power 8, becomes tapped, look-take, Federation), 31 cards | 14545f2e | 153 | 293k | 7.4M |
| 2026-11-01 | MC-5.3: batch 4a — counters of any kind, Sagas, kicker, can't block, once each turn, 28 cards | dfef712d | 143 | 297k | 7.4M |
| 2026-11-01 | MC-5.3: batch 4b — delayed return, leaves the graveyard, type change, second resolution, granted trigger, control, counters plus one, block/target restrictions, 17 cards | 8cd8b49f | 173 | 286k | 7.6M |
| 2026-11-02 | MC-5.3: batch 4c — exile face down, play an opponent's exiled card, one destroy judged together, draw, can't attack alone, lands enter untapped, Zefram, 14 cards | 7cf44660 | 185 | 278k | 8.3M |
| 2026-11-04 | MC-5.3: batch 5a — tier 1–2 rares and mythics, 40 cards | c6125612 | 143 | 280k | 5.6M |
| 2026-11-05 | MC-5.3: batch 5b — delayed return on a dead card, charge doubled, a mana ability that costs mana, tap N as a set, life lost this turn, base P/T for good, reveal top X (+ sprint 4 planning), 8 cards | 9c72e4a6 | 146 | 268k | 5.8M |
| 2026-11-05 | MC-5.3 batch 6a: shocklands, sunburst, X targets picked as cast, changeling, a number as it enters, 14 cards | 7c02dbfc | 164 | 280k | 6.5M |
