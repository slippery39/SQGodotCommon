---
id: AI-26
title: Option pricing by the target — a held card is worth what it will hit later, less the mana it costs then
status: backlog
area: AI
created: 2026-11-16
---

## Why

Shayne, 2026-11-16 (his session with Fable 5.2, to see whether it finds an answer the earlier ones didn't): "we price
spells like removal spells or even creatures with etb's not solely based on their immediate board impact if played, but
rather based on how the potential value we could get from them if we were to hold them vs later." "Cards which don't get
much better when we wait, are priced cheaply so that the AI will use them more liberally. Cards which do get better are
priced more expensive, so that the AI will be patient and use them when a good opportunity arises." The misplay it must
stop: "lightning bolting a vanilla 1/1 at 20 life". "Its sort of a balance with no true right answer, especially
considering the random nature of limited when you don't know what is in your opponents deck."

**What was tried, and why it did not land (Claude's reading of AI-13 and AI-20):**
- The price is one number per card NAME (`CastGains`, `Evaluator.Terms.HoldOption`, AI-13.4): each card's best cast a
  turn across self-play games, as twenty quantiles. It cannot see this game — the board, their colours, how many
  answers we hold. Every card's median offer is 0.
- A sorcery-speed option was cut to its AVERAGE offer (`ExpectedBest` at k = 1, AI-13.10) so the AI would not sit on
  its hand; that is about its flat price, so sorcery-speed removal has almost no option price today.
- It decides little: every kept position passes with the option off (AI-13.4's own finding); its matches were flat.
- The root is #213 (`Records/ShayneNotesSeed1.json`, Extended Absence spent on a 1/1 Yoshimaru, 8.0 against 7.7): the
  look-ahead's opponent never casts a card we haven't seen (`Rollout.Settings.KnownOnly`), so nothing better ever
  comes and a wasted answer is never punished. Shayne: "the AI can see the perfect future."

## Requirements

Drafted from Shayne's words — for him to confirm; each needs a position he agrees with BEFORE any change.

- [ ] An answer is not spent on a target far below what is likely to come: a 3-damage burn spell on a vanilla 1/1 at
      20 life is held; #213 holds Extended Absence.
- [ ] Scarcity: the same position with three removal spells in hand fires the spare one; with one in hand and few left
      in the deck it holds.
- [ ] Mana is the tempo cost: an expensive answer (3–5 mana) held is mana wasted and can lose on tempo before a better
      target comes; a cheap one fits the curve and costs little to hold.
- [ ] A tempo swing still fires: two removal spells on two middling blockers to connect for 7, with follow-up plays.
- [ ] Activated abilities are options with a deadline — the end of the opponent's turn: a tapper is not used in its
      own upkeep for nothing, a pinger stays untapped while a block matters.
- [ ] Card-agnostic (it reads what a card does, never its name), a switch, measured (kept positions with controls,
      suite, match) — judged by reasonable plays, not win rate.

## Decisions

- 2026-11-16, Shayne (more answers in hand means firing more freely — asked whether "the spare removal spell is worth
  the n-th best threat" is how he thinks, or tempo): **both.** "Its more about tempo because of the wasted mana. If your
  removal spells are cheap it matters less, but limited can have like 3, 4 or 5 mana removal spells, where if you don't
  use your mana effectively you can lose on tempo even before a better target comes out. The cheaper it is, the more
  likely you will find a fit for it in your curve which makes it less of a tempo loss." And on the n-th best threat:
  "Yes that is correct."
- 2026-11-16, Shayne (asked whether he holds premium removal for bombs or for anything decent): "Typically, but its
  also like if I can set up a way to suddenly turn on tempo i.e. lets say the opponent has 2 mid blockers and I can deal
  7 damage to them if I remove both of their threats. I might use 2 removal spells to do so, forcing them to either
  have answers or blockers for my board. This is even better if I have follow up plays the next turn (i.e. more removal
  or bigger threats coming)."
- 2026-11-16, Shayne (asked for a position where he fires removal at a middling target for tempo): none to hand — "I
  would have to find one from playing myself."
- 2026-11-16, Shayne: **activated abilities too.** "options pricing should also work with activated abilities such as
  pingers or tappers … the AI would use abilities as soon as possible, leading them to use their tappers during their
  own upkeep (for no value) and pingers would tap asap (not keeping them up for blocks). In this case, the time limit
  is before the end of the opponents turn, and the options pricing should account for that they are looking for the
  best way to use the ability before their turn." (AI-13's lesson 6, in his earlier words. Where it stands: AI-13.12's
  `WaitOnTies` stopped most own-upkeep uses — a tie-break, not a price; "kept up for blocks" and the best moment in
  their turn are not covered, and its counts were taken under the search before AI-20.4.)
- 2026-11-16, Shayne (on the own-upkeep uses stopped by AI-13.12's tie-break): "this is more of a stopgap which solves
  the symptom but not the problem." So the tie-break is a candidate to retire once abilities are priced (AI-26.5).
- 2026-11-16, Shayne: **the report is agreed** ("Yes do the report"), and the tempo position is hand-built for now
  ("Sure hand build this position for now, and i'll look for better ones when I playtest").
- 2026-11-16, Shayne (on the report's numbers, AI-26.3): **no Bolt on the vanilla 1/1, with three in hand either** —
  "I probably would not bolt a 1/1 even if I had 3 bolts in hand, and its not even close. The other scenarios are
  context dependent, but probably good enough for our AI for now." So the report's "hold by 0.1" there is wrong, and
  today's search plays it wrong too: Cast Bolt → the 1/1 (−6.73, tied with Pass, whose line Bolts it later). The
  position is `OptionReportTests.ABoltOnAVanillaOneOne…`; his answer is HOLD, one Bolt or three.
- 2026-11-16, Shayne: **a vanilla 1/1 is a share of a card.** "So in general a 1/1 is not worth a full card. Kind of
  like how surveil is worth 0.33 of a card. A vanilla 1/1 is usually about 0.33 to 0.5 of a card, but it depends on
  the format and is obviously context dependant. This is something that I have learned over my years of playing
  magic, and its probably something that would be hard for the AI to figure out generically. We could use this to
  price using removal on vanilla 1/1's so that we are not just bolting 1/1's every time." (Claude: used in the report
  as he said it — their vanilla 1/1 as a removal target, 0.4 of the average measured card, 3.4 points, plus what its
  attack costs us. NOT extended to other small bodies, tokens with abilities or the evaluator's board price: his to
  give. The evaluator's own body for a 1/1, 4.5, is 0.53 of a card — just over the top of his range.)
- 2026-11-16, Shayne (asked how far the rule goes — a 1/1 token, a vanilla 2/2, a 1/1 with a minor ability): "Its
  entirely context dependant. a 1/1 token does count the same, and for all other creatures it depends on the abilities
  it has." So: **the rule is the vanilla 1/1 alone, token or card; no scale for other bodies** — they stay at the
  evaluator's price, which counts their abilities. (The report never asked whether it is a token, so it already
  prices one the same. 0.4 against 0.33 was asked and not answered: 0.4 stays, Claude's choice.)
- 2026-11-16, Shayne (on pricing bodies by cards, and whether to normalize the evaluator to 1 = a card): "that also
  seems like a different story altogether, semi related to this specific scenario, but it still doesn't solve options
  pricing. Our options pricing problem would still be unsolved." The scale is AI-27. (Claude built AI-26.4 on this,
  OFF by default, having asked twice and been answered on other points — his to overrule.)
- 2026-11-16, Shayne (on AI-26.4's result — the three-Bolt position still Bolts the 1/1): "So the example might be
  flawed here. I mean if all I had were lightning bolts in my hand, and my deck had no potential creatures to block,
  then yes I would be forced to lightning bolt the 1/1's because I would eventually die. But i'm wonderinig if that
  logic changes if I have like a 3 mana 2/2 or 4 mana 3/3 in my hand that would be able to potentially block those
  1/1's? Because then I have an answer to those 1/1's its just it will take a bit of time to get to that point. I'm
  still not happy with the fact that the bolt is being used on a 1/1 right away." (Claude's reading, not his words:
  the 1/1 is worth an answer only for the damage it does until something cheaper — a blocker — handles it; so a
  target's worth depends on what it can still do against OUR board. That is AI-12's subject.)
- 2026-11-16, Shayne (offered: price their creature by what it can still do against our board, behind the same
  switch, tested on his blocker position; asked for the 1/1's worth in three cases): "Yes lets try your do next
  suggestions." No numbers given, so the share is Claude's (AI-26.12) — his to set.
- 2026-11-16, Shayne (given the suite's 15 changed positions under both trials, each as its board and card texts):
  "The plays do look more reasonable than before on first galcne. And sure play the turn out to make sure." A first
  glance, not a position-by-position answer.
- 2026-11-16, Shayne (the order from here): "lets do that in order, run the match, then commit, then begin activated
  abilities afterwards". One match approved. (Claude stopped it at 19 games when the pre-commit review found bugs in
  the trial code, fixed them, and started the SAME match again on the fixed code — one approved match, played once
  to its end.)
- 2026-11-16, Shayne (seed 2 move 47 — a kill spell; their 3/2 that our 5/5 walls, their 2/1, their 1/3 that makes
  the 2/1 unblockable; the trials destroyed the 2/1): **wrong — the first changed position he calls wrong.** "Thats
  an interesting scenario that is hard to price. So on stats alone the 3/2 is the best card. But the 1/3 that makes
  things unblockable is what allows the opponent to attack through. It depends on what our goals are, if we are just
  pure racing, we might kill the 3/2 and attack. If we want to be more defensive, we kill the 1/3 and keep our guys
  back to block. So it depends on what our current role is. We should never destroy the 2/1 though."
- 2026-11-16, Claude (bookkeeping): its own story rather than more of AI-13 — AI-13 is the rules route, this computes
  the price. #213 and AI-13.5 / 13.6 / 13.8 / 13.9 and lesson 6 are this story's once it is agreed.

**The design proposed (Claude, 2026-11-16 — NOT agreed, nothing built):** price the target, not the card.
- **Now** — what the best target on the board is worth: the search already plays the cast out.
- **Later** — the best target likely to appear in the turns left: the set's creatures in the opponent's colours that
  this card can actually hit (its own target filter and effect), weighted by how common they are, each at its card
  value. No per-card table to measure or go stale.
- **How many answers** — the best answer is kept for the best threat, the next for the next: with n answers (in hand,
  plus those expected from our own list) the spare one is worth only the n-th best threat.
- **Mana** — the later use is charged for the mana it will need then: nothing when the mana would be spare (a cheap
  spell, an empty hand), a turn's worth when it would displace a play.
- **Abilities** — the same with a short clock: worth the best use before our next untap; at the last chance (their
  end step) waiting is worth nothing, so anything is taken. Nothing is used up, so scarcity does not apply; using it
  costs the creature's attack or block.
- Known limits: target values are today's card values and evaluator (AI-12 not built); tricks and protection are
  options against THEIR plays, a different "later" — after removal; the same "what they will probably play" model is
  the first half of AI-20's item 2b (the opponent's unknown hand).

Open questions (for Shayne, before `ready`):
- **What is a target worth?** Answered in part (Decisions, 2026-11-16): a vanilla 1/1 is not worth a Bolt, "not even
  close" — the report's 5.0 for it (body 4.5 = 2 + 1.5 × power + toughness, clock 0.5) against 10.0 for a 3/2 is far
  too high. Measured: pricing ten turns ahead instead of four moves "hold by 0.1" only to 1.3, so the horizon is not
  the cause. Open: what DOES make a small creature worth an answer — to be asked with AI-26.6's proposal.
- Does he confirm the hand-built tempo position (`OptionReportTests.TheTempoSwing…`)? It needs an opponent that can
  punish before it tests anything: today's search wins it outright.
- Is 0.5 a mana (`RecastPerMana`, chosen for a recast) the right size for wasted mana? A 4-mana spell fully busy is
  charged 2.0 — a fifth of a 3/2.

## Tasks

- [ ] AI-26.1 The interview — begun 2026-11-16 (Decisions); the open questions above
- [x] AI-26.2 A report, no change to the AI (`MagicCore/Ai/OptionReport.cs`; `--record=FILE --options=N`; the
      hand-built positions are `OptionReportTests`, explicit, `--filter "TestCategory=OptionReport"`). Built
      2026-11-16; its numbers are in `docs/findings/magic-ai.md` ("AI-26.2"). As built — Claude's choices, each for
      Shayne to overrule:
      - **"What they probably play" is 200 sample AI sealed decks** sharing the colours they have shown (their lands,
        and their cards in public zones), not a rarity table: it carries the builder's choices and the packs' real odds.
      - **A target's worth is measured, not written per effect:** the creature is put onto their real battlefield and
        the card really cast at it, settled. So what a card can hit and kill is the engine's answer.
      - **A card's held price is the hand with it less the hand without it**, over 400 sampled futures (one fixed
        seed) — the spare answer falls to the n-th best target with no rule for it. Greedy assignment.
      - **Mana:** idle mana now if held (the best the other cards could use of our open lands) plus its cost later
        times how busy our mana is, at `RecastPerMana`.
      - Not done: Ajani's Anguish at 20 (an X spell with nothing to kill — not run); a card that can't be cast at
        this moment isn't priced; no time discount; future creatures are priced on today's board (so #547, at 5 life,
        is unreadable); an enters trigger's target isn't attributed; no `RunProgress` (one position, ~8 s).
- [ ] AI-26.3 Shayne reads the numbers against his own judgment; the price is corrected until they match — first
      reading 2026-11-16: the three-Bolt position is wrong (he holds, "not even close"; the report: hold by 0.1), the
      others "probably good enough for our AI for now"
- [x] AI-26.6 **Shayne's rule in the report: their vanilla 1/1 is a share of a card** (Decisions, 2026-11-16;
      `OptionReport.VanillaOneOneShare` 0.4 of `ACard`, the average measured card, 8.5). And the report prices up to
      8 turns ahead (`MaxTurns`; the sooner clock still shortens it) where it priced 4 — Claude's choice: at 4 the
      third Bolt often meets no creature at all. The three-Bolt position: hold by 0.1 → 0.4 with the rule → **1.5**
      with both (one Bolt: 4.8); the tempo swing still fires (cast now by 1.0), #213 holds by 2.3, #547 unchanged
      (its clock prices one turn). `--turns=N` prices another horizon. Claude's first proposal — a target priced by
      what it does if left alone (AI-12's subject) — is NOT built: his rule is simpler and his own.
- [x] AI-26.7 How far the rule goes — answered 2026-11-16 (Decisions): the vanilla 1/1 alone, a token the same
- [ ] AI-26.8 **Where the 1/1 rule lives once the search prices options** (found while planning AI-26.4, for Shayne):
      in the report the rule lowers what a Bolt gains NOW. In the search that gain is not a number the option price
      owns — it is the evaluator's board score with the 1/1 gone (4.5 + its clock). A rule kept inside the option
      price would lower only the HOLD side (the 1/1 as a future use of the Bolt) and push towards casting. So in the
      search it has to be the evaluator's price of a vanilla 1/1 (`CreatureValue`), which also reprices every 1/1
      token made, chump-blocked with or traded. Proposed: AI-26.4 first without it (the arithmetic alone held three
      Bolts by 0.9 at 8 turns), then the 1/1 price as its own switch, measured on its own
- [x] AI-26.4 **The price in the search, behind a switch that is OFF** (`MagicCore/Ai/OptionPrice.cs`,
      `BeamSearchAi.Settings.OptionPrice`, `beam+optionprice`; built 2026-11-16). Our answers in hand are priced
      TOGETHER in place of their held prices (`Evaluator.Side`): what they make of the creatures on their board and
      the ones to come, over 32 sampled futures read at the search's root, less the mana charge. As built:
      - **An answer** is a card whose target is chosen as it is cast and that gains from some likely creature of
        theirs; an enters-trigger target is not one (a creature held for its trigger made AI-13.10's clunky hands).
      - **A gain is measured once per (answer, creature) and kept** (`OptionPrice.Gain`): the card really cast at
        the creature in the card-value fixture, threat term off — not on today's board as the report does, which
        cost ~8 s a position. The report shares the decks, the futures and the assignment, and prints the search's
        price for the hand beside its own.
      - Never below the flat price a card; a card short of mana keeps today's price; the old table (`HoldOption`)
        still prices what is not an answer (a pump, a flash creature).
      - **Shayne's 1/1 rule is NOT in it** (AI-26.8): there it would lower the hold side.
      Measured (findings, "AI-26.4"): 1598 tests pass with it off (the golden replays: the default is unchanged).
      **The three-Bolt position is NOT fixed:** with no look-ahead holding now wins (3.44 against 3.21 — the Bolt
      in hand is 5.22 where it was 1.20, against 5.00 for the 1/1), but the search still Bolts it (1.15, tied with
      a Pass that Bolts later): its look-ahead adds the 1/1's two attacks, 2 life at a point each. The tempo swing
      still fires both Kills. **Suite: 11 of 288 decisions change**, +2.9% engine steps a position; no match run
- [ ] AI-26.9 (2026-11-16: the 15 changes under both trials given to Shayne, each as its board and card texts —
      `--suite=beam+optionprice+byuse --shown=30 --boards`, `PositionSuite.Board`; his answers to come. Two show
      only a first move, a land in place of No Admittance — whether the burn follows that turn is not read.)
      **Then each turn played out under both searches (`PositionSuite.TurnPlayed`, printed by `--boards`; findings,
      "AI-26.9"):** the burn does NOT follow the land in either — a 4-mana creature is cast instead; one "hold" was a
      different target (Extended Absence on the 3/1 trampler, not the 2/2); and three turns are for his word, each
      giving up a removal play that did something: seed 4 move 18 (was: burn the 3/2, fight the 1/3, attack for 5;
      now: a land and a 4-drop, no attack), seed 2 move 21 (was: the 1/4 flier killed by an attack and the burn; now:
      the 3/2 with reach cast), seed 2 moves 73/83 (was: Extended Absence on their Jace token at 8 loyalty; now: a
      second 3/3 deathtouch and an attack at the token).
      **His word on those three (2026-11-16):** seed 4 move 18 — "it could go either way, getting 5 extra power on
      board to potentially do the same thing next turn (i.e. remove both their creatures) does give a higher
      ceiling"; seed 2 move 21 — "looks fine to me"; seed 2 moves 73/83 (described again in full) — "its not clear
      what the best play is at first glance, I would say its a toss up for those ones. Either play is justifiable,
      which means the AI is playing good enough. If it were doing something weird like extdned absensing the 2/2 and
      then attacking face and ignoring jace then that would be pretty bad, but I can see either way." **So all 15
      are read and none is called wrong** — his bar, in his words: either play justifiable is good enough.
      Read the suite's 11 changes with Shayne — eight are removal held where it was cast (No Admittance ×3,
      Vraska's Final Mercy, Prophesied End on Koth, Academic Ascent, Extended Absence, one more land choice each
      way): which are reasonable holds? Each by the card's text and the board, never a nickname
- [x] AI-26.11 His position, built (`OptionReportTests.ThreeBolts_AndABlockerInHand_Printed`, 2026-11-16): three
      Bolts, three Mountains, their vanilla 1/1, and a blocker in hand. **A {2}{R} 2/2 (castable now):** today's
      search casts the 2/2 (−0.21; Bolt the 1/1 first −0.35) and so does the switch (11.26; Bolt first 9.15 — 2.1
      behind where it was 0.14). In both, their 1/1 then does not attack — and the imagined next turn Bolts it
      anyway, to send the 2/2 past it. **A {3}{R} 3/3 (a land short):** both Bolt the 1/1 at once (today −2.02, the
      switch 6.42, each tied with a Pass that Bolts later in the turn). So with a blocker he can cast the Bolt is
      no longer the FIRST play, and the switch widens that; what is left is the evaluator paying 4.5 to remove a
      1/1 that can do nothing against our board (findings, "AI-26.4")
- [x] AI-26.12 **Their creatures that ours hold off count for less** — a second trial, OFF by default, its own
      switch (`Evaluator.HeldOff`, `BeamSearchAi.Settings.ByUse`, `beam+byuse`; built 2026-11-16 on his go-ahead).
      A creature of theirs is held off when one of ours beats it outright in a block (kills it and survives it);
      each of ours holds off one of theirs, their biggest first; a creature in our hand we can cast within a land
      counts as ours. Held off, it keeps `HeldOffShare` 0.4 of its value — on the board score and as a target in the
      option price alike. Claude's choices, each his to overrule: the share (a vanilla 1/1: 4.5 → 1.8, 0.21 of a
      card); THEIR creatures only, for the searching player (ours held off by theirs are priced as always — the
      same rule on our side would stop the AI casting small creatures into a bigger board); by power and toughness
      alone. **His position plays as he described** (`beam+optionprice+byuse`): with the {2}{R} 2/2 it casts the
      2/2 (12.90; Bolt the 1/1 first 9.15) and the imagined next turn no longer Bolts the 1/1; with the {3}{R} 3/3 a
      land short it PASSES (6.58 against 6.42 for the Bolt — a thin 0.16) and holds all three. Three Bolts and no
      blocker anywhere still Bolts, as he said it must; the tempo swing still fires both Kills. Tests 1598 pass
      with both off. Suite: `beam+byuse` 11 of 288 changed, `beam+optionprice+byuse` 15 of 288 (findings). No match
- [x] AI-26.13 **The pre-commit review's ten findings, fixed before the match** (2026-11-16; both trials only —
      the default is untouched, 1598 tests pass):
      - *The last answer in hand was over-held:* the hand's price counted the answers still to be DRAWN, but only
        while an answer was in hand — casting the last one lost all of it. Now the hand is worth what it ADDS to
        what the drawn answers would make anyway.
      - *A crash waiting in a match:* a legend on their board with a known copy in their hand threw (the name was
        taken out of the likely creatures but left among the known cards).
      - *One more answer could LOWER the hand* (each was charged mana by how busy the whole hand was, the other
        answers included): now each answer's gains are net of its own mana, set by the REST of the hand — monotonic.
      - *Held off by a pump:* `HeldOff` read this turn's power and toughness; now what lasts past the turn.
      - *Kept gains depended on who asked first* (the asking search's switched-off terms, and its step count): now
        measured with exactly three terms off, their steps counted for nobody; the deck sample and each gain made once.
      - *A land that makes any colour* made the opponent "five colours"; a known non-creature card in their hand was
        counted as unseen; known creatures skipped the mana budget; `Explain` priced from the sampled position, not
        the one decided in; `--boards` had no progress line (now `RunProgress`, a position a core: 1 min for 12);
        the new ceilings are in `DesignNotes.md`.
      **What the fixes changed** (findings, "AI-26.13"): the suite's changes fall from 15 to **12** under both
      trials, from 11 to **5** under the option price alone, 11 to 10 under held-off alone. Of the 15 Shayne read,
      four play as the default again — Koth cast (his "looks fine"), the burn-and-fight tempo turn he called "either
      way", the Jace-token turn before combat ("a toss up"), and the pump held — and one is new: seed 2 move 47, the
      kill spell on their 2/1 that can't be blocked instead of their 3/2 that our 5/5 walls. The blocker position
      still plays as he described (Pass 6.70 against 6.45 with the 3/3; the 2/2 cast at 13.01).
- [x] AI-26.14 **A creature we attack with holds nothing off** (2026-11-16, from his answer on seed 2 move 47).
      The trials destroyed the 2/1 and then ATTACKED with the 5/5 that `HeldOff` had counted as the 3/2's wall: the
      rule read our tapped creatures as blockers. Now only a creature untapped for their next attack counts, as
      `Evaluator.Through` counts blockers — his two roles, each consistent: race (the 5/5 attacks, the 3/2 is at full
      value, kill it) or defend (the 5/5 stays home). The suite's position plays the default again (the 3/2 is
      destroyed). A guard on his "never the 2/1", hand-built:
      `OptionReportTests.AKillSpell_NeverTakesTheirTwoOne…` (today and `beam+byuse` both take the 3/2; NOT a control
      — the rule before the fix took the 1/3 there, his defensive play). 1599 tests pass. Suite, both trials: 14 of
      288 changed (findings, "AI-26.14"). The match stopped a second time for it, at 107 games — 43.9% (47-60) for
      the trials BEFORE this fix: not a result, but it leaned against them — and started again.
- [x] AI-26.15 **The match** (Shayne's word, 2026-11-16; `--match=beam+optionprice+byuse,beam --until-clear`, on
      the code after AI-26.13 and AI-26.14): **49.2% ± 1.7pp over 384 games (189-195), stopped as "no difference of
      5pp either way"**, at 11 312.3 engine steps a decision against 10 500.1 (+7.7%), 102.9 min. By deck: Red-Green
      80-48, White-Black 75-53, White-Blue Jace 34-94 (its usual share). So the trials neither gain nor lose
      strength that this match can show; what they change is the plays (findings, "AI-26.15"). Both stay OFF: whether
      either becomes the default is his call, by the plays.
- [ ] AI-26.10 What still Bolts the 1/1 is on the board side, AI-27's: the 1/1 at 4.5 (his 3.4) and a point of life
      at 20 priced 1.0, so a 1/1's two attacks are a quarter of a card. Decide there, then this position is the test
- [ ] AI-26.5 Activated abilities with the end-of-their-turn deadline (lesson 6; AI-17.25's position). **Begun
      2026-11-16 with a measurement** (`AbilityTimingTests`, explicit, `--filter "TestCategory=AbilityTiming"`):
      hand-built positions, our turn and theirs played out by the default search in both seats, 20 life each.
      - *A tapper ("{T}: tap target creature", a 1/1) and our 2/2 against their 3/3 and 2/2:* fine. Nothing in our
        turn; in THEIR upkeep it taps the 3/3, and their 2/2 stays home. The same from our upkeep.
      - *A pinger ("{T}: 1 damage to any target", a 1/1) and our 2/2 against their 3/3:* fine. Our 2/2 attacks,
        their 3/3 blocks, the ping finishes it.
      - *The pinger alone against their 2/3, nothing it can kill:* **the misplay he described.** It pings their
        face in OUR turn — at our beginning of combat, and from our upkeep at our DRAW step — so it is tapped
        through their turn, where their end step would have given the same point with every option kept.
      Why (read, not yet measured): the search looks at our decisions this turn; their turn is the rollout's cheap
      policy, which never uses the ability at their end step — so "ping now" is a point and "wait" is nothing, and
      `WaitOnTies` only defers within our own turn (it moved the ping from upkeep to the draw step).
      **Proposed (not built, for Shayne):** the look-ahead uses a free tap ability at its last chance — their end
      step, before our untap — so waiting is seen to give the same; a line that waits then ties with using it now,
      and the potential tie-break (an untapped creature kept) holds it; a better use before then (a kill, a combat
      finished) still wins on the score. Behind its own switch, measured as the others

## Next

**Handoff (2026-11-16, end of the Fable session; Shayne continues on Opus in a new session).**

**State.** Committed `1ca72bec`: the option report, two trials in the search — `beam+optionprice` (our answers in
hand priced together by what they are likely to meet, AI-26.4) and `beam+byuse` (their creatures that one of ours,
untapped for their next attack, beats outright keep 0.4 of their value, AI-26.12/26.14) — both **OFF by default**.
Match, both on: 49.2% ± 1.7pp over 384 games, no 5pp difference either way, +7.7% cost (AI-26.15). Shayne read the
suite's changed positions: "more reasonable than before"; none called wrong after the fixes; his bar — "Either play
is justifiable, which means the AI is playing good enough".

**Uncommitted in the working tree (leave for him to say):** `MagicCore.Tests/Ai/AbilityTimingTests.cs` (new — the
AI-26.5 measurement), this story's AI-26.5 notes, and `FraCardSheet.md` / `TrkCardSheet.md` regenerated because the
new test file names cards (`python tools/magic_docs.py --check` must stay clean). Commit only on his word, after
`/pre-commit`.

**Next task: AI-26.5, activated abilities** (his order was: match, commit, abilities — the first two are done).
Measured, not yet built: a tapper is used well (in their upkeep); a pinger with nothing to kill pings their face in
OUR turn (begin combat, or the draw step from upkeep) — the misplay he described. Read: `AbilityTimingTests`
(`--filter "TestCategory=AbilityTiming" --logger "console;verbosity=detailed"`). **Proposed, waiting on his
go-ahead:** the look-ahead uses a free tap ability at its last chance (their end step, before our untap), so waiting
scores the same as using it now and the potential tie-break keeps the creature untapped; behind its own switch. He
called `WaitOnTies` (AI-13.12) "a stopgap which solves the symptom but not the problem" — this is meant to replace
it. Where to look: `Rollout.OpponentStep` / `Rollout.OurPlay` (the rollout's policies), `BeamSearchAi.Waits`,
`BeamSearchAi.Potential`; AI-13's lesson 6 (his words on tap abilities).

**Still his to decide:** whether either trial becomes the default; the 0.4 held-off share (the three-Bolts-and-a-3/3
case holds by only 0.25); three changed suite positions he has not read (findings, "AI-26.14": s1 #70, s1 #111,
s1 #121); AI-26.10 / AI-27 (scores in cards — what still Bolts a vanilla 1/1 when there is no blocker anywhere).

**Tools built this session:** `--record=FILE --options=N [--turns=N]` (the option report); `--suite=A --boards`
(each changed position's board, card texts, and its whole turn under both searches, ~1 min for 12); the hand-built
positions in `OptionReportTests` (`--filter "TestCategory=OptionReport"`).

**Traps met:** the 200-deck sample (~10 s) and each new answer's gains are measured on first use — a hitch at the
table if a trial becomes the default; `OptionPrice.Measured` clears the root and runs with exactly its own terms off,
or the price reads itself and depends on who asked first; a creature we attack with must never count as a wall
(AI-26.14); the suite's oracle is the default evaluator read deeper, so it leans against any new term; describe a
position to him by each card's text and the board, never a nickname.

## Log

- 2026-11-16: created — Shayne's session on option pricing; his answers recorded, the design proposed, nothing built.
- 2026-11-16: AI-26.2 on his go-ahead — the report, run on #213, #547, the Bolt on a 1/1 (one and three) and the
  hand-built tempo swing; numbers in `docs/findings/magic-ai.md` ("AI-26.2"). Three faults fixed on the way, each
  found by a number that looked wrong: an unsettled baseline in combat (Unsummon "gained nothing" from 99%), a second
  copy of a legend already on their board counted as a target (the legend rule), and all 200 decks used when under
  ten fit three shown colours. The AI is unchanged; not committed.
- 2026-11-16: Shayne's first reading — no Bolt on the 1/1 with three in hand, "not even close". Checked: ten turns
  priced gives hold by 1.3 (0.1 at four), so the cause is the 1/1's worth; today's search Bolts it (−6.73, a tie with
  Pass). AI-26.6 proposed.
- 2026-11-16: Shayne's rule — a vanilla 1/1 is 0.33 to 0.5 of a card. In the report at 0.4, with 8 turns priced: the
  third Bolt is held by 1.5 (0.1 before), the other positions keep their answers (findings, "AI-26.2").
- 2026-11-16: his question on the scale (AI-27, created). AI-26.4 built, off by default: tests 1598 pass, the suite
  changes 11 of 288, the three-Bolt position still Bolts (findings, "AI-26.4"). Not committed.
- 2026-11-16: his blocker-in-hand position built and run (AI-26.11): the castable 2/2 is cast first by both searches
  (the switch puts "Bolt first" 2.1 behind); with the 3/3 a land short both Bolt at once.
- 2026-11-16: AI-26.5 begun — a tapper and a pinger measured under the default search (Tasks); the pinger with
  nothing to kill pings their face in our own turn. Not committed (the test file only).
- 2026-11-16: the match, on the fixed code: 49.2% ± 1.7pp over 384 games (189-195), no difference of 5pp either
  way; 11 312.3 engine steps a decision against 10 500.1. Committed on his word, both trials off.
- 2026-11-16: seed 2 move 47 called wrong by Shayne ("never destroy the 2/1"); the cause was our attacker counted
  as a wall — fixed (AI-26.14), the position plays the default again. The match stopped at 107 games (43.9%) for it
  and started a third time.
- 2026-11-16: `/pre-commit`: ten findings, all in the trial code, fixed (AI-26.13); the suite changes 12 of 288
  under both trials where it changed 15. The match stopped at 19 games for it and started again.
- 2026-11-16: AI-26.12 on his go-ahead — their held-off creatures count 0.4 (`beam+byuse`). With the option price
  his position holds the Bolts both ways; suite 11 and 15 of 288; tests 1598 pass with the trials off. Not committed.
