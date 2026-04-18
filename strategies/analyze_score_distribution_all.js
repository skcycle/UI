#!/usr/bin/env node
const fs = require('fs');
const path = require('path');

const dates = ['2026-04-13', '2026-04-14', '2026-04-15'];
const dimensions = [
  { key: 'sector_heat', file: 'sector_heat_scored', field: 'sector_heat_score', max: 20 },
  { key: 'fundamental_momentum', file: 'fundamental_momentum_scored', field: 'fundamental_10', max: 10 },
  { key: 'volume_price_sync', file: 'volume_price_sync_scored', field: 'score15', max: 15 },
  { key: 'kline_pattern', file: 'kline_pattern_scored', field: 'score15', max: 15 },
  { key: 'technical_indicators', file: 'technical_scored', field: 'tech_15', max: 15 },
  { key: 'main_moneyflow', file: 'main_moneyflow_scored', field: 'main_money_15', max: 15 },
  { key: 'emotion', file: 'emotion_scored', field: 'emotion_10', max: 10 },
];

const allScores = {};
for (const d of dimensions) allScores[d.key] = [];

for (const date of dates) {
  for (const dim of dimensions) {
    const p = path.join(__dirname, '..', 'output', `${dim.file}_${date}.json`);
    if (!fs.existsSync(p)) {
      console.error(`missing: ${p}`);
      continue;
    }
    const obj = JSON.parse(fs.readFileSync(p, 'utf8'));
    const items = obj.items || [];
    for (const item of items) {
      const v = item[dim.field];
      if (typeof v === 'number') allScores[dim.key].push(v);
    }
  }
}

function stats(arr) {
  if (!arr.length) return { n: 0, min: null, max: null, mean: null, std: null, p25: null, p50: null, p75: null };
  const sorted = [...arr].sort((a, b) => a - b);
  const n = arr.length;
  const min = sorted[0];
  const max = sorted[n - 1];
  const mean = arr.reduce((a, b) => a + b, 0) / n;
  const std = Math.sqrt(arr.reduce((s, x) => s + (x - mean) ** 2, 0) / n);
  const p25 = sorted[Math.floor(n * 0.25)];
  const p50 = sorted[Math.floor(n * 0.5)];
  const p75 = sorted[Math.floor(n * 0.75)];
  return { n, min, max, mean, std, p25, p50, p75 };
}

console.log('维度得分分布分析 (4/13~4/15 全部候选股)\n');
console.log('维度'.padEnd(22), 'n', 'max', 'min', 'mean', 'std', 'p25', 'p50', 'p75', '区分度评价');
console.log('-'.repeat(110));

for (const dim of dimensions) {
  const s = stats(allScores[dim.key]);
  const range = s.max - s.min;
  const maxPossible = dim.max;
  const usedRatio = range / maxPossible;
  const cv = s.mean ? s.std / s.mean : 0;
  
  let eval_ = '';
  if (usedRatio < 0.2) eval_ = '❌ 区分度很低';
  else if (usedRatio < 0.4) eval_ = '⚠️ 区分度偏低';
  else if (usedRatio > 0.7) eval_ = '✅ 区分度好';
  else eval_ = '○ 正常';
  
  // 检查普遍低分/高分
  if (s.mean < maxPossible * 0.25) eval_ += ' | 普遍低分';
  else if (s.mean > maxPossible * 0.7) eval_ += ' | 普遍高分';
  
  // 检查集中
  if (cv < 0.15) eval_ += ' | 分数集中';
  
  console.log(
    dim.key.padEnd(22),
    String(s.n).padStart(4),
    s.max.toFixed(1).padStart(5),
    s.min.toFixed(1).padStart(5),
    s.mean.toFixed(2).padStart(6),
    s.std.toFixed(2).padStart(5),
    s.p25.toFixed(1).padStart(5),
    s.p50.toFixed(1).padStart(5),
    s.p75.toFixed(1).padStart(5),
    eval_
  );
}

console.log('\n说明:');
console.log('- n: 样本数');
console.log('- max/min/mean: 该维度的最高/最低/平均分');
console.log('- std: 标准差，越大说明分数越分散');
console.log('- p25/p50/p75: 25%/50%/75%分位数');
console.log('- 区分度评价基于 (max-min)/理论最大值');
