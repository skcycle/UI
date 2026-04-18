/**
 * 巴菲特-评分维度1: 板块热度(近7日) -> 0~20分
 *
 * 数据源: 麦瑞 ztgc
 *   GET https://api.mairuiapi.com/hslt/ztgc/<YYYY-MM-DD>/<licence>
 *   字段: dm, mc, zf, cje, hs, lbc, fbt, lbt, zj, zbc, hy
 *
 * 输入:
 *   - 候选股列表: input/candidate_stocks_YYYY-MM-DD.json
 *     {"candidate_stocks": ["002837.SZ", ...]}
 *
 * 输出:
 *   - output/sector_heat_scored_YYYY-MM-DD.json
 *     {
 *       "date": "2026-04-13",
 *       "window_days": 7,
 *       "dimension": "sector_heat",
 *       "max_score": 20,
 *       "items": [
 *         {"ts_code":"002837.SZ","hy":"电力","sector_heat_score":18.4,"sector_rank":1},
 *         ...
 *       ],
 *       "sector_table": {"电力": {"score":18.4, ...metrics...}, ...}
 *     }
 */

const https = require('https');
const fs = require('fs');
const path = require('path');

function parseArgs(argv) {
  const out = {};
  for (let i = 2; i < argv.length; i++) {
    const a = argv[i];
    if (a === '--date') out.date = argv[++i];
    else if (a === '--license' || a === '--licence') out.license = argv[++i];
    else if (a === '--in') out.inPath = argv[++i];
    else if (a === '--out') out.outPath = argv[++i];
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

function httpGetJson(url) {
  return new Promise((resolve, reject) => {
    https
      .get(url, (res) => {
        let data = '';
        res.on('data', (c) => (data += c));
        res.on('end', () => {
          if (res.statusCode && res.statusCode >= 400) {
            return reject(new Error(`HTTP ${res.statusCode}: ${data.slice(0, 200)}`));
          }
          if (!data || !data.trim()) return reject(new Error('empty body'));
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

function timeToMinutes(t) {
  // HH:mm:ss or HH:mm
  if (!t) return null;
  const s = String(t).trim();
  const m = s.match(/^(\d{2}):(\d{2})(?::(\d{2}))?$/);
  if (!m) return null;
  const hh = Number(m[1]);
  const mm = Number(m[2]);
  if (!Number.isFinite(hh) || !Number.isFinite(mm)) return null;
  return hh * 60 + mm;
}

function clamp(x, a, b) {
  return Math.max(a, Math.min(b, x));
}

function scoreBand(val, bands) {
  // bands: [{min, max, score}] where min inclusive, max exclusive (except Infinity)
  for (const b of bands) {
    const okMin = val >= b.min;
    const okMax = b.max === Infinity ? true : val < b.max;
    if (okMin && okMax) return b.score;
  }
  return 0;
}

function p90(arr) {
  if (!arr.length) return 0;
  const a = [...arr].sort((x, y) => x - y);
  const idx = Math.floor(0.9 * (a.length - 1));
  return a[idx];
}

async function fetchZtgcForDate(date, license) {
  const url = `https://api.mairuiapi.com/hslt/ztgc/${encodeURIComponent(date)}/${encodeURIComponent(license)}`;
  const rows = await httpGetJson(url);
  if (!Array.isArray(rows)) throw new Error('ztgc response not array');
  return rows;
}

async function main() {
  const args = parseArgs(process.argv);
  const date = normalizeDate(args.date);
  const license = args.license || process.env.MAIRUI_LICENSE;
  if (!license) throw new Error('missing license');

  const windowDays = Number.isFinite(args.window) && args.window > 0 ? args.window : 7;

  const inPath =
    args.inPath ||
    path.join(__dirname, '..', 'input', `candidate_stocks_${date}.json`);
  if (!fs.existsSync(inPath)) throw new Error(`missing input: ${inPath}`);
  const candidates = JSON.parse(fs.readFileSync(inPath, 'utf8'));
  const tsCodes = Array.isArray(candidates.candidate_stocks) ? candidates.candidate_stocks : [];

  // 拉近 windowDays 天的 ztgc
  const base = new Date(date + 'T00:00:00+08:00');
  const dates = [];
  for (let i = 0; i < windowDays; i++) {
    const d = new Date(base.getTime() - i * 24 * 3600 * 1000);
    const ds = d.toISOString().slice(0, 10);
    dates.push(ds);
  }

  // 并发拉取（上限7），交易时间不使用缓存
  const all = [];
  const CONCURRENCY = 7;
  let idxFetch = 0;
  const runners = Array.from({ length: Math.min(CONCURRENCY, dates.length) }, async () => {
    while (idxFetch < dates.length) {
      const i = idxFetch++;
      const ds = dates[i];
      try {
        const rows = await fetchZtgcForDate(ds, license);
        for (const r of rows) all.push({ ...r, _date: ds });
      } catch {
        // 忽略单日失败
      }
    }
  });
  await Promise.all(runners);

  // 按 hy 与 dm 聚合（用于板块公共分 + 个股分化分）
  const byHy = new Map();
  const byHyDm = new Map(); // key = `${hy}|||${dm}`
  for (const r of all) {
    const hy = (r && r.hy ? String(r.hy).trim() : '') || '未知';
    const dm = (r && r.dm ? String(r.dm).trim() : '') || '';
    if (!byHy.has(hy)) byHy.set(hy, []);
    byHy.get(hy).push(r);
    if (dm) {
      const k = `${hy}|||${dm}`;
      if (!byHyDm.has(k)) byHyDm.set(k, []);
      byHyDm.get(k).push(r);
    }
  }

  const sectorTable = {};

  // 先算全市场近 windowDays 的最高连板（用于相对高度）
  const marketMaxLbc = Math.max(
    0,
    ...all
      .map((x) => Number(x.lbc))
      .filter((x) => Number.isFinite(x)),
  );

  // 计算板块公共分(0~12) + 热度变化率(近2日 score_delta12)
  // 思路：先算每个交易日的板块 base12，然后对比 date vs prev day。
  // 注意：all 里 _date 是 ISO 日期字符串。
  const daySet = new Set(all.map((x) => x._date).filter(Boolean));
  const daySorted = Array.from(daySet).sort();
  const prevDate = daySorted.length ? daySorted[daySorted.length - 2] : null;

  // helper: compute base12 for one sector from its rows (same as below logic)
  function computeSectorBase12(hy, rows) {
    const limitUpCount = rows.length;
    const activeDays = new Set(rows.map((x) => x._date)).size;
    const lbcList = rows.map((x) => Number(x.lbc)).filter((x) => Number.isFinite(x));
    const maxLbc = lbcList.length ? Math.max(...lbcList) : 0;
    const heightRatio = marketMaxLbc > 0 ? maxLbc / marketMaxLbc : 0;
    const countLbcGe2 = lbcList.filter((x) => x >= 2).length;

    const zjList = rows.map((x) => Number(x.zj)).filter((x) => Number.isFinite(x) && x > 0);
    const zjP90 = p90(zjList);

    const zbcList = rows.map((x) => Number(x.zbc)).filter((x) => Number.isFinite(x));
    const avgZbc = zbcList.length ? zbcList.reduce((a, b) => a + b, 0) / zbcList.length : 0;

    let earlyCnt = 0;
    let earlyDen = 0;
    let durSum = 0;
    let durDen = 0;
    for (const x of rows) {
      const lbt = timeToMinutes(x.lbt);
      const fbt = timeToMinutes(x.fbt);
      if (lbt !== null) {
        earlyDen++;
        if (lbt <= 600) earlyCnt++;
      }
      if (lbt !== null && fbt !== null) {
        durDen++;
        durSum += Math.max(0, lbt - fbt);
      }
    }
    const earlySealRatio = earlyDen ? earlyCnt / earlyDen : 0;
    const avgSealDuration = durDen ? durSum / durDen : 999;

    const cjeList = rows.map((x) => Number(x.cje)).filter((x) => Number.isFinite(x) && x > 0);
    const sumCje = cjeList.reduce((a, b) => a + b, 0);

    const A = scoreBand(limitUpCount, [
      { min: 20, max: Infinity, score: 100 },
      { min: 15, max: 20, score: 85 },
      { min: 10, max: 15, score: 65 },
      { min: 6, max: 10, score: 45 },
      { min: 3, max: 6, score: 25 },
      { min: 1, max: 3, score: 10 },
      { min: 0, max: 1, score: 0 },
    ]);
    const B = scoreBand(activeDays, [
      { min: 7, max: Infinity, score: 100 },
      { min: 6, max: 7, score: 90 },
      { min: 5, max: 6, score: 70 },
      { min: 4, max: 5, score: 50 },
      { min: 3, max: 4, score: 30 },
      { min: 2, max: 3, score: 20 },
      { min: 1, max: 2, score: 10 },
      { min: 0, max: 1, score: 0 },
    ]);
    const C1 = scoreBand(maxLbc, [
      { min: 6, max: Infinity, score: 100 },
      { min: 5, max: 6, score: 85 },
      { min: 4, max: 5, score: 70 },
      { min: 3, max: 4, score: 50 },
      { min: 2, max: 3, score: 30 },
      { min: 1, max: 2, score: 10 },
      { min: 0, max: 1, score: 0 },
    ]);
    const C2 = scoreBand(countLbcGe2, [
      { min: 8, max: Infinity, score: 100 },
      { min: 5, max: 8, score: 80 },
      { min: 3, max: 5, score: 60 },
      { min: 2, max: 3, score: 40 },
      { min: 1, max: 2, score: 20 },
      { min: 0, max: 1, score: 0 },
    ]);
    let C = Math.max(C1, C2);
    C = C * (0.7 + 0.3 * clamp(heightRatio, 0, 1));

    const D = scoreBand(zjP90, [
      { min: 5e8, max: Infinity, score: 100 },
      { min: 2e8, max: 5e8, score: 80 },
      { min: 1e8, max: 2e8, score: 60 },
      { min: 5e7, max: 1e8, score: 40 },
      { min: 2e7, max: 5e7, score: 20 },
      { min: 1, max: 2e7, score: 10 },
      { min: 0, max: 1, score: 0 },
    ]);

    const E = scoreBand(avgZbc, [
      { min: 0, max: 0.2, score: 100 },
      { min: 0.2, max: 0.5, score: 80 },
      { min: 0.5, max: 1.0, score: 60 },
      { min: 1.0, max: 2.0, score: 30 },
      { min: 2.0, max: Infinity, score: 0 },
    ]);

    const F1 = scoreBand(earlySealRatio, [
      { min: 0.6, max: Infinity, score: 100 },
      { min: 0.45, max: 0.6, score: 80 },
      { min: 0.30, max: 0.45, score: 60 },
      { min: 0.15, max: 0.30, score: 30 },
      { min: 0, max: 0.15, score: 0 },
    ]);
    const F2 = scoreBand(avgSealDuration, [
      { min: 0, max: 5, score: 100 },
      { min: 5, max: 15, score: 80 },
      { min: 15, max: 30, score: 60 },
      { min: 30, max: 60, score: 20 },
      { min: 60, max: Infinity, score: 0 },
    ]);
    const F = 0.7 * F1 + 0.3 * F2;

    const G = scoreBand(sumCje, [
      { min: 1e11, max: Infinity, score: 100 },
      { min: 5e10, max: 1e11, score: 80 },
      { min: 2e10, max: 5e10, score: 60 },
      { min: 1e10, max: 2e10, score: 40 },
      { min: 5e9, max: 1e10, score: 20 },
      { min: 0, max: 5e9, score: 0 },
    ]);

    const total100 = 0.20 * A + 0.10 * B + 0.20 * C + 0.15 * D + 0.10 * E + 0.15 * F + 0.10 * G;
    const sectorBase12 = Math.round(clamp(total100, 0, 100) * 0.12 * 10) / 10;

    return { sectorBase12, total100, metrics: { limitUpCount, activeDays, maxLbc, countLbcGe2, zjP90, avgZbc, earlySealRatio, avgSealDuration, sumCje }, subscores_100: { A, B, C, D, E, F, G } };
  }

  // compute base12 per day, then delta
  const sectorBaseByDay = new Map(); // day -> { hy -> base12 }
  for (const day of [prevDate, date]) {
    if (!day) continue;
    const map = {};
    for (const [hy, rows] of byHy.entries()) {
      const dayRows = rows.filter((r) => r && r._date === day);
      if (!dayRows.length) continue;
      const { sectorBase12 } = computeSectorBase12(hy, dayRows);
      map[hy] = sectorBase12;
    }
    sectorBaseByDay.set(day, map);
  }

  // 计算板块公共分(0~12)
  for (const [hy, rows] of byHy.entries()) {
    const limitUpCount = rows.length;
    const activeDays = new Set(rows.map((x) => x._date)).size;
    const lbcList = rows.map((x) => Number(x.lbc)).filter((x) => Number.isFinite(x));
    const maxLbc = lbcList.length ? Math.max(...lbcList) : 0;
    const heightRatio = marketMaxLbc > 0 ? maxLbc / marketMaxLbc : 0;
    const countLbcGe2 = lbcList.filter((x) => x >= 2).length;

    const zjList = rows.map((x) => Number(x.zj)).filter((x) => Number.isFinite(x) && x > 0);
    const zjP90 = p90(zjList);

    const zbcList = rows.map((x) => Number(x.zbc)).filter((x) => Number.isFinite(x));
    const avgZbc = zbcList.length ? zbcList.reduce((a, b) => a + b, 0) / zbcList.length : 0;

    let earlyCnt = 0;
    let earlyDen = 0;
    let durSum = 0;
    let durDen = 0;
    for (const x of rows) {
      const lbt = timeToMinutes(x.lbt);
      const fbt = timeToMinutes(x.fbt);
      if (lbt !== null) {
        earlyDen++;
        if (lbt <= 600) earlyCnt++;
      }
      if (lbt !== null && fbt !== null) {
        durDen++;
        durSum += Math.max(0, lbt - fbt);
      }
    }
    const earlySealRatio = earlyDen ? earlyCnt / earlyDen : 0;
    const avgSealDuration = durDen ? durSum / durDen : 999;

    const cjeList = rows.map((x) => Number(x.cje)).filter((x) => Number.isFinite(x) && x > 0);
    const sumCje = cjeList.reduce((a, b) => a + b, 0);

    // 分档打分(0-100)
    const A = scoreBand(limitUpCount, [
      { min: 20, max: Infinity, score: 100 },
      { min: 15, max: 20, score: 85 },
      { min: 10, max: 15, score: 65 },
      { min: 6, max: 10, score: 45 },
      { min: 3, max: 6, score: 25 },
      { min: 1, max: 3, score: 10 },
      { min: 0, max: 1, score: 0 },
    ]);
    const B = scoreBand(activeDays, [
      { min: 7, max: Infinity, score: 100 },
      { min: 6, max: 7, score: 90 },
      { min: 5, max: 6, score: 70 },
      { min: 4, max: 5, score: 50 },
      { min: 3, max: 4, score: 30 },
      { min: 2, max: 3, score: 20 },
      { min: 1, max: 2, score: 10 },
      { min: 0, max: 1, score: 0 },
    ]);
    const C1 = scoreBand(maxLbc, [
      { min: 6, max: Infinity, score: 100 },
      { min: 5, max: 6, score: 85 },
      { min: 4, max: 5, score: 70 },
      { min: 3, max: 4, score: 50 },
      { min: 2, max: 3, score: 30 },
      { min: 1, max: 2, score: 10 },
      { min: 0, max: 1, score: 0 },
    ]);
    const C2 = scoreBand(countLbcGe2, [
      { min: 8, max: Infinity, score: 100 },
      { min: 5, max: 8, score: 80 },
      { min: 3, max: 5, score: 60 },
      { min: 2, max: 3, score: 40 },
      { min: 1, max: 2, score: 20 },
      { min: 0, max: 1, score: 0 },
    ]);
    let C = Math.max(C1, C2);
    // 相对高度加权：板块最高板越接近市场最高板，连板强度越有含金量
    C = C * (0.7 + 0.3 * clamp(heightRatio, 0, 1));

    const D = scoreBand(zjP90, [
      { min: 5e8, max: Infinity, score: 100 },
      { min: 2e8, max: 5e8, score: 80 },
      { min: 1e8, max: 2e8, score: 60 },
      { min: 5e7, max: 1e8, score: 40 },
      { min: 2e7, max: 5e7, score: 20 },
      { min: 1, max: 2e7, score: 10 },
      { min: 0, max: 1, score: 0 },
    ]);

    const E = scoreBand(avgZbc, [
      { min: 0, max: 0.2, score: 100 },
      { min: 0.2, max: 0.5, score: 80 },
      { min: 0.5, max: 1.0, score: 60 },
      { min: 1.0, max: 2.0, score: 30 },
      { min: 2.0, max: Infinity, score: 0 },
    ]);

    const F1 = scoreBand(earlySealRatio, [
      { min: 0.6, max: Infinity, score: 100 },
      { min: 0.45, max: 0.6, score: 80 },
      { min: 0.30, max: 0.45, score: 60 },
      { min: 0.15, max: 0.30, score: 30 },
      { min: 0, max: 0.15, score: 0 },
    ]);
    const F2 = scoreBand(avgSealDuration, [
      { min: 0, max: 5, score: 100 },
      { min: 5, max: 15, score: 80 },
      { min: 15, max: 30, score: 60 },
      { min: 30, max: 60, score: 20 },
      { min: 60, max: Infinity, score: 0 },
    ]);
    const F = 0.7 * F1 + 0.3 * F2;

    const G = scoreBand(sumCje, [
      { min: 1e11, max: Infinity, score: 100 },
      { min: 5e10, max: 1e11, score: 80 },
      { min: 2e10, max: 5e10, score: 60 },
      { min: 1e10, max: 2e10, score: 40 },
      { min: 5e9, max: 1e10, score: 20 },
      { min: 0, max: 5e9, score: 0 },
    ]);

    // 板块公共分: 0-100 合成为 0-12
    const total100 = 0.20 * A + 0.10 * B + 0.20 * C + 0.15 * D + 0.10 * E + 0.15 * F + 0.10 * G;
    const sectorBase12 = Math.round(clamp(total100, 0, 100) * 0.12 * 10) / 10; // 0~12, 1位小数

    // "热度变化率": 用 date vs prevDate 的 base12 变化来做动量修正
    const basePrev = prevDate && sectorBaseByDay.has(prevDate) ? (sectorBaseByDay.get(prevDate)[hy] ?? null) : null;
    const baseNow = sectorBaseByDay.has(date) ? (sectorBaseByDay.get(date)[hy] ?? sectorBase12) : sectorBase12;
    const delta12 = basePrev === null || basePrev === undefined ? 0 : (baseNow - basePrev);
    // map delta to momentum bonus/penalty in [-3, +3]
    const mom = clamp(delta12, -1.5, 1.5) * 2; // 1.5 base12 change -> 3 pts
    const sectorBase12_mom = Math.round(clamp(sectorBase12 + mom, 0, 12) * 10) / 10;

    sectorTable[hy] = {
      score: sectorBase12_mom, // 使用动量修正后的板块分(0~12)
      score_base: sectorBase12,
      score_delta12: Math.round(delta12 * 10) / 10,
      score_momentum: Math.round(mom * 10) / 10,
      total100,
      metrics: {
        limit_up_count_7d: limitUpCount,
        active_days_7d: activeDays,
        max_lbc_7d: maxLbc,
        market_max_lbc_7d: marketMaxLbc,
        height_ratio: heightRatio,
        count_lbc_ge_2_7d: countLbcGe2,
        zj_p90_7d: zjP90,
        avg_zbc_7d: avgZbc,
        early_seal_ratio_7d: earlySealRatio,
        avg_seal_duration_min_7d: avgSealDuration,
        sum_cje_7d: sumCje,
      },
      subscores_100: { A, B, C, D, E, F, G },
    };
  }

  // 个股分化: 在同板块内基于近7天涨停表现，打 0~8
  // 对每个 hy，计算每只 dm 的: lu_count, max_lbc, quality(log(1+zj_sum)-0.5*zbc_sum), eff(early_ratio - norm(duration))
  const stockAdj = new Map(); // key dm -> adj0to8
  for (const hy of byHy.keys()) {
    // collect stocks in this hy
    const dms = [];
    for (const k of byHyDm.keys()) {
      if (k.startsWith(hy + '|||')) dms.push(k);
    }
    if (!dms.length) continue;

    const per = dms.map((k) => {
      const rows = byHyDm.get(k) || [];
      const dm = k.split('|||')[1];
      const luCount = rows.length;
      const lbcList = rows.map((x) => Number(x.lbc)).filter((x) => Number.isFinite(x));
      const maxLbc = lbcList.length ? Math.max(...lbcList) : 0;
      const zjSum = rows.map((x) => Number(x.zj)).filter((x) => Number.isFinite(x) && x > 0).reduce((a, b) => a + b, 0);
      const zbcSum = rows.map((x) => Number(x.zbc)).filter((x) => Number.isFinite(x) && x > 0).reduce((a, b) => a + b, 0);

      let earlyCnt = 0;
      let earlyDen = 0;
      let durSum = 0;
      let durDen = 0;
      for (const x of rows) {
        const lbt = timeToMinutes(x.lbt);
        const fbt = timeToMinutes(x.fbt);
        if (lbt !== null) {
          earlyDen++;
          if (lbt <= 600) earlyCnt++;
        }
        if (lbt !== null && fbt !== null) {
          durDen++;
          durSum += Math.max(0, lbt - fbt);
        }
      }
      const earlyRatio = earlyDen ? earlyCnt / earlyDen : 0;
      const avgDur = durDen ? durSum / durDen : 999;

      const quality = Math.log1p(zjSum) - 0.5 * zbcSum;
      const eff = earlyRatio - Math.min(1, avgDur / 60);

      return { dm, luCount, maxLbc, quality, eff };
    });

    // percentile helper
    function percentileScore(value, arr) {
      const sorted = [...arr].sort((a, b) => a - b);
      const n = sorted.length;
      if (n === 0) return 0.2;
      let idx = 0;
      while (idx < n && sorted[idx] <= value) idx++;
      const p = idx / n; // 0..1
      if (p >= 0.75) return 2.0;
      if (p >= 0.50) return 1.5;
      if (p >= 0.25) return 0.8;
      return 0.2;
    }

    const qualArr = per.map((x) => x.quality);
    const effArr = per.map((x) => x.eff);

    for (const x of per) {
      // 1) 7天涨停次数 0~2
      const s1 = x.luCount >= 3 ? 2.0 : x.luCount === 2 ? 1.4 : x.luCount === 1 ? 0.8 : 0;
      // 2) 7天最高连板 0~2
      const s2 = x.maxLbc >= 4 ? 2.0 : x.maxLbc === 3 ? 1.5 : x.maxLbc === 2 ? 1.0 : x.maxLbc === 1 ? 0.5 : 0;
      // 3) 封板质量 分位 0.2~2
      const s3 = percentileScore(x.quality, qualArr);
      // 4) 封板效率 分位 0.2~2
      const s4 = percentileScore(x.eff, effArr);

      const adj = clamp(s1 + s2 + s3 + s4, 0, 8);
      stockAdj.set(x.dm, Math.round(adj * 10) / 10);
    }
  }

  // 给 sector rank（按板块公共分 0-12 排序）
  const ranked = Object.entries(sectorTable)
    .map(([hy, v]) => ({ hy, score: v.score }))
    .sort((a, b) => b.score - a.score || a.hy.localeCompare(b.hy));
  const rankMap = new Map(ranked.map((x, i) => [x.hy, i + 1]));

  // 候选股需要 hy: 优先从“Top5板块成分股池”反查所属板块(更符合选股来源)
  // 读取比尔输出的 components 文件: /root/.openclaw/workspace-select/output/ztgc_top5_components_<date>.json
  const compPath = `/root/.openclaw/workspace-select/output/ztgc_top5_components_${date}.json`;
  const dm2hy = new Map();
  if (fs.existsSync(compPath)) {
    const comp = JSON.parse(fs.readFileSync(compPath, 'utf8'));
    const topSectors = Array.isArray(comp.top_sectors) ? comp.top_sectors : [];
    // sw2_code -> hy
    const sw2ToHy = new Map(topSectors.filter(s=>s && s.sw2_code && s.hy).map(s=>[String(s.sw2_code), String(s.hy)]));
    const components = comp.components || {};
    for (const [sw2, obj] of Object.entries(components)) {
      const hy = sw2ToHy.get(String(sw2)) || null;
      const items = obj && Array.isArray(obj.items) ? obj.items : [];
      for (const it of items) {
        const dm = it && it.dm ? String(it.dm).trim() : '';
        if (!dm) continue;
        if (hy) dm2hy.set(dm, hy);
      }
    }
  }

  // 兜底: 若仍缺失，尝试从当日 ztgc 的 dm->hy 映射（仅对当日涨停股有效）
  if (dm2hy.size === 0) {
    const todayRows = await fetchZtgcForDate(date, license);
    for (const r of todayRows) {
      if (r && r.dm && r.hy) dm2hy.set(String(r.dm).trim(), String(r.hy).trim());
    }
  }

  const items = tsCodes.map((ts) => {
    const [code] = String(ts).split('.');
    const hy = dm2hy.get(code) || '未知';
    const sec = sectorTable[hy];
    const base12 = sec ? sec.score : 0;
    const adj8 = stockAdj.get(code) || 0;
    const total20 = Math.round(clamp(base12 + adj8, 0, 20) * 10) / 10;
    return {
      ts_code: ts,
      hy,
      sector_heat_score: total20,
      sector_heat_base: base12,
      sector_heat_adj: adj8,
      sector_rank: rankMap.get(hy) || null,
    };
  });

  const out = {
    date,
    window_days: windowDays,
    dimension: 'sector_heat',
    max_score: 20,
    items,
    sector_table: sectorTable,
  };

  const outPath =
    args.outPath ||
    path.join(__dirname, '..', 'output', `sector_heat_scored_${date}.json`);
  fs.mkdirSync(path.dirname(outPath), { recursive: true });
  fs.writeFileSync(outPath, JSON.stringify(out, null, 2));

  process.stdout.write(
    JSON.stringify(
      {
        ok: true,
        outPath,
        candidates: items.length,
        sectors_scored: Object.keys(sectorTable).length,
        score_range: {
          min: items.length ? Math.min(...items.map((x) => x.sector_heat_score)) : null,
          max: items.length ? Math.max(...items.map((x) => x.sector_heat_score)) : null,
        },
      },
      null,
      2,
    ),
  );
}

main().catch((e) => {
  process.stderr.write(String(e.stack || e.message || e) + '\n');
  process.exit(1);
});
