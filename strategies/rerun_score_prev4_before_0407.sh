#!/usr/bin/env bash
set -euo pipefail
export TUSHARE_TOKEN='d7843466ea6bd6a8125e5d6990763087b6ab0df586f5f21a8b8795d2'
export MAIRUI_LICENSE='EE4E0843-AAA7-4B04-B367-4A7345CAE9A7'
cd /root/.openclaw/workspace-score

DATES=("2026-04-03" "2026-04-02" "2026-04-01" "2026-03-31")

for DATE in "${DATES[@]}"; do
  echo "=== Rerun score: $DATE ==="
  node strategies/prefetch_daily_bundle.js --date "$DATE"
  node strategies/fundamental_momentum_score.js --date "$DATE"
  node strategies/volume_price_sync_score.js --date "$DATE"
  node strategies/kline_pattern_score.js --date "$DATE"
  node strategies/technical_indicators_score.js --date "$DATE"
  node strategies/main_moneyflow_score.js --date "$DATE"
  node strategies/sector_heat_score.js --date "$DATE"
  node strategies/emotion_score.js --date "$DATE"
  node strategies/export_for_test.js --date "$DATE" --top 10
  echo "=== Done: $DATE ==="
done
