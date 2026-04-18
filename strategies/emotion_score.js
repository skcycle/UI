/**
 * 巴菲特-评分维度7: 情绪维度(实时) 0~10分
 *
 * 数据源:
 *  - 麦瑞 ssjy_more：今日实时 p/o/h/l
 *  - daily_bundle：昨日/近几日 close/pct_chg
 *
 * 输出: output/emotion_scored_<date>.json
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
    else if (a === '--license' || a === '--licence') out.license = argv[++i];
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

function httpGetJson(url) {
  return new Promise((resolve, reject) => {
    https
      .get(url, (res) => {
        let data = '';
        res.on('data', (c) => (data += c));
        res.on('end', () => {
          if (res.statusCode && res.statusCode >= 400) return reject(new Error(`HTTP ${res.statusCode}: ${data.slice(0, 200)}`));
          try {
            resolve(JSON.parse(data));
          } catch (e) {
            reject(new Error(`JSON parse failed: ${e.message}; body(head)=${data.slice(0, 200)}`));
          }
        });
      })
      .on('error', reject);
  });
}

function yyyymmdd(s) {
  return s.replace(/-/g, '');
}

function getLastNDays(series, n) {
  return series.slice(-n);
}

function safeNum(x) {
  const v = Number(x);
  return Number.isFinite(v) ? v : null;
}

function scoreEmotion(ctx) {
  const { y_ret, open_gap, open_ret, open_to_high, open_to_low, cum_3d, cum_5d, ret_3d, r3dd, streaks } = ctx;

  let S1 = 0,
    S2 = 0,
    S3 = 0,
    S4 = 0,
    S5 = 0;
  const tags = [];

  // S1 yesterday strong validate (focus on open)
  if (y_ret !== null && y_ret >= 0.07) {
    if (open_gap !== null && open_ret !== null && open_gap <= -0.01 && open_ret <= -0.005) {
      S1 = -2;
      tags.push('strong_y_low_open');
    } else if (open_gap !== null && open_to_low !== null && open_gap >= 0.01 && open_to_low <= -0.02) {
      S1 = -1.5;
      tags.push('strong_y_open_then_drop');
    } else if (open_gap !== null && open_ret !== null && open_to_low !== null && open_gap >= 0 && open_ret >= 0.01 && open_to_low > -0.01) {
      S1 = 2;
      tags.push('strong_y_good_open');
    } else if (open_ret !== null && open_ret > 0) {
      S1 = 1;
      tags.push('strong_y_soft_open');
    }
  }

  // S2 yesterday big drop reversal (focus on open)
  if (y_ret !== null && y_ret <= -0.05) {
    if (open_gap !== null && open_ret !== null && open_gap <= -0.01 && open_ret <= -0.01) {
      S2 = -2;
      tags.push('panic_low_open');
    } else if (open_gap !== null && open_ret !== null && open_to_high !== null && open_gap >= 0 && open_ret >= 0.003 && open_to_high >= 0.01) {
      S2 = 2;
      tags.push('reversal_open_strong');
    } else if (open_ret !== null && open_to_high !== null && open_ret > -0.005 && open_to_high >= 0.015) {
      S2 = 1;
      tags.push('reversal_open_soft');
    }
  }

  // S3 ice point after consecutive drops (focus on open)
  let downDays3 = 0;
  if (Array.isArray(ret_3d)) downDays3 = ret_3d.filter((x) => x < 0).length;
  const iceCond = (downDays3 >= 2 && cum_3d !== null && cum_3d <= -0.08) || (cum_5d !== null && cum_5d <= -0.12);
  if (iceCond) {
    if (open_gap !== null && open_to_high !== null && open_gap >= -0.005 && open_gap <= 0.005 && open_to_high >= 0.015) {
      S3 = 2;
      tags.push('ice_open_rebound');
    } else if (open_ret !== null && open_to_low !== null && open_ret <= -0.01 && open_to_low <= -0.02) {
      S3 = -2;
      tags.push('ice_open_break');
    } else if (open_ret !== null && open_ret > -0.005) {
      S3 = 1;
      tags.push('ice_open_stabilize');
    }
  }

  // S4 open-to-low risk
  if (open_to_low !== null) {
    if (open_to_low <= -0.03) {
      S4 = -2;
      tags.push('open_big_drop');
    } else if (open_to_low <= -0.02) {
      S4 = -1;
      tags.push('open_drop');
    }
  }

  // S5 open emotion (gap only)
  if (open_gap !== null) {
    if (open_gap >= 0.02) {
      S5 = 2;
      tags.push('gap_up');
    } else if (open_gap >= 0.01) {
      S5 = 1;
      tags.push('gap_up_small');
    } else if (open_gap <= -0.02) {
      S5 = -2;
      tags.push('gap_down');
    } else if (open_gap <= -0.01) {
      S5 = -1;
      tags.push('gap_down_small');
    }
  }

  // S6 R3DD: 近3日收益/回撤结构 (A: 拉开区间)
  // r3dd = { ret_3d, mdd_3d, ratio }
  let S6 = 0;
  if (r3dd && Number.isFinite(r3dd.ret_3d) && Number.isFinite(r3dd.mdd_3d)) {
    const r = r3dd.ret_3d;
    const m = r3dd.mdd_3d;
    // reward strong return with tight drawdown
    if (r >= 0.08 && m <= 0.025) {
      S6 = 4;
      tags.push('r3dd_very_strong_tight');
    } else if (r >= 0.06 && m <= 0.03) {
      S6 = 3;
      tags.push('r3dd_strong_low_mdd');
    } else if (r >= 0.04 && m <= 0.04) {
      S6 = 2;
      tags.push('r3dd_good');
    } else if (r >= 0.03 && m <= 0.06) {
      S6 = 1;
      tags.push('r3dd_ok');
    }

    // penalties: high drawdown, or negative return with drawdown
    if (m >= 0.10) {
      S6 = Math.min(S6, -4);
      tags.push('r3dd_mdd_10p');
    } else if (m >= 0.08) {
      S6 = Math.min(S6, -3);
      tags.push('r3dd_mdd_8p');
    } else if (m >= 0.06 && r < 0.03) {
      S6 = Math.min(S6, -2);
      tags.push('r3dd_high_mdd');
    }
    if (r < 0 && m >= 0.06) {
      S6 = Math.min(S6, -4);
      tags.push('r3dd_weak_high_mdd');
    }
  }

  // S7 intraday risk (B: 短线风险惩罚)
  // 1) long upper shadow: (h - max(o,p)) / o
  // 2) fade: close below open after gap up
  let S7 = 0;
  if (open_to_high !== null && open_to_low !== null && open_ret !== null) {
    const upper = open_to_high; // approx upper move from open
    const lower = -open_to_low; // magnitude
    // long upper shadow proxy: big up but weak close often shows as large upper with pullback
    if (upper >= 0.04 && lower >= 0.02) {
      S7 -= 2;
      tags.push('intraday_spike_and_fade');
    } else if (upper >= 0.03 && lower >= 0.015) {
      S7 -= 1;
      tags.push('intraday_fade');
    }

    // gap up then weak day (open gap up but intraday drawdown large)
    if (open_ret >= 0.02 && open_to_low <= -0.03) {
      S7 -= 2;
      tags.push('gap_up_failed');
    } else if (open_ret >= 0.01 && open_to_low <= -0.025) {
      S7 -= 1;
      tags.push('gap_up_weak');
    }
  }

  const raw = S1 + S2 + S3 + S4 + S5 + S6 + S7;

  // S8 连续性情绪信号：连续高开/收阳/上涨
  let S8 = 0;
  if (streaks) {
    const { gapUpStreak = 0, bullStreak = 0, upStreak = 0 } = streaks;
    // 连续高开
    if (gapUpStreak >= 3) { S8 += 4; tags.push('gap_up_3d'); }
    else if (gapUpStreak >= 2) { S8 += 2; tags.push('gap_up_2d'); }
    else if (gapUpStreak >= 1) { S8 += 0.5; tags.push('gap_up_1d'); }

    // 连续收阳
    if (bullStreak >= 3) { S8 += 3; tags.push('bull_3d'); }
    else if (bullStreak >= 2) { S8 += 1.5; tags.push('bull_2d'); }

    // 连续上涨
    if (upStreak >= 3) { S8 += 3; tags.push('up_3d'); }
    else if (upStreak >= 2) { S8 += 1.5; tags.push('up_2d'); }
  }

  const raw2 = raw + S8;
  // raw range widened after adding stronger S6 + S7 + S8; map with a wider window
  // expected raw roughly in [-16, +24]
  const emotion_10 = Math.round(clamp(((raw2 + 18) / 36) * 10, 0, 10) * 100) / 100;

  return { parts: { S1, S2, S3, S4, S5, S6, S7, S8 }, raw: raw2, emotion_10, tags };
}

async function main() {
  const args = parseArgs(process.argv);
  const date = normalizeDate(args.date);
  const license = args.license || process.env.MAIRUI_LICENSE;
  if (!license) throw new Error('missing MAIRUI_LICENSE');

  const inPath = args.inPath || path.join(__dirname, '..', 'input', `candidate_stocks_${date}.json`);
  if (!fs.existsSync(inPath)) throw new Error(`missing input: ${inPath}`);
  const cand = JSON.parse(fs.readFileSync(inPath, 'utf8'));
  const tsCodes = Array.isArray(cand.candidate_stocks) ? cand.candidate_stocks : [];

  // ensure bundle exists (use prefetch script, which is incremental)
  const token = process.env.TUSHARE_TOKEN;
  if (!token) throw new Error('missing TUSHARE_TOKEN');
  const prefetch = path.join(__dirname, 'prefetch_daily_bundle.js');
  const { spawnSync } = require('child_process');
  const pre = spawnSync(process.execPath, [prefetch, '--date', date, '--in', inPath], {
    env: { ...process.env, TUSHARE_TOKEN: token },
    encoding: 'utf8',
  });
  if (pre.status !== 0) throw new Error(pre.stderr || `prefetch failed: ${pre.status}`);

  const bundlePath = path.join(__dirname, '..', 'output', `daily_bundle_${date}.json`);
  const bundle = JSON.parse(fs.readFileSync(bundlePath, 'utf8'));

  // 非交易时间分支：用 TuShare daily 获取 open/high/low/close
  // 交易时间：用麦瑞 ssjy_more 获取实时 p/o/h/l
  const isTradingTimeNow = () => {
    const tz = 'Asia/Shanghai';
    const parts = new Intl.DateTimeFormat('en-GB', {
      timeZone: tz,
      hour: '2-digit',
      minute: '2-digit',
      hour12: false,
    }).formatToParts(new Date());
    const hh = Number(parts.find((p) => p.type === 'hour').value);
    const mm = Number(parts.find((p) => p.type === 'minute').value);
    const mins = hh * 60 + mm;
    return mins >= 9 * 60 + 30 && mins <= 17 * 60;
  };

  const rtMap = new Map();

  if (!isTradingTimeNow()) {
    // 用 bundle 当日的 open/high/low/close 作为今日情绪输入（等价于用 TuShare daily）
    // bundle 已由 prefetch_daily_bundle 维护，字段包含 open/high/low/close
    for (const ts of tsCodes) {
      const code = ts.split('.')[0].padStart(6, '0');
      const series = (bundle.items && bundle.items[ts]) ? bundle.items[ts] : [];
      if (!Array.isArray(series) || series.length < 1) continue;
      const d = series[series.length - 1];
      const o = safeNum(d.open);
      const h = safeNum(d.high);
      const l = safeNum(d.low);
      const p = safeNum(d.close);
      if (p === null || o === null || h === null || l === null) continue;
      rtMap.set(code, { dm: code, p, o, h, l, t: `${date} 15:00:00`, _source: 'tushare_daily' });
    }
  } else {
    // realtime fetch from ssjy_more (最多20只/次)
    const codes = tsCodes.map((ts) => ts.split('.')[0].padStart(6, '0'));
    const chunks = [];
    for (let i = 0; i < codes.length; i += 20) chunks.push(codes.slice(i, i + 20));

    // 并发上限10
    let idxFetch = 0;
    const runners = Array.from({ length: Math.min(10, chunks.length) }, async () => {
      while (idxFetch < chunks.length) {
        const i = idxFetch++;
        const c = chunks[i];
        const url = `https://api.mairuiapi.com/hsrl/ssjy_more/${encodeURIComponent(license)}?stock_codes=${encodeURIComponent(c.join(','))}`;
        const rt = await httpGetJson(url);
        const rtArr = Array.isArray(rt) ? rt : [];
        for (const r of rtArr) {
          if (!r || !r.dm) continue;
          const k = String(r.dm).trim().padStart(6, '0');
          rtMap.set(k, r);
        }
      }
    });
    await Promise.all(runners);
  }

  // quick sanity debug (kept in output when missing)
  const rtKeysSample = Array.from(rtMap.keys()).slice(0, 5);

  const items = [];
  for (const ts of tsCodes) {
    const code = ts.split('.')[0].padStart(6, '0');
    const series = (bundle.items && bundle.items[ts]) ? bundle.items[ts] : [];
    if (!Array.isArray(series) || series.length < 6) {
      items.push({ ts_code: ts, emotion_10: null, reason: 'not enough bundle rows' });
      continue;
    }
    // bundle 最后一条通常就是 date，当日实时情绪需要用“上一交易日”作为昨收基准
    const y = series[series.length - 1]; // 上一交易日(通常就是 date)
    const yprev = series[series.length - 2]; // 再往前一日
    const close_y = safeNum(y.close);
    const y_ret = safeNum(y.pct_chg);
    const y_ret_d = y_ret === null ? null : y_ret / 100;

    // 近3/5日累计使用 close_y(上一交易日) 相对更早收盘
    const ret3 = getLastNDays(series, 4)
      .slice(0, -1)
      .map((r) => safeNum(r.pct_chg))
      .filter((x) => x !== null)
      .map((x) => x / 100);

    const close_t3 = safeNum(series[series.length - 4].close);
    const cum_3d = close_y !== null && close_t3 !== null ? close_y / close_t3 - 1 : null;

    // R3DD inputs: use last 4 closes (t-3..t) where t is close_y (latest in bundle)
    const closes4 = getLastNDays(series, 4).map((r) => safeNum(r.close));
    const hasCloses4 = closes4.length === 4 && closes4.every((x) => x !== null);
    let r3dd = null;
    if (hasCloses4) {
      const c0 = closes4[0], c1 = closes4[1], c2 = closes4[2], c3 = closes4[3];
      const ret_3d = c0 > 0 ? c3 / c0 - 1 : null;
      // max drawdown over the 4 closes sequence
      let peak = c0;
      let mdd = 0;
      for (const c of [c1, c2, c3]) {
        if (c > peak) peak = c;
        const dd = peak > 0 ? (peak - c) / peak : 0;
        if (dd > mdd) mdd = dd;
      }
      const ratio = ret_3d !== null ? ret_3d / (mdd + 1e-6) : null;
      r3dd = { ret_3d, mdd_3d: mdd, ratio };
    }

    const close_t5 = safeNum(series[series.length - 6].close);
    const cum_5d = close_y !== null && close_t5 !== null ? close_y / close_t5 - 1 : null;

    // 连续性情绪信号：用近4天的 open/close
    // series 最后一条是 date，往前依次是 t-1, t-2, t-3
    const days4 = series.slice(-4);
    const opens4 = days4.map((r) => safeNum(r.open));
    const closes4raw = days4.map((r) => safeNum(r.close));

    // 连续高开：open[i] > close[i-1]
    let gapUpStreak = 0;
    for (let i = 1; i < days4.length; i++) {
      const o = opens4[i];
      const prevC = closes4raw[i - 1];
      if (o !== null && prevC !== null && o > prevC) gapUpStreak++;
      else break;
    }
    // 反向统计从最后一天往前有多少天高开
    gapUpStreak = 0;
    for (let i = days4.length - 1; i >= 1; i--) {
      const o = opens4[i];
      const prevC = closes4raw[i - 1];
      if (o !== null && prevC !== null && o > prevC) gapUpStreak++;
      else break;
    }

    // 连续收阳：close[i] > open[i]
    let bullStreak = 0;
    for (let i = days4.length - 1; i >= 0; i--) {
      const o = opens4[i];
      const c = closes4raw[i];
      if (o !== null && c !== null && c > o) bullStreak++;
      else break;
    }

    // 连续上涨：close[i] > close[i-1]
    let upStreak = 0;
    for (let i = days4.length - 1; i >= 1; i--) {
      const c = closes4raw[i];
      const prevC = closes4raw[i - 1];
      if (c !== null && prevC !== null && c > prevC) upStreak++;
      else break;
    }

    const streaks = { gapUpStreak, bullStreak, upStreak };

    const rtr = rtMap.get(code);
    // debug: 若 rtMap 命中异常，尝试兼容前导0等问题
    if (!rtr || close_y === null) {
      items.push({ ts_code: ts, emotion_10: null, reason: 'missing realtime or close_y', debug: { code, has_rt: !!rtr, close_y, rtKeysSample } });
      continue;
    }

    const p = safeNum(rtr.p);
    const o = safeNum(rtr.o);
    const h = safeNum(rtr.h);
    const l = safeNum(rtr.l);
    if (p === null || o === null || h === null || l === null) {
      items.push({ ts_code: ts, emotion_10: null, reason: 'invalid realtime fields' });
      continue;
    }

    const open_gap = o / close_y - 1;
    const open_ret = open_gap;
    const open_to_high = o > 0 ? h / o - 1 : null;
    const open_to_low = o > 0 ? l / o - 1 : null;

    const ctx = {
      y_ret: y_ret_d,
      open_gap,
      open_ret,
      open_to_high,
      open_to_low,
      cum_3d,
      cum_5d,
      ret_3d: ret3,
      r3dd,
      streaks,
    };
    const res = scoreEmotion(ctx);

    items.push({
      ts_code: ts,
      realtime: { p, o, h, l },
      derived: { open_gap, open_to_high, open_to_low, y_ret: y_ret_d, cum_3d, cum_5d },
      ...res,
    });
  }

  const scored = items.filter((x) => x.emotion_10 !== null).sort((a, b) => b.emotion_10 - a.emotion_10 || a.ts_code.localeCompare(b.ts_code));

  const out = {
    date,
    dimension: 'emotion',
    max_score: 10,
    items: scored,
    skipped: items.filter((x) => x.emotion_10 === null),
  };

  const outPath = args.outPath || path.join(__dirname, '..', 'output', `emotion_scored_${date}.json`);
  fs.mkdirSync(path.dirname(outPath), { recursive: true });
  fs.writeFileSync(outPath, JSON.stringify(out, null, 2));
  process.stdout.write(JSON.stringify({ ok: true, outPath, top5: scored.slice(0, 5) }, null, 2));
}

main().catch((e) => {
  process.stderr.write(String(e.stack || e.message || e) + '\n');
  process.exit(1);
});
