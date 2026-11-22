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
  (Zuran Orb's land) is not free — what it spends must be priced instead.
