#!/usr/bin/env bash
export TUSHARE_TOKEN='d7843466ea6bd6a8125e5d6990763087b6ab0df586f5f21a8b8795d2'
cd /root/.openclaw/workspace-score
node strategies/export_for_test.js --date 2026-04-14 --top 10
