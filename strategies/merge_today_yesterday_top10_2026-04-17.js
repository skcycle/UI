#!/usr/bin/env node
const fs = require('fs');
const path = require('path');

const DATE = '2026-04-17';
const YDAY = '2026-04-16';
const base = '/root/.openclaw/workspace-score';

function readJson(p) {
  return JSON.parse(fs.readFileSync(p, 'utf8'));
}

const dims = {
  sector_heat: { file: `sector_heat_scored_${DATE}.json`, field: 'sector_heat_score' },
  fundamental_momentum: { file: `fundamental_momentum_scored_${DATE}.json`, field: 'fundamental_10' },
  volume_price_sync: { file: `volume_price_sync_scored_${DATE}.json`, field: 'score15' },
  kline_pattern: { file: `kline_pattern_scored_${DATE}.json`, field: 'score15' },
  technical_indicators: { file: `technical_scored_${DATE}.json`, field: 'tech_15' },
  main_moneyflow: { file: `main_moneyflow_scored_${DATE}.json`, field: 'main_money_15' },
  emotion: { file: `emotion_scored_${DATE}.json`, field: 'emotion_10' },
};

function buildDimMaps() {
  const maps = {};
  for (const [dim, info] of Object.entries(dims)) {
    const items = readJson(path.join(base, 'output', info.file)).items || [];
    const m = new Map();
    for (const it of items) m.set(it.ts_code, Number(it[info.field]) || 0);
    maps[dim] = m;
  }
  return maps;
}

function buildRow(ts, label, dimMaps, nameMap) {
  const scores = {};
  let total = 0;
  for (const dim of Object.keys(dims)) {
    const v = dimMaps[dim].get(ts) ?? 0;
    scores[dim] = Math.round(v * 100) / 100;
    total += v;
  }
  return {
    ts_code: ts,
    mc: nameMap[ts] || null,
    source: label,
    total_score: Math.round(total * 100) / 100,
    scores,
  };
}

function main() {
  const todayPool = readJson(path.join(base, 'input', `candidate_stocks_${DATE}.json`)).candidate_stocks || [];
  const ydayPool = readJson(path.join(base, 'input', `candidate_stocks_${YDAY}.json`)).candidate_stocks || [];
  const names = (readJson(path.join(base, 'output', 'stock_names_cache.json')).stock_names) || {};
  const dimMaps = buildDimMaps();

  const merged = [];
  for (const ts of todayPool) merged.push(buildRow(ts, 'today', dimMaps, names));
  for (const ts of ydayPool) {
    if (todayPool.includes(ts)) continue;
    merged.push(buildRow(ts, 'yesterday', dimMaps, names));
  }

  merged.sort((a, b) => b.total_score - a.total_score || a.ts_code.localeCompare(b.ts_code));

  const top10 = merged.slice(0, 10).map((r, idx) => ({ rank: idx + 1, ...r }));
  const out = {
    date: DATE,
    today_pool_size: todayPool.length,
    yesterday_pool_size: ydayPool.length,
    merged_unique_size: merged.length,
    top10,
  };

  const outPath = path.join(base, 'output', `merged_today_yesterday_top10_${DATE}.json`);
  fs.writeFileSync(outPath, JSON.stringify(out, null, 2));

  console.log(JSON.stringify({ ok: true, outPath, top10 }, null, 2));
}

main();
