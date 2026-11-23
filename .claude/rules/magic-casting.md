---
paths:
  - "MagicCore/MagicRules.cs"
  - "MagicCore/Actions/*.cs"
  - "MagicCore/Targeting/*.cs"
  - "MagicCore/Rules/Playable.cs"
  - "MagicCore/Rules/Prepare.cs"
  - "MagicCore/Sets/*.cs"
  - "MagicCore/Sets/*/*.cs"
  - "MagicCore/Effects/*.cs"
  - "MagicCore.Tests/Sets/*.cs"
---

# MagicCore — casting, activating, the stack and resolving

The real-rules engine's per-mechanic rules for modes, targets, alternative and free casts, casting from other zones, countering, copies of spells, kicker and replicate, planeswalker abilities — each the ONE way that mechanic works
here. The rules that hold on every task are in `MagicCore/CLAUDE.md`. One file per subsystem (DEV-4):
`magic-casting.md`, `magic-triggers.md`, `magic-combat.md`, `magic-mana.md`, `magic-zones.md`, `magic-statics.md`, `magic-ai.md`; a card script or card test loads all but the AI's. A new rule goes in the one file
whose subsystem it lives in.

- **Planeswalkers:** enter with printed loyalty (`EnterBattlefield`), loyalty abilities are sorcery
  speed and once per turn per permanent, damage removes loyalty, 0 loyalty is a state-based death,
  and the defending player's planeswalkers are attack options. Empower Jace creates and loads the
  token in ONE resolution, so a fresh token never meets the check at 0.
- **Modes (CR 700.2):** a modal text is its `Modes` + `Choose`. `Modal.Compose` is the ONE place the
  chosen modes become target words and steps, in printed order; each mode's effects see only its own
  targets (`Target(0)` is that mode's first). A spell's modes are chosen when cast (`CastSpellAction.
  Modes`), a trigger's as it goes on the stack, an activated ability's as it is activated
  (`ActivateAbilityAction.Modes`, one legal action per mode — Tea, Earl Grey, Hot; never two abilities
  with one cost: TRK's dilemma counts mode choices). A mode with an unchoosable required target can't be
  chosen. The scripted mode count must match the printed bullets — `Scripting.Printed` throws otherwise.
- **"You can't cast this unless …"** is `CastOnlyIf`, read by `Playable.CanCastFromHere` — never offered.
  **"Behold a … or pay {N}"** is `CastSpellAction.BeholdId` (a card you control, or in hand, not this
  one); nothing moves.
- **Copying a spell** is `CopySpell` (CR 707.10): a `Card.IsCopy` on top of the stack, controlled by
  the copier, not cast (no "whenever you cast"), its targets chosen anew with the original's among the
  options — and gone once it leaves the stack (the state-based check prepared copies use). "When you
  next cast an instant or sorcery this turn" is a delayed trigger kept in the command zone
  (`CopyNextInstantOrSorcery`, a `ThisTurnEffect`), taken by that cast and carried on its `SpellCastEvent`.
  Replace a component with `WithComponentReplaced` — `WithComponent` ADDS, and `GetComponent` reads the first.
- **"Can't cast spells or activate abilities"** is one piece, `CantCastOrActivate` — on a permanent for every
  player (Yuriko: `If = new DuringCombat()`), or a "this turn" effect in one player's command zone (Sen Triplets:
  `new ThisTurn(…) { On = Ref.Target(0) }`). Its options (MC-4.4d2b) say whose (`By`, from its holder: Voice of
  Victory's "your opponents"), when (`If`, judged as its holder's: "during your turn"), which of the two (`Spells`,
  `Abilities`), after how many spells (`SpellsEachTurn = 1`, Archon of Emeria's "can't cast more than one spell
  each turn" — the asking PLAYER's count this turn) and of which sources (`Of`, Clarion Conqueror's "of artifacts,
  creatures, and planeswalkers": the spell, or the PERMANENT whose ability it is — never a card in a hand or
  graveyard). It and **split second** (Samut) are `Combat.LockedDown(s, player, what, spell)`, asked by casting and
  activating alike with the card cast or the ability's source, never by triggers; a mana ability only by a lock
  with `ManaAbilitiesToo` (Sen; the Conqueror: no Mox, no Elf, a land still taps) — `TapForManaAction`, and
  `ManaPayment.Find`, which takes the player's locks once (`Combat.Locks`) and asks `Combat.Stops` of each source,
  so nothing locked taps to pay.
- **"You may activate [its] loyalty abilities any time you could cast an instant"** is `LoyaltyAtInstantSpeed(filter)`,
  the filter asked of the planeswalker (MC-4.4d2b): the player's in a `ThisTurn` (Jace's Machinations:
  `Is.Subtype("Jace")`), or the planeswalker's own (The Wandering Emperor: `Is.EnteredThisTurn` — "as long as [she]
  entered this turn"). `ActivateLoyaltyAction` reads both; still once a turn for each planeswalker.
- **"You may play cards from that player's hand this turn"** (Sen Triplets) is the play-from-a-zone rule
  (`MayPlayFromZone(Hand, Is.AnyCard) { Of }`) in a `ThisTurn` — your command zone, read by `Playable.ZonePermission`
  beside your permanents' rules (cast, land, `Candidates`): the whole zone while it lasts, a card drawn later too. A
  `ThisTurnEffect` naming a player by a `Ref` keeps who it resolved to (`MayPlayFromZone.OwnerId`).
- **"This spell can't be countered" for one cast** (Theorist's Proxy) is `OnStackComponent.CantBeCountered`
  — gone with the stack object, so a recast can be countered; `StackResolution.CanBeCountered` reads both.
- **Casting from the graveyard (flashback):** `Costs.Flashback` is the ONE answer to "may this card
  be cast from its graveyard, and for what" (its own flashback, or its mana cost when it gained
  flashback this turn); `Costs.Of` starts from it, so taxes still apply. The stack records where a
  spell came from (`OnStackComponent.CastFrom`), and `MoveCard` exiles a flashback spell whenever it
  would leave the stack — resolved, fizzled or bounced (CR 702.34a).
- **Prepare** (Secrets of Strixhaven's mechanic; FRA reuses it) lives in `Rules/Prepare.cs`. The card is
  a creature in every zone; its spell half (`PrepareSpellComponent`, the second face) is never cast
  from hand. Becoming prepared puts a real COPY of the spell in exile (`Card.IsCopy`, linked to the
  creature's id AND incarnation); its controller may cast it at the spell's own timing while that
  same creature is still prepared, and casting it unprepares the creature. A copy anywhere but the
  stack ceases to exist, unless it is a live prepared copy in exile (state-based, CR 704.5e).
- **Adventures** (CR 715; Brazen Borrower // Petty Theft, MC-4.4g5) are `AdventureComponent(Face, Text)` (the second
  face, `LsvCards.Adventure`) and `CastSpellAction.Adventure` — every cast of the card offered again as the Adventure
  (`|a` in `MoveKey`), from anywhere but exile. The card BECOMES the Adventure for the cast, a copy stamp as face-down's
  (`Card.AsAdventure` → `CastAsAdventure(Original)`, status): the action validates and casts the stamped card as any
  other, so cost, timing, targets and "noncreature spell" all read Petty Theft; `PutOnStack` re-stamps it after the
  move (whose `Fresh` undoes it — `WithoutAdventure`). Resolved, `FinishSpellAction` exiles it with its caster's
  `PlayableFromExile { CastOnly }` (forever): the creature castable from there; fizzled (`Resolved = false`) or
  countered, it goes to the graveyard as the creature.
- **Abilities from the hand or graveyard** (landcycling, "exile this from your graveyard:") are
  `ActivatedAbilityComponent.From`; card costs (discard this / a card, exile this / another card)
  are chosen like a sacrifice, one legal action each. A finality counter exiles a creature instead
  of letting it die (`MoveCard`); `MagicState.Untap` is the ONE untap, so a stun counter eats it.
- **Countering is `StackResolution.Counter`** — a spell to its graveyard through MoveCard (flashback and
  prepared copies still hold), an ability removed, "can't be countered" untouched — or "instead" to its owner's hand
  (Remand) or library top (Memory Lapse): `Counter { To }`, only when it was countered. "If an ability of a
  [permanent] is countered this way, that permanent …" (Tishana's Tidebinder) is `Counter.Then` with `Ref.ThisWay`
  what was countered — a spell's card, an ability's SOURCE while it is still that object on the battlefield; a delayed
  trigger's (`StackAbility.Delayed`, set by `PutOnStack`) is never the card it sits on (CR 603.7d), so none. "Unless its
  controller pays" asks that player (`CounterUnlessPays`). **Ward** is a trigger whose "it" is the
  targeting spell or ability, carried as the event card of `BecameTargetEvent`. "Ward—Pay 3 life" (Sedgemoor Witch,
  MC-4.4h2d) is `CounterUnlessPays { Costs = [PayLifeCost(3)] }` — a fixed life cost is paid by choosing (`LoseLife`),
  asked only while the payer has the life (`Life.CanPay`).
- **A target is a zone and a filter** (`TargetSpec(From, Filter)`): the zone says where candidates
  come from, the filter which of them (`Is.…`), and `TargetSpec` alone adds the targeting rules —
  hexproof on the battlefield, and off it a spell or ability never targets its own card. Targets in a
  graveyard are `TargetSpec.InYourGraveyard` (or `InAnyGraveyard`, "from a graveyard") — the same
  legality test on choosing and resolving. A COST that picks (sacrifice, exile from your graveyard) is
  not targeting: a bare `Filter` over the chooser's own zone (`filter.Among(…)`, in `FilterChoices`).
  "One or two target creatures" is two words, the second `Optional` and `NotThePrevious`.
- **"You may … If you do"** is `May` (a `YesNoChoice`, asked only when it can be done) or
  `MaySacrifice`; both clear the shared "chosen" key so a "no" cannot read a stale "yes". "Its owner
  may" is `May.Who` — another player answers, the effects stay yours — and "if they didn't" is `Otherwise`.
- **"Without paying its mana cost"** (Omnipresence) is `CastSpellAction.WithoutPayingManaCost`, offered
  and checked by `Playable.FreeCosts`: X is 0, and `Costs.Of(…, free: true)` still adds
  taxes and additional costs. **An alternative cost on the card** (Flare of Fortitude: "sacrifice a nontoken
  white creature rather than pay") is the same `MayCastFromHandFree(Is.Source) { Costs }`, from wherever it is
  cast but never with flashback (two alternative costs don't combine, CR 118.9a) — only `Is.Source` counts on the
  card itself, so Omnipresence's static is no free cast for itself; its costs join `CastSpellAction.CostsOf`, and
  a free cast with a cost is offered BESIDE the paid one
  (one with none replaces it — never worse). Its options (MC-4.4): `If` (Snuff Out's "if you control a Swamp", Force
  of Negation's "if it's not your turn", judged as it is cast), `Mana` (Baleful Mastery's {1}{B} — paid instead, and
  stamped `OnStackComponent.AlternativeCost`, read by `AlternativeCostWasPaid`; a copy has it too, CR 707.10), and in
  `Costs` Force of Will's `PayLifeCost(1)` and `ExileFromHandCost(blue)` (`CastSpellAction.ExileId`), Daze's
  `ReturnToHandCost(Island)` (beam me up's cost, its filter an option). `Playable.FreeCast` is the one answer.
- **A counted life payment** is `PayLifeCost(Count)`, fixed for the cast before anything is paid (`PayLifeCost.For`,
  from `CastSpellAction.CostsOf`: `Ref.Source` the card, `Count.X` its X). **"As an additional cost, pay X life"**
  (Toxic Deluge, MC-4.4h2c) chooses X with no {X} in the cost (`CastSpellAction.XInCosts`): one cast per X up to the
  caster's life, and X is never added as mana (`XIsMana`). **"Cast spells from the top of your library … pay life equal
  to its mana value rather than pay its mana cost"** (Bolas's Citadel) is `MayPlayFromZone { Free, Costs }`: the
  permission's cast is `WithoutPayingManaCost`, its `Costs` the free cast's (`Playable.FreeCast`), never added to a
  paid cast, and the paid cast from there is refused; a land is played as ever.
- **"[This cost] or [that]"** is `AdditionalCost.Or` (MC-4.4) — Silence the Echo's "or pay {3}" (`PayManaCost`),
  Bitter Triumph's "discard a card or pay 3 life", Bone Shards' "sacrifice a creature or discard a card": the cost's
  slot picking nothing (id 0) pays its `Or`. `CostList.Paid` is the one list validation, payment and the mana owed
  read; a cast picking both halves or neither is never offered (`PicksOnlyWhatIsPaid`) nor valid.
- **A card exiled for a linked ability** (Null Summoner) carries `ExiledBy(source, incarnation)`; "you may cast the
  exiled card" is the play-from-a-zone rule on the source, `MayPlayFromZone(Exile, Is.ExiledBySource) { Of, AnyTime,
  If, AnyType }` — the filter asked from the holder's side, so only while that same object is on the battlefield
  (CR 607.2a). "Mana of any type can be spent" is `ManaCost.OfAnyType` in `Costs.Of`.
- **"−X" loyalty** is `LoyaltyAbilityComponent.MinusX`: one legal action per X up to the loyalty, X on
  the stack for the effects. "Activate only if" is `LoyaltyAbilityComponent.OnlyIf`.
- **Casting in the middle of a resolution** ("you may cast that card", Chandra, Torch of Defiance;
  Uldaros' free copies) is `CastNow`: a real `CastSpellAction` with `DuringResolution` — the engine's
  own cast candidates, checked by `ValidateAdd` (no priority or timing; modes, targets, X, additional
  costs all still apply), the player picking one — or none: an empty answer casts nothing (TBL-2.16; the options
  stay as they were, a record's offers fingerprint hashes them) — then `Cast`, through `PutOnStack` — so "whenever you
  cast" sees it. A copy of a permanent spell resolves into a token.
- **An activated ability an Aura grants** ("enchanted creature has '{T}: …'", Splinter Twin) is
  `GrantsActivatedAbility` on the Aura. `s.ActivatedAbilitiesOf(card)` is THE list activation indexes
  into — own, then granted — read by the candidates, the action and `Describe`; never
  `AbilitiesOf<ActivatedAbilityComponent>` for activation, or a granted ability is offered and refused.
  It is not copiable: a token copy of the host has none. A delayed trigger on a new token
  (`CreateTokenCopy.ExileAtNextEndStep`) is added AFTER it enters — entering strips one given before.
- **A permanent spell resolves in two steps** (`EnterFromStackAction`, then `ActivePlayerGetsPriorityAction`),
  so a choice it makes as it enters is answered before anyone gets priority — and before the state
  check sees Flesh Duplicate as a 0/0. **"Enter as a copy"** (`MayEnterAsCopy`) is that choice, then
  `BecomeCopyOfChosenAction`: its copiable values, with `EnteredAsCopy(original)` restored as it leaves
  (`Card.WithoutCopyEffect`), never in cleanup. **Vanishing** is `Vanishing.Of(n)`: the marker, an upkeep
  trigger (`RemoveTimeCounter`) and `Card.TimeCounters`. **Fading** (Parallax Wave, MC-4.4j1) is `LsvCards.Fading(n)`,
  composed: `EntersWithCountersComponent(n) { Kind = Fade }` and an upkeep `If(a fade counter, remove one) { Otherwise =
  sacrifice }`; "remove a fade counter:" is `RemoveCountersCost(1, Is.Source) { Kind }`.
- **Targeting an ability on the stack** is `TargetSpec.Ability(filter)` (`From.Ability`; a `StackAbility`,
  not a card); `Is.TriggeredAbility` / `Is.ActivatedAbility` read `StackAbility.Triggered`, set where a
  trigger goes on the stack. Countering one removes it (`StackResolution.Counter`).
- **Replicate** is `CastSpellAction.Replicate` (enumerated like X, in `MoveKey`): the cost grows by that many
  `ReplicateComponent` costs (a mana cost: Lose Focus' {U} adds a blue pip each time), and a triggered ability above the spell copies it that many times.
  **Squad** (Sicarian Infiltrator, MC-4.4f) is the same cast with `ReplicateComponent { AsItEnters }`: nothing is
  copied as it is cast; the count rides the spell (`OnStackComponent.Replicate` → `Card.CastAs`), and
  `Scripting.Squad(cost)`'s "when this enters, if its squad cost was paid" makes `CreateTokenCopy(Ref.Source) { N =
  Count.TimesReplicated(Ref.Source) }`. A token copy wasn't cast, so its squad makes none.
  **Multikicker** (CR 702.33c, Batroc the Leaper, MC-4.4i2b) is that payment with nothing made of it,
  `Scripting.Multikicker(cost)`: the card reads `Count.TimesReplicated(Ref.Source)` — "enters with a +1/+1 counter for
  each time" is `EntersWithCountersComponent(that count)` (its context has `Ref.Source`, the card entering). Paid at
  least once, it was kicked (CR 702.33d: `ReplicateComponent.Kicker` stamps `OnStackComponent.Kicked`, so `WasKicked`
  reads it). **"Each of up to X targets, where X is …"** is `TargetSpec.PerX` — a `Count`, this word that many times,
  each `NotThePrevious` (`Optional`: "up to"): a trigger's counted as it triggers (`Triggers.QueueTargets`, one
  `ChooseTargetChoice` per word; a required one with fewer objects than X removes the trigger), a spell's its X
  (Dominion Infiltration, Pest Infestation: `Count.X` — another count THROWS), an activated ability's its X too
  (Candelabra of Tawnos: "{X}, {T}: Untap X target lands", MC-4.4l3c) — `ActivateAbilityAction.ChoosesTargets`: ONE
  legal action per X with no targets in it, refused unless X lands can be chosen; activating asks
  `ChooseXTargetsChoice { Ability }` (exactly X), then `ActivateWithChosenTargetsAction` activates with them, before
  anything is paid. Never one activation per set of lands (2^n); an activation naming its own X targets is refused.
  A spell's and an activation's X words are ONE expansion, `TargetSpec.XWords(x, of)`.
  **Storm** (CR 702.40, Brain Freeze, MC-4.4g7) is `Scripting.Storm`: a "when you cast this spell" trigger
  (`TriggerEvent.SelfCast`) of `CopySpell(Ref.EventCard) { N = Count.SpellsCastBefore(Ref.EventCard) }`. The count is
  EVERY player's spells this turn, stamped as it is cast (`OnStackComponent.SpellsCastBefore`, from both tallies), so a
  spell cast in response isn't one; countered first, it is still copied — a `SelfCast` trigger keeps the spell as it was
  cast (its `EventSnapshot`), and the count and `CopySpell` read that once it is gone. Never a replicate cast: nothing is paid.
  **Delve** (CR 702.66, MC-4.4g2: Treasure Cruise, Dig Through Time — read from the printed keyword, `s.Has(card,
  "Delve")`) is `CastSpellAction.Delve = N`: one cast per number, at most the generic in the TOTAL cost (taxes in) and
  the other cards in your graveyard; the total less N generic; the N cards picked AFTER the cast, one at a time
  (`ExileFromGraveyardCost.PickAfter`, Drivnod's path — N = 1 too, never the cost's one-pick slot). Never a cast per
  set of cards: C(20, 7) is 77,520 casts. **The AI's picks are a rule, never searched** (Shayne, 2026-11-17):
  `ChooseAmongChoice.Delve`, answered by `GreedyAi` with the first card left in the graveyard and never branched by the
  beam — a cheap procedural pick is MC-4.4g2b (DesignNotes).
  **How many modes** is one `ModeChoice` (`Choose`) on spells, triggered and activated abilities alike — `AtLeast` and
  `AtMost` as Counts (an int converts: exactly that many); "choose one or more" is `{ AtMost = N }`, "up to one" (Ertai)
  `{ AtLeast = 0 }`. `Modal.Choices`/`IsLegal` take the range.
- **"Choose three. You may choose the same mode more than once"** (Fiery Confluence, Mystic Confluence) is
  `ModeChoice { Repeats }`: `Modal.Choices` offers each multiset once (10 of three modes), `IsLegal` accepts repeats,
  `Compose` runs a mode once per time chosen — and marks a repeated mode's target word `NotThePrevious`, never the
  same target twice (Mystic can't counter one spell twice: DesignNotes). **A spell's is ONE cast per choice of modes,
  its target words chosen one at a time as it is cast** (MC-4.4e2f): `CastSpellAction.ChoosesTargets` with `EachWord`
  (the text's `Repeats`) — a `ChooseTargetChoice` per word, as a copy's are retargeted, never a cast per set: "return
  target creature" three times was 220 casts against 12 creatures, Mystic Confluence 576 with three spells on the stack.
- **Spree** (CR 702.172, Three Steps Ahead, MC-4.4g8) is a modal spell whose modes have a cost each, `Mode.Cost` — read
  from the printed "+ {1}{U} — …" by `Scripting.Printed`, never retyped — added to the cast's total for each mode chosen
  (`CastSpellAction.CostBeforeLife`, beside replicate's; paid on a free cast too). "Choose one or more" is `{ AtMost = 3 }`.
  A spree text is `EachWord` as the Confluences' (Shayne, 2026-11-18): 7 casts, never one per spell × permanent of yours
  (199 with three spells on the stack). The other modal spells still enumerate until MC-8.
  **Escalate** (CR 702.120, Collective Brutality, MC-4.4h2a) is `SpellEffectsComponent.Escalate`, a `DiscardCost` (the
  cube's only escalate; a mana or sacrifice escalate makes it any `AdditionalCost` with a count) paid once per mode beyond the first: `CastSpellAction.CostsOf` adds it `with { Count = modes − 1 }` (`DiscardCost.Count`
  — the cards picked AFTER the cast in one choice, as delve's, never a cast per card; `CanPay` refuses a cast with too
  few other cards in hand). The cast generator never sees it, so a cast with more modes than the hand can pay for is
  offered and refused by `ValidateAdd`.
- **"Target artifact a player controls and target artifact card in that player's graveyard"** (Goblin Welder) is the
  second word `TargetSpec.InAnyGraveyard(…) { OfThePreviousTargetsController }` — its card's owner controls the target
  before it (`TargetSpec.FitsPrevious`), asked by `AreLegal` and again on resolution, where a control change makes it
  illegal. "If both targets are still legal" is `If(Both(Matches(Target(0), …), Matches(Target(1), …)))`: an illegal
  target reads as nothing.
- **Escape from a permanent's rule** (Underworld Breach: "each nonland card in your graveyard has escape … its mana
  cost plus exile three other cards from your graveyard") is `MayPlayFromZone(Graveyard, !Is.Land) { AnyTime, Costs }`
  — the permission's costs join `CastSpellAction.CostsOf` for the player it lets cast (never a cast during a
  resolution); `ExileFromGraveyardCost(!Is.Source) { Count = 3 }` is picked after casting, as Drivnod's. Not flashback:
  `ByPermission`, so the spell goes back to the graveyard and may escape again.
- **"During any turn you [condition], you may cast that card"** (Robber of the Rich) is `PlayPermission { Forever, If,
  CastOnly }` → `PlayableFromExile.If`, judged by `Playable.IsImpulsePlayableBy` with the player it names as "you"
  (the exiled card's own controller is its owner, the opponent); `CastOnly` never lets a land be played.
- **Beam me up** (TRK) is a `FlashbackComponent` whose cost beside the mana is `ReturnToHandCost(Is.Creature)` — return a
  creature you control (`CastSpellAction.ReturnId`, one cast per creature) — so it is cast and exiled as
  flashback is (`TrkCards.BeamMeUp`). Marooned's `EnchantedCantBeBeamedUp` takes its creature out of the
  candidates.
- **Face a dilemma** (TRK) is `FacedDilemmaEvent`, ONE however many modes, raised where modes are chosen:
  a cast (`PutOnStack`, a cast during a resolution too), an activation with `Modes`, and a modal trigger's
  `ChooseModesChoice` (`QueueModalTriggerAction`) — whether or not it resolves. Copies choose nothing, and "up to
  one" with none chosen faces none (Ertai; with none choosable the trigger is removed). All three
  go through `FacedDilemmaEvent.Face`, which also counts it for the turn (`TurnTally.DilemmasFaced`,
  `Count.DilemmasFacedThisTurnBy`: Sisko's Officers, The Caretaker's "if you faced a dilemma this turn").
- **"N divided as you choose among any number of targets"** (Pyrokinesis' 4 damage; Pure Energy's two counters among
  one or two, CR 601.2d) is ONE piece, `TargetSpec.Divide = N` (MC-4.4c2e): `TextOf` makes it N words, all but the
  first optional and different, and each target's share is `Count.Share` with `Ref.It` — `DealDamage(Ref.EachTarget,
  Count.Share)`, `PutCounters(…)`. The shares are announced as it is cast (`CastSpellAction.Shares` →
  `OnStackComponent.Shares`, empty in the action when the number of targets leaves one way) and read by the target's
  place among the targets as CHOSEN, so one gone illegal takes its share with it. Past two it is ONE cast
  (`ChoosesTargets`): `ChooseXTargetsChoice` (one to N), then — two or three targets — a `ChooseKeywordChoice` of the
  ways (`Division.Splits`); never a cast per set and split (1,365 against 12 creatures). Pure Energy's two stay casts.
  **A triggered ability divides too** (Fury: "4 damage divided as you choose among any number of target creatures
  and/or planeswalkers", MC-4.4i2a): `Triggers.QueueTargets` makes the same words (`Division.Words`), a
  `ChooseTargetChoice` each, and `PutTriggerOnStackAction` asks the split (`Division.Ask`) after them — the shares on
  `StackAbility.Shares`, read as a spell's.
- **Kicker** is `KickerComponent(mana) { Costs }` and `CastSpellAction.Kicked` (one cast unkicked, one
  kicked; `|k` in `MoveKey` only when kicked): the mana joins the total in `Costs.Of(…, added:)`, the costs
  join `CastSpellAction.CostsOf(…, kicked)` — a kicker sacrifice is the ordinary `SacrificeId`. The stack
  keeps it (`OnStackComponent.Kicked`, copies too); "if kicked" is `WasKicked`; a target word that reads
  differently kicked is `TargetSpec.IfKicked`, swapped in by `StackResolution.TextOf(…, kicked)`.
  **Teamwork** (We Say Thee Nay!, MC-4.4g6) is kicker by another name — any optional additional cost is — `LsvCards.
  Teamwork(n)`: `KickerComponent(ManaCost.None) { Costs = [TapUntappedCost { TotalPower }], Word = "teamwork" }`; "if
  this spell was cast using teamwork" is `WasKicked`. `Word` is only the cast's description ("(teamwork)").
  **Buyback** (Corpse Dance, MC-4.4h2b) is the same: `KickerComponent("{2}") { Word = "buyback" }`, and the spell's last
  step `If(WasKicked, MoveTo(Ref.Source, Hand) { From = [Stack] })` — `FinishSpellAction` leaves a spell that is no
  longer on the stack where it went.
  **"Kicker {X}. X can't be 0"** (Thieving Skydiver, MC-4.4e2c) is `KickerComponent("{X}") { MinX = 1 }`: the KICKED
  cast chooses X (one per X from `MinX`; unkicked, X is 0), paid as any X and carried onto the permanent as
  `Card.CastX`. A permanent's "if it was kicked" reads its spell, `WasKicked` the stack's, the permanent's, or the
  source as it last was. **What a permanent remembers of the spell it was is ONE field, `Card.CastAs`** (MC-4.4f) —
  the `OnStackComponent` it resolved from, carried by `EnterFromStackAction.Spell` into `EnterBattlefield(spell:)`:
  its X (`CastX` reads it), kicked, the alternative cost (evoke), squad's payments. `Card.AsCast` is the one read, the
  stack's or the permanent's; never a new `Cast…` field beside it.
- **Evoke** (Subtlety; Solitude, Endurance, Fury, Grief) is `Scripting.Evoke(colour)`: Force of Will's alternative
  cost on the card, `MayCastFromHandFree(Is.Source) { Costs = [ExileFromHandCost(colour)] }` (one cast per card
  exiled), and "when it enters, if its evoke cost was paid, sacrifice it" — an intervening if on
  `AlternativeCostWasPaid`, read on the permanent (`Card.CastAs`), so a flickered one stays.
  **Warp** (CR 702.185, Quantum Riddler, MC-4.4g9) is `Scripting.Warp(cost)`: Baleful Mastery's alternative mana cost
  `{ From = [Hand] }` (the zones the card's own alternative cost is cast from — the CASTER's: never another's hand
  under Sen Triplets; empty: anywhere), and `PaidAtNextEndStep` — effects put on the permanent as a delayed trigger when
  that cost was paid, controlled by the spell's controller (CR 603.7d), set up in `EnterFromStackAction` as it resolves, never a "when it enters" (nothing to answer, CR 702.185a). Warp's is `MoveTo(Ref.Source, Exile)` then
  `MakePlayable(Ref.ThisWay, { By = its owner, Forever, LaterTurn, CastOnly })` — `LaterTurn` stamps
  `PlayableFromExile.FromTurn`, the next turn, read by `Playable.IsImpulsePlayableBy`. From exile it costs its mana cost.
  **Dash** (CR 702.109, Ragavan, Death-Greeter's Champion, MC-4.4i2b) is `Scripting.Dash(cost)`: warp's shape, its
  `PaidAtNextEndStep` `MoveTo(Ref.Source, Hand) { From = [Battlefield] }`, and haste a static `If =
  AlternativeCostWasPaid` (read on the permanent, `Card.CastAs`: a new object of it wasn't dashed).
  **Backup N** (CR 702.165, Death-Greeter's Champion) is `Scripting.Backup(n, keywords)`: a `SelfEnters` trigger,
  `PutCounters(target, n)`, then `If(Matches(Target(0), !Is.Source))` each keyword until end of turn — keywords only
  (DesignNotes).
  **"You may cast this spell as though it had flash. If you cast it any time a sorcery couldn't have been cast, …
  sacrifices it at the beginning of the next cleanup step"** (Necromancy, MC-4.4h5) is `CastAsThoughItHadFlash(effects)`:
  read by `Priority.AtInstantSpeed`, the cast stamped `OnStackComponent.NotAtSorcerySpeed` (judged before it moves to the
  stack), and the effects a delayed trigger at `Beginning(Step.Cleanup)` set up as warp's is — the cleanup step raises
  `StepBeganEvent` (CR 514.3a: something triggered, so players get priority there, then another cleanup).
  **Overload** (CR 702.96, Damn, MC-4.4h3) is `Scripting.Overload(cost)`: Baleful Mastery's alternative mana cost with
  `Overload` — cast for it (`CastSpellAction.PaysItsAlternativeCost`, the candidates' `Playable.OwnAlternativeCost`), the text has
  NO target words (`StackResolution.TextOf(…, alternative)`; a copy's too, from the stack's `AlternativeCost`),
  so hexproof and protection never stop it and nothing fizzles. "Each" is the script's: `If(AlternativeCostWasPaid,
  [the each version]) { Otherwise = [the target version] }` — never an effect rewritten by the engine. One cast overloaded.
  **Bestow** (Springheart Nantuko, MC-4.4k3) is the mirror, `MayCastFromHandFree { Mana, Enchant }`: cast for it the
  spell GAINS a target word, one cast per creature (magic-statics.md, "Bestow").
  **Splice** (CR 702.47, Goryo's Vengeance, MC-4.4h4) is `Scripting.SpliceOntoArcane(cost)` (`SpliceComponent(Cost,
  Onto)`, a filter asked of the spell) and `CastSpellAction.Splice` — the ids of cards in the caster's hand, one cast per
  set (`|s` in `MoveKey`). The cast is validated and made on the card stamped `Spliced` (the texts and costs, status — an
  Adventure's shape): the costs join the total as kicker's, the cards are revealed and stay in the hand, and
  `StackResolution.TextOf` appends each text after the spell's own, its targets scoped as a mode's (`ScopeTargetsAction`:
  Goryo's `Target(0)` is its own word; the spell's own text scoped to its own words, so its "each target" isn't Goryo's). `PutOnStack` re-stamps it after the move; leaving the stack takes it, a copy keeps it.
  Through the Breach takes it with red.
  **Emerge** (CR 702.119, Crabomination, MC-4.4h8) is `Scripting.Emerge(cost, filter)`: Flare of Fortitude's sacrifice and
  Baleful Mastery's mana as ONE alternative cost on the card, `{ LessTheSacrificed }` — that mana less the mana value of
  the card the cast's `SacrificeId` names, generic only (`CastSpellAction.CostBeforeLife`); one cast per permanent to sacrifice.
  **Miracle** (CR 702.94, Metamorphosis Fanatic, MC-4.4h9) is `Scripting.Miracle(cost)`: a trigger read from the HAND
  (`TriggeredAbilityComponent.From`) on the card's own draw, the first this turn (`Nth = 1`), whose `CastOneOf(Ref.Source)
  { Instead }` casts it as it resolves paying that cost rather than its mana cost (`CastNow.Instead` →
  `CastSpellAction.ManaInstead`, beside `WithoutPayingManaCost`) — at any timing, as every cast during a resolution; the
  card moved out of the hand first is a new object, and nothing is cast.
  **"Its owner puts it on their choice of the top or bottom of their library"** is Clash of Elements' `May { Who =
  Ref.OwnerOfTarget(), Otherwise }` over two `MoveTo(…, Library) { From = [Stack] }`.
- **"Choose target [card] in your graveyard. You may cast that card this turn"** (Emry, Lurker of the Loch) is
  `MakePlayable(Ref, PlayPermission)`: the permission's mark (`PlayableFromExile`) on a card left where it is — a
  graveyard or exile, REPLACING one left from an earlier turn; `Playable.IsImpulsePlayableBy` reads it in both. A cast
  from a graveyard by it (`Playable.MarkedInGraveyard`) is `ByPermission`, never flashback (a card with flashback casts
  by that) — not exiled, a permanent spell enters — and no permanent's zone permission: Underworld Breach's cost
  isn't added, a once-each-turn rule isn't used up. A resolution's cast keeps its own exile (Bilbo). **"If that spell
  would be put into your graveyard, exile it instead"** (Jace, Telepath Unbound's −3, MC-4.4i7) is `PlayPermission {
  ExiledLeavingTheStack }` → the mark's own, so the cast is not `ByPermission`: flashback's exile (`MoveCard`).
- **Channel** (Boseiju, Who Endures; Otawara, Soaring City — MC-4.4l3c) is `LsvCards.Channel(cost, targets, effects)`:
  cycling's shape (`From = Hand`, `DiscardSelfCost`) with the ability's OWN `CostChange` — "costs {1} less to activate
  for each legendary creature you control" is `Count.NumberOf(…).Negated`, read by `Costs.OfAbility` as the activating
  player's (the card is in a hand). An ability word: no keyword, and it is not cycling.
- **"Activate only once each turn"** is `ActivatedAbilityComponent.OncePerTurn`, stamped on the card as it
  is activated (`Card.ActivatedOncePerTurnOn`) — a new object may activate again.
- **"If this is the second time this ability has resolved this turn"** is `IfNthResolutionThisTurn(n, …)`:
  counted on the source as it resolves (`Card.ResolvedTimes`, by turn), the rest run on exactly the Nth. "…, [do
  this] instead" (Scythecat Cub, MC-4.4k1: one +1/+1 counter, or on the second time double them) is its `Otherwise`,
  run on every other resolution — and when the source has left.
- **Playing a card from exile** is `PlayableFromExile(player, until)` on it — anyone's card, in its owner's
  exile (`Playable.IsImpulsePlayableBy`; `Candidates` reads both exiles): `AnyType` ("mana of any type",
  Vulcan Mind-Meld) and `Tax` (Zero's "{2} more") are read by `Costs.Of`. **"Exile these, they may be played" is ONE
  effect, `ExileTopPlayable`** (MC-6.9): the top N or `Until` a card matches (Exploratory Mission, The Caretaker:
  `!Is.Land`; discover: and mana value N or less), whose library (`Of`), `ShuffleFirst` (Q), face down, the rest to the
  bottom (`RestOnBottom`), or only a chosen card (`Choose`, Chekov's land). **What may be played is ONE `PlayPermission`**:
  who and how long (`By`, `ThisTurn`, "for as long as it remains exiled" `Forever`, Inti's "until your next end step"
  `UntilYourNextEndStep`) — a mark on the card, `Free` on it too (Urza's "until end of turn, you may play that card
  without paying its mana cost": `PlayableFromExile.Free`, answered first by `Playable.FreeCast`) — or **`Now`**,
  a cast as it resolves and never a mark (Chandra; `Free`: The Caretaker, discover's `OrIntoHand`; Sisko's `AnyNumber`
  under `ManaValueAtMost`, counted once, CR 608.2h), with "if you don't" (`Otherwise`). Q's card `Gains` an ability
  instead. **`Play = null`** leaves no permission at all, for its `Then` to decide (Black Widow, Super Spy, MC-4.4h2d:
  "You may put a +1/+1 counter on Black Widow. If you don't, you may cast the exiled nonland card until end of turn and
  mana of any type can be spent" — a `May { Otherwise = MakePlayable(Ref.ThisWay, { ThisTurn, AnyType, CastOnly }) }`;
  `CastOnly` keeps the lands exiled on the way unplayed). Cast or played, the card is public again (`ExiledFaceDown` is status). Tersa Lightshatter's "exile a card at
  random from your graveyard" is the same effect `{ From = Graveyard, AtRandom }`; "if you exiled a land card this way"
  (Bonehoard Dracosaur) is its `Then`, with `Ref.ThisWay` the cards exiled. Crabomination's "target opponent exiles the
  top card of their library, a card at random from their graveyard, and a card at random from their hand. You may cast a
  spell from among cards exiled this way without paying its mana cost" (MC-4.4h8) is three of them `{ Of, Play = null,
  Linked }` — `Linked` stamps each `ExiledBy` this object of the source, so three zones make one group,
  `Ref.All(Exile, Is.ExiledBySource, Ref.Target())` — then `CastOneOf` over it: an opponent's card, cast from their exile
  under your control.
- **"[Once during each of your turns,] you may play [cast] … from your graveyard / the top of your library [/ outside
  the game / exile / that player's hand]"** is ONE piece (MC-6.9), `MayPlayFromZone(From, filter) { OncePerTurn,
  Finality, OutsideTheGame, Of, AnyTime, If, AnyType }` — Overflow Submatrix, The Holodeck, Nog's gained lands, Zefram
  (`!Is.Land`: "cast"), Captain Pike, Null Summoner, Sen Triplets (a `ThisTurn`) — read by `Playable.ZonePermission`
  for casts AND land plays, during your turn unless `AnyTime`, never a face-down card, an unlimited rule first; a once-each-turn
  use stamps its permanent (`Card.PermissionUsedOn`). A graveyard cast this way is not
  flashback: the stack records `OnStackComponent.ByPermission`, so `MoveCard` doesn't exile it and it resolves
  onto the battlefield (`Finality`: with a finality counter). A card with its own way out of the graveyard uses
  that. `Candidates` looks in the sideboard only while such a permanent says "outside the game". **"If you do, it
  gains '[triggered ability]'"** (Serra Paragon, MC-4.4d3) is its `Gains`, carried as `Finality` is — a spell's on
  `OnStackComponent.Gains`, a land's by `PlayLandAction` — into `EnterBattlefield(gains:)`: an `UntilEndOfTurn
  { Trigger }` that no turn ends, so the permanent has it until it leaves. "A land … or a permanent spell with mana
  value 3 or less" is one filter (`Is.Type(PermanentTypes) & Is.ManaValueAtMost(3)`: a land's is 0), asked of the
  card as the SPELL it would be — `Playable.ZonePermission(…, x)`, from the cast's validation: X counts on the
  stack (CR 202.3e), so an X spell is offered only with an X that keeps it within the bound.
- **"The first spell you've cast this game"** is stamped as it is cast (`OnStackComponent.FirstSpellThisGame`,
  from `MagicPlayer.SpellsCastThisGame`), read by `FirstSpellYouCastThisGame` — a copy wasn't cast. Asked BEFORE the
  cast (Once Upon a Time: "…, you may cast it without paying its mana cost" — `MayCastFromHandFree(Is.Source) { If }`,
  MC-4.4k2b), of a card not yet on the stack, it reads the player's count: no spell cast this game.
- **"You may cast this card from your graveyard / from exile"** is the card's own `MayCastThisFrom(From) { Finality }`
  (MC-6.9), read by `Playable.CanCastFromHere` — Storied Veteran (Graveyard) is not flashback: it isn't exiled leaving
  the stack, it resolves onto the battlefield (with a finality counter, `EnterFromStackAction.Finality`); Khan (Exile):
  your own exile, never face down (a face-down card has no abilities). **Escape** (From the Catacombs: "Escape—{3}{B}{B},
  Exile five other cards from your graveyard", MC-4.4h2e) is that permission with `Costs` — joined by
  `CastSpellAction.CostsOf` for a cast from the graveyard by its player (never during a resolution, nor by a mark), the
  five picked after the cast as Underworld Breach's; not flashback, so the sorcery goes back to the graveyard.
- **An activated ability's event card** is the creature tapped for its cost (Station's "its power") or, with
  none tapped, the one sacrificed (Gowron: "if you sacrificed a Klingon this way" — `Matches(Ref.EventCard, …)
  { AsItLastWas }`, its snapshot from before the cost: a sacrificed token has ceased to exist by then).
- **"Choose one. If [condition], choose one or more instead"** (Captain Kirk) is
  `ModeChoice.AnyNumberIf`, judged as the modes are chosen (the trigger going on the stack).
- **"X target [things]"** (Dominion Infiltration) is `TargetSpec.PerX`: `StackResolution.TextOf(…, x)` makes it
  X words, each `NotThePrevious` (`AreLegal` refuses any repeat in the chain). There is ONE legal cast per X
  (`CastSpellAction.ChoosesTargets`): casting it asks `ChooseXTargetsChoice` (exactly X), then casts with them —
  never a cast per set, which is 2^n and hung the fuzz probe (Shayne, 2026-11-05). The AI copies the biggest
  (`GreedyAi`). "For each of them" is `Ref.EachTarget`.
- **"You may cast that card [free]"** as it resolves (`PlayPermission.Now` on `ExileTopPlayable` — Chandra, The
  Caretaker, discover: `TrkCards.Discover`) is a yes/no, then `CastNow`; asked only when it can be cast.
  **"Cast any number of spells with mana value ≤ N from among the exiled cards, free"** (Sisko, `Now { AnyNumber }`)
  is Uldaros' choose-and-cast loop (`CastCopiesWithinBudget`) with `Originals` (the cards, linked while it runs, not
  copies) and `EachAtMost` (a cap on each, not a total); N counted once as it resolves.
  **"Copy that card X times; cast the copies free"** (Infinite Diversity) is `CastCopiesOf`: one copy at a time
  (`CastChosenCopy.MakeCopy`), each asked — a copy not cast ceases to exist (CR 704.5e).
- **"During your main phase, each card in your graveyard has beam me up for its mana cost"** (Scotty) is
  `GraveyardSpellsHaveBeamMeUp`, answered by `Costs.Flashback` (the ONE graveyard-cast answer): an instant or
  sorcery, its owner's main phase, `ReturnToHandCost { ExceptId }` (never returning Scotty). One cast per spell ×
  creature returned, as every beam-me-up card (Shayne, 2026-11-06).
- **"That player may exile it. If they don't, you may cast a noncreature spell from your hand free"** (Brazen
  Bluff) is `May { Who, Otherwise }` with `MoveTo(…, Exile) { From = [Stack] }` (a card on the stack, to its owner's exile) and
  `CastOneOf(Ref.All(Hand, !Is.Creature, Ref.You))` (a choice among the castable, then `CastNow`).
- **"You may cast one of [these cards]" as it resolves is ONE piece, `CastOneOf(Ref) { Free, Exiled }`** (MC-4.4e2d):
  the cards a `Ref` names — Bluff's hand, Bilbo's "an artifact, instant, or sorcery spell from your graveyard"
  (`Free = false`: paid), Malcolm's "the discarded card" (`Ref.Chosen` in `Discard.Then`) — chosen (or none), then
  `CastNow`. **A cast from a graveyard as an effect resolves is by that effect's permission (CR 608.2g), never
  flashback:** `OnStackComponent.ByPermission`, so it goes back to the graveyard and a permanent spell enters —
  unless the effect exiles it: Bilbo's "if an instant or sorcery spell cast this way would be put into your
  graveyard, exile it instead" is `Exiled = Is.Instant | Is.Sorcery` → `CastSpellAction.ExiledLeavingTheStack`,
  flashback's exile. Such a cast pays the mana cost, never a flashback cost (`Costs.Of(…, flashback: false)`), and never
  uses up a permanent's once-each-turn zone permission. **"You may cast any number of spells from among [them]"**
  (Etali, Primal Conqueror, MC-4.4i6: each player's top cards until a nonland, `ExileTopPlayable { Of, Until, Linked,
  Play = null }` twice) is `CastOneOf { AnyNumber }` — asked again after each cast, among what is left, until declined.
- **"Choose one that hasn't been chosen"** (Kobayashi Maru Scenario) is `ModeChoice.NotChosenBefore`:
  the modes chosen are stamped on that object (`Card.ModesChosen`, a bit per mode, in `QueueModalTriggerAction`) and
  left out by `Triggers.ChoosableModes`; none left and it is removed (CR 700.2a); a new object starts afresh.
  **"You lose the game"** is `LoseTheGame`, at once, as `WinTheGame`.
- **"That player may … If they do, they …"** (Peace in Our Time) is `May { Who = Ref.ActivePlayer, ThenAsThem }`:
  Then runs as the one who answered, so their `ThisTurn` (`CantAttackThisTurn`, read by `Combat.CanAttack`) is in
  THEIR command zone; `Ref.Source` is still the enchantment.
- **An ability activated from exile** (Q, Stacking the Deck's gift: "Activate only during your turn and only if this
  card is exiled") is `ActivatedAbilityComponent.From = ZoneType.Exile` — your own exile, which `LegalActions`
  reads beside the hand and graveyard. "Exile a card from your hand:" is `ExileFromHandCost` (one action per card).
  "Create a copy of this card. You may cast the copy" is `CastCopiesOf { PayCost }` — `CastNow` paying its costs.
  Q's "That nonland card gains …" is `ExileTopPlayable { ShuffleFirst, Until, Gains }` (in place of a permission).
- **Cycling is a keyword ability** (MC-6.7): `Scripting.Cycling(cost)`; landcycling is `Cycling(cost, search)`, the
  same keyword (CR 702.29e). The ability carries it (`ActivatedAbilityComponent.Keyword`), the activation event too,
  and "when you cycle [this card]" is `Trigger(AbilityActivated) { Ability = "Cycling", Subject = Is.Source }`, read
  from the graveyard (the card was discarded to pay). A discard-from-hand ability with no keyword isn't cycling.
- **"You may play that card"** is one `PlayPermission` (MC-6.9; above) — on
  `ExileTopPlayable.Play` and on `SearchLibrary.Play`: Thada Adel searches THEIR library (`SearchLibrary.Of`; the
  choice is the searcher's, `ChooseFromLibraryChoice.DeciderId`), exiles the card and may play it this turn.
- **"A modal spell"** is `Is.Modal` (its text has modes); **"the number of times you chose a mode for that spell"**
  (Riku of Many Paths) is `Count.ModesChosen(Ref.EventCard)`, read from its `OnStackComponent`. A trigger's
  `ModeChoice` is counted with the TRIGGER's context (`Triggers.ContextOf`: its source and event card), so
  `AtMost = Count.ModesChosen(Ref.EventCard)` is "choose up to X".
- **MC-4.4m2a's options** (multicolour; tests in `LsvBatch2mTests.cs`):
  - **"If that spell is countered this way, exile it instead"** (No More Lies) is `CounterUnlessPays { To }` — `Counter.To`'s
    option, on both ways it counters (unpaid, or unpayable).
  - **"Each opponent can cast spells only any time they could cast a sorcery"** (Teferi, Time Raveler) is
    `CantCastOrActivate { By = Opponent, Abilities = false, ExceptAtSorcerySpeed }`: the lock binds only while the player
    it binds could not cast a sorcery NOW (`Priority.AtSorcerySpeed` for THEM, never the holder's `If`) — a cast during a
    resolution is locked too. His +1, **"until your next turn, you may cast sorcery spells as though they had flash"**, is
    Necromancy's `CastAsThoughItHadFlash([]) { Of = Is.Sorcery }` in a `ThisTurn { UntilYourNextTurn }` — read by
    `Priority.AtInstantSpeed` from the caster's command zone, `Of` asked of the card.
  - **"Choose two —" with its targets chosen one at a time** (Kolaghan's Command, Shayne 2026-11-22) is `ModeChoice {
    EachWord }` → `CastSpellAction.EachWord`: one cast per pair of modes (6), never per pair of targets (644). MC-8 makes
    it every modal spell's.
  - **"X damage divided as you choose among any number of target creatures"** (Fire Covenant) is `TargetSpec.Divide`
    as a `Count` — a number, or `Count.X` (any other count throws, `Division.Total`). An X division's targets are one
    `ChooseXTargetsChoice`, then each target's share a `ChooseNumberChoice { Min = 1 }` in turn (`Division.AskShares`,
    `TakeShareAction`), the last taking the rest — never one choice of the ways (75,582 at X = 20 over 12). `Division.
    Fits` checks shares by sum, never by enumerating. X = 0 is refused (CR 601.2d).
  - **Cascade** (CR 702.85, Bloodbraid Challenger) is `LsvCards.Cascade`: discover's `ExileTopPlayable { Until = !land &
    mana value < this spell's, Play = Now { Free }, RestOnBottom }` on a `SelfCast` trigger. **Plot** (CR 702.170, Pillage
    the Bog) is `LsvCards.Plot(cost)`: warp's exile and `PlayPermission { Forever, LaterTurn, Free, CastOnly }` on a
    sorcery-speed ability `From = Hand` — it uses the stack (a ceiling, DesignNotes).
  - **Escape for a cost of its own** (Phlage: "Escape—{R}{R}{W}{W}, Exile five other cards") is `MayCastThisFrom {
    Mana, Costs }` — `Costs.Of` pays `Mana` for its owner's cast from the graveyard (never flashback, never an effect's
    cast). **"Sacrifice it unless it escaped"** is `When(SelfEnters, Sacrifice) with { If = Not(Escaped) }`: the cast
    stamped `OnStackComponent.Escaped` (a graveyard cast paying an escape's costs — its own or Underworld Breach's), read
    on the permanent through `Card.AsCast`, as evoke's `AlternativeCostWasPaid`.
- **MC-4.4m2b + m2c's options** (multicolour; tests in `LsvBatch2m2Tests.cs`):
  - **Airbend** (CR 701.65, Aang, Swift Savior: "Exile it. While it's exiled, its owner may cast it for {2} rather than
    its mana cost") is `LsvCards.Airbend(ref)`: `MoveTo(…, Exile) { From = [Battlefield, Stack] }` whose `Then` is
    `MakePlayable(Ref.ThisWay, { By = its owner, Forever, CastOnly, Instead = {2} })` — `PlayPermission.Instead` →
    `PlayableFromExile.Instead`, the mana `Costs.Of` charges in place of the card's mana cost (taxes still apply), and
    X is 0 for that cast (CR 107.3b: `CastSpellAction` refuses any other).
  - **A loyalty ability's targets chosen one at a time** (Oko, Thief of Crowns' −5) is `LoyaltyAbilityComponent.
    EachWord` → `ActivateLoyaltyAction.ChoosesTargets`: ONE legal action with no targets, a `ChooseTargetChoice` per
    word as it is activated, then `ActivateLoyaltyWithChosenTargetsAction` validates and activates with them — nothing
    paid if they aren't legal. An exchange of control is two `GainControl` under an `If(Both(Matches, Matches))`: both
    targets still legal, or nothing (CR 701.12a).
  - **"You may activate [its] loyalty ability two more times this turn"** (Comet, Stellar Pup) is `ActivateLoyaltyAgain
    (ref, n)` — `Card.ExtraLoyaltyActivations`, each activation after the turn's first taking one; "may it activate
    now" is ONE question, `Card.LoyaltyUsedOn(turn)` (the action, the evaluator's lifetime price and the rollout ask
    it — never `LoyaltyActivatedOnTurn == turn`). A die row's "[+2]" is `PutCounters(Ref.Source, 2) { Kind = Loyalty }`.
  - **Exhaust is stamped per ability** (Loot, the Pathfinder has three): `Card.ExhaustUsed` is a bit per activated
    ability (its index) and `ManaAbilityComponent.ManaAbilityBit` for a mana ability's.
