---
name: design-card
description: Design or build a card, companion or foe for THE COMPANION GAME (KIN) — the principles every card is judged by, the anatomy of a companion, the levers the board offers, and how a card is built, tested and checked on screen in KinCore/Party. Use when running a card design session, adding or changing a card, a companion kit, a passive, or a foe's intent pattern; when asked "is this card good", "why do these feel the same", or to design a new companion; or before touching KinCore/Party/PartyContent.cs.
---

# Designing a card for THE COMPANION GAME

The game is **Pokemon / Monster Rancher / Digimon as a roguelike deckbuilder**: up to three
companions ARE the board, one combined deck holds each companion's cards, foes play telegraphed
intents. Rules: "THE COMPANION GAME" at the top of `KinJam.md`. The kits this skill produced first
are "KITS v2" there.

**We are EXPLORING, not tuning** (root `CLAUDE.md`). A principle below is a note from a past
session, not a veto — breaking one on purpose is a design decision. Say what a break COSTS. No sims:
judge by principle, prove by test, then look at it.

## 1. The principles — judge every card by these

1. **Every card creates a decision.** A card that is only numbers is filler; filler is the
   exception. Ask: *when would I NOT play this, or play it differently?* No answer = no decision.
2. **Each companion is an archetype with its OWN relationship to the board.** Same board, opposite
   goals: Bramble wants to be hit, Pike wants to never be where the hit lands. Two companions that
   want the same thing from position feel the same whatever their numbers (the first playtest
   proved it: Thump and Jab were one card).
3. **A companion = stats + a PASSIVE + a signature mechanic its cards build and spend.** The passive
   gives it identity in a hand of basics; the cards feed and cash the mechanic.
4. **Foes ask what is in your deck.** Every foe property has a VICTIM (a companion or card it
   punishes) and an ANSWER (what beats it). A flat tax is good against no deck and bad against none.
5. **The stall test: does a longer battle pay this more?** If yes it is a stall engine — cut it.
   Repeatable healing fails. Thorns passes: it pays only when struck, so it ends fights sooner.
6. **Rules text says the rule ONCE and fits two lines.** Cut any clause the player can derive. If it
   will not fit, the card is too complicated or the words are wrong — never shrink the box.
7. **Identity must READ at a glance.** The owner's colour is on every card and cell
   (`KinPalette.Companion`); a new companion needs a colour (never gold or red) before it ships.

## 2. The levers — what a card can pull on

| Lever | Examples | Primitive today |
|---|---|---|
| **Position** — shapes, columns, neighbours, steps | Sweep (3 wide), Flank (lone foe), Root Wall (neighbours), Lunge | `StrikeAction.Offsets`, `DoubleIfAlone`, `GuardAction.AndBeside`, `StepAction` |
| **The telegraph** — react to or change an intent | Draw Fire (redirect a single hit) | `DrawFireAction`; *changing/cancelling an intent: not built* |
| **Moving foes** — re-aims their attacks | Gust, Slam, Whirlwind (Gale) | `PushAction` (`Collision`), `SwapAction` |
| **Ownership** — who plays it, cross-companion combos | Draw Fire protects a neighbour | *"if X acted this turn": not built* |
| **Stats** — Power, Block, Speed | Retaliate (= Block), "+ Power" | `AddPower`, `AddBlock`; *cooldown reset: not built* |
| **Passives** | Thorns, Momentum, Off-Balance | `Ally.Thorns`/`BonusThorns`, `MomentumPerStep`/`Momentum`, `Unbalances` → `Foe.OffBalance` |
| **The deck** | Feint draws | `DrawAction` |
| **Knockouts** | a fallen friend's cards are dead | *cards that care: not built* |

**The biggest unbuilt lever is the telegraph itself** — cancelling, delaying or changing an intent.
Moving foes is built (Gale); cross-companion "if X acted" combos are not.

## 3. The checklist — before a card is done

- [ ] What decision does it create, and for WHICH companion's plan?
- [ ] Does it deepen its owner's relationship to the board, or blur it into another's?
- [ ] Which foe is it good against, and which is it bad against?
- [ ] Stall test passed?
- [ ] Text ≤ two lines on the card, the rule said once, and TRUE to the template.
- [ ] A test proves it FIRES — the consequence, never the construction (section 4).
- [ ] Seen on screen (section 5).

## 4. Building it in code

- **Content** — `KinCore/Party/PartyContent.cs`. A card is `Card(name, cost, text, steps...)`: steps
  are `CardStep` templates resolved IN ORDER by its owner (`Lunge` = step, then strike; `Hit and
  Run` = strike, then step). A step played ON a space overrides `NeedsSpace` and `SpaceRefusal`
  (step, push, swap do); the play's validation asks each, and the board lights exactly the spaces
  validation accepts — never a second opinion. The card is discarded AFTER its steps — so a draw can
  never draw the card itself.
- **A new mechanic** = a new `CardStep` record in `PartyActions.cs`, or a field on an existing one.
  Records only, no delegates (the Serialization Rule in the root `CLAUDE.md`). A passive = a field on
  `Ally` + `PartyCompanion`, copied by `PartyBattleFactory`, plus a `Passive` tag and a
  `PassiveRule` sentence.
- **Where it resolves** — foe attacks in `EndPartyTurnAction`; damage through `PartyState.HitAlly`
  / `HitFoe`; where an attack lands ONLY through `PartyState.IntentTargets` (the telegraph and the
  resolution must share one account). Per-turn state clears in `StartPartyTurnAction`.
- **Tests** — `KinCore.Tests/PartyTests.cs`, **inline definitions only**, and read a number that
  moved: `FoeIn(s, 2).Hp`, `Named(s, "Pike").Space`. An inert card throws no error — four silent
  no-op bugs in this codebase were found only by testing the consequence.

## 5. Seeing it — nothing is done until it has been looked at

```
./Run-Godot.ps1 KinGame/kin_party.tscn -Capture shots/party -Seconds 1.5 -GameArgs '--scenario=1','--play=1'
```

Flags are in `Commands.md` (`--play`, `--focus`, `--click-space`, `--end-turn`). Then READ the frame:
- **Does the whole rules line show?** The shared fitter measures without line spacing, so text it
  judges to fit can still be CLIPPED with no error — Root Wall lost "Block.", Flank "lone foe.".
  Cards with no stat row now get the stat row's room; keep text short anyway.
- **Does the companion cell still fit** (passive, move, forecast lines)? A wrapped line pushes the
  forecast off the bottom.
- **Is the owner obvious** from across the room?

Record the design in `KinJam.md` (what, and WHY), numbers never in a `CLAUDE.md`.
