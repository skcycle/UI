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

// 新权重（基于30只股票回测相关性）
// 注意：sector_heat 和 technical_indicators 是负相关，作为惩罚项
const newWeights = {
  sector_heat: -0.272,      // 负相关，惩罚项
  fundamental_momentum: 0.301,  // 最高正相关
  volume_price_sync: 0.012,     // 几乎无相关
  kline_pattern: 0.125,         // 正相关
  technical_indicators: -0.108, // 负相关，惩罚项
  main_moneyflow: 0.065,        // 弱正相关
  emotion: 0.116,               // 正相关
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
  // 由于有负权重，可能产生负分，需要处理
  return Math.round(total * 100 * 100) / 100;
}

function main() {
  const date = '2026-04-16';
  const p = path.join(__dirname, '..', 'output', `to_test_${date}.json`);
  const obj = JSON.parse(fs.readFileSync(p, 'utf8'));
  const items = obj.backtest_targets || [];
  
  console.log('=== 新老权重对比 (2026-04-16 Top10) ===\n');
  console.log('老权重: 等权 (每个维度权重=1)');
  console.log('新权重: 基于30只股票回测相关性');
  console.log('  - fundamental_momentum: 30.1% (正相关)');
  console.log('  - sector_heat: -27.2% (负相关，惩罚项)');
  console.log('  - kline_pattern: 12.5%');
  console.log('  - emotion: 11.6%');
  console.log('  - technical_indicators: -10.8% (负相关，惩罚项)');
  console.log('  - main_moneyflow: 6.5%');
  console.log('  - volume_price_sync: 1.2%\n');
  console.log('-'.repeat(100));
  console.log('排名 | 代码       | 名称       | 老总分 | 新总分 | 变化    | 详细分数');
  console.log('-'.repeat(100));
  
  const results = items.map((item, idx) => {
    const oldTotal = item.total_score;
    const newTotal = computeTotalScore(item.scores, newWeights);
    return {
      oldRank: idx + 1,
      ts_code: item.ts_code,
      mc: item.mc,
      oldTotal,
      newTotal,
      scores: item.scores,
    };
  });
  
  // 按新总分排序
  results.sort((a, b) => b.newTotal - a.newTotal);
  
  for (let i = 0; i < results.length; i++) {
    const r = results[i];
    const newRank = i + 1;
    r.newRank = newRank;
    
    const rankChange = r.oldRank - newRank;
    const changeStr = rankChange > 0 ? `↑${rankChange}` : rankChange < 0 ? `↓${Math.abs(rankChange)}` : '-';
    
    // 简化详细分数显示
    const s = r.scores;
    const scoreStr = `FM=${s.fundamental_momentum?.toFixed(0)||0} SH=${s.sector_heat?.toFixed(1)||0} KP=${s.kline_pattern?.toFixed(1)||0} EM=${s.emotion?.toFixed(1)||0} TI=${s.technical_indicators?.toFixed(1)||0}`;
    
    console.log(`${String(newRank).padStart(2)} | ${r.ts_code} | ${r.mc.padEnd(8)} | ${r.oldTotal.toFixed(1).padStart(5)} | ${r.newTotal.toFixed(1).padStart(5)} | ${changeStr.padStart(5)} | ${scoreStr}`);
  }
  
  // 输出新权重下的 Top10
  console.log('\n=== 新权重下的 Top10 ===');
  console.log('-'.repeat(80));
  
  const newResults = results.slice(0, 10);
  for (const r of newResults) {
    console.log(`${r.newRank}. ${r.ts_code} ${r.mc}: ${r.newTotal.toFixed(1)} (老排名: ${r.oldRank})`);
  }
  
  // 分析排名变化最大的股票
  console.log('\n=== 排名变化分析 ===');
  
  const bigRisers = results.filter(r => r.oldRank - r.newRank >= 3);
  const bigFallers = results.filter(r => r.newRank - r.oldRank >= 3);
  
  if (bigRisers.length > 0) {
    console.log('\n上升≥3名:');
    for (const r of bigRisers) {
      const s = r.scores;
      console.log(`  ${r.ts_code} ${r.mc}: ${r.oldRank}→${r.newRank} (FM=${s.fundamental_momentum?.toFixed(0)||0}, SH=${s.sector_heat?.toFixed(1)||0})`);
    }
  }
  
  if (bigFallers.length > 0) {
    console.log('\n下降≥3名:');
    for (const r of bigFallers) {
      const s = r.scores;
      console.log(`  ${r.ts_code} ${r.mc}: ${r.oldRank}→${r.newRank} (FM=${s.fundamental_momentum?.toFixed(0)||0}, SH=${s.sector_heat?.toFixed(1)||0})`);
    }
  }
  
  // 保存结果
  const outPath = path.join(__dirname, '..', 'output', `to_test_${date}_new_weights.json`);
  fs.writeFileSync(outPath, JSON.stringify({
    date,
    weights: { old: oldWeights, new: newWeights },
    results: newResults,
    allResults: results,
  }, null, 2));
  
  console.log(`\n结果已保存到: ${outPath}`);
}

main();
