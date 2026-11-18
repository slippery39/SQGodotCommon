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

- 2026-11-13, Claude (cube lane) — **4.4c2 surveyed: 13 of the 16 are options, 3 need a new rule** (MC-6's rule, each
  read against the nearest piece's code). Parts: **2a** Bombardiers, Cori-Steel Cutter, Dracosaur, Inti, Magda, Tersa;
  **2b** Sneak Attack (`MoveChosen` + `AtNextEndStep { On }` widened from a dead card to a permanent), Orcish Lumberjack
  (a mana ability's "any combination of" colours), Fiery Confluence (`ModeChoice` repeating a mode — `Compose` already
  takes repeats), Screaming Nemesis (`LifeTotalCantChange` for gaining only, kept for the rest of the game), Goblin
  Welder (a target "in that player's graveyard"; "if both still legal" is `Both(Matches…)`), Robber of the Rich
  (`AtLeast` against a Count, `PlayPermission.If`, cast only, an attacked-subtype tally), Underworld Breach (escape is
  `MayPlayFromZone` with `Costs` — not flashback: an escaped card isn't exiled); then a part per new rule — **2c** Laelia
  (`LeftGraveyardEvent` generalised to a zone-change event with From/To, library→exile raised too), **2d** "Name
  Sticker" Goblin (`EnteredBattlefieldEvent.From`, and a die roll: `FlipACoin` can't take it — a coin flip isn't a die
  roll, CR 705/706), **2e** Pyrokinesis (damage divided among ANY number of targets — Pure Energy's two-target split is
  2^n target sets; the targets a choice after casting, as PerX's).
- 2026-11-13, Claude — **4.4c2a's options**, each on the piece the card reads as: `CreateTokens.Then` and
  `ExileTopPlayable.Then` with `Ref.ThisWay` (Decree of Pain's pattern: Cori-Steel Cutter's "you may attach this
  Equipment to it", Dracosaur's "if you exiled a land card this way"); `ExileTopPlayable { From = Graveyard, AtRandom }`
  (Tersa); `Discard.UpTo` with `Count.NumberOf(Ref.Chosen)` ("discard up to two, then draw that many");
  `PlayPermission.UntilYourNextEndStep` (Inti — a known ceiling: through that whole turn, DesignNotes);
  `SacrificeCost.Count` on an activation, the set picked after activating as Drivnod's exile (Magda's "Sacrifice five
  Treasures" — 252 actions per set from ten). Prowess moved to `Scripting` (the Monk token is LSV's). Bombardiers'
  boast is `OncePerTurn` + `OnlyIf(Matches(Source, AttackedThisTurn))`, "the sacrificed permanent's mana value" the
  ability's event card (`GreatestManaValueOf(Ref.EventCard) { Plus = 2 }`) — tested, no engine change.
  Pre-commit review: a card Tersa exiles from the graveyard was marked `Blind` (priced by the AI as an unseen card) —
  now only a library card is (tested, caught). Ceilings added (DesignNotes): `SacrificeCost.Count` beside a mana cost
  could pay with a Treasure it is about to need; its `Describe` says "permanents". Left as cleanups: one "run `Then` with
  `ThisWay`" helper for the three copies, `PlayPermission`'s durations as one enum, `From` beside the library-only
  `ShuffleFirst`/`RestOnBottom`.
- 2026-11-13, Claude — **Open (for Shayne): the AI never discards to Tersa's "discard up to two".** `GreedyAi` answers a
  hand choice with its minimum unless it is "up to one", so an "up to two" is answered with none: the AI's Tersa
  draws nothing. Default: left to the AI lane (a rule for "up to N" hand choices — discard the cards it values least
  below what a draw is worth); Tersa still plays, only her looting is lost. **Shayne, 2026-11-14: backlog for now** —
  AI-19.24.
- 2026-11-14, Claude — **4.4c2b's options**, each on the piece the card reads as (MC-6's rule; no new piece):
  `AtNextEndStep.On` a permanent on the battlefield, in `MoveChosen.Then` with `ForGood` haste (Sneak Attack);
  `ManaAbilityComponent.OrColor` + `TapForManaAction.OfTheOther`, one move per Forest × split — 4 per Forest, 16 with
  four (Orcish Lumberjack); `ModeChoice.Repeats` (Fiery Confluence: 10 mode sets; a repeated mode's target word is
  `NotThePrevious`, so n artifacts make C(n,2)+C(n,3) casts for the destroy modes, not n² + n³ — 286 against 12, not
  1,872); `DealDamage.Then` with `Ref.ThisWay`, `LifeTotalCantChange { GainOnly }` in a `ThisTurn { RestOfTheGame }`
  (Screaming Nemesis); `TargetSpec.OfThePreviousTargetsController`, asked on choosing and on resolving (Goblin
  Welder); `PlayPermission { If, CastOnly }`, `AtLeast.MoreThan`, `SubtypeEnteredThisTurn { Attacked }` on a new
  `TurnTally.SubtypesAttacked` (Robber of the Rich); `MayPlayFromZone.Costs`, joined in `CastSpellAction.CostsOf`
  (Underworld Breach's escape). Two lines the revert-check showed no test could see were taken out, not kept:
  lifting the mode cap past the number of modes, and a second "that player's graveyard" filter in `Combinations`
  (`AreLegal` already refuses). Ceilings (DesignNotes): the same artifact can't be Confluence'd twice; `OrColor` only
  on a cost that picks; a card with its own flashback never escapes; Welder sacrifices, then returns.
- 2026-11-14, Claude — **4.4c2c: `LeftGraveyardEvent` became `ChangedZoneEvent(owner, card, From, To)`**, raised by
  `MoveCard` for a card leaving a graveyard (as before) and one exiled from its library; `Trigger.From` takes it and
  `Trigger.To` is new. Vash and Blastoneuron moved onto it (`From = Graveyard`); Laelia is `{ To = Exile, OneOrMore }`
  with no From — the two zones the event is raised for. Not every zone change: `MoveCard` is the hottest path, so a
  draw raises nothing (a ceiling in the event's summary; the "to exile" test on the library case is that saving and
  no test can see it). No recorded game moved.

- 2026-11-14, Claude — **4.4c2d: "Name Sticker" Goblin — one option, one new piece.** "Enters from" is an option:
  `EnteredBattlefieldEvent.From` (the zone `EnterBattlefield` saw it leave — the stack for a resolving spell, null for
  a token just made), read by `Trigger.From` and the new `Trigger.NotFrom` ("anywhere other than a graveyard or exile";
  an `EventTest` case would have been an enum case per card phrase). The intervening "if" is tested pieces — but "if
  it's on the battlefield" is `Matches(Ref.Permanents(Is.Source), …)`, not `Ref.Source`: a `Matches` reads a gone
  source as it last was (the first script rolled for a Goblin killed in response; its test caught it). **The d20 is
  `RollDie(sides, rows)`, new beside `FlipACoin`** — read first: `FlipACoin` is `IfWon`/`IfLost` on a fair `Next(2)`;
  a die's result is a number read against a table of ranges of unequal width, so "sides" on it would leave won/lost
  meaningless for a die and rows meaningless for a coin (two effects in one record), and the rules keep the two events
  apart (CR 705 flip, 706 roll — "whenever you roll a die" never sees a flip). The die test's rows are out of order: in
  order, a row's lower bound was a line no test could see. `tools/magic_docs.py` now finds a test naming a card
  whose name has quotes. The probe: 2 of 150 random games ran past 300 permanents (its cap; none with Laelia named)
  — random play spending the Goblin's 4–6 mana, not a loop of the Goblin's (it stops at ten).
- 2026-11-14, Claude — **4.4c2e: "N divided among any number of targets" is one piece, `TargetSpec.Divide`** —
  Pyrokinesis (4) and Pure Energy (2, moved onto it; `TargetChosen` is gone). Counted first: a cast per set of
  targets and split is 12 + 66×3 + 220×3 + 495 = 1,365 against 12 creatures (more with each red card to exile), so
  past two it is ONE cast (`ChoosesTargets`, as PerX): `ChooseXTargetsChoice` generalised from "exactly X" to the
  words' range (one to four), then — only with two or three targets — the ways to split, a `ChooseKeywordChoice`
  (no new choice type). The division rides the cast (`CastSpellAction.Shares` → `OnStackComponent.Shares`), each
  target's share is `Count.Share` with `Ref.It`, and `DealDamage` now counts its amount per object as `PutCounters`
  did. Pure Energy's two stay casts (one per creature and per pair — its moves, records and the AI's play are
  unchanged; no recorded game moved), its script one `PutCounters(Ref.EachTarget, Count.Share)`. "Exile a red card
  rather than pay" is Force of Will's piece, script only. Two lines the revert-check showed no test could see were
  taken out, not kept: padding the chosen targets to the word count, and a bounds guard in `Count.Share`.
  Pre-commit review, both fixed with a test that fails without the fix: `DealDamage` counted its amount again after
  each target was dealt its damage (now all before any — simultaneous), and a share was read by its place among ALL
  the words, so a hand-built cast with a word left empty between two targets misread it. The AI answers
  the targets with ONE creature, whichever plays out best (never its own); splitting is AI-19.25 (backlog), and a
  ceiling (DesignNotes): one divided word, no modes.

- 2026-11-14, Claude (cube lane) — **4.4d: white's tier 1 (36) read card by card: 22 are tested pieces, 14 need an
  option; none needs a sibling piece** (MC-6's rule, each read against the nearest piece's code). Parts:
  **d1** the 22 (list in Tasks) — a card whose FIRES test shows a missing option moves to d2;
  **d2a** zones and tokens, 6 — Oust and Unexpectedly Absent (`MoveTo.OnTop` is the top or the bottom: "beneath the
  top X" is a depth on it), Phelia (`Flicker { AtNextEndStep }` returns by a delayed trigger and runs nothing after:
  "if it entered under your control" is a `Then`, as `MoveChosen`'s), Elspeth, Storm Slayer (`NumericReplacement` has
  damage, counters, draw and mill: "twice that many tokens" is a `ReplacedEvent` row applied in `CreateTokens`, the
  one place a token is made), Containment Priest (`PermanentsEnter` changes HOW a permanent enters: "exile it instead"
  unless cast is an outcome on it, decided before the entry), Enduring Innocence (`MoveTo` returns a card as it is
  printed: "it's an enchantment, not a creature" is a type it loses for good, stamped as assimilate's);
  **d2b** restrictions and timing, 5 — Archon of Emeria, Voice of Victory and Clarion Conqueror
  (`CantCastOrActivate` is everyone, spells AND abilities, during combat or always: whose, which of the two, of which
  sources and when become its options, and `Combat.LockedDown` is told what is being cast or activated; Voice's
  mobilize is `CreateTokens { Tapped, SacrificeAtNextEndStep }` plus entering attacking, `PermanentsEnter`'s word on
  a token), The Wandering Emperor (`JaceLoyaltyAtInstantSpeed` is a this-turn flag for Jace planeswalkers: a filter
  on it, and a permanent may hold it — Jace's Machinations moves onto it), Anointed Peacekeeper
  (`ChoosesNameAsItEnters` names a nonland card unseen: looking at the hand first and any name are options;
  `CostChange` taxes spells: abilities of sources a filter names is an option, read by `Costs.OfAbility`);
  **d3** leaving the battlefield, 2 — Skyclave Apparition and Serra Paragon: `ChangedZoneEvent` is raised for a
  graveyard and library → exile; "when this leaves the battlefield" and "put into a graveyard from the battlefield"
  (any permanent, not only a creature dying) widen it in `MoveCard`, the one place — never a second event. Skyclave's
  token is `CreateTokens { ControlledBy }` with a counted power and toughness; the Paragon's "it gains …" an option on
  `MayPlayFromZone`, carried as `Finality` is;
  **d4** Balance — `EachSacrifices` is one each and `Discard` a fixed number: "all but as many as the player with the
  fewest" is a count on each (each player's choice, after the cast — never an action per set).
- 2026-11-14, Claude — **4.4d1's notes.** Jacked Rabbit moved to d2a: its FIRES test showed ravenous' "if X is 5 or
  more" never triggers — a trigger's intervening "if" is judged as it triggers with `Triggers.ContextOf`, which has no
  X, while its effects get the X it was cast for (`QueueTargets`: `Source.CastX` for "when this enters"); the fix is one
  X for both, an engine change, so not this script-only part. Its script: `EntersWithCountersComponent(Count.X)`,
  `When(SelfEnters, draw) with { If = AtLeast(Count.X, 5) }`, `When(SelfAttacks, CreateTokens(Rabbit,
  Count.PowerOf(Ref.Source)))`. The other 21 are scripts only (and one name, `CounterKinds.Story`): Thalia is
  `CostChange { By = Whose.Any }`; Elite Spellbinder is Zero's script; Reprieve is `MoveTo { From = [Stack] }` (not a
  counter: it takes an uncounterable spell too); battle cry and For Mirrodin! are scripted triggers (`CreateTokens.Then`
  with `AttachSourceTo(Ref.ThisWay)`); Glimmer Lens' "and at least one other creature attack" is `Trigger.While` two
  attackers; Sage of the Skies is `CopySpell(Ref.Source)` on its own cast trigger (the copy resolves into a token);
  Staff of the Storyteller reads "create" as a creature token entering (a ceiling, DesignNotes).
- 2026-11-15, Claude — **4.4d2a's notes: seven cards on six options; three plans changed as the code was read.**
  As planned: Jacked Rabbit (`Triggers.XOf` — one X for a trigger's "if" and its effects), Oust and Unexpectedly
  Absent (`MoveTo { Beneath = N }`, a `Count`: the card on top, then the N above it put back over it), Containment
  Priest (`PermanentsEnter { ExiledUnlessCast }`, decided at the top of `EnterBattlefield` — so a caller may find the
  card NOT on the battlefield after it; `MoveTo` checks).
  **Phelia — `Then` alone did not fit.** The delayed return sits on the exiled card, so its "this" is that card and
  nothing in it could name Phelia; hosted on Phelia it would be lost when she leaves, and the card must still return.
  So two options: `Flicker.Then` → the delayed `MoveTo.Then` (`Ref.ThisWay`, as `Destroy.Then`), and
  `DelayedTrigger.MadeBy` — the object whose ability set it up (CR 603.7f), the trigger's `Ref.EventObject`, nothing
  once she has left. `Flicker.Then` without `AtNextEndStep` throws (no card has it).
  **Elspeth — two places, not one:** a token copy is made in `CreateTokenCopy`, not through `CreateTokens`, so the
  `ReplacedEvent.Tokens` row is applied in both.
  **Enduring Innocence — the stamp is IN the entry, not after it:** `MoveTo { Loses = Creature }` passes
  `EnterBattlefield(loses:)`, so the Priest's check and "whenever a creature enters" see no creature (the first build
  stamped after entering, and the Priest exiled it). Its "if it was a creature" is not scripted: only a creature or
  planeswalker dies here (`DiedEvent`), so returned as an enchantment it never triggers again — no test could see the
  line. `Loyalty(…)` moved from FraCards to `Scripting` (a second set scripts planeswalkers).
  The probe now names an overrun game's seed and what filled the battlefield (`CardProbeTests`).
  **`/pre-commit`'s review, fixed:** `MadeBy` is the trigger's source AS IT WAS (`SourceSnapshot`), not whatever has
  its id — a Phelia blinked in response is a new object and gets no counter (a test); the "until it leaves" type
  stamp is one `Card.WithTypesUntilItLeaves`, Assimilate moved onto it. **Left as ceilings (DesignNotes):** the Priest
  judges a card's types before it moves (Mogis); `RevealUntilOntoBattlefield` and `MoveChosenToAction` don't look
  whether the entry was refused, and Empower Jace's token isn't made by `CreateTokens` (Elspeth wouldn't double it) —
  none reachable in one pool today. Not taken: a shared helper for the five `Then`/`Ref.ThisWay` spawns (MC-6's
  kind of work, it would touch four old effects), and `EnterBattlefield` scanning the entry rules twice for an entry
  that wasn't cast (no bench for a card batch).
  **Open (for Shayne):** Unexpectedly Absent is one cast per X × target — 84 with 8 lands and 12 nonland permanents,
  the way Mind Twist and Khaaaaaaaaaaaannn! are built; linear, and the probe cast it 213 times with no hang. Default
  (taken): left so; X becomes a choice after the cast if the AI's branching on it shows in a profile.
  **Answered (Shayne, 2026-11-15):** "we need to fix the way the AI handles X spells for sure, but another time" —
  Unexpectedly Absent stays one cast per X × target; how the AI handles X spells is AI-19.26 (backlog, a note only).
- 2026-11-15, Claude — **4.4d2b's notes: five cards on four pieces' options; the plans held, two grew on reading.**
  **The lock** (`CantCastOrActivate`: Archon of Emeria, Voice of Victory, Clarion Conqueror) has five options —
  `By` (whose, from its holder), `If` (when, as its holder's), `Spells` / `Abilities` (which of the two),
  `SpellsEachTurn` (Archon: it binds a player who has cast that many — the asking PLAYER's count, which a
  holder-side `If` can't say) and `Of` (Clarion: the sources — a permanent, never a card in a hand or graveyard).
  `Combat.LockedDown` is told the card cast or the ability's source. Grown: Yuriko's `DuringCombat` flag became the
  condition `DuringCombat` in `If` and she moved onto it (one "when", not a flag beside a condition), and a
  permanent's lock is looked for always (the "only during combat" ceiling is gone). Grown: the Conqueror stops a
  Mox and an Elf but not a land, so a payment can't ask "is this player's mana locked" once — `ManaPayment.Find`
  takes the locks once (`Combat.Locks`) and asks `Combat.Stops` of each source.
  **Voice's mobilize** is `CreateTokens { Tapped, Attacking, SacrificeAtNextEndStep }`: `Attacking` is
  `EnterBattlefield(attacking:)`, Kira's stamp; tapped stays its own word.
  **The Wandering Emperor:** `JaceLoyaltyAtInstantSpeed` → `LoyaltyAtInstantSpeed(filter)`, asked of the
  planeswalker — the player's in a `ThisTurn` (Jace's Machinations moved: `Is.Subtype("Jace")`, and has a test that
  no other planeswalker gets it) or the planeswalker's own (`Is.EnteredThisTurn`). Only her own is read from the
  battlefield: no card gives it to another.
  **Anointed Peacekeeper:** `ChoosesNameAsItEnters { LooksAtHand, AnyName }` (`CardRegistry.NonlandNames` →
  `CardNames(s, lands)`); the ability tax is `CostChange { Abilities }`, read by `Costs.OfAbility` for everyone's
  abilities — `By` isn't read there (a revert-check showed no test could see it: the card's tax is everyone's).
  **Nothing offers an action per combination:** the name is a choice (Kai Winn's), a lock only removes actions.
  **Left as ceilings (DesignNotes):** a LOYALTY ability of the named card isn't taxed (the loyalty action pays no
  mana); mobilize's tokens attack the player, never a planeswalker; the X bound and the AI's mana count don't ask
  the mana lock (Sen's was so already).
  **`/pre-commit`'s review, fixed:** a mana ability that COSTS mana (`WithCostlyMana`: Starfleet Academy under Sen
  Triplets, an artifact's under the Conqueror) was still used to pay under a mana lock — it asks the locks too (a
  test); the lock and the ability tax were looked for on both battlefields at every cast, activation and payment —
  the statics board notes once per state whether either is in play at all (`Characteristics.RuleWork`, a gained
  one too: a test), so with none nothing is scanned; `Combat.Stops` → `Stopped` (it shared the name of the
  priority-stops class) and a shadowed `spell`; `ponytail:` marks on the two ceilings that had none.
  **Not taken, and why:** `AnyName` stays a yes/no, not a filter — the menu is read from the sets' PRINTINGS
  (`CardData`, cached by set), which a `Filter` over cards in the game can't be asked of; a type mask when a third
  naming card differs. The hand look in `EnterBattlefield` is a third two-line copy of `LookAtHand`'s loop: one
  helper would touch two old effects (MC-6's kind of work). `By` on a "this turn" lock isn't read — it is put with
  the player it binds (`ThisTurn.On`), said on the option. **AI-only, backlog:** the AI names the Peacekeeper's
  card by Kai Winn's rule (AI-19.27).
- 2026-11-15, Claude — **4.4d3's notes: two cards on the widened event; the plan held, and grew in four places.**
  **The event** — `MoveCard` raises `ChangedZoneEvent` for a permanent leaving the battlefield (any kind, a token
  too — after the review, only one that reads its own leaving: below), to
  the zone it really went to (after the exile-instead replacements). Grown: it carries the permanent AS IT LAST WAS
  (`LastKnown`), because "when this leaves" looks back (CR 603.10a) — and the reader is the one "when this dies"
  already had: `ReadFrom.TheDyingCard` became `TheCardThatLeft`, and `Triggers.Collect`'s look-back is one local
  (`LookingBack`) for a death and a leaving, so a doubler repeats both. A death raises both events; `SelfDies` reads
  only `DiedEvent`, `TriggerEvent.SelfLeaves` only the zone change (a revert-check: without that a dying Apparition
  made two tokens, and a Sanguine Evangelist a Bat for any leaving). `Trigger.From`/`NotFrom`/`To` are one test
  (`ZonesFit`) for both readers. **Laelia moved with it:** her "no From" meant "the two zones the event is raised
  for", so she says `NotFrom = [Battlefield]`.
  **Skyclave Apparition** — `Exile { Linked }` (The Measure of a Man's), then `When(SelfLeaves, CreateTokens(Illusion,
  1) { ControlledBy, Size })`. Grown: (1) `CreateTokens.Size`, one `Count` for power and toughness; (2) "the exiled
  card's OWNER" — `Ref.OwnerOfTarget(i)` generalised to `Ref.OwnerOf(ref)` (`RefKind.OwnerOf`; the old helper is
  `OwnerOf(Target(i))`, its cards unchanged); (3) "the exiled card" asked by an ability whose source has LEFT:
  `Is.ExiledBySource` compared the link with the source as it is now, so nothing once it was gone — a resolving
  ability now asks as the object it came from (`FilterContext.SourceIncarnation`, from the context's source
  snapshot); a static or a cost has none and asks the live source as before. **An old card plays differently, and
  rightly:** The Measure of a Man's chapter III returns its card though the Saga left in response (CR 607.2a; a test).
  **Serra Paragon** — `MayPlayFromZone(Graveyard, a permanent card of mana value 3 or less) { OncePerTurn, Gains }`:
  `Gains` is carried as `Finality` is (`OnStackComponent.Gains` → `EnterFromStackAction` → `EnterBattlefield(gains:)`),
  and a LAND played this way gets it in `PlayLandAction` — Finality never needed that path. It is an `UntilEndOfTurn
  { Trigger }` no turn ends: there until the permanent leaves. The gift is `SelfLeaves with { To = Graveyard }` —
  any permanent, so a land too, which no `DiedEvent` sees.
  **Nothing offers an action per combination:** an event, a filter, a ref and two options; the Paragon's casts are
  the graveyard's cards that fit, once a turn (The Holodeck's).
  **Lines that went (no test could see them):** `SelfLeaves`' `From = Battlefield` — the card that left is its own
  From; `OwnerOf`'s `Distinct()` — every reader takes the first.
  **Left as ceilings (DesignNotes):** the look-back asks "had it lost all abilities" of the state now (an Aura the
  state check already took is missed; `DiedEvent.Attached` answers it for deaths only); several cards exiled by one
  Apparition (its trigger doubled) make one token of the greatest mana value for the first owner.
- 2026-11-15, Claude — **4.4d4's notes: Balance on one count; the plan held, and grew in two places.**
  **The count** — `Count.BeyondTheFewest(ref)`: how many more of [ref] the player being acted on (`Ref.It`) has than
  the player with the fewest — "the rest". Grown: a `Count` is one `Of` with a sign and three ints (`Per`, `Times`,
  `Plus`), never one count less another, so "the rest" is a kind of its own; it takes a zone and a filter
  (`BeyondTheFewest(Battlefield, Is.Land)`, `(Hand)`), so it counts any zone, and it is 0 where no player is being
  acted on (the AI pricing an effect).
  **The two effects** — `EachSacrifices { N }` (a `Count`, 1 unless said; as many of it as the player can
  sacrifice) and `Discard(who, count)`. Grown: `Discard`'s `int` became a `Count` (an int converts, so its 25
  cards' scripts are unchanged). Both count for each player with `Ref.It` that player, as `PutCounters` counts for
  each object, and ask ONE choice of exactly that many — only of a player who has more than none.
  **Balance** is `Spell([], EachSacrifices(lands), Discard, EachSacrifices(creatures))`, in printed order; an
  effect's choices finish before the next is counted (a test: a land creature sacrificed as a land is no creature
  by then, so the opponent's one creature goes too). The player picks what to GIVE UP, not what to keep — the same
  sets.
  **Legal actions, counted before building:** on a 12-creature board Balance is ONE cast (no target, no X). As it
  resolves at most three choices are asked — lands, hand, creatures, and with two players only the one who has more
  — each ONE answer of exactly N, never an action or a choice per set (12 creatures against 1: one choice of 11, a
  test). The beam branches a choice of several picks up to 32 of them (`BeamSearchAi.MaxPicks`; permanents are each
  their own identity, so it stops enumerating at the 33rd) and past that takes `GreedyAi`'s one answer.
  **Left as a ceiling (DesignNotes):** `Evaluator.Effect` prices `EachSacrifices` as one sacrifice whatever its N
  and reads `Discard`'s count with no player acted on (nothing it prices has either). **AI-only, backlog
  (AI-19.28):** the greedy answer to "sacrifice N" is the first N offered, not the N worth least.
  **`/pre-commit`'s review, fixed:** (1) the count was NEGATIVE where `Ref.It` is no player (a counter or damage
  for each permanent: 0 less the fewest) — it is what one player has beyond the other, never below 0 (a test), and
  two counts, not three; (2) written with a ref that didn't say `Ref.It` it was silently 0 and the card inert — so
  it takes the zone and filter and writes the ref itself; (3) **Emrakul's ward moved onto the option:**
  `SacrificePermanents(N)` was `EachSacrifices(Ref.You, Is.AnyCard) { N }` beside it, and is gone (a revert-check
  on her ward test); (4) the choice is built once, with no dead `required`. **Not taken:** `SacrificeCost`'s own
  "sacrifice five Treasures" pick (Magda) is a cost's payment, with no effect context to run the effect in; the
  evaluator's per-player count for `Discard` (no card it prices has one, so no test could see the line — a
  `ponytail:` instead).
  **`/pre-commit`'s review, fixed:** (1) the event is raised only for a permanent that READS its own leaving
  (`MagicState.ReadsItsLeaving`: a printed or gained `SelfLeaves`) — raised for every permanent, each Treasure
  sacrificed or permanent bounced staged an event, and so a `Collect` and a state-check round in every search
  node; "whenever ANOTHER permanent leaves" drops the guard when a card reads it (a test that a plain permanent
  stages nothing). (2) Two more readers of "exiled with this" had their own copy of the old test and now
  disagreed with the filter — `ExiledWithSourceShareACardType` (All Good Things...) and
  `AssimilateExiledWithSource` (Borg Cube) ask `Is.ExiledBySource` too, so each still finds its cards when its
  source left with the ability on the stack (a test each). (3) The Paragon's "mana value 3 or less" was asked of
  the card in the graveyard, where X is 0: Engineered Explosives could be cast for X = 5 — the cast's validation
  passes its X and `Playable.ZonePermission` asks the filter of the card as that spell (a test: {X}{1} is offered
  for X 0 to 2). (4) `FilterContext.SourceIncarnation` is nullable, not -1: -1 is also what
  `ExileChosenLinkedAction` stamps for "no object", and the two would have matched (a test). (5) `Ref.OwnerOf`
  names each owner once (a test: two cards of mine gain me 1). (6) Stale comments (`EventKind.ChangedZone`,
  `Triggers.Collect`'s summary).
  **Not taken, and why:** the gift's "exile it" finds the card by id in the graveyard, so one returned and put
  there again before the trigger resolves is exiled though it is another object — every "when this dies, return
  it" reads `Ref.EventCard { From = [Graveyard] }` the same way (MC-6's kind of work: one fix for all of them).
  A permanent moved from one battlefield to the other counts as leaving: `MoveCard` already treats it so (a new
  object, its exile-until links returned), and no script does it. The event's `By` stays the OWNER (Vash's and
  Laelia's "your"); a "permanent you control leaves" would want the controller — with the widening above.
  `MagicCore/CLAUDE.md`'s "MoveCard owns leaving it" bullet is not touched (206 lines against 200): the rule is in
  `magic-triggers.md`.
- 2026-11-15, Claude (cube lane) — **4.4e: blue's tier 1 (35) read card by card: 12 are tested pieces, 23 need an
  option; none needs a sibling piece** (MC-6's rule, each read against the nearest piece's code). Parts:
  **e1** the 12 (list in Tasks) — a card whose FIRES test shows a missing option moves to an option part;
  **e2a** the library's top, 4 — Ponder, Stock Up, Consult the Star Charts, Thassa's Oracle: `LookAndTake` takes
  ONE card (`MinChoices` 0 or 1, no most) to the hand or the battlefield and the rest go down at random — how many
  (Stock Up's two, Consult's two when kicked: a `Count`), where the taken go (the Oracle's "up to one of them on
  top") and "put them back in any order" (Ponder: Surveil's `OrderLibraryTopChoice`; its "you may shuffle" a `May`
  over the shuffle, which is an action and no effect yet) are options on it; the Oracle's "if X is greater than or
  equal to the number of cards in your library" is `AtLeast(Count, int)` with a counted bound (`Count.DevotionTo`
  exists);
  **e2b** draws, 4 — Faerie Mastermind, Hullbreacher, Narset (Parter of Veils), Jace (Wielder of Mysteries):
  `DrewCardEvent` carries only the player, so "their second card each turn" is a number on it, stamped as the card
  is drawn (as `SpellCastEvent.SpellNumber` — a tally read at the next priority would miss two cards drawn at
  once); `NumericReplacement(Draw)` is times and plus: Hullbreacher's "instead you create a Treasure", Narset's
  "can't draw more than one" and Jace's "you win the game instead" are an outcome on the draw's replacement, in
  `MagicState.Draw`, the one place (Narset's −2 and Jace's two loyalty abilities are tested pieces);
  **e2c** mana and costs, 5 — Gitaxian Probe and Phyrexian Metamorph (a SPELL's `{U/P}`: only an activation
  chooses life today, `ManaPayment` throws; the Metamorph's "except it's an artifact in addition" is an option on
  `MayEnterAsCopy`, beside `NotLegendary`), Urza (Lord High Artificer) ("Tap an untapped artifact you control: Add
  {U}" — every mana ability taps its own permanent, and a cost that picks is a move per candidate: the artifact
  tapped is the payment's, as convoke's creature; his {5} is `ExileTopPlayable { ShuffleFirst }`, tested), Forensic
  Gadgeteer (`CostChange { Abilities }` reduces as it taxes, the filter asked as its holder — but "can't reduce the
  mana in that cost to less than one mana" is a floor it lacks), Thieving Skydiver (`KickerComponent`'s mana is
  fixed: "Kicker {X}" pays the cast's X; "attach it to this creature" is `AttachSourceTo` the other way round);
  **e2d** casting off the hand, 4 — Bilbo (`CostChange` asks the spell, never where it is cast from; "you may cast
  an artifact, instant, or sorcery spell from your graveyard" as it resolves is `CastFromHandFree`'s choice over
  another zone, paid, with flashback's exile), Malcolm ("you may cast the discarded card without paying" — `CastNow`
  takes an id, not `Ref.Chosen`), Flash and Transmute Artifact ("sacrifice it unless you pay its mana cost reduced
  by {2}", "you may pay {X}, where X is the difference": `PayMana`'s cost is fixed);
  **e2e** "for as long as this remains on the battlefield", 2 — The Wondrous Wasp, Tishana's Tidebinder: "loses all
  abilities" is until end of turn, for good, or under an Aura — a duration tied to the source (this object of it),
  as Lwaxana's `UntilUntapped`; the Tidebinder's "that permanent" is the countered ability's source
  (`TargetSpec.Ability` and countering one are tested);
  **e2f** choices after the cast, 4 — Frantic Search and Time Spiral (`Untap` untaps all a `Ref` names: "untap up
  to three lands" is one choice on it; Time Spiral's "Exile Time Spiral" is `ExileSourceAndCopiesFromGraveyard(0)`),
  Mystic Confluence (below), Occult Epiphany (`Count.CardTypesInAllGraveyards` is Tarmogoyf's own kind: "card types
  among [a Ref]", here `Ref.Chosen`, and Tarmogoyf moves onto it).
  **Legal actions, counted on a 12-creature board before planning:** every e1 card is one cast or activation per
  single target or sacrifice (Boomerang Basics 24 with 12 nonland permanents a side, Tinker one per artifact,
  Cryogen Relic one per tapped creature) or a trigger's target choice — none per set. **Mystic Confluence is not
  e1:** `ModeChoice.Repeats` (Fiery Confluence's) is one cast per multiset of modes × SET of targets, so "return
  target creature" three times is C(12,3) = 220 casts, twice 66 (× the spells to counter), 378 with one spell on
  the stack — its repeated modes' targets become a choice after the cast (`ChoosesTargets`, as `PerX` and `Divide`),
  and Fiery Confluence moves with it (220 against 12 artifacts today).
- 2026-11-15, Claude — **4.4e1's notes: 12 cards, scripts only — no engine change, no card moved to an option
  part.** Mystical Tutor is Enlightened Tutor's search; Spellseeker and Trinket Mage `SearchLibrary { Filter =
  Is.ManaValueAtMost(n), Reveal }` on entering (the search's "may" is its choice of none); Tinker
  `AdditionalCostsComponent([SacrificeCost(Is.Artifact)])` — one cast per artifact, none without one; Snapcaster Mage
  is Stingcaster Mage's script; Displacer Kitten `Flicker` on `YouCastNoncreatureSpell` with an optional target;
  Boomerang Basics asks "if you controlled that permanent" BEFORE the return and draws after it (`If { Otherwise }`,
  the return in both branches); Upheaval `MoveTo(Ref.Permanents(Is.AnyCard), Hand)`; Cryogen Relic and Sewer-veillance
  Cam read "enters or leaves" as two triggers (`SelfEnters`, d3's `SelfLeaves` — sacrificed for its own cost, each
  still triggers).
  **Timetwister and Echo of Eons** share one helper (`LsvCards.Timetwister`): two `MoveTo(Ref.All(zone, null,
  Ref.EachPlayer), Library) { Shuffle }` and `DrawCards(Ref.EachPlayer, 7)` — the spell is on the stack, so it isn't
  shuffled in, and flashback exiles the Echo. **A ceiling (DesignNotes, `ponytail:`):** `MoveTo` shuffles for each
  card moved, so a player with no hand and no graveyard doesn't shuffle; one shuffle effect by `Ref` comes with
  Ponder's "you may shuffle" (e2a), and the helper takes it then.
  **Sewer-veillance Cam's "you may tap or untap target creature"** is one yes/no: tapping a tapped creature and
  untapping an untapped one do nothing, so the only real choice is whether to turn it (`May` → `If(Matches(target,
  Is.Tapped), untap) { Otherwise = tap }`) — the printed choices, no option needed.
  **Test helpers:** `Lsv(name)` and `Put(card, player, zone)` moved up into `FraBoardTest` (their nine identical
  copies in the LSV and FRA test files are gone), and `CastAsking` with them (2d's; `ResolveAsking` beside it for an
  activation), `Offered<T>(asked)` and `TapIt(permanent)`.
  **`/pre-commit`'s review: no bug in the scripts; the tests had holes, fixed** — each new assertion shown to bite
  by a revert-check (9/9 caught): the three searches assert "reveal it" (the opponent knows the card), Timetwister
  and Echo of Eons that the library was SHUFFLED (the seven drawn aren't the seven that were on top — one test with
  only a graveyard to shuffle in, one with only a hand, so each `Shuffle` line is seen), Displacer Kitten that its
  target is "up to one", and the scripted costs are counted in lands tapped (Echo's flashback {2}{U}, Cryogen Relic's
  {1}{U}, the Cam's {3}{U}, Snapcaster's "its mana cost").
  **Left as ceilings (DesignNotes):** "you MAY search … then shuffle" (Spellseeker, Trinket Mage, as Stoneforge
  Mystic) always shuffles — a `May` around the search would ask a yes/no on every entry; the search's own "none
  chosen: no shuffle" goes with e2a, where known tops begin to matter. The search prompt reads badly with a
  mana-value `FilterText` and a two-type search (the table's wording).
  **Not taken, and why:** `MoveTo` shuffling each owner once after its loop (a smaller fix than a shuffle effect) —
  an engine change that moves the random stream of every recorded game with a shuffled return, in a scripts-only
  part, and it still wouldn't shuffle for a player with nothing to move: e2a's shuffle effect does both.
  `Ref.All(zone, null, Ref.EachPlayer)` restates `Ref.All`'s default (every player, yours first): kept, because it
  reads as the card does ("each player") and a change of the order players are visited would move the recorded
  games for nothing.
- 2026-11-15, Claude — **4.4e2a's plan (the library's top, 4 cards), from the pieces' CODE — two of e1's five
  findings changed on reading.** Legal actions on a 12-creature board: Ponder, Stock Up and Thassa's Oracle are one
  cast each, Consult the Star Charts two (kicked and not); everything picked is a choice after the cast.
  **`LookAndTake` takes its three flags' cards with it** — it had two flags for where things go, each one card
  phrase (`OntoBattlefield`, `RestToGraveyard`), and the part needs a third destination for each, so both become
  an option with a value: **`To`** (a zone: the hand; the battlefield — Hit It!, Loot move onto it; the library,
  its TOP — Thassa's Oracle's "up to one of them on top", the card simply stays above the rest and its owner knows
  it), **`Rest`** (`BottomAtRandom`; `Graveyard` — Pattern Buffer Preservation, Hit It! move; `TopInAnyOrder` —
  Ponder: Surveil's `OrderLibraryTopChoice` and `PutOnTopInOrderAction`, asked through one helper both use, the
  cards made known to their owner as a surveil's are) and **`Take`** (a `Count`, 1 unless said: Stock Up's two;
  Ponder's none — no pick is asked; with `Must` exactly that many, else up to). No new choice type: the pick is
  `ChooseAmongChoice`, the order `OrderLibraryTopChoice` (the AI's rule, `LandUnlock.Order`).
  **Consult the Star Charts needs no conditional `Count`:** `If(WasKicked, [take two]) { Otherwise = [take one] }`
  over the same look (`Count.LandsYouControl`) — "instead" is the printed shape.
  **The shuffle is ONE piece, an effect by `Ref`: `ShuffleLibrary(who)`** — `ShuffleLibraryAction(playerId)` goes,
  and the search's "then shuffle" uses the effect (`Ref.Id(owner)`), so there is one shuffle step, not an action
  beside an effect. Ponder's "you may shuffle" is `May` over it. **`LsvCards.Timetwister` takes it** (Timetwister,
  Echo of Eons): the two `MoveTo` lose `{ Shuffle }`, then `ShuffleLibrary(Ref.EachPlayer)` — each player shuffles
  once, a player with no hand and no graveyard too; its DesignNotes ceiling and `ponytail:` go. **A golden-out
  move there is intended**: one shuffle a player instead of one a card moved is a different random stream in every
  recorded game that casts either.
  **"You MAY search … then shuffle" is `SearchLibrary { May }`**: no card taken, no shuffle (the player declined
  the search — a known top stays known). **Twelve scripted cards say "may search", not only the three the survey
  named** — LSV: Stoneforge Mystic, Spellseeker, Trinket Mage, Path to Exile and Erode (`ItsControllerMayFetch`);
  TRK: 70,000 Light-Years from Home, Andromedan Automatons, H.M.S. Bounty, Warp Core, Arcum Dagsson; FRA: Hexhaven
  Invigorator, Simulacrum Shaper. **LSV's five move onto it; FRA's two and TRK's five do not, yet** (below: Open).
  A search that isn't "may" (Mystical Tutor, landcycling) still shuffles with nothing found (CR 701.19).
  **Golden-out moves for these too** (a random game where a "may" search took nothing no longer shuffles):
  intended.
  **Open (for Shayne): the seven FRA and TRK "may search" cards.** Built with `May = true` on all twelve first:
  the engine was right and every card's test passed, but the full suite then failed three AI tests on kept TRK
  game records (`EveryKeptRecord_StillReplays`: BorgCubeNotCast91; `ASpacecraftPastItsThreshold_IsNotChargedFurther`;
  `TheCerritos_BlocksTheBorg_NotTheRomulanAMinusOneFinishes` — a declined search in the record no longer shuffles,
  so the random stream moves and the record is stale; confirmed by taking the seven back off: all three pass).
  Those records are the AI lane's test cases and can only be re-kept from new games. **Default taken: the seven
  stay bare `SearchLibrary`** (they shuffle when declined, as before — DesignNotes names them), and the AI lane
  adds `May = true` to them when it can re-keep or drop those records. The other way round — move them now and
  re-keep three records in this lane — is his call, not a cube part's.
  **Thassa's Oracle needs no engine change for its win:** `AtLeast` already has a counted bound, `MoreThan`
  (Robber of the Rich's) — `AtLeast(Count.DevotionTo(Blue), 0) { MoreThan = the cards in your library }` is "X is
  greater than or equal to the number of cards in your library"; e1's survey read the positional signature only.
  Devotion is counted as the trigger resolves, twice (the look, the win), with nothing between that changes it.
  **Not built:** several cards taken to the top keep their old order (no card says "put two on top in any order"
  of a look yet).
- 2026-11-15, Claude — **4.4e2a's notes.** Built as planned but for the Open above. **Test helpers:**
  `OnTop(card[, player])` moved up into `FraBoardTest` (fifteen private copies gone; three of them went through
  `MoveCard`, which marked the card known to its owner — the one helper leaves it unknown, and no test read that).
  **`/pre-commit`'s review: no bug in the four scripts, the Timetwister change or the search; three engine points
  fixed, each with a test that fails without it.** (1) A card its owner KNEW (Ponder's look, a surveil's kept card)
  stayed "known" when a later look sent it to the bottom in a random order — the reorder never forgot it, and
  Ponder's and the Oracle's new marks make that common: `MagicState.ToBottomAtRandom` is now the one way a
  library's own cards go to its bottom at random (`LookAndTake` and `RevealUntilOntoBattlefield`), and it forgets
  as a shuffle does; the random stream is unchanged. (2) `LookAndTake.To` naming any zone but the hand, the
  battlefield or the library fell silently to the hand: it is the zone it names. (3) `Rest` and `TheRest` name
  their cards; the "may" search's ceiling is a `ponytail:` in the code.
  **Not taken, and why:** `PutLookedAtAction` carrying the `LookAndTake` record instead of repeating its four
  options (a cleanup of a shape it already had — three options before); `MoveTo { Shuffle }` replaced by a move
  and one `ShuffleLibrary` (four FRA and TRK single-card users: the TRK pair would go from two shuffles to one and
  stale kept records — it goes with the Open); the AI's answers (its pick of two, Ponder's order by the surveil
  rule): AI-19.29. **Left as ceilings (DesignNotes):** a "may" search can't be made and failed on purpose to
  shuffle; `ExileTopPlayable { RestOnBottom }` still marks its rest known to their owner.
- 2026-11-16, Claude — **4.4e2b's plan (draws, 4 cards), from the pieces' CODE — the survey's two findings hold,
  each smaller than it read.** Legal actions on a 12-creature board: each card is one cast; Faerie Mastermind one
  activation; Narset's −2 one; Jace's +1 two (a target player), his −8 one. Nothing per set.
  **"Their second card each turn" is `Trigger.Nth`, not a second number beside `SpellNumber`:** `Trigger.SpellNumber`
  (it refuses every kind but `SpellCast`) is renamed `Nth` and fits `SpellCast` and `Drew` — Stella Lee and
  Cori-Steel Cutter move with it. `DrewCardEvent(PlayerId, Number)`: the drawer's count this turn
  (`TurnTally.CardsDrawn`) stamped as each card is drawn, so two drawn in one resolution are the first and the
  second. Faerie Mastermind is `new Trigger(Drew) { By = Opponent, Nth = 2 }`; its {3}{U} is `DrawCards(
  Ref.EachPlayer, 1)`, Timetwister's.
  **The three "instead" are TWO options on `NumericReplacement(Draw)`, read in `MagicState.Draw`:** `Instead`
  (effects run in place of ONE card's draw, as the rule's controller — Hullbreacher `[CreateTreasure]`, Jace
  `[WinTheGame]`, Narset `[]`: "can't" is nothing instead) and `If` (a `Condition` judged from its holder's side,
  `Ref.It` the drawing player — Jace `YourLibraryIsEmpty`, Narset "has drawn one this turn"). `Draw` applies the
  numbers first (Spock's two), then asks for each card: an empty `Instead` first ("can't" beats a replacement —
  Narset beside Hullbreacher makes no Treasure), else the first found; asked BEFORE the empty-library flag, so
  Jace wins and a Narset-stopped draw from no library loses nothing. `Replacements.Apply` reads `If` too. The
  effects are spawned (a pipeline, the holder's context) as the shockland's `May` is from `EnterBattlefield`:
  they run before the resolving ability's next step.
  **Narset's count needs no new kind:** the seven "you … this turn" counts read the controller's tally;
  `Of` (unset: you) names whose — `Count.CardsYouDrewThisTurn with { Of = Ref.It }`.
  **Hullbreacher's "except the first one they draw in each of their draw steps" is `ExceptTheTurnsDraw`**, an
  option on the same replacement: the draw step's own draw passes `turnDraw` to `Draw`. Read against the
  alternative — a step condition and a per-step tally, three pieces for one phrase: `DuringYourTurn` and
  `DuringCombat` would have to become one condition with a step and a "whose", and their cards move. **A ceiling
  (DesignNotes, `ponytail:`):** when the turn's draw didn't happen (the starting player's first turn), another
  card drawn in that draw step isn't excepted.
  **Tested pieces:** Narset's −2 is `LookAndTake(4, !Creature & !Land) { Reveal }`; Jace's +1 `Mill(Target, 2)`
  and `DrawCards`, his −8 `DrawCards(7)` then Fblthp's `If(YourLibraryIsEmpty, [WinTheGame])`.
  **Not built:** which replacement applies when two would (CR 616.1, the drawing player's choice) — the first
  found, player one's permanents first.
- 2026-11-16, Claude — **4.4e2b's notes.** Built as planned, but the spawned effects get the holder's context
  alone (no `Ref.It`: no `Instead` reads the drawing player yet). A replaced card isn't drawn — no tally, no
  `DrewCardEvent` — so Hullbreacher beside Narset on an opponent's main-phase draw is three Treasures (they never
  drew a first card); Narset stops them only after a card they did draw (their draw step's). 14 tests (15
  cases) in `LsvBatch2eTests.cs`, 33/33 breaks caught. **Test helpers there:** `DrawsThree(caster, target)`
  (Ancestral Recall), `ToTheirDrawStep()`, `Treasures(player)`, `JaceWielder(loyalty)` and `Activate(walker,
  ability, targets)` — NOT moved up to `FraBoardTest`: `FraTriggerTests` and `FraCardTests` each have a private
  `Loyalty(walker, cost)` of another meaning, and 2d's `Loyalty(walker, index)` stays where it is for the same
  reason. **Left as ceilings (DesignNotes):** the turn's draw is the excepted one; two replacements of one draw;
  `NumericReplacement.If` on the numeric events is read and untested (no card).
  **`/pre-commit`'s review: ten findings, no bug in the four scripts; one engine fix, with its test (33/33 breaks
  caught):** the turn's draw is ONE card — a second that Spock makes of it is any other draw to Hullbreacher
  (`turnDraw && i == 0`). `tools/survey.py` still named `Trigger.SpellNumber` (Flurry): fixed. **Documented, not
  changed:** the replacing effects are spawned, so they run after the action that drew (DesignNotes); an
  `Instead` in a `ThisTurnReplacement` isn't read (its summary says so). **Not taken, and why:** a scan of both
  battlefields per card drawn (no bench for a card batch — the rule stays whatever it costs; collect the rules
  once per `Draw` if a profile shows it); null against empty `Instead` (no MagicCore state is round-tripped
  through JSON — records replay by token); `Of` on a turn count reading one player, not a sum (it names whose).
  **Open (for Shayne): `ExceptTheTurnsDraw` is a bool on the piece and a `turnDraw` argument** — the review
  reads it as a one-card flag against MC-6's rule. The other way is a draw-step count in `TurnTally` and a step
  condition in `If` (`DuringYourTurn` and `DuringCombat` generalised, their cards moved): three pieces, and it
  closes the first-turn ceiling. Default taken: the flag.
- 2026-11-16, Claude — **4.4e2c's plan (mana and costs, 5 cards), from the pieces' CODE — the survey's five
  readings hold; two grew (Urza's {5} isn't tested, the Skydiver needs more than a kicker).** Legal actions on a
  12-creature, 12-artifact board: Gitaxian Probe one cast per target player × life paid (4); the Metamorph one cast
  per life paid (2), the copy a choice as it enters (24 permanents); Urza one cast, his mana NO legal action, his
  {5} one activation; the Gadgeteer one cast; the Skydiver one cast unkicked and one per X kicked (X from 1 to the
  mana there is), the artifact a choice of its trigger. Nothing per set.
  **A spell's `{U/P}` is the activation's option on the cast:** `CastSpellAction.LifeForPhyrexian` — one cast per
  number paid with 2 life (`MagicRules.CastCandidates`), `CostToPay` turns the rest into their colour
  (`ManaCost.PayingLifeFor`, the activation's), validated with `Life.CanPay`, the life lost after the mana; `|pN`
  in `MoveKey` only when paid, so old keys stay byte-identical. No FRA or TRK spell has a Phyrexian symbol: no
  record moves.
  **"Except it's an artifact in addition to its other types"** is `MayEnterAsCopy.AlsoType` (a `CardType` added to
  the copy's), beside `NotLegendary` — a value, not a flag: Phyrexian Metamorph `{ Of = Is.Artifact | Is.Creature,
  AlsoType = Artifact, Optional }`.
  **Urza's "Tap an untapped artifact you control: Add {U}"** is a `ManaAbilityComponent { Costs =
  [TapUntappedCost(1, Is.Artifact & Is.Yours)] }`: `ManaPayment.Find` reads the player's mana abilities whose ONE
  cost taps another permanent, and each untapped permanent their filter matches pays their colour, tapped — the
  same unit convoke's creature makes (convoke and Urza are one list of what tapping it for ANOTHER's cost pays; a
  permanent making one mana of its own unions both, as convoke does). Its `ChoosesCost` keeps it out of `YieldOf`
  (Urza isn't tapped) and `ManaMoves` (a tap slot is no sacrifice: no move). The X bound counts it. **Not a sibling
  of convoke:** convoke is the spell's keyword, Urza's an ability of a permanent — they meet in the one place both
  are units. **Ceilings (DesignNotes):** a Talisman tapped for Urza's {U} is paid as its own {U} (`PayAsUsed`: its
  damage, its activation event — convoke's too); tapping by hand can't use it (`TapOptions`).
  **Urza's {5} is `ExileTopPlayable { ShuffleFirst, Play = { ThisTurn, Free } }` — but `Free` on a mark isn't
  read:** only `Now` casts free today. `PlayPermission.For` carries `Free` onto `PlayableFromExile.Free`, and
  `Playable.FreeCast` answers it first (nothing to pay beyond taxes, so it replaces the paid cast, as Omnipresence's
  does). His Construct is `CreateTokens` with a counted static (`StaticEffectComponent(Is.Source) { Power,
  Toughness }`, Pyrogoyf's shape).
  **Forensic Gadgeteer's floor** is `CostChange.Floor` (an int: "can't reduce the mana in that cost to less than
  N"), read by `Costs.OfAbility`: the floored reductions apply after every other change and never below
  min(Floor, the mana value they started from) — two Gadgeteers on a Clue's {2} make {1}, a {0} stays {0}. Its
  "whenever you cast an artifact spell, investigate" is `Trigger(SpellCast) { By, Subject }` and `CreateClue`.
  **Ceiling:** "activated abilities of artifacts" includes mana abilities with a mana cost; none is reduced (a
  mana ability is no `ActivatedAbilityComponent`, as the Peacekeeper's tax).
  **Thieving Skydiver** is three options and one rename:
  `KickerComponent("{X}") { MinX = 1 }` — a kicker with X lets the KICKED cast choose X (`HasX` of the cost or the
  kicker; "X can't be 0" is `MinX`); the X is paid as any X (`addedGeneric`), and enters as `Card.CastX`.
  "If it was kicked" on a PERMANENT: `WasKicked` reads only the spell's `OnStackComponent`, gone as it enters —
  `EnterFromStackAction.Kicked` stamps `Card.CastKicked` beside `CastX` (the same carry of the cast onto the
  permanent, reset by `Fresh`), and `WasKicked` reads either, the source's snapshot too (a Skydiver bounced in
  response was still kicked). "Target artifact with mana value X or less": a filter's counted bound reads X as 0
  (`StatBetween.Bound`); it now reads the SOURCE's X — its `OnStackComponent.X` on the stack, else `CastX`
  (CR 107.3m) — no filter uses `Count.X` yet. "Attach it to this creature": `AttachSourceTo(Host)` becomes
  `Attach(What, To)` and its three users move (equip, Cori-Steel Cutter, Glimmer Lens) — under `If(Matches(target,
  Equipment))`.
- 2026-11-16, Claude — **4.4e2c's notes.** Built as planned but one line: the cast generator's "X from `MinX`"
  is gone — `ValidateAdd` already refuses a kicked X below it (legality lives there only), and the revert-check
  showed the generator's copy changed nothing. 10 tests (13 cases) in `LsvBatch2eTests.cs` — the Probe's two
  casts also checked as four DISTINCT moves (`MoveKey`'s `|pN`). **33 breaks, 31 caught at first:** the other
  was a vacuous test — an unkicked Skydiver's X is 0, so the mana-value-1 artifact it "didn't take" was never a
  legal target; it takes a mana-value-0 one now, and both its breaks are caught. **Test helpers:** `TapMyLands()`
  (what pays is the rest of the board), `KnownToMe(theirCard)`. **Left as ceilings (DesignNotes):** Urza's
  {U} is one mana per artifact, paid as the artifact's own (a Talisman's damage, its activation event — convoke's
  too), and never by hand; the Gadgeteer reduces no mana ability's mana cost.
  **`/pre-commit`'s review: ten findings; three bugs fixed, each with a test (13 tests, 16 cases; 36 breaks
  caught):** with Urza out every artifact's Lotus or Treasure sacrifice was thrown away (convoke's "only tapped"
  line saw his {U}) — his colour now joins after it; a free cast from Urza's mark counted as the card's OWN
  alternative cost (Baleful Mastery's opponent drew) — only the card's own `MayCastFromHandFree` is; a free
  Skydiver couldn't be kicked — kicker {X} is an additional cost (`CastSpellAction.XInKicker`). Also taken: Urza's
  rule no longer drops when another cost taps or sacrifices him (CR 601.2g); the life check only when life is paid;
  "does it choose X" is one `CastSpellAction.ChoosesX` for the generator and the validator; `AvailableMana` one
  method. **Documented, not changed (DesignNotes):** a SPELL's own X-bounded target filter reads 0 in the hand and
  Phyrexian life is offered for the printed cost's symbols only — no card has either; a free cast whose rule has
  costs chooses no kicker X. **Not taken:** a board flag to skip Urza's scan on every payment (no bench for a card
  batch, as e2b's). Test helper `UrzaExiles(card)`: his {5} with only that card in the library.
- 2026-11-16, Claude — **4.4e2d's plan (casting off the hand, 4 cards), from the pieces' CODE — the survey's four
  readings hold; three grew.** Legal actions on a 12-creature, 12-artifact board with a full hand: Bilbo, Malcolm
  and Flash one cast each, Transmute Artifact one cast — its sacrifice is in the EFFECT, not a cost, so the artifact,
  the library card, Flash's creature and Bilbo's graveyard card are each a choice made as it resolves (then, for
  Bilbo, the chosen spell's own casts: its targets, as any spell's). Nothing per card, nothing per set.
  **Bilbo's "Spells you cast from anywhere other than your hand cost {1} less"** is `CostChange { NotFrom = [Hand] }`
  — Trigger's option of the same name; not a filter: `Filter.cs` says "no zone in a filter — whoever supplies the
  candidates owns the zone", and `Costs.Of` is asked while the card is still where it is cast from.
  **"Whenever Bilbo attacks, you may cast an artifact, instant, or sorcery spell from your graveyard"** grew: a
  graveyard cast as an effect resolves is FLASHBACK today — `CastNow` skips the zone check, but `MoveCard` exiles any
  spell `CastFrom: Graveyard` without `ByPermission`, and `StackResolution` sends such a PERMANENT spell to the
  graveyard instead of the battlefield. A cast during a resolution is by that effect's permission (CR 608.2g), so it
  is `ByPermission` (Malcolm's card goes back to the graveyard, Bilbo's artifact enters) — unless the effect says
  "exile it instead": `CastSpellAction.ExiledLeavingTheStack`, flashback's exile. Nothing in FRA or TRK casts from a
  graveyard as it resolves (Tersa and Q exile first), so no record moves. A cast during a resolution no longer uses up
  a once-each-turn zone permission either (`Cast` asked `ZonePermission` for it). **Brazen Bluff's
  `CastFromHandFree(filter)` becomes `CastOneOf(Ref) { Free, Exiled }`** (and `CastChosenFree` → `CastChosen`): the
  cards are a `Ref` — Bluff's `Ref.All(Hand, !Is.Creature, Ref.You)`, Bilbo's graveyard, Malcolm's `Ref.Chosen` —
  paid unless `Free`, and `Exiled` the filter of the spells exiled leaving the stack (Bilbo: instants and sorceries).
  **Malcolm** — survey's reading held: `CastNow` takes an id, so "the discarded card" is `CastOneOf(Ref.Chosen)` in
  `Discard.Then`, under `If(AtLeast(Count.CountersOfKind(Ref.Source, "chorus"), 4))`; chorus is a named counter.
  **Flash's "sacrifice it unless you pay its mana cost reduced by {2}"** and **Transmute's "you may pay {X}, where X
  is the difference"**: `PayMana`'s cost is fixed. Its options: `ManaCostOf` (a Ref: the card's printed cost — Flash),
  `Generic` (a Count added: Transmute's X), `ReducedBy` (a Count of generic taken off — Flash's 2, Transmute's
  sacrificed mana value: "the difference" is the found card's MV reduced by the sacrificed one's, never one Count
  less another); `CanPay { Pay }` asks the same owed cost. "Unless you pay" must run its "otherwise" when it CAN'T be
  paid too, which `May` never does (not asked) — `Scripting.MayPay(prompt, pay, otherwise)` is `If(CanPay) [May]
  { Otherwise }`, both "no" and "can't" running it.
  **Transmute grew twice:** "the sacrificed artifact" — `MaySacrifice.Then` has no name for it (its pick is the
  permanent choice's key, which any `May` inside clears), so the Then reads it as `Ref.ThisWay`, as every "[done]
  this way" does (Destroy, CreateTokens); and the found card's zone depends on a payment, so `SearchLibrary.Then`
  decides it: run with the card in `Ref.Chosen`, still in the library, IN PLACE of putting it `To`, then the shuffle.
  Tiers: Malcolm 2 (`CastOneOf`'s Ref), Bilbo 2 (`NotFrom`, `ByPermission`/`Exiled`), Flash 2 (`PayMana`'s cost,
  `MayPay`), Transmute 2 (the same, `ThisWay`, `SearchLibrary.Then`). No new piece.
- 2026-11-16, Claude — **4.4e2d's notes.** Built as planned. 8 tests (11 cases) in `LsvBatch2eTests.cs`, all through
  real casting and real combat; **17 breaks, all caught** — one at the second try: the first break of Bilbo's
  `NotFrom` made the reduction apply from the hand too, which is the sorcery test's case (it caught it), so the
  artifact test's check broke it the other way (never applied). Brazen Bluff's test (`TrkBatch7aTests`) catches a
  `CastOneOf` that reads the wrong cards. **Ceilings (DesignNotes):** Bilbo's exile is flashback's (a bounced
  instant is exiled too); Transmute reads the sacrificed artifact's mana value in the graveyard (a token: 0).
  **Not taken:** moving `AttackWith`/`InExile` up to `FraBoardTest` — eleven test files have their own, five of them
  different (to the postcombat main; `InExile` of `me`), so a base one would hide or change them; this file's
  `AttackWith(answer, attackers)` plays through combat answering each choice.
  **`/pre-commit`'s review: ten findings; two bugs fixed, each with a test (10 tests, 13 cases; 19 breaks caught):**
  Flash putting in Phyrexian Metamorph threw in the solver (a printed `{U/P}` reached it) — `PayMana.Owed` pays it
  with its colour (a ceiling: never 2 life, DesignNotes); Bilbo casting a FLASHBACK card from the graveyard paid its
  flashback cost — a cast during a resolution is never flashback, so `Costs.Of` and `CastSpellAction.CostsOf` take
  `flashback: false` for it. Also taken: `SearchLibrary.Then` runs only when a card was found; MaySacrifice's two
  summaries made one; `CastChosen`'s summaries name Bilbo and Malcolm. **Not taken:** a sacrificed token's mana
  value (already a ceiling), a `CastOneOf` over cards in several zones (no card), `CastOneOf` building every cast
  of every card to ask "can it be cast" (no bench for a card batch; the probe cast Bilbo 326 times unhindered),
  `CanPay`'s unused `Cost` beside `Pay`, a `ManaCostOf` that names nothing paying {0} (Flash's card always exists).
- 2026-11-17, Claude — **4.4e2e's plan, read against the code.** Both readings grew.
  **"Loses all abilities for as long as [this] remains on the battlefield"** is not a duration on the stripped card:
  `UntilEndOfTurn` is read card by card (`Characteristics.Lost` has no state), so a link to the source there would
  need every reader to ask the battlefield. The nearest piece by MECHANISM is Infinite Coursework's
  `EnchantedLosesAllAbilities` — a holder that strips another object, collected live into the statics board's
  `Stripped`, over the moment the holder leaves. Its option: **`Object`**, the object it strips (id and
  incarnation) in place of what it is attached to — stamped on the SOURCE by the resolving effect, as Lwaxana's link
  is (status: gone as the source leaves, never copied), and read whether or not the source has lost its own
  abilities (an effect's result, not the source's ability; CR 611.2). The effect is `ModifyUntilEndOfTurn {
  LosesAllAbilities, WhileSourceRemains }` — a third duration beside `UntilYourNextTurn` and `ForGood`; with the
  source gone before it resolves it does nothing (CR 611.2b), the tap still happens.
  **The Tidebinder's "that permanent"** has no name: `Counter` returns nothing. Its option: **`Counter.Then`** with
  `Ref.ThisWay` what was countered — a spell's card, an ability's SOURCE while it is still that object on the
  battlefield (`StackAbility.Source`'s incarnation) — as every "[done] this way" (Destroy, MaySacrifice);
  "of an artifact, creature, or planeswalker" is a `Matches` on it.
  Legal actions: each is one cast (flash) and one target choice as its trigger goes on the stack — a choice among
  the creatures, or the abilities on the stack; nothing grows with the board. Tiers: Wasp 2 (`WhileSourceRemains`,
  `Object`), Tidebinder 2 (`Counter.Then`, the same strip). No new piece.
- 2026-11-17, Claude — **4.4e2e's notes.** Built as planned. 5 tests in `LsvBatch2eTests.cs`, all through real
  casting; **10 breaks, all caught** — one at the second try: without the "source gone" guard the stamp lands on the
  Wasp's card in the graveyard, which nothing reads, so the test now has the Wasp leave AND come back before its trigger
  resolves (a new object the effect must not name). `WithTap` moved up to `FraBoardTest`.
  **`/pre-commit`'s review: nine findings; four taken.** A real bug, fixed with a test: Tidebinder countering a
  DELAYED trigger stripped the card it sits on — Sneak Attack's "sacrifice the creature" countered by its own
  controller lost the sneaked creature its abilities. A delayed trigger's source is the source of the ability that
  made it (CR 603.7d), so it goes on the stack marked `StackAbility.Delayed` (`Pending.Delayed`, set by `PutOnStack`
  from the fired `DelayedTrigger`) and `Counter.Then` names no permanent for it. Also taken: `Object` is a
  `Filters.TheObject`, as Lwaxana's link (a record, not a tuple); `WhileSourceRemains` without `LosesAllAbilities`
  throws; `Then` doesn't run when nothing was countered. **Not taken:** phasing and a wrath with the Wasp (ceilings,
  DesignNotes), one helper for "stamp on this object of the source" shared with Lwaxana (two copies of two lines), dead
  links piling on a source that stays (one per resolution), `ThisWay` mixing a spell's card and an ability's source (no
  card reads both).
- 2026-11-17, Claude — **4.4e2f's plan, read against the code.** Two of the four readings grew.
  **Mystic Confluence's targets after the cast** are not `ChooseXTargetsChoice` (Dominion Infiltration's): that picks a
  SET from ONE word's candidates, and "counter target spell" beside "return target creature" twice is three words of
  two kinds. The nearest piece by mechanism is a copy's retargeting (`CopySpell`): one `ChooseTargetChoice` per word,
  in order, `NotThePrevious` read against every earlier pick — the trigger's own choice, which the search already
  branches one pick at a time and the look-ahead judges (`Lookahead.Chosen`). Its option: `CastSpellAction.
  ChoosesTargets` on a text whose `ModeChoice.Repeats` and whose chosen modes have a target word — one cast per
  multiset of modes (10 of three modes), the words chosen as it is cast (`ChooseTargets`), then cast with them
  (`CastWithChosenTargetsAction` reads either choice). Fiery Confluence moves with it, unscripted: its own text.
  **Legal actions, 12 creatures and 12 artifacts, three spells on the stack:** Mystic Confluence 576 casts as
  `Repeats` is built ([1,1,1] 220, [0,1,1] 3 × 66 = 198, [1,1,2] 66, …), Fiery Confluence 392 ([2,2,2] 220, [0,2,2]
  and [1,2,2] 66 each, …); after, at most 10 each, every pick at most 12 options. Frantic Search and Time Spiral one
  cast each (no target), Occult Epiphany one per X — linear in the mana.
  **"Untap up to three lands"** is `Untap { UpTo }` (a `Count`): a choice among what the `Ref` names, then those
  untapped. ponytail: the choice offers only the TAPPED ones you control and asks for as many as it can untap
  (min of N and them; unasked when that is all of them) — an untapped land does nothing untapped, an opponent's is
  never yours to want, and "up to" answered with fewer is never better, while the AI answers a choice of several
  with its minimum (`GreedyAi`, `Lookahead.QuickAnswer`): asked as "up to", it would never untap one.
  **Occult Epiphany's count** is `Count.CardTypesAmong(Ref)` (as `ColorsAmong`, `SubtypesAmong`): "card types among
  cards discarded this way" is `Ref.Chosen` in `Discard.Then` (Tersa's), Tarmogoyf's and Pyrogoyf's "among cards in
  all graveyards" `Ref.All(Graveyard)` — `CardTypesInAllGraveyards` goes.
  **Time Spiral** is `ExileSourceAndCopiesFromGraveyard(0)`, Timetwister's three steps (the helper becomes a list
  both scripts spread) and `Untap { UpTo = 6 }`.
  **Kept as it is (Open, for Shayne):** a repeated mode's word is still `NotThePrevious`, so Mystic Confluence can't
  counter ONE spell twice or three times ("unless its controller pays {3}", then {3} again — a real play: the tax
  grows). Lifting it is a line in `Modal.Compose`, now that targets are chosen after the cast rather than enumerated,
  but Fiery Confluence's test asserts "three artifacts" for its triple destroy (legal by CR 115.3 — the same artifact
  twice, never useful), and the brief keeps that test passing.
  Tiers: Frantic Search 2 (`Untap { UpTo }`), Time Spiral 2 (the same), Occult Epiphany 2 (`CardTypesAmong`),
  Mystic Confluence 2 (`ChoosesTargets` for repeated modes). No new piece.
- 2026-11-17, Claude — **4.4e2f's notes.** Built as planned. 4 tests in `LsvBatch2eTests.cs`, all through real casting
  (Mystic Confluence through the legal cast itself: at most 10 casts with 12 creatures and a spell on the stack, then a
  `ChooseTargetChoice` per word, the repeated word never offering the creature chosen before); Fiery Confluence's,
  Tarmogoyf's and Pyrogoyf's tests pass unchanged. **9 breaks, all caught — two at the second try:** a revert of
  `CardTypesAmong` to the number of cards still read 2 (the break was wrong-shaped: a popcount of 3), and dropping
  "you control" from `Untap.UpTo` was missed by Frantic Search's test — `ChooseAmongChoice` on the battlefield already
  offers only the chooser's, so the filter decides only when nothing is asked; Time Spiral's test now has an
  opponent's land tapped as all six of its own are untapped unasked. `LsvCards.Timetwister` became
  `TimetwisterSteps`, spread by both scripts.
  **Fiery Confluence moved 4 kept card-replay games** (`GoldenCardReplay.txt`: Constable Odo 02, Fiery Confluence 02,
  Greenhouse Propagator 03, Shatterwing Pegasus 01) — each first differs at a Fiery cast, now one of at most 10 with
  its target chosen after; no random replay, AI replay or AI-lane record (`MagicCore.Tests/Records`) moved. Re-kept
  with the batch's cards OUT (Shayne, 2026-11-17), so golden-out still means "the engine moved nothing else".
  **`/pre-commit`'s review: nine findings; four taken.** `ChooseXTargetsChoice.CanChoose` checks each run of a word and
  its `NotThePrevious` repeats (one per chosen mode) — it replaces a `TargetSpec.Combinations(…).Any()` that built all
  220 sets on every validation; one list of mode choices in `LegalActions`; `CastWithChosenTargetsAction.Words`
  carried from `ChooseTargets` rather than worked out again; `CardTypesAmong` counts the ids (no lists under
  Tarmogoyf's P/T). **Not taken:** a later word left with nothing when two repeated modes' filters overlap (no card's
  do: a ceiling, DesignNotes); the look-ahead answering the later words with the first legal target (as every
  trigger's; the search branches each word); `Words(modes)` composed per candidate cast (ten per Confluence); "untap
  up to 0" asking an empty choice (no card counts it); the renamed `CountOf` (records replay by move, never by
  component).
  **Shayne, 2026-11-17, on the two:** "untap up to N" as many as it can is fine ("we usually want as many lands
  untapped as possible"); the repeated mode's one-target limit, and every modal spell's enumeration of its targets,
  go to the backlog as one fix — MC-8.
  **DEV-5:** `batch_checks.py`'s probe150 ran `--no-build` straight after the revert step and found the LAST revert
  still in the DLL (Mystic Confluence casting with no targets chosen) — a crash that wasn't there; built, 150 games
  passed. The probe steps need a build after `revert`.
- 2026-11-17, Claude — **4.4f's plan (blue's tier 2, 6 cards), read against the code.** One part: the six share no
  piece, and each new option is small. What grew on reading:
  **Squad** (Sicarian Infiltrator) is `ReplicateComponent { AsItEnters }`: the cast is replicate's (one per number of
  payments, bounded by mana — `CastSpellAction.Replicate`), but no copy goes on the stack; the count rides the spell
  (`OnStackComponent.Replicate`) onto the permanent, and `Scripting.Squad(cost)`'s "when this enters" trigger makes
  `CreateTokenCopy(Ref.Source) { N = Count.TimesReplicated(Ref.Source) }` — a token copy wasn't cast, so its own
  squad makes none. `CreateTokenCopy` had no count: `N` is its option.
  **What a permanent remembers of its spell grew a third and fourth fact** (squad's count, evoke's alternative cost,
  beside `CastX` and `CastKicked`): rather than two more sibling fields, `Card.CastKicked` becomes `Card.CastAs` — the
  `OnStackComponent` it resolved from — and `CastX` reads it. `WasKicked`, `AlternativeCostWasPaid` and the new count
  read the stack's or the permanent's. Thieving Skydiver moves; its tests must pass.
  **Evoke** (Subtlety; Solitude, Endurance, Fury, Grief later) is `Scripting.Evoke(filter)`: Force of Will's
  `MayCastFromHandFree(Is.Source) { Costs = [ExileFromHandCost(colour)] }` — already stamped `AlternativeCost` — and
  "when it enters, if its evoke cost was paid, sacrifice it", an intervening if on `AlternativeCostWasPaid`, now read
  on the permanent. "Its owner puts it on their choice of the top or bottom" is `May { Who = Ref.OwnerOfTarget(0),
  Otherwise }` over two `MoveTo(…, Library) { From = [Stack] }` — tier 1 pieces.
  **Improvise** (Kappa Cannoneer) is convoke's unit in `ManaPayment.Find`: one answer, `ManaPayment.Helps(s, spell,
  permanent)` — a creature for a Convoke spell (its colour or {1}), an artifact for an Improvise one ({1} only) —
  read by the payment and the X/replicate bound (`AvailableMana`) alike; never a second flag beside `convoke`.
  **Scry** (Preordain) is `Surveil { To = Library }`: the chosen go to the BOTTOM (`MoveChosenToAction`), the rest
  are ordered on top as a surveil's; a scry is no surveil, so no `SurveilledEvent` and no tally. LookAndTake doesn't
  fit: its taken cards are a fixed `Take`, its `To = Library` is the top, and "any number to the bottom" is a choice of
  how many. ponytail: the bottom cards go in the order chosen, not reordered (CR 701.22a says any order).
  **Emry's "you may cast that card this turn"** has no piece that leaves a card where it is: every `PlayPermission`
  rides an effect that exiles first (`ExileTopPlayable.Play`, `SearchLibrary.Play`). New: `MakePlayable(What, Play)`,
  the same mark (`PlayableFromExile`) on a card in its owner's graveyard; `Playable.IsImpulsePlayableBy` reads the
  mark in exile OR a graveyard, and a cast from a graveyard by it is `ByPermission` (not flashback: it isn't exiled,
  and an artifact creature enters). Affinity is `CostChange(Count.NumberOf(artifacts you control).Negated) {
  OnItself }` (Myrel's domain pattern) — tier 1.
  **Lórien Revealed** is `Cycling("{1}", new SearchLibrary(Land, Hand) { Subtype = "Island", Reveal })` — "an Island
  card", not basic: tier 1. **Kappa's** ward is `Ward("{4}")`, its trigger a Subject `Is.Source | Is.Artifact &
  Is.Yours` — tier 1 beside improvise.
  **Legal actions, 12 creatures and 12 artifacts, a full hand:** squad is one cast per number of {2}s the mana can
  pay (linear in mana, as replicate); improvise is the payment's, never a move; Emry's {T} one activation per artifact
  card in the graveyard; evoke one cast per other blue card in hand (Force of Will's); Subtlety's trigger one target
  per creature or planeswalker spell. A test counts them (`LegalActions_StayLinear`).
  Tiers: Emry 2 (`MakePlayable`, the mark from a graveyard), Sicarian Infiltrator 2 (`AsItEnters`, `CastAs`,
  `TimesReplicated`, `CreateTokenCopy.N`), Subtlety 2 (`Evoke`, `AlternativeCostWasPaid` on the permanent), Kappa
  Cannoneer 2 (`Helps`), Preordain 2 (`Surveil.To`), Lórien Revealed 1. No new piece but `MakePlayable`.
- 2026-11-17, Claude — **4.4f's notes.** Built as planned. 12 tests in `LsvBatch2fTests.cs` (16 after the review), all through real casting;
  on 12 creatures, 12 artifacts and a full hand, 39 legal actions (squad 9 casts, evoke 6). 19 breaks, all caught at
  the first try. The engine change moved no recorded game (golden-out, and the AI lane's kept records in the full suite
  with the six cards out). `FraUncommonCreatureTests` names `global::MagicCore.Targeting` now — the moved-up
  `Targeting` helper shadows the namespace.
  **`/pre-commit`'s review: ten findings; five taken, each with a test that fails without it (3 more breaks, all
  caught):** Emry's mark REPLACES one left from an earlier turn (`WithComponent` added a second, and `GetComponent`
  read the stale one); a cast by the mark is no permanent's zone permission (`Playable.MarkedInGraveyard`: Underworld
  Breach's escape cost was added to it); a resolution's cast keeps its own exile (Bilbo's, with a marked card);
  `OnStackComponent.Replicate`'s doc (a copy keeps it, CR 707.10); `Card.AsCast` is the one read (was
  `WasKicked.SpellOf`); `Helps` allocates nothing for an artifact. **Not taken:** squad's count once the creature has
  left — false, `Ref.Source` falls back to its snapshot (a test now says so); `MakeChosenPlayableAction` as a sibling
  (named in `MakePlayable`'s summary: ExileTopPlayable's own step, a mark built there with `Blind` and `Tax`); one more
  keyword check per payment for improvise; the AI's `Evaluator.ImpulseCards` scanning exile only, so it prices no
  graveyard mark (AI lane: Emry's activation is worth nothing to the evaluator until it is cast).
- 2026-11-17, Claude — **4.4g's plan (blue's tier 3, 11 cards, ten new mechanics), read against the code.** Ten
  parts, shared mechanics first; one built per session. Each names its nearest piece and why no option on it fits:
  1. **g1 Protection** (CR 702.16; True-Name Nemesis — then Giver of Runes, Mother of Runes, Figure of Fable,
     Hexdrinker, Seasoned Dungeoneer, The One Ring, Emrakul). Nearest: hexproof in `TargetSpec` and Species 8472's
     `CantBeTargetedByAbilitiesFrom(Sources)`, which already asks a filter of the SOURCE — but targeting only, and
     abilities only; protection is DEBT, four rules. `CantBeBlockedBy` is the attacker's filter on blockers (B only).
     New: `Protection(Filter From)`, the filter asked of the source from the holder's side, in each rule's one place
     (`TargetSpec`, `Damage.Prevented`, `Combat.CanBlock`, `Attachments` — enchant and the Equipment fall-off). Read
     through `AbilitiesOf`, so Giver's grant until end of turn (`UntilEndOfTurn.Gains`) is the same piece; "from the
     colour of your choice" will be a colour chosen, then `Protection(Is.Color(c))` gained. A player's protection
     (The One Ring) waits for its card. "Choose a player as it enters" is `ChoosesPlayerAsItEnters` → `ChosenPlayer`,
     beside its three siblings (colour, number, name): each chooses a different KIND of value with its own choice and
     AI answer, so one generalised component would be a switch on the kind — not an option. "From the chosen
     player" is `Is.ControlledByChosenPlayer`, `Is.NamedChosenName`'s pattern.
  2. **g2 Delve** (Dig Through Time, Treasure Cruise). Nearest: Drivnod's `ExileFromGraveyardCost { Count }`, whose
     cards are picked after the action — but its count is fixed, and delve's pays generic mana, so the count is the
     caster's and the mana owed shrinks with it (replicate's shape: a number on the cast). Convoke/improvise's
     `ManaPayment.Helps` is never a move — wrong here: WHICH graveyard cards go matters (threshold, Tarmogoyf), and
     how many trades mana kept open against the graveyard. Plan: `CastSpellAction.Delve = N` (one cast per N, ≤ the
     generic in its cost), `Costs.Of` less N generic, the N cards picked after the cast as Drivnod's. Dig's "the rest
     on the bottom in any order" is `LookAndTake { Take = 2 }` with a bottom-in-any-order `Rest` (read first).
  3. **g3 Dungeons and the initiative** (Feywild Caretaker — then White Plume Adventurer, Seasoned Dungeoneer, Caves
     of Chaos Adventurer, Undermountain Adventurer, From the Catacombs). Nearest: Sagas (`SagaComponent`, chapter
     triggers on lore counters) — a dungeon is a Saga-like card in the command zone (where emblems are) whose rooms
     trigger as the venture marker moves; read whether a room is a chapter with a branch choice before building.
     The initiative is a designation taken by combat damage to its holder (CR 725), nothing like it exists.
  4. **g4 Face-down permanents** (Abhorrent Oculus' manifest dread — then Headliner Scarlett, Memory Jar, Shelldock
     Isle). Nearest: `ExiledFaceDown` + `Card.KnownTo` (hidden in exile, Vulcan Mind-Meld) and "loses all abilities"
     + base P/T: a face-down 2/2 is all three on the battlefield, plus turning face up (a special action). Oculus'
     "exile six cards from your graveyard" is `AdditionalCostsComponent([ExileFromGraveyardCost { Count = 6 }])` —
     tier 1 (Underworld Breach's escape picks them after the cast).
  5. **g5 Adventures** (Brazen Borrower — then Embereth Shieldbreaker). Nearest: prepare (`PrepareSpellComponent`,
     a second face cast as a spell) and `MayCastThisFrom(Exile)` (Khan): cast the Adventure face from the hand,
     exiled as it resolves (Bilbo's `ExiledLeavingTheStack`), then the creature castable from that exile. The new
     part is casting a card AS its other face. "Can block only creatures with flying" is the blocker's side of
     `CantBeBlockedBy` — read `Combat.CanBlock` for an option first.
  6. **g6 Teamwork** (We Say Thee Nay! — crew, Esika's Chariot and Reckoner Bankbuster, takes the same cost).
     Nearest: kicker (`KickerComponent`, one cast with the optional cost, one without) and `RemoveCountersCost`
     (picked after activating). "Tap any number of creatures with total power 2 or more" is a new cost picked after
     the cast, one creature at a time until the total is reached; "if cast using teamwork" reads it on the stack.
  7. **g7 Storm** (Brain Freeze). Nearest: replicate's cast trigger (`CopySpell` repeated, each copy retargeted). The
     count is every player's spells cast before it this turn — not paid, so not `ReplicateComponent`; a
     `Scripting.Storm` "when you cast this spell" (`TriggerEvent.SelfCast`) with the count read from the cast's number.
  8. **g8 Spree** (Three Steps Ahead). Nearest: `ModeChoice { AtMost = 3 }` and replicate's added cost: a `Mode` with
     a cost of its own, added in `Costs.Of` for each mode chosen. Targets chosen word by word (`EachWord`), or "copy
     target artifact or creature you control" with "counter target spell" is a cast per pair — **MC-8's mechanism for
     one more kind of text; ask Shayne then** (MC-8 itself is left alone).
  9. **g9 Warp** (Quantum Riddler). Nearest: an alternative mana cost (`MayCastFromHandFree(Is.Source) { Mana }`,
     Baleful Mastery), a delayed "exile it at the next end step" (`ExileAtNextEndStep`) and `MayCastThisFrom(Exile)`
     with "a later turn". Its draw replacement is `NumericReplacement(Draw) { Plus = 1, If }` — tier 1.
  10. **g10 Classes** (Does Machines). Nearest: Hexdrinker's level up (real level counters) and Sagas — but a Class
     level is no counter (CR 716): a level on the card, "Level N" activations at sorcery speed gated on the level
     before, "when this Class becomes level 2", and level-gated abilities.
  **Legal actions, 12 creatures and 12 artifacts a side, a full hand and a full graveyard:** delve one cast per number
  exiled (≤ 6 for Dig, 7 for Cruise, whatever the graveyard), the cards picked after; Oculus one cast, its six picked
  after; spree 7 casts (sets of modes) with its words chosen one at a time — per pair it would be 24 × the stack;
  storm one cast per target of the original (2 players), each copy retargeted as a choice; teamwork two casts, the
  creatures picked after (a cast per set would be 4,096); Petty Theft one per nonland permanent of theirs (24); warp
  two casts; a Class one activation, its next level; a dungeon one room choice per branch; protection none (a rule);
  True-Name Nemesis' player two options as it enters.
- 2026-11-17, Claude — **4.4g1's notes (protection).** Built as planned: `Protection(From)` with one question,
  `Protection.Stops(s, holder, source)`, asked in `TargetSpec` (a spell or ability's source, battlefield targets),
  `Damage.Prevented` (now given the source — combat's "dealt damage to a player" asks per hit), `Combat.CanBlock`,
  `Attachments.CanEnchant` and the Equipment fall-off. True-Name Nemesis' player is a `ChooseKeywordChoice` of "An
  opponent" / "You" (no new choice type; the AI's default answer takes the first). 4 tests in `LsvBatch2gTests.cs`,
  7 breaks caught. Not done: the AI's evaluator prices no protection yet (an AI-lane note).
  **`/pre-commit`'s review: seven findings; two taken, each with a test that fails without it (9 breaks caught):**
  a spell being cast is still in its owner's zone, so its card's controller was its OWNER — `TargetSpec` now asks
  protection of the source as controlled by the caster or activator (Robber of the Rich casting their card at my
  Nemesis); `Attachments.Attach` refuses a host with protection from the attachment (CR 701.3b: a non-targeting
  "attach it to" does nothing, it stays where it was). **Not taken (for Shayne):** a clone entering as a copy of
  Nemesis isn't asked for a player, so it has protection from nobody — the same gap Kai Winn, Nyota Uhura and Room
  of Refuge already have (the as-it-enters choices run before `MayEnterAsCopy`; one fix for all four); `Damage.Excess`
  counts excess against a protected creature (no card reads excess from a damage that can reach one yet). AI lane:
  `Lookahead`'s block and attack predictions and `BlunderLint`'s block check don't ask protection.
- 2026-11-17, Claude — **4.4g1b's and 4.4g2's notes (a copy's choices; delve).** g1b: the four "as this enters"
  choices are ONE method now, `MagicState.ChooseAsItEnters`, reading the permanent as it is; `EnterBattlefield` asks it
  for a card that doesn't enter as a copy, and `BecomeCopyOfChosenAction` after the copy (declined: itself). No new
  action — an extra action per entry would run the post-processor on every permanent. g2, **what changed on reading:**
  (1) `ExileFromGraveyardCost { Count = 1 }` is a pick IN the action (Gallia's `ExileId` slot), so delve of one would
  have been a cast per graveyard card — its Count > 1 path is now `PickAfter`, which delve calls for any N; (2) a cast's
  costs are paid BEFORE `PutOnStack`, but `PickAfter` is spawned, so the cards are picked with the spell on the stack
  (CR 702.66: it can't exile itself — `!Is.Source` all the same, for a Breach escape); (3) delve pays from the TOTAL
  cost (taxes in, CR 702.66a), so `CostBeforeLife` less N generic and the bound read from the total; (4) delve is the
  printed keyword (`s.Has(card, "Delve")`, convoke's way), no component; (5) `LookAndTake.Rest` had no bottom-in-any-
  order: `TheRest.BottomInAnyOrder` (known to the owner, the looked order — scry's ceiling, DesignNotes), through a
  shared `MagicState.ToBottom` (`ToBottomAtRandom` shuffles, then calls it). Legal casts on 12 creatures, 12 artifacts,
  a full hand and 20 in the graveyard: Cruise 8 (delve 0–7), Dig 7 (0–6) — a cast per set would be C(20, 7) = 77,520.
  The enumeration's graveyard bound is only an efficiency bound: `ValidateAdd` is the rule (a revert-check found the
  first test couldn't see it; one asks `ValidateAdd` directly now).
  **Shayne (2026-11-17), on the 8 and 7 casts and their picks:** "a procedural take on exiling cards is going to be
  much better than doing an AI search on exiling cards" — for now the AI exiles the first cards in the graveyard
  (`ChooseAmongChoice.Delve`, a rule in `GreedyAi.Answer`, never branched by the beam); a cheap price per card that
  sees Barrowgoyf, Nethergoyf, Animate Dead and Regrowth is MC-4.4g2b, after the cube's cards are in. The casts per
  number stay (as an X spell's).
  **`/pre-commit`'s review: eight findings; four taken.** The look-ahead's `Settle` searched a forced pick of 8 or
  fewer, delve's too — now excluded beside the combat choices (a cost fix: graveyard cards tie in the evaluator, so no
  test can tell the answers apart). The generator's delve bound read the PRINTED generic, so an {X} delve spell
  offered no delve — now each cast's own `GenericBeforeDelve` (X, kicker, taxes; a free cast's 0), `s.Has` once per
  card (a test fails without it: `{X}{U}` at X = 3 delves 0–3). `ManaCost.Generic`, the one sum. **Not taken (for
  Shayne):** delve's exiles happen after `PutOnStack` raises the cast event (CR 601.2h pays costs before the spell is
  cast) — the same as escape's and Drivnod's picks; nothing judges the graveyard as a spell is cast yet. A delve pick
  offered NO card (a Breach escape and delve together, past validation's count) would be answered empty — validation
  subtracts the escape's count, so it can't happen with a filter-free escape. `TheRest.BottomInAnyOrder` read as a new
  enum case: kept — `Rest` IS the option ("where the rest go"), its values the destinations the texts print, as
  `TopInAnyOrder` beside the random bottom.
- 2026-11-18, Claude — **4.4g3's notes (dungeons and the initiative).** **A room is no Saga chapter**: a chapter fires
  as a lore COUNT crosses N, and Undercity branches (Forge and Lost Well share a depth), so no count says which room
  you are in. **What changed on reading:** a room's ability is `WhenYouDo`'s shape — a triggered ability an effect
  puts on the stack itself (`Triggers.QueueTargets`), no event, no `Trigger` option — and the Saga's "sacrificed once
  no chapter is waiting" (`ChapterToResolve`) is the dungeon's completion rule as it stands. Built: `Effects/Dungeons.cs`
  — `DungeonComponent(Room)` on an object in the command zone (the emblem path), `Undercity.Rooms` (static data, nine
  rooms and their exits), `Venture` (two exits: ONE `ChooseKeywordChoice`), completion in the state-based check; the
  initiative an object in its holder's command zone (`InitiativeComponent`, nearest piece the emblem — forever, never
  changing hands) with its upkeep and combat-damage triggers, `TakeTheInitiative`, `YouHaveTheInitiative`. Undercity's
  rooms needed goad (Arena: `Combat.Goaded`, no "Don't attack" offered) and `LookAndTake.Then` (Throne). Only
  Undercity (Shayne's brief). **Legal actions:** none added — venture is an effect, a branch one choice of two. **The
  AI:** the room pick is a two-option forced pick the look-ahead already settles (no rule needed); the evaluator prices
  no initiative (DesignNotes, an AI-lane note). Ceilings (DesignNotes): the take's venture folded into the take, goad's
  two-player reading, the active player takes the initiative, Throne's counters after it enters, nothing reads
  "completed a dungeon". 10 tests in `LsvBatch2g3Tests.cs`, 17 breaks caught.
  **`/pre-commit`'s review: eight findings; three taken, each with a test that fails without it.** The look-ahead's
  quick answer (and the card-value sandbox through it) answered every attacker "don't" — a goaded creature crashed it:
  ONE answer now, `DeclareAttackerChoice.NoAttack`, for the look-ahead, `WholeAttacks` and the table. Goad was read
  through `s.Has`, so "loses all abilities" ended it — it is an effect: `Combat.IsGoaded` reads its stamp. Venturing on
  the last room did nothing — it completes that dungeon and begins a new one. A `ponytail:` on the active player.
  **Not taken:** `LookAndTake.Then` on a card Containment Priest exiled — `PutCounters` and the hexproof grant act on
  permanents only; the inspector's "No attack" for a plan whose goaded creature attacks anyway (a label); a last room
  with a target pending as the state check runs — no check runs while a choice is pending.

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
  - [x] MC-4.4c Red's tier 1 (`LsvSurvey.md` listed 28; Simian Spirit Guide went with 4.4b), read card by card and
        split in two (Decisions):
    - [x] MC-4.4c1 The tested pieces, 11 — done 2026-11-13: Flame Slash, Sear, Flametongue Kavu, Wheel of Fortune,
          Faithless Looting, Mine Collapse, Suplex, Legion Extruder, Seasoned Pyromancer, Pyrogoyf, Generous Plunderer.
          Scripts only, no engine change; 14 tests; `batch_checks.py` all 7 steps (golden-out unchanged; golden-in moved
          29 random and 649 card-replay games, added 44; probe 150 games, none unfinished, every card used)
    - [x] MC-4.4c2 The options and new pieces, 16 — surveyed and split in five parts (Decisions) — done 2026-11-14:
      - [x] MC-4.4c2a Options, 6 — done 2026-11-13: Broadside Bombardiers, Cori-Steel Cutter, Bonehoard Dracosaur, Inti,
            Magda, Tersa Lightshatter. Six options (Decisions); 7 tests, 9/9 breaks caught; `batch_checks.py` all 7 steps
            (golden-out unchanged; golden-in moved 29 random and 336 card-replay games, added 24; probe 150 games, none
            unfinished, every card used — Magda's search 3 times)
      - [x] MC-4.4c2b Options, 7 — done 2026-11-14: Sneak Attack, Orcish Lumberjack, Fiery Confluence, Screaming
            Nemesis, Goblin Welder, Robber of the Rich, Underworld Breach. Seven options (Decisions); 10 tests, 24/24
            breaks caught; `batch_checks.py` all 7 steps (golden-out unchanged; golden-in moved 29 random and 451
            card-replay games, added 28; probe 150 games, none unfinished, every card cast — the Lumberjack's mana
            move isn't counted as an activation)
      - [x] MC-4.4c2c Laelia — the zone-change event — done 2026-11-14: `ChangedZoneEvent` (Decisions); 3 tests, 6/6
            behavioural breaks caught; `batch_checks.py` all 7 steps (golden-out unchanged)
      - [x] MC-4.4c2d "Name Sticker" Goblin — entering from a zone, a die roll — done 2026-11-14: `Trigger.NotFrom`,
            `RollDie` (Decisions); 8 tests, 14/14 breaks caught; `batch_checks.py` all 7 steps (golden-out unchanged;
            golden-in moved 29 random and 88 card-replay games, added 4; probe 150 games, 2 past 300 permanents)
      - [x] MC-4.4c2e Pyrokinesis — damage divided among any number of targets — done 2026-11-14: `TargetSpec.Divide`,
            Pure Energy moved onto it (Decisions); 9 tests, 22/22 breaks caught; `batch_checks.py` all 7 steps
            (golden-out unchanged; golden-in moved 29 random and 71 card-replay games, added 4; probe 150 games, none
            unfinished, cast 522 times)
  - [x] MC-4.4d White's tier 1 (`LsvSurvey.md` lists 36), read card by card and split (Decisions) — done
        2026-11-15, 36 cards in five parts:
    - [x] MC-4.4d1 The tested pieces, 21 — done 2026-11-14: Thraben Inspector, Cathar Commando, Luminarch Aspirant,
          Agent Bishop, Spectacular Spider-Man, Stoneforge Mystic, Thalia (Guardian of Thraben), Elite Spellbinder,
          Loran, Sanguine Evangelist, Auriok Salvagers, Reprieve, Exorcise, Portable Hole, Leyline Binding, Lingering
          Souls, Touch the Spirit Realm, Glimmer Lens, Staff of the Storyteller, Sage of the Skies, Enlightened Tutor.
          Scripts only, no engine change (one name, `CounterKinds.Story`); 28 tests; `batch_checks.py` all 7 steps
          (golden-out unchanged; golden-in moved 29 random and 1123 card-replay games, added 84; probe 150 games, none
          unfinished, every card cast — its report lists the first 12 names, the other 9 probed in a second run)
    - [x] MC-4.4d2a Options, zones and tokens, 7 — done 2026-11-15: Oust, Unexpectedly Absent, Phelia, Elspeth (Storm
          Slayer), Containment Priest, Enduring Innocence, Jacked Rabbit. Six options (Decisions: three plans changed);
          20 tests, 32/32 breaks caught; `batch_checks.py` all 8 steps (golden-out unchanged; golden-in moved 29 random
          and 415 card-replay games, added 28; probe 150 games, every card cast, 1 past 300 permanents — seed 119, 280
          Troubling Tribbles, not this batch)
    - [x] MC-4.4d2b Options, restrictions and timing, 5 — done 2026-11-15: Archon of Emeria, Voice of Victory, Clarion
          Conqueror, The Wandering Emperor, Anointed Peacekeeper. Options on four pieces (Decisions; Yuriko and Jace's
          Machinations moved onto theirs); 13 tests, 66/66 breaks caught; `batch_checks.py` all 7 steps, twice (after
          the review's fixes too: golden-out unchanged; golden-in moved 29 random and 308 card-replay games, added 20;
          probe 150 games, none unfinished, every card cast)
    - [x] MC-4.4d3 Leaving the battlefield, 2 — done 2026-11-15: Skyclave Apparition, Serra Paragon. The zone-change
          event widened to a permanent's own leaving, read from the card that left (Decisions; Laelia moved with
          it; The Measure of a Man, All Good Things... and Borg Cube onto the one "exiled with this" filter);
          16 tests (18 cases), 44/44 breaks caught; `batch_checks.py` all 7 steps, twice (after the review's
          fixes too: golden-out unchanged; golden-in
          moved 29 random and 126 card-replay games, added 8; probe 150 games, both cards cast, 1 past 300
          permanents — seed 94, 271 Troubling Tribbles, not this batch)
    - [x] MC-4.4d4 Balance — done 2026-11-15: `Count.BeyondTheFewest`, `EachSacrifices { N }`, `Discard`'s number a
          `Count`, Emrakul's ward moved onto it (Decisions); 6 tests, 16/16 breaks caught; `batch_checks.py` all 8 steps,
          twice (after the review's fixes too: golden-out unchanged;
          golden-in moved 29 random and 56 card-replay games, added 4; probe 150 games, cast 565 times, 1 past 300
          permanents — seed 94, 273 Troubling Tribbles, not this batch)
  - [x] MC-4.4e Blue's tier 1 (Shayne, 2026-11-15) — 35 mono-blue cards in `LsvSurvey.md`'s tier 1, read card by
        card and split as white was (Decisions, "4.4e"), in seven parts:
    - [x] MC-4.4e1 The tested pieces, 12 — done 2026-11-15: Mystical Tutor, Spellseeker, Trinket Mage, Tinker,
          Snapcaster Mage, Displacer Kitten, Boomerang Basics, Upheaval, Timetwister, Echo of Eons, Cryogen Relic,
          Sewer-veillance Cam. Scripts only, no engine change; 12 tests, the review's assertions 9/9 breaks caught;
          `batch_checks.py` all 7 steps (golden-out unchanged; golden-in moved 29 random and 681 card-replay games,
          added 48; probe 150 games, none unfinished, every card cast)
    - [x] MC-4.4e2a Options, the library's top, 4 — done 2026-11-15: Ponder, Stock Up, Consult the Star Charts,
          Thassa's Oracle. `LookAndTake { Take, To, Rest }` (Hit It!, Loot and Pattern Buffer Preservation moved
          onto it), `ShuffleLibrary(who)` (`ShuffleLibraryAction` gone; Timetwister and Echo of Eons take it),
          `SearchLibrary { May }` (LSV's five "may search" cards; FRA's two and TRK's five are an Open — Decisions);
          `MagicState.ToBottomAtRandom` from the review; 14 tests (17 cases), 50/50 breaks caught; `batch_checks.py`
          all 8 steps, three times (the Open, then the review's fixes; golden-out moved 5 random games,
          each read: four by a "may" search that took nothing, one by Timetwister's one shuffle; golden-in moved 29
          random and 351 card-replay games, added 16, the AI replay unmoved; probe 150 games, none unfinished, each
          card cast 396–543 times)
    - [x] MC-4.4e2b Options, draws, 4 — done 2026-11-16: Faerie Mastermind, Hullbreacher, Narset (Parter of
          Veils), Jace (Wielder of Mysteries). `Trigger.Nth` (`SpellNumber` renamed: Stella Lee and Cori-Steel
          Cutter moved; `DrewCardEvent.Number`), `NumericReplacement(Draw) { Instead, If, ExceptTheTurnsDraw }`
          read per card in `MagicState.Draw`, the "you … this turn" counts' `Of`; 14 tests (15 cases), 33/33
          breaks caught; `batch_checks.py` all 7 steps, twice (after the review's fix too: golden-out unchanged;
          golden-in moved 29 random and 257 card-replay games, added 16, the AI replay unmoved; probe 150 games,
          none unfinished, each card cast 174–453 times). Open: `ExceptTheTurnsDraw` (Decisions, e2b's notes)
    - [x] MC-4.4e2c Options, mana and costs, 5 — done 2026-11-16: Gitaxian Probe, Phyrexian Metamorph, Urza (Lord
          High Artificer), Forensic Gadgeteer, Thieving Skydiver. A spell's `{U/P}` (`CastSpellAction.
          LifeForPhyrexian`), `MayEnterAsCopy.AlsoType`, a mana ability tapping ANOTHER permanent (`ManaAbilities.
          TappingAnother`, paid as convoke's unit), `PlayableFromExile.Free`, `CostChange.Floor`, `KickerComponent
          { MinX }` with an `{X}` kicker, `Card.CastKicked`, a filter's X the source's, `AttachSourceTo` →
          `Attach(What, To)` (equip, Cori-Steel Cutter, Glimmer Lens moved); 13 tests (16 cases), 36 breaks caught
          (the review's three fixes included); `batch_checks.py` all 7 steps, twice (golden-out unchanged; golden-in
          moved 29 random and 319 card-replay games, added 20, the AI replay unmoved; probe 150 games, none
          unfinished both times, each card cast 212–448 times in the first)
    - [x] MC-4.4e2d Options, casting off the hand, 4 — done 2026-11-16: Bilbo (Thief in the Night), Malcolm
          (Alluring Scoundrel), Flash, Transmute Artifact. `CostChange.NotFrom`, `CastFromHandFree` →
          `CastOneOf(Ref) { Free, Exiled }` (Brazen Bluff moved), a graveyard cast during a resolution by the
          effect's permission (`CastSpellAction.ExiledLeavingTheStack` for Bilbo), `PayMana { ManaCostOf, Generic,
          ReducedBy }` + `CanPay { Pay }` + `Scripting.MayPay`, `MaySacrifice.Then`'s `Ref.ThisWay`,
          `SearchLibrary.Then`; 10 tests (13 cases), 19 breaks caught (the review's two fixes included); `batch_checks.py` all 8 steps (golden-out
          unchanged; golden-in moved 29 random and 253 card-replay games, added 16, the AI replay unmoved; probe 150
          games, none unfinished, each card cast 292–430 times; full suite 1,904 passed — re-run after the review's fixes: golden-out
          unchanged again, 19 of 19 breaks caught)
    - [x] MC-4.4e2e Options, "for as long as this remains on the battlefield", 2: The Wondrous Wasp, Tishana's
          Tidebinder — done 2026-11-17: `ModifyUntilEndOfTurn.WhileSourceRemains` stamping
          `EnchantedLosesAllAbilities { Object }` on the source, `Counter.Then`, `StackAbility.Delayed`; 5 tests, 10
          breaks caught; `batch_checks.py` all 8 steps, run again after the review's fix (golden-out unchanged;
          golden-in moved 29 random and 139 card-replay games, added 8, the AI replay unmoved; probe 150 games, each
          card cast 488–500 times, 1 past 300 permanents; full suite 1,909 passed)
    - [x] MC-4.4e2f Options, choices after the cast, 4: Frantic Search, Time Spiral, Mystic Confluence (Fiery
          Confluence moves with it), Occult Epiphany — done 2026-11-17: `Untap { UpTo }`, `Count.CardTypesAmong(Ref)`
          (Tarmogoyf, Pyrogoyf moved), a `Repeats` text's targets chosen word by word as it is cast
          (`CastSpellAction.EachWord`, Fiery Confluence moved: at most 10 casts, was 392 against 12 artifacts); 4
          tests, 9 breaks caught (twice: before and after the review's fixes); `batch_checks.py` all 8 steps
          (golden-out moved only Fiery's 4 card replays, re-kept; golden-in moved 29 random and 250 card-replay games,
          added 16, the AI replay unmoved; probe 150 games, none unfinished, each card cast 261–996 times; full suite
          1,913 passed, again after the review's fixes)
  - [x] MC-4.4f Blue's tier 2 (Shayne, 2026-11-17) — 6 in `LsvSurvey.md`'s tier 2: Emry (Lurker of the Loch),
        Sicarian Infiltrator, Subtlety, Kappa Cannoneer, Preordain, Lórien Revealed — done 2026-11-17, one part:
        `Card.CastAs` (the spell a permanent was; `CastKicked` gone, Thieving Skydiver moved), squad
        (`ReplicateComponent.AsItEnters`, `Count.TimesReplicated`, `CreateTokenCopy.N`), `Scripting.Evoke`, improvise
        (`ManaPayment.Helps`, convoke's flag gone), scry (`Surveil.To`), `MakePlayable`; 16 tests, 22 breaks caught
        (the review's three fixes included); `batch_checks.py` all 8 steps, run again after the review's fixes
        (golden-out unchanged; golden-in moved 29 random and 370 card-replay games, added 24, the AI replay unmoved;
        probe 150 games, none unfinished, each card cast or activated 301–526 times; full suite 1,929 passed)
  - [ ] MC-4.4g Blue's tier 3 (Shayne, 2026-11-17) — 11 in `LsvSurvey.md`'s tier 3, ten mechanics in ten parts, one
        per session (Decisions, 4.4g's plan):
    - [x] MC-4.4g1 Protection: True-Name Nemesis — done 2026-11-17: `Protection(From)`, `Protection.Stops` in
          targeting, damage, blocking and attachments; `ChoosesPlayerAsItEnters`, `Is.ControlledByChosenPlayer`; 5
          tests, 9 breaks caught (the review's two fixes included); `batch_checks.py` all 8 steps (golden-out unchanged; golden-in moved 29 random and
          72 card-replay games, added 4, the AI replay unmoved; probe 150 games, 1 unfinished past 300 permanents,
          Nemesis cast 358 times; full suite 1,934 passed — run again after the review's fixes)
    - [x] MC-4.4g1b Bug (Shayne, 2026-11-17): a clone entering as a copy of an as-it-enters chooser (True-Name
          Nemesis, Kai Winn, Nyota Uhura, Room of Refuge) makes that choice — done 2026-11-17:
          `MagicState.ChooseAsItEnters`, asked after the copy; 2 tests (Metamorph → Nemesis, Flesh Duplicate → Kai Winn)
    - [x] MC-4.4g2 Delve: Dig Through Time, Treasure Cruise — done 2026-11-17: `CastSpellAction.Delve`, the cards
          picked after (`ExileFromGraveyardCost.PickAfter`), `TheRest.BottomInAnyOrder`; 6 tests (the review's one
          included), with g1b's 10 breaks caught; `batch_checks.py` all steps (golden-out: the random games unmoved, one card replay moved by g1b as
          intended — Compel Brutality 03's Flesh Duplicate copying Kai Winn now names a card; golden-in moved 29 random
          and 135 card-replay games, added 8, the AI replay unmoved; probe 150 games, 1 unfinished past 300
          permanents, Dig cast 495 times, Cruise 508; full suite 1,942 passed — all run again after the review's fixes). The AI's picks are a rule (Shayne, 2026-11-17): the first cards in the graveyard
    - [ ] MC-4.4g2b (LATER — after the cube's cards are in; Shayne, 2026-11-17) The AI's delve picks by a cheap
          procedural price per card, never a search: goyfs' card types, reanimation and Regrowth targets, flashback and
          escape cards (DesignNotes, MagicCore known ceilings)
    - [x] MC-4.4g3 Dungeons and the initiative: Feywild Caretaker — done 2026-11-18: Undercity, `Venture`, the
          initiative, goad, `LookAndTake.Then`; 10 tests, 17 breaks caught (the review's three fixes included);
          `batch_checks.py` all 8 steps (golden-out unchanged; golden-in moved 29 random and 64 card-replay games,
          added 4, the AI replay unmoved; probe 150 games, none unfinished, the Caretaker cast 380 times; full suite
          1,952 passed) — all run again after the review's fixes
    - [ ] MC-4.4g4 Face-down, manifest dread: Abhorrent Oculus
    - [ ] MC-4.4g5 Adventures: Brazen Borrower // Petty Theft
    - [ ] MC-4.4g6 Teamwork: We Say Thee Nay!
    - [ ] MC-4.4g7 Storm: Brain Freeze
    - [ ] MC-4.4g8 Spree: Three Steps Ahead
    - [ ] MC-4.4g9 Warp: Quantum Riddler
    - [ ] MC-4.4g10 Classes: Does Machines

## Next

Red's tier 1 (4.4c), white's (4.4d), blue's tiers 1 and 2 (4.4e, 4.4f) and blue's tier 3's first three parts —
protection (4.4g1, with g1b's copy fix), delve (4.4g2) and dungeons and the initiative (4.4g3) — are done: **228 of
528 play.** Next, in a NEW session: **MC-4.4g4 — face-down, manifest dread** (Abhorrent Oculus): the plan is in
Decisions (4.4g's plan, part 4). Then g5–g10 in the plan's order, one a session.
From 4.4g3: a dungeon is `DungeonComponent(Room)` on a command-zone object, `Undercity.Rooms` its data; `Venture` is
"venture into Undercity" (a branch ONE `ChooseKeywordChoice`; on the last room, a new dungeon), a room's ability put
on the stack as `WhenYouDo`'s; the initiative is `TakeTheInitiative(who)` / `YouHaveTheInitiative` /
`InitiativeComponent.HolderOf`. Another dungeon (Lost Mine, Tomb of Annihilation) waits for a card that ventures into
it (White Plume Adventurer, Seasoned Dungeoneer, Caves of Chaos and Undermountain Adventurer all take the initiative —
Undercity only; ask Shayne before another). Goad is `Undercity.Goad(ref)`, read by `Combat.IsGoaded`; an attack answer
is `DeclareAttackerChoice.NoAttack` everywhere. `FraBoardTest` has `VentureSpell` (an instant), `Ventures(answer)` and
`Room(player)` now.
From 4.4g2: delve is `CastSpellAction.Delve = N` (the printed keyword, one cast per N ≤ the total's generic), the N
cards picked after the cast by `ExileFromGraveyardCost.PickAfter` — the one "pick N from your graveyard after" path
(Oculus' six in g4 is that cost with `Count = 6`); "the rest on the bottom in any order" is `TheRest.BottomInAnyOrder`
(`MagicState.ToBottom`); the AI's picks are the first cards in the graveyard, a rule — its procedural price is
MC-4.4g2b, LATER, once the cube's cards are in (Shayne). From g1b: "as this enters, choose …" is ONE method, `MagicState.ChooseAsItEnters` — a new
chooser goes there, and a clone asks it after its copy. g2's helpers in `LsvBatch2gTests.cs`: `Graveyard(n)` (n bears
in mine), `DelvePicks(asked)`.
From 4.4g1: protection is ONE question, `Protection.Stops(s, holder, source)` — a `Protection(From)` filter asked of
the SOURCE from the holder's side, read through `AbilitiesOf` (a grant until end of turn counts); "choose a player as
it enters" is `ChoosesPlayerAsItEnters` → `ChosenPlayer`; `Damage.Prevented` takes the source. 4.4g's tests are in
`LsvBatch2gTests.cs` (`Pyroclasm`: 2 damage to each creature, targeting nothing; `AttachedTo`: set up attached).
From 4.4f: a permanent's memory of its spell is ONE field, `Card.CastAs` (X, kicked, alternative cost, squad's count;
`Card.AsCast` reads the stack's or the permanent's) — never a new `Cast…` field. Evoke is `Scripting.Evoke(colour)`
(Solitude, Endurance, Fury, Grief take it as they come); squad `Scripting.Squad(cost)`; improvise and convoke one answer,
`ManaPayment.Helps`; scry `Surveil { To = Library }`; "you may cast that card this turn" on a card left in a graveyard
`MakePlayable(Ref, PlayPermission)`. Moved up to `FraBoardTest`: `UntappedLands`, `Library`, `TapMyLands`, `InExile`,
`InGraveyard`, `Targeting(target, yes)`; 4.4f's tests are in `LsvBatch2fTests.cs`.
From e2f, backlogged (Shayne, 2026-11-17): Mystic Confluence can't counter ONE spell two or three times, and the other
modal spells (Sublime Epiphany) still offer a cast per combination of targets — one fix, **MC-8**.
From e2f: "untap up to N lands" is `Untap(Ref.Permanents(Is.Land)) { UpTo }` (your tapped ones, as many as it can);
"card types among [cards]" is `Count.CardTypesAmong(Ref)`; a `Repeats` modal spell is one cast per choice of modes,
its words chosen one at a time as it is cast (`CastSpellAction.EachWord` → `ChooseTargetChoice` each,
`CastWithChosenTargetsAction.Words`). e2f's helper in `LsvBatch2eTests.cs`: `MyTappedLands()`.
From e2e: "loses all abilities for as long as [this] remains on the battlefield" is `ModifyUntilEndOfTurn {
LosesAllAbilities, WhileSourceRemains }`, which stamps Infinite Coursework's `EnchantedLosesAllAbilities { Object }`
on the SOURCE (status: it ends as the source leaves); "if [something] is countered this way" is `Counter.Then` with
`Ref.ThisWay` (an ability's source while still that object). `WithTap(card)` ("{T}: You gain 1 life") is
`FraBoardTest`'s now; e2e's helpers in `LsvBatch2eTests.cs`: `Bird` (flying, haste), `Kill(permanent)`, `Top()`.
From e2d: "you may cast one of [cards]" is `CastOneOf(Ref) { Free, Exiled }` (Brazen Bluff's piece, generalised); a
cast from a graveyard during a resolution is `ByPermission` (not flashback) unless `ExiledLeavingTheStack`; a cost
reduction by where the spell is cast from is `CostChange.NotFrom`; a mana payment whose cost is counted is `PayMana
{ ManaCostOf, Generic, ReducedBy }`, and "unless you pay" / "if you don't" is `Scripting.MayPay` (May never runs its
Otherwise when it can't be paid); `MaySacrifice.Then` reads the sacrificed as `Ref.ThisWay`; `SearchLibrary.Then`
decides the found card's zone. e2d's test helpers (`LsvBatch2eTests.cs`): `AttackWith(answer, attackers)` (through
combat to the postcombat main), `InExile(card)`, `InGraveyard(card)`.
From e2c: a spell's Phyrexian mana is `CastSpellAction.LifeForPhyrexian` (as an activation's); a cost that taps
ANOTHER permanent for mana (Urza) is a unit of `ManaPayment.Find`, as convoke's creature (`ManaAbilities.
TappingAnother`); `PlayPermission { Free }` works on a mark now (`PlayableFromExile.Free`); a permanent remembers it
was kicked (`Card.CastKicked`, beside `CastX`) and a filter's counted bound reads the source's X; "attach" is ONE
effect, `Attach(What, To)`. e2c's test helpers (`LsvBatch2eTests.cs`): `TapMyLands()`, `KnownToMe(theirCard)`,
`UrzaExiles(card)`.
**Open (for Shayne), from e2b:** Hullbreacher's "except the first one they draw in each of their draw steps" is a
bool on the replacement, `ExceptTheTurnsDraw`, and a `turnDraw` argument to `Draw` — the review reads it as a
one-card flag against MC-6's rule; the other way is a draw-step count in `TurnTally` and a step condition (three
pieces, `DuringYourTurn` and `DuringCombat` generalised). Default taken: the flag (Decisions, e2b's notes).
From e2b: a draw is replaced or stopped by `NumericReplacement(Draw) { Instead, If }`, asked per card in
`MagicState.Draw` before the library is looked at; a replaced card isn't drawn (no tally, no event); "their Nth
[spell / card] each turn" is `Trigger.Nth`; another player's "this turn" count is the count `with { Of = … }`. Its
test helpers in `LsvBatch2eTests.cs`: `DrawsThree(caster, target)`, `ToTheirDrawStep()`, `Treasures(player)`,
`JaceWielder(loyalty)`, `Activate(walker, ability, targets)`.
**Open (for Shayne), from e2a:** the seven FRA and TRK cards that say "may search" still shuffle when the search
is declined — `May = true` on them makes three AI tests on kept TRK records stale (Decisions, e2a's plan). Default
taken: left as they were, for the AI lane to move with those records re-kept.
From e2a: `LookAndTake { Take, To, Rest }` is the look ("put N of them" / "on top" / "back in any order" are
options, never a flag); a shuffle is `ShuffleLibrary(who)`; a "may search" is scripted `SearchLibrary { May }`
(Golos and Boseiju, not yet scripted, say it too); a declined "may" search or a moved shuffle changes the random
stream, so **an engine change that adds or drops a shuffle on FRA or TRK cards stales the AI lane's kept records**
(`MagicCore.Tests/Records`) — the full suite shows it, golden-out doesn't.
The blue tests are in `LsvBatch2eTests.cs` (`Targeting(target, yes)`: a trigger's target and its "you may",
`UntappedLands()`: what a cost tapped is the difference, `Revealed(card)`, `TopSeven(player)` / `HandOf(player)`:
was the library shuffled, `HandsAndGraveyards(…)`, and from e2a `Library(player)`: top first — unchanged after an
effect, it was NOT shuffled, `OwnerKnows(card)`, `LibraryOf(n)`: my library cut to its top n); `Lsv(name)`,
`Put(card, player, zone)`, `OnTop(card[, player])` (fifteen private copies gone), `CastAsking(card,
answer)`, `ResolveAsking(answer)`, `Offered<T>(asked)` (the ids a choice of that type offered) and
`TapIt(permanent)` are `FraBoardTest`'s now — a helper a later file needs MOVES up there too.
Read the nearest piece's CODE first — three of d2a's six plans changed on reading, two of d2b's grew, d3's grew in
four places, d4's in two (their notes).
From d4: a number on an each-player effect is counted for each player with `Ref.It` that player
(`EachSacrifices { N }`, `Discard(who, count)`), and "the rest beyond the player with the fewest" is
`Count.BeyondTheFewest(zone, filter)`; a `Count` is never one count less another. Emrakul's ward moved onto
`EachSacrifices { N }` (`SacrificePermanents` is gone).
Useful to the next part: `EnterBattlefield` may refuse an entry (Containment Priest), so check `IsOnBattlefield` after
it; `MoveTo.Then` / `Ref.ThisWay` and `DelayedTrigger.MadeBy` (`Ref.EventObject`) exist; from d2b, a rule every
cast or activation asks about permanents notes itself on the statics board (`Characteristics.RuleWork`) rather than
scanning the battlefield each time; from d3, a permanent that reads its OWN leaving raises `ChangedZoneEvent` with
the card as it last was (`TriggerEvent.SelfLeaves`; any permanent, a land too — but never for another permanent's
leaving: `MagicState.ReadsItsLeaving`, DesignNotes), `Ref.OwnerOf(ref)` names a card's owner, and a zone
permission's filter is asked with the X of the cast (`Playable.ZonePermission(…, x)`). The white tests are in
`LsvBatch2dTests.cs` (its `UntapMyLands`, `Elspeth()`,
`Emperor()`, `Loyalty(walker, ability)`, `LoyaltyOffered(walker)`, `WithTap(card)`: "{T}: You gain 1 life", `Raise`:
a card from any graveyard onto the battlefield, `Remove(zone)`: target permanent destroyed, bounced or exiled,
`Costing(cost)`: a 2/2 of that cost). A FIRES test per scripted card. For Shayne: nothing Open (d2a's is
answered: AI-19.26); two AI notes on the backlog (AI-19.27, the AI's name for Anointed Peacekeeper; AI-19.28, its
"sacrifice N" answer). DEV-5 still open: `batch_checks.py`'s probe report lists only the
first 12 cards named (probe the rest with `MAGIC_PROBE_CARDS`), the probe names no never-used
card, and a re-run after a fix needs the golden files restored from HEAD by hand
(`git checkout HEAD -- MagicCore.Tests/Golden*.txt`).

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
- 2026-11-13 (cube lane, sprint 5 C5): MC-4.4c2a — red's 16 surveyed (13 options, 3 new rules; five parts), part a's
  six cards on six options; no recorded game moved by the engine; 137 of 528 play. For Shayne: one Open (the AI and
  "discard up to two").
- 2026-11-14 (cube lane, sprint 5 C5): MC-4.4c2b — seven cards on seven options, no new piece; no recorded game moved
  by the engine; 144 of 528 play. For Shayne: nothing Open.
- 2026-11-14 (cube lane, sprint 5 C5): MC-4.4c2c — Laelia; the leave-graveyard event generalised to a zone change with
  From/To, Vash and Blastoneuron moved onto it; 145 of 528 play. For Shayne: nothing Open.
- 2026-11-14 (cube lane, sprint 5 C5): MC-4.4c2d — "Name Sticker" Goblin: `Trigger.NotFrom` on the entry event's new
  From, `RollDie`; no recorded game moved by the engine; 146 of 528 play. For Shayne: nothing Open.
- 2026-11-14 (cube lane, sprint 5 C5): MC-4.4c2e — Pyrokinesis; `TargetSpec.Divide`, Pure Energy moved onto it; red's
  tier 1 (4.4c) done; 147 of 528 play. For Shayne: nothing Open — which colour 4.4d takes is his (default: white).
- 2026-11-14 (cube lane, sprint 5 C5): MC-4.4d1 — white's tier 1 surveyed (36: 21 tested pieces, 15 on options; five
  parts) and its 21 script-only cards built; no engine change; 168 of 528 play. For Shayne: nothing Open.
- 2026-11-15 (cube lane, sprint 5 C5): MC-4.4d2a — seven white cards on six options (a library depth, a delayed
  trigger's maker, tokens doubled, an entry refused, a type lost on entering, one X for a trigger); no recorded game
  moved by the engine; 175 of 528 play. For Shayne: one Open (Unexpectedly Absent: a cast per X, default taken).
- 2026-11-15 (cube lane, sprint 5 C5): MC-4.4d2b — five white cards on four pieces' options (the lock: whose, when,
  which, how many spells, of which sources; a token made attacking; loyalty at instant speed by a filter; a tax on
  abilities and a name chosen after a look at the hand); Yuriko and Jace's Machinations moved onto the generalised
  pieces; no recorded game moved by the engine; 180 of 528 play. d2a's Open answered (Shayne: the AI's X spells
  another time — AI-19.26). For Shayne: nothing Open; AI-19.27 on the backlog.
- 2026-11-15 (cube lane, sprint 5 C5): MC-4.4d3 — Skyclave Apparition and Serra Paragon on the zone-change event,
  widened to a permanent's own leaving of the battlefield and read from the card that left (one look-back with
  "when this dies"); a token's counted size, a card's owner as a ref, the linked exile asked from a source that has
  left (The Measure of a Man's chapter III, All Good Things...' IV and Borg Cube's attack now find their cards
  though the source left in response), a gained trigger on the graveyard permission, its mana-value bound counting
  X; Laelia moved with the event; no recorded game moved by the engine; 182 of 528 play. For Shayne: nothing Open.
- 2026-11-15 (cube lane, sprint 5 C5): MC-4.4d4 — Balance: "the rest beyond the player with the fewest" as one
  count (`Count.BeyondTheFewest`), counted for each player by `EachSacrifices { N }` and `Discard` (its number now
  a `Count`); Emrakul's ward moved onto `EachSacrifices { N }`; one cast and at most three choices whatever the
  board; no recorded game moved by the engine; 183 of
  528 play. White's tier 1 (4.4d) done. For Shayne: nothing Open — which colour comes next is his; AI-19.28 on the
  backlog.
- 2026-11-15: Shayne — blue next: MC-4.4e, blue's tier 1 (35 cards in the survey's tier 1).
- 2026-11-15: MC-4.4e split in seven parts (12 tested pieces, 23 on options — Decisions) and MC-4.4e1 done: 12
  cards, scripts only; no recorded game moved by the engine; 195 of 528 play. For Shayne: nothing Open. Next:
  MC-4.4e2a, the library's top.
- 2026-11-15: MC-4.4e2a done: Ponder, Stock Up, Consult the Star Charts, Thassa's Oracle — 199 of 528 play.
  `LookAndTake { Take, To, Rest }`, `ShuffleLibrary`, `SearchLibrary { May }` on LSV's five; the seven FRA and TRK
  "may search" cards are an Open (three kept AI records go stale with them). Next: MC-4.4e2b, draws.
- 2026-11-16: MC-4.4e2b done: Faerie Mastermind, Hullbreacher, Narset (Parter of Veils), Jace (Wielder of
  Mysteries) — 203 of 528 play. `Trigger.Nth`, `NumericReplacement(Draw) { Instead, If, ExceptTheTurnsDraw }`;
  `ExceptTheTurnsDraw` is an Open (a flag, against a draw-step count and a step condition). Next: MC-4.4e2c, mana
  and costs.
- 2026-11-16: MC-4.4e2c done: Gitaxian Probe, Phyrexian Metamorph, Urza (Lord High Artificer), Forensic
  Gadgeteer, Thieving Skydiver — 208 of 528 play. A spell's Phyrexian mana, a mana cost tapping another permanent,
  free play from an exile mark, a cost floor, an X kicker, `Attach(What, To)`. Next: MC-4.4e2d, casting off the hand.
- 2026-11-16: MC-4.4e2d done: Bilbo (Thief in the Night), Malcolm (Alluring Scoundrel), Flash, Transmute Artifact —
  212 of 528 play. `CastOneOf`, a graveyard cast as an effect resolves by its permission, `CostChange.NotFrom`,
  `PayMana`'s counted cost and `MayPay`, `SearchLibrary.Then`. Next: MC-4.4e2e.
- 2026-11-17: MC-4.4e2e done: The Wondrous Wasp, Tishana's Tidebinder — 214 of 528 play. "Loses all abilities for as
  long as this remains" (`WhileSourceRemains`, Infinite Coursework's piece with `Object`), `Counter.Then`, and a
  delayed trigger known as one on the stack. Next: MC-4.4e2f, choices after the cast.
- 2026-11-17: MC-4.4e2f done: Frantic Search, Time Spiral, Mystic Confluence, Occult Epiphany — 218 of 528 play;
  blue's tier 1 done. `Untap { UpTo }`, `Count.CardTypesAmong`, and a `Repeats` spell's targets chosen after the cast
  (Fiery Confluence moved; 4 of its card replays re-kept). Next: Shayne's pick of blue's tiers 2–3 or the next colour.
- 2026-11-17: MC-4.4f done: Emry, Sicarian Infiltrator, Subtlety, Kappa Cannoneer, Preordain, Lórien Revealed — 224 of
  528 play; blue's tier 2 done. `Card.CastAs`, squad, evoke, improvise, scry, `MakePlayable`. Next: blue's tier 3.
- 2026-11-17: MC-4.4g planned (ten parts, Decisions) and g1 done: True-Name Nemesis — 225 of 528 play. Protection
  (`Protection(From)`, `Protection.Stops` in targeting, damage, blocking, attachments). Next: MC-4.4g2, delve.
- 2026-11-17: MC-4.4g1b and g2 done: a clone makes its copy's "as this enters" choices (`MagicState.ChooseAsItEnters`);
  delve — Dig Through Time, Treasure Cruise — 227 of 528 play. The AI's delve picks are a rule (the first cards); a
  procedural price is MC-4.4g2b, later. Next: MC-4.4g3, dungeons and the initiative.
- 2026-11-18: MC-4.4g3 done: Feywild Caretaker — 228 of 528 play. Undercity and venture (a room no chapter: it
  branches), the initiative, goad, `LookAndTake.Then`. Next: MC-4.4g4, face-down.
