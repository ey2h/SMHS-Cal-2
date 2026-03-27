#!/bin/bash
# Test Aluminum Calculations with Real Database Profile Values
# Note: Database stores Ix in cm^4, API expects mm^4 (multiply by 10000)

BASE_URL="http://127.0.0.1:5188"
ALUM_URL="$BASE_URL/api/AluminumCalculation"

echo "=============================================="
echo "  Aluminum API + Database Profile Tests"
echo "  (60/80系列 profile from database)"
echo "=============================================="
echo ""

# 60/80系列 profile values from database (converting cm^4 to mm^4)
# SectionArea = 4.52 cm² = 452 mm²
# MomentOfInertiaX = 2656350 cm^4 = 26563500000 mm^4
# SectionModulusX = 40905 cm³ = 40905000 mm³
# ShearArea = 26247 cm³ = 26247000 mm³

echo "=== Test 1: Mullion with 60/80系列 (Normal Wind) ==="
curl -s -X POST "$ALUM_URL/Mullion" \
  -H "Content-Type: application/json" \
  -d '{
    "Height": 3000,
    "ShortSpan": 450,
    "LongSpan": 2550,
    "Spacing": 1000,
    "ProfileSpec": "60/80系列",
    "WindLoad": 0.0008,
    "GravityLoad": 0.0005,
    "SeismicGravityLoad": 0.0005,
    "AluminumGrade": "6063-T5",
    "Ix": 26563500000,
    "Wnx": 40905000,
    "An": 452,
    "WebThickness": 5,
    "Sx": 26247000,
    "Gamma": 1.0
  }' | python3 -c "import sys,json; d=json.load(sys.stdin); print(f\"  Passed: {d['passed']}, Deflection: {d['maxDeflection']:.2f}mm (limit: {d['deflectionLimit']:.2f}mm)\"); print(f\"  Strength: {d['strengthCheck']['passed']}, σ={d['strengthCheck']['actualValue']:.2f}MPa (limit: {d['strengthCheck']['allowableValue']}MPa)\")"

echo ""
echo "=== Test 2: Rafter with 60/80系列 (Normal Wind) ==="
curl -s -X POST "$ALUM_URL/Rafter" \
  -H "Content-Type: application/json" \
  -d '{
    "Span": 1200,
    "Spacing": 1500,
    "ProfileSpec": "60/80系列",
    "WindLoad": 0.0008,
    "GravityLoad": 0.0004,
    "AluminumGrade": "6063-T5",
    "Ix": 26563500000,
    "Iy": 12365000000,
    "Wnx": 40905000,
    "Wny": 36620000,
    "An": 452,
    "Sx": 26247000,
    "Sy": 35460000,
    "Tx": 5,
    "Ty": 3,
    "Gamma": 1.0
  }' | python3 -c "import sys,json; d=json.load(sys.stdin); print(f\"  Passed: {d['passed']}, Deflection: {d['maxDeflection']:.2f}mm (limit: {d['deflectionLimit']:.2f}mm)\"); print(f\"  Strength: {d['strengthCheck']['passed']}, σ={d['strengthCheck']['actualValue']:.2f}MPa (limit: {d['strengthCheck']['allowableValue']}MPa)\")"

echo ""
echo "=== Test 3: Rafter with 60/80系列 (High Wind) ==="
curl -s -X POST "$ALUM_URL/Rafter" \
  -H "Content-Type: application/json" \
  -d '{
    "Span": 1200,
    "Spacing": 1500,
    "ProfileSpec": "60/80系列",
    "WindLoad": 0.002,
    "GravityLoad": 0.0004,
    "AluminumGrade": "6063-T5",
    "Ix": 26563500000,
    "Iy": 12365000000,
    "Wnx": 40905000,
    "Wny": 36620000,
    "An": 452,
    "Sx": 26247000,
    "Sy": 35460000,
    "Tx": 5,
    "Ty": 3,
    "Gamma": 1.0
  }' | python3 -c "import sys,json; d=json.load(sys.stdin); print(f\"  Passed: {d['passed']}, Deflection: {d['maxDeflection']:.2f}mm (limit: {d['deflectionLimit']:.2f}mm)\"); print(f\"  Strength: {d['strengthCheck']['passed']}, σ={d['strengthCheck']['actualValue']:.2f}MPa (limit: {d['strengthCheck']['allowableValue']}MPa)\")"

echo ""
echo "=== Test 4: Mullion with 60/80系列 (Large Span) ==="
curl -s -X POST "$ALUM_URL/Mullion" \
  -H "Content-Type: application/json" \
  -d '{
    "Height": 4500,
    "ShortSpan": 675,
    "LongSpan": 3825,
    "Spacing": 1200,
    "ProfileSpec": "60/80系列",
    "WindLoad": 0.0012,
    "GravityLoad": 0.0005,
    "SeismicGravityLoad": 0.0005,
    "AluminumGrade": "6063-T5",
    "Ix": 26563500000,
    "Wnx": 40905000,
    "An": 452,
    "WebThickness": 5,
    "Sx": 26247000,
    "Gamma": 1.0
  }' | python3 -c "import sys,json; d=json.load(sys.stdin); print(f\"  Passed: {d['passed']}, Deflection: {d['maxDeflection']:.2f}mm (limit: {d['deflectionLimit']:.2f}mm)\"); print(f\"  Strength: {d['strengthCheck']['passed']}, σ={d['strengthCheck']['actualValue']:.2f}MPa (limit: {d['strengthCheck']['allowableValue']}MPa)\")"

echo ""
echo "=============================================="
echo "  Database Profile Tests Complete"
echo "=============================================="
