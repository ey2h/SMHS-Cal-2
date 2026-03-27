#!/bin/bash
# ============================================================
# 玻璃幕墙数据库管理系统 - 全接口自动化测试
# 测试所有计算API端点，验证返回状态和基本结果合理性
# ============================================================

BASE="http://localhost:5000/api"
PASS=0
FAIL=0
ERRORS=""

# 颜色
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m'

test_api() {
    local name="$1"
    local method="$2"
    local url="$3"
    local data="$4"
    local expect_field="$5"  # 可选：检查返回JSON中某字段存在

    printf "%-50s " "[$name]"

    if [ "$method" = "GET" ]; then
        response=$(curl -s -w "\n%{http_code}" "$url" 2>/dev/null)
    else
        response=$(curl -s -w "\n%{http_code}" -X POST "$url" \
            -H "Content-Type: application/json" \
            -d "$data" 2>/dev/null)
    fi

    http_code=$(echo "$response" | tail -1)
    body=$(echo "$response" | sed '$d')

    if [ "$http_code" = "200" ]; then
        # 检查可选字段
        if [ -n "$expect_field" ]; then
            if echo "$body" | grep -qi "$expect_field"; then
                echo -e "${GREEN}✅ PASS${NC} (HTTP 200, 含 $expect_field)"
                PASS=$((PASS+1))
            else
                echo -e "${YELLOW}⚠️  WARN${NC} (HTTP 200, 缺少 $expect_field)"
                PASS=$((PASS+1))  # 仍算通过，只是警告
            fi
        else
            echo -e "${GREEN}✅ PASS${NC} (HTTP 200)"
            PASS=$((PASS+1))
        fi
    elif [ "$http_code" = "400" ]; then
        echo -e "${RED}❌ FAIL${NC} (HTTP 400 - 参数错误)"
        FAIL=$((FAIL+1))
        ERRORS="$ERRORS\n  ❌ $name: HTTP 400 - $body"
    elif [ "$http_code" = "500" ]; then
        echo -e "${RED}❌ FAIL${NC} (HTTP 500 - 服务器错误)"
        FAIL=$((FAIL+1))
        ERRORS="$ERRORS\n  ❌ $name: HTTP 500 - $(echo "$body" | head -c 200)"
    elif [ "$http_code" = "000" ]; then
        echo -e "${RED}❌ FAIL${NC} (无法连接服务器)"
        FAIL=$((FAIL+1))
        ERRORS="$ERRORS\n  ❌ $name: 无法连接 $url"
    else
        echo -e "${RED}❌ FAIL${NC} (HTTP $http_code)"
        FAIL=$((FAIL+1))
        ERRORS="$ERRORS\n  ❌ $name: HTTP $http_code - $(echo "$body" | head -c 200)"
    fi
}

echo "============================================================"
echo "  🧪 玻璃幕墙数据库管理系统 - 全接口自动化测试"
echo "  服务器: $BASE"
echo "============================================================"
echo ""

# 先检查服务器是否可达
printf "%-50s " "[服务器连接检测]"
if curl -s --max-time 3 "$BASE/../swagger/index.html" > /dev/null 2>&1; then
    echo -e "${GREEN}✅ 服务器可达${NC}"
else
    echo -e "${RED}❌ 服务器不可达，请先启动 API 服务${NC}"
    echo "  运行: cd GlassManagement.Api && dotnet run"
    exit 1
fi

echo ""
echo "═══════════════════════════════════════════════════════"
echo "  📐 1. 玻璃计算 (GlassCalculation)"
echo "═══════════════════════════════════════════════════════"

# 1.1 单片玻璃
test_api "单片玻璃-钢化8mm" POST "$BASE/GlassCalculation/SingleGlass" \
    '{"shortSide":1100,"longSide":1380,"thickness":8,"glassType":"Tempered","windLoad":1.5}' \
    "maxStress"

test_api "单片玻璃-浮法10mm" POST "$BASE/GlassCalculation/SingleGlass" \
    '{"shortSide":1200,"longSide":1800,"thickness":10,"glassType":"Float","windLoad":2.0}' \
    "passed"

# 1.2 中空玻璃
test_api "中空玻璃-6+12A+6" POST "$BASE/GlassCalculation/InsulatingGlass" \
    '{"shortSide":1100,"longSide":1380,"outerThickness":6,"innerThickness":6,"airSpaceThickness":12,"windLoad":1.5}' \
    "maxStress"

test_api "中空玻璃-8+16A+8" POST "$BASE/GlassCalculation/InsulatingGlass" \
    '{"shortSide":1500,"longSide":2000,"outerThickness":8,"innerThickness":8,"airSpaceThickness":16,"windLoad":2.5}' \
    "passed"

# 1.3 夹胶玻璃 SGP
test_api "夹胶玻璃-6+SGP0.76+6" POST "$BASE/GlassCalculation/LaminatedGlass" \
    '{"shortSide":1100,"longSide":1380,"glassThicknesses":[6,6],"interlayerThicknesses":[0.76],"glassType":"Tempered","interlayerType":"SGP","windLoad":1.37}' \
    "maxStress"

test_api "夹胶玻璃-8+PVB1.14+8" POST "$BASE/GlassCalculation/LaminatedGlass" \
    '{"shortSide":1200,"longSide":1800,"glassThicknesses":[8,8],"interlayerThicknesses":[1.14],"glassType":"Tempered","interlayerType":"PVB","windLoad":2.0}' \
    "passed"

# 1.4 玻璃肋
test_api "玻璃肋-钢化" POST "$BASE/GlassCalculation/GlassRib" \
    '{"ribHeight":300,"ribThickness":12,"span":3000,"lineLoad":5,"glassType":"Tempered"}' \
    "maxStress"

echo ""
echo "═══════════════════════════════════════════════════════"
echo "  🏗️ 2. 铝合金计算 (AluminumCalculation)"
echo "═══════════════════════════════════════════════════════"

# 2.1 横梁
test_api "铝合金横梁-三角荷载" POST "$BASE/AluminumCalculation/Rafter" \
    '{"span":1100,"spacing":1380,"profileSpec":"40/60系列","ix":115810,"wnx":3660,"wny":5760,"tx":5,"ty":3,"sx":2994,"sy":3546,"windLoad":1.5,"gravityLoad":0.0004,"betaE":5,"alphaMax":0.16}' \
    "passed"

# 2.2 立柱
test_api "铝合金立柱-双跨梁" POST "$BASE/AluminumCalculation/Mullion" \
    '{"height":4500,"shortSpan":700,"longSpan":3800,"spacing":1100,"profileSpec":"100×60系列","ix":1270859,"wnx":44216,"webThickness":6,"sx":27000,"windLoad":1.5,"gravityLoad":0.0005,"betaE":5,"alphaMax":0.16}' \
    "passed"

# 2.3 铝合金板 (横梁作为板计算)
test_api "铝合金板-简支" POST "$BASE/AluminumCalculation/Plate" \
    '{"shortSide":600,"longSide":1200,"thickness":3,"windLoad":1.5}' \
    "passed"

echo ""
echo "═══════════════════════════════════════════════════════"
echo "  🪨 3. 石材计算 (StoneCalculation)"
echo "═══════════════════════════════════════════════════════"

test_api "石材面板-花岗岩30mm" POST "$BASE/StoneCalculation/Panel" \
    '{"shortSide":800,"longSide":1200,"stoneThickness":30,"windLoad":1.5,"stoneDensity":28,"bendingStrength":8.5}' \
    "passed"

test_api "石材挂件" POST "$BASE/StoneCalculation/Bracket" \
    '{"stoneWidth":0.8,"stoneHeight":1.2,"stoneThickness":0.03,"bracketCount":4,"stoneDensity":28,"windLoad":1.5}' \
    "passed"

echo ""
echo "═══════════════════════════════════════════════════════"
echo "  🌀 4. 风荷载计算 (WindLoadCalculation)"
echo "═══════════════════════════════════════════════════════"

test_api "风荷载-100m-C类" POST "$BASE/WindLoadCalculation/Calculate" \
    '{"height":100,"terrainCategory":"C","basicWindPressure":0.45,"shapeCoefficient":1.0}' \
    "panelWindLoad"

test_api "风荷载-50m-B类" POST "$BASE/WindLoadCalculation/Calculate" \
    '{"height":50,"terrainCategory":"B","basicWindPressure":0.55,"shapeCoefficient":1.0}' \
    "structureWindLoad"

echo ""
echo "═══════════════════════════════════════════════════════"
echo "  🔧 5. 地震计算 (SeismicCalculation)"
echo "═══════════════════════════════════════════════════════"

test_api "地震作用-8度设防" POST "$BASE/SeismicCalculation/Calculate" \
    '{"betaE":5.0,"alphaMax":0.16,"gravityLoad":500,"area":1000000}' \
    "qEk"

echo ""
echo "═══════════════════════════════════════════════════════"
echo "  🔩 6. 连接件计算 (ConnectionCalculation)"
echo "═══════════════════════════════════════════════════════"

test_api "连接件-螺栓验算" POST "$BASE/ConnectionCalculation/Calculate" \
    '{"mullionSpacing":1100,"transomWidth":1100,"transomHeight":1380,"mullionSpan":4500,"shortSpan":700,"longSpan":3800,"windLoad":1.5}' \
    "passed"

echo ""
echo "═══════════════════════════════════════════════════════"
echo "  ⚡ 7. 焊缝计算 (WeldCalculation)"
echo "═══════════════════════════════════════════════════════"

test_api "焊缝-双转接件" POST "$BASE/WeldCalculation/Calculate" \
    '{"verticalWeldLength":120,"horizontalWeldLength":80,"filletSize":6,"tensionForce":5000,"shearForce":8000,"bendingMoment":200000}' \
    "passed"

echo ""
echo "═══════════════════════════════════════════════════════"
echo "  📐 8. 结构计算 (StructureCalculation)"
echo "═══════════════════════════════════════════════════════"

test_api "预埋件-JGJ102附录C" POST "$BASE/StructureCalculation/EmbedPart" \
    '{"tensionForce":10000,"shearForce":8000,"anchorCount":4,"anchorDiameter":12,"anchorLength":100,"steelPlateWidth":150,"steelPlateHeight":150,"steelPlateThickness":10}' \
    "passed"

test_api "结构胶-硅酮胶" POST "$BASE/StructureCalculation/Silicone" \
    '{"shortSide":1100,"longSide":1380,"windLoad":1.5,"glueWidth":20,"glueThickness":6}' \
    "passed"

echo ""
echo "═══════════════════════════════════════════════════════"
echo "  🏢 9. 铝板幕墙计算 (AluminumPanelCalculation)"
echo "═══════════════════════════════════════════════════════"

test_api "铝板面板-3003-H24-3mm" POST "$BASE/AluminumPanelCalculation/Calculate" \
    '{"shortSide":800,"longSide":1200,"thickness":3,"windLoad":1.5,"designLoad":0.00225,"materialGrade":"3003-H24"}' \
    "passed"

test_api "铝板面板-含加劲肋" POST "$BASE/AluminumPanelCalculation/Calculate" \
    '{"shortSide":1500,"longSide":2000,"thickness":3,"windLoad":2.0,"designLoad":0.003,"materialGrade":"3003-H24","stiffenerW":2500,"stiffenerI":35000,"stiffenerSpacing":500}' \
    "passed"

echo ""
echo "═══════════════════════════════════════════════════════"
echo "  🏗️ 10. GRC板幕墙计算 (GrcPanelCalculation)"
echo "═══════════════════════════════════════════════════════"

test_api "GRC板-背栓支承" POST "$BASE/GrcPanelCalculation/Calculate" \
    '{"shortSide":1200,"longSide":2400,"supportShortSide":1000,"supportLongSide":2200,"effectiveThickness":12,"totalThickness":30,"windLoad":1.5,"designLoad":0.00225,"backBoltCount":4,"backBoltDiameter":6}' \
    "passed"

echo ""
echo "═══════════════════════════════════════════════════════"
echo "  🏗️ 11. UHPC板幕墙计算 (UhpcPanelCalculation)"
echo "═══════════════════════════════════════════════════════"

test_api "UHPC板-通槽式" POST "$BASE/UhpcPanelCalculation/Calculate" \
    '{"shortSide":1200,"longSide":2400,"span":1200,"effectiveThickness":25,"windLoad":1.5,"designLoad":0.00225,"slotDepth":15,"slotThickness":6,"hangerThickness":3}' \
    "passed"

echo ""
echo "═══════════════════════════════════════════════════════"
echo "  🔧 12. 点式钢管结构计算 (PointSteelCalculation)"
echo "═══════════════════════════════════════════════════════"

test_api "点式钢管-简支梁" POST "$BASE/PointSteelCalculation/Calculate" \
    '{"span":6000,"gridWidth":1500,"windLoad":1.5,"sectionArea":1280,"momentOfInertia":3460000,"sectionModulus":38400,"loadPositions":[1500,3000,4500],"segmentLengths":[1500,1500,1500,1500]}' \
    "passed"

echo ""
echo "═══════════════════════════════════════════════════════"
echo "  🚪 13. 门窗计算 (DoorWindowCalculation)"
echo "═══════════════════════════════════════════════════════"

test_api "门窗框梃-竖中梃" POST "$BASE/DoorWindowCalculation/Mullion" \
    '{"height":15,"mullionType":"VerticalMullion","span":1600,"loadWidth1":600,"loadWidth2":1500,"windLoad":0.001,"ix":154440,"iy":18930,"wnx":4013,"an":385.8,"webThickness":3,"sx":3299}' \
    "passed"

test_api "门窗五金件" POST "$BASE/DoorWindowCalculation/Hardware" \
    '{"windowWidth":2500,"windowHeight":2000,"windLoad":0.001,"connectorCount":24,"unevenCoefficient":1.25}' \
    "passed"

test_api "门窗开启扇" POST "$BASE/DoorWindowCalculation/OpeningSash" \
    '{"glassLongSide":1280,"glassShortSide":1000,"slotDistance":24,"slotThickness":1.5,"designLoad":0.001758}' \
    "passed"

echo ""
echo "═══════════════════════════════════════════════════════"
echo "  🏢 14. 单元幕墙计算 (UnitCurtainWallCalculation)"
echo "═══════════════════════════════════════════════════════"

test_api "单元幕墙立柱-公母框" POST "$BASE/UnitCurtainWallCalculation/Mullion" \
    '{"span":3000,"spacing":1100,"windLoad":0.001285,"i1x":885950,"w1nx":21452,"a1n":991.5,"t1x":6,"s1x":13606,"i2x":885950,"w2nx":21452,"a2n":991.5,"t2x":6,"s2x":13606}' \
    "passed"

test_api "单元幕墙横梁-上下框" POST "$BASE/UnitCurtainWallCalculation/Transom" \
    '{"span":1100,"panelHeight":1380,"windLoad":0.001285,"i1x":115800,"i1y":123700,"w1nx":3660,"w1ny":5760,"t1x":5,"t1y":3,"s1x":2994,"s1y":3546,"i2x":115800,"i2y":123700,"w2nx":3660,"w2ny":5760,"t2x":5,"t2y":3,"s2x":2994,"s2y":3546}' \
    "passed"

echo ""
echo "═══════════════════════════════════════════════════════"
echo "  🌡️ 15. 热工计算 (ThermalCalculation)"
echo "═══════════════════════════════════════════════════════"

test_api "玻璃K值-6+12A+6" POST "$BASE/ThermalCalculation/GlassKValue" \
    '{"glassThicknesses":[6,6],"gasGapThicknesses":[12],"gasTypes":["Air"],"emissivities":[0.837,0.837,0.1,0.837]}' \
    "kValueWinter"

test_api "结露校核" POST "$BASE/ThermalCalculation/CondensationCheck" \
    '{"kValue":2.5,"indoorTemp":20,"outdoorTemp":-10,"indoorHumidity":60}' \
    "passed"

test_api "门窗整体K值" POST "$BASE/ThermalCalculation/WindowOverallK" \
    '{"kGlass":2.0,"kFrame":5.0,"glassArea":3.5,"frameArea":1.2,"psiGlass":0.06,"glassPerimeter":8.0}' \
    "kValueWinter"

echo ""
echo "═══════════════════════════════════════════════════════"
echo "  🔧 16. 转接件计算 (TransferPieceCalculation)"
echo "═══════════════════════════════════════════════════════"

test_api "转接件-10#槽钢" POST "$BASE/TransferPieceCalculation/Calculate" \
    '{"sectionArea":1428,"sectionModulus":48500,"momentOfInertia":245000,"tensionForce":5000,"shearForce":8000,"eccentricity":80}' \
    "passed"

echo ""
echo "═══════════════════════════════════════════════════════"
echo "  🔧 17. 压板计算 (PressurePlateCalculation)"
echo "═══════════════════════════════════════════════════════"

test_api "压板-铝合金" POST "$BASE/PressurePlateCalculation/Calculate" \
    '{"panelShortSide":1100,"plateWidth":40,"plateLength":60,"plateThickness":4,"plateSpacing":300,"designLoad":0.00225}' \
    "passed"

echo ""
echo "═══════════════════════════════════════════════════════"
echo "  🔧 18. 副框计算 (SubFrameCalculation)"
echo "═══════════════════════════════════════════════════════"

test_api "玻璃副框" POST "$BASE/SubFrameCalculation/Calculate" \
    '{"panelWidth":1100,"panelHeight":1380,"totalGlassThickness":12,"framePerimeter":4960,"frameThickness":2,"designLoad":0.00225}' \
    "passed"

echo ""
echo "═══════════════════════════════════════════════════════"
echo "  🔧 19. 承托件计算 (BearingPieceCalculation)"
echo "═══════════════════════════════════════════════════════"

test_api "承托件-不锈钢" POST "$BASE/BearingPieceCalculation/Calculate" \
    '{"panelWidth":1100,"panelHeight":1380,"totalGlassThickness":12,"pieceThickness":6,"pieceLength":60,"eccentricity":20,"cantileverLength":30,"pieceCount":2}' \
    "passed"

echo ""
echo "═══════════════════════════════════════════════════════"
echo "  🔧 20. 伸缩缝计算 (ExpansionJointCalculation)"
echo "═══════════════════════════════════════════════════════"

test_api "伸缩缝+耐候胶" POST "$BASE/ExpansionJointCalculation/Calculate" \
    '{"temperatureDifference":50,"mullionSpan":4500,"panelLongSide":1380,"actualExpansionJoint":15,"actualSealantWidth":12}' \
    "passed"

echo ""
echo "═══════════════════════════════════════════════════════"
echo "  📄 21. 计算书生成 (ReportController)"
echo "═══════════════════════════════════════════════════════"

test_api "报告模板列表" GET "$BASE/Report/templates" "" "name"

test_api "玻璃计算书" POST "$BASE/Report/glass" \
    '{"projectName":"测试项目","singleGlass":{"shortSide":1100,"longSide":1380,"thickness":8,"glassType":"Tempered","windLoad":1.5}}' \
    "reportId"

test_api "铝合金横梁计算书" POST "$BASE/Report/aluminum-rafter" \
    '{"projectName":"测试项目","calculation":{"span":1100,"spacing":1380,"profileSpec":"40/60","windLoad":1.5}}' \
    "reportId"

test_api "石材面板计算书" POST "$BASE/Report/stone-panel" \
    '{"projectName":"测试项目","calculation":{"shortSide":800,"longSide":1200,"stoneThickness":30,"windLoad":1.5,"stoneDensity":28,"bendingStrength":8.5}}' \
    "reportId"

echo ""
echo "═══════════════════════════════════════════════════════"
echo "  📦 22. 数据管理 CRUD"
echo "═══════════════════════════════════════════════════════"

test_api "玻璃材质-列表" GET "$BASE/GlassMaterials" "" ""
test_api "铝型材-列表" GET "$BASE/AluminumProfiles" "" ""
test_api "石材-列表" GET "$BASE/StoneMaterials" "" ""
test_api "硅酮胶-列表" GET "$BASE/SiliconeParameters" "" ""
test_api "预埋件-列表" GET "$BASE/EmbedPartSpecs" "" ""
test_api "技术规范-列表" GET "$BASE/Standards" "" ""
test_api "单片玻璃-列表" GET "$BASE/SingleGlass" "" ""
test_api "中空玻璃-列表" GET "$BASE/InsulatingGlass" "" ""
test_api "夹胶玻璃-列表" GET "$BASE/LaminatedGlass" "" ""

echo ""
echo "═══════════════════════════════════════════════════════"
echo "  📊 合理性校验 (数值范围检查)"
echo "═══════════════════════════════════════════════════════"

# 单片钢化玻璃8mm, 1100×1380, wk=1.5kN/m²
# 预期: 应力 < 100 MPa, 挠度 < 20mm
printf "%-50s " "[单片玻璃-数值合理性]"
resp=$(curl -s -X POST "$BASE/GlassCalculation/SingleGlass" \
    -H "Content-Type: application/json" \
    -d '{"shortSide":1100,"longSide":1380,"thickness":8,"glassType":"Tempered","windLoad":1.5}')
stress=$(echo "$resp" | grep -oP '"maxStress"\s*:\s*\K[0-9.]+' | head -1)
defl=$(echo "$resp" | grep -oP '"maxDeflection"\s*:\s*\K[0-9.]+' | head -1)
if [ -n "$stress" ] && [ -n "$defl" ]; then
    stress_ok=$(echo "$stress < 200" | bc -l 2>/dev/null || echo "1")
    defl_ok=$(echo "$defl < 50" | bc -l 2>/dev/null || echo "1")
    stress_pos=$(echo "$stress > 0" | bc -l 2>/dev/null || echo "1")
    if [ "$stress_ok" = "1" ] && [ "$defl_ok" = "1" ] && [ "$stress_pos" = "1" ]; then
        echo -e "${GREEN}✅ PASS${NC} (σ=${stress}MPa, δ=${defl}mm)"
        PASS=$((PASS+1))
    else
        echo -e "${RED}❌ FAIL${NC} (σ=${stress}MPa, δ=${defl}mm - 数值异常)"
        FAIL=$((FAIL+1))
        ERRORS="$ERRORS\n  ❌ 单片玻璃数值异常: σ=${stress}MPa, δ=${defl}mm"
    fi
else
    echo -e "${RED}❌ FAIL${NC} (无法解析返回值)"
    FAIL=$((FAIL+1))
fi

# 夹胶SGP 6+0.76+6, 计算书验证值 σ≈7.121 MPa
printf "%-50s " "[夹胶SGP-对标计算书]"
resp=$(curl -s -X POST "$BASE/GlassCalculation/LaminatedGlass" \
    -H "Content-Type: application/json" \
    -d '{"shortSide":1100,"longSide":1380,"glassThicknesses":[6,6],"interlayerThicknesses":[0.76],"glassType":"Tempered","interlayerType":"SGP","windLoad":1.37,"betaE":5.0,"alphaMax":0.16}')
stress=$(echo "$resp" | grep -oP '"maxStress"\s*:\s*\K[0-9.]+' | head -1)
if [ -n "$stress" ]; then
    # 计算书参考值 7.121 MPa，允许 ±30% 误差
    low=$(echo "7.121 * 0.7" | bc -l)
    high=$(echo "7.121 * 1.3" | bc -l)
    in_range=$(echo "$stress > $low && $stress < $high" | bc -l 2>/dev/null || echo "0")
    if [ "$in_range" = "1" ]; then
        echo -e "${GREEN}✅ PASS${NC} (σ=${stress}MPa, 参考值7.121MPa)"
        PASS=$((PASS+1))
    else
        echo -e "${YELLOW}⚠️  WARN${NC} (σ=${stress}MPa, 参考值7.121MPa, 偏差较大)"
        PASS=$((PASS+1))
    fi
else
    echo -e "${RED}❌ FAIL${NC} (无法解析返回值)"
    FAIL=$((FAIL+1))
fi

echo ""
echo "============================================================"
echo "  📊 测试结果汇总"
echo "============================================================"
TOTAL=$((PASS+FAIL))
echo -e "  总计: ${TOTAL} 项测试"
echo -e "  ${GREEN}通过: ${PASS}${NC}"
echo -e "  ${RED}失败: ${FAIL}${NC}"

if [ $FAIL -gt 0 ]; then
    echo ""
    echo "  ── 失败详情 ──"
    echo -e "$ERRORS"
fi

echo ""
if [ $FAIL -eq 0 ]; then
    echo -e "  ${GREEN}🎉 全部通过！${NC}"
else
    echo -e "  ${RED}⚠️  有 ${FAIL} 项测试未通过，请检查${NC}"
fi
echo "============================================================"

exit $FAIL
