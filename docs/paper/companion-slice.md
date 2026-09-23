# Paper play — the companion game, first slice (2026-09-23)

Play this BY HAND before any code. Every number is a guess; the point is to find out whether the
decisions are real. Rules are the "THE COMPANION GAME" block at the top of `KinJam.md`.

## Rules for the table

- **Board:** a row of 5 spaces a side, facing each other. Column 1 faces column 1.
- **Your turn:** draw 5, 3 energy. Play any card whose OWNER is standing and not knocked out.
  **Your attacks fire straight ahead** from the owner's column — no enemy there, the attack misses.
- **Free move:** one step into an adjacent EMPTY space. Speed 3 = every turn, 2 = every other turn,
  1 = every third. Every companion starts the battle ready. Move cards ignore the cooldown.
- **Block** lasts until the end of the enemies' turn, then clears.
- **End of turn:** enemies act left to right, each doing the intent it showed. Then the next intents
  are revealed. Discard your hand.
- **Knocked out:** its cards are dead draws for the rest of the battle. Win = every enemy at 0.
  Lose the battle = every companion at 0.

## The companions

**Bramble** — HP 30, Power 2, **Speed 1**. The wall: slow, blocks, can't dodge often.

| Card | Cost | Text |
|---|---|---|
| Thump ×2 | 1 | Deal 3 + Power ahead. |
| Bark Skin | 1 | Bramble gains 6 Block. |
| Root Wall | 2 | Bramble and any companion beside it gain 5 Block. |
| Draw Fire | 1 | This turn, single-space attacks on a companion beside Bramble hit Bramble instead. |

**Pike** — HP 18, Power 3, **Speed 3**. The skirmisher: fragile, dodges, aims.

| Card | Cost | Text |
|---|---|---|
| Jab ×2 | 1 | Deal 2 + Power ahead. |
| Lunge | 1 | Step 1, then deal 2 + Power ahead. |
| Sweep | 2 | Deal Power to the enemy ahead and to each enemy beside that column. |
| Feint | 0 | Step 1. Draw a card. |

## The enemies — intents cycle in order

| Enemy | HP | Intent cycle |
|---|---|---|
| **Boar** | 22 | Charge: 9 to its own column → Trample: 5 to its column and both beside it (3 wide — the middle can't step out) → repeat |
| **Wisp** | 12 | Zap: 4 to the companion with the LOWEST HP, wherever it stands (can't be dodged) → Drift: move 1 left → repeat |
| **Stonebeak** | 16 | Dive: 7 to its column and the one to its left (2 wide) → Preen: gains 6 Block → repeat |

## Scenario A — one companion against two

Pike alone. Deck: Pike's five cards twice (10). Pike in space 3.
Enemies: **Boar in 2, Wisp in 4.** Turn 1 intents: Charge 9 → space 2; Zap 4 → Pike.

## Scenario B — two against three

Deck: both companions' cards (10). **Bramble in 2, Pike in 4.**
Enemies: **Boar in 2, Wisp in 4, Stonebeak in 5.**
Turn 1 intents: Charge 9 → space 2 (Bramble). Zap 4 → Pike (lowest HP). Dive 7 → spaces 4–5 (Pike).
**Fixed opening hand for the first play:** Thump, Bark Skin, Jab, Lunge, Sweep.

Pike faces the Wisp but stands in the Dive. Stepping to 3 dodges it and faces an empty column;
stepping to 5 stays in the Dive but faces the Stonebeak. Bramble can block the Charge and Thump the
Boar, or step out of it — and then wait three turns to move again.

## What to notice — write it down as you play

1. **Is choosing where to stand a real decision most turns**, or is one answer obvious?
2. **Does "attacks fire straight ahead" make aiming matter**, or does it just make you miss?
3. **Does the Speed cooldown feel strategic, or just annoying?** Is Speed 1 ever fun?
4. **Block versus dodge:** does the slow wall and the fast skirmisher feel like two different
   monsters, or two sets of numbers?
5. **Dead draws:** does a hand full of the other companion's cards feel like a decision or bad luck?
6. **Does it feel like a monster game?** — the question the pivot was made to answer.
