---
paths:
  - "MagicCore/Ai/*.cs"
  - "MagicCore.Tests/Ai/*.cs"
---

# MagicCore — the AI's evaluation

The real-rules engine's per-mechanic rules for the position score's terms — each the ONE way that mechanic works
here. The rules that hold on every task are in `MagicCore/CLAUDE.md`. One file per subsystem (DEV-4):
`magic-casting.md`, `magic-triggers.md`, `magic-combat.md`, `magic-mana.md`, `magic-zones.md`, `magic-statics.md`, `magic-ai.md`; a card script or card test loads all but the AI's. A new rule goes in the one file
whose subsystem it lives in.

- **AI evaluation terms are MONOTONIC** — more of a good thing always scores more, tapering not
  capping. A cap makes "more" tie with "less", and a tie silently picks the passive move: a life cap
  at 20 stopped every attack past tapped blockers; a land cap at 7 made the AI hoard lands.
- **A move the search should never try is left out in `Evaluator.SearchMoves`, BEFORE it is validated** —
  `MagicRules.LegalActions(worthTrying)`, the search's filter and never a front end's; `LegalActions()` stays complete.
  Candelabra of Tawnos ("{X}, {T}: Untap X target lands") is offered to the search only for X up to the number of its
  player's lands making more than one mana (Shayne, 2026-11-22), with none never: 21 activations on a 20-land board
  cost their VALIDATION at every node (move generation 0.87 → 3.91 ms), so filtering the finished list saved nothing.
  The choice it then asks is answered by rule (`GreedyAi`: its own lands by `ManaAbilities.YieldOf`, tapped first).
  **A free ability — no mana, no X, no other cost (`ActivatedAbilityComponent.IsFree`: Lightning Greaves' "Equip {0}") —
  is offered to the search once per permanent a turn** (`RepeatsAFreeAbility`, read from `Card.FreeAbilityActivatedOn`,
  stamped as any free ability is activated; Shayne, 2026-11-22, MC-4.4l5): each activation is a new position, so the
  search moved Greaves from creature to creature for 30 decisions and the turn never ended. An ability with a cost
  (Zuran Orb's land) is not free — what it spends must be priced instead. **A mana move that would add no mana**
  (Vivi Ornitier at power 0: "Add X mana … where X is Vivi Ornitier's power") is left out (`MakesNoMana`, MC-9.2).
- **A mana move must BEAT holding, never tie it** (MC-9.5; Shayne, 2026-11-23: a free ability has no opportunity cost,
  "you don't need to use them right away"): its mana floats and scores nothing, and its counter or exhaust is spent for
  good unseen, so a tie went to acting and Pentad Prism's counters went on sight. Of the lines close to the best, those
  with the fewest mana moves (`BeamSearchAi.ManaMoves`) are kept, in `Best` and `Prune` alike; the rollout takes one
  only when it scores better (`Rollout`'s strict rule); `LandMatters` counts the mana floating.
- **AI notes live in `MagicCore/Ai/Notes/`, a file per MECHANIC** (AI-29.1's shape, agreed in the AI lane 2026-11-19;
  the first built here, MC-9.6): matched by what the card has, never its name; called at ONE place for its kind (the
  search's moves, a choice's answer, a held card's price); each its own switch in `Evaluator.Terms` (`beam+no<note>`).
  **A free life gain at instant speed** (Zuran Orb's "Sacrifice a land: You gain 2 life") is offered only when NEEDED
  (`Notes.FreeLifeGain`): passing now loses, or the sacrifice triggers something of ours — Shayne, 2026-11-23: "The
  player effectively already has that life"; watched, below 10 life the search sacrificed every land. The rollout,
  which plays our side only at sorcery speed, sacrifices one at a time to survive.
- **A divided spell's targets and shares are a rule, never searched past a few sets** (MC-9.1): Fire Covenant's
  share for a target is the damage that kills it (`ChooseNumberChoice.ShareOf` → `Lookahead.KillingShare`, in the
  search, the look-ahead and `GreedyAi` alike); past `BeamSearchAi.MaxPicks` target sets `GreedyAi` tries each single
  target and two kill sets (`KillSets`). Watched: before it, Fire Covenant took one 4/4 of twelve creatures.
