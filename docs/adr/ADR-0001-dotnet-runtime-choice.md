# ADR 0001: .NET runtime choice

## Status
Accepted

## Context
The project must be portable, modern, and aligned with the specification while remaining runnable in a constrained local environment.

## Decision
Use .NET 8 as the execution target for the initial scaffold because the environment does not currently provide .NET 10 tooling, and .NET 8 is the stable fallback in the specification.

## Consequences
The code remains compatible with later migration to .NET 10 with minimal breakage.
