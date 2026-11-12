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
- [ ] MC-6.9 **Playing from another zone** — part done 2026-11-06 (MC-5.6): `PlayPermission { ThisTurn, Forever,
      By, AnyType }`, taken out of `ExileTopPlayable` (Kirk, Exploratory Mission, Chekov, Vulcan Mind-Meld moved) and
      given to `SearchLibrary.Play` with `SearchLibrary.Of` (Thada). Batch 3a: Prosper's "play from exile" is
      `Trigger.From`. Batch 3b (2026-11-07): Sen Triplets' `MayPlayFrom(zone, of)`, a "this turn" permission on the
      player, read by `Playable` beside the rest. The rest after MC-5.4. `MayCastFromExile`, `MayCastFromYourGraveyard`, `MayPlayFromYourGraveyard`,
      `MayCastArtifactFromLibraryTop`, `MayPlayLandsFromLibraryTop`, `MayCastWhatItExiled`, `GrantedFlashback`,
      `CastFromHandFree`, `CastFromHandFreeUpToCreatureCount`, `ExileTopPlayable` and its options, `ExileTopMayCast`,
      `ExileTopAndCastFree`, `ExileUntilNonlandCastFree` → one permission (which zone, whose, a filter, how
      long, free or paid), read by `Rules/Playable.cs`, and one "exile these, they may be played" effect.
- [ ] MC-6.10 **Entering** — `EntersTappedComponent`, `PermanentsEnterTapped`, `LandsYouControlEnterUntapped`,
      `EntersWithCountersComponent`, `YourPermanentsEnterWithCounters`, `YourCreaturesEnterAttacking`,
      `EnteringArtifactsAndCreaturesDontTrigger` → "permanents matching a filter enter [tapped / untapped /
      with counters / attacking]".
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

MC-5.6 does MC-6.1–6.8 and part of 6.9 as it builds the Stardates (its story's Next has the prompt). Then this
story, after MC-5.4 (the boosters): one family a step, its old cards moved onto the composed piece, each step
checked by the golden replays (Commands.md, "Golden replays") and the full suite. At the end, count the
catalog's one-card pieces again (entries whose use count reads `[1: …]`, per section) against the Log's.

## Log

- 2026-11-06: created from the MC-5.6 survey (Shayne). The audit counted the catalog's `[N: …]` use counts per
  section: effects 118 (34 used by one card), components 103 (54), conditions 18 (9), costs 13 (4).
