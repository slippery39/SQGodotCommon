---
id: AI-27
title: An evaluator pricing pass — one consistent theory, scored in cards, instead of piecemeal fixes
status: backlog
area: AI
created: 2026-11-16
---

## Why

**Reshaped 2026-11-17 (Shayne).** "I think we might need an evaliuator pricing pass and please note what we found
here, where instead of fixing everything peacemeal one by one, we actually try to come up with a better way for the
evaluator to score that is more consistent and less finicky." And: "thats what that story should focus on, is sort of
combining all the issues we have had into an easier to understand theory that spans across our whole system."

His theory, in his words (2026-11-17):
- **One unit:** "perhaps we need to normalize our scoring by 'cards' that is a value of +1 means you are now up a card
  from before."
- **Drawing is never punished, cards valued once seen:** "we also have to make sure that drawing a random card is not
  punished (since drawing a random card will always get you 1 card deeper in your deck), but that cards are properly
  evaluated once they have been drawn (i.e. a random card should always be worth a card... a 1/1 vanilla creature on
  turn 10, is usually not worth much even if it was the card you drawn.)"
- **Deploying is not punished:** "We have had some bugs where not deploying cards is punished, because we might lose
  that card to a line later on (which makes no sense, a deployed resource is usually better than a non deployed
  resource), so like if I play a 3/2 and it trades with the opposing 3/2, both of us are neutral on cards, which is
  usually fine we should not choose to hold our 3/2 in our hand because that might happen."
- **Card advantage counts whatever the cards are:** "if I play a draw 2 and I draw 2 lands, even if it is the late game
  and the lands are not that valuable, I am still up 2 cards from a total gameplay perspective. Otherwise I would have
  spent the next 2 turns drawing those lands anyways and being in a much worse spot. It shouldn't matter that my draws
  happened to be lands but just the fact that I got to draw 2 cards deeper in the deck."
- **Drawing now beats drawing later:** "drawing a card gets 1 carc closer in your deck. Waiting to draw a card doesn't
  really give you any additional value since you will be a card deeper in your deck either way."
- **A bounce gives no card:** "the value is in the opponent having to spend mana to deploy their card again. It does
  not 'give a card back'".
- **Life against a card:** 5 life paid at 20 is about a card; a card spent to gain life needs 8+; life is worth more
  the lower it is (Decisions, 2026-11-17).

**Value and tempo, two concepts (Shayne, 2026-11-17, end of session):** "basically the whole evaluator rates simply on
perceived value, and this might be where we run into problems, because alot of games in magic are not just about
value, but tempo as well. So value is basically card advantage which is what we were kind of describing above (i.e.
how many cards am I ahead of my opponent). Card advatnage can be in raw cards (divination drawing 2 cards is card
advantage), or it can be on board card advantage (a card that makes 2 2/2's can be a form of card advantage,
flametounge kavu is a form of card advantage as well). Tempo is not about card advantage but about how much mana you
are deploying vs how much mana you are 'negating' your opponent. For example divination is a tempo negative card, you
have to spend 3 mana to draw 2 cards, but you don't negate any tempo from your opponent, nor do you add any tempo
yourself. Lightning Bolt is a tempo neutral card if you kill a 1 mana card. But it is a tempo positive card if you kill
a 4 mana card (up 3 mana on tempo), as you could negate your opponents 4 drop, and drop a 3 mana card yourself. And this
'rule' plays for most removal spells. If you are trading up on mana by using a removal spell it can be a game winning
play sometimes. (i.e. opponent plays a 6 drop, you path it and play a 5 drop, you are now ahead on tempo). Bounce
Spells are card disadvantage usually, but they give you tempo. If you bounce an opponents 6 drop twice so that they
can't blkock and the game is over before it actually has impact, then the card disadvantage does not matter. As you
can see tempo is basically about forcing your opponent to either spend more mana than you for the same value. Tempo
positive plays can be value negative plays, but you can have both tempo positive and value positive. Repulse on a 5
drop awith an armadillo cloak for example is both a tempo positive and a value positive play. Generally the player who
is going for positive tempo but value negative is trying to win quickly or get the opponent into a certain state
quickly before the card disadvantage matters. So all this goes to say, i'm wondering if properly modeling this was the
missing piece to our eveluator instead of trying to shoehorn 2 completely different concepts into one, have them be
seperate concepts altogether, then figure out how to combine them and choose within themselves."

Claude's answer (for him to correct): yes — today one number per thing carries both (a creature's body price is its
card AND the mana in it; a bounced creature "itself less its recast" is tempo inside a card price; the option price's
mana charge; the held fraction 0.5 is in effect a tempo fudge so casting beats holding), so moving it to fix one breaks
the other. Two ledgers read cleanly: Divination +1 card / −3 tempo; Bolt on a 4-drop 0 cards / +3; Unsummon −1 card /
+ their recast. **Combined by how long the game will last** — tempo matters in a short race, cards in a long game ("who's
the beatdown"; his "it depends on what our current role is", AI-26, seed 2 move 47): the exchange rate from the clocks
the evaluator already computes. Care: the look-ahead already sees some tempo implicitly (a board developed sooner hits
sooner) — not counted twice. No MTG simulator found that keeps the two apart: Forge, by its forum record, is per-card
rules and a creature score from stats and keywords (`evaluateCreature`), without real look-ahead; card-game research
uses feature-based evaluations, one Legends of Code and Magic design "tempo-based" — so this would be our own design.

**The trifecta (Shayne, 2026-11-17):** "Can we also add notes for options pricing, I think this is the trifecta (at
least for now), Value Pricing (subsets, immediate value vs value over time cards), Tempo Pricing, and Options Pricing
(holding cards for better value pretty much)". So the design has THREE concepts, each its own ledger:
- **Value** — card advantage, with two subsets: *immediate* value (a removal spell's kill, Divination's two cards, a
  creature's body now) and *value over time* (a planeswalker's loyalty, an engine, a recurring ability, a Clue's card
  for later — AI-9.3's lifetime price is one of these today).
- **Tempo** — mana deployed against mana negated (above).
- **Options** — what holding a card is worth: the better use it may find later, less what waiting costs (AI-26: the
  option price for answers, a held card priced by the targets it is likely to meet; AI-26.5: a tap ability kept for its
  last chance; AI-13.12's `WaitOnTies`, the stopgap still on). Holding has a tempo cost (mana unspent this turn) and an
  option gain — the two ledgers meet here.
Today all three live in one number per thing; the inventory (Next) labels each existing term value (immediate or over
time), tempo, option, or mixed.

Claude's reading of the theory in cards (for him to correct): a position is scored as cards up, both players counted alike —
cards in hand, cards' worth on the board (what each is DOING, against an average card), library depth, life converted
at his rate, mana spent as tempo. A drawn card is an average card at the moment it is drawn; seeing it is a land is no
loss, because the library left is better for it (the evaluator sees "a land in hand" and never "and my next draws got
better" — the missing half). A trade is −1 each, so casting never scores below holding the same card. Loyalty that
buys a draw is worth at most that draw now. A bounce costs them the recast, and no card.

**What pointed here — fixed one by one, each in its own units** (the evaluator prices "a card" in at least six places:
a held card at half its measured value, an unseen card 2.65, the flat 1.2, a walker's future draws at the unseen price,
the option price, a bounced creature as itself less its recast):
- AI-26: a Bolt on a vanilla 1/1 (the 1/1 at 4.5, "not even close"); the held-off share on the board counted a wall
  twice and jumped when the wall died (Jace −1 for −3, Divining Duelist in its own combat — AI-26.16, option A).
- AI-26.16: removal gains measured without the held values made a bounce worth more than a kill.
- AI-28.2: a token sacrificed at the end step priced as a 2/1 kept, so the line that made it lost.
- AI-28.3: a Clue (a flat 2.0) lost to 4 life at 21 (3.5).
- This story's trial, unseen cards at full value (2026-11-17): three Jace −3 draws became passes and four removal
  spells were held (Prophesied End, Fatehold Charm's bounce, Vraska's Final Mercy, Academic Ascent) — Shayne: "The fact
  you described these alone tells me that somehting is way off in our evaluator." Not traced: the causes are guesses.

**Earlier (2026-11-16), the story as first opened:**

Shayne, 2026-11-16, while pricing a vanilla 1/1 as a removal target (AI-26): "if we price power / toughness at about
0.4 of a card, i.e. a 1/1 is worth 0.4, a 2/2 is woth 0.8, Then we also have to discount cards that are high power
but low toughness (i.e. a 7/2 and a 5/2 are not that much different (since they both die to 2/2s and the same damage
based removal) But how do our evaluator scores currently work? I think we just set arbitrary numbers before right?
Would it make sense to make it more "normalized" so that a value of 1 = 1 card? That way it would be easier to
evaluate states from a human side (i.e. if I am +1 over my opponent, i am basically up a card from him). But that
also seems like a different story altogether, semi related to this specific scenario, but it still doesn't solve
options pricing."

**Today (read from `Evaluator.cs`, 2026-11-16).** Hand-chosen points, adjusted on positions and matches, consistent
with each other but anchored to nothing. An average measured card is 8.5 points (`OptionReport.ACard`: FRA 8.97,
TRK 8.17). A creature is 2 + 1.5 × power + toughness + 0.8 a keyword (`Evaluator.Body`): a vanilla 1/1 4.5 (0.53 of
a card), a 2/2 7.0 (0.82), a 3/3 9.5 (1.12), a 5/2 11.5 and a 7/2 14.5 (1.35 and 1.71). A land in play 1.5 (0.18), a
point of life 1.0 to 20 (0.12), a card in hand half its measured value.

Against his scale: the middle already agrees (a 2/2 is 0.82 for his 0.8); the flat 2 makes a 1/1 too dear (0.53 for
0.4) and big bodies too cheap; and power counts MORE than toughness, the opposite of his 7/2-against-5/2 point.

## Requirements

Not agreed — to shape with Shayne.

- [ ] A score reads in cards wherever a person reads it (the option report, the inspector, `--explain`).
- [ ] Creature bodies priced on his scale: about 0.4 of a card a point of power-and-toughness; power past what
      toughness can protect discounted (a 7/2 close to a 5/2).

## Decisions

- 2026-11-16, Shayne: a different story from option pricing (AI-26), "semi related".
- 2026-11-16, Shayne (AI-26): a vanilla 1/1, token or card, is 0.33 to 0.5 of a card; "for all other creatures it
  depends on the abilities it has."

- 2026-11-17, Shayne (asked, from AI-28.3's Clue, what a card is worth against life): "At 20 life, I would say 5 life is worth it to pay to draw a card as a baseline, but depending on the
  matchup it could be higher or lower. Obviously at lower life totals, this does not sustain. But also there is a
  difference between paying life to draw a card, and spending a card to gain life. In general spending a card to gain
  life is priced much higher, usually needing a lot of life (8+) before its actually worth an entire card and even then
  its situational. Obviously this also changes based on current life (more life is generally worth more the lower life
  total you are)".
  Claude, measured against today's evaluator: its life curve already has his shape — below 20 a point costs 1.0, above
  20 it gains 0.5, so 5 life paid at 20 is 5.0 and 8–10 life gained is 4.0–5.0 — IF a card is ~5. But an unseen card
  is 2.65 to it (an unseen spell 4.38; held cards at half their measured value, lands mixed in): it values 5 life at
  ~2 cards. So by his rule the gap is on the CARD side, not life's.

- 2026-11-17, Shayne, on the proposal's five questions (`docs/findings/evaluator-pricing.md`): value is "context
  dependant … kind of what we are already doing with the lookahead": "Drawing 2 cards when you are dead next turn is
  obviously not a good play. But divinaton is always worth 2 cards for its cost."
  1. "A raw drawn card is always worth 1 card. Once a card is known to be seen (i.e. through surveil or unsummon, its
     value is determined by its actual game value), but at the same time, even if you draw a card, the value of
     drawing a card should not just be based on the card you draw, but by the fact that you are technically still
     'digging' through your deck even if you have not found a good card yet … This is something that I feel we don't
     have currently and is causing some misevluation from the AI on wehn to draw a card (expecially with jace tokens)."
  2. "A Draw 2 is +2 without any context, but with the context of a look ahead, it might not be the best play. This is
     what we have to try to figure out, is how do we balance this."
  3. "A token's tempo is what it adds to the board, it should work exactly the same as any other creature or permanent
     you might play from your hand."
  4. Lands in play as tempo or cards: "I don't want to say for sure, I think we should discuss this further."
  5. "lands are a tricky case as usually you want enough lands to do exactly what you need to do, but they don't have
     much additional value after that … on a turn when you might want to double spell you might want a land just for
     that turn, but afterwards no. So lets discuss this." (The exchange rate: not committed.)

- 2026-11-17, Shayne, on tempo by what a permanent adds to the board (a vanilla body's mana): "A 2/2 historically has
  been worth about 1 card and usually costs 2 mana. That might change from format to format depending on the speed and
  power level of the format … So yes deploying 2 cards that are 2/2's is more or less worth 4 mana. But sure we can use
  the 2/2 for 2 3/3 for 3 starting point, this might have to be adjusted on a format to format basis though."
- 2026-11-17, Shayne, lands: "it is actually highly variable what a land is worth from deck to deck and even from game
  to game. Some games you draw a couple 5 drops and having 5 lands is super important. Some games you really want to
  be able to play 2 3 drops on turn 6, so even if you don't have 6 drops in hand you might still want 6 lands in play.
  Some games drawing 6 lands is kind of not ideal, especially if you are a low curve aggro deck whose curve stops at 4
  … So its not something we can just price in isolation and expect it to work amazingly."

- 2026-11-17, Shayne: not yet (no building). "in FRA a 2/2 for 2 was not usually worth it. There were 2/2's for 2 with
  additional abilities that were just not good enough due to how slow the format was. In these cases I am hoping that
  we could just modify the baseline setting to make a 2/2 for 2 not worth a full card, something like 0.8 or 0.7 of a
  card or something like that." So the per-format baseline is a body's worth in CARDS (FRA: a 2/2 0.7–0.8).

- 2026-11-18, Shayne, on step 2's library term: "A drawn card should be woth a drawn card and nothing more or less …
  The result of the drawn card should not impact the action of drawing a card if that makes sense." So the act of
  drawing is priced as one drawn card, whatever the look-ahead makes it - not by a library term that nets out to it.

- 2026-11-18, Shayne, on a drawn land the look-ahead plays: "You should never get an immediate loss for deploying a
  resource in hand, no matter what it is." On keeping the draw's credit wherever the card goes: "This might be the fix,
  but the issue is that there are scenarios wher eyou draw a bunch of cards but don't draw anything useful. Our
  eveluator should be able to value those scenarios accordingly (i.e. a bunch of lands in hand worth nothing could
  still mean you are behind your opponent even if you have theoretically more cards than them in total). So this is a
  tricky thing and something we hsould try to flehs out." Claude's proposal to flesh out (Next): a card the look-ahead
  DRAWS is a guess, priced as a drawn card; a card already in hand at the search's root is seen, priced by what it does.

- 2026-11-18, Shayne, on options against deploying: "spells like lighting bolt are evaluated differently than resources
  like creatures or mana. In general you aren't holding back your curve to get more value out of it later, you will play
  your 1 drop creature on turn 1, your 2 drop creature on turn 2 and your 3 drop creature on turn 3. Lightning Bolt is an
  answer card, so it gets evaluated based on how much value it 'stops' your opponent from getting … It killing a 1/1 is
  worth much less than it killing a 3/3 … So no we don't want to deploy every card asap, only the cards that make sense
  to do so. Options pricing does not only apply to spells, but we need to start somewhere and spells are the easiest
  divide. We could have options pricing on creatures as well, specifically ones with etb's … yoshimaru, where you
  probably don't want to play it on turn 2 as a 2 mana 1/1 with no value. Its better to wait and try to get a fight out
  of it". So "never a loss for deploying" holds for a card with no option; a card with one carries an explicit price.

- 2026-11-18, Shayne: "I would like to normalize a card to 1 if possible. It would just make things simpler to
  evaluate. I.e. a player that is +2 over his opponent means he is up 2 cards." Claude's proposal (for his answer): the
  anchor is the average measured card (8.5 points, where bodies already sit on his scale), shown in cards at once.

- 2026-11-18, Shayne, on `beam+drawncard` dropping the Jace token's −3 to deploy Thalia, the Survivor or Protege's
  Awakening: "this is not technically bad … If they still drew a card afterwards is still fine, not 100% ideal, but
  still fine."

- 2026-11-18, Shayne, reading `beam+drawncard`'s changes: seed 4 #75 (a Jace token at 1, an empty hand, 6 life) "Yes
  this is a clear surveil to try and draw some gas. No use keeping the token around." Seed 2 #38 (Prophesied End held,
  Thalia cast): "Yes this is pretty clear. The 3/4 thalia blocks their koth, and prophesized end is better used on an
  attacker anyways. So holding it makes sense." Seed 2 #23 (a Jace token at 1 kept): "why wasn't the konstari annex
  played sooner? It seems like a jace token is already out so it would have come into play untapped? If that was
  solved than surveiling looking to bin extra lands is the clear play. Now though you need the jace for the annex to
  come into play untapped next turn so you can surveil here." Traced: on turn 6, holding Rockfall Vale ("enters tapped
  unless you control two or more other lands") and Konstrari Annex ("enters tapped unless you control a planeswalker"),
  with the token in play, it played the Vale - whose condition holds from then on anyway (AI-9.10).

- 2026-11-18, Shayne, on the three `beam+drawncard` positions: he agrees seed 2 #16 (Divining Duelist's loot over
  Mindseeker Oculus and the −3) and seed 1 #79 (the token at 9 surveils where it drew) are misplays; seed 4 #43 (Konstrari
  Charm binned) "is not entirely clear". The match was run (`--match=beam+drawncard,beam --until-clear`).

- 2026-11-18, Shayne: `beam+drawncard` the DEFAULT ("I wanted the switch on not off"); off is `beam+nodrawncard`.

Open questions:
- Show in cards only (divide by 8.5 when printing — no play changes), or rescale the evaluator itself (every
  constant, threshold, table and test number moves; no decision changes if done exactly)? Claude's lean: show first.
- The fragile discount: by what rule — power counted only up to some multiple of toughness?
- Lands and life in cards (0.18 and 0.12 today): are those his numbers too?

## Tasks

- [ ] AI-27.1 The interview: the open questions above, and the proposal's five (findings, `evaluator-pricing.md`)
- [ ] AI-27.2 Scores shown in cards where a person reads them
- [ ] AI-27.3 The body on his scale behind a switch: positions, suite, match
- [x] AI-27.4 The inventory: every price, its formula, unit, story and ledger; every seen-against-unseen comparison
- [x] AI-27.5 His principles as tests (`PricingPrincipleTests`): 8 pass, 4 fail (`[Explicit]`, the target)
- [ ] AI-27.6 The three-ledger design, agreed with Shayne — then its five steps, each behind a switch
- [x] AI-27.7 A drawn card worth a drawn card, whatever it turns out to be (Shayne, Decisions 2026-11-18) - the
      default since 2026-11-18 (off: beam+nodrawncard); its tests in `PricingPrincipleTests`; Log 2026-11-18.

## Next

**Now (2026-11-18): AI-27.7 is the DEFAULT** (Shayne; off: `beam+nodrawncard`) - the suite's committed answers
re-accepted (20 changed). Next: plan step 1, one card scale - seed 2 #16 and seed 1 #79 (misplays he agrees with) and
seed 4 #43 (unclear) are its positions. Seed 2 #23's land order is AI-9.10.

**Handoff (2026-11-17, second session).** Read `docs/findings/evaluator-pricing.md`: the inventory (§1), the
principle tests and what they found (§2), the three-ledger proposal (§3) and five questions for Shayne. Nothing in
the AI changed. **Waiting on Shayne:** his read of the proposal and the five questions — no code before that.
What the tests found, in one line each:
- Seen and unseen cards are on different scales: our seen card averages 1.52 against an unseen draw's 2.54 (an
  unseen draw is never short on mana or colour, a seen one is), theirs 3.76 known against 2.65 unseen.
- A draw-2 that hits two lands late scores 0.10 against two unseen cards' 5.09 — the library's draws still to come
  are not priced (the missing half).
- 5 life at 20 is 1.96 cards by the evaluator's own unseen card — the card side is low, as found before.
- Drawing now, a bounce as the recast, a 3/2 cast or traded rather than held: these pass today.
Then, once agreed, §3's order: one card scale first (the seen/unseen tests turn green, nothing else moves). The five
draw-or-board positions (findings, "AI-27") still wait for his read. AI-26.8 is still this story's first piece.
(The first session's trial switch `beam+unseenfull` was removed before commit: it priced cards on two scales, so its
odd plays may be its own artifacts.)

## Log

- 2026-11-18: `DrawnCard` made the default (Shayne); `--suite=beam --accept`: 20 answers changed, oracle 76.7%, score
  given up 0.56, wins missed 2, 19 944 engine steps a position.

- 2026-11-18: /pre-commit on AI-27.7: the switch now applies under `LandOrNot` alone (another library mode ignores it -
  it had dropped the surveil bonus there with no look credit), and `Explain` reads the root `Decide` read. Five
  ceilings left for the switch, in DesignNotes ("MagicCore known ceilings"): a searched or landcycled land counts as a
  drawn guess; a guessed spell cast loses its credit; the opponent's own view prices its guesses alike; the rollout's
  later looks earn no credit; the walker floor's removal is not monotonic at a lifetime of 0, and the option price's
  fixture keeps the floor. The default search is unchanged.

- 2026-11-18: MATCH `--match=beam+drawncard,beam --until-clear`: 384 games (6 pairings x 32 seeds x both seats), 84 min,
  **beam+drawncard 51.0% ± 1.5pp (196-188)** - early stop: no 5pp difference either way. Cost 10 128 against 9 865
  engine steps a decision (+2.7%); 63 against 62 decisions a game; lands 7.3 and spells 9.6 each, alike. By its deck:
  Red-Green 89-39, White-Black 76-52, White-Blue Jace 31-97.

- 2026-11-18: `beam+drawncard` gained two parts, both under the switch only: each look at an unseen card credited
  `SurveilShare` of a drawn card (0.87) in place of the pending-surveil bonus - alone it fixed nothing at seed 4 #75 and
  turned seed 2 #60 (a Jace token at 6, Island in hand) from Island-then-−3 into −1 (traced: a three-way near tie,
  Island 9.34 / −3 9.02 / −1 8.69 off, 8.67 / 8.09 / 8.67 on - every drawn card now 2.65 where the spell version had
  4.38); and no walker floor of 2 for a walker its lifetime prices (a Jace token at 1 was 2.10, its one Surveil 1 to
  come ~0.6). With both, suite: 20 changed; oracle 76.7% (default 76.0%); score given up 0.56 (0.71); wins missed 2 (3);
  engine steps 19 944 (20 132). Seeds 4 #75, 2 #23, 2 #60 back to the committed answers. New: a token at 1 surveils
  BEFORE deploying the same cards (seed 2 #38 W-B, #52; seed 3 #64; seed 4 #43), or after combat (seed 3 #25); seed 1
  #79 Tam's Resistance's counter now on our Mindseeker Oculus, not their Rampart Hunter, but the token at 9 then
  surveils where it drew; seed 2 #16 (turn 5) Divining Duelist cast to loot an Island where it cast Mindseeker Oculus
  and drew with the new token; seed 4 #43 binned Konstrari Charm (a real spell, priced at half its measured value,
  below a drawn card's 2.65 - the two scales, X2).

- 2026-11-18: the 15 `beam+drawncard` changes read with their boards (`--boards`, 0.7 min): SEVEN are the same turn in
  another order - three now draw with the Jace token's −3 before deploying (seed 2 #28, #66, #71: better, the card
  seen first), two after (seed 3 #28 Thalia, #67 Protege's Awakening: both still draw - Shayne's "fine"), two timing
  only (seed 3 #113; seed 4 #53, Fatehold Charm still bounces the Cadet token that turn). FIVE are a Jace token at 1-2
  spending its last loyalty on Surveil 1 or not: one now surveils (seed 1 #24), four now keep it (seed 2 #23, seed 3
  #16 and #43 - each with Way of the Mentor or Mindseeker Oculus in hand to empower it - and seed 4 #75, an empty hand
  at 6 life). TWO turn-1 Plains → Swamp (seed 4 #2, both seats: two Swamps, a Plains, black and white spells - either
  is fine). ONE removal held: seed 2 #38, Prophesied End ("Destroy target creature. If it wasn't attacking, its
  controller draws a card") no longer cast on a tapped Koth in our draw step - the −3 drew
  Thalia, which it cast instead. Note: the look-ahead cannot value drawing BEFORE deploying (a guessed spell can't be
  cast), so draw-first against draw-last is a near tie it can't see.

- 2026-11-18: AI-27.7 built behind `beam+drawncard` (off by default): a card of ours the look-ahead draws - one in
  our library at the root, its place unknown - is priced in hand as a drawn card (`HeldValues.Unknown`, 2.65),
  whatever the look-ahead made it; a land of them played keeps its credit over a land in hand. Cards in hand at the
  root are priced as before. Tests: the Jace token at 6 on three Forests, land version - the drawn land 0.05 → 2.65,
  the −3 6.85 against banking 7.64 → 9.45; playing the drawn land still gains; a seen hand of useless lands
  unchanged. Full suite 1612 passed. Position suite (288): 15 decisions change; oracle agreement 76.0% both; score
  given up 0.63 a position against the default's 0.71; wins missed 4 against 3; engine steps 20 272 against 20 132.
  Changed: Jace −3 now taken at four positions (over Pass, Fatehold Annex, Mindseeker Oculus, Ajani's Anguish X=1);
  −1 dropped at five (for Pass or a land); −3 dropped twice for a creature or Aura (Thalia, the Survivor; Protege's
  Awakening); a turn-1 Plains now a Swamp twice; two removal spells now held (Prophesied End on Koth, the Geomancer;
  Fatehold Charm's bounce on a Cadet).

- 2026-11-17: Shayne answered the five questions (Decisions): a raw draw is 1 card plus the digging; a seen card its game
  value; the context is the look-ahead's; a token's tempo as any permanent's; lands to discuss.

- 2026-11-17 (second session): the inventory and the principle tests (AI-27.4, 27.5), and the three-ledger
  proposal for discussion — all in `docs/findings/evaluator-pricing.md`. Read-only for the AI; one test file added.

- 2026-11-17: Shayne: "Yeah lets try raising the value of a card towards 5 points, this might just be a raw value
  thing." Tried with the existing held fraction (`beam-held=X`; an unseen card is LandShare × 0.3 + the rest × the
  average measured spell × X). Measured, nothing changed in the code:

  | held fraction | unseen card | #364 (Raffi Musiker: life or a Clue) | suite changed | oracle agrees | engine steps |
  |---|---|---|---|---|---|
  | 0.5 (default) | 2.65 | gains 2 life (7.93 / 6.43) | — | 76.0% | 20 132 |
  | 0.75 | ~3.9 | gains 2 life (6.82 / 6.50) | 18 of 288 | 75.0% | 19 575 |
  | 1.0 | ~5.2 | **investigates** (7.65 / 5.70) | 32 of 288 | 70.5% | 18 792 |

  At 1.0: 9 of the 32 now pass where they played (Jace −3 thrice, Mindseeker Oculus twice), 5 play where they passed,
  18 play something else. AI-1 (2026-10-16) measured 1.0 hoarding and losing 26–46 — before most of today's search.
  **The match** (`--match=beam-held=1.0,beam --until-clear`, 600 games, 155 min): **47.5% ± 1.4pp (285-315)**,
  undecided at the cap (no 5pp difference; the estimate about −2.5pp). Per game it cast as many spells (9.8 against
  9.7) and played as many lands (7.5 / 7.4) — **not hoarding**. By deck: Red-Green 130-70, White-Black 103-97,
  White-Blue Jace 52-148; 9 463 engine steps a decision against 9 947. Shayne's reading: drawing over-valued against
  board presence ("I could see the AI maybe choosing to play card draw spells over getting board presence") — what the
  spells are, not how many; to test by raising unseen cards (draws) and cards in hand apart.

- 2026-11-16: created from Shayne's question during AI-26.
