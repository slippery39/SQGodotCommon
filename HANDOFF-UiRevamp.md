# Handoff — the Dark Arcane UI revamp, official card art, and three measured fixes (2026-10-06/07)

**State at handoff:** branch `MTG-ImmutableObjects-PlayingAround`, **16 commits ahead of origin,
not pushed** (Shayne pushes). Tests this session: **SQGodotCommon 119, MtgSimulator 468**, all green.
MtgCore and ImmutableGameObjects were not touched and not re-run.

**Everything is committed** — the probe audit and the gauntlet re-measurement were the last two
commits before this file. Shayne reviews and asks for each commit — never commit unasked.

---

## 1. What happened, in order

**Chose a look** (`7f04acb`). Shayne supplied Shandalar / MTGO / MTGA / Hearthstone screenshots.
The engine (one phase, no blocking, no priority) makes Hearthstone the layout reference, not MTGO.
Two ChatGPT directions on one layout; **Dark Arcane** won (leather, brass, candlelight, split P/T
discs) and held across a second screen. Everything is in **`MtgVisualDesign.md`** — requirements,
prompts, rubric scores, the locked palette sampled from the mockups, and a build log per step.
Mockups and references: `docs/mockups/mtg/`.

**Built it in seven steps** (`fc1102e`..`007e308`), each captured and looked at:

| step | what | the bug it found |
|---|---|---|
| 1 kit | EB Garamond theme, Cinzel titles, 9-slice plates from `tools/make_mtg_ui.py` | 121 CSC cards had no art — the scraper was never run for the cube |
| 2 card face | `MtgCardMapper.Face` (one factory for 5 screens), light frame, cost + colour gems top-right | **frame-by-colour had NEVER rendered**: `new_holo.gdshader` dropped `COLOR` |
| 3 backdrop | ComfyUI leather mat with a centre-line medallion | — |
| 4 board | `BoardToken`s, `PlayerHud` (portraits, life seal, zone piles), exile viewer | lands never reach the battlefield (they become mana and are exiled) — the planned land row was deleted |
| 5 mana | `ManaBar`: generic crystals + a gem per colour | the hand's playable glow ignored colour; now asks `CostEngine.ValidateManaPayment` |
| 6 icons | 19 game-icons.net symbols, `MtgSymbols` registry, `CREDITS.md` | — |
| 7 screens | title ("Arcane Duel"), deck select, draft, tournament | deck select pushed **Start Battle off a 720p screen** |

**Played on a phone** (APK over LAN). Three fixes (`083fff7`): a draft tap picked before the card
could be read (now tap = select, tap again / PICK = pick); hand previews unreadable on a phone
(near-full-height preview on mobile); **the three CMB decks played 17 Plains** (their builder
skipped `ManaBase`; pinned by `ManaBaseTests.EveryRegisteredDeck_ProducesTheColoursItsSpellsNeed`).
Shayne confirmed all three fixed on the device.

**Attachments** (`474bcac`): an Aura/Equipment is a round art chip on its host token.

**Art** (`ec49cbb`, `852e8fe`): official Scryfall art for every real card, including tokens (now
discovered by walking each card, not a hand list). The 21 original designs got ComfyUI art, picked
by eye. **Every card in every set has art.**

**Probe audit** — handoff item 6 of the previous session. Real: an activated loot's
discard stayed pending in `Activate`, so Chandra's Regulator moved nothing (DES graveyard enablers
32 -> 34, Sun Titan no longer unbuildable). Not real, by measurement: the trigger probes (a pending
choice does not block later actions, and their fixture's hand is empty — mode 7 output byte-identical
with or without settling them). `ProbeCostDemands` processes no actions.

**CMB gauntlet re-measured** with real reference manabases: field vs references
**45.5%** (Twin 71.5% / Elves 68.0% / Reanimator 24.0% reference wins). Table and caveats in
`docs/findings/evolution.md`. The run's report printed every evolved deck as "Nx Plains" — a
`Decklist.Format` print bug only; fixed.

---

## 2. Decisions already taken — do not re-litigate

- **Dark Arcane** is the look; the palette in `MtgVisualDesign.md` §6 is sampled, not invented.
- **Official art first**: Scryfall (via `MtgArtScraper`) for every real card; ComfyUI only for
  original designs. Saved as a memory.
- **Board creatures are compact tokens**; the full card is the hover / long-press preview.
- **PC and phone equally** — the APK is how Shayne playtests on the go.
- **Mana is two tracks on screen**: generic crystals + per-colour gems. Neither mockup got this right.
- **Tribe-tinted name plates are gone** — a green plate on a black card read as a colour claim.
- **No PICK-less one-tap drafting** — touch needs select-then-pick.
- Title is **"Arcane Duel"**; the menu's dead **Options** entry was removed.
- **The Twin/Elves references still beat the evolved DES field** — a builder/search finding for the
  sim side, not a UI task.

---

## 3. Scars — each cost real time

- **A tint that "should" show may never have rendered.** The card shader dropped `COLOR` and the
  colour tests (which check VALUES) passed throughout. Only a capture shows rendering.
- **"Before colours" leftovers print or pad Plains in three places now found**: the CMB deck
  builder, `Decklist.Format`, and earlier `Draft`/`CardPool`. Grep for `Plains` before trusting a
  colour-related result.
- **Touch: a press is hover and click at once.** Anything that acts on the first tap leaves no
  chance to read. Verify a click with `--tap`, never by reasoning.
- **A Godot import whose project theme references a NEW font fails the first pass** — run
  `--import` twice and check the `.import` files exist.
- **Adding a node to an ancestor from a child's `_Ready` silently fails** (busy parent). In
  `godot-frontend.md`.
- **A probe test that "fails with the fix" may be testing the wrong consequence.** Twice this
  session a pinning test failed for a fixture reason (empty hand; pool creature density under the
  50% likelihood gate). Diagnose before concluding the fix is wrong.
- **Git Bash heredocs expand backticks** — a `CREDITS.md` path vanished into a command
  substitution. Use a quoted heredoc (`<<'EOF'`) or the Edit tool for markdown with backticks.
- **The CSharpier hook re-stages whole `.cs` files**; to split one file across two commits, stash
  or temporarily strip the second half (done for step 7 vs the phone fixes).

---

## 4. Open items, roughly by value

1. **Push** — origin is 16 commits behind.
2. **Rebuild the APK and play on the phone** — it predates the attachment chips and all the new
   art. `./Build-Apk.ps1`, then serve `build/` on the LAN (`python -m http.server 8000 --bind
   0.0.0.0`; this PC was `10.0.0.158`). Nothing in the touch half of `godot-frontend.md` beyond
   the three fixes has been checked on a device.
3. **Hand-play checklist nobody has clicked** (captures cannot hover): token hover preview,
   keyword-disc and mana-gem tooltips, portrait attack, pile -> viewer (graveyard, exile),
   attachment chip targeting, the tournament screen (needs a live tournament; never captured).
4. **Evolved DES decks lose to hand-built Twin/Elves** (~70%). A builder question for the sim side
   (`docs/findings/evolution.md` items (c)/(f) already argue why — synergy cards rate low in
   isolation).
5. Leftover disposable files in `sim_results/`: today's `engines_*`, `metagame_*`, `mutations_*`.
   The new `constructed_values_des_*.json` tables are real (DES presim, 300 decks) — keep those.

---

## 5. Where things are

| | |
|---|---|
| The look: requirements, palette, prompts, per-step log | `MtgVisualDesign.md` |
| Mockups, references, backdrop / portrait / original-art prompts | `docs/mockups/mtg/` |
| UI textures (re-run, then `--import`) | `tools/make_mtg_ui.py` -> `MtgGame/Assets/UI/` |
| Kit helpers (plates, buttons, Title, StyleChoice, backdrop, preview scale) | `MtgGame/Board/MtgUiStyles.cs` |
| Card face skin / symbol meanings | `MtgGame/Cards/MtgCardSkin.cs`, `MtgSymbols.cs` |
| Board pieces | `Board/BoardToken.cs`, `PlayerHud.cs`, `ManaBar.cs`, `ZonePile.cs` |
| Presentation rules (auto-load on MtgGame files) | `.claude/rules/mtg-presentation.md`, `godot-frontend.md` |
| Icon / font / art credits | `CREDITS.md` |

```
dotnet build SQGodotCommon/SQGodotCommon.csproj
./Run-Godot.ps1 MtgGame/MtgGameScene.tscn -Capture shots/x -Seconds 4 -GameArgs '--decks=Affinity,Zoo','--autoplay=6'
./Run-Godot.ps1 MtgGame/Draft/DraftScene.tscn -Capture shots/x -Seconds 4 -GameArgs '--tap=270,170@15'
dotnet run --project MtgArtScraper -- SQGodotCommon/MtgGame/Assets/Card_Art CSC   # then LEG, CMB
./Build-Apk.ps1
```

`--autoplay=N` (AI plays both seats to turn N), `--decks=A,B` (DeckRegistry names) and `--tap=X,Y@F`
(a real click at window pixel X,Y on frame F) are documented in `Commands.md`.
