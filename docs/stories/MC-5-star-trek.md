---
id: MC-5
title: Star Trek (TRK) plays in MagicCore — sealed and draft before Arena's day 1
status: ready
area: MC
created: 2026-10-30
---

## Why

Shayne, 2026-10-30: a Star Trek set comes out on Arena on **November 10, 2026** (prerelease November 6–12,
tabletop November 13) — "it might be worth implementing up front to get ahead of everything". Getting
ahead means: **"I can practice sealed and draft before Day 1 on arena, get a feel for all the cards and
stuff."**

Scryfall already lists the set (2026-10-30): code `trk`, 311 distinct cards (96 common, 110 uncommon,
80 rare, 25 mythic). New mechanics: Station (17 cards), Strange new worlds (13), Face a dilemma (8);
also Typecycling, Investigate and Kicker, which MagicCore may not have yet (grep MtgCore first). Most
keywords exist already (surveil, landcycling, cycling, treasure, mill, equip).

## Requirements

- [ ] TRK sealed and draft play on MagicTable before November 10 — the cards Shayne will open at the
      prerelease, scripted, as FRA's are (`/card-batch`)
- [ ] Every TRK card plays by Nov 10 — rares and mythics too (Shayne, 2026-10-31)
- [ ] A card's functionality is decoupled from its printings: a reprint is scripted once, its art,
      rarity and collector number are per set (Shayne, 2026-10-31 — sets and cubes alike)
- [ ] The draft bots pick by our own ratings: card win rates pre-simulated against bots (DATA-2), plus
      colour lean — there is no 17lands data before release (Shayne, 2026-10-31)

## Decisions

- 2026-10-30, Shayne: TRK goes into the CURRENT sprint (sprint 3), ahead of the cube (MC-4); "get ahead"
  is practising sealed and draft before Arena's day 1. Not the Commander product (TRC) unless asked.
- 2026-10-30, Shayne: not started in the session that planned it — that session finishes the AI fixes
  (AI-17.6); the survey is the first task of a fresh session.

- 2026-10-31, Shayne: survey first — Scryfall data, the new mechanics, how many cards today's pieces
  build — and the plan reviewed before anything is built. **Reprints are not re-implemented**: a card in
  two sets is one card (one script, one rules text); only its printing — art, rarity, collector number —
  differs by set.

- 2026-10-31, Shayne (MC-5.1's questions): **decouple a card's functionality from its printings, up
  front** — future sets and cubes face it too. **Every card by Nov 10, rares and mythics included** — the
  budget fits now that each batch is a fresh session (this set is the test of that). **Bot ratings are
  our own: card win rates pre-simulated against bots** (DATA-2's method), accepted as not fully accurate.

- 2026-10-31, Shayne: **Stardates (SDS) last, but still aimed at Nov 10** — 30 reprints (mythic,
  borderless, ~1 pack in 25 in place of a common), TRK's Special Guests. Until they play, that slot opens
  a common.

- 2026-10-31, Shayne (MC-5.5's questions): **each card in a deck carries its set** — "this matches how
  MTGA and MTGO work, where you can actually choose which art to use in your deck. In draft and sealed
  this would obviously just mean that it would be the art of whatever set you are drafting." **Rarity
  and collector number split out of `CardData`** (a `CardPrinting` per set). The registry is named
  **`CardRegistry`**.

- 2026-10-31, Shayne (MC-5.3 batch 1's questions): **V'Ger's "look at target opponent's hand" goes to the
  log for now**; the table showing known cards face up in the opponent's hand is its own story (TBL-6).
  **A modal activated ability is a real "choose one"** (Tea, Earl Grey, Hot), never two abilities with one
  cost: dilemma cards count mode choices.
  Shayne, the same day: the table showing a modal activation's modes as separate plays is fine, as long as
  dilemma counts it — so DILEMMA (MC-5.2) is raised where modes are chosen: the cast, a trigger's
  `ChooseModesChoice`, AND an activation (`ActivateAbilityAction.Modes`).

- 2026-10-31, Shayne (MC-5.2 Station's questions): **Zefram Cochrane waits** for the tier-3 one-offs (look
  at the top any time, cast an artifact from the top, Spacecraft enter with two more counters). **The AI
  values station progress by a simple formula, to iterate on** — "you want to put counters on your
  spacecraft, but you do need to account for opponent counter attacks if you tap your creatures." **The
  tapped creature's last-known power is a snapshot taken as it is tapped**: "shortcutting LKI … not worth
  a full engine overhaul for what is probably a very rare situation overall. If this situation comes up
  more in playing then we can reconsider doing full LKI" (noted in `DesignNotes.md`).

- 2026-10-31, Shayne (MC-5.3 batch 3's questions): **Worf's "tap two untapped artifacts and/or creatures"
  is a choice** — the player picks both. **"The rest on the bottom in any order" is a random order for
  now, with a note** (Spock, Always Your Friend; `DesignNotes.md`). **Pure Energy's distribution is the
  exact rule**: split as it is cast, so a second target gone illegal loses its counter.

- 2026-11-01, Shayne (MC-5.3 batch 4's questions): **batch 4 is three sessions** — 4a counters (any kind,
  quests), Sagas, Kicker, can't block, once each turn and the tier-2 options (28); 4b the old tags'
  one-offs (delayed return, leaves the graveyard, type change, second resolution, granted trigger,
  control, Brad Boimler, block and target restrictions — 17); 4c the 14 remaining one-offs. **A Saga's
  lore counter is added as your precombat main phase begins** (a turn-based action, CR 714.3b — no
  trigger). **Warp Speed: every land you control enters untapped** — its own "enters tapped", Root
  Maze's, a token created tapped and "onto the battlefield tapped" alike.

- 2026-11-01, Shayne (MC-5.3 batch 4b's question): **"one or more cards leave your graveyard" triggers once
  per priority pass** — everything that left since a player last had priority is one batch. Exact for
  cards leaving together; it undercounts only where the rules see two events before anyone has priority
  (one resolution moving cards out in two separate steps; a card cast from the graveyard while its cost
  exiles others from there, CR 601.2a vs 601.2h) — noted in `DesignNotes.md`.

- 2026-11-01, Shayne (MC-5.3 batch 4c's questions): **exile face down is real** — Vulcan Mind-Meld's cards
  are hidden from the player who didn't look (the table shows a back), not exiled face up. **Zefram
  Cochrane's "look at the top card any time" is done as V'Ger's look: the log says what your top card is**,
  with a note — showing it on the table goes with TBL-6. **Pet Sehlat declared alone just doesn't attack,
  with a note**; the table refusing that declaration is a backlog story (TBL-7).

- 2026-11-02, Shayne: **the rares and mythics come BEFORE the boosters** (MC-5.4) — "I would rather continue
  on with the rares and mythics first." Every card plays before a pack is opened; the plan was boosters
  first (they are sealed and draft's critical path, and a missing rare would play vanilla). The C/U finished
  four days ahead of plan, so both still fit before Nov 10.

- 2026-11-04, Shayne (MC-5.3 batch 5's questions): the re-tier from the full text gave 24 tier 1, 19 tier
  2 and 6 tier 3. **Batch 5 is two sessions**: 5a the 24 tier-1 cards and 16 of the tier-2 cards (40);
  5b Dr. McCoy, Geordi La Forge, Starfleet Academy and five tier-3 cards (Collation Trimatrix, Captain
  Picard: tap three or seven; Gul Dukat: life lost this turn; Seven of Nine: base P/T; Branching
  Timeline). Dominion Infiltration (X targets, changeling) joins the tier-3 one-offs. **Starfleet
  Academy's "{5}, {T}: add mana" is a REAL mana ability** — the payment solver learns mana abilities
  that cost mana (tier 3), not a stack ability.

- 2026-11-04, Shayne (batch 5a's review): **Sulu's "an Equipment or Spacecraft entered … this turn" counts
  one that has left again** — batch 5a read it from what is still there; fixed 2026-11-05
  (`SubtypeEnteredThisTurn`, `TurnTally.SubtypesEntered`). The sprint ends here
  (usage 98%, the reset tomorrow): what is unfinished moves to the next sprint.

- 2026-11-05, Shayne (sprint 4's plan): **the order stands with Arena on November 10** — every card,
  then the boosters (MC-5.4), then the Stardates. **The tier-3 one-offs go ~14 a batch**, each a fresh session.

- 2026-11-05, Shayne (batch 5b's question): **"tap three / seven" is the player's choice of every one, one
  legal action per set** (as Worf's two) — the easiest to build: "there is a lot of tapping creatures in
  this set (station as well), which has ballooning choice combinations … we might want to have a new
  story which looks into this and any potential optimizations" → AI-18. Claude's designs, taken as
  proposed: Geordi's doubling AFTER Brad Boimler's plus one ((n + 1) × 2, always the better order for its
  controller); Starfleet Academy by auto-payment only (tapping it by hand gives {C}, `DesignNotes.md`);
  life loss in ONE path (two writers, not three: paying life already went through `LoseLife`).

- 2026-11-05, Shayne (batch 6a's questions): the 57 re-tiered from the full text — 11 are tier 1–2 (Sela,
  Antagonist Incorporator, Deanna Troi, Enterprise-D, The Inner Light, Will Riker, M'Benga, Deep Space 9,
  Captain Pike, Dathon, Mirror Universe Intrusion). **Batch 6a: the 10 shocklands, Engineered Explosives,
  Dominion Infiltration, Nyota Uhura, Mirror Universe Intrusion** — Nyota shares the as-it-enters choice and
  "mana value equal to a number", Mirror Universe the token copy with a keyword. **A shockland's "pay 2 life"
  is the search's to decide** (both answers are beam nodes; it pays when the turn's plan spends the mana).
  **Sunburst: the player picks X, the payment spends as many different colours as it can**; the counters
  are the colours actually spent. **Nyota's AI names the mana value most common among the opponent's cards
  it knows of, else 2.** Claude's designs, taken as proposed: changeling answered per question
  (`HasSubtype`), never a list of every type. **X targets: one cast per X, the targets picked as it is cast**
  (Shayne, the same day) — one cast per SET was built first and hung the fuzz probe (2^n against n creatures).
- 2026-11-05, Shayne (batch 6a's review): **every card implemented first; each card's AI use and rulings
  are optimized after** — Engineered Explosives should pick its X for the most appropriate kills in play,
  noted in the backlog story AI-19 (card-by-card play and rulings), where later batches add theirs.
- 2026-11-05, Shayne (batch 6b's questions): the ten re-tiered from the full text — 3 tier 1, 5 tier 2, 2 tier 3
  (Enterprise-D's "charge counters put on it for the first time each turn", Dathon's "another counter of that
  kind") — **all ten built as designed**: charge counters raise `CountersPutEvent` (`Trigger.Counters` the kind),
  "first time each turn" is `OncePerTurn` (the trigger is only about itself), Dathon's several kinds a
  `ChooseKeywordChoice`; Deep Space 9 a Spacecraft never a creature (`StationAbility` alone).
  The ten came in cheap (2.8M), so three tier-3 one-offs were proposed and **Shayne added all three**: Mr. Spock
  (draws doubled in `Draw`), Georgiou (no loss at 0 life, in the state-based check), Miles O'Brien (a
  permanent returned to hand this turn, a tally). Kobayashi Maru (modes not chosen before) waits for the one-offs.
- 2026-11-06, Shayne (batch 7a's questions): the 15 re-tiered from the full text — every one needs a new piece
  (13 in all). **Q, Stacking the Deck moves to 7b** (an ability gained by an exiled card, activated from exile
  with an exile-from-hand cost, a copy cast paying its cost — three parts). **Lwaxana Troi's "you may choose not
  to untap" is a real yes/no in the untap step** (asked while her link is live), not "stays tapped while linked".
  **Scotty's granted beam me up stays one action per spell × creature returned** (~88 on a 12-creature board with
  8 spells — linear per card, as every beam-me-up card is today).
- 2026-11-06, Shayne (batch 7b's question): the 16 re-tiered from the full text — Gwyndala tier 2, the other 15
  tier 3; five flagged bigger than one small piece. **Batch 7b is the 11 smaller ones** (Gwyndala, Genesis Device,
  Kobayashi Maru, Quark, Kira Nerys, All Good Things..., Peace in Our Time, The Needs of the Many, Data, Rok-Tahk,
  Janeway); **7c the five flagged** — Q (three parts), Nog (types, a trigger and statics gained for good, lands from
  the graveyard), H.M.S. Bounty (an extra turn: `TurnStructure` and the rollout), The City on the Edge of Forever
  (phasing as a hidden zone), Kai Winn (a name chosen as it enters, an "ability activated" event). Claude's
  defaults, built as proposed and noted (`DesignNotes.md`, AI-19) — Shayne answered only the split, so confirm at
  review: Kira's creature attacks the opponent, never a planeswalker; All Good Things IV with nothing exiled gains
  nothing; Data's counters are three picks after activating (364 sets on 12 creatures), the kind a rule.
- 2026-11-06, Shayne (batch 7c's questions): all five tier 3, one session (Nog, Q, Kai, Bounty, City last).
  **Kai Winn triggers on MANA abilities too** — "It does not say non mana abilities. If you name Llanowar Elves
  and they tap it for mana it would trigger her." **Phased-out permanents stay visible on the table, clearly
  marked as phased out.** Kai's name list: every nonland name printed in the game's sets.
- 2026-11-06, Shayne (after batch 7c): **Kai Winn's AI names step by step in limited** — an activated ability in
  play, else a known hand card, else a high win-rate common in a colour their lands show (AI-19.14, built). **The
  Stardates (MC-5.6) come NEXT, before the boosters** — "I would rather finish up the special guests". The
  collation slot for them is MC-5.4's work; until then nothing opens them.
- 2026-11-06, Shayne (MC-5.6's survey): **composition first.** The survey had tiered 13 Stardates as new
  pieces from catalog greps; most were an option on a piece that already exists (Sisay's mana value bound,
  Phyrexian Tower's sacrifice cost, Isshin beside Crusher's one-event flag). "If we make every slight
  alternative of an ability a brand new effect, we get effect ballooning, bugs, harder to maintain, and harder
  to create new cards." So: **the audit first (MC-6, written), then MC-5.6 fixes the families its cards touch
  (MC-6.1–6.8, part of 6.9), then MC-6 does the rest after MC-5.4.** Phyrexian Tower's sacrifice is never
  auto-paid; the AI gets it as a move. Cycling stays a keyword ("when you cycle" is about cycling, landcycling
  included); islandwalk is the existing unblockable under a condition. TRK.json re-imported with the 30
  guests (`GUESTS["trk"] = "set:sds"`); Scryfall's own fixes came with it (Jett Reno's "+1/+1"; Stray from
  History and Test of Identity list their mode names as keywords), kept.

- 2026-11-06, Shayne (MC-5.6 batch 1's shapes): **Isshin doubles "whenever you attack" too** — "that is what the
  card does" — so its cause is both `Attacked` and `AttackersDeclared`. Lwaxana's link is "until she untaps"
  (`TriggersAdditionalTime.UntilUntapped`), a duration, not an `If` (Claude's, explained; nothing she does changed).
  **Phyrexian Tower's sacrifice is `TapForManaAction.SacrificeId`**, one legal move per creature, its mana floating. Auto-pass stops while it can be activated — left as is (Shayne:
  "a niche card that I don't expect to be played in limited that much").
- 2026-11-07, Shayne (MC-5.6 batch 3's survey): **the six split 3a / 3b** — 3a Riku, Prosper, Drivnod (modes
  counted, play from exile, Phyrexian mana, keyword counters, a three-card exile cost); 3b Sen Triplets, Flare of
  Fortitude, Mogis (MC-6.15's rule statics with Yuriko moved, MC-6.13's type change with Puppet Crafting moved,
  devotion, an alternative cost). Claude's defaults, proposed and taken: `{B/P}` is a real choice (one move per number
  paid with life); Drivnod's three cards are picked one at a time after activating; Sen's lock includes mana
  abilities ("activate abilities", as Kai Winn's ruling); Sen's permission is their whole hand this turn, not a stamp.
- 2026-11-07, Shayne (MC-5.6 batch 3b's shapes, re-checked against the code): taken as Claude proposed — Sen's
  mana lock in `ManaPayment.Find` too (`TapForManaAction` is only the table's manual tap); `MayPlayFrom` keeps the
  owner it resolved to (`ThisTurnEffect.Bound`); a card drawn into the revealed hand later isn't marked known (a
  ceiling); paying life one question, `Life.CanPay`, Hive Mind Coprocessor moved onto it (its `Always()` let a player
  pay at 0 or below); a free cast WITH a cost offered beside the paid one; `Animated.SourceId`, `Count.Colors`.
  **`RandomPlay_EveryScriptedCard(21)` is skipped, the reason in the test** — its new decks double Troubling
  Tribbles and every step is ~n³ in permanents; "ideally … group permanents that are exactly the same together when
  selecting choices" goes on the backlog (AI-18).

- 2026-11-07, Shayne (MC-5.4, run OVERNIGHT, unattended — sealed and maybe draft by morning): **if MTGJSON
  still has no TRK booster data, build a provisional collation from Wizards' published slot odds** (the
  Stardates' slot included) without waiting, every assumption recorded for his review; **the rating run may
  run unattended, any length**; **commit each finished step after `/pre-commit`** (never push); **fetch
  TRK's art from Scryfall**. Claude's default, in the prompt: sealed and draft first on interim ratings,
  DATA-2's real ratings last, swapped in only if they rank FRA acceptably against 17lands.
- 2026-11-07, Claude (MC-5.4.1, on Shayne's permission above — **for review**): MTGJSON still has no TRK booster
  data (checked 2026-11-07: TRK.json.gz has 586 cards, no `booster`), so **`Sets/TRK.boosters.json` is PROVISIONAL,
  built by `tools/build_trk_boosters.py`** from Wizards' Play Booster section of
  https://magic.wizards.com/en/news/feature/collecting-star-trek. Wizards' slots, each a sheet:
  - **7 commons** (`common`, the 4-colour rule as FRA's) — 81 main-set commons 97.1%, the 1 Klingon-frame common 2.9%;
    **"in 1 out of 25 Play Boosters, a Stardates card will replace a common"**: 6 commons + 1 Stardate (`specialGuest`)
  - **3 uncommons** — main 78.1%, the 10 scene cards 7.7%, 7 LCARS 4.6%, 5 classic *Enterprise* 4.6%, 6 Klingon 5%
  - **1 wildcard** — main C 20.8 / U 54.1 / R 16.7 / M 2.6%, Klingon U 1%, scene U 1.5%, LCARS U 1%, the rest "<1%"
  - **1 rare or mythic** — main R 80.1 / M 12.4%, scene R 1%, LCARS R 1.8%, *Enterprise* R 1.5%, borderless land R 1%,
    LCARS shock land 1.4%, the rest "<1%"
  - **1 traditional foil** (Arena: a second non-foil wildcard, as FRA) — main C 51.2 / U 34.7 / R 6.3 / M 1%, Klingon U
    1%, scene U 1.6%, LCARS U 1.1%, the rest "<1%"
  - **1 land** — a common dual (the 10 Planets) 50% (non-foil 40 + foil 10), a basic 50% (default, holodeck, away team)
  Claude's assumptions, each for review: **a treatment is the same card** — its odds folded onto the name, as FRA's
  import folds them (so a card with a treatment opens more often: Worf, Honor-Bound Warrior, LCARS and Klingon, at ~3×
  a plain uncommon); **which printing is which treatment** is read from Scryfall's collector-number runs (scene and
  borderless lands 332–362, LCARS and its shocklands 363–401, *Enterprise* 402–425, Klingon 426–435) — they reproduce
  every count Wizards gives (1 Klingon common; 10 / 7 / 5 / 6 uncommons), and the script stops if they ever don't;
  **"less than 1%"** shares evenly what a slot's stated odds leave of 100%; **within a group every card is equally
  likely**, the 30 Stardates too; Arena's TRK packs mirror paper (Wizards publishes nothing for Arena).

- 2026-11-07, Claude (MC-5.4.3 — **Claude's default, for review**): **TRK's ratings are INTERIM** —
  `Sets/TRK.ratings.json` (`MagicConsole --interim-ratings=trk`, `CardRatings.Interim`): each card at its rarity's
  median FRA 17lands GIH WR, moved ±0.02 by its measured `CardValues` rank among its rarity (an unmeasured card at
  the median). Checked on FRA: ρ 0.41 against 17lands, the rarity alone 0.25, the values alone 0.21
  (`docs/findings/limited.md`). `CardRatings.Of` reads a set's `<SET>.ratings.json` before its 17lands file; the
  basics are unrated (the bots take them last; the builder adds them free).

- 2026-11-07, Claude (MC-5.4.4 — **Claude's defaults, for review**): **the deck select's Set button** (one button that
  steps FRA → TRK, not a dropdown: a popup's rect is in window pixels) chooses what Sealed and Draft open
  (`DeckSelectScene.LimitedSet`; `--set=TRK` for captures); a sealed or draft session carries its set
  (`SealedSession.Set`, `DraftSession.Set`), so a session of the other set is replaced, never mixed. **A pasted Arena
  pool reads as that set's cards** (`ArenaDeck.ReadPool(text, set)` — a TRK prerelease pool pastes in); a deck export
  (`--builds`, the deck select's paste) is still FRA's. The console's `--compare`/`--draft` take `--set=trk`.

Open questions:
- Booster collation: provisional (above) until MTGJSON has TRK's — then `import_mtgjson_boosters.py trk` replaces it.

## Tasks

- [x] MC-5.1 The survey (a fresh session): import TRK from Scryfall (`tools/import_scryfall_set.py`), tier
      every card by what the engine needs, the new mechanics' rules read in full, a batch plan sized to
      reach sealed and draft by November 10
- [x] MC-5.2 The new mechanics (Station, Strange new worlds, Face a dilemma, and whatever the survey finds)
      — Station done 2026-10-31 (batch 2): `TrkCards.Station`, a creature at its threshold read live by
      `Card.Is`, the tapped creature a real choice (`ActivateAbilityAction.TapId`), the AI's station term.
      Batch 3 (2026-10-31): beam me up (`BeamMeUpCost`), strange new worlds (`Count.DistinctNamesOf`), face
      a dilemma (`FacedDilemmaEvent`, three places), power 8 (`Trigger.While`, `Count.TotalPowerOf`), becomes
      tapped (`MagicState.Tap`, all six tap paths), look and take (`LookAndTake`), Federation
      (`Count.CreatureTypesAmong`); "tap two" is now the player's choice (Worf — Tenured Tethermage too).
      Batch 4a (2026-11-01): counters of any kind (`Card.Counters`, `MagicState.PutCounters`, `Is.WithAnyCounter`),
      quests, Sagas (`SagaComponent`, `Scripting.Chapter`), kicker (`KickerComponent`), can't block
      (`Combat.CantBlock`), once each turn (`ActivatedAbilityComponent.OncePerTurn`). Batch 4b (2026-11-01):
      delayed return (`Flicker.AtNextEndStep`, `AtNextEndStep`), leaves the graveyard (`LeftGraveyardEvent`,
      `Trigger.OneOrMore`), type change (`s.Subtypes`), the second resolution (`IfNthResolutionThisTurn`),
      granted triggers (`GrantsTriggeredAbility`), gain control (`MagicState.GainControl`), every counter
      "plus one" (`Replacements.Counters`), cast from your graveyard (`MayCastFromYourGraveyard`),
      `CantBeBlockedBy`, `CantBeTargetedByAbilitiesFrom`. Batch 4c (2026-11-01): exile face down
      (`ExiledFaceDown`, `Card.IsHiddenFrom`, a back on the table), playing an opponent's exiled card
      (`ExileTopPlayable.Of/By/Forever/Choose/AnyType`, `PlayableFromExile.Tax`), one destroy path
      (`MagicState.Destroy`, Dr. McCoy's replacement), a draw event (`DrewCardEvent`), can't attack alone,
      lands enter untapped, Spacecraft enter with counters, a mana restriction knowing the ability's source
      (`ManaRestriction.OnlyArtifacts`), the first spell this game, a this-turn trigger on one object
      (`WheneverThisTurn.About`, `TheObject`), `Is.WithAttached`, an artifact cast from the library's top. The rest came with MC-5.3's batches
      5–7c (its list below); done 2026-11-06, every TRK card playing
- [x] MC-5.3 Commons and uncommons in large batches, then rares and mythics — batch 1 done 2026-10-31:
      the 88 tier 0–1 C/U not waiting on STATION or ANY-COUNTER (re-tiered: 4 tier 3, 6 tier 2, list in
      `TrkCoverage.md`); new pieces: modal activated abilities, `PutSelfOnBottomCost`, `AddManaOfAnyOneColor`,
      `LookAtHand`, `Count.Plus`; FraCards' shared helpers moved to `Sets/Scripting.cs`. Batch 2 done
      2026-10-31: the 12 STATION C/U waiting on nothing else (Stray from History re-tiered to 2); new
      options: `Is.Tapped`, `RevealUntilOntoBattlefield.HowMany/Tapped`, a spell's cost change seeing its
      targets. Batch 3 done 2026-10-31: the 31 C/U on batch 3's mechanics (Hemmer and Rutherford in);
      new options: `ExileTopPlayable.ThisTurn` (Kirk), `TargetChosen` (Pure Energy's split as cast),
      `ChooseAmongChoice.Filter`. Batch 4a done 2026-11-01: 28 C/U on the counter map, quests, Sagas, kicker,
      can't block and once each turn, with the tier-2 options (`ExileTopPlayable.N`, `SacrificeAtNextEndStep`,
      `CreateTokenCopy.NotLegendary`, `AddManaOfAnyOneColor.Colors`, `SearchLibrary.Who`, `Is.Named`,
      `ReturnFromYourGraveyard`, `Trigger.CombatDamageBy`, `Exile.Linked` + `ReturnLinkedExile`,
      `WheneverThisTurn`, `ShuffleIntoLibrary.FromGraveyards`). Batch 4b done 2026-11-01: the old tags' 17
      one-offs, with the options `ExileTopPlayable.UntilNonland`, `CreateTokenCopy.OfAnExiledCard`, `GainLife`
      counting a `Count`; `Threshold` moved to `Scripting.cs`. Batch 4c
      done 2026-11-01: the last 14 C/U (every common and uncommon now plays, 206/311), with the options
      `MayEnterAsCopy.Of/NotLegendary/WithCounters/Optional`, `ReturnFromYourGraveyard.To`, `Both`,
      `YouChooseTheyDiscard.Optional/OwnerMayPlayTaxed`. Batch 5a done 2026-11-04: 40 tier 1–2 rares and
      mythics (246/311), with the options `CreateTokenCopy.Tapped`, `ArtifactTokensBecome.AnyToken`,
      `YouChooseTheyDiscard.Then`, `LookAndTake.OntoBattlefieldTapped`, `TriggeredAbilityComponent.ChooseAnyNumberIf`,
      `MayCastFromExile`, `NoMaximumHandSizeUntilYourNextTurn`, `Count.LifeTotalOf`/`AllCountersOn`, an
      activation's event card the creature sacrificed, `TrkCards.EntersUntapped` (ENTERS-UNTAPPED); and a fix:
      a dying permanent's own "whenever a creature dies" now sees its own death (CR 603.10a). Batch 5b done
      2026-11-05: 8 tier-3 rares and mythics (254/311) — `AtNextEndStep.On` (a delayed trigger on the card
      that died, read in its graveyard), `ChargeCountersOnYourArtifactsDoubled`, `ManaOfOneColorComponent.Cost`
      (the solver's `WithCostlyMana`), tap three or more (`CostSlot.TapSet`, `ActivateAbilityAction.TapIds`),
      `Rules.Life.Lose` and `TurnTally.LifeLost`, `ModifyUntilEndOfTurn.ForGood`,
      `RevealUntilOntoBattlefield.Top`, `SearchLibrary.ChargeCounters`. Batch 6a done 2026-11-05: 14 tier-3
      one-offs (268/311) — the shocklands (`EntersTappedComponent.UnlessYouPayLife`), sunburst (`Sunburst`,
      `ManaPayment.Find(mostColors)`, `OnStackComponent.ColorsSpent`), `Ref.ManaValueIs`, X targets
      (`TargetSpec.PerX`, `ChooseXTargetsChoice`, `Ref.EachTarget`), changeling (`s.HasSubtype`), a number chosen as it enters
      (`ChoosesNumberAsItEnters`, `Is.ManaValueIsChosenNumber`, the AI's rule in `GreedyAi`). Batch 6b done 2026-11-05:
      the re-tier's ten (278/311) — `MayPlayLandsFromLibraryTop`, `CountersOnYourCreaturesDoubled`
      (`Replacements.Doubled<T>`), charge counters put (`Trigger.Counters`), `PlayerDealtDamageEvent.Amount` and
      `WheneverThisTurn.About` on combat damage, `AddAnotherCounterOfAKind`, `RevealUntilOntoBattlefield.Exile`
      / `SacrificeAtNextEndStep`, `TrkCards.StationAbility`; then three tier-3 one-offs (281/311) — `DrawsDoubled`
      (Spock), `DontLoseForZeroLife` (Georgiou), `TurnTally.PermanentsReturnedToHand` (Miles O'Brien); the fuzz
      probe ends a game past 300 permanents (Troubling Tribbles doubling hung it). Batch 7a done 2026-11-06: the
      grouped tier 3 (295/311) — trigger twice (`LifeGainTriggersAdditionalTime`, `TriggersAdditionalTimeWhileTapped`,
      `Triggers.AdditionalTimes`), may choose not to untap (`MayChooseNotToUntap`, `TurnStructure.FinishUntap`),
      assimilate (`Assimilate`, `AssimilateExiledWithSource`), control until end of turn (`GainControl.UntilEndOfTurn`,
      `ModifyUntilEndOfTurn.AlsoCreatureType`), `AtBeginningOfNextCombat` (a permanent's delayed trigger with its own
      controller), dilemmas this turn (`FacedDilemmaEvent.Face`, `Count.DilemmasFacedThisTurnBy`),
      `ExileUntilNonlandCastFree` (The Caretaker, discover), `ExileTopAndCastFree` (Sisko), `CastCopiesOf` (Infinite
      Diversity), `GraveyardSpellsHaveBeamMeUp` (Scotty), `MayPlayFromYourGraveyard` (Submatrix, Holodeck),
      `ExileSpell` + `CastFromHandFree` (Brazen Bluff), `MayDiscard.Exile`. Batch 7b done 2026-11-06: 11 one-offs
      (306/311) — modes not chosen before (`TriggeredAbilityComponent.ModesNotChosenBefore`, `Card.ModesChosen`),
      `LoseTheGame`, can't sacrifice Treasures (`MagicState.CanSacrifice`), enters attacking
      (`YourCreaturesEnterAttacking`), a granted mana ability (`YourCreaturesTapForMana`), explore (`Explore`), an
      extra land each turn (`MagicState.LandDrops`), a player's hexproof and prevented damage (`YouHaveHexproof`,
      `PreventAllDamageToYou`), "that player may" (`Ref.ActivePlayer`, `May.ThenAsThem`, `CantAttackThisTurn`),
      counters removed from among (`RemoveCountersFromAmongCost`), `CreateTokens.AttachTo`,
      `MaySacrifice.AnyNumber` + `Count.PermanentsChosen`, `MayDiscard.Must/Linked`,
      `ExiledWithSourceShareACardType`. Batch 7c done 2026-11-06: the last five (311/311 — TRK complete) — abilities
      gained for good (`UntilEndOfTurn.Gains`, `ModifyUntilEndOfTurn.BecomesCreatureTypes`,
      `MayPlayFromYourGraveyard.Unlimited`), an ability activated from exile (`From = ZoneType.Exile`,
      `ExileFromHandCost`, `CastCopiesOf.PayCost`, `ExileUntilNonlandCastFree.ShuffleFirst/Gains`), a name chosen as
      it enters (`ChoosesNameAsItEnters`, `Is.NamedChosenName`) and "activates an ability" — mana abilities too
      (`AbilityActivatedEvent`), an extra turn (`TakeExtraTurn`, `MagicGame.ExtraTurns`, `MagicPlayer.TurnBeganOn`
      for summoning sickness), phasing (`Rules.Phasing`, `ZoneType.PhasedOut`, `PermanentsCantPhaseIn`),
      `MaySacrifice.Must`
- [ ] MC-5.4 TRK boosters, sealed and draft on the table, bots without 17lands
- [x] MC-5.5 FIRST, before any card: a card's functionality decoupled from its printings — one registry
      by name across sets (one script per card; a test fails on a name scripted twice or a reprint whose
      rules text differs), printing data per set (art, rarity, collector number); `FraCards.Get`'s ~50
      call sites moved to it. Done 2026-10-31: `CardRegistry`, `CardPrinting`, `Card.Set`; boosters,
      ratings and drafts by set code; art in `Art/<set>/` first; `TrkCards` with the TRK tokens and the
      10 Planet duals; `TrkCardSheet.md`
- [x] MC-5.6 (Shayne, 2026-11-06: before MC-5.4), aimed at Nov 10: the 30 Stardates (SDS) — imported as TRK's
      guests (`GUESTS` in `tools/import_scryfall_set.py`, as FRA's SPG, story MC-3), scripted once each; their pack
      slot goes in with MC-5.4's collation. Imported and surveyed 2026-11-06 (TRK.json 341; none scripted
      before); built on the composed pieces of MC-6.1–6.8 (Next has the tiers and the batches). Batch 1 done
      2026-11-06: 17 (328/341) — the 9 tier 1, then MC-6.1–6.4 composed and their old cards moved: Isshin; Torbran,
      Bruvac; Rishkar, Talon Gates, Phyrexian Tower; Sisay, Weatherlight Captain, Loot, Exuberant Explorer.
      Batch 2 done 2026-11-06: 7 (335/341) on MC-6.5–6.8 and 6.9's permission — Mikaeus, Bristly Bill; Ertai
      Resurrected; Decree of Pain; Shizo, Thada Adel; Stella Lee (`SpellCastEvent.SpellNumber`). Batch 3a done
      2026-11-07: 3 (338/341) — Riku, Prosper, Drivnod. Batch 3b done 2026-11-07: the last 3 (341/341, every
      Stardate plays) — Sen Triplets, Flare of Fortitude, Mogis, on MC-6.15's `CantCastOrActivate` (Yuriko moved)
      and `LifeTotalCantChange`, 6.9's `MayPlayFrom`, 6.13's `BecomesType` (Puppet Crafting moved), devotion

## Next

**MC-5.6 is done (2026-11-07): 341/341 — every TRK card and every Stardate plays.** Batch 3b, the last three:
Sen Triplets (Yuriko's lock → `CantCastOrActivate { DuringCombat, ManaAbilitiesToo }`, on a permanent or a
`ThisTurn { On }` effect; `Combat.LockedDown(s, player)` asked by casting, activating, `TapForManaAction` and
`ManaPayment.Find`; `MayPlayFrom(zone, of)` read by `Playable`; the hand revealed by `LookAtHand`); Flare of Fortitude
(`MayCastFromHandFree(Is.Source) { Costs }`, a free cast with a cost offered beside the paid one; `LifeTotalCantChange`
in `Life.Gain`/`Lose`; paying life one question, `Life.CanPay` — Phyrexian mana, the shocklands, Necrodominance, Hive
Mind Coprocessor via `CanPayLife`); Mogis, God of Slaughter (`EnchantedBecomesCreature` → `BecomesType(scope) { Add,
Remove, Subtype, Power, Toughness, If }`, Puppet Crafting moved, stamped in `EnterBattlefield` too;
`Count.DevotionTo`; the punisher from `If`/`May { Otherwise }`). With the three taken out every golden game holds (161
random + AI, 620 per-card): no ruling changed. **Seed 21 of `RandomPlay_EveryScriptedCard` is skipped** (Shayne): its
new decks double Troubling Tribbles, and Scotty's and Station's one move per creature make each step ~n³ — the
profile also found `ManaAbilities.Granted` scanning the battlefield per source (`Find` quadratic), now listed once per
state (`Grants`). Grouping identical permanents in choices is AI-18's. Ceilings in `DesignNotes.md` (a permanent's lock
looked for in combat only; a card drawn into Sen's revealed hand; one type change per permanent). The review caught two
of the alternative cost's own, fixed with a test (no golden game moved): Omnipresence in hand read its own static as a
free cast for itself, and a flashback cast could also skip its mana cost (CR 118.9a) — the card's own is `What =
Is.Source` only, never with flashback. No AI-19 row. AI
bench: −10.2% (5 rounds, best 5.3 s vs 5.9 s). Probe: 150 games, none unfinished, all three played.

**Next: MC-5.4, TRK's boosters — the Stardates' slot included — in a NEW session, OVERNIGHT and unattended
(Shayne, 2026-11-07: Decisions); then MC-6's remaining families (after MC-5.4, MC-6's Next).** The prompt:

```
MC-5.4 OVERNIGHT: TRK sealed, then draft, playable on MagicTable by morning. Shayne is asleep — work
unattended on the permissions below; never stop to ask. Sprint 4 (docs/sprints/2026-11-05.md), item 5;
Arena's TRK release is Nov 10. Every TRK card and Stardate plays (341/341, MC-5.6 done 2026-11-07).

Permissions (Shayne, 2026-11-07 — MC-5's Decisions): build a provisional collation from Wizards' published
slot odds if MTGJSON still has none, every assumption recorded; long runs allowed, any length; COMMIT each
finished step after /pre-commit passes (never push, never --no-verify); fetch TRK's art from Scryfall
(official art first). Anything else that would need Shayne: take the conservative default, record it in
MC-5's Decisions as "Claude's default, for review", and carry on.

Read, in order and nothing more yet: docs/stories/MC-5-star-trek.md (Requirements, Decisions from 2026-11-06,
Next), docs/stories/LIM-1-sealed-draft.md and LIM-2-draft.md (how FRA's sealed and draft were built and put on
the table), docs/stories/DATA-2-engine-ratings.md, MagicCore/CLAUDE.md (the Limited/ rows), Commands.md (how
the table and console run sealed and draft). Grep before reading engine source; read only what you edit.

Steps, each finished = tests pass, /pre-commit, a commit whose subject starts "MC-5.4.n:" — in this order, so
the morning has the most playable thing:
1. Collation. Check MTGJSON for TRK booster data first (tools/import_mtgjson_boosters.py). Else build
   Sets/TRK.boosters.json from Wizards' "Collecting Star Trek" (magic.wizards.com) slot odds, the Stardates'
   slot included; the source URL, each slot and its odds in MC-5's Decisions. Boosters open TRK packs (a test
   on the slot counts and that a Stardate appears at about its rate).
2. Art: start the Scryfall fetch for TRK in the background (python tools/fetch_set_art.py trk; the Stardates too).
3. Interim ratings for TRK — fast, labelled INTERIM in the file and the story (the engine's card values where
   they exist, else by rarity), on CardRatings' scale — so the sealed builder and the bots have a table.
4. Sealed: TRK selectable for sealed on MagicTable and in the console — a pool, the suggested builds, an AI
   opponent's deck, a game played. Run the table through Run-Godot.ps1 and look at a screenshot.
5. Draft: TRK Quick Draft on the table, bots on the interim ratings plus colour lean.
6. Real ratings (DATA-2's method: card win rates pre-simulated by self-play, not 17lands). Check the method on
   FRA against Sets/FRA.17lands.json FIRST (the rank correlation, the worst misses, in docs/findings/); run
   it on TRK only if it beats the interim ratings on FRA; then swap TRK's table and re-check sealed and draft.
   Otherwise keep the interim table and write up why.

Traps: sim_results/ resolves against the working directory (root CLAUDE.md); a long run goes in the
background with its output in a file, polled by Monitor — never a foreground sleep; Git Bash has no pgrep;
many files are CRLF — edit with tools/edit.py, specs written with the Write tool. If a step fails twice the
same way, record it in MC-5's Next and move to the next step rather than looping.

End: MC-5's Next says what plays and how to start it, what is interim, each default taken for review, the
ratings' FRA check; the usage row (tools/usage_report.py --log "MC-5.4 overnight: …").
```

**MC-5.6 batch 3a was done (2026-11-07): 338/341** — 3 Stardates, the six split 3a / 3b (Shayne): Riku of Many Paths
(`Is.Modal`; `Count.ModesChosen(Ref.EventCard)`; a trigger's `ModeChoice` counted with its own context, so "choose up to
X" is `AtMost`); Prosper, Tome-Bound (`Trigger.From` on a `SpellCast` and on a new `LandPlayed` event — "play a card
from exile"); Drivnod, Carnage Dominus (a row of MC-6.1's `TriggersAdditionalTime`; Phyrexian mana as
`ActivateAbilityAction.LifeForPhyrexian`, one move per number paid with life; `ExileFromGraveyardCost { Count }` picked
after activating, one at a time; a keyword counter read by `Card.HasOwn`). **One bug of its own, found by the per-card
replay** (Lore 03, with the new cards taken out): the life check read "life < 2 × 0", refusing every activation below 0
life under Georgiou — fixed, with a test. With the three taken out every golden game holds (162 random + AI, 617
per-card): no ruling changed. Ceilings in `DesignNotes.md` (`{B/P}` on a spell; Drivnod dying alongside others);
AI-19.20 (Drivnod's three picks, and when it pays life). AI bench: +3.7% (5 rounds, best 5.4 s vs 5.6 s), inside the run-to-run noise.

**MC-5.6 batch 2 is done (2026-11-06): 335/341** — 7 Stardates on MC-6's families, each composed first and its
old cards moved: **6.5** `PutCounters(What, N) { Kind }` / `RemoveCounters` / `RemoveCountersCost` and `Ref.It`, a
Count per object (~125 uses of nine old pieces moved; Mikaeus, Bristly Bill); **6.6** one `ModeChoice` on every
modal component (Sublime Epiphany, Kirk, Kobayashi Maru moved; Ertai's "up to one"); **6.7** `Cycling(cost[, search])`
with the ability's `Keyword` and `Trigger.Ability` (the 10 landcyclers moved; Decree of Pain, and `Destroy.Then` +
`Ref.ThisWay` for "destroyed this way"); **6.8** `Landwalk`, `Fear` (Thada, Shizo); 6.9's `PlayPermission`
(Kirk, Exploratory Mission, Chekov, Vulcan Mind-Meld moved; Thada's search of THEIR library). Stella Lee is
`SpellCastEvent.SpellNumber`. **One ruling:** a +1/+1 counter goes on any permanent (CR 122.1) — `AddCounters`
had skipped noncreatures, so Teyo's and Shields Up!'s "if it's a creature" are now written out (the per-card
replay of Class-F Shuttlecraft caught Teyo). With the seven new cards taken out, every golden game holds (162
random + AI, 610 per-card): no other game changed. No AI-19 row (nothing left simple); one ceiling in
`DesignNotes.md` (an opponent's searched library isn't learned by `Hidden`). AI bench: −5.1% (5 rounds), noise.

| Batch | Cards | Families (MC-6) |
|---|---|---|
| 1 | DONE 2026-11-06 (17) | 6.1–6.4 |
| 2 | DONE 2026-11-06 (7) | 6.5–6.8, 6.9's permission |
| 3a | DONE 2026-11-07 (3): Riku, Prosper, Drivnod | 6.1's and 6.6's rows; Phyrexian mana, keyword counters, a three-card exile cost, "play from exile" |
| 3b | DONE 2026-11-07 (3): Sen Triplets, Flare of Fortitude, Mogis | 6.15 (`CantCastOrActivate`, Yuriko moved; `LifeTotalCantChange`), 6.9 (`MayPlayFrom`), 6.13 (`BecomesType`, Puppet Crafting moved), devotion, an alternative cost |

**MC-5.6 batch 1 is done (2026-11-06): 328/341** — the 9 tier 1 (Empress Galina, Sheoldred, Tatyova, Hull
Breach, Jhoira, Captain Sisay, Minamo, Kenrith, Arcum Dagsson) and 8 on MC-6's composed pieces: **6.1**
`TriggersAdditionalTime(cause, whose)` (Crusher, Lwaxana moved; Isshin), **6.2** `NumericReplacement(event)` (Spock,
Geordi, M'Benga, Brad, Tomik, Yoshimaru moved; Torbran, Bruvac), **6.3** one `ManaAbilityComponent` with the shared
`Costs` and `GrantsManaAbility` (Loot the Nexus, Treasure, Starfleet Academy, Rok-Tahk moved; Rishkar, Talon Gates,
Phyrexian Tower — its sacrifice a move), **6.4** `StatBetween` with `Count` bounds (Omnipresence, Confidant, Chandra
moved; Sisay, Weatherlight Captain, Loot, Exuberant Explorer). No ruling changed: the FRA golden games didn't move;
every per-card replay that did has a new Stardate dealt into its decks (checked by the decks' own hash). Left for
MC-6 after MC-5.4: Molten Tide's and Emrakul's mana pieces, Sisko's per-card cap (MC-6's tasks say). AI-19.19
(Tower). The two old one-card scans became loops: the AI bench reads +1.8% (5 rounds), noise.


**MC-5.3 batch 7c is done (2026-11-06): 311/311 — TRK is complete**, every card in the set plays. The last five, all
tier 3 from the full text, in one session: Nog, Ambitious Cadet; Q, Stacking the Deck; Kai Winn, Faithless Leader;
H.M.S. Bounty, Unlikely Timeship; The City on the Edge of Forever. Each rule in `magic-mechanics.md`; ceilings in
`DesignNotes.md` (Kai's mana-ability event for nonland sources only, convoke read as an activation; an extra turn
shortening impulse draw's window and spending a rollout turn; cleanup missing a phased-out permanent's
until-end-of-turn effects); AI-19.15–18 (Q's exiled card; the Bounty's attack; when to cast the City; Nog's
levelling); AI-19.14 built after (Kai's naming, step by step — Shayne). Shayne's answers: Kai triggers on MANA
abilities; phased-out permanents stay visible on the table, dimmed and badged "PHASED OUT" (`CardViews`).
**Next: MC-5.6, the 30 Stardates, in a NEW session (Shayne, 2026-11-06: before the boosters)** — import them as
TRK's guests, survey and tier them, build what fits a session; then **MC-5.4** (boosters, with the Stardates'
slot; TRK on Sealed and Draft; bots without 17lands — the rating run is long: ask first; TRK art not fetched yet).
Sprint 4's items 7–12 are re-ordered with Shayne now that the TRK cards are finished (the sprint's plan).

The prompt that starts MC-5.6 (paste it into a NEW session):

```
MC-5.6 batch 1: the 30 Stardates (SDS), TRK's Special Guests, in this fresh session (/card-batch step 0).
Sprint 4 (docs/sprints/2026-11-05.md). Shayne, 2026-11-06: the guests BEFORE the boosters (MC-5.4).
Last commits: cdba6d4 (TRK complete, 311/311) and 433d7fc (Kai Winn's AI naming).

Read, in order and nothing more yet: docs/stories/MC-5-star-trek.md (Decisions, Tasks MC-5.5 and MC-5.6,
Next), docs/stories/MC-3-special-guests.md (how FRA's guests were imported and scripted),
MagicCore/TrkCoverage.md (status paragraph), the /card-batch skill, MagicCore/CLAUDE.md,
MagicCore/Cookbook.md. GREP MagicCore/AbilityCatalog.md for each card's pieces — never read it whole.

First the import: find the Stardates on Scryfall (check the set code and collector numbers — the
story says SDS, 30 mythic borderless reprints), add TRK's entry to GUESTS in
tools/import_scryfall_set.py the way FRA's SPG is, re-import TRK, and check the TRK.json diff adds
only the guests. A guest FRA or TRK already scripts is NOT scripted again (CardRegistry refuses it)
— it plays as is. Then python tools/magic_set.py coverage --set TRK and
python tools/magic_set.py text --set TRK "Full Name" for each unscripted one.

Re-tier each card from its FULL text and tell me the tiers, and each new piece's or option's design,
before scripting. If the 30 are more than one session (more than ~14 tier 3, or one card bigger than
its share), propose the split and ask. Grep MtgCore before each new piece. Before building any "one
legal action per combination", count the actions it makes on a 12-creature board. Tier 2 gets one
test, tier 3 tests + revert-checks — and the test run after revert_check must BUILD (its DLL holds the
last revert). Probe with MAGIC_PROBE_GAMES=10 first, then the full 150. Hot path touched →
tools/bench_vs_head.sh "FullyQualifiedName~GreedyAiTests". A card whose play or ruling we leave simple
goes in AI-19 (now at 19.18). Their pack slot is NOT this task: it goes in with MC-5.4's collation.

Test traps: a Hand(…) inside s.Act(new … { CardId = Hand(…) }) runs on the old state — own line; so does
any helper that replaces s inside a call on s (s.MoveObject(id, ZoneOf(ref s, …)) moved to a zone that
didn't exist yet); never `c is T ? [x] : …` (CSharpier skips the file) — use the On<T>(ids) helper;
PassUntil stops at the first choice of ANY kind — wait for the type you want. A bare s.EnterBattlefield or
s.PutCounters puts no trigger on the stack — cast it, or pass priority. In the test files `Is` is NUnit's:
write global::MagicCore.Filters.Is.X, and Does.Contain. GoldenCardReplay.txt is an explicit test —
regenerate it with GoldenReplay.txt (Commands.md, "Golden replays"). Git Bash has no pgrep — wait on a
background run's output file.

End with the usage row (tools/usage_report.py --log), /pre-commit, then ask before committing. If
Stardates are left, the story's Next gets the prompt for the next batch; when all 30 play, Next points to
MC-5.4 (boosters, the Stardates' slot included).
```

**MC-5.3 batch 7b is done (2026-11-06): 306/311 play** — every C/U, 76/80 R, 24/25 M. The 11 smaller one-offs
(Shayne's split): Gwyndala (tier 2), Genesis Device, Kobayashi Maru Scenario, Quark, Kira Nerys, All Good
Things..., Peace in Our Time, The Needs of the Many, Lieutenant Commander Data, Rok-Tahk, Captain Janeway. Each
rule in `magic-mechanics.md`; ceilings in `DesignNotes.md` (Kira attacks the opponent only, Rok-Tahk's one yield,
Data's counter kind a rule, explore of a creature already gone, nothing exiled with All Good Things); AI-19.9–13
(Genesis Device's lands, Data's counters, explore's keep, Peace's draw, Kobayashi Maru's Abandon Ship). The review
fixed three, each with a test: prevented combat damage no longer counts as "dealt combat damage to a player"
(`Damage.Prevented`); an edict under Quark takes an artifact that can be sacrificed, not the Treasure; a Treasure's
own "sacrifice this:" can't be activated under Quark (`SacrificeSelfCost.CanPay`).
**Next: batch 7c in a NEW session — the last 5, each flagged bigger than one small piece:** Q, Stacking the Deck
(an ability gained by an exiled card, activated from exile with an exile-from-hand cost, a copy cast paying its
cost — one activation per hand card), Nog, Ambitious Cadet (becomes a Scout/Officer for good: subtypes and base
P/T, a gained "becomes tapped" trigger, gained statics — "play lands from your graveyard" unlimited, unlike
Submatrix's, and `AdditionalLandEachTurn`), H.M.S. Bounty, Unlikely Timeship (an extra turn — MtgCore's
`TakeExtraTurnAction`; "haven't begun an extra turn this game" a per-player flag; check `TurnStructure`'s turn
passing and the rollout's absolute turn), The City on the Edge of Forever (phasing: a hidden per-player zone moved
with `MoveObject`, phased in by the untap step unless "Permanents can't phase in"), Kai Winn (a name chosen as it
enters — copy `ChoosesNumberAsItEnters`; the trigger needs an "ability activated" event, none outside loyalty).
Re-tier each from its full text; when all 5 play, TRK is complete (311/311) and Next points to MC-5.4 (boosters)
and MC-5.6 (the Stardates).

**MC-5.3 batch 7a is done (2026-11-06): 295/311 play** — every C/U, 70/80 R, 19/25 M. The grouped tier 3: Dr.
Beverly Crusher, Lwaxana Troi (trigger twice; her "keep me tapped?" a real yes/no — Shayne), Death Is Irrelevant,
Borg Cube (assimilate), Worf, Intelligence Contractor, The Wrath of Khan (control until end of turn), Captain
Benjamin Sisko, The Caretaker (dilemmas this turn), Scotty, Overflow Submatrix, The Holodeck, Infinite Diversity,
Brazen Bluff, U.S.S. Discovery (casting from odd zones). **Q moved to 7b** (Shayne). Each rule in
`magic-mechanics.md`; ceilings in `DesignNotes.md` (Khan's combat trigger not ended at the turn's end, Scotty's
cost beside a card's own, assimilate stamped just after entering, a "for good" control after an until-end-of-turn
one, the Holodeck's sideboard only in limited, Submatrix and the Holodeck together); AI-19.5–8 (Lwaxana's answer, Bluff's opponent, the free casts, a steal
scored as kept). The review fixed four, each with a test: two until-end-of-turn steals return to the FIRST
controller; Lwaxana doubles her creature's "when this dies" (it looks back) but never a delayed trigger on it;
discover's "into your hand" is the owner's hand.
**Next: batch 7b in a NEW session — 16 left:** Q, Stacking the Deck (an ability gained by an exiled card,
activated from exile, exile-from-hand cost, a copy cast paying its cost), Kai Winn (a name chosen as it enters),
and the one-offs: The Needs of the Many, Peace in Our Time, Gwyndala, H.M.S. Bounty, Unlikely Timeship, Kobayashi
Maru Scenario (designed), Lieutenant Commander Data, Quark, Captain Kathryn Janeway, Genesis Device, Rok-Tahk, All
Good Things..., The City on the Edge of Forever, Kira Nerys, Nog — re-tier each from its full text, ~14 a batch;
then MC-5.4 boosters, the Stardates.

**MC-5.3 batch 6b is done (2026-11-05): 281/311 play** — every C/U, 61/80 R, 14/25 M. The re-tier's ten: Sela,
Antagonist Incorporator, Deanna Troi (tier 1, card sheet); Will Riker, Captain Pike, M'Benga, The Inner Light,
Deep Space 9 (tier 2, a test each); Enterprise-D and Dathon (tier 3); then Mr. Spock, Georgiou and Miles
O'Brien (tier 3, Shayne: the ten came in cheap). New, each rule in `magic-mechanics.md`:
lands from the library's top, counters doubled on your creatures, "charge counters are put on", the combat
damage a creature deals as "that many", another counter of a chosen kind, a Spacecraft never a creature, exile
the top N and keep the rest exiled, draws doubled, no loss at 0 life, a permanent returned to hand this turn.
Ceiling in `DesignNotes.md` (DS9's permanents never pass through exile); AI-19.2–3 (DS9's station progress
unscored; Georgiou below 0 life). The fuzz probe now ends a game past 300 permanents: Troubling Tribbles,
dealt in at random, doubled to 936 tokens and hung it. **Next: 30 left, ~14 a batch, each in a NEW session** —
the tier-3 groups as below (trigger twice, assimilate, control until end of turn, dilemmas this turn, casting
from odd zones, Kai Winn) and the one-offs (Kobayashi Maru …); then MC-5.4 boosters, the Stardates.

**MC-5.3 batch 6a is done (2026-11-05): 268/311 play** — the 10 shocklands, Engineered Explosives, Dominion
Infiltration, Nyota Uhura, Mirror Universe Intrusion. New, each rule in `magic-mechanics.md`: pay 2 life as it
enters or it enters tapped (asked only when that decides it; the search answers for the AI), sunburst (the
colours actually spent, the payment spending the most it can), "mana value equal to" a count, X targets (one cast
per X, the targets then picked), changeling (asked per type), a number chosen as it enters (the AI's rule: the opponent's commonest
known mana value, else 2). Ceilings in `DesignNotes.md`. **Next: 43 left, each batch in a NEW session.** Batch 6b,
the cheap one: the 10 tier 1–2 cards found by the re-tier (Sela, Antagonist Incorporator, Deanna Troi,
Enterprise-D, The Inner Light, Will Riker, M'Benga, Deep Space 9, Captain Pike, Dathon) — re-check each
from its full text. Then the tier-3 groups, ~14 a batch: trigger twice (Crusher, Lwaxana), assimilate (Death Is
Irrelevant, Borg Cube), control until end of turn (Worf, Intelligence Contractor; The Wrath of Khan), dilemmas this
turn (Sisko, The Caretaker), casting from odd zones (Scotty, Overflow Submatrix, The Holodeck, Infinite Diversity,
Q, Brazen Bluff, Discovery), Kai Winn (a name chosen as it enters — Nyota's place), and 17 one-offs; then MC-5.4
boosters, the Stardates.

**MC-5.3 batch 5b is done (2026-11-05): 254/311 play** — every common and uncommon, 38/80 R, 10/25 M. New,
each rule in `magic-mechanics.md`: Dr. McCoy's return (a delayed trigger on the card in its graveyard),
Geordi's doubled charge counters (entering too), Starfleet Academy (a mana ability that costs mana, tried by
the solver only when nothing else pays), tap three or seven (one action per set — the AI's combinations
are AI-18), the life lost this turn (`Rules.Life.Lose`, the one path), base P/T for good (Seven of Nine),
reveal the top X (Branching Timeline). Shortcuts in `DesignNotes.md`: every tap set a move; the Academy by
auto-payment only. **Next: the 57 tier-3 one-offs, ~14 a batch, each in a NEW session** (`/card-batch`
step 0; re-tier each from its full text — `python tools/magic_set.py coverage --set TRK` lists them,
Dominion Infiltration among them); then MC-5.4 boosters, the Stardates (MC-5.6). A name with quotes is
escaped in C# — the doc tools unescape it now (McCoy read as unscripted until they did).

**MC-5.3 batch 5a was done (2026-11-04): 246/311 play** — every common and uncommon, 34/80 R, 6/25 M. New,
each rule in `magic-mechanics.md`: the enters-untapped lands (Warp Speed's trigger them too), no maximum
hand size until your next turn, every token an Illusion (Holo-Engineer), cast from exile (Khan), "choose one
or more" with an empty hand (Kirk), the sacrificed creature as an ability's event card (Gowron), a dying
permanent seeing its own death (Excalbian Gamemaster — a fix in `Triggers.Collect`). Shortcut: Sulu's
"entered this turn" counts what is still there (`DesignNotes.md`). **Then: batch 5b in a NEW session** (Sulu's "entered this turn" fixed 2026-11-05) —
Dr. McCoy (`AtNextEndStep` on that card in the graveyard), Geordi La Forge (charge counters doubled in
`PutCounters`, entering with them too), Starfleet Academy (a REAL mana ability that costs {5} — the payment
solver, Shayne 2026-11-04), Collation Trimatrix and Captain Picard (tap three / seven: today
`ActivatedAbilityComponent.Costs` throws past two — interview Shayne on how the N are chosen), Gul Dukat
(life lost this turn: a tally and ONE life-loss path — damage, `LoseLife` and paying life write it in three
places now), Seven of Nine (base P/T 7/9 for good — `UntilEndOfTurn.SetBase` is the until-end-of-turn
version), Branching Timeline (reveal X, every permanent card onto the battlefield tapped). Then the tier-3
one-offs (57, Dominion Infiltration among them), MC-5.4 boosters, the Stardates (MC-5.6).

**MC-5.3 batch 4c was done (2026-11-01): 206/311 play — every common and uncommon** (96/96 C, 110/110 U).
New this batch, each rule in `magic-mechanics.md`: exile face down, playing an opponent's exiled card, one
destroy path judged together, a draw event, can't attack alone, lands enter untapped, Spacecraft enter with
counters, a mana restriction for artifacts' abilities, the first spell this game, a this-turn trigger on one
object, enter as a copy with counters, Zefram's cast from the top. Batch 4c's shortcuts are in
`DesignNotes.md` (Sehlat alone not refused — TBL-7; Zefram's top card a log line — TBL-6; face-down cards
not hidden from the search; a copy misses others' entry rules). **Next: the rares and mythics (105) in
batches, each in a NEW session — then MC-5.4, boosters.**

**Changed 2026-11-02 (Shayne): the rares and mythics FIRST, then MC-5.4.** Each batch re-tiers its cards from
the full text before building — the re-tiers mostly go UP (batch 5: six 1–2s were tier 3) — and is cut when
too many are tier 3 (ask Shayne). The tier-3 one-offs go in batches of ~14 (the 10 shocklands are one
mechanic: SHOCK, with Engineered Explosives' sunburst nearby).
Every batch starts in a NEW session
(`/card-batch` step 0) and ends with its usage row (`tools/usage_report.py --log`,
`docs/findings/card-batch-usage.md`) — this set tests that workflow.

Read: this story, `MagicCore/TrkCoverage.md` (every card's tier, what it waits on, what to reuse —
the survey), `/card-batch`, `MagicCore/AbilityCatalog.md`, `MagicCore/Cookbook.md`. Card text:
`python tools/magic_set.py text --set TRK "Name"`. Rules for the new mechanics: Wizards' "Star Trek
Release Notes" (magic.wizards.com/en/news/feature/star-trek-release-notes) — Station's are Edge of
Eternities'. Booster odds: "Collecting Star Trek" on the same site. MTGJSON has no TRK boosters yet
(2026-10-31) — re-check before building the provisional collation.

The plan, one or more fresh sessions per row, every card by Nov 10:

| Dates | Work |
|---|---|
| Nov 1 | **MC-5.5** the card registry (functionality decoupled from printings); TrkCards; tokens; the 10 Planet duals |
| Nov 2–3 | Tier 0–1 commons, then uncommons (~118) |
| Nov 4–6 | Station; the small mechanics (beam me up, strange new worlds, dilemma, Federation, power 8, becomes tapped, counters); Sagas, Kicker → ~185 of 206 C/U |
| Nov 2–7 | Rares and mythics: ~49 tier 1–2, then ~56 tier-3 one-offs in batches of ~14 (moved ahead of boosters — Shayne, 2026-11-02) |
| Nov 7–9 | Provisional boosters, TRK on Sealed and Draft (MC-5.4); DATA-2's rating method checked on FRA against 17lands |
| Nov 9–10 | The TRK rating run (ask first: long), bots on it, a draft to check; the Stardates (MC-5.6) last |

Traps: scripts go in `Sets/Trk/TrkCards.cs`; a reprint FRA already scripts is never scripted again
(only the basics overlap). A FraCards helper TRK needs (`When`, `Spell`, `Target`, `Equip` …) moves to
a shared file then — never copied (Cookbook). TRK art isn't fetched yet: `python tools/fetch_set_art.py
trk` (reprints land in `Art/trk/`), then a Godot import. `SealedSession.Set` is a const "FRA" until
MC-5.4. MagicCore's `CLAUDE.md` is at its 200-line budget: move content down rather than add.

## Log

- 2026-10-30: created — Shayne moved TRK into sprint 3, ahead of the cube; release dates and Scryfall's
  card count checked the same day.
- 2026-10-31: MC-5.1 survey — TRK imported (311), every card tiered, release-note rules read, MTGJSON
  checked (no TRK boosters yet), Stardates (SDS) found; plan put to Shayne. Counts are in Next.
- 2026-10-31: requirements changed (Shayne) — every card by Nov 10, not commons and uncommons first with
  rares if time; printings decoupled from functionality; bots on pre-simulated ratings, not colour lean
  alone.
- 2026-10-31: usage per batch is now logged — `docs/findings/card-batch-usage.md` (the survey: 1.9M).
- 2026-10-31: MC-5.5 — the card registry across sets, Shayne's three answers in Decisions; 21 new tests,
  13/13 revert-checks caught; TRK 16/311 playable.
- 2026-10-31: MC-5.3 batch 1 — 88 cards (48 C, 40 U), TRK 104/311; 11 tests for the tier 2–3 cards,
  11/11 revert-checks caught (one only after a test was strengthened); probe: 150 games, no crash, every
  tier 2–3 card used. TBL-6 created (known hand cards on the table).
- 2026-10-31: MC-5.2 Station + MC-5.3 batch 2 — Shayne's three answers in Decisions; 12 cards (7 C, 5 U),
  TRK 116/311; 15 tests, 16/16 revert-checks caught (two only after a fix: a redundant clamp deleted, a
  test made to ask WHO chooses); probe: 150 games, no crash, every card used; the search AI stations on
  a simple board; bench +2.1% (noise); golden replays regenerated (only games dealing a new card moved).
- 2026-10-31: MC-5.2 small mechanics + MC-5.3 batch 3 — Shayne's three answers in Decisions (and the
  bottom-of-library knowledge left as the rule has it, noted); 31 cards (12 C, 19 U), TRK 147/311; 29 tests,
  37/37 revert-checks caught (one only after the revert itself was fixed to compile); probe: 150 games, no
  crash, every card used; bench +0.0%; golden replays regenerated — every moved game deals a new card or a
  "tap two" card (Tenured Tethermage's became a choice too). The review's fixes: either tap clicked first
  (one move by `MoveKey`), "tap two" not split again on a copied cost list, the beamed-up creature may tap
  for the mana first.
- 2026-11-01: MC-5.3 batch 4a — Shayne's three answers in Decisions (the cut into 4a/4b/4c; lore at the
  precombat main phase; Warp Speed untaps every land); 28 cards (9 C, 19 U), TRK 175/311; 24 tests, 36/36
  revert-checks caught (one only after a vacuous test was fixed); probe: 150 games, no crash, every card
  used; bench −1.0% on the unchanged games (+7.7% on all, from the 30 every-card games now dealing the new
  cards); golden replays regenerated — 1,102 of 1,716 recorded card games moved, every one dealing a new card; no
  FRA game or AI replay moved. The review's fixes: the greedy AI picks among the colours offered (Dal R'El
  crashed it), a linked exile only for the same object of the source, a Saga that lost all abilities is not
  sacrificed, no zero-count entries in the counter map.
- 2026-11-01: MC-5.3 batch 4b — Shayne's answer in Decisions ("one or more" once per priority pass; no
  FRA/TRK card has a shape it undercounts); 17 cards (2 C, 15 U), TRK 192/311; 29 tests, 51/51 revert-checks
  caught (two only after a fix: a vacuous De-evolve check, a revert that didn't compile); probe: 150 games,
  no crash, every card used; bench −1.5% (noise); golden replays regenerated — 887 of 1,896 card games
  moved, every one dealing a new card, and the 30 every-card random games; no other game or AI replay moved.
  The review's fixes: a delayed trigger triggers only once (CR 603.7c — a countered one no longer fires at
  every later end step), Ghost's return is controlled by Ghost's controller (CR 603.7d), a De-evolve that
  lost its abilities sets no type, two `///` summaries back on their own records, Erica reuses
  `MoveChosenToAction`.
- 2026-11-01: MC-5.3 batch 4c — Shayne's three answers in Decisions (exile face down for real; Zefram's top
  card in the log, TBL-6; Pet Sehlat alone, TBL-7 created); 14 cards (3 C, 11 U), TRK 206/311, every C/U;
  30 tests, 42/42 revert-checks caught; probe: 150 games, no crash, every card used; bench −7.4% (noise);
  golden replays regenerated — 719 of 1,896 card games moved (706 deal a new card, 13 Flesh Duplicate, whose
  copy prompt changed) and the 30 every-card random games; no other game or AI replay moved. The review's
  fixes: a destroy is judged for all at once (an indestructible grant, McCoy beside the creature he saves),
  Chekov asks nothing when no land was exiled.
- 2026-11-01: after batch 4b, Shayne on the two review findings left: Storied Veteran told from flashback by
  its component is a known issue (`DesignNotes.md`); counters now go through ONE funnel —
  `MagicState.PutCounters` for every kind (+1/+1, -1/-1, charge, stun, time, loyalty and the named ones),
  so Yoshimaru and Brad Boimler apply in one place; `CounterWritesTests` fails on an engine file that adds
  to a counter field itself. No game moved (both golden files unchanged).
- 2026-11-06: batch 7b — the 16 left re-tiered (15 tier 3, five flagged); Shayne split them 11 / 5; the 11 built
  (306/311).
- 2026-11-07: MC-5.6 batch 3a — Riku, Prosper, Drivnod (338/341); the six split 3a / 3b with Shayne. The per-card
  replay with the new cards out caught the Phyrexian life check refusing activations below 0 life (Georgiou).
- 2026-11-06: MC-5.6 batch 1 — 17 Stardates (328/341): the 9 tier 1, then MC-6.1–6.4 composed with their old cards
  moved (Isshin; Torbran, Bruvac; Rishkar, Talon Gates, Phyrexian Tower; Sisay, Weatherlight Captain, Loot, Exuberant
  Explorer).
- 2026-11-07: MC-5.4.1 — TRK's collation, PROVISIONAL from Wizards' published odds (MTGJSON still has none): `tools/build_trk_boosters.py` → `Sets/TRK.boosters.json`, the Stardates 1 pack in 25 (Decisions)
- 2026-11-07: MC-5.4.2 — TRK's art from Scryfall (`tools/fetch_set_art.py trk`, the Stardates and TRK tokens too): 706 images for 355 cards, imported; the basics' TRK printings (reprints of FRA's) in `Art/trk/`, `Cards/trk/`
- 2026-11-07: MC-5.4.3 — TRK's INTERIM ratings (`Sets/TRK.ratings.json`, rarity + measured value; ρ 0.41 on FRA, Decisions); `CardRatings.Of` reads a set's own table first
- 2026-11-07: MC-5.4.4 — TRK sealed on MagicTable (the deck select's Set button) and in the console (`--compare=seed:N --set=trk`: 9 builds × 2 opponents, 18 games played); captured: a TRK pool, the White/Black suggestion, a game to turn 1
- 2026-11-07: MC-5.4.5 — TRK Quick Draft on the table (the Set button; bots on the INTERIM ratings + colour lean): captured a TRK pack, 42 picks to the deck builder, a run's game against seat 2 to turn 2; the bots' colours over 10 drafts W 39 / U 36 / B 34 / R 30 / G 21 of 80 (`docs/findings/limited.md`)
