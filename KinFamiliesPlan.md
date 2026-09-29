# FAMILIES — monsters as engines, cards as fuel (design, 2026-09-27)

## ROUND 2 — ONE FAMILY PER RUN (interview, 2026-09-28) — supersedes "the build emerges from finds"

**Status: DECIDED (three rounds), not built.** Shayne, after playing KIN: "once you commit to a bonus, it's kind of bad
to catch non-bonus monsters, as well as some cards kind of become useless … we can curate cards and
monsters so they better fit within a single run." The Slay the Spire class model: **the family is your
class.** Everything below is his answer from three rounds of questions.

| | Decided |
|---|---|
| Choosing | **The starter IS the family** (Bramble = Grove, Pike = Ember). No new screen |
| Rewards and shop | **Only your family's cards and COLOURLESS ones** |
| Catching | **Only your family's monsters and colourless ones.** You still FIGHT every family |
| Wild foes | **Uniformly mixed** across families in every area (the themed areas go) |
| Colourless monsters | **Generalists** — each has a passive that CAN be built around, generic enough that any family might want it in the right spot, backed by colourless cards that work well with it |
| Kin bonus | **DROPPED** — with one family it is noise. Family colours, labels, live numbers, pop-ups stay |
| Pool size | **STS-scale**: per family ~8 monsters (starter included) and 20+ cards; colourless ~6 monsters, ~15 cards |
| Starting deck | **4 Strike, 4 Guard, + 2 small family cards** that show the engine on turn one |
| Rarity | **Common / uncommon / rare**, rewards weighted; a leader win guarantees a rare |
| Monster decks | **DROPPED** — a monster brings its passive and moves; every card comes from rewards |
| Storm, Mire | **Shelved** — their cards stay theirs and wait; their monsters are fight-only foes |
| First slice | **Grove + Ember**, each deepened to a full curated pool |
| Order | **System first** (playable with today's content, thin), then the pools |
| Content design | **An interview PER FAMILY** before any drafts |

### THE RUN'S SHAPE — difficulty lives in elites and bosses (same interview, round 3)

Shayne: "the difficulty should mainly come from the elites and bosses, and not the wild monsters …
You were rarely dying in Pokémon because you couldn't handle the wild monsters in an area, it was
always the gym leaders and trainers." **The loop: go to the wild to find monsters to catch, so you
are strong enough for the elites and the boss.** One family at a time is also what makes that
balanceable — "much easier to balance for both fun and difficulty".

| | Decided |
|---|---|
| Length | **5 regions** (the 10-region map goes) |
| Fight tiers | **wild < trainer < ELITE < BOSS**. Trainers stay, a middle tier; the rare's lair stays a catch spot |
| Wild fights | **Light attrition** — few foes, at or under your level; rarely kill, but HP carries and healing costs gold. They pay in catches and XP |
| Elites | **1–2 per route, always shown on the map**; branching makes them dodgeable. Mini-bosses with UNIQUE play patterns — not species, never catchable |
| Elite reward | **A rare card, a held item / relic, and big XP + gold** |
| Bosses | **STS bosses, not monsters** — never catchable, never a species. **Either shape, per boss**: one huge creature with its own rules and phases, or a leader with a signature beast and unique support |
| Boss pool | **2 per region, the one you face SHOWN as you enter** — the route becomes preparing for it. 10 in time |
| Items | **Both**: trainer RELICS (global, permanent) and monster HELD items |

This retires the current leaders (Old Tusker and Old Mire lines, built of scaled wild species),
the ten-region `Tiers` table, and "late routes field 4–5 foes" — the late-game ease the last handoff
measured came from leaders that never killed while routes did.

### The work, in order

1. ☑ **System** (built 2026-09-28; 310 tests): drop KIN (`KinBonus`, `KinGrowAction`, its two tests); drop
   monster decks (`DeployDeck`/`WithdrawDeck`/`OwnerName`, their tests; each starter's signature cards
   move into its family's pool); `KinCard.Rarity`; `PartyRun.Family` from the starter; rewards and
   shop filtered to it + colourless, weighted by rarity; the catch refusal "Not your family"
   (and a mark on catchable foes); areas' wild pools mixed; the new starting decks.
2. ☑ **Run shape** (built 2026-09-28; 317 tests; the boss at the ROUTE's end, a spring before it, the
   route drawn left to right): 5 regions; wild fights eased to light attrition; ELITE nodes (1–2, shown); the boss
   shown on entering a region; elite rewards; RELICS as a system with ~6 starters. Placeholder elites
   and bosses reuse today's leader lines until designed.
3. **Interview: bosses and elites** — what each tests, its unique pattern.
4. **Interview: Grove**, then **Ember** — fantasy, big turn, fears → ~8 monsters and 20+ cards each.
5. **Interview: colourless** — the generalists and the cards that make each worth a slot.
6. **Held items** (monster-held).
7. Playtest a run of each family.

### What this costs

- **"The build emerges from finds" is reversed** (the brief, §1): the build is chosen at the start;
  catches and rewards now pick WITHIN it.
- **About a third of wild foes are catchable** (your family, plus colourless) — mixed areas are what
  keep that from being zero in three areas of four.
- **The practice scenarios** deal monster decks and off-family teams; they need re-dealing.
- `party-sim` numbers from before this are void — not that we are simming (exploring).

**Status: REVIEWED — building the Grove + Ember slice.** Shayne: "Our cards and monsters just feel kind of generic.
The fun part of card games is figuring out and achieving combos and synergies." The brief below is his
answers from an interview (five rounds, 2026-09-27); the family drafts are a proposal to react to.
Exploring, not tuning: every number is a guess; tests prove things fire; no sims until a design holds.

## ROUND 4 — A SIMPLER GAME: the CARDS matter, the monsters guide (interview 2026-09-28)

**Status: the RULES are BUILT (2026-09-28, four commits); the FAMILY DRAFTS are next. It supersedes catching, the bench, levels, the relay's monster cycles
and KIN below.** Shayne, after playing: catching "creates underleveled monsters on the bench which
don't really do much … I always just play [Strike] on my strongest monster anyway"; the relay "never
mattered … you just keep your highest health monster at the front". The fix is to simplify: "the
cards should matter, the monsters just guide you in what cards you are choosing."

| | Decided |
|---|---|
| A monster | **HP, Power and Spell Power**, and a **passive** that guides your deck. **No move cycle, no auto-attack** |
| Attacking | **Only with attack cards**, played ON a monster (+ its Power) |
| First-attack bonus | **The first attack card played on each monster EACH TURN triggers that monster's OWN bonus** — spreading attacks is the good play |
| Spells | Still dropped on a foe; **Spell Power is your TEAM's total**. A spell is not an attack: it does not trigger a first-attack bonus (so an Ember monster's bonus should FEED spells) |
| The line | **Kept**: foes aim at your front, back or weakest, **and cards care about position** ("front: …", "back: …", swaps) |
| Getting monsters | **Start with one. The first two bosses each offer 3 of your family's (colourless among them); pick 1.** Region 1 is designed for one monster; regions 4–5 test the full team. The boss relic stays |
| Catching, the bench, Snares, levels, XP | **CUT.** Monsters grow through cards, relics and upgrades; exams scale by region |
| Knocked out | Out for the fight; **back at 1 HP** after it. The run ends when all are down |
| Wild fights pay | **a card and gold** |
| Springs | **heal OR upgrade a card** (STS's campfire); **every card has a + version**, authored with it |
| Families | **No automatic family mechanic** (Kindle as a free rule goes). A family is a set of SHARED card mechanics, and its monsters' passives lean on them |
| Sub-mechanics relate by | **BRIDGES**: each works alone; a few bridge cards pay two at once |
| Roster | **5 per family** (the starter + 4 boss picks) **+ 3 colourless** |
| Order | **Rules first** on today's cards as placeholders, then drafts of both families for review, then build |

**EMBER — spellslinging**: damage spells, and boosting them. Sub-mechanics: **Spell Power
stacking** (the scaling engine), **Burn** (damage over time), **Spell chains** (cheap spells, "your
3rd spell this turn…"), **Big spells** (X-cost, charge-ups).

**GROVE — Block, and turning it into damage.** Sub-mechanics: **Rooted Block** (Block that stays),
**Thorns**, **Block → damage**, **Growth**. **Balance rule (Shayne): a card that both defends and
deals damage must not do both efficiently** — that is why Grove was too strong. Price conversion as
a premium: Block spent to become damage, or damage that needs Block already banked.

### What goes, with the rules
Monster move cycles and the RELAY for your side (foes keep their telegraphed patterns — every exam
survives); Hasten, Echo, Finisher (relay-count), Kindle as a free rule, catching, Snares, the bench and
the Pen, levels and XP, tokens as fighters (Sow, Swarm, Harvest — unless a draft brings them back).

## ROUND 3 — BOSSES AND ELITES (interview 2026-09-28; all 8 APPROVED and BUILT — `PartyExams`, `PartyBosses`)

Shayne played Pike into the first elite: "pretty much unbeatable … no possible thing I could have
done … a 68 health enemy attacking all my guys for 10." The wild fights were "the exact opposite".
`party-sim` agreed: Pike died in region 1 in 40% of runs, Bramble never (`docs/findings/companion-balance.md`).

| | Decided |
|---|---|
| What makes them hard | **All four**: big TELEGRAPHED turns, PHASES, RULE-BENDERS, MINIONS |
| Fairness | **Designer judgement**, per boss — no blanket rule. (Every draft below still says its answer) |
| Exams | **They test DECK QUALITIES** (a Block check, a damage race, focus vs spread, line order) — any family can pass with the right cards; the boss is shown early so you can draft for it |
| A catch joins at | **half HP** (was: the HP it was caught at, a third or less) |
| Elites | **either** one creature or a small group, per elite |
| This pass | **regions 1–2**: 4 bosses, 4 elites; regions 3–5 keep the placeholders |
| Wild fights | **a little chip** — usually 5–15% of the team's HP |
| A boss pays | a rare-led card, **a BOSS RELIC (one of three, some with a drawback)**, **a heal in the next town**, **big gold and XP** |

### The drafts

Numbers are at Lv 5, as all content is (`PartyLevels` scales them: region 1's elites are Lv 6, its
bosses Lv 7). A region-1 team is the starter and one catch, ~Lv 6. Every number is a guess.

**REGION 1 bosses — one idea each, for a team of two**

| Boss | Tests | Shape | Pattern | The answer |
|---|---|---|---|---|
| **The Old Tusker** (beast) | BLOCK — reading the telegraph | one, 56 HP | **Paw the Ground** (WIND-UP: "next: GORE 18 → front") → **Gore 18** → **Trample 4 → all** | Guard or Stagger on the wind-up turn; a wall in front (Thornwall pays double) |
| **The Goblin Chief** | FOCUS vs SPREAD — minions | the Chief (36 HP) + adds | **Call the Band** (a Goblin, 8 HP, stab 3, at the front) → **Spear 7 → front** → **Call the Band** | sweep the band (Whirl, Flurry, Arc) or reach past it for the Chief (spells, Gust) |

**REGION 1 elites — beatable by a starter and one half-HP catch**

| Elite | Tests | Shape | Pattern / rule | The answer |
|---|---|---|---|---|
| **The Iron Sentinel** (construct) | BIG HITS | one, 40 HP | RULE: **SHELL — ignores any hit of 4 or less.** **Brace 10** ↔ **Slam 8 → front** | Rally + Strike, Kindle-fed spells; chip damage and tokens do nothing |
| **Goblin Raiders** | SPREAD | three goblins, 14 HP each | **Stab 4** each; the back one **Snatches** a card (the Magpie's thief) | sweeps; kill the thief to get the card back |

**REGION 2 bosses — a phase and a rule each, for a team of three**

| Boss | Tests | Shape | Pattern | The answer |
|---|---|---|---|---|
| **The Old Mire** (a giant toad) | LINE ORDER | one, 70 HP | **Tongue** (pulls your BACK monster to the FRONT) → **Swallow 16 → front** → **Deluge 5 → all**. PHASE at half: **Submerge** — Block 15 each turn, and a **Toadling** (10 HP) every other turn | put the one who can take a Swallow at the back; burst it before the phase |
| **The Black Knight** | a DAMAGE RACE | one, 64 HP | **Cleave 8 → front two**, **Guard 10**; RULE: **ENRAGE — +2 to its attacks every round**. PHASE at half: **Second Wind** — 20 Block, once | burst (Rally, Kindle, Harvest); a slow wall loses |

**REGION 2 elites**

| Elite | Tests | Shape | Pattern / rule | The answer |
|---|---|---|---|---|
| **The Hexer and her Golem** (a rival mage) | REACH | the Hexer (26 HP) behind a Golem (40 HP) | RULE: **HEX — your first card each turn costs 1 more** while she stands. Golem **Guard 10 → the one ahead** ↔ **Slam 9**; Hexer **Bolt 6 → your weakest** | spells, back-line attacks or a pull reach her; kill her first |
| **Harpy Flock** | PROTECTING the fragile | three harpies, 12 HP each | **Rake 5 → your weakest** each; one **Screeches** (swaps your front two) | Block the weak one, Decoy, kill fast |

**BOSS RELICS — pick one of three after a boss. NO drawbacks** (Shayne: "we can do these without
the drawbacks"), so War Drum and Heavy Crown were one relic and merged:

| Relic | Rule |
|---|---|
| **War Drum** | +1 energy every turn. |
| **Ancient Lens** | Draw 1 more card every turn. |
| **Warband Banner** | Your monsters have +2 Power. |
| **Big Tent** | Your team holds 4 monsters, not 3. |
| **Kin Totem** | Your first card each turn costs 0. |

**The heal in town after a boss: FULL** (read from "without the drawbacks"; the 75% draft was not
kept). Boss gold 50 → 100; XP was already double.

**Built as drafted, one change**: the Hexer WARDS the Golem ahead of her (a Block can only go to
the one AHEAD), rather than the Golem guarding her.

### What building them costs (engine, before content)

- **WIND-UP**: an intent that shows the NEXT move in its telegraph ("next: GORE 18"). Small.
- **PHASES**: at half HP a foe swaps to a second pattern (and can gain a rule). New, small.
- **Rule-benders**: SHELL (ignore small hits) and ENRAGE (+N a round) are new components; HEX is
  the existing first-card tax; TONGUE (pull your back to the front) is a new intent.
- **Minions that stay**: a foe summon with no fade — the Summon intent already exists.
- **Boss relics** join `PartyRelics`; the 1-of-3 choice is a new screen after a boss.

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
5. ☑ Shayne played the slice (2026-09-28): no sense a family build was wanted or paid; could not tell
   which monsters and cards were kin, nor whether Kindle fired. → **KIN** (family cards pay per kin
   in the line, in the family's resource), family colours, live spell numbers, engine pop-ups — see
   the top of `KinJam.md`.
6. ☐ Shayne plays KIN. Then Storm and Mire (each needs its kin bonus: energy? draw?).
