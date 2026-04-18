#!/usr/bin/env node
/**
 * 使用新权重对今天（2026-04-16）的股票进行评分
 * 并与老权重对比
 * 
 * 老权重计算方式：total_score = sum(各维度分数)
 * 新权重计算方式：total_score = sum(各维度分数 * 权重)
 */

const fs = require('fs');
const path = require('path');

// 理论最大分（用于归一化显示）
const maxScores = {
  sector_heat: 20,
  fundamental_momentum: 10,
  volume_price_sync: 15,
  kline_pattern: 15,
  technical_indicators: 15,
  main_moneyflow: 15,
  emotion: 10,
};

// 计算各维度最大总分
const totalMax = Object.values(maxScores).reduce((a, b) => a + b, 0); // 100

// 老权重（等权，权重=1）
const oldWeights = {
  sector_heat: 1,
  fundamental_momentum: 1,
  volume_price_sync: 1,
  kline_pattern: 1,
  technical_indicators: 1,
  main_moneyflow: 1,
  emotion: 1,
};

// 新权重（基于30只股票回测相关性，归一化后保留正负号）
// 原始权重：sector_heat=-27.2%, FM=30.1%, VP=1.2%, KP=12.5%, TI=-10.8%, MF=6.5%, EM=11.6%
// 归一化方法：将权重映射到 -1 ~ +1 范围
const newWeights = {
  sector_heat: -0.5,         // 负相关，惩罚项
  fundamental_momentum: 1.5, // 最高正相关
  volume_price_sync: 0.1,    // 几乎无相关
  kline_pattern: 0.5,        // 正相关
  technical_indicators: -0.3, // 负相关，惩罚项
  main_moneyflow: 0.2,       // 弱正相关
  emotion: 0.4,              // 正相关
};

function computeTotalScore(scores, weights) {
  let total = 0;
  for (const [dim, weight] of Object.entries(weights)) {
    const score = scores[dim] || 0;
    total += score * weight;
  }
  return Math.round(total * 100) / 100;
}

function main() {
  const date = '2026-04-16';
  const p = path.join(__dirname, '..', 'output', `to_test_${date}.json`);
  const obj = JSON.parse(fs.readFileSync(p, 'utf8'));
  const items = obj.backtest_targets || [];
  
  console.log('=== 新老权重对比 (2026-04-16 Top10) ===\n');
  console.log('老权重: 等权 (每个维度权重=1)');
  console.log('新权重: 基于回测相关性调整');
  console.log('  - fundamental_momentum: 1.5 (正相关，奖励)');
  console.log('  - sector_heat: -0.5 (负相关，惩罚)');
  console.log('  - kline_pattern: 0.5');
  console.log('  - emotion: 0.4');
  console.log('  - technical_indicators: -0.3 (负相关，惩罚)');
  console.log('  - main_moneyflow: 0.2');
  console.log('  - volume_price_sync: 0.1\n');
  console.log('-'.repeat(100));
  console.log('排名 | 代码       | 名称       | 老总分 | 新总分 | 变化    | FM  | SH   | KP  | EM  | TI  ');
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
    
    const s = r.scores;
    const scoreStr = `${String(s.fundamental_momentum?.toFixed(0)||0).padStart(2)} | ${String(s.sector_heat?.toFixed(1)||0).padStart(4)} | ${String(s.kline_pattern?.toFixed(0)||0).padStart(2)} | ${String(s.emotion?.toFixed(0)||0).padStart(2)} | ${String(s.technical_indicators?.toFixed(0)||0).padStart(2)}`;
    
    console.log(`${String(newRank).padStart(2)} | ${r.ts_code} | ${r.mc.padEnd(8)} | ${r.oldTotal.toFixed(1).padStart(5)} | ${r.newTotal.toFixed(1).padStart(5)} | ${changeStr.padStart(5)} | ${scoreStr}`);
  }
  
  // 输出新权重下的 Top10
  console.log('\n=== 新权重下的 Top10 ===');
  console.log('-'.repeat(80));
  
  const newResults = results.slice(0, 10);
  for (const r of newResults) {
    console.log(`${r.newRank}. ${r.ts_code} ${r.mc}: ${r.newTotal.toFixed(1)} (老排名: ${r.oldRank}, 老总分: ${r.oldTotal.toFixed(1)})`);
  }
  
  // 分析排名变化最大的股票
  console.log('\n=== 排名变化分析 ===');
  
  const bigRisers = results.filter(r => r.oldRank - r.newRank >= 3);
  const bigFallers = results.filter(r => r.newRank - r.oldRank >= 3);
  
  if (bigRisers.length > 0) {
    console.log('\n上升≥3名:');
    for (const r of bigRisers) {
      const s = r.scores;
      console.log(`  ${r.ts_code} ${r.mc}: ${r.oldRank}→${r.newRank}`);
      console.log(`    FM=${s.fundamental_momentum?.toFixed(1)} (权重1.5), SH=${s.sector_heat?.toFixed(1)} (权重-0.5)`);
    }
  }
  
  if (bigFallers.length > 0) {
    console.log('\n下降≥3名:');
    for (const r of bigFallers) {
      const s = r.scores;
      console.log(`  ${r.ts_code} ${r.mc}: ${r.oldRank}→${r.newRank}`);
      console.log(`    FM=${s.fundamental_momentum?.toFixed(1)} (权重1.5), SH=${s.sector_heat?.toFixed(1)} (权重-0.5)`);
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
