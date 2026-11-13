---
paths:
  - "MagicCore/Mana/*.cs"
  - "MagicCore/Rules/Costs.cs"
  - "MagicCore.Tests/Mana/*.cs"
  - "MagicCore/Sets/*.cs"
  - "MagicCore/Sets/*/*.cs"
  - "MagicCore/Effects/*.cs"
  - "MagicCore.Tests/Sets/*.cs"
---

# MagicCore — mana and costs

The real-rules engine's per-mechanic rules for mana abilities and payment, restricted mana, cost changes, costs beyond mana, Phyrexian mana, sunburst — each the ONE way that mechanic works
here. The rules that hold on every task are in `MagicCore/CLAUDE.md`. One file per subsystem (DEV-4):
`magic-casting.md`, `magic-triggers.md`, `magic-combat.md`, `magic-mana.md`, `magic-zones.md`, `magic-statics.md`, `magic-ai.md`; a card script or card test loads all but the AI's. A new rule goes in the one file
whose subsystem it lives in.

- **What tapping a permanent for mana makes** is `ManaAbilities.YieldOf`: units of mana, each with its
  allowed colours — several of ONE colour (Loot, the Nexus: `SameColor`), a Mountain's extra {R} under
  Molten Tide (`ExtraRedFromMountains`, a `ThisTurnEffect`), mana that can't pay for spells from your hand
  (Heartwood Crafter). `ManaPayment.Find` solves units: a permanent taps once, and what the payment
  doesn't use floats (`Payment.Floats`). **A mana ability is an activated ability** (CR 605): ONE
  `ManaAbilityComponent` — `Amount`, `AnyOneColor`, a mana `Cost`, and the shared `Costs` list ("{T}" unless
  it says otherwise); a grant is `GrantsManaAbility(filter, ability)` (Rok-Tahk, Rishkar), read with the
  permanent's own by `ManaAbilities.AbilitiesOf`. **A cost that picks a card** (Phyrexian Tower's "Sacrifice a
  creature") is never auto-paid: it is a move in `LegalActions`, one per candidate (`TapForManaAction.SacrificeId`,
  Shayne 2026-11-06), its mana floating. **A mana ability that costs mana** (Starfleet Academy: "{5}, {T}:
  Choose a color. Add N"; Talon Gates' "{1}, {T}", `ManaAbilityComponent.Cost`) is never a plain tap: `Find` tries it only when
  nothing else pays (`WithCostlyMana`), its N units of one colour in place of its {C}, its own cost owed
  beside the spell's and never paid by the mana it makes (`Need.NotFrom`).
  Manual tapping floats restricted mana AS restricted (`ManaPool.Restricted`).
  A restriction is a `ManaRestriction` the solver checks against the spell it pays for
  (`Find(…, forSpell)`), or the source of the activated ability it pays for (`forAbilityOf` —
  Isolinear Processor's "an ability of an artifact source"). Treasure and Lotus are `Tokens.SacrificeForMana`
  (`Costs = [TapSelfCost, SacrificeSelfCost]`): `Pay` sacrifices them, and the solver spends them last.
- **What mana may pay for is ONE `ManaRestriction(Spells) { Abilities, NotFromHand }`** (MC-4) — a filter asked of
  the spell paid for (Mishra's Workshop: `new(Is.Artifact)`, "artifact spells"; Chandra's `new(!Is.Creature)`), with
  `Abilities` the ability's source too (Isolinear Processor), `NotFromHand` Heartwood Crafter's. `default` is none.
  Read by `ManaAbilities.Allows` alone; never a new case per card.
- **What a mana ability costs beyond {T}, and the damage it deals you** (Mana Confluence's `PayLifeCost(1)`; the
  Talismans' and Ancient Tomb's `DealsDamageToYou`) are paid by `ManaAbilities.PayAsUsed` — manual tapping
  (`TapForManaAction`) and payment (`ManaPayment.Pay`) alike — for the ability that made the colour (`Used`: the one
  making the most, then the one costing least, so a Talisman's {C} is painless). An ability whose costs can't be paid
  makes no mana (`Usable`: Confluence at 0 life). ponytail: the solver doesn't weigh the pain — it tries a source's
  colours in its abilities' order ({C} first, so generic is paid painlessly), and two life-paying sources are each
  checked against the life alone.
- **Convoke** is `ManaPayment.Find(…, convoke: true)`: your untapped creatures join the sources after
  lands, each paying its own colour or {1} (as Colorless); summoning sickness doesn't matter.
- **Cost changes all live in `Costs.Of`**: each is a `CostChange` — on a permanent, for the spells
  `By` names (Thalia's tax on opponents, Geist's reduction on yours), or `OnItself` ("this spell costs
  … less"); an activated ability's (Warrior's Blades) is `ActivatedAbilityComponent.CostChange`, read
  by `Costs.OfAbility` with the targets known — and a spell's own (Guidance Failure: "if it targets an
  attacking creature") sees its targets too, `Costs.Of(…, targets)` from the cast.
- **Costs beyond mana are one list of `AdditionalCost`** (Effects/AdditionalCosts.cs) — activated
  abilities, a spell's `AdditionalCostsComponent`, flashback, ward. A cost that picks a card names it
  in the action's `SacrificeId`/`DiscardId`/`ExileId`/`BeholdId`/`TapId`/`SecondTapId`/`ReturnId` (`CostSlot`), a set in `TapIds` — and the table's
  `Targeting.PicksOf`/`WithoutPicks`, so it is clicked — one legal action per
  choice; `CostList` is the one place candidates, legality and "or pay" read them. Taps are paid
  first; ward pays by choosing (`PaidByChoosing`).
- **Restricted floating mana** ("spend this only to cast a noncreature spell") is
  `ManaPool.Restricted`: the solver offers it first, only to a spell `ManaAbilities.Allows` — the one
  restriction check, permanents' mana too — and `Pay` takes that unit. `ManaPool.Total` excludes it.
- **"Tap two untapped …"** (Worf, Tenured Tethermage) is the player's choice of both: `TapUntappedCost(2, …)`
  is split as `ActivatedAbilityComponent.Costs` is built into two one-pick costs (`TapId`, `SecondTapId`),
  each pair in either click order (the table), one move by `MoveKey` (the AI) — Shayne, 2026-10-31.
  **"Tap three" or more** (Collation Trimatrix, Picard's seven Officers) is ONE cost picking the whole set
  (`CostSlot.TapSet`, `ActivateAbilityAction.TapIds`): one legal action per set, never per order
  (`MagicRules.TapSets`), clicked in any order on the table (`Targeting.Narrow`) — Shayne, 2026-11-05.
- **Sunburst** (`Sunburst`, Engineered Explosives) is the colours actually SPENT: the cast pays with the most
  different colours it can for the X chosen (`ManaPayment.Find(…, mostColors)`, Shayne, 2026-11-05), records
  them (`ManaPayment.ColorsSpent` → `OnStackComponent.ColorsSpent`), and `EnterFromStackAction` puts that many
  charge counters through `PutCounters` (Geordi doubles them). A copy spent nothing (CR 702.44c). It never
  sacrifices a Treasure or uses Starfleet Academy for a colour that the plain payment wouldn't have.
- **"Creatures you control have '{T}: Add {G}{G}'"** (Rok-Tahk, `GrantsManaAbility`) is read by
  `ManaAbilities.ColorsOf` and `YieldOf` — the one place mana is answered — so payment, manual tapping and the X
  bound all see it; a summoning-sick creature can't (`CanPayTapCost`).
- **"Remove N counters from among creatures you control"** (Lieutenant Commander Data, `RemoveCountersCost`)
  is picked AFTER activating, one counter at a time (`ChoosePermanentChoice`, then `RemoveACounterFromChosenAction`:
  a harmful kind first) — one action per set would be 364 on 12 creatures. Activation asks every cost's `CanPay`.
- **Phyrexian mana** (`{B/P}`, `SymbolKind.Phyrexian`; Drivnod) is chosen with the activation:
  `ActivateAbilityAction.LifeForPhyrexian`, one legal action per number of symbols paid with 2 life each (CR 107.4f;
  `|pN` in `MoveKey`), only with the life to pay (CR 119.4). `ManaCost.PayingLifeFor` turns the rest into their
  colour, so the solver never sees one — a spell with `{B/P}` throws there until a card needs it.
- **"Exile N cards from your graveyard"** as a cost (Drivnod: three creature cards) is `ExileFromGraveyardCost { Count }`:
  one is a legal action per card (`ExileId`, Gallia); more are picked AFTER activating, one at a time
  (`ChooseAmongChoice`, then `MoveChosenToAction`), as `RemoveCountersCost`'s counters — never one action per set.
