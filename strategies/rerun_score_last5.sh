#!/usr/bin/env bash
# 重新生成最近5个交易日的评分数据
set -euo pipefail

export TUSHARE_TOKEN='d7843466ea6bd6a8125e5d6990763087b6ab0df586f5f21a8b8795d2'
export MAIRUI_LICENSE='EE4E0843-AAA7-4B04-B367-4A7345CAE9A7'

DATES=(
  "2026-04-14"
  "2026-04-13"
  "2026-04-10"
  "2026-04-09"
  "2026-04-08"
)

echo "重新生成最近5个交易日的评分数据..."

for DATE in "${DATES[@]}"; do
  echo ""
  echo "=========================================="
  echo "处理日期: $DATE"
  echo "=========================================="
  
  cd /root/.openclaw/workspace-score
  
  echo "Step 1/9: 预取数据..."
  node strategies/prefetch_daily_bundle.js --date "$DATE"
  
  echo "Step 2/9: 基本面动量评分..."
  node strategies/fundamental_momentum_score.js --date "$DATE"
  
  echo "Step 3/9: 量价协同评分..."
  node strategies/volume_price_sync_score.js --date "$DATE"
  
  echo "Step 4/9: K线形态评分..."
  node strategies/kline_pattern_score.js --date "$DATE"
  
  echo "Step 5/9: 技术指标评分..."
  node strategies/technical_indicators_score.js --date "$DATE"
  
  echo "Step 6/9: 主力资金流评分..."
  node strategies/main_moneyflow_score.js --date "$DATE"
  
  echo "Step 7/9: 板块热度评分..."
  node strategies/sector_heat_score.js --date "$DATE"
  
  echo "Step 8/9: 情绪评分..."
  node strategies/emotion_score.js --date "$DATE"
  
  echo "Step 9/9: 导出Top10..."
  node strategies/export_for_test.js --date "$DATE" --top 10
  
  echo "[完成] $DATE 处理成功"
done

echo ""
echo "=========================================="
echo "全部完成！"
echo "=========================================="
