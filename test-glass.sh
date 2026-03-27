#!/bin/bash
# 玻璃计算测试脚本

BASE_URL="http://127.0.0.1:5188"
SHORT_SIDE=1000
LONG_SIDE=2000
WIND_LOAD=1.5

PASS_COUNT=0
FAIL_COUNT=0

echo "=== 玻璃计算测试 ==="

# 单片玻璃
echo ""
echo "=== 单片玻璃测试 (6mm-12mm) ==="
for t in 6 8 10 12; do
    JSON="{\"shortSide\":$SHORT_SIDE,\"longSide\":$LONG_SIDE,\"thickness\":$t,\"glassType\":\"Tempered\",\"windLoad\":$WIND_LOAD,\"supportCondition\":\"FourSides\"}"
    RESPONSE=$(curl -s "$BASE_URL/api/GlassCalculation/SingleGlass" -X POST -H "Content-Type: application/json" -d "$JSON")
    
    # 简单检查是否包含passed:true
    if echo "$RESPONSE" | grep -q '"passed":true'; then
        MAX_STRESS=$(echo "$RESPONSE" | sed 's/.*"maxStress":\([0-9.]*\).*/\1/' | head -1)
        MAX_DEFLECTION=$(echo "$RESPONSE" | sed 's/.*"maxDeflection":\([0-9.]*\).*/\1/' | head -1)
        echo "单片 ${t}mm: σ=${MAX_STRESS}MPa, df=${MAX_DEFLECTION}mm -> PASS"
        ((PASS_COUNT++))
    else
        echo "单片 ${t}mm: FAIL"
        ((FAIL_COUNT++))
    fi
done

# 中空玻璃
echo ""
echo "=== 中空玻璃测试 (6+6到12+12) ==="
for t1 in 6 8 10 12; do
    for t2 in 6 8 10 12; do
        JSON="{\"shortSide\":$SHORT_SIDE,\"longSide\":$LONG_SIDE,\"outerThickness\":$t1,\"innerThickness\":$t2,\"outerGlassType\":\"Tempered\",\"innerGlassType\":\"Tempered\",\"windLoad\":$WIND_LOAD,\"altitudeDifference\":0,\"temperatureDifference\":0}"
        RESPONSE=$(curl -s "$BASE_URL/api/GlassCalculation/InsulatingGlass" -X POST -H "Content-Type: application/json" -d "$JSON")
        
        if echo "$RESPONSE" | grep -q '"passed":true'; then
            MAX_STRESS=$(echo "$RESPONSE" | sed 's/.*"maxStress":\([0-9.]*\).*/\1/' | head -1)
            MAX_DEFLECTION=$(echo "$RESPONSE" | sed 's/.*"maxDeflection":\([0-9.]*\).*/\1/' | head -1)
            echo "中空 ${t1}+${t2}: σ=${MAX_STRESS}MPa, df=${MAX_DEFLECTION}mm -> PASS"
            ((PASS_COUNT++))
        else
            echo "中空 ${t1}+${t2}: FAIL"
            ((FAIL_COUNT++))
        fi
    done
done

echo ""
echo "=== 测试结果 ==="
echo "通过: $PASS_COUNT"
echo "失败: $FAIL_COUNT"
