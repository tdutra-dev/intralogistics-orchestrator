# Runbook

## Local startup

- Start the supporting infrastructure: `docker compose up --build`
- Start the API: `dotnet run --project src/Orders.Api/Orders.Api.csproj`
- Start the orchestrator host: `dotnet run --project src/Orchestrator.Host/Orchestrator.Host.csproj`
- Start the edge gateway: `dotnet run --project src/Edge.Gateway/Edge.Gateway.csproj`
- Start the machine simulator: `dotnet run --project src/Machine.Simulator/Machine.Simulator.csproj`
- Trigger a sample request: `./scripts/demo.sh`

## Operational checks

- Health endpoint: `/health/live`
- Readiness endpoint: `/health/ready`
- Prometheus scrape endpoint: `/metrics`
- RabbitMQ management UI: http://localhost:15672
- Grafana: http://localhost:3000

## Monitoring and CI

- Grafana provisions the `Intralogistics Overview` dashboard automatically from `deploy/grafana/dashboards`
- Prometheus scrapes the Orders API directly on the internal compose network
- GitHub Actions workflow `.github/workflows/ci.yml` restores, builds, and tests the full solution on `main`, `develop`, and pull requests

## Edge outage recovery check

- Stop RabbitMQ while the simulator is running: `docker compose stop rabbitmq`
- Keep the simulator and gateway running for at least 10 seconds so telemetry accumulates in the SQLite buffer
- Restart RabbitMQ: `docker compose start rabbitmq`
- Check edge gateway logs for buffered telemetry replay and verify there are no duplicate `(MachineId, Sequence)` forwards

## Final verification

- Run `dotnet test intralogistics-orchestrator.sln` to validate the full repository
- Open Grafana and confirm the `Intralogistics Overview` dashboard shows Orders API metrics
- Run `./scripts/demo.sh` and confirm order creation, edge telemetry flow, and monitoring endpoints are all reachable locally
