# Runbook

## Local startup

- Start the supporting infrastructure: `docker compose up --build`
- Start the API: `dotnet run --project src/Orders.Api/Orders.Api.csproj`
- Trigger a sample request: `./scripts/demo.sh`

## Operational checks

- Health endpoint: `/health/live`
- Readiness endpoint: `/health/ready`
- RabbitMQ management UI: http://localhost:15672
- Grafana: http://localhost:3000
