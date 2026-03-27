#!/bin/bash
# 静力计算测试脚本 - 梁的力学计算

BASE_URL="http://127.0.0.1:5188"
PASS_COUNT=0
FAIL_COUNT=0

echo "=== 静力计算测试 ==="

# ===== 1. 简支梁测试 =====
echo ""
echo "=== 1. 简支梁 (SimpleBeam) ==="

# 简支梁-均布荷载
JSON='{"Support":0,"Load":0,"L":6000,"Q":1.5,"E":70000,"I":1000000,"NumPoints":100}'
RESPONSE=$(curl -s "$BASE_URL/api/StaticCalc/beam" -X POST -H "Content-Type: application/json" -d "$JSON")
if echo "$RESPONSE" | grep -q '"maxMoment"'; then
    MAX_M=$(echo "$RESPONSE" | python3 -c "import sys,json; d=json.load(sys.stdin); print(d.get('maxMoment',0))" 2>/dev/null || echo "0")
    MAX_D=$(echo "$RESPONSE" | python3 -c "import sys,json; d=json.load(sys.stdin); print(d.get('maxDeflection',0))" 2>/dev/null || echo "0")
    echo "  简支梁-均布荷载: M=${MAX_M}N·mm, df=${MAX_D}mm -> PASS"
    ((PASS_COUNT++))
else
    echo "  简支梁-均布荷载: FAIL - $RESPONSE"
    ((FAIL_COUNT++))
fi

# 简支梁-集中荷载
JSON='{"Support":0,"Load":1,"L":6000,"P":10,"E":70000,"I":1000000,"NumPoints":100}'
RESPONSE=$(curl -s "$BASE_URL/api/StaticCalc/beam" -X POST -H "Content-Type: application/json" -d "$JSON")
if echo "$RESPONSE" | grep -q '"maxMoment"'; then
    MAX_M=$(echo "$RESPONSE" | python3 -c "import sys,json; d=json.load(sys.stdin); print(d.get('maxMoment',0))" 2>/dev/null || echo "0")
    echo "  简支梁-集中荷载: M=${MAX_M}N·mm -> PASS"
    ((PASS_COUNT++))
else
    echo "  简支梁-集中荷载: FAIL"
    ((FAIL_COUNT++))
fi

# ===== 2. 悬臂梁测试 =====
echo ""
echo "=== 2. 悬臂梁 (Cantilever) ==="

JSON='{"Support":1,"Load":0,"L":3000,"Q":1.5,"E":70000,"I":1000000,"NumPoints":100}'
RESPONSE=$(curl -s "$BASE_URL/api/StaticCalc/beam" -X POST -H "Content-Type: application/json" -d "$JSON")
if echo "$RESPONSE" | grep -q '"maxMoment"'; then
    MAX_M=$(echo "$RESPONSE" | python3 -c "import sys,json; d=json.load(sys.stdin); print(d.get('maxMoment',0))" 2>/dev/null || echo "0")
    echo "  悬臂梁-均布荷载: M=${MAX_M}N·mm -> PASS"
    ((PASS_COUNT++))
else
    echo "  悬臂梁-均布荷载: FAIL"
    ((FAIL_COUNT++))
fi

# ===== 3. 双跨梁测试 =====
echo ""
echo "=== 3. 双跨梁 (DoubleSpan) ==="

JSON='{"Support":2,"Load":0,"L":6000,"L2":6000,"Q":1.5,"E":70000,"I":1000000,"NumPoints":100}'
RESPONSE=$(curl -s "$BASE_URL/api/StaticCalc/beam" -X POST -H "Content-Type: application/json" -d "$JSON")
if echo "$RESPONSE" | grep -q '"maxMoment"'; then
    MAX_M=$(echo "$RESPONSE" | python3 -c "import sys,json; d=json.load(sys.stdin); print(d.get('maxMoment',0))" 2>/dev/null || echo "0")
    echo "  双跨梁-均布荷载: M=${MAX_M}N·mm -> PASS"
    ((PASS_COUNT++))
else
    echo "  双跨梁-均布荷载: FAIL"
    ((FAIL_COUNT++))
fi

# ===== 4. 三跨连续梁测试 =====
echo ""
echo "=== 4. 三跨连续梁 (ContinuousThreeSpan) ==="

JSON='{"Support":3,"Load":0,"L":6000,"L2":6000,"L3":6000,"Q":1.5,"E":70000,"I":1000000,"NumPoints":100}'
RESPONSE=$(curl -s "$BASE_URL/api/StaticCalc/beam" -X POST -H "Content-Type: application/json" -d "$JSON")
if echo "$RESPONSE" | grep -q '"maxMoment"'; then
    MAX_M=$(echo "$RESPONSE" | python3 -c "import sys,json; d=json.load(sys.stdin); print(d.get('maxMoment',0))" 2>/dev/null || echo "0")
    echo "  三跨连续梁-均布荷载: M=${MAX_M}N·mm -> PASS"
    ((PASS_COUNT++))
else
    echo "  三跨连续梁-均布荷载: FAIL"
    ((FAIL_COUNT++))
fi

# ===== 5. 多跨铰接梁测试 =====
echo ""
echo "=== 5. 多跨铰接梁 (MultiSpanHinged) ==="

# 静定
JSON='{"Support":4,"Load":0,"L":6000,"L2":6000,"Q":1.5,"E":70000,"I":1000000,"NumPoints":100}'
RESPONSE=$(curl -s "$BASE_URL/api/StaticCalc/beam" -X POST -H "Content-Type: application/json" -d "$JSON")
if echo "$RESPONSE" | grep -q '"maxMoment"'; then
    MAX_M=$(echo "$RESPONSE" | python3 -c "import sys,json; d=json.load(sys.stdin); print(d.get('maxMoment',0))" 2>/dev/null || echo "0")
    echo "  多跨铰接静定梁: M=${MAX_M}N·mm -> PASS"
    ((PASS_COUNT++))
else
    echo "  多跨铰接静定梁: FAIL"
    ((FAIL_COUNT++))
fi

# 超静定
JSON='{"Support":5,"Load":0,"L":6000,"L2":6000,"Q":1.5,"E":70000,"I":1000000,"NumPoints":100}'
RESPONSE=$(curl -s "$BASE_URL/api/StaticCalc/beam" -X POST -H "Content-Type: application/json" -d "$JSON")
if echo "$RESPONSE" | grep -q '"maxMoment"'; then
    MAX_M=$(echo "$RESPONSE" | python3 -c "import sys,json; d=json.load(sys.stdin); print(d.get('maxMoment',0))" 2>/dev/null || echo "0")
    echo "  多跨铰接超静定梁: M=${MAX_M}N·mm -> PASS"
    ((PASS_COUNT++))
else
    echo "  多跨铰接超静定梁: FAIL"
    ((FAIL_COUNT++))
fi

# ===== 6. 带悬臂简支梁测试 =====
echo ""
echo "=== 6. 带悬臂简支梁 (OverhangBeam) ==="

JSON='{"Support":6,"Load":0,"L":6000,"Lc":1500,"Q":1.5,"E":70000,"I":1000000,"NumPoints":100}'
RESPONSE=$(curl -s "$BASE_URL/api/StaticCalc/beam" -X POST -H "Content-Type: application/json" -d "$JSON")
if echo "$RESPONSE" | grep -q '"maxMoment"'; then
    MAX_M=$(echo "$RESPONSE" | python3 -c "import sys,json; d=json.load(sys.stdin); print(d.get('maxMoment',0))" 2>/dev/null || echo "0")
    echo "  带悬臂简支梁: M=${MAX_M}N·mm -> PASS"
    ((PASS_COUNT++))
else
    echo "  带悬臂简支梁: FAIL"
    ((FAIL_COUNT++))
fi

# ===== 7. 获取支持的模型列表 =====
echo ""
echo "=== 7. 获取力学模型列表 ==="
RESPONSE=$(curl -s "$BASE_URL/api/StaticCalc/models")
MODEL_COUNT=$(echo "$RESPONSE" | grep -o '"id"' | wc -l)
if [ "$MODEL_COUNT" -gt 0 ]; then
    echo "  获取模型列表: ${MODEL_COUNT}个模型 -> PASS"
    ((PASS_COUNT++))
else
    echo "  获取模型列表: FAIL"
    ((FAIL_COUNT++))
fi

# ===== 8. 边界条件测试 =====
echo ""
echo "=== 8. 边界条件测试 ==="

# 跨度为0应该报错
JSON='{"Support":0,"Load":0,"L":0,"Q":1.5,"E":70000,"I":1000000}'
RESPONSE=$(curl -s "$BASE_URL/api/StaticCalc/beam" -X POST -H "Content-Type: application/json" -d "$JSON")
if echo "$RESPONSE" | grep -q 'L必须大于0'; then
    echo "  跨度=0错误处理: -> PASS"
    ((PASS_COUNT++))
else
    echo "  跨度=0错误处理: FAIL"
    ((FAIL_COUNT++))
fi

echo ""
echo "========================================="
echo "=== 测试结果 ==="
echo "通过: $PASS_COUNT"
echo "失败: $FAIL_COUNT"
echo "总计: $((PASS_COUNT + FAIL_COUNT))"
echo "========================================="
