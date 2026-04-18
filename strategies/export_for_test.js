/**
 * 导出给 Test Agent(马云) 的回测目标 TopN，并附带股票名称 mc。
 *
 * 输入：各维度输出文件（默认读取 2026-04-13 的 output）
 * 名称：从 TuShare stock_basic 获取 name
 * 输出：output/to_test_<date>.json 并复制到 workspace-test/input/
 */

const fs = require('fs');
const path = require('path');
const https = require('https');

function parseArgs(argv) {
  const out = {};
  for (let i = 2; i < argv.length; i++) {
    const a = argv[i];
    if (a === '--date') out.date = argv[++i];
    else if (a === '--top') out.top = Number(argv[++i]);
    else if (a === '--weights') out.weights = argv[++i];
    else if (a === '--token') out.token = argv[++i];
  }
  return out;
}

function normalizeDate(s) {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(s)) throw new Error(`invalid --date: ${s}`);
  return s;
}

function postTushare(payload, token) {
  const body = JSON.stringify({ ...payload, token });
  return new Promise((resolve, reject) => {
    const req = https.request(
      {
        hostname: 'api.tushare.pro',
        path: '/',
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'Content-Length': Buffer.byteLength(body),
        },
      },
      (res) => {
        let data = '';
        res.on('data', (c) => (data += c));
        res.on('end', () => {
          try {
            resolve(JSON.parse(data));
          } catch {
            reject(new Error(`tushare non-json: ${data.slice(0, 200)}`));
          }
        });
      },
    );
    req.on('error', reject);
    req.write(body);
    req.end();
  });
}

function loadJson(p) {
  return JSON.parse(fs.readFileSync(p, 'utf8'));
}

function getMap(pathOrNull, keyField, valueField) {
  if (!fs.existsSync(pathOrNull)) return {};
  const j = loadJson(pathOrNull);
  const items = j.items || [];
  const m = {};
  for (const it of items) {
    m[it[keyField]] = it[valueField];
  }
  return m;
}

async function main() {
  const args = parseArgs(process.argv);
  const date = normalizeDate(args.date);
  const topN = Number.isFinite(args.top) && args.top > 0 ? args.top : 10;
  const token = args.token || process.env.TUSHARE_TOKEN;
  if (!token) throw new Error('missing TUSHARE_TOKEN');

  // 支持按权重重算总分：传入 JSON 字符串，如：
  //   --weights '{"sector_heat":0.6,"fundamental_momentum":0.8,"volume_price_sync":1.2,"kline_pattern":1.2,"technical_indicators":0.6667,"main_moneyflow":1.4667,"emotion":1.2}'
  // 若未传入则默认各维度权重为 1（等同旧逻辑）。
  let weights = null;
  if (args.weights) {
    try {
      weights = JSON.parse(args.weights);
    } catch {
      throw new Error(`invalid --weights json: ${args.weights}`);
    }
  }

  const base = '/root/.openclaw/workspace-score/output';
  const m1 = getMap(path.join(base, `sector_heat_scored_${date}.json`), 'ts_code', 'sector_heat_score');
  const m2 = getMap(path.join(base, `fundamental_momentum_scored_${date}.json`), 'ts_code', 'fundamental_10');
  const m3 = getMap(path.join(base, `volume_price_sync_scored_${date}.json`), 'ts_code', 'score15');
  const m4 = getMap(path.join(base, `kline_pattern_scored_${date}.json`), 'ts_code', 'score15');
  const m5 = getMap(path.join(base, `technical_scored_${date}.json`), 'ts_code', 'tech_15');
  const m6 = getMap(path.join(base, `main_moneyflow_scored_${date}.json`), 'ts_code', 'main_money_15');
  const m7 = getMap(path.join(base, `emotion_scored_${date}.json`), 'ts_code', 'emotion_10');

  const codes = Array.from(new Set([...
    Object.keys(m1),...Object.keys(m2),...Object.keys(m3),...Object.keys(m4),...Object.keys(m5),...Object.keys(m6),...Object.keys(m7)
  ]));

  const rows = codes.map((ts) => {
    const d1 = m1[ts] || 0;
    const d2 = m2[ts] || 0;
    const d3 = m3[ts] || 0;
    const d4 = m4[ts] || 0;
    const d5 = m5[ts] || 0;
    const d6 = m6[ts] || 0;
    const d7 = m7[ts] || 0;

    const w = weights || {};
    const total =
      d1 * (Number.isFinite(w.sector_heat) ? w.sector_heat : 1) +
      d2 * (Number.isFinite(w.fundamental_momentum) ? w.fundamental_momentum : 1) +
      d3 * (Number.isFinite(w.volume_price_sync) ? w.volume_price_sync : 1) +
      d4 * (Number.isFinite(w.kline_pattern) ? w.kline_pattern : 1) +
      d5 * (Number.isFinite(w.technical_indicators) ? w.technical_indicators : 1) +
      d6 * (Number.isFinite(w.main_moneyflow) ? w.main_moneyflow : 1) +
      d7 * (Number.isFinite(w.emotion) ? w.emotion : 1);
    return {
      ts_code: ts,
      total_score: Math.round(total*100)/100,
      breakdown: { d1, d2, d3, d4, d5, d6, d7 },
    };
  }).sort((a,b)=>b.total_score-a.total_score||a.ts_code.localeCompare(b.ts_code));

  const top = rows.slice(0, topN);

  // 从本地缓存读取股票名称
  const cachePath = path.join(base, 'stock_names_cache.json');
  let nameMap = new Map();
  if (fs.existsSync(cachePath)) {
    const cache = loadJson(cachePath);
    if (cache.stock_names) {
      for (const [ts, nm] of Object.entries(cache.stock_names)) {
        nameMap.set(ts, nm);
      }
    }
  }

  // 如果缓存中没有，提示用户更新缓存
  if (nameMap.size === 0) {
    console.error('Warning: stock_names_cache.json not found or empty.');
    console.error('Please run: node strategies/update_stock_names_cache.js');
  }

  const targets = top.map((r) => ({
    ts_code: r.ts_code,
    mc: nameMap.get(r.ts_code) || null,
    total_score: r.total_score,
    scores: {
      sector_heat: Math.round(r.breakdown.d1 * 100) / 100,
      fundamental_momentum: Math.round(r.breakdown.d2 * 100) / 100,
      volume_price_sync: Math.round(r.breakdown.d3 * 100) / 100,
      kline_pattern: Math.round(r.breakdown.d4 * 100) / 100,
      technical_indicators: Math.round(r.breakdown.d5 * 100) / 100,
      main_moneyflow: Math.round(r.breakdown.d6 * 100) / 100,
      emotion: Math.round(r.breakdown.d7 * 100) / 100
    }
  }));

  // 马云输入格式（扩展版）
  const out = {
    date,
    top: topN,
    backtest_targets: targets,
  };

  const outPath = path.join(base, `to_test_${date}.json`);
  fs.writeFileSync(outPath, JSON.stringify(out, null, 2));

  const destDir = '/root/.openclaw/workspace-test/input';
  fs.mkdirSync(destDir, { recursive: true });
  const destPath = path.join(destDir, `to_test_${date}.json`);
  fs.copyFileSync(outPath, destPath);

  process.stdout.write(JSON.stringify({ ok: true, outPath, destPath, sample: targets }, null, 2));
}

main().catch((e) => {
  process.stderr.write(String(e.stack || e.message || e) + '\n');
  process.exit(1);
});
