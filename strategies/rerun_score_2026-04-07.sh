#!/usr/bin/env bash
set -euo pipefail
export TUSHARE_TOKEN='d7843466ea6bd6a8125e5d6990763087b6ab0df586f5f21a8b8795d2'
export MAIRUI_LICENSE='EE4E0843-AAA7-4B04-B367-4A7345CAE9A7'
DATE='2026-04-07'
cd /root/.openclaw/workspace-score
node strategies/prefetch_daily_bundle.js --date "$DATE"
node strategies/fundamental_momentum_score.js --date "$DATE"
node strategies/volume_price_sync_score.js --date "$DATE"
node strategies/kline_pattern_score.js --date "$DATE"
node strategies/technical_indicators_score.js --date "$DATE"
node strategies/main_moneyflow_score.js --date "$DATE"
node strategies/sector_heat_score.js --date "$DATE"
node strategies/emotion_score.js --date "$DATE"
node strategies/export_for_test.js --date "$DATE" --top 10
