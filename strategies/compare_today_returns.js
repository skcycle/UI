#!/usr/bin/env node
/**
 * 计算昨天（2026-04-16）新老权重 Top10 股票在今天（2026-04-17）的收益
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

async function fetchDaily(tsCode, date) {
  const resp = await httpPost('https://api.tushare.pro', {
    api_name: 'daily',
    token: TUSHARE_TOKEN,
    params: {
      ts_code: tsCode,
      start_date: date.replace(/-/g, ''),
      end_date: date.replace(/-/g, ''),
    },
    fields: 'ts_code,trade_date,open,close,pct_chg',
  });
  if (!resp.data || !resp.data.items || resp.data.items.length === 0) {
    return null;
  }
  const row = resp.data.items[0];
  return {
    ts_code: row[0],
    trade_date: row[1],
    open: row[2],
    close: row[3],
    pct_chg: row[4],
  };
}

async function main() {
  // 昨天老权重 Top10
  const oldTop10 = [
    '600152.SH', '002074.SZ', '603698.SH', '002733.SZ', '688006.SH',
    '603200.SH', '002709.SZ', '301248.SZ', '300490.SZ', '301392.SZ'
  ];
  
  // 昨天新权重 Top10
  const newTop10 = [
    '301248.SZ', '301085.SZ', '300231.SZ', '603698.SH', '300277.SZ',
    '300846.SZ', '300166.SZ', '300438.SZ', '301053.SZ', '603200.SH'
  ];
  
  const today = '2026-04-17';
  
  console.log('=== 获取今天（2026-04-17）的收益数据 ===\n');
  
  // 获取老权重 Top10 的收益
  console.log('昨天老权重 Top10 今日表现:');
  const oldResults = [];
  for (const code of oldTop10) {
    try {
      const daily = await fetchDaily(code, today);
      if (daily) {
        console.log(`  ${code}: ${daily.pct_chg > 0 ? '+' : ''}${daily.pct_chg.toFixed(2)}%`);
        oldResults.push({ code, pct_chg: daily.pct_chg });
      } else {
        console.log(`  ${code}: 无数据`);
      }
    } catch (e) {
      console.log(`  ${code}: 错误 - ${e.message}`);
    }
  }
  
  console.log('\n昨天新权重 Top10 今日表现:');
  const newResults = [];
  for (const code of newTop10) {
    try {
      const daily = await fetchDaily(code, today);
      if (daily) {
        console.log(`  ${code}: ${daily.pct_chg > 0 ? '+' : ''}${daily.pct_chg.toFixed(2)}%`);
        newResults.push({ code, pct_chg: daily.pct_chg });
      } else {
        console.log(`  ${code}: 无数据`);
      }
    } catch (e) {
      console.log(`  ${code}: 错误 - ${e.message}`);
    }
  }
  
  // 计算平均收益
  const oldAvg = oldResults.length > 0 
    ? oldResults.reduce((sum, r) => sum + r.pct_chg, 0) / oldResults.length 
    : 0;
  const newAvg = newResults.length > 0 
    ? newResults.reduce((sum, r) => sum + r.pct_chg, 0) / newResults.length 
    : 0;
  
  console.log('\n=== 收益对比 ===');
  console.log(`老权重 Top10 平均收益: ${oldAvg > 0 ? '+' : ''}${oldAvg.toFixed(2)}%`);
  console.log(`新权重 Top10 平均收益: ${newAvg > 0 ? '+' : ''}${newAvg.toFixed(2)}%`);
  console.log(`差异: ${newAvg > oldAvg ? '+' : ''}${(newAvg - oldAvg).toFixed(2)}% (新权重 - 老权重)`);
  
  // 计算正收益比例
  const oldPositive = oldResults.filter(r => r.pct_chg > 0).length;
  const newPositive = newResults.filter(r => r.pct_chg > 0).length;
  
  console.log('\n=== 正收益比例 ===');
  console.log(`老权重 Top10 正收益: ${oldPositive}/${oldResults.length} (${(oldPositive/oldResults.length*100).toFixed(0)}%)`);
  console.log(`新权重 Top10 正收益: ${newPositive}/${newResults.length} (${(newPositive/newResults.length*100).toFixed(0)}%)`);
}

main().catch(e => {
  console.error(e.stack || e.message);
  process.exit(1);
});
