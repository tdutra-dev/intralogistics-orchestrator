using Akka.Actor;
using Akka.Hosting;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddHostedService<OrchestratorBackgroundService>();

builder.Services.AddAkka("orchestrator-system", (akkaBuilder, provider) =>
{
    akkaBuilder.WithActors((system, registry) =>
    {
        var orderManager = system.ActorOf(Props.Create<OrderManagerActor>(), "order-manager");
        registry.Register<OrderManagerActor>(orderManager);
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

public sealed class OrderManagerActor : ReceiveActor
{
    public OrderManagerActor()
    {
        ReceiveAny(_ => { });
    }
}
