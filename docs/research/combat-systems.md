# Combat systems — what could replace the lanes (2026-09-25)

**Why this exists:** Shayne's playtest of round one (2026-09-25): "The lanes and moving around just
isn't working for me. It still feels bad when you have a main monster attacking all the enemies, and
your other monsters just sit there attacking the opponent trainer." Lane combat is retired. Monster
Train's "row of creatures that attack every round, positioned strategically" works — but it must not
be cloned. This surveys the battle systems that could replace it. The earlier survey
(`companion-games.md`) covers Roguebook, Cobalt Core, Across the Obelisk, Monster Rancher, Pokemon and
Inscryption; this one adds the systems that make *a team of creatures* fight well.

## 1. What went wrong, stated as rules to judge the candidates by

1. **Position decided WHETHER a monster contributed, not HOW.** A monster in the wrong column did
   nothing useful (it hit the leader, or empty air). In every system below that works, every creature
   contributes every round; position changes who gets hit, when it acts, or which ability it uses.
2. **Positioning was per-turn micro** (five monsters, a step each, every turn) rather than a few
   weighty decisions. The good systems make position a *formation* you set and occasionally bend.
3. **The player watched.** Monsters acted on their own and the deck only adjusted numbers. The good
   systems give the deck verbs that change the fight's SHAPE (order, timing, targets).

## 2. The systems

| Game | Formation | Who hits whom | Timing | What the player does | Why it works |
|---|---|---|---|---|---|
| **Monster Train** (1, 2) | 3 floors + a Pyre; a row of units per floor | Every enemy hits your FRONT unit; every unit hits their front. Overflow is wasted | Enemies, then you, each turn; enemies climb a floor a turn | Summon units as cards (Ember + floor Capacity), spells, rooms | The front is the only defensive decision, so ORDER is the whole puzzle: tank in front, damage behind. Everyone attacks, wherever they stand. MT2 added a deployment phase before combat and champion abilities with cooldowns |
| **Wildfrost** | Two rows of three a side | A unit hits the FRONT enemy in its row (the other row if empty) | **Every unit has its own counter**, ticking down; at 0 it acts and resets. Enemies act first on ties | Deploy companions, play items; bend counters (Snow freezes, the bell redraws) | Timing is visible and bendable: the fight is "when does each thing go off", not "where does it stand" |
| **Super Auto Pets** | One queue a side | Only the two FRONT pets fight, simultaneously; a fainted pet's place is taken by the next | Fully automatic | Buy, order and level pets between fights | Abilities key off ORDER: start of battle, faint, hurt, "the pet ahead/behind". The formation IS the build |
| **Dicefolk** | Your creatures in a rotating chain | Only the front (the "leader") fights and takes hits | Dice decide the actions — **yours AND the enemy's** | Spend dice: attack, block, ROTATE | Rotation is the tactic: spin a hurt leader out, spin an on-rotate ability in. Some creatures do more damage by rotating than attacking |
| **Darkest Dungeon** | Ranks 1–4 a side | **Each skill is usable only from certain ranks and hits certain ranks**; shuffles move people | Speed order | Choose each hero's skill | Position decides WHICH moves you have. Enemies that knock you out of rank take your best moves away; monsters out of rank have weaker moves that walk them back |
| **Beastieball** (2025) | 2×2 court a side, front and back | Front: +50% offense. Back: +50% defense. A front/back pair in one column shares both | Turn-based, volleyball | Attack, move (row or lane), switch | One simple trade — offense or safety — that every decision runs through |
| **The Bazaar** | A board of items | Items fire on their own cooldowns; **adjacency** and leftmost/rightmost matter | Real-time cooldowns (haste, slow, charge) | Build and arrange the board between fights | Arrangement is the build: "triggers when the item on its left is used" |
| **Monster Sanctuary** | 3 v 3 | Free targeting | **All your monsters act, then theirs.** Every hit adds to a COMBO: +5% per hit for the next action | Order your monsters' actions | Sequencing is the synergy: many small hits first, one big finisher last. Resets each turn |
| **Siralim** | 6 v 6 | Free targeting | A timeline | Choose actions | Traits combine across the team ("when this attacks, all allies attack") — synergy over stats |
| **Chrono Ark** | 4 heroes | Free targeting | Shared turn | One shared hand; each hero adds 12 cards | A party's cards in one hand (as our monster decks already do) |

**The competition, checked because it must not be cloned either:**
- **Montabi** (Aug 2026) is a creature-collecting deckbuilder on a **3×3 grid**, with each creature's own card set, evolution, and a trainer who ends the run if defeated.
- **Decktamer** (Q4 2025) makes **every card a living monster** that acts on its own on a tactical board, with fusion and permadeath.

KIN's trainer deck played *onto* a caught team is already distinct from both. A grid or free movement would drift towards Montabi.

## 3. Candidate combat systems for KIN

Each keeps what the last three sessions built: catching, monster decks, the four deck strategies,
triggers, telegraphed foes, the map. Each is judged against the three rules in section 1.

### A. THE RELAY — one line, the front holds, the line fires back to front
Your monsters stand in a single line facing the foes' line. **Foes hit your FRONT** (Monster Train),
unless an intent says otherwise ("the back", "the weakest", "everyone"). **Every monster acts every
round, in order from the BACK of the line to the FRONT**, and each action builds on the last
(Monster Sanctuary's combo, or plain setup-then-payoff). So one decision — the ORDER — has two
consequences: who takes the blows, and who cashes the setup. **The tension is built in: your
finisher is most useful in front, and the front is where the blows land.**
- *Deck verbs:* swap two, send one to the front, shield the front, make the back act twice, hit
  their back.
- *Monster abilities:* "while in front", "the monster behind me", "when I become the front", "when
  the one ahead faints". Tokens are natural fodder in front (Summon becomes a real strategy).
- *Rules check:* everyone contributes (1) · the order is a formation, bent by cards (2) · the deck
  bends the shape (3).
- *Clone risk:* low. MT has floors and simultaneous attacks; SAP has no deck and only fronts fight.
- *Cost:* moderate. `Space` becomes a line index; `IntentTargets` becomes front/back/weakest/all;
  Speed order is replaced by line order; the board is redrawn as two facing lines.

### B. CLOCKS — every creature on its own countdown
Wildfrost's counters instead of Speed: each creature acts when its counter hits 0. The fight is a
timing puzzle — line up your big hits, stall theirs. **Deck verbs** tick clocks forward or back
(Hasten becomes −1, a freeze +1).
- *Rules check:* everyone contributes (1) · timing replaces position (2) · strong deck verbs (3).
- *Clone risk:* HIGH taken whole — counters plus front-of-row targeting *is* Wildfrost. As a
  LAYER on top of A or C it is safer.
- *Cost:* low on its own (Speed → a counter).

### C. THE RING — rotate the team, the front fights
Dicefolk's chain: only your leader fights and takes blows; the others wait in a ring, and ROTATING
is the core verb. On-rotate abilities ("when rotated in, strike") make rotation damage. **Deck
verbs** rotate yours, rotate THEIRS, lock a leader in place.
- *Rules check:* (1) is the weak point — the ring waits, the same failure as the lanes, unless every
  monster has a rotate-in or passive ability. (2) is strong: rotation is one weighty verb.
- *Clone risk:* moderate (Dicefolk uses dice, not cards; ours would be cards).
- *Cost:* moderate.

### D. RANKS — where a monster stands decides which moves it has
Darkest Dungeon: a line of 3–4 ranks; each monster's moves (its cycle, and its monster-deck cards)
require a rank and target ranks. Foes shuffle you out of your best rank. Beastieball's simpler
version: two rows, front = offense, back = defense.
- *Rules check:* everyone contributes if placed well (1) · position is capability, not aim (2) ·
  cards can shuffle and restore (3).
- *Clone risk:* low in a deckbuilder.
- *Cost:* high — every move and card needs rank data, and it is the most rules to read.

### E. ACTIVE + BENCH — one fights, the bench powers it
Pokemon TCG: one active monster per side, a small bench that gives passive support; switching
(retreat) costs energy or a card. Truest to the monster-collecting fantasy.
- *Rules check:* (1) fails unless the bench does real work — the same waiting problem.
- *Clone risk:* low for a roguelike; it is the genre's own grammar.
- *Cost:* moderate.

## 4. Recommendation

**A, THE RELAY**, with B's clocks kept in reserve as a layer if timing needs more texture.
- It keeps what Shayne likes in Monster Train: a row of creatures that all fight every round,
  positioned strategically.
- Its one positional decision (order) carries two consequences (defense and sequencing). That is
  its own twist, not MT's floors or SAP's front-only brawl.
- It rescues the round-one work: tokens become front-line fodder, Spellcraft reaches past the front,
  Surge pays for reordering, and Discard + Draw is untouched.
- It fixes all three failures by construction.

**Before building: paper-prototype A against C** (the other genuinely different shape), one scenario
each, as `docs/paper/companion-slice.md` did. Exploring, so no sims.

## Sources

- [Wildfrost Wiki – Counter](https://wildfrostwiki.com/Counter) · [Attack](https://wildfrostwiki.com/Attack) · [Checkpoint review](https://checkpointgaming.net/reviews/2023/04/wildfrost-review-dynamic-and-delightful/)
- [Monster Train Wiki – Battle](https://monster-train.fandom.com/wiki/Battle) · [MT2 Wiki – Combat](https://monstertrain2.miraheze.org/wiki/Combat) · [Checkpoint – Monster Train 2 review](https://checkpointgaming.net/reviews/2025/05/monster-train-2-review-tracks-ahead/) · [Skybox – MT2 review](https://skyboxcritics.com/2025/05/21/monster-train-2-review-draw-and-clear-until-it-is-done/)
- [Super Auto Pets Wiki – The Basics](https://superautopets.wiki.gg/wiki/The_Basics) · [Faint trigger](https://superautopets.fandom.com/wiki/Faint_(Trigger)) · [a327ex – SAP mechanics](https://a327ex.com/posts/super_auto_pets_mechanics)
- [GodisaGeek – Dicefolk review](https://godisageek.com/reviews/dicefolk-review/) · [Gosunoob – Dicefolk review](https://www.gosunoob.com/reviews/dicefolk-review/)
- [Darkest Dungeon Wiki – Arbalest strategy](https://darkestdungeon.wiki.gg/wiki/Arbalest_(Darkest_Dungeon)/Strategy) · [Steam – enemy positioning](https://steamcommunity.com/app/262060/discussions/0/3211505894125676158/)
- [Beastiepedia – Field](https://www.beastiepedia.net/wiki/Row) · [Console Creatures – Beastieball review](https://www.consolecreatures.com/review-beastieball/)
- [TheGamer – The Bazaar mechanics](https://www.thegamer.com/the-bazaar-beginner-tips-tricks-mechanics-explained-guide/) · [Mobalytics – Bazaar guide](https://mobalytics.gg/the-bazaar/guides/beginner-guide)
- [Monster Sanctuary Wiki – Combo](https://monster-sanctuary.fandom.com/wiki/Combo)
- [Siralim Ultimate Wiki – Combat](https://siralimultimate.wiki.gg/wiki/Combat) · [Jim Mander – Siralim Ultimate](https://youdonthaveto.substack.com/p/siralim-ultimate)
- [Automaton – Chrono Ark](https://automaton-media.com/en/indie-games/chrono-ark-is-the-hot-new-deckbuilder-roguelike-rpg/)
- [The Magic Rain – Montabi](https://themagicrain.com/2026/08/montabi-brings-creature-collecting-and-deckbuilding-together-in-new-tactical-roguelike/) · [FinalBoss – Decktamer](https://finalboss.io/decktamer-brings-creature-collecting-mayhem-to-roguelike-dec)
