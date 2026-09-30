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

## GROVE — the family draft (interview 2026-09-29; DRAFT 1 APPROVED and BUILT — `PartyGrove`, `GroveCards`)

Shayne: "good as a first pass, we will see how it plays out, and maybe we can brainstorm some more
aggressive mechanics if the family feels too slow."

**AFTER THE FIRST PLAYTEST (Shayne, 2026-09-30)** — "too easy for the grove": a whole act for 3
damage, "too easy for the grove to both block and kill things at the same time". Three things
multiplied: THORNWALL made every Block card a damage card; Rooted Block stacked forever (a stall
engine by our own test); Bark Slam read that wall for free. Changed:
- **ROOTED Block lasts ONE extra turn** — kept at your next turn start, gone at the one after unless
  rooted again (`Ally.Carried` is the Block on its extra turn; hits spend it first). Mossback and
  Ancient Bark root all Block the same way. Deep Roots and Heartwood read only the fresh Rooted Block.
- **THORNWALL is gone**: Bramble has plain Thorns 2 ("THORNS"). Block into damage lives in cards.
- Bark Slam and Crushing Weight stay READ-only; the bounded wall is what bounds them. Re-check in play.
- (Ember, same playtest) **Spell Surge costs 3 and is RARE (+ costs 2)**: at 1 it refunded a whole
  turn with no setup. "One card that does all that without much effort is too much."
- Both families were still "too easy" overall: a difficulty pass on the foes comes after this.

**As built** (what the tables below do not say):
- **A token is marked `Ally.IsToken`**, apart from fading: `FadesIn` 0 now means it stays. Tokens
  arrive at your FRONT, and the line holds 5 — with three monsters and a Sprout, Seedlings fits one.
- **A token that withers has not FALLEN**: Pack Leader, Life Cycle and a Log's draw hear a token hit
  down or sacrificed, not a Seedling fading. (Otherwise Seedlings were free growth every turn.)
- **Graft sacrifices your FRONT token** (a card is dropped on one creature, so it cannot also pick one).
- Heartwood is a spell dropped on a foe; its damage is spell damage, so Spell Power adds to it.
- Bramble's first attack gives +5 Rooted Block (THORNWALL went after the playtest, above).
- Seedling, Log and Treant have no art yet: they draw as the generated figure.

| | Decided (Shayne) |
|---|---|
| Creatures | **a FOREST MIX** — plants, fungi, mossy beasts, shelled things |
| Archetypes | **all four**: ROOTED wall, THORNS, BLOCK INTO DAMAGE, GROWTH — plus TOKENS as the substrate |
| Weak at | **all four**: slow start, no area damage, poor at burst, card-starved |
| Block → damage | **READ only** (STS Body Slam): damage equal to Block, and the Block stays. The card is weak alone |
| Growth | **by cards only** — no automatic per-turn growth; a monster's PASSIVE may grow things |
| Thorns | **by card**: for the TURN (big numbers) or for the FIGHT (small numbers, rarer) |
| Tokens | **back, as WALL, FUEL and ATTACKER** — "if a mechanic can serve multiple purposes, that's a good thing" |
| Token lifetime | **by card**: cheap ones wither, rarer ones stay |
| Sacrifice pays | **by card**: damage, Block, growth or cards |
| Token places | **any free place** on the line of 5 — a solo region-1 monster can field four |
| Attacks on tokens | **yes** — an attack card played on a token swings with its Power (no first-attack bonus) |

**GROW N** = +N Power for the rest of the fight, on a monster or a token. **ROOTED** Block stays at
your turn start until it is broken. Numbers are guesses (Lv 5), pushed first.

### Tokens

| Token | HP · POW | Lasts | From |
|---|---|---|---|
| **Seedling** | 4 · 0 | withers at your next turn | Seedlings |
| **Sprout** | 6 · 1 | until it falls | Sow, Broodvine |
| **Log** | 12 · 0 | until it falls; **when it falls, draw 2** | Nurse Log |
| **Treant** | 20 · 3 | until it falls | Treant |

### Monsters

| Monster | Archetype | HP · POW | Passive | First attack each turn |
|---|---|---|---|---|
| **Bramble** (starter) | Block → damage, Thorns | 30 · 2 | **THORNWALL**: a foe that attacks her takes 2 + her Block | +5 Rooted Block |
| **Mosshell** | ROOTED | 34 · 1 | **MOSSBACK**: its Block is Rooted; when its Block stops a hit, it Grows 1 | +6 Block |
| **Hushcap** | THORNS | 20 · 2 | **SPORECAP**: a card that gives Thorns gives 3 more | +6 Thorns this turn |
| **Broodvine** | TOKENS | 22 · 1 | **NURSERY**: your tokens arrive with +3 HP and Grow 1 | summon a Sprout |
| **Howler** | GROWTH | 24 · 4 | **PACK LEADER**: when a token of yours falls, each of your monsters Grows 1 | Grow 1 |

### Cards — DRAFT 1

| Card | Cost | R | Text | + | For |
|---|---|---|---|---|---|
| **Root** | 1 | C | Gain 8 Rooted Block. | 11 | [rooted] (start) |
| **Sow** | 1 | C | Summon a Sprout. Draw a card. | costs 0 | [tokens] (start) |
| **Seedlings** | 0 | C | Summon 2 Seedlings. | 3 | [tokens] cheap wall / fuel |
| **Bristle** | 1 | C | 8 Thorns this turn. | 12 | [thorns] |
| **Bark Slam** | 1 | C | ATTACK: deal damage equal to its Block. | costs 0 | [convert] |
| **Thorn Lash** | 1 | C | ATTACK: deal 4 + its Thorns. | 6 + | [thorns] → damage |
| **Overgrow** | 1 | C | Grow 3. | Grow 4 | [growth] |
| **Thicket** | 1 | C | Every creature on your line gains 5 Block. | 7 | wall · [tokens] |
| **Hardwood** | 1 | C | Gain 7 Block. If it already had Block, 7 more. | 9 / 9 | [rooted] |
| **Compost** | 0 | C | Sacrifice a token: draw 2 and gain 1 energy. | draw 3 | [tokens] fuel · card-starved answer |
| **Barkskin** | 1 | C | Gain Block equal to three times its Power. | four times | [growth] → wall |
| **Deep Roots** | 1 | U | Its Rooted Block doubles. | costs 0 | [rooted] |
| **Ironbark** | 2 | U | Gain 16 Rooted Block. | 22 | [rooted] |
| **Brace Roots** | 1 | U | Gain 4 Block. All its Block becomes Rooted. | 7 | [rooted] enabler |
| **Briar Patch** | 1 | U | Every creature on your line gains 5 Thorns this turn. | 7 | [thorns] · [tokens] |
| **Needles** | 1 | U | +3 Thorns for the rest of the fight. | +4 | [thorns] |
| **Graft** | 1 | U | Sacrifice a token: a monster Grows by its Power and gains Block equal to its HP. | also draw 1 | [tokens] → growth + wall |
| **Harvest** | 2 | U | Sacrifice all your tokens: deal their total HP to a foe. | costs 1 | [tokens] big turn |
| **Pack Charge** | 1 | U | Each of your tokens attacks their front for its Power. | costs 0 | [tokens] · [growth] |
| **Wild Growth** | 1 | U | Every creature on your line Grows 1. | Grow 2 | [growth] · [tokens] |
| **Nurse Log** | 1 | U | Summon a Log. | 16 HP | [tokens] wall → draw |
| **Crushing Weight** | 2 | R | ATTACK: deal twice its Block. | three times | [convert] nuke |
| **Heartwood** | 1 | R | Deal damage equal to all the Rooted Block on your line. It stays. | costs 0 | [rooted] → damage |
| **Ancient Bark** | 2 | R | AURA: all your Block is Rooted. | costs 1 | [rooted] engine |
| **Thornmail** | 1 | R | AURA: a foe that hits any creature on your line takes 3. | 5 | [thorns] engine |
| **Wild Heart** | 2 | R | AURA: at the start of each of your turns, each of your monsters Grows 1. | costs 1 | [growth] engine |
| **Treant** | 2 | R | Summon a Treant. | 26 HP · 4 POW | [tokens] |
| **Rampant Growth** | 1 | R | A creature Grows by its Power. | costs 0 | [growth] burst |
| **Life Cycle** | 1 | R | AURA: when a token of yours falls, draw a card and your front gains 4 Rooted Block. | costs 0 | [tokens] engine |

Attacks add the monster's Power as always — so Growth pays every attack, Bark Slam included.
**The starting deck**: 4 Strike, 4 Guard, **Root, Sow**. **29 cards**: 11 common, 10 uncommon, 8 rare.

**The combos it is built for** (the finds):
- **Rooted**: Root / Ironbark / Brace Roots → Deep Roots → Bark Slam, Crushing Weight or Heartwood;
  Ancient Bark makes every Block Rooted; Mosshell walls and grows; Bramble's Thornwall reads it.
- **Thorns**: Bristle / Briar Patch / Needles → Thorn Lash; Thornmail for the whole line; Hushcap adds
  3 to every Thorns card; Briar Patch on a line of tokens makes every hit hurt.
- **Growth**: Overgrow / Wild Growth → Rampant Growth → any attack; Barkskin turns Power into Block;
  Howler grows the team whenever a token falls; Wild Heart for the long fight.
- **Tokens**: Sow / Seedlings / Nurse Log / Treant wall the front → Compost, Graft or Harvest cash
  them; a grown Sprout swings; Pack Charge swings them all; Broodvine grows them as they arrive;
  Life Cycle and Nurse Log turn every loss into cards.
- **Bridges**: Barkskin (growth → wall), Graft (token → growth + wall), Thicket and Briar Patch
  (tokens + wall / Thorns), Bark Slam + Power (growth → conversion), Life Cycle (tokens → rooted +
  cards), Mosshell (rooted → growth), Howler (tokens → growth).

**The weaknesses, as drafted**: nothing hits more than one foe (Thornmail and Thorns only answer
attackers); the burst cards all need turns of banking first; draw exists only as a token's price
(Compost, Log, Life Cycle); turn 1 is Root and Sow.

### Engine it needs

GROW as +Power for the fight on any creature (the old automatic per-turn Grow goes); **a token flag
apart from fading** (today `FadesIn > 0` IS the token mark, so a lasting token needs its own); attack
cards on tokens (check it); a Log's draw when it falls; attacks that read Block or Thorns; Thorns for
the turn and the fight on any creature, and a line-wide Thorns aura; an aura that roots all Block;
sacrifice with payoffs (cards and energy, growth and Block, Harvest's total HP); line-wide Block,
Thorns and Grow; Heartwood's damage from the line's Rooted Block; Mossback's growth when its Block
stops a hit; Sporecap (Stoker for Thorns); Nursery via the existing `TokenBoost`.
**Goes**: the old Grow and Nursery, Spores, Alpha, the old Thicket and Harvest, and the placeholder
Grove cards (Thornhide, Bristle's old text, Call Sparks, Decoy, Swarm, Offering, Graft's old text).

## EMBER — the family draft (interview 2026-09-28; DRAFT 2 APPROVED and BUILT — `PartyEmber`, `EmberCards`)

**As built** (what the tables below do not say):
- **A SPELL is every card that is not an ATTACK; an attack card makes one of your monsters attack**
  (Shayne, 2026-09-28). So Guard, Kindle, Flicker and the auras are spells: they count toward chains
  and Echo, Fan the Flames doubles them, Spell Surge makes them cheaper. The card face says which
  ("SPELL · EMBER", "ATTACK"); rarity shows on the reward and shop tiles only, as it clipped.
- **Firestorm recasts every 0-cost spell**, not only damage: Heat Surge's energy, Flicker+'s draw.
  Steps that must land on your own monster (Block) are skipped, X cards (Meteor) are not recast, and
  the recasts do not count toward the chain.
- The Ember monsters have no move cycle (round 4); each carries one placeholder "Wait" so the shared
  record stays valid. They reuse the wild species' names, so the art comes with them.
- Meteor is an X card; its cost gem shows **0**, as Unleash's always has — the card face has no X.
- `kin_card_preview.tscn -- --party` shows the pool's seven longest texts; all fit.

| | Decided (Shayne) |
|---|---|
| Creatures | **a MIX of fire creatures** — the theme is heat, not a creature type |
| Big turns | **all four**: STACK then nuke, CHAINS, BURN detonate, ENERGY dump — four archetypes to bridge |
| Weak at | **fragile, and card-hungry** |
| Block | **it has Block cards, but big Block takes creativity** — never cheap |
| Burn | **STS Poison**: Burn N deals N at the start of the foe's turn, ignoring Block, then drops by 1 |
| Spell Power | **ONE number** (Kindle merges into it): monsters give a base; cards add for the TURN or the FIGHT |
| Attacks | **hybrid monsters**: some attack, their hits scaling with Spell Power |
| Roster | **Pike, a flexible starter, and one boss pick per archetype** |

Numbers are guesses (Lv 5). SP = Spell Power. Every card has a +.

### Monsters

| Monster | Archetype | HP · POW · SP | Passive | First attack each turn |
|---|---|---|---|---|
| **Pike** (starter) | hybrid | 24 · 3 · 1 | **SPELLBLADE**: its attacks add your Spell Power | +1 SP this turn |
| **Emberling** | STACK | 14 · 0 · 2 | **STOKER**: when a card gives Spell Power, it gives 1 more | +1 SP this fight |
| **Cinder Newt** | BURN | 18 · 1 · 1 | **SMOULDER**: your spells apply 1 Burn to each foe they hit | apply 2 Burn to the foe it hits |
| **Echo Owl** | CHAINS | 16 · 1 · 1 | **ECHO**: your 3rd spell each turn is cast twice | draw a card |
| **Ironhorn** | ENERGY (hybrid) | 26 · 4 · 0 | **BANK**: up to 2 unspent energy carries into your next turn | gain 1 energy |

### Cards — DRAFT 2 (pushed: Shayne, "push the cards' limits first, then nerf")

Draft 1 was "not exciting", thin on chains, and pre-nerfed (an energy card that cost next turn's
energy; a Burn payoff that removed the Burn). Draft 2 pushes every card and adds **AURAS** — cards
that set a rule for the rest of the fight (STS's powers) — where the "oh, THAT combo" lives.

| Card | Cost | R | Text | + | For |
|---|---|---|---|---|---|
| **Zap** | 1 | C | Deal 5. | Deal 7 | spell (start) |
| **Kindle** | 1 | C | +3 SP this turn. Draw a card. | +4 SP | [stack] (start) |
| **Spark** | 0 | C | Deal 3. | Deal 4 | [chain] |
| **Ember Dart** | 0 | C | Deal 2. Draw a card. | Deal 3 | [chain] |
| **Kindling** | 1 | C | Add 2 Sparks to your hand. | 3 Sparks | [chain] fodder |
| **Singe** | 1 | C | Deal 4. Apply 4 Burn. | 5 / 5 | [burn] |
| **Fire Fan** | 1 | C | Deal 3 to every foe. Apply 2 Burn to each. | 4 / 3 | [burn] area |
| **Flicker** | 1 | C | Draw 3 cards. | costs 0 | card-hungry |
| **Charge Up** | 1 | C | Next turn, +2 energy. Draw a card. | +3 | [energy] |
| **Flame Ward** | 1 | C | Gain Block: 6 + three times your SP. | 9 + three times | Block · [stack] |
| **Heat Haze** | 1 | C | Gain 6 Block. If you have cast 2 spells this turn, gain 12 more. | 8 / 14 | Block · [chain] |
| **Stoke** | 1 | U | +2 SP for the rest of the fight. | +3 | [stack] |
| **Fan the Flames** | 1 | U | Your next 2 spells this turn are cast twice. | costs 0 | [stack] [chain] |
| **Spell Surge** | 1 | U | Your spells cost 1 less this turn. | costs 0 | [chain] enabler |
| **Wildfire** | 1 | U | Deal 6. Costs 0 if you have cast 2 spells this turn. | Deal 8 | [chain] |
| **Chain Lightning** | 1 | U | Deal 3 for each spell you have cast this turn, this one included. | 4 each | [chain] payoff |
| **Ignite** | 1 | U | Double a foe's Burn. | costs 0 | [burn] |
| **Spreading Flames** | 1 | U | Every foe's Burn rises to the highest Burn among them. | then +2 each | [burn] |
| **Smoke Screen** | 1 | U | Gain 5 Block for each spell you have cast this turn. | 6 each | Block · [chain] |
| **Cinder Shield** | 1 | U | Gain Block equal to twice the Burn on their line. | three times | Block · [burn] |
| **Heat Surge** | 0 | U | +2 energy. | +3 | [energy] |
| **Flashpoint** | 2 | R | Deal three times a foe's Burn. (The Burn stays.) | four times | [burn] detonate |
| **Meteor** | X | R | Spend all energy: deal 8 per energy to a foe, and 4 per energy to the one behind. | 10 / 5 | [energy] dump |
| **Overload** | 1 | R | Deal all the spell damage dealt this turn. | costs 0 | [chain] finisher |
| **Pyroblast** | 3 | R | Deal 12. Your SP counts three times. | Deal 16 | [stack] nuke |
| **Firestorm** | 2 | R | Cast every 0-cost spell in your discard pile, each at a random foe. | costs 1 | [chain] big turn |
| **Inner Fire** | 2 | R | AURA: at the start of each of your turns, +1 SP for the rest of the fight. | costs 1 | [stack] engine |
| **Everburn** | 1 | R | AURA: Burn no longer drops at the foe's turn. | costs 0 | [burn] engine |
| **Spellweaver** | 2 | R | AURA: whenever you cast your 3rd spell in a turn, draw 2 and gain 1 energy. | costs 1 | [chain] engine |

**The starting deck**: 4 Strike, 4 Guard, **Zap, Kindle**. **29 cards**: 11 common, 10 uncommon, 8 rare.

**The combos it is built for** (the finds):
- **Chains**: Kindling / Ember Dart / Spark → Spell Surge → Chain Lightning or Wildfire; Firestorm
  recasts every Spark in the discard; Spellweaver refuels it; Echo Owl doubles the 3rd.
- **Burn**: Singe / Fire Fan → Spreading Flames → Ignite → Flashpoint (the Burn stays, so again next
  turn); Everburn makes it permanent; Cinder Newt adds Burn to every spell; Cinder Shield turns it to Block.
- **Stack**: Kindle / Stoke / Inner Fire → Pyroblast or Pike's SPELLBLADE Strikes; Flame Ward walls
  with it; Emberling adds 1 to every gain.
- **Energy**: Charge Up / Heat Surge / Ironhorn's BANK → Meteor.
- **Bridges**: Fan the Flames (stack + chain), Fire Fan (area + Burn), Smoke Screen and Heat Haze
  (chains keep you alive), Pike (attacks cash the stack).

**Block** (bigger, as asked): Flame Ward, Heat Haze, Smoke Screen, Cinder Shield — each asks for its
engine first, and each walls a boss hit once the engine is running.

### Engine it needs

Burn (a foe counter, ticking at the foe's turn start); Spell Power for the turn and the fight (Kindle
renamed); "your Nth spell this turn" (the counter exists); X-cost spells (the X exists for attacks);
energy carried over; Block that reads SP, spells cast or Burn; SPELLBLADE; SMOULDER; ECHO on the 3rd;
**AURAS** (a rule on your side for the rest of the fight); **adding a card to your hand**; **casting
from the discard**; a cost cut for the turn; "the next 2 spells are cast twice".

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
