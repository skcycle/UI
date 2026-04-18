#!/usr/bin/env node
/**
 * 回测脚本：分析 4/7~4/9 Top10 各维度与后续收益的相关性
 * 
 * 步骤：
 * 1. 读取三天的 Top10 数据
 * 2. 从 Tushare 获取这些股票在之后 3~5 天的收益
 * 3. 分析各维度分数与收益的相关性
 * 4. 推荐新的权重
 */

const fs = require('fs');
const path = require('path');
const https = require('https');

const TUSHARE_TOKEN = process.env.TUSHARE_TOKEN;
if (!TUSHARE_TOKEN) throw new Error('missing TUSHARE_TOKEN');

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
  const [code, market] = tsCode.split('.');
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
  // 读取三天的 Top10 数据
  const dates = ['2026-04-07', '2026-04-08', '2026-04-09'];
  const dimensions = ['sector_heat', 'fundamental_momentum', 'volume_price_sync', 'kline_pattern', 'technical_indicators', 'main_moneyflow', 'emotion'];
  
  const allData = [];
  
  for (const date of dates) {
    const p = path.join(__dirname, '..', 'output', `to_test_${date}.json`);
    if (!fs.existsSync(p)) {
      console.error(`missing: ${p}`);
      continue;
    }
    const obj = JSON.parse(fs.readFileSync(p, 'utf8'));
    const items = obj.backtest_targets || [];
    
    for (const item of items) {
      // 计算 3~5 天后的收益
      // 使用更长的日期范围（+7 天）确保有足够的交易日数据
      const d = new Date(date + 'T00:00:00+08:00');
      d.setDate(d.getDate() + 7); // 延长到 7 天
      const endDate = d.toISOString().slice(0, 10);
      
      try {
        const rawDaily = await fetchDaily(item.ts_code, date, endDate);
        if (rawDaily.length < 2) {
          console.error(`${item.ts_code}: not enough data`);
          continue;
        }
        
        // 按日期升序排序（旧数据在前）
        const daily = [...rawDaily].sort((a, b) => a.trade_date.localeCompare(b.trade_date));
        
        // 计算收益（从评分日开始）
        const firstClose = daily[0].close; // 评分日的收盘价
        let ret3d = null, ret5d = null;
        
        if (daily.length >= 4) {
          ret3d = (daily[3].close / firstClose - 1) * 100;
        }
        if (daily.length >= 6) {
          ret5d = (daily[5].close / firstClose - 1) * 100;
        } else if (daily.length >= 4) {
          ret5d = (daily[daily.length - 1].close / firstClose - 1) * 100;
        }
        
        allData.push({
          date,
          ts_code: item.ts_code,
          mc: item.mc,
          total_score: item.total_score,
          scores: item.scores,
          ret3d,
          ret5d,
          daily: daily.slice(0, 6),
        });
        
        console.log(`${item.ts_code} ${item.mc}: ret3d=${ret3d?.toFixed(2)}%, ret5d=${ret5d?.toFixed(2)}%`);
      } catch (e) {
        console.error(`${item.ts_code}: ${e.message}`);
      }
    }
  }
  
  // 分析各维度与收益的相关性
  console.log('\n=== 相关性分析 ===');
  
  const correlations = {};
  for (const dim of dimensions) {
    const pairs = allData.filter((d) => d.scores[dim] !== undefined && d.ret5d !== null)
      .map((d) => [d.scores[dim], d.ret5d]);
    
    if (pairs.length < 5) {
      correlations[dim] = { corr: null, n: pairs.length };
      continue;
    }
    
    const n = pairs.length;
    const sumX = pairs.reduce((s, p) => s + p[0], 0);
    const sumY = pairs.reduce((s, p) => s + p[1], 0);
    const sumXY = pairs.reduce((s, p) => s + p[0] * p[1], 0);
    const sumX2 = pairs.reduce((s, p) => s + p[0] * p[0], 0);
    const sumY2 = pairs.reduce((s, p) => s + p[1] * p[1], 0);
    
    const corr = (n * sumXY - sumX * sumY) / Math.sqrt((n * sumX2 - sumX * sumX) * (n * sumY2 - sumY * sumY));
    correlations[dim] = { corr: isNaN(corr) ? null : corr, n };
  }
  
  console.log('\n维度相关性（与5日收益）:');
  for (const [dim, data] of Object.entries(correlations)) {
    console.log(`${dim}: corr=${data.corr?.toFixed(3) || 'N/A'}, n=${data.n}`);
  }
  
  // 推荐新权重
  console.log('\n=== 推荐新权重 ===');
  
  // 基于相关性绝对值分配权重
  const absCorrs = {};
  let sumAbsCorr = 0;
  for (const [dim, data] of Object.entries(correlations)) {
    absCorrs[dim] = Math.abs(data.corr || 0);
    sumAbsCorr += absCorrs[dim];
  }
  
  // 如果相关性都很低，使用等权
  if (sumAbsCorr < 0.1) {
    console.log('相关性都很低，建议使用等权');
    for (const dim of dimensions) {
      console.log(`${dim}: 1/7 = 0.143`);
    }
  } else {
    // 按相关性分配权重
    const newWeights = {};
    for (const dim of dimensions) {
      newWeights[dim] = absCorrs[dim] / sumAbsCorr;
      console.log(`${dim}: ${(newWeights[dim] * 100).toFixed(1)}%`);
    }
  }
  
  // 输出详细数据
  const outPath = path.join(__dirname, '..', 'output', 'backtest_2026-04-07_to_09.json');
  fs.mkdirSync(path.dirname(outPath), { recursive: true });
  fs.writeFileSync(outPath, JSON.stringify({
    data: allData,
    correlations,
    generated_at: new Date().toISOString(),
  }, null, 2));
  
  console.log(`\n详细数据已保存到: ${outPath}`);
}

main().catch((e) => {
  console.error(e.stack || e.message);
  process.exit(1);
});
