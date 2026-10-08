# ADR 0001: .NET runtime choice

## Status
Accepted

## Context
The project must be portable, modern, and aligned with the specification while remaining runnable in a constrained local environment.

## Decision
Use .NET 10 as the primary execution target, aligned with the specification and current platform support.

## Consequences
The solution targets net10.0 across services and tests. If a local environment cannot run .NET 10 tooling, fallback to .NET 8 should be treated as temporary and explicitly documented.
