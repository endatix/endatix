using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Reporting.Features.SearchCopy;
using Endatix.Modules.Reporting.Features.SubmissionGrid;

namespace Endatix.Modules.Reporting.Tests.Features.SearchCopy;

public sealed class RemoveSearchKeyHandlerTests
{
    private readonly SearchKeyHandlerTestContext _context = new();
    private readonly RemoveSearchKeyHandler _sut;

    public RemoveSearchKeyHandlerTests()
    {
        _sut = new RemoveSearchKeyHandler(_context.UserSearchKeys, _context.Rows);
    }

    [Fact]
    public async Task Handle_UserKey_SavesTheKeysWithoutItAndReturnsThem()
    {
        // Arrange
        _context.GivenForm(SampleData.TENANT_ID);
        var schema = _context.GivenSchema(SearchKeyHandlerTestContext.TextQuestions(51));
        var withUserKey = FormSearchKeys.Of(schema).AddUser(new SearchableColumn("q051", SubmissionGridFilterTypes.Text), DateTime.UtcNow).Value;
        schema.UpdateSearchKeys(withUserKey.ToJson(), withUserKey.Revision);

        // Act
        var result = await _sut.Handle(Command("q051"), CancellationToken.None);

        // Assert
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Keys.Should().NotContain(key => key.Key == "q051");
        result.Value.Slots.UserUsed.Should().Be(0);
        await _context.Schemas.Received(1).SaveAsync(schema, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AutomaticKey_ReturnsInvalidAndSavesNothing()
    {
        // Arrange
        _context.GivenForm(SampleData.TENANT_ID);
        _context.GivenSchema(SearchKeyHandlerTestContext.TextQuestions(3));

        // Act
        var result = await _sut.Handle(Command("q001"), CancellationToken.None);

        // Assert
        result.Status.Should().Be(ResultStatus.Invalid);
        await _context.Schemas.DidNotReceiveWithAnyArgs().SaveAsync(default!, default);
    }

    [Fact]
    public async Task Handle_KeyNotSearchable_ReturnsNotFound()
    {
        // Arrange
        _context.GivenForm(SampleData.TENANT_ID);
        _context.GivenSchema(SearchKeyHandlerTestContext.TextQuestions(3));

        // Act
        var result = await _sut.Handle(Command("age"), CancellationToken.None);

        // Assert
        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task Handle_FormOfAnotherTenant_ReturnsNotFound()
    {
        // Arrange
        _context.GivenForm(tenantId: SampleData.TENANT_ID + 1);

        // Act
        var result = await _sut.Handle(Command("q001"), CancellationToken.None);

        // Assert
        result.Status.Should().Be(ResultStatus.NotFound);
        await _context.Schemas.DidNotReceiveWithAnyArgs().SaveAsync(default!, default);
    }

    private static RemoveSearchKeyCommand Command(string key) => new(SearchKeyHandlerTestContext.Target(key));
}
