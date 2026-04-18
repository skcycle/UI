#!/usr/bin/env bash
set -euo pipefail
export TUSHARE_TOKEN='d7843466ea6bd6a8125e5d6990763087b6ab0df586f5f21a8b8795d2'
DATE='2026-04-07'
# 新权重方案：
# sector_heat 12/20=0.6
# fundamental_momentum 8/10=0.8
# volume_price_sync 18/15=1.2
# kline_pattern 18/15=1.2
# technical_indicators 10/15=0.6667
# main_moneyflow 22/15=1.4667
# emotion 12/10=1.2
WEIGHTS='{"sector_heat":0.6,"fundamental_momentum":0.8,"volume_price_sync":1.2,"kline_pattern":1.2,"technical_indicators":0.6667,"main_moneyflow":1.4667,"emotion":1.2}'
cd /root/.openclaw/workspace-score
node strategies/export_for_test.js --date "$DATE" --top 10 --weights "$WEIGHTS"
