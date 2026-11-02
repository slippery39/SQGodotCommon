---
id: TBL-6
title: Known cards in the opponent's hand show face up on the table
status: backlog
area: TBL
created: 2026-10-31
---

## Why

Shayne, 2026-10-31 (MC-5.3, V'Ger, the Intruder: "Look at target opponent's hand"): "we should be able
to see cards in the opponent's hand … any known cards will show the actual card in hand rather than
the back." The opponent's hand is drawn at the top of the table as card backs. The engine already
knows which of them you have seen (`Card.KnownTo`, set by `s.MakeKnown` on every look and reveal), but
no front end shows it — V'Ger's look goes to the game log for now (MC-5 Decisions).

## Requirements

- [ ] A card in the opponent's hand that is known to you is drawn face up (its real card), the rest as backs
- [ ] It stays face up only while it stays known (a shuffle into the library, say, ends it — `MoveCard` decides)
- [ ] Zefram Cochrane's "You may look at the top card of your library any time": while you control him,
      your library's top card is shown face up to you (`MagicState.TopIsKnownToOwner`) — the log says it for
      now (MC-5 Decisions, 2026-11-01)

## Decisions

Open questions:
- Mark a known card as known (a badge), or only face up?

## Tasks

- [ ] TBL-6.1 The opponent's hand reads `KnownTo` and draws known cards face up

## Next

Not started. Read `MagicCore/Cards/Card.cs` (`KnownTo`, `Knowers`) and the rule "Who knows a hidden card"
in `.claude/rules/magic-mechanics.md`; the table's opponent hand is in `SQGodotCommon/MagicTable/`.

## Log

- 2026-10-31: created — Shayne, while V'Ger was scripted in MC-5.3 batch 1.
- 2026-11-01: requirement added — Zefram Cochrane's top card (Shayne, MC-5.3 batch 4c).
