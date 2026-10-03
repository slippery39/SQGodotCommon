# Handoff — the run's new shape: three monsters from the start, bosses evolve

**Read this, then the top of `KinJam.md` (the run's new shape, then the 2026-10-02 playtest fixes),
then `KinFamiliesPlan.md` round 5 (the evolutions) and `KinEnemiesPlan.md` (the enemy pass, paused).**
For UI work, `KinUI.md` and `.claude/rules/kin-frontend.md`. `HANDOFF-KinFamiliesBuilt.md` is the
session before — read it only for its scars.

## State at handoff (2026-10-03)

- Branch **`kin-pivot`**, nothing pushed. **351 KinCore tests green**; the solution and the Godot
  project build. `SQGodotCommon/project.godot` is still left uncommitted on purpose — every commit
  uses `git add -A -- . ':!SQGodotCommon/project.godot'`.
- **The run's new shape is BUILT, and Shayne has NOT played it.** That playtest is next.
- **We are EXPLORING**: balance is knowingly off (HP ×0.6, three monsters, region 1 still authored
  for one). No sims.

## 1. The game now

Choose a FAMILY (Grove or Ember). **Three of its five monsters are rolled** (seeded; REROLL once on
the YOUR TEAM screen). The run is **three regions**; bosses 1 and 2 each **EVOLVE one of your
monsters** (single forms: +50% HP, +2 Power, a bigger first-attack bonus, a second passive) and offer
a boss relic; boss 3 wins the run. A boss heals HALF. The team's order is set in TOWN (the hospital:
click a monster to send it to the front) — there is no deploy. Cards say their rules in WORDS
("Attack 5.", which shows the monster's real total while dragged over it); drop targets are a gold
highlight, not text.

## 2. This session's commits (oldest first)

| Commit | What |
|---|---|
| `ed5cd1b` | Cards in words again (icons misread: Kindle's Spell Power read as Burn); drop targets a highlight; the attack preview |
| `b5b4aeb` `1b7f7a9` `0eca237` | The playtest fixes: deploy cut (order set in town); a boss heals half; remove-a-card shows real cards |
| `654928f` | `KinEnemiesPlan.md` (the enemy brainstorm); CRUSH only on fragile foes (Rock Mite) |
| `ee89053` | The run's new shape, decided (docs) |
| `d202d48` | A run starts from a family: three rolled, one reroll; HP ×0.6; three regions |
| `e7892e8` | Bosses evolve a monster (`PartyCompanion.EvolvesInto`) |
| `9e77e35` | The nine new passives (REACH, SHELTERED, CINDERFALL, CADENCE, GUARDIAN, SHELLSTRIKE, SPORE CLOUD, SEEDFALL, HUNT CALL) |
| (this one) | The screens: family, YOUR TEAM + reroll, EVOLVE ONE; the ★ on the field; evolved art falls back to the base form's; docs and this handoff |

## 3. Where things are

- **Run shape**: `PartyRun.Start(Family, seed)`, `Roll`, `Reroll`, `EvolutionDue` / `Evolvable` /
  `Evolve` (`PartyRun.cs`); `PartyContent.Families`, `PoolOf`, `BaseFormOf`.
- **Evolved forms**: declared BEFORE their base forms (static init order) — `PartyContent.cs`
  (Lancepike, Bramble Elder), `PartyEmberCards.cs`, `PartyGroveCards.cs`. Their passives:
  `PartyEmber.cs` / `PartyGrove.cs` ("EVOLVED PASSIVES"); tests `PartyTests.Evolved.cs`.
- **Screens**: `KinPartyRunScreens.ShowFamilies`, `ShowTrio`, `ShowEvolution`; art fallback in
  `KinArt.Drawing`; the ★ in `KinRelayField.AllyLook`.
- **Captures**: `--screen=trio`, `monsters` (now EVOLVE ONE), `evolvedfight` (`Commands.md`).

## 4. Rules Shayne set this session

- **Cards are WORDS; symbols are for status** you glance at (memory + `KinUI.md`).
- **Drop targets are a highlight, never text**; no card-name float after a play.
- **CRUSH only on fragile foes, small amounts** — a test holds it (`TheFirstRegionsCrushersAreFragileAndSmall`).
- **Encounters are AUTHORED groups** (balance one encounter at a time); enemies first, balance after.
- **Interview before building** — discuss and confirm every unknown first (Shayne, 2026-10-02).

## 5. Scars worth not re-earning

1. **A Python patch string turned `\b` into a BACKSPACE** in a C# regex: the card went green but its
   number never changed. Write regexes with the Edit tool, or check the bytes (`od -c`).
2. **The pre-commit hook re-`git add`s every staged .cs file WHOLE** (CSharpier) — partial staging
   is impossible. To split a mixed file across commits, write each commit's version into the working
   tree, commit, then restore (`scratchpad/split.py` pattern: save → state N → commit → restore).
3. **`"\t\t}\n"` also matches the tail of `"\t\t\t}\n"`** — a block deletion cut mid-block. Bound a
   deletion by a unique terminator (`"\n\t\t\treturn;\n\t\t}\n"`) and assert what the cut contains.
4. **Test filler shares names**: `Deal` pads the deck with cards named "Guard", so `Play(s, "Guard")`
   may play a 0-Block filler. Name a test's own card uniquely.
5. **Names collide with CARDS**: Ironbark, Heartwood and Wildfire are all cards. Grep `"Name"` across
   `KinCore/Party` before naming any monster, passive or foe.
6. **Gold on a creature means "drop here"** — never use gold for anything else on the field.

## 6. Next

1. **Shayne playtests the new shape** — three monsters from floor 1, the reroll, evolution, the
   passives. Watch: is 12–18 HP a monster too fragile; does evolution feel like a reward; which
   passives never come up.
2. **Enemy pass step 1 is BUILT** (2026-10-03, `KinEnemiesPlan.md`): no levels; region 1 authored
   for three (goblins, wolves, Boar, Powder Goblin…); authored encounters, easy first; Grass/Trainer
   gone; exams +50% HP. Regions 2–3 are placeholders. **Not yet played.** Next: the debuff system.
3. Then the debuff and junk-card systems; the MAP session; the HOOK; art for the evolved forms.
