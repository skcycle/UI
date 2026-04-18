#!/usr/bin/env node
/**
 * 对比 2026-04-17：仅今日候选(61) vs 合并池(121) 的 Top10
 * 前置：
 * - mergedTop10 已保存为 output/to_test_2026-04-17_merged.json
 * - 运行本脚本会把 input/candidate_stocks_2026-04-17.json 切回仅今日候选池，并重跑 9 步评分导出 Top10
 */

const fs = require('fs');
const { execSync } = require('child_process');

const DATE = '2026-04-17';
const scoreDir = '/root/.openclaw/workspace-score';

function readJson(p){return JSON.parse(fs.readFileSync(p,'utf8'));}
function writeJson(p,obj){fs.writeFileSync(p, JSON.stringify(obj,null,2));}

function listTop10(obj){
  return (obj.backtest_targets||[]).map(x=>({ts_code:x.ts_code, mc:x.mc, total_score:x.total_score}));
}

function main(){
  const mergedPath = `${scoreDir}/output/to_test_${DATE}_merged.json`;
  if(!fs.existsSync(mergedPath)){
    console.error('missing merged snapshot:', mergedPath);
    process.exit(1);
  }
  const mergedTop10 = listTop10(readJson(mergedPath));

  console.log('Re-running score for ONLY today candidate pool...');
  execSync(`${scoreDir}/strategies/run_score_${DATE}.sh`, { stdio: 'inherit', env: process.env });

  const onlyPath = `${scoreDir}/output/to_test_${DATE}.json`;
  const onlyTop10 = listTop10(readJson(onlyPath));

  const setMerged = new Set(mergedTop10.map(x=>x.ts_code));
  const setOnly = new Set(onlyTop10.map(x=>x.ts_code));

  const added = mergedTop10.filter(x=>!setOnly.has(x.ts_code));
  const removed = onlyTop10.filter(x=>!setMerged.has(x.ts_code));

  console.log('\n=== ONLY(61) Top10 ===');
  onlyTop10.forEach((x,i)=>console.log(`${i+1}. ${x.ts_code} ${x.mc} ${x.total_score}`));

  console.log('\n=== MERGED(121) Top10 ===');
  mergedTop10.forEach((x,i)=>console.log(`${i+1}. ${x.ts_code} ${x.mc} ${x.total_score}`));

  console.log('\n=== Diff ===');
  console.log('In merged but not in only:', added.map(x=>x.ts_code).join(', ') || '(none)');
  console.log('In only but not in merged:', removed.map(x=>x.ts_code).join(', ') || '(none)');

  const out = {
    date: DATE,
    onlyTop10,
    mergedTop10,
    inMergedNotInOnly: added,
    inOnlyNotInMerged: removed,
    generated_at: new Date().toISOString()
  };
  const outPath = `${scoreDir}/output/diff_top10_only_vs_merged_${DATE}.json`;
  writeJson(outPath, out);
  console.log('\nSaved:', outPath);
}

main();
