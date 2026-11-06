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

Open questions:
- Booster collation: MTGJSON has TRK's cards but NO booster data yet (checked 2026-10-31); a provisional
  collation from Wizards' published slot odds is proposed (Collecting Star Trek, magic.wizards.com).

## Tasks

- [x] MC-5.1 The survey (a fresh session): import TRK from Scryfall (`tools/import_scryfall_set.py`), tier
      every card by what the engine needs, the new mechanics' rules read in full, a batch plan sized to
      reach sealed and draft by November 10
- [ ] MC-5.2 The new mechanics (Station, Strange new worlds, Face a dilemma, and whatever the survey finds)
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
      (`WheneverThisTurn.About`, `TheObject`), `Is.WithAttached`, an artifact cast from the library's top
- [ ] MC-5.3 Commons and uncommons in large batches, then rares and mythics — batch 1 done 2026-10-31:
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
      probe ends a game past 300 permanents (Troubling Tribbles doubling hung it). Next: the tier-3 one-offs
- [ ] MC-5.4 TRK boosters, sealed and draft on the table, bots without 17lands
- [x] MC-5.5 FIRST, before any card: a card's functionality decoupled from its printings — one registry
      by name across sets (one script per card; a test fails on a name scripted twice or a reprint whose
      rules text differs), printing data per set (art, rarity, collector number); `FraCards.Get`'s ~50
      call sites moved to it. Done 2026-10-31: `CardRegistry`, `CardPrinting`, `Card.Set`; boosters,
      ratings and drafts by set code; art in `Art/<set>/` first; `TrkCards` with the TRK tokens and the
      10 Planet duals; `TrkCardSheet.md`
- [ ] MC-5.6 LAST, aimed at Nov 10: the 30 Stardates (SDS) — imported as TRK's guests
      (`GUESTS` in `tools/import_scryfall_set.py`, as FRA's SPG), scripted once each, into the collation

## Next

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
