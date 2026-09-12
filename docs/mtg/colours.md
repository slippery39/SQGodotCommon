# MTG — Colours and the manabase

Moved out of the root `CLAUDE.md` when this repo became the DOOMJAM project. Engine detail is in
`MtgCore/CLAUDE.md`; how colour shapes the evolver's field, the three win-rate tables and the
identity-scoped presim are in `.claude/rules/sim-evolution.md`.

Colour is a **second, independent mana track**. A land grants 1 generic AND its colours; a cost of
"1W" spends 1 generic and 1 White. The two never substitute for each other, so payment is fully
determined — no ordering choice, no solver, no manual tapping. **Coloured mana DEPLETES and refills
each turn, exactly like generic** — it is not an Eternal-style permanent threshold. That is the
whole design: a five-colour manabase caps you at one single-pip spell per colour per turn, so greed
costs throughput while focus costs nothing.

**A double pip is effectively a mono-colour card, and that is where the whole colour constraint
lives.** Measured in this engine (`ManaBaseCalibrationTests`) — sources of one colour needed in a
60-card, 24-land deck for a 90% on-curve cast:

| pips | cost 1 | 2 | 3 | 4 | 5 | 6 |
|---|---|---|---|---|---|---|
| 1 | 13 | 12 | 11 | 10 | 9 | 9 |
| 2 | – | 18 | 17 | 17 | 15 | 15 |
| 3 | – | – | 22 | 22 | 21 | 20 |

A two-colour deck split 12/12 casts a single pip **89% on turn one and 94% by turn three**, but a
double pip only **65% by turn three** — 17 of its 24 lands would have to be one colour. So
single-pip greed is barely taxed and double pips carry the constraint. **Treat the pip depth of a
card as its real colour commitment**, and expect `WW` cards to belong to mono decks.

**Paper Magic's manabase tables do not transfer here and must not be used.** The opening hand is
guaranteed to contain exactly three lands (`SetupGameAction.OpeningHandLandCount`) drawn uniformly
from the manabase, which makes early colour access far more reliable than a real seven-card draw; a
borrowed table systematically over-builds. Re-run `ManaBaseCalibrationTests` if the opening-hand,
land-drop or deck-size rules ever change.

`ManaBase.Build` is the only place a manabase is made. `CardValueSandbox` grants every colour at the
generic depth — a table handing out generic only would score coloured cards as uncastable and
rewrite the value tables into a report about colour screw. `MtgGameFactory.CreateForTesting` grants
99 of every colour: a test about a mechanic should not fail on colour, and a test about colour zeroes
it explicitly (`ManaColorTests`).

