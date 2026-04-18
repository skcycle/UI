/**
 * 巴菲特-评分维度5: 技术维度(基于 daily_bundle) 0~15分
 *
 * 指标: MA, MACD, KDJ, BOLL, RSI
 * 结构: base(0~13) + synergy(-2~+2) => tech_15(0~15)
 *
 * 输入:
 *  - input/candidate_stocks_<date>.json
 *  - output/daily_bundle_<date>.json (自动增量补齐)
 *
 * 输出:
 *  - output/technical_scored_<date>.json
 */

const fs = require('fs');
const path = require('path');
const { spawnSync } = require('child_process');

function parseArgs(argv) {
  const out = {};
  for (let i = 2; i < argv.length; i++) {
    const a = argv[i];
    if (a === '--date') out.date = argv[++i];
    else if (a === '--in') out.inPath = argv[++i];
    else if (a === '--out') out.outPath = argv[++i];
  }
  return out;
}

function normalizeDate(s) {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(s)) throw new Error(`invalid --date: ${s}`);
  return s;
}

function clamp(x, a, b) {
  return Math.max(a, Math.min(b, x));
}

function sma(values, n) {
  if (values.length < n) return null;
  let sum = 0;
  for (let i = values.length - n; i < values.length; i++) sum += values[i];
  return sum / n;
}

function emaSeries(values, n) {
  if (values.length < n) return null;
  const k = 2 / (n + 1);
  let ema = values[0];
  const out = [];
  for (let i = 0; i < values.length; i++) {
    ema = i === 0 ? values[0] : values[i] * k + ema * (1 - k);
    out.push(ema);
  }
  return out;
}

function stddev(values, n) {
  if (values.length < n) return null;
  const slice = values.slice(-n);
  const mean = slice.reduce((a, b) => a + b, 0) / n;
  const varr = slice.reduce((a, b) => a + (b - mean) ** 2, 0) / n;
  return Math.sqrt(varr);
}

function rsi(values, n = 14) {
  if (values.length < n + 1) return null;
  let gains = 0;
  let losses = 0;
  for (let i = values.length - n; i < values.length; i++) {
    const diff = values[i] - values[i - 1];
    if (diff > 0) gains += diff;
    else losses -= diff;
  }
  if (losses === 0) return 100;
  const rs = gains / losses;
  return 100 - 100 / (1 + rs);
}

function kdj(rows, n = 9) {
  if (rows.length < n) return null;
  const win = rows.slice(-n);
  const lowN = Math.min(...win.map((r) => r.low));
  const highN = Math.max(...win.map((r) => r.high));
  const close = win[win.length - 1].close;
  const rsv = highN === lowN ? 50 : ((close - lowN) / (highN - lowN)) * 100;
  // 简化: 采用常见递推初值 50
  let K = 50;
  let D = 50;
  // 用窗口内 RSV 迭代
  for (let i = 0; i < win.length; i++) {
    const c = win[i].close;
    const lo = Math.min(...win.slice(0, i + 1).map((r) => r.low));
    const hi = Math.max(...win.slice(0, i + 1).map((r) => r.high));
    const rsv_i = hi === lo ? 50 : ((c - lo) / (hi - lo)) * 100;
    K = (2 / 3) * K + (1 / 3) * rsv_i;
    D = (2 / 3) * D + (1 / 3) * K;
  }
  const J = 3 * K - 2 * D;
  return { K, D, J, rsv };
}

function macd(values) {
  // DIF=EMA12-EMA26, DEA=EMA9(DIF), hist = DIF-DEA
  if (values.length < 35) return null;
  const ema12 = emaSeries(values, 12);
  const ema26 = emaSeries(values, 26);
  if (!ema12 || !ema26) return null;
  const dif = values.map((_, i) => ema12[i] - ema26[i]);
  const dea = emaSeries(dif, 9);
  if (!dea) return null;
  const hist = dif.map((v, i) => v - dea[i]);
  const n = values.length;
  const cross = dif[n - 2] <= dea[n - 2] && dif[n - 1] > dea[n - 1] ? 'golden' :
                dif[n - 2] >= dea[n - 2] && dif[n - 1] < dea[n - 1] ? 'death' : null;
  const slope3 = (dif[n - 1] - dif[n - 3]);
  return { dif: dif[n - 1], dea: dea[n - 1], hist: hist[n - 1], cross, slope3 };
}

function computeTech(ts, series) {
  const tags = [];
  const missing = [];

  const closes = series.map((r) => r.close);
  const last = series[series.length - 1];

  // MA
  const ma5 = sma(closes, 5); if (ma5 === null) missing.push('ma5');
  const ma10 = sma(closes, 10); if (ma10 === null) missing.push('ma10');
  const ma20 = sma(closes, 20); if (ma20 === null) missing.push('ma20');

  let A = 0;
  if (ma20 !== null && last.close > ma20) { A += 1.5; tags.push('close_above_ma20'); }
  if (ma5 !== null && ma10 !== null) {
    if (ma5 > ma10) { A += 1.0; tags.push('ma5_gt_ma10'); }
    else { A -= 1.0; tags.push('ma5_lt_ma10'); }
  }
  if (ma10 !== null && ma20 !== null) {
    if (ma10 > ma20) { A += 1.5; tags.push('ma10_gt_ma20'); }
    else { A -= 1.5; tags.push('ma10_lt_ma20'); }
  }
  A = clamp(A, 0, 4);

  // MACD (short-term: downweight)
  const m = macd(closes);
  let B = 0;
  if (!m) {
    missing.push('macd');
  } else {
    // reduce weights vs previous (1.5/1.0/0.5)
    if (m.cross === 'golden') { B += 0.8; tags.push('macd_golden_cross'); }
    if (m.hist > 0) { B += 0.6; tags.push('macd_hist_pos'); }
    if (m.slope3 > 0) { B += 0.3; tags.push('dif_up_3d'); }
    B = clamp(B, 0, 2);
  }

  // KDJ (short-term: upweight)
  const k = kdj(series, 9);
  let C = 0;
  if (!k) {
    missing.push('kdj');
  } else {
    if (k.K > k.D) { C += 1.2; tags.push('kdj_k_gt_d'); }
    if (k.J > k.K && k.K > k.D) { C += 1.0; tags.push('kdj_bull_stack'); }
    if (k.K < 20 && k.K > k.D) { C += 0.6; tags.push('kdj_oversold_rebound'); }
    // add mild penalty for overheat (helps 3~5d)
    if (k.K > 85 && k.J > 95) { C -= 0.4; tags.push('kdj_overheat'); }
    C = clamp(C, 0, 3);
  }

  // BOLL
  let D = 0;
  const mid = sma(closes, 20);
  const sd = stddev(closes, 20);
  if (mid === null || sd === null) {
    missing.push('boll');
  } else {
    const upper = mid + 2 * sd;
    const lower = mid - 2 * sd;
    const bw = (upper - lower) / mid;
    if (last.close > mid) { D += 0.8; tags.push('boll_above_mid'); }
    if ((upper - last.close) / mid < 0.015) { D += 0.7; tags.push('boll_near_upper'); }
    // bandwidth expansion vs last 10 days avg
    if (closes.length >= 30) {
      const bws = [];
      for (let i = closes.length - 10; i < closes.length; i++) {
        const mid_i = sma(closes.slice(0, i + 1), 20);
        const sd_i = stddev(closes.slice(0, i + 1), 20);
        if (mid_i && sd_i) bws.push(((mid_i + 2 * sd_i) - (mid_i - 2 * sd_i)) / mid_i);
      }
      const avgBw = bws.length ? bws.reduce((a, b) => a + b, 0) / bws.length : null;
      if (avgBw && bw > avgBw) { D += 0.5; tags.push('boll_bandwidth_expand'); }
    }
    D = clamp(D, 0, 2);
  }

  // RSI (short-term: upweight)
  let E = 0;
  const r = rsi(closes, 14);
  if (r === null) {
    missing.push('rsi');
  } else {
    if (r >= 50 && r <= 70) { E = 2.4; tags.push('rsi_50_70'); }
    else if ((r >= 40 && r < 50) || (r > 70 && r < 80)) { E = 1.2; tags.push('rsi_mid'); }
    else if (r < 30) { E = 1.0; tags.push('rsi_oversold'); }
    else if (r >= 80) { E = 0.2; tags.push('rsi_overheat'); }
    else { E = 0.6; }
    // oversold needs confirmation
    if (r < 30 && !(k && k.K > k.D)) E = 0.3;
    E = clamp(E, 0, 3);
  }

  let base = A + B + C + D + E; // short-term weights adjusted; max ~14

  // synergy
  let syn = 0;
  const bullStack = ma5 !== null && ma10 !== null && ma20 !== null && ma5 > ma10 && ma10 > ma20;
  if (ma20 !== null && last.close > ma20 && bullStack) { syn += 1.0; tags.push('syn_trend'); }
  if (m && m.hist > 0 && (k && k.K > k.D) && (r !== null && r > 50)) { syn += 1.0; tags.push('syn_momentum'); }

  // negative synergy
  if (r !== null && r >= 80 && tags.includes('boll_near_upper')) { syn -= 1.0; tags.push('syn_overheat_risk'); }
  if (ma20 !== null && last.close < ma20 && m && m.hist < 0 && ((k && k.K < k.D) || (r !== null && r < 40))) {
    syn -= 1.0;
    tags.push('syn_down_risk');
  }
  syn = clamp(syn, -2, 2);

  const tech15 = Math.round(clamp(base + syn, 0, 15) * 100) / 100;

  return {
    ts_code: ts,
    tech_15: tech15,
    base,
    synergy: syn,
    indicators: {
      ma5,
      ma10,
      ma20,
      macd: m,
      kdj: k,
      boll: mid === null || sd === null ? null : { mid, upper: mid + 2 * sd, lower: mid - 2 * sd },
      rsi14: r,
    },
    tags,
    missing,
  };
}

async function main() {
  const args = parseArgs(process.argv);
  const date = normalizeDate(args.date);

  const inPath = args.inPath || path.join(__dirname, '..', 'input', `candidate_stocks_${date}.json`);
  if (!fs.existsSync(inPath)) throw new Error(`missing input: ${inPath}`);
  const cand = JSON.parse(fs.readFileSync(inPath, 'utf8'));
  const tsCodes = Array.isArray(cand.candidate_stocks) ? cand.candidate_stocks : [];

  const token = process.env.TUSHARE_TOKEN;
  if (!token) throw new Error('missing TUSHARE_TOKEN');

  const bundlePath = path.join(__dirname, '..', 'output', `daily_bundle_${date}.json`);
  const prefetch = path.join(__dirname, 'prefetch_daily_bundle.js');

  // 若 bundle 不存在或缺少候选股，则增量补齐；否则直接使用现有 bundle
  let needPrefetch = true;
  if (fs.existsSync(bundlePath)) {
    try {
      const b0 = JSON.parse(fs.readFileSync(bundlePath, 'utf8'));
      const have = new Set(Array.isArray(b0.ts_codes) ? b0.ts_codes : []);
      needPrefetch = tsCodes.some((ts) => !have.has(ts));
    } catch {
      needPrefetch = true;
    }
  }

  if (needPrefetch) {
    const pre = spawnSync(process.execPath, [prefetch, '--date', date, '--in', inPath], {
      env: { ...process.env, TUSHARE_TOKEN: token },
      encoding: 'utf8',
    });
    if (pre.status !== 0) throw new Error(pre.stderr || `prefetch failed: ${pre.status}`);
  }

  if (!fs.existsSync(bundlePath)) throw new Error(`missing bundle: ${bundlePath}`);
  const bundle = JSON.parse(fs.readFileSync(bundlePath, 'utf8'));

  const items = [];
  for (const ts of tsCodes) {
    const series = (bundle.items && bundle.items[ts]) ? bundle.items[ts] : [];
    // need enough rows
    if (!Array.isArray(series) || series.length < 20) {
      items.push({ ts_code: ts, tech_15: null, reason: 'not enough bundle rows', rows: series.length });
      continue;
    }
    // map numeric
    const cleaned = series
      .map((r) => ({
        trade_date: r.trade_date,
        open: Number(r.open),
        high: Number(r.high),
        low: Number(r.low),
        close: Number(r.close),
        pct_chg: Number(r.pct_chg),
      }))
      .filter((r) => [r.open, r.high, r.low, r.close, r.pct_chg].every(Number.isFinite));

    const last60 = cleaned.slice(-60);
    items.push(computeTech(ts, last60));
  }

  const scored = items.filter((x) => x.tech_15 !== null).sort((a, b) => b.tech_15 - a.tech_15 || a.ts_code.localeCompare(b.ts_code));

  const out = {
    date,
    dimension: 'technical',
    max_score: 15,
    structure: { base_max: 13, synergy_range: [-2, 2] },
    input_count: tsCodes.length,
    scored_count: scored.length,
    items: scored,
    skipped: items.filter((x) => x.tech_15 === null),
  };

  const outPath = args.outPath || path.join(__dirname, '..', 'output', `technical_scored_${date}.json`);
  fs.mkdirSync(path.dirname(outPath), { recursive: true });
  fs.writeFileSync(outPath, JSON.stringify(out, null, 2));

  process.stdout.write(JSON.stringify({ ok: true, outPath, top5: scored.slice(0, 5) }, null, 2));
}

main().catch((e) => {
  process.stderr.write(String(e.stack || e.message || e) + '\n');
  process.exit(1);
});
