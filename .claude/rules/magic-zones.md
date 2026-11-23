---
paths:
  - "MagicCore/MagicState.cs"
  - "MagicCore/Zones/*.cs"
  - "MagicCore/Rules/Phasing.cs"
  - "MagicCore/Sets/*.cs"
  - "MagicCore/Sets/*/*.cs"
  - "MagicCore/Effects/*.cs"
  - "MagicCore.Tests/Sets/*.cs"
---

# MagicCore — zones and moving cards

The real-rules engine's per-mechanic rules for entering the battlefield, tokens, destroying and dying, discarding, hidden information, the sideboard, tapping, control, phasing — each the ONE way that mechanic works
here. The rules that hold on every task are in `MagicCore/CLAUDE.md`. One file per subsystem (DEV-4):
`magic-casting.md`, `magic-triggers.md`, `magic-combat.md`, `magic-mana.md`, `magic-zones.md`, `magic-statics.md`, `magic-ai.md`; a card script or card test loads all but the AI's. A new rule goes in the one file
whose subsystem it lives in.

- **Discarding is `MagicState.Discard`** — effects, costs (cast or ability) and the cleanup alike: one
  `DiscardedEvent` per CARD (MC-4.4i2a: Ivora's "whenever you discard a card" triggers twice for two, as `DrewCardEvent`;
  "one or more cards" — Tinybones, Inti — is `Trigger.OneOrMore`, `TriggerEvent.APlayerDiscards`), counted in
  `TurnTally.CardsDiscarded`. A chosen
  card going to the graveyard is a discard only from the hand (`MoveChosenToGraveyardAction`; surveil
  isn't). "When you discard this card" triggers `From = Graveyard`, where the card now is. Something that
  triggers in the cleanup (a discard to hand size) gets priority THERE, then another cleanup step
  follows (`TurnStructure.FinishCleanup`, CR 514.3a) — never carried into the next turn.
- **Every token is made by `CreateTokens`** — so a replacement on tokens (Draconic Visitor's "artifact
  tokens are Dragons instead"; Holo-Engineer's ANY token an Illusion, `ArtifactTokensBecome.AnyToken`) is
  applied in that one place (`CreateTokens.Instead`, copies too), under whoever `ControlledBy` names. **"The exiled
  card's owner creates an X/X … token, where X is [its] mana value"** (Skyclave Apparition, MC-4.4d3) is `ControlledBy
  = Ref.OwnerOf(ref)` — the owner of the cards a Ref names; `Ref.OwnerOfTarget(i)` is `OwnerOf(Target(i))` — and
  `CreateTokens.Size`, one `Count` for power and toughness, counted as it resolves.
- **A card put onto the battlefield from anywhere enters through `MagicState.EnterBattlefield`** —
  a search, a return, a token: never a bare `MoveCard` to the battlefield, which skips its entry
  (controller, tapped, its "enters" event). `MoveChosenToAction` routes a battlefield search there.
- **Hexproof** is checked in `TargetSpec.IsLegal` through `s.Has` (an opponent's spells and abilities
  only; mass effects never target); **shroud** (CR 702.18, Sylvan Safekeeper's grant, MC-4.4k2a) beside it — every
  player's, its controller's too. **Destroying is `MagicState.Destroy`** — the Destroy effect and the
  state-based check's lethal damage and deathtouch alike: indestructible (`s.IsIndestructible`) is asked
  there, and Dr. McCoy's replacement ("instead remove all +1/+1 counters … damage healed",
  `CountersRemovedInsteadOfDestruction`) applies there. Several at once are JUDGED together before any
  leaves (CR 701.7) — a wrath doesn't end an indestructible grant halfway, nor kill McCoy before the creature
  he saves — then leave in the battlefield's order. 0 toughness isn't destruction and still kills.
- **"If a card would be put into your graveyard from anywhere this turn, exile that card instead"** (Yawgmoth's Will,
  MC-4.4h2c) is Necrodominance's `ExileInsteadOfYourGraveyard` in a `ThisTurn` (a `ThisTurnEffect`, read beside the
  permanents' in `MoveCard`), `What = !Is.Token` — "a card"; Necrodominance's "a card or token" sets none. Its "you may
  play lands and cast spells from your graveyard" is `ThisTurn(MayPlayFromZone(Graveyard, Is.AnyCard))`, Sen's shape.
  **"If a card would be put into an opponent's graveyard from anywhere, instead exile it with a void counter on it"**
  (Dauthi Voidwalker, MC-4.4h2e) is the same piece `{ Whose = Is.Opponents, What = !Is.Token, CounterKind }`: whose
  graveyard asked of the card's OWNER from the holder's side (unset: yours), so `MoveCard` reads both battlefields; the
  counter is put on in exile through `PutCounters`. With several, the owner picks (CR 616.1): one without a counter first.
- **"Exile it instead" of dying** is decided in `MoveCard` alone: a finality counter, or Essence Burn's
  `UntilEndOfTurn.ExileInsteadOf = [Graveyard]` — so the card never dies and no "dies" trigger sees it. **"If that
  creature would leave the battlefield, exile it instead of putting it anywhere else"** (From the Catacombs, MC-4.4h2e)
  is the same list, every other zone, for good (`ModifyUntilEndOfTurn { ForGood }`): a bounce exiles it too.
- **"If you cast it"** is `Card.WasCast`, set by `EnterBattlefield(cast: true)` from a resolving spell.
- **Who knows a hidden card is `Card.KnownTo`** (`Knowers`, relative to the owner — story AI-3): in a
  library, who knows its position; in a hand, whether the opponent saw it. `MoveCard` sets it on every
  move — out of a public zone into a hand or library is `Everyone`; into its library the owner knows
  where it went; into a hand only the opponent's knowledge counts, and only from the top (a draw — a
  search takes it face down) — and `Shuffle` clears the library's. Anything else that LOOKS or REVEALS
  adds with `s.MakeKnown`: surveil's look, a search's `Reveal` (set it whenever the text says "reveal
  it"), "reveals their hand", behold from the hand (a cost — Theorist's Sanctum's behold as it enters
  is a condition and reveals nothing, a known gap), and **"look at [cards]" is ONE piece, `LookAt(Ref)`** (MC-4.4l3b;
  it was `LookAtHand(player)`): the cards a Ref names — "target opponent's hand" `Ref.All(Hand, Is.AnyCard, target)`
  (V'Ger; the table logs what you now know there, showing it in the hand is TBL-6), "the top card of target player's
  library" `Ref.All(Library, …) { Top }` (Mishra's Bauble), "a card at random in target player's hand" `{ AtRandom }`
  (Urza's Bauble) — each known to the one who LOOKED: its owner for their own card, else the owner's opponent; never a
  flag per zone. **Exiled face down**
  (`ExileTopPlayable.FaceDown`, Vulcan Mind-Meld) is `ExiledFaceDown` + `KnownTo` (who looked), stamped by
  `ExiledFaceDown.On(card, knownTo)` alone; a linked exile's `ExiledBy` by `ExiledBy.ByTheSource` (this object of the
  source only while it is still the one the ability came from); every front
  end asks `Card.IsHiddenFrom(viewer)` and draws a back. **Zefram's "look at the top card any time"** is
  read, never stamped — `MagicState.TopIsKnownToOwner` (the top changes on every path). A random order (Identity Echo's bottom) is NOT
  known. A new card that looks at or reveals a hidden card must do the same, or the search's "shuffle
  what isn't known" reshuffles a card its player has seen.
- **Mill is counted, read and repeated** (MC-4.4l3b): `Mill(who, N)` with N a `Count` (Palantír of Orthanc: "you mill X
  cards, where X is the number of influence counters"), `IfMilled` run with `Ref.ThisWay` the cards milled ("that player
  loses life equal to the total mana value of those cards": `Count.TotalManaValueOf`), and `RepeatWhile` — Grindstone's
  "If two cards that share a color were milled this way, repeat this process": `TwoShareAColor(Ref.ThisWay)`, judged of
  the cards JUST milled, so it ends with the library.
- **Face down from a hand** (Memory Jar: "Each player exiles all cards from their hand face down") is `MoveTo(…, Exile)
  { FaceDown }`: `ExiledFaceDown`, as known as it was in the hand — and a face-down exiled card moved back to a hand or
  library was never public, so `MagicState.KnownAfterMoving` keeps who knew it (never "everyone saw it").
- **Look and take's other options** (MC-4.4l3b): `ChosenBy` (Karn, Scion of Urza: "An opponent chooses one of them" —
  the choice's `DeciderId`), `Rest = TheRest.Exile` with `RestCounter` ("exile the other with a silver counter on it";
  his −1 finds it by `Is.WithCounter`), and, taken to EXILE, `{ FaceDown, Linked }` — hideaway (Shelldock Isle): the
  card hidden from all but you and linked (`ExiledBy`), so "the exiled card" is `Ref.All(Exile, Is.ExiledBySource)`,
  played by `MakePlayable(…, { ThisTurn, Free })` (its timing a ceiling, DesignNotes).
  **"Becomes a copy of target permanent card in your graveyard"** (Shifting Woodland) is `BecomeCopyUntilEndOfTurn` with
  a graveyard target: the model is the card wherever it is.
- **"Target player reveals their hand. You choose a [kind of] card from it"** is ONE piece, `YouChooseTheyDiscard(who,
  Types) { Filter, Optional, ExileInstead, UntilSourceLeaves, … }` (MC-4.4h2a): `Types` matches ANY of its types, so
  Duress' "noncreature, nonland" and Inquisition of Kozilek's "nonland … mana value 3 or less" are a `Filter` (an
  artifact creature is a creature). Deep-Cavern Bat's "you may exile a nonland card from it until this creature leaves
  the battlefield" is `{ Optional, UntilSourceLeaves }` → `ExileUntilSourceLeaves(Ref.Chosen)`: a permanent OR a card in
  a hand, each stamped with the zone it left (`ExiledUntilLeaves.From`) and returned there by `MoveCard` as the source
  leaves — a hand card to its owner's hand, never onto the battlefield.
- **"From outside the game"** (Extrapolate the Impossible) is the `Sideboard` zone — Bo1 limited's
  unplayed pool. It exists only in a game given one, through `MagicRules.WithSideboard` (NewGame's
  `sideboard1/2`, and tests), made after both decks: a game without keeps every id it had —
  the golden replays and the position suite's move tokens hold ids. `CardsIn` of a missing sideboard
  is empty. A card leaving it was revealed, so `MoveCard` makes it known to everyone.
- **Randomness** (a discard at random, a random order, Mana Crypt's coin flip: `FlipACoin`, "Name Sticker" Goblin's
  d20: `RollDie(sides, rows)` — a number read against a table of `DieRow`s, never a flip with sides, CR 705/706) takes `s.TakeRng()`, so a seeded game replays.
  A search for several cards may take two of a name unless `SearchLibrary.DifferentNames` (Fblthp).
  A spell that exiles itself as it resolves (Sphinx's Approach) is left there by `FinishSpellAction`.
- **"If a nontoken creature would enter and it wasn't cast, exile it instead"** (Containment Priest) is
  `PermanentsEnter(Is.Creature & !Is.Token) { ExiledUnlessCast }`, decided at the top of `EnterBattlefield` — before
  the move, so it goes to its owner's exile and nothing of the entry happens (no event, no counters, no stamp). Every
  caller of `EnterBattlefield` may find the card NOT on the battlefield after it: check before acting on "it entered"
  (`MoveTo` and `Assimilate` do; the rest are a ceiling, DesignNotes).
- **"Second from the top" / "just beneath the top X cards"** (Oust, Unexpectedly Absent) is `MoveTo { Beneath = N }`
  (a `Count`; 0 is the top, a shorter library the bottom): the card goes on top, then the N cards that were above are
  put back over it with `MoveObjectToFront` — a reorder, so each stays as known as it was. **"It's an enchantment.
  (It's not a creature.)"** on a return (Enduring Innocence) is `MoveTo { Loses = Creature }`: stamped once it has
  entered, as assimilate's artifact type (an `Animated` with no source), undone as it leaves. **"If it entered under
  your control"** after a move is `MoveTo.Then` with `Ref.ThisWay` the cards that moved.
- **How OTHER permanents enter is one piece, `PermanentsEnter(filter) { Tapped, Untapped, CounterKind, Counters,
  Attacking }`** (MC-6.10) on the rule's holder, the filter asked from its side (`Is.Yours`: its controller's) and
  never for itself (CR 614.12, Metallic Mimic's ruling) — Root Maze `{ Tapped }` (everyone's), read by `EnterBattlefield`
  beside the card's own `EntersTappedComponent` / `EntersWithCountersComponent` (its replacement on itself, kept apart).
- **"Untap up to three lands"** (Frantic Search; Time Spiral's six, MC-4.4e2f) is `Untap(Ref.Permanents(Is.Land))
  { UpTo }`: a `ChooseAmongChoice` among the TAPPED ones of those you control, for as many as it can untap — unasked
  when that is all of them (a ceiling, DesignNotes); then `Untap(Ref.Chosen)` through `MagicState.Untap`.
- **Becoming tapped is `MagicState.Tap`** — the ONE way a permanent taps: attacking, a {T} cost, "tap N
  untapped" (Station's too), the Tap effect, tapping for mana and mana payment. It raises
  `BecameTappedEvent` only on untapped → tapped ("whenever Hemmer becomes tapped"); entering tapped is
  not becoming tapped. A new tap path must call it, or Hemmer silently misses it.
- **Scry** (Preordain, MC-4.4f) is `Surveil(n) { To = Library }`: the chosen go to the BOTTOM, the rest are
  ordered on top as a surveil's — and it is no surveil: no `SurveilledEvent`, no tally ("whenever you scry" would need
  an event of its own). The bottom ones go in the order chosen (a ceiling, DesignNotes).
- **Look and take** ("look at the top N, you may reveal a [filter] card and put it into your hand, the rest
  on the bottom in a random order") is ONE piece, `LookAndTake { Take, To, Rest, Must, Reveal }` (MC-4.4e2a) —
  never a flag per destination. **How many** are taken is `Take` (a `Count`, 1 unless said: Stock Up's two; with
  `Must` exactly that many, else up to; Ponder's 0 asks nothing). **Where they go** is `To`: the hand, the
  battlefield (Hit It!), or the library — its TOP (Thassa's Oracle's "up to one of them on top": the card stays
  where it is, above the rest, and its owner knows it). **Where the rest go** is `Rest`: the bottom in a random
  order (not known, as Identity Echo's), `Graveyard`, or `TopInAnyOrder` — Ponder's "put them back in any order":
  each made known to its owner, then a surveil's order choice (`OrderKeptOnTopAction.Ask`, the one place it is
  asked), or `BottomInAnyOrder` — Dig Through Time's "the rest on the bottom of your library in any order": known to their
  owner, in the order looked at (a ceiling, DesignNotes; `MagicState.ToBottom`). `Reveal`: the taken are known to everyone.
  `Then` runs after, with `Ref.ThisWay` the taken (Throne of the Dead Three: three +1/+1 counters, hexproof). "Two instead if this spell was kicked" (Consult the Star
  Charts) is `If(WasKicked) { Otherwise }` over two looks, never a conditional number.
- **A shuffle is `ShuffleLibrary(who)`** (MC-4.4e2a), each player named shuffling once: Ponder's "you may shuffle"
  (under a `May`), every search's "then shuffle", and Timetwister's "each player shuffles their hand and graveyard
  into their library" — two `MoveTo`, then ONE shuffle for `Ref.EachPlayer`, so a player with nothing to shuffle in
  shuffles too. `MoveTo { Shuffle }` is one card shuffled in (Fblthp; Green Sun's Zenith's "shuffle Green Sun's Zenith
  into its owner's library" is the spell's last step, `MoveTo(Ref.Source, Library) { From = [Stack], Shuffle }`, as
  buyback's return to hand); several are moved, then shuffled once.
  **"You MAY search … then shuffle"** is `SearchLibrary { May }`: no card taken is the search declined — nothing is
  shuffled, so a top card its owner knows stays known. A search with no "may" (Mystical Tutor, landcycling) shuffles
  with nothing found (CR 701.19). Script `May` whenever the text says "may search" — LSV's five are; FRA's two and
  TRK's five still shuffle when declined, because kept AI records replay them (DesignNotes; MC-4's Open).
- **A linked "the exiled card"** (The Measure of a Man) is `Exile { Linked }` (stamps `ExiledBy` with this
  object of the source) and `MoveTo(Ref.All(Exile, Is.ExiledBySource), Battlefield)` — the filter asked from the source:
  a new object of it finds nothing (CR 607.2a). A resolving ability asks as the object it CAME from
  (`FilterContext.SourceIncarnation`, from the context's source snapshot, MC-4.4d3): Skyclave Apparition's "when this
  leaves" still finds its card, and chapter III returns it though the Saga left in response; a static or a cost has
  no snapshot and asks the source as it is now (Null Summoner). Every reader of "exiled with this" asks that one
  filter — `ExiledWithSourceShareACardType` (All Good Things...) and `AssimilateExiledWithSource` (Borg Cube) too.
- **Gaining control is `MagicState.GainControl`** — the same object (counters, damage, attachments,
  until-end-of-turn effects stay), moved to its new controller's battlefield zone, summoning sick for them,
  out of combat; no zone change, no event. **"Until end of turn"** (Worf, Intelligence Contractor; The Wrath of
  Khan) is `GainControl.UntilEndOfTurn`: an `UntilEndOfTurn { ReturnControlTo }` on it, given back by cleanup
  (`Combat.CleanupPermanents`) — gone with a zone change like every until-end-of-turn effect. "Become Warriors
  in addition to their other types" is `ModifyUntilEndOfTurn.AlsoCreatureType` (`s.Subtypes` adds it).
- **"Reveal the top X, put every [type] card onto the battlefield, the rest on the bottom in a random
  order"** (Branching Timeline) is `RevealUntilOntoBattlefield { Top }` — Identity Echo's effect, every match
  among the top X instead of "until".
- **"Lands you control enter untapped"** (Warp Speed, `PermanentsEnter(Is.Land & Is.Yours) { Untapped }`) wins over
  every "enters tapped" in `EnterBattlefield` — the land's own, Root Maze's, a token or a return made tapped (Shayne,
  2026-11-01). **"[Permanents] you control enter with N additional [kind] counters"** (Zefram Cochrane) is its
  `{ CounterKind, Counters }`, put on there through `PutCounters`.
- **An entering's cost is ONE piece, `EntryCost(cost) { Instead }`** (MC-4.4l4a; it was `EntersTappedComponent {
  UnlessYouPayLife }`): "as this enters, you may [cost]" — a cost from the shared list, paid BY CHOOSING as ward's is
  (`AdditionalCost.PaidByChoosing` / `CanPayByChoosing`), asked only when it can be paid. **"If you don't, it enters
  tapped"** (the shocklands' `PayLifeCost(2)`, Soporific Springs' 3, Multiversal Passage) is no `Instead`: it enters
  tapped, and `EnterBattlefield` spawns a `May` ONLY when its own text is what taps it — not under Warp Speed (untapped
  anyway) or Root Maze / "onto the battlefield tapped" (tapped anyway) — AFTER the "as it enters, choose" (Passage:
  "choose a basic land type. Then you may pay 2 life"). Yes pays and it untaps. The AI's answer is the search's
  (Shayne, 2026-11-05): both answers are beam nodes, so it pays when the turn's plan spends the mana. **"If this would
  enter, you may [cost] instead. If you don't, put it into its owner's graveyard"** (Mox Diamond's `DiscardCost(Is.Land)`)
  is `Instead = Graveyard`: asked at the top of `EnterBattlefield`, BEFORE it moves (`EntryCost.AskInstead`), so a "no"
  never enters — no enter or leave event — and a yes enters again through `EnterPaidAction` (`paidToEnter`). The land is
  picked just after it enters (a ceiling, DesignNotes).
- **"Choose a basic land type … This land is the chosen type"** (Multiversal Passage) is `ChoosesBasicLandTypeAsItEnters`
  → `ChosenType` (status), a fifth "as it enters, choose" beside colour, number, player and name — not folded into one:
  each is its own status read by its own rule. `s.Subtypes` adds the type, and the land's mana is that type's
  (`ManaAbilities.ColorsOf`'s intrinsic mana, CR 305.6).
- **"If this card is in your opening hand and you're not the starting player, you may begin the game with it on the
  battlefield"** (Gemstone Caverns, MC-4.4l4b) is `BeginsTheGameOnTheBattlefield`: once both hands are kept
  (`FinishKeepAction`), the non-starting player is asked, then it enters with a luck counter and a card is exiled from
  the hand (`BeginOnTheBattlefieldAction`), then the first turn starts (`StartFirstTurnAction`). Nothing else reads an
  opening hand; a Leyline would make its three parts options.
- **"As this enters, choose a number"** (Nyota Uhura) is `ChoosesNumberAsItEnters` → `ChosenNumber` on the
  card (status: gone as it leaves); "a spell with mana value equal to the chosen number" is
  `Is.ManaValueIsChosenNumber`. The AI names the mana value commonest among the opponent's nonland cards it
  knows of, else 2 (`GreedyAi`, Shayne) — the beam doesn't branch on it (`ChooseNumberChoice.NamesAManaValue`).
  **"Choose odd or even. (Zero is even.)"** (Gollum, Riddle Master, MC-4.4h2d) is the same piece `{ OddOrEven }`: the
  choice is 0 ("Even") or 1 ("Odd"), the filter matches a mana value by its parity, and the AI names the commoner
  parity the same way (none known: 2, even).
- **"Put onto the battlefield … sacrifice those at the beginning of your next end step"** (Will Riker) is
  `RevealUntilOntoBattlefield.SacrificeAtNextEndStep` — the tokens' delayed trigger on each card it put there.
- **Assimilate** (Death Is Irrelevant, Borg Cube) is `Assimilate`: `EnterBattlefield` under your control, a +1/+1
  counter, then the artifact type STAMPED with an `Animated` whose AuraId is 0 (no Aura: the state check leaves
  it, `Card.WithoutAnimation` undoes it as it leaves and for a copy) and "Borg, loses all other creature types" an
  `UntilEndOfTurn` that never ends. "A creature card exiled with it" is `AssimilateExiledWithSource` (this object's
  `ExiledBy` links; "if you can't" runs the rest). `ChooseAmongChoice` finds a card off the battlefield in its
  OWNER's zone, so a choice among anyone's exiled cards works.
- **"A permanent was returned to your hand from the battlefield this turn"** (Miles O'Brien) is
  `TurnTally.PermanentsReturnedToHand` — its OWNER's, counted in `MoveCard` (battlefield → hand: bounce, beam
  me up's cost, a token too) — read by `Count.PermanentsReturnedToHandThisTurn`. **Revolt** — "if a permanent left the
  battlefield under your control this turn" (Fatal Push, MC-4.4h2e) — is `TurnTally.PermanentsLeftTheBattlefield`, its
  CONTROLLER's, to any zone (a token too; phasing is no zone change), read by `Count.PermanentsLeftTheBattlefieldThisTurn`.
- **"Choose an exiled card …"** with no move (Dauthi Voidwalker: "Choose an exiled card an opponent owns with a void
  counter on it. You may play it this turn") is `MoveChosen(Exile, filter, Exile)` — To the zone it is already in is the
  choice alone, the same object, `Then` reading it as `Ref.Chosen` (`MakePlayable(Ref.Chosen, { ThisTurn, Free })`).
  "With a [kind] counter on it" is `Is.WithCounter(kind)` (`WithPlusOneCounter` is its +1/+1).
- **"You can't sacrifice Treasure tokens"** (Quark, `CantSacrificeTreasures`) is `MagicState.CanSacrifice`, asked by
  every sacrifice: `MagicState.Sacrifice` itself (it does nothing), `SacrificeCost`'s candidates, a
  `ChoosePermanentChoice { Sacrifice }` (MaySacrifice, EachSacrifices) and
  `ManaAbilities.CanTap` (a Treasure's only mana is sacrificing it).
- **Explore** (CR 701.44, Captain Janeway) is `Explore(ref)`: the top card revealed (`MakeKnown`), a land to the
  hand, else a +1/+1 counter through `PutCounters` and a yes/no to bin the card (`MoveTo(Ref.Id(card)) { From = [Library] }`).
  **"An additional land on each of your turns"** (`AdditionalLandEachTurn`) is `MagicState.LandDrops`, the one count
  the land play and the rollout both read; Fastbond's "any number of lands" is its `{ AnyNumber }` (`int.MaxValue`), and
  "if it wasn't the first land you played this turn" a `LandPlayed` trigger's intervening `If` on
  `Count.LandsYouPlayedThisTurn` ≥ 2 (MC-4.4k2a). **Persist** (CR 702.79, Woodfall Primus) is `LsvCards.Persist`: `SelfDies
  { If = no -1/-1 counter as it last was }` → `MoveTo(Ref.EventCard, Battlefield) { Counters = 1, CounterKind = MinusOne }`
  — `MoveTo.CounterKind` the kind of its `Counters` (+1/+1 unless said; another kind put on just after it enters, DesignNotes).
- **"Create a token … Attach it to [this]"** (Gwyndala) is `CreateTokens.AttachTo`; **"sacrifice any number of
  [lands], then … that many"** (Genesis Device) is `MaySacrifice { AnyNumber }` with `Count.PermanentsChosen`.
- **"Exile [this spell]"** as it resolves (The Needs of the Many, Infinite Diversity) is
  `ExileSourceAndCopiesFromGraveyard(0)`; "exile a card from your hand" linked to a Saga (All Good Things...) is
  `MayDiscard { Exile, Must, Linked }`, and "all cards exiled with this share a card type" is
  `ExiledWithSourceShareACardType`.
- **"As this enters, choose a nonland card name"** (Kai Winn) is `ChoosesNameAsItEnters` → `ChosenName`, the options
  every nonland name printed in the game's sets (`CardRegistry.CardNames` — never the decks: that would show a
  hidden list; Shayne, 2026-11-06), a `ChooseKeywordChoice { NamesACard }` the AI answers by rule (`GreedyAi.LikeliestName`), never
  searched — in LIMITED (Shayne, 2026-11-06), step by step: a card with an activated ability on their battlefield;
  else one known in their hand; else the highest-rated common in a colour their lands show (`CardRatings.TryOf`). "With the chosen name" is `Is.NamedChosenName`. **"Activates an ability"** is `AbilityActivatedEvent`
  (the source as it was), raised by EVERY activation: `ActivateAbilityAction`, a loyalty ability, and a MANA ability
  — `TapForManaAction` and the payment (`ManaPayment.RaiseActivated`) alike (Shayne: "If you name Llanowar Elves and
  they tap it for mana it would trigger her").
  **"Look at an opponent's hand, then choose any card name"** (Anointed Peacekeeper) is the same piece
  `{ LooksAtHand, AnyName }` (MC-4.4d2b): the hand is made known before the name is asked, and the lands' names are
  offered too (`CardRegistry.CardNames(s, lands: true)`).
  **"Choose a card name" as a spell resolves** (Demonic Consultation, MC-4.4h6) is the effect `ChooseCardName` — the same
  question (`MagicState.AskName`), the name kept on the SPELL while it is on the stack (`SetChosenNameAction` takes a
  stack card; `ChosenName` is status, gone as it leaves), so `Is.NamedChosenName` reads it in the steps after. `{ YourOwn }`
  makes its choice `NamesYourOwn`: the AI names the card in its OWN library worth most in hand (`GreedyAi.BestNameInYourLibrary`), blind
  to the order. "Reveal until you reveal a card with the chosen name. Put that card into your hand and exile all other
  cards revealed" is Identity Echo's `RevealUntilOntoBattlefield { Matching, To = Hand, Exile }` — revealed, so known to everyone.
- **A face-down permanent** (CR 708; Abhorrent Oculus' manifest dread, MC-4.4g4) is a COPY stamp, as `EnteredAsCopy`:
  `Card.TurnedFaceDown` gives it a blank 2/2's copiable values (no name, creature only, no cost, colour or abilities)
  and keeps the card on `FaceDown(Original)` — status, so a copy of it is a nameless 2/2 with no card under it, and
  `Card.Fresh` turns it face up as it leaves (CR 708.9: the graveyard sees the card). It ENTERS face down —
  `EnterBattlefield(faceDown:)`, stamped before the entry event, so none of its own "enters" happens (CR 708.3) and
  Containment Priest sees a nontoken creature. Manifest dread is `LookAndTake { To = Battlefield, FaceDown, Must, Rest =
  Graveyard }` (`LsvCards.ManifestDread`). **"Turn it face up any time for its mana cost if it's a creature card"** is
  `TurnFaceUpAction`, a special action (CR 116.2b): offered with priority, no stack, the mana cost paid, the same object
  (`Card.TurnedFaceUp`: damage, counters, tapped stay; a type stamp undone, the state check re-stamps it) — no "enters". `Card.IsHiddenFrom` hides it from all but its
  controller (CR 708.5).
- **Phasing** (The City on the Edge of Forever, CR 702.26) is `Rules.Phasing`: a phased-out permanent is MOVED, with
  `MoveObject`, to its controller's `PhasedOut` zone — no zone change: the same object, its counters, tapped state
  and links, nothing triggers, "until it leaves" never fires — and what is attached goes with it. It phases in as
  that controller's untap step begins, before untapping, with what is attached, unless a permanent says
  "Permanents can't phase in" (`PermanentsCantPhaseIn`). The zone is made on first use, as the Sideboard. The table
  shows it in its row, dimmed and badged (Shayne, 2026-11-06). **"Sacrifice a [filter]. If you do"** — not a may —
  is `MaySacrifice { Must }`.
- **Moving a card a Ref names is ONE piece, `MoveTo(What, To) { From, Tapped, Counters, Finality, UnderYourControl,
  ExileAtNextEndStep, OnTop, Shuffle }`** (MC-6.12): to its OWNER's zone (CR 400.3), onto the battlefield through
  `EnterBattlefield` under its owner unless `UnderYourControl` (Vindictive Triumph). `From` is where it must still be —
  `[Graveyard]` its owner's ("return … from your graveyard", Talon Gates' `[Hand]`), `[Battlefield, Stack]` anyone's
  (Unsummon's "spell or creature"), empty anywhere (Paradox Shaper); `Shuffle` is its owner's, once per card (Fblthp).
  A token that has left the battlefield moves nowhere (CR 111.8). **A CHOSEN move** ("return a land card from your
  graveyard", "a land you control", "you may put a land card from your hand") is `MoveChosen(From, filter, To) { May,
  Then, Otherwise }` — each zone its own choice (graveyard `ChooseAmongChoice`, battlefield `ChoosePermanentChoice`,
  hand `ChooseCardsFromHandChoice`, so a record's answers still fit), then `MoveTo(Ref.Chosen)`; "if you can't" is
  `Otherwise`, "if you do" `Then` (Temporal Conqueror, Sylvia Tilly, Erica Ortegas). How the card moves is `As`, a
  `MoveTo` with its options (What, To and From are the choice's): Brainstorm's "on top of your library in any order" is
  two of them `{ As = …{ OnTop } }`, the second on top; Emperor of Bones' "onto the battlefield under your control with
  a finality counter" `{ UnderYourControl, Finality }`, from EXILE — a `ChooseAmongChoice` among every player's exile,
  the filter (`Is.ExiledBySource`) saying which (MC-4.4h2b). "Each player puts a creature card from their graveyard"
  (Exhume) is `Who = Ref.EachPlayer`: each chooses from their own zone, the active player first (APNAP), one after another (each enters as chosen — a
  ceiling, DesignNotes). **"The top creature card of your graveyard"** (Shallow Grave) is `Ref.All { Top }`: per
  player, the one the filter matches nearest the top — a library's first, a graveyard's LAST (cards are appended);
  any other zone is refused.
  **`Exile` takes a card in a graveyard too** (Emperor of Bones: "exile up to one target card from a graveyard",
  `{ Linked }`), never a card elsewhere.
- **"Sacrifice an artifact. If you do, …the sacrificed artifact's mana value"** (Transmute Artifact) is `MaySacrifice
  { Must }` whose `Then` reads the sacrificed as `Ref.ThisWay` (MC-4.4e2d). **A search whose card's zone is decided
  after it is found** ("if that card's mana value is … put it onto the battlefield … If you don't, put it into its
  owner's graveyard. Then shuffle") is `SearchLibrary.Then`: run with the card in `Ref.Chosen`, still in the library,
  IN PLACE of putting it `To`; the shuffle after.
- **"[Effect]. … for each [one] [done] this way"** (Decree of Pain) is the effect's `Then` with `Ref.ThisWay` — the
  ids it actually did it to (`Destroy`: the ones that left; an indestructible one or McCoy's save didn't;
  `CreateTokens`: the tokens made — Cori-Steel Cutter's "You may attach this Equipment to it"; `ExileTopPlayable`:
  the cards exiled). **"Discard up to two cards, then draw that many"** (Tersa) is `Discard { UpTo, Then }` with
  `Count.NumberOf(Ref.Chosen)`, the cards the discard chose.
- **"Each player chooses a number of [lands] equal to the number controlled by the player who controls the fewest,
  then sacrifices the rest. Players discard cards … the same way"** (Balance, MC-4.4d4) is a COUNT on the two
  each-player effects, never a piece of its own: `EachSacrifices { N }` (1 unless said) and `Discard(who, count)`
  count for each player, `Ref.It` that player, and `Count.BeyondTheFewest(zone, filter)` is "the rest" — how many
  more of them that player has than the player with the fewest (0 where no player is being acted on). "Sacrifice
  three permanents" (Emrakul's ward) is the same effect, `(Ref.You, Is.AnyCard) { N = 3 }`, never a piece beside
  it. A player is asked ONCE, for exactly that many to give up (the same sets as choosing which to keep), and only
  when it is more than none: Balance is one cast whatever the board. Its three effects run in printed order, each
  counted as its turn comes — a land creature sacrificed as a land is no creature when the creatures are counted.
  **"Chooses an artifact, a creature, an enchantment, and a planeswalker from among the nonland permanents they control,
  then sacrifices the rest"** (Ajani, Nacatl Avenger's −4, MC-4.4j2b) is `EachSacrifices(who, !Is.Land) { Keeps =
  [types] }`: one required choice per type there is one of (an artifact creature may be kept as both), then every
  other permanent the filter matches is sacrificed (`SacrificeTheRestAction`) — four choices, never a choice of sets.
- **Voting** — will of the council (CR 701.38, Council's Judgment, MC-4.4j6) — is `Vote(filter) { Then }`: no target
  (hexproof and protection don't stop it); each player, you first, picks one of what the filter matches from YOUR side
  ("a nonland permanent you don't control": `!Is.Land & !Is.Yours`) — a `ChooseAmongChoice` each (`DeciderId` the
  voter), one choice per player whatever the board — then `TallyVotesAction` runs `Then` with `Ref.Chosen` every
  permanent with the most votes, ties included. Two players only (DesignNotes).
- **"Then shuffle and put the last cards you looked at this way on top in any order"** (Lim-Dûl's Vault, MC-4.4m2a) is
  `ShuffleLibrary { KeepTop = 5 }` (`MagicState.Shuffle(…, keepTop)`): the top N stay where they are, as known as they
  were — their order was chosen as they were last looked at (`LookAndTake { Take = 0, Rest = TopInAnyOrder }`).
- **MC-4.4m2b + m2c's choice options** (multicolour; tests in `LsvBatch2m2Tests.cs`):
  - **"Choose a nonland permanent that player controls"** — chosen, never targeted (Chaos Defiler) — is `MoveChosen
    (Battlefield, filter, Battlefield) { Anyones, Then }`: `ChoosePermanentChoice.Anyones` offers EVERY player's
    permanents the filter matches, asked from the chooser's side (`Is.Opponents`), and `Then` reads `Ref.Chosen`.
    **"To a creature or player"** (Comet, Stellar Pup) adds `OrPlayers`: the players the filter matches too.
  - **"A nonland card from their hand or the chosen creature"** (Cloak and Dagger, Entwined) is `YouChooseTheyDiscard
    { OrThe = Ref.Target(1) }` — the permanents it names offered beside the hand's cards (`ChooseCardsFromHandChoice.
    Also`), exiled until the source leaves the same way.
  - **"For each card type, you may put a card of that type from among them into your hand"** (Atraxa, Grand Unifier) is
    `LookAndTake { OnePerType }`: one choice per card type there is among the cards looked at — every `CardType`,
    kindred too — up to one card each (`PutLookedAtAction.PerType`), a card taken as one type never offered as another
    (`ChooseAmongChoice.ExceptUnder`) — never a choice of sets.
  - **"Reveal [cards]"** with nothing chosen is `LookAt(ref) { Reveal }` — known to everyone.
