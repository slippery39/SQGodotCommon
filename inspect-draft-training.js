// Reads sim_results/draft_training.json and prints it the way the picker actually sees it.
//
//   node inspect-draft-training.js [path] [topN]
//
// Raw ratios in the JSON will mislead you on two counts, both corrected here:
//   1. A pair at 2-for-2 reads as 100%. Every rate is shrunk toward its baseline.
//   2. A pair containing a strong card looks great because the card is strong, not because
//      the two interact. Synergy is measured against the log-odds independence baseline —
//      what those two cards should do together on their own merits.
// Mirrors DraftTrainingData.Shrink / ExpectedPairRate and DraftPickers.Trained.

const fs = require("fs");

const path = process.argv[2] || "sim_results/draft_training_csc.json";
const topN = Number(process.argv[3] || 20);

if (!fs.existsSync(path)) {
  console.error(`No training data at ${path}. Run console mode 4 to generate it.`);
  process.exit(1);
}

const d = JSON.parse(fs.readFileSync(path, "utf8"));

const CARD_K = 25; // DraftPickers.Trained shrinkK
const PAIR_K = 200; // DraftPickers.Trained pairShrinkK

const prior = d.Wins / d.Perspectives;
const shrink = (wins, games, baseline, k) => (wins + k * baseline) / (games + k);
const logit = (p) => {
  const c = Math.min(Math.max(p, 1e-6), 1 - 1e-6);
  return Math.log(c / (1 - c));
};
const sigmoid = (x) => 1 / (1 + Math.exp(-x));
const logitPrior = logit(prior);

const rate = {};
const draw = {};
for (const c of d.Cards) {
  rate[c.Name] = shrink(c.Wins, c.Games, prior, CARD_K);
  draw[c.Name] = c.DeckGames > 0 ? c.Games / c.DeckGames : null;
}

const median = (xs) => {
  const s = [...xs].sort((a, b) => a - b);
  return s.length ? s[s.length >> 1] : 0;
};
const cardDraw = median(d.Cards.filter((c) => c.DeckGames > 0).map((c) => c.Games / c.DeckGames));
const pairDraw = median(d.Pairs.filter((p) => p.DeckGames > 0).map((p) => p.Games / p.DeckGames));
const pairDrawRatio = cardDraw > 0 && pairDraw > 0 ? pairDraw / cardDraw : 1;

// Expected pair rate under independence: each card's effect added in log-odds space.
const expectedPair = (a, b) => sigmoid(logitPrior + (logit(rate[a]) - logitPrior) + (logit(rate[b]) - logitPrior));

const pairs = d.Pairs.filter((p) => rate[p.A] !== undefined && rate[p.B] !== undefined).map((p) => {
  const expected = expectedPair(p.A, p.B);
  const actual = shrink(p.Wins, p.Games, expected, PAIR_K);
  return {
    name: `${p.A} + ${p.B}`,
    synergy: 100 * (actual - expected), // points above the independence baseline
    scaled: pairDrawRatio * 100 * (actual - expected), // what the picker adds per game
    actual: 100 * actual,
    expected: 100 * expected,
    raw: p.Games > 0 ? (100 * p.Wins) / p.Games : 0,
    n: p.Games,
  };
});

const pct = (x) => `${x.toFixed(1)}%`;
const pad = (s, w) => String(s).padEnd(w);

console.log(`\n${path}`);
console.log(`  ${d.Perspectives} deck-games  |  base win rate ${pct(100 * prior)}  |  ` + `${d.Cards.length} cards, ${d.Pairs.length} pairs`);
console.log(`  P(card drawn) ${cardDraw.toFixed(3)}  P(pair drawn) ${pairDraw.toFixed(3)}  ` + `ratio ${pairDrawRatio.toFixed(3)}`);
console.log(`  shrink k: cards ${CARD_K}, pairs ${PAIR_K}\n`);

function printPairs(title, rows) {
  console.log(title);
  console.log(`  ${pad("Pair", 46)}${pad("Synergy", 10)}${pad("Pair WR", 10)}` + `${pad("Expected", 10)}${pad("Raw WR", 9)}n`);
  for (const r of rows) {
    console.log(
      `  ${pad(r.name, 46)}${pad((r.synergy >= 0 ? "+" : "") + r.synergy.toFixed(2), 10)}` +
        `${pad(pct(r.actual), 10)}${pad(pct(r.expected), 10)}${pad(pct(r.raw), 9)}${r.n}`,
    );
  }
  console.log();
}

const bySynergy = [...pairs].sort((a, b) => b.synergy - a.synergy);
printPairs(`TOP ${topN} SYNERGIES (vs independence baseline)`, bySynergy.slice(0, topN));
printPairs(`BOTTOM ${topN} (anti-synergies)`, bySynergy.slice(-topN).reverse());

// The trap this script exists to avoid: sorting by raw pair win rate just re-derives
// "which cards are good", because a strong card lifts every pair it appears in.
const byRaw = [...pairs].sort((a, b) => b.actual - a.actual).slice(0, 5);
console.log("BY RAW PAIR WIN RATE (for contrast — mostly just 'contains a strong card')");
for (const r of byRaw) {
  console.log(`  ${pad(r.name, 46)}${pad(pct(r.actual), 10)}synergy ${r.synergy >= 0 ? "+" : ""}` + `${r.synergy.toFixed(2)}`);
}
console.log();

const cards = d.Cards.map((c) => ({
  name: c.Name,
  rate: 100 * rate[c.Name],
  n: c.Games,
  draw: draw[c.Name],
})).sort((a, b) => b.rate - a.rate);

console.log(`TOP 10 / BOTTOM 10 CARDS (shrunk games-in-hand win rate)`);
console.log(`  ${pad("Card", 30)}${pad("WR", 9)}${pad("P(drawn)", 11)}n`);
for (const c of [...cards.slice(0, 10), null, ...cards.slice(-10)]) {
  if (c === null) {
    console.log(`  ${"...".padEnd(30)}`);
    continue;
  }
  console.log(`  ${pad(c.name, 30)}${pad(pct(c.rate), 9)}` + `${pad(c.draw === null ? "n/a" : c.draw.toFixed(3), 11)}${c.n}`);
}
console.log();
