# Limited — measured results

Numbers runs produced for sealed and draft (stories `LIM-*`). The rules they support live in the
stories and `MagicCore/CLAUDE.md`.

## Splash and greedy suggestions: how often the rules fire (2026-10-24, LIM-3.2, LIM-3.5)

`SealedBuilder.Suggestions` on seeded FRA pools, with Shayne's rules of 2026-10-24 (a splash for the
set's top 10% by 17lands GIH WR — ≥ 61.7%; a single pip of the splash colour; 3 sources, one more
per extra card; 7 per main colour; 17 lands):

| | Pools |
|---|---|
| a top-10% card off the streamlined pair's colours | 226 of 300 (75%) |
| two such cards in ONE off colour | 9 of 300 (3%) |
| a splash suggested | 22 of 60 (37%) — 15 before the review's fix (below) |
| a greedy splash suggested (LIM-3.2: one colour, lands only) | 0 of 60 |
| **a greedy build suggested (LIM-3.5: up to four colours, every fixer counted)** | **56 of 60** — 25 three colours, 31 four |
| — four colours with no rule against them | 53 of the 59 |

- **Most off-colour bombs can't be splashed by the rules:** gold cards of two other colours, or two
  pips of the splash colour.
- **LIM-3.2's greedy was rare by construction:** two top-10% cards in ONE off colour (9 of 300
  pools), counting only lands as sources. Shayne (round 3): count every fixer — landcyclers and
  land-searchers for every colour, Heartwood makers, mana creatures — and let greedy add colours.
- **Greedy is scored by its cards' ratings alone**, so another colour always scores higher when the
  mana clears the bar: without a rule, 53 of 59 greedy builds were four colours. Shayne (round 4):
  four only for a top-10% card the best three can't play — 31 four, 25 three. Nothing yet prices
  inconsistency; LIM-3.3's simulated draws should.
- **A fetcher needs a basic to fetch** (Shayne, on seed 3's greedy build splashing Lich's Relic with
  NO Swamp — "landcyclers cannot fetch a black source otherwise"): a landcycler or land-searcher is a
  source of every colour the deck has at least one basic of, and of no other — two landcyclers and a
  Swamp are 3 black sources, two and no Swamp are 0. The mix of
  suggestions didn't change (the fix adds the basic far more often than it rules a build out).
- **Basics are ALLOCATED to the rule, not split by pips and then checked** (the /pre-commit review):
  each main colour first gets the basics it needs for 7 sources, the rest by pips. Split by pips, a
  lopsided pair (9/5 of 14) failed the check and lost a legal splash — 7 of the 22 pools.
  `OneBomb_OnBasicsAlone_…` pins it (revert-checked).
- **Two real bombs pull the pair onto their colour:** the builder picks the pair by its best 23, so a
  pool given two top-10% cards of one colour chose that colour in every pool tried (seeds 1–20) —
  they became the deck, not a splash.

## How often each suggestion's spells are castable on time (2026-10-25, LIM-3.3)

`Castability.OnTime` (simulated draws, 1 000 games a deck, on the play and the draw; a main-colour
card on curve, a splash card by turn 6 — Shayne) over the suggestions of 60 seeded FRA pools:

| Suggestion | Pools | Castable on time: mean (range) |
|---|---|---|
| Two colours | 60 | **82.2%** (74.2–88.7) |
| Splash | 22 | **67.2%** (54.2–75.6) |
| Greedy, three colours | 25 | **60.6%** (48.5–72.4) |
| Greedy, four colours | 31 | **50.8%** (29.5–60.9) |

- **The rule of thumb passes builds that are half on time.** Every suggestion meets its source
  counts, yet a four-colour greedy build casts its spells on time about half as often as it would
  in two colours. Shayne's decision stands: shown, not gated — the screen puts each beside the
  two-colour build's number.
- **A splash costs ~15 points, mostly in the MAIN colours:** the splash's basics come out of the
  pair's (7 each instead of 8–9), so their cards are late more often — the splash card itself is
  judged by turn 6.
- **What it measures:** having the mana, as Karsten's tables do — no spell but fixers, mana
  sources and surveil is cast, so the curve itself never spends mana. A land that enters tapped is a
  source from the next turn (the first version counted it the turn it entered; the tapped-land test
  caught it). After the /pre-commit review: {2/X} paid by its colour or two generic, a Treasure
  spent once (not a lasting source), a surveil only as the card is cast or enters — the means moved
  by 0.2 points at most.

## Fixing's tempo: a landcycle spends the turn it's measured on (2026-10-27, LIM-3.7)

The same 60 seeded pools and 1 000 games a deck. Before: a landcycler cycled only when out of lands,
AFTER the turn was measured, so its {2} was also counted as casting the 2-drop. After (Shayne): it
cycles patiently, out of lands or for a colour a card needs by next turn (that card's colour, never
the landcycler's own), and its mana leaves the measured turn. "Before" is the code as it stood on 10-27, which is higher than the 10-25 table above
because fixing lands now make two of the deck's colours.

| Suggestion | Pools | Decks with a landcycler | On time, before → after | Games with a landcycle in turns 1–4: mean (max) |
|---|---|---|---|---|
| Two colours | 60 | 40 | 82.2% → **80.6%** | 7.0% (18%) |
| Splash | 22 | 11 | 73.2% → **72.0%** | 5.6% (20%) |
| Greedy, three colours | 25 | 21 | 67.0% → **64.5%** | 13.7% (28%) |
| Greedy, four colours | 31 | 28 | 55.7% → **52.2%** | 20.9% (46%) |

- **Fixing's tempo grows with greed:** a four-colour build cycles early in a fifth of its games (one
  pool in nearly half), and pays 3.5 points of castability for it. A two-colour build cycles early mostly
  to make a land drop.
- Seed 3's greedy build (the capture): castable on time 59% → 55%, cycles early in 25% of games.

## Shayne's qualifier pool, every build compared by AI-vs-AI sims (2026-10-27, LIM-3.4)

`MagicConsole --compare=MagicCore.Tests/Limited/Pools/Qualifier20261025.txt`: the 11 builds the Sealed
screen offers (the best pair's three, and the 4 closest pairs' two colours and greedy), each against
the SAME 200 AI sealed opponents (pool i from seed i, one of its suggestions), the search on both
sides; 104 min on 16 cores.

| Build | Win rate (± 1 SE) | Against Blue/Black, paired by opponent |
|---|---|---|
| **White/Black** | **55.0% ± 3.5** | **+20.5pp ± 4.3** |
| Blue/Black greedy | 49.0% ± 3.5 | +14.5pp ± 4.4 |
| White/Black greedy | 47.0% ± 3.5 | +12.5pp ± 4.3 |
| White/Blue greedy | 43.0% ± 3.5 | +8.5pp ± 4.2 |
| Blue/Green greedy | 42.5% ± 3.5 | +8.0pp ± 4.3 |
| Blue/Black + splash | 40.5% ± 3.5 | +6.0pp ± 4.2 |
| White/Blue | 35.5% ± 3.4 | +1.0pp ± 4.4 |
| **Blue/Black** (the builder's first, what he played) | 34.5% ± 3.4 | — |
| Blue/Green | 33.0% ± 3.3 | −1.5pp ± 4.3 |
| Blue/Red | 29.5% ± 3.2 | −5.0pp ± 4.1 |
| Blue/Red greedy | 29.0% ± 3.2 | −5.5pp ± 4.1 |

- **Orzhov, as he concluded after the qualifier:** White/Black two colours beats the pair the
  builder rates highest by 20.5 points — 4.8 standard errors, beyond chance even over 10
  comparisons. Its 16 spells at 1–3 mana (Blue/Black's 13) are the likeliest reason. The rating a card
  (59.7% against 58.4%) points the wrong way.
- **Greedy helps the blue pairs** (Blue/Black +14.5, White/Blue +7.5, Blue/Green +9.5 over their two
  colours), not White/Black (−8, unpaired).
- **What it measures:** which build THIS AI pilots better against AI-built sealed decks, not which
  is right for a person. The AI plays both seats, so a deck that needs patience it lacks (AI-13) is
  underrated.
- The report's lines all ended "within 2 SE, no difference is shown", even the 20.5-point gap; each
  now says which it is. The estimate (~81 min) was low: 1.3 games a minute a core here, not 1.7.

## FRA Quick Draft: eight bots, rating plus the colour lean (2026-10-29, LIM-2.2)

`MagicConsole --draft --seed=N`, seeds 1–10 (80 seats, every seat a bot). A bot takes the highest 17lands
GIH WR, ±2 points inside or outside its two colours (by value invested; a second colour left open
until one is worth taking), ramped over 10 picks.

- **Basic lands:** 17lands rates a Forest 56.5 (games in hand means nothing for a basic), and the bots
  took them as early as pack 2 pick 6. Now a basic comes last: all 114 basics in the 10 drafts went as a
  pack's 14th pick.
- **Colours are skewed toward Blue and Black:** seats in Blue 58 of 80, Black 43, White 24, Green 21,
  Red 14 (even would be 32 each). FRA's own ratings say so — its single-colour cards average Blue 58.4%,
  Black 56.6%, White 56.1%, Red 55.4%, Green 54.1% (Sealed GIH WR) — so a rating bot overdrafts them.
- **The decks still build:** a seat's coloured picks are 64% inside its two colours (lowest 34%), and it
  holds 23.9 coloured playables in its best pair on average; 2 seats of 80 under 17.

## Interim ratings for a set 17lands hasn't seen, checked on FRA (2026-11-07, MC-5.4.3)

How well a rating made without 17lands ranks FRA's cards against 17lands' GIH WR (Sealed, else Premier, ≥200
games; basics out) — Spearman's ρ over 279 cards:

| rating | ρ |
|---|---|
| rarity alone (each card at its rarity's median GIH WR) | 0.25 (ranked C < U < R < M instead: 0.22 — FRA's common median sits above its uncommon one) |
| `CardValues` alone | 0.21 |
| rarity median ± 0.02 by the CardValues rank in its rarity, unmeasured as 0 | 0.395 |
| **the same, an unmeasured card (38 of 295) at its rarity's median** — `CardRatings.Interim` | **0.413** |

The values rank far better INSIDE a rarity than across (ρ within: common 0.12, uncommon 0.33, rare 0.43, mythic
−0.05) — across, they climb with cost. FRA's medians: common 56.65, uncommon 56.63, rare 58.07, mythic 61.56. The
spread was picked on FRA (0.04 total; 0.02 gives 0.38, 0.06 gives 0.415), the set it is checked on — an in-sample
fit. TRK's interim table (`Sets/TRK.ratings.json`) is this method with FRA's medians.

## TRK Quick Draft on the interim ratings: the bots' colours (2026-11-07, MC-5.4.5)

Ten drafts (`--draft --set=trk`, seeds 1–10), 80 bot seats, rating (TRK's INTERIM table) plus the colour lean: White
39, Blue 36, Black 34, Red 30, Green 21 seats (an even spread is 32 each) — far flatter than FRA on 17lands (Blue 55,
Red 15 of 80, LIM-2.2), as a table that is mostly rarity gives no colour a lead. A seat averages 30.0 of its 42 picks
in its colours, 5.7 outside, the rest colourless.

## Self-play ratings (DATA-2) checked on FRA against 17lands (2026-11-07, MC-5.4.6)

`--sim-ratings=fra`: each card's games-in-hand win rate over AI-vs-AI sealed games — two AI sealed decks a game
(`SealedBuilder.Opponent`, built on FRA's 17lands ratings), a card "in hand" if a copy left its library — pulled to
the mean below 200 games, as `CardRatings` does. Spearman's ρ against 17lands' GIH WR, beside the interim method
(rarity median ± the CardValues rank) on the same cards:

| self-play | cards (games in hand) | ρ self-play | ρ interim |
|---|---|---|---|
| GreedyAi, 10 000 games (17 min, 243 906 card-games) | 279 (all) | 0.10 | 0.41 |
| | 189 (≥ 50) | 0.15 | 0.52 |
| | 158 (≥ 200) | 0.20 | 0.56 |

**GreedyAi's self-play loses to the interim table everywhere.** Its worst misses are blue value and control cards it
can't play — Fblthp, Impossibly Lost (the lowest of all: 38.6% over 2 265 games; 17lands 59.9%), Austere Command, Jace's
Machinations, Jace, Reality Sculptor, Sublime Epiphany, Seasoned Cryomancer, Verdant Kraken: the rate measures the
AI's skill with a card as much as the card. 32 FRA cards never reached a game in hand (the builder rarely plays them).

The search (beam), 500 games before the 2-hour background limit stopped the run (59 min; 12 000 card-games): ρ 0.03 on
all 279, **0.11 on the 74 with ≥ 50 games in hand (interim 0.42)**, 0.14 on the 17 with ≥ 200. Thin, but the same way
as greedy. **Verdict (MC-5.4): TRK keeps the interim table.** Both AIs' worst misses are the walkers and blue value
cards; the walker games below say why.

## FRA's planeswalkers in self-play: why they read low (2026-11-07, MC-5.4)

`--card-games=planeswalkers`: 32 games of the search on both sides, 2 lost and 2 won per walker, each one its walker was
drawn in (CHOSEN by result, so the counts are kinds, not rates), traced with `--record=FILE --card=NAME`. Of the 32:
cast 16 (abilities used, + and −, often several times); drawn in the last 1–2 turns 6; never castable 4 (greedy
three-colour builds with a thin double pip — Ajani Unrelenting {4}{R}{R} on 5 Mountains, Chandra, Chill of Compliance
{1}{U}{U} on 6 Islands); **discarded 3 — The Theorist, Jace Beleren to Rank Rat (below Protege's Awakening and Hapatra:
the position AI-9.3 keeps), Jace, Reality Sculptor over Tam's Resistance**, Ajani Unrelenting pitched to a flashback
while uncastable; held while castable 3 (2 in games being won; Ajani Resolute in a loss); never in hand 2 (milled — the
rating proxy's flaw, DATA-2). **The price behind the discards:** a card in hand is priced by the card-value sandbox, the
cast turn alone — 7 of the 8 walkers 2.7–3.75 (Ajani Resolute 2.7, the lowest card in FRA; Garruk, Curse Breaker 8.15,
his abilities making tokens), against a median of 8.0. On the battlefield, `Evaluator.Walker`: half of ONE activation.
Neither is a lifetime (AI-9.3). And a scoring cliff, harmless where seen: a line the rollout wins scores ~9 000, any
other its position, so a slow walker loses to a kill in sight (Garruk, Veiled Butcher held three turns while winning).

## TRK self-play ratings, two rounds, and do they build better decks? (2026-11-10, DATA-2.4–2.6)

The search on both sides (`--sim-ratings=trk --ai=beam`), Shayne's halved plan: round 1, 1,000 games on decks built
from TRK's interim table; round 2, 1,000 games on decks built from round 1's ratings (`--built-on`); a game past 5 min
abandoned and its board logged (57 over both rounds and the reruns, ~3–4% — Troubling Tribbles boards and long late
games; AI-21). Cards with games in hand: round 1 281 (median 34; ≥ 100: 57; ≥ 200: 24), round 2 219 (median 41; ≥ 100:
63; ≥ 200: 28).

**The interim table rates by cost; self-play doesn't.** Rank correlation with mana value, cards with ≥ 100 games:
interim **+0.64**, round 1 **−0.13**. Mean rating by mana value 1→6: interim 56.6 → 58.5% (climbing); round 1 50.0,
51.9, 51.6, 49.2, 51.1, 48.4% (2–3 best, 6 worst). Round 1's ranking against the interim's: ρ −0.01. Top of round 1:
Common Goal 59.3% (interim #92), Xindi Surveyors 57.4% (#163), Dominion Supervisor, Fugitive Listener (#179), Vexing
Drone (#191), Xenobotanist (#200). Bottom: Suus Mahna Practitioner 41.7%, Humpback Whales 43.1% (#84), Emergency
Holographic Crew 43.1%, Cold-Blooded Crew 46.7% (#78), Constable Odo 48.1% (#81). One card over ~150 games is ±4 points;
the lean by cost holds over many cards.

**But decks built on round 2's ratings are no better** (`--ratings-match=trk`, 300 games, pools in pairs each built on
both tables, 0 pairs unchanged): **49.6% ± 2.9pp** (1 SE) over 141 pairs, 9 pairs dropped (a stopped game). The
2-SE bar (> 50% + 5.8pp) not met; TRK keeps the interim table. The match can't see a gap under ~6 points.

Readings, not yet tested: most cards' ratings are still pulled to the mean (median ~40 games of the 200 the pull asks),
so round 2's builds differ from the interim's mostly by noise among the many unmeasured cards; the builder takes the
best 23 by rating with no curve (LIM-1, DATA-2's Why), so a cheaper rating table alone may not make a faster deck; and
both sides are the same AI, which may not press an aggressive deck's edge the way Shayne does.

## Shayne's TRK sealed lists, tested by simulation (2026-11-10, LIM-1.12)

`--builds=… --both-seats` (BuildReport; the procedure is the `/sealed-test` skill): each list against the
same AI sealed opponents, every opponent once on the play and once on the draw, the search on both sides.
± is one standard error; "paired" is the game-by-game difference (same opponent, seat and shuffles).

**Pool 1 (aggro).** RB 60.2% ± 5.1 (play 67.4, draw 53.2) against BG-r 54.1% ± 5.4, paired +5.0 ± 6.1 over
50 opponents. Shayne played RB and went **6-3 on Arena** (3-2 first). His cuts, from the report: Tea, Earl
Grey, Hot (IWD −20: tempo for 2 damage) and Cleveland Booker (−12), for a second Captain's Tutelage. The 40th
card three ways, 100 opponents: **Cha'DIch Investigator 60.3% ± 3.5** against Tal Shiar Analyst 54.9 (paired
+5.8 ± 2.3) and Artificial Anomaly as a 17th land 54.9 (+5.7 ± 2.8); Analyst − Anomaly 0.0 ± 2.6. The Analyst's
number is partly the AI's misplay (it pings in its own upkeep — AI-17.25).

**Pool 2 (GB soup, splashing).** Against the AI field, 50 opponents unless said:

| Test | Result |
|---|---|
| Riker's slot: Will Riker / Lore / a second Moopsy | 51.5 / 53.1 / 47.9%; Riker − Lore −2.1 ± 4.0, Riker − Moopsy +3.2 ± 3.2 |
| All Good Things → Lore, beside Riker | +1.0 ± 3.1 (no difference) |
| "Soup-Red": −Island −Swamp −All Good Things −Guidance Failure, +Forest +Mountain +Cha'DIch Investigator +Plasma Cascade (75 opponents) | +2.2 ± 5.2 (four changes, land counts among them — the pairing is lost) |
| 5 Swamps / 4 Forests instead of 4 / 5 | −1.1 ± 3.7 (no difference) |
| **The City on the Edge of Forever → All Good Things** | **+6.3 ± 2.9** — the final list, 59.4% |

**Against his own RB list** (`--opponent`, 69 games): the soup won **36% ± 6** (on the draw 22–32%) — aggro is
its weak matchup, and RB looks above the AI field: RB beat the AI decks 60%, the soup 64%.

**What reads true and what doesn't:**
- **One card strongly positive in every run is real:** Will Riker's IWD +26, +22, +18. Confirmed at the table (Shayne, 2026-11-11, Arena, the soup with Riker): "absolutely nuts. Just winning every game i've played him and not even close" — a card he says he wouldn't have played without the runs. One run's IWD is not:
  Lore +28 then −7, Adversary by Algorithm +40 then −13.5 — at ~100 games a card's IWD swings ±10–20.
- **A one-card swap overstates its size** (+5.8 for a card in hand in ~37% of games): the AI's imagined
  library differs between the lists too. The direction holds.
- **Cards the AI misplays read low**: Borg Cube negative in every run, and never cast in four self-play
  games where it was drawn (AI-19.22); The City (a phasing Saga) −5, −12, −21.
- **A repeat is consistent**: Soup-Riker 51.5% then 52.5% against the same 50 opponents.

