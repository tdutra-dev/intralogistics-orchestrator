# ADR 0003: actor-based orchestration

## Status
Accepted

## Context
Pallet coordination, machine queues, fault handling, and re-routes are naturally concurrent and event-driven.

## Decision
Use Akka.NET for orchestration and queue management patterns as the specification requires.

## Consequences
The system gains concurrency and supervision semantics, at the cost of somewhat higher operational complexity.
