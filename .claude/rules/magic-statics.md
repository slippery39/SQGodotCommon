---
paths:
  - "MagicCore/MagicState.cs"
  - "MagicCore/Cards/*.cs"
  - "MagicCore/Filters/*.cs"
  - "MagicCore/Rules/Attachments.cs"
  - "MagicCore/Rules/Characteristics.cs"
  - "MagicCore/Rules/Damage.cs"
  - "MagicCore/Rules/Life.cs"
  - "MagicCore/Rules/Replacements.cs"
  - "MagicCore.Tests/Cards/*.cs"
  - "MagicCore.Tests/Filters/*.cs"
  - "MagicCore/Sets/*.cs"
  - "MagicCore/Sets/*/*.cs"
  - "MagicCore/Effects/*.cs"
  - "MagicCore.Tests/Sets/*.cs"
---

# MagicCore — characteristics, statics, counters, damage and life

The real-rules engine's per-mechanic rules for losing and gaining abilities, type and P/T changes, subtypes, copies of permanents, Auras and Equipment, counters, numeric replacements, damage, life, filters, cards and printings — each the ONE way that mechanic works
here. The rules that hold on every task are in `MagicCore/CLAUDE.md`. One file per subsystem (DEV-4):
`magic-casting.md`, `magic-triggers.md`, `magic-combat.md`, `magic-mana.md`, `magic-zones.md`, `magic-statics.md`, `magic-ai.md`; a card script or card test loads all but the AI's. A new rule goes in the one file
whose subsystem it lives in.

- **Numeric replacements** ("… that much plus one / twice that many instead") are ONE piece,
  `NumericReplacement(ReplacedEvent)` — which event (damage, counters, draw, mill), filters for the source and
  for what it happens to (asked from its holder's side: `Is.Opponents` is a player or their permanent), a
  counter kind, combat or not, `Times` and `Plus` — on a permanent of either player, or a this-turn object
  (`ThisTurnReplacement`, Brad Boimler). `Rules.Replacements.Apply` applies all of them inside the one place
  each event happens: `Damage.Deal` (and `Damage.Excess`, so excess sees the replaced amount),
  `MagicState.PutCounters` (every counter path, entering with them included), `MagicState.Draw` and
  `Mill.Top`. Every plus first, then every "twice" — (n + 1) × 2, the most (CR 616.1). A permanent's own
  never applies to its own entry (CR 614.12). Lifted from MtgCore's ReplacementEngine. Never a new
  component per card phrase (MC-6.2: `DrawsDoubled` and four siblings were this).
- **-1/-1 counters** are `Card.MinusOneCounters`, read into `OwnPower`/`OwnToughness`; the
  state-based check cancels them against +1/+1 counters in pairs (CR 704.5q).
- **"Your life total can't change"** (Flare of Fortitude) is `LifeTotalCantChange`, read by `Life.Gain`/`Lose`:
  damage is still dealt (lifelink, "deals damage" triggers), no life moves. **Paying life** is ONE question,
  `Life.CanPay` (at least that much, CR 119.4; never while it can't change, 119.8) — Phyrexian mana, the
  shocklands and Hive Mind Coprocessor (`CanPayLife(n)`), Necrodominance's number.
- **"Loses all abilities"** is `s.HasLostAllAbilities` — until end of turn (`UntilEndOfTurn.LosesAllAbilities`)
  or under an Aura (`EnchantedLosesAllAbilities`, found once per state with the statics). It reaches
  every rule because every rule reads abilities through `s.AbilitiesOf<T>`, keywords through `s.Has`,
  mana through `ManaAbilities.ColorsOf` (a basic land's intrinsic ability included). What the AURA
  says ("doesn't untap", `DoesntUntap(Is.AttachedToSource)`) is the Aura's, so it still applies — unless the AURA
  loses its abilities (read with `AbilitiesOf` on the holder, MC-6.15). No timestamps: everything goes.
- **Auras and Equipment are one mechanism** (`Card.AttachedToId`, Rules/Attachments.cs). An Aura targets
  as it is cast (`AuraComponent`) and attaches on resolving; what either grants is a live static
  (scope `Is.AttachedToSource`), never stamped. "When enchanted creature dies" looks back through
  `DiedEvent.Attached` — the state check takes the Aura in the same pass.
- **Excess damage** (CR 120.4a) is `Damage.Excess`, measured BEFORE the damage is dealt (past
  loyalty, or toughness less marked damage; 1 from deathtouch). `DealDamage.IfExcess` runs with X = it.
- **Copying a permanent** (CR 707.2) is `Card.WithCopiableValuesOf` — name, cost, types, colours,
  P/T, keywords and abilities, never its status (`Card.IsStatus`: counters, damage, what it was
  given; the same set a zone change strips). A token copy (`CreateTokenCopy`) is a blank token with
  those values, entering through `EnterBattlefield` so its own "enters" fires; a source that just died
  is copied as it last existed. "Becomes a copy until end of turn" (Hall of Echoes) keeps the original
  in `CopyUntilEndOfTurn`, restored in cleanup AND as it leaves (`Card.Fresh`), so a dead Hall is a Hall.
- **A type change** is `BecomesType(scope) { Add, Remove, Subtype, Power, Toughness, If }` — Puppet Crafting
  (`Is.AttachedToSource`: "is a 5/5 Construct creature in addition"), Mogis (`Is.Source`, `Remove = Creature`,
  `If` devotion < 7). STAMPED on what the scope matches (`Card.AnimatedBy`, what it was kept in `Animated`) and
  put back by `Attachments.Check` once it doesn't, and by `Card.Fresh` as it leaves; stamped too in
  `EnterBattlefield` before the entry event (`Attachments.Retype`), so Mogis never enters a creature. Every
  rule reads `card.Is(Creature)`, so a live layer-4 read would need every one of them; the stamp is right by
  the time anyone gets priority. **Devotion** is `Count.DevotionTo(colours)`: a hybrid symbol of two counts once.
- **Station** (Edge of Eternities' mechanic; TRK's Spacecraft, MC-5.2) is `TrkCards.Station(n, keywords)`. The
  station ability's cost is `TapUntappedCost(1, …)` — tapping ONE is a choice (`ActivateAbilityAction.TapId`,
  one legal action per creature; summoning sickness doesn't matter), and that creature is the ability's
  event card, so "its power" is read as it resolves, from its snapshot once it left (a `Count` only —
  `Ref.CountedAsTheyWere`; a `Matches` asks where it is now;
  a known ceiling: the snapshot is as it was tapped). A negative power counts 0 (`Count.Value`). **"It's an
  artifact creature at N+" is read LIVE by `Card.Is`** from the card's OWN charge counters
  (`StationComponent`) — not stamped like Puppet Crafting, since nothing outside the card decides it — so it
  is a creature exactly while it has N, and stays one when it loses all abilities (a type change, layer 4).
- **Base P/T effects** ("has base power and toughness 4/5") sit under counters and pumps, latest
  wins (`UntilEndOfTurn.SetBase`, read in `Card.OwnPower`/`OwnToughness` under `s.Power`/`Toughness`).
  With no duration (Seven of Nine: "base power and toughness become 7/9") it is `ModifyUntilEndOfTurn.ForGood`
  — `UntilTurnOf = UntilEndOfTurn.Never`, which no turn ends: only leaving the battlefield.
- **One card, a printing per set (MC-5.5).** `CardRegistry` is the one lookup across sets: a card's
  rules data and script exist once; its printing (`CardPrinting`: rarity, collector number) is per set,
  and a card in play carries its printing's set (`Card.Set`, kept through zone changes) — read for its
  ART only, never by a rule. A sealed or draft session passes its set (`CardRegistry.Get(name, set)`);
  boosters, ratings and drafts take a set code (`Boosters.Pack(set, rng)`, `CardRatings.Of(set)`,
  `Draft.New(set, seed)`). Rarity is never read from `CardData` — it isn't there.
- **Counters of every kind are put on and removed through `MagicState.PutCounters` alone** (`CounterKinds`):
  the named ones (TRK: quest, lore, promotion) in ONE map, `Card.Counters`, raising `CountersPutEvent` (how
  many it had and has — charge raises it too: Enterprise-D's "whenever charge counters are put on", read by
  `Trigger.Counters`, the kind); the older kinds (+1/+1, -1/-1, charge, stun, time, loyalty) in their own fields
  (`Card.CountersOf`/`WithCounters`). Finality is a yes/no flag, not a count. `CounterWritesTests` fails on
  an engine file that adds to a counter field itself — it would skip every replacement.
  "A permanent with a counter on it" is ANY kind — `Is.WithAnyCounter` (`Card.HasAnyCounter`) reads every
  field and the map; a new counter field must join it. Proliferate doesn't see the map yet (no card needs it).
- **A permanent's subtypes are `s.Subtypes(card)`** (Characteristics) — never `Card.Subtypes` in a rule.
  "Becomes a Doctor (it loses all other creature types)" until end of turn (`ModifyUntilEndOfTurn.BecomesCreatureTypes`;
  several too: Nog's "becomes a Ferengi Scout")
  or under an Aura (`EnchantedIsCreatureType`, De-evolve) is that one type plus the subtypes that aren't
  creature types (`Characteristics.IsCreatureType`). `Is.Subtype`, Federation's count, behold and Kindred
  Judgment read it.
- **"That many plus one of each kind of counter"** (Brad Boimler), **"twice that many charge counters on an
  artifact you control"** (Geordi La Forge) and **"twice that many of each kind on a creature you control"**
  (Dr. M'Benga) are `NumericReplacement(Counters)` rows (the numeric-replacement rule above) — so they reach every
  counter path: effects, costs (`Loyalty.Change`), entering with loyalty, time, charge or +1/+1 counters
  (Picard's seven become fourteen), proliferate.
- **Life is lost in `Rules.Life.Lose` alone** — damage to a player, `LoseLife`, paying life — which counts it
  in `TurnTally.LifeLost` (Gul Dukat: "the life that player lost this turn", `Count.LifeLostThisTurnOf`).
  His "you gain life equal to the life lost this way" is scripted gain FIRST, so both read the count before
  the loss adds to it.
- **"Enter as a copy of any artifact or creature, except it isn't legendary, with its counters"** (Dominion
  Saboteur) is `MayEnterAsCopy { Of, NotLegendary, WithCounters, Optional }` — the counters through
  `PutCounters` (`Card.AllCounters`); `Optional` is a real "may" (Flesh Duplicate's stays forced).
- **"With mana value equal to [a number]"** counted as it resolves is `Ref.ManaValueIs` (a `Count`) on a
  `Ref.All` — Explosives' own charge counters, read from the sacrificed source as it last was.
- **Changeling** ("every creature type") is `s.HasSubtype(card, type)`, asked one type at a time — every
  subtype question goes there (`Is.Subtype`, Kindred Judgment), never `s.Subtypes(…).Contains`, except behold
  (planeswalker types). Read from the card's own keywords: `s.Has` would loop through the statics' scopes.
- **"Choose a counter on [it]. Put another counter of that kind on it"** (Dathon) is `PutCounters { Kind = null }`:
  one kind is put unasked; several are a `ChooseKeywordChoice` of the kinds' names.
- **A Spacecraft that is never a creature** (Deep Space 9: station, then "9+ | Creatures you control get
  +3/+3") is `TrkCards.StationAbility` alone, no `StationComponent`; its N+ static is an `If` on its charge
  counters. "Exile the top N, put every permanent card onto the battlefield" is
  `RevealUntilOntoBattlefield { Top, Exile }` — the rest stay exiled.
- **"If you would draw a card, draw two instead"** (Mr. Spock, `NumericReplacement(Draw) { Times = 2 }`) is applied
  in `MagicState.Draw`, which every draw goes through — twice per copy (two Spocks: four). Bruvac's "they mill
  twice that many" is the same in `Mill.Top`, every mill.
- **A counted bound in a filter** ("mana value less than Sisay's power", Omnipresence's "≤ the number of creatures
  you control") is `StatBetween` with `Count` bounds (`Is.ManaValueAtMost(Count …)`), counted as the filter is asked
  with its controller and source — so a choice's filter passes the ability's source (`SourceOf.Context`). X reads 0
  there. "Cast from your hand without paying" is `MayCastFromHandFree(filter)`.
- **A player's hexproof and prevented damage** (The Needs of the Many) are `ThisTurnEffect`s on that player:
  `YouHaveHexproof` asked in `TargetSpec.IsLegal` (an opponent's spell or ability only), `PreventAllDamageToYou` in
  `Damage.Deal` before anything is dealt — so no lifelink, no "dealt damage" trigger.
- **An ability a card GAINS** (Nog, Ambitious Cadet: "gains 'You may play lands from your graveyard'"; Q's exiled
  card) is `UntilEndOfTurn.Gains` — a static or activated ability read by `s.AbilitiesOf<T>` beside the card's own,
  so every rule sees it; for good (`ModifyUntilEndOfTurn.ForGood`) it lasts until the card changes zones. A gained
  TRIGGER is `UntilEndOfTurn.Trigger`, as Lyra's. **"You may play lands from your graveyard"** with no "once each
  turn" is `MayPlayFromYourGraveyard { Unlimited }`, asked before a once-a-turn permission so it doesn't use one up.
- **Counters are one put and one remove** (MC-6.5): `PutCounters(What, N) { Kind }` (+1/+1 by default) and
  `RemoveCounters(What, N) { Kind }`, each through `MagicState.PutCounters`. **N is counted per object** with
  `Ref.It` the one being acted on — Bristly Bill's "double the number of +1/+1 counters on each creature" is
  `Count.CountersOn(Ref.It)`, Beckett's "remove those counters" `Count.CountersOfKind(Ref.It, kind)`. **A +1/+1 counter
  goes on any permanent** (CR 122.1): "if it's a creature" (Teyo, Shields Up!) is the card's own `If`. A remove COST is
  `RemoveCountersCost(N, Filter) { Kind }` — "from Mikaeus" is `(1, Is.Source)`, paid unasked when one permanent can.
- **A keyword counter** (CR 122.1b; Drivnod's indestructible counter, `CounterKinds.Indestructible`) is read by
  `Card.HasOwn`: a counter named as the keyword, lower case, gives it — so every rule that asks `s.Has` sees it.
