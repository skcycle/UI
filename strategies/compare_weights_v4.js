#!/usr/bin/env node
/**
 * 使用新权重对今天（2026-04-16）的股票进行评分
 * 并与老权重对比
 * 
 * 老权重计算方式：total_score = sum(各维度分数)，每个维度权重=1
 * 新权重计算方式：total_score = sum(各维度分数 * 权重)
 * 
 * 权重设置原则：
 * - 正权重总和 ≈ 7（和老权重一致）
 * - 负权重作为惩罚项
 */

const fs = require('fs');
const path = require('path');

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

// 新权重（基于回测相关性调整）
// 设计原则：正权重总和 ≈ 7，负权重作为惩罚项
const newWeights = {
  fundamental_momentum: 2.0,   // 最高正相关，权重最高
  kline_pattern: 1.2,          // 正相关
  emotion: 1.0,                // 正相关
  main_moneyflow: 0.8,         // 弱正相关
  volume_price_sync: 0.5,      // 几乎无相关，低权重
  sector_heat: -0.8,           // 负相关，惩罚项
  technical_indicators: -0.5,  // 负相关，惩罚项
};

// 正权重和：2.0 + 1.2 + 1.0 + 0.8 + 0.5 = 5.5
// 负权重和：-0.8 + (-0.5) = -1.3

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
  console.log('老权重: 等权 (每个维度权重=1，总和=7)');
  console.log('新权重: 基于回测相关性调整');
  console.log('  正权重:');
  console.log('    - fundamental_momentum: 2.0 (最高正相关)');
  console.log('    - kline_pattern: 1.2');
  console.log('    - emotion: 1.0');
  console.log('    - main_moneyflow: 0.8');
  console.log('    - volume_price_sync: 0.5');
  console.log('  负权重(惩罚项):');
  console.log('    - sector_heat: -0.8 (负相关)');
  console.log('    - technical_indicators: -0.5 (负相关)\n');
  console.log('-'.repeat(100));
  console.log('排名 | 代码       | 名称       | 老总分 | 新总分 | 变化    | FM   | SH    | KP  | EM  | TI  ');
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
    const scoreStr = `${String(s.fundamental_momentum?.toFixed(1)||'0').padStart(4)} | ${String(s.sector_heat?.toFixed(1)||'0').padStart(5)} | ${String(s.kline_pattern?.toFixed(0)||0).padStart(2)} | ${String(s.emotion?.toFixed(0)||0).padStart(2)} | ${String(s.technical_indicators?.toFixed(0)||0).padStart(2)}`;
    
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
      console.log(`    FM=${s.fundamental_momentum?.toFixed(1)} (权重2.0), SH=${s.sector_heat?.toFixed(1)} (权重-0.8)`);
    }
  }
  
  if (bigFallers.length > 0) {
    console.log('\n下降≥3名:');
    for (const r of bigFallers) {
      const s = r.scores;
      console.log(`  ${r.ts_code} ${r.mc}: ${r.oldRank}→${r.newRank}`);
      console.log(`    FM=${s.fundamental_momentum?.toFixed(1)} (权重2.0), SH=${s.sector_heat?.toFixed(1)} (权重-0.8)`);
    }
  }
  
  // 计算总分范围
  const oldRange = { min: Math.min(...items.map(i => i.total_score)), max: Math.max(...items.map(i => i.total_score)) };
  const newRange = { min: Math.min(...results.map(r => r.newTotal)), max: Math.max(...results.map(r => r.newTotal)) };
  
  console.log('\n=== 分数范围对比 ===');
  console.log(`老权重总分范围: ${oldRange.min.toFixed(1)} ~ ${oldRange.max.toFixed(1)}`);
  console.log(`新权重总分范围: ${newRange.min.toFixed(1)} ~ ${newRange.max.toFixed(1)}`);
  
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
