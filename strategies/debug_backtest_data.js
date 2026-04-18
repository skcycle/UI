#!/usr/bin/env node
/**
 * 检查 4/8 和 4/9 的数据为什么缺失
 */

const https = require('https');

const TUSHARE_TOKEN = 'd7843466ea6bd6a8125e5d6990763087b6ab0df586f5f21a8b8795d2';

function httpPost(url, body) {
  return new Promise((resolve, reject) => {
    const data = JSON.stringify(body);
    const urlObj = new URL(url);
    const options = {
      hostname: urlObj.hostname,
      port: urlObj.port || 443,
      path: urlObj.pathname,
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Content-Length': data.length,
      },
    };
    const req = https.request(options, (res) => {
      let buf = '';
      res.on('data', (c) => (buf += c));
      res.on('end', () => {
        if (res.statusCode >= 400) return reject(new Error(`HTTP ${res.statusCode}: ${buf.slice(0, 200)}`));
        try {
          resolve(JSON.parse(buf));
        } catch (e) {
          reject(new Error(`JSON parse failed: ${e.message}`));
        }
      });
    });
    req.on('error', reject);
    req.write(data);
    req.end();
  });
}

async function fetchDaily(tsCode, startDate, endDate) {
  const resp = await httpPost('https://api.tushare.pro', {
    api_name: 'daily',
    token: TUSHARE_TOKEN,
    params: {
      ts_code: tsCode,
      start_date: startDate.replace(/-/g, ''),
      end_date: endDate.replace(/-/g, ''),
    },
    fields: 'ts_code,trade_date,close,pct_chg',
  });
  return resp;
}

async function main() {
  // 检查 4/8 的股票（川环科技 300547.SZ）
  console.log('=== 检查 4/8 的股票（川环科技 300547.SZ）===');
  console.log('请求范围: 2026-04-08 到 2026-04-13\n');
  
  const resp1 = await fetchDaily('300547.SZ', '2026-04-08', '2026-04-13');
  console.log('API 返回:');
  console.log(JSON.stringify(resp1, null, 2));
  
  // 检查 4/9 的股票（镇海股份 603637.SH）
  console.log('\n=== 检查 4/9 的股票（镇海股份 603637.SH）===');
  console.log('请求范围: 2026-04-09 到 2026-04-14\n');
  
  const resp2 = await fetchDaily('603637.SH', '2026-04-09', '2026-04-14');
  console.log('API 返回:');
  console.log(JSON.stringify(resp2, null, 2));
  
  // 检查当前日期的数据
  console.log('\n=== 检查最新交易日数据 ===');
  const resp3 = await fetchDaily('300547.SZ', '2026-04-14', '2026-04-16');
  console.log('川环科技 4/14~4/16:');
  console.log(JSON.stringify(resp3, null, 2));
  
  const resp4 = await fetchDaily('603637.SH', '2026-04-14', '2026-04-16');
  console.log('\n镇海股份 4/14~4/16:');
  console.log(JSON.stringify(resp4, null, 2));
}

main().catch((e) => {
  console.error(e.stack || e.message);
  process.exit(1);
});
