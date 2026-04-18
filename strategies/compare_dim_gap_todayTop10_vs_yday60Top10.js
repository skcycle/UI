#!/usr/bin/env node
/**
 * 对比：
 * - 今天池 Top10 (to_test_2026-04-17.json)
 * - 昨天60里按今天数据重算的 Top10 (yesterday60_scored_on_2026-04-17.json)
 * 看主要差在哪些维度（均值差、分布差）。
 */

const fs = require('fs');

const DATE='2026-04-17';
const scoreDir='/root/.openclaw/workspace-score';

function readJson(p){return JSON.parse(fs.readFileSync(p,'utf8'));}

function mean(arr){return arr.reduce((a,b)=>a+b,0)/arr.length;}

function pick(items, codes){
  const set=new Set(codes);
  return items.filter(x=>set.has(x.ts_code));
}

function main(){
  const todayTop = readJson(`${scoreDir}/output/to_test_${DATE}.json`).backtest_targets;
  const yRows = readJson(`${scoreDir}/output/yesterday60_scored_on_${DATE}.json`).rows;
  const yTop10 = yRows.filter(r=>typeof r.total_score==='number').sort((a,b)=>b.total_score-a.total_score).slice(0,10);

  const dims=['sector_heat','fundamental_momentum','volume_price_sync','kline_pattern','technical_indicators','main_moneyflow','emotion'];

  const res=[];
  for(const dim of dims){
    const tVals=todayTop.map(x=>x.scores[dim]).filter(v=>typeof v==='number');
    const yVals=yTop10.map(x=>x[dim]).filter(v=>typeof v==='number');
    res.push({
      dim,
      today_mean: mean(tVals),
      yday_mean: mean(yVals),
      diff: mean(tVals)-mean(yVals),
      today_min: Math.min(...tVals),
      today_max: Math.max(...tVals),
      yday_min: Math.min(...yVals),
      yday_max: Math.max(...yVals),
    });
  }

  res.sort((a,b)=>Math.abs(b.diff)-Math.abs(a.diff));

  console.log('=== 维度均值差 (今天Top10 - 昨天60的Top10) ===');
  for(const r of res){
    console.log(`${r.dim}: diff=${r.diff.toFixed(2)} (today_mean=${r.today_mean.toFixed(2)}, yday_mean=${r.yday_mean.toFixed(2)})`);
  }

  console.log('\n=== 每个维度范围对比 ===');
  for(const r of res){
    console.log(`${r.dim}: today[${r.today_min.toFixed(2)},${r.today_max.toFixed(2)}] vs yday[${r.yday_min.toFixed(2)},${r.yday_max.toFixed(2)}]`);
  }

  const outPath=`${scoreDir}/output/dim_gap_todayTop10_vs_yday60Top10_${DATE}.json`;
  fs.writeFileSync(outPath, JSON.stringify({date:DATE, dims:res, todayTop10: todayTop.map(x=>x.ts_code), yday60Top10: yTop10.map(x=>x.ts_code)}, null, 2));
  console.log('\nSaved:', outPath);
}

main();
