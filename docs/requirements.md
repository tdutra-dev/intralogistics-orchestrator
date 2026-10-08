# Product requirements

## User stories

1. As a warehouse planner, I want to create customer orders so that the system can split them into pallets and route them for dispatch.
2. As an operations lead, I want pallet state transitions to be validated so that invalid lifecycle states are rejected early.
3. As a systems engineer, I want infrastructure services to be deployable via Docker so the project can be run consistently.
4. As a platform owner, I want rules and routing logic to be isolated, testable, and documented for change control.

## Acceptance criteria

- The domain rejects invalid pallet transitions.
- An order can be created with valid lines and data.
- The API exposes health and order intake endpoints.
- The project includes Docker infrastructure for broker and database services.
- Architecture and actor queue tests are included.
