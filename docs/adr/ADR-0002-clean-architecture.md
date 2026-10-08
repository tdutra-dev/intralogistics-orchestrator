# ADR 0002: clean architecture

## Status
Accepted

## Context
The project mixes domain logic, interfaces, and infrastructure responsibilities if not structured carefully.

## Decision
Keep the domain free of infrastructure concerns and separate it from application and adapter layers.

## Consequences
The project is easier to test and evolve as bus, database, and runtime components change.
