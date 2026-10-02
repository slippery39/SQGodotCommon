# Handoff — both families built, balanced to parity, and the declutter pass

**Read this, then the top of `KinJam.md`, then `KinFamiliesPlan.md` from the top (the GROVE and EMBER
drafts with their as-built and playtest notes, then round 4).** For any UI work, `KinUI.md`'s
DECLUTTER PASS section and `.claude/rules/kin-frontend.md` (it loads by itself). `HANDOFF-KinRound4.md`
is the session before this one — read it only for its scars.

## State at handoff (2026-10-01)

- Branch **`kin-pivot`**, nothing pushed. **337 KinCore tests green**; the solution and the Godot
  project build. `SQGodotCommon/project.godot` is still left uncommitted on purpose (a headless
  import strips two comment lines) — every commit uses `git add -A -- . ':!SQGodotCommon/project.godot'`.
- **Both families are drafted and built** (Ember draft 2, Grove draft 1), playtested once, and
  **balanced to parity**: the bot wins **38.8%** of runs (target 35%) — **Bramble 36.5%, Pike 41%**.
- **The UI is decluttered**: symbols + numbers, every symbol explains itself on hover, a ? panel.
- **Shayne has NOT played the decluttered UI or the parity balance yet.** That playtest is next.

## 1. The game now, in one paragraph

A deckbuilder whose monsters guide the deck. **Pick a starter — it IS your family**: Bramble = GROVE
(Block and turning it into damage: Rooted Block, Thorns, Growth, tokens as wall/fuel/attacker), Pike =
EMBER (spellslinging: Spell Power, Burn, chains, energy, auras). Five regions, each town → route →
BOSS; routes show 1–2 elites; springs heal 30% or upgrade a card. Monsters act ONLY through attack
cards, the first attack on each monster each turn fires its FIRST-ATTACK bonus. You start with one
monster; the first two bosses each offer three of your family (one per archetype). **A spell is every
card that is not an attack.** **A card with no target plays anywhere on the field** (MTG's rule).
**Rooted Block lasts ONE extra turn.** Foes run fixed, telegraphed cycles; some have traits that test
decks (CRUSH ignores Block; ENRAGE grows each round).

## 2. This session's commits (oldest first)

| Commit | What |
|---|---|
| `5a13ae6` | **Ember draft 2**: 29 cards with + versions, 4 boss monsters, Burn, one Spell Power, auras |
| `e6df7d8` | **Grove draft 1**; the first playtest's fixes (Rooted one extra turn, THORNWALL gone, Spell Surge cost 3); **MTG's targeting rule** (`CardStep.NeedsTarget`); spring row scrolls; no foe levels shown |
| `2102b79` | **Foe difficulty pass** (CRUSH, ENRAGE on wild foes, exam hits ×1.3 from region 2), then **family parity** (Ember's floor up, Grove trimmed); `party-sim variants` / `party-sim cards` |
| `7f11808` | **Foe pass redone on parity** — 38.8% won, Bramble 36.5% / Pike 41% |
| `68eaccc` | **The declutter pass** — symbols, not sentences; hover tips everywhere; ? panel; real card faces on run screens |
| (this one) | The session's UI lessons in docs, the `add-icon` skill, this handoff |

## 3. Where things are

- **Rules**: `KinCore/Party/PartyEmber.cs`, `PartyGrove.cs`, `PartyFamilies.cs` (Rooted, GROW,
  turn start, token falls); content `PartyEmberCards.cs` (`EmberCards`), `PartyGroveCards.cs`
  (`GroveCards`); foe tiers and exam multipliers `PartyWorld.cs` (`Tiers`, `ExamHit`); the bot's
  target `PartySim.BotWins`.
- **Tests**: `KinCore.Tests/PartyTests.Ember.cs`, `PartyTests.Grove.cs`;
  `EveryCardARunCanHoldCanBePlayedSomewhere` and the targeting test in `PartyTests.Families.cs`.
- **UI**: `KinSymbols` (every symbol's meaning, ONCE), card text is WORDS again (`KinUI.md`, the
  reversal 2026-10-01), `KinRelayCreature` (Block shield, chips, badge, `TipAt`),
  `KinRelayField` (chips, intent dots from `IntentTargets`), `KinPartyBoard` (tips, ? panel),
  `KinPartyRunScreens` (`CardButton`, `MonsterTile`). Icons in `Art/icons/`, credited in `CREDITS.md`.
- **Measurements**: `docs/findings/companion-balance.md` — the difficulty pass, parity, the redone
  pass, each with per-region and per-starter tables.
- **Skills**: `design-card` (now with "Symbols — every new mechanic needs its wiring"), new
  `add-icon` (game-icons.net recipe).

## 4. Rules Shayne set this session (all also in memory)

- **Families within 10 points of each other's win rate before ANY foe balancing**; report
  `party-sim` per starter, ≥200 runs a starter (±5 points of noise at 100). Harder foes hurt the
  fragile family (Ember) more — re-check parity after every foe pass.
- **Symbols first** (a default, not a law): when unsure, a symbol + number with the words in a
  hover; text must earn its place. Every symbol must explain itself on hover.
- **A mechanic serving several purposes is good** (tokens: wall, fuel AND attacker).
- **Push cards first, nerf after play** (still standing).
- **The bot wins ~35%**: a person plans further than its one-turn lookahead.

## 5. Scars worth not re-earning

1. **Build the GODOT project before a capture** (`dotnet build SQGodotCommon/SQGodotCommon.csproj`)
   — `Run-Godot.ps1` runs the last build; stale numbers look exactly like a bug in the change.
2. **Captures cannot hover**: `--mouse=x,y` (canvas pixels = 1600×900 capture × 1.2; re-read after any
   layout change) and `--hover-card=N`; native tooltips need ~2.5 s. Run captures from the repo root.
3. **A missing button or row in a capture = an exception while the screen was built** (the shared
   card filled before `Ready` dropped the reward cards AND the SKIP). Check the log first. The other
   Godot traps (`TextureRect` sizing order, live cards in menus, `MouseFilter.Pass`) are in
   `.claude/rules/kin-frontend.md`.
4. **Edit scripts**: write long Python patch scripts with the Write tool (a Bash heredoc broke on an
   apostrophe); keep each file's own line endings; **assert every anchor's count** and order
   replacements so one cannot create a second copy of the next one's anchor (Chain Lightning's
   "deal 5" did, and the script aborted — good, it asserted). CSharpier reformats on commit, so
   anchors from before a commit may no longer match.
5. **A sim number is the bot's, not a person's.** The bot takes the FIRST card offered (so per-card
   win rates are a near-random experiment — `party-sim cards`) and misses multi-turn plans (Ember's
   chains and banked energy). Ember's combo pieces needed a standalone floor for the bot.
6. **A dangling `else`** bound to the inner `if` of a loop and broke the hint silently — brace them.
7. **Old tests encode old rules** (a summon only on your front; region-1 foes ≤ Lv 5): when Shayne
   overturns a rule, update the test with the reason rather than bending the change to it.

## 6. Next

1. **Shayne playtests** the decluttered UI and the parity balance. Things to watch: hover tips (icons,
   a hand card's panel position — only checked via `--hover-card`), drop-on-hand returns the card,
   no-target cards on empty grass, reward/shop/spring card clicks; does Pike feel on par with
   Bramble; Grove vs the Goblin Chief (the bot stalls there — 7–9 runs in 400).
2. **The colourless draft**: 3 colourless monsters and their cards (`KinFamiliesPlan.md`, round 4:
   "5 per family + 3 colourless"), interview-led like the families.
3. Then: regions 3–5's own exams (they reuse region 2's); knocked-out monsters greyed on the field
   (planned in the declutter pass, not done); art for the new cards and tokens (Seedling, Log, Treant,
   most Ember cards draw a silhouette; Bristle draws an old turtle by name); Meteor's cost shows 0
   (the card face has no X).
