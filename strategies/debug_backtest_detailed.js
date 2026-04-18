#!/usr/bin/env node
/**
 * 详细调试 4/8 和 4/9 的数据
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
  if (!resp.data || !resp.data.items) return [];
  return resp.data.items.map((row) => ({
    ts_code: row[0],
    trade_date: row[1],
    close: row[2],
    pct_chg: row[3],
  }));
}

async function main() {
  // 模拟回测脚本的逻辑
  const testCases = [
    { date: '2026-04-07', ts_code: '603585.SH', mc: '苏利股份' },
    { date: '2026-04-08', ts_code: '300547.SZ', mc: '川环科技' },
    { date: '2026-04-09', ts_code: '603637.SH', mc: '镇海股份' },
  ];
  
  for (const { date, ts_code, mc } of testCases) {
    console.log(`\n=== ${date} ${ts_code} ${mc} ===`);
    
    // 计算日期范围
    const d = new Date(date + 'T00:00:00+08:00');
    d.setDate(d.getDate() + 5);
    const endDate = d.toISOString().slice(0, 10);
    
    console.log(`评分日期: ${date}`);
    console.log(`结束日期: ${endDate}`);
    
    // 获取数据
    const rawDaily = await fetchDaily(ts_code, date, endDate);
    console.log(`返回数据条数: ${rawDaily.length}`);
    
    if (rawDaily.length < 2) {
      console.log('数据不足，跳过');
      continue;
    }
    
    // 排序
    const daily = [...rawDaily].sort((a, b) => a.trade_date.localeCompare(b.trade_date));
    
    console.log('\n排序后的数据:');
    daily.forEach((d, i) => {
      console.log(`  [${i}] ${d.trade_date} close=${d.close}`);
    });
    
    // 计算收益
    const firstClose = daily[0].close;
    console.log(`\n评分日收盘价: ${firstClose}`);
    
    let ret3d = null, ret5d = null;
    
    if (daily.length >= 4) {
      ret3d = (daily[3].close / firstClose - 1) * 100;
      console.log(`daily[3].close = ${daily[3].close}`);
      console.log(`ret3d = (${daily[3].close} / ${firstClose} - 1) * 100 = ${ret3d?.toFixed(2)}%`);
    }
    
    if (daily.length >= 6) {
      ret5d = (daily[5].close / firstClose - 1) * 100;
    } else if (daily.length >= 4) {
      ret5d = (daily[daily.length - 1].close / firstClose - 1) * 100;
      console.log(`daily[last].close = ${daily[daily.length - 1].close}`);
      console.log(`ret5d = (${daily[daily.length - 1].close} / ${firstClose} - 1) * 100 = ${ret5d?.toFixed(2)}%`);
    }
    
    console.log(`\n最终结果: ret3d=${ret3d?.toFixed(2)}%, ret5d=${ret5d?.toFixed(2)}%`);
  }
}

main().catch((e) => {
  console.error(e.stack || e.message);
  process.exit(1);
});
