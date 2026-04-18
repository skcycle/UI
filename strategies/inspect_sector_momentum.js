#!/usr/bin/env node
const fs = require('fs');
const path = require('path');

const date = process.argv[2];
if (!date) {
  console.error('usage: inspect_sector_momentum.js YYYY-MM-DD');
  process.exit(1);
}
const p = path.join(__dirname, '..', 'output', `sector_heat_scored_${date}.json`);
const obj = JSON.parse(fs.readFileSync(p, 'utf8'));
const sectors = Object.entries(obj.sector_table || {}).map(([hy, v]) => ({
  hy,
  score: v.score,
  base: v.score_base,
  delta: v.score_delta12,
  mom: v.score_momentum,
}));
sectors.sort((a, b) => (b.score - a.score) || a.hy.localeCompare(b.hy));
console.log('top sectors (score, base, delta, mom):');
for (const s of sectors.slice(0, 15)) {
  console.log(`${s.hy}\t${s.score}\tbase=${s.base}\tdelta=${s.delta}\tmom=${s.mom}`);
}
