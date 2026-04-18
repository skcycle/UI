/**
 * 巴菲特-评分维度3: 量价协同(近5日) 0~15分（允许减分逻辑已内置在原始分）
 *
 * 数据源: TuShare daily (pct_chg, vol)
 * 输入: input/candidate_stocks_<date>.json
 * 输出: output/volume_price_sync_scored_<date>.json
 *
 * 连续版规则(覆盖最近4个相邻日对):
 *   对每一天 d(从第二天开始)，计算:
 *     ret = pct_chg/100
 *     dv = vol_d / vol_{d-1}
 *     s_d = ret * ln(dv)
 *   解释:
 *     ret>0 且 dv>1 => 正贡献(量价齐升)
 *     ret>0 且 dv<1 => 负贡献(价涨量缩背离)
 *     ret<0 且 dv>1 => 负贡献(价跌量增派发)
 *     ret<0 且 dv<1 => 正贡献(价跌量缩)
 *
 *   raw = sum(last4 s_d)
 *   score15 采用样本内分位映射，区分度更强:
 *     score15 = percentile_rank(raw) * 15
 *   并保留 raw 与逐日明细供解释。
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
  if (!/^\d{4}-\d{2}-\d{2}$/.test(s)) throw new Error(`invalid --date: ${s} (expect YYYY-MM-DD)`);
  return s;
}

function yyyymmdd(dateStr) {
  return dateStr.replace(/-/g, '');
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

async function main() {
  const args = parseArgs(process.argv);
  const date = normalizeDate(args.date);
  const token = args.token || process.env.TUSHARE_TOKEN;
  if (!token) throw new Error('missing TUSHARE_TOKEN');

  const windowDays = Number.isFinite(args.window) && args.window >= 5 ? args.window : 5;

  const inPath = args.inPath || path.join(__dirname, '..', 'input', `candidate_stocks_${date}.json`);
  if (!fs.existsSync(inPath)) throw new Error(`missing input: ${inPath}`);
  const cand = JSON.parse(fs.readFileSync(inPath, 'utf8'));
  const tsCodes = Array.isArray(cand.candidate_stocks) ? cand.candidate_stocks : [];
  if (!tsCodes.length) throw new Error('empty candidate_stocks');

  // 自动确保 daily_bundle 存在且已补齐（增量更新）
  const bundlePath = path.join(__dirname, '..', 'output', `daily_bundle_${date}.json`);
  const prefetch = path.join(__dirname, 'prefetch_daily_bundle.js');
  // 使用 node 同进程子调用，避免 exec 工具限制
  const { spawnSync } = require('child_process');
  const pre = spawnSync(process.execPath, [prefetch, '--date', date, '--in', inPath], {
    env: { ...process.env, TUSHARE_TOKEN: token },
    encoding: 'utf8',
  });
  if (pre.status !== 0) throw new Error(pre.stderr || `prefetch failed: ${pre.status}`);

  let bundle = null;
  if (fs.existsSync(bundlePath)) {
    bundle = JSON.parse(fs.readFileSync(bundlePath, 'utf8'));
  }

  const map = new Map();
  if (bundle && bundle.items) {
    for (const ts of tsCodes) {
      const rows = bundle.items[ts] || [];
      for (const r of rows) {
        if (r && typeof r.trade_date === 'string' && Number.isFinite(r.pct_chg) && Number.isFinite(r.vol)) {
          if (!map.has(ts)) map.set(ts, []);
          map.get(ts).push({ trade_date: r.trade_date, ret: r.pct_chg / 100, pct_chg: r.pct_chg, vol: r.vol });
        }
      }
    }
  } else {
    // fallback: 直接拉 TuShare
    const end = yyyymmdd(date);
    const startDate = new Date(date + 'T00:00:00+08:00');
    const start = new Date(startDate.getTime() - windowDays * 3 * 24 * 3600 * 1000);
    const startStr = start.toISOString().slice(0, 10);
    const startYmd = yyyymmdd(startStr);

    const payload = {
      api_name: 'daily',
      token,
      params: { ts_code: tsCodes.join(','), start_date: startYmd, end_date: end },
      fields: 'ts_code,trade_date,pct_chg,vol',
    };

    const json = await postTushare(payload);
    if (!json || json.code !== 0) throw new Error(json ? json.msg : 'tushare error');
    const data = json.data;
    const fields = data.fields;
    const items = data.items;
    const idx = Object.fromEntries(fields.map((f, i) => [f, i]));

    for (const it of items) {
      const ts = it[idx.ts_code];
      const td = it[idx.trade_date];
      const pct = Number(it[idx.pct_chg]);
      const vol = Number(it[idx.vol]);
      if (!ts || !td || !Number.isFinite(pct) || !Number.isFinite(vol)) continue;
      if (!map.has(ts)) map.set(ts, []);
      map.get(ts).push({ trade_date: td, ret: pct / 100, pct_chg: pct, vol });
    }
  }

  // keep last N trade days per stock
  const scored = [];
  for (const ts of tsCodes) {
    const rows = (map.get(ts) || []).sort((a, b) => a.trade_date.localeCompare(b.trade_date));
    const last = rows.slice(-windowDays);
    if (last.length < 5) {
      scored.push({ ts_code: ts, raw: null, score15: null, reason: 'not enough daily rows', series: last });
      continue;
    }

    const dailyScores = [];
    let raw = 0;
      // score for d=1..(n-1)
    for (let i = 1; i < last.length; i++) {
      const prev = last[i - 1];
      const cur = last[i];
      const dv = prev.vol > 0 ? cur.vol / prev.vol : null;
      const ret = cur.ret;
      let s = null;
      if (dv !== null && dv > 0) {
        s = ret * Math.log(dv);
      }
      dailyScores.push({ trade_date: cur.trade_date, ret, pct_chg: cur.pct_chg, vol: cur.vol, dv, s });
    }

    const lastTransitions = dailyScores.slice(-4);
    raw = lastTransitions.reduce((a, b) => a + (Number.isFinite(b.s) ? b.s : 0), 0);

    scored.push({ ts_code: ts, raw, transitions: lastTransitions });
  }

  // 根据 raw 做分位映射到 0~15
  const raws = scored.map((x) => x.raw).filter((x) => Number.isFinite(x));
  const sorted = [...raws].sort((a, b) => a - b);
  function pctRank(v) {
    if (!Number.isFinite(v) || sorted.length === 0) return 0;
    // <= v 的比例
    let lo = 0,
      hi = sorted.length;
    while (lo < hi) {
      const mid = (lo + hi) >> 1;
      if (sorted[mid] <= v) lo = mid + 1;
      else hi = mid;
    }
    const p = sorted.length === 1 ? 1 : (lo - 1) / (sorted.length - 1);
    return clamp(p, 0, 1);
  }

  for (const x of scored) {
    if (!Number.isFinite(x.raw)) {
      x.score15 = null;
      continue;
    }
    x.score15 = Math.round(pctRank(x.raw) * 15 * 100) / 100;
  }

  scored.sort((a, b) => (b.score15 ?? -999) - (a.score15 ?? -999) || a.ts_code.localeCompare(b.ts_code));

  const out = {
    date,
    dimension: 'volume_price_sync',
    max_score: 15,
    window_days: windowDays,
    mapping: { method: 'percentile_rank', to: '0~15' },
    rules: {
      formula: 's_d = ret_d * ln(vol_d/vol_{d-1}); raw = sum(last4 s_d)',
    },
    input_count: tsCodes.length,
    items: scored,
  };

  const outPath = args.outPath || path.join(__dirname, '..', 'output', `volume_price_sync_scored_${date}.json`);
  fs.mkdirSync(path.dirname(outPath), { recursive: true });
  fs.writeFileSync(outPath, JSON.stringify(out, null, 2));

  process.stdout.write(JSON.stringify({ ok: true, outPath, top5: scored.slice(0, 5) }, null, 2));
}

main().catch((e) => {
  process.stderr.write(String(e.stack || e.message || e) + '\n');
  process.exit(1);
});
