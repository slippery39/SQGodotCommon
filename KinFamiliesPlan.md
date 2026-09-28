# FAMILIES — monsters as engines, cards as fuel (design, 2026-09-27)

**Status: REVIEWED — building the Grove + Ember slice.** Shayne: "Our cards and monsters just feel kind of generic.
The fun part of card games is figuring out and achieving combos and synergies." The brief below is his
answers from an interview (five rounds, 2026-09-27); the family drafts are a proposal to react to.
Exploring, not tuning: every number is a guess; tests prove things fire; no sims until a design holds.

## 1. The brief (Shayne's answers)

| | Decided |
|---|---|
| Feel | **Slay the Spire's deckbuilding × a monster-team game** (Pokémon / Cassette Beasts) |
| The payoff | **An ENGINE that snowballs into a HUGE TURN** — built over turns, drawn into, or cascaded down the relay |
| What is generic now | everything: cards are numbers, monsters are samey, nothing to build toward, rewards don't excite |
| Centre of gravity | **MONSTERS ARE THE ENGINE** — each a build-around (like a relic or a clan); **cards are the fuel** |
| Families | **Four families that mesh** — types in feel, **no weakness chart**; tags exist only for YOUR synergy. Two of a family combo; three is a build |
| A monster | **a rich kit**: a rule-changing passive, a move cycle that matters, a monster deck of signature cards. **A mix** of safe generalists and risky specialists. Each can be built around — or benched |
| A catch excites by | changing a rule, completing a combo, growing with you, and sometimes being a jackpot |
| Commitment | **the build emerges from finds**; starters are family flagships, the rest is caught |
| Line | families **loosely** lean front (cash in) or back (set up) — the relay order becomes family craft |
| Cards | monster decks bring signatures; the reward pool offers family cards **weighted to your team's families**, plus neutral glue. **Medium decks** |
| Growth | **training (moves), held items, card upgrades** — not evolution, for now |
| Enemies | **leaders are exams** that test a build; **wild foes are the catchable families**; **some foes disrupt** |
| First slice | **two families, deep — Grove + Ember** — built and played before the other two |

## 2. The four families

| Family | Area | Engine | Leans | Flagship |
|---|---|---|---|---|
| **GROVE** | Mossy Hollow | growth — bodies that grow each turn, Block that stays, walls that bite | FRONT | Bramble |
| **EMBER** | Ember Crags | kindling — every spell stokes a fire that burns hotter all fight | BACK (+ a front finisher) | Pike |
| STORM | Stony Ridge | tempo — surplus energy, extra actions, momentum | back | Gale |
| MIRE | Misty Marsh | hoarding — draw, discard and hoard; payoffs scale with what passes through your hand | back | (caught) |

Storm and Mire are named here so the reward pool and the areas stay coherent; they are designed after
the first slice is played. Round one's four strategies (Summon, Spellcraft, Surge, Discard + Draw)
already live in these areas' creatures, so each family starts from built, tested pieces.

## 3. GROVE — draft

**The family's resource: GROW.** A creature with Grow gains **+1 Power and +2 max HP (and HP) at the
start of each of your turns** while it stands. The engine is TIME: every turn a Grove line survives, it
is bigger. **The big turn: HARVEST** — cash a grown line in all at once.

**Also: ROOT.** Rooted Block does not vanish at your turn start — it stacks turn on turn.

Monsters (existing bodies, new kits — each a build-around):

| Monster | Role | Passive (the engine rule) | Cycle | Monster deck |
|---|---|---|---|---|
| **Bramble** (starter) | safe FRONT wall | **THORNWALL**: a foe that attacks her takes damage equal to her BLOCK | Bash · Brace (Rooted) | Thornhide, Bristle |
| **Broodvine** | back ENGINE | **NURSERY**: your tokens have Grow | Brood (summon a Sprout) · Lash | Sow, Graft |
| **Mosshell** | safe front | **MOSSBACK**: its Block is Rooted | Shell Up · Slam | Root |
| **Hushcap** | risky mid | **SPORES**: when a token of yours falls, draw a card and gain 1 energy | Puff (weaken front) · Drift | Overgrow |
| **Howler** (rare) | PAYOFF | **ALPHA**: at end of turn, each of your tokens attacks the front for its Power | Howl (+2 Power to tokens) · Bite | Harvest |

Grove cards (the fuel; reward pool, Grove-tagged):
- **Sow** (1) — summon a Sprout (3 HP) in front. *Enabler*
- **Graft** (1) — a monster gains Grow for this fight. *Enabler, the key to non-token builds*
- **Root** (1) — gain 6 Rooted Block. *Enabler for Thornwall*
- **Overgrow** (2) — everything with Grow grows now, twice. *Accelerator*
- **Thicket** (1) — each Grove ally gains Block equal to its Power. *Bridge: growth → wall*
- **Harvest** (2) — each of your tokens falls; each deals its HP to their front. *THE BIG TURN*
- **Deep Roots** (0, upgrade target) — Rooted Block doubles. *Payoff for a patient wall*

Two example engines: **Bramble + Mosshell + Root** (a wall whose Thorns climb every turn), and
**Broodvine + Howler + Sow/Overgrow → Harvest** (a swarm that grows, then detonates).

## 4. EMBER — draft

**The family's resource: KINDLE.** A counter for the fight (on your side, shown on the energy orb):
**each spell you play adds 1 Kindle, and every spell deals +1 per Kindle.** It never resets during a
fight — the engine is SPELL COUNT. **The big turn: FLASHPOINT** — spend all the Kindle at once.

Monsters:

| Monster | Role | Passive | Cycle | Monster deck |
|---|---|---|---|---|
| **Pike** (starter) | front FINISHER | **FINISHER**: +2 damage for each ally that acted before it, **and +1 per Kindle** | Jab · Jab · Flurry | Charge, Hold the Line |
| **Emberling** | fragile back ENGINE | **STOKER**: every spell adds 2 Kindle, not 1 | Spark · Flicker | Zap, Stoke |
| **Echo Owl** (rare) | back ENGINE | **ECHO**: the first spell each turn is cast twice | Echo (repeat your last spell) · Hoot | Spark Scroll |
| **Cinder Newt** | safe back | **EMBERSKIN**: gains Block equal to Kindle at your turn start | Spit (all) · Flare | Cinderwall |
| **Ironhorn** | risky front | **TRAMPLE**: damage beyond what fells a foe hits the next | Gore · Brace | Flashpoint |

Ember cards:
- **Zap** (1) — 4 to a foe. *Filler that feeds Kindle*
- **Spark Scroll** (0, toss) — 3 to a random foe. *Cheap Kindle*
- **Stoke** (1) — +3 Kindle. Draw a card. *Enabler*
- **Cinderwall** (1) — a monster gains Block equal to Kindle. *Bridge: Ember survives*
- **Arc** (2) — 3 to every foe. *Spreads the Kindle bonus*
- **Fan the Flames** (1) — your next spell is cast twice. *Accelerator*
- **Flashpoint** (2) — spend all Kindle: deal 3 × Kindle to a foe. *THE BIG TURN*

Two example engines: **Emberling (back) + Pike (front)** — the back stokes, Pike's Finisher cashes the
Kindle in the relay; and **Echo Owl + Fan the Flames + Flashpoint** — spells doubled, then detonated.

## 5. How the slice tests the brief

- **Relay cascade**: Grove wants the front, Ember the back plus a finisher — a mixed line is the craft.
- **Leaders as exams**: an **Ember exam** (the Warden's SPELLGUARD line: spells half damage, so Kindle
  must be spent, not trickled) and a **Grove exam** (the Old Tusker: a huge Gore on the front and a
  Stampede on everyone — wide token lines and Thornwall both answer it).
- **Disruptors**: the Magpie steals a card; a new **Firebreak** foe that removes 2 Kindle when hit.
- **Growth hooks** (later): training teaches Grow or Kindle moves; held items ("a Kindle-starting
  charm"); upgrades (Deep Roots, Flashpoint+).
- **Family tags on screen**: a small family icon on monsters and cards; the reward screen weights
  Grove and Ember cards to the families on your team.

## 6. Build order (after Shayne's review)

1. ☑ Reviewed (Shayne, 2026-09-27): "sounds good". **The big finishers (Harvest, Flashpoint) are an
   EXPERIMENT** — "it might play out nice, it might not … let's be able to pivot quickly". So they are
   plain CARDS with no engine hooks of their own: dropping or reworking one is a content edit.
2. ☑ Engine (2026-09-27) — `PartyFamilies.cs`: the `Family` tag (creatures, companions, cards), GROW,
   ROOTED Block (`Ally.Rooted`; hit-through loses it), KINDLE (`PartyBattle.Kindle`: each spell CAST
   adds `1 + Stoker`, every spell deals +Kindle; an Echo's first spell and Fan the Flames cast again),
   and the passives as data components (Thornwall, Nursery, Mossback, Spores, Alpha, Stoker, Echo,
   Emberskin, KindleFinisher). Hooks: turn start, `HitAlly`, Thorns, `AttackFoes`, `SpellDamageTo`,
   card play, token summon and faint.
3. ☑ Content — the ten kits as drafted (Hushcap's wild HUSH trait kept), ten new cards (Graft, Root,
   Overgrow, Thicket, Harvest, Deep Roots, Stoke, Cinderwall, Fan the Flames, Flashpoint) with
   ComfyUI art, every card and creature tagged by area, rewards 3× as likely for your team's
   families. The practice scenarios 4 (EMBER) and 6 (GROVE) deal the new kits. 21 tests; 310 green.
   Not yet: the leader exams (Warden line, Firebreak) — after Shayne plays the slice.
4. ☑ Screen — family word first on each creature's status line and on each card (under the art);
   GROW and ROOTED on the status line; KINDLE in orange over the energy orb; the level moved into
   the HP bar ("LV5 · 24/24") — on the name line it cut long names.
5. ☐ Shayne plays the slice. Then Storm and Mire.
