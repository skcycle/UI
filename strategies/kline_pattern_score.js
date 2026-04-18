/**
 * 巴菲特-评分维度4: K线形态(近20交易日) 0~15分
 *
 * 目标: 同时覆盖趋势信号与反转信号，通过多标签加减分拉开个股差异。
 * 数据源: TuShare daily (open,high,low,close,pct_chg)
 * 输入: input/candidate_stocks_<date>.json
 * 输出: output/kline_pattern_scored_<date>.json
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
    else if (a === '--lookback') out.lookback = Number(argv[++i]);
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

function sma(arr, n) {
  if (arr.length < n) return null;
  const out = new Array(arr.length).fill(null);
  let sum = 0;
  for (let i = 0; i < arr.length; i++) {
    sum += arr[i];
    if (i >= n) sum -= arr[i - n];
    if (i >= n - 1) out[i] = sum / n;
  }
  return out;
}

function candle(x) {
  const o = x.open,
    c = x.close,
    h = x.high,
    l = x.low;
  const body = Math.abs(c - o);
  const range = Math.max(1e-9, h - l);
  const upper = h - Math.max(o, c);
  const lower = Math.min(o, c) - l;
  return { o, c, h, l, body, range, upper, lower, bodyPct: body / range };
}

function near(a, b, eps) {
  return Math.abs(a - b) <= eps;
}

function detectPatterns(rows) {
  // rows sorted asc by trade_date
  const n = rows.length;
  const tags = [];
  let raw = 0;

  const closes = rows.map((r) => r.close);
  const ma5 = sma(closes, 5);
  const ma10 = sma(closes, 10);
  const ma20 = sma(closes, 20);

  const last = rows[n - 1];
  const prev = rows[n - 2];
  const prev2 = rows[n - 3];

  const c0 = candle(last);
  const c1 = candle(prev);
  const c2 = candle(prev2);

  // helper: position
  const high20 = Math.max(...closes);
  const low20 = Math.min(...closes);
  const pos = (last.close - low20) / Math.max(1e-9, high20 - low20); // 0..1

  // Trend env
  if (ma5[n - 1] && ma10[n - 1]) {
    if (ma5[n - 1] > ma10[n - 1] && last.close > ma5[n - 1]) {
      tags.push({ tag: 'trend_up_ma', score: 1.0 });
      raw += 1.0;
    }
    if (ma5[n - 1] < ma10[n - 1] && last.close < ma5[n - 1]) {
      tags.push({ tag: 'trend_down_ma', score: -1.0 });
      raw -= 1.0;
    }
  }

  // Breakout / breakdown (close)
  const closes19 = closes.slice(0, -1);
  const prevHigh = Math.max(...closes19);
  const prevLow = Math.min(...closes19);
  if (last.close > prevHigh) {
    tags.push({ tag: 'close_breakout_20', score: 1.5 });
    raw += 1.5;
  }
  if (last.close < prevLow) {
    tags.push({ tag: 'close_breakdown_20', score: -1.5 });
    raw -= 1.5;
  }

  // Single-candle patterns
  // Big bull/bear
  if (last.close > last.open && c0.bodyPct >= 0.65 && last.pct_chg > 2) {
    tags.push({ tag: 'big_bull', score: 2.0 });
    raw += 2.0;
  }
  if (last.close < last.open && c0.bodyPct >= 0.65 && last.pct_chg < -2) {
    tags.push({ tag: 'big_bear', score: -2.0 });
    raw -= 2.0;
  }

  // Doji / long-legged
  if (c0.bodyPct <= 0.10) {
    const s = pos < 0.3 ? 0.6 : pos > 0.7 ? -0.6 : 0;
    tags.push({ tag: 'doji', score: s });
    raw += s;
    if (c0.upper / c0.range > 0.35 && c0.lower / c0.range > 0.35) {
      tags.push({ tag: 'long_legged_doji', score: s !== 0 ? s : -0.2 });
      raw += s !== 0 ? 0 : -0.2;
    }
  }

  // Hammer / hanging man / shooting star / inverted hammer
  if (c0.lower >= 2 * c0.body && c0.upper <= 0.5 * c0.body && c0.body > 0) {
    // hammer-like
    const s = pos < 0.4 ? 1.5 : -0.5;
    tags.push({ tag: pos < 0.4 ? 'hammer' : 'hanging_man', score: s });
    raw += s;
  }
  if (c0.upper >= 2 * c0.body && c0.lower <= 0.5 * c0.body && c0.body > 0) {
    const s = pos > 0.6 ? -1.2 : 0.8;
    tags.push({ tag: pos > 0.6 ? 'shooting_star' : 'inverted_hammer', score: s });
    raw += s;
  }

  // Spinning top
  if (c0.bodyPct > 0.10 && c0.bodyPct < 0.30 && c0.upper / c0.range > 0.25 && c0.lower / c0.range > 0.25) {
    const s = 0;
    tags.push({ tag: 'spinning_top', score: s });
  }

  // Two-candle: Engulfing
  const prevBodyHigh = Math.max(prev.open, prev.close);
  const prevBodyLow = Math.min(prev.open, prev.close);
  const curBodyHigh = Math.max(last.open, last.close);
  const curBodyLow = Math.min(last.open, last.close);

  if (prev.close < prev.open && last.close > last.open && curBodyHigh >= prevBodyHigh && curBodyLow <= prevBodyLow) {
    tags.push({ tag: 'bullish_engulfing', score: 2.5 });
    raw += 2.5;
  }
  if (prev.close > prev.open && last.close < last.open && curBodyHigh >= prevBodyHigh && curBodyLow <= prevBodyLow) {
    tags.push({ tag: 'bearish_engulfing', score: -2.5 });
    raw -= 2.5;
  }

  // Harami
  if (c1.bodyPct >= 0.6 && c0.bodyPct <= 0.3 && curBodyHigh <= prevBodyHigh && curBodyLow >= prevBodyLow) {
    const s = prev.close < prev.open ? 1.0 : -1.0;
    tags.push({ tag: prev.close < prev.open ? 'bullish_harami' : 'bearish_harami', score: s });
    raw += s;
  }

  // Piercing line / Dark cloud cover
  const midPrev = (prev.open + prev.close) / 2;
  if (prev.close < prev.open && last.open < prev.low && last.close > midPrev) {
    tags.push({ tag: 'piercing_line', score: 2.0 });
    raw += 2.0;
  }
  if (prev.close > prev.open && last.open > prev.high && last.close < midPrev) {
    tags.push({ tag: 'dark_cloud_cover', score: -2.0 });
    raw -= 2.0;
  }

  // Tweezers top/bottom (near same high/low)
  const eps = (c0.range + c1.range) / 2 * 0.02;
  if (near(c0.h, c1.h, eps) && pos > 0.6) {
    tags.push({ tag: 'tweezers_top', score: -1.2 });
    raw -= 1.2;
  }
  if (near(c0.l, c1.l, eps) && pos < 0.4) {
    tags.push({ tag: 'tweezers_bottom', score: 1.2 });
    raw += 1.2;
  }

  // Gaps
  if (last.low > prev.high) {
    tags.push({ tag: 'gap_up', score: 1.0 });
    raw += 1.0;
  }
  if (last.high < prev.low) {
    tags.push({ tag: 'gap_down', score: -1.0 });
    raw -= 1.0;
  }

  // Three-candle: Morning/Evening star (simplified)
  if (prev2.close < prev2.open && c1.bodyPct <= 0.3 && last.close > last.open && last.close >= (prev2.open + prev2.close) / 2) {
    tags.push({ tag: 'morning_star', score: 3.0 });
    raw += 3.0;
  }
  if (prev2.close > prev2.open && c1.bodyPct <= 0.3 && last.close < last.open && last.close <= (prev2.open + prev2.close) / 2) {
    tags.push({ tag: 'evening_star', score: -3.0 });
    raw -= 3.0;
  }

  // Three white soldiers / black crows
  const last3 = rows.slice(-3);
  const up3 = last3.every((r) => r.close > r.open) && last3[0].close < last3[1].close && last3[1].close < last3[2].close;
  const down3 = last3.every((r) => r.close < r.open) && last3[0].close > last3[1].close && last3[1].close > last3[2].close;
  if (up3) {
    tags.push({ tag: 'three_white_soldiers', score: 2.0 });
    raw += 2.0;
  }
  if (down3) {
    tags.push({ tag: 'three_black_crows', score: -2.0 });
    raw -= 2.0;
  }

  // Rising/Falling three methods (very simplified)
  // Rising: strong up, then 2-3 small down within range, then up
  const last5 = rows.slice(-5);
  if (last5.length === 5) {
    const r0 = last5[0], r4 = last5[4];
    const cR0 = candle(r0);
    const cR4 = candle(r4);
    const mid = last5.slice(1, 4);
    const midSmall = mid.every((r) => candle(r).bodyPct < 0.35);
    const midInside = mid.every((r) => r.high <= r0.high && r.low >= r0.low);
    if (r0.close > r0.open && r4.close > r4.open && midSmall && midInside && r4.close > r0.close) {
      tags.push({ tag: 'rising_three_methods', score: 2.0 });
      raw += 2.0;
    }
    if (r0.close < r0.open && r4.close < r4.open && midSmall && midInside && r4.close < r0.close) {
      tags.push({ tag: 'falling_three_methods', score: -2.0 });
      raw -= 2.0;
    }
  }

  // Normalize raw to 0~15
  const score15 = Math.round(clamp(raw + 7.5, 0, 15) * 100) / 100;
  return { raw, score15, tags };
}

async function main() {
  const args = parseArgs(process.argv);
  const date = normalizeDate(args.date);
  const token = args.token || process.env.TUSHARE_TOKEN;
  if (!token) throw new Error('missing TUSHARE_TOKEN');

  const lookback = Number.isFinite(args.lookback) && args.lookback >= 20 ? args.lookback : 20;

  const inPath = args.inPath || path.join(__dirname, '..', 'input', `candidate_stocks_${date}.json`);
  if (!fs.existsSync(inPath)) throw new Error(`missing input: ${inPath}`);
  const cand = JSON.parse(fs.readFileSync(inPath, 'utf8'));
  const tsCodes = Array.isArray(cand.candidate_stocks) ? cand.candidate_stocks : [];

  // date range: back 60 calendar days to cover 20 trading days
  const end = yyyymmdd(date);
  const endDate = new Date(date + 'T00:00:00+08:00');
  const start = new Date(endDate.getTime() - 60 * 24 * 3600 * 1000);
  const startYmd = yyyymmdd(start.toISOString().slice(0, 10));

  // 自动确保 daily_bundle 存在且已补齐（增量更新）
  const bundlePath = path.join(__dirname, '..', 'output', `daily_bundle_${date}.json`);
  const prefetch = path.join(__dirname, 'prefetch_daily_bundle.js');
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
        if (!r || typeof r.trade_date !== 'string') continue;
        const row = {
          trade_date: r.trade_date,
          open: Number(r.open),
          high: Number(r.high),
          low: Number(r.low),
          close: Number(r.close),
          pct_chg: Number(r.pct_chg),
        };
        if (![row.open, row.high, row.low, row.close, row.pct_chg].every(Number.isFinite)) continue;
        if (!map.has(ts)) map.set(ts, []);
        map.get(ts).push(row);
      }
    }
  } else {
    const payload = {
      api_name: 'daily',
      token,
      params: { ts_code: tsCodes.join(','), start_date: startYmd, end_date: end },
      fields: 'ts_code,trade_date,open,high,low,close,pct_chg',
    };

    const json = await postTushare(payload);
    if (!json || json.code !== 0) throw new Error(json ? json.msg : 'tushare error');
    const data = json.data;
    const idx = Object.fromEntries(data.fields.map((f, i) => [f, i]));

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
      };
      if (![row.open, row.high, row.low, row.close, row.pct_chg].every(Number.isFinite)) continue;
      if (!map.has(ts)) map.set(ts, []);
      map.get(ts).push(row);
    }
  }

  const items = [];
  for (const ts of tsCodes) {
    const rows = (map.get(ts) || []).sort((a, b) => a.trade_date.localeCompare(b.trade_date)).slice(-lookback);
    if (rows.length < 10) {
      items.push({ ts_code: ts, raw: null, score15: null, tags: [], reason: 'not enough rows' });
      continue;
    }
    const res = detectPatterns(rows);
    items.push({ ts_code: ts, ...res });
  }

  items.sort((a, b) => (b.score15 ?? -999) - (a.score15 ?? -999) || a.ts_code.localeCompare(b.ts_code));

  const out = {
    date,
    dimension: 'kline_pattern',
    max_score: 15,
    lookback_trading_days: lookback,
    items,
  };

  const outPath = args.outPath || path.join(__dirname, '..', 'output', `kline_pattern_scored_${date}.json`);
  fs.mkdirSync(path.dirname(outPath), { recursive: true });
  fs.writeFileSync(outPath, JSON.stringify(out, null, 2));

  process.stdout.write(JSON.stringify({ ok: true, outPath, top5: items.slice(0, 5) }, null, 2));
}

main().catch((e) => {
  process.stderr.write(String(e.stack || e.message || e) + '\n');
  process.exit(1);
});
