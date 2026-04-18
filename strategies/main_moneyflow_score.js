/**
 * 巴菲特-评分维度6: 主力资金(近10日) 0~15分
 *
 * 数据源: TuShare moneyflow + moneyflow_dc
 *  - 优先用 moneyflow_dc 的主力净流入字段（若缺失则回退 moneyflow 的 net_mf_amount）
 *
 * 评分结构:
 *  A(0~6): 主力净流入强度（net_main_10d 与 net_ratio_10d 的分位）
 *  B(0~3): 连续性（pos_days 与 streak 的分位）
 *  C(0~3): 大单结构质量（big_share 分位）
 *  D(-2~0): 风险惩罚（neg_big_days 分档）
 *  main_money_15 = clamp(A+B+C+D,0,15)
 *
 * 输入:
 *  - input/candidate_stocks_<date>.json
 *  - output/daily_bundle_<date>.json（用于 amount 兜底；若没有 amount 则用 vol/close 近似，不推荐）
 *
 * 输出:
 *  - output/main_moneyflow_scored_<date>.json
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
    else if (a === '--window') out.window = Number(argv[++i]);
  }
  return out;
}

function normalizeDate(s) {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(s)) throw new Error(`invalid --date: ${s}`);
  return s;
}

function yyyymmdd(s) {
  return s.replace(/-/g, '');
}

function clamp(x, a, b) {
  return Math.max(a, Math.min(b, x));
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

function pctRankMap(valuesByKey) {
  const arr = [];
  for (const [k, v] of Object.entries(valuesByKey)) {
    if (Number.isFinite(v)) arr.push({ k, v });
  }
  arr.sort((a, b) => a.v - b.v);
  const n = arr.length;
  const out = {};
  if (n === 0) return out;
  // average rank for ties
  let i = 0;
  while (i < n) {
    let j = i + 1;
    while (j < n && arr[j].v === arr[i].v) j++;
    const avgPos = (i + (j - 1)) / 2;
    const r = n === 1 ? 1 : avgPos / (n - 1);
    for (let t = i; t < j; t++) out[arr[t].k] = r;
    i = j;
  }
  return out;
}

function maxConsecutivePos(list) {
  let best = 0;
  let cur = 0;
  for (const x of list) {
    if (x > 0) {
      cur++;
      if (cur > best) best = cur;
    } else {
      cur = 0;
    }
  }
  return best;
}

async function main() {
  const args = parseArgs(process.argv);
  const date = normalizeDate(args.date);
  const token = args.token || process.env.TUSHARE_TOKEN;
  if (!token) throw new Error('missing TUSHARE_TOKEN');

  const windowN = Number.isFinite(args.window) && args.window >= 5 ? args.window : 10;

  const inPath = args.inPath || path.join(__dirname, '..', 'input', `candidate_stocks_${date}.json`);
  if (!fs.existsSync(inPath)) throw new Error(`missing input: ${inPath}`);
  const cand = JSON.parse(fs.readFileSync(inPath, 'utf8'));
  const tsCodes = Array.isArray(cand.candidate_stocks) ? cand.candidate_stocks : [];
  if (!tsCodes.length) throw new Error('empty candidate_stocks');

  // date range (calendar back)
  const end = yyyymmdd(date);
  const endDate = new Date(date + 'T00:00:00+08:00');
  const start = new Date(endDate.getTime() - windowN * 4 * 24 * 3600 * 1000);
  const startYmd = yyyymmdd(start.toISOString().slice(0, 10));

  // moneyflow
  const mfPayload = {
    api_name: 'moneyflow',
    token,
    params: { ts_code: tsCodes.join(','), start_date: startYmd, end_date: end },
    fields: 'ts_code,trade_date,net_mf_amount,buy_elg_amount,sell_elg_amount,buy_lg_amount,sell_lg_amount',
  };

  // moneyflow_dc：实测返回字段会被服务端裁剪（比如只返回 net_amount），因此只依赖 net_amount。
  // 这里用并发分批请求，提升吞吐（上限10并发）。
  const dcFields = 'ts_code,trade_date,net_amount';
  const CHUNK = 2500;
  const dcChunks = [];
  for (let i = 0; i < tsCodes.length; i += CHUNK) dcChunks.push(tsCodes.slice(i, i + CHUNK));

  const mfPromise = postTushare(mfPayload);

  async function runPool(limit, items, fn) {
    let idx = 0;
    const results = new Array(items.length);
    const runners = Array.from({ length: Math.min(limit, items.length) }, async () => {
      while (idx < items.length) {
        const i = idx++;
        results[i] = await fn(items[i], i);
      }
    });
    await Promise.all(runners);
    return results;
  }

  const dcResults = await runPool(10, dcChunks, async (chunk) => {
    const payload = {
      api_name: 'moneyflow_dc',
      token,
      params: { ts_code: chunk.join(','), start_date: startYmd, end_date: end },
      fields: dcFields,
    };
    try {
      return await postTushare(payload);
    } catch (e) {
      return { code: -1, msg: String(e.message || e) };
    }
  });

  const mfJson = await mfPromise;
  const dcJsonList = dcResults;

  if (!mfJson || mfJson.code !== 0) throw new Error(mfJson ? mfJson.msg : 'moneyflow error');
  // moneyflow_dc 可能无权限，允许降级。这里汇总所有分批结果。
  const dcOk = Array.isArray(dcJsonList) && dcJsonList.some((x) => x && x.code === 0);

  function group(json) {
    const data = json.data;
    const idx = Object.fromEntries(data.fields.map((f, i) => [f, i]));
    const map = new Map();
    for (const it of data.items) {
      const ts = it[idx.ts_code];
      const td = it[idx.trade_date];
      if (!ts || !td) continue;
      if (!map.has(ts)) map.set(ts, []);
      const row = { trade_date: td };
      for (const f of data.fields) {
        if (f === 'ts_code' || f === 'trade_date') continue;
        row[f] = Number(it[idx[f]]);
      }
      map.get(ts).push(row);
    }
    for (const [ts, rows] of map.entries()) {
      rows.sort((a, b) => a.trade_date.localeCompare(b.trade_date));
    }
    return map;
  }

  const mfMap = group(mfJson);

  // 合并 dc 分批结果
  let dcMap = new Map();
  if (dcOk) {
    for (const part of dcJsonList) {
      if (!part || part.code !== 0) continue;
      const m = group(part);
      for (const [ts, rows] of m.entries()) {
        if (!dcMap.has(ts)) dcMap.set(ts, []);
        dcMap.get(ts).push(...rows);
      }
    }
    // sort merged
    for (const [ts, rows] of dcMap.entries()) rows.sort((a, b) => a.trade_date.localeCompare(b.trade_date));
  }

  // amount 10d: 尽量从 daily_bundle 取 close/vol 无法直接得出成交额，所以这里只用 net_ratio 时若无 amount 则跳过该项
  // 若你后续希望更准确，可把 daily_bundle 追加 amount 字段。
  const amountMap = {}; // ts_code -> sum_amount_10d (not available now)

  // per stock metrics
  const perStock = {};
  for (const ts of tsCodes) {
    // choose main net series
    let netSeries = [];
    if (dcOk && dcMap.has(ts)) {
      const rows = dcMap.get(ts);
      // 实测 dc 返回 net_amount（主力净流入，单位通常为万元）
      netSeries = rows.map((r) => ({ trade_date: r.trade_date, v: Number(r.net_amount) })).filter((x) => Number.isFinite(x.v));
    }
    if (!netSeries.length && mfMap.has(ts)) {
      const rows = mfMap.get(ts);
      netSeries = rows.map((r) => ({ trade_date: r.trade_date, v: Number(r.net_mf_amount) })).filter((x) => Number.isFinite(x.v));
    }

    netSeries = netSeries.slice(-windowN);
    const netVals = netSeries.map((x) => x.v);
    const netSum = netVals.reduce((a, b) => a + b, 0);

    const posDays = netVals.filter((x) => x > 0).length;
    const streak = maxConsecutivePos(netVals);

    // big net from mf (elg+lg)
    let bigVals = [];
    if (mfMap.has(ts)) {
      const rows = mfMap.get(ts).slice(-windowN);
      bigVals = rows
        .map((r) => (Number(r.buy_elg_amount) + Number(r.buy_lg_amount)) - (Number(r.sell_elg_amount) + Number(r.sell_lg_amount)))
        .filter((x) => Number.isFinite(x));
    }
    const bigSum = bigVals.reduce((a, b) => a + b, 0);
    const negBigDays = bigVals.filter((x) => x < 0).length;

    const netAbs = Math.max(1, Math.abs(netSum));
    const bigShare = bigSum / netAbs;

    // net_ratio: if we don't have amount, set null
    const netRatio = null;

    perStock[ts] = {
      net_main_10d: netSum,
      net_ratio_10d: netRatio,
      pos_days: posDays,
      streak,
      big_net_10d: bigSum,
      big_share: bigShare,
      neg_big_days: negBigDays,
      window: windowN,
    };
  }

  // ranks
  const netRank = pctRankMap(Object.fromEntries(Object.entries(perStock).map(([k, v]) => [k, v.net_main_10d])));
  const ratioRank = {}; // none
  const posRank = pctRankMap(Object.fromEntries(Object.entries(perStock).map(([k, v]) => [k, v.pos_days])));
  const streakRank = pctRankMap(Object.fromEntries(Object.entries(perStock).map(([k, v]) => [k, v.streak])));
  const bigShareRank = pctRankMap(Object.fromEntries(Object.entries(perStock).map(([k, v]) => [k, v.big_share])));

  const items = [];
  for (const ts of tsCodes) {
    const m = perStock[ts];
    const pNet = netRank[ts] ?? 0;
    // net_ratio 暂缺，先只用 net 的分位占满 6 分
    const A = 6 * pNet;

    const pPos = posRank[ts] ?? 0;
    const pStreak = streakRank[ts] ?? 0;
    const B = 1.5 * pPos + 1.5 * pStreak;

    const pBig = bigShareRank[ts] ?? 0;
    const C = 3 * pBig;

    let D = 0;
    if (m.neg_big_days >= 6) D = -2;
    else if (m.neg_big_days >= 4) D = -1;
    else if (m.neg_big_days >= 2) D = -0.5;

    const score = Math.round(clamp(A + B + C + D, 0, 15) * 100) / 100;

    items.push({
      ts_code: ts,
      main_money_15: score,
      parts: {
        A: Math.round(A * 100) / 100,
        B: Math.round(B * 100) / 100,
        C: Math.round(C * 100) / 100,
        D,
      },
      ranks: { net: pNet, pos: pPos, streak: pStreak, big_share: pBig },
      metrics: m,
      source: { moneyflow_dc: dcOk },
    });
  }

  items.sort((a, b) => b.main_money_15 - a.main_money_15 || a.ts_code.localeCompare(b.ts_code));

  const out = {
    date,
    dimension: 'main_moneyflow',
    max_score: 15,
    window: windowN,
    note: 'net_ratio_10d 未引入成交额(amount)时暂不使用；如需更精确可在 bundle 中加入 amount 字段。',
    items,
  };

  const outPath = args.outPath || path.join(__dirname, '..', 'output', `main_moneyflow_scored_${date}.json`);
  fs.mkdirSync(path.dirname(outPath), { recursive: true });
  fs.writeFileSync(outPath, JSON.stringify(out, null, 2));

  process.stdout.write(JSON.stringify({ ok: true, outPath, top5: items.slice(0, 5) }, null, 2));
}

main().catch((e) => {
  process.stderr.write(String(e.stack || e.message || e) + '\n');
  process.exit(1);
});
