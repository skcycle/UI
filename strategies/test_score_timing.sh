#!/usr/bin/env bash
set -euo pipefail
export TUSHARE_TOKEN='d7843466ea6bd6a8125e5d6990763087b6ab0df586f5f21a8b8795d2'
export MAIRUI_LICENSE='EE4E0843-AAA7-4B04-B367-4A7345CAE9A7'
DATE='2026-04-14'
cd /root/.openclaw/workspace-score

echo "========== 评分流程耗时测试 =========="
echo "开始时间: $(date '+%H:%M:%S')"
TOTAL_START=$(date +%s)

# Step 1: 预取数据
STEP1_START=$(date +%s)
echo -e "\n[Step 1/9] 预取数据..."
node strategies/prefetch_daily_bundle.js --date "$DATE" > /dev/null 2>&1
STEP1_END=$(date +%s)
STEP1_TIME=$((STEP1_END - STEP1_START))
echo "Step 1 耗时: ${STEP1_TIME} 秒"

# Step 2: 基本面动量评分
STEP2_START=$(date +%s)
echo -e "\n[Step 2/9] 基本面动量评分..."
node strategies/fundamental_momentum_score.js --date "$DATE" > /dev/null 2>&1
STEP2_END=$(date +%s)
STEP2_TIME=$((STEP2_END - STEP2_START))
echo "Step 2 耗时: ${STEP2_TIME} 秒"

# Step 3: 量价协同评分
STEP3_START=$(date +%s)
echo -e "\n[Step 3/9] 量价协同评分..."
node strategies/volume_price_sync_score.js --date "$DATE" > /dev/null 2>&1
STEP3_END=$(date +%s)
STEP3_TIME=$((STEP3_END - STEP3_START))
echo "Step 3 耗时: ${STEP3_TIME} 秒"

# Step 4: K线形态评分
STEP4_START=$(date +%s)
echo -e "\n[Step 4/9] K线形态评分..."
node strategies/kline_pattern_score.js --date "$DATE" > /dev/null 2>&1
STEP4_END=$(date +%s)
STEP4_TIME=$((STEP4_END - STEP4_START))
echo "Step 4 耗时: ${STEP4_TIME} 秒"

# Step 5: 技术指标评分
STEP5_START=$(date +%s)
echo -e "\n[Step 5/9] 技术指标评分..."
node strategies/technical_indicators_score.js --date "$DATE" > /dev/null 2>&1
STEP5_END=$(date +%s)
STEP5_TIME=$((STEP5_END - STEP5_START))
echo "Step 5 耗时: ${STEP5_TIME} 秒"

# Step 6: 主力资金流评分
STEP6_START=$(date +%s)
echo -e "\n[Step 6/9] 主力资金流评分..."
node strategies/main_moneyflow_score.js --date "$DATE" > /dev/null 2>&1
STEP6_END=$(date +%s)
STEP6_TIME=$((STEP6_END - STEP6_START))
echo "Step 6 耗时: ${STEP6_TIME} 秒"

# Step 7: 板块热度评分
STEP7_START=$(date +%s)
echo -e "\n[Step 7/9] 板块热度评分..."
node strategies/sector_heat_score.js --date "$DATE" > /dev/null 2>&1
STEP7_END=$(date +%s)
STEP7_TIME=$((STEP7_END - STEP7_START))
echo "Step 7 耗时: ${STEP7_TIME} 秒"

# Step 8: 情绪评分
STEP8_START=$(date +%s)
echo -e "\n[Step 8/9] 情绪评分..."
node strategies/emotion_score.js --date "$DATE" > /dev/null 2>&1
STEP8_END=$(date +%s)
STEP8_TIME=$((STEP8_END - STEP8_START))
echo "Step 8 耗时: ${STEP8_TIME} 秒"

# Step 9: 导出Top10
STEP9_START=$(date +%s)
echo -e "\n[Step 9/9] 导出Top10..."
node strategies/export_for_test.js --date "$DATE" --top 10 > /dev/null 2>&1
STEP9_END=$(date +%s)
STEP9_TIME=$((STEP9_END - STEP9_START))
echo "Step 9 耗时: ${STEP9_TIME} 秒"

TOTAL_END=$(date +%s)
TOTAL_TIME=$((TOTAL_END - TOTAL_START))

echo -e "\n========== 评分流程完成 =========="
echo "结束时间: $(date '+%H:%M:%S')"
echo "总耗时: ${TOTAL_TIME} 秒"

echo -e "\n各步骤耗时汇总:"
echo "  Step 1 (预取数据):        ${STEP1_TIME} 秒"
echo "  Step 2 (基本面动量):      ${STEP2_TIME} 秒"
echo "  Step 3 (量价协同):        ${STEP3_TIME} 秒"
echo "  Step 4 (K线形态):         ${STEP4_TIME} 秒"
echo "  Step 5 (技术指标):        ${STEP5_TIME} 秒"
echo "  Step 6 (主力资金流):      ${STEP6_TIME} 秒"
echo "  Step 7 (板块热度):        ${STEP7_TIME} 秒"
echo "  Step 8 (情绪评分):        ${STEP8_TIME} 秒"
echo "  Step 9 (导出Top10):       ${STEP9_TIME} 秒"
echo "  ----------------------------------------"
echo "  总计:                     ${TOTAL_TIME} 秒"
