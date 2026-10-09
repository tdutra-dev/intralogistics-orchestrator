# Final recap

This project is a completed Smart Intralogistics Orchestrator reference implementation for customer orders, pallet lifecycles, machine coordination, dynamic routing, telemetry buffering, and operational observability in a modern warehouse environment.

The main flow begins with order intake through the API, then splits and stages pallets through a stateful lifecycle, interacts with machines and routing rules, and finally dispatches the pallet. Telemetry flows from machine simulators through MQTT into a store-and-forward gateway before being forwarded to broker-based downstream systems.

## Technology summary

| Technology | Purpose |
| --- | --- |
| .NET 10 | Runtime and service implementation |
| ASP.NET Core | REST API and health endpoints |
| EF Core | Persistence model foundation |
| Dapper | Read-side query pattern |
| SQL Server | Target transactional store |
| RabbitMQ | Event messaging |
| Akka.NET | Actor-based orchestration |
| NRules | Rule-driven decisioning |
| Stateless | Lifecycle state machine |
| MQTTnet | Edge telemetry transport |
| SQLite | Store-and-forward local buffer |
| OpenTelemetry | Traces and metrics |
| Prometheus/Grafana | Monitoring |
| Testcontainers | Integration testing |
| Docker Compose | Local deployment |

## Current status

All planned phases in the implementation roadmap are complete. The solution includes local runtime infrastructure, CI validation, messaging with outbox/inbox safeguards, supervised orchestration actors, rule-driven routing, edge outage recovery, Prometheus scraping, Grafana dashboard provisioning, and a runbook for local operations.

## Delivery summary

- Orders are accepted through the API, persisted in SQL Server, and emitted through a transactional outbox.
- The orchestrator consumes order events with duplicate protection and supervised queue handling.
- Route planning uses NRules-backed decisions over a Dijkstra-based warehouse graph with blocked-node rerouting.
- Machine telemetry is generated over MQTT, buffered in SQLite when RabbitMQ is unavailable, and replayed after recovery.
- CI, metrics, dashboards, and operator guidance are included for repeatable validation and demos.
