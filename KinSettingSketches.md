# Setting — GENERIC FANTASY as a substrate, and three frames kept for later

**Status: DECIDED 2026-09-22 — and what was decided is deliberately NOT one of the three frames
below.**

> **The direction is generic fantasy, in the MTG register: enough to get started, and permissive
> enough to try different themes on top of.** Goblins, knights, undead, beasts, a rival mage with a
> line of creatures. Legible without explanation, cheap to draw, and it commits to nothing.
>
> **This is a SUBSTRATE, not a frame, and that is the point.** Each of the three frames below is
> high-concept and each one narrows what content is allowed to exist — a circuit of small-town
> contests cannot field a lich, a herd migration cannot field a rival wizard. A neutral fantasy
> vocabulary unblocks every naming job — cards, enemies, acts, palette, the 56 SVGs, the title —
> **without making the setting decision at all**, which is the right shape for a game whose
> mechanics are still open.
>
> **What it costs, recorded honestly:** generic fantasy is the least distinctive option on this
> page, and it is NOT the "monster-collecting-adjacent, warm, not cosy" register this document had
> already settled on. That register is not abandoned — it is deferred. A substrate can carry a
> register later; a frame has to be chosen up front.
>
> **The three frames are therefore NOT deleted.** They stop being a choice and become candidate
> THEMES to drop onto the substrate once themes are swappable — which is what the next bullet makes
> real.
>
> **The two code prerequisites below are no longer conditional.** "If regions become real places"
> was the hypothetical; "allows us to try out different themes" is the requirement. So both are now
> features to build, not caveats:
>
> - **Enemies are not act-scoped.** `PlayableOn(floor)` is `MinFloor <= floor` and run-wide, so act
>   3 still draws act 1's creatures.
> - **`ActMap.Order` is a fixed array of three** with no pool to draw a variant from.

> **THE WHOLE GAME IS EXPLORATORY, the companion most of all.** Nothing below or in any other design
> doc is settled. See the root `CLAUDE.md` — a past note is evidence, never a veto.

> **Decided 2026-09-22, and all three cost nothing because they are what the code already does.**
> **Two parts of this block are SUPERSEDED by the header above** — "the re-theme waits until the
> game has been played by hand" (it is being done now, on a substrate rather than a frame), and
> "nobody is sold on what the Opponent IS" (generic fantasy answers it: a rival mage with a line of
> creatures, which is what the code already models). The rest stands.
>
> - **One companion, FOR NOW, because it is the cheapest thing to test.** What several would cost,
>   so the trade is on the table rather than closed: five lanes minus the companions' own leaves
>   fewer to contest; bringing three axes at once means no reward is more correct than another,
>   which was the build-around premise; and the upgrade curve would need re-deriving. All three are
>   costs to pay or design around, not reasons it cannot be done.
> - **Decks accumulate**, and rewards should lean toward the current region rather than resetting.
> - **The Opponent stays as it is** — a thing that summons reinforcements and telegraphs. Nobody is
>   sold on what it IS fictionally; that is an open question to answer by playing, not by writing.
>
> **And the meta-decision: stop modelling, start playing.** The standing rule from the card pass
> still holds — *we are exploring what is fun, not enforcing what is written.* The naming does not
> block play, so the re-theme waits until the game has been played by hand and something has been
> found to be fun.
>
> Where the collecting fantasy lives, given one companion: **horizontally BETWEEN runs** (which
> companion you bring, more unlocked over time) and **vertically WITHIN one** (it grows). Both
> already fit what is built.

**Everything from here down is the earlier three-frame proposal, kept as candidate themes.** The
per-frame rename tables are still the most useful thing on the page: they are worked examples of
what a themed pass over the card pool looks like, and the generic-fantasy pass can crib their shape
without taking their words.

The register these were written for — **monster-collecting-adjacent, warm, not cartoonish and not
cosy** — is deferred with them, not cancelled.

---

## The reframe: a FRAME, not a setting

A run is three acts of fifteen floors. So the thing to pick is not one setting — it is a **frame**:
a premise that explains why you keep moving, plus **regions** that hang off it. Each act is a
region. More regions can be written later and dropped into the same slot, so act 2 is "whichever
region came up this run" rather than always the same one.

That is the structure this document is written for. Each frame below gives **three regions to ship**
and **two spares**, to show the frame can keep producing them.

## What an act actually is, in code

| | today | for multi-region |
|---|---|---|
| name + description | `ThemeDefinition` | works as-is |
| boss | `ThemeDefinition.Boss` | works as-is |
| card rewards | `RewardPool(theme)` — shared pool + per-act cards | **works as-is**, and is the model |
| **enemies** | `PlayableOn(floor)` — `MinFloor <= floor`, run-wide | **needs scoping per act.** Today act 3 still draws act 1's creatures, which reads as wrong the moment acts are different places |
| region variants | nothing — `ActMap.Order` is a fixed array of three | needs a pool per slot, and a seeded pick at run start |

Neither change is large. Both are prerequisites, not polish.

---

## Frame A — THE CIRCUIT

**You and your companion travel a circuit of small-town contests.** Each region is a venue with its
own local creatures and a resident champion. You are not killing anything — you are *out-fielding*
the other handler, and the Opponent is that handler's line.

The closest to a straight monster-collecting read, and the easiest to keep writing: a circuit can
have any number of stops, forever.

- **The Opponent** is a rival handler. Bosses are named handlers with a signature creature.
- **Tone:** competitive, warm, a bit rustic. Nobody dies; a creature that loses a lane is *drawn*.
- **Palette:** dusty gold, orchard green, sun-bleached canvas, deep evening blue for contrast.

| | region | what fields it | the champion |
|---|---|---|---|
| 1 | **Windfall Fair** — an orchard meeting at harvest | orchard pests, working dogs, hedge creatures | **The Orchardist** |
| 2 | **Saltmarket** — a harbour town's tidal contest | shore birds, crabs, net-trained things | **Harbourmaster Ruell** |
| 3 | **The High Pitch** — a mountain arena above the cloud line | goats, raptors, cold-weather beasts | **The Standing Champion** |
| spare | **Coldwater Ferry** — a contest held on a moving barge | river creatures | — |
| spare | **The Ashfield Meet** — a mining town, soot and lamps | burrowers, lamp-moths | — |

**Companions** (same five mechanical axes, renamed): **Pip** the terrier (attrition — pays for what
died), **Barrow** the tortoise (survival), **Tally** the magpie (volume — unchanged, it already
fits), **Gil** the heron (the face), **Bramblejack** the hedgehog (spatial).

**Cards:** Scavenger → **Gleaner**. Bulwark → **Hedge-Standing**. Ash Walker → **Longwalker**.
Lantern Bearer → **Lamp-Girl**. Pyre Keeper → **Stockman**. Gallows Feast → **Prize Draw**.
Butcher's Bill → **Forfeit**. Twice Buried → **Second Wind**. Drone Swarm → **Pigeon Loft**.
Salvage Rig → **Cart and Tack**. Reactor Crew → **The Stewards**. Long Watcher → **Old Hand**.

---

## Frame B — THE LONG MIGRATION

**You and your companion move with a herd, and the herd cannot stop.** Each region is terrain you
have to get across before the season turns. The Opponent is whatever holds the route — a territorial
animal, a swollen crossing, something that has made the pass its own.

Warmer and more naturalistic than A, and quietly the most *emotional* frame: you are protecting
something, not competing.

- **The Opponent** is the route's holder. Bosses are large, territorial, and not evil.
- **Tone:** gentle-epic, weather and distance, nothing grim but real stakes.
- **Palette:** pale sky, grass gold, river slate, warm amber for your side.

| | region | what fields it | what holds it |
|---|---|---|---|
| 1 | **The Floodplain** — first crossing, water rising | waders, reed-dwellers, biting things | **The Weir-Keeper** |
| 2 | **Pine Ridge** — cold forest, narrow paths | forest predators, territorial birds | **The Hoarder** |
| 3 | **The Salt Flats** — open, exposed, no cover | scavengers, heat things, mirages | **The Long Shadow** |
| spare | **The Burn** — recently fired grassland, regrowing | ash-feeders, opportunists | — |
| spare | **Nightpasture** — a crossing made after dark | moths, owls, glowing things | — |

**Companions:** **Ash** the grey dog (attrition — the name survives and stops meaning ash-the-ruin),
**Moss** the tortoise (survival), **Kite** (volume), **Pike** the heron (the face — unchanged),
**Burr** (spatial).

**Cards:** Scavenger → **Follower**. Bulwark → **Windbreak**. Ash Walker → **Far-Walker**.
Lantern Bearer → **Nightwatch**. Pyre Keeper → **Herdsman**. Gallows Feast → **Lean Season**.
Butcher's Bill → **The Culling**. Twice Buried → **Returns**. Drone Swarm → **Starlings**.
Salvage Rig → **The Wagon**. Reactor Crew → **The Drovers**. Long Watcher → **Old Bull**.

---

## Frame C — THE MENAGERIE

**A collection got loose, and you are putting it back.** Each region is a wing of a vast overgrown
estate where the exhibits have taken over. Invented creatures rather than real animals, so the art
can go strange without going grim.

The most distinctive and the most work: it needs invented creature designs rather than "a heron".
It is also the frame with the most room for *weird*, which generated art is unusually good at.

- **The Opponent** is an escaped headliner — the wing's biggest exhibit, now running it.
- **Tone:** whimsical-Victorian, brass and glass, slightly absurd, never menacing.
- **Palette:** brass, verdigris, glasshouse green, velvet red as the accent.

| | region | what fields it | the headliner |
|---|---|---|---|
| 1 | **The Glasshouse** — humid, overgrown, panes gone | plant-things, climbers, pollinators | **The Century Bloom** |
| 2 | **The Aviary** — netting collapsed, flight everywhere | winged exhibits, nest-builders | **The Weathercock** |
| 3 | **The Deep Tanks** — flooded basement galleries | tank things, lamp-eyed swimmers | **The Exhibit** |
| spare | **The Clockwork Wing** — automata that kept running | wind-ups, brass beasts | — |
| spare | **The Reading Room** — whatever lived in the books | paper things, silverfish | — |

**Companions:** **Sixpence** (attrition), **Mantle** (survival), **Abacus** (volume), **Spindle**
(the face), **Creeper** (spatial).

**Cards:** Scavenger → **Loose Exhibit**. Bulwark → **Display Case**. Ash Walker → **The Escapee**.
Lantern Bearer → **Lamplighter**. Pyre Keeper → **The Curator**. Gallows Feast → **Feeding Time**.
Butcher's Bill → **Inventory**. Twice Buried → **Re-Catalogued**. Drone Swarm → **The Swarm Case**.
Salvage Rig → **The Trolley**. Reactor Crew → **Night Staff**. Long Watcher → **The Old Exhibit**.

---

## How they compare

| | A — Circuit | B — Migration | C — Menagerie |
|---|---|---|---|
| closest to monster-collecting | **yes** | partly | partly |
| explains why you keep moving | a schedule | a season | a list |
| explains the Opponent naturally | **yes** — a rival | mostly — a holder | **yes** — an escapee |
| new regions are cheap to add | **very** | moderate — geography must connect | **very** — wings |
| art subjects | real animals | real animals | **invented** — most distinctive, most work |
| risk | could read generic | could read slow | could read fussy |

**If you want my pick: A, with C's willingness to invent creatures.** A gives the clearest reason
for a ladder of fights against a *person's* line rather than wildlife, which is what the Opponent
actually is in code — and "one more stop on the circuit" is the cheapest possible justification for
an endless supply of regions.

## What I need to start

1. **A frame** — one of the three, or a mix.
2. **The title**, or permission to propose some once the frame is picked.
3. Whether regions are **fixed in order** (as now) or **drawn from a pool per slot** — the second is
   the variant-acts idea and needs the two code changes listed at the top.
