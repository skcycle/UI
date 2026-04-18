#!/usr/bin/env bash
set -euo pipefail
export TUSHARE_TOKEN='d7843466ea6bd6a8125e5d6990763087b6ab0df586f5f21a8b8795d2'
DATE='2026-04-14'
cd /root/.openclaw/workspace-score

echo "========== Step 6 主力资金流详细耗时测试 =========="
echo "开始时间: $(date '+%H:%M:%S')"
TOTAL_START=$(date +%s)

# 读取候选股列表
CAND_PATH="/root/.openclaw/workspace-score/input/candidate_stocks_${DATE}.json"
if [ ! -f "$CAND_PATH" ]; then
  echo "错误: 候选股文件不存在: $CAND_PATH"
  exit 1
fi

CAND_COUNT=$(jq '.candidate_stocks | length' "$CAND_PATH")
echo "候选股数量: ${CAND_COUNT}"

# 使用 Node.js 进行详细耗时测试
node -e "
const fs = require('fs');
const https = require('https');

const token = process.env.TUSHARE_TOKEN;
const date = '${DATE}';
const yyyymmdd = (s) => s.replace(/-/g, '');

const cand = JSON.parse(fs.readFileSync('${CAND_PATH}', 'utf8'));
const tsCodes = cand.candidate_stocks || [];
console.log('候选股:', tsCodes.length, '只');

const end = yyyymmdd(date);
const endDate = new Date(date + 'T00:00:00+08:00');
const windowN = 10;
const start = new Date(endDate.getTime() - windowN * 4 * 24 * 3600 * 1000);
const startYmd = yyyymmdd(start.toISOString().slice(0, 10));

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
            reject(new Error('non-json'));
          }
        });
      }
    );
    req.on('error', reject);
    req.write(body);
    req.end();
  });
}

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

(async () => {
  // Step 1: moneyflow 接口
  console.log('\\n[1] moneyflow 接口请求...');
  const t1_start = Date.now();
  const mfPayload = {
    api_name: 'moneyflow',
    token,
    params: { ts_code: tsCodes.join(','), start_date: startYmd, end_date: end },
    fields: 'ts_code,trade_date,net_mf_amount,buy_elg_amount,sell_elg_amount,buy_lg_amount,sell_lg_amount',
  };
  const mfJson = await postTushare(mfPayload);
  const t1_end = Date.now();
  console.log('moneyflow 耗时:', ((t1_end - t1_start) / 1000).toFixed(2), '秒');
  console.log('moneyflow 返回数据:', mfJson && mfJson.data ? mfJson.data.items.length : 0, '条');

  // Step 2: moneyflow_dc 接口（分批并发）
  console.log('\\n[2] moneyflow_dc 接口请求...');
  const CHUNK = 2500;
  const dcChunks = [];
  for (let i = 0; i < tsCodes.length; i += CHUNK) {
    dcChunks.push(tsCodes.slice(i, i + CHUNK));
  }
  console.log('分批数量:', dcChunks.length, '批');
  console.log('并发上限: 10');
  
  const t2_start = Date.now();
  const dcResults = await runPool(10, dcChunks, async (chunk, idx) => {
    const payload = {
      api_name: 'moneyflow_dc',
      token,
      params: { ts_code: chunk.join(','), start_date: startYmd, end_date: end },
      fields: 'ts_code,trade_date,net_amount',
    };
    try {
      return await postTushare(payload);
    } catch (e) {
      return { code: -1, msg: String(e.message || e) };
    }
  });
  const t2_end = Date.now();
  console.log('moneyflow_dc 耗时:', ((t2_end - t2_start) / 1000).toFixed(2), '秒');
  
  let dcTotal = 0;
  for (const r of dcResults) {
    if (r && r.code === 0 && r.data) dcTotal += r.data.items.length;
  }
  console.log('moneyflow_dc 返回数据:', dcTotal, '条');

  // Step 3: 数据处理
  console.log('\\n[3] 数据处理...');
  const t3_start = Date.now();
  
  // 模拟数据处理
  function group(json) {
    const data = json.data;
    const idx = Object.fromEntries(data.fields.map((f, i) => [f, i]));
    const map = new Map();
    for (const it of data.items) {
      const ts = it[idx.ts_code];
      const td = it[idx.trade_date];
      if (!ts || !td) continue;
      if (!map.has(ts)) map.set(ts, []);
      map.get(ts).push({ trade_date: td });
    }
    return map;
  }
  
  const mfMap = group(mfJson);
  
  const t3_end = Date.now();
  console.log('数据处理耗时:', ((t3_end - t3_start) / 1000).toFixed(2), '秒');

  // 汇总
  const total = (t1_end - t1_start) + (t2_end - t2_start) + (t3_end - t3_start);
  console.log('\\n========== 汇总 ==========');
  console.log('moneyflow 请求:', ((t1_end - t1_start) / 1000).toFixed(2), '秒');
  console.log('moneyflow_dc 请求:', ((t2_end - t2_start) / 1000).toFixed(2), '秒');
  console.log('数据处理:', ((t3_end - t3_start) / 1000).toFixed(2), '秒');
  console.log('总计:', (total / 1000).toFixed(2), '秒');
})();
"

TOTAL_END=$(date +%s)
echo -e "\n结束时间: $(date '+%H:%M:%S')"
