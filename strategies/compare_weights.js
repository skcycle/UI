#!/usr/bin/env node
/**
 * 使用新权重对今天（2026-04-16）的股票进行评分
 * 并与老权重对比
 */

const fs = require('fs');
const path = require('path');

// 老权重（等权）
const oldWeights = {
  sector_heat: 1,
  fundamental_momentum: 1,
  volume_price_sync: 1,
  kline_pattern: 1,
  technical_indicators: 1,
  main_moneyflow: 1,
  emotion: 1,
};

// 新权重（基于回测相关性）
// 注意：emotion 是负相关，所以作为惩罚项
const newWeights = {
  sector_heat: 0.17,
  fundamental_momentum: 0,
  volume_price_sync: 0.076,
  kline_pattern: 0.187,
  technical_indicators: 0.282,
  main_moneyflow: 0.002,
  emotion: -0.283, // 负相关，作为惩罚项
};

// 理论最大分
const maxScores = {
  sector_heat: 20,
  fundamental_momentum: 10,
  volume_price_sync: 15,
  kline_pattern: 15,
  technical_indicators: 15,
  main_moneyflow: 15,
  emotion: 10,
};

function normalizeScore(score, max) {
  return score / max;
}

function computeTotalScore(scores, weights) {
  let total = 0;
  for (const [dim, weight] of Object.entries(weights)) {
    const score = scores[dim] || 0;
    const max = maxScores[dim];
    const normalized = normalizeScore(score, max);
    total += normalized * weight;
  }
  // 映射到 0-100 范围
  return Math.round(total * 100 * 100) / 100;
}

function main() {
  const date = '2026-04-16';
  const p = path.join(__dirname, '..', 'output', `to_test_${date}.json`);
  const obj = JSON.parse(fs.readFileSync(p, 'utf8'));
  const items = obj.backtest_targets || [];
  
  console.log('=== 新老权重对比 (2026-04-16 Top10) ===\n');
  console.log('老权重: 等权 (每个维度权重=1)');
  console.log('新权重: 基于回测相关性');
  console.log('  - technical_indicators: 28.2%');
  console.log('  - emotion: -28.3% (负相关，惩罚项)');
  console.log('  - kline_pattern: 18.7%');
  console.log('  - sector_heat: 17.0%');
  console.log('  - volume_price_sync: 7.6%');
  console.log('  - main_moneyflow: 0.2%');
  console.log('  - fundamental_momentum: 0%\n');
  console.log('-'.repeat(80));
  console.log('排名 | 代码 | 名称 | 老总分 | 新总分 | 变化');
  console.log('-'.repeat(80));
  
  const results = items.map((item, idx) => {
    const oldTotal = item.total_score;
    const newTotal = computeTotalScore(item.scores, newWeights);
    return {
      rank: idx + 1,
      ts_code: item.ts_code,
      mc: item.mc,
      oldTotal,
      newTotal,
      change: newTotal - (oldTotal / 100 * 70), // 粗略对比
    };
  });
  
  // 按新总分排序
  results.sort((a, b) => b.newTotal - a.newTotal);
  
  for (const r of results) {
    const changeStr = r.change > 0 ? `+${r.change.toFixed(1)}` : r.change.toFixed(1);
    console.log(`${r.rank} | ${r.ts_code} | ${r.mc.padEnd(8)} | ${r.oldTotal.toFixed(1).padStart(5)} | ${r.newTotal.toFixed(1).padStart(5)} | ${changeStr}`);
  }
  
  // 输出新权重下的 Top10
  console.log('\n=== 新权重下的 Top10 ===');
  console.log('-'.repeat(80));
  
  const newResults = results.slice(0, 10).map((r, idx) => ({
    ...r,
    newRank: idx + 1,
  }));
  
  for (const r of newResults) {
    console.log(`${r.newRank}. ${r.ts_code} ${r.mc}: ${r.newTotal.toFixed(1)} (老排名: ${r.rank})`);
  }
  
  // 保存结果
  const outPath = path.join(__dirname, '..', 'output', `to_test_${date}_new_weights.json`);
  fs.writeFileSync(outPath, JSON.stringify({
    date,
    weights: { old: oldWeights, new: newWeights },
    results: newResults,
  }, null, 2));
  
  console.log(`\n结果已保存到: ${outPath}`);
}

main();
