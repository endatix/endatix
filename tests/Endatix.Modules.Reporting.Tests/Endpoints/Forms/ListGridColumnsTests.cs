using Ardalis.Specification;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Endpoints.Forms;
using Endatix.Modules.Reporting.Features.SubmissionGrid;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Modules.Reporting.Tests.Endpoints.Forms;

public sealed class ListGridColumnsTests
{
    private const long FormId = 100;

    private readonly IRepository<Form> _formsRepository = Substitute.For<IRepository<Form>>();
    private readonly IFlattenedSubmissionRepository _flattenedSubmissionRepository = Substitute.For<IFlattenedSubmissionRepository>();
    private readonly ListGridColumns _endpoint;

    public ListGridColumnsTests()
    {
        ITenantContext tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(SampleData.TENANT_ID);
        _flattenedSubmissionRepository
            .CountByStatusAsync(SampleData.TENANT_ID, FormId, Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, int>());
        ListSubmissionGridColumns readModel = new(
            Substitute.For<IFormSchemaRepository>(),
            _flattenedSubmissionRepository,
            _formsRepository,
            Substitute.For<IRepository<Submission>>());
        _endpoint = Factory.Create<ListGridColumns>(readModel, tenantContext);
    }

    [Fact]
    public async Task ExecuteAsync_FormFound_ReturnsColumns()
    {
        // Arrange
        _formsRepository
            .SingleOrDefaultAsync(Arg.Any<ISingleResultSpecification<Form>>(), Arg.Any<CancellationToken>())
            .Returns(Form.Create(new FormCreateArgs(TenantId: SampleData.TENANT_ID, Name: "Grid form")));

        // Act
        var response = await _endpoint.ExecuteAsync(new ListGridColumnsRequest { FormId = FormId }, CancellationToken.None);

        // Assert
        var ok = response.Result as Ok<SubmissionGridColumns>;
        ok.Should().NotBeNull();
        ok!.Value!.FormId.Should().Be(FormId);
        ok.Value.SchemaReady.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_FormNotFound_ReturnsProblemDetails()
    {
        // Act
        var response = await _endpoint.ExecuteAsync(new ListGridColumnsRequest { FormId = FormId }, CancellationToken.None);

        // Assert
        var problemResult = response.Result as ProblemHttpResult;
        problemResult.Should().NotBeNull();
        problemResult!.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }
}
