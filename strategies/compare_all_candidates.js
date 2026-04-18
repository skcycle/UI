#!/usr/bin/env node
/**
 * 对全部 60 只候选股进行新权重评分
 * 并与老权重对比
 */

const fs = require('fs');
const path = require('path');

// 新权重（基于回测相关性调整）
const newWeights = {
  fundamental_momentum: 2.0,   // 最高正相关
  kline_pattern: 1.2,
  emotion: 1.0,
  main_moneyflow: 0.8,
  volume_price_sync: 0.5,
  sector_heat: -0.8,           // 负相关，惩罚项
  technical_indicators: -0.5,  // 负相关，惩罚项
};

function computeTotalScore(scores, weights) {
  let total = 0;
  for (const [dim, weight] of Object.entries(weights)) {
    const score = scores[dim] || 0;
    total += score * weight;
  }
  return Math.round(total * 100) / 100;
}

async function main() {
  const date = '2026-04-16';
  
  // 读取各维度评分文件
  const dimensions = [
    'emotion',
    'fundamental_momentum',
    'kline_pattern',
    'main_moneyflow',
    'sector_heat',
    'technical',
    'volume_price_sync',
  ];
  
  const allScores = {};
  
  // 维度名称映射到分数字段名
  const dimScoreField = {
    'emotion': 'emotion_10',
    'fundamental_momentum': 'fundamental_10',
    'kline_pattern': 'score15',
    'main_moneyflow': 'main_money_15',
    'sector_heat': 'sector_heat_score',
    'technical': 'tech_15',
    'volume_price_sync': 'score15',
  };
  
  for (const dim of dimensions) {
    const fileName = dim === 'technical' ? 'technical_scored' : `${dim}_scored`;
    const p = path.join(__dirname, '..', 'output', `${fileName}_${date}.json`);
    
    if (!fs.existsSync(p)) {
      console.error(`Missing: ${p}`);
      continue;
    }
    
    const data = JSON.parse(fs.readFileSync(p, 'utf8'));
    
    // 从 items 数组提取评分
    const stocks = data.items || [];
    const scoreField = dimScoreField[dim] || dim;
    
    for (const stock of stocks) {
      const tsCode = stock.ts_code;
      if (!tsCode) continue;
      
      if (!allScores[tsCode]) {
        allScores[tsCode] = { ts_code: tsCode };
      }
      
      // 提取该维度的分数
      const score = stock[scoreField];
      if (score !== undefined) {
        allScores[tsCode][dim] = score;
      }
    }
    
    console.log(`Loaded ${dim}: ${stocks.length} stocks`);
  }
  
  // 读取老权重的 Top10
  const toTestPath = path.join(__dirname, '..', 'output', `to_test_${date}.json`);
  const toTestData = JSON.parse(fs.readFileSync(toTestPath, 'utf8'));
  const oldTop10 = toTestData.backtest_targets || [];
  const oldTop10Codes = new Set(oldTop10.map(s => s.ts_code));
  
  console.log(`\n老权重 Top10: ${oldTop10.length} 只股票`);
  
  // 计算新权重总分
  const results = [];
  for (const [tsCode, scores] of Object.entries(allScores)) {
    // 映射 technical -> technical_indicators
    const mappedScores = {
      sector_heat: scores.sector_heat,
      fundamental_momentum: scores.fundamental_momentum,
      volume_price_sync: scores.volume_price_sync,
      kline_pattern: scores.kline_pattern,
      technical_indicators: scores.technical,
      main_moneyflow: scores.main_moneyflow,
      emotion: scores.emotion,
    };
    
    const newTotal = computeTotalScore(mappedScores, newWeights);
    
    // 查找老排名
    const oldItem = oldTop10.find(s => s.ts_code === tsCode);
    const oldRank = oldItem ? oldTop10.indexOf(oldItem) + 1 : null;
    const oldTotal = oldItem ? oldItem.total_score : null;
    
    results.push({
      ts_code: tsCode,
      oldRank,
      oldTotal,
      newTotal,
      scores: mappedScores,
    });
  }
  
  console.log(`\n共 ${results.length} 只股票有评分数据`);
  
  // 按新权重排序
  results.sort((a, b) => b.newTotal - a.newTotal);
  
  // 输出新权重下的 Top20
  console.log('\n=== 新权重下的 Top20 ===');
  console.log('-'.repeat(100));
  console.log('新排名 | 代码       | 老排名 | 新总分 | 老总分 | 状态');
  console.log('-'.repeat(100));
  
  for (let i = 0; i < Math.min(20, results.length); i++) {
    const r = results[i];
    const newRank = i + 1;
    
    let status = '';
    if (r.oldRank === null) {
      status = '🆕 新进 Top10';
    } else if (r.oldRank <= 10 && newRank > 10) {
      status = '⬇️ 跌出 Top10';
    } else if (r.oldRank > 10 && newRank <= 10) {
      status = '⬆️ 进入 Top10';
    }
    
    const oldRankStr = r.oldRank !== null ? String(r.oldRank).padStart(2) : '  -';
    const oldTotalStr = r.oldTotal !== null ? r.oldTotal.toFixed(1).padStart(5) : '    -';
    
    console.log(`${String(newRank).padStart(2)}    | ${r.ts_code} | ${oldRankStr}     | ${r.newTotal.toFixed(1).padStart(5)} | ${oldTotalStr} | ${status}`);
  }
  
  // 分析 Top10 变化
  const newTop10 = results.slice(0, 10);
  const newTop10Codes = new Set(newTop10.map(r => r.ts_code));
  
  const added = newTop10.filter(r => !oldTop10Codes.has(r.ts_code));
  const removed = oldTop10.filter(s => !newTop10Codes.has(s.ts_code));
  
  console.log('\n=== Top10 变化分析 ===');
  
  if (added.length > 0) {
    console.log('\n新进入 Top10:');
    for (const r of added) {
      console.log(`  ${r.ts_code}: 新总分=${r.newTotal.toFixed(1)}, 老排名=${r.oldRank || '不在 Top10'}`);
    }
  }
  
  if (removed.length > 0) {
    console.log('\n跌出 Top10:');
    for (const s of removed) {
      const newRank = results.findIndex(r => r.ts_code === s.ts_code) + 1;
      console.log(`  ${s.ts_code}: 老总分=${s.total_score.toFixed(1)}, 新排名=${newRank}`);
    }
  }
  
  if (added.length === 0 && removed.length === 0) {
    console.log('\nTop10 成分股未变化，仅排名顺序改变');
  }
  
  // 保存结果
  const outPath = path.join(__dirname, '..', 'output', `all_candidates_new_weights_${date}.json`);
  fs.writeFileSync(outPath, JSON.stringify({
    date,
    weights: newWeights,
    top10: newTop10,
    allResults: results,
  }, null, 2));
  
  console.log(`\n结果已保存到: ${outPath}`);
}

main().catch(e => {
  console.error(e.stack || e.message);
  process.exit(1);
});
