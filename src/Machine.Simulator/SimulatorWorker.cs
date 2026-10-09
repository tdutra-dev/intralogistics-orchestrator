using System.Text.Json;
using Contracts;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;

public sealed class SimulatorWorker : BackgroundService
{
    private static readonly Guid[] MachineIds =
    [
        Guid.Parse("10000000-0000-0000-0000-000000000001"),
        Guid.Parse("10000000-0000-0000-0000-000000000002")
    ];

    private readonly IConfiguration _configuration;
    private readonly ILogger<SimulatorWorker> _logger;

    public SimulatorWorker(IConfiguration configuration, ILogger<SimulatorWorker> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var mqttFactory = new MqttFactory();
        using var mqttClient = mqttFactory.CreateMqttClient();

        var options = new MqttClientOptionsBuilder()
            .WithTcpServer(_configuration.GetValue<string>("Mqtt:Host") ?? "localhost", _configuration.GetValue<int?>("Mqtt:Port") ?? 1883)
            .Build();

        await mqttClient.ConnectAsync(options, stoppingToken);

        var sequenceByMachine = MachineIds.ToDictionary(x => x, _ => 0L);

        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (var machineId in MachineIds)
            {
                sequenceByMachine[machineId]++;
                var sequence = sequenceByMachine[machineId];
                var faultCode = sequence % 10 == 0 ? "OVERHEAT" : null;
                var status = faultCode is null ? (sequence % 2 == 0 ? "Busy" : "Idle") : "Faulted";
                var telemetry = new MachineTelemetryReceived(
                    machineId,
                    sequence,
                    status,
                    20m + (sequence % 15),
                    sequence * 12,
                    faultCode,
                    DateTime.UtcNow);

                var payload = JsonSerializer.SerializeToUtf8Bytes(telemetry);
                var topic = $"machines/{machineId}/telemetry";

                var message = new MqttApplicationMessageBuilder()
                    .WithTopic(topic)
                    .WithPayload(payload)
                    .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                    .Build();

                await mqttClient.PublishAsync(message, stoppingToken);
                _logger.LogInformation("Published telemetry {Sequence} for machine {MachineId} to {Topic}", sequence, machineId, topic);
            }

            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }
}