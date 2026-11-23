# Card-batch usage

What each MagicCore card batch cost, to check that a workflow change saves usage (story MC-5: TRK is
the test of one fresh session per batch). Measured by `tools/usage_report.py`: **input-token
equivalents** (input 1, output 5, cache read 0.1, cache write 2 — as the API prices them), subagents
included. One batch = one session, so a row is the whole session.

**Add a row at the end of every batch, just before its commit:**
`python tools/usage_report.py --log "<task>: <what>, <N> cards"`. Cost per card = total / N.

**Baseline (FRA, 2026-10-10, before fresh sessions):** the rare batches cost ~3M each, eight in one
session (e24a44f6, 81.8M in all); 70% was cache reads of a ~442k-token median context. A fresh
session per batch was simulated to save 37–40%.

| Date | Batch | Session | Calls | Median context | Total |
|---|---|---|---|---|---|
| 2026-10-31 | MC-5.1: the TRK survey + plan, 0 cards | f711d95c | 61 | 184k | 2.0M |
| 2026-10-31 | MC-5.5: card registry across sets, TRK tokens, Planet duals, 10 cards | 498c3cea | 142 | 225k | 4.7M |
| 2026-10-31 | MC-5.3: batch 1 — tier 0-1 TRK commons and uncommons, 88 cards | 0065e5f8 | 125 | 254k | 5.0M |
| 2026-10-31 | MC-5.2 + MC-5.3 batch 2: Station, 12 cards | a4330530 | 116 | 248k | 4.4M |
| 2026-11-01 | MC-5.3: batch 3, the small TRK mechanics (beam me up, SNW, dilemma, power 8, becomes tapped, look-take, Federation), 31 cards | 14545f2e | 153 | 293k | 7.4M |
| 2026-11-01 | MC-5.3: batch 4a — counters of any kind, Sagas, kicker, can't block, once each turn, 28 cards | dfef712d | 143 | 297k | 7.4M |
| 2026-11-01 | MC-5.3: batch 4b — delayed return, leaves the graveyard, type change, second resolution, granted trigger, control, counters plus one, block/target restrictions, 17 cards | 8cd8b49f | 173 | 286k | 7.6M |
| 2026-11-02 | MC-5.3: batch 4c — exile face down, play an opponent's exiled card, one destroy judged together, draw, can't attack alone, lands enter untapped, Zefram, 14 cards | 7cf44660 | 185 | 278k | 8.3M |
| 2026-11-04 | MC-5.3: batch 5a — tier 1–2 rares and mythics, 40 cards | c6125612 | 143 | 280k | 5.6M |
| 2026-11-05 | MC-5.3: batch 5b — delayed return on a dead card, charge doubled, a mana ability that costs mana, tap N as a set, life lost this turn, base P/T for good, reveal top X (+ sprint 4 planning), 8 cards | 9c72e4a6 | 146 | 268k | 5.8M |
| 2026-11-05 | MC-5.3 batch 6a: shocklands, sunburst, X targets picked as cast, changeling, a number as it enters, 14 cards | 7c02dbfc | 164 | 280k | 6.5M |
| 2026-11-05 | MC-5.3 batch 6b: the re-tier's ten (lands from the top, counters doubled, charge counters put, combat damage as that many, a counter of a chosen kind, DS9) + Spock, Georgiou, Miles; probe capped (Tribbles), 13 cards | f5c958fd | 126 | 217k | 4.0M |
| 2026-11-06 | MC-5.3 batch 7a: TRK's grouped tier 3 (trigger twice, assimilate, control until end of turn, dilemmas this turn, casting from odd zones), 14 cards | 1343051e | 165 | 221k | 5.4M |
| 2026-11-06 | MC-5.3 batch 7b: 11 TRK one-offs (306/311) — modes not chosen before, can't sacrifice Treasures, enters attacking, granted mana, explore, player hexproof, counters from among, 11 cards | 940bf8cb | 131 | 205k | 4.2M |
| 2026-11-06 | MC-5.3 batch 7c: TRK's last five, tier 3 (gained abilities, an ability from exile, a chosen name + ability activated, an extra turn, phasing) — TRK complete 311/311, 5 cards | 3cb37ded | 158 | 253k | 5.5M |
| 2026-11-06 | MC-5.6: Stardates imported + surveyed, MC-6 composition audit, 0 cards | 41778005 | 76 | 169k | 2.1M |
| 2026-11-06 | MC-5.6 batch 1: the Stardates on MC-6.1–6.4 (9 tier 1, 4 families composed, 16 old cards moved), 17 cards | d66c1b3a | 176 | 257k | 5.8M |
| 2026-11-06 | MC-5.6 batch 2: Stardates on MC-6.5–6.8 + 6.9's permission (counters, modes, cycling, evasion), 7 cards | 3ff48e06 | 197 | 273k | 6.6M |
| 2026-11-07 | MC-5.6 batch 3a: Riku, Prosper, Drivnod (Phyrexian mana, keyword counters, LandPlayed, modes counted), 3 cards | 5b8df0f2 | 149 | 265k | 4.9M |
| 2026-11-07 | MC-5.6 batch 3b: Sen Triplets, Flare of Fortitude, Mogis on MC-6.15/6.9/6.13 + Granted perf fix, 3 cards | b3f0fe83 | 181 | 232k | 5.2M |
| 2026-11-07 | MC-5.4 overnight: TRK collation, art, interim ratings, sealed + draft on the table; DATA-2 self-play checked on FRA (loses to interim); walker games -> AI-9.3 next | 872d3ed6 | 325 | 337k | 13.1M |
| 2026-11-07 | AI-9.3: walkers priced over their lifetime, 2 positions, match no difference | d0d30868 | 144 | 217k | 4.9M |

## DEV-4: the mechanics rules split by subsystem (2026-11-12)

**Before:** `.claude/rules/magic-mechanics.md`, 65.7 KB (~16k tokens), loaded by ANY MagicCore file. Of the last
58 `mtg-real` sessions, 45 loaded it: 23 card batches or card-script edits (7,220 calls after it loaded) and **22
others** — AI, sealed and table work that opened an `Ai/`, `Limited/` or engine file (12,785 calls).

**Which files a batch opens** (the last ten `/card-batch` sessions, Read/Write/Edit and Bash paths): `Effects/` in
every one, then `Sets/` scripts and `MagicCore.Tests/Sets/`, `Actions/`, `Turns/`, `Rules/`, `Mana/`, `Filters/`. A
batch touches nearly every subsystem, so a batch saves nothing; the split pays in the other sessions.

**After:** seven files, every rule in exactly one — casting 20.1 KB, triggers 14.3, statics 13.2, zones 13.1,
mana 6.8, combat 2.9, ai 0.9. A card script, card test or `Effects/` file loads the six mechanics files (69.3 KB,
+5% for their headers); an engine file loads its own subsystem's; `Ai/` loads 0.9 KB; `Limited/` nothing.

**Estimate (an upper bound — some of the 22 opened an engine file too):** at 0.1 per cache read, ~16k × 12,785 ×
0.1 ≈ **20M** over those 22 sessions (~0.9M each) not spent; card batches pay ~+0.02M each. A real batch's row
after the split is DEV-4.3.
| 2026-11-13 | MC-4.3a: LSV batch 1 power and mana, 54 cards (44 scripted + 10 duals), 4 new pieces | 0d5f7560 | 128 | 247k | 4.2M |
| 2026-11-13 | MC-4.3b: LSV batch 1 plain spells, 22 cards, 4 options | 0d5f7560 | 177 | 297k | 7.6M |
| 2026-11-13 | MC-4.4a: batch 2's alternative and or costs, 10 cards | 14ab6621 | 143 | 290k | 5.7M |
| 2026-11-13 | MC-4.4b: the Spirit Guides and Delighted Halfling, mana from the hand and a yield per spell, 3 cards | 3368d9ff | 114 | 213k | 3.9M |
| 2026-11-13 | MC-4.4c1: red's tier 1, the tested pieces, 11 cards | 3368d9ff | 133 | 227k | 4.6M |
| 2026-11-13 | MC-4.4c2a: LSVPower, red's options part a, 6 cards | 5b15092f | 106 | 241k | 4.0M |
| 2026-11-14 | MC-4.4c2b: LSVPower, red's options part b, 7 cards | 743a66fb | 58 | 196k | 2.1M |
| 2026-11-14 | MC-4.4c2c: Laelia, the zone-change event, 1 card | 743a66fb | 70 | 230k | 2.5M |
| 2026-11-14 | MC-4.4c2d: "Name Sticker" Goblin - NotFrom on the entry event, RollDie, 1 card | 909c1a69 | 45 | 170k | 1.5M |
| 2026-11-14 | MC-4.4c2e: Pyrokinesis - TargetSpec.Divide, Pure Energy moved onto it, 1 card | 909c1a69 | 78 | 213k | 2.8M |
| 2026-11-14 | MC-4.4d1: white's tier 1 surveyed (36, five parts) and its tested pieces scripted, 21 cards | c4f7ec87 | 59 | 214k | 2.0M |
| 2026-11-15 | MC-4.4d2a: white's options, zones and tokens - MoveTo.Beneath/Loses/Then, DelayedTrigger.MadeBy, tokens doubled, an entry refused, one X for a trigger, 7 cards | 70b49af9 | 92 | 239k | 4.3M |
| 2026-11-15 | MC-4.4d2b: LSVPower white's options, restrictions and timing, 5 cards | 472a6aaa | 64 | 271k | 3.8M |
| 2026-11-15 | MC-4.4d3: LSVPower, white's options - leaving the battlefield, 2 cards | 771fbcee | 99 | 314k | 5.4M |
| 2026-11-15 | MC-4.4d4: LSVPower, Balance - the rest beyond the fewest as one count, Emrakul's ward moved onto EachSacrifices.N, 1 card | db1e96c0 | 60 | 227k | 2.8M |
| 2026-11-15 | MC-4.4e1: LSVPower, blue's tier 1 split in seven parts, and its tested pieces, 12 cards | f5fb4b80 | 53 | 248k | 3.4M |
| 2026-11-16 | MC-4.4e2a: LSVPower, blue's options - the library's top, 4 cards | 3f336516 | 80 | 302k | 4.3M |
| 2026-11-16 | MC-4.4e2b: LSVPower blue's options - draws, 4 cards | 24bec349 | 51 | 232k | 2.8M |
| 2026-11-16 | MC-4.4e2c: LSVPower blue's options - mana and costs, 5 cards | fe033ae1 | 134 | 240k | 5.1M |
| 2026-11-16 | MC-4.4e2d: casting off the hand, 4 cards | 8bdc7f1e | 144 | 237k | 4.9M |
| 2026-11-17 | MC-4.4e2e: blue's options, for as long as this remains, 2 cards | c090bbd1 | 89 | 188k | 2.8M |
| 2026-11-17 | MC-4.4e2f: LSVPower, blue's options - choices after the cast, 4 cards | bf9c2a7a | 107 | 242k | 4.2M |
| 2026-11-17 | MC-4.4f: blue's tier 2 - squad, evoke, improvise, scry, a graveyard cast, 6 cards | 28f01c34 | 147 | 282k | 6.3M |
| 2026-11-17 | MC-4.4g1: protection + blue tier 3 plan, 1 card | 126064e1 | 78 | 191k | 3.1M |
| 2026-11-17 | MC-4.4g1b+g2: a clone's choices, delve, 2 cards | deb22edb | 116 | 213k | 3.9M |
| 2026-11-18 | MC-4.4g3: dungeons and the initiative (Undercity, venture, goad), 1 card | ff3671cc | 109 | 220k | 3.8M |
| 2026-11-18 | MC-4.4g4: face-down permanents, manifest dread, 1 card | e89ba4c1 | 74 | 206k | 2.8M |
| 2026-11-18 | MC-4.4g5: Adventures (Brazen Borrower // Petty Theft), 1 card | fdc9b8fb | 79 | 213k | 3.1M |
| 2026-11-18 | MC-4.4g6: teamwork (We Say Thee Nay!), 1 card | e2798839 | 58 | 182k | 2.1M |
| 2026-11-18 | MC-4.4g7: storm (Brain Freeze), 1 card | d8cb5e5f | 73 | 171k | 2.1M |
| 2026-11-18 | MC-4.4g8: spree (Three Steps Ahead), 1 card | 471f3273 | 57 | 167k | 1.7M |
| 2026-11-18 | MC-4.4g9: warp - LSVPower Quantum Riddler, 1 card | b80d6d6e | 83 | 182k | 2.7M |
| 2026-11-18 | MC-4.4g10: Classes - Does Machines (ClassLevel, GainLevel, Scripting.Level), 1 card | c123515f | 96 | 201k | 3.0M |
| 2026-11-18 | MC-4.4h1: black's tested pieces, 17 cards | 4a569576 | 71 | 177k | 2.2M |
| 2026-11-19 | MC-4.4h2a: hands - YouChooseTheyDiscard Filter/UntilSourceLeaves, escalate, Chain of Smog, 5 cards | 38e66320 | 84 | 191k | 2.9M |
| 2026-11-19 | MC-4.4h2b: graveyard returns + MC-4.5, 5 cards | b25199e7 | 135 | 224k | 4.5M |
| 2026-11-19 | MC-4.4h2c: costs and permissions (PayLifeCost counted, X from a cost, MayPlayFromZone.Free, mana ability without {T}, this-turn exile-instead, entering counter kind), 5 cards | f0b96d71 | 114 | 244k | 4.4M |
| 2026-11-19 | MC-4.4h2d: triggers (magecraft's copy event, the turn's draw, odd or even, an exile Then decides), 4 cards | 8febb927 | 104 | 227k | 3.9M |
| 2026-11-19 | MC-4.4h2e: zones and combat (revolt, three blockers, shadow/void, escape), 4 cards | cab75733 | 105 | 240k | 3.8M |
| 2026-11-19 | MC-4.4h3: overload (Damn), 1 card | f5a8fcd4 | 77 | 198k | 2.5M |
| 2026-11-20 | MC-4.4h4: splice onto Arcane, 1 card | 3f6773f9 | 97 | 227k | 3.6M |
| 2026-11-20 | MC-4.4h5: Auras returning a creature (Animate Dead, Necromancy), 2 cards | f4253bfd | 85 | 224k | 3.3M |
| 2026-11-20 | MC-4.4h6: naming a card as a spell resolves (Demonic Consultation), 1 card | d929fb26 | 63 | 179k | 1.8M |
| 2026-11-20 | MC-4.4h7-h9: saddle, emerge, miracle as ONE batch (Caustic Bronco, Crabomination, Metamorphosis Fanatic), 3 cards | 35f5d2dd | 127 | 243k | 4.7M |
| 2026-11-20 | MC-4.4i1 + i2a: red's tested pieces and trigger/combat options, 10 cards | f62f97fb | 103 | 222k | 3.7M |
| 2026-11-20 | MC-4.4i2b + i3: multikicker, dash, backup, an additional combat phase, 4 cards | dbad31cf | 87 | 230k | 3.5M |
| 2026-11-20 | MC-4.4i4–i6: counters on a player (energy, poison), transforming DFCs, transform by an activation, 3 cards | 48c24254 | 123 | 264k | 5.0M |
| 2026-11-20 | MC-4.4i7: the missed DFCs (DealtDamageEvent, TransformedEvent, CR 701.28f, modal DFCs), 5 cards | 573e163d | 116 | 260k | 4.7M |
| 2026-11-20 | MC-4.4j1 + j2a: white's tested pieces and protection/trigger options, 11 cards | 5c8c0471 | 89 | 230k | 2.8M |
| 2026-11-21 | MC-4.4j2b + j3: white's counter/cost/token options and the monarch, 5 cards | 3e948233 | 118 | 218k | 4.2M |
| 2026-11-21 | MC-4.4j4–j6: ascend, reconfigure, voting (Ocelot Pride, Lion Sash, Council's Judgment), 3 cards | 81ccf22f | 96 | 227k | 3.4M |
| 2026-11-21 | MC-4.4k1: green's tested pieces (two took an option: IfNth Otherwise, a land's zone-change event), 21 cards | 4aaa62c6 | 73 | 225k | 2.3M |
| 2026-11-21 | MC-4.4k2a: green's mana, land and token options (OnlyIf, eternalize, sacrifice event, Fastbond, shroud, persist), 7 cards | 88002b3e | 96 | 230k | 3.6M |
| 2026-11-21 | MC-4.4k2b: green's trigger, combat and casting options (a spell as a target, attacks that player, any-kind counter removal, unpreventable combat damage, into a graveyard from anywhere, level up), 8 cards | a492df18 | 91 | 276k | 4.5M |
| 2026-11-21 | MC-4.4k3: green's four new pieces (crew, earthbend, bestow, day/night), 4 cards | ae115d7d | 82 | 308k | 5.4M |
| 2026-11-21 | MC-4.4l plan + l1 + l2: colourless surveyed and planned (105), then its tested pieces - 40 lands, 22 others; Chromatic Star's mana ability fixed (CashedIn), Lightning Greaves out (the AI loops), 62 cards | 610fb846 | 142 | 387k | 9.5M |
| 2026-11-22 | MC-4.4l3a + l3b: colourless' options (mana; exile, graveyard and library), 21 cards | 743aac59 | 119 | 419k | 8.4M |
| 2026-11-22 | MC-4.4l3c: colourless' trigger, combat, equipment and casting options, 12 cards | 9e827eca | 144 | 302k | 8.0M |
| 2026-11-22 | MC-4.4l4a + l4b + l5: colourless' new pieces and Lightning Greaves, 10 cards | bf0edc53 | 231 | 392k | 11.6M |
| 2026-11-22 | MC-4.4m1 + m2a: multicolour's 20 tested pieces and 12 casting and stack options, 32 cards | 3849d1be | 182 | 317k | 8.2M |
