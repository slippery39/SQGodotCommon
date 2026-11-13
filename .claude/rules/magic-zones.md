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
  `DiscardedEvent` per batch ("one or more cards"), counted in `TurnTally.CardsDiscarded`. A chosen
  card going to the graveyard is a discard only from the hand (`MoveChosenToGraveyardAction`; surveil
  isn't). "When you discard this card" triggers `FromGraveyard`, where the card now is. Something that
  triggers in the cleanup (a discard to hand size) gets priority THERE, then another cleanup step
  follows (`TurnStructure.FinishCleanup`, CR 514.3a) — never carried into the next turn.
- **Every token is made by `CreateTokens`** — so a replacement on tokens (Draconic Visitor's "artifact
  tokens are Dragons instead"; Holo-Engineer's ANY token an Illusion, `ArtifactTokensBecome.AnyToken`) is
  applied in that one place (`CreateTokens.Instead`, copies too), under whoever `ControlledBy` names.
- **A card put onto the battlefield from anywhere enters through `MagicState.EnterBattlefield`** —
  a search, a return, a token: never a bare `MoveCard` to the battlefield, which skips its entry
  (controller, tapped, its "enters" event). `MoveChosenToAction` routes a battlefield search there.
- **Hexproof** is checked in `TargetSpec.IsLegal` through `s.Has` (an opponent's spells and abilities
  only; mass effects never target). **Destroying is `MagicState.Destroy`** — the Destroy effect and the
  state-based check's lethal damage and deathtouch alike: indestructible (`s.IsIndestructible`) is asked
  there, and Dr. McCoy's replacement ("instead remove all +1/+1 counters … damage healed",
  `CountersRemovedInsteadOfDestruction`) applies there. Several at once are JUDGED together before any
  leaves (CR 701.7) — a wrath doesn't end an indestructible grant halfway, nor kill McCoy before the creature
  he saves — then leave in the battlefield's order. 0 toughness isn't destruction and still kills.
- **"Exile it instead" of dying** is decided in `MoveCard` alone: a finality counter, or Essence Burn's
  `UntilEndOfTurn.ExileIfItWouldDie` — so the card never dies and no "dies" trigger sees it.
- **"If you cast it"** is `Card.WasCast`, set by `EnterBattlefield(cast: true)` from a resolving spell.
- **Who knows a hidden card is `Card.KnownTo`** (`Knowers`, relative to the owner — story AI-3): in a
  library, who knows its position; in a hand, whether the opponent saw it. `MoveCard` sets it on every
  move — out of a public zone into a hand or library is `Everyone`; into its library the owner knows
  where it went; into a hand only the opponent's knowledge counts, and only from the top (a draw — a
  search takes it face down) — and `Shuffle` clears the library's. Anything else that LOOKS or REVEALS
  adds with `s.MakeKnown`: surveil's look, a search's `Reveal` (set it whenever the text says "reveal
  it"), "reveals their hand", behold from the hand (a cost — Theorist's Sanctum's behold as it enters
  is a condition and reveals nothing, a known gap), "look at target opponent's hand" (`LookAtHand`, V'Ger —
  the table logs what you now know there; showing it in the hand is TBL-6). **Exiled face down**
  (`ExileTopPlayable.FaceDown`, Vulcan Mind-Meld) is `ExiledFaceDown` + `KnownTo` (who looked); every front
  end asks `Card.IsHiddenFrom(viewer)` and draws a back. **Zefram's "look at the top card any time"** is
  read, never stamped — `MagicState.TopIsKnownToOwner` (the top changes on every path). A random order (Identity Echo's bottom) is NOT
  known. A new card that looks at or reveals a hidden card must do the same, or the search's "shuffle
  what isn't known" reshuffles a card its player has seen.
- **"From outside the game"** (Extrapolate the Impossible) is the `Sideboard` zone — Bo1 limited's
  unplayed pool. It exists only in a game given one, through `MagicRules.WithSideboard` (NewGame's
  `sideboard1/2`, and tests), made after both decks: a game without keeps every id it had —
  the golden replays and the position suite's move tokens hold ids. `CardsIn` of a missing sideboard
  is empty. A card leaving it was revealed, so `MoveCard` makes it known to everyone.
- **Randomness** (a discard at random, a random order) takes `s.TakeRng()`, so a seeded game replays.
  A search for several cards may take two of a name unless `SearchLibrary.DifferentNames` (Fblthp).
  A spell that exiles itself as it resolves (Sphinx's Approach) is left there by `FinishSpellAction`.
- **How OTHER permanents enter is one piece, `PermanentsEnter(filter) { Tapped, Untapped, CounterKind, Counters,
  Attacking }`** (MC-6.10) on the rule's holder, the filter asked from its side (`Is.Yours`: its controller's) and
  never for itself (CR 614.12, Metallic Mimic's ruling) — Root Maze `{ Tapped }` (everyone's), read by `EnterBattlefield`
  beside the card's own `EntersTappedComponent` / `EntersWithCountersComponent` (its replacement on itself, kept apart).
- **Becoming tapped is `MagicState.Tap`** — the ONE way a permanent taps: attacking, a {T} cost, "tap N
  untapped" (Station's too), the Tap effect, tapping for mana and mana payment. It raises
  `BecameTappedEvent` only on untapped → tapped ("whenever Hemmer becomes tapped"); entering tapped is
  not becoming tapped. A new tap path must call it, or Hemmer silently misses it.
- **Look and take** ("look at the top N, you may reveal a [filter] card and put it into your hand, the rest
  on the bottom in a random order") is `LookAndTake` — `Must`, `RestToGraveyard`, `Reveal` (known to
  everyone). The rest go down as Identity Echo's: a random order, not known.
- **A linked "the exiled card"** (The Measure of a Man) is `Exile { Linked }` (stamps `ExiledBy` with this
  object of the source) and `ReturnLinkedExile` — nothing once the source has left (CR 607.2a).
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
- **The shocklands' "you may pay 2 life. If you don't, it enters tapped"** is `EntersTappedComponent { UnlessYouPayLife }`:
  it enters tapped, and `EnterBattlefield` spawns a `May` ONLY when its own text is what taps it — not under Warp
  Speed (untapped anyway) or Root Maze / "onto the battlefield tapped" (tapped anyway) — and the controller has
  the life (CR 119.4). Yes is `LoseLife` (the one life-loss path) and it untaps. The AI's answer is the search's
  (Shayne, 2026-11-05): both answers are beam nodes, so it pays when the turn's plan spends the mana.
- **"As this enters, choose a number"** (Nyota Uhura) is `ChoosesNumberAsItEnters` → `ChosenNumber` on the
  card (status: gone as it leaves); "a spell with mana value equal to the chosen number" is
  `Is.ManaValueIsChosenNumber`. The AI names the mana value commonest among the opponent's nonland cards it
  knows of, else 2 (`GreedyAi`, Shayne) — the beam doesn't branch on it (`ChooseNumberChoice.NamesAManaValue`).
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
  me up's cost, a token too) — read by `Count.PermanentsReturnedToHandThisTurn`.
- **"You can't sacrifice Treasure tokens"** (Quark, `CantSacrificeTreasures`) is `MagicState.CanSacrifice`, asked by
  every sacrifice: `MagicState.Sacrifice` itself (it does nothing), `SacrificeCost`'s candidates, a
  `ChoosePermanentChoice { Sacrifice }` (MaySacrifice, EachSacrifices, SacrificePermanents) and
  `ManaAbilities.CanTap` (a Treasure's only mana is sacrificing it).
- **Explore** (CR 701.44, Captain Janeway) is `Explore(ref)`: the top card revealed (`MakeKnown`), a land to the
  hand, else a +1/+1 counter through `PutCounters` and a yes/no to bin the card (`MoveCardTo { From = Library }`).
  **"An additional land on each of your turns"** (`AdditionalLandEachTurn`) is `MagicState.LandDrops`, the one count
  the land play and the rollout both read.
- **"Create a token … Attach it to [this]"** (Gwyndala) is `CreateTokens.AttachTo`; **"sacrifice any number of
  [lands], then … that many"** (Genesis Device) is `MaySacrifice { AnyNumber }` with `Count.PermanentsChosen`.
- **"Exile [this spell]"** as it resolves (The Needs of the Many, Infinite Diversity) is
  `ExileSourceAndCopiesFromGraveyard(0)`; "exile a card from your hand" linked to a Saga (All Good Things...) is
  `MayDiscard { Exile, Must, Linked }`, and "all cards exiled with this share a card type" is
  `ExiledWithSourceShareACardType`.
- **"As this enters, choose a nonland card name"** (Kai Winn) is `ChoosesNameAsItEnters` → `ChosenName`, the options
  every nonland name printed in the game's sets (`CardRegistry.NonlandNames` — never the decks: that would show a
  hidden list; Shayne, 2026-11-06), a `ChooseKeywordChoice { NamesACard }` the AI answers by rule (`GreedyAi.LikeliestName`), never
  searched — in LIMITED (Shayne, 2026-11-06), step by step: a card with an activated ability on their battlefield;
  else one known in their hand; else the highest-rated common in a colour their lands show (`CardRatings.TryOf`). "With the chosen name" is `Is.NamedChosenName`. **"Activates an ability"** is `AbilityActivatedEvent`
  (the source as it was), raised by EVERY activation: `ActivateAbilityAction`, a loyalty ability, and a MANA ability
  — `TapForManaAction` and the payment (`ManaPayment.RaiseActivated`) alike (Shayne: "If you name Llanowar Elves and
  they tap it for mana it would trigger her").
- **Phasing** (The City on the Edge of Forever, CR 702.26) is `Rules.Phasing`: a phased-out permanent is MOVED, with
  `MoveObject`, to its controller's `PhasedOut` zone — no zone change: the same object, its counters, tapped state
  and links, nothing triggers, "until it leaves" never fires — and what is attached goes with it. It phases in as
  that controller's untap step begins, before untapping, with what is attached, unless a permanent says
  "Permanents can't phase in" (`PermanentsCantPhaseIn`). The zone is made on first use, as the Sideboard. The table
  shows it in its row, dimmed and badged (Shayne, 2026-11-06). **"Sacrifice a [filter]. If you do"** — not a may —
  is `MaySacrifice { Must }`.
- **"[Effect]. … for each [one] [done] this way"** (Decree of Pain) is the effect's `Then` with `Ref.ThisWay` — the
  ids it actually did it to (`Destroy`: the ones that left; an indestructible one or McCoy's save didn't).
