/**
 * 查看同板块内 sector_heat_score 的差异
 */
const fs = require('fs');
const p = process.argv[2] || '/root/.openclaw/workspace-score/output/sector_heat_scored_2026-04-13.json';
const j = JSON.parse(fs.readFileSync(p, 'utf8'));
const items = j.items || [];

const grp = new Map();
for (const it of items) {
  const hy = it.hy || '未知';
  if (!grp.has(hy)) grp.set(hy, []);
  grp.get(hy).push(it);
}

for (const [hy, arr] of Array.from(grp.entries()).sort((a, b) => b[1].length - a[1].length)) {
  arr.sort((a, b) => b.sector_heat_score - a.sector_heat_score);
  const scores = arr.map((x) => x.sector_heat_score);
  const min = Math.min(...scores);
  const max = Math.max(...scores);
  console.log(`HY ${hy}: n=${arr.length}, score_range=${min}~${max}`);
  console.log(
    ' top3:',
    arr.slice(0, 3).map((x) => ({ ts: x.ts_code, score: x.sector_heat_score, base: x.sector_heat_base, adj: x.sector_heat_adj })),
  );
  console.log(
    ' bot3:',
    arr.slice(-3).map((x) => ({ ts: x.ts_code, score: x.sector_heat_score, base: x.sector_heat_base, adj: x.sector_heat_adj })),
  );
}
