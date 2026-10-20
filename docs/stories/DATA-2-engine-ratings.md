---
id: DATA-2
title: Rate a set's cards without 17lands — measured by the engine, checked against FRA's real rates
status: backlog
area: DATA
created: 2026-10-20
---

## Why

Shayne (2026-10-20): the sealed builder needs card ratings, and 17lands has none for a custom set or
one revealed but not yet playable online — "so we cannot fully rely on it… we also need to be able
to pre-measure cards". The engine's measured `CardValues` can't stand in: they count only the turn a
card is cast, so every FRA planeswalker reads last (2.7–3.75) and value climbs with cost (median
6-drop 16.5, 1-drop 4.5); FRA's median mythic measures BELOW its median common.

## Requirements

- [ ] A rating for every card of a set from the engine alone, on `CardRatings`' scale, so the
      sealed builder takes it unchanged
- [ ] Checked on FRA against 17lands' GIH WR (`Sets/FRA.17lands.json`): how well it ranks the cards,
      and which it gets most wrong

## Decisions

- 2026-10-20, Shayne: needed; FRA uses 17lands meanwhile (LIM-1).

Open questions:
- How: a simulated games-in-hand win rate from AI sealed games (17lands' own measure, made by
  self-play), a fix to `CardValues`, or both?
- How many games that takes, and how long (ask before any run over ~15 minutes)

## Tasks

- [ ] DATA-2.1 Interview the open questions

## Next

Read `MagicCore/Limited/CardRatings.cs` (the scale and the table shape), `MagicCore/Ai/CardValues.cs`
(what is measured today and its known limits), `Limited/SealedBuilder.cs` and `Boosters.cs` (pools
and decks for self-play).

## Log

- 2026-10-20: created from LIM-1.4.
