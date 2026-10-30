using Endatix.Core.Abstractions;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Audience.Endpoints.Audience.Imports;
using Endatix.Modules.Audience.Features.Imports;
using FastEndpoints;
using FluentValidation.Results;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Modules.Audience.Tests.Endpoints.Audience.Imports;

public sealed class ListRejectionsTests
{
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly ListRejections _endpoint;

    public ListRejectionsTests()
    {
        var tenant = Substitute.For<ITenantContext>();
        tenant.TenantId.Returns(1);
        _endpoint = Factory.Create<ListRejections>(_mediator, tenant);
    }

    [Fact]
    public async Task ExecuteAsync_ImportWithRejections_ReturnsThePageWithEachRowsCells()
    {
        // Arrange
        Paged<ImportRejectedRowDto> page = new(2, 1, 3, 3, [new ImportRejectedRowDto(3, "Identifier is required.", ["", "Sofia"])]);
        _mediator.Send(Arg.Any<ListImportRejectionsQuery>(), Arg.Any<CancellationToken>()).Returns(Result.Success(page));

        // Act
        var response = await _endpoint.ExecuteAsync(
            new ListAudienceImportRejectionsRequest { FormId = 10, ImportId = 20, Page = 2, PageSize = 1 },
            TestContext.Current.CancellationToken);

        // Assert
        var ok = response.Result as Ok<Paged<AudienceImportRejectedRowResponse>>;
        ok.Should().NotBeNull();
        ok!.Value!.TotalRecords.Should().Be(3);
        var row = ok.Value.Items.Should().ContainSingle().Subject;
        row.RowNumber.Should().Be(3);
        row.Reason.Should().Be("Identifier is required.");
        row.Cells.Should().Equal("", "Sofia");
        await _mediator.Received(1).Send(
            new ListImportRejectionsQuery(1, 10, 20, 2, 1), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_MissingImport_ReturnsNotFound()
    {
        // Arrange
        _mediator.Send(Arg.Any<ListImportRejectionsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<Paged<ImportRejectedRowDto>>.NotFound("Import not found."));

        // Act
        var response = await _endpoint.ExecuteAsync(
            new ListAudienceImportRejectionsRequest { FormId = 10, ImportId = 20 }, TestContext.Current.CancellationToken);

        // Assert
        var problem = response.Result as ProblemHttpResult;
        problem.Should().NotBeNull();
        problem!.StatusCode.Should().Be(404);
    }

    [Theory]
    [InlineData(null, null, true)]
    [InlineData(1, 5_000, true)]
    [InlineData(0, null, false)]
    [InlineData(null, 5_001, false)]
    public void Validate_Paging_StaysWithinTheAudiencePageLimits(int? page, int? pageSize, bool valid)
    {
        // Act
        var result = new ListAudienceImportRejectionsValidator().Validate(
            new ListAudienceImportRejectionsRequest { FormId = 10, ImportId = 20, Page = page, PageSize = pageSize });

        // Assert
        result.IsValid.Should().Be(valid);
    }
}
