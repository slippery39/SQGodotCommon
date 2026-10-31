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
