using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Specifications;
using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Features.BackgroundJobs;
using Endatix.Modules.Reporting.Features.Outbox;
using Endatix.Modules.Reporting.Features.SearchCopy;
using Endatix.Modules.Reporting.Persistence;
using Microsoft.Extensions.Logging.Abstractions;

namespace Endatix.Modules.Reporting.Tests.Features.BackgroundJobs;

/// <summary>
/// The job that runs a search copy rebuild request when the outbox delivers to the job queue. The rebuild reads the
/// form's schema first, so a rebuild that ran shows as that read.
/// </summary>
public sealed class RebuildSearchCopyJobHandlerTests
{
    private const long TenantA = 11;
    private const long TenantB = 12;
    private const long FormId = 100;
    private const long MessageId = 700;

    private readonly IRepository<OutboxMessage> _outbox = Substitute.For<IRepository<OutboxMessage>>();
    private readonly IFormSchemaRepository _schemas = Substitute.For<IFormSchemaRepository>();
    private readonly List<OutboxMessage> _messages = [];

    public RebuildSearchCopyJobHandlerTests()
    {
        _outbox
            .FirstOrDefaultAsync(Arg.Any<OutboxMessageByIdForTenantSpec>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<OutboxMessageByIdForTenantSpec>().Evaluate(_messages).FirstOrDefault());
    }

    [Fact]
    public async Task ExecuteAsync_RequestOfTheJobsTenant_RebuildsTheForm()
    {
        // Arrange
        Seed($$"""{"tenantId":"{{TenantA}}","formId":"{{FormId}}"}""", TenantA);

        // Act
        var result = await CreateHandler().ExecuteAsync(Job(TenantA), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _schemas.Received(1).GetByFormIdAsync(TenantA, FormId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_UnreadableRequest_FailsWithoutRebuilding()
    {
        // Arrange
        Seed($$"""{"tenantId":"{{TenantA}}"}""", TenantA);

        // Act
        var result = await CreateHandler().ExecuteAsync(Job(TenantA), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        await _schemas.DidNotReceiveWithAnyArgs().GetByFormIdAsync(default, default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ExecuteAsync_RequestOfAnotherTenant_FailsWithoutRebuilding()
    {
        // Arrange
        Seed($$"""{"tenantId":"{{TenantA}}","formId":"{{FormId}}"}""", TenantA);

        // Act
        var result = await CreateHandler().ExecuteAsync(Job(TenantB), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        await _schemas.DidNotReceiveWithAnyArgs().GetByFormIdAsync(default, default, TestContext.Current.CancellationToken);
    }

    private RebuildSearchCopyJobHandler CreateHandler() =>
        new(
            _outbox,
            new RebuildSearchCopyOutboxHandler(
                new SearchCopyRebuilder(Substitute.For<IReportingDbContext>(), _schemas, NullLogger<SearchCopyRebuilder>.Instance)),
            NullLogger<RebuildSearchCopyJobHandler>.Instance);

    private void Seed(string payload, long tenantId) =>
        _messages.Add(new OutboxMessage(SearchCopyRebuildRequests.EventTypeName, payload, tenantId, DateTime.UtcNow, 1) { Id = MessageId });

    private static BackgroundJobContext Job(long tenantId) =>
        new(1, ReportingRebuildSearchCopyPayload.JobType, tenantId,
            BackgroundJobPayloadSerializer.Serialize(new ReportingRebuildSearchCopyPayload(MessageId)), 1);
}
