# ADR 0005: MQTT store-and-forward

## Status
Accepted

## Context
Telemetry may be generated while downstream brokers are temporarily unavailable.

## Decision
Buffer telemetry in SQLite and forward it when the broker is reachable again.

## Consequences
This increases resilience but adds local state management and deduplication requirements.
