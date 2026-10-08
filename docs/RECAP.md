# Final recap

This project is a scaffold for a Smart Intralogistics Orchestrator intended to manage customer orders, pallet lifecycles, machine coordination, dynamic routing, logs, and telemetry in a modern warehouse environment. It addresses the need to turn orders into validated pallet flows while supporting resilience, fault handling, and operational observability.

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

This is an implementation base aligned with the specification, not a fully production-grade deployment of every stretch requirement. The domain model, API skeleton, orchestration and queue concepts, tests, and infrastructure scaffolding are in place.
