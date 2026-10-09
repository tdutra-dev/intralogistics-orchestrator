using System.IO.Compression;
using System.Text.Json;
using Contracts;
using MassTransit;
using Microsoft.Data.Sqlite;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;

public interface ITelemetryPublisher
{
    Task PublishAsync(MachineTelemetryReceived telemetry, CancellationToken cancellationToken);
}

public sealed class MassTransitTelemetryPublisher : ITelemetryPublisher
{
    private readonly IPublishEndpoint _publishEndpoint;

    public MassTransitTelemetryPublisher(IPublishEndpoint publishEndpoint)
    {
        _publishEndpoint = publishEndpoint;
    }

    public Task PublishAsync(MachineTelemetryReceived telemetry, CancellationToken cancellationToken)
    {
        return _publishEndpoint.Publish(telemetry, cancellationToken);
    }
}

public sealed record BufferedTelemetry(Guid MachineId, long Sequence, byte[] Payload, DateTime BufferedAtUtc);

public sealed class TelemetryBufferStore
{
    private readonly string _connectionString;

    public TelemetryBufferStore(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = @"
CREATE TABLE IF NOT EXISTS TelemetryBuffer (
    MachineId TEXT NOT NULL,
    Sequence INTEGER NOT NULL,
    Payload BLOB NOT NULL,
    BufferedAtUtc TEXT NOT NULL,
    ForwardedAtUtc TEXT NULL,
    PRIMARY KEY (MachineId, Sequence)
);
CREATE INDEX IF NOT EXISTS IX_TelemetryBuffer_ForwardedAtUtc ON TelemetryBuffer (ForwardedAtUtc);";

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<bool> StageAsync(MachineTelemetryReceived telemetry, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = @"
INSERT OR IGNORE INTO TelemetryBuffer (MachineId, Sequence, Payload, BufferedAtUtc, ForwardedAtUtc)
VALUES ($machineId, $sequence, $payload, $bufferedAtUtc, NULL);";
        command.Parameters.AddWithValue("$machineId", telemetry.MachineId.ToString());
        command.Parameters.AddWithValue("$sequence", telemetry.Sequence);
        command.Parameters.Add("$payload", SqliteType.Blob).Value = Compress(JsonSerializer.SerializeToUtf8Bytes(telemetry));
        command.Parameters.AddWithValue("$bufferedAtUtc", telemetry.UtcTimestamp);

        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    public async Task<IReadOnlyList<MachineTelemetryReceived>> GetPendingAsync(int batchSize, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = @"
SELECT MachineId, Sequence, Payload
FROM TelemetryBuffer
WHERE ForwardedAtUtc IS NULL
ORDER BY BufferedAtUtc
LIMIT $limit;";
        command.Parameters.AddWithValue("$limit", batchSize);

        var result = new List<MachineTelemetryReceived>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var payload = (byte[])reader[2];
            var telemetry = JsonSerializer.Deserialize<MachineTelemetryReceived>(Decompress(payload));
            if (telemetry is not null)
            {
                result.Add(telemetry);
            }
        }

        return result;
    }

    public async Task MarkForwardedAsync(Guid machineId, long sequence, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = @"
UPDATE TelemetryBuffer
SET ForwardedAtUtc = $forwardedAtUtc
WHERE MachineId = $machineId AND Sequence = $sequence;";
        command.Parameters.AddWithValue("$forwardedAtUtc", DateTime.UtcNow);
        command.Parameters.AddWithValue("$machineId", machineId.ToString());
        command.Parameters.AddWithValue("$sequence", sequence);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<int> CountBufferedAsync(CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM TelemetryBuffer WHERE ForwardedAtUtc IS NULL;";
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static byte[] Compress(byte[] data)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            gzip.Write(data, 0, data.Length);
        }

        return output.ToArray();
    }

    private static byte[] Decompress(byte[] data)
    {
        using var input = new MemoryStream(data);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);
        return output.ToArray();
    }
}

public sealed class TelemetryForwarder
{
    private readonly TelemetryBufferStore _bufferStore;
    private readonly ITelemetryPublisher _publisher;
    private readonly ILogger<TelemetryForwarder> _logger;

    public TelemetryForwarder(TelemetryBufferStore bufferStore, ITelemetryPublisher publisher, ILogger<TelemetryForwarder> logger)
    {
        _bufferStore = bufferStore;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task StageAndForwardAsync(MachineTelemetryReceived telemetry, CancellationToken cancellationToken)
    {
        var inserted = await _bufferStore.StageAsync(telemetry, cancellationToken);
        if (!inserted)
        {
            _logger.LogInformation("Skipping duplicate telemetry {MachineId}/{Sequence}", telemetry.MachineId, telemetry.Sequence);
            return;
        }

        await FlushPendingAsync(cancellationToken);
    }

    public async Task FlushPendingAsync(CancellationToken cancellationToken)
    {
        var pending = await _bufferStore.GetPendingAsync(50, cancellationToken);
        foreach (var telemetry in pending)
        {
            try
            {
                await _publisher.PublishAsync(telemetry, cancellationToken);
                await _bufferStore.MarkForwardedAsync(telemetry.MachineId, telemetry.Sequence, cancellationToken);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Unable to forward telemetry {MachineId}/{Sequence}; message remains buffered.", telemetry.MachineId, telemetry.Sequence);
                break;
            }
        }
    }
}

public sealed class EdgeGatewayWorker : BackgroundService
{
    private readonly IConfiguration _configuration;
    private readonly TelemetryBufferStore _bufferStore;
    private readonly TelemetryForwarder _forwarder;
    private readonly ILogger<EdgeGatewayWorker> _logger;

    public EdgeGatewayWorker(
        IConfiguration configuration,
        TelemetryBufferStore bufferStore,
        TelemetryForwarder forwarder,
        ILogger<EdgeGatewayWorker> logger)
    {
        _configuration = configuration;
        _bufferStore = bufferStore;
        _forwarder = forwarder;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _bufferStore.InitializeAsync(stoppingToken);

        var mqttFactory = new MqttFactory();
        using var mqttClient = mqttFactory.CreateMqttClient();

        mqttClient.ApplicationMessageReceivedAsync += async args =>
        {
            try
            {
                var payload = args.ApplicationMessage.PayloadSegment.ToArray();
                var telemetry = JsonSerializer.Deserialize<MachineTelemetryReceived>(payload);
                if (telemetry is not null)
                {
                    await _forwarder.StageAndForwardAsync(telemetry, stoppingToken);
                }
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to process telemetry message from topic {Topic}", args.ApplicationMessage.Topic);
            }
        };

        var mqttOptions = new MqttClientOptionsBuilder()
            .WithTcpServer(_configuration.GetValue<string>("Mqtt:Host") ?? "localhost", _configuration.GetValue<int?>("Mqtt:Port") ?? 1883)
            .Build();

        await mqttClient.ConnectAsync(mqttOptions, stoppingToken);
        await mqttClient.SubscribeAsync(new MqttTopicFilterBuilder()
            .WithTopic("machines/+/telemetry")
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .Build(), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            await _forwarder.FlushPendingAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }
    }
}