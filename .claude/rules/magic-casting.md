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
  player (Yuriko: `DuringCombat`), or a "this turn" effect in one player's command zone (Sen Triplets:
  `new ThisTurn(…) { On = Ref.Target(0) }`). It and **split second** (Samut) are `Combat.LockedDown(s, player)`,
  asked by casting and activating alike, never by triggers; a mana ability only by a lock with
  `ManaAbilitiesToo` (Sen) — `TapForManaAction` and `ManaPayment.Find`, so nothing taps to pay for them.
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
- **Abilities from the hand or graveyard** (landcycling, "exile this from your graveyard:") are
  `ActivatedAbilityComponent.From`; card costs (discard this / a card, exile this / another card)
  are chosen like a sacrifice, one legal action each. A finality counter exiles a creature instead
  of letting it die (`MoveCard`); `MagicState.Untap` is the ONE untap, so a stun counter eats it.
- **Countering is `StackResolution.Counter`** — a spell to its graveyard through MoveCard (flashback and
  prepared copies still hold), an ability removed, "can't be countered" untouched — or "instead" to its owner's hand
  (Remand) or library top (Memory Lapse): `Counter { To }`, only when it was countered. "Unless its
  controller pays" asks that player (`CounterUnlessPays`). **Ward** is a trigger whose "it" is the
  targeting spell or ability, carried as the event card of `BecameTargetEvent`.
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
  trigger (`RemoveTimeCounter`) and `Card.TimeCounters`.
- **Targeting an ability on the stack** is `TargetSpec.Ability(filter)` (`From.Ability`; a `StackAbility`,
  not a card); `Is.TriggeredAbility` / `Is.ActivatedAbility` read `StackAbility.Triggered`, set where a
  trigger goes on the stack. Countering one removes it (`StackResolution.Counter`).
- **Replicate** is `CastSpellAction.Replicate` (enumerated like X, in `MoveKey`): the cost grows by that many
  `ReplicateComponent` costs (a mana cost: Lose Focus' {U} adds a blue pip each time), and a triggered ability above the spell copies it that many times.
  **How many modes** is one `ModeChoice` (`Choose`) on spells, triggered and activated abilities alike — `AtLeast` and
  `AtMost` as Counts (an int converts: exactly that many); "choose one or more" is `{ AtMost = N }`, "up to one" (Ertai)
  `{ AtLeast = 0 }`. `Modal.Choices`/`IsLegal` take the range.
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
- **A distribution decided as it is cast** (Pure Energy, CR 601.2d) reads `TargetChosen(i)` — the targets
  as CHOSEN (`ContextKeys.ChosenTargets`); `Ref.Target` reads 0 for one gone illegal, which would hand
  its share to the others.
- **Kicker** is `KickerComponent(mana) { Costs }` and `CastSpellAction.Kicked` (one cast unkicked, one
  kicked; `|k` in `MoveKey` only when kicked): the mana joins the total in `Costs.Of(…, added:)`, the costs
  join `CastSpellAction.CostsOf(…, kicked)` — a kicker sacrifice is the ordinary `SacrificeId`. The stack
  keeps it (`OnStackComponent.Kicked`, copies too); "if kicked" is `WasKicked`; a target word that reads
  differently kicked is `TargetSpec.IfKicked`, swapped in by `StackResolution.TextOf(…, kicked)`.
- **"Activate only once each turn"** is `ActivatedAbilityComponent.OncePerTurn`, stamped on the card as it
  is activated (`Card.ActivatedOncePerTurnOn`) — a new object may activate again.
- **"If this is the second time this ability has resolved this turn"** is `IfNthResolutionThisTurn(n, …)`:
  counted on the source as it resolves (`Card.ResolvedTimes`, by turn), the rest run on exactly the Nth.
- **Playing a card from exile** is `PlayableFromExile(player, until)` on it — anyone's card, in its owner's
  exile (`Playable.IsImpulsePlayableBy`; `Candidates` reads both exiles): `AnyType` ("mana of any type",
  Vulcan Mind-Meld) and `Tax` (Zero's "{2} more") are read by `Costs.Of`. **"Exile these, they may be played" is ONE
  effect, `ExileTopPlayable`** (MC-6.9): the top N or `Until` a card matches (Exploratory Mission, The Caretaker:
  `!Is.Land`; discover: and mana value N or less), whose library (`Of`), `ShuffleFirst` (Q), face down, the rest to the
  bottom (`RestOnBottom`), or only a chosen card (`Choose`, Chekov's land). **What may be played is ONE `PlayPermission`**:
  who and how long (`By`, `ThisTurn`, "for as long as it remains exiled" `Forever`) — a mark on the card — or **`Now`**,
  a cast as it resolves and never a mark (Chandra; `Free`: The Caretaker, discover's `OrIntoHand`; Sisko's `AnyNumber`
  under `ManaValueAtMost`, counted once, CR 608.2h), with "if you don't" (`Otherwise`). Q's card `Gains` an ability
  instead. Cast or played, the card is public again (`ExiledFaceDown` is status).
- **"[Once during each of your turns,] you may play [cast] … from your graveyard / the top of your library [/ outside
  the game / exile / that player's hand]"** is ONE piece (MC-6.9), `MayPlayFromZone(From, filter) { OncePerTurn,
  Finality, OutsideTheGame, Of, AnyTime, If, AnyType }` — Overflow Submatrix, The Holodeck, Nog's gained lands, Zefram
  (`!Is.Land`: "cast"), Captain Pike, Null Summoner, Sen Triplets (a `ThisTurn`) — read by `Playable.ZonePermission`
  for casts AND land plays, during your turn unless `AnyTime`, never a face-down card, an unlimited rule first; a once-each-turn
  use stamps its permanent (`Card.PermissionUsedOn`). A graveyard cast this way is not
  flashback: the stack records `OnStackComponent.ByPermission`, so `MoveCard` doesn't exile it and it resolves
  onto the battlefield (`Finality`: with a finality counter). A card with its own way out of the graveyard uses
  that. `Candidates` looks in the sideboard only while such a permanent says "outside the game".
- **"The first spell you've cast this game"** is stamped as it is cast (`OnStackComponent.FirstSpellThisGame`,
  from `MagicPlayer.SpellsCastThisGame`), read by `FirstSpellYouCastThisGame` — a copy wasn't cast.
- **"You may cast this card from your graveyard / from exile"** is the card's own `MayCastThisFrom(From) { Finality }`
  (MC-6.9), read by `Playable.CanCastFromHere` — Storied Veteran (Graveyard) is not flashback: it isn't exiled leaving
  the stack, it resolves onto the battlefield (with a finality counter, `EnterFromStackAction.Finality`); Khan (Exile):
  your own exile, never face down (a face-down card has no abilities).
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
  `CastFromHandFree` (a choice among the castable, then `CastNow`).
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
