# Round one: deck strategies, and monsters that shape how you play cards

**Paper draft, 2026-09-24, for Shayne to cut.** Nothing here is built or decided. Every name is a
working name and every number is a guess: we are EXPLORING. What survives goes into `KinJam.md`.

**The idea (Shayne):** so far cards have only done things to monsters. Reverse it: **the monsters on
the board change how your cards work.** They boost spell damage, pay off discards, or give you
energy when a condition is met. A monster can be a relic that fights, and because it stands in a
column, the aim-and-dodge game now protects your deck strategy too.

**Round one covers four strategies:** Spellcraft, Discard + Draw, Surge and Summon. Combat and
Block reuse what Pikae and Bramble alredy are.

---

## How every card and monster ability is classified

| Field | Values |
|---|---|
| **Role** | **Enabler** (produces what a strategy needs) · **Payoff** (spends it) · **Engine** (an enabler that repeats; usually a monster) · **Bridge** (links two strategies) · **Answer** (built for one foe or leader) · **Filler** |
| **Strategies** | 0–2 tags |
| **Power band** | **Pushed** (a build-around that pulls you in; rare) · **Standard** · **Narrow** (strong in its strategy, weak outside it) · **Filler** (honest, never exciting) |

Two rules make the classification mean something:
- **An enabler must be playable alone**, so picking one early is never a trap.
- **A payoff must be weak alone**, so taking one is a commitment.

## Decided / proposed rules

- **DECIDED (Shayne): the end-of-turn discard does NOT count as discarding.** Only a card or an
  ability discards. Discard stays an active choice, and holding cards earns nothing.
- **Proposed: SPELL = a card that deals damage by itself, dropped on a foe.** It is not a monster's
  attack. Strike ("it attacks now") stays Combat. The line has to be sharp because auras and the
  Warden care about the difference.
- **Proposed: a monster's deck ability works only for its trainer.** A wild creature shows only its
  cycle, plus a foe trait if it has one. The ability wakes when it is caught.
- **Proposed: a tester, once caught, becomes a bridge for the strategy it punished.** The Warden
  halves spells against it; caught, it gives Block when you cast them. The creature that taught you
  the lesson becomes the tool for it.

## What we have now, classified

| Card | Role | Strategies | Band |
|---|---|---|---|
| Guard, Bulwark | Enabler | Block | Filler, Standard |
| Dash, Sprint, Gust, Tailwind | Enabler | Motion (Sprint: Pike only) | Filler / Narrow / Standard |
| Rally, Frenzy, Strike, Whirl | Enabler / Filler | Combat | Filler, Standard |
| Thornhide, Bristle | Enabler | Block (Bramble only) | Narrow |
| Hasten | Bridge | Combat, timing | Standard |
| Stagger | Answer | — | Standard |

**The finding: of 17 cards, not one is a payoff.** Every payoff in the game is a passive (Thorns,
Momentum, Off-Balance). That is why nothing combines.

---

## 1. SPELLCRAFT: win without aiming — BUILT 2026-09-25 (practice scenario 4)

**What it is for:** damage that does not care about columns. It answers homing attacks, foes that
move, and leaders who punish position. It loses to the Warden.

| Card | Cost | Text | Role · Band |
|---|---|---|---|
| **Zap** | 1 | Drop on a foe: deal 4. | Enabler · Filler (a fine card anywhere) |
| **Arc** | 2 | Deal 2 to every foe. | Enabler · Standard (an answer to swarms) |
| **Spark Scroll** | 1 | Deal 3 to a foe. TOSS: deal 3 to a random foe. | Bridge (Discard) · Standard |
| **Overload** | 2 | Deal damage equal to all spell damage dealt this turn. | Payoff · Narrow (0 alone, 12 after two Zaps) |
| **Focus** | 1 | Your spells this turn also hit the foe's neighbours. | Payoff · Narrow |

*TOSS = what the card does when a card or ability discards it.*

**Monster engines**
- **Emberling** (HP 12, Speed 2; cycle: Ember 2, Ember 2). *Aura: your spells deal +2.*
  - A fragile body carrying the whole strategy, so you have to protect it.
  - Wild: a weak chip creature. Easy to catch and easy to overlook.
- **Echo Owl** (HP 16, Speed 1; cycle: Echo, Peck 3). *Move, ECHO: replays the last spell you
  played this turn.*
  - Pushed. Speed 1 means it acts last, after your whole hand, so the decision is which spell to
    play LAST.
  - Wild, Echo does nothing (no trainer), so it reads as a slow pecker.

**Foe that tests it: the Warden** (HP 22, Speed 1; cycle: Shell 6 Block, Slam 8)
- *Wild trait: spells deal half to it.* Victim: Spellcraft. Answer: Combat, or Retaliate.
- *Caught: when you play a spell, the Warden gains 3 Block* (a bridge from Spellcraft to Block).

**Leader that tests it: the Warden Queen.** Rule: spells deal half to her and her creatures. The
question: can your monsters kill without your deck? Answer: Combat, or Bramble and Block.

---

## 2. DISCARD + DRAW: sort the deck, and get paid for it — BUILT 2026-09-25 (practice scenario 3)

**What it is for:** consistency (draw) turned into damage (discard payoffs). The two strategies are
built as a pair because each enables the other.

| Card | Cost | Text | Role · Band |
|---|---|---|---|
| **Sift** | 0 | Draw 2, then discard 1. | Enabler (both) · Filler (card selection anywhere) |
| **Rummage** | 1 | Discard any number of cards, then draw that many. | Enabler (both) · Standard |
| **Ration** | 1 | Gain 5 Block. TOSS: +1 energy. | Bridge (Surge, Block) · Standard |
| **Scrap Hammer** | 3 | It attacks now: 6 + Power. Costs 1 less per card discarded this turn. | Payoff (Discard, Combat) · Narrow |
| **Page Storm** | 1 | It attacks now: 1 + Power for each card in your hand. | Payoff (Draw) · Narrow |

**Monster engines**
- **Magpie** (HP 14, Speed 3; cycle: Snatch 3, Hop).
  - *Caught, aura: whenever you discard a card, deal 2 to a random foe.*
  - *Wild trait, THIEF: its Snatch takes the top card of your draw pile until it dies.*
  - The same creature is the test when wild and the engine when caught.
- **Inkling** (HP 16, Speed 2; cycle: Splash 2 three wide, Ink 4 Block).
  - *Aura: the first time each turn you draw during your turn, +1 energy.*
  - Pairs Draw with Surge. Sift turns it on for 0 energy.

**Foe that tests it: the Hoard Drake** (HP 26, Speed 1; cycle: Hoard 6 Block, Tail 7)
- *Wild trait: whenever you draw or discard during your turn, it gains 2 Block.* Victim: Discard
  and Draw. Answer: kill it first, or leave the engine idle while it stands. Either way it becomes
  a priority target.
- *Caught: whenever you discard, it gains 3 Block* (a bridge from Discard to Block).

**Leader that tests it: the Archivist.** Rule: each time you draw or discard during your turn, his
creatures gain +1 attack for the rest of the fight. The question: does your engine end the fight
before it feeds his?

---

## 3. SURGE: more energy than the turn allows, at a price — BUILT 2026-09-25 (practice scenario 5)

**What it is for:** expensive bombs, and several big turns in a row. **The conditions that grant
energy are where the board and the deck lock together** (Shayne's example).

| Card | Cost | Text | Role · Band |
|---|---|---|---|
| **Surge** | 0 | +2 energy. Next turn, 1 less. | Enabler · Standard (borrowing: the tension is built in) |
| **Quicken** | 1 | The next card you play this turn costs 0. | Enabler · Standard |
| **Battle Cry** | 1 | Draw a card. If a foe died this turn, +2 energy. | Bridge (Combat) · Standard |
| **Unleash** | X | It attacks now: 4 × X, three wide. | Payoff · Pushed, rare |
| **Meteor** | 4 | Deal 14 to a foe and 4 to each neighbour. | Payoff (Spellcraft) · Narrow |

**Monster engines**
- **Glowmoth** (HP 10, Speed 3; cycle: Dust 1 three wide, Flutter).
  - *Condition: at the start of your turn, if it was not hit last turn, +1 energy.*
  - Protecting it IS the energy, so position is paying for your hand.
- **Stormbuck** (HP 20, Speed 2; cycle: Antler 5, Rear 4 Block).
  - *Trigger: when a foe dies during your turn, +1 energy.*
  - **Timing trap:** most kills happen at END of turn, where energy is useless. "During your turn"
    rewards the kills you make with your hand (Hasten, Strike, spells). That makes it a bridge,
    which is the point.

**Foe that tests it: the Hushcap** (HP 14, Speed 2; cycle: Spores 3 homing, Cap 4 Block)
- *Wild trait: while it stands, your first card each turn costs 1 more.* Victim: Surge and
  cheap-card turns. Answer: kill it first, since it is fragile.
- *Caught, a channel ability: the first card you play ON the monster beside it each turn costs 1 less.*

**Leader that tests it: the Magistrate.** Rule: cards that cost 2 or more cost 1 more. The
question: are you all bombs? Answer: cheap cards, Draw, and Quicken.

---

## 4. SUMMON: tokens — BUILT 2026-09-25 (practice scenario 6)

**What tokens are (proposal):**
- A simple ally: one move, no ability of its own, cannot be healed or caught, and gone at the end
  of the battle.
- It takes a column, steps and swaps like any monster, and does not count toward losing.
- **It FADES after N turns**, shown as a countdown.

**Why fading is necessary:** free bodies filling the two empty columns would undo trainer health
(the fix for free dodging) and blunt "more foes than monsters", the lever the curve rests on.
Fading is the cheapest guard. A cap on how many tokens can stand, and Trample, are the others.

| Card | Cost | Text | Role · Band |
|---|---|---|---|
| **Sow** | 1 | Summon a Sprout (3 HP, no move, fades in 2). When it faints, its neighbours gain 3 Block. | Enabler (Block) · Filler |
| **Call Sparks** | 1 | Summon two Sparks (1 HP, Speed 3, hit 2 ahead; fade at end of turn). | Bridge (Spellcraft: a spell that walks) · Standard |
| **Decoy** | 1 | Summon a Decoy (5 HP, fades in 1). Homing attacks aim at it. | Enabler + Answer (homing) · Standard |
| **Swarm** | 2 | Each of your tokens attacks now: 2 + Power. | Payoff · Narrow |
| **Offering** | 0 | One of your tokens faints: draw 2, +1 energy. | Payoff (Draw, Surge: sacrifice) · Narrow |

**Monster engines**
- **Broodvine** (HP 24, Speed 1; cycle: Brood, Lash 4).
  - *Move, BROOD: summons a Grub (2 HP, bites 2, fades in 2) in an empty space beside it.*
  - Wild, it broods for the FOES, which fills their columns and asks for Arc.
- **Howler** (HP 20, Speed 2; cycle: Bite 4, Howl).
  - *Aura: your tokens have +2 HP and +1 to their attacks.*
  - *Howl: +1 to its next Bite for each token on your board.*

**Foe that tests it: the Ironhorn** (HP 24, Speed 1; cycle: Charge 8, Stomp 4 three wide)
- *Wild trait, TRAMPLE: damage beyond what fells its target goes to the trainer.* Victim: Summon
  walls. Answer: real monsters with Block, or Stagger.
- *Caught: its Trample carries into the foe beside the one it fells* (a bridge to Combat).

**Leader that tests it: the Harrower.** Rule: his creatures deal double to tokens, and all of them
Trample. The question: are your walls made of paper?

---

## Where they would live

A suggestion. **Each area's pool carries a strategy**, so choosing an area is choosing a strategy
as well as a threat:

| Area | Adds |
|---|---|
| Ember Crags | Emberling; Echo Owl (rare) |
| Misty Marsh | Magpie, Hoard Drake; Inkling (rare) |
| Stony Ridge | Stormbuck, Warden; Glowmoth (rare) |
| Mossy Hollow | Broodvine, Hushcap; Howler (rare) |

**Leaders:** a pool per stage of the run, one previewed at each region's town. The four above are
mid-run leaders. The early pool keeps the simple exams (the Old Tusker: "can you step out?"; the Old
Mire: "can you block the row?").

## What the engine needs (all of it, once, before any content)

1. **Events** for card played, card discarded (by a card or ability only) and card drawn during
   your turn, which abilities listen to. Abilities are data: an ability record plus an amount, no
   delegates.
2. **A damage source** (spell or move) on every hit.
3. **Energy changes:** temporary energy, debt against next turn, cost modifiers (the next card,
   the first card, cards costing 2 or more), and X costs.
4. **TOSS** on a card.
5. **Tokens:** an ally flagged as a token, with a fade countdown.
6. **Foe traits** (Half vs Spells, Thief, Trample, Tax) and **leader rules**, the same kind of data.

## Answered (Shayne, 2026-09-24)

1. **Starter deck:** answered by the MONSTER DECK idea below. The starter monster brings its
   strategy's first cards.
2. **Wild creatures show only their cycle.** The deck ability wakes when caught. DECIDED.
3. **Tokens fade.** DECIDED.
4. **Cuts wait for play.** Nothing can be judged until the cards are in hand.

## MONSTER DECKS (Shayne's idea, 2026-09-24): DECIDED, and built for the starters

**Each monster brings a small deck of its own. Its cards are in your draw pile only while it
fights.** A catch arrives with its strategy's first cards, and who fights becomes a deck decision.

The shape (confirmed by Shayne):
- **2–3 cards per monster, capped at about 4.** Trainer deck 10 + three monsters × 3 = 19 cards,
  about one card from each monster per hand.
- **Its cards join at the start of a battle if it is on the board.** A benched monster's cards are
  shuffled in when it steps in. **When it faints, its cards leave every zone.** Faints happen only
  at END of turn, after the hand is discarded, so a card never vanishes from your hand.
- **Its cards play on ANY monster**, like trainer cards; the owner only decides whether they are
  present. They carry the owner's colour so you can see what leaves with it.
- **A monster deck carries ENABLERS for its own ability; payoffs come from rewards.** Otherwise
  each monster is a closed box (Emberling brings Zaps and boosts Zaps) and the reward screen has
  nothing left to decide.
- **Manipulated in towns:** *Train* (swap a card in its deck for one of a few offered), *Teach*
  (move a card to the trainer deck: permanent, survives faints; costs gold), and a reward card can
  go into a monster's deck instead of the trainer's. That last one lets you tailor the deck for a
  gym by choosing who fights.

What it costs:
- **It reverses "monsters cost nothing to include"** (AUTO-BATTLE v1): fielding a monster now
  changes your deck.
- **A faint loses the monster's ability AND its cards** (principle 16, snowballs). A smaller deck
  draws the rest more often, which softens it.
- **The cap matters:** without it, a benched monster is a free place to park bad cards.
