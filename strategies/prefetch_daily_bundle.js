/**
 * 预取 TuShare daily 数据包（给量价协同+K线形态共用）
 *
 * 输入: input/candidate_stocks_<date>.json
 * 输出: output/daily_bundle_<date>.json
 *
 * 内容:
 *  - items: 按 ts_code 分组的 daily rows（升序）
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
    else if (a === '--days') out.days = Number(argv[++i]);
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

async function main() {
  const args = parseArgs(process.argv);
  const date = normalizeDate(args.date);
  const token = args.token || process.env.TUSHARE_TOKEN;
  if (!token) throw new Error('missing TUSHARE_TOKEN');

  const inPath = args.inPath || path.join(__dirname, '..', 'input', `candidate_stocks_${date}.json`);
  if (!fs.existsSync(inPath)) throw new Error(`missing input: ${inPath}`);
  const cand = JSON.parse(fs.readFileSync(inPath, 'utf8'));
  const tsCodes = Array.isArray(cand.candidate_stocks) ? cand.candidate_stocks : [];
  if (!tsCodes.length) throw new Error('empty candidate_stocks');

  const days = Number.isFinite(args.days) && args.days > 0 ? args.days : 60;
  const end = yyyymmdd(date);
  const endDate = new Date(date + 'T00:00:00+08:00');
  const start = new Date(endDate.getTime() - days * 24 * 3600 * 1000);
  const startYmd = yyyymmdd(start.toISOString().slice(0, 10));

  // 若 bundle 已存在，则只拉缺失的 ts_code
  const outPath = args.outPath || path.join(__dirname, '..', 'output', `daily_bundle_${date}.json`);
  let existing = null;
  if (fs.existsSync(outPath)) {
    try {
      existing = JSON.parse(fs.readFileSync(outPath, 'utf8'));
    } catch {
      existing = null;
    }
  }
  const existingSet = new Set(existing && Array.isArray(existing.ts_codes) ? existing.ts_codes : []);
  // 如果 bundle 已存在但缺少 amount 字段，也要重拉补齐
  const needAmountBackfill = !!(existing && Array.isArray(existing.fields) && !existing.fields.includes('amount'));
  const missing = needAmountBackfill ? tsCodes : tsCodes.filter((ts) => !existingSet.has(ts));

  // 如果没有缺失且不需要补字段，直接返回
  if (missing.length === 0) {
    process.stdout.write(JSON.stringify({ ok: true, outPath, stocks: existingSet.size, added: 0, skippedFetch: true }, null, 2));
    return;
  }

  const payload = {
    api_name: 'daily',
    token,
    params: { ts_code: missing.join(','), start_date: startYmd, end_date: end },
    fields: 'ts_code,trade_date,open,high,low,close,pct_chg,vol,amount',
  };

  const json = await postTushare(payload);
  if (!json || json.code !== 0) throw new Error(json ? json.msg : 'tushare error');
  const data = json.data;
  const idx = Object.fromEntries(data.fields.map((f, i) => [f, i]));

  const map = {};
  for (const it of data.items) {
    const ts = it[idx.ts_code];
    const td = it[idx.trade_date];
    if (!ts || !td) continue;
    const row = {
      trade_date: td,
      open: Number(it[idx.open]),
      high: Number(it[idx.high]),
      low: Number(it[idx.low]),
      close: Number(it[idx.close]),
      pct_chg: Number(it[idx.pct_chg]),
      vol: Number(it[idx.vol]),
      amount: Number(it[idx.amount]),
    };
    if (!map[ts]) map[ts] = [];
    map[ts].push(row);
  }

  for (const ts of Object.keys(map)) {
    map[ts].sort((a, b) => a.trade_date.localeCompare(b.trade_date));
  }

  fs.mkdirSync(path.dirname(outPath), { recursive: true });

  // 增量合并: merge 到 existing.items

  const mergedItems = (existing && existing.items && typeof existing.items === 'object') ? existing.items : {};
  // merge new map into mergedItems
  for (const [ts, rows] of Object.entries(map)) {
    mergedItems[ts] = rows;
  }

  const mergedTsCodes = Array.from(new Set([...(existing && Array.isArray(existing.ts_codes) ? existing.ts_codes : []), ...tsCodes])).sort();

  const out = {
    date,
    days_calendar_back: days,
    fields: ['open', 'high', 'low', 'close', 'pct_chg', 'vol', 'amount'],
    ts_codes: mergedTsCodes,
    items: mergedItems,
  };

  fs.writeFileSync(outPath, JSON.stringify(out, null, 2));

  // 统计补齐数量
  const existingCount = existing && existing.ts_codes ? existing.ts_codes.length : 0;
  const added = mergedTsCodes.length - existingCount;
  process.stdout.write(JSON.stringify({ ok: true, outPath, stocks: mergedTsCodes.length, added }, null, 2));
}

main().catch((e) => {
  process.stderr.write(String(e.stack || e.message || e) + '\n');
  process.exit(1);
});
