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

echo "To validate edge outage recovery:"
echo "1. Keep Edge.Gateway and Machine.Simulator running"
echo "2. Stop RabbitMQ with: docker compose stop rabbitmq"
echo "3. Wait at least 10 seconds while telemetry is buffered"
echo "4. Restart RabbitMQ with: docker compose start rabbitmq"
echo "5. Inspect Edge.Gateway logs for replay without duplicate MachineId/Sequence pairs"

echo

echo "Demo flow complete."
