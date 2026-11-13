---
id: MC-4
title: MTGA's powered cube plays in MagicCore — surveyed, then built in batches
status: in-progress
area: MC
created: 2026-10-28
---

## Why

Shayne plays powered cube on MTGA and wants to practise it here (LIM-2's Decisions, 2026-10-23). LIM-2
deferred the cube's CARDS to their own story, "planned in depth when its time comes": power brings
rules FRA never needed (extra turns, storm, fast mana and others), so the cube is surveyed before
anything is built. At sprint 3 planning (2026-10-28) Shayne named AI, drafting and the powered cube
as his three priorities.

## Requirements

- [ ] Every card on MTGA's powered cube list plays in MagicCore, each with a test that it FIRES
      (`/card-batch` tiers)
- [ ] The cube is a card list, drafted the same way as FRA's packs (LIM-2: the draft must not assume
      packs)

## Decisions

- 2026-10-28, Shayne: **MTGA's powered cube** (not MTGO's Vintage Cube). Sprint 3 covers the survey
  and the first large batch, not the whole cube.
- 2026-11-07, Shayne: **no new set is built before DEV-4 and DEV-5** (the rules split, one end-of-batch check
  script) — TRK's usage review. The cube's batches count as a new set.

- 2026-10-29, Shayne: **the survey's first pass is a script** (each card's Oracle text to what it
  needs), checked by hand after — not every card read in context.
- 2026-11-12, Shayne: **LSVPower, not MTGA's cube** (supersedes 2026-10-28): "there is a ton of overlap anyways, and
  we can always implement MTGA Arena cube afterwards". Built in the cube lane (DEV-6), its first session overnight
  2026-11-12, after DEV-6.5, DEV-4 and DEV-5.

- 2026-11-12, Claude (cube lane, overnight): **imported by name** — `tools/import_scryfall_set.py lsv` reads
  `LSV.list.txt` when it exists (Scryfall's collection endpoint, 75 names a request, an unmatched name looked up exact
  then fuzzy) and writes `LSV.json` in the LIST's order: a cube card's collector number is its place in the list, its
  rarity the printing Scryfall returned (the Power is "bonus"). Double-faced cards come back under their full name
  ("Fable of the Mirror-Breaker // Reflection of Kiki-Jiki") — 17 of them. All 528 found.
- 2026-11-12, Claude: **the 31 reprints match FRA's and TRK's rules data exactly** (the ten shocklands, Sheoldred,
  Mind Twist, Chandra …), so LSV can join `CardRegistry.Sets` without the load refusing — not done yet: registering it
  adds its names to Kai Winn's options (`NonlandNames`), which moves TRK games; it goes in with the first batch.
- 2026-11-12, Claude: **the survey's first pass** is `tools/survey.py lsv` → `MagicCore/LsvSurvey.md` (generated): each
  card's needs from Scryfall's keyword list, its layout and a few text patterns, each HAVE / OPTION (named piece) /
  NEW. 31 play today; of 497, tier 1 387, tier 2 35, tier 3 75. **Tier 1 only means no keyword or layout names a
  new piece** — the text is read per batch (`/card-batch` step 1). Hand check so far: the tier-3 list (right), the
  70 shortest tier-1 texts (right, but the ten original duals carry only reminder text: `IsImplemented` needs a
  rule that a reminder-only line is implemented — their mana comes from their land types, CR 305.6, as the
  shocklands' does).
- 2026-11-12, Claude: **MC-6 before the batches** (the sprint's rule, now decidable): LSVPower needs the MC-6 families
  broadly — by a text grep, playing from another zone 53 cards (6.9), entering 57 (6.10), moving a card 43 (6.12),
  "can't" statics 37 (6.15), "this turn" conditions 32 (6.14); chosen-as-it-enters 4 (6.11), naming a card 3 (6.16).
- 2026-11-12, Claude — **the batch plan, Claude's default, for review**: (1) tier 1 power and mana, ~80 cards — the
  Moxen, Lotus, Petal, Sol Ring, the Talismans, mana creatures, rituals, the duals (the reminder-text rule), Strip Mine,
  Wasteland, Ancient Tomb, Workshop — plus the plain counters, burn and removal (Counterspell, Mana Leak, Bolt, Swords,
  Cut Down, Vindicate, Wrath, Time Walk, Ancestral); LSV registered in `CardRegistry.Sets` with it. (2–5) the rest of
  tier 1 by colour, ~75 each, after the MC-6 families their texts need. (6) tier 2 by piece (alternative cost +
  sacrifice: the five Evoke Elementals; typecycling 5; card types in your graveyard 4 …). (7+) tier 3, one rule a
  batch, by how many cards wait on it: transforming DFCs 12, protection 8, dungeons 6, the monarch 4, face-down 4,
  crew 3, companion 3, modal DFCs 3, then the one- and two-card rules (`LsvSurvey.md` has the list).

- 2026-11-12, Shayne (reviewing the plan): **the batch plan as written** — "I'm not in a rush", so no playable subset
  of the cube first. **The draft: 3 packs of 15, 8 seats** (CubeCobra's); custom draft settings deferred. **Every
  mechanic is built, even for one or two cards** (storm, voting, spree, splice, day/night, backgrounds, warp, the
  bending abilities, dungeons, companion …) — "we may implement different cards using those mechanics later. If we
  build them now that means we don't need to build them later."

- 2026-11-13, Claude (cube lane, batch 1): **split as it splits — 4.3a power and mana, 4.3b the plain spells**, LSV
  registered with 4.3a (no golden game moved by registering it: `NonlandNames` reads the game's own sets). 4.3a is 54
  cards — 44 scripted, the ten original duals by the reminder-text rule (`CardRegistry.IsImplemented`: a line wholly in
  parentheses has no rules force, CR 207.2; `magic_set.py` mirrors it).
- 2026-11-13, Claude — **4.3a's new pieces, each the nearest one extended** (MC-6's rule):
  - **Mishra's Workshop: `ManaRestriction` composed** into `ManaRestriction(Spells) { Abilities, NotFromHand }` — a
    filter on the spell paid for. Nearest: `OnlyArtifacts`, which also pays an artifact's ABILITY; the Workshop's mana
    can't (its rulings), and an enum case takes no option, so the four old cases became one filter and their cards
    (Heartwood Crafter, Gideon's Memorial, Chandra, Isolinear Processor) moved onto it. No game moved.
  - **The Talismans, Ancient Tomb: `ManaAbilityComponent.DealsDamageToYou`**; **Mana Confluence, the fetchlands:
    `PayLifeCost(n)`** in the shared costs list. Nearest: the cost list itself — damage is not a cost (it is dealt, so
    prevention applies; CR 605.1a's ability makes the mana AND deals it), and no life cost was in the list (Phyrexian
    mana and the shocklands pay life outside it). Both paid by ONE `ManaAbilities.PayAsUsed`, for the ability that made
    the colour (a Talisman's {C} is painless). Known ceiling (DesignNotes): the solver doesn't weigh the pain.
  - **Mana Crypt: `FlipACoin { IfLost, IfWon }`**, from the seeded random. Nearest: `If` — a `Condition` must answer the
    same each time it is asked (an intervening "if" asks twice), so a random one can't be a condition.
  - **Exalted: `Scripting.Exalted`**, a trigger from tested pieces (`EventTest.Alone`, any attack target — CR 702.83a).
- 2026-11-13, Claude — **4.3b's options**, each on the piece the card reads as: `Counter { To }` (Remand: its owner's
  hand; Memory Lapse: the library top — only when it was countered, so an uncounterable spell stays),
  `TakeExtraTurn { Who }` (Time Warp's target player), `Stat.PowerPlusToughness` on `StatBetween` (Cut Down's
  "total power and toughness"), `MoveChosen { OnTop }` (Brainstorm: two of them, the second on top — "in any order"
  is the order chosen). Swords' life is read before the exile (last-known power); Path to Exile's and Erode's search is scripted before the removal (pre-commit review: once the creature has left, "its controller" reads its owner — a stolen creature's land would go to the wrong player; as 70,000 Light-Years). Wrath's "can't be regenerated" needs
  nothing: the engine has no regeneration yet — the card that brings it must read the flag on Wrath and Snuff Out.
- 2026-11-13, Claude (pre-commit review, 4.3a): **a second Mana Confluence planned at 1 life pays its life too**, never
  makes its mana free — `ManaAbilities.Used` names the ability without asking its costs again; the solver asking each
  source against the life alone stays a known ceiling (DesignNotes), with convoke's and the coin flip's.
- 2026-11-13, Claude — **left out of batch 1, to the batch whose piece they share:** the Spirit Guides (a mana ability
  activated from the HAND — payment reads only permanents), Delighted Halfling (one permanent with unrestricted {C} and
  restricted any-colour mana — `YieldOf` keeps one restriction per permanent — plus "that spell can't be countered").
  4.3b leaves out the alternative-cost spells (Force of Will, Daze, Snuff Out, Baleful Mastery), "or" additional costs
  (Bitter Triumph, Bone Shards), Mana Drain's delayed mana, Chain Lightning's copy, Lose Focus's coloured replicate.

- 2026-11-13, Claude (cube lane, batch 2) — **4.4a's pieces, each the nearest one with an option** (MC-6's rule):
  - **The alternative costs: `MayCastFromHandFree(Is.Source)` (Flare of Fortitude's) takes `If` and `Mana`.** Snuff
    Out's "if you control a Swamp" and Force of Negation's "if it's not your turn" are `If`; Baleful Mastery's {1}{B}
    is `Mana`, paid instead and stamped on the stack (`OnStackComponent.AlternativeCost`, read by
    `AlternativeCostWasPaid` — not `WasKicked`: an alternative cost isn't a kicker, CR 118.9 vs 702.33). A free cast
    asking mana is offered BESIDE the paid one, as one asking a cost. `Playable.FreeCast` is the one answer.
  - **Force of Will's "exile a blue card from your hand": `ExileFromHandCost(filter)`** (Q's, a filter option) —
    `CastSpellAction.ExileId`, the slot activations already had. **Daze's "return an Island you control":
    `ReturnToHandCost(filter)`** — beam me up's `BeamMeUpCost` generalised (its creature is now its filter); TRK's
    beam-me-up cards and Scotty moved onto it.
  - **"Or" between two costs: `AdditionalCost.Or` (a cost) replaces `OrPay` (an int)** — nearest: `OrPay`, whose "or"
    was mana only. Silence the Echo and Countersculpt moved onto `Or = new PayManaCost(n)`; Bitter Triumph is
    `DiscardCost { Or = PayLifeCost(3) }`, Bone Shards `SacrificeCost(Is.Creature) { Or = DiscardCost() }`.
    `CostList.Paid` resolves each "or" by what its slot picked; a cast picking both halves or neither is refused.
  - **Lose Focus' replicate {U}: `ReplicateComponent(ManaCost)`** — was `(int Generic)`; Consign to Memory moved.
  - **Mana Drain: `AtNextEndStep { NextMainPhase, X }`** — Erica Ortegas' delayed trigger in the command zone, its
    step an option (your precombat main, or this turn's postcombat main when set up before it — the card's
    rulings; the postcombat main now raises `StepBeganEvent`), and a number fixed as it is set up
    (`TriggeredAbilityComponent.X`, read as `Count.X`). "That spell's mana value" is `GreatestManaValueOf`, which now
    reads a spell's X on the stack (`ManaValueNow`, CR 202.3e).
  - **Chain Lightning: `May { Who = ControllerOfTarget, ThenAsThem }`** (Peace in Our Time's) with `PayMana` and
    `CopySpell` — `ControllerOfTarget` of a PLAYER target is that player. "May pay {R}{R}. If the player does, they
    may copy" is one yes: paying is only for the copy.
  - Force of Negation's "exile it instead" is `Counter { To = Exile }` (4.3b's option). Snuff Out's "can't be
    regenerated" waits for regeneration, as Wrath's.
  - Pre-commit review: a cast for an alternative cost now READS differently — "(not paying its mana cost)" in
    `Describe` (the table groups plays by their text, so Baleful Mastery's two casts were one play); a cast made
    during a resolution is left as it was (a choice's option text is in a record's fingerprint). The golden traces
    hold that text, so every game with a free cast (Flare of Fortitude, Omnipresence, the new cards) moved — verified
    text only: with the suffix blanked every golden passed. Mana Drain cast in your upkeep or draw step pays at
    THIS turn's precombat main (the review's catch).
  - Probe: 150 games, one unfinished past 300 permanents — the probe's "growth without end" case (token decks); none of
    4.4a's cards makes a permanent.

- 2026-11-13, Shayne: **no speed bench for the card batches** — "its on aspects of the game that are necessary … even if
  its slower, its not like im going to remove these changes"; the bench is for testing ways to OPTIMIZE, not for new
  features. 4.3a's and 4.4a's waiting benches dropped; `/card-batch` says so.

- 2026-11-13, Claude (cube lane, batch 2) — **4.4b's pieces, each the nearest one with an option** (MC-6's rule):
  - **The Spirit Guides: `ManaAbilityComponent.From = Hand`** with `Costs = [ExileSelfCost]` — nearest: the mana ability
    itself; `ActivatedAbilityComponent.From` (landcycling, Q) is an option already, but an activated ability uses the
    stack and is a move in `LegalActions`, and mana abilities are neither (payment taps what it needs). So the mana
    ability took the same option: `ManaAbilities.AbilitiesOf(…, from)` keeps each zone's apart (a Guide on the
    battlefield makes nothing), `ManaPayment.Find` adds the hand's mana only when the battlefield can't pay and after
    every permanent (`FromHand`: never the spell being cast, never Force of Will's exiled card), `Pay` exiles it and
    raises the activation (Kai Winn names it: rules-true, as Shayne's mana-ability ruling), and the X bound counts it.
  - **Delighted Halfling: `YieldOf(…, paying)` and `ManaRestriction.CantBeCountered`** — nearest: `YieldOf`'s one
    restriction per permanent, which would make its {C} restricted too. The yield now leaves out the abilities whose
    mana can't pay for the spell being paid (Payment passes it; unset reads as before, so no old card moved); tapped by
    hand, a colour the restricted yield lacks comes from the unrestricted abilities (its {C}). "That spell can't be
    countered" is a rider on the restriction, stamped as `OnStackComponent.CantBeCountered` (Theorist's Proxy's) when the
    payment spent such mana (`ManaPayment.MakesUncounterable`). `Used` no longer reads "any colour" as {C}, so the
    {C} is the free one whatever the ability order.
  - Known ceilings (DesignNotes): hand mana is spent only by payment (no manual float), never beside Starfleet
    Academy's costly mana; the Halfling's yield for a legendary spell is its any colour alone, so a legendary {C} pip
    can't use its {C} (none in the cube).
  - Pre-commit review: Sunburst no longer spends a Spirit Guide for a colour the plain payment didn't need; the X bound
    asks the Halfling's yield for that spell (its {C} counts for a nonlegendary X); the sealed builder counts neither a
    Guide nor the Halfling as a fixer; `Pay` exiles a hand card once. Ceilings added (DesignNotes): a Halfling convoking
    a legendary spell would stamp it, and the AI's colour reads see the Halfling as every colour. Left as designed:
    `CantBeCountered` on the restriction (the rider floats with the mana in the pool), and the hand scan on a failed
    payment (no bench for card batches).

- 2026-11-13, Claude (cube lane) — **4.4c: red's tier 1 read card by card, split in two.** The survey tiers by keywords
  and a few patterns; read, 11 of red's 27 are tested pieces (4.4c1) and 16 need an option or a new piece (4.4c2, the
  list in Next). 4.4c1's notes: Seasoned Pyromancer discards FIRST, so its count reads a hand of two discarded without
  a choice (`Count.NonlandCardsChosen` — tested); Pyrogoyf's "that creature deals" is `DamageFrom(Ref.EventCard, …)`,
  its power read as it resolves; Generous Plunderer's "defending player" is the one opponent; the tokens (Elemental,
  Golem) are properties, not fields — a static field declared below `Scripts` is still null when the scripts are built.

## Tasks

- [x] MC-4.1 The list from Shayne; imported by name, with art (`fetch_set_art.py`) — `LSV.json`, 528 cards; art for
      each (reprints of FRA/TRK under `Art/lsv/`, `Cards/lsv/`)
- [x] MC-4.2 The survey: each card sorted by what it needs (first pass `tools/survey.py`, `MagicCore/LsvSurvey.md`;
      hand check and batch plan in Decisions) — scripts with today's primitives, needs a
      new primitive (named), or needs a new rule (extra turns, storm, ...) — and the batches planned
      in this story, the new rules first in each
- [x] MC-4.3 The first batch (`/card-batch`), in two parts — done 2026-11-13 (what it left out: Decisions):
  - [x] MC-4.3a Power and mana, 54 cards — done 2026-11-13: the Moxen, Black Lotus, Lotus Petal, Sol Ring, Mana Crypt,
        Mana Vault, the Monoliths, the six Talismans, Llanowar Elves, Avacyn's Pilgrim, Birds, the Hierarchs (exalted),
        Dark and Cabal Ritual, Strip Mine, Wasteland, Ancient Tomb, Mishra's Workshop, City of Traitors, Gaea's Cradle,
        Tolarian Academy, Mana Confluence, the ten fetchlands and Prismatic Vista; the ten original duals unscripted. LSV
        registered. New pieces in Decisions; 38 tests, 12/12 breaks caught; `batch_checks.py` all 7 steps (golden-out
        unchanged: no recorded game moved by the engine; golden-in: the new cards joining the random decks moved 29
        random and 1222 card-replay games and added 176; probe 150 games, none unfinished — the three never used are
        mana-only lands, which the probe can't see).
  - [x] MC-4.3b The plain spells, 22 — done 2026-11-13: Counterspell, Mana Leak, Mana Tithe, Spell Pierce, Memory Lapse,
        Remand, Stern Scolding, Lightning Bolt, Burst Lightning, Swords to Plowshares, Path to Exile, Erode, Cut Down,
        Vindicate, Abrupt Decay, Wrath of God, Abrade, Pyroblast, Time Walk, Time Warp, Ancestral Recall, Brainstorm.
        Four options (Decisions); 23 more tests, 7/7 breaks caught; `batch_checks.py` all 7 steps (golden-out unchanged;
        golden-in moved 29 random and 1126 card-replay games, added 88; probe 150 games, none unfinished, none unused).
- [ ] MC-4.4 The second batch (`/card-batch`, sprint 5 C5), in parts:
  - [x] MC-4.4a The costs batch 1 left out, 10 — done 2026-11-13: Force of Will, Force of Negation, Daze, Snuff Out,
        Baleful Mastery (an alternative cost on the card), Bitter Triumph, Bone Shards ("or" costs), Lose Focus
        (coloured replicate), Chain Lightning, Mana Drain. Options in Decisions (Silence the Echo, Countersculpt,
        Consign to Memory and the beam-me-up cards moved); 11 tests, 13/13 breaks caught; `batch_checks.py` all 7 steps
        (golden-out unchanged; golden-in moved 29 random and 600 card-replay games, added 40; then the review's
        `Describe` text, above)
  - [x] MC-4.4b The mana batch 1 left out, 3 — done 2026-11-13: Elvish and Simian Spirit Guide (mana from the hand),
        Delighted Halfling. Two options (Decisions); 16 tests, 15/15 breaks caught; `batch_checks.py` all 7 steps
        (golden-out unchanged: no recorded game moved by the engine; golden-in moved 29 random and 192 card-replay games,
        added 12; probe 150 games, none unfinished — the Guides' mana from the hand is a payment, which the probe's counts
        don't see, as a land's)
  - [ ] MC-4.4c Red's tier 1 (`LsvSurvey.md` listed 28; Simian Spirit Guide went with 4.4b), read card by card and
        split in two (Decisions):
    - [x] MC-4.4c1 The tested pieces, 11 — done 2026-11-13: Flame Slash, Sear, Flametongue Kavu, Wheel of Fortune,
          Faithless Looting, Mine Collapse, Suplex, Legion Extruder, Seasoned Pyromancer, Pyrogoyf, Generous Plunderer.
          Scripts only, no engine change; 14 tests; `batch_checks.py` all 7 steps (golden-out unchanged; golden-in moved
          29 random and 649 card-replay games, added 44; probe 150 games, none unfinished, every card used)
    - [ ] MC-4.4c2 The options and new pieces, 16 (Next)

## Next

MC-4.4c2 — red's last 16, each read 2026-11-13; the survey's "nothing the keywords name" hid an option or a
new piece in every one (each the nearest piece with an option first — MC-6's rule):
- Broadside
  Bombardiers (boast: `OncePerTurn` + attacked this turn, "2 plus the sacrificed permanent's mana value"), Cori-Steel
  Cutter (flurry: `SpellNumber = 2`, a token you MAY attach), Bonehoard Dracosaur (tokens by what was exiled "this way"),
  Inti (reflexive on a discard; "whenever you discard one or more" → play until your NEXT end step), Laelia ("cards put
  into exile from your library and/or graveyard" — a new event?), Magda ("sacrifice five Treasures", Dwarf tapped),
  Tersa ("discard up to two, draw that many"; a RANDOM card exiled from the graveyard), Sneak Attack (a creature from the
  hand, haste, sacrificed at the next end step — `MoveChosen` + `SacrificeAtNextEndStep`), Robber of the Rich ("spend
  mana as though any colour", "during any turn you attacked with a Rogue"), Screaming Nemesis ("can't gain life for the
  rest of the game"), Orcish Lumberjack (three mana in any combination of {R}/{G}), Fiery Confluence (the same mode
  more than once), Pyrokinesis (4 damage divided among ANY number of targets), Underworld Breach (escape), "Name
  Sticker" Goblin (a d20 and "enters from anywhere other than…"), Goblin Welder (two targets, simultaneous swap).
Each part its own commit; a FIRES test per scripted card. DEV-5 still open: the probe names no never-used card, and a
re-run after a fix needs the golden files restored from HEAD by hand (`git checkout HEAD -- MagicCore.Tests/Golden*.txt`).

## Log

- 2026-10-28: created at sprint 3 planning (Shayne): MTGA's powered cube; survey and first batch this
  sprint.
- 2026-11-12: the cube is LSVPower (Shayne); his list (528 cards, the CubeCobra export's mainboard) saved as
  `MagicCore/Sets/LSV.list.txt` — the code `LSV` is Claude's default, the cube lane may rename it.
- 2026-11-12 (night, cube lane): MC-4.1 imported by name with art, MC-4.2's first pass by script and its hand check started; the batch plan written.
- 2026-11-12: Shayne took the batch plan, the draft default, and every mechanic built.
- 2026-11-13 (cube lane, sprint 5 C5): MC-4.3a — batch 1's power and mana, 54 cards; LSV registered; the mana
  restriction composed into a filter (four old cards moved), a mana ability's pain and life, a coin flip, exalted, the
  reminder-text rule. No recorded game moved by the engine; 85 of 528 play. 4.2M input-token equivalents to here.
- 2026-11-13 (cube lane): MC-4.3b — batch 1's 22 plain spells, four options; no recorded game moved by the engine;
  107 of 528 play. Batch 1 done (the session: 7.6M input-token equivalents, `card-batch-usage.md`). For Shayne: nothing Open — the left-out cards and their pieces are batch 2's (Next);
  the bench on 4.3a's payment change waits for a quiet machine.
- 2026-11-13 (cube lane, sprint 5 C5): MC-4.4a — batch 2's alternative and "or" costs, 10 cards; `OrPay` became `Or`
  (a cost), beam me up's cost took a filter, replicate a mana cost, the delayed trigger a main phase and an X. No
  recorded game moved by the engine; free casts read differently (text only). 117 of 528 play.
  Session summary (5.7M input-token equivalents): Part 1's ten cost cards done; Part 1's three mana cards (4.4b) designed,
  in Next, not built; Part 2 (red's tier 1) not started. For Shayne: nothing Open.
- 2026-11-13 (cube lane, sprint 5 C5): MC-4.4b — the Spirit Guides and Delighted Halfling; a mana ability FROM THE HAND
  (`ManaAbilityComponent.From`), a yield per spell paid for, and the uncounterable rider. No recorded game moved by the
  engine; 120 of 528 play. For Shayne: nothing Open.
- 2026-11-13 (cube lane, sprint 5 C5): MC-4.4c1 — red's 11 tier-1 cards made of tested pieces, scripts only; 131 of
  528 play. Red's other 16 need options or pieces (4.4c2, Next). For Shayne: nothing Open.
