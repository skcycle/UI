#!/usr/bin/env node
const fs = require('fs');
const path = require('path');

const dates = ['2026-04-13', '2026-04-14', '2026-04-15'];
const dimensions = [
  'sector_heat',
  'fundamental_momentum', 
  'volume_price_sync',
  'kline_pattern',
  'technical_indicators',
  'main_moneyflow',
  'emotion'
];

const dimMax = {
  sector_heat: 20,
  fundamental_momentum: 10,
  volume_price_sync: 15,
  kline_pattern: 15,
  technical_indicators: 15,
  main_moneyflow: 15,
  emotion: 10
};

const allScores = {};
for (const d of dimensions) allScores[d] = [];

for (const date of dates) {
  const p = path.join(__dirname, '..', 'output', `to_test_${date}.json`);
  if (!fs.existsSync(p)) {
    console.error(`missing: ${p}`);
    continue;
  }
  const obj = JSON.parse(fs.readFileSync(p, 'utf8'));
  const items = obj.backtest_targets || [];
  for (const item of items) {
    for (const d of dimensions) {
      const v = item.scores && item.scores[d];
      if (typeof v === 'number') allScores[d].push(v);
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

console.log('维度得分分布分析 (4/13~4/15 Top10 合计30条)\n');
console.log('维度'.padEnd(22), 'max', 'min', 'mean', 'std', 'p25', 'p50', 'p75', '区分度评价');
console.log('-'.repeat(100));

for (const d of dimensions) {
  const s = stats(allScores[d]);
  const range = s.max - s.min;
  const maxPossible = dimMax[d];
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
    d.padEnd(22),
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
console.log('- max/min/mean: 该维度的最高/最低/平均分');
console.log('- std: 标准差，越大说明分数越分散');
console.log('- p25/p50/p75: 25%/50%/75%分位数');
console.log('- 区分度评价基于 (max-min)/理论最大值');
console.log('- 普遍低分: 平均分 < 理论最大值*25%');
console.log('- 普遍高分: 平均分 > 理论最大值*70%');
console.log('- 分数集中: 变异系数(std/mean) < 0.15');
