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
