---
id: AI-20
title: The look-ahead stops betting on one guessed future — unknown cards handled honestly
status: in-progress
area: AI
created: 2026-11-10
---

## Why

Shayne, 2026-11-10, after AI-17.24 (the landcycle skipped again): "I'm wondering if our lookahead should not count
unknown cards? This seems to be the main culprit for all these weird issues that are intermittently coming up. I.e.
Tasha, Land Cyclers, weird times to draw cards vs not." He asked for it to be the most urgent AI story: how many other
issues go away if the root cause is fixed?

**How the search works today.** Before each decision the search takes ONE guess at everything it can't see
(`Hidden.Sample`, AI-3): the order of both libraries (`ShuffleUnknown`, the default since AI-3.9) and the
opponent's unseen hand. It then plays the turn out and a rollout two turns past it (`Rollout.Horizon`) in that one
guessed world, draws included, and scores the end. Every move is compared inside the same guess. A different guess
can give a different decision.

**The symptom family — the same root each time, a separate patch each time:**

| Where | What the one guess did | The patch |
|---|---|---|
| AI-17.24, `Records/RogueLandcycleSkipped` #157 | the Pass line's guess drew an Island on turn 10, so keeping the 6-drop looked free; it passed with a landcycler on 3 lands, then cycled in Shayne's upkeep. Over 30 guesses: passes in 7 | none yet |
| AI-17.21, `ALandcycler_OnTwoLands…` | a Borg on 2 lands kept over a land | `Terms.ManaShort` (a discount), 18 → 7 of 30 passes |
| AI-17.21, `TheTashaAttack_DoesNotHangOnTheSampledTopCard` | one sampled land at the horizon swung a hand of 4-drops by 11 points | "one short is free" in `ManaShort` |
| AI-17.20, AI-17.11 | a surveil never binned: the rollout keeps the one guessed top card, so binning is never seen to gain | `Terms.SurveilLands`, `SurveilSpells`, `SurveilPrice` (rules beside the search) |
| AI-17.16 | "exile the top card, you may play it" judged by the guessed card ("it shouldn't matter what the exact top card is") | `Terms.ImpulseValue` |
| AI-13.10, AI-17.23 | the Jace −3 against Pass decided by the guessed draw (a Plains) | moved to a calm position |
| AI-19.23's scan, `Records/LintTrkSeed1` #48 | a basic landcycling already paid, its search answered "none" — fetching a land reorders the guessed library, so every imagined draw after it changes. Over 30 guesses (`--record=FILE --guesses=30 --at=48`): Plains 14, none 11, Island 5 | none yet |

Several tests already have to count a decision over 30 guesses, because one guess decides by luck
(`AjanisAnguish_WithNothingToKill_IsHeld`, the Tasha test, the Jace test, `ASurveil_KeepsAGoodSpellOnTop`).

**What has been measured (`docs/findings/magic-ai.md`, "AI-3's matrix"):** seeing the REAL library order is worth
about 5pp (`beam+realorder`), but it is cheating and Shayne chose the honest default (AI-3.9). The opponent's real
hand, its casts and its answers were worth nothing measurable. So what the look-ahead knows about hidden cards
matters, and how it guesses them is the open question.

## The approaches and their trade-offs

**A. Known information only (Shayne's first idea).** The look-ahead plays only with what is on the board and in
known hands. A card drawn in the look-ahead is not a real card from the library: it is an "unknown card", worth the
average card (`HeldValues.Unknown`, as the opponent's unseen hand is priced already), and can't be cast inside the
look-ahead.
- **Gains:** no luck. The same position gives the same decision, every time. The whole family above goes, and with it
  probably most of the patches (surveil, impulse, the Tasha "one short is free" rule). It is cheaper (no sampling).
  The AI's plays become explainable from what is on the table.
- **Costs:** the future has no draws in it. The AI never expects to draw a land, so it would undervalue a hand that
  is one land short (the Borg at 4 lands, which Shayne said is fine to hold), and overvalue fetching lands. A
  drawn card can't be cast, so "draw a card" is worth only an average card, never the play it enables. The
  opponent never draws a threat, so their future is understated. Surveil, scry and "look at the top" need rules
  of their own (some exist). And the 5pp the real order gave says future cards do matter.
- **Variant A+ (an expected draw):** a drawn card is "an unknown card", but with its expected make-up: a land with
  the chance of the lands left in the library, counted toward mana (for `ManaShort`, `LandUnlock`). This answers the
  land-draw cost above without guessing which card. It does not let the AI cast what it draws.

**B. A fixed library (Shayne's second idea).** The look-ahead uses one fixed order. If it is the REAL order, the AI
sees its own draws: that is `beam+realorder`, stronger by 5pp but cheating, and set aside at AI-3.9. If it is one
guessed order fixed for the whole game, the AI is at least consistent from one decision to the next (no "pass on
turn 8, cycle in the upkeep" flip-flop).
- **Gains:** consistency. Simple.
- **Costs:** a fixed guess is still one guess. In AI-17.24 the bad guess would just stay bad for the whole game.
  The real order is the cheat Shayne rejected. This does not fix the root cause.

**C. Several guesses, averaged.** Decide over N guessed worlds (say 4 to 8) and play the move that is best on
average. This is the textbook answer in card-game AI (Perfect Information Monte Carlo).
- **Gains:** keeps the rich future (real-looking draws, the land curve, casting what it draws) while averaging out
  the luck. In AI-17.24, 23 of 30 guesses cycle, so the average would cycle.
- **Costs:** N times the work for each decision, or a narrower beam to pay for it. The table's ~1 s cap gets
  tight. A known flaw: in each world the AI "knows" its next draw, so draw timing and surveil can still be judged as
  if it knew. It reduces the noise but doesn't remove it. It averages the luck away instead of not betting on it.

**D. Mixed: known cards in our turn, average draws after.** Our own turn is played with what we know (A). Draws
after it are unknown cards with an expected make-up (A+), maybe with a few guesses only for what the opponent holds.
- **Gains:** most of A's honesty, with a future that still has lands in it.
- **Costs:** the most design. Two kinds of "unknown" to keep consistent.

**Claude's lean (2026-11-10, for Shayne to decide):** A+ first. It is Shayne's idea with the one gap that matters
most fixed (expected lands), and it is the one that removes the root cause rather than averaging it. Then measure:
if the match says the AI got weaker (the 5pp warns that future cards matter), C is the fallback, and it can be
built on top.

## Requirements

- [ ] One measure of the problem before any change: on a fixed set of positions (the suite's 288 plus the kept
      records above), how often the decision changes between guesses (the "flip rate", over N guesses each).
      Every approach is judged against it.
- [x] The chosen approach is a switch (a `beam+…` name), so it can be matched against today's search: `beam+landornot`.
- [ ] The kept positions above decide right with it, each with its control.
- [ ] Patches made redundant are tried off one at a time and dropped only when measured redundant.

## Decisions

- 2026-11-10, Shayne: this is its own story and the most urgent AI story. It starts with the discussion and
  trade-offs above.
- 2026-11-10, Shayne: the principle - **known information is used in the look-ahead, unknown information stays
  unknown**: "any known information should be allowed to be used in the lookahead, any unknown information is
  unknown. Thats the way I would try it." So a card already seen on top (a surveil's keep, a "look at the top") is
  drawn and may be played as itself; a card nobody has seen is an unknown card. This is approach A's family.
- 2026-11-10, Shayne: **land odds, in principle (A+)** - "Land odds is fine, gives us a % chance of hitting lands for
  certain turns, then the AI can make decisions based on that." The details are to be discussed when the story
  starts: "I think we should discuss that more when we start the story."
- 2026-11-11, Shayne (Claude's proposal): **AI-20 goes right after the misplay scan (AI-19), before the scan's value
  fixes** — the scan decides each suspect under 30 guesses (AI-20.2's flip rate, built there once) and the NOISE ones
  become this story's positions; the value fixes are then measured once, on the look-ahead kept. Shayne's note that
  started it: an AI opponent skipped its land drop and landcycled in his upkeep, "for no real reason" — one guess
  drew the card it needed, another didn't.
- 2026-11-12, Shayne: **AI-22 (speed) goes first**, limited to what AI-20 won't rewrite; AI-20 then picks the
  search's shape (several guesses multiply its cost). Research first (sprint 5): Forge's AI and the published work on
  hidden information in card-game search (determinization ensembles, information-set MCTS) — a findings note.
- 2026-11-14, Shayne (AI-20.1): **the AI counts its own deck list** - the lands left over the cards left is known
  information, so its land odds are exact ("That would be fine yeah").
- 2026-11-14, Shayne (AI-20.1): **no fixed "in time" window for a card short on lands** - whether to hold the 6/5
  landcycler on 4 lands or cycle it on 3 is "really a context dependant situation, it depends on what the opponent is
  doing, it depends on what other cards are in your hand. There is no one size fits all here." (Claude's reading: this
  argues against a price formula with a turn window, and for the look-ahead playing the land / no-land futures out on
  the real position.)
- 2026-11-14, Shayne (AI-20.1): **the AI's own unknown draws are played both ways - a land, or not a land - and
  averaged by the odds**, each on the real board and hand; a non-land draw is an unknown card it can't cast. "Thats
  actually how I would be thinking during a regular game, like what happens if i draw a land vs not. So we can try that
  here." To try, not final: #157 and its control judge it. His note on cost: decide once and commit, acting as late as
  the board score allows, rather than deciding again at every priority stop (today: `CommitPlans` within a turn,
  `WaitOnTies` for instant-speed moves; a plan does not carry into the opponent's turn).
- 2026-11-14, Shayne (AI-20.1): **the opponent's unknown hand is NOT changed in this story** - "Let's hold off on the
  stand in opponent. This is really another options pricing problem that we have never fully solved yet." Claude had
  proposed the look-ahead's opponent casting stand-in "typical creatures" from its unseen hand (against several guessed
  hands averaged, or a price). His reading of #213: "I am thinking about the value gained by playing it now vs the
  value gained by saving it for later. In this case the value gained by killing the 1/1 is almost nothing. The 1/1 cant
  block any of my creatures effectively anyways and I would be better off spending my mana adding to my board or
  saving it for a better target." So #213 goes back to AI-13 (option pricing) and is not one of this story's
  positions; the look-ahead's opponent stays as it is (casts only cards we know).
- 2026-11-14, Shayne (AI-20.1): **three separate problems, this story is only the first.** (1) The look-ahead making
  up its own draws - #157 (the landcycle) and #247 (the surveil): this story, worked first. (2) #24, a Forest played
  over Rockfall Vale "even though it is strictly better to do so" - a land valuation problem of its own, not unknown
  cards (Claude had filed it as a gap needing draw odds; wrong). (3) #213 - "just a bad evaluation, because it does
  not account for the potential value of the card (options pricing)", AI-13. (2) and (3) come after this story.
- 2026-11-14, Shayne (AI-20.4, after the match: 54.2% ± 1.5pp over 576 games at 2.17× the cost a decision): **the
  land / not-a-land look-ahead is the default.** "I mean its clearly better so yes it should be the default. We will
  have to see if there are any optimization techniques we can use later on. But its clear to me that this is the way
  to go." The cost is for later, not a condition.
- 2026-11-15, Shayne (on AI-20.6): **a surveil over a top card we already know and left there is worth nothing** -
  "when I surveil and know the card on top (and leave it there) i'm not really incenticized to surveil again, as I
  won't get any value out of it. In this case, exiling a card is 100% the right play for at least the potential value
  of denying your opponent the potential use out of a card in their graveyard (from like gravedigger effects or
  something)." (Claude: AI-20.6 was about an UNSEEN top card, where he said surveil is right, 2026-11-08. His case is
  not built: `Evaluator.PendingSurveil` pays a third of a card for every surveil trigger, known top card or not -
  no position kept for it yet.)
- 2026-11-15, Shayne (AI-20.7, after playing the Patrol position on the table): **there is no decision there - the
  attack is dead on board.** "I attacked with the 2/1 and activated the land … there was also a beam me up in the
  graveyard which the player would win if he had any additional creature that cost 2 or less … an exiled land from a
  card played from the previous turn that could be played, so essentially, the AI was Dead on board from its attack.
  So there isn't really a decision here. The AI should have held its 2/2 back. He is likely losing this game anyways,
  but if he attacks he is dead on board. If he doesn't there is a very small chance that maybe he can still pull it
  out." So Claude's question (how to weigh a 50% win against a 50% loss) was the wrong one: it took the imagined
  lines at their word. The imagined Shayne spends his turn-13 mana recasting Boarding Action from his graveyard
  and drains only on turn 15 (`Rollout.OpponentStep` asks `TheirPlay` before `TheirAbility`), so it never sees
  2 from the 2/1 plus 2 from the land = the AI's 4 life that turn. The test stays as it is.
- 2026-11-15, Shayne (on the imagined recast of Boarding Action): "I already had a 2/1 in play, im not going to
  beam me up my 2/1 token to just get another 2/1 token. Thats basically me spending 3 mana to discard a card for no
  reason. So the imagined opponent is off for sure. And again, it does'nt need to be perfect but it does need to
  account for on board decisions." Claude took this as the go-ahead for AI-20.8 (asked "shall I build that?" the
  message before). What AI-20.8 does NOT fix: with no ability on its board to use instead, the imagined opponent
  still casts a spell that leaves it no better (the rollout's "no worse" rule) - his recast for nothing.
- 2026-11-15, Shayne (on the ten controls, asked "point them at the old search, or drop them?"): "Do what you
  think is best, im not fully following why all these are needed though". Claude's choice: point them at the old
  search (AI-20.7). His doubt stands - whether each patch is still needed at all is AI-20.5, and worth asking
  whether a lesson test needs a control once the play holds.

Open questions (for Shayne, before `ready`):
- What counts as done: the flip rate down, the kept positions right, and a match not worse? (Proposed by Claude,
  not yet answered.)
- A look at an unseen top card (surveil, #247): played both ways too, and three-way ("a land of the missing colour",
  "another land", "not a land") when a colour is missing? (Proposed, not yet answered.)

## Tasks

- [ ] AI-20.1 Shayne picks the approach and answers the open questions - the principle decided (known used, unknown
      unknown) and land odds in principle; the land-odds details and the opponent's draws open, to discuss at the start
- [x] AI-20.2 The flip rate: `--suite=A --guesses=N` (`PositionSuite.Flips`, with `RunProgress`) decides each suite
      position under N guesses and counts the positions with more than one answer. **The baseline: 69 of 288 (24.0%)
      flip under 10 guesses**, 9.2 min - mostly the Jace token's draw or surveil against a land or spell first. It
      counts first moves, so it is rough (`docs/findings/magic-ai.md`, "The flip rate"). `--explain=N --guess=K` shows
      what one guess imagined
- [x] AI-20.3 The land / not-a-land look-ahead behind a switch, `beam+landornot` (`Hidden.Library.LandOrNot`), a
      trial and not the default. Nothing is shuffled: an unseen card on top of a library is an "Unknown card" (no
      type, never cast or played, held at the average spell's price), and every line of the search is rolled out
      twice from the same position - our unseen draws are lands, or they are not - the two averaged by the land
      odds of our deck list (`Hidden.LandOdds`). Claude's choices in building it, each for Shayne to overrule:
      - **One tree, both rollouts at every line** - not two searches averaged per first move. Two searches would let
        the plan differ by the draw ("pass now; keep the 6/5 if a land comes, cycle late if not"), which is seeing
        the future again; with one tree our decisions this turn are made before the draw is known.
      - A card drawn inside the searched turn (the token's "draw a card") is an unknown card in the tree; the "lands"
        rollout makes it a land, which its cheap policy then plays.
      - **A committed plan is dropped when a card the plan hadn't seen is in our hand** - it was made for a land
        and for none, and now we know which.
      - The land drawn is the commonest land left; all of one rollout's unseen draws go the same way; the
        opponent's unseen top cards are unknown cards too (they were shuffled before; its unknown hand is never
        played either way). Known limits, in `DesignNotes.md` ("MagicCore known ceilings").
      - `--explain` shows both rollouts with their shares; `--ai=beam+landornot` works with `--explain` and
        `--guesses` on a record.
- [x] AI-20.4 Measured (2026-11-14, `docs/findings/magic-ai.md`, "The land / not-a-land look-ahead"): the flip
      rate is **0 of 288**; the suite changes 38 decisions of 288, at +87% engine steps a position; the landcycle
      game has its test (`HiddenTests.TheLandcycler_OnThreeLands_IsCycledAndTheLandPlayed_ThatTurn`: turn 8 ends
      with the land played, under two seeds; the control, today's search, ends it on 3 lands). **The match against
      today's search: 54.2% ± 1.5pp over 576 games, stopped as "better"**, at 2.17× the engine steps a decision.
      **The default since (Shayne, Decisions):** `beam` is this search, the one shuffled guess is `beam+shuffle`
      (`beam+landornot` still names the default); the suite's answers accepted (34 changed, after AI-20.6)
- [ ] AI-20.5 Retire the patches it makes redundant, each measured. The candidates are AI-20.7's ten controls
- [x] AI-20.6 A bug the Dominion Assault Force tests found once it was the default (AFTER the match, so the match
      played with it): a surveil that looked at an unknown card and kept it marked the card known, so its place was
      no longer arranged and the "lands" half drew it as a card that is never a land - the surveil line lost that
      whole half (Surveil then keep 15.53 against exile 21.09). An unknown card looked at is still unknown
      (`Hidden.Places`; `HiddenTests.AnUnknownCardLookedAt_IsStillALandTheOtherWay`). Both Dominion positions surveil
      again; the suite changes 34 decisions where it changed 38; the flip rate is still 0 of 288 (3 seeds)
- [x] AI-20.8 The look-ahead's opponent uses an ability on its board before a spell that does less with the
      mana (`Rollout.AbilityOver`, `Terms.AbilityOverSpell`, `+spellfirst` is the look-ahead before it). It cast any
      spell that left it no worse BEFORE looking at an ability, so Boarding Action's recast took the drain's mana.
      Now the better of the two by its settled result goes first; a land is still played first. Built and checked:
      the Patrol stays home (control: `+spellfirst` attacks, 232.81); the suite changes 0 of 288 decisions (19 883.7
      steps a position, 19 882.5 before). **The match (on Shayne's word, 2026-11-15): 50.0% over 96 games,
      48-48, no difference of 5pp either way** - and 9676.1 engine steps a decision against 9676.0, so in these
      three decks it almost never comes up. Claude's read: keep it (it stops the Patrol misplay at no cost);
      Shayne's call. Not committed.
- [x] AI-20.7 **Twelve lesson tests were red under the new default** (1583 passed). Green now (1595 pass, 0
      fail; Shayne, 2026-11-15: "do what you think is best"): the Patrol fixed (AI-20.8); the ten controls run
      under the search before AI-20.4 (`AiPositionTests.OneGuess` = `beam+shuffle`), where their misplay still
      shows - each PLAY is still tested under the default; `TheOnlySourceOfAColour` runs whole under the old
      search (ColorShort alone no longer keeps the Vale in the default; the block fix does, tested by
      `KeepingACreature…`: the Forest goes). What was red:
      - **One play is back:** `AtFourLife_AgainstAVisibleDrain_ThePatrolStaysHomeToBlock` (`PatrolRacesIntoDrain`
        #287, AI at 4 life, Shayne at 7 with a 2/1 and the land that drains 2). It attacks again: 232.81 against
        −2.19 for staying home. The attack WINS in the half where its next draw is a land (50%: +9035) and LOSES
        in the half where it is not (−8569); a win counts a little more than a loss (it comes a turn sooner), so a
        coin flip for the game reads as +233. **Shayne, 2026-11-15 (Decisions): no coin flip - the attack is dead
        on board next turn** (his 2/1 for 2, then the land's drain for 2). The look-ahead's opponent misses that
        kill: it casts a spell first and has no mana left for the drain. The old search passed this test for the
        wrong reason (its one guess drew no land, so the AI lost on turn 15 anyway). **Fixed by AI-20.8: it stays
        home** (−2.19 against −9499.14 for the attack, lost both ways). The test is still red, at an older
        control: with `RootOptionTurns` off the AI no longer attacks either (the drain first makes the 2/1's attack
        lethal whatever the imagined Shayne holds back for) - one more for the controls below, ten now.
      - **One term alone no longer holds:** `TheOnlySourceOfAColour_IsNotDiscarded` - with `BlocksSeeTricks` off,
        Rockfall Vale is discarded again (the same fact fails `KeepingACreature…`'s control, which expected Koth).
        With every term on the Forest goes, as he wants.
      - **Nine controls no longer show the misplay without their patch** (so in that position the look-ahead no
        longer needs it - AI-20.5's list): Ajani's Anguish held at 20 and at 5 (`SaveBurn` + `Potential`), the
        landcycler on two lands (`ManaShort` + `ColorShort`), `ChangSac305` (`TrueDefender` + `DamagePotential`),
        the surveil keeping Energize (`SurveilSpells`), the won line not burning our own face
        (`ProgressBy.TheirLife`), the Duelist's trade (`Trample`), the Tasha attack (`ImpulseValue`), Unsummon held at
        #547 (board-only + `SettleChoices`). Four of them counted "over 30 shuffles" - there is no shuffle now.

## Next

**Where it stands (2026-11-15, committed as `7b1f1f77` and the commit after it):** the land / not-a-land look-ahead
is the default (`beam`; the one shuffled guess before it is `beam+shuffle`). No position flips with the seed (0 of
288), and it beats the search before it 54.0% ± 1.5pp over 480 games (played again after the surveil fix; 54.2%
before), at 2.20× the engine steps a decision. The look-ahead's opponent uses an ability on its board before a
spell that does less (AI-20.8). 1595 tests pass; the suite's answers are accepted.

**Open, each on Shayne's word - interview first, one topic at a time, nothing built before his go-ahead:**
1. **The Jace token and the surveil.** The Jace deck wins least for the new search (49-111 in the match, 66-126
   the time before) - NOT shown to be the look-ahead's doing: it is the weakest deck with the same search on
   both sides too (11-21 in AI-20.8's match, 32 games). What is known: most of the suite decisions it changed are
   the Jace token used or not against another first move (22 of the 38 before the surveil fix; 34 change now) -
   look with `--suite=beam+shuffle --shown=40` (the changes read the other way round) and `--explain`. And the
   open question on the surveil: `Records/ShayneNotesSeed1.json` #247 still passes
   (−17.99 against −18.09) where he wants the token to surveil for its missing red source - a look at an unseen
   top card played both ways, or three ways ("a land of the missing colour", "another land", "not a land")?
2. **His known-top-card case** (Decisions, 2026-11-15): no surveil bonus when the top card is already known and
   left there (`Evaluator.PendingSurveil` is flat). Needs a position first.
3. **The imagined opponent casts a spell for nothing** when no ability competes (his Boarding Action point:
   "spending 3 mana to discard a card for no reason" - the rollout's "no worse" rule, `Rollout.OurPlay`). "It
   doesn't need to be perfect": only with a position where it costs a decision.
4. **AI-20.5: the ten patches**, each tried off under the default (suite, then a match if the suite moves): the
   controls in `AiPositionTests` / `BasicsTests` marked `OneGuess` name them. Shayne: "im not fully following why
   all these are needed" - a patch the default no longer needs goes, with its control.
5. **The cost**, later ("we will have to see if there are any optimization techniques"): the second rollout is
   wasted where no unseen card is drawn inside the horizon; each lands rollout rebuilds the whole arrangement
   (`Hidden.Unknown`: both libraries out and back in) where reordering the top places would do. The table's AI is
   capped at 1 s a decision, so there the doubled cost is a shallower search - not yet looked at on the table.
6. **What counts as done** (the open question above), then `ready` for review.

To SEE a position on the table (he asked for the Patrol one):
`./Run-Godot.ps1 MagicTable/MagicTableScene.tscn -GameArgs '--resume=<repo>\MagicCore.Tests\Records\PatrolRacesIntoDrain.json','--resume-at=286','--show-hand','--inspector'`
(`--resume-at` is the move number less one; build `SQGodotCommon.csproj` first - the table ran the old search until
it was rebuilt; delete the `game_records` file the run writes, by name).

After this story, as he ordered them: #24 and `LintTrkSeed1` #48 (land valuation - #48 answers "none" by 0.01 because
the fetched land scores −0.20 in hand), then #213 (option pricing, AI-13).
The code: `Hidden.Unknown` / `LandOdds` (`MagicCore/Ai/Hidden.cs`), `BeamSearchAi.RolloutScore`, `Rollout.Score`'s
`landsDrawn`, `Rollout.OpponentStep` / `AbilityOver`. Read "AI-3's matrix" and the two AI-20 sections in
`docs/findings/magic-ai.md`; the look-ahead's known limits are in `DesignNotes.md` ("MagicCore known ceilings").
Outside this story, found by the pre-commit review: on the table, Undo keeps the AI searches of the undone moves
(`MagicTableManager`), so a note written just after an undo can quote a search from the abandoned line (TBL-10).

## Log

- 2026-11-10: created from AI-17.24 (Shayne): the landcycle, Tasha, surveil, impulse and Jace issues share one root,
  the single guessed future.
- 2026-11-12: sprint 5 — after AI-22; the research note first.
- 2026-11-12: the research note — Cowling et al. on MTG (several guessed futures, one budget split), ISMCTS, Forge — in `docs/findings/ai-research.md`; its questions are AI-20.1's.
- 2026-11-13: a position for it, from Shayne's notes (TBL-10.3; `Records/ShayneNotesSeed1.json` #247): AI 2 keeps its Jace token's
  loyalty instead of surveilling for its missing red source ("he should be aggressively surveilling for it here"). In the
  one sampled library the top card was a keeper, so the surveil changed nothing and lost 0.1 (walkers 2.20 vs 2.10, Pass
  −13.83 vs Jace −1 −13.93): the chance of a bad top card, or of digging to a needed colour, is never weighed. Also
  AI-9.1's "NOT done: the library-top price for surveil".
- 2026-11-13: a second position from Shayne's notes (`Records/ShayneNotesSeed1.json` #24, from AI-9.9): turn 2, AI 2 plays
  a Forest over Rockfall Vale ("it would actually be better to play the Rockfall Vale" — free tapped now, both colours
  next turn). An exact tie, 18.64 each: with this hand only a red 2-drop DRAWN would separate the orders, and the one
  sampled library held none — an option on unknown draws, this story's.
- 2026-11-13: a third position from Shayne's notes, and his diagnosis (`Records/ShayneNotesSeed1.json` #213, from AI-13.13):
  AI 1 spends Extended Absence on a 1/1 Yoshimaru — exiling it, the drain and a faster clock (8.0) just beat keeping
  premium removal (7.7, its measured option over 4 turns). AI 2 holds Koth, two Heartstring Pullers, Chandra's
  Emberling and Marwyn, unknown to AI 1 — and the look-ahead's opponent casts only cards we know (AI-10.4's KnownOnly),
  so no threat ever comes. Shayne: "the AI can see the perfect future. It knows that the opponent is [not] going to do
  anything so it ends up using its premium cards on scrappy targets because it won't get punished for it … an extension
  of the look ahead issue." The open question "does the opponent draw/hold unknown cards, priced how" is this one.
- 2026-11-14: AI-20.1's interview (Decisions), AI-20.2's baseline (24.0%), and on Shayne's go-ahead AI-20.3: the land /
  not-a-land look-ahead as `beam+landornot`. #157 now: both first moves score 8.72 and both best lines cycle this
  turn (not lands 54%: 10.06; lands 46%: 7.18); the wait wins the tie and the plan cycles - played forward, turn 8 ends
  with the land played. Today's search is unchanged by the code (the suite's steps and rollouts are identical to the
  last commit's). Not committed.
- 2026-11-14: AI-20.4's match, on Shayne's go-ahead: `beam+landornot` 54.2% ± 1.5pp against `beam` over 576 games
  (312–264), stopped as "better"; 9781 engine steps a decision against 4498. Still a trial, not the default.
- 2026-11-14: on Shayne's word the land / not-a-land look-ahead is the default (`BeamSearchAi.Settings.Library`);
  `beam+shuffle` is the search before. The suite reads the same under the swapped names (38 changed, 19 885 steps;
  `beam+shuffle` 1 changed, 10 660 steps) and its answers are accepted. Not committed.
- 2026-11-14: as the default it met Shayne's lesson tests: a bug fixed (AI-20.6, the surveil's kept unknown card),
  and twelve tests red (AI-20.7): the Patrol attacks at 4 life on a 50% draw, one term alone no longer keeps the
  Vale, nine controls no longer show their misplay. Suite answers accepted (34 changed); flip rate 0 of 288.
- 2026-11-15: Shayne played the Patrol position on the table: the attack is dead on board (the 2/1 and the drain,
  turn 13). The imagined opponent recasts Boarding Action instead and drains two turns late - the fault to fix.
- 2026-11-15: AI-20.8 on Shayne's word ("it does need to account for on board decisions"): the look-ahead's opponent
  uses an ability on its board before a spell that does less. The Patrol stays home; suite 0 of 288 changed; the
  same twelve tests red (the Patrol now at its `RootOptionTurns` control). No match yet; not committed.
- 2026-11-15: AI-20.8's match, on Shayne's word: `beam` 50.0% against `beam+spellfirst` over 96 games (48-48),
  stopped as "no difference"; 9676.1 engine steps a decision against 9676.0.
- 2026-11-15: the ten controls pointed at the search before AI-20.4 (Shayne: "do what you think is best"); the
  tests are green, 1595 pass. Not committed.
- 2026-11-15: committed (`7b1f1f77`), then the match played again on Shayne's word, as committed: `beam` 54.0% ±
  1.5pp against `beam+shuffle` over 480 games (259-221), "better"; 2.20× the engine steps a decision.
