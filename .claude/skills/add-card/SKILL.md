---
name: add-card
description: Add or change an MTG card in MtgCore — building it with CardFactory, assigning its colour pips, making its rules text render, and verifying it actually does something. Use when adding cards to a set (Core Set Cube / CSC, Combo Proving Ground / CMB, Legacy / LEG), filling a colour or colourless section, implementing a card's ability, changing a card's cost or pips, or when a card "does nothing" / "renders blank" / "is inert". Covers the four silent failure modes that produce a card which compiles, passes tests, and is still broken.
---

# Adding a card to MtgCore

Reference for the builder API, per-colour section notes and the full cookbook is
`.claude/rules/mtg-cards.md` — it loads automatically when you open a file under `MtgCore/Sets/` or
`MtgCore/Cards/`. Engine mechanics are `.claude/rules/mtg-mechanics.md`. **This skill is the order
of operations and the checks**, not a second copy of the reference.

## Why this needs a procedure

A card can compile, be registered, pass the whole suite, and still be broken in four ways that
produce no error at all:

1. **Inert** — the ability never fires. Nothing throws; the card is just a vanilla body.
2. **Blank-faced** — `MtgCardMapper.GetRulesText` doesn't know the action, so the card renders with
   an empty rules box and is undraftable. Invisible in a screenshot.
3. **Mis-pipped** — colour landed on the wrong card. Every existing test still passes.
4. **Unbuildable** — the pip depth makes it uncastable in any real deck.

Each step below exists because of one of these.

## Steps

### 1. Read the section header before adding

Open the file you're adding to (`MtgCore/Sets/CoresetCube/CoresetCube<Colour><Kind>.cs`) and read
its header comment. Every divergence from the printed card, and why, is recorded there. If the card
needs a mechanic the engine lacks, **build the mechanic** — dropping the ability defeats the point
of sourcing from a real cube. Only cut when the concept is structurally absent (no blocking, no
priority), and comment the cut on the card itself.

### 2. Build the card

Use `CardFactory`. Pick the matching recipe from the cookbook in `.claude/rules/mtg-cards.md` rather
than inventing a shape — the recipes are the verified-working ones.

Two rules that are easy to get wrong:
- A **death trigger** needs `ActiveInZone = ZoneType.Graveyard`. The card is already in the
  graveyard when `CheckStateBasedEffectsAction` scans, so without it the trigger never fires.
- `TriggeredAbilityComponent.Effects` is a **list** — read it, not `Effect`. `Effect = ...` is a
  write-only convenience that appends.

### 3. Assign colour pips — anchor on the factory call

**Never anchor a bulk edit on a quoted card name.** Card names appear in other cards' doc comments,
so a name-anchored insertion lands the pips on whatever card is defined next. This has already
happened: Llanowar Elves came out needing UUBRG, and Lotus Bloom — a colourless artifact — needed
UU. Every existing test still passed.

Anchor on the `CardFactory.Creature(` / `.Spell(` call itself, and add an expected pip assertion.

Artifacts and lands stay **colourless**: an artifact's real colour is colourless, and a land's
colour is what it PRODUCES (`LandColorComponent`), not what it costs.

**Check the pip depth against the table in the root `CLAUDE.md` before committing to it.** A double
pip is effectively a mono-colour card — a two-colour deck casts `WW` on curve only 65% of the time
by turn three. If the card isn't meant to be a mono-deck card, it shouldn't be double-pipped.

### 4. Make the rules text render

`MtgCardMapper` silently omits any mechanic it doesn't know. Wiring CSC into the draft UI produced
**22 completely blank card faces** on the first run of the rules-text test — the engine was correct
and fully tested, the cards were simply undraftable.

Two describe paths must both be extended, and missing either leaves a hole:
- `DescribeEffect` — the action used directly as a `CardEffect.ActionTemplate`
- `DescribeStep` — the same action used inside a `PipelineAction`

A **counterspell trap** is the worst case: a `SpellComponent` with no effects at all, so nothing in
the effect machinery has anything to say about it. That is why `DescribeCounterTrap` exists.

Keep it terse. The rules box shrinks to fit and then clips at a 14pt floor — a clipped card is
invisible in a screenshot but stops telling you what it does.

### 5. Verify — the consequence, not the construction

Run these, in this order. **Do not skip to the build; a build proves nothing here.**

```
dotnet test MtgCore.Tests --filter "FullyQualifiedName~ColorTests"
dotnet test SQGodotCommon.Tests --filter "FullyQualifiedName~RulesTextTests"
dotnet test MtgCore.Tests
```

`CoresetCubeColorTests` / `LegacyAndComboColorTests` catch mis-pipping; add a `[TestCase]` row for
your card's expected colour and pip count. `CoresetCubeRulesTextTests` catches blank faces —
`NoCardWithAMechanic_RendersBlankRulesText` and `EveryCard_HasATypeLine` are the set-wide nets;
extend them when you add a mechanic.

Then **write a test that asserts the card's effect happened** — not that the card exists, not that
it has a component. Put the card on the battlefield, fire the trigger, assert the board changed.
Four silent no-op engine bugs were found only this way.

Tests use **inline card definitions**, never `CardLibrary` lookups, so card balance tweaks can't
break them.

### 6. Read the rendered text yourself

Dump the card's rendered face and read it. The blank-face sweep catches *empty*; it does not catch
*wrong* or *omitted a clause*. This is the step that gets skipped and it is the one that finds the
card that says less than it does.

## Done when

- [ ] Section header read; any divergence commented on the card
- [ ] Pips anchored on the factory call, with a `[TestCase]` asserting them
- [ ] Pip depth is deliberate, checked against the mana table
- [ ] `DescribeEffect` and `DescribeStep` both handle any new action
- [ ] Colour + rules-text + full `MtgCore.Tests` all green
- [ ] A test asserts the card's *effect*, using an inline definition
- [ ] The rendered text has been read, not just asserted non-empty
