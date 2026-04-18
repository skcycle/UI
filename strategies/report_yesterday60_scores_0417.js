#!/usr/bin/env node
/**
 * 查看“昨天候选60只(2026-04-16)”在“今天数据(2026-04-17)”下的得分情况。
 * 使用 merged 跑出来的各维度 scored 文件（它们包含 121 只的评分）。
 */

const fs = require('fs');

const DATE = '2026-04-17';
const YDAY = '2026-04-16';
const scoreDir = '/root/.openclaw/workspace-score';

function readJson(p){return JSON.parse(fs.readFileSync(p,'utf8'));}

function main(){
  const yCodes = new Set(readJson(`${scoreDir}/input/candidate_stocks_${YDAY}.json`).candidate_stocks);

  const dimFiles = {
    sector_heat: { path: `${scoreDir}/output/sector_heat_scored_${DATE}.json`, field: 'sector_heat_score' },
    fundamental_momentum: { path: `${scoreDir}/output/fundamental_momentum_scored_${DATE}.json`, field: 'fundamental_10' },
    volume_price_sync: { path: `${scoreDir}/output/volume_price_sync_scored_${DATE}.json`, field: 'score15' },
    kline_pattern: { path: `${scoreDir}/output/kline_pattern_scored_${DATE}.json`, field: 'score15' },
    technical_indicators: { path: `${scoreDir}/output/technical_scored_${DATE}.json`, field: 'tech_15' },
    main_moneyflow: { path: `${scoreDir}/output/main_moneyflow_scored_${DATE}.json`, field: 'main_money_15' },
    emotion: { path: `${scoreDir}/output/emotion_scored_${DATE}.json`, field: 'emotion_10' },
  };

  // build per-dim maps
  const maps = {};
  for (const [dim, info] of Object.entries(dimFiles)) {
    const items = readJson(info.path).items;
    const m = new Map();
    for (const it of items) {
      if (!yCodes.has(it.ts_code)) continue;
      m.set(it.ts_code, it[info.field]);
    }
    maps[dim] = m;
  }

  // total map from merged to_test (sum-of-scores)
  const mergedTop = readJson(`${scoreDir}/output/to_test_${DATE}_merged.json`).backtest_targets;
  const nameMap = new Map(mergedTop.map(x=>[x.ts_code, x.mc]));

  const rows = [];
  for (const code of yCodes) {
    const r = {
      ts_code: code,
      mc: nameMap.get(code) || '',
      sector_heat: maps.sector_heat.get(code) ?? null,
      fundamental_momentum: maps.fundamental_momentum.get(code) ?? null,
      volume_price_sync: maps.volume_price_sync.get(code) ?? null,
      kline_pattern: maps.kline_pattern.get(code) ?? null,
      technical_indicators: maps.technical_indicators.get(code) ?? null,
      main_moneyflow: maps.main_moneyflow.get(code) ?? null,
      emotion: maps.emotion.get(code) ?? null,
    };
    // compute sum like old score (note: using dim file fields, approximate)
    const vals = [r.sector_heat,r.fundamental_momentum,r.volume_price_sync,r.kline_pattern,r.technical_indicators,r.main_moneyflow,r.emotion];
    if (vals.every(v=>typeof v==='number')) r.total_score = vals.reduce((a,b)=>a+b,0);
    else r.total_score = null;
    rows.push(r);
  }

  rows.sort((a,b)=>(b.total_score??-1)-(a.total_score??-1));

  console.log(`Yesterday candidates: ${rows.length}`);
  console.log('Top 20 among yesterday-60 (scored on 2026-04-17 data):');
  for (const [i,r] of rows.slice(0,20).entries()) {
    console.log(`${i+1}. ${r.ts_code}\t${r.mc}\t${r.total_score?.toFixed(2)}`);
  }

  // show best rank compared to overall 121 pool
  const overall = readJson(`${scoreDir}/output/to_test_${DATE}_merged.json`).backtest_targets;
  const overallSorted = [...overall].sort((a,b)=>b.total_score-a.total_score);
  const overallRank = new Map(overallSorted.map((x,i)=>[x.ts_code,i+1]));
  const best = rows.filter(r=>r.total_score!==null).slice(0,10).map(r=>({
    ts_code:r.ts_code, mc:r.mc, total:r.total_score, rank_overall: overallRank.get(r.ts_code) || null
  }));

  console.log('\nBest 10 yesterday-candidates with overall rank in 121-pool:');
  best.forEach((x,i)=>console.log(`${i+1}. ${x.ts_code}\t${x.total.toFixed(2)}\toverall_rank=${x.rank_overall}`));

  const outPath = `${scoreDir}/output/yesterday60_scored_on_${DATE}.json`;
  fs.writeFileSync(outPath, JSON.stringify({date:DATE, yesterday:YDAY, rows}, null, 2));
  console.log('\nSaved:', outPath);
}

main();
