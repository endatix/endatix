using System.Text.Json;
using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Specifications;
using Endatix.Infrastructure.Features.BackgroundJobs;
using Endatix.Infrastructure.Features.Outbox;
using Endatix.Outbox.Engine;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.Infrastructure.Tests.Features.Outbox;

/// <summary>A job of an outbox work reads a row an earlier version wrote as this version's event, as the relay published it.</summary>
public sealed class LegacyOutboxWorkJobTests
{
    private const long TenantA = 11;
    private const long MessageId = 701;

    private readonly IRepository<OutboxMessage> _outbox = Substitute.For<IRepository<OutboxMessage>>();
    private readonly List<OutboxMessage> _messages = [];

    public LegacyOutboxWorkJobTests() =>
        _outbox
            .FirstOrDefaultAsync(Arg.Any<OutboxMessageByIdForTenantSpec>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<OutboxMessageByIdForTenantSpec>().Evaluate(_messages).FirstOrDefault());

    [Fact]
    public async Task ExecuteAsync_LegacyCollectionStatusChangedRow_ReachesASubmissionClosedWorkAsSubmissionClosed()
    {
        // Arrange — a row an earlier version wrote for a screen-out, re-read by the job of a submission.closed work.
        _messages.Add(new OutboxMessage(
            "submission.collection_status_changed",
            $$"""{"tenantId":"{{TenantA}}","submissionId":"34","collectionStatus":"screen_out","previousCollectionStatus":"in_progress"}""",
            TenantA,
            DateTime.UtcNow,
            1) { Id = MessageId });
        using var provider = Build();

        // Act
        using var scope = provider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredKeyedService<IBackgroundJobHandler>(ClosedWorkStub.JobType);
        var job = new BackgroundJobContext(1, ClosedWorkStub.JobType, TenantA,
            BackgroundJobPayloadSerializer.Serialize(new OutboxMessageJob<ClosedWorkStub>(MessageId)), 1);
        var result = await handler.ExecuteAsync(job, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var work = scope.ServiceProvider.GetRequiredService<ClosedWorkStub>();
        work.EventType.Should().Be("submission.closed");
        using var payload = JsonDocument.Parse(work.Payload!);
        var status = payload.RootElement.GetProperty("collectionStatus");
        status.GetProperty("code").GetString().Should().Be("screen_out");
        status.GetProperty("phase").GetString().Should().Be("closed");
    }

    private ServiceProvider Build()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMetrics();
        services.AddSingleton(_outbox);
        services.AddSingleton(Substitute.For<IBackgroundJobQueue>());
        services.AddOutboxWork<ClosedWorkStub>(OutboxWorkTests.Defaults);
        return services.BuildServiceProvider();
    }

    /// <summary>A host's work on submission.closed, which keeps the message its job read.</summary>
    internal sealed class ClosedWorkStub : IOutboxWork<ClosedWorkStub>
    {
        public static string JobType => "TestClosedWork";

        public static IReadOnlyCollection<string> HandledEventTypes { get; } = ["submission.closed"];

        public string? EventType { get; private set; }

        public string? Payload { get; private set; }

        public OutboxWork Read(IOutboxMessage message)
        {
            EventType = message.EventType;
            Payload = message.Payload;
            return OutboxWork.None;
        }
    }
}
