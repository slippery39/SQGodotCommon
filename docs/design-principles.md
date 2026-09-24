# Design principles for deckbuilders — what we learned by building one

**Lessons with their evidence, not rules.** Each entry says what we tried, what happened, and the
principle we drew. A principle here is a note from a past session, never a veto: if a new idea breaks
one, say what the break COSTS (the evidence below is what it costs) and decide — breaking one on
purpose is a design decision, not an error. When a lesson is overturned, rewrite its entry; do not
delete the evidence.

Written 2026-09-24 from KIN (a monster-collecting roguelike deckbuilder on `ImmutableGameObjects`).
General lessons go here; KIN-specific ones stay in `KinJam.md` and the `design-card` skill.

---

## Tension — the game must never reward waiting

### 1. Every way to wait needs a cost
**Tried:** foes telegraphed attacks into fixed columns, and your monsters could step out of them for
free. **Happened:** the best play was to dodge until the hand favoured you — Shayne: "If you can just
wait something out indefinitely so you can choose when to strike only when it favors you, then that
makes the gameplay predictable and boring." **Fixed by** a second resource: an attack that finds no
monster hits the trainer, so dodging spends YOUR health. **Principle:** find the player's safest line
and ask what it costs. If nothing, it becomes the only line.

### 2. The stall test: does a longer battle pay this more?
If yes, it is a stall engine — repeatable healing is the classic. Thorns passes (it pays only when
struck, so it ends fights). A heal once per region passes (a longer fight earns no more of it).

### 3. Two clocks beat one
A single pool of HP lets the player trade freely inside it. Two pools that both lose the game (the
trainer's health AND the monsters') make every blow a choice of which to spend.

## Cards and the deck

### 4. Every card creates a decision
Ask: *when would I NOT play this, or play it differently?* No answer = filler. Filler is allowed as
the exception, not the deck.

### 5. No dead draws — a card must not wait on something the player does not control
**Tried:** a companion whose cards paid only when a foe attacked HER. **Happened:** foes spent turns
blocking or moving, so half her cards were dead at any time. **Tried:** every card owned by one
character, unplayable if it fainted. **Happened:** a knocked-out character's cards clogged the hand —
"pretty much a death sentence." **Principle:** a card's value should come from the player's
situation, not the opponent's schedule; losing a piece should not also poison the draw.

### 6. Adding to the team must not dilute the deck
**Tried:** each character brought its own cards into one shared deck. **Happened:** a new character
thinned everyone's draws, so recruiting had a cost — and Shayne disliked the combined deck after one
run. **Fixed by** a generic deck played ONTO characters, with each character's passive deciding what a
card means there (a Block card on the thorny one is damage). **Principle:** let the roster and the
deck grow independently; get variety from the interaction, not from more card pools.

### 7. Identity comes from opposite goals, not from numbers
**Tried:** three characters with different stats and similar cards. **Happened:** "they did not feel
different" — two were the same card with different numbers. **Fixed by** "same board, opposite
goals": one wants to be hit, one never to be where the hit lands, one moves the enemies. The playtest:
"completely different." **Principle:** give each character a different relationship to the board,
and a passive that makes ordinary cards mean something different in its hands.

## Enemies

### 8. Enemies ask questions
Every enemy property should have a VICTIM (a strategy it punishes) and an ANSWER (what beats it). A
flat tax — more HP, more damage — is bad for every deck equally and asks nothing.

### 9. Bosses are exams
A boss should test what its act taught: a huge one-column blow asks "can you step out?", a
whole-row blow asks "can you block or kill it first?".

### 10. Measure HOW things are won, not only how often
**Tried:** a gym leader with health you could race. **Happened:** a 90%+ gym win rate looked fine —
until the sim counted HOW: nine gyms in ten fell to racing the leader within four turns. **Principle:**
a win rate hides a dominant strategy. Record the route to victory; if one route is ~90%, the others
are not decisions.

## Readability

### 11. Playtests fail on READING before they fail on rules
**Happened:** the first playtest judged the mechanics while misreading them — a card played for the
wrong character, effects nobody could see happen. Identity colours, the owner's art on cards, and
floating feedback fixed the reading; only then could the rules be judged. **Principle:** fix what the
player cannot see before changing what the game does.

### 12. Show everything the player needs to plan
Hidden information in a telegraph game is a bug: turn order, where each attack lands, what ending
the turn will cost. Put the short form on the board and the full form one hover away. Computing it in
the engine and only DISPLAYING it keeps the two from disagreeing.

### 13. Certainty over dice when the game is about reading
**Choice:** catching is certain below a threshold, not a roll. In a game about reading telegraphs, a
failed roll after correct setup punishes the player for nothing. Save randomness for the draw.

## Structure and difficulty

### 14. Define the curve before judging difficulty
"Too easy" means nothing without a target. Our curve (Shayne): 90% of runs through act 3 of 10, 75%
through act 5, 25% win — easy early so players explore, hard late. Convert it to a survival target
per act and tune against that.

### 15. Scaling the player also inherits cancels out
**Tried:** multiplying later enemies' HP and damage (up to 2.6× and 4.2×). **Happened:** almost
nothing — 88% still won — because the player's catches came from the same scaled pools, and with at
most three enemies, three monsters always covered every attack. **Fixed by** a STRUCTURAL lever:
more enemies than the player has pieces, so something is always uncovered. **Principle:** if the
player's power grows from the same source as the threat, raise a different axis (count, coverage,
tempo), not the multiplier.

### 16. Check for snowballs
Losing one piece should not start a cascade. A fainted monster left its column open, which hit the
player, which lost more monsters — the in-battle bench exists to stop that. Ask of every loss
condition: what happens on the NEXT turn after the first thing goes wrong?

### 17. Early game is where a new rule bites hardest
Trainer health punished the one-monster opening most, exactly when the player had the fewest pieces.
Check every new rule at the weakest point of a run, not only the average.

## Measuring — sims, bots and phases

### 18. A sim measures its bot first
**Happened:** a one-move-ahead bot reported 67% and "the early game is brutal"; a trace showed it
walking into fights alone and never setting up a catch. A beam-search bot said 99%. **Principle:**
trace a run by hand before believing any number. Use the bot as a BASELINE, then compare real players
to it — above or below — rather than treating it as the truth.

### 19. Never report one number across acts
Three acts at 58% / 13% / 1.5% average to 24% against a 25% target — the aggregate reassured while
two acts were unplayable. Report per act, every time.

### 20. Name the phase: exploring or tuning
**Exploring:** design and build to find what is fun; tests prove a mechanic FIRES (cheap, never
stale); no sims — the next design change invalidates them. **Tuning:** the design holds still and
the sim decides the numbers. A session spent an hour simulating numbers the next change deleted.

### 21. Verify a mechanic fires before relying on it
An inert card throws no error. Test the CONSEQUENCE (a number that moved), not the construction (a
field that was set) — several silent no-op bugs were found only that way.
