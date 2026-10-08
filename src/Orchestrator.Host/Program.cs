using Akka.Hosting;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddHostedService<OrchestratorBackgroundService>();

builder.Services.AddAkka("orchestrator-system", (akkaBuilder, provider) =>
{
    akkaBuilder.WithActors((system, registry) =>
    {
        registry.Register<OrderManagerActor>("order-manager");
    });
});

var host = builder.Build();
host.Run();

public sealed class OrchestratorBackgroundService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}

public sealed class OrderManagerActor
{
    public string Name => "OrderManagerActor";
}
