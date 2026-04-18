/**
 * 更新股票名称缓存
 * 
 * 从 TuShare stock_basic 接口获取全市场股票名称
 * 保存到本地缓存文件，供其他脚本使用
 * 
 * 建议每天或每周运行一次
 */

const fs = require('fs');
const path = require('path');
const https = require('https');

function parseArgs(argv) {
  const out = {};
  for (let i = 2; i < argv.length; i++) {
    const a = argv[i];
    if (a === '--token') out.token = argv[++i];
    else if (a === '--out') out.outPath = argv[++i];
  }
  return out;
}

function postTushare(payload, token) {
  const body = JSON.stringify({ ...payload, token });
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
  const token = args.token || process.env.TUSHARE_TOKEN;
  if (!token) throw new Error('missing TUSHARE_TOKEN');

  const outPath = args.outPath || path.join(__dirname, '..', 'output', 'stock_names_cache.json');

  console.log('正在从 TuShare 获取股票名称...');

  const payload = {
    api_name: 'stock_basic',
    params: { list_status: 'L' },
    fields: 'ts_code,name',
  };

  const basic = await postTushare(payload, token);
  if (!basic || basic.code !== 0) {
    throw new Error(basic ? basic.msg : 'stock_basic error');
  }

  const idx = Object.fromEntries(basic.data.fields.map((f, i) => [f, i]));
  const stockNames = {};
  for (const it of basic.data.items) {
    const ts = it[idx.ts_code];
    const nm = it[idx.name];
    if (ts && nm) {
      stockNames[ts] = nm;
    }
  }

  const cache = {
    updated_at: new Date().toISOString(),
    count: Object.keys(stockNames).length,
    stock_names: stockNames,
  };

  fs.mkdirSync(path.dirname(outPath), { recursive: true });
  fs.writeFileSync(outPath, JSON.stringify(cache, null, 2));

  console.log(`已缓存 ${cache.count} 只股票名称到: ${outPath}`);
  process.stdout.write(JSON.stringify({ ok: true, outPath, count: cache.count }, null, 2));
}

main().catch((e) => {
  process.stderr.write(String(e.stack || e.message || e) + '\n');
  process.exit(1);
});
