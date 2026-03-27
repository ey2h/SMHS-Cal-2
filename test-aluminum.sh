#!/bin/bash
# Test Aluminum (Mullion/Column) and Rafter/Beam calculations

BASE_URL="http://127.0.0.1:5188"

echo "=============================================="
echo "  Aluminum Calculation API Tests"
echo "=============================================="
echo ""

# Test 1: Mullion (立柱) calculation - basic
echo "=== Test 1: Mullion (立柱) - Basic ==="
curl -s -X POST "$BASE_URL/api/Aluminum/Mullion" \
  -H "Content-Type: application/json" \
  -d '{
    "Height": 3600,
    "ShortSpan": 600,
    "LongSpan": 3000,
    "Spacing": 1200,
    "ProfileSpec": "150系列",
    "WindLoad": 0.0028,
    "GravityLoad": 0.0005,
    "SeismicGravityLoad": 0.0005,
    "AluminumGrade": "6063-T5",
    "Ix": 2656350,
    "Wnx": 40905,
    "An": 1261.5,
    "WebThickness": 6,
    "Sx": 26247,
    "Gamma": 1.0
  }' | jq '{passed, maxDeflection, deflectionLimit, strengthCheck, deflectionCheck}'

# Test 2: Mullion with different profile
echo ""
echo "=== Test 2: Mullion (立柱) - Stronger Profile ==="
curl -s -X POST "$BASE_URL/api/Aluminum/Mullion" \
  -H "Content-Type: application/json" \
  -d '{
    "Height": 3600,
    "ShortSpan": 600,
    "LongSpan": 3000,
    "Spacing": 1500,
    "ProfileSpec": "180系列",
    "WindLoad": 0.0035,
    "GravityLoad": 0.0006,
    "SeismicGravityLoad": 0.0006,
    "AluminumGrade": "6063-T6",
    "Ix": 4500000,
    "Wnx": 55000,
    "An": 1500,
    "WebThickness": 8,
    "Sx": 35000,
    "Gamma": 1.0
  }' | jq '{passed, maxDeflection, deflectionLimit, strengthCheck, deflectionCheck}'

# Test 3: Mullion - cantilever model (高风压区)
echo ""
echo "=== Test 3: Mullion (立柱) - High Wind Pressure ==="
curl -s -X POST "$BASE_URL/api/Aluminum/Mullion" \
  -H "Content-Type: application/json" \
  -d '{
    "Height": 4200,
    "ShortSpan": 700,
    "LongSpan": 3500,
    "Spacing": 1200,
    "ProfileSpec": "200系列",
    "WindLoad": 0.005,
    "GravityLoad": 0.0007,
    "SeismicGravityLoad": 0.0007,
    "AluminumGrade": "6063A-T6",
    "Ix": 6800000,
    "Wnx": 72000,
    "An": 1800,
    "WebThickness": 10,
    "Sx": 45000,
    "Gamma": 1.0
  }' | jq '{passed, strengthCheck, deflectionCheck, shearCheck}'

# Test 4: Rafter (横梁) calculation - basic
echo ""
echo "=== Test 4: Rafter (横梁) - Basic ==="
curl -s -X POST "$BASE_URL/api/Aluminum/Rafter" \
  -H "Content-Type: application/json" \
  -d '{
    "Span": 1200,
    "Spacing": 1500,
    "ProfileSpec": "50系列",
    "WindLoad": 0.0028,
    "GravityLoad": 0.0004,
    "AluminumGrade": "6063-T5",
    "Ix": 115810,
    "Iy": 123650,
    "Wnx": 3662,
    "Wny": 5763,
    "An": 449.3,
    "Sx": 2994,
    "Sy": 3546,
    "Tx": 5,
    "Ty": 3,
    "Gamma": 1.0,
    "BetaE": 5.0,
    "AlphaMax": 0.16
  }' | jq '{passed, strengthCheck, deflectionCheck, shearCheck}'

# Test 5: Rafter with larger span
echo ""
echo "=== Test 5: Rafter (横梁) - Large Span ==="
curl -s -X POST "$BASE_URL/api/Aluminum/Rafter" \
  -H "Content-Type: application/json" \
  -d '{
    "Span": 1800,
    "Spacing": 1500,
    "ProfileSpec": "60系列",
    "WindLoad": 0.003,
    "GravityLoad": 0.0005,
    "GravityHeight": 750,
    "AluminumGrade": "6063-T6",
    "Ix": 250000,
    "Iy": 280000,
    "Wnx": 5500,
    "Wny": 7200,
    "An": 650,
    "Sx": 4500,
    "Sy": 5200,
    "Tx": 6,
    "Ty": 4,
    "Gamma": 1.0
  }' | jq '{passed, strengthCheck, deflectionCheck, shearCheck}'

# Test 6: Aluminum Plate calculation
echo ""
echo "=== Test 6: Aluminum Plate - Basic ==="
curl -s -X POST "$BASE_URL/api/Aluminum/Plate" \
  -H "Content-Type: application/json" \
  -d '{
    "ShortSide": 600,
    "LongSide": 1200,
    "Thickness": 3,
    "WindLoad": 2.8,
    "MaterialGrade": "3003-H24"
  }' | jq '{passed, strengthCheck, deflectionCheck}'

# Test 7: Aluminum Plate - thicker
echo ""
echo "=== Test 7: Aluminum Plate - Thicker ==="
curl -s -X POST "$BASE_URL/api/Aluminum/Plate" \
  -H "Content-Type: application/json" \
  -d '{
    "ShortSide": 800,
    "LongSide": 1500,
    "Thickness": 4,
    "WindLoad": 3.5,
    "MaterialGrade": "5052-H32"
  }' | jq '{passed, strengthCheck, deflectionCheck}'

echo ""
echo "=============================================="
echo "  Aluminum Calculation Tests Complete"
echo "=============================================="
