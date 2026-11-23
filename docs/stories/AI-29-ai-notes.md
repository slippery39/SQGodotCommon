---
id: AI-29
title: AI notes — specific evaluators for troublesome effects, called by the general one
status: backlog
area: AI
created: 2026-11-18
---

## Why

Shayne, 2026-11-18 (during AI-27): "making a completely generic AI that plays everything correctly without any
additional input from us would be a dream, but obviously the amount of complexity and edge cases in magic makes this
quite complicated. It seems to be quite hard to find a single evaluator system that can work for every single card in
the game. I'm wondering if maybe, even though we have stated the opposite up until this point, it would be worth it to
add 'AI Notes' for cards or abilities that are troublesome … And i'm saying notes, but in reality these could just be
like evaluator functions used for specific effects or cards that we write … Have a general evaluator, and then specific
eveluators that can be called by the general evaluator … So we would basically be doing our general eveluator, + a forge
style how to play certain cards but not necessarily on every single card."

His examples, in his words (2026-11-18):
- **Flash:** "prefer playing at end of opponents turn OR if extra value would be gained by playing it before then (like
  being able to block and kill a high value creature, or being able to block a creature for free, or in the case of
  dvine duelist being able to tap a creature to prevent some damage etc..)"
- **Looting:** "we could provide a procedural evaluator for determining which cards to discard (i.e. excess lands,
  spells we can't currently cast, low value spells etc..) instead of the AI lookahead trying to determine that all by
  itself."
- **Bounce (Unsummon):** "evaluate the baseline of the card, and say only use this in non desperation, if you will get
  more than the baseline value of the card, or it will help you win the game quickly etc.."
- **Yoshimaru, Scrappy Stray** ("When Yoshimaru enters, another target creature you control fights up to one target
  creature an opponent controls"): edge it toward casting with a creature that fights something.
"Now it sounds easy because Im saying this all in english, so the implementation is something we would need to figure
out."

A candidate from the same session (AI-9.10, 2026-11-18): land sequencing - Konstrari Annex ("enters tapped unless you
control a planeswalker") played while a Jace token makes it enter untapped, before Rockfall Vale ("unless you control
two or more other lands"), whose condition holds from then on anyway. The general evaluator can't see it: the look-ahead
plays the Annex tapped inside its own turns, where its one spell a turn fits the mana left.
Shayne (2026-11-18): "do we need to look ahead for this? Is there no way to determine which lands would come into play
untapped in the current situation vs next turn and sequence the lands that way? Like the lookahead seems to be causing
too much issues, even though in theory it should help."

What pointed here (AI-27): seed 2 #16 — Divining Duelist cast in our main phase to loot away our next land drop, where
Mindseeker Oculus now and Duelist held for their turn was the play; the look-ahead never plays our cards in their turn.

Claude's proposed shape (2026-11-18, for him to correct): notes attach to an EFFECT or mechanic (flash, "enters,
fight", loot, bounce), not a card name — one note covers every card using that piece (MC-6's compose rule); a card-name
note only for a truly unique card. Three kinds: when to play it, how to answer its choice, what holding it is worth.
They live in the AI code, never on the cards (the rules engine stays AI-free, game state serializable). A note tilts a
price rather than forbidding a move, so the search can still override it. Each is measured like any term: a position
from his table notes as its test, an off switch, the suite and a match. The rules already scattered through the AI are
notes in all but name (burn waits for a kill, never pass with a land drop, surveil lands binned by rule, Nyota's number,
Kai Winn's name, tap abilities at their last chance, waiting wins a tie) and can move into the same home.

## Requirements

**Notes in general (AI-29.1), the shape - Claude's proposal, Shayne 2026-11-19: "Great".**
- A note is AI code about one MECHANIC (flash, loot, bounce, "enters, fight"), matched by what the card has (the Flash
  keyword, a discard effect) - every card using it gets the note; a card-name note only for a truly unique card.
- Three kinds, each called at ONE place, every note asked in turn: **when to play it** (the search's move list, where
  `LandOrder`, `SaveBurn`, `LandDrop`, `StationCap` sit), **how to answer its choice** (before the search branches on it;
  `SurveilLands` is one), **what holding it is worth** (the held price; `HoldOption` / `OptionPrice` are).
- Two strengths: a note DECIDES (drops the other moves - saves search, as the land rule did: 1 s, not 20-34 s) or
  TILTS (adds to or takes from a move's score - the search can still overrule it). Tilt by default; decide only where
  the answer is clear without looking ahead (lands; loot away the extra land).
- Home: `MagicCore/Ai/Notes/`, a file per mechanic; each its own switch in `Evaluator.Terms` (`beam+no<note>`), a test
  from a position, the suite and a match. In the AI only - the rules engine stays AI-free, game state serializable.
- The rules already in the AI move into that home when next touched - not one big move.

**The flash pilot's tests (AI-29.2), Shayne 2026-11-19, in his words** - "a nice basic test suite, if anything fails
here, it is likely that more complicated examples would fail as well". A Flash 3/3 creature:
1. "Playing the 3/3 flash creature main phase for no extra value - should never happen"
2. "Waiting and playing the 3/3 flash creature at opponents end step - ideal."
3. The opponent has a 2/2: "Opponent attacks with 2/2, instead of playing the 3/3 at end step, you play it before
   blocks, block the 2/2 and gain extra value."
4. "You have some sort of creature in play already, that draws you a card whenever a creature of yours comes into
   play, and you have 6 mana - play the 3/3 flash" (in the main phase).
5. "You have that same creature in play, but you only have 3 mana untapped, have already played your land, and have
   no other plays - wait until opponents end of turn."
Tests 4 and 5 differ in one thing (Shayne, 2026-11-19): "One has no mana left over, so (in theory) you won't be able to
play anything extra that turn anyways so there is no use to main phasing it. The other has [mana] left over, so in
theory you could draw something else and play that. That's the whole difference." So the main-phase value of the draw
is mana left over to cast what it draws. How little leftover still counts (1 or 2 mana) is for a position to show.
"We can think of more." Written up front, before the note, each with the search today as its control where it fails;
then the note; then seed 2 #16 (Divining Duelist looting away our next land drop in our main phase).

**Land sequencing (AI-29.3), agreed 2026-11-19.** On a land turn the AI plays the land a rule picks, from what it
holds and its deck, no look-ahead; the search keeps everything else (when to play it, what to cast). The rule compares
the lands in hand, first difference wins:
1. the mana the hand can spend THIS turn (entering as it would: tapped adds none; an X spell for X 1 or more,
   spending the rest) - changed from "the spells castable" on 2026-11-19, below;
2. the coloured pips in hand no source in play makes, that it makes;
3. untapped now on a condition that can break (a planeswalker, beholding a Jace) - it goes first, the only move, so
   nothing this turn breaks it;
4. enters tapped now - free when the mana isn't needed;
5. the colours the rest of the deck's spells need, uncovered.

One exception to step 1 (AI-29.8): a land spending less now that casts a spell in hand next turn which the step-1
land casts neither now nor next turn is played instead when that spell's measured value is higher than what the
step-1 land casts now.

Common scenarios first, edge cases after (Shayne). **Curated tests up front** - positions built to be the scenario,
not found in games - each in `MagicCore.Tests/Ai/LandOrderTests.cs`:
- [x] an untapped land on turn 1 to play the one-drop
- [x] a tapped land on turn 1 with no one-drop
- [x] a Plains on turn 1 in a two-colour deck with a WW card in hand
- [x] a Plains on turn 1 in a two-colour deck with WW cards in the deck
- [x] the red source (Rockfall Vale) not discarded beside a Forest
- [x] Rockfall Vale over a Forest on turn 2 with nothing to cast (`ShayneNotesSeed1` #24)
- [x] Konstrari Annex first while a planeswalker makes it untapped (AI-9.10)
- [x] an X spell is no reason for an untapped land on turn 1 (from the suite)
- [x] the untapped land that casts the 5-drop over a tapped one (from the suite)
- [x] an R card and a WW card in hand, two Plains and a Mountain - card value decides (AI-29.8): the Plains when the
      WW card is worth more, the Mountain when the R card is

More scenarios like the last to be thought of (Shayne's todo).

## Decisions

- 2026-11-18, Shayne: noted, not started — "I want to finish what we have in this session first."
- 2026-11-18, Shayne (sprint 5 review): notes can also SAVE search - "this could be a way to both save on performance
  and make the AI play better if done correctly" - so each note is written as a shortcut where it can replace search.
  **Card tags** (his idea: tag a card by what it does - FaceDamage, removal - by hand or by reflection, so the AI looks
  a tag up instead of checking each card; later, search in deck building): built only when the first note or shortcut
  needs one, derived from the card's components by reflection, hand tags only where reflection can't see (Claude's
  recommendation at sprint 6 planning, Shayne agreed the plan).
- 2026-11-19, Shayne (AI-29.3): colours by rule, not look-ahead - "look at our hand / deck, determine color
  requirements, determine which colors we might need this turn / next turn and play out our lands accordingly". "A
  real player wouldn't look 3 turns ahead to realize playing the red green land is better than a forest." Basic
  scenarios before edge cases; curated tests written up front "so that we aren't solely relying on in game scenarios".
  Rockfall Vale tapped on turn 2 is fine: it enters untapped on turn 3.
- 2026-11-19, Shayne (AI-29.8): the R-and-WW case is decided by measured card value, "the value of the R card played
  on turn 1 vs the value of the WW card played on turn 2" - and only that case: an exception to step 1, not a two-turn
  value for every land (Claude's options; he chose the narrow one).

## Tasks

- [x] AI-29.1 The shape agreed: where a note lives, how the general evaluator calls it, its three kinds
- [ ] AI-29.2 The pilot: flash (his rule above) - his five tests first (Requirements), then the note, then seed 2 #16
- [x] AI-29.3 Land sequencing as a simple rule, not the look-ahead (Shayne, 2026-11-18, above): which land to play -
      what enters untapped now and not later, the colours the hand needs - on the Konstrari Annex position (AI-9.10).
      Sprint 6 item 3, before the flash pilot
- [x] AI-29.4 A land whose colour is chosen as it enters (Room of Refuge) is any colour off the battlefield, in
      `ManaAbilities.ColorsOf` - every reader (LandUnlock's discard and surveil, Castability) sees it as no source today;
      LandOrder's own patch goes
- [x] AI-29.5 The rollout plays the land LandOrder picks, not the first in hand - lines are scored with the land
      sequence the AI would really play
- [x] AI-29.6 Step 5 counts the spells we KNOW are on top of the library (a scry or surveil kept them there) - today
      `Hidden.Unseen` leaves them out
- [x] AI-29.7 `HiddenTests`' sample test covers the DEFAULT search too, not only with the land rule off
- [x] AI-29.8 Shayne's open case: the R card now against the WW card next turn, by measured value (Decisions)

## Next

AI-29.2, the flash pilot - a PLANNING session on 2026-11-19, nothing built. Start by writing Shayne's five tests
(Requirements; the Flash 3/3 costs 2, so 6 mana leaves his 4) and running them against the search as it is: which fail says what the note must do. Then the note
(tilt, `Notes/Flash.cs`, its switch), the suite and a match. Shayne's harder land scenarios stay his todo.

## Log

- 2026-11-18: created from Shayne's question during AI-27.
- 2026-11-18: sprint 6 - AI-29.3 (land sequencing) added and put first; card tags recorded as a decision.
- 2026-11-19: AI-29.3's rule agreed with Shayne (Requirements), and his curated scenarios as tests up front.
- 2026-11-19: AI-29.3 built - `LandOrder.cs`, `Terms.LandOrder` (`beam+nolandorder`), in `BeamSearchAi.Children`. The
  seven scenarios written first (`LandOrderTests`): the search before the rule failed FOUR (both Plains cases played the
  Mountain, the Annex lost to the Vale, #24 the Forest) - the first run showed six passing only because a tie took the
  first land in hand; the expected land now goes last, and each of the four has the old search as its control. All
  seven pass; #24 decides in 1 s, not 20-34 s (one land, no search over two). `HiddenTests`' sample test keeps its
  game with the rule off (the land choices it saw differ are the rule's now). Suite: 15 of 288 changed (5.2%), cost
  19028 steps a position; oracle agreement 72.9% (committed 76.0%) - being read.
- 2026-11-19: the 15 read (`--boards`). Right: the Annex played first under a Jace token six times (before a −1 or
  −3), Deserted Beach tapped on turn 1 twice; neutral three. WRONG four, one cause - step 1 counted SPELLS: Ajani's
  Anguish for X=0 made a Mountain "cast" on turn 1 (over the Annex, free tapped; twice), and a 2-drop tied Way of the
  Wildspeaker / a 3-drop, so a tapped Annex wasted a turn's mana (seed 3 #32, seed 4 #13; seed 2 #9 the same).
  Step 1 is now the MANA the hand can spend (X 1 or more, spending the rest); two tests from these. Suite: 10 of 288
  changed (3.5%), all the right or neutral ones; 19102 steps a position; oracle 74.0% (committed 76.0%), score given
  up 0.65 a position.
- 2026-11-19: the 10 read with Shayne and accepted (`FRA.positions.txt`): Deserted Beach tapped on turn 1 right
  (twice); the Annex first under a Jace token - only seed 3 #43 (loyalty 1, its −1 kills it) needed it, five neutral;
  two draw-step instants now cast in main, same play. Ceiling noted: step 3 goes first even when nothing this turn can
  break the condition, so a Jace −3's card is drawn after the land is chosen.
- 2026-11-19: match `beam,beam+nolandorder --until-clear`: 288 games, beam 50.7% ± 1.3pp (146-142) - early stop, NO
  difference of 5pp either way; cost per decision 10225 steps vs 10645 (4% less, one land searched instead of
  several); lands played 7.5 vs 7.4 a game, spells 9.6 both. 114 min on a busy machine (cost-deterministic: only
  slower). A flat match is the expected result for a rule that fixes occasional land plays.
- 2026-11-19: pre-commit review fixes - a land that enters tapped and asks a choice as it enters (Room of Refuge's
  colour) read as untapped, and its colour unknown in hand made a Plains win step 2; now tapped, and any colour in
  hand (`TurnOne_ATappedLand_ThatAsksAColourAsItEnters`, failed before). A land making no mana no longer counts as
  a source. Suite: 0 changed.
- 2026-11-19: AI-29.3 committed (31929632). The review's four leftovers made AI-29.4-29.7, to fix now (Shayne).
- 2026-11-19: AI-29.4-29.7 done, each with a test that fails without it. 29.4: `ManaAbilities.ColorsOf` reads a
  colour chosen as it enters as any colour OFF the battlefield (LandUnlock's discard and surveil, LandOrder - its own
  patch gone); `Hidden`'s guess of the opponent's colours ignores a land of more than two, as `OptionPrice` does.
  29.5: the rollout plays `LandOrder`'s land, under `Terms.LandOrder`. 29.6: the rule's deck is the whole library,
  the known top included. 29.7: the sample test runs the default search, on seed 2 (seed 3's difference was land
  choices). Suite: 2 of 288 changed (seed 2 #21 Koth cast before combat instead of after, no attack either way;
  seed 4 #13 an Island instead of a Plains, Repurposed Enforcer cast either way); 19094 steps a position. Both accepted (Shayne); match beam vs beam+nolandorder flat: 144-144, 50.0% ± 1.3pp, no 5pp difference either way; 10346 vs 10588 steps a decision (2% cheaper), lands 7.5 vs 7.4.
- 2026-11-19: AI-29.4-29.7 committed (f073adb3).
- 2026-11-19: AI-29.8 built. Probe first: in Shayne's position the search played the Mountain with the rule and
  without it, the R card a 1/1, 3/3 or 5/5 - the search would not have decided it either. `LandOrder.Pick` takes the
  held values (search and rollout alike); next turn's land is read from the hand, not the moves (the search offers one
  move for two Plains - the first version missed exactly that). Two tests, one each way, values given by the test;
  unmeasured cards tie at the flat price and keep step 1. Suite: 0 of 288 changed - no match run (nothing to measure).
- 2026-11-19: AI-29.8 committed (c00328c4). AI-29.1 started.
- 2026-11-19: AI-29.1 - the shape proposed and agreed ("Great"); Shayne's five flash tests recorded for AI-29.2. Planning only.
