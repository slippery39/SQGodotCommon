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
  `NumericReplacement(ReplacedEvent)` — which event (damage, counters, draw, mill, tokens: Elspeth, Storm Slayer's
  "twice that many of those tokens", applied in `CreateTokens` and `CreateTokenCopy` to the player they are created
  under), filters for the source and
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
- **"They can't gain life for the rest of the game"** (Screaming Nemesis) is the same piece, `LifeTotalCantChange
  { GainOnly }` — `Life.Gain` refuses, `Life.Lose` and `Life.CanPay` don't — in a `ThisTurn { On, RestOfTheGame }`: the
  command-zone object carries `RestOfTheGame`, which the turn's end leaves; a player gets one, however often. **"If a
  player is dealt damage this way"** is `DealDamage.Then` with `Ref.ThisWay` what was actually dealt damage (nothing
  prevented) — `ThisTurn.On` takes only a player from it.
- **"Loses all abilities"** is `s.HasLostAllAbilities` — until end of turn (`UntilEndOfTurn.LosesAllAbilities`)
  or under an Aura (`EnchantedLosesAllAbilities`, found once per state with the statics). **"For as long as [this]
  remains on the battlefield"** (The Wondrous Wasp, Tishana's Tidebinder; MC-4.4e2e) is that Aura's piece with
  `Object`, the object stripped, stamped on the SOURCE by `ModifyUntilEndOfTurn { LosesAllAbilities,
  WhileSourceRemains }` — status, so it ends the moment the source leaves and a new object of it has none; it holds
  while the source has lost its own abilities (an effect's result, CR 611.2), and the source gone before it resolves
  makes nothing (CR 611.2b). It reaches
  every rule because every rule reads abilities through `s.AbilitiesOf<T>`, keywords through `s.Has`,
  mana through `ManaAbilities.ColorsOf` (a basic land's intrinsic ability included). What the AURA
  says ("doesn't untap", `DoesntUntap(Is.AttachedToSource)`) is the Aura's, so it still applies — unless the AURA
  loses its abilities (read with `AbilitiesOf` on the holder, MC-6.15). No timestamps: everything goes.
- **Auras and Equipment are one mechanism** (`Card.AttachedToId`, Rules/Attachments.cs). "Attach" is ONE effect,
  `Attach(What, To)` — equip's `(Ref.Source, Ref.Target())`, Thieving Skydiver's "attach it to this creature"
  `(Ref.Target(), Ref.Source)`. An Aura targets
  as it is cast (`AuraComponent`) and attaches on resolving; what either grants is a live static
  (scope `Is.AttachedToSource`), never stamped. "When enchanted creature dies" looks back through
  `DiedEvent.Attached` — the state check takes the Aura in the same pass.
  **An Aura on a card in a graveyard** (Animate Dead: "Enchant creature card in a graveyard", MC-4.4h5) attaches as any
  Aura: a host is a permanent or a card in its owner's graveyard (`Attachments.HostOf`, `Attach`, `Ref.EnchantedCreature`),
  the same object only while it stays there. **"It loses 'enchant A' and gains 'enchant B'"**, and Necromancy's "it
  becomes an Aura with 'enchant creature …'", is `ModifyUntilEndOfTurn { ForGood, Enchant }` — `UntilEndOfTurn.Enchant`,
  status, read in place of the `AuraComponent` by `Attachments.EnchantOf` (THE enchant: the state check, `CanEnchant`).
  "When this leaves the battlefield, that creature's controller sacrifices it" is `SelfLeaves` with
  `Sacrifice(Ref.EnchantedCreature)`, read from the Aura as it last was.
- **Excess damage** (CR 120.4a) is `Damage.Excess`, measured BEFORE the damage is dealt (past
  loyalty, or toughness less marked damage; 1 from deathtouch). `DealDamage.IfExcess` runs with X = it.
- **Copying a permanent** (CR 707.2) is `Card.WithCopiableValuesOf` — name, cost, types, colours,
  P/T, keywords and abilities, never its status (`Card.IsStatus`: counters, damage, what it was
  given; the same set a zone change strips). A token copy (`CreateTokenCopy`) is a blank token with
  those values, entering through `EnterBattlefield` so its own "enters" fires; a source that just died
  is copied as it last existed. **Its "except …"** (MC-4.4k2a) are options on `CreateTokenCopy`, applied over the copied
  values: Vaultborn Tyrant's "an artifact in addition to its other types" `AlsoType`; eternalize's "a 4/4 black Zombie
  [its types] with no mana cost" `SetBase`, `Colors`, `AlsoSubtypes` (before the original's), `NoManaCost` — beside
  `NotLegendary` and `AddingKeywords`. **Eternalize** (CR 702.129, Fanatic of Rhonas) is `LsvCards.Eternalize(cost)`: a
  sorcery-speed graveyard ability with `ExileSelfCost`, copying the card as it was there (the source snapshot). "Becomes a copy until end of turn" (Hall of Echoes) keeps the original
  in `CopyUntilEndOfTurn`, restored in cleanup AND as it leaves (`Card.Fresh`), so a dead Hall is a Hall.
- **A type change by an effect, for good** (Does Machines: "it becomes a 0/0 Robot creature in addition to its other
  types") is `ModifyUntilEndOfTurn { ForGood, AlsoType, SetBase, AlsoCreatureType }` — the card type stamped
  (`Card.WithTypesUntilItLeaves`, Assimilate's), so ForGood only: no cleanup undoes it.
- **A type change** is `BecomesType(scope) { Add, Remove, Subtype, Power, Toughness, If }` — Puppet Crafting
  (`Is.AttachedToSource`: "is a 5/5 Construct creature in addition"), Mogis (`Is.Source`, `Remove = Creature`,
  `If` devotion < 7). STAMPED on what the scope matches (`Card.AnimatedBy`, what it was kept in `Animated`) and
  put back by `Attachments.Check` once it doesn't, and by `Card.Fresh` as it leaves; stamped too in
  `EnterBattlefield` before the entry event (`Attachments.Retype`), so Mogis never enters a creature. Every
  rule reads `card.Is(Creature)`, so a live layer-4 read would need every one of them; the stamp is right by
  the time anyone gets priority. **Devotion** is `Count.DevotionTo(colours)`: a hybrid symbol of two counts once.
- **Reconfigure** (CR 702.151, Lion Sash, MC-4.4j5) is `LsvCards.Reconfigure(cost)`: equip's attach (`Is.Creature &
  Is.Yours & !Is.Source`), "unattach" `Unattach(Ref.Source)` with `OnlyIf = Matches(Ref.Source, Is.Attached)`, both
  sorcery speed, and "while attached, this isn't a creature" Mogis' `BecomesType(Is.Source & Is.Attached) { Remove =
  Creature }`. An Equipment that is a creature falls off (CR 301.5c), so `Attachments.Check` puts the type changes
  right BEFORE the fall-off: the attach resolves while it is still a creature, and the stamp is on by the time the
  fall-off is judged. Unattached, or its host gone, the stamp comes off and it is a creature again. `Attachments.
  Unattach` is THE unattach (the fall-off and the effect).
- **Ascend** (CR 702.131, Ocelot Pride, MC-4.4j4) is the printed keyword; the city's blessing is `MagicPlayer.CitysBlessing`,
  for the game, set by the state-based check for a player controlling ten or more permanents, one with ascend
  (`YouHaveTheCitysBlessing.Earned`). "If you have the city's blessing" is `YouHaveTheCitysBlessing` — the flag, or earned
  THIS moment (ascend is a static ability, CR 702.131b): the Cat token that makes ten gives it in time for the copies.
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
- **A card with only engine keywords and reminder text needs no script** (`CardRegistry.IsImplemented`): a line
  wholly in parentheses has no rules force (CR 207.2) — the original duals' "({T}: Add {W} or {U}.)" is their land
  types' mana (CR 305.6), made by the subtypes as a basic land's is. `tools/magic_set.py` mirrors the rule.
- **One card, a printing per set (MC-5.5).** `CardRegistry` is the one lookup across sets: a card's
  rules data and script exist once; its printing (`CardPrinting`: rarity, collector number) is per set,
  and a card in play carries its printing's set (`Card.Set`, kept through zone changes) — read for its
  ART only, never by a rule. A sealed or draft session passes its set (`CardRegistry.Get(name, set)`);
  boosters, ratings and drafts take a set code (`Boosters.Pack(set, rng)`, `CardRatings.Of(set)`,
  `Draft.New(set, seed)`). Rarity is never read from `CardData` — it isn't there.
- **Counters of every kind are put on and removed through `MagicState.PutCounters` alone** (`CounterKinds`):
  the named ones (TRK: quest, lore, promotion) in ONE map, `Card.Counters`; every kind put but loyalty (its own `LoyaltyCountersPutEvent`) raises `CountersPutEvent`
  (how many it had and has: Enterprise-D's "whenever charge counters are put on", Emperor of Bones' "whenever one or
  more +1/+1 counters are put on this creature" — read by `Trigger.Counters`, the kind, MC-4.4h2b); the older kinds
  (+1/+1, -1/-1, charge, stun, time, loyalty) in their own fields
  (`Card.CountersOf`/`WithCounters`). Finality is a yes/no flag, not a count. `CounterWritesTests` fails on
  an engine file that adds to a counter field itself — it would skip every replacement.
  "Enters with N [kind] counters" is `EntersWithCountersComponent(N) { Kind }` (+1/+1 unless said; Wishclaw Talisman's
  three wish counters, MC-4.4h2c), put through `PutCounters` as it enters.
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
  `PutCounters` (`Card.AllCounters`); `Optional` is a real "may" (Flesh Duplicate's stays forced). "Except it's an
  artifact in addition to its other types" (Phyrexian Metamorph) is `AlsoType`, a type added to the copy's.
  **A copy makes its own "as this enters" choices** (CR 707.5, MC-4.4g1b): colour, number, player and name are ONE
  method, `MagicState.ChooseAsItEnters`, reading the permanent as it is — `EnterBattlefield` asks it, or, for a card
  entering as a copy, `BecomeCopyOfChosenAction` after the copy (a Metamorph copying True-Name Nemesis chooses a player).
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
- **"If [a player] would draw a card, [something else] instead"** and **"can't draw"** are two options on that same
  `NumericReplacement(Draw)` (MC-4.4e2b), never a piece beside it: `Instead` — effects run in place of ONE card's
  draw, as the rule's controller (Hullbreacher `[CreateTreasure]`, Jace, Wielder of Mysteries `[WinTheGame]`; Narset,
  Parter of Veils' "can't draw more than one card each turn" is EMPTY, nothing instead) — and `If`, a condition
  judged from its holder's side with `Ref.It` the player drawing (Jace `YourLibraryIsEmpty`; Narset `AtLeast(
  Count.CardsYouDrewThisTurn with { Of = Ref.It }, 1)` — each "you … this turn" count reads another player's tally
  with `Of`). `MagicState.Draw` applies the numbers first, then asks `Replacements.InsteadOfDraw` for each card,
  BEFORE it looks at the library — so Jace wins and a draw Narset stops from an empty library loses nothing; a
  "can't" is found before any replacement (Narset beside Hullbreacher: no Treasure). A replaced card isn't drawn:
  no tally, no `DrewCardEvent`. The effects are spawned, so they run before the resolving ability's next step.
  Hullbreacher's "except the first one they draw in each of their draw steps" is `ExceptTheTurnsDraw`: the draw
  step calls `Draw(…, turnDraw: true)`, and only its FIRST card is the turn's draw — a second that Spock makes of
  it is any other draw (a ceiling, DesignNotes). An `Instead` is read on a permanent only, never in a
  `ThisTurnReplacement`.
- **A counted bound in a filter** ("mana value less than Sisay's power", Omnipresence's "≤ the number of creatures
  you control") is `StatBetween` with `Count` bounds (`Is.ManaValueAtMost(Count …)`), counted as the filter is asked
  with its controller and source — so a choice's filter passes the ability's source (`SourceOf.Context`). X is the
  SOURCE's X there (Thieving Skydiver's "target artifact with mana value X or less", MC-4.4e2c): its stack X, else
  the X it was cast for (`CastX`, CR 107.3m). "Cast from your hand without paying" is `MayCastFromHandFree(filter)`. "Total power and toughness 5 or less"
  (Cut Down) is the same filter over `Stat.PowerPlusToughness` (`Is.TotalPowerAndToughnessAtMost`).
- **A player's hexproof and prevented damage** (The Needs of the Many) are `ThisTurnEffect`s on that player:
  `YouHaveHexproof` asked in `TargetSpec.IsLegal` (an opponent's spell or ability only), `PreventAllDamageToYou` in
  `Damage.Deal` before anything is dealt — so no lifelink, no "dealt damage" trigger.
- **Protection** (CR 702.16, MC-4.4g1) is `Protection(From)`: a filter asked of the SOURCE (a spell, an ability's
  source, a creature dealing damage or blocking, an Aura or Equipment) from the holder's side — True-Name Nemesis'
  "from the chosen player" is `Is.ControlledByChosenPlayer`. ONE question, `Protection.Stops(s, holder, source)`, asked
  in each DEBT rule's one place: `Damage.Prevented` (given the source), `Attachments` (enchant, and the Equipment
  fall-off), `Combat.CanBlock`, `TargetSpec`. Read through `AbilitiesOf`: a grant until end of turn counts, losing all
  abilities ends it. "As this enters, choose a player" is `ChoosesPlayerAsItEnters` → `ChosenPlayer` (status).
  **"Gains protection from the color of your choice until end of turn"** (Mother of Runes, Giver of Runes, MC-4.4j2a)
  is Hungering Puppetbeast's `GainsChosenKeyword { Grants }` — each word's `Protection(Is.Color(c))` (Giver's
  `Is.Colorless` a sixth) through `ModifyUntilEndOfTurn.Gains`, the word only for the table, as Fear's; a fixed grant is
  `Gains = [Protection(Is.Creature)]` (Seasoned Dungeoneer).
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
- **Counters on a player** (CR 122.1; Galvanic Discharge's energy, Etali, Primal Sickness' poison — MC-4.4i4–i6) are
  `MagicPlayer.Counters`, put and removed through the same `MagicState.PutCounters` (given a player's id) — "you get
  {E}{E}{E}" is `PutCounters(Ref.You, 3) { Kind = CounterKinds.Energy }`, which puts on every player its Ref names too.
  No replacement or `CountersPutEvent` for a player yet (DesignNotes). Ten or more poison counters lose the game in the
  state-based check (CR 704.5c, `StateBasedActions`). **"You may pay any amount of [life / {E}]" is ONE piece,
  `PayAnyAmount { Kind, AtMost, Then }`** — a `ChooseNumberChoice` from 0 to what you have (life: never all of it), the
  amount paid, then `Then` with `Count.AmountPaid` — "deals that much damage", Necrodominance's "draw that many cards"
  (`AtMost`: the cards in your library). Never a "pay X, then do Y" piece per card. A FIXED payment (Guide of Souls:
  "you may pay {E}{E}{E}. When you do, …", MC-4.4j2b) is `May(AtLeast(Count.CountersOfKind(Ref.You, Energy), 3),
  [RemoveCounters(Ref.You, 3) { Kind }, WhenYouDo(…)])` — the count and the removal reach a player as `PutCounters` does.
- **A transforming double-faced card** (CR 712; Fable of the Mirror-Breaker, Etali, Primal Conqueror — MC-4.4i5–i6) is
  its front face, with its back as `TransformsInto(Face, Keywords, Abilities)` — `LsvCards.BackFace(card, abilities…)`,
  the back face's script. **`Card.Transform` is a copy stamp, as face-down's**: the back face's values, the front kept
  on `Transformed(Original)` (status), so leaving the battlefield turns it front face up (`Card.Fresh` → `FrontFaceUp`,
  CR 712.14), and a copy of it is the back face with nothing to transform into. "Transform [it]" is `Transform(ref)` —
  the same object, its damage and counters kept; "return it to the battlefield transformed" `MoveTo { Transformed }` →
  `EnterBattlefield(transformed:)`, stamped before the entry event (a Saga returned transformed is no Saga). A token copy
  transforms (a transforming token); a permanent under a copy effect never does (Phyrexian Metamorph, Hall of Echoes:
  CR 701.28c). Each face's colours are Scryfall's (`CardFace.Colors` — the back's colour indicator: Etali, Primal Sickness
  is red and green; the front's when the card has none at the top); a back face has mana value 0 (DesignNotes).
  **A transforming token** (Sunfall's incubate, MC-4.4j2b: `LsvCards.Incubate(x)`, an Incubator with "{2}: Transform
  this token" becoming a 0/0 Phyrexian artifact creature) carries `TransformsInto` among `CreateTokens.Abilities`.
  **"When this transforms into [it]"** (Revealing Eye, MC-4.4i7) is `TriggerEvent.SelfTransforms` on the back face —
  `TransformedEvent`, raised by the `Transform` effect alone (entering transformed isn't transforming). **An ability of a
  permanent transforms it only if it hasn't transformed since it was put on the stack** (CR 701.28f: Cecil, Dark Knight's
  second trigger, Etali activated twice) — `Transform` compares the source snapshot's face with the permanent's.
  **A modal double-faced card** (CR 712.12; Sink into Stupor // Soporific Springs) is `BackFace(…) with { Modal = true }`:
  never transformed (`Card.Transform` refuses either face up), its back face a land played as `PlayLandAction { BackFace }`
  — offered beside the front's casts (`MagicRules`, `Playable.Candidates`: a land permission from a graveyard plays it),
  judged and entering as that face (`Card.ModalBackFace` → `BackFaceUp`, the copy stamp), front face up once it leaves.
- **A keyword counter** (CR 122.1b; Drivnod's indestructible counter, `CounterKinds.Indestructible`; Guide of Souls'
  `Flying`) is read by
  `Card.HasOwn`: a counter named as the keyword, lower case, gives it — so every rule that asks `s.Has` sees it.
