# Draft training — measured results

Not loaded into context. Read when a change touches what these runs measured;
the live rules that came out of them are in `.claude/rules/`.

## Measured: Hollowmere (HLM), 300 cards

> **HLM IS RETIRED.** Every Hollowmere number in this file is a historical record of a pool that no
> longer exists — the set, its tests and its trained model were deleted. The METHOD still applies;
> the figures cannot be reproduced. DES is now CSC + CMB.


600 drafts, 16 800 games, 33 600 deck-games, ~112 deck-games per card (the Legacy model has
410/card off the same run size — pair and card density both fall as the pool grows). Evaluated
over 72 games at 9 seats:

| Picker | Win rate |
|---|---|
| Trained | **75.0%** |
| Curve | 37.5% |
| Random | 37.5% |

Curve does not underperform Random here as it does on the Legacy pool, because Hollowmere's
expensive cards are genuinely castable via the reanimation package rather than being traps.

## Measured: Core Set Cube (CSC), 134 cards

300 drafts, 8 400 games, 16 800 deck-games, **median 1 445 games per card** — an order of
magnitude denser than Hollowmere off half the drafts, purely because the pool is 134 cards rather
than 300. Evaluated over 72 games at 9 seats:

| Picker | Win rate |
|---|---|
| Trained | **85.4%** |
| Random | 39.6% |
| Curve | 25.0% |

Zero draws in evaluation; all 72 games ended by damage.

**13% of training games were flagged, all `TimeLimitReached`, and this is a harness artifact, not
a game bug.** Training plays its games in one parallel batch, so each game gets a fraction of a
core and the 5 000 ms limit is wall-clock. The same decks run sequentially draw 0–2%. Diagnose the
difference by the reason: `TimeLimitReached` under parallel load is expected, whereas
`ActionLimitReached` would mean a genuine loop. Extra turns and counterspell traps — the two loop
risks in blue — produced no action-limit games at all.

**Do not record specific card values in this file — record the query that produces them.** A line
here once read "Wall of Frost tops the model at +16.1pp, 5.7pp clear of second"; after a balance
pass the file on disk had it at **+1.85, rank 167/408**, and the stale figure was used to argue a
design position a session later. Card values move double digits in a day, and prose in a document
loaded into every session is the worst possible place to cache them.

```
python -c "
import json; d=json.load(open('sim_results/draft_training_csc.json'))
prior=d['Wins']/d['Perspectives']; k=25
v=sorted((100*((c['Wins']+k*prior)/(c['Games']+k))-100*prior, c['Name']) for c in d['Cards'])
print(v[:10]); print(v[-10:])"
```

Check the model's mtime against `git log -1` before trusting it — one written before the last
balance commit is describing a game that no longer exists.

Key rules:
- **Picks are indices into `Seat.Offer`, never `Card` values.** `Card` is a record, so two copies of one template in a pack compare equal and picking by value would remove the wrong card.
- **Packs exclude lands** — `Draft.BuildDeck` supplies the mana base through `ManaBase.Build`: 23 spells + **17 lands** in a 40-card deck. See `Draft.DefaultMaxSpells` for why 17 rather than 13. It stamps `OwnerId`/`ControllerId`, so it must be called **per game**, not once per seat.
- **The deck is chosen inside one `ColorIdentity`, not taken in pick order.** Pick order alone is a five-colour pile — measured on 200 CSC drafts, 4.97 colours per deck and **20.9 of 23 cards uncastable**. `Draft.ChooseSpells` scores all fifteen identities by the pick equity they keep (`1 - index/poolSize` per card) multiplied by each card's CASTABILITY in the manabase that deck would really build. The multiplication is what makes it a real choice: a pair can only play more cards by splitting its sources. A shortfall PENALTY was tried first and forced mono — a mono deck's requirement can never exceed 22, so it scored zero penalty by construction while every pair paid 6-10.
- **A deck may SPLASH one extra colour, up to `Draft.MaxSplash` cards, WITH real mana support.** The
  splash is in the manabase — that is what separates it from filler — so every source it takes comes
  out of the main colours and the whole deck's castability pays for it. One extra colour only: a
  four-colour manabase would let the search rediscover the five-colour pile.
  **The curve decides what a splash costs, and no curve heuristic is coded.** It falls out of
  `ManaBase.SourcesNeeded`: a one-drop single pip wants 13 sources of 24 and a five-drop wants 9, so
  a cheap deck's own colours already claim the manabase. Measured pick position at which a deck stops
  taking the splash (`SplashEconomicsDiagnostic`): **cost 1 -> top 9 picks, cost 3 -> top 12, cost 5
  -> top 15.** An aggressive deck demands a near-first-pick bomb; an expensive deck will take the
  15th-best card in its pool. Measured over 200 CSC drafts, 3 of 200 decks end three-colour, each
  giving the splash 2-3 of 17 sources — the format has no fixing, so a splash gets the leftovers and
  has to be worth having at roughly 40% castability.
- **A short lane fills to 23 anyway, and the filler gets no sources.** An identity holding 19 playables used to yield a 19-spell deck with 21 lands; above ~18 lands almost any card beats another land. The remaining slots take the best picks left regardless of colour, and `BuildDeck` builds the manabase from the identity-legal CORE only, so filler cannot drag a 9/8 into an 8/7/2.
- **`DraftPickers.Trained` commits to a colour lane** (`DefaultLaneWeight = 2.0`), scoring a card +/- that many points for being inside the seat's two most-invested colours, ramped in over the first 10 picks. Commitment is measured by VALUE invested, not card count, so late filler cannot define the lane. **Measured 537-423, 55.9% +/- 1.6 over 960 games** against the same picker with the term off, drafting at the same table. Seats unable to field 23 playables in their own identity fall from **18% to 1%**, and colour concentration rises from 0.22 (0.20 is an even five-colour spread).
- **Selection lives in exactly one place.** `DraftTrainer.DeckSpellsOf` (which decides what the model counts) and Godot's `DeckListPopup` both call `Draft.ChooseSpells`; they used to carry their own copies of "first N non-lands", so colour-aware selection would have silently desynced the training data from the decks actually played.
- **`DraftPicker` is a delegate**, not an interface. The no-delegates serialization rule does not apply because draft state never enters a `GameState` — same reasoning as `DeckInfo.Builder`.
- **Human seats have no picker type.** The caller drives the loop and supplies that seat's index; `RunToCompletion` is for all-AI drafts only. This keeps all presentation (console, Godot) out of the library — a UI renders `Seats[i].Offer` / `.Pool` and needs no library change.
- **Determinism**: `Draft.Create(format, pool, seed, …)` consumes one `Random(seed)` in fixed seat order and fixes the entire draft. `ApplyPicks` and `RunToCompletion` are pure. Only `DraftPickers.Random` holds RNG; `DraftRunner` seeds it as `seed + 100 + seatIndex`, and games as `seed + 1000 + gameIndex * 5` (mirroring `SimulatorRunner`).
