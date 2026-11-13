---
id: MC-6
title: MagicCore's pieces compose — one piece per family, not one per card phrase
status: ready
area: MC
created: 2026-11-06
---

## Why

Shayne, 2026-11-06 (the MC-5.6 survey): "If we make every slight alternative of an ability a brand new
effect, we get effect ballooning, bugs, harder to maintain, and harder to create new cards." The survey had
tiered 13 Stardates as new pieces from catalog greps alone. Three of them were already there:
- Sisay's "mana value less than her power" is `Is.ManaValueAtMost`, except its bound is an int.
- Phyrexian Tower is an activated ability with a sacrifice cost, except mana abilities grew their own
  cost flags.
- Isshin's "triggers an additional time" sat beside Crusher's one-event flag.

The audit below, of `MagicCore/AbilityCatalog.md` on 2026-11-06, found that **54 of the 103 components
and 34 of the 118 effects are used by one card**. Not all of that is ballooning (Necrodominance's rules
really are unique), but the families below are.

## Requirements

- [ ] Each family below is one composed piece (options, filters, `Count`s), its old one-card pieces deleted
      and their cards moved onto it, with no game changed except where a ruling was wrong (golden replays)
- [ ] A new card's tier-3 line names the nearest existing piece and why an option on it can't do the job
      (`/card-batch` step 1, `MagicCore/CLAUDE.md`)

## Decisions

- 2026-11-06, Shayne: **audit first, then MC-5.6 fixes the families its Stardates touch, then this story
  does the rest**, so the Stardates aren't delayed and the rest is written down for later.
- 2026-11-06, Shayne: **keywords stay keywords.** Cycling is its own keyword ability, and "when you cycle"
  is about it (landcycling included). Islandwalk is the existing unblockable under a condition, not a new
  rule in combat.
- 2026-11-06, Shayne: **a mana ability is an activated ability that doesn't use the stack**, so it takes the
  same cost list. Phyrexian Tower's sacrifice is never auto-paid: it is always activated by hand, and the
  AI gets it as a move (one per creature).

- 2026-11-12, Claude (cube lane, overnight; MC-6.10, for review): **a card's own "this enters tapped / with N counters"
  stays its own component** — it is the card's replacement effect on itself (CR 614.12), with the shocklands' "unless
  you pay 2 life" and X on it; the composed `PermanentsEnter` is a permanent's rule for OTHER permanents, and never
  applies to its holder (Metallic Mimic's ruling) — one rule now where Warp Speed's check alone had included itself
  (no card could tell: Warp Speed isn't a land). **Karn's "entering doesn't cause abilities to trigger" stays apart**:
  it is not how a permanent enters but which entry EVENTS triggers see, judged on the event's types when triggers are
  collected — the card may be gone by then, so a filter on it can't be asked. Its filter gains `Is.Yours` where the
  old piece read only its controller's permanents (Deanna Troi, Zefram).
- 2026-11-12, Claude: **MC-6.10 before 6.9** — LSVPower's survey (MC-4) needs both (57 cards enter-related, 53
  play-from-another-zone), and 6.10 is the smaller, self-contained family; 6.9 is next.
- 2026-11-13, Claude (cube lane; MC-6.9, for review): **two shapes of "may play", not one.** A RULE over a zone —
  `MayPlayFromZone`, on a permanent or (now) a `ThisTurn` object: Null Summoner's linked card is its `Exile` row with
  `Is.ExiledBySource` (asked from the holder, so CR 607.2a's "that same object" is the filter), Sen Triplets' its `Hand`
  row in the command zone. A MARK on one card — `PlayableFromExile`, what `ExileTopPlayable`/`SearchLibrary` leave
  through `PlayPermission`: it must die with the card's zone change (CR 400.7), which a mark does for free and a rule
  naming an id would have to re-check. Both read by `Playable`.
- 2026-11-13, Claude: **"you may cast that card" as it resolves is the same `PlayPermission`, `Now`** — never a mark
  (a test: Chandra's declined card isn't castable later). Sisko's cap stays a `Count` counted ONCE as it resolves, not
  a filter asked at each choice: CR 608.2h ("determined only once, when the effect is applied"), so a free cast's own
  sacrifice of an Officer can't lower it. MC-6.4's note asked for "a filter on that loop"; the rule says otherwise.
  The planeswalker price of a cast now (`Evaluator.Lifetime`) now reads every `Now`, not only Chandra's — no
  planeswalker or emblem uses the others, so nothing scores differently today.
- 2026-11-13, Claude: **Brazen Bluff's `CastFromHandFree(filter)` stays** — it moves nothing (not "exile these"), and
  it is the one hand-cast-now card; its choose-one step is Sisko's loop at most once. Merge them into one "cast from
  among [a Ref] now" when a second hand card needs it (LSVPower: none in the survey's play-from-another-zone list).

## Tasks

The audit (2026-11-06): each family, its members today, and the composed piece. **MC-5.6** marks the ones
the Stardates fix.

- [x] MC-6.1 **Triggers an additional time** (MC-5.6: Isshin, Drivnod). Done 2026-11-06: Crusher, Lwaxana (her link
      stamped `{ UntilUntapped }` — "for as long as she remains tapped" is a duration, not an `If`) and Isshin
      (two rows: Attacked, AttackersDeclared — Shayne) on it; Drivnod (batch 3a: a creature dying, `Is.Yours`). `LifeGainTriggersAdditionalTime`,
      `TriggersAdditionalTimeWhileSourceTapped` / `WhileTapped`, hard-coded in `Triggers.Collect` (two
      branches) → `TriggersAdditionalTime(Trigger cause, Filter whoseAbilities) { If }`, where the cause is
      checked by the one `Trigger` matcher. Crusher, Lwaxana, Isshin and Drivnod are all rows of it.
- [x] MC-6.2 **Numeric replacements** (MC-5.6: Torbran, Bruvac). Done 2026-11-06: `NumericReplacement(ReplacedEvent)`
      (Source, Affected, CounterKind, Combat, Times, Plus; Brad's a `ThisTurnReplacement`) — Spock, Geordi, M'Benga, Brad,
      Tomik, Yoshimaru, Torbran, Bruvac; every plus, then every "twice" (a ponytail in `Rules/Replacements.cs`). `DrawsDoubled`,
      `ChargeCountersOnYourArtifactsDoubled`, `CountersOnYourCreaturesDoubled`, `CountersPlusOneThisTurn`,
      `AmountReplacement` + the `ReplaceableEvent` enum (one case per card) → one replacement: which event
      (damage, counters, draws, mill), filters for the source and the affected object or player, a counter
      kind, combat or not, and ×N or +N.
- [x] MC-6.3 **Mana abilities** (MC-5.6: Phyrexian Tower, Rishkar, Talon Gates). Done 2026-11-06 in part:
      `ManaAbilityComponent` { Amount, AnyOneColor, Cost, Costs } + `GrantsManaAbility(filter, ability)` — Loot the Nexus,
      Treasure, Starfleet Academy, Rok-Tahk moved; a cost that picks is a move (`TapForManaAction.SacrificeId`, Tower).
      LEFT for after MC-5.4: `ExtraRedFromMountains`, Emrakul's `TapsForTwoColorlessWhileExiled` / `LandTapsForTwoUntilThisIsCast`. `ManaAbilityComponent`,
      `ManaOfOneColorComponent` (`SacrificeSelf`, `Cost`), `YourCreaturesTapForMana`,
      `ExtraRedFromMountains`, `TapsForTwoColorlessWhileExiled`, `LandTapsForTwoUntilThisIsCast` → a mana
      ability with the shared `Costs` list and a mana cost (CR 605: an activated ability). A grant is a
      filter, as `GrantsTriggeredAbility` is. Treasure's `SacrificeSelf` becomes `Costs = [SacrificeSelfCost]`.
- [x] MC-6.4 **Bounds that are counted** (MC-5.6: Sisay, Loot). Done 2026-11-06 in part: `StatBetween(Stat, Count, Count)`
      (counted with the filter's controller and source); Omnipresence → `MayCastFromHandFree(filter)`, Confidant and Chandra →
      `ReturnChosenToHandIf(filter)`; `LookAndTake` { OntoBattlefield, Tapped }. LEFT: Sisko's `ExileTopAndCastFree` cap
      (it is `CastCopiesWithinBudget.EachAtMost`, an int through Uldaros' loop) — a filter on that loop, with MC-6.9. `StatBetween` takes int bounds, so a
      counted bound was baked into effects instead: `ExileTopAndCastFree(N, Count ManaValueAtMost)`,
      `ReturnChosenToHandIfManaValueAtMost(Count)`, `CastFromHandFreeUpToCreatureCount` → `StatBetween`
      bounds as `Count`s (an int converts), and those effects take a filter.
- [x] MC-6.5 **Counters** (MC-5.6: Bristly Bill's per-object count, Mikaeus's kind on the cost). Done 2026-11-06:
      `PutCounters(What, N) { Kind }` (+1/+1 by default; no Kind is Dathon's "of a kind on it"), `RemoveCounters(What, N)
      { Kind }` (no Kind is Mabel's rule), `RemoveCountersCost(N, Filter) { Kind }` (Data; Mikaeus's own counter, unasked)
      and `Ref.It` — a Count counted per object (Bristly Bill's doubling, Beckett's "those counters"). ~125 uses moved.
      Ruling: +1/+1 counters go on any permanent (CR 122.1), so Shields Up! says "if it's a creature" itself; no FRA game
      and no every-card game moved (checked with the new Stardates taken out).
      `AddCounters` (+1/+1), `AddChargeCounters`, `AddStunCounters`, `AddMinusOneCounters`, `PutCounters(kind)`,
      `PutEventCardCounters`, `AddAnotherCounterOfAKind`; `RemoveCounters`, `RemoveCountersOfKind`,
      `RemoveCountersFromAmongCost` → one put and one remove, each with a kind and a `Count` counted per
      object. They already share one funnel (`MagicState.PutCounters`); this is the script vocabulary.
- [x] MC-6.6 **Modes** (MC-5.6: Ertai's "up to one"; Riku's "up to X" in batch 3). `Choose` is a fixed
      int; `ChooseAnyNumberIf` and `ModesNotChosenBefore` are flags beside it → min and max as `Count`s.
      Done 2026-11-06: one `ModeChoice { AtLeast, AtMost (Counts), AnyNumberIf, NotChosenBefore }` as `Choose` on the
      spell, triggered and activated components (an int converts); Sublime Epiphany, Kirk, Kobayashi Maru moved;
      Ertai's `{ AtLeast = 0 }`. `Ref.ControllerOfTarget` reads a stack ability's controller too. Riku (batch 3a):
      `{ AtLeast = 0, AtMost = Count.ModesChosen(Ref.EventCard) }`, a trigger's range counted with its own context.
- [x] MC-6.7 **Cycling** (MC-5.6: Decree of Pain). Done 2026-11-06: `ActivatedAbilityComponent.Keyword`, carried by
      `AbilityActivatedEvent` and matched by `Trigger.Ability`; `Scripting.Cycling(cost)` and `Cycling(cost, BasicLand)`
      — the 10 landcyclers moved, `BasicLandcycling` deleted. Decree's "destroyed this way" is `Destroy.Then` with
      `Ref.ThisWay` (the ones that left), for Discard's and others' "this way" later. `BasicLandcycling` is a helper with nothing saying it is
      cycling → `Cycling(cost)`, with landcycling as cycling that searches. The ability carries its keyword's
      name (a general field, also usable for equip or station), and "when you cycle" is the `AbilityActivated`
      event filtered to it.
- [x] MC-6.8 **Evasion** (MC-5.6: Thada's islandwalk, Shizo's fear). Done 2026-11-06: `Scripting.Landwalk(type)` (the
      plain unblockable under an `If`) and `Scripting.Fear` (a `CantBeBlockedBy`, granted until end of turn through
      `ModifyUntilEndOfTurn.Gains`); no combat rule changed. `Combat.CantBeBlocked` and
      `CantBeBlockedBy(filter)` exist; landwalk and fear are those under a condition or a filter (helpers in
      `Scripting.cs`), with no new combat rule.
- [x] MC-6.9 **Playing from another zone** — done 2026-11-13 (cube lane): the linked and this-turn permissions →
      `MayPlayFromZone` rows (`Of`, `OwnerId`, `AnyTime`, `If`, `AnyType`; a `ThisTurnEffect` too; `Is.ExiledBySource`,
      `Is.AnyCard`) — Null Summoner, Sen Triplets moved, `MayCastWhatItExiled`, `MayPlayFrom`,
      `Playable.IsCastableThroughLink` / `IsPlayableFromTheirZone` gone. The effects → `ExileTopPlayable { Until,
      ShuffleFirst, RestOnBottom, Gains }` with `PlayPermission { Now, Free, AnyNumber, ManaValueAtMost, Otherwise,
      OrIntoHand }` — Chandra, Torch of Defiance, The Caretaker, discover (U.S.S. Discovery), Q, Sisko, Exploratory
      Mission moved; `ExileTopMayCast`, `ExileTopAndCastFree`, `ExileUntilNonlandCastFree` and `UntilNonland` gone.
      Every golden file unchanged; two tests (Null Summoner's other object and their turn; Chandra's no-mark),
      revert-checked. Kept: `PlayableFromExile` (the mark) and Bluff's `CastFromHandFree` — Decisions. Earlier: part done 2026-11-06 (MC-5.6): `PlayPermission { ThisTurn, Forever,
      By, AnyType }`, taken out of `ExileTopPlayable` (Kirk, Exploratory Mission, Chekov, Vulcan Mind-Meld moved) and
      given to `SearchLibrary.Play` with `SearchLibrary.Of` (Thada). Batch 3a: Prosper's "play from exile" is
      `Trigger.From`. Batch 3b (2026-11-07): Sen Triplets' `MayPlayFrom(zone, of)`, a "this turn" permission on the
      player, read by `Playable` beside the rest. The rest after MC-5.4. `MayCastFromExile`, `MayCastFromYourGraveyard`, `MayPlayFromYourGraveyard`,
      `MayCastArtifactFromLibraryTop`, `MayPlayLandsFromLibraryTop`, `MayCastWhatItExiled`, `GrantedFlashback`,
      `CastFromHandFree`, `CastFromHandFreeUpToCreatureCount`, `ExileTopPlayable` and its options, `ExileTopMayCast`,
      `ExileTopAndCastFree`, `ExileUntilNonlandCastFree` → one permission (which zone, whose, a filter, how
      long, free or paid), read by `Rules/Playable.cs`, and one "exile these, they may be played" effect.
      2026-11-12 (cube lane): the permanents' permissions — `MayPlayFromYourGraveyard`, `MayCastArtifactFromLibraryTop`,
      `MayPlayLandsFromLibraryTop` → `MayPlayFromZone(From, filter) { OncePerTurn, Finality, OutsideTheGame }`, read by
      ONE `Playable.ZonePermission` (casts and land plays; `GraveyardPermission`, `LibraryTopPermission` and Pike's
      check gone). Overflow Submatrix, The Holodeck, Nog, Zefram, Pike moved; no game moved; revert-checked. A use is
      stamped only for a once-each-turn rule (Nog's unlimited one was stamped before, read by nothing). Then the card's own:
      `MayCastFromExile`, `MayCastFromYourGraveyard` → `MayCastThisFrom(From) { Finality }` (Storied Veteran, Khan; no
      game moved, revert-checked). LEFT: the linked (`MayCastWhatItExiled`) and this-turn (`MayPlayFrom`,
      `PlayableFromExile`) permissions, and the effects (`ExileTopPlayable`, `ExileTopMayCast`, `ExileTopAndCastFree`,
      `ExileUntilNonlandCastFree`, `CastFromHandFree`).
- [x] MC-6.10 **Entering** — `EntersTappedComponent`, `PermanentsEnterTapped`, `LandsYouControlEnterUntapped`,
      `EntersWithCountersComponent`, `YourPermanentsEnterWithCounters`, `YourCreaturesEnterAttacking`,
      `EnteringArtifactsAndCreaturesDontTrigger` → "permanents matching a filter enter [tapped / untapped /
      with counters / attacking]". Done 2026-11-12 (cube lane): `PermanentsEnter(filter) { Tapped, Untapped,
      CounterKind, Counters, Attacking }` — Root Maze, Warp Speed, Zefram Cochrane, Deanna Troi, Kira Nerys moved; four
      pieces deleted; one funnel in `EnterBattlefield` (`EntryRules`). No game moved (every golden file unchanged); a
      test that the filter is the holder's and never applies to itself, revert-checked. Kept apart, with reasons in
      Decisions: a card's OWN entry (`EntersTappedComponent`, `EntersWithCountersComponent`) and Karn's
      `EnteringArtifactsAndCreaturesDontTrigger`.
- [ ] MC-6.11 **Chosen as it enters** — `ChoosesColorAsItEnters`, `ChoosesNameAsItEnters`,
      `ChoosesNumberAsItEnters` (+ their `Chosen*` markers) → one component, by what is chosen.
- [ ] MC-6.12 **Moving a card** — `ReturnFromGraveyard` (moves from any zone, despite its name),
      `ReturnFromYourGraveyard`, `ReturnFromExileUnderYourControl`, `ReturnLinkedExile`, `ReturnToHand`,
      `ReturnYourPermanentToHand`, `MayPutFromHandOntoBattlefield`, `PutIntoLibrary`, `ShuffleIntoLibrary` →
      a move to a zone with options (chosen or targeted, tapped, under whose control).
- [ ] MC-6.13 **Statics on the enchanted creature** — part done 2026-11-07 (MC-5.6 batch 3b): `EnchantedBecomesCreature`
      → `BecomesType(scope) { Add, Remove, Subtype, Power, Toughness, If }` (Puppet Crafting moved; Mogis's "isn't a
      creature" on it), `Attachments.Animations` made general and run in `EnterBattlefield` too. The rest after MC-5.4:
      `EnchantedIsCreatureType`, `EnchantedLosesAllAbilities`,
      `EnchantedDoesntUntap`, `EnchantedCantBeBeamedUp` → the general statics with the scope
      `Is.AttachedToSource`.
- [ ] MC-6.14 **"This turn" and "this game" conditions** — `FirstSpellYouCastThisGame`, `SubtypeEnteredThisTurn`,
      `HaventBegunAnExtraTurn`, `IfNthResolutionThisTurn` → a `Count` and `AtLeast`, where a tally exists.
- [ ] MC-6.15 **Rule-changing statics** — part done 2026-11-07 (MC-5.6 batch 3b): `NoSpellsOrAbilitiesDuringCombat` →
      `CantCastOrActivate { DuringCombat, ManaAbilitiesToo }`, on a permanent (Yuriko moved) or a `ThisTurn { On }`
      effect (Sen Triplets); `LifeTotalCantChange` (Flare of Fortitude) with paying life one question (`Life.CanPay`).
      The rest after MC-5.4 ("can't", "doesn't", "don't"): `CantAttackThisTurn`, `CantBeCountered`,
      `NextSpellCantBeCountered`, `CantSacrificeTreasures`,
      `DoesntUntapInYourUntapStep`, `YouHaveHexproof`, `PreventAllDamageToYou`, `DontLoseForZeroLife` … —
      look for the families among them (who, what action, a filter, how long). Batch 2's Sen Triplets ("can't
      cast spells or activate abilities") and Flare of Fortitude ("your life total can't change") should
      land on what this finds.

- [ ] MC-6.16 **"Name a card" answered with the name itself** (Shayne, 2026-11-11 — deferred, after AI-19's scan): the rules
      hold no list of names and check none — the answer is a string, unchecked (a wrong name matches nothing, as naming
      a card nobody plays); names come from OUTSIDE the engine: the AI's helper (`GreedyAi.LikeliestName`, Shayne's rule,
      candidates from the set being played) and a search box on the table; a record stores the string. One piece for
      Kai Winn now and Pithing Needle, Meddling Mage later (the cube). Why now: `CardRegistry.NonlandNames` reads the
      game's printings (the only rule that reads a printing, against MagicCore/CLAUDE.md), and a record keeps names
      only, so a TRK game with Kai Winn replays stale — the scan leaves those records out (DesignNotes)

## Next

MC-6.12 moving a card, then 6.15 the "can't" statics, then 6.14 the "this turn / this game" conditions (the order
LSVPower needs them, MC-4's Decisions). MC-6.9 is done: LSVPower's escape, hideaway, cascade, plot and adventures land
on `MayPlayFromZone`, `MayCastThisFrom` and `ExileTopPlayable`/`PlayPermission` (cascade is `Until` + `Now`). Each
step: one family, its old cards moved, the golden replays UNCHANGED (or a ruling fixed and said), the full suite.

**6.12, read 2026-11-13 (not started; ~65 uses in `ZoneEffects.cs`, both sets).** Two shapes, as 6.9 found:
- A move of what a `Ref` names — `ReturnFromGraveyard(What, To) { From, Counters, Tapped, Finality }` (35 uses; already
  the near-general one), `ReturnToHand` (battlefield or stack), `PutIntoLibrary(What, OnTop)`, `ShuffleIntoLibrary
  { FromGraveyards }` (move, then shuffle, per card), `ReturnFromExileUnderYourControl { Tapped, ExileAtNextEndStep,
  UnderItsOwnersControl }` (no tokens), `ReturnLinkedExile` (= `Ref.All(Exile, Is.ExiledBySource, Ref.EachPlayer)` to
  the battlefield under its owner — the 6.9 filter), `MoveCardTo(id, To) { From }` → one `MoveTo(What, To) { From
  (zones it must be in: Graveyard, Battlefield, Stack, Exile…; unset anywhere), Tapped, Counters, Finality, UnderYourControl,
  OnTop, Shuffle, ExileAtNextEndStep }`. Keep each old `From` check exactly — which zones each accepts is what the
  golden replays will catch.
- A CHOSEN move (not targeted) — `ReturnFromYourGraveyard(filter) { To, Otherwise }`, `ReturnYourPermanentToHand
  (filter)`, `MayPutFromHandOntoBattlefield(types, Then)`: a choice over your zone + filter, min/max, then `MoveChosenTo`;
  "if you can't" `Otherwise`, "if you do" `Then`. Their choice types differ (`ChooseAmongChoice`,
  `ChoosePermanentChoice`, `ChooseCardsFromHandChoice`) — keep each card's, or the random replays move.

## Log

- 2026-11-06: created from the MC-5.6 survey (Shayne). The audit counted the catalog's `[N: …]` use counts per
  section: effects 118 (34 used by one card), components 103 (54), conditions 18 (9), costs 13 (4).
- 2026-11-12 (night, cube lane): MC-6.10 — entering composed (`PermanentsEnter`), four pieces gone, no game moved.
- 2026-11-12 (night, cube lane): MC-6.9 part — the permanents' play-from-a-zone permissions composed (`MayPlayFromZone`), three pieces gone, no game moved.
- 2026-11-12 (night, cube lane): MC-6.9 part — the card's own cast-from-a-zone permission composed (`MayCastThisFrom`), two pieces gone, no game moved.
- 2026-11-13 (cube lane, sprint 5 C4): MC-6.9 done — the linked and this-turn permissions onto `MayPlayFromZone`
  (Null Summoner, Sen Triplets), the four exile/cast-now effects onto `ExileTopPlayable` + `PlayPermission.Now`
  (Chandra, The Caretaker, discover, Q, Sisko, Exploratory Mission); five pieces gone, every golden file unchanged,
  three new tests, 8/8 breaks caught. Stopped before 6.12 (context; its design is in Next). For Shayne: the three
  2026-11-13 Decisions (two shapes of "may play"; Sisko's cap counted once, CR 608.2h; Brazen Bluff's hand cast kept apart).
