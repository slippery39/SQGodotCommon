# Handoff — ENDLING: combat v3, three acts, and what the measurements kept overturning

**Read this, then `KinJam.md`, then `KinV3Plan.md`.** `HANDOFF-KinVisualPass.md` is the previous
session and is superseded — read only its §4 scars, which are all still true.

State at handoff: **146 tests green, Godot builds, KinConsole builds, working tree CLEAN.**
Nineteen commits, `2219c49` through `a8b0e87`.

**The game is called ENDLING to a player. DOOMJAM is the working title and the code prefix** — only
`MainMenu.Title` and `project.godot`'s `config/name` face outward. Do not rename the solution; the
user has confirmed the name is temporary and not worth the churn.

---

## 1. The headline

**A playtest found three things eating the fun, and two of them had one cause.** Ash was irrelevant,
board stalls were common, and every card was a stat line with no synergy.

The stall was the seam between two economies stapled together: **the hand was Slay the Spire —
drawn to 5, discarded every turn — and the board was Hearthstone — pay once, keep forever.** An
ephemeral hand keeps feeding a permanent board until it saturates, and then five drawn units have
nowhere to go and End Turn is the only legal move. **It was the reward for playing well**, and it bit
hardest on easy floors.

So units became ephemeral. That is **combat v3**, and everything else in this session followed from
it.

---

## 2. The one thing to carry

**Every measurement in this session overturned something that had been believed on reasoning.** Not
once or twice — it is the pattern of the whole session:

| believed | measured |
|---|---|
| v3 would make the stalemate tail worse | it **halved** it, 55 turns to 33 |
| the bot would need pruning and re-pricing for v3 | it needed **neither**; 10x the node budget changed nothing |
| The Rising's cards were the four worst in the game | the **table was measuring the act**, not the cards |
| the boss dial needed the right value | it was a **cliff**, not a dial, and the fix was deleting it |
| a bigger act-1 boss would make it cost life | a bigger boss made the fight LONGER, so you healed MORE |
| power pays and toughness barely does (v2) | **reversed** under v3 |
| companion marks were a stat trickle | worth **+40/+60** by floor 45; cutting them cost a third of the run |

**Reason about what to measure. Do not reason about what the measurement will say.** Every one of the
above was a confident, well-argued position with a comment explaining it.

---

## 3. What exists now

- **Combat v3.** Units withdraw at end of turn. Any lane is always playable and the held unit is
  discarded, no refund. **Withdrawn ≠ dead** — this is load-bearing far beyond where it was written.
- **A run is all three acts**, fixed order (Reckoning → Long Emergency → Rising), 15 floors each,
  45 total, ~8 battles an act. `ActMap.Layout` is the act's shape as a **data table** — change the
  array, not arithmetic.
- **Synergy primitives**: `CountOf` (amounts that scale on what the board says), `BuffAction`, and
  `KinTarget.AdjacentLanes`. Every effect amount in the game used to be a literal.
- **The companion is its ABILITY.** Ash: +2/+0 per Loss, for that turn only. **Marks are cut.**
- **A shop**, with buying, healing, and **card removal** — the first thing other than an apocalypse
  that can take a card out of a run. Engine, bot, and a Godot screen.
- **Per-act bosses**, each with its own mechanic and no healing.
- **A main menu** that looks like this game's, with no MTG entries.

---

## 4. Scars worth not re-earning

**The v2 findings in `docs/findings/doom-balance.md` runs 1-14 describe a different game.** The
methodology transfers; the numbers do not. Runs 15-24 are v3.

**Engine and rules:**

- **The spawn queue is FIFO and nothing in `EndTurnAction` resolves inline.** Withdrawing units
  inside `Execute` would empty the board BEFORE the apocalypse read it, and all six
  `FiringRead.Standing` scenarios would silently do nothing. `ADoomFiringSeesTheBoardYouCommitted`
  is the only thing holding that ordering.
- **A step that empties a zone must ask what the engine had not finished doing to the things in it.**
  Withdrawal was discarding units the apocalypse had just killed, as survivors — no `OnDeath`,
  nothing recorded, Zombie unpaid. Found by a NullReferenceException, not an assertion.
- **An expiring buff needs no duration system**: the same buff negated on the opposite trigger. **It
  only works for a count that cannot change within a turn.** `DiedLastTurn` qualifies;
  `CardsPlayedThisTurn` would apply a small buff and remove a large one.
- **`OnTurnStart` can never fire for a card** — the field is empty when it fires. The companion is
  the one legal holder. `NoCardDeclaresOnTurnStart` enforces it.
- **A heal makes a fight a THRESHOLD, and no multiplier can sit on a threshold.** Boss health at 0.8
  vs 1.0 swung deaths 6.2% → 42.2%. **No boss heals.** Healing also cannot exceed MaxHealth any more
  — it used to raise the ceiling, so `Gravecaller` grew 4 a turn without bound for a whole battle.

**Measurement:**

- **`SimCommand`'s survival table silently narrowed** when `ActLength` changed meaning: it printed
  floors 1-15 of a 45-floor run while looking complete. Act 2's boss was tuned blind for two
  iterations. **A table that narrows is worse than one that errors.**
- **The CARD VALUE table lies across acts.** A themed card is only taken inside its own act, so its
  "without" group is the other two acts. Compare a themed card **only within its own theme**.
- **An unchanged number is evidence of a change that did not happen.** A sim silently never ran
  because `dotnet build | grep -c error && dotnet run` short-circuits — `grep -c` exits non-zero when
  it matches nothing. Caught only because deck size came back byte-identical.
- **`sim 120` now takes longer than a 10-minute tool window** (45-floor runs, ~12s each). `sim 25`
  fits and is enough to separate acts; it is NOT enough to argue about 20% vs 25%.

**Front end** — everything in `HANDOFF-KinVisualPass.md` §4 still applies, plus:

- **A grid that fits twelve cards tells you nothing about one that fits forty-six.** The shop's
  removal grid ran off both edges of the screen and buried its own BACK button. It solves for the
  band now. **Look at the worst case, and there is a capture flag for it** (`--shop --remove`).
- **`KinCardFace.Pt()` owns the card scale chain.** A raw point size parented to a card is
  multiplied by the card scene's own 0.7 AND the screen's scale. Put labels beside cards, never in.

---

## 5. Where the numbers are

`docs/findings/doom-balance.md`, runs 15-24. The short version:

| | now |
|---|---|
| act completed | **12%** (target 25-50%) |
| mean floor of 45 | **36.00** |
| life lost per battle | 8.7 |
| deck at end | ~42 |

Deaths are spread — mid-act-1, mid-act-3, and the finales — rather than sitting on three floors.

---

## 6. What to do next

**In order, and the first is not optional:**

1. **Play it.** The last three passes are tuned against a bot that cannot shop well, never declines
   a reward deliberately, and sees one turn ahead. Every remaining number is a floor.
2. **Finish the difficulty.** 12% against a 25-50% target. The two known killers are **Vampires**
   (17.6% of its band) and **the final floor** (40%) — and only five runs in twenty-five reach floor
   45, so that last one **must not be tuned again on this sample**.
3. **2-drops are still behind 1-drops** (+3.89 vs +7.63). They were repriced against lanes this
   session and it narrowed the gap without closing it. **The next move is not "buff them more"** —
   power-heavy 2-drops are the bad ones and toughness-heavy ones are fine.
4. **`DesignNotes.md` has three costed items**: Field Dressing's frequency, the 2-drop maths, and
   **the boss not feeling like a boss** — the last is the most interesting, and the fix is telling
   the player which boss is coming while they can still build for it.

**Not built, still designed:** `Persistent` as a keyword, enemy intent SEQUENCES (Piercing and
Shifting must land before the first persistent card), events as a floor kind. `KinV3Plan.md` phases
5, 6 and 8.

---

## 7. What needs a human

- **Whether v3 is actually more fun.** The sim says the stalls are gone by construction and every
  turn has a decision. It cannot say whether the decision is interesting.
- **The name.** ENDLING is a placeholder the user picked as "temporary".
- **The 2-drop question is a design question, not a tuning one.** If lanes stay the scarce resource,
  a 2-drop may need to do something a 1-drop cannot rather than simply being bigger.

---

## 8. How to reproduce anything here

```
dotnet test KinCore.Tests/KinCore.Tests.csproj          # 146, ~4 min
dotnet run --project KinConsole -c Release -- sim 25     # ~5 min; 120 will time out
dotnet run --project KinConsole -c Release -- content    # every card, as the player sees it
```

Captures — `Commands.md` has the full set, and `shots*/` must exist first:

```
godot-mono --path SQGodotCommon --position 1920,0 --resolution 1600x900 \
  --write-movie shots_menu/m.png --fixed-fps 10 --quit-after 16
... KinGame/kin_board.tscn -- --shop            # the shop
... KinGame/kin_board.tscn -- --shop --remove   # the removal grid, the layout that breaks
... KinGame/kin_board.tscn -- --reward          # the reward screen
```

**`--autostart` is gone** — it existed only to skip a theme picker that no longer exists.
