using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Data.Locking;
using Endatix.Modules.Reporting.Features.SearchCopy;
using Endatix.Modules.Reporting.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using FormSchemaEntity = Endatix.Modules.Reporting.Domain.FormSchema;

namespace Endatix.Modules.Reporting.Tests.Features.SearchCopy;

/// <summary>
/// The refusals of <see cref="AddSearchKeyHandler"/>. An accepted key rebuilds the rows in SQL, which
/// <c>UserSearchKeysIntegrationTests</c> covers on both databases.
/// </summary>
public sealed class AddSearchKeyHandlerTests
{
    private readonly SearchKeyHandlerTestContext _context = new();
    private readonly AddSearchKeyHandler _sut;

    public AddSearchKeyHandlerTests()
    {
        SearchCopyRebuilder rebuilder = new(
            Substitute.For<IReportingDbContext>(),
            _context.Schemas,
            NullLogger<SearchCopyRebuilder>.Instance);
        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(new DateTimeOffset(2026, 10, 16, 0, 0, 0, TimeSpan.Zero));
        SearchCopyRebuildRequests rebuildRequests = new(_context.OutboxMessages, dateTimeProvider);
        _sut = new AddSearchKeyHandler(
            _context.UserSearchKeys,
            new FormSearchCopy(dateTimeProvider, rebuilder, rebuildRequests),
            _context.Rows);
    }

    [Fact]
    public async Task Handle_WithoutTenant_ReturnsUnauthorized()
    {
        // Act
        var result = await _sut.Handle(Command("q001", tenantId: 0), CancellationToken.None);

        // Assert
        result.Status.Should().Be(ResultStatus.Unauthorized);
    }

    [Fact]
    public async Task Handle_FormOfAnotherTenant_ReturnsNotFoundWithoutLocking()
    {
        // Arrange
        _context.GivenForm(tenantId: SampleData.TENANT_ID + 1);

        // Act
        var result = await _sut.Handle(Command("q001"), CancellationToken.None);

        // Assert
        result.Status.Should().Be(ResultStatus.NotFound);
        await _context.Schemas.DidNotReceiveWithAnyArgs().LockAndGetByFormIdAsync(default, default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Handle_FormWithoutSchema_ReturnsNotFound()
    {
        // Arrange
        _context.GivenForm(SampleData.TENANT_ID);

        // Act
        var result = await _sut.Handle(Command("q001"), CancellationToken.None);

        // Assert
        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task Handle_ColumnWithoutAnswerFilter_ReturnsInvalidAndSavesNothing()
    {
        // Arrange
        _context.GivenForm(SampleData.TENANT_ID);
        _context.GivenSchema(SearchKeyHandlerTestContext.TextQuestions(30));

        // Act
        var result = await _sut.Handle(Command("age"), CancellationToken.None);

        // Assert
        result.Status.Should().Be(ResultStatus.Invalid);
        result.ValidationErrors.Should().ContainSingle().Which.ErrorMessage.Should().Be(FormSearchCopy.NotFilterableMessage);
        await _context.Schemas.DidNotReceiveWithAnyArgs().SaveAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Handle_FifteenthUserKey_ReturnsInvalidNamingTheCapAndSavesNothing()
    {
        // Arrange
        _context.GivenForm(SampleData.TENANT_ID);
        var schema = _context.GivenSchema(SearchKeyHandlerTestContext.TextQuestions(80));
        GivenUserKeys(schema, count: FormSearchKeys.MaxUserKeys);

        // Act
        var result = await _sut.Handle(Command("q080"), CancellationToken.None);

        // Assert
        result.Status.Should().Be(ResultStatus.Invalid);
        result.ValidationErrors.Should().ContainSingle().Which.ErrorMessage.Should().Be(FormSearchKeys.CapMessage);
        await _context.Schemas.DidNotReceiveWithAnyArgs().SaveAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Handle_FreeSlot_SavesTheKeyRequestsARebuildAndReturnsTheKeyPending()
    {
        // Arrange
        _context.GivenForm(SampleData.TENANT_ID);
        var schema = _context.GivenSchema(SearchKeyHandlerTestContext.TextQuestions(51));
        _context.Rows.OldestSearchKeysRevisionAsync(SampleData.TENANT_ID, SearchKeyHandlerTestContext.FormId, Arg.Any<CancellationToken>())
            .Returns(schema.SearchKeysRevision);

        // Act
        var result = await _sut.Handle(Command("q051"), CancellationToken.None);

        // Assert
        result.Value.Keys.Should().ContainSingle(key => key.Key == "q051").Which.Pending.Should().BeTrue();
        await _context.Schemas.Received(1).SaveAsync(schema, Arg.Any<CancellationToken>());
        await _context.OutboxMessages.Received(1).AddAsync(
            Arg.Is<OutboxMessage>(message => message.EventType == SearchCopyRebuildRequests.EventTypeName &&
                                             message.TenantId == SampleData.TENANT_ID &&
                                             message.Payload.Contains($"\"formId\":\"{SearchKeyHandlerTestContext.FormId}\"")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SchemaLockedTooLong_ReturnsUnavailable()
    {
        // Arrange
        _context.GivenForm(SampleData.TENANT_ID);
        _context.Schemas.LockAndGetByFormIdAsync(SampleData.TENANT_ID, SearchKeyHandlerTestContext.FormId, Arg.Any<CancellationToken>())
            .Returns<FormSchemaEntity?>(_ => throw new TransactionLockTimeoutException(
                new TransactionLockRequest(TransactionLockScopes.ReportingFormSchema, "form")));

        // Act
        var result = await _sut.Handle(Command("q001"), CancellationToken.None);

        // Assert
        result.Status.Should().Be(ResultStatus.Unavailable);
    }

    private static AddSearchKeyCommand Command(string key, long tenantId = SampleData.TENANT_ID) =>
        new(SearchKeyHandlerTestContext.Target(key, tenantId));

    // Columns 51 to 64 of the form, added by a person.
    private static void GivenUserKeys(FormSchemaEntity schema, int count)
    {
        var keys = Enumerable.Range(51, count).Aggregate(
            FormSearchKeys.Of(schema),
            (current, index) => current.AddUser(new SearchableColumn($"q{index:D3}", "text"), DateTime.UtcNow).Value);
        schema.UpdateSearchKeys(keys.ToJson(), keys.Revision);
    }
}
