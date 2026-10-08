# ADR 0006: dynamic route calculation

## Status
Accepted

## Context
Warehouse routing must handle blocked nodes and change during operation.

## Decision
Use a directed graph and Dijkstra-style shortest path calculation in the domain layer.

## Consequences
This keeps the algorithm explicit, deterministic, and testable, while allowing re-routing scenarios when graph edges or nodes are blocked.
