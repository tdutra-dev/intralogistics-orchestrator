using Microsoft.Extensions.Hosting;
using MassTransit;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton(sp =>
    new TelemetryBufferStore(builder.Configuration.GetConnectionString("TelemetryBuffer") ?? "Data Source=edge-gateway-buffer.db"));
builder.Services.AddSingleton<ITelemetryPublisher, MassTransitTelemetryPublisher>();
builder.Services.AddSingleton<TelemetryForwarder>();

builder.Services.AddMassTransit(x =>
{
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration.GetValue<string>("Messaging:RabbitMqHost") ?? "localhost", "/", host =>
        {
            host.Username(builder.Configuration.GetValue<string>("Messaging:RabbitMqUsername") ?? "guest");
            host.Password(builder.Configuration.GetValue<string>("Messaging:RabbitMqPassword") ?? "guest");
        });
    });
});

builder.Services.AddHostedService<EdgeGatewayWorker>();

var host = builder.Build();
host.Run();
