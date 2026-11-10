---
id: TBL-6
title: Known cards in the opponent's hand show face up on the table
status: done
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

- [x] A card in the opponent's hand that is known to you is drawn face up (its real card), the rest as backs
- [x] It stays face up only while it stays known (a shuffle into the library, say, ends it — `MoveCard` decides)
- [x] Zefram Cochrane's "You may look at the top card of your library any time": while you control him,
      your library's top card is shown face up to you (`MagicState.TopIsKnownToOwner`) — the log says it for
      now (MC-5 Decisions, 2026-11-01)

## Decisions

- 2026-11-10, Claude (Shayne: decide what can reasonably be decided; open to change): face up AND a small eye
  badge (hover: "Known to you"), so a face-up card in their hand can't be mistaken for one in yours.
  REVISED the same night, building it: the table drew no opponent's hand at all (a count on the plate), so
  the hand went ON the AI's plate — a back per card, a known one face up — where it can't be taken for one of
  yours; no eye badge (no eye icon in the repo either), the hover says "Known to you".

## Tasks

- [x] TBL-6.1 The opponent's hand reads `KnownTo` and draws known cards face up
      `Card.IsKnownTo(player)` (MagicCore; `IsHiddenFrom` now reads it); `PlayerPlate`: the other player's hand as
      mini cards (MtgGame's `card_back.png`, a known one its face, hover previews), and your library's top while
      `TopIsKnownToOwner` (Zefram). Real input: Zefram and V'Ger put in, V'Ger's look chosen and the AI targeted —
      seven backs became seven faces; "top of your library" with the card. Stays face up while `MoveCard` keeps it
      known (a shuffle forgets). Test: `FraKnowledgeTests` reads `IsKnownTo` per player

## Next

Not started. Read `MagicCore/Cards/Card.cs` (`KnownTo`, `Knowers`) and the rule "Who knows a hidden card"
in `.claude/rules/magic-mechanics.md`; the table's opponent hand is in `SQGodotCommon/MagicTable/`.

## Log

- 2026-10-31: created — Shayne, while V'Ger was scripted in MC-5.3 batch 1.
- 2026-11-01: requirement added — Zefram Cochrane's top card (Shayne, MC-5.3 batch 4c).
- 2026-11-10: TBL-6.1 built (overnight); the eye badge dropped for the plate (Decisions).
