using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.UseCases.Forms;
using Endatix.Core.UseCases.Forms.List;

namespace Endatix.Core.Tests.UseCases.Forms.List;

public class ListFormsHandlerTests
{
    private readonly IFormsRepository _repository;
    private readonly ListFormsHandler _handler;

    public ListFormsHandlerTests()
    {
        _repository = Substitute.For<IFormsRepository>();
        _handler = new ListFormsHandler(_repository);
    }

    [Fact]
    public async Task Handle_ValidRequest_ReturnsPagedForms()
    {
        // Arrange
        var formsList = new List<FormDto>
        {
            new()
            {
                Name = "Form 1",
                Description = "Description 1",
                IsEnabled = true,
                SubmissionsCount = 3,
            },
            new()
            {
                Name = "Form 2",
                Description = "Description 2",
                IsEnabled = true,
                SubmissionsCount = 0,
            },
        };
        var request = new ListFormsQuery(1, 10, FilterExpressions: ["name:form1"]);
        _repository.CountAsync(Arg.Any<FormsListFilterSpec>(), Arg.Any<CancellationToken>())
            .Returns(2);
        _repository.ListAsync(Arg.Any<FormsWithSubmissionsCountSpec>(), Arg.Any<CancellationToken>())
            .Returns(formsList);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().NotBeNull();
        result.Value!.Items.Should().BeEquivalentTo(formsList);
        result.Value.TotalRecords.Should().Be(2);
        result.Value.Page.Should().Be(1);
        result.Value.PageSize.Should().Be(10);
    }

    [Fact]
    public async Task Handle_ValidRequest_NullFilter_ReturnsPagedForms()
    {
        // Arrange
        var formsList = new List<FormDto>
        {
            new()
            {
                Name = "Form 1",
                Description = "Description 1",
                IsEnabled = true,
                SubmissionsCount = 2,
            },
        };
        var request = new ListFormsQuery(1, 10);
        _repository.CountAsync(Arg.Any<FormsListFilterSpec>(), Arg.Any<CancellationToken>())
            .Returns(1);
        _repository.ListAsync(Arg.Any<FormsWithSubmissionsCountSpec>(), Arg.Any<CancellationToken>())
            .Returns(formsList);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().NotBeNull();
        result.Value!.Items.Should().BeEquivalentTo(formsList);
        result.Value.TotalRecords.Should().Be(1);
    }

    [Fact]
    public async Task Handle_NoMatchingForms_DoesNotQueryList()
    {
        // Arrange
        var request = new ListFormsQuery(1, 10, Search: "missing");
        _repository.CountAsync(Arg.Any<FormsListFilterSpec>(), Arg.Any<CancellationToken>())
            .Returns(0);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().NotBeNull();
        result.Value!.TotalRecords.Should().Be(0);
        result.Value.Items.Should().BeEmpty();
        await _repository.DidNotReceive().ListAsync(
            Arg.Any<FormsWithSubmissionsCountSpec>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PageBeyondTotalPages_QueriesLastPage()
    {
        // Arrange — 15 records, page size 10 → 2 pages; request page 5
        var lastPageForms = new List<FormDto>
        {
            new() { Name = "Form 15", IsEnabled = true, SubmissionsCount = 0 },
        };
        var request = new ListFormsQuery(5, 10);
        _repository.CountAsync(Arg.Any<FormsListFilterSpec>(), Arg.Any<CancellationToken>())
            .Returns(15);
        _repository.ListAsync(Arg.Any<FormsWithSubmissionsCountSpec>(), Arg.Any<CancellationToken>())
            .Returns(lastPageForms);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value!.Page.Should().Be(2);
        result.Value.TotalPages.Should().Be(2);
        result.Value.Items.Should().BeEquivalentTo(lastPageForms);
        await _repository.Received(1).ListAsync(
            Arg.Is<FormsWithSubmissionsCountSpec>(spec => spec.Skip == 10 && spec.Take == 10),
            Arg.Any<CancellationToken>());
    }
}
