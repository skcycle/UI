#!/usr/bin/env bash
set -euo pipefail
export TUSHARE_TOKEN='d7843466ea6bd6a8125e5d6990763087b6ab0df586f5f21a8b8795d2'
export MAIRUI_LICENSE='EE4E0843-AAA7-4B04-B367-4A7345CAE9A7'
DATE='2026-04-14'
cd /root/.openclaw/workspace-score

ROUNDS=10
echo "========== 评分系统 ${ROUNDS} 轮耗时测试 =========="
echo "开始时间: $(date '+%Y-%m-%d %H:%M:%S')"

# 存储每轮耗时
declare -a TIMES

for i in $(seq 1 $ROUNDS); do
  echo -e "\n----- 第 ${i}/${ROUNDS} 轮 -----"
  START=$(date +%s)
  
  # 运行所有评分步骤（静默模式）
  node strategies/prefetch_daily_bundle.js --date "$DATE" > /dev/null 2>&1
  node strategies/fundamental_momentum_score.js --date "$DATE" > /dev/null 2>&1
  node strategies/volume_price_sync_score.js --date "$DATE" > /dev/null 2>&1
  node strategies/kline_pattern_score.js --date "$DATE" > /dev/null 2>&1
  node strategies/technical_indicators_score.js --date "$DATE" > /dev/null 2>&1
  node strategies/main_moneyflow_score.js --date "$DATE" > /dev/null 2>&1
  node strategies/sector_heat_score.js --date "$DATE" > /dev/null 2>&1
  node strategies/emotion_score.js --date "$DATE" > /dev/null 2>&1
  node strategies/export_for_test.js --date "$DATE" --top 10 > /dev/null 2>&1
  
  END=$(date +%s)
  ELAPSED=$((END - START))
  TIMES+=($ELAPSED)
  echo "第 ${i} 轮耗时: ${ELAPSED} 秒"
done

echo -e "\n========== 测试完成 =========="
echo "结束时间: $(date '+%Y-%m-%d %H:%M:%S')"

# 计算统计信息
SUM=0
MIN=${TIMES[0]}
MAX=${TIMES[0]}

echo -e "\n每轮耗时统计:"
for i in "${!TIMES[@]}"; do
  T=${TIMES[$i]}
  echo "  第 $((i+1)) 轮: ${T} 秒"
  SUM=$((SUM + T))
  if [ $T -lt $MIN ]; then MIN=$T; fi
  if [ $T -gt $MAX ]; then MAX=$T; fi
done

AVG=$((SUM / ROUNDS))
echo -e "\n汇总统计:"
echo "  总轮数: ${ROUNDS}"
echo "  总耗时: ${SUM} 秒"
echo "  平均耗时: ${AVG} 秒"
echo "  最小耗时: ${MIN} 秒"
echo "  最大耗时: ${MAX} 秒"
