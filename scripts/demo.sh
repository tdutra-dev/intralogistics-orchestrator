#!/usr/bin/env bash
set -euo pipefail

echo "Creating sample order..."
curl -sS -X POST http://localhost:5000/api/v1/orders \
  -H 'Content-Type: application/json' \
  -d '{
    "customerId": "C-1001",
    "destinationRegion": "north",
    "dockPreference": "dock-7",
    "lines": [{"sku":"SKU-01","quantity":10,"weightKg":6.5}]
  }'

echo

echo "Demo flow complete."
