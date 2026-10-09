using Contracts;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;

namespace Edge.GatewayTests;

public sealed class TelemetryForwarderTests : IDisposable
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"edge-gateway-tests-{Guid.NewGuid():N}.db");

    [Fact]
    public async Task Should_buffer_when_publish_fails_and_flush_after_recovery()
    {
        var store = new TelemetryBufferStore($"Data Source={_databasePath}");
        await store.InitializeAsync(CancellationToken.None);

        var publisher = new FakeTelemetryPublisher { ShouldFail = true };
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var forwarder = new TelemetryForwarder(store, publisher, loggerFactory.CreateLogger<TelemetryForwarder>());
        var telemetry = CreateTelemetry(sequence: 1);

        await forwarder.StageAndForwardAsync(telemetry, CancellationToken.None);

        (await store.CountBufferedAsync(CancellationToken.None)).ShouldBe(1);
        publisher.Published.Count.ShouldBe(0);

        publisher.ShouldFail = false;
        await forwarder.FlushPendingAsync(CancellationToken.None);

        (await store.CountBufferedAsync(CancellationToken.None)).ShouldBe(0);
        publisher.Published.Count.ShouldBe(1);
        publisher.Published.Single().Sequence.ShouldBe(1);
    }

    [Fact]
    public async Task Should_ignore_duplicate_machine_sequence()
    {
        var store = new TelemetryBufferStore($"Data Source={_databasePath}");
        await store.InitializeAsync(CancellationToken.None);

        var publisher = new FakeTelemetryPublisher();
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var forwarder = new TelemetryForwarder(store, publisher, loggerFactory.CreateLogger<TelemetryForwarder>());
        var telemetry = CreateTelemetry(sequence: 7);

        await forwarder.StageAndForwardAsync(telemetry, CancellationToken.None);
        await forwarder.StageAndForwardAsync(telemetry, CancellationToken.None);

        publisher.Published.Count.ShouldBe(1);
        (await store.CountBufferedAsync(CancellationToken.None)).ShouldBe(0);
    }

    public void Dispose()
    {
        if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
    }

    private static MachineTelemetryReceived CreateTelemetry(long sequence)
    {
        return new MachineTelemetryReceived(
            Guid.Parse("20000000-0000-0000-0000-000000000001"),
            sequence,
            "Busy",
            32m,
            sequence * 10,
            null,
            DateTime.UtcNow);
    }

    private sealed class FakeTelemetryPublisher : ITelemetryPublisher
    {
        public bool ShouldFail { get; set; }
        public List<MachineTelemetryReceived> Published { get; } = new();

        public Task PublishAsync(MachineTelemetryReceived telemetry, CancellationToken cancellationToken)
        {
            if (ShouldFail)
            {
                throw new InvalidOperationException("RabbitMQ unavailable.");
            }

            Published.Add(telemetry);
            return Task.CompletedTask;
        }
    }
}