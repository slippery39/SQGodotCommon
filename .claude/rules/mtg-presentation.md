---
paths:
  - "SQGodotCommon/MtgGame/*.cs"
  - "SQGodotCommon/MtgGame/*/*.cs"
  - "SQGodotCommon/MtgGame/*.tscn"
  - "SQGodotCommon/MtgGame/*/*.tscn"
  - "SQGodotCommon.Tests/MtgGameTests/*.cs"
---

# MTG presentation layer — card faces, board layout, theme

Loaded when you touch the Godot front end. **Game logic never lives here** (see the root
`CLAUDE.md`) — this file is about how a card and a board are RENDERED.

## Rules text is not cosmetic

**Card text is part of making a set playable, not a cosmetic afterthought.** A pack is read, not glanced at, and `MtgCardMapper.GetRulesText` silently omits any mechanic it does not know — invisible in a screenshot, but it makes the card undraftable. `SQGodotCommon.Tests/MtgGameTests/CoresetCubeRulesTextTests.cs` and `ComboProvingRulesTextTests.cs` pin one card per mechanic and assert no card *with a mechanic* renders blank. Extend them when adding a mechanic. (Hollowmere had a third; it was retired with that set.)

This is not a hypothetical. Wiring the Core Set Cube into the draft UI produced **22 completely
blank card faces** on the first run of that test — every freeze effect, every bounce-to-library,
every prevention, modal and conditional spell — because `GetRulesText` knew none of the actions
they were built from. The engine was correct and fully tested; the cards were simply undraftable.

Two describe paths must both be extended, and missing either leaves a hole:
- `DescribeEffect` — an action used directly as a `CardEffect.ActionTemplate`
- `DescribeStep` — the same action used inside a `PipelineAction`

A **counterspell trap is the worst case**: it is a `SpellComponent` with *no effects at all*, so
nothing in the effect machinery has anything to say about it. It needs its own branch off the
component, which is why `DescribeCounterTrap` exists.

A card face is built from three `MtgCardMapper` calls, not one — each renders to its own element of the card frame:

| Call | Frame element | Notes |
|---|---|---|
| `GetTypeLine(card)` | band across the bottom of the art | Never blank, so it is the "this face rendered something" guarantee. No `"Creature — "` prefix when subtypes exist — the badge already says it. Every spell reads a flat `"Spell"` — see DesignNotes.md. |
| `GetPowerToughness(card, state)` | badge in the bottom-right corner | The **only** P/T source. `state: null` → printed stats, for draft packs. Returns null for non-creatures, which hides the badge. |
| `GetRulesText(card)` | rules box | Deliberately excludes P/T. Legitimately blank for a French-vanilla creature. |

**The card face is a fixed budget, and text is generated, so verbosity is a bug not a style question.** The rules box shrinks its font to fit and then clips at a 14pt readability floor; a clipped card is invisible in a screenshot but stops telling you what it does. Every place that joins rendered fragments goes through `CombineParts`, which squeezes out the two ways generated text repeats itself — a sequence authored twice over (`"take the opponent's best creature, destroy it"` × 2 → `"… — twice"`) and consecutive clauses differing only in their verb (`"Each creature you control gets +2/+2 …"` + `"… gains Flying …"` → one sentence). The clause merge only combines **predicates**: merging noun middles distributed a shared trailing noun and turned four tokens into two. Failing to merge costs a line; merging wrongly misprints the card, so `IsMergeableVerb` is a closed list.

That budget was pinned set-wide by `HollowmereRulesTextTests` (≤6 rendered lines, ≤24-char type lines, no merged noun clauses) without naming cards, so retuning card balance could not break it. **That test went with Hollowmere — a new set should re-establish the same set-wide budget.**

P/T used to be printed by `GetRulesText` *and* drawn by a `BoardCard` overlay label, from printed and effective stats respectively — so a lord-buffed creature read "2/2" in its box and "4/4" in its corner. Keep it single-sourced.

**Board layout is a budget, not a free-form arrangement.** `BoardUI.tscn` is a `MainColumn` of `[TopBar] [OpponentRow] [PlayerRow] [BottomBar]`, where each row is an `HBox` of `[panel][battlefield zone]`. The player panels live *beside* their rows rather than above them specifically so they stop driving the column's height — that is what allows `BattlefieldZone.CardScale` to be 0.68 instead of 0.45. See DesignNotes.md before adding anything to the column.

The event log is a collapsible overlay on its own `CanvasLayer`, closed by default, with an unread count on its toggle so an AI turn cannot pass unnoticed. Nothing reclaims its space automatically — `BoardUI.SetBoardWidth` moves `MainColumn`'s right anchor between 0.78 and 1.0. The hand's drop target is synced from `BoardUI.GetPlayerBattlefieldRect()` rather than hardcoded, since the board changes width when the log opens.

`MtgCardTheme` colours the **frame by the card's COLOUR** (`Card.ColorPips` — WUBRG, gold for two or more, grey for colourless, brown for lands) and the **name plate by tribe**, via `SelfModulate` so the tint cannot bleed onto the labels.

Frame-by-colour replaced frame-by-card-TYPE, which was all this could do before the engine had colours. Type is already stated in words on the type line, so spending the strongest visual channel on it spent it twice; colour is what decides whether a card is castable in a drafter's deck, which is the first question they ask of a pack. Order inside `FrameColor` is load-bearing and commented: lands first (a land has no pips — its colour is what it PRODUCES), then pips before the artifact subtype so a coloured artifact takes its colour.

**Every frame value must stay light.** It is MULTIPLIED against the frame art, so black is a violet-grey rather than black — `MtgCardThemeTests` pins a luminance floor precisely because "black" invites someone to use an actually-black value and turn the art to mud.

Card art is keyed by a slug of the card name (`CardArtLoader`). Hollowmere has essentially none, which degrades to the card scene's default artwork rather than failing — `Details.ApplyTo` assigns the texture unconditionally and the setter falls back, so a reused node cannot inherit the previous card's art.
