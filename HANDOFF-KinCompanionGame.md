# Handoff — THE COMPANION GAME: the pivot, the slice, three companions

**Read this, then "THE COMPANION GAME" at the top of `KinJam.md`, then the `design-card` skill
before touching any card, companion or foe.** `HANDOFF-KinCompanionGuard.md` is the previous session
and describes a game that no longer exists — read only its §5 scars.

State at handoff (2026-09-23): **216 tests green**, solution and Godot project build, branch
`kin-pivot`, **nothing pushed**. Check `git status` for anything written after the commits below.

> **UPDATE 2026-09-24 — AUTO-BATTLE v1, uncommitted when written.** Shayne's first full run (lost
> in battle 3) found a FLOW failure, and the owned-card deck was replaced: every creature plays its
> own move cycle at end of turn in Speed order, the deck is the trainer's (generic cards dropped on a
> monster or foe), every monster steps once a turn and a step into an ally swaps. Design and reasons:
> "AUTO-BATTLE v1" at the top of `KinJam.md`. Much of §2–§3 below (OwnedBy, the move cooldown, Draw
> Fire, per-companion rewards) no longer exists. Then the same day: a hover INSPECTOR on every
> creature, and **CATCHING v1** (the Snare item; the bench between battles — KinJam "CATCHING v1").
> Then **THE MAP v1**: two regions of town → one of two wild areas (each with its own pool) → gym
> (KinJam "THE MAP v1"; `PartyWorld`, `PartyRun.Phase`). **Next: Shayne plays it.** Not built: the
> in-battle bench, passives/colours for caught monsters.
>
> **Scar: Python text-mode writes CRLF on Windows.** Patch scripts that `open(p, 'w')` turned LF files
> into CRLF and every commit rewrote every line. The repo MIXES endings per file — keep each file's
> own: read with `newline=''`, normalise, write back in the file's original ending.

---

## 0. What changed, in one paragraph

A playtest of Guard found the lane/unit game and the companion game fighting each other ("Slay the
Spire with extra steps, not a true companion game"). **Shayne pivoted KIN to a monster-companion
deckbuilder — Pokemon / Monster Rancher / Digimon as a roguelike.** Units are cut. Up to three
companions ARE the board; one combined deck holds each companion's cards; foes are creatures playing
telegraphed intents, never a deck. It is built as ONE BATTLE, beside the old game, and it plays.
**The old lane game still exists and still runs** (DESCEND on the menu) — deleting it is a later,
deliberate step (§6).

## 1. Commits this session

| Commit | What |
|---|---|
| `8f16cef` | The last of the lane game: the Guard/move playtest fixes, committed as a clean point to return to |
| `9b51e5a` | **The companion game slice** — `KinCore/Party/`, `kin_party.tscn`, COMPANIONS on the main menu, companion colours, hover-lights-the-owner, floating feedback, the pivot docs |
| `f6ab8c0` | **KITS v2** — Bramble (Thorns) and Pike (Momentum): opposite goals on one board. The `design-card` skill |
| `b6c09f0` | **Gale** (the Controller — pushes foes, Off-Balance), "Three against three", six generated portraits, the monster-fills-the-cell board |

## 2. The rules as built — the decided list lives in `KinJam.md`

- **Board:** a row of 5 spaces a side, column N faces column N. **Your attacks fire straight ahead.**
- **Team of up to 3**, each with its own HP. **No player life** — all knocked out = defeat.
- **Combined deck; every card belongs to one companion** (`OwnedBy`) and only it can play the card.
  Knocked out = its cards are dead. Companions do nothing without cards.
- **Stats:** HP, Power (added to attack cards), Speed (the free move's cooldown: 3 every turn, 2 every
  other, 1 every third; a move is one step). **Cards that move you ignore the cooldown.**
- **Foes** cycle a fixed intent pattern; every intent is a SHAPE anchored on the foe's column, or
  homing. `PartyState.IntentTargets` is the ONE account of where an attack lands — the telegraph and
  the resolution both read it.
- **A card is discarded AFTER it resolves** — before that, Feint drew itself back (found in play).

**The three companions — "same board, opposite goals":**

| | Wants | Passive | Signature cards |
|---|---|---|---|
| **Bramble** (green) | to be HIT | Thorns 2 | Thornhide, Retaliate (= her Block), Draw Fire, Root Wall |
| **Pike** (blue) | never to be where the hit lands | Momentum +2/step | Feint, Lunge, Hit and Run, Flank |
| **Gale** (purple) | the FOES where it chooses | Off-Balance +2 | Gust, Slam, Whirlwind |

Playtests: v1 kits "did not feel different" (fixed by passives + opposite goals — "completely
different"); v1 visuals "every card looked the same" (fixed by identity colour + owner portrait);
Gale "interesting, we'll see" — undecided.

## 3. Where things are

- **Engine** — `KinCore/Party/`: `PartyModel` (Ally, Foe, Intent, OwnedBy, PartyBattle),
  `PartyActions` (play, move, end/start turn, every `CardStep`), `PartyState` (the API the board
  reads), `PartyContent` (companions, foes, scenarios, rewards, encounters, `PartyBattleFactory`),
  `PartyRun` (the run, outside GameState).
- **Tests** — `KinCore.Tests/PartyTests.cs` and `PartyRunTests.cs`, 55 of the 216. Inline definitions only.
- **Screen** — `KinPartyBoard` + `KinPartyCell`; reuses `KinHandView`, `KinCardFace`, `KinArt`,
  `KinAnimator` unchanged except where noted in `KinUI.md` ("THE COMPANION GAME screen").
- **Art** — `Art/{bramble,pike,gale,boar,wisp,stonebeak}.png`, generated locally. ComfyUI is
  installed at `D:\AI\ComfyUI_windows_portable` with DreamShaper XL Turbo; it was running.
- **Docs** — `KinJam.md` (design + playtests), `KinUI.md` (screen rules), `Commands.md` (flags:
  `--scenario`, `--click-space`, `--focus`, `--play`, `--end-turn`), `docs/research/companion-games.md`
  (the survey), `docs/paper/companion-slice.md` (v1, history).

## 4. Scars worth not re-earning

1. **Splitting a mixed tree into commits** — scar 1 of the last handoff still holds (the hook
   re-adds the whole working-tree file). What worked: format the tree with `dotnet-csharpier` FIRST,
   `git worktree add -b split-temp <scratch> HEAD`, build commit 1's files there by script (cut the
   later change's blocks out of the final files), build + test it, commit; copy every changed file
   over for commit 2; verify byte-identity against the main tree; `git reset --mixed` onto it.
2. **The shared card text fitter lies twice.** It assumes a 112px box (ours was 104 — Root Wall lost
   "Block.") and it measures WITHOUT line spacing (Flank lost "lone foe." at 112). Companion cards
   have no stat row and now take that room. **Read every new card's text in a capture.**
3. **A capture cannot hover or drag.** `--focus=N` stands in for a hover, and `_Process` must treat it
   as a FALLBACK or the next frame clears it. It shows the look, not that hovering triggers it.
4. **A plain headless run does NOT import new art** — `godot-mono --path SQGodotCommon --headless
   --import` does. Check `git diff SQGodotCommon/project.godot` after (it strips comments).
5. **CSharpier reformats on commit, so a patch script written against pre-commit text stops
   matching.** Read the current text before anchoring on it.
6. **Anchoring a band to a Control's bottom and growing it upward grew it DOWN**, and the clip ate the
   forecast line. A full-rect VBox (top band, expanding spacer, foot band) is what works.
7. **Name clashes with the lane game**: `IntentKind` exists in `KinCore`, so the Party enum is
   `IntentType`; a `Name(int)` helper on a `Node2D` collides with `Node.Name`.
8. **Generated art: the subject noun decides the picture.** "a heavy grey bird with a stone beak"
   gave three plain grey birds; "a bird carved from grey granite" gave stone. Inspect at 500px.
9. **`cat > file` with no heredoc waits on stdin forever** in a Bash call — a probe hung until it was
   killed. Write files with the Write tool.

## 5. What was deferred, on purpose

- **HOW catching works** — a card or another action (Shayne: "defer it for now"). There will be one.
- **Which enemies are catchable** — leaning "regular foes yes, bosses no"; a catchable foe's intents
  should match the cards it brings.
- **A grid instead of a row** — the fallback if the row plays flat. It has not.

## 6. What I would do next — agreed with Shayne, in this order

1. **The run — v1 BUILT at the end of this session, unplayed** (`KinCore/Party/PartyRun.cs`,
   `KinPartyRunScreens`, KinJam.md "THE RUN"). Play it first. Then **catching** — the join after
   battles 1 and 2 is its stand-in. The lane game's `Run`/`ActMap`/shop are prior art to read, not to
   reuse blindly — they are built around a single companion and a life total.
2. **More foes, and a boss** — every companion needs a foe it punishes and one that punishes it
   (Gale has the Wisp; Bramble and Pike have nothing that singles them out yet). One boss as an exam.
3. **The telegraph lever** — cards that cancel, delay or change an intent. The biggest unbuilt lever
   in the `design-card` skill; probably a fourth companion's identity.
4. **Delete the lane game** once the pivot is certain: `KinBoard`, units, the Opponent, Guard, most of
   `StarterContent`, ~160 tests, the old-theme SVGs. Then fix the root `CLAUDE.md` solution map,
   which still describes lanes.
5. **A hover panel for foes and companions** — rules text today only appears on click.
