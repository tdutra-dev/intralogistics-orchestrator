# ADR 0004: brokered eventing

## Status
Accepted

## Context
Order and machine events need durable, decoupled propagation across services.

## Decision
Use RabbitMQ with MassTransit-inspired contracts and outbox patterns as the backbone for system integration.

## Consequences
This enables decoupled processing and replayability but requires careful idempotency and retry handling.
