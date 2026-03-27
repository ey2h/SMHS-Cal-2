#!/bin/bash
# 完整玻璃计算测试脚本

BASE_URL="http://127.0.0.1:5188"
SHORT_SIDE=1000
LONG_SIDE=2000
WIND_LOAD=1.5

PASS_COUNT=0
FAIL_COUNT=0

echo "=== 完整玻璃计算测试 ==="

# ====== 1. 单片玻璃测试 ======
echo ""
echo "=== 1. 单片玻璃测试 (6mm-12mm) ==="
for t in 6 8 10 12; do
    JSON="{\"shortSide\":$SHORT_SIDE,\"longSide\":$LONG_SIDE,\"thickness\":$t,\"glassType\":\"Tempered\",\"windLoad\":$WIND_LOAD,\"supportCondition\":\"FourSides\"}"
    RESPONSE=$(curl -s "$BASE_URL/api/GlassCalculation/SingleGlass" -X POST -H "Content-Type: application/json" -d "$JSON")
    
    if echo "$RESPONSE" | grep -q '"passed":true'; then
        MAX_STRESS=$(echo "$RESPONSE" | sed 's/.*"maxStress":\([0-9.]*\).*/\1/' | head -1)
        MAX_DEFLECTION=$(echo "$RESPONSE" | sed 's/.*"maxDeflection":\([0-9.]*\).*/\1/' | head -1)
        echo "  ${t}mm: σ=${MAX_STRESS}MPa, df=${MAX_DEFLECTION}mm -> PASS"
        ((PASS_COUNT++))
    else
        echo "  ${t}mm: FAIL"
        ((FAIL_COUNT++))
    fi
done

# ====== 2. 中空玻璃测试 ======
echo ""
echo "=== 2. 中空玻璃测试 (6+6 到 12+12) ==="
for t1 in 6 8 10 12; do
    for t2 in 6 8 10 12; do
        JSON="{\"shortSide\":$SHORT_SIDE,\"longSide\":$LONG_SIDE,\"outerThickness\":$t1,\"innerThickness\":$t2,\"outerGlassType\":\"Tempered\",\"innerGlassType\":\"Tempered\",\"windLoad\":$WIND_LOAD,\"altitudeDifference\":0,\"temperatureDifference\":0}"
        RESPONSE=$(curl -s "$BASE_URL/api/GlassCalculation/InsulatingGlass" -X POST -H "Content-Type: application/json" -d "$JSON")
        
        if echo "$RESPONSE" | grep -q '"passed":true'; then
            MAX_STRESS=$(echo "$RESPONSE" | sed 's/.*"maxStress":\([0-9.]*\).*/\1/' | head -1)
            MAX_DEFLECTION=$(echo "$RESPONSE" | sed 's/.*"maxDeflection":\([0-9.]*\).*/\1/' | head -1)
            echo "  ${t1}+${t2}: σ=${MAX_STRESS}MPa, df=${MAX_DEFLECTION}mm -> PASS"
            ((PASS_COUNT++))
        else
            echo "  ${t1}+${t2}: FAIL"
            ((FAIL_COUNT++))
        fi
    done
done

# ====== 3. 夹胶玻璃测试 (使用CompositeGlass) ======
echo ""
echo "=== 3. 夹胶玻璃测试 (6+6 到 12+12) ==="
for t in 6 8 10 12; do
    # 使用统一计算API测试夹胶玻璃
    JSON="{\"shortSide\":$SHORT_SIDE,\"longSide\":$LONG_SIDE,\"windLoad\":$WIND_LOAD,\"GlassConfig\":{\"PanelCategory\":1,\"OuterPane\":{\"IsLaminated\":true,\"Thickness\":$t,\"TemperedType\":1,\"LaminatedSheets\":[{\"Thickness\":$t,\"TemperedType\":1},{\"Thickness\":$t,\"TemperedType\":1}],\"LaminatedLayers\":[{\"FilmType\":0,\"FilmThickness\":1.52}]}}}"
    RESPONSE=$(curl -s "$BASE_URL/api/GlassCalculation/UnifiedCalculate" -X POST -H "Content-Type: application/json" -d "$JSON")
    
    if echo "$RESPONSE" | grep -q '"passed":true'; then
        MAX_STRESS=$(echo "$RESPONSE" | sed 's/.*"maxStress":\([0-9.]*\).*/\1/' | head -1)
        MAX_DEFLECTION=$(echo "$RESPONSE" | sed 's/.*"maxDeflection":\([0-9.]*\).*/\1/' | head -1)
        echo "  夹胶 ${t}+${t}: σ=${MAX_STRESS}MPa, df=${MAX_DEFLECTION}mm -> PASS"
        ((PASS_COUNT++))
    else
        echo "  夹胶 ${t}+${t}: FAIL"
        ((FAIL_COUNT++))
    fi
done

# ====== 4. 中空夹胶玻璃测试 ======
echo ""
echo "=== 4. 中空夹胶玻璃测试 ==="
# 测试 6+pvb+6+12A+6 到 12+pvb+12+12A+12
for ot in 6 8 10 12; do
    for it in 6 8 10 12; do
        JSON="{\"shortSide\":$SHORT_SIDE,\"longSide\":$LONG_SIDE,\"windLoad\":$WIND_LOAD,\"GlassConfig\":{\"PanelCategory\":2,\"OuterPane\":{\"IsLaminated\":true,\"Thickness\":$ot,\"TemperedType\":1,\"LaminatedSheets\":[{\"Thickness\":$ot,\"TemperedType\":1},{\"Thickness\":$ot,\"TemperedType\":1}],\"LaminatedLayers\":[{\"FilmType\":0,\"FilmThickness\":1.52}]},\"InnerPane\":{\"IsLaminated\":false,\"Thickness\":$it,\"TemperedType\":1},\"FirstGap\":{\"Thickness\":12,\"GasType\":0}}}"
        RESPONSE=$(curl -s "$BASE_URL/api/GlassCalculation/UnifiedCalculate" -X POST -H "Content-Type: application/json" -d "$JSON")
        
        if echo "$RESPONSE" | grep -q '"passed":true'; then
            MAX_STRESS=$(echo "$RESPONSE" | sed 's/.*"maxStress":\([0-9.]*\).*/\1/' | head -1)
            MAX_DEFLECTION=$(echo "$RESPONSE" | sed 's/.*"maxDeflection":\([0-9.]*\).*/\1/' | head -1)
            echo "  ${ot}+pvb+${ot}+12A+${it}: σ=${MAX_STRESS}MPa, df=${MAX_DEFLECTION}mm -> PASS"
            ((PASS_COUNT++))
        else
            echo "  ${ot}+pvb+${ot}+12A+${it}: FAIL"
            ((FAIL_COUNT++))
        fi
    done
done

# ====== 5. 双夹胶中空玻璃测试 ======
echo ""
echo "=== 5. 双夹胶中空玻璃测试 ==="
for t in 6 8 10 12; do
    JSON="{\"shortSide\":$SHORT_SIDE,\"longSide\":$LONG_SIDE,\"windLoad\":$WIND_LOAD,\"GlassConfig\":{\"PanelCategory\":2,\"OuterPane\":{\"IsLaminated\":true,\"Thickness\":$t,\"TemperedType\":1,\"LaminatedSheets\":[{\"Thickness\":$t,\"TemperedType\":1},{\"Thickness\":$t,\"TemperedType\":1}],\"LaminatedLayers\":[{\"FilmType\":0,\"FilmThickness\":1.52}]},\"InnerPane\":{\"IsLaminated\":true,\"Thickness\":$t,\"TemperedType\":1,\"LaminatedSheets\":[{\"Thickness\":$t,\"TemperedType\":1},{\"Thickness\":$t,\"TemperedType\":1}],\"LaminatedLayers\":[{\"FilmType\":0,\"FilmThickness\":1.52}]},\"FirstGap\":{\"Thickness\":12,\"GasType\":0}}}"
    RESPONSE=$(curl -s "$BASE_URL/api/GlassCalculation/UnifiedCalculate" -X POST -H "Content-Type: application/json" -d "$JSON")
    
    if echo "$RESPONSE" | grep -q '"passed":true'; then
        MAX_STRESS=$(echo "$RESPONSE" | sed 's/.*"maxStress":\([0-9.]*\).*/\1/' | head -1)
        MAX_DEFLECTION=$(echo "$RESPONSE" | sed 's/.*"maxDeflection":\([0-9.]*\).*/\1/' | head -1)
        echo "  ${t}+pvb+${t}+12A+${t}+pvb+${t}: σ=${MAX_STRESS}MPa, df=${MAX_DEFLECTION}mm -> PASS"
        ((PASS_COUNT++))
    else
        echo "  ${t}+pvb+${t}+12A+${t}+pvb+${t}: FAIL"
        ((FAIL_COUNT++))
    fi
done

# ====== 6. 三玻两腔测试 ======
echo ""
echo "=== 6. 三玻两腔测试 ==="
for t in 6 8 10 12; do
    JSON="{\"shortSide\":$SHORT_SIDE,\"longSide\":$LONG_SIDE,\"windLoad\":$WIND_LOAD,\"GlassConfig\":{\"PanelCategory\":3,\"OuterPane\":{\"IsLaminated\":false,\"Thickness\":$t,\"TemperedType\":1},\"MiddlePane\":{\"IsLaminated\":false,\"Thickness\":$t,\"TemperedType\":1},\"InnerPane\":{\"IsLaminated\":false,\"Thickness\":$t,\"TemperedType\":1},\"FirstGap\":{\"Thickness\":12,\"GasType\":0},\"SecondGap\":{\"Thickness\":12,\"GasType\":0}}}"
    RESPONSE=$(curl -s "$BASE_URL/api/GlassCalculation/UnifiedCalculate" -X POST -H "Content-Type: application/json" -d "$JSON")
    
    if echo "$RESPONSE" | grep -q '"passed":true'; then
        MAX_STRESS=$(echo "$RESPONSE" | sed 's/.*"maxStress":\([0-9.]*\).*/\1/' | head -1)
        MAX_DEFLECTION=$(echo "$RESPONSE" | sed 's/.*"maxDeflection":\([0-9.]*\).*/\1/' | head -1)
        echo "  ${t}+12A+${t}+12A+${t}: σ=${MAX_STRESS}MPa, df=${MAX_DEFLECTION}mm -> PASS"
        ((PASS_COUNT++))
    else
        echo "  ${t}+12A+${t}+12A+${t}: FAIL"
        ((FAIL_COUNT++))
    fi
done

# ====== 7. 夹胶三玻两腔测试 ======
echo ""
echo "=== 7. 夹胶三玻两腔测试 ==="
for t in 6 8 10 12; do
    JSON="{\"shortSide\":$SHORT_SIDE,\"longSide\":$LONG_SIDE,\"windLoad\":$WIND_LOAD,\"GlassConfig\":{\"PanelCategory\":3,\"OuterPane\":{\"IsLaminated\":true,\"Thickness\":$t,\"TemperedType\":1,\"LaminatedSheets\":[{\"Thickness\":$t,\"TemperedType\":1},{\"Thickness\":$t,\"TemperedType\":1}],\"LaminatedLayers\":[{\"FilmType\":0,\"FilmThickness\":1.52}]},\"MiddlePane\":{\"IsLaminated\":false,\"Thickness\":$t,\"TemperedType\":1},\"InnerPane\":{\"IsLaminated\":true,\"Thickness\":$t,\"TemperedType\":1,\"LaminatedSheets\":[{\"Thickness\":$t,\"TemperedType\":1},{\"Thickness\":$t,\"TemperedType\":1}],\"LaminatedLayers\":[{\"FilmType\":0,\"FilmThickness\":1.52}]},\"FirstGap\":{\"Thickness\":12,\"GasType\":0},\"SecondGap\":{\"Thickness\":12,\"GasType\":0}}}"
    RESPONSE=$(curl -s "$BASE_URL/api/GlassCalculation/UnifiedCalculate" -X POST -H "Content-Type: application/json" -d "$JSON")
    
    if echo "$RESPONSE" | grep -q '"passed":true'; then
        MAX_STRESS=$(echo "$RESPONSE" | sed 's/.*"maxStress":\([0-9.]*\).*/\1/' | head -1)
        MAX_DEFLECTION=$(echo "$RESPONSE" | sed 's/.*"maxDeflection":\([0-9.]*\).*/\1/' | head -1)
        echo "  ${t}+pvb+${t}+12A+${t}+pvb+${t}: σ=${MAX_STRESS}MPa, df=${MAX_DEFLECTION}mm -> PASS"
        ((PASS_COUNT++))
    else
        echo "  ${t}+pvb+${t}+12A+${t}+pvb+${t}: FAIL"
        ((FAIL_COUNT++))
    fi
done

echo ""
echo "========================================="
echo "=== 测试结果 ==="
echo "通过: $PASS_COUNT"
echo "失败: $FAIL_COUNT"
echo "总计: $((PASS_COUNT + FAIL_COUNT))"
echo "========================================="
