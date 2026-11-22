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
- 2026-11-18, Claude — **4.4g4's notes (face-down permanents, manifest dread).** Built as planned, with one change on
  reading: the nearest piece was not `ExiledFaceDown` but `EnteredAsCopy(Original)` — a face-down permanent is a COPY
  stamp, a blank 2/2's copiable values (`Card.TurnedFaceDown`), the card kept on `FaceDown(Original)`. It cannot be an
  option on `EnteredAsCopy`: the marker must be status (a Clone of a face-down 2/2 is a nameless 2/2 with no card
  under it), and only it turns face up on the battlefield. It enters face down (`EnterBattlefield(faceDown:)`, stamped
  before the entry event: no "enters" of its own, CR 708.3); leaving turns it face up (`Card.Fresh`, CR 708.9);
  `Card.IsHiddenFrom` hides it from all but its controller. Manifest dread is `LookAndTake { FaceDown }`
  (`LsvCards.ManifestDread`); "turn it face up any time for its mana cost" is `TurnFaceUpAction`, a special action —
  offered with priority, no stack, the same object. Oculus' six is tier 1 as planned. **Legal actions:** one more
  per face-down creature card you can pay for. **The table:** a face-down permanent reads "Face down"; its controller
  sees the card only in the "Turn face up: X" move (TBL work, DesignNotes). Ceilings (DesignNotes): the AI's search
  sees an opponent's face-down card; no "turned face up" event; Phyrexian and no-cost cards; X = 0.
  **`/pre-commit`'s review: nine findings; four taken, three with a test that fails without it.** Turning face up
  under a type change kept the stamp that remembered the 2/2 — `TurnedFaceUp` undoes it and the state check stamps
  the card afresh. Containment Priest's check judged the hidden card's name, colour and mana value — it judges the
  2/2 (`entering.TurnedFaceDown()`). Face up as it leaves moved from `WithoutCopyEffect` into `Fresh`, one job each.
  The story. **Not taken (ceilings):** Phyrexian life and no-cost cards (`ponytail:`), the owner's view on the table
  (TBL), the AI deciding at every window while it can turn one up (as with an instant held), the label read from the
  empty name (a Clone's nameless copy reads "face-down" too).

- 2026-11-18, Claude — **4.4g5's notes (Adventures).** Built as planned, with one change on reading: the nearest piece
  was face-down's copy stamp more than prepare. `PrepareSpellComponent` has the Adventure's shape (a face and a text)
  but not its rules: a prepare spell is a COPY in exile, never the card from the hand, so the Adventure is its own
  `AdventureComponent(Face, Text)`; the face-to-card step is shared (`CardData.OfFace`, Prepare's moved onto it). The
  card cast as its Adventure is STAMPED (`Card.AsAdventure`: the face's copiable values, the card kept on
  `CastAsAdventure(Original)`, status) and the action validates and casts the stamped card exactly as any other — one
  `CastSpellAction.Adventure` flag that delegates, never a second copy of the cast's rules; `Fresh` undoes the stamp
  (`WithoutAdventure`), and `PutOnStack` re-stamps it after its move. Resolved, it is exiled with its caster's
  `PlayPermission { Forever, CastOnly }` mark; fizzled (`FinishSpellAction.Resolved = false`, CR 608.2b) or countered,
  it is the creature in the graveyard. "Can block only creatures with flying" is `CanBlockOnly(filter)`, the blocker's
  mirror of `CantBeBlockedBy` (the attacker's): a side flag on that piece would be a flag. **Legal actions:** an
  adventurer card in hand offers its casts twice (`|a` in `MoveKey`). Ceilings (DesignNotes): never cast as the
  Adventure from exile; a filter that admits only the Adventure never sees it; the AI's heuristics and the table's art
  read the creature. **`/pre-commit`'s review: nine findings; five taken, one with a test that fails without it** — the
  table's payment preview priced the creature's {1}{U}{U} (`PaymentPreview` delegates now); the Adventure candidates
  skipped in exile (ValidateAdd refused them all); `Describe` guarded; the mark built by `PlayPermission.For`; two
  summaries quote the card. **Not taken:** the instant-only filter (a ceiling, above); one stamp read by every member
  and the AI rather than three entry points that delegate (no cube Adventure needs it yet); building the face once
  per script rather than per call (the probe ran at its usual speed); a cast left stamped in hand when its target
  choice is abandoned (latent: no Adventure chooses targets after the cast).
- 2026-11-18, Claude — **4.4g6's notes (teamwork).** Built as planned, smaller: teamwork IS kicker — an optional
  additional cost, one cast with it, one without — so `LsvCards.Teamwork(n)` is `KickerComponent(ManaCost.None) {
  Costs, Word = "teamwork" }` (Eject the Warp Core's mana-less kicker with a cost), "cast using teamwork" `WasKicked`;
  `Word` only names it in the cast's description. The new part is the cost: "tap any number of creatures with total
  power 2 or more" is `TapUntappedCost { TotalPower }` — an option on the tap cost (Station, Worf, Trimatrix), no pick
  in the action, the creatures picked after it one at a time until the total (`TapChosenForPowerAction`, an existing
  `ChoosePermanentChoice` each, so no new choice type for the AI or the table). The cast pays its mana BEFORE the
  picks, so `Uses` keeps the most powerful creatures out of the payment until the total (`Reserved`) — a mana
  creature can't do both. Ceilings (DesignNotes): it stops at the total and offers no 0-power creature; the
  reservation can refuse a cast a smaller set would allow. **Legal actions:** two casts per target, the creatures
  picked after (a cast per set would be 4,096). Crew (Esika's Chariot, Reckoner Bankbuster) is the same cost on an
  activated ability — read `ActivateAbilityAction`'s `Picks(c)` / `Uses` path first: a `CostSlot.None` cost asks
  `Uses` nothing there yet, and the ability would go on the stack (its event card 0) before the picks tap anything.
  **`/pre-commit`'s review: nine findings; one taken** — power asked once per creature, and `CanPay` a plain sum (no
  behaviour change; its revert-check re-run, caught). **Ceilings, DesignNotes:** the picks come after the spell is on
  the stack (as Drivnod's and delve's), so a cast trigger sees the creatures untapped and a power that changes as the
  cast is paid could leave the total short. **Not taken:** teamwork as kicker (the plan: every "kicked" reader sees
  it, and no cube card reads kicked spells); `Count` beside `TotalPower` (no card sets both); `Describe`'s fallback (a
  guard). **Open (for Shayne):** the beam searches teamwork's picks — one branch per creature at each pick, every order
  of a set. Delve's picks are a rule (Shayne, 2026-11-17); teamwork's could be too (the least valuable creatures
  first). Default taken: left searched — the probe ran at its usual speed (150 games in 1.0 min).
- 2026-11-18, Claude — **4.4g7's notes (storm).** Built as planned, read against `CopySpell` and replicate's
  trigger: storm is a `SelfCast` trigger (`Scripting.Storm`), its copies one `CopySpell` with a count — `CopySpell { N
  }`, as `CreateTokenCopy { N }` (squad) — not replicate's pushed trigger, which is `CastSpellAction`'s alone and so
  misses a cast during a resolution. The count is new: "each spell cast before it this turn" is every player's, fixed
  as it is cast (CR 702.40a — a spell cast in response to the trigger isn't before it), so it rides the spell
  (`OnStackComponent.SpellsCastBefore`, from both players' `TurnTally.SpellsCast`) and `Count.SpellsCastBefore(ref)`
  reads it through `Card.AsCast`. **Found on testing:** a `SelfCast` trigger's pending ability had no snapshot of the
  spell, so a spell countered in response was copied zero times (TRK's SelfCast copier the same) — `Triggers.Collect`
  now gives it the spell as it was cast, the one `CopySpell` and `CountedAsTheyWere` already fall back to.
  **Legal actions:** one cast per target player (2); the copies' targets are chosen one copy at a time (`CopySpell`'s
  `ChooseTargetChoice`s), never a cast per set.
  **`/pre-commit`'s review: six findings; one taken** — replicate's trigger is ONE `CopySpell { N = Replicate }` now,
  not N effects (the replicate tests and the random and AI golden replays unmoved). **Noted:** the snapshot also makes
  TRK's Combadge (`SelfCast` → `CopySpell(Ref.EventCard)`) copied when countered in response — rules-correct (CR
  707.10c); golden-out and the kept records didn't move. **Not taken:** `CopySpell` copies whatever holds the id on the
  stack, so a spell bounced (Remand) and RECAST before the first storm trigger resolves is copied as the new cast with
  the old count — true of every `EventCard` copier before this, and invisible on Brain Freeze (the copies retarget, the
  count is the same); a copy keeps `SpellsCastBefore` (nothing reads a copy's); the two-player sum (the engine is two
  players throughout, as `TurnStructure`'s reset).
- 2026-11-18, Claude — **4.4g8's notes (spree).** Built as planned: `Mode.Cost`, read from the printed "+ {1}{U} — …"
  by `Scripting.Printed` (never retyped), each chosen mode's cost added in `CastSpellAction.CostBeforeLife` beside
  replicate's. **Legal actions, counted before building** (12 creatures and 12 artifacts of mine): a cast per set of
  targets is 49 with an empty stack, 99 in response to one spell, 199 to three. **Shayne, 2026-11-18: word by word** —
  a spree text is `EachWord` (a mode with a cost), as the Confluences': 7 casts, each target chosen as it is cast. The
  other modal spells (Sublime Epiphany) are left to MC-8.
  **`/pre-commit`'s review: nine findings; five taken, three with a test that fails without it (3 breaks caught):** a
  mode index out of range is `Invalid`, never a throw (`CostBeforeLife` runs before the modes are validated, from the
  Phyrexian and delve checks); `Describe.Modes` shows a spree mode's effect, not its cost, and no longer strips a "+"
  from an ordinary bullet ("• +2/+0 …"); `Printed` refuses a spree line outside `ModalSpell` (a trigger or ability
  would never pay it) and names a line with no " —". The summary of `CostToPay` names spree. **Not taken:** `EachWord`
  for every multi-mode spell (MC-8, Shayne); one helper for the four `added` sums (style); the AI's land and curve
  heuristics read the printed mana value (kicker's and replicate's costs the same: an AI-lane note).
- 2026-11-18, Claude — **4.4g9's notes (warp).** No new piece — three options on the plan's nearest pieces: the card's
  own alternative cost `MayCastFromHandFree(Is.Source) { Mana }` takes `From = [Hand]` (warp is cast from the hand
  alone, CR 702.185a; empty is anywhere, as before) and `PaidAtNextEndStep` (effects put on the permanent as a delayed
  trigger when that cost was paid — dash's return takes it as it comes), and `PlayPermission.LaterTurn` →
  `PlayableFromExile.FromTurn` ("on a later turn"). The setup is in `EnterFromStackAction`, as the spell resolves —
  not a "when it enters" trigger as evoke's (evoke IS a trigger; warp isn't, so Tishana's Tidebinder can't keep a
  warped creature by countering its setup). `Scripting.Warp(cost)`. The draw replacement is tier 1:
  `NumericReplacement(Draw) { Plus = 1, If }`, its `If` asked of numeric replacements too (`Replacements.Affects`).
  **Legal actions:** two casts from the hand (paid, warp), one from exile on a later turn.
  **`/pre-commit`'s review: nine findings; three taken, two with a test that fails without it (2 breaks caught):** the
  hand asked was the card's OWNER's, so under Sen Triplets warp was offered from their hand — now the caster's; the
  delayed trigger had no controller, so a stolen Riddler's exile was the thief's — now the spell's (CR 603.7d); a
  summary claimed dash, which isn't built. **Not taken:** naming WHICH alternative cost was paid on the stack (a ceiling,
  DesignNotes — no card has two); the evaluator's impulse term pricing the exiled card before its turn (it is the
  player's card all the same; DesignNotes); reading warp from the spell before it enters (nothing re-stamps it there);
  `LaterTurn` as a number (no second card); a bounce or flicker test (a zone change strips a delayed trigger, tested).
- 2026-11-18, Claude — **4.4g10's notes (Classes).** Built as planned, one new piece: a Class's level (CR 716) is
  `ClassLevel(N)`, a STATUS component (gone as it leaves, never copied; none is level 1) — never a counter, which Brad
  Boimler would add to and "a permanent with a counter" would see. "[Cost]: Level N" is `Scripting.Level(cost, n)`: an
  activated ability, sorcery speed, `OnlyIf` level N−1, resolving to `GainLevel(n)`, which raises `BecameLevelEvent`.
  "When this Class becomes level N" is an option on the Saga's piece, not a sibling: `Trigger.Chapter` on the new
  `EventKind.BecameLevel` (`Scripting.WhenLevel`). A level's abilities are had from that level on: the level-3 trigger's
  `Trigger.While = AtLevel(3)` (judged as the event happens; the ability exists once triggered, CR 113.7a). "It becomes
  a 0/0 Robot creature in addition to its other types" is `ModifyUntilEndOfTurn { ForGood, SetBase, AlsoCreatureType }`
  plus one option, `AlsoType` — the card type stamped as Assimilate's (`Card.WithTypesUntilItLeaves`), ForGood only.
  **Legal actions:** one activation, the next level. 5 tests in `LsvBatch2g10Tests.cs`, 9 breaks caught (one first
  NOT CAUGHT — a level trigger firing at any level; the level-2 test now goes on to level 3).
  **`/pre-commit`'s review: seven findings; one taken:** `Level(cost, n)` with n < 2 was a silent inert ability — it
  throws now. **Not taken:** the type stamp's single `Animated` (a `BecomesType` static on the same artifact — Assimilate's
  limit too; a ceiling, DesignNotes); `AlsoType`'s ForGood check at resolution, not construction (the record's init order
  can't cross-check; the fires test covers the one card); two counts in `OnlyIf` (trivial); `Chapter` read by the Saga's
  state check (it checks the source is that Saga — no card is both).
- 2026-11-18, Claude (cube lane) — **4.4h: black read card by card — 59 mono-black, 11 play; of the 48, 19 are tested
  pieces, 22 options, 7 new pieces (seven mechanics).** The survey's 35/6/7 moved: Grief is tier 1 now (evoke, 4.4f),
  From the Catacombs and Corpse Dance are options (the initiative is g3's; buyback is kicker by another name); Sedgemoor
  Witch's magecraft ("cast or copy" — a copy raises no event) and Rain of Filth (`ManaAbilities` reads no gained ability)
  are options, not tier 1; Animate Dead and Necromancy hide a new piece. Parts:
  **h1** the 19 — Dark Confidant, Iron-Shield Elf, Sunset Saboteur, Harvester of Misery, Archon of Cruelty, Griselbrand,
  Grief, Entomb, Demonic Tutor, Vampiric Tutor, Imperial Seal, Sheoldred's Edict, Dismember, Duress, Thoughtseize, Hymn
  to Tourach, Reanimate, Unearth, Wishclaw Talisman. "Lose life equal to its mana value" (Confidant, Reanimate) is
  `Count.GreatestManaValueOf` of the one card — Reanimate's loss scripted BEFORE the move (the card entered is a new
  object; the same result); Vampiric Tutor and Imperial Seal are Mystical Tutor's `SearchLibrary(None, Library)`;
  **h2a** hands, 4 — Inquisition of Kozilek (a `Filter` on `YouChooseTheyDiscard`; its hand choice has one), Deep-Cavern
  Bat ("look at", not reveal; exile from a HAND until it leaves — `ExileUntilSourceLeaves` takes permanents only),
  Collective Brutality (escalate: spree's `Mode.Cost` per mode beyond the first, a discard — picked after the cast, as
  delve's), Chain of Smog ("that player may copy this spell": `CopySpell` controlled by another);
  **h2b** graveyard returns, 5 — Shallow Grave, Corpse Dance ("the top creature card of your graveyard", a `Ref` option;
  buyback = kicker + to its owner's hand as it resolves), Exhume (each player's own `MoveChosen`), Emperor of Bones
  (adapt composed; `MoveChosen` from exile, under your control, with finality, haste and the end-step sacrifice —
  Sneak Attack's `Then`), Barrowgoyf (`MillAndMayTake`'s N a `Count`; Tarmogoyf's P/T);
  **h2c** costs and permissions, 4 — Toxic Deluge and Bolas's Citadel (`PayLifeCost`'s amount a `Count`; Deluge's X
  chosen with no {X} in its cost, a cast per X up to the life; Citadel is the top-of-library `MayPlayFromZone { Free,
  Costs }`, its sacrifice `SacrificeCost { Count = 10 }`), Rain of Filth (a mana ability gained until end of turn, read
  by `ManaAbilities`), Yawgmoth's Will (Necrodominance's exile-instead in a `ThisTurn`, beside a `ThisTurn` play-from-
  graveyard);
  **h2d** triggers, 4 — Sedgemoor Witch (an event `CopySpell` raises), Orcish Bowmasters (Hullbreacher's "except the
  first in their draw step" on the draw event; amass composed), Gollum (odd or even: a parity option on
  `ChoosesNumberAsItEnters`), Black Widow (`ExileTopPlayable.Then`: may put a counter, else `MakePlayable { ThisTurn,
  AnyType }`);
  **h2e** zones and combat, 4 — Fatal Push (revolt, a `TurnTally` count), Troll of Khazad-dûm (menace's "two or more"
  a number; swampcycling is Lórien Revealed's), Dauthi Voidwalker (shadow is `CantBeBlockedBy` + `CanBlockOnly`; the
  exile-instead with "whose graveyard" and a void counter; a choice among exiled cards, then a free play), From the
  Catacombs (the card's own escape, `MayCastThisFrom { Costs }`; Essence Burn's "would die" widened to "would leave");
  **h3–h9** new pieces, one a session, each with its nearest piece: **h3 overload** (Damn) — `TargetSpec.IfKicked`
  swaps a target word, overload removes targeting; **h4 splice onto Arcane** (Goryo's Vengeance) — no piece adds one
  card's effects to another spell; **h5 Auras returning a creature from a graveyard** (Animate Dead, Necromancy) —
  `AuraComponent` attaches to permanents, and the state check bins an Aura on a graveyard card; Necromancy's "flash,
  sacrificed at cleanup if cast at instant speed" too; **h6 naming a card as a spell resolves** (Demonic Consultation)
  — Kai Winn's name is chosen as a permanent enters and kept on it, a spell has nowhere; the AI's rule for naming in
  its own library; **h7 saddle** (Caustic Bronco) — teamwork's `TapUntappedCost { TotalPower }` plus a "saddled"
  status until end of turn, crew's later; **h8 emerge** (Crabomination) — an alternative cost that sacrifices exists,
  none reduced by the sacrificed's mana value; **h9 miracle** (Metamorphosis Fanatic) — nothing triggers from the hand
  on a draw (its lifelink counter is the keyword counter, Drivnod's).
  **Legal actions, counted on a 12-creature board:** one per single choice — the discards, sacrifices and escalate's
  cards picked after the cast; the most are Deluge's X (~20) and Reanimate (one per creature card in all graveyards).
- 2026-11-18, Shayne (reviewing 4.4h's plan): **as written — h2 in five parts.** **A mechanic's other cards wait for
  their colour:** h3 builds Damn only (Winds of Abandon with white), h4 Goryo's Vengeance only (Through the Breach with
  red).
- 2026-11-18, Claude (cube lane) — **4.4h1: 17 of the 19 scripted; two were options, moved.** Duress' "noncreature,
  nonland" can't be a `CardType` — `ChooseCardsFromHandChoice` matches ANY of the types, so an artifact creature would
  pass as an artifact — it is Inquisition's `Filter` on `YouChooseTheyDiscard` (h2a, now 5). Wishclaw Talisman's "enters
  with three wish counters" has no piece: `EntersWithCountersComponent` puts +1/+1 counters only, a `Kind` option (h2c,
  now 5). Sunset Saboteur's "Ward—Discard a card" is Gideon the Oathless' ward, inline; Archon of Cruelty's two triggers
  share one effect list (`LsvCards.ArchonsCruelty`). Every FIRES test passed first time — no engine line changed.
- 2026-11-19, Claude (cube lane) — **4.4h2a: the five hand cards, four options, no new piece.** `YouChooseTheyDiscard.
  Filter` (Inquisition, Duress — passed to its hand choice's existing `Filter`). Deep-Cavern Bat is `{ Optional,
  UntilSourceLeaves }` → `ExileUntilSourceLeaves(Ref.Chosen)`, widened from permanents to a card in a hand: each stamped
  with the zone it left (`ExiledUntilLeaves.From`), and `MoveCard` returns a hand card to its owner's hand. Escalate is
  `SpellEffectsComponent.Escalate` — not spree's `Mode.Cost`, a `ManaCost`, which can't hold a discard — added by the
  cast's `CostsOf` once per mode beyond the first as `DiscardCost { Count }` (Drivnod's `Count` shape: picked after the
  cast, one choice; `CanPay` bounds it). Chain of Smog composed with no engine change: `May { Who = Ref.Target(),
  ThenAsThem }` over `CopySpell(Ref.Source)` — the copy is theirs; asked only while their opponent has a card in hand, so
  an AI that always says yes can't copy forever (DesignNotes). Legal actions: Brutality's 7 mode sets × targets (12 against
  12 creatures), the discards one choice after.
- 2026-11-19, Claude (cube lane) — **4.4h2b: the five graveyard returns, five options and two widenings, no new piece.**
  Shallow Grave's "the top creature card of your graveyard" is `Ref.All { Top }` (a graveyard's top is its LAST card:
  cards are appended); Corpse Dance's buyback is kicker (`Word = "buyback"`) with `If(WasKicked, MoveTo(Source, Hand) {
  From = [Stack] })` as its last step — `FinishSpellAction` leaves it there. Exhume is `MoveChosen.Who` (each player
  from their own graveyard, one after another — each enters as chosen, a ceiling in DesignNotes). Emperor of Bones:
  `MoveChosen` from EXILE (every player's, `Is.ExiledBySource` saying which) and `MoveChosen.As`, a `MoveTo` carrying
  the move's options (under your control, finality) — Brainstorm's `OnTop` moved onto it, the bool gone; adapt composed
  (`If(Not(AtLeast(CountersOn(Source), 1)))`). **Two pieces widened the plan didn't see:** +1/+1 counters raised no
  `CountersPutEvent` (named kinds and charge only) — now EVERY kind does, and `Trigger.Counters` takes any kind; and
  `Exile` exiled permanents only, so "exile target card from a graveyard" was inert — it takes a graveyard card now (no
  old card targeted one). Barrowgoyf: `MillAndMayTake.N` a `Count` (`Count.X`, the damage); its printed `*/1+*` is
  already base toughness 1, so its toughness static is Tarmogoyf's (Pyrogoyf's), not `Plus = 1`. Legal actions: one
  per choice — Exhume's and Emperor's are a choice each as they resolve. The review: Exhume asks the active player
  first (`Ref.EachPlayer` is always Player 1 first), loyalty raises no second event, `Top` and a loyalty `Counters`
  trigger are refused, the Lhurgoyfs share `LhurgoyfSize`. Seed 28 of `RandomPlay_EveryScriptedCard` hung as 21 did —
  Troubling Tribbles dealt into its decks, 915 permanents by turn 62 — so MC-4.5 was done first (Shayne, 2026-11-19).
- 2026-11-19, Claude (cube lane) — **4.5: Troubling Tribbles out of the random decks.** One filter,
  `RandomGameTests.Dealt` (no land, not Tribbles), read by `RandomPlay_EveryScriptedCard` and the probe's extras
  (`CardProbeTests.Deck`); seed 21's skip is gone, every seed plays. Its own tests and AI-21's Tribble boards are
  untouched. The golden random games regenerated with h2b's.
- 2026-11-19, Claude (cube lane) — **4.4h2c: costs and permissions, five options, no new piece.** Toxic Deluge:
  `PayLifeCost.Amount` is a `Count`, fixed for the cast before paying (`PayLifeCost.For`: `Ref.Source` the card, `Count.X`
  its X); a cost reading X makes the cast choose X with no {X} (`CastSpellAction.XInCosts`) — one cast per X up to the
  life (21 at 20), never added as mana (`XIsMana`). Bolas's Citadel: `MayPlayFromZone.Free` — the permission's cast is
  free, its `Costs` (`PayLifeCost(GreatestManaValueOf(Source))`) the free cast's (`Playable.FreeCast`), the paid cast
  refused, `AnyTime` (instants at instant speed); "sacrifice ten" is Magda's `SacrificeCost { Count }`. Yawgmoth's Will:
  Necrodominance's `ExileInsteadOfYourGraveyard` is a `ThisTurnEffect` now, `What = !Is.Token` ("a card"), beside
  `ThisTurn(MayPlayFromZone(Graveyard))`. Wishclaw Talisman: `EntersWithCountersComponent.Kind`. **Rain of Filth differed
  from the plan:** `AbilitiesOf` already read a gained mana ability — the gap was the solver, whose sources tap once, so a
  land under Rain tapped for {B} and was never sacrificed. A permanent's mana ability without {T} is a move now, as
  Phyrexian Tower's (`ManaAbilityComponent.NoTap` → `TapForManaAction.NoTap`, one per land, a tapped land too; DesignNotes'
  "first such ability" line updated). Its first cut caught the Spirit Guides (a hand card's "exile this" has no {T}
  either) — golden-out moved four games; `NoTap` is a permanent's only. Legal actions: Deluge's 21, Rain's one per land.
  Probe 150: none unfinished; the Citadel's ten-for-ten activated once. The review: Deluge's X is no mana value
  (`Card.ManaValueNow` counts an X in the mana cost only, CR 202.3e), Deluge from the Citadel still chooses X, a cast's
  life payments are judged together (CR 119.4: the Citadel's beside X), `ExileInsteadOfYourGraveyard.What` is read on a
  permanent too; two ceilings in DesignNotes (the Citadel beside Zefram Cochrane; Rain's moves one per land).
- 2026-11-19, Claude (cube lane) — **4.4h2d: triggers, four options and one event, no other new piece.** Sedgemoor
  Witch's magecraft "cast or copy": a copy raised no event, and a `SpellCastEvent` can't take an option — every
  "whenever you cast" and storm's own `SelfCast` would see the copy — so `CopySpell` (the one place a spell copy goes
  on the stack) raises `SpellCopiedEvent`, and `LsvCards.Magecraft` is two triggers sharing the effects (a ceiling,
  DesignNotes); its "Ward—Pay 3 life" is `PayLifeCost` paid by choosing (fixed amounts). Orcish Bowmasters:
  `DrewCardEvent.TurnDraw` (the draw step's first card, Hullbreacher's `turnDraw`) read by `EventTest.NotTheTurnsDraw`;
  amass composed (`If` no Army, an Orc Army; a counter on each Army — ceiling, DesignNotes). Gollum, Riddle Master:
  `ChoosesNumberAsItEnters(1) { OddOrEven }` → "Even"/"Odd", the parity kept on `ChosenNumber.OddOrEven` (so a copy
  effect can't change its meaning), matched by `Is.ManaValueIsChosenNumber`; the AI names the commoner parity (none
  known: even); "one that hasn't been chosen" is Kobayashi Maru's `NotChosenBefore`. Black Widow, Super Spy:
  `ExileTopPlayable { Play = null }` — no permission, `Then` decides: if she's still on the battlefield, "you may put a
  +1/+1 counter", else (or on a no) `MakePlayable(Ref.ThisWay, { ThisTurn, AnyType, CastOnly })` — the review's catch:
  gone, she can't take the counter, so you may cast. "That player" is `Ref.EachOpponent` (Bo1, two players). Legal
  actions: none grow with the board. Golden-out moved nothing; probe 150: none unfinished, each card cast 311–343 times. The
  suite's trigger table (`FilterMigrationTests`) takes `SpellCopied` and Bowmasters' row.
- 2026-11-19, Claude (cube lane) — **4.4h2e: zones and combat, five options and one small piece.** Fatal Push: revolt
  is `TurnTally.PermanentsLeftTheBattlefield` (its CONTROLLER's, any zone, counted in `MoveCard`) read by
  `Count.PermanentsLeftTheBattlefieldThisTurn` — not Miles O'Brien's tally, which is the OWNER's and the hand only;
  the spell is `If(≤2) { Otherwise = If(Both(revolt, ≤4)) }`. Troll of Khazad-dûm: the one new piece,
  `MinimumBlockers(3)`, read with menace by ONE number, `Combat.MinimumBlockers` — the blockers' finish, the look-ahead's
  chumps and joins and the misplay scan all asked `Has("Menace")` before (not a `CantBeBlockedBy` option: that asks one
  blocker, this counts them); swampcycling is Lórien Revealed's. Dauthi Voidwalker: `Scripting.Shadow` (`CantBeBlockedBy`
  + `CanBlockOnly`); `ExileInsteadOfYourGraveyard { Whose, CounterKind }` — `MoveCard` reads both battlefields now, the
  owner picking a rule without a counter first; `Is.WithCounter(kind)` (`WithPlusOneCounter` moved onto it, its
  `Property` gone); "choose an exiled card" is `MoveChosen(Exile, filter, Exile)` — To = From moves nothing (the
  revert-check's catch: a move would make a new object, so the test asserts the void counter stayed). From the
  Catacombs: escape is `MayCastThisFrom { Costs }`, joined by `CastSpellAction.CostsOf` for its player's graveyard cast;
  Essence Burn's `ExileIfItWouldDie` became `ExileInsteadOf` (zones; Essence Burn, Khaaaaaaaaaaaannn! and Suplex
  moved to `[Graveyard]`), the Catacombs' "anywhere else" every other zone, for good; its corpse counter is put on after it
  enters (a ceiling, DesignNotes); the review's catches: escape's cost never joins a flashback cast (CR 118.9a),
  `What` asked from the holder's side, one pass over the rules. 11 tests in `LsvBatch2h2eTests.cs` (`Costing(cost)`: a 3/3 of that cost, `Bounce`,
  `TrollAttacks(troll, n)`), 12/12 revert-checks caught. Legal actions: none grow with the board (the choices come after).
  Golden-out moved nothing; probe 150: none unfinished, each card used 297–434 times (Troll cast 38, cycled 405).
- 2026-11-19, Claude (cube lane) — **4.4h3: overload (Damn), one option, 276 of 528 play.** Nearest pieces read: kicker's
  `TargetSpec.IfKicked` swaps a word in `StackResolution.TextOf`, and Baleful Mastery's `MayCastFromHandFree { Mana }` is
  the alternative mana cost. Overload is both: `MayCastFromHandFree.Overload` (`Scripting.Overload(cost)`), and `TextOf(…,
  alternative)` drops every target word when that cost is paid — the cast's validation and target choice
  (`CastSpellAction.PaysItsAlternativeCost`, extracted from `Cast`'s inline check), the candidates
  (`Playable.OwnAlternativeCost`, moved there by the review: the "may it be cast" place) and a copy (`CopySpell`, from the stack's `AlternativeCost`: no new target asked). "Each" is the
  script's `If(AlternativeCostWasPaid, [Destroy(Ref.Permanents(Is.Creature))]) { Otherwise = [Destroy(Ref.Target())] }`
  — not an engine rewrite of `Ref.Target` (a word holds one id; "each" is many), so Winds of Abandon is the same shape.
  Resolution's `TextOf` was given the flag too and the revert-check said NOT CAUGHT: an empty target list never fizzles,
  so it was taken out. "Can't be regenerated" is inert: no card in any set regenerates. The table reads the overloaded
  cast as "(overload)" (`Describe`; Shayne, 2026-11-19 — not Baleful Mastery's "(not paying its mana cost)"). 3 tests in `LsvBatch2h3Tests.cs`, 6/6 revert-checks
  caught. Legal actions: one overloaded cast whatever the board. Golden-out moved nothing; probe 150: none unfinished,
  Damn cast 701 times.
- 2026-11-19, Claude (cube lane) — **4.4h4: splice onto Arcane (Goryo's Vengeance), one new piece, 277 of 528 play.**
  Nearest pieces read: an Adventure's cast stamp (`CastSpellAction.Adventure`, `Card.AsAdventure`: the card cast differs
  from the card in hand), kicker's added mana, and a mode's `ScopeTargetsAction` (a text's `Target(0)` its own first). No
  option on them adds a card's text from the hand to another spell, so splice is new: `SpliceComponent(Cost, Onto)`
  (`Scripting.SpliceOntoArcane`) and `CastSpellAction.Splice`, the ids of the hand cards spliced. The cast is validated and
  made on the card stamped `Spliced` (texts, ids, the summed cost — status, as `CastAsAdventure`): `CostBeforeLife` adds
  the cost as kicker's, `Cast` reveals the cards (`MakeKnown`), `PutOnStack` re-stamps after the move, and
  `StackResolution.TextOf` appends each text after the spell's own, scoped. So the validation, the target choice, the
  resolution and a copy (`CopySpell` keeps the card's components, CR 707.10) all read one text with no new parameter;
  leaving the stack drops it (`Card.IsStatus`). Candidates: one cast per set of splice cards in hand (`|s` in `MoveKey`,
  "(splicing …)" in `Describe`) — 2^n, a ceiling (DesignNotes; the cube has two). Goryo's own text is tested pieces:
  `MoveTo { From = [Graveyard], ExileAtNextEndStep, Then = haste ForGood }`. 6 tests in `LsvBatch2h4Tests.cs`; 9/9
  revert-checks caught. The review (2026-11-20) added two: the spell's own text is scoped to its own words (an Arcane
  "exile each target" took Goryo's graveyard card too — `Banish`'s test), and a modal spliced text throws rather than
  sitting inert; four shapes no card prints are ceilings (DesignNotes). Legal actions: an Arcane cast × (1 + Goryo's targets) — We Say Thee Nay!
  against S spells with L legends in the graveyard is 2S(1 + L) with teamwork. Golden-out moved nothing; golden-in moved
  30 random games and 81 card games (the new card in the decks); probe 150: none unfinished, Goryo's cast 205 times; full
  suite 2,086 passed. Two findings on the way: four card replays (Bat'leth 02, Damn 01, Data 04, Semester Foreseer 01)
  failed at HEAD already — h3's recording was made before its review's last change; and `tools/batch_checks.py`'s probe
  ran `--no-build` after the revert-checks, so it played the last revert's DLL — it builds now.
- 2026-11-20, Claude (cube lane) — **4.4h5: Auras that return a creature (Animate Dead, Necromancy), one widening, one
  option and one small piece, 279 of 528 play.** Nearest pieces read: `AuraComponent` and `Attachments.Attach` (both
  ends on the battlefield — the state check binned an Aura on a graveyard card), warp's `PaidAtNextEndStep` (a delayed
  trigger set up as the spell resolves, but tied to an alternative cost). **The widening:** an Aura's host is a permanent
  or a card in its owner's graveyard (`Attachments.HostOf`, `Attach`, `Ref.EnchantedCreature`) — the same object only
  while it stays there, so only an Aura attached in a graveyard ever has one. **The option:** "loses 'enchant A', gains
  'enchant B'" and "becomes an Aura with 'enchant creature'" are `ModifyUntilEndOfTurn { ForGood, Enchant }` →
  `UntilEndOfTurn.Enchant`, status, read by `Attachments.EnchantOf` in place of the `AuraComponent` (Does Machines'
  `AlsoType` shape). **The piece:** `CastAsThoughItHadFlash(effects)` — read by `Priority.AtInstantSpeed`, the cast stamped
  `OnStackComponent.NotAtSorcerySpeed` before it moves to the stack, the effects a delayed trigger at
  `Beginning(Step.Cleanup)` set up in `EnterFromStackAction` as warp's; the cleanup step now raises `StepBeganEvent`.
  Animate Dead: its "enters" trigger (intervening "if it's on the battlefield": `Matches(Ref.Permanents(Is.Source))`)
  changes the enchant, then `MoveTo(Ref.EnchantedCreature, Battlefield) { From = [Graveyard], UnderYourControl, Then =
  Attach(Ref.Source, Ref.ThisWay) }`; "when this leaves, that creature's controller sacrifices it" `SelfLeaves` →
  `Sacrifice(Ref.EnchantedCreature)`, read from the Aura as it last was. Necromancy the same with a target. Two
  ceilings (DesignNotes): "enchant creature put onto the battlefield with this" read as "creature", and an Animate Dead
  put onto the battlefield uncast offered only permanents. Legal actions: Animate Dead one cast per creature card in
  all graveyards (Reanimate's); Necromancy one cast, its trigger one target each. 7 tests in `LsvBatch2h5Tests.cs`
  (`Ogre`, `Disenchant`, `ToMyEndStep()`, `ToTheirTurn()`: their turn with my priority, the cleanup behind); 12/12
  revert-checks caught. Golden-out moved nothing (the cleanup event included); golden-in moved 30 random and 118 card
  games, 8 added (the new cards); probe 150: none unfinished, Animate Dead cast 358 times, Necromancy 513; full suite
  2,094 passed. The probe's "abilities on the stack" counts distinct ids across all games — ids restart each game, so it
  undercounts (92 for Animate Dead): read it as "some", not a number. The review (2026-11-20) fixed three: `HostOf`
  is a permanent unless asked `orGraveyard` (the state check, `Ref.EnchantedCreature`) — the table would have hidden an
  Aura under a card it doesn't draw; a Necromancy become an Aura has the subtype (`s.Subtypes`: a "non-Aura enchantment"
  missed it); and the search's "could as well wait for instant speed" (`BeamSearchAi.InstantSpeed`) no longer holds
  Necromancy, whose late cast costs the creature. Protection on a graveyard host is a ceiling, and the AI's "lasts this turn" an AI issue for after
  the cube's implementations (Shayne, 2026-11-20; DesignNotes, the fix outlined). Suite 2,094 and card replays 861 passed after.
- 2026-11-20, Claude (cube lane) — **4.4h6: naming a card as a spell resolves (Demonic Consultation), one small piece
  and one option, 280 of 528 play.** Nearest pieces read: Kai Winn's `ChoosesNameAsItEnters` (asked by
  `EnterBattlefield` only, the name kept on the permanent — a spell had nowhere), Identity Echo's
  `RevealUntilOntoBattlefield` (a card type, the battlefield) and `ExileTopPlayable { Until }` (exiles the found card
  too — Consultation's goes from the library to the hand, never through exile). **The piece:** `ChooseCardName`, an
  effect asking Kai Winn's question (`MagicState.AskName`, now shared), the name kept on the SPELL on the stack
  (`SetChosenNameAction` takes a stack card; `ChosenName` is status, gone as it leaves), so `Is.NamedChosenName` reads
  it in the next steps. **The option:** `RevealUntilOntoBattlefield { Matching, To }` — a filter in place of the type,
  and the hand (revealed: known to everyone); `Exile` was already its "exile the rest". The top six are
  `ExileTopPlayable { N = 6, Play = null }`. **The AI's rule** (`ChooseCardName { YourOwn }` →
  `ChooseKeywordChoice.NamesYourOwn` → `GreedyAi.BestNameInYourLibrary`): the card in its own library worth most in
  hand, by the static evaluator, one name each, blind to the order — never searched (a rule, as Kai Winn's). A ceiling
  (DesignNotes): it never weighs the miss. Legal actions: one cast. 3 tests in `LsvBatch2h6Tests.cs` (`Naming(name)`:
  the name choice answered); 8/8 revert-checks caught. Golden-out moved nothing (162 random, 861 card games); golden-in
  moved 30 random and 75 card games, 4 added; probe 150: none unfinished, Consultation cast 187 times; full suite 2,097
  passed. The review (2026-11-20) made "your own" an option on the effect (it was hardcoded: a Cabal Therapy would have
  named its own card); the singleton miss stays a ceiling.
- 2026-11-20, Claude (cube lane) — **4.4h7–h9 as one batch: saddle, emerge, miracle — 283 of 528 play; black is
  finished.** **Saddle** (Caustic Bronco) composed: teamwork's `TapUntappedCost { TotalPower }` on a sorcery-speed
  activated ability (`LsvCards.Saddle(n)`, "other" `!Is.Source`), "saddled" a keyword granted until end of turn
  (`Is.With("Saddled")` — goad's stamp was the alternative; a ceiling in DesignNotes); the attack trigger is Dark
  Confidant's `LookAndTake` with an `If`. **Emerge** (Crabomination) is an option on the card's alternative cost,
  `MayCastFromHandFree.LessTheSacrificed` (`Scripting.Emerge`: Flare of Fortitude's sacrifice + Baleful Mastery's mana, less
  the `SacrificeId` card's mana value); its three exiles are `ExileTopPlayable { Linked }` (nearest: `Exile { Linked }`;
  `Then`'s `Ref.ThisWay` sees only its own effect's cards) and `CastOneOf` over `Ref.All(Exile, Is.ExiledBySource)`.
  **Miracle** (Metamorphosis Fanatic): `TriggeredAbilityComponent.FromGraveyard` generalised to `From` (a zone — its five
  cards moved), read for the hand too; `DrewCardEvent` carries the card (its subject and event card); `CastOneOf {
  Instead }` → `CastNow.Instead` → `CastSpellAction.ManaInstead` (an effect's cast skipped the card's own alternative
  cost, so it needed a cost of its own). Found on the way: the opening hand drawn unsmoothed, or a mulligan's, raised
  `DrewCardEvent`s the first priority would read — a miracle from the opening hand; `Draw` raises none before turn 1.
  Legal actions: one cast per artifact to sacrifice; saddle one activation, the creatures picked after. The review
  (2026-11-20) fixed four: a trigger from the hand REVEALS its card as it goes on the stack (`PutOnStack` — or the AI's
  hidden-hand sampling could swap the Fanatic for a guess under its own trigger); `Instead` alone is the free path (it
  needed `Free` too, silently); the cast's prompt names the cost, not "free"; `Collect` copies only the hand and graveyard
  cards with such a trigger. The drawn card is the event's subject but never its event card (an opponent's draw is hidden).
  Ceilings (DesignNotes): the reveal never asked, `AlternativeCostWasPaid` false for a miracle, an X miracle cost at X = 0,
  emerge reading the cast's one `SacrificeId`. 12 tests in `LsvBatch2h7Tests.cs`; 13/13 revert-checks caught (the
  opening-hand test was vacuous at first — a smoothed hand is moved, not drawn — and now uses `New(smoothing: false)`).
  Golden-out moved nothing (162 random, 862 card games); golden-in moved 30 random and 194 card games, 12 added; probe 150:
  none unfinished, Bronco cast 393 / saddled 842, Crabomination 248, Fanatic 184 ("activated 5": Q's gift, as before);
  full suite 2,109 passed — all 8 steps run again after the review's fixes. **The one-batch experiment:** the checks ran
  as one batch (22 min, then 34 min in full after the review) where three batches would each have run them; its cost is
  its row in `docs/findings/card-batch-usage.md`.
- 2026-11-20, Shayne: **red is the next colour** (4.4i; its tier 1 was 4.4c).
- 2026-11-20, Claude (cube lane) — **4.4i: red read card by card — 17 left of 54 (52 mono-red, 37 play, and the two
  transforming DFCs); 6 tested pieces, 7 options, 4 new pieces (four mechanics).** **Every colour's survey missed its
  double-faced cards:** it counted `colors == [X]`, and Scryfall's JSON has no top-level `colors` on a transforming or
  modal DFC — so Etali and Fable were outside red's 52, and blue's (Tamiyo, Inquisitive Student; Jace, Vryn's Prodigy;
  Sink into Stupor) and black's (Cecil, Dark Knight; Concealing Curtains) were never counted: **blue and black are not
  finished.** White's and green's DFCs (Ajani, Witch Enchanter, Outland Liberator, Ulvenwald Oddity) are read from the
  front face's mana cost when their colour comes. Parts:
  **i1** the 6 — Embereth Shieldbreaker // Battle Display (tier 1 since g5), Oliphaunt (swampcycling's piece as
  mountaincycling; "another target creature you control" +2/+0 and trample), Unholy Heat (delirium is `AtLeast(
  Count.CardTypesAmong(your graveyard), 4)`: `If { Otherwise }` for the 6 or the 2), Through the Breach (Sneak Attack's
  `MoveChosen { Then }` + `Scripting.SpliceOntoArcane("{2}{R}{R}")`, h4's), Gut, True Soul Zealot ("whenever you
  attack" `AttackersDeclared`, `MaySacrifice { Then }`, a Skeleton token `{ Attacking, Tapped }` with menace; "Choose
  a Background" has no rules force outside a Commander game — no Background is in the cube — so it stays unscripted),
  Headliner Scarlett (`ModifyUntilEndOfTurn(Ref.PermanentsOfTargetPlayer(Creature, 0))` granting `Combat.CantBlock`;
  her upkeep is Vulcan Mind-Meld's `ExileTopPlayable { FaceDown, Play { ThisTurn, AnyType } }`, the looker you);
  **i2a** options, triggers and combat, 4 — Fury (evoke is `Scripting.Evoke`; `TargetSpec.Divide` is read only from a
  SPELL's text, `Division.Of` → `SpellEffectsComponent` — a triggered ability's targets take it, chosen after it
  triggers as Pyrokinesis' are after the cast), Ivora (`DiscardedEvent` is one event per discard however many cards,
  so "whenever you discard a card" triggers once for two: one event per card, as `DrewCardEvent`, and a "one or more"
  discard trigger takes `Trigger.OneOrMore` — the old cards moved; the Blood token composed from tested costs, a
  `Tokens` property), Dragon's Rage Channeler (delirium's static `If`; "attacks each combat if able" is goad's
  requirement — `Combat.IsGoaded`, read by `DeclareAttackerChoice` — generalised to the keyword, granted by the static;
  the surveil trigger is prowess' with `Surveil`), Caves of Chaos Adventurer ("if you've completed a dungeon" — `Venture`
  completes one on its last room and keeps nothing: a game-long count on the player, as `SpellsCastThisGame`, read by a
  condition; White Plume and Undermountain Adventurer read it later; the exile is `ExileTopPlayable { Play = null,
  Then }` → `MakePlayable { ThisTurn [, Free] }`);
  **i2b** options, casting, 3 — Batroc the Leaper (multikicker is kicker paid any number of times: replicate's count on
  the cast, `Card.CastAs`, read as a `Count`; "enters with a counter for each time" `EntersWithCounters` by it; "each of
  up to X targets" PerX words with X that count), Ragavan and Death-Greeter's Champion (dash is warp's alternative cost
  `{ Mana, From = [Hand], PaidAtNextEndStep = return it to its owner's hand }` and haste while that cost was paid, a
  static `If` reading `Card.AsCast`: `Scripting.Dash(cost)`; Ragavan's exile is `ExileTopPlayable { Of = that player,
  Play { ThisTurn, CastOnly } }`; backup is `Scripting.Backup(n, keywords)` — a `SelfEnters` trigger, `PutCounters`, and
  `If(Matches(target, !Is.Source))` the grant until end of turn);
  **i3–i6** new pieces, one a session, each with its nearest piece: **i3 an additional combat phase** (Fear of Missing
  Out) — the steps are an enum walked by `game.Step + 1` (`TurnStructure`), and extra TURNS a queue on `MagicGame`
  read by `FinishCleanup`: an extra combat is a count read as `EndCombat` ends (back to `BeginCombat`, no main phase:
  CR 500.8); its "attacks for the first time each turn" can't be `OncePerTurn` — that stamps a trigger, and an attack
  before delirium never triggered — so an attack's number this turn on `AttackedEvent`, read by `Trigger.Nth` (as
  `SpellNumber`); **i4 counters on a player: energy** (Galvanic Discharge; Guide of Souls waits for white) — counters
  exist on permanents only (`Card.Counters`, `PutCounters`); a player has none, so `MagicPlayer` gets them (CR 122.1),
  "pay any amount of {E}" one number chosen as it resolves; **i5 transforming double-faced cards** (Fable of the
  Mirror-Breaker) — nearest: the face-down copy stamp (`Card.TurnedFaceDown` / `TurnedFaceUp`: the same object, another
  face's copiable values) and the Adventure's second face (`AdventureComponent(Face, Text)`); chapter III "exile, then
  return it transformed" an option on the return; Reflection's copy `CreateTokenCopy { SacrificeAtNextEndStep }` with
  haste; chapter II Tersa's "discard up to two, then draw that many"; **i6 Etali, Primal Conqueror** — transform by an
  activation ({G/P} paid as an activation's Phyrexian mana), poison as the second kind of i4's player counters with
  its state-based loss (CR 704.5c), and "cast any number of spells from among [them] free" `CastOneOf { Free, Exiled }`
  asked again until declined (it casts one); the "until a nonland" exile is `ExileTopPlayable { Until, Of }` for each
  player.
  **Legal actions, counted on a 12-creature board:** none grow with it if Batroc's and Fury's targets are a choice
  after the action — up to X of 14 creatures and players as one move per set is 2^14; picked word by word it is one
  move per kick count (~6). Dash adds one cast per card; Galvanic Discharge's amount and Etali's casts are one choice
  each, asked again.
  **Open (for Shayne): the five blue and black DFCs** — in i5, or a part after it (default: **i7**, after Etali, so i5
  builds the transform on one card first).
- 2026-11-20, Shayne (reviewing 4.4i's plan): **more cards a session** — h7–h9 as one batch cost 4.7M against 1.8–3.6M
  for each one-mechanic part (`card-batch-usage.md`), so red's eight parts are four batches: **i1 + i2a** (the 6 tested
  pieces and the 4 trigger and combat options, 10 cards), **i2b + i3** (multikicker, dash, backup and the extra combat,
  4 cards), **i4–i6** (energy, transforming DFCs, Etali — poison is i4's player counters, Etali i5's transform; 3
  cards, as h7–h9), **i7** (the five missed DFCs, on i5's transform; Sink into Stupor is modal, its own piece). **Each
  session's summary gives the cube's cards implemented by rarity AND by colour** — `python tools/magic_set.py coverage
  --set LSV` prints both (its colour line reads a DFC's front face, the survey's gap).
- 2026-11-20, Claude (cube lane) — **4.4i1 + i2a as one batch: red's 6 tested pieces and 4 trigger and combat options —
  293 of 528 play (red 47 of 54).** i1 as planned, all tier 1 (read on the card sheet): Embereth Shieldbreaker
  (`Adventure`), Oliphaunt (Troll's landcycling as Mountain), Unholy Heat (`If { Otherwise }` on `LsvCards.Delirium`,
  `CardTypesAmong(Ref.CardsInYourGraveyard)` ≥ 4), Through the Breach (Sneak Attack's `MoveChosen { Then }` +
  `SpliceOntoArcane("{2}{R}{R}")`), Gut (`YouAttack`, `MaySacrifice`, a Skeleton `{ Tapped, Attacking }`; "Choose a
  Background" unscripted), Headliner Scarlett (`PermanentsOfTargetPlayer` + `Combat.CantBlock`; `ExileTopPlayable {
  FaceDown, Play { ThisTurn } }`). i2a, four options: **Fury** — `TargetSpec.Divide` on a TRIGGER: `QueueTargets` makes
  the words (`Division.Words`, now shared with `TextOf`), one `ChooseTargetChoice` each, and `PutTriggerOnStackAction`
  asks the split (`Division.Ask`, shared with the cast) after them, the shares on `StackAbility.Shares`. **Ivora** —
  `DiscardedEvent` is one per CARD (`CardId`), and `TriggerEvent.APlayerDiscards` (Tinybones, Inti: "one or more") is
  `OneOrMore`, one per PLAYER per priority pass (`Triggers.OnePerBatch`: Wheel of Fortune's two discards trigger Tinybones
  twice, CR 603.2c — the review caught it collapsing to one; a ceiling left: one player's two separate discards before
  priority trigger Inti once, DesignNotes); the Blood token is `Tokens.CreateBlood`. **Dragon's Rage Channeler** — goad's requirement generalised to
  `Combat.MustAttack` (goaded, or the keyword-like `Combat.AttacksEachCombat`, granted by its delirium static).
  **Caves of Chaos Adventurer** — `Undercity.Complete` (both removals) counts `MagicPlayer.DungeonsCompleted`, read by
  `YouCompletedADungeon`; the exile `ExileTopPlayable { Play = null, Then }` → `MakePlayable { ThisTurn [, Free] }`.
  Legal actions: none grow with the board (Fury's targets one word at a time, the split one choice — the review noted
  the words reach one set in every order, k! duplicates for the AI's search; left as it is, the probe's 272 Fury casts
  finished, and `ChooseXTargetsChoice` taking a spec is the upgrade). 12 tests in `LsvBatch2iTests.cs`; 8/8
  revert-checks caught (Tinybones' OneOrMore by its own old test). Golden-out moved nothing (162 random, 865 card games);
  golden-in moved 30 random and 618 card games, 40 added; probe 150: none unfinished, every card used (Oliphaunt cycled
  248, Fury 272 casts); the checks 17.2 min; after the review's fixes the full suite again, 2,121 passed.
- 2026-11-20, Claude (cube lane) — **4.4i2b + i3 as one batch: multikicker, dash, backup and an additional combat phase —
  297 of 528 play (red 51 of 54).** **Batroc the Leaper** — multikicker is squad's payment with nothing made of it,
  `Scripting.Multikicker` (`ReplicateComponent { AsItEnters }`, `Count.TimesReplicated`); "enters with a counter for
  each time" `EntersWithCountersComponent(that count)` — its context now has `Ref.Source`; "each of up to X targets,
  where X is the times kicked" generalised `TargetSpec.PerX` from a bool to a `Count` (Dominion Infiltration moved to
  `Count.X`): a trigger counts it as it triggers (`Triggers.QueueTargets`, one word at a time), a spell's is its X
  (DesignNotes). **Ragavan, Death-Greeter's Champion** — dash `Scripting.Dash(cost)`: warp's alternative cost, its
  `PaidAtNextEndStep` the return to hand, haste a static `If = AlternativeCostWasPaid`; Ragavan's exile Black Widow's
  `ExileTopPlayable { Of = EachOpponent }` with `Play { ThisTurn, CastOnly }`; backup `Scripting.Backup(n, keyword)`
  (one keyword, DesignNotes). **Fear of Missing Out** (i3, the new piece) — `AdditionalCombatPhase` counts
  `MagicGame.AdditionalCombats`, taken by `TurnStructure.EndStep` as an end of combat step ends (`Combat.EndCombat`,
  then a beginning of combat: CR 500.8, no main phase); "attacks for the first time each turn" is `Trigger.Nth = 1`
  on `Attacked`, against `AttackedEvent.Number` (`Card.TimesAttacked`). Legal actions: none grow with the board
  (Batroc one cast per kick count, his targets one word at a time; dash one cast more per card). 13 tests (2 the review's) in
  `LsvBatch2i2bTests.cs`; 11/11 revert-checks caught (the extra combat's removal from combat only after the test
  gained a Bear attacking in the first combat alone). Golden-out moved nothing (162 random, 875 card games); golden-in
  moved 30 random and 257 card games, 16 added, no AI game; probe 150: none unfinished, every card used (Batroc 313
  casts, Fear of Missing Out 289); suite 2,132 passed; the checks 22.8 min. The review's fixes, each with a test
  (2/2 revert-checks): a required "X targets" trigger with fewer objects than X is removed (CR 603.3d), never an
  unanswerable pick; the evaluator's "their combat is over" (`untapsFirst`, a walker's `attackFirst`) reads
  `AdditionalCombats` — a tapped blocker can't block the combat still owed. After them the full suite again, 2,134 passed.
- 2026-11-20, Shayne (after the 4.4i2b + i3 commit): **look at the "not fixed" items before moving on** — "better to make
  sure everything works now, then me having to find them afterwards". Claude (cube lane): no card in FRA, TRK or LSV
  meets any of the four, but each now works or fails LOUDLY, never silently wrong — **multikicker is kicked** once paid
  (CR 702.33d: `ReplicateComponent.Kicker` → `OnStackComponent.Kicked`, `WasKicked` reads it); **backup grants any
  number of keywords** (`Scripting.Backup(n, keywords)`); **a spell's "X targets" other than its X throws**, and so does
  **an activated ability's** — the search found Candelabra of Tawnos ("{X}, {T}: Untap X target lands", unscripted)
  would have taken ONE silent target: its batch builds a `ChooseXTargetsChoice` for activations (DesignNotes); Pest
  Infestation's "up to X target" spell already works (`ChooseXTargetsChoice`'s min is the required words); **an
  additional combat phase** is right during combat AND the first main phase (two combats in a row either way, now
  tested) and throws anywhere else. 7 tests, 5/5 revert-checks.
- 2026-11-20, Claude (cube lane) — **4.4i4–i6 as one batch: counters on a player, transforming double-faced cards,
  transform by an activation — 300 of 528 play, red 54 of 54: red is finished.** **Galvanic Discharge** (i4) — counters
  exist on a player now (CR 122.1): `MagicPlayer.Counters`, put and removed through the one `MagicState.PutCounters`
  (given a player's id; `MagicPlayer.WithCounters` its own write, as `Card.WithCounters` — `CounterWritesTests` excludes
  both), and `PutCounters` puts on every player its Ref names. "Then you may pay any amount of {E}. … deals that much
  damage" is Necrodominance's piece generalised, not a sibling: `PayAnyLifeThenDraw` became `PayAnyAmount { Kind,
  AtMost, Then }` — a `ChooseNumberChoice` up to what you have, then `Then` with `Count.AmountPaid` (Necrodominance:
  `AtMost` the cards in its library, `Then` the draw; golden-out moved nothing). **Fable of the Mirror-Breaker** (i5) —
  nearest pieces the Adventure's second face and the face-down copy stamp, and a DFC is both: `TransformsInto(Face,
  Keywords, Abilities)` on the card (`LsvCards.BackFace`, the keywords its text names — Scryfall merges both faces'),
  `Card.Transform` the copy stamp (the front on `Transformed`, status; `Card.Fresh` → `FrontFaceUp`, CR 712.14; never a
  token or a copy, CR 701.28c); chapter III `MoveTo(Source, Exile) { Then = MoveTo(ThisWay, Battlefield) { UnderYourControl,
  Transformed } }` → `EnterBattlefield(transformed:)`, stamped before the entry event (so no lore counter, no Saga);
  chapters I–II and Reflection tested pieces (a token with a trigger, Tersa's `Discard { UpTo, Then }`, `CreateTokenCopy
  { AddingKeywords, SacrificeAtNextEndStep }`). **Etali, Primal Conqueror** (i6) — `Transform(ref)` for "{9}{G/P}:
  Transform Etali" (the same object), poison the second kind of player counter with its state-based loss (CR 704.5c),
  "cast any number of spells from among [them]" `CastOneOf { AnyNumber }` (asked again after each cast), the exile two
  `ExileTopPlayable { Of, Until = !Is.Land, Linked, Play = null }`. **Found on the way:** a transforming card has no
  top-level `colors` in Scryfall's JSON, so every cube DFC was COLOURLESS in the engine — `CardFactory` reads the front
  face's now (LSV's 13 DFCs only; none was scripted). The review found the back faces' colour indicators differ (Etali,
  Primal Sickness is red-green; Ajani's, Tamiyo's, Cecil's and She-Hulk's too): the importer keeps `card_faces[].colors`
  (`CardFace.Colors`), LSV re-imported — only face colours changed. A back face has mana value 0 (DesignNotes).
  The review's other fixes, each with a test (5/5 revert-checks): a permanent under a copy effect never transforms, a
  token copy does (CR 701.28c); `CastOneOf { AnyNumber }` offers each card once (`Picked`) — a pick whose cast was
  declined re-offered it forever; a player's counter at 0 is removed, as a permanent's. Legal actions: none grow with the board (the amount is one choice; Etali's casts one choice each, asked
  again; Reflection one activation per creature). 14 tests in `LsvBatch2i4Tests.cs` (2 the review's); 12/12
  revert-checks caught, and the review's 5/5.
  Golden-out moved nothing (162 random, 879 card games); golden-in moved 30 random and 195 card games, 12 added, no AI
  game; probe 150: none unfinished, every card used (Galvanic Discharge 378 casts, Reflection activated 486, Etali
  transformed 77) — the probe counted a transformed card under its back face's name, so Reflection first read
  "activated 0": `CardProbeTests` counts it under the card's name now. After the review's fixes the checks again from
  golden-in (no AI game moved, probe 150 clean); suite 2,155 passed.
- 2026-11-20, Shayne: **i7 as the default** — the five DFCs before another colour, as tiered (below).
- 2026-11-20, Claude (cube lane) — **4.4i7: the five blue and black DFCs every survey missed — 305 of 528 play; blue 75 of
  75 and black 61 of 61: both finished.** **Tamiyo, Inquisitive Student** (tier 2) — "your third card in a turn" is
  `Trigger.Nth = 3` on `Drew`, Fable's exile-and-return (`LsvCards.ReturnTransformed`, under its owner); a planeswalker
  back face enters with its own loyalty (tested: 2). Her −7's "half … rounded up" is `Count.RoundUp` (an option on
  `Per`), the emblem `GetEmblem([UntilEndOfTurn { NoMaximumHandSize, UntilTurnOf = Never }])` — read by
  `MaxHandSizeOf` past the next turn. **Jace, Vryn's Prodigy** (tier 2) — loot, then `If(AtLeast(graveyard, 5))` the same
  return; his −3's "if that spell would be put into your graveyard, exile it instead" is `PlayPermission {
  ExiledLeavingTheStack }` on Emry's mark (the cast not `ByPermission`: flashback's exile). **Cecil, Dark Knight** (tier
  3) — "whenever Cecil deals damage" to anything: no event named the source of damage to a creature, and damage to a
  planeswalker raised none, so `DealtDamageEvent(SourceId, Amount)` from `Damage.Deal` for every recipient
  (`TriggerEvent.SelfDealsDamage`; one per recipient, as lifelink — DesignNotes); "half your starting life total" is
  `MagicPlayer.StartingLife / 2`. **CR 701.28f** in the `Transform` effect: an ability of a permanent transforms it only
  if it hasn't since it was put on the stack (the source snapshot's face) — Cecil's second trigger, Etali activated
  twice. **Concealing Curtains** (tier 3, small) — "when this transforms into Revealing Eye" is `TransformedEvent`,
  raised by `Transform` alone (`TriggerEvent.SelfTransforms`); the discard Salt Vampire's `YouChooseTheyDiscard {
  Filter, Optional, Then }`. **Sink into Stupor // Soporific Springs** (tier 3) — a modal DFC is `BackFace(…) with {
  Modal = true }`: `Card.Transform` refuses it either face up (the test found it flipping a back-face-up one to the
  front: the marker is on the front, kept on `Transformed`), and `PlayLandAction { BackFace }` plays the land face
  (`Card.ModalBackFace` → `BackFaceUp`, the copy stamp), offered beside the casts — a land permission from a graveyard
  plays it though the instant can't be cast there. Legal actions: none grow with the board (one land play per MDFC).
  11 tests in `LsvBatch2i7Tests.cs`; 11/11 revert-checks caught. Golden-out moved nothing (162 random, 882 card games);
  golden-in moved 30 random and 247 card games, 20 added, no AI game; probe 150: none unfinished, every card used (Jace
  activated 1,422, Curtains 311; Sink into Stupor cast 280 — its land plays aren't counted). The probe's "activated 8"
  for Cecil (no activated ability) is Q, Stacking the Deck's grant, as before. Suite 2,166 passed.
- 2026-11-20, Shayne: **white is the next colour** (4.4j; its tier 1 was 4.4d).
- 2026-11-20, Claude (cube lane) — **4.4j: white read card by card — 19 left of 60 (the three DFCs read from the front
  face's cost: Ajani {1}{W}, Witch Enchanter {3}{W}, Jennifer Walters {1}{W}); 6 tested pieces, 9 options, 4 new
  pieces.** MtgCore has none of the four (no monarch, voting, ascend or reconfigure). Parts:
  **j1** the 6 tested pieces — Eagles of the North (Oliphaunt's landcycling as Plains; `SelfEnters`
  `ModifyUntilEndOfTurn(Ref.CreaturesYouControl, 1, 0, "First strike")`), Solitude (`Scripting.Evoke` white; "up to one
  other target creature", Swords to Plowshares' life for its controller, counted before the exile), Witch Enchanter //
  Witch-Blessed Meadow (i7's modal `BackFace`; `EntersTappedComponent { UnlessYouPayLife = 3 }` and a {W} mana ability;
  the front's "destroy target artifact or enchantment an opponent controls"), White Plume Adventurer
  (`TakeTheInitiative`; `EachUpkeep` by an opponent, `If(YouCompletedADungeon) { Otherwise }` over `Untap(all)` and
  `Untap { UpTo = 1 }`), Parallax Wave (fading composed: `EntersWithCountersComponent(5) { Kind = "fade" }`, an upkeep
  `If(AtLeast(CountersOfKind fade, 1), remove) { Otherwise = sacrifice }`, `RemoveCountersCost(1, Is.Source) { Kind }`;
  `Exile { Linked }` and Skyclave Apparition's `SelfLeaves` → `MoveTo(Ref.All(Exile, Is.ExiledBySource), Battlefield)`,
  each card to its owner — the classic trick, an exile resolving after the Wave left, keeps the card: right, as printed),
  Get Lost (a Map token beside Clue in `Tokens.cs`: `{1}, {T}, sacrifice: target creature you control explores`, sorcery
  speed — Explore is tested; `CreateTokens(Map, 2) { ControlledBy = Ref.ControllerOfTarget(0) }`);
  **j2a** options, protection and triggers, 5 — Mother of Runes and Giver of Runes ("protection from the color of your
  choice [or from colorless] until end of turn": Hungering Puppetbeast's `GainsChosenKeyword` with the choices granting
  `Protection(Is.OfColor(c))` / `Protection(Is.Colorless)` through `ModifyUntilEndOfTurn.Gains` — no card yet grants
  `Protection` until end of turn), Seasoned Dungeoneer (that grant with `Protection(Is.Creature)`; `YouAttack`, a target
  attacking Cleric, Rogue, Warrior or Wizard, `Explore`; `TakeTheInitiative`), Winds of Abandon (h3's overload; single:
  Path to Exile's fetch with no "may"; overloaded: `Exile(each) { ThenWithXExiled }` → `SearchLibrary { Who =
  EachOpponent, UpTo = Count.X, Tapped }` — a counted search by another player), Jennifer Walters // The Sensational
  She-Hulk (Voice of Victory's `CantCastOrActivate { By, Spells, If = DuringYourTurn }` on both faces; Etali's transform
  by an activation, sorcery speed; "whenever a creature you control is dealt damage" is `SelfDealtDamage`'s event with
  `Subject = Is.Creature & Is.Yours`, "that much" its X; "do this only once each turn" `OncePerTurn` — a ceiling: the
  rules let every instance trigger and you pick which one to use, DesignNotes);
  **j2b** options, counters, costs and tokens, 4 — Guide of Souls (i4's energy: `PutCounters(Ref.You)`; "you may pay
  {E}{E}{E}" needs a player's counters COUNTED — `Count.CountersOfKind` reads cards only — and removed, then
  `WhenYouDo`: two +1/+1 counters and a flying counter (a keyword counter, as Drivnod's indestructible), "becomes an
  Angel in addition to its other types" with no duration `ModifyUntilEndOfTurn { ForGood, AlsoCreatureType }`),
  Prismatic Ending (converge: Sunburst's `OnStackComponent.ColorsSpent` as a `Count`, and the most-colours payment for a
  converge spell too — today it is keyed to `HasComponent<Sunburst>()`; `If(Matches(target, ManaValueAtMost(that)))`),
  Sunfall (`Exile(all creatures) { ThenWithXExiled }`, incubate X: an Incubator artifact token with X +1/+1 counters and
  "{2}: Transform this token", its back face a 0/0 Phyrexian artifact creature — i5's `TransformsInto` on a token's
  `CardData`), Ajani, Nacatl Pariah // Ajani, Nacatl Avenger (front: a 2/1 Cat Warrior token; "one or more other Cats
  you control die" `OneOrMore`, then i7's `LsvCards.ReturnTransformed`; +2 `PutCounters` on Cats; 0: the token, then
  `WhenYouDo` under "if you control a red permanent other than Ajani", damage `Count.CreaturesYouControl` to any
  target; **−4** "each opponent chooses an artifact, a creature, an enchantment, and a planeswalker from among the
  nonland permanents they control, then sacrifices the rest" — Balance's `EachSacrifices` with what is KEPT, one choice
  per type, never a set);
  **j3–j6** new pieces, each with its nearest: **j3 the monarch** (Palace Jailer; Okoye, Forth Eorlingas! and Aragorn
  take it with multicolour) — the initiative's object in the command zone (`InitiativeComponent`: one holder, its two
  triggers, `Take`), generalised to a designation with its own triggers (the monarch's: "at the beginning of your end
  step, draw a card", "whenever a creature deals combat damage to you, its controller becomes the monarch") — no
  venture; "exile until an opponent becomes the monarch" is `ExileUntilSourceLeaves`' stamp with another end — returned
  as the designation moves to an opponent of the exiler, not as a source leaves; **j4 ascend** (Ocelot Pride) — the
  city's blessing is a game-long flag on the player, as `DungeonsCompleted`, got in the state-based check by a player
  controlling ten or more permanents while one has ascend (CR 702.131); "if you gained life this turn" an intervening if
  on `LifeYouGainedThisTurn`; "for each token you control that entered this turn" `CreateTokenCopy(Ref.Permanents(Is.Token
  & Is.Yours & Is.EnteredThisTurn))`; **j5 reconfigure** (Lion Sash) — `Equip`'s attach plus "unattach", and "while
  attached, this isn't a creature" — Mogis' `BecomesType { Remove = Creature, If }`, but an Equipment that is a creature
  falls off in `Attachments.Check` (CR 301.5c), which reconfigure is exempt from (CR 702.151b), so a rule there; the rest
  is tested (an exile from a graveyard, a counter if it was a permanent card, `AttachedGets` counted by its counters);
  **j6 voting** (Council's Judgment) — "each player votes for a nonland permanent you don't control" is no target
  (hexproof and protection don't stop it); nearest `EachSacrifices`' each-player choice, but the votes are tallied, and
  "exile each with the most votes or tied" reads the tally — a new effect, `Vote(filter) { Then }` with `Ref.Chosen` the
  winners.
  **Legal actions, counted on a 12-creature board:** none grow with it — the colour choices are one choice (six), the
  votes one choice per player, Ajani's −4 four choices, Winds' overload one cast, reconfigure one action per creature.
  **Batches (Shayne: more cards a session):** **j1 + j2a** (11 cards), **j2b + j3** (5), **j4–j6** (3).
- 2026-11-20, Shayne (reviewing 4.4j's plan): **taken as written** — the tiers and the three batches; j1 + j2a begins
  in the same session.
- 2026-11-20, Claude (cube lane) — **4.4j1 + j2a as one batch: white's 6 tested pieces and 5 protection and trigger
  options — 316 of 528 play (white 52 of 60).** j1 as planned, all tier 1 (read on the card sheet): Eagles of the North,
  Solitude, Witch Enchanter // Witch-Blessed Meadow, White Plume Adventurer (`Beginning(Upkeep, Opponent)`), Parallax
  Wave (`LsvCards.Fading(n)`, composed; `CounterKinds.Fade`), Get Lost (the Map token is LSV's own, `LsvCards.CreateMap` —
  one set makes it, so not `Tokens.cs`). j2a: **"protection from the color of your choice"** is `GainsChosenKeyword {
  Grants }` — the component each word grants by its index, through `ModifyUntilEndOfTurn.Gains` (Mother of Runes; Giver
  of Runes adds colorless); Seasoned Dungeoneer the same `Gains = [Protection(Is.Creature)]`; Winds of Abandon's
  overload `Exile { ThenWithXExiled }` → `SearchLibrary { Who = EachOpponent, UpTo = Count.X, Tapped }` (no new piece:
  the search took both options already); She-Hulk's trigger `CreatureDealtDamage` with `Subject = Is.Creature &
  Is.Yours`. Voice of Victory and Jennifer Walters share `LsvCards.OpponentsCantCastDuringYourTurn` (Voice moved onto
  it). Two ceilings in DesignNotes: She-Hulk's "do this only once each turn" as `OncePerTurn`, and Winds' one search
  for the one opponent. 5 tests in `LsvBatch2jTests.cs` (`Rune(source, target, option)`; `Bolt`/`Murder`: a red and a
  black instant — an inline card has no colour unless its data says so); 9/9 revert-checks caught. Golden-out moved
  nothing (162 random, 887 card games); golden-in moved 30 random and 583 card games, 44 added, no AI game; probe 150:
  none unfinished, every card used (Mother activated 868, Giver 742, Parallax Wave 799, Jennifer 74). Suite 2,171 passed.
- 2026-11-21, Claude (cube lane) — **4.4j2b + j3 as one batch: white's counter, cost and token options and the monarch —
  321 of 528 play (white 57 of 60).** As planned, but for three things found building. **Guide of Souls**: `Count.
  CountersOfKind` and `RemoveCounters { Kind }` reach a player as `PutCounters` does — "you may pay {E}{E}{E}. When you
  do" is `May(AtLeast(CountersOfKind(You, Energy), 3), [RemoveCounters, WhenYouDo])`; the flying counter `CounterKinds.
  Flying` (Drivnod's keyword counter); the Angel `ForGood, AlsoCreatureType`. **Prismatic Ending**: converge is the
  `Sunburst` marker on the card (the most-colours payment) and `Count.ColorsSpent(Ref.Source)` — no option needed on an
  instant or sorcery (a converge permanent would get the charge counters: DesignNotes). Found: the most-colours payment
  repeated the cost's OWN colour ({X}{W}, X = 2 spent two) — its known ceiling; fixed at the root, the subsets leave out
  the cost's pips' colours (Engineered Explosives' {X} unchanged). **Sunfall**: `LsvCards.Incubate(x)`, an Incubator
  token with `TransformsInto` among its abilities — no new piece. **Ajani**: `WhenYouDo { If }` (the intervening if on a
  reflexive trigger, checked as it triggers and again on resolution); the −4 `EachSacrifices { Keeps = [types] }` — one
  required choice per type there is one of, then the rest of the filter sacrificed (`SacrificeTheRestAction`).
  **j3 the monarch**: `InitiativeComponent` generalised to `DesignationComponent(name)` (`Initiative`, `Monarch`; the
  initiative's tests moved onto it), `BecomeTheMonarch`; Palace Jailer's exile `ExileUntilSourceLeaves {
  UntilAnOpponentTakes = Monarch }` — stamped with no source, returned by `Take` as the designation MOVES to an opponent
  of the exiler (ceiling in DesignNotes: one exiled while they already hold it waits until they take it again). 12
  tests in `LsvBatch2j2bTests.cs`; 10/10 revert-checks caught (the Ajani one after its test also asserted no target is
  asked — the resolution check alone stopped the damage). Golden-out moved nothing (162 random, 898 card games);
  golden-in moved 30 random and 309 card games, 20 added, no AI game; probe 150: none unfinished, every card used (Ajani
  activated 343). Suite 2,183 passed.
- 2026-11-21, Claude (cube lane) — **4.4j4–j6 as one batch: ascend, reconfigure and voting — 324 of 528 play (white 60 of
  60: white is finished).** As planned. **j4 ascend** (Ocelot Pride): the city's blessing is `MagicPlayer.CitysBlessing`,
  set by the state-based check (ten or more permanents, one with the printed keyword: `YouHaveTheCitysBlessing.Earned`);
  "if you have the city's blessing" is `YouHaveTheCitysBlessing` — the flag OR earned this moment, because ascend is a
  static ability (CR 702.131b): with nine permanents the end-step Cat is the tenth and is copied. The trigger is `YourEndStep`
  `{ If = LifeYouGainedThisTurn ≥ 1 }`, `CreateTokens(Cat)`, then `CreateTokenCopy(Ref.Permanents(Is.Token & Is.Yours &
  Is.EnteredThisTurn))`. **j5 reconfigure** (Lion Sash): `LsvCards.Reconfigure(cost)` — equip's attach, `Unattach(Ref.Source)`
  (new, Attach's opposite) `OnlyIf = Matches(Ref.Source, Is.Attached)` (a new filter leaf, `Property.Attached`), and
  Mogis' `BecomesType(Is.Source & Is.Attached) { Remove = Creature }`; `Attachments.Check` now puts the type changes
  right BEFORE the fall-off (an Equipment creature falls off, CR 301.5c; the attach resolves while it is one) —
  found in /pre-commit, it replaced a reconfigure exemption; `Attachments.Unattach` is the one unattach.
  **j6 voting** (Council's Judgment): `Vote(filter) { Then }` — a `ChooseAmongChoice` per player (`DeciderId` the voter,
  you first), the candidates asked from the caster's side, no target; `TallyVotesAction` runs `Then` with `Ref.Chosen` the
  most-voted, ties included. Two ceilings in DesignNotes: voting is two players; reconfigure read live (one losing it
  while attached falls off). 11 tests in `LsvBatch2j4Tests.cs` (`KeepLands(n)`: my lands but n to the graveyard;
  `Votes(mine, theirs)`); 11/12 revert-checks caught — the twelfth (exile every permanent voted for) can't differ from
  "the most votes" with two voters, so no test can see it. Golden-out moved nothing (162 random, 903 card games);
  golden-in moved 30 random and 202 card games, 12 added, no AI game; probe 150: none unfinished, every card used (Lion
  Sash activated 3,658, Council's Judgment cast 248). Suite 2,194 passed.
- 2026-11-21, Shayne: **green is next** (4.4k) — the last colour; 40 left of 50.
- 2026-11-21, Claude (cube lane) — **4.4k: green read card by card — 40 left of 50 (the three DFCs from the front
  face's cost: Outland Liberator {1}{G}, Ulvenwald Oddity {2}{G}{G}, Emeritus of Abundance // Regrowth a prepare card
  {2}{G}); 21 tested pieces, 15 options, 4 new pieces.** MtgCore has none of the four (no crew, earthbend, bestow or
  day/night). Parts:
  **k1** the 21 tested pieces — Bejeweled Warg (a modal `SelfDealsCombatDamageToPlayer`: `PutCounters` on a target Wolf
  you control, or `CreateTreasure`), Keen-Eyed Curator (`{1}: Exile { Linked }` a target card from a graveyard; a static
  `If = AtLeast(Count.CardTypesAmong(Ref.All(Exile, Is.ExiledBySource)), 4)` +4/+4 and trample), Lotus Cobra (landfall
  `AddManaOfAnyOneColor(1)` — a trigger's mana, as Mana Drain's delayed one), Scythecat Cub (landfall `PutCounters` on a
  target creature you control; Burnham's "the second time this ability has resolved this turn", Bristly Bill's double),
  Sylvan Caryatid (`AnyColorMana`), Emeritus of Abundance // Regrowth (`EntersPrepared`; `SelfAttacks { If =
  AtLeast(Count.LandsYouControl, 8) }` → `BecomePrepared`; `PrepareSpell` returning a target card from your graveyard to
  hand), Endurance (`Evoke` green; "up to one target player" `MoveTo(their graveyard, Library)` — the bottom is MoveTo's
  default; "in a random order" read when built), Formidable Speaker (`May` discard → `SearchLibrary(Creature, Hand) {
  Reveal }`; `{1}, {T}: Untap` another target permanent), Mole Man, Moloid Master (`MayPlayFromZone(Graveyard, Is.Land)`
  — Nog's; landfall a Moloid token with `SelfAttacks` `May` mill one), Sentinel of the Nameless City (`SelfEnters` and
  `SelfAttacks` → `LsvCards.CreateMap`), Six (`MillAndMayTake(3, land)` — First Contact's; retrace is
  `MayPlayFromZone(Graveyard, nonland permanent) { Costs = [discard a land] }` — Underworld Breach's escape shape, cast
  in addition to its mana cost, during your turn), Icetill Explorer (`AdditionalLandEachTurn`, Nog's graveyard lands,
  landfall mill one), Ouroboroid (`BeginningOfCombatOnYourTurn` `PutCounters(Ref.CreaturesYouControl,
  Count.PowerOf(Ref.Source))`), Ulvenwald Oddity // Ulvenwald Behemoth (Etali's `Transform` by an activation; the back's
  anthem a static), Generous Ent (`CreateFood`; forestcycling — Eagles of the North's landcycling as Forest), Tear
  Asunder (`KickerComponent {1}{B}`, `TargetSpec.IfKicked` — Highly Illogical's), Force of Vigor (Force of Negation's
  `MayCastFromHandFree { If = not your turn, Costs = [exile a green card] }`; up to two targets), Natural Order
  (`AdditionalCostsComponent([SacrificeCost(green creature)])` — Silence the Echo's; `SearchLibrary` a green creature
  onto the battlefield), Malevolent Rumble (`MillAndMayTake(4, permanent)` — First Contact's exactly; an Eldrazi Spawn
  token, new token data only: 0/1, "Sacrifice this token: Add {C}"), Titania, Protector of Argoth (`SelfEnters` `MoveTo`
  a target land card from your graveyard to the battlefield; `ChangedZone { From = Battlefield, To = Graveyard }` with
  `Subject = Is.Land & Is.Yours` → a 5/3 Elemental), Pest Infestation (up to X targets — Dominion Infiltration's pick
  after X; `CreateTokens(Pest, Count.X { Times = 2 })`, the Pest's dies-gain-1 its own trigger);
  **k2a** options, mana, lands and tokens, 7 — Fanatic of Rhonas (ferocious: "activate only if" on a MANA ability —
  `ManaAbilityComponent` has no `OnlyIf`, the activated ability's option moved across; eternalize `LsvCards.Eternalize`:
  a graveyard ability `{ From = Graveyard, ExileSelfCost }` at sorcery speed, `CreateTokenCopy { OfAnExiledCard }`
  with "except it's a 4/4 black Zombie … with no mana cost" — new EXCEPT options on CreateTokenCopy: base P/T, colour,
  added creature type, no mana cost), Undermountain Adventurer (`TakeTheInitiative`; "add {G}{G}; if you've completed a
  dungeon, add six {G} instead" — two mana abilities under the same `OnlyIf`, `YouCompletedADungeon` and its `Not`; the
  yield picks the most), Baloth Prime (enters tapped with six stun counters: `EntersTappedComponent`,
  `EntersWithCountersComponent(6) { Kind = Stun }` — stun is tested by `Untap`; "whenever you sacrifice a land" — no
  event says a move was a sacrifice: `MagicState.Sacrifice` marks its `ChangedZoneEvent` `{ Sacrificed }`, a Trigger
  option; `{4}, sacrifice a land: gain 2`), Fastbond ("any number of lands" — `AdditionalLandEachTurn { AnyNumber }`,
  read by `LandDrops`; `LandPlayed` `{ If = lands played this turn ≥ 2 }`, a Count over `MagicPlayer.LandsPlayed`),
  Sylvan Safekeeper (shroud, CR 702.18: hexproof from every player — beside `"Hexproof"` in `TargetSpec`, the
  controller too; a `SacrificeCost(Is.Land)`), Vaultborn Tyrant (`CreatureYouControlEnters { Subject = Is.Source |
  Is.PowerAtLeast(4) }`; `SelfDies { If = not a token }` → a token copy of the card that died, "except it's an
  artifact" — the same EXCEPT option as eternalize's, a type added), Woodfall Primus (persist `LsvCards.Persist`:
  `SelfDies { If = no −1/−1 counter as it last was }` → `MoveTo(Battlefield) { Counters }` — today +1/+1 only: a counter
  KIND option; ETB destroy a target noncreature permanent);
  **k2b** options, triggers, combat and casting, 8 — Surrak, Elusive Hunter (`CantBeCountered`; "a creature you control
  or a creature SPELL you control becomes the target of" an opponent's — `SelfBecomesTargetOfOpponentsSpellOrAbility`'s
  event with `Subject = Is.Creature & Is.Yours`, a spell on the stack a subject too — She-Hulk's shape), Ursine
  Monstrosity (beginning of combat: mill one; "attacks that player this combat if able" DRC's `MustAttack` grant until
  end of turn; indestructible and +X/+X, X `Count.CardTypesAmong(Ref.CardsInYourGraveyard)`; "an opponent at random"
  is the opponent — two players, a ceiling as voting's), Leatherhead, Swamp Stalker (enters with a hexproof counter —
  a keyword counter as Guide of Souls' flying; combat damage → `May` remove A counter of any kind (one choice among the
  kinds on her), `WhenYouDo` destroy a target artifact or enchantment that player controls), Questing Beast
  (`CantBeBlockedBy(power ≤ 2)`; "combat damage that would be dealt by creatures you control can't be prevented" — an
  option read where `Damage.Deal` prevents (protection, `PreventAllDamageToYou`); combat damage to an opponent → "that
  much" to a target planeswalker that player controls), Worldspine Wurm (`SelfDies` three 5/5 Wurms; "put into a
  graveyard from anywhere" a `ChangedZone { To = Graveyard }` trigger read from the graveyard (`From = Graveyard`) →
  `MoveTo(Library) { Shuffle }` — Fblthp's), Once Upon a Time (`MayCastFromHandFree(Is.Source) { If }` — "the first
  spell you've cast this game" is today read from the spell's stamp (`FirstSpellYouCastThisGame`), so before casting it
  needs the player's count: an option on the condition; top five, may take a creature or land, the rest
  `BottomAtRandom`), Green Sun's Zenith (`SearchLibrary` green creature `ManaValueAtMost(X)` onto the battlefield;
  "shuffle Green Sun's Zenith into its owner's library" — the spell's own way off the stack, Jace's
  `ExiledLeavingTheStack` the nearest: a zone option), Hexdrinker (level up `LsvCards.LevelUp(cost)`: sorcery-speed
  `PutCounters(Ref.Source, 1) { Kind = level }` and statics `If` on the level counters, LEVEL 3–7 4/4 and LEVEL 8+ 6/6;
  "protection from instants / everything" granted by a STATIC — no static grants a component yet, only
  `UntilEndOfTurn.Gains`: a `Grants` option read where `AbilitiesOf` reads Gains; base P/T as Station sets it, else
  +N/+N with a ceiling);
  **k3** new pieces, each with its nearest: **crew** (Esika's Chariot) — saddle's cost (`TapUntappedCost {
  TotalPower }`, the creatures picked after activating; summoning-sick ones may crew, CR 702.122) at instant speed,
  then "becomes an artifact creature until end of turn" (`ModifyUntilEndOfTurn { AlsoType = Creature }`, its printed
  P/T read while it is one — Station's rule); likely composed as `LsvCards.Crew(n)` (Smuggler's Copter and Reckoner
  Bankbuster wait for it in colourless); the rest is tested (two Cat tokens; `SelfAttacks` `CreateTokenCopy` of a target
  token you control — Replicator's); **earthbend** (Badgermole Cub) — a keyword action: a target land you control
  becomes a 0/0 creature with haste that's still a land (Does Machines' `AlsoType` 0/0, `ForGood`), a +1/+1 counter, and
  "when it dies or is exiled, return it to the battlefield tapped" — a leave trigger it GAINS, read from the card as it
  last was (the risk); "whenever you tap a creature for mana, add an additional {G}" is Molten Tide's
  `ExtraRedFromMountains` generalised to a filter and a colour, a this-turn effect or a static (Molten Tide moved onto
  it); **bestow** (Springheart Nantuko, CR 702.103) — cast for its bestow cost as an Aura with enchant creature
  (Necromancy's `ModifyUntilEndOfTurn.Enchant`, an alternative cost as Baleful Mastery's `Mana`), a creature again once
  unattached (it doesn't go to the graveyard); landfall `May` pay {1}{G} `If = Is.Attached` → `CreateTokenCopy` of the
  host, `Otherwise` a 1/1 Insect; **day/night** (Outland Liberator // Frenzied Trapbreaker, CR 726) — a game
  designation (j3's `DesignationComponent` is per player; this is the game's), set to day as a daybound permanent
  appears, flipped as each turn begins by the spells the previous active player cast (`Count.SpellsYouCastThisTurn`:
  none → night, two or more → day); daybound/nightbound permanents transform to match (i5's `Transform`), and one
  entering at night enters transformed.
  **Legal actions, counted on a 12-creature board:** Force of Vigor's up to two targets among 12 artifacts and
  enchantments is 78 casts (the existing combinations; under the few dozen per side it was built for only in a normal
  game — a choice after the cast if the probe slows); Six's retrace one cast per graveyard card per land in hand (12 ×
  5 = 60 at worst); the rest don't grow — Pest Infestation's targets picked after X, crew's creatures after
  activating, Leatherhead's counter one choice, Curator one activation per graveyard card.
  **Batches (Shayne: more cards a session):** **k1** (21 cards, script-only, read on the card sheet), **k2a** (7),
  **k2b** (8), **k3** (4 new pieces).
- 2026-11-21, Shayne (reviewing 4.4k's plan): **taken as written** — the tiers and the four batches; k1 begins in a new
  session.
- 2026-11-21, Claude (cube lane) — **4.4k1: green's 21 tested pieces — 345 of 528 play (green 31 of 50).** Two of the 21
  weren't tier 1 on reading the code, each an OPTION on the nearest piece: **Scythecat Cub**'s "… instead" is
  `IfNthResolutionThisTurn { Otherwise }` (Burnham's piece had no other branch; one counter, the second time double —
  `PutCounters(target, Count.CountersOn(Ref.It))`); **Titania**'s "whenever a land you control is put into a graveyard from
  the battlefield" needed `ChangedZoneEvent` for a land's move there — `MoveCard` raised it only for a permanent reading its
  own leaving; now for every land to a graveyard too (rare: fetchlands, sacrificed lands — Baloth Prime's k2a sacrifice
  option builds on it). Ouroboroid's X fixed as it resolves is script order: the others first, then itself. Malevolent
  Rumble is `LookAndTake { Rest = Graveyard }` (not a mill: "put the rest into your graveyard"); Six's retrace
  `MayPlayFromZone(Graveyard, nonland permanent) { Costs = [DiscardCost(Is.Land)] }`; Emeritus takes FRA's prepare
  helpers, moved up to `Scripting` (`EntersPrepared`, `PrepareSpell`). New token data: Moloid, Eldrazi Spawn (sacrifice
  for {C}, a no-tap mana ability), Titania's 5/3 Elemental; Pest Infestation reuses Sedgemoor Witch's Pest. Two ceilings
  in DesignNotes: Endurance's "random order" is the graveyard's; Titania's "you control" reads the land card's owner. 2
  tests in `LsvBatch2kTests.cs` (`LandEnters(target)`: a New Planet token's landfall, its target answered), 2/2
  revert-checks caught; FraPrepareTests' every-prepare-card test puts a card in my graveyard (Regrowth's target).
  Golden-out moved nothing (162 random, 906 card games); golden-in moved 30 random and 1,200 card games, 84 added, no AI
  game; probe 150: none unfinished, all 21 used (Keen-Eyed Curator activated 698, Formidable Speaker 430). Suite 2,197
  passed.
- 2026-11-21, Claude (cube lane) — **4.4k2a: green's mana, land and token options — 352 of 528 play (green 38 of 50).**
  All seven tier 2, each an option on the nearest piece as planned: a mana ability's "activate only if" is
  `ManaAbilityComponent.OnlyIf`, judged in `ManaAbilities.AbilitiesOf` (Fanatic's ferocious {G}{G}{G}{G}; Undermountain
  Adventurer's {G}{G} under `Not(YouCompletedADungeon)` beside a six-{G} ability under it — the yield picks the most
  allowed); eternalize `LsvCards.Eternalize(cost)` with `CreateTokenCopy`'s new except-options `SetBase`, `Colors`,
  `AlsoSubtypes`, `NoManaCost` (and `AlsoType`, Vaultborn Tyrant's artifact); "whenever you sacrifice a land" is
  `EventTest.Sacrificed` on k1's land zone-change event — `MagicState.Sacrifice` tells `MoveCard` (`sacrificed:`), which
  marks `ChangedZoneEvent.Sacrificed`; Fastbond `AdditionalLandEachTurn { AnyNumber }` (`LandDrops` = `int.MaxValue`) and
  `Count.LandsYouPlayedThisTurn`; shroud beside hexproof in `TargetSpec` (every player's); persist `LsvCards.Persist`
  with `MoveTo.CounterKind`. New token data: Baloth Prime's untrampled 4/4 Beast. One ceiling in DesignNotes (a
  non-+1/+1 `MoveTo` counter put just after entering; a sacrificed land exiled instead raises no event; Baloth reads the
  land's owner, Titania's). The review moved `OnlyIf` from `Usable` into `AbilitiesOf` (every reader, no ceiling) and
  `LandDrops` back to an allocation-free loop; `AnyNumber` stays a flag, as `MaySacrifice { AnyNumber }`. 8 tests in `LsvBatch2k2aTests.cs` (`Mana(card)`: the units its yield makes, `Settle()`:
  triggers raised outside an action resolved, `Enters(card)`); 12/12 revert-checks caught. Golden-out moved nothing (162
  random, 927 card games); golden-in moved 30 random and 438 card games, 28 added, no AI game; probe 150: none
  unfinished, all seven used (Sylvan Safekeeper activated 1,168, Baloth Prime 479; Undermountain's "activated 0" is a
  mana ability's, never counted). Suite 2,205 passed.
- 2026-11-21, Claude (cube lane) — **4.4k2b: green's trigger, combat and casting options — 360 of 528 play (green 46 of
  50).** All eight tier 2, each an option on the nearest piece; three read differently from the plan once the code was
  open. **Surrak** is MACO Strike Team's trigger as written — the fix is in the EVENT: `BecameTargetEvent`'s subject is a
  permanent or a spell on the stack, never a card targeted in a graveyard (nobody controls it); MACO, "a creature you
  control" alone, takes the new `Is.Spell` (`& !Is.Spell`). **Ursine Monstrosity**'s "attacks that player" is a stamp
  until end of turn, `Combat.AttacksThePlayer`, read as goad's (`Combat.MustAttackThePlayer`; an effect, so losing all
  abilities doesn't end it) — `MustAttack`, and `DeclareAttackerChoice` offers it no planeswalker. **Leatherhead**: a
  hexproof keyword counter (`CounterKinds.Hexproof`); "remove a counter from her" is `RemoveCounters { AnyKind }` —
  Dathon's choose-a-kind (`PutCounters.OfAKindOn`) removing; her "may" is asked only with such a counter on her now.
  **Questing Beast**: `CombatDamageCantBePrevented(filter)`, read by `Damage.Prevented(…, combat)` alone (protection's
  and The Needs of the Many's prevention; no option on either says damage ISN'T prevented). **Worldspine Wurm**:
  `MoveCard` raises the zone-change event for a card that reads its own arrival in a graveyard
  (`ReadsItsArrivalInAGraveyard`, asked of the card as it arrives — a face-down Wurm is the Wurm there), from any zone;
  Emrakul, the Aeons Torn takes it with colourless. **Once Upon a Time**: no option needed — `FirstSpellYouCastThisGame`
  asked of a card not yet on the stack reads the player's count. **Green Sun's Zenith**: tested pieces together
  (`Is.ManaValueAtMost(Count.X)` in a search's filter; `MoveTo(Ref.Source, Library) { From = [Stack], Shuffle }`).
  **Hexdrinker**: not a static `Grants` — `Protection { If }`, a level's box as a static's `If`; level up is scripted
  (`CounterKinds.Level`, `LsvCards.HasLevel(n)`), no `LevelUp` helper for one card; its P/T by level are +N/+N statics
  (a ceiling). New token data: the 5/5 trample Wurm. Ceilings in DesignNotes (Hexdrinker's base P/T; Ursine's "this
  combat" and random opponent; a finality counter is no counter Leatherhead can remove). The review found five: a
  face-down Wurm (fixed: the arriving card is asked), Ursine losing its abilities (fixed: the stamp), Leatherhead's
  "when you do" with nothing removed (fixed: the condition), MACO still matching a creature spell (fixed: `Is.Spell`),
  a doc comment on the wrong method. 19 tests in `LsvBatch2k2bTests.cs` (`TheyCast(instant, targets)`,
  `TheirPlaneswalker(loyalty)`); 22/22 revert-checks caught. **Golden-out moved ONE game, intended**: Necromancy 02,
  where MACO Strike Team used to trigger off a creature card targeted in its controller's graveyard (162 random, 933
  other card games unmoved); golden-in moved 30 random and 507 card games, 32 added, no AI game; probe 150: none
  unfinished, all eight used (Hexdrinker activated 702, Green Sun's Zenith cast 232; Worldspine Wurm cast 2, its
  shuffle on the stack 23). Suite 2,224 passed.
- 2026-11-21, Claude (cube lane) — **4.4k3: green's four new pieces — 364 of 528 play; green is finished (50 of 50).**
  Read with the nearest piece's code open, three of the four were OPTIONS; only day/night is a new piece.
  **Crew** (Esika's Chariot) is `LsvCards.Crew(n)`: saddle's cost at instant speed, and `ModifyUntilEndOfTurn.AlsoType`
  without `ForGood` — the type stamp marked `Animated.EndsAtCleanup`, put back by the cleanup step (it threw before:
  "ForGood only"); the printed 4/4 is read while it is a creature. Smuggler's Copter and Reckoner Bankbuster can take
  it with colourless. **Earthbend** (Badgermole Cub) is `LsvCards.Earthbend(n)`, composed: Does Machines' stamp for good
  (0/0, haste), the counter, and "when it dies or is exiled, return it to the battlefield tapped" two gained leave
  triggers (Serra Paragon's shape) — the plan's risk held no surprise: a gained trigger is already read from the card
  as it last was. "Whenever you tap a creature for mana, add an additional {G}" is `ExtraMana(filter, colour)`,
  **`ExtraRedFromMountains` generalised and Molten Tide moved onto it** (in a `ThisTurn`; the Cub's on the permanent).
  **Bestow** (Springheart Nantuko) is `Scripting.Bestow(cost)`: an `Enchant` option on the card's own alternative cost
  (overload's mirror — the cast GAINS a target word, one cast per creature, beside the plain cast), the permanent
  entering attached before its types are read, and Lion Sash's "not a creature while attached" (`BecomesType` on itself,
  with subtype Aura); its host gone it falls off as a reconfigured Equipment does and is a creature again; its target
  illegal as it resolves, it resolves a creature (never countered). The landfall is `If(attached to a creature you
  control, [MayPay → CreateTokenCopy(the host)]) { Otherwise = the Insect }`. **Day/night** (Outland Liberator //
  Frenzied Trapbreaker) is the new piece, `Rules/DayNight.cs`: `MagicGame.Daytime` (the game's, where the monarch is a
  player's object), day as the first daybound permanent enters, judged as each turn ends by the spells its active
  player cast (none by day: night; two or more by night: day), every daybound permanent transformed to match, one
  entering at night transformed — read where something changes, never by the state-based check. New token data: a 2/2
  green Cat, a 1/1 green Insect. Ceilings in DesignNotes (crew: one type stamp a permanent, the AI crews by search
  alone and `Evaluator.NextTurn` — the AI lane's — still counts a Vehicle crewed this turn as a creature next turn;
  earthbend's return is the land's controller's; a bestowed spell still reads as a creature spell on the stack;
  day/night's three; the extra mana's restriction and convoke corners). The review found eight; fixed with a test
  each: the additional mana keeps its own colour beside an any-colour creature (Birds of Paradise: `SameColor` binds
  only units with a choice — the solver, the float and tapping by hand), a creature tapped by a cost-picking mana
  ability gets it too (Orcish Lumberjack: `ManaAbilities.ExtraFor`, the one answer), the cleanup undoes a crew stamp
  before a copy until end of turn (`Card.Fresh`'s order), one battlefield scan for grants and extras, one read in
  `Describe` ("(bestow)"); the rest are the ceilings above. 25 tests in `LsvBatch2k3Tests.cs` (`NextTurn()`,
  `LandEnters(answer)`, `CastBestowed(host)`, `Earthbend()`); 44/44 revert-checks caught. Golden-out moved nothing (162
  random, 942 card games — Molten Tide's move and the entry reorder changed no recorded game); golden-in moved 30 random
  and 256 card games, 16 added, no AI game; probe 150: none unfinished, all four used (Esika's Chariot activated 2,517,
  Outland Liberator 316; Badgermole Cub and Springheart Nantuko cast 312 and 379 — their "activated 0" is right, neither
  has an activated ability). Suite 2,249 passed.
- 2026-11-21, Shayne: **colourless is next** (4.4l) — 105 left of 163, the lands among them; multicolour (59) after.
- 2026-11-21, Claude (cube lane) — **4.4l: colourless read card by card — 105 left of 163: 54 lands and 51 others
  (five the coverage counts colourless sit among the multicolour names: Aether Spellbomb, Pyrite Spellbomb, Haywire
  Mite, Sowing Mycospawn — devoid — and Relic of Sauron); 63 tested pieces, 33 options, 9 cards on 5 new pieces.**
  MtgCore has none of the five (no computed colour, entering cost, skipped turn, pregame action, exert). Parts:
  **l1** the 40 tested LANDS, by family — the 10 surveil lands (Meticulous Archive, Undercity Sewers, Raucous Theater,
  Commercial District, Lush Portico, Shadowy Backstreet, Thundering Falls, Underground Mortuary, Elegant Parlor, Hedge
  Maze: their basic land types give the mana, as Tundra's; `EntersTappedComponent`, `SelfEnters` → `Surveil(1)`), the
  10 Triomes (Spara's Headquarters, Raffine's Tower, Xander's Lounge, Ziatora's Proving Ground, Jetmir's Garden, Savai,
  Ketria, Indatha, Raugrin, Zagoth: `EntersTappedComponent`, `Cycling("{3}")`), the 10 fast lands (Seachrome Coast,
  Darkslick Shores, Blackcleave Cliffs, Copperline Gorge, Razorverge Thicket, Concealed Courtyard, Spirebluff Canal,
  Blooming Marsh, Inspiring Vantage, Botanical Sanctum: `EntersTappedComponent(Unless)` with `Not(AtLeast(other lands
  you control, 3))` — `YouControlTwoOrMoreOtherLands`' count), the 5 horizon lands (Horizon Canopy, Fiery Islet,
  Nurturing Peatland, Sunbaked Canyon, Waterlogged Grove: Mana Confluence's `Costs = [TapSelfCost, PayLifeCost(1)]` on
  each colour; `{1}, {T}, SacrificeSelfCost`: draw), the 2 Verges (Blazemire, Bleachbone: the second colour's
  `ManaAbilityComponent.OnlyIf` you control a Swamp or a Mountain — Fanatic of Rhonas' option), Karakas (`{T}`:
  `MoveTo(Hand)` a target legendary creature), Library of Alexandria (`OnlyIf = Both(AtLeast(cards in hand, 7),
  Not(AtLeast(…, 8)))`), Abandoned Air Temple (unless you control a basic land; `PutCounters(Ref.CreaturesYouControl)`);
  **l2** the 23 other tested pieces — Aether Spellbomb, Pyrite Spellbomb (`SacrificeSelfCost` activations), Haywire
  Mite (`SelfDies` gain 2; exile a target noncreature artifact or noncreature enchantment), Expedition Map
  (`SearchLibrary(Land, Hand) { Reveal }`), Soul-Guide Lantern (ETB exile a target card from a graveyard; `Exile` each
  opponent's graveyard), Voltaic Key, Manifold Key (`Untap` another target artifact; `Combat.CantBeBlocked` until end
  of turn), Zuran Orb (`SacrificeCost(Is.Land)` — Baloth Prime's), Chromatic Star (a mana ability with `Cost = {1}` and
  `SacrificeSelfCost`; its own `ChangedZone` battlefield → graveyard draws), Gleemox (`AnyColorMana`; "This card is
  banned" is no rules text), Mox Opal (metalcraft: each colour `OnlyIf = AtLeast(ArtifactsYouControl, 3)`), Retrofitter
  Foundry (Servo, Thopter, 4/4 Construct tokens; `SacrificeCost(Is.Subtype)`), Sensei's Divining Top (Ponder's
  `LookAndTake(3) { Take = 0, Rest = TopInAnyOrder }`; draw, then `MoveTo(Ref.Source, Library) { OnTop }`), Skateboard,
  Skullclamp (`AttachedCreatureDies` draw two), Lightning Greaves (statics haste and shroud; `Equip("{0}")`),
  Smuggler's Copter (`Crew(1)`; `SelfAttacks` and a `Blocks` trigger, `May` draw then discard), Crucible of Worlds
  (Nog's `MayPlayFromZone(Graveyard, Is.Land)`), Golos, Tireless Pilgrim (`SearchLibrary(Land, Battlefield) { Tapped,
  May }`; Urza's `ExileTopPlayable { N = 3, Play = { ThisTurn, Free } }`), Memnarch, the Warden (two Myr; `DrawCards`
  by `ArtifactsYouControl`), Tezzeret, Cruel Captain (`AddLoyalty` as an artifact you control enters; untap, a counter
  `If` it is an artifact creature; `SearchLibrary` an artifact of mana value ≤ 1; the emblem's combat trigger — Does
  Machines' 0/0 Robot, `ForGood`), Portal to Phyrexia (`EachSacrifices(EachOpponent, Creature) { N = 3 }`; upkeep
  `TargetSpec.InAnyGraveyard(Creature)` → `MoveTo(Battlefield) { UnderYourControl, Then }` a Phyrexian too), Cogwork
  Librarian (its text is the DRAFT's, not a game's: an empty script, a 3/3 — a ceiling; see the questions);
  **l3a** options, mana, 10 — Sunken Ruins (a HYBRID `Cost` {U/B} on a mana ability; two mana `Color`/`OrColor` —
  Orcish Lumberjack's), Relic of Sauron ("any combination of {U}, {B}, and/or {R}": `OrColor` holds ONE other colour —
  a list of colours, Orcish Lumberjack moved onto it), Pentad Prism (`Sunburst`; a counter-removal cost,
  `RemoveCountersCost(1, Is.Source) { Kind }`, on a MANA ability with no `{T}`), Gemstone Mine (three mining counters;
  the same cost, and "if there are no mining counters on this land, sacrifice it" — a mana ability's rider: `Then`
  effects, where `DealsDamageToYou` is today's only rider — read whether the Talismans fold onto it), Chrome Mox
  (imprint: `SelfEnters` `May` exile a nonartifact, nonland card from your hand, `Linked`; "one mana of any of the
  exiled card's colors" — a mana ability's colours read from a Ref, `Is.ExiledBySource`), Lion's Eye Diamond ("discard
  your hand" as a COST — `DiscardCost` has a count, not the hand; built as an activated ability adding
  `AddManaOfAnyOneColor(3)`, never a `ManaAbilityComponent`: the payment solver taps mana abilities by itself and must
  never discard a hand — a ceiling, it uses the stack), Wizard's Rockets (the same shape with `{X}`: "X mana in any
  combination of colors" — `AddManaOfAnyOneColor` chooses one colour: a per-unit choice option; enters tapped; its own
  leaving draws), Coalition Relic (`AnyColorMana`; `{T}`: a charge counter; `YourFirstMainPhase` remove them all,
  "one mana of any color for each" — the Rockets' option, counted by the counters removed), Starting Town ("unless
  it's your first, second, or third turn of the game" — no count of a player's turns: a `CountOf` stamped by
  `TurnStructure`; `{T}`, pay 1 life: any colour), Walking Ballista (`{X}{X}`: only Pest Infestation pays X twice, and
  it enters with nothing — `EntersWithCountersComponent(Count.X)` read from an XX cost; `RemoveCountersCost`: 1 damage
  to any target);
  **l3b** options, exile, graveyard and library, 11 — Currency Converter (`APlayerDiscards` by you → `May` exile THAT
  card from your graveyard, `Linked`; `{T}`: `MoveChosen(Exile, Is.ExiledBySource, Graveyard) { Then }` a Treasure if
  `Ref.Chosen` is a land, else a 2/2 black Rogue), Ghost Vacuum (Keen-Eyed Curator's linked exile; `{6}, {T}`,
  sacrifice: every creature card "exiled with this" onto the battlefield under your control with a flying counter, a
  1/1 Spirit too — the links read from the source AS IT WAS, its cost having sacrificed it: the risk), Memory Jar (each
  player's hand exiled face down, `Linked`, seven drawn; `AtNextEndStep` each discards and takes theirs back — the
  same read of a sacrificed source, by a delayed trigger), Karn, Scion of Urza (+1: the top two revealed, "an opponent
  chooses one" — Extrapolate's `DeciderId`; the other exiled "with a silver counter" — `MoveTo.CounterKind` to EXILE,
  a counter on a card there; −1 `MoveChosen(Exile, …, Hand)` by that counter; −2 a Construct with `StaticEffectComponent`
  +1/+1 per artifact), Ugin, Eye of the Storms (`SelfCast` and `YouCastSpell { Subject = Is.Colorless }` → exile up to
  one target `!Is.Colorless` permanent; 0: `AddMana(Colorless, 3)`; −11 `SearchLibrary { UpTo, Filter, To = Exile,
  Play = { ThisTurn, Free } }` — Thada Adel's `Play` with any number and free), Shelldock Isle (hideaway 4:
  `LookAndTake(4) { To = Exile, FaceDown, Rest = BottomAtRandom }` — `Linked` is not one of its options; `{U}, {T}`:
  `MakePlayable` the exiled card free, `OnlyIf` a library has twenty or fewer cards), Grindstone (`Mill(target, 2)`,
  "if two cards that share a color were milled this way, repeat this process" — `Mill` has `IfMilled` only: a repeat
  option reading the two; it stops when fewer than two are milled — with Painter's Servant, the whole library),
  Palantír of Orthanc (`YourEndStep`: an influence counter, scry 2 — `Surveil(2) { To = Library }`; target opponent
  `May { Who }` have you draw; `Otherwise` you mill X — `Mill.N` is an int, a `Count` — and "loses life equal to the
  total mana value of those cards", a `CountOf` over the milled), Mishra's Bauble and Urza's Bauble ("look at the top
  card of target player's library" / "a card at random in target player's hand" — `LookAtHand` makes a whole hand
  known: one card of it, or a library's top; "draw a card at the beginning of the next turn's upkeep" —
  `AtNextEndStep` has `NextMainPhase`: its step an option, not a second flag), Shifting Woodland (unless you control a
  Forest; delirium `OnlyIf = AtLeast(Count.CardTypesAmong(Ref.CardsInYourGraveyard), 4)`; Hall of Echoes'
  `BecomeCopyUntilEndOfTurn(Ref.Source, Ref.Target())` of a permanent CARD in your graveyard — today a creature on the
  battlefield);
  **l3c** options, triggers, combat, equipment and casting, 12 — Sowing Mycospawn (`KickerComponent` {1}{C}; two
  `SelfCast` triggers, the second `If = WasKicked` with a target — a cast trigger reading the kick of its own spell),
  Emrakul, the Aeons Torn (`CantBeCountered`; `SelfCast` → `TakeExtraTurn`; `Protection(Is.Spell & !Is.Colorless)`;
  annihilator 6 a scripted `SelfAttacks` → `EachSacrifices(EachOpponent, any permanent) { N = 6 }` — two players, the
  defending player is the opponent; Worldspine Wurm's graveyard arrival → the owner's whole graveyard `MoveTo(Library)
  { Shuffle }`), Candelabra of Tawnos ("{X}, {T}: Untap X target lands" — Dominion Infiltration's targets picked after
  X, on an ACTIVATED ability), Reckoner Bankbuster (three charge counters; `{2}, {T}`, remove one: draw, then `If` none
  are left a Treasure and a Pilot; `Crew(3)`; the Pilot "crews Vehicles as though its power were 2 greater" — an
  option where `TapUntappedCost.TotalPower` sums), Sword of the Meek (a trigger `From = Graveyard` on a creature you
  control entering with power AND toughness 1; `May` `MoveTo(Battlefield) { Then = [Attach(Ref.ThisWay,
  Ref.EventCard)] }`), The Fantasticar (a Vehicle with no crew: `YouCastNoncreatureSpell` → `May` crew's `AlsoType`;
  "your FOURTH noncreature spell each turn" — `EventTest.FirstNoncreatureSpell` reads `NoncreatureSpellNumber: 1`:
  the number an option, the first moved onto it; `May` sacrifice → four 4/4 Constructs with flying and haste), The One
  Ring (`SelfEnters { If = SourceWasCast }` "you gain protection from everything until your next turn" — The Needs of
  the Many's `YouHaveHexproof` and `PreventAllDamageToYou` are this-turn effects: `UntilTurnOf` their option; burden
  counters; upkeep `LoseLife` by them; `{T}`: one more, then draw that many), Coveted Jewel (draw three; three mana of
  any one colour; "whenever one or more creatures an opponent controls attack you and aren't blocked" — no event says
  an attacker went unblocked: one raised as blockers are declared (`IsBlocked` false), `OneOrMore`; that player draws
  three, `GainControl(Ref.Source, …)`, `Untap`), Kaldra Compleat (living weapon: Cori-Steel Cutter's `CreateTokens(Germ)
  { Then = Attach }`; statics on `Is.AttachedToSource`; "whenever this creature deals combat damage to a creature,
  exile that creature" written on the Equipment — a `CreatureDealtDamage` trigger by the attached creature, in combat),
  Boseiju, Who Endures and Otawara, Soaring City (channel: an activated ability `From = Hand` with `DiscardSelfCost`,
  cycling's shape; "costs {1} less to activate for each legendary creature you control" — `CostChange { Abilities }`
  is Anointed Peacekeeper's tax on OTHER sources: an ability's own reduction; Boseiju's `ItsControllerMayFetch` with
  "a land card with a basic land type", untapped), Urza's Saga (a `SagaComponent(3)` on a LAND — it enters by a land
  drop, never cast; chapters I and II "this Saga GAINS" an ability — `ModifyUntilEndOfTurn { Gains, ForGood }`; III
  `SearchLibrary(Artifact, Battlefield)` "with mana COST {0} or {1}" — not mana value: a filter on the printed cost);
  **l4** new pieces, each with its nearest: **computed colours** (Painter's Servant; Celestial Colonnade, Creeping Tar
  Pit, Restless Vinestalk) — colour is read as printed (`Card.Colors`, 18 reads outside the sets and the AI: `Filter`,
  `Refs`, `ManaPayment` …); the nearest is `Rules/Characteristics` (THE power, toughness and keywords: own + every
  static) — it has no colour, so no option fits: a `ColorsOf` beside them that every read moves onto. Then Painter is
  `ChoosesColorAsItEnters` (Room of Refuge's) and a static over every zone, and a creature land's "becomes a 4/4 white
  and blue Elemental creature … still a land" is crew's `ModifyUntilEndOfTurn { AlsoType = Creature }` with `SetBase`,
  `AlsoCreatureType` and a `Colors` option (Tar Pit `Combat.CantBeBlocked`; Vinestalk's `SelfAttacks` `SetBase (3, 3)`
  on up to one other target creature — Perfected Theory's); **an entering's cost** (Mox Diamond; Multiversal Passage)
  — nearest `EntersTappedComponent.UnlessYouPayLife`, the shocklands' "as this enters, you may pay 2 life; if you
  don't, it enters tapped": an int of life with one consequence. Generalised to a cost from the shared list
  (`PayLifeCost(2)`; `DiscardCost(Is.Land)`) and what a "no" does (tapped; Mox Diamond's "put it into its owner's
  graveyard" INSTEAD of entering — no enter or leave event), the shocklands moved onto it. Multiversal Passage also
  "choose a basic land type … this land is the chosen type": a fifth "as it enters, choose" beside colour, name,
  number and player — read whether the four fold into one when built; the type stamped as a subtype, which gives the
  mana; **skipping a turn** (Time Vault) — nearest `SkipYourDrawStep` (a static: a STEP never begins) and
  `TurnStructure.FinishCleanup` (whose turn is next, the extra-turn queue): "if you would begin your turn while this
  artifact is tapped, you may skip that turn instead. If you do, untap this artifact" is a CHOICE as a turn would
  begin — no option on a static fits; the rest is tested (`EntersTappedComponent`, `DoesntUntapItself`, `{T}`:
  `TakeExtraTurn`); **beginning the game on the battlefield** (Gemstone Caverns) — nearest the mulligan
  (`Actions/MulliganActions`), the only pregame choice; nothing reads an opening hand after it (no Leyline plays):
  once hands are kept, the player who isn't starting may put it onto the battlefield with a luck counter and exile a
  card from their hand; its mana two abilities under `OnlyIf` and its `Not` (Undermountain Adventurer's shape);
  **exert, and mana that grants** (Arena of Glory) — exert "won't untap during your next untap step": nearest a stun
  counter (`MagicState.Untap` removes it instead) — any untap spends a stun counter, an exerted permanent still untaps
  by an effect — and `DoesntUntap`, a static with no end; "if any of that mana is spent on a creature spell, it gains
  haste until end of turn": nearest `ManaRestriction` (the pool's mana tagged with what it may pay) — that limits a
  payment, this gives the spell something: a rider on the same tag, read as the spell is paid for.
  **Legal actions, counted on a 12-creature board:** nothing grows by combinations — Candelabra's lands, crew's
  creatures and annihilator's six are picked after the action; Wizard's Rockets' and Coalition Relic's colours are one
  choice per unit (never one option per combination: 5^X); Relic of Sauron's solver sees 6 outputs, Sunken Ruins' 3.
  One action each per: graveyard card (Ghost Vacuum, Soul-Guide Lantern, Portal, Shifting Woodland — 30 late), land
  (Zuran Orb), legendary creature (Karakas), target (Walking Ballista: ~26). **Loops the probe must show end:**
  Lightning Greaves' `Equip {0}` (free, without limit at sorcery speed — the AI's search within a turn), Time Vault
  with Voltaic Key, Manifold Key or Tezzeret (a turn after every turn), Painter's Servant with Grindstone (ends with
  the library). Each probed at 10 games before 150.
  **Batches (Shayne: more cards a session):** **l1** (40 lands, script-only, read on the card sheet), **l2** (23,
  script-only), **l3a** (10, mana), **l3b** (11, exile, graveyard, library), **l3c** (12, triggers, combat, casting),
  **l4a** (6: computed colours, an entering's cost), **l4b** (3: Time Vault, Gemstone Caverns, Arena of Glory).
  **Questions for Shayne's review:** (1) Cogwork Librarian — an empty script (a 3/3 in games, the draft ability a
  ceiling in DesignNotes) is planned; the other reading is `Limited/Draft.cs` learning "draft an additional card".
  (2) Lion's Eye Diamond and Wizard's Rockets as abilities that use the stack (above) — the ceiling is that they can
  be responded to and can't pay mid-cast, which LED's "activate only as an instant" forbids anyway.
- 2026-11-21, Shayne (reviewing 4.4l's plan, its two questions): **Cogwork Librarian is a 3/3 with no script** — it
  "only applies in the draft, it's kind of an edge case". **Lion's Eye Diamond and Wizard's Rockets as abilities that
  use the stack are "fine for now, but please note the limitation"** — so l3a writes it down where ceilings live
  (DesignNotes, "MagicCore known ceilings", and a `ponytail:` on each script), in these words' substance: both are
  mana abilities in the rules (CR 605) and are built as ordinary activated abilities, because the payment solver taps
  mana abilities by itself and must never discard a hand or choose an X. What that costs: (1) the ability goes on the
  stack, so an opponent gets priority and may respond — and Sublime Epiphany's "counter target activated or triggered
  ability" can counter it, which the rules forbid; (2) casting never uses them — the mana must be made BEFORE the
  spell is cast and float, so a spell payable only through them isn't offered until they have been activated, and
  Wizard's Rockets can't fix a colour in the middle of a payment (LED's "activate only as an instant" forbids that for
  LED anyway); (3) the AI has to choose the activation as its own move. Upgrade path: a mana ability the solver may
  not choose by itself — offered as an action and while paying, never auto-tapped.
- 2026-11-21, Shayne (the same review): **more cards a session** — 4.4l's seven parts are built as FOUR batches:
  **l1 + l2** (63: every tested piece, lands and others, script-only, read on the card sheet), **l3a + l3b** (21
  options: mana; exile, graveyard and library), **l3c** (12 options: triggers, combat, equipment, casting), **l4a +
  l4b** (9 cards on the 5 new pieces). The parts keep their names and lists. Three batches (every option, 33, in one
  session) is his to ask for: it is twice the most options a session has built (h2's and k2's were 5–8 each), and
  each option carries a test and a revert-check, so that session's context is the one the usage log would show.
- 2026-11-21, Claude (cube lane) — **4.4l1 + l2: 62 of colourless' 63 tested pieces — 426 of 528 play (colourless
  120 of 163); the review took one out and found one broken, fixed here.** Shayne took the plan by starting the
  batch. One engine option, Chromatic Star's (the entry after Shayne's note). The 37
  family lands are five helpers (`SurveilLand`, `Triome`, `FastLand`, `HorizonLand`, `Verge`), with
  `YouControlA(filter)` and `SacrificeThis(cost, tap:, targets, effects)` (the Spellbombs, Expedition Map, Soul-Guide
  Lantern, the horizon lands' draw); Retrofitter Foundry's Thopter is FRA's token, referenced, not copied. **The
  lands got tests although tier 1** (`LsvBatch2lTests`, 41 cases, a family each against its printed text — fast lands
  at two and three other lands and their two colours, a surveil land's entering, a Triome's cycling, a horizon land's
  life and sacrifice, a Verge's second colour, Library of Alexandria at six, seven and eight cards, Abandoned Air
  Temple): a land is played, never cast, so the probe says nothing of ten fast lands and two Verges. Cogwork
  Librarian is `[]` (DesignNotes' ceilings).
  **Both found by /pre-commit's review, neither by the card sheet nor the probe:** **Chromatic Star, broken as
  scripted — fixed in this session (below).**
  "{1}, {T}, Sacrifice this artifact: Add one mana of any color": a mana ability with a mana `Cost` AND
  `SacrificeSelfCost` is not `IsPlain`, so only `ManaPayment.WithCostlyMana` uses it — which never sacrifices — and
  no move activates it by hand: it tapped as a reusable colour filter and never drew. The plan called it tested (its
  tier came from the catalog); it is an option. **Lightning Greaves → l5, waiting on the AI.** Its script is right
  (statics `Haste` and `Shroud` on `Is.AttachedToSource`, `Equip("{0}")`) but the search AI re-equips it without end
  — watched, on a board of Greaves, a 2/2 and a 4/4: "Activate Lightning Greaves → Bear | Pass | Pass | Activate
  Lightning Greaves → Ogre | Pass | Pass | …" for 30 decisions, the turn never ending. Free, at sorcery speed, and
  each equip a new position, so nothing in the beam ends it; random play only showed it as 4 of 150 probe games
  unfinished. **Zuran Orb is in, and the AI overuses it:** nine lands sacrificed in a row on its first turn
  ("Activate Zuran Orb, sacrificing Plains" …, still going at 30 decisions, on the test board's twenty basics) — it
  ends, bounded by the lands, but 2 life is priced above a land there. Smuggler's Copter, Retrofitter Foundry,
  Sensei's Divining Top and Karakas, watched the same way, take sensible turns that end. The recipe: on
  `FraBoardTest`'s board `Field` the card and two creatures, then `s = s.AutoPass(); s = new
  BeamSearchAi(s.DecidingPlayerId()).Decide(s).State` until the turn number changes, collecting each `What`.
  **Review's other fixes:** `CardRegistry.NeedsNoScript(CardData)` extracted from `IsImplemented` (no behaviour
  change), so `LsvBatch1Tests`' reminder-text rule is asked of the printed data — a Triome was its unscripted example
  until this batch scripted it; `Scripting.OtherLandsYouControl` (the slow and the fast lands' one count); Snuff Out
  onto `YouControlA`; `SacrificeToDestroy` through `SacrificeThis`; `CanTapFor` moved to `FraBoardTest`.
  **Checks (the final 62, after the Star's fix):** golden-out unmoved (162 + 946) — the engine change moves no
  recorded game; golden-in regenerated, 62 card replays added (1,008), the AI replay unmoved; probe, the 21 non-lands:
  150 games, none unfinished, every card cast or used, and the Star alone: 150 games, none unfinished, cast 575
  times, its draw on the stack 58 (the first run, all 63 with Greaves in: 4 unfinished at the 20,000-step cap).
  `batch_checks.py`'s probe150 with every card named timed out at its 30 minutes — three of each is a 208-card deck:
  a batch this size names its non-lands to the probe, its lands being tested. Revert-checks 6 of 6 caught. Full suite
  2,295 passed, 1 skipped; docs current.
- 2026-11-21, Shayne (on those two findings): **"We might need an AI note to stop it from using Zuran Orb so
  liberally. Same with Lightning Greaves — free abilities in general, I guess."** So the note, FOR THE AI LANE (its
  code, its measurements — nothing of the search or the evaluator is changed in the cube lane): **an activated
  ability that costs no mana is not free to the search.** Two cases, both watched above: (1) one that can be taken
  without end (Greaves' `Equip {0}`: every activation a new position) — the search needs a rule that ends it, e.g. no
  move back to a position this turn has already had, or a cap on activations of one ability a turn; until it has one
  Lightning Greaves stays unscripted (l5). (2) one that spends a RESOURCE the evaluator underprices (Zuran Orb: a
  land for 2 life, nine times) — a land sacrificed should cost what the land count and `LandUnlock` say it is worth,
  and more while cards in hand still need it. The same look is owed to the next ones the cube adds: Lion's Eye
  Diamond (discard your hand), Mox Diamond and Chrome Mox (a card from hand), Gemstone Mine, Walking Ballista (its
  counters), Karn's and Tezzeret's 0s. Claude's reading of "note": written here and in Next for the merge — not a
  change to the AI made in this lane; say if a guard should be built here instead.
- 2026-11-21, Shayne (asked why Chromatic Star wasn't working, and how to fix it): **"Yes please fix it in this
  session so we can move on."** Claude (cube lane) — **Chromatic Star is back in l2, with one engine option: a mana
  ability that costs mana AND sacrifices itself, `ManaAbilityComponent.CashedIn`.** The nearest was Starfleet
  Academy's costly mana (`ManaPayment.WithCostlyMana`: auto-payment only, its source STAYS) — the Star's is that with
  `SacrificeSelfCost`, so no new piece: (1) `WithCostlyMana` marks its source sacrificed when the ability says so,
  and `Pay` sacrifices it as a Treasure — its "put into a graveyard from the battlefield, draw a card" then fires;
  (2) cashing it in is a decision, so it is also a MOVE, one per colour — `TapForManaAction.PaysItsCost`, beside the
  two mana moves there were (a cost that picks a card, no {T}): its {1} paid by the other permanents and the pool
  (`ManaPayment.Find` without it), then its {T} and sacrifice, the mana floating; (3) `RaiseActivated` sees it (it
  has no plain tap, so `CanTap` hid its activation from Kai Winn), and "any color" is never {C} on a mana move.
  Tests in `LsvBatch2lTests` (cashed in by hand: {1} paid, sacrificed, the colour, the draw; paying for a spell;
  nothing to pay its {1}: no move; never colourless; Kai Winn's 3), each line revert-checked. **The AI's turn with
  it, watched:** "Play Forest | Pay for and sacrifice Chromatic Star for White | Cast Graft Surgeon | Attack with
  Bear" — it ends; the cash-in was for the card, its mana unused. Ceilings in DesignNotes ("Chromatic Star"): one
  costly source a payment plan (two Stars never pay one spell together unasked); five moves a Star, each solving its
  {1}; a saved record in which a mana MOVE was offered reads stale (the new field is in the move's key — the kept
  records and the position suite are unaffected). A second /pre-commit review of the engine change found the
  activation event, the colourless hole and two stale doc lines (`MagicCore/CLAUDE.md`, the action's summary) — all
  fixed. **For l3a:** Lion's Eye Diamond and Wizard's Rockets were planned as abilities that use the stack because
  the solver must never discard a hand or choose an X by itself — this move is the other way to that (a mana ability
  the solver may not choose, offered as a move); weigh it there before taking the stack.

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
    - [x] MC-4.4g4 Face-down, manifest dread: Abhorrent Oculus — done 2026-11-18: `Card.TurnedFaceDown` /
          `TurnedFaceUp`, `FaceDown(Original)`, `EnterBattlefield(faceDown:)`, `LookAndTake.FaceDown`,
          `TurnFaceUpAction`; 7 tests, 12 breaks caught (the review's three fixes included); `batch_checks.py` all 8
          steps (golden-out unchanged; golden-in moved 29 random and 58 card-replay games, added 4, the AI replay
          unmoved; probe 150 games, none unfinished, the Oculus cast 320 times; full suite 1,957 passed) — all run again
          after the review's fixes (12/12 caught; probe 150 games, 1 unfinished past 300 permanents, the Oculus cast 91
          times; full suite 1,959 passed)
    - [x] MC-4.4g5 Adventures: Brazen Borrower // Petty Theft — done 2026-11-18: `AdventureComponent`,
          `CastSpellAction.Adventure`, `Card.AsAdventure` / `CastAsAdventure(Original)`, `FinishSpellAction.Resolved`,
          `CanBlockOnly`; 3 tests, 7 breaks caught (the review's one fix included); `batch_checks.py` all 7 steps
          (golden-out unchanged; golden-in moved 29 random and 69 card-replay games, added 4, the AI replay unmoved;
          probe 150 games, none unfinished, the Borrower cast 970 times; full suite 1,962 passed — run again after the
          review's fixes)
    - [ ] MC-4.4g5b (LATER — out of scope for now; Shayne, 2026-11-18) The AI's heuristics read an Adventure cast as
          the creature in hand (`BeamSearchAi.InstantSpeed`, `FaceBurn`, the BlunderLint checks): read the card as
          its Adventure for a `CastSpellAction.Adventure` (DesignNotes, MagicCore known ceilings)
    - [ ] MC-4.4g5c (LATER; Shayne, 2026-11-18) The table draws Petty Theft on the stack with no art — it looks art up
          by the face's name. One line in `SQGodotCommon/MagicTable/MagicArt.ImageName`: a `CastAsAdventure` card shows
          its `Original`'s art, as a prepared copy shows its creature's. The table is the AI lane's file: do it there,
          or here with a note at the next merge
    - [x] MC-4.4g6 Teamwork: We Say Thee Nay! — done 2026-11-18: `LsvCards.Teamwork` (kicker), `TapUntappedCost.
          TotalPower`, `TapChosenForPowerAction`, `KickerComponent.Word`; 4 tests, 8 breaks caught; `batch_checks.py`
          all 8 steps (golden-out unchanged; golden-in moved 29 random and 63 card-replay games, added 4, the AI replay
          unmoved; probe 150 games, 1 unfinished past 300 permanents, the Nay cast 447 times; full suite 1,966 passed)
    - [x] MC-4.4g7 Storm: Brain Freeze — done 2026-11-18: `Scripting.Storm`, `CopySpell { N }`, `Count.SpellsCastBefore`
          (`OnStackComponent.SpellsCastBefore`), a `SelfCast` trigger's snapshot; 2 tests, 4 breaks caught;
          `batch_checks.py` all 8 steps (golden-out unchanged; golden-in moved 29 random and 48 card-replay games, added
          4, the AI replay unmoved; probe 150 games, none unfinished, Brain Freeze cast 506 times; full suite 1,968 passed)
    - [x] MC-4.4g8 Spree: Three Steps Ahead — done 2026-11-18: `Mode.Cost` (from the printed "+ {cost} —"), spree's
          casts word by word (`EachWord`); 6 tests, 7 breaks caught; `batch_checks.py` all 7 steps (golden-out
          unchanged; golden-in moved 29 random and 78 card-replay games, added 4, the AI replay unmoved; probe 150
          games, none unfinished, Three Steps Ahead cast 543 times; full suite 1,971 passed)
    - [x] MC-4.4g9 Warp: Quantum Riddler — done 2026-11-18: `Scripting.Warp` (`MayCastFromHandFree { From,
          PaidAtNextEndStep }`, `PlayPermission.LaterTurn`); 5 tests, 8 breaks caught; `batch_checks.py` all 8 steps
          (golden-out unchanged; golden-in moved 29 random and 68 card-replay games, added 4, the AI replay unmoved;
          probe 150 games, 1 unfinished past 300 permanents, Quantum Riddler cast 802 times; full suite 1,979 passed)
    - [x] MC-4.4g10 Classes: Does Machines — done 2026-11-18: `ClassLevel`, `GainLevel`, `BecameLevelEvent`
          (`Trigger.Chapter` on it), `Count.ClassLevelOf`, `Scripting.Level` / `WhenLevel` / `AtLevel`,
          `ModifyUntilEndOfTurn.AlsoType`; 5 tests, 9 breaks caught; `batch_checks.py` all 8 steps (golden-out unchanged;
          golden-in moved 29 random and 66 card-replay games, added 4, the AI replay unmoved; probe 150 games, 1 unfinished
          past 300 permanents, Does Machines cast 482 times, never leveled — random play, the ability offered (tests);
          full suite 1,984 passed, run again after `EventKind.BecameLevel` joined the trigger-coverage table)
    - [ ] MC-4.4g10b (LATER; Shayne, 2026-11-18) Does Machines never levels in random play (probe: cast 482, activated
          0) — likely because its levels pay off only with artifacts (in the graveyard for level 2, on the battlefield for
          level 3), which the random decks seldom hold. An isolated test: a board with artifacts and the mana free — is
          the level ability taken, by the probe's random play and by the AI?
  - [x] MC-4.4h Black (Shayne, 2026-11-18: the next colour in the list; done 2026-11-20) — 48 cards read card by card and tiered
        (Decisions, "4.4h"): 19 tested pieces, 22 options, 7 new pieces; one part per session:
    - [x] MC-4.4h1 The tested pieces, 17 of the 19 (list in Decisions; Duress and Wishclaw Talisman moved to h2a, h2c)
    - [x] MC-4.4h2a Options, hands, 5: Inquisition of Kozilek, Duress, Deep-Cavern Bat, Collective Brutality, Chain of
          Smog (Decisions, "4.4h2a")
    - [x] MC-4.4h2b Options, graveyard returns, 5: Shallow Grave, Corpse Dance, Exhume, Emperor of Bones, Barrowgoyf
          (Decisions, "4.4h2b")
    - [x] MC-4.4h2c Options, costs and permissions, 5: Toxic Deluge, Bolas's Citadel, Rain of Filth, Yawgmoth's Will,
          Wishclaw Talisman (Decisions, "4.4h2c")
    - [x] MC-4.4h2d Options, triggers, 4: Sedgemoor Witch, Orcish Bowmasters, Gollum (Riddle Master), Black Widow
          (Decisions, "4.4h2d")
    - [x] MC-4.4h2e Options, zones and combat, 4: Fatal Push, Troll of Khazad-dûm, Dauthi Voidwalker, From the Catacombs
          (Decisions, "4.4h2e")
    - [x] MC-4.4h3 Overload: Damn (Winds of Abandon waits for white; Decisions, "4.4h3")
    - [x] MC-4.4h4 Splice onto Arcane: Goryo's Vengeance (Through the Breach waits for red; Decisions, "4.4h4")
    - [x] MC-4.4h5 Auras that return a creature from a graveyard: Animate Dead, Necromancy (Decisions, "4.4h5")
    - [x] MC-4.4h6 Naming a card as a spell resolves: Demonic Consultation (Decisions, "4.4h6")
    - [x] MC-4.4h7 Saddle: Caustic Bronco (one batch with h8, h9; Decisions, "4.4h7–h9")
    - [x] MC-4.4h8 Emerge: Crabomination
    - [x] MC-4.4h9 Miracle: Metamorphosis Fanatic
  - [x] MC-4.4i Red beyond tier 1 (Shayne, 2026-11-20: the next colour) — 17 cards read card by card and tiered
        (Decisions, "4.4i"): 6 tested pieces, 7 options, 4 new pieces; four batches — i1 + i2a, i2b + i3, i4–i6, i7
        (Shayne: more cards a session):
    - [x] MC-4.4i1 The tested pieces, 6 (one batch with i2a): Embereth Shieldbreaker // Battle Display, Oliphaunt, Unholy Heat, Through the
          Breach, Gut, True Soul Zealot, Headliner Scarlett
    - [x] MC-4.4i2a Options, triggers and combat, 4: Fury, Ivora, Insatiable Heir, Dragon's Rage Channeler, Caves of
          Chaos Adventurer
    - [x] MC-4.4i2b Options, casting, 3 (one batch with i3): Batroc the Leaper (multikicker), Ragavan, Nimble Pilferer and Death-Greeter's
          Champion (dash, backup)
    - [x] MC-4.4i3 An additional combat phase: Fear of Missing Out
    - [x] MC-4.4i4 Counters on a player — energy (one batch with i5, i6): Galvanic Discharge (Guide of Souls waits for white)
    - [x] MC-4.4i5 Transforming double-faced cards: Fable of the Mirror-Breaker
    - [x] MC-4.4i6 Etali, Primal Conqueror — transform by an activation, poison, casting any number free
    - [x] MC-4.4i7 (Shayne: the default) The blue and black DFCs every survey missed: Tamiyo, Inquisitive
          Student; Jace, Vryn's Prodigy; Sink into Stupor (modal); Cecil, Dark Knight; Concealing Curtains
  - [x] MC-4.4j White beyond tier 1 (Shayne, 2026-11-20: the next colour, after i7) — 19 cards read card by card and
        tiered (Decisions, "4.4j"): 6 tested pieces, 9 options, 4 new pieces; three batches — j1 + j2a, j2b + j3, j4–j6:
    - [x] MC-4.4j1 The tested pieces, 6 (one batch with j2a): Eagles of the North, Solitude, Witch Enchanter // Witch-Blessed
          Meadow, White Plume Adventurer, Parallax Wave, Get Lost (the Map token)
    - [x] MC-4.4j2a Options, protection and triggers, 5: Mother of Runes, Giver of Runes, Seasoned Dungeoneer, Winds of
          Abandon, Jennifer Walters // The Sensational She-Hulk
    - [x] MC-4.4j2b Options, counters, costs and tokens, 4 (one batch with j3): Guide of Souls, Prismatic Ending (converge),
          Sunfall (incubate), Ajani, Nacatl Pariah // Ajani, Nacatl Avenger
    - [x] MC-4.4j3 The monarch: Palace Jailer
    - [x] MC-4.4j4 Ascend (one batch with j5, j6): Ocelot Pride
    - [x] MC-4.4j5 Reconfigure: Lion Sash
    - [x] MC-4.4j6 Voting: Council's Judgment
  - [ ] MC-4.4k Green beyond tier 1 (Shayne, 2026-11-21: the next colour, after white) — 40 cards read card by card and
        tiered (Decisions, "4.4k"): 21 tested pieces, 15 options, 4 new pieces; four batches — k1, k2a, k2b, k3
        (Shayne took it as written):
    - [x] MC-4.4k1 The tested pieces, 21 (list in Decisions; done 2026-11-21 — Scythecat Cub and Titania took an option)
    - [x] MC-4.4k2a Options, mana, lands and tokens, 7: Fanatic of Rhonas, Undermountain Adventurer, Baloth Prime,
          Fastbond, Sylvan Safekeeper, Vaultborn Tyrant, Woodfall Primus (done 2026-11-21, all seven options as planned)
    - [x] MC-4.4k2b Options, triggers, combat and casting, 8: Surrak, Elusive Hunter, Ursine Monstrosity, Leatherhead,
          Swamp Stalker, Questing Beast, Worldspine Wurm, Once Upon a Time, Green Sun's Zenith, Hexdrinker (done
          2026-11-21; MACO Strike Team's trigger corrected with Surrak's)
    - [x] MC-4.4k3 New pieces, 4: crew (Esika's Chariot), earthbend (Badgermole Cub), bestow (Springheart Nantuko),
          day/night (Outland Liberator // Frenzied Trapbreaker) (done 2026-11-21 — three were options; day/night the
          one new piece; Molten Tide moved onto `ExtraMana`)
  - [ ] MC-4.4l Colourless beyond tier 1 (Shayne, 2026-11-21: after the colours) — 105 cards, 54 of them lands, read
        card by card and tiered (Decisions, "4.4l"): 63 tested pieces, 33 options, 9 cards on 5 new pieces; seven
        parts built as FOUR batches — l1 + l2, l3a + l3b, l3c, l4a + l4b (Shayne, 2026-11-21: more cards a session;
        he reviews the plan before the first):
    - [x] MC-4.4l1 The tested lands, 40: surveil lands, Triomes, fast lands, horizon lands, Verges, Karakas, Library of
          Alexandria, Abandoned Air Temple (done 2026-11-21, one batch with l2; a test per land family)
    - [x] MC-4.4l2 The other tested pieces, 22 of the 23 (list in Decisions) — Cogwork Librarian a 3/3 with no script
          (Shayne: draft-only, an edge case); its draft ability a line in DesignNotes' ceilings (done 2026-11-21;
          the review moved Lightning Greaves to l5 and found Chromatic Star broken — fixed with an engine option, a
          mana ability that costs mana and sacrifices itself)
    - [ ] MC-4.4l3a Options, mana, 10: Sunken Ruins, Relic of Sauron, Pentad Prism, Gemstone Mine, Chrome Mox, Lion's
          Eye Diamond, Wizard's Rockets, Coalition Relic, Starting Town, Walking Ballista — LED and the Rockets use
          the stack (Shayne: fine for now, the limitation NOTED: DesignNotes' ceilings and a `ponytail:` each, the
          words in Decisions)
    - [ ] MC-4.4l3b Options, exile, graveyard and library, 11: Currency Converter, Ghost Vacuum, Memory Jar, Karn,
          Scion of Urza, Ugin, Eye of the Storms, Shelldock Isle, Grindstone, Palantír of Orthanc, Mishra's Bauble,
          Urza's Bauble, Shifting Woodland
    - [ ] MC-4.4l3c Options, triggers, combat, equipment and casting, 12: Sowing Mycospawn, Emrakul, the Aeons Torn,
          Candelabra of Tawnos, Reckoner Bankbuster, Sword of the Meek, The Fantasticar, The One Ring, Coveted Jewel,
          Kaldra Compleat, Boseiju, Who Endures, Otawara, Soaring City, Urza's Saga
    - [ ] MC-4.4l4a New pieces, 6 cards: computed colours (Painter's Servant; Celestial Colonnade, Creeping Tar Pit,
          Restless Vinestalk), an entering's cost (Mox Diamond; Multiversal Passage)
    - [ ] MC-4.4l4b New pieces, 3: skipping a turn (Time Vault), beginning the game on the battlefield (Gemstone
          Caverns), exert and mana that grants (Arena of Glory)
    - [ ] MC-4.4l5 Lightning Greaves — scripted, then taken out (2026-11-21): the search AI re-equips `Equip {0}`
          without end. Waits on the AI lane's rule for abilities that cost no mana (Shayne's note, Decisions); then
          its three-line script and a kept test that the AI's turn ends
- [x] MC-4.5 (done 2026-11-19 with h2b; Shayne, 2026-11-18) Troubling Tribbles out of the random decks — the fuzz probe's extras
      (`CardProbeTests.Deck`) and `RandomGameTests.RandomPlay_EveryScriptedCard` (which already skips seed 21 for it):
      the doubling balloons the board past 300 permanents ("not much additional benefit" — Shayne). Its own tests stay,
      and AI-21's Tribble boards are the AI lane's, untouched. It moves the golden random games: regenerate them after
      the change (Commands.md, "Golden replays").

## Next

Red's tier 1 (4.4c), white's (4.4d), blue's tiers 1 and 2 (4.4e, 4.4f) and blue's tier 3's first five parts —
protection (4.4g1, with g1b's copy fix), delve (4.4g2), dungeons and the initiative (4.4g3), face-down permanents
(4.4g4), Adventures (4.4g5), teamwork (4.4g6), storm (4.4g7), spree (4.4g8), warp (4.4g9) and Classes (4.4g10) — are
done: **blue is finished.** Black is surveyed and planned (Decisions, "4.4h"; Shayne took it as written); h1 (17 tested
pieces), h2a (the five hand cards), h2b (the five graveyard returns), h2c (costs and permissions), h2d (triggers) and
h2e (zones and combat), h3 (overload: Damn), h4 (splice onto Arcane: Goryo's Vengeance), h5 (Auras returning a
creature: Animate Dead, Necromancy), h6 (naming a card as a spell resolves: Demonic Consultation) and **h7–h9 as one
batch (saddle: Caustic Bronco; emerge: Crabomination; miracle: Metamorphosis Fanatic — 283 of 528 play)** are done:
**black is finished** (Decisions, "4.4h1", "4.4h2a" … "4.4h2e", "4.4h3" … "4.4h6", "4.4h7–h9") — but for its two DFCs,
which every colour's survey missed (Decisions, "4.4i"; blue's three too). **Red is next (Shayne, 2026-11-20), planned:
4.4i** (Decisions, "4.4i") — i1 the 6 tested pieces, i2a/i2b the 7 options, then i3–i6 one new piece a session (an
extra combat, energy, transforming DFCs, Etali); i7 the missed blue and black DFCs — built as four batches, i1 + i2a,
i2b + i3, i4–i6, i7 (Shayne: more cards a session). Each session ends with the cube's cards implemented by rarity AND
by colour (`python tools/magic_set.py coverage --set LSV`). **i1 + i2a, i2b + i3 and i4–i6 are done: red is
finished** (Decisions, "4.4i1 + i2a", "4.4i2b + i3", "4.4i4–i6"; 300 of 528 play, red 54 of 54). **i7 is done: blue and
black are finished** (Decisions, "4.4i7"; 305 of 528 play — W 41/60, U 75/75, B 61/61, R 54/54, G 10/50, multicolour
6/65, colourless 58/163). **MC-4.4j is planned (Decisions, "4.4j"; Shayne took it as written): three batches — j1 +
j2a (the 6 tested pieces and 5 protection and trigger options), j2b + j3 (4 counter, cost and token options and the
monarch), j4–j6 (ascend, reconfigure, voting). j1 + j2a is done** (Decisions, "4.4j1 + j2a"; 316 of 528 play — W 52/60,
U 75/75, B 61/61, R 54/54, G 10/50, multicolour 6/65, colourless 58/163). **j2b + j3 is done** (Decisions, "4.4j2b +
j3"; 321 of 528 play — W 57/60, U 75/75, B 61/61, R 54/54, G 10/50, multicolour 6/65, colourless 58/163). **j4–j6 is
done: white is finished** (Decisions, "4.4j4–j6"; 324 of 528 play — W 60/60, U 75/75, B 61/61, R 54/54, G 10/50,
multicolour 6/65, colourless 58/163). **MC-4.4k is planned (Decisions, "4.4k"; Shayne took it as written): four
batches — k1 (the 21 tested pieces), k2a (7 mana, land and token options), k2b (8 trigger, combat and casting
options), k3 (crew, earthbend, bestow, day/night).** **k1 is done** (Decisions, "4.4k1"; 345 of 528 play — W 60/60,
U 75/75, B 61/61, R 54/54, G 31/50, multicolour 6/65, colourless 58/163). **k2a is done** (Decisions, "4.4k2a"; 352 of
528 play — W 60/60, U 75/75, B 61/61, R 54/54, G 38/50, multicolour 6/65, colourless 58/163). **k2b is done**
(Decisions, "4.4k2b"; 360 of 528 play — W 60/60, U 75/75, B 61/61, R 54/54, G 46/50, multicolour 6/65, colourless
58/163). **k3 is done: green is finished** (Decisions, "4.4k3"; 364 of 528 play — W 60/60, U 75/75, B 61/61, R 54/54,
G 50/50, multicolour 6/65, colourless 58/163). **Every colour is done. Colourless is next (Shayne, 2026-11-21):
MC-4.4l** — its 105 unplayed cards, the lands among them; multicolour (59) after — read card by card
(`python tools/magic_set.py text --missing --set LSV`) and tiered as each colour was (each new piece naming its
nearest), written in Decisions as "4.4l", then batches of more cards a session; Shayne reviews the plan before the
first batch. **MC-4.4l is planned (Decisions, "4.4l") and waits for Shayne's review — no card of it is built:** 63
tested pieces, 33 options, 9 cards on 5 new pieces, in seven parts — l1 (the 40 tested lands), l2 (the 23 other
tested pieces), l3a (10 mana options), l3b (11 exile, graveyard and library options), l3c (12 trigger, combat,
equipment and casting options), l4a (computed colours, an entering's cost: 6 cards), l4b (Time Vault, Gemstone
Caverns, Arena of Glory). Its two questions are answered (Shayne, 2026-11-21; Decisions, after "4.4l"): Cogwork
Librarian is a 3/3 with no script, and Lion's Eye Diamond and Wizard's Rockets use the stack for now — l3a must write
that limitation into DesignNotes' ceilings. **Built as four batches (Shayne, 2026-11-21: more cards a session): l1 +
l2 (63, script-only), l3a + l3b (21 options), l3c (12 options), l4a + l4b (9 cards, the 5 new pieces).** **l1 + l2
is done, 62 of its 63** (Decisions, "4.4l1 + l2"; 426 of 528 play — W 60/60, U 75/75, B 61/61, R 54/54, G 50/50,
multicolour 6/65, colourless 120/163): the review took out Lightning Greaves (l5: the search AI re-equips it without
end) and found Chromatic Star broken — fixed in the batch (`ManaAbilityComponent.CashedIn`: a mana ability that
costs mana and sacrifices itself is sacrificed by the solver and is a move, `TapForManaAction.PaysItsCost`).
**For the AI lane at the merge
(Shayne, 2026-11-21; Decisions, after "4.4l1 + l2"): an ability that costs no mana is not free to the search** —
Greaves' endless `Equip {0}` needs a rule that ends it, and Zuran Orb (in, and playing) had the AI sacrifice nine
lands in a row for life. **Next: l3a + l3b** — the 21 options (mana; exile, graveyard and library), each with its
test and revert-check; LED and Wizard's Rockets were to use the stack, the limitation written into DesignNotes
there — first weigh the Star's move for them (a mana ability the solver may not choose, offered as a move:
Decisions, the Star's entry); WATCH the AI's turn with each card that has a free or resource-spending ability (the recipe is in
Decisions) — the probe's random play doesn't show it. From l1 + l2:
the land helpers `SurveilLand`, `Triome`, `FastLand(a, b)`, `HorizonLand(a, b)`, `Verge(a, b, type, type)`;
`YouControlA(filter)`; `SacrificeThis(cost, tap, targets, effects…)`; tokens `Servo`, `Myr`, `BigConstruct` (4/4;
`Construct` is the 0/0 — Karn's and Urza's Saga's needs its +1/+1 static); `MayLoot`; the lands' tests are in
`LsvBatch2lTests.cs` (`EmptyBoard()` — the base board's twenty basics answer every "unless you control";
`PlayAmong(land, n)`, `TapsFor(land, colour)`). Two rules the plan didn't know, from the rules files: a plain `{T}`
mana ability doesn't read `OrColor` (Relic of Sauron, Sunken Ruins: magic-mana.md), and "X targets" on an activated
ability THROWS today (Candelabra: magic-casting.md). From k3: crew `LsvCards.Crew(n)` (Smuggler's Copter and Reckoner Bankbuster wait on nothing else for it);
a type until end of turn `ModifyUntilEndOfTurn { AlsoType }` without `ForGood`; earthbend `LsvCards.Earthbend(n)`;
"whenever you tap [these] for mana, add an additional …" `ExtraMana(filter, colour)` (`ManaAbilities.ExtraFor`);
bestow `Scripting.Bestow(cost)` (`MayCastFromHandFree.Enchant`); day/night `Rules.DayNight` (`MagicGame.Daytime`,
daybound the printed keyword on a card with a `BackFace`); k3's tests are in `LsvBatch2k3Tests.cs` (`NextTurn()`,
`LandEnters(answer)`, `CastBestowed(host)`). For the AI lane after the merge: `Evaluator.NextTurn` doesn't yet undo a
crew stamp (DesignNotes, "Crew"), and no front end shows day or night. From k2b: "becomes the target" has a permanent or a spell as its subject (`Is.Spell` tells
them apart); "attacks that player if able" `Combat.AttacksThePlayer`; a counter of any kind removed `RemoveCounters {
AnyKind }`; `CombatDamageCantBePrevented(filter)`; "put into a graveyard from anywhere" a `ChangedZone { Subject =
Is.Source, To = Graveyard }` trigger read `From = Graveyard`; `Protection { If }`; level counters `CounterKinds.Level`;
k2b's tests are in `LsvBatch2k2bTests.cs` (`TheyCast`, `TheirPlaneswalker`). From k2a: a mana ability's "activate only if" is `ManaAbilityComponent.OnlyIf`; a token copy's
"except" is `CreateTokenCopy { AlsoType, SetBase, Colors, AlsoSubtypes, NoManaCost }`; eternalize `LsvCards.Eternalize`,
persist `LsvCards.Persist` (`MoveTo.CounterKind`); "whenever you sacrifice" `EventTest.Sacrificed`
(`ChangedZoneEvent.Sacrificed`, lands only — the event is raised for a land); shroud beside hexproof; k2a's tests are in
`LsvBatch2k2aTests.cs` (`Mana(card)`, `Settle()`, `Enters(card)`). From k1: "… instead" on the
Nth resolution is `IfNthResolutionThisTurn { Otherwise }`; a land put into a graveyard from the battlefield raises
`ChangedZoneEvent` (`LastKnown` the land as it was — Baloth Prime's "whenever you sacrifice a land" marks that event);
the prepare helpers `EntersPrepared` / `PrepareSpell` are `Scripting`'s; k1's tests are in `LsvBatch2kTests.cs`
(`LandEnters(target)`). **Green is next (Shayne, 2026-11-21): MC-4.4k** — its 40 unplayed cards
(`python tools/magic_set.py text --missing --set LSV`, the green ones) read card by card and tiered as 4.4h, 4.4i and
4.4j were (a DFC from its front face's cost; each new piece naming its nearest), written in Decisions as "4.4k", then
batches of more cards a session; Shayne reviews the plan before the first batch. Then multicolour (59) and colourless
(105). From j4–j6: the city's blessing `MagicPlayer.CitysBlessing` / `YouHaveTheCitysBlessing`; reconfigure
`LsvCards.Reconfigure(cost)` (`Unattach`, `Is.Attached`); voting `Vote(filter) { Then }` (`Ref.Chosen` the most-voted).
j4–j6's tests are in `LsvBatch2j4Tests.cs` (`KeepLands(n)`, `Votes(mine, theirs)`). From j2b + j3: the monarch and the
initiative are `DesignationComponent(name)` (Okoye, Forth
Eorlingas! and Aragorn take the monarch with multicolour); a player's counters are counted and removed as cards' are;
`EachSacrifices { Keeps }`; `WhenYouDo { If }`; incubate `LsvCards.Incubate(x)`; the tests are in
`LsvBatch2j2bTests.cs`. From j1 + j2a: "protection from the color of your choice" is `GainsChosenKeyword { Grants }`;
fading `LsvCards.Fading(n)`; the Map token `LsvCards.CreateMap`; j2a's tests are in `LsvBatch2jTests.cs` (`Rune`,
`Bolt`, `Murder`). White's 19 (Shayne, 2026-11-20: white next),
read card by card and tiered as 4.4h and 4.4i were (Shayne: more cards a session) — Giver of Runes, Guide
of Souls, Mother of Runes, Ocelot Pride, Ajani, Nacatl Pariah // Ajani, Nacatl Avenger, Lion Sash, White Plume
Adventurer, Palace Jailer, Seasoned Dungeoneer, Witch Enchanter // Witch-Blessed Meadow (modal: i7's piece), Solitude,
Eagles of the North, Get Lost, Prismatic Ending, Winds of Abandon (overload: h3's), Council's Judgment, Sunfall,
Parallax Wave, Jennifer Walters // The Sensational She-Hulk. Guide of Souls takes i4's energy; White Plume and
Seasoned Dungeoneer the initiative (g3's). From i7: a modal DFC is `BackFace(…) with { Modal
= true }` and `PlayLandAction { BackFace }` (Witch Enchanter // Witch-Blessed Meadow takes it with white); "when this
transforms into" `TriggerEvent.SelfTransforms` (`TransformedEvent`); "whenever this deals damage" `TriggerEvent.
SelfDealsDamage` (`DealtDamageEvent`); "rounded up" `Count.RoundUp`; a graveyard mark's exile-instead `PlayPermission {
ExiledLeavingTheStack }`; `LsvCards.ReturnTransformed`. i7's tests are in `LsvBatch2i7Tests.cs` (`Transformed(name,
loyalty)`: back face up with that loyalty, `Loyalty(walker, ability, targets)`). **For the AI lane:** an MDFC in hand
counts as its front face, never a land (DesignNotes). From i4–i6: counters on a player are `MagicPlayer.Counters` through `MagicState.PutCounters` (a player's id;
`PutCounters(Ref.You, 3) { Kind = CounterKinds.Energy }`), poison's loss at ten in the state-based check; "pay any amount
of [life / {E}]" is `PayAnyAmount { Kind, AtMost, Then }` with `Count.AmountPaid` (Necrodominance moved onto it); a
transforming DFC's back face is `LsvCards.BackFace(card, abilities…)` (`TransformsInto`), `Card.Transform` the copy
stamp (`Transformed(Original)`, front face up again in `Fresh`), "transform" `Transform(ref)`, "return it transformed"
`MoveTo { Transformed }`; "cast any number of spells from among them" `CastOneOf { AnyNumber }`. A DFC's colours are its
front face's cost now (`CardFactory`) — the survey's gap was the engine's too. i4–i6's tests are in `LsvBatch2i4Tests.cs`
(`Discharge(target, pay)`, `CastFable(toChapter)`: real turns — a lore counter put by hand never reaches a chapter
trigger outside an action, `Reflection()`: Fable transformed, `CastingAll(cast)`). **For the AI lane (after the cube's
implementations):** the evaluator prices neither energy nor poison, and the table's `PlayerPlate` shows neither
(DesignNotes). From i2b + i3: multikicker is `Scripting.Multikicker`
(`Count.TimesReplicated(Ref.Source)`); `TargetSpec.PerX` is a `Count` (a trigger's counted as it triggers); dash
`Scripting.Dash(cost)`, backup `Scripting.Backup(n, keyword)`; an additional combat `AdditionalCombatPhase`
(`MagicGame.AdditionalCombats`, `TurnStructure.EndStep`); "attacks for the first time each turn" `Trigger.Nth = 1` on
`Attacked` (`AttackedEvent.Number`). Their tests are in `LsvBatch2i2bTests.cs` (`Ox(name)`, `Delirium()`,
`AttackInEveryCombat(fomo, alongside)`). From i1 + i2a: delirium is `LsvCards.Delirium`; a trigger's divided targets are
`Division.Words` / `Division.Ask` (`StackAbility.Shares`); `DiscardedEvent` is one per card ("one or more" is
`OneOrMore`); "attacks each combat if able" is `Combat.AttacksEachCombat` (`Combat.MustAttack`); "if you've completed a
dungeon" `YouCompletedADungeon`; the Blood token `Tokens.CreateBlood`. Their tests are in `LsvBatch2iTests.cs`
(`Delirium()`: four card types in my graveyard, `Ox(name)`: their 1/5). From
h7–h9: saddle is `LsvCards.Saddle(n)` ("saddled"
`Is.With("Saddled")`); emerge `Scripting.Emerge(cost, filter)` (`MayCastFromHandFree.LessTheSacrificed`);
`ExileTopPlayable { Linked }` groups exiles from several zones for `Is.ExiledBySource`; a trigger read from a hand or
graveyard is `TriggeredAbilityComponent.From` (was `FromGraveyard`); miracle `Scripting.Miracle(cost)` (`CastOneOf {
Instead }`, `DrewCardEvent.CardId`). The one-batch experiment's row is in `card-batch-usage.md`. h7–h9's tests are in
`LsvBatch2h7Tests.cs` (`Four`: a {2}{B}{B} 4/4, `Relic`: a {3} artifact, `Draw(n)`). From h6: "choose a card name" as
a spell resolves is `ChooseCardName` (the name on the spell on the stack, `MagicState.AskName` the one question; the AI
names its own card when `ChooseKeywordChoice.NamesYourOwn`); Identity Echo's reveal-until takes `{ Matching, To }`. h6's
tests are in `LsvBatch2h6Tests.cs` (`Naming(name)`). From h5: an Aura's host may be a card in its owner's graveyard (`Attachments.HostOf`); its
enchant changed for good is `ModifyUntilEndOfTurn { ForGood, Enchant }` (`Attachments.EnchantOf`); "you may cast this as
though it had flash" `CastAsThoughItHadFlash(effects at the next cleanup)`, and the cleanup step raises `StepBeganEvent`.
h5's tests are in `LsvBatch2h5Tests.cs` (`Ogre`: a 3/3, `Disenchant`, `ToMyEndStep()`, `ToTheirTurn()`). From h4: splice is `CastSpellAction.Splice`
(hand card ids, one cast per set) and the `Spliced` stamp on the spell (Adventure's shape), its text appended by
`StackResolution.TextOf`, scoped as a mode's; Through the Breach takes `Scripting.SpliceOntoArcane("{2}{R}{R}")` with red.
h4's tests are in `LsvBatch2h4Tests.cs` (`Legend`: a legendary 3/3, `Rite`: a {1} Arcane instant, "target player gains 2
life", `ToEndStep()`). From h3: overload is
`Scripting.Overload(cost)` — the cast has no target words (`StackResolution.TextOf(…, alternative)`), "each" the
script's `If(AlternativeCostWasPaid) { Otherwise }`; Winds of Abandon takes it with white. h3's tests are in
`LsvBatch2h3Tests.cs` (`Twin`: an instant copying target spell). From h2e: revolt is
`Count.PermanentsLeftTheBattlefieldThisTurn` (the controller's tally); how many must block is ONE number,
`Combat.MinimumBlockers` (menace, `MinimumBlockers(N)`); shadow `Scripting.Shadow`; "an opponent's graveyard … with a
void counter" `ExileInsteadOfYourGraveyard { Whose, CounterKind }`; "with a [kind] counter" `Is.WithCounter`; a choice
that moves nothing `MoveChosen(Exile, filter, Exile)`; escape `MayCastThisFrom { Costs }`; "exile it instead" of going
to a zone `ExileInsteadOf` (Essence Burn's `[Graveyard]`). h2e's tests are in `LsvBatch2h2eTests.cs` (`Costing(cost)`,
`Bounce`, `TrollAttacks(troll, n)`). From h2d: a spell copy raises `SpellCopiedEvent` (magecraft is
`LsvCards.Magecraft`: Witherbloom Apprentice when B/G comes); "except the first one they draw in their draw steps" on a
trigger is `EventTest.NotTheTurnsDraw`; odd or even is `ChoosesNumberAsItEnters { OddOrEven }`; an exile whose
permission its `Then` decides is `ExileTopPlayable { Play = null }`; ward can cost a fixed `PayLifeCost`. h2d's tests
are in `LsvBatch2h2dTests.cs` (`Army(player)`, `TheyCast(card, mode)`: they cast at instant speed, a modal trigger's mode
answered — `TheirSpellOnTheStack` can't answer a mode choice). From h2c: a counted life cost is `PayLifeCost(Count)` (fixed per cast,
`PayLifeCost.For`); X from a cost `CastSpellAction.XInCosts`; "rather than its mana cost" from a zone permission is
`MayPlayFromZone.Free`; a permanent's mana ability without {T} is a move (`TapForManaAction.NoTap`); a "this turn"
exile-instead is `ExileInsteadOfYourGraveyard { What }` in a `ThisTurn`. h2c's tests are in `LsvBatch2h2cTests.cs`.
h2b's tests are in `LsvBatch2h2bTests.cs` (`ToEndStep()`, `Ogre`);
`MoveChosen.As` is how a chosen move carries `MoveTo`'s options, `Ref.All { Top }` "the top [card] of", and `Exile`
takes a graveyard card. h2a's are in `LsvBatch2h2aTests.cs` (`CastAt(card, target,
answer)`, `Dear`: a {3} sorcery — as h1's in `LsvBatch2hTests.cs`). Remember both players start with an opening hand:
assert hand counts relative, or clear it (Collective Brutality's test). A mechanic's cards of another colour wait for that colour (Winds of Abandon, Through the Breach).
The probe's "activated N" for a card with no activated ability (g9's Riddler; h1's Dismember, Thoughtseize, Hymn) is
answered: Q, Stacking the Deck's exiled card gains "exile a card from your hand: copy this" — counted under that card.
Later, not now: MC-4.4g10b (does Does
Machines ever level?); **an AI issue from h5, after the cube's implementations (Shayne, 2026-11-20)** — the
evaluator prices a flashed Necromancy's creature as lasting (DesignNotes, the Auras-returning-a-creature ceiling: the fix outlined). MC-4.5 is done (Decisions, "4.5").
From 4.4g10: a Class's level is `ClassLevel(N)` (status, not a counter; none is level 1, `Count.ClassLevelOf`);
"[cost]: Level N" `Scripting.Level(cost, n)` → `GainLevel`, "when this Class becomes level N" `Scripting.WhenLevel`
(`Trigger.Chapter` on `BecameLevel`), a level's abilities gated by `AtLevel(n)` (a trigger's `While`, a static's `If`).
"Becomes a [type] in addition" for good is `ModifyUntilEndOfTurn { ForGood, AlsoType }`. g10's tests are in
`LsvBatch2g10Tests.cs` (`LevelUp(dm, answer)`: the one level on offer, activated and resolved; `Level(dm)`).
From 4.4g9: warp is `Scripting.Warp(cost)` — the card's own alternative cost `{ Mana, From = [Hand], PaidAtNextEndStep }`,
the end-step effects put on the permanent as a delayed trigger by `EnterFromStackAction` when that cost was paid (no
"enters" trigger), and `MakePlayable { LaterTurn }` → `PlayableFromExile.FromTurn`. g9's tests are in
`LsvBatch2g9Tests.cs` (`CastRiddler(warp)`, `KeepInHand(n)`, `ToMyNextMain()`). The probe's 150 games had one
unfinished past 300 permanents (g8's had none) — likely the Tribbles card that doubles itself (Shayne); look if it
recurs. The probe's "activated 6" for Riddler (no activated ability of its own) is ignored unless it recurs (Shayne).
From 4.4g8: spree is `Mode.Cost` — read from the printed "+ {cost} —" by `Scripting.Printed`, added per chosen mode in
`CastSpellAction.CostBeforeLife` — and a spree text is `EachWord` (one cast per set of modes, targets word by word;
Shayne). g8's tests are in `LsvBatch2g8Tests.cs` (`CastOf(card, modes)`: the cast offered for those modes).
From 4.4g7: storm is `Scripting.Storm` — `SelfCast`, `CopySpell(Ref.EventCard) { N = Count.SpellsCastBefore(…) }`; the
count is every player's spells, stamped as it is cast (`OnStackComponent.SpellsCastBefore`); a `SelfCast` trigger now
carries the spell as it was cast (`EventSnapshot`), so a countered spell is still copied. `CopySpell { N }` is "copy it
N times". g7's tests are in `LsvBatch2g7Tests.cs` (`CastFreezeAt(target)`, `Nope`: counter target spell).
From 4.4g6: teamwork is kicker (`LsvCards.Teamwork(n)`, "cast using teamwork" `WasKicked`); "tap any number of
creatures with total power N or more" is `TapUntappedCost { TotalPower }`, picked after the action
(`TapChosenForPowerAction`) and kept out of the mana payment (`Uses` → `Reserved`) — crew's cost when Esika's Chariot
comes. g6's tests are in `LsvBatch2g6Tests.cs` (`LeaveThemLands(n)`: their untapped lands cut to n, `Dork(size)`: a
creature with "{T}: Add {U}"). **Open (for Shayne), from g6:** should the AI's teamwork picks be a rule, as delve's
are? (Decisions, g6's notes; default: searched.)
From 4.4g5: an Adventure is `AdventureComponent(Face, Text)` (`LsvCards.Adventure(card, targets, effects)`, the second
face) and `CastSpellAction.Adventure` — the card stamped as the face for the cast (`Card.AsAdventure` →
`CastAsAdventure`), exiled as it resolves with a `PlayPermission { Forever, CastOnly }` mark. Embereth Shieldbreaker //
Battle Display is tier 1 now ("Destroy target artifact"). "Can block only [filter]" is `CanBlockOnly`. g5's tests are in
`LsvBatch2g5Tests.cs` (`AsAdventure(card)`: the Adventure casts offered).
From 4.4g4: a face-down permanent is a copy stamp — `Card.TurnedFaceDown` (a blank 2/2's copiable values) with the
card on `FaceDown(Original)` (status), `Card.TurnedFaceUp` undoing it (and any type stamp: the state check re-stamps);
entering face down is `EnterBattlefield(faceDown:)`, manifest dread `LsvCards.ManifestDread` (`LookAndTake {
FaceDown }`), turning face up `TurnFaceUpAction` (a special action, offered with priority). Headliner Scarlett, Memory
Jar and Shelldock Isle exile face down (`ExiledFaceDown`), not this. g4's helpers in `LsvBatch2g4Tests.cs`:
`Manifest(pick)` (a {0} instant of manifest dread), `Ogre` (a {1}{G} 4/4 flier that draws as it enters).
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
- 2026-11-18: MC-4.4g4 done: Abhorrent Oculus — 229 of 528 play. Face-down permanents (a copy stamp, `FaceDown`),
  manifest dread (`LookAndTake.FaceDown`), turning face up (`TurnFaceUpAction`, a special action). Next: MC-4.4g5,
  Adventures.
- 2026-11-18: MC-4.4g5 done: Brazen Borrower // Petty Theft — 230 of 528 play. Adventures (the card stamped as its
  Adventure for the cast, `CastSpellAction.Adventure`; exiled on resolving, the creature castable from there),
  `CanBlockOnly`. Next: MC-4.4g6, teamwork.
- 2026-11-18: MC-4.4g6 done: We Say Thee Nay! — 231 of 528 play. Teamwork is kicker with a new tap cost, "total power N
  or more" (`TapUntappedCost.TotalPower`), its creatures picked after the cast. Next: MC-4.4g7, storm.
- 2026-11-18: MC-4.4g7 done: Brain Freeze — 232 of 528 play. Storm is a cast trigger copying the spell once per spell
  cast before it this turn (every player's, counted as it is cast). Next: MC-4.4g8, spree.
- 2026-11-18: MC-4.4g8 done: Three Steps Ahead — 233 of 528 play. Spree is a cost on each mode, read from the printed
  line; its targets are chosen word by word (Shayne: 7 casts, not 199). Next: MC-4.4g9, warp.
- 2026-11-18: MC-4.4g9 done: Quantum Riddler — 234 of 528 play. Warp is the card's alternative cost from the hand, its
  end-step exile set up as the spell resolves, and the exiled card castable from the next turn on. Next: MC-4.4g10, Classes.
- 2026-11-18: MC-4.4g10 done: Does Machines — 235 of 528 play; blue's tier 3 is finished. A Class's level is status on
  the card, not a counter; leveling is a sorcery-speed ability, and "becomes level N" triggers as a Saga's chapter does.
- 2026-11-18: MC-4.4h planned (Shayne took it): black's 48 — 19 tested pieces (h1), 22 options in five parts (h2a–e),
  seven new mechanics one a session (h3–h9). Next: MC-4.4h1.
- 2026-11-18: MC-4.4h1 done: 17 of black's tested pieces — 252 of 528 play, no engine change. Duress and Wishclaw
  Talisman each need an option, moved to h2a and h2c. Next: MC-4.4h2a.
- 2026-11-19: MC-4.4h2a done: the five hand cards — 257 of 528 play. `YouChooseTheyDiscard { Filter,
  UntilSourceLeaves }`, `ExileUntilSourceLeaves` from a hand, escalate (`SpellEffectsComponent.Escalate`,
  `DiscardCost.Count`); Chain of Smog composed. Next: MC-4.4h2b.
- 2026-11-19: MC-4.4h2b done: the five graveyard returns — 262 of 528 play. `Ref.All { Top }`, buyback as kicker,
  `MoveChosen { Who, As }` and from exile, `MillAndMayTake.N` a Count; `CountersPutEvent` for every kind and `Exile` of
  a graveyard card widened. MC-4.5 with it: Troubling Tribbles out of the random decks. Next: MC-4.4h2c.
- 2026-11-19: MC-4.4h2c done: costs and permissions — 267 of 528 play. `PayLifeCost(Count)`, X from a cost,
  `MayPlayFromZone.Free`, a mana ability without {T} as a move, `ExileInsteadOfYourGraveyard` "this turn",
  `EntersWithCountersComponent.Kind`. Next: MC-4.4h2d.
- 2026-11-19: MC-4.4h2d done: triggers — 271 of 528 play. `SpellCopiedEvent` (magecraft), `DrewCardEvent.TurnDraw`,
  `ChoosesNumberAsItEnters.OddOrEven`, `ExileTopPlayable.Play` null, ward paid in life. Next: MC-4.4h2e.
- 2026-11-19: MC-4.4h2e done: zones and combat — 275 of 528 play; black's options finished. Revolt's tally,
  `Combat.MinimumBlockers`, shadow, `ExileInsteadOfYourGraveyard { Whose, CounterKind }`, `Is.WithCounter`, escape's
  `MayCastThisFrom.Costs`, `ExileInsteadOf` (three cards moved). Next: MC-4.4h3.
- 2026-11-19: MC-4.4h3 done: overload — 276 of 528 play. `Scripting.Overload`, `TextOf(…, alternative)` with no target
  words. Next: MC-4.4h4.
- 2026-11-19: MC-4.4h4 done: splice onto Arcane — 277 of 528 play. `CastSpellAction.Splice`, the `Spliced` stamp,
  `TextOf` appending a spliced text. Next: MC-4.4h5.
- 2026-11-20: MC-4.4h5 done: Auras returning a creature — 279 of 528 play. An Aura's host in a graveyard,
  `ModifyUntilEndOfTurn.Enchant`, `CastAsThoughItHadFlash` with a cleanup delayed trigger. Next: MC-4.4h6.
- 2026-11-20: MC-4.4h6 done: naming a card as a spell resolves — 280 of 528 play. `ChooseCardName` (the name on the
  spell on the stack), `RevealUntilOntoBattlefield { Matching, To }`, the AI naming its own best card. Next: MC-4.4h7.
- 2026-11-20: h7, h8 and h9 to be built as one batch, an experiment in bigger batches (Shayne: the checks afterwards are
  the bottleneck). Measured by its row in `docs/findings/card-batch-usage.md`.
- 2026-11-20: MC-4.4h7–h9 done as one batch: saddle, emerge, miracle — 283 of 528 play; black is finished. `Saddle`,
  `Emerge` (`LessTheSacrificed`), `ExileTopPlayable.Linked`, `TriggeredAbilityComponent.From` (hand), `CastOneOf.Instead`;
  the opening hand no longer raises draw events. Next: Shayne's pick of the next colour (red is next in the list).
- 2026-11-20: Shayne picked red. MC-4.4i planned: red's 17 — 6 tested pieces (i1), 7 options in two parts (i2a, i2b),
  four new mechanics one a session (i3–i6); the survey's filter missed every DFC (blue's 3, black's 2: i7 by default).
  Next: MC-4.4i1, after Shayne's review.
- 2026-11-20: Shayne: more cards a session — red's parts as four batches (i1 + i2a, i2b + i3, i4–i6, i7); each
  session's summary by rarity and by colour (`magic_set.py coverage` prints both). Next: MC-4.4i1 + i2a.
- 2026-11-20: MC-4.4i1 + i2a done as one batch: red's 6 tested pieces and 4 options — 293 of 528 play (red 47 of 54).
  A trigger's divided targets, one discard event per card, `Combat.MustAttack`, completed dungeons counted.
  Next: MC-4.4i2b + i3.
- 2026-11-20: MC-4.4i2b + i3 done as one batch: multikicker, dash, backup, an additional combat phase — 297 of 528 play
  (red 51 of 54). `TargetSpec.PerX` a `Count`; `AdditionalCombatPhase`; `Trigger.Nth` on attacks. Next: MC-4.4i4–i6.
- 2026-11-20: Shayne: the batch's four ceilings looked at before moving on — multikicker is kicked, backup any keywords,
  the rest refused loudly (Candelabra of Tawnos' activated X targets found waiting). Next: MC-4.4i4–i6.
- 2026-11-20: MC-4.4i4–i6 done as one batch: counters on a player (energy, poison), transforming DFCs, transform by an
  activation — 300 of 528 play, **red finished (54 of 54)**. `PayAnyAmount` (Necrodominance moved onto it);
  `Card.Transform`; DFC colours from the front face. Next: MC-4.4i7 (Shayne to confirm).
- 2026-11-20: Shayne took i7 as the default. MC-4.4i7 done: the five missed DFCs — 305 of 528 play, **blue (75 of 75)
  and black (61 of 61) finished**. `DealtDamageEvent`, `TransformedEvent`, CR 701.28f, modal DFCs (`PlayLandAction {
  BackFace }`). Next: Shayne to pick the next colour.
- 2026-11-20: Shayne picked white. Next: MC-4.4j, white's 19 — survey, tiers, plan.
- 2026-11-20: MC-4.4j planned — 6 tested pieces, 9 options, 4 new pieces (the monarch, ascend, reconfigure, voting);
  three batches. Shayne took it as written; j1 + j2a next.
- 2026-11-20: MC-4.4j1 + j2a done — 11 white cards, 316 of 528 play (white 52 of 60). Next: j2b + j3.
- 2026-11-21: MC-4.4j2b + j3 done — 5 white cards and the monarch, 321 of 528 play (white 57 of 60). Next: j4–j6.
- 2026-11-21: MC-4.4j4–j6 done — ascend, reconfigure, voting; 324 of 528 play, **white (60 of 60) finished**. Next:
  Shayne to pick what follows (green, multicolour, colourless).
- 2026-11-21: Shayne picked green. Next: MC-4.4k, green's 40 — survey, tiers, plan.
- 2026-11-21: MC-4.4k1 done — green's 21 tested pieces, 345 of 528 play (green 31 of 50). Next: k2a.
- 2026-11-21: MC-4.4k2a done — green's 7 mana, land and token options, 352 of 528 play (green 38 of 50). Next: k2b.
- 2026-11-21: MC-4.4k2b done — green's 8 trigger, combat and casting options, 360 of 528 play (green 46 of 50); MACO
  Strike Team's become-target trigger corrected (one golden game moved, intended). Next: k3.
- 2026-11-21: MC-4.4k3 done — crew, earthbend, bestow, day/night; 364 of 528 play, **green (50 of 50) finished: every
  colour is done**. Shayne picked colourless. Next: MC-4.4l, colourless' 105 — survey, tiers, plan.
- 2026-11-21: MC-4.4l planned — colourless' 105 read and tiered (63 tested pieces, 33 options, 9 cards on 5 new
  pieces), seven batches; no card built (364 of 528 play). Shayne answered its two questions (Cogwork Librarian no
  script; LED and Wizard's Rockets on the stack, the limitation to be noted in l3a), and asked for more cards a
  session: four batches. Next: l1 + l2.
- 2026-11-21: MC-4.4l1 + l2 done — 62 of colourless' 63 tested pieces (40 lands, 22 others); 426 of 528 play
  (colourless 120 of 163). The review took out Lightning Greaves (l5: the AI re-equips it without end) and found
  Chromatic Star broken — fixed at Shayne's asking (a mana ability that costs mana and sacrifices itself: sacrificed
  by the solver, and a move); Shayne: an AI note on free abilities — Zuran Orb too. Next: l3a + l3b, the 21 mana and
  exile/graveyard/library options.
