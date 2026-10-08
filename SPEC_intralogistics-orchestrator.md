# SPEC — Smart Intralogistics Orchestrator (.NET)

> Portfolio project. Goal: demonstrate modern .NET (Core) senior-level engineering in an industrial / intralogistics context: DDD, event-driven microservices, actor model, rules + routing, edge-to-cloud, T-SQL, testing, CI/CD.
> Inspired by the problem space of warehouse orchestration software. Not a clone of any commercial product; do not use third-party brand names in code or docs.

---

## 0. Instructions for Copilot (read first)

1. Work **phase by phase** (section 6). Do not start a phase before the previous one builds, passes tests, and its acceptance criteria are met.
2. One logical change per commit, Conventional Commits (`feat:`, `fix:`, `test:`, `docs:`, `chore:`). Feature branches + PRs into `develop`, `main` is release-only (Git-flow).
3. Every public behavior gets a test in the same commit. No untested domain logic.
4. Do not invent requirements. If something is ambiguous, pick the simplest option, document it in `docs/adr/`.
5. Keep dependencies minimal and **license-safe** (section 2). Do not add MediatR, FluentAssertions >= 8, or MassTransit >= 9.
6. Target: `dotnet build -warnaserror` clean, `dotnet test` green, `docker compose up` working at the end of every phase.
7. **Commits and push, mandatory for every phase:**
   - Commit messages must be descriptive: a short imperative subject line (<= 72 chars) plus a body explaining *what* changed and *why* (e.g. `feat(orchestrator): add PalletActor lifecycle FSM` + body listing transitions, retry/timeout behavior, tests added).
   - At the end of each phase: make sure the work is fully committed, then **push** the branch to the remote (`git push`) and open/merge the PR into `develop`. Never leave a completed phase unpushed.
   - Tag each completed phase (`phase-1-done` ... `phase-6-done`) and push the tags.
8. **The final project must be tested and working.** Before declaring the project done you must actually run and report the results of: `dotnet build -warnaserror`, the full `dotnet test` suite (unit, integration, actor, architecture), a clean `docker compose up --build` from a fresh clone, and the end-to-end demo script. Fix every failure; do not skip or disable tests to make things pass. If something cannot be verified, say so explicitly instead of claiming it works.
9. **Final recap and README (mandatory last step, see section 8).** Do not finish without it.

---

## 1. Domain

**Palletizing & dispatch flow.** Customer orders are turned into pallets, which pass through machines and are routed through a warehouse graph to a dispatch dock.

Entities / aggregates (DDD):
- `CustomerOrder` (aggregate root): `OrderId`, `Lines[] (Sku, Quantity, WeightKg)`, `Priority (Low|Normal|Urgent)`, `Destination (Region, Dock preference)`, `HazardousFlag`, status.
- `Pallet` (aggregate root): `PalletId`, `OrderId`, `Weight`, `State`, `AssignedRoute`, `Timeline[]`.
- `Machine`: `MachineId`, `Type (Palletizer | Wrapper | Labeler | Shuttle)`, `Status (Idle|Busy|Faulted|Maintenance)`.
- `WarehouseGraph`: nodes (stations, junctions, docks), weighted edges (travel time), edges/nodes can be **blocked** (fault, maintenance).

Pallet lifecycle (state machine):
`Created -> Building -> Built -> Wrapped -> Labeled -> Routed -> InTransit -> Dispatched`
with side states `Faulted` (machine failure; retry or reroute) and `Cancelled`.
Illegal transitions must throw a domain exception and be covered by tests.

Domain events (integration events are versioned records):
`OrderReceived`, `PalletCreated`, `PalletBuilt`, `PalletWrapped`, `PalletLabeled`, `PalletRouted`, `PalletDispatched`, `MachineFaulted`, `MachineRecovered`, `TelemetryReceived`.

---

## 2. Tech stack (fixed)

| Area | Choice |
|---|---|
| Runtime | **.NET 10 (LTS)**, latest C#; fallback .NET 8 LTS if tooling blocks. Nullable on, `TreatWarningsAsErrors`, central package management (`Directory.Packages.props`), `.editorconfig`, Roslyn analyzers |
| API | ASP.NET Core Web API (Minimal APIs + endpoint groups), ProblemDetails, API versioning, OpenAPI |
| Persistence | EF Core + **SQL Server 2022** (container); migrations; **Dapper** for read-side queries; hand-written **T-SQL** views/stored procedures for reporting |
| Messaging | **RabbitMQ** + **MassTransit 8.x** (Apache-2.0). Transactional outbox (EF), idempotent consumers (inbox), retry + dead-letter |
| Actor model | **Akka.NET** (Akka.Hosting, Akka.TestKit.Xunit2) |
| Rules engine | **NRules** |
| State machine | **Stateless** (pallet lifecycle) |
| Edge | **MQTTnet** + Mosquitto container, **SQLite** local store-and-forward |
| Observability | Serilog (structured), OpenTelemetry (traces+metrics), Prometheus + Grafana, ASP.NET health checks |
| Tests | xUnit, **Shouldly**, NSubstitute, **Testcontainers** (SQL Server, RabbitMQ), Akka.TestKit, NetArchTest, FsCheck (property tests for routing) |
| CI/CD | GitHub Actions (build, test, coverage, docker build, optional push to GHCR), multi-stage Dockerfiles, `docker-compose.yml` |
| Architecture | Clean/Hexagonal + CQRS **without MediatR** (own minimal `ICommandHandler<T>` / `IQueryHandler<T,R>` + decorators for logging/validation/transaction) |

---

## 3. Deployables (4 services + infra)

1. **`Orders.Api`** — REST API, order intake and queries, CQRS, EF Core write model, Dapper read model, outbox publisher.
2. **`Orchestrator.Host`** — .NET Worker Service hosting the Akka.NET actor system; consumes events, drives pallet/machine state, applies rules, computes routes, publishes events. Runnable as **Windows Service** (`UseWindowsService`) and **systemd** unit (ship both sample unit files in `deploy/`).
3. **`Machine.Simulator`** — Worker Service simulating PLCs (palletizer, wrapper, labeler, shuttle): publishes telemetry over MQTT, accepts commands, injects random faults (configurable rate).
4. **`Edge.Gateway`** — Worker Service: subscribes to MQTT, normalizes telemetry, stores locally in SQLite when the broker is unreachable (store-and-forward), forwards to RabbitMQ when back online, preserving order and avoiding duplicates.

Infra in compose: SQL Server, RabbitMQ (+ management UI), Mosquitto, Prometheus, Grafana, (optional) Seq.

---

## 4. Architecture rules

- Solution layout:
  ```
  /src
    /BuildingBlocks        (Result type, domain base classes, CQRS abstractions, outbox helpers)
    /Orders.Domain  /Orders.Application  /Orders.Infrastructure  /Orders.Api
    /Orchestrator.Domain  /Orchestrator.Application  /Orchestrator.Host
    /Contracts             (versioned integration events, no logic)
    /Machine.Simulator
    /Edge.Gateway
  /tests
    /*.UnitTests  /*.IntegrationTests  /Architecture.Tests  /Orchestrator.ActorTests
  /deploy  (docker, compose, systemd, windows-service scripts, grafana dashboards)
  /docs    (README, architecture.md with Mermaid C4-style diagrams, requirements.md, /adr)
  ```
- Domain layer has **zero** infrastructure dependencies (enforced by NetArchTest).
- No shared database between services. Services talk only through contracts/messages.
- Async all the way, `CancellationToken` everywhere, no `.Result`/`.Wait()`.
- Result pattern for expected failures; exceptions only for invariant violations.
- Configuration via Options pattern + environment variables; no secrets in repo.

---

## 5. Functional requirements

### 5.1 Orders.Api
- `POST /api/v1/orders` — create order (validation: at least one line, weight > 0, valid priority). Idempotency via `Idempotency-Key` header.
- `GET /api/v1/orders/{id}` — order with its pallets and timeline.
- `GET /api/v1/orders?status=&priority=&from=&to=&page=` — paged, filtered (Dapper).
- `GET /api/v1/pallets/{id}/timeline`.
- `POST /api/v1/orders/{id}/cancel`.
- `GET /api/v1/reports/throughput` — backed by a **T-SQL stored procedure** (pallets/hour, avg lead time per priority, fault rate per machine) using CTEs and window functions.
- Health: `/health/live`, `/health/ready`.

### 5.2 Orchestrator (Akka.NET)
Actor hierarchy:
- `OrderManagerActor` (one): receives `OrderReceived`, splits order into pallets (bin-packing by weight limit, first-fit decreasing), spawns child `PalletActor`s.
- `PalletActor` (one per pallet): drives lifecycle FSM (Stateless inside or Akka `FSM`), requests machine slots, handles timeouts/retries.
- `MachineActor` (one per machine): bounded FIFO queue with priority (Urgent first), tracks status, handles `MachineFaulted`/`MachineRecovered`, applies backpressure (rejects/defers when queue full).
- `RouterActor`: computes routes, reacts to graph changes (blocked nodes) by **re-routing in-flight pallets**.
- Supervision strategy: restart on transient errors with backoff, stop + dead-letter on poison messages. Document it in an ADR.
- Optional (stretch): Akka.Persistence (SQL) so pallet actors recover state after restart.

### 5.3 Rules (NRules)
Rules in code, versioned, unit-tested in isolation:
- Dock selection: by region, hazardous flag (hazardous -> dedicated dock), weight, priority.
- Priority escalation: order waiting > N minutes -> escalate one level.
- Machine assignment: avoid machines with recent fault rate above threshold.
- Rules must be testable without the actor system (pure facts in -> decisions out).

### 5.4 Routing
- Weighted directed `WarehouseGraph` loaded from JSON config.
- **Dijkstra** (and optionally A*) in the domain layer; blocked nodes/edges excluded; returns route + cost, or `NoRoute` result.
- Property-based tests (FsCheck): route cost is never worse than any alternative found by brute force on small random graphs; blocked nodes never appear in route.
- Benchmark with BenchmarkDotNet on a 10k-node graph (results in `docs/`).

### 5.5 Edge
- Simulator publishes telemetry (`machines/{id}/telemetry`, QoS 1) every second: status, temperature, cycle count, fault code.
- Gateway: batch + compress, store-and-forward when RabbitMQ is down (test by stopping the container), de-duplicate by `(MachineId, Sequence)`, expose metrics (buffered messages, forward lag).

---

## 6. Phases and acceptance criteria

**Phase 1 — Foundation + Orders.Api**
Solution scaffold, building blocks, domain model (Order, Pallet FSM), EF Core + migrations, CQRS handlers, endpoints 5.1, T-SQL report, Dockerfile, compose with SQL Server.
*Accept:* all endpoints work via compose; domain unit tests cover every FSM transition (valid + invalid); architecture tests green; integration tests on Testcontainers SQL Server.

**Phase 2 — Messaging**
MassTransit + RabbitMQ, contracts project, transactional outbox in Orders.Api, idempotent consumers, retry/redelivery policy, dead-letter queue.
*Accept:* order creation publishes `OrderReceived` exactly once even if the API crashes between commit and publish (test it); duplicate delivery does not duplicate effects.

**Phase 3 — Orchestrator (Akka.NET)**
Actor hierarchy 5.2, pallet splitting, machine queues with priority and backpressure, supervision, event publishing back to Orders.Api (updates status/timeline).
*Accept:* end-to-end: POST order -> pallets progress to `Dispatched` using in-process fake machines; Akka.TestKit tests for queue ordering, fault handling, restart supervision.

**Phase 4 — Rules + Routing**
NRules rule set 5.3, Dijkstra 5.4, re-routing on blocked node.
*Accept:* rule unit tests; property-based routing tests; demo scenario in README: block a junction mid-flight and show the pallet re-routed (logs + timeline).

**Phase 5 — Edge**
Simulator + Mosquitto + Edge.Gateway 5.5, replace in-process fake machines with simulator-driven ones.
*Accept:* kill RabbitMQ for 60s -> no telemetry lost, order preserved, forwarded after recovery (integration test or scripted demo documented in README).

**Phase 6 — Quality, ops, docs**
OpenTelemetry traces across API -> broker -> orchestrator -> gateway (single trace visible), Prometheus metrics + Grafana dashboard JSON (throughput, queue depth per machine, fault rate, edge buffer size), GitHub Actions pipeline (build, test, coverage >= 80% on domain/application, docker build), Windows Service + systemd samples, README with architecture diagram (Mermaid), ADRs (>= 6), `docs/requirements.md` (user stories + acceptance criteria written like a spec a product owner would sign), `docs/runbook.md`.
*Accept:* fresh clone -> `docker compose up --build` -> run `scripts/demo.ps1|sh` -> dashboard shows data. CI green badge in README.

**Stretch (only if time, mark clearly as optional in README)**
- SignalR live dashboard (small React or Blazor page).
- Elsa Workflows (or a BPMN-style flow) for exception handling approval (e.g., faulted pallet requires operator decision).
- MassTransit Kafka rider for telemetry stream instead of RabbitMQ.
- Deploy to AWS/Azure (Terraform) with managed SQL + broker.

---

## 7. Non-functional requirements

- Throughput target (documented, measured): >= 500 orders/min on a laptop with simulated machines; p95 API latency < 100 ms for reads.
- Resilience: any single container restart must not lose accepted orders.
- Security basics: input validation, no secrets in repo, JWT bearer auth on API (simple issuer config, dev key), HTTPS in non-dev.
- Docs and code in English.

---

## 8. Definition of Done (whole project)

### 8.1 Final verification (before the recap)
Run and show real output for: build, all tests, `docker compose up --build` from a fresh clone, demo script end-to-end (order created -> pallet dispatched, a machine fault -> re-route, broker outage -> no telemetry lost). Everything must pass. Report test counts and coverage.

### 8.2 Final recap (write it in the chat AND in `docs/RECAP.md`)
- **What it is:** one-paragraph description of the system and the problem it solves.
- **What it does:** the main flows, step by step (order intake -> pallet splitting -> machines -> rules -> routing -> dispatch; edge telemetry path; fault/re-route path).
- **Technologies:** table of every technology used.
- **Why each decision:** for each major technology/pattern (.NET 10, SQL Server + EF Core + Dapper, CQRS without MediatR, RabbitMQ/MassTransit + outbox/inbox, Akka.NET, NRules, Stateless, Dijkstra, MQTT + store-and-forward, OpenTelemetry/Prometheus/Grafana, Testcontainers, Docker/CI) state the reason, the alternative considered, and the trade-off.
- **Limitations and next steps**, stated honestly.

### 8.3 README.md (must explain the whole flow)
- What the project is and why it exists
- Architecture diagram (Mermaid) and a **sequence diagram of the full flow** (order -> dispatch, including fault and re-route)
- Description of each service and each actor
- Tech stack with the reason for each choice (link to ADRs)
- How to run (prerequisites, `docker compose up --build`, demo script), how to run tests, ports/URLs
- Demo scenario with screenshots/GIF
- Project structure, testing strategy, CI/CD, observability
- Honest "limitations" section

### 8.4 Checklist
- [ ] Public GitHub repo, clean history, descriptive commits, every phase pushed and tagged, PR-based workflow visible
- [ ] Final verification (8.1) done with real results
- [ ] CI green, coverage badge
- [ ] README (8.3) and `docs/RECAP.md` (8.2) complete
- [ ] >= 6 ADRs (e.g., actor model vs plain workers, outbox, no MediatR, SQL Server choice, store-and-forward, supervision strategy)
- [ ] Benchmarks and measured throughput numbers recorded in `docs/`

---

## 9. CV entry (fill real numbers only after they are measured)

**Smart Intralogistics Orchestrator** — Personal project, 2026 · github.com/tdutra-dev/intralogistics-orchestrator
*.NET 10, C#, ASP.NET Core, EF Core, SQL Server (T-SQL), Akka.NET, RabbitMQ/MassTransit, MQTT, NRules, Docker, GitHub Actions, OpenTelemetry*
- Designed and built an event-driven palletizing and dispatch platform on .NET 10 with DDD/CQRS, transactional outbox and idempotent consumers on RabbitMQ.
- Modeled machines and pallets as Akka.NET actors with supervision, priority queues and backpressure; automatic re-routing of in-flight pallets via Dijkstra on a dynamic warehouse graph.
- Implemented routing and escalation policies with NRules; covered with unit, property-based (FsCheck) and Testcontainers integration tests (<N> tests, <X>% domain coverage).
- Built an edge gateway (MQTT -> store-and-forward SQLite -> broker) with zero telemetry loss during broker outages; deployable as Windows Service or systemd unit.
- Delivered CI/CD (GitHub Actions), OpenTelemetry tracing, Prometheus/Grafana dashboards, and ADR-based documentation.

Skills line to add: `.NET 10 / ASP.NET Core · EF Core · Akka.NET · MassTransit · T-SQL · NRules · MQTT · Testcontainers`
