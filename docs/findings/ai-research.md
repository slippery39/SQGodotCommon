# AI research — what others do about hidden cards and holding cards

Reading, not measurement: sources checked 2026-11-12 for AI-20 (the look-ahead's one guessed future) and the option
pricing AI-13 never finished. Each claim names its source; nothing here is measured in our engine yet.

## Where our AI stands (the problem, in our terms)

- **One guessed future per decision.** `Hidden.Sample` (MagicCore/Ai/Hidden.cs) shuffles every card whose place the AI
  doesn't know, once, at the search's root; the whole tree and every rollout play out that one deal. The scan's
  `--guesses=30` showed the cost: the same position decided differently across deals (the NOISE class, AI-19.23).
- **Option value is bolted on, because one fixed future has none.** In a single known future there is nothing to wait
  for — you either see the creature they'll play, or you see that they won't. So the search can't value holding by
  itself; AI-13.4 added it from outside: `CastGains` measures each instant's best "offer" per turn in self-play, and a
  held card is priced at the expected best of its next k offers (`Terms.HoldOption`). Its own finding: **every agreed
  position still passes with it forced off** — the bounce's value held them, not the option. AI-13.10 (every targeted
  card an option) was reverted: holding a sorcery-speed card costs a turn of tempo the offers never charge.
- So the two stories are one problem seen twice: **the expected value of acting now vs. holding only exists across
  several possible futures**, and our search looks at one.

## The published work on MTG specifically

**Cowling, Ward & Powley (2012), "Ensemble Determinization in Monte Carlo Tree Search for the Imperfect Information
Card Game Magic: The Gathering"**, IEEE TCIAIG — [paper](https://eprints.whiterose.ac.uk/75050/1/EnsDetMagic.pdf).
A cut-down MTG (creatures and lands), MCTS against a hand-tuned expert rules player that beat strong humans 63.7%.
- **Many guessed futures, one budget.** Several search trees from the same root, each with its own deal ("lazy
  determinization": a draw is fixed the first time the tree reaches it), their root-move results summed; the move
  with the most visits overall is played. **At a fixed total budget**, 10,000 simulations split as ~40 deals × 250
  was best (best range 20–100 deals); one deal with all 10,000 was clearly worse. CPU per move rose only slightly with
  more trees (0.62 s for one, 1.12 s for 50). **The cost is the same budget split, not multiplied** — relevant to AI-22.
- **"Interesting" deals.** A deal that makes the game a foregone win or loss teaches nothing; they reject deals where
  a quick playout's result doesn't depend on the move, and keep balanced ones. Effective.
- **Pruning dominated moves.** A move that plays a subset of another legal move's cards is dropped; a turn that
  could play a land and doesn't is dropped. Big gains for little knowledge.
- **Binary decisions.** "Play card A? yes/no, then B? …" instead of one node per subset of the hand — stronger and
  faster. (Our search already builds a turn move by move, which is close to this.)
- **The rollout policy matters most.** A deterministic expert policy in the rollouts was clearly WORSE than a weak
  rules policy with some randomness; uniformly random was very weak. Their best combination slightly beat the expert
  player; the expert's flaw per its human testers: "a little too cautious … did not act as aggressively as it might".

**Cowling, Powley & Whitehouse (2012), "Information Set Monte Carlo Tree Search"**, IEEE TCIAIG —
[paper](https://eprints.whiterose.ac.uk/75048/1/CowlingPowleyWhitehouse2012.pdf). The general method, tested on
three games (not MTG).
- **Determinization's two flaws** (Frank & Basin): *strategy fusion* — each guessed future is searched as if the AI
  could act differently in each, though it can't tell them apart; *non-locality* — some futures are made unlikely by
  what the other player would do. For holding a card this matters: in each guessed future the "hold" line gets to
  respond perfectly to what that future brings, so **averaging over guesses tends to overvalue holding**.
- **ISMCTS** searches one tree whose nodes are what the player can know (information sets), a new deal sampled each
  iteration — so a decision is one decision across all deals. It won where hidden information changes the right
  strategy or a deep search is needed; in Dou Di Zhu (a card game) it was only on par with determinization.
- Determinization's effectiveness is predictable from how fast hidden information is revealed and how much late
  decisions can still swing the game (Long et al., cited there).

## Forge (the open-source MTG engine)

Source read on GitHub (`Card-Forge/forge`, `forge-ai`), 2026-11-12:
- **The main AI does not search.** Each effect type has its own decision class (`SpellAbilityAi` subclasses: damage,
  pump, counters, …) answering "should I play this now?", with per-card hints written into card scripts (`AILogic`).
  Combat is `AiAttackController`/`AiBlockController`; card value `ComputerUtilCard`/`CreatureEvaluator`.
- **Holding is a first-class answer, with a reason.** `AiPlayDecision` returns not only "play" or "don't" but **when**:
  `WaitForCombat`, `WaitForMain2`, `WaitForEndOfTurn`, `StackNotEmpty`, `AnotherTime` — and why it plays
  (`Removal`, `Tempo`, `CardAdvantage`, `ImpactCombat`, `AddBoardPresence`) or doesn't (`DoesntImpactGame`,
  `IncreasesLifeInDanger`).
- **Option pricing is rules and dice, from a profile** (`forge-gui/res/ai/Default.ai`): hold combat tricks until
  blockers 65% of the time; hold the land drop for main 2; hold an X burn spell for more damage below X = 5;
  counter a 1-drop 30%, a 2-drop 75%, a 3+ always, removal and counters always; flash creatures cast as an ambush after
  attacks, for their enters, or as a surprise blocker; trade attacks only at lower life pressure.
- **Its experimental simulation AI** (`forge-ai/.../simulation`) evaluates with **perfect information** (it reads every
  hand), plays the best-scoring ability if it beats doing nothing, and **compares acting now with acting after blockers**
  (`formulatePlanWithPhase`) — a plan that waits for a better phase. The score is 2 × life, hand size, creature values,
  lands by the mana they make.

## What this suggests for us (Claude's reading — for Shayne)

1. **Several guessed futures, the budget split** — Cowling et al.'s result, in our beam search: decide each root move
   over N deals (e.g. 8–30) with the rollout budget divided, and average its score. Holding then gains value where it
   should — in the deals where they play the creature — without a separate option term. It fits the principle already
   decided ("known used, unknown unknown"). Its known flaw (overvaluing holding) is smallest where a line's later
   choices are made by a policy that doesn't plan around the deal — to check in our search: its deeper levels do
   search within the deal, the rollout only plays it out.
2. **Land odds (A+, decided in principle)** — the cheap complement: where only the number of lands matters, a
   probability instead of a sample.
3. **"Interesting" deals** — reject guesses that make the position trivially won or lost; it would also tame the
   won-line ties (AI-19's item 8).
4. **The rollout policy with some randomness** — their strongest single lesson: a deterministic cheap policy in the
   rollouts made a stronger player play worse. Our rollout is deterministic.
5. **Forge's timing reasons** — "wait for combat / main 2 / end of turn" as named outcomes is close to what AI-13.12's
   `WaitOnTies` does; a Station or a flash creature (AI-23, AI-13.8) may be clearest as "do it now vs. at that stop".
6. **ISMCTS is not the next step:** a rewrite of the search, on par with determinization in the card game tested.

**Questions for AI-20.1 (for Shayne):** how many deals for how much budget (speed, AI-22); whether the option term
(`HoldOption`) stays beside it or is retired once measured redundant; land odds' details (which prices read them);
whether the opponent's unknown draws are dealt too; and what counts as done — the flip rate down, the kept positions
right, a match not worse.
