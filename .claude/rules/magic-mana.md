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
  Molten Tide (`ExtraMana(filter, colour)` in a `ThisTurn` — the same piece on a permanent is Badgermole Cub's
  "whenever you tap a creature for mana, add an additional {G}", MC-4.4k3: one more for each permanent of its
  holder's controller the filter matches, listed once per state; `ManaAbilities.ExtraFor` is the one answer, read by
  a tap whose cost picks a card too, and a unit of ONE colour is never the "one colour chosen": beside Birds' any-colour
  mana the extra is {G}), mana that can't pay for spells from your hand
  (Heartwood Crafter). `ManaPayment.Find` solves units: a permanent taps once, and what the payment
  doesn't use floats (`Payment.Floats`). **A mana ability is an activated ability** (CR 605): ONE
  `ManaAbilityComponent` — `Amount`, `AnyOneColor`, a mana `Cost`, and the shared `Costs` list ("{T}" unless
  it says otherwise); a grant is `GrantsManaAbility(filter, ability)` (Rok-Tahk, Rishkar), read with the
  permanent's own by `ManaAbilities.AbilitiesOf`. **"Activate only if"** on a mana ability (Fanatic of Rhonas'
  ferocious {G}{G}{G}{G}; Undermountain Adventurer's "add six {G} instead" — two abilities, the {G}{G} under `Not`,
  MC-4.4k2a) is `ManaAbilityComponent.OnlyIf`, judged where the abilities are listed (`ManaAbilities.AbilitiesOf`): an
  ability it refuses isn't there for payment, manual tapping, the mana moves or the X bound, so the yield's "the one
  making the most" picks among those it allows.
  **A cost that picks a card** (Phyrexian Tower's "Sacrifice a
  creature") is never auto-paid: it is a move in `LegalActions`, one per candidate (`TapForManaAction.SacrificeId`,
  Shayne 2026-11-06), its mana floating. **So is a mana ability without {T}** (Rain of Filth: "until end of turn, lands
  you control gain 'Sacrifice this land: Add {B}'", MC-4.4h2c — gained through `ModifyUntilEndOfTurn.Gains`, read by
  `AbilitiesOf` as any gain): `ManaAbilityComponent.NoTap` (a permanent's only — a Spirit Guide's in a hand never taps
  and stays plain), so it is not `IsPlain` and the solver, which taps each source once, never sees it; it is a move per land, `TapForManaAction.NoTap`, on a tapped land too. **A mana ability that costs mana** (Starfleet Academy: "{5}, {T}:
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
  `CantBeCountered` (Delighted Halfling: "and that spell can't be countered") stamps the spell it paid for
  (`ManaPayment.MakesUncounterable`, asked before `Pay`: floating, or the ability `Used` names). **A permanent whose
  abilities differ in restriction** (the Halfling's free {C} beside its legendary-only colours) is answered per payment:
  `YieldOf(…, paying)` leaves out the abilities whose mana can't pay for that spell; tapped by hand, a colour its
  restricted yield lacks comes from its unrestricted abilities (`TapForManaAction.YieldFor`).
- **"Add three mana in any combination of {R} and/or {G}"** (Orcish Lumberjack: "{T}, Sacrifice a Forest") is
  `ManaAbilityComponent { Amount = 3, OrColor }` on an ability whose cost picks a card: one move per Forest × split
  (`TapForManaAction.OfTheOther`, how many units are the other colour — 4 splits of three), the mana floating. A plain
  {T} ability doesn't read `OrColor` (DesignNotes).
- **A mana ability of a card in the HAND** (the Spirit Guides: "Exile this card from your hand: Add {G}") is
  `ManaAbilityComponent.From = Hand` with `Costs = [ExileSelfCost]` — `ManaAbilities.AbilitiesOf(…, from)` keeps each
  zone's apart, so it is no mana on the battlefield. `ManaPayment.Find` spends it only when the battlefield can't pay,
  after every permanent (`FromHand`, never the spell being cast or a card another cost uses); `Pay` exiles it and raises
  the activation (Kai Winn). The X bound counts it (`AvailableMana`).
- **What a mana ability costs beyond {T}, and the damage it deals you** (Mana Confluence's `PayLifeCost(1)`; the
  Talismans' and Ancient Tomb's `DealsDamageToYou`) are paid by `ManaAbilities.PayAsUsed` — manual tapping
  (`TapForManaAction`) and payment (`ManaPayment.Pay`) alike — for the ability that made the colour (`Used`: the one
  making the most, then the one costing least, so a Talisman's {C} is painless). An ability whose costs can't be paid
  makes no mana (`Usable`: Confluence at 0 life). ponytail: the solver doesn't weigh the pain — it tries a source's
  colours in its abilities' order ({C} first, so generic is paid painlessly), and two life-paying sources are each
  checked against the life alone.
- **Convoke and improvise** are ONE answer, `ManaPayment.Helps(Helpers(s, spell), permanent)` (MC-4.4f), read by
  `Find` (from its `forSpell`) and by the X bound (`AvailableMana`): for a Convoke spell your untapped creatures, each
  paying its own colour or {1} (as Colorless); for an Improvise one (Kappa Cannoneer) your untapped artifacts, {1}
  only — joining the sources after lands; summoning sickness doesn't matter. Never a move.
  **"Tap an untapped artifact you control: Add {U}"** (Urza, Lord High Artificer, MC-4.4e2c) is a
  `ManaAbilityComponent { Costs = [TapUntappedCost(1, filter)] }` — a cost that taps ANOTHER permanent:
  `ManaAbilities.TappingAnother` lists them, and in `Find` each untapped permanent their filter matches pays that
  colour, tapped, in the SAME unit as convoke's creature (`TappedFor`; the X bound counts it). Never a move: its
  `ChoosesCost` keeps it out of `YieldOf` (the holder isn't tapped) and out of `ManaMoves` (no sacrifice).
  One mana each, paid as the tapped permanent's own (DesignNotes).
- **"Spells you cast from anywhere other than your hand cost {1} less"** (Bilbo, Thief in the Night, MC-4.4e2d) is
  `CostChange(-1) { NotFrom = [Hand] }` — where the card is as it is cast (`Costs.Of` runs before it moves to the
  stack); never a filter, which holds no zone. **"Pay its mana cost reduced by {2}"** (Flash) and **"{X}, where X is
  the difference"** (Transmute Artifact) are `PayMana { ManaCostOf, Generic, ReducedBy }`, owed as it is paid
  (`PayMana.Owed`; `CanPay { Pay }` asks the same; a printed `{U/P}` is paid with its colour); "X less Y" is a
  `ReducedBy`, never one `Count` less another.
  **"Unless you pay" / "If you don't"** must run when it CAN'T be paid too, which `May` never does:
  `Scripting.MayPay(prompt, pay, otherwise)`.
- **Cost changes all live in `Costs.Of`**: each is a `CostChange` — on a permanent, for the spells
  `By` names (Thalia's tax on opponents, Geist's reduction on yours), or `OnItself` ("this spell costs
  … less"); an activated ability's (Warrior's Blades) is `ActivatedAbilityComponent.CostChange`, read
  by `Costs.OfAbility` with the targets known — and a spell's own (Guidance Failure: "if it targets an
  attacking creature") sees its targets too, `Costs.Of(…, targets)` from the cast. **A permanent's tax on
  abilities** (Anointed Peacekeeper: "Activated abilities of sources with the chosen name cost {2} more to activate
  unless they're mana abilities") is `CostChange(2, filter) { Abilities = true }` (MC-4.4d2b): the filter asked of
  the ability's SOURCE, whoever activates it, read by `Costs.OfAbility` and never by `Costs.Of`. A mana ability is
  no `ActivatedAbilityComponent`, so it is never taxed; a loyalty ability pays no mana (a ceiling, DesignNotes).
  **"This effect can't reduce the mana in that cost to less than one mana"** (Forensic Gadgeteer's `CostChange(-1,
  …) { Abilities, Floor = 1 }`) is `Floor`: the floored reductions apply after every other change, never below
  min(Floor, the mana value they started from) — two Gadgeteers make a Clue's {2} {1}; a {0} stays {0}.
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
  **"Tap any number of creatures you control with total power N or more"** (teamwork, We Say Thee Nay!, MC-4.4g6; saddle
  and crew, below) is `TapUntappedCost { TotalPower = N }`: no pick in the action, the creatures picked AFTER it
  one at a time until the total (`TapChosenForPowerAction`, a `ChoosePermanentChoice` each) — a cast per set would be
  4,096 against 12 creatures. The mana is paid first, so `Uses` keeps the most powerful out of the payment until the
  total (`Reserved`) — a creature can't tap for the mana and the teamwork both. **Saddle** (Caustic Bronco, MC-4.4h7) is
  that cost on a sorcery-speed activated ability, `LsvCards.Saddle(n)` (`Is.Creature & !Is.Source`: "other creatures"),
  the Mount's "saddled" a keyword granted until end of turn (`Is.With("Saddled")`; DesignNotes for its ceiling).
  **Crew** (CR 702.122, Esika's Chariot, MC-4.4k3) is the same cost at instant speed, `LsvCards.Crew(n)` — summoning-sick
  creatures may crew (no {T}) — resolving to `ModifyUntilEndOfTurn(Ref.Source, 0, 0) { AlsoType = Creature }`.
- **Sunburst** (`Sunburst`, Engineered Explosives) is the colours actually SPENT: the cast pays with the most
  different colours it can for the X chosen (`ManaPayment.Find(…, mostColors)`, Shayne, 2026-11-05), records
  them (`ManaPayment.ColorsSpent` → `OnStackComponent.ColorsSpent`), and `EnterFromStackAction` puts that many
  charge counters through `PutCounters` (Geordi doubles them). A copy spent nothing (CR 702.44c). It never
  sacrifices a Treasure or uses Starfleet Academy for a colour that the plain payment wouldn't have.
  **Converge** (Prismatic Ending, MC-4.4j2b) is that payment on an instant or sorcery: the card carries `Sunburst`, its
  text reads `Count.ColorsSpent(Ref.Source)` (`Card.AsCast`). The colours the generic adds are ones the cost's own pips
  don't already spend, so {X}{W} with X = 2 spends three.
- **"Creatures you control have '{T}: Add {G}{G}'"** (Rok-Tahk, `GrantsManaAbility`) is read by
  `ManaAbilities.ColorsOf` and `YieldOf` — the one place mana is answered — so payment, manual tapping and the X
  bound all see it; a summoning-sick creature can't (`CanPayTapCost`).
- **"Remove N counters from among creatures you control"** (Lieutenant Commander Data, `RemoveCountersCost`)
  is picked AFTER activating, one counter at a time (`ChoosePermanentChoice`, then `RemoveACounterFromChosenAction`:
  a harmful kind first) — one action per set would be 364 on 12 creatures. Activation asks every cost's `CanPay`.
- **Phyrexian mana** (`{B/P}`, `SymbolKind.Phyrexian`; Drivnod) is chosen with the activation or the cast:
  `ActivateAbilityAction.LifeForPhyrexian` and `CastSpellAction.LifeForPhyrexian` (Gitaxian Probe, Phyrexian
  Metamorph, MC-4.4e2c), one legal action per number of symbols paid with 2 life each (CR 107.4f; `|pN` in
  `MoveKey`), only with the life to pay (CR 119.4) — a free cast has none to pay. `ManaCost.PayingLifeFor` turns the
  rest into their colour, so the solver never sees one; the life is lost after the mana is paid.
- **"Exile N cards from your graveyard"** as a cost (Drivnod: three creature cards) is `ExileFromGraveyardCost { Count }`:
  one is a legal action per card (`ExileId`, Gallia); more are picked AFTER activating, one at a time
  (`ChooseAmongChoice`, then `MoveChosenToAction`), as `RemoveCountersCost`'s counters — never one action per set.
  **"Sacrifice five Treasures"** (Magda) is `SacrificeCost(filter) { Count = 5 }` the same way: no pick in the action
  (`CostSlot.None`), offered only with five to sacrifice, the set chosen after activating (`ChoosePermanentChoice
  { Sacrifice }`, then `SacrificeChosenAction`).
