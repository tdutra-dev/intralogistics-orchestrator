using Akka.Actor;
using Akka.Hosting;
using Contracts;
using Orchestrator.Domain;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddHostedService<OrchestratorBackgroundService>();
builder.Services.AddSingleton<OrderMessageInbox>();

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<OrderReceivedConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration.GetValue<string>("Messaging:RabbitMqHost") ?? "rabbitmq", "/", host =>
        {
            host.Username(builder.Configuration.GetValue<string>("Messaging:RabbitMqUsername") ?? "guest");
            host.Password(builder.Configuration.GetValue<string>("Messaging:RabbitMqPassword") ?? "guest");
        });

        cfg.ReceiveEndpoint("orchestrator-order-received", endpoint =>
        {
            endpoint.UseMessageRetry(retry => retry.Interval(3, TimeSpan.FromSeconds(1)));
            endpoint.Consumer<OrderReceivedConsumer>(context);
        });
    });
});

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
    private readonly IActorRef _machineQueueActor;

    public OrderManagerActor() : this(10)
    {
    }

    public OrderManagerActor(int maxQueueDepth)
    {
        _machineQueueActor = Context.ActorOf(Props.Create(() => new MachineQueueActor(maxQueueDepth)), "machine-queue");

        Receive<OrderReceivedMessage>(message => _machineQueueActor.Forward(new QueueOrderMessage(message.OrderId)));
        Receive<MachineFaultMessage>(message => _machineQueueActor.Forward(message));
        Receive<MachineRecoveryMessage>(message => _machineQueueActor.Forward(message));
        Receive<RouteBlockedMessage>(message => _machineQueueActor.Forward(message));
        Receive<QueryQueueDepthMessage>(message => _machineQueueActor.Forward(message));
    }

    protected override SupervisorStrategy SupervisorStrategy() => new OneForOneStrategy(
        ex => ex is InvalidOperationException ? Directive.Restart : Directive.Escalate);
}

public sealed class MachineQueueActor : ReceiveActor
{
    private readonly MachineQueue _queue;

    public MachineQueueActor(int maxQueueDepth = 10)
    {
        _queue = new MachineQueue(maxQueueDepth);

        Receive<QueueOrderMessage>(message =>
        {
            if (_queue.TryEnqueue(message.OrderId))
            {
                Sender.Tell(new OrderQueuedMessage(message.OrderId, _queue.Count));
            }
            else
            {
                Sender.Tell(new OrderBackpressuredMessage(message.OrderId, _queue.Count));
            }
        });

        Receive<MachineFaultMessage>(_ => throw new InvalidOperationException("Machine queue faulted."));

        Receive<MachineRecoveryMessage>(_ =>
        {
            if (_queue.Dequeue() is Guid palletId)
            {
                Sender.Tell(new OrderQueuedMessage(palletId, _queue.Count));
            }
            else
            {
                Sender.Tell(new QueueDepthMessage(_queue.Count));
            }
        });

        Receive<RouteBlockedMessage>(_ => Sender.Tell(new QueueDepthMessage(_queue.Count)));
        Receive<QueryQueueDepthMessage>(_ => Sender.Tell(new QueueDepthMessage(_queue.Count)));
    }
}

public sealed class OrderReceivedConsumer : IConsumer<OrderReceived>
{
    private readonly OrderMessageInbox _inbox;
    private readonly ILogger<OrderReceivedConsumer> _logger;

    public OrderReceivedConsumer(OrderMessageInbox inbox, ILogger<OrderReceivedConsumer> logger)
    {
        _inbox = inbox;
        _logger = logger;
    }

    public Task Consume(ConsumeContext<OrderReceived> context)
    {
        if (!_inbox.TryRegister(context.Message.EventId))
        {
            _logger.LogInformation("Ignoring duplicate order event {EventId} for order {OrderId}", context.Message.EventId, context.Message.OrderId);
            return Task.CompletedTask;
        }

        _logger.LogInformation("Received order {OrderId} for customer {CustomerId}", context.Message.OrderId, context.Message.CustomerId);
        return Task.CompletedTask;
    }
}
