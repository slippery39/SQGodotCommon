---
id: AI-13
title: Shayne's lessons as general rules for the search — card-agnostic, switchable, measured
status: in-progress
area: AI
created: 2026-10-24
---

## Why

Shayne (2026-10-24): Magic is complex enough that years of play teach lessons a simulated game
state won't find on its own — when to hold Unsummon (AI-4's cases), when the right time to play a
card comes. Rather than only hoping the look-ahead finds the line, try coding the lessons in. "I
think it might be a good thing to test out."

The look-ahead can't learn these by itself: it sees two turns, its simulated opponent plays the
board only (AI-10), and our side casts one spell a turn (AI-8.2). A lesson that comes from
thousands of games is exactly what a two-turn search won't rediscover.

## Requirements

- [x] A **lessons ledger** in this story: each lesson in Shayne's words, the general rule it
      becomes, and its status (proposed, built, kept, dropped — with the measurement)
- [x] Each lesson is a **general rule, never a card's own script** — it reads what a card does
      (an instant, a bounce, a token, an Aura), never its name — so it holds for every card and set
- [x] Each is its **own switch** (as `Evaluator.Terms` and the rollout models are), so it can be
      measured on and off
- [ ] Each is pinned by positions where Shayne agrees the right answer (AI-4.2's for the bounce
      cases), and **kept only if it doesn't lose games** — the AI-6 way: `--suite`, then
      `--match … --until-clear`; a rule that wins its example but loses overall doesn't ship

## Decisions

- 2026-10-24, Shayne: create the story; worth testing.
- 2026-10-24, Claude (proposed, open to change): **general rules, not per-card logic.** Forge, the
  open-source Magic AI, writes "when to play me" per card: it plays decently, but every new card
  needs new code and the rules quietly contradict each other. This repo already refuses a special
  case per card phrase (MtgCore's lesson). A lesson becomes a rule that steers the search — a
  penalty, a bonus, or a move it doesn't consider — and the search still decides.
- 2026-10-24, Claude (proposed): **how this splits with AI-4.** AI-4 COMPUTES the price of holding
  a spell (plans, held spells as options); AI-13 tries the RULES route on the same problems. Both
  are measured on the same positions, and a rule that turns out to be a cheap stand-in for AI-4's
  computed answer is fine until AI-4 lands.

- 2026-10-28, Shayne (AI-13.1): **the first rule is patience with instant-speed cards** (lessons 1,
  4, 5): a card that can be cast later at instant speed (an instant, or flash) isn't cast now unless
  now beats waiting; **it steers as a HOLD VALUE** — such a card is worth more held than a
  sorcery-speed one, so casting now must beat holding by that much, and a big swing (desperation, a
  blowout) still casts. Card-agnostic: it reads instant speed, never a name. Its own switch, measured.
  Positions: Ferocity of the Hunt with Last Gasp unseen (today a TIE, Pass 5.40 = cast 5.40, and the
  tie casts) and #547 (passes since AI-10.4's known-card opponent — kept as a guard).

- 2026-10-28, Shayne (AI-13.3, lessons 2 and 4b checked first): on hand-built positions (they attack
  him at 20 with one creature; Unsummon and two Islands, no creatures) he agrees **bounce a token, an
  Aura'd creature and a countered one**, and **hold against Fateshaper Aspirant** (a recast gives its
  enters again) — today's AI plays all four so. **But it bounces a plain 3/3 and a vanilla 5-mana 3/4
  too, where he holds** ("I'm at 5 life, I can still take a hit", #547): it bounces for the damage, a
  point a life at 20, not for the value. So the value cases are right for the wrong reason — they
  become AI-9.2's tests (life by what it buys), with the plain attackers held.

- 2026-10-28, Shayne (after AI-13.3's finding that the look-ahead spends a held Unsummon in its next
  main phase anyway): the patience cost is not what he meant — **"it should be about calculating the
  expected value of playing the spell now vs holding for a better situation"**, like an option: "Do I
  use this now, spending my mana and either gaining or removing my opponents tempo? Or do I wait for a
  better target, sacrificing my own tempo, but potentially getting better value later on" — so the AI
  "doesn't just play out its spells ASAP, but also … doesn't just hold onto them for the whole game".
  Agreed design (optimal stopping + real options): per card, its **gain-when-castable distribution**
  from self-play (one offer a turn: the best gain it could make that turn); a held instant-speed card
  is worth **C = the expected best of the next k offers**, k ≈ the turns the game has left (from both
  clocks) — so casting needs a gain ≥ C, and C falls as the game shortens. It replaces the patience
  cost; instants and flash first, sorcery-speed removal later.

- 2026-10-29, Shayne (sprint 3 planning): AI-13.6's position (flag 5) is gone since AI-15 — **hand-build
  one now** (Ajani's Anguish for 3 at the face at 20 life, nothing worth killing), Shayne agreeing "hold"
  before the change; swap in a real one if a replay shows it.

Open questions:
- How a rule steers: a score adjustment the search adds, a move left out of the search (pruning),
  or an ordering hint (searched first)? Pruning is the cheapest and the most dangerous — a pruned
  move can never be chosen, even in the exception.
- Where rules live: one file of named rules, each a switch, beside the evaluator?
- Which lesson first — Shayne's pick.

## The lessons ledger

| # | Lesson (Shayne) | General rule (proposed) | Status |
|---|---|---|---|
| 1 | Bounce and removal: wait until they give real value, or use them in desperation (AI-4, Decisions 2026-10-24: his value and desperation cases) | **Patience with instants:** don't cast an instant now if casting it later does as much or more — unless the alternative is losing (desperation) | **built, kept** (AI-13.4: a held instant priced as an option — the expected best of its offers over the turns left; AI-13.2's flat cost replaced) |
| 2 | Unsummon's value cases: a token, an Aura or counters, a creature in a double block, your bomb under removal, an attacker at a planeswalker that matters, a big creature for tempo (AI-4) | **What a bounce costs them:** a bounce is worth what is lost when the creature leaves play — a token is gone, Auras and counters are gone, the mana is paid again, a combat is won — computed for any card | proposed |
| 4 | At #547 (AI-4.2's holding position, 2026-10-27): don't bounce what you can trade with in combat; a recast gives them its "enters" again (Fateshaper returning a creature from their graveyard); a creature already stunned or tapped needs no answer now — "at the very latest wait a turn"; at 5 life "I can still take a hit"; hold for their bad attack, when Unsummon plus a trade wins two for one "when they don't expect it" | Part of lesson 1, sharpened: **an instant cast now must beat holding it** — beat what combat already does (a trade), not hand them an "enters" again, and not answer what can't hurt you this turn | proposed |
| 5 | On AI-10.4's hand-built position (2026-10-27): "ferocity of the hunt is best held until the opponent plays their removal spell first, or if you can trade it with a creature. So the AI shouldn't have been playing this out at first possibility anyways" | Lesson 1 for an Aura or pump (Ferocity has flash): **hold a card that makes one creature better until their removal is spent, or until it wins a combat now** (a trade it makes possible) | not held since AI-13.4 — the risk of their unseen removal is AI-9.4's |
| 3 | Hard removal: a 1-for-1 — weigh the threat against tempo and against holding for a better target (AI-4) | needs a target's threat over time (AI-12) | waiting on AI-12 |

New lessons go here as Shayne thinks of them — in his words first, the rule after.

## Tasks

- [x] AI-13.1 Shayne picks the first lesson; agree how a rule steers (open questions) — 2026-10-28: patience with instant-speed cards, as a cost on casting early (Decisions)
- [x] AI-13.7 Never pass with a land to play (Shayne's playtest, 2026-11-08: `2026-11-08_231959_seed347243` #27, the AI's
      first turn, three Forests in hand, it passed and discarded to hand size - "pretty much a 0% chance this was
      correct"). Pass -15.78 against Play Forest -21.63: the discarded Photosynthetic Physician returned from the graveyard
      as an Officer by turn 4, inside the horizon, and the land's mana only shows past it. `Evaluator.Terms.LandDrop`
      (`+nolanddrop`): `BeamSearchAi.Children` drops Pass while a land play is legal. Kept `AiSkipsLandDrop`, test
      `WithALandToPlay_TheSearchPlaysIt_NotPass`, control included. Suite 19 changed (was 17), cost per position
      -10% (8 024 steps), oracle 84.7% unchanged; 1529 tests. Match not run. Not yet committed
- [ ] AI-13.10 Option pricing step 1 (Shayne, 2026-11-09, after Claude's three-step proposal: "lets begin with step 1
      and leave 2 and 3 for later"; steps 2 desperation and 3 our deck are later): every card with a target held as an
      option, not only instants. **TRIED AND REVERTED (2026-11-09, patch kept in the session scratchpad):**
      `CastGains.IsOption` (instant speed, or a spell's/mode's target, an Aura, an enters-trigger target), offers in a
      sorcery-speed card's own turns only, `Terms.TargetedOptions` (`+instantoptions`); tables regenerated (FRA 35 -> 60
      cards, 186 s; TRK 21 -> 58, 249 s; TRK's 21 instants identical, so the measurement itself held). Suite, against
      24 changed / 85.1% / 0.94 given up: all targeted cards 36 / 80.2% / 1.54, and the AI PASSES where it acted (Jace's
      -3, Way of the Wildspeaker's control); spells only (no permanents) 32 / 81.6% / 1.22, Wildspeaker still fails.
      **Why (Claude):** holding an instant costs nothing - it is cast in their turn - but holding a sorcery-speed card
      costs a turn of tempo, a creature most; measured offers do not charge it, so held it is priced at the best of its
      next few chances and the AI waits for a top-quartile turn instead of developing (Shayne's B4: "a clunky hand that
      can very easily lose to tempo"). **Found beside it: FRA's instant table is stale** - regenerated on today's AI
      alone (step 1 off) the suite moves 24 -> 30, 83.0%, 1.30 given up, and two agreed positions break
      (`AJaceToken_DrawsWithItsMinusThree…`, `Surveil_BinsTheCheapSpell…(False)`). Next is Shayne's call: a tempo cost on
      holding (before step 1 is retried), and/or why the fresh FRA table breaks those two
      **Looked into (Shayne: "what doesn't make sense is not using jace and not casting wildspeaker. Those kind of have
      fixed values"):** neither is about their value. *Wildspeaker:* the real AI still casts it; only the test's
      CONTROL broke - it expects the pump instead, and Tam's Resistance is a targeted SORCERY, now an option and held.
      *Jace -3:* the position (suite White-Black vs Jace seed 1 #108, the AI at 4) is LOST in every line the search
      imagines (all ~ -8 590, the same death on turn 19); -3 against Pass is a tie-break between dead lines, and Pass
      keeps 3 loyalty that the walker's lifetime price counts as a future draw. On the committed code the -3 won only
      through the stale FRA instant table: with no option prices at all (`+nooption`) the committed AI passes too.
      So the defect is future value counted when there is no future - Jace's banked loyalty, a held option - which is
      step 2 (desperation): cap those at the turns we have left
      **Then measured (2026-11-09, Shayne: "the AI should recognize the value of getting a free draw automatically, and
      not keep it for later because the card we are drawing off of it is not what we want"):** in a CALM hand-built
      position (a Jace token at 6, empty hands, life 20 each) the committed AI draws in 24 of 30 shuffles, surveils
      with -1 in 6, never passes - it already values the free draw. Two pricing changes were tried and reverted: a card
      drawn in the search priced as our library's average (13 of 30 draws, 13 passes - worse: the walker's future draws
      stay priced as a generic card, so banking loyalty looked dearer) and a walker's lifetime capped at the turns we
      survive (suite position 13 -> 19 of 30, no fix). A "never skip a free draw" move filter was started and removed -
      Shayne: "I never meant to add a never skip a free draw rule". So the suite position's Pass is the LOST position's
      noise (every line ~ -8 595, 0.3 apart), not a mispriced draw; `AJaceToken_DrawsWithItsMinusThree…` pins a choice
      among dead lines and needs a position that is not lost
- [ ] AI-13.9 The option value for STAR TREK's instants (found 2026-11-09, Shayne asking whether "spells are options" is
      done): `CastGains` is FRA's alone (`FRA.castgains.json`, 35 cards, no TRK name; `--cast-gains` plays FRA's sealed
      decks), so in every TRK game a held instant or flash card falls back to its flat held price - `HoldOption` does
      nothing there. Needs TRK self-play decks for `--cast-gains` (`--match` has none either)
      **BUILT (2026-11-09):** `CastGains` per set (`TableSets` FRA, TRK; `TRK.castgains.json`, merged by name as
      `CardValues`); `--cast-gains [--set=TRK]` (every set without it) with a progress line. TRK: 400 games of its
      sealed decks, 21 cards, 108 s - the first run hung on four Tribbles games (AI-18.2 groups the AI's moves; a game
      also stops past 60 permanents). FRA's table unchanged. **What the table exposed:** a held option's turns left
      were read in EVERY line the search compared, so a line that shortened the game also marked down its attacker's
      held instants - with TRK's offers, the imagined Shayne at 4 life held his hasty 2/1 back and the AI raced into
      the drain again (`AtFourLife_AgainstAVisibleDrain…` failed). `Evaluator.Terms.RootOptionTurns`
      (`+liveoptionturns`): turns left read once, at the search's root (`WithRootImpulse`); the bar still falls decision
      by decision, as agreed. The drain test gains it as a second control. Suite: 24 changed (21 without it): Koth held
      back from an attack (seed 1 #60), a spell before the land (seed 3 #56), Prophesied End on Koth (Jace seed 3 #71),
      Break Under Pressure moved from Jace seed 3 #85 to seed 4 #93; oracle 85.1% (85.4%). **Match (2026-11-09):
      `beam` vs `beam+liveoptionturns` 51.0% ± 3.2pp over 96 (49-47), no difference - kept.** Suite not yet accepted
- [ ] AI-13.8 The option value for a flash creature's protective enters (Shayne's playtest, 2026-11-08:
      `2026-11-08_232615_seed2123881407` #145 and `232136` #216): Saru, Unthreatened cast in the MAIN phase - "almost
      never a reason, it has flash, and bricking an opponent's removal by giving one of yours hexproof is very valuable".
      Cast and Pass score the same (-5.36 each; an acting move wins a tie): the rollout never sees the removal Saru would
      have answered. Offers for a protective flash enters (hexproof, protection), priced as an option when held (AI-13.4's
      way). Position kept: `SaruMainPhase`
- [ ] AI-13.6 The option value for sorcery-speed removal and burn, X spells included (AI-14's flag 5,
      Shayne: Ajani's Anguish for 3 at the face, No Admittance next in line — hold both: "If you draw
      lands the anguish gets better anyways, and there is always a chance the opponent plays some
      creature you need to kill"): offers measured for them too (an offer is already the best X of
      the turn), priced as an option when held. Position: `MagicConsole --decisions` game 1 seed 1,
      turn 16
      **Hand-built and checked 2026-11-09:** four Mountains, Ajani's Anguish in hand, they at 20 with no creatures -
      held in 30 of 30 shuffles, by its measured price (flat hand prices cast it in 20 of 30): test
      `BasicsTests.AjanisAnguish_WithNothingToKill_IsHeld_AtTwenty`, control included. Shayne to confirm the position is
      his flag-5 answer. **But at THEIR low life it goes at the face:** they at 6, 5 or 4, the AI casts it for 3 in 20
      of 30 shuffles (none lethal; at 8 it holds) - the symmetric low-life price makes chip damage look good, the risk
      noted when the burn rule was deferred (AI-17.6, 2026-10-30). That is Shayne's burn rule (player-only, repeatable,
      or enough to burn them out) - to build when he says
- [ ] AI-13.5 The option value for a creature whose enters ability needs a target (AI-14's first
      reading, Shayne: Yoshimaru, Scrappy Stray cast with no other creature, its fight wasted — "costs
      you a potential fight later on"): offers measured for such creatures too, cast when now beats
      the expected best of later. Position: game 1 seed 1 of `MagicConsole --decisions`, turn 4
      **Checked 2026-11-09: the position now HOLDS** (`Records/YoshimaruWithNoFight.json` #70: Pass 9.52, cast 7.37;
      0 of 30 shuffles cast it). No switch brings the cast back - only flat hand prices (`WithoutHeld`): since AI-17.6's
      Settle fights an "up to one" fight, the card-value table prices Yoshimaru 16.5 (was 4.5), so held it is a creature
      that will fight, and a cast that can't scores below it. Card-agnostic - any creature whose enters needs a target
      gets it from its measured price. Test `AnEntersFight_WithNothingToFight_IsHeld` (control: flat prices cast). NOT
      built: the offers version (cast when now beats the expected best of later) - the held price is fixed, so it never
      falls as the game shortens; whether that is enough is Shayne's call
- [x] AI-13.4 The option value, replacing the patience cost (Shayne, 2026-10-28 — Decisions):
      `CastGains` (`MagicConsole --cast-gains`: 400 self-play games of the AI's sealed decks, each
      instant or flash card's offer a turn, game-ending casts excluded, twenty mid-slice quantiles —
      `MagicCore/Sets/FRA.castgains.json`); `Evaluator.Terms.HoldOption` prices our held one at
      `CastGains.ExpectedBest` of `OptionTurns` (the sooner clock, 1 to `MaxOptionTurns` = 4); a known
      creature in their hand at its board value less `RecastPerMana` (0.5) — the bounce's value was the
      real error (Shayne picked fixing it: "a plain 3/3" was a top-10% use of Unsummon); the patience
      cost removed. Positions (`Unsummon_EarnsItsCard_OnAValueTarget`, `…_IsHeld_AgainstAPlainAttacker`
      with its control, #547, the known-removal Aura): all right. Ferocity is not held (a later risk);
      its position moves to AI-9.4. Match: **51.7% ± 1.4pp, 600 games**, worse rejected, +1.6% cost.
      Review fixes: offers measured with the option OFF (it was measuring its own last table), each
      game's last turn kept, the known-creature price under AI-9.5's `KnownHand`. **Then the honest
      finding: every position passes with the option forced off** — the bounce's value holds them
      (the plain-attacker control is `KnownHand` off). The option is kept as Shayne's design (22 of 288
      suite decisions move; `CastGainsTests` pins its arithmetic) but unproven on a position yet —
      prove it on AI-13.6 (Ajani's Anguish) and Ferocity (AI-9.4)
- [x] AI-13.3 Check lessons 2 and 4b before building (Shayne, 2026-10-28): his bounce value cases and
      an enters-creature, hand-built — today's AI already plays them (the enters ability is counted: a
      vanilla 3/4 of the same cost it bounces, Fateshaper it holds, a 7.7-point swing); but it bounces
      plain attackers at 20 life too — moved to AI-9.2 with these positions as its tests
- [x] AI-13.2 The first rule behind a switch, its positions (AI-4.2), measured; kept or dropped in
      the ledger — **patience, kept** (2026-10-28): `Evaluator.Terms.Patience` — an instant or flash
      cast at sorcery timing costs `Evaluator.PatienceCost` (1.0), charged by the search's tree on
      every such cast in a line (`BeamSearchAi.Impatience`) and by our rollout policy
      (`Rollout.Impatience`), none on a line that ends the game. Tried first as a hold value on the
      card in hand (Shayne's pick) — it did nothing: the best line after "Pass" cast the flash card
      later that turn; he chose the cost on the act. Charged in the tree only, it was a one-turn delay
      (the review) — so the rollout waits too. Positions: `AFlashAura_IsHeld_NotCastAtTheFirstChance`
      (Ferocity, Last Gasp unseen: a tie that cast, now held; control without patience) and
      `Unsummon_IsHeld_AtMove547` (a guard; their turn, so patience doesn't touch it). Suite: 4
      decisions differ from no patience. Match: **53.0% ± 1.0pp, 600 games (318–282)**, undecided at
      5pp, "worse" rejected, cost the same — kept (Shayne's rule: sensible plays count; the agreed
      position fixed). `docs/findings/magic-ai.md`
## Next

Lesson 1 is built as an option value (AI-13.4) — but no agreed position depends on it yet: the bounce
positions are held by pricing a bounced creature as itself (AI-9.5's `KnownHand`). **Next: AI-13.6**
(sorcery-speed removal and burn as options — Ajani's Anguish for 3 at the face, game 1 seed 1 turn 16
of `MagicConsole --decisions`) — the first position the option value itself should fix; then AI-13.5
(creatures whose enters needs a target — Yoshimaru, turn 4). Lesson 5 (Ferocity) needs AI-9.4's risk of
the unseen hand; lesson 3 waits on AI-12. The constants (`MaxOptionTurns` 4, `RecastPerMana` 0.5) were
chosen, not measured. After any evaluator change, rerun `--cast-gains` — **due now**: AI-15 changed the look-ahead's blocks
(2026-10-28) after the table was measured.

## Log

- 2026-10-24: created — Shayne's idea; machine learning for the AI noted as a later option in
  `DesignNotes.md` ("MagicCore AI: machine learning, later").
- 2026-10-27: lesson 4 from Shayne's reasons at #547 (AI-4.2's first agreed holding position).
- 2026-10-27: lesson 5 from AI-10.4's position (the AI casts Ferocity of the Hunt at the first chance).
- 2026-10-28: AI-13.1 interviewed, AI-13.2 built and kept — patience with instant-speed cards
  (53.0% ± 1.0pp over 600 games, worse rejected; the agreed position held). Status → in-progress.
- 2026-10-28: AI-13.3 — lessons 2 and 4b checked: played right, but for the damage; AI-9.2 next.
- 2026-10-28: AI-13.4 — the patience cost replaced by an option value (offers from self-play, the
  expected best over the turns left) and a bounced creature priced as itself less its recast; 51.7%
  over 600 games, worse rejected; every agreed bounce position right, Ferocity to AI-9.4.
