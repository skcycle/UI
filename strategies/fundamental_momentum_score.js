/**
 * 巴菲特-评分维度2: 基本面动量(当日) 0~10分
 *
 * 输入:
 * 1) 候选股列表(带市场后缀): input/candidate_stocks_<date>.json
 *    {"candidate_stocks": ["002837.SZ", ...]}
 *
 * 2) 当日第一轮/第二轮筛选结果(用于取 pc/hs/cje):
 *    优先第二轮：/root/.openclaw/workspace-select/output/second_filter_live_<date>.json
 *               /root/.openclaw/workspace-select/output/second_filter_tushare_<date>.json
 *    兜底第一轮：/root/.openclaw/workspace-select/output/first_filter_live_<date>.json
 *               /root/.openclaw/workspace-select/output/first_filter_tushare_<date>.json
 *
 * 评分逻辑(总分10):
 * - pc(涨幅) 分位 40%
 * - hs(换手) 分位 30%，并对过高换手做惩罚：hs>25% *0.9, hs>35% *0.75
 * - cje(成交额) log1p 后分位 30%
 * fundamental_10 = (0.4*pc_rank + 0.3*hs_rank_adj + 0.3*cje_rank) * 10
 *
 * 输出:
 *   output/fundamental_momentum_scored_<date>.json
 */

const fs = require('fs');
const path = require('path');
const https = require('https');

function parseArgs(argv) {
  const out = {};
  for (let i = 2; i < argv.length; i++) {
    const a = argv[i];
    if (a === '--date') out.date = argv[++i];
    else if (a === '--in') out.inPath = argv[++i];
    else if (a === '--out') out.outPath = argv[++i];
    else if (a === '--token') out.token = argv[++i];
  }
  return out;
}

function normalizeDate(s) {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(s)) throw new Error(`invalid --date: ${s} (expect YYYY-MM-DD)`);
  return s;
}

function tryLoadJson(p) {
  if (!p || !fs.existsSync(p)) return null;
  try {
    return JSON.parse(fs.readFileSync(p, 'utf8'));
  } catch {
    return null;
  }
}

function pctRank(values) {
  // returns map idx->rank in [0,1]
  const arr = values.map((v, i) => ({ v, i }))
    .filter((x) => Number.isFinite(x.v));
  arr.sort((a, b) => a.v - b.v);
  const n = arr.length;
  const out = new Array(values.length).fill(null);
  if (n === 0) return out;
  // average rank for ties
  let k = 0;
  while (k < n) {
    let j = k + 1;
    while (j < n && arr[j].v === arr[k].v) j++;
    const avgPos = (k + (j - 1)) / 2;
    const r = n === 1 ? 1 : avgPos / (n - 1);
    for (let t = k; t < j; t++) out[arr[t].i] = r;
    k = j;
  }
  return out;
}

function clamp(x, a, b) {
  return Math.max(a, Math.min(b, x));
}

function yyyymmdd(s) {
  return s.replace(/-/g, '');
}

function postTushare(payload) {
  const body = JSON.stringify(payload);
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

async function fetchTushareFallback({ token, date, tsCodes }) {
  const tradeDate = yyyymmdd(date);
  const missing = Array.from(new Set(tsCodes));
  const pcMap = new Map();
  const hsMap = new Map();
  const cjeMap = new Map();
  if (!missing.length) return { pcMap, hsMap, cjeMap, fetchedDaily: 0, fetchedDailyBasic: 0 };

  const chunkSize = 80;
  let fetchedDaily = 0;
  let fetchedDailyBasic = 0;

  for (let i = 0; i < missing.length; i += chunkSize) {
    const chunk = missing.slice(i, i + chunkSize);

    const dailyJson = await postTushare({
      api_name: 'daily',
      token,
      params: { ts_code: chunk.join(','), trade_date: tradeDate },
      fields: 'ts_code,trade_date,pct_chg,amount',
    });
    if (!dailyJson || dailyJson.code !== 0) throw new Error(dailyJson ? dailyJson.msg : 'tushare daily error');
    const dailyData = dailyJson.data || { fields: [], items: [] };
    const dIdx = Object.fromEntries((dailyData.fields || []).map((f, idx) => [f, idx]));
    for (const row of (dailyData.items || [])) {
      const ts = row[dIdx.ts_code];
      if (!ts) continue;
      if (dIdx.pct_chg != null) pcMap.set(ts, Number(row[dIdx.pct_chg]));
      if (dIdx.amount != null) cjeMap.set(ts, Number(row[dIdx.amount]));
      fetchedDaily += 1;
    }

    const dailyBasicJson = await postTushare({
      api_name: 'daily_basic',
      token,
      params: { ts_code: chunk.join(','), trade_date: tradeDate },
      fields: 'ts_code,trade_date,turnover_rate_f,turnover_rate',
    });
    if (!dailyBasicJson || dailyBasicJson.code !== 0) throw new Error(dailyBasicJson ? dailyBasicJson.msg : 'tushare daily_basic error');
    const basicData = dailyBasicJson.data || { fields: [], items: [] };
    const bIdx = Object.fromEntries((basicData.fields || []).map((f, idx) => [f, idx]));
    for (const row of (basicData.items || [])) {
      const ts = row[bIdx.ts_code];
      if (!ts) continue;
      const turnover = bIdx.turnover_rate_f != null && row[bIdx.turnover_rate_f] != null
        ? Number(row[bIdx.turnover_rate_f])
        : (bIdx.turnover_rate != null ? Number(row[bIdx.turnover_rate]) : NaN);
      if (Number.isFinite(turnover)) hsMap.set(ts, turnover / 100);
      fetchedDailyBasic += 1;
    }
  }

  return { pcMap, hsMap, cjeMap, fetchedDaily, fetchedDailyBasic };
}

async function main() {
  const args = parseArgs(process.argv);
  const date = normalizeDate(args.date);
  const token = args.token || process.env.TUSHARE_TOKEN;

  const inPath = args.inPath || path.join(__dirname, '..', 'input', `candidate_stocks_${date}.json`);
  const cand = tryLoadJson(inPath);
  if (!cand || !Array.isArray(cand.candidate_stocks)) throw new Error(`missing/invalid input: ${inPath}`);
  const tsCodes = cand.candidate_stocks;

  // load select outputs
  const baseSel = '/root/.openclaw/workspace-select/output';
  // preferred: explicit mode-suffixed outputs
  const secondLive = tryLoadJson(path.join(baseSel, `second_filter_live_${date}.json`));
  const secondTu = tryLoadJson(path.join(baseSel, `second_filter_tushare_${date}.json`));
  const firstLive = tryLoadJson(path.join(baseSel, `first_filter_live_${date}.json`));
  const firstTu = tryLoadJson(path.join(baseSel, `first_filter_tushare_${date}.json`));
  // fallback: generic outputs (current run_select_complete.sh style)
  const secondGeneric = tryLoadJson(path.join(baseSel, `second_filter_${date}.json`));
  const firstGeneric = tryLoadJson(path.join(baseSel, `first_filter_${date}.json`));

  // build lookup for hs/sz from second, and pc/cje from first
  const hsMap = new Map();
  const srcSecond = secondLive || secondTu || secondGeneric;
  if (srcSecond && Array.isArray(srcSecond.passed)) {
    for (const r of srcSecond.passed) {
      if (!r || !r.code || !r.mkt) continue;
      hsMap.set(`${r.code}.${r.mkt}`, Number(r.hs));
    }
  }

  const pcMap = new Map();
  const cjeMap = new Map();
  const srcFirst = firstLive || firstTu || firstGeneric;
  if (srcFirst && Array.isArray(srcFirst.passed)) {
    for (const r of srcFirst.passed) {
      if (!r || !r.code || !r.mkt) continue;
      pcMap.set(`${r.code}.${r.mkt}`, Number(r.pc));
      cjeMap.set(`${r.code}.${r.mkt}`, Number(r.cje));
    }
  }

  // assemble rows
  const missingForFallback = tsCodes.filter((ts) => !(pcMap.has(ts) && hsMap.has(ts) && cjeMap.has(ts)));
  let fallbackStats = null;
  if (missingForFallback.length > 0) {
    if (!token) throw new Error('missing TUSHARE_TOKEN for fallback fetch');
    fallbackStats = await fetchTushareFallback({ token, date, tsCodes: missingForFallback });
    for (const ts of missingForFallback) {
      if (!pcMap.has(ts) && fallbackStats.pcMap.has(ts)) pcMap.set(ts, fallbackStats.pcMap.get(ts));
      if (!hsMap.has(ts) && fallbackStats.hsMap.has(ts)) hsMap.set(ts, fallbackStats.hsMap.get(ts));
      if (!cjeMap.has(ts) && fallbackStats.cjeMap.has(ts)) cjeMap.set(ts, fallbackStats.cjeMap.get(ts));
    }
  }

  const rows = tsCodes.map((ts) => {
    const pc = pcMap.has(ts) ? pcMap.get(ts) : null;
    const hs = hsMap.has(ts) ? hsMap.get(ts) : null;
    const cje = cjeMap.has(ts) ? cjeMap.get(ts) : null;
    return { ts_code: ts, pc, hs, cje };
  });

  // ranks
  const pcVals = rows.map((r) => (Number.isFinite(r.pc) ? r.pc : NaN));
  const hsValsPct = rows.map((r) => (Number.isFinite(r.hs) ? r.hs * 100 : NaN)); // 转成 % 判断惩罚
  const hsVals = rows.map((r) => (Number.isFinite(r.hs) ? r.hs : NaN));
  const cjeVals = rows.map((r) => (Number.isFinite(r.cje) ? Math.log1p(r.cje) : NaN));

  const pcRank = pctRank(pcVals);
  const hsRank = pctRank(hsVals);
  const cjeRank = pctRank(cjeVals);

  const items = rows.map((r, i) => {
    let hsAdj = hsRank[i];
    const hsPct = hsValsPct[i];
    if (hsAdj !== null && Number.isFinite(hsPct)) {
      if (hsPct > 35) hsAdj *= 0.75;
      else if (hsPct > 25) hsAdj *= 0.9;
    }

    const pcS = pcRank[i] ?? 0;
    const hsS = hsAdj ?? 0;
    const cjeS = cjeRank[i] ?? 0;

    const raw = 0.4 * pcS + 0.3 * hsS + 0.3 * cjeS;
    const fundamental_10 = Math.round(clamp(raw, 0, 1) * 10 * 100) / 100; // 2位小数

    return {
      ts_code: r.ts_code,
      pc: r.pc,
      hs: r.hs,
      cje: r.cje,
      subscores: {
        pc_rank: pcS,
        hs_rank_adj: hsS,
        cje_rank: cjeS,
      },
      fundamental_10,
    };
  });

  items.sort((a, b) => b.fundamental_10 - a.fundamental_10 || a.ts_code.localeCompare(b.ts_code));

  const out = {
    date,
    dimension: 'fundamental_momentum',
    max_score: 10,
    weights: { pc: 0.4, hs: 0.3, cje: 0.3 },
    hs_penalty: { gt25: 0.9, gt35: 0.75 },
    input_count: tsCodes.length,
    scored_count: items.length,
    items,
    sources: {
      first: srcFirst ? srcFirst.mode || 'unknown' : null,
      second: srcSecond ? srcSecond.mode || 'unknown' : null,
      fallback: fallbackStats ? {
        provider: 'tushare',
        apis: ['daily', 'daily_basic'],
        missing_candidates: missingForFallback.length,
        fetched_daily_rows: fallbackStats.fetchedDaily,
        fetched_daily_basic_rows: fallbackStats.fetchedDailyBasic,
      } : null,
    },
  };

  const outPath = args.outPath || path.join(__dirname, '..', 'output', `fundamental_momentum_scored_${date}.json`);
  fs.mkdirSync(path.dirname(outPath), { recursive: true });
  fs.writeFileSync(outPath, JSON.stringify(out, null, 2));

  process.stdout.write(JSON.stringify({ ok: true, outPath, top5: items.slice(0, 5) }, null, 2));
}

main().catch((e) => {
  process.stderr.write(String(e.stack || e.message || e) + '\n');
  process.exit(1);
});
