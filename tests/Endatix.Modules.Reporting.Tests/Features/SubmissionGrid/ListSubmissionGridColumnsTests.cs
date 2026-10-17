using Ardalis.Specification;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Reporting.Contracts;
using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Features.SubmissionGrid;
using FormSchemaEntity = Endatix.Modules.Reporting.Domain.FormSchema;

namespace Endatix.Modules.Reporting.Tests.Features.SubmissionGrid;

public sealed class ListSubmissionGridColumnsTests
{
    private const long FormId = 100;

    private const string FlatteningMapJson =
        """{"version":1,"columns":[{"key":"city","kind":"Simple","label":"city","dataType":"string"}]}""";

    private const string CodebookJson =
        """
        {"version":1,
         "questions":{"city":{"surveyJsType":"text","title":{"default":"City"},"pageName":"about"}},
         "columns":{"city":{"parentKey":"city","surveyJsType":"text","title":{"default":"City"}}}}
        """;

    private readonly IFormSchemaRepository _formSchemaRepository = Substitute.For<IFormSchemaRepository>();
    private readonly IFlattenedSubmissionRepository _flattenedSubmissionRepository = Substitute.For<IFlattenedSubmissionRepository>();
    private readonly IRepository<Form> _formsRepository = Substitute.For<IRepository<Form>>();
    private readonly IRepository<Submission> _submissionsRepository = Substitute.For<IRepository<Submission>>();
    private readonly ListSubmissionGridColumns _sut;

    public ListSubmissionGridColumnsTests()
    {
        _sut = new ListSubmissionGridColumns(
            _formSchemaRepository,
            _flattenedSubmissionRepository,
            _formsRepository,
            _submissionsRepository);
        _flattenedSubmissionRepository
            .CountByStatusAsync(SampleData.TENANT_ID, FormId, Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, int>());
    }

    [Fact]
    public async Task ExecuteAsync_WithoutTenant_ReturnsUnauthorized()
    {
        // Act
        var result = await _sut.ExecuteAsync(new SubmissionGridColumnsQuery(0, FormId, "default"), CancellationToken.None);

        // Assert
        result.Status.Should().Be(ResultStatus.Unauthorized);
        await _formsRepository.DidNotReceiveWithAnyArgs().SingleOrDefaultAsync(default(ISingleResultSpecification<Form>)!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ExecuteAsync_FormMissing_ReturnsNotFound()
    {
        // Act
        var result = await _sut.ExecuteAsync(Query(), CancellationToken.None);

        // Assert
        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task ExecuteAsync_FormOfAnotherTenant_ReturnsNotFound()
    {
        // Arrange
        GivenForm(tenantId: SampleData.TENANT_ID + 1);

        // Act
        var result = await _sut.ExecuteAsync(Query(), CancellationToken.None);

        // Assert
        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutSchema_ReturnsCountsAndNoColumns()
    {
        // Arrange
        GivenForm(SampleData.TENANT_ID);
        _submissionsRepository.CountAsync(Arg.Any<ISpecification<Submission>>(), Arg.Any<CancellationToken>())
            .Returns(10, 7);
        _flattenedSubmissionRepository
            .CountByStatusAsync(SampleData.TENANT_ID, FormId, Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, int>
            {
                [SubmissionIntegrationStatusCodes.Processed] = 4,
                [SubmissionIntegrationStatusCodes.Pending] = 1,
                [SubmissionIntegrationStatusCodes.Processing] = 1,
                [SubmissionIntegrationStatusCodes.Failed] = 2,
                [SubmissionIntegrationStatusCodes.Skipped] = 3,
            });

        // Act
        var result = await _sut.ExecuteAsync(Query(), CancellationToken.None);

        // Assert
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.SchemaReady.Should().BeFalse();
        result.Value.Columns.Should().BeEmpty();
        result.Value.Readiness.Should().Be(new SubmissionGridReadiness(
            Submissions: 10,
            CompletedSubmissions: 7,
            Prepared: 4,
            Pending: 2,
            Failed: 2,
            SkippedInProgress: 3));
    }

    [Fact]
    public async Task ExecuteAsync_WithSchema_ReturnsItsColumns()
    {
        // Arrange
        GivenForm(SampleData.TENANT_ID);
        _formSchemaRepository.GetByFormIdAsync(SampleData.TENANT_ID, FormId, Arg.Any<CancellationToken>())
            .Returns(new FormSchemaEntity(SampleData.TENANT_ID, FormId, 1, FlatteningMapJson, CodebookJson));

        // Act
        var result = await _sut.ExecuteAsync(Query(), CancellationToken.None);

        // Assert
        result.Value.SchemaReady.Should().BeTrue();
        var column = result.Value.Columns.Should().ContainSingle().Subject;
        column.Title.Should().Be("City");
        column.FilterType.Should().Be(SubmissionGridFilterTypes.Text);
        column.PageName.Should().Be("about");
    }

    [Fact]
    public async Task ExecuteAsync_UserKeyNewerThanOldestRow_ReportsItSearchablePendingAndCountsTheSlot()
    {
        // Arrange
        GivenForm(SampleData.TENANT_ID);
        var schema = new FormSchemaEntity(SampleData.TENANT_ID, FormId, 1, FlatteningMapJson, CodebookJson);
        schema.UpdateSearchKeys(
            """[{"key":"city","source":"user","filterType":"text","addedAt":"2026-10-16T00:00:00Z","revision":1}]""",
            revision: 1);
        _formSchemaRepository.GetByFormIdAsync(SampleData.TENANT_ID, FormId, Arg.Any<CancellationToken>()).Returns(schema);
        _flattenedSubmissionRepository.OldestSearchKeysRevisionAsync(SampleData.TENANT_ID, FormId, Arg.Any<CancellationToken>())
            .Returns(0);

        // Act
        var result = await _sut.ExecuteAsync(Query(), CancellationToken.None);

        // Assert
        var column = result.Value.Columns.Should().ContainSingle().Subject;
        (column.Searchable, column.SearchPending, column.SearchSource).Should().Be((true, true, "user"));
        result.Value.SearchSlots.Should().Be(new SubmissionGridSearchSlots(Used: 1, Max: 64, UserUsed: 1, UserMax: 14));
    }

    [Fact]
    public async Task ExecuteAsync_WithoutSchema_ReportsNoSearchSlotsUsed()
    {
        // Arrange
        GivenForm(SampleData.TENANT_ID);

        // Act
        var result = await _sut.ExecuteAsync(Query(), CancellationToken.None);

        // Assert
        result.Value.SearchSlots.Should().Be(new SubmissionGridSearchSlots(Used: 0, Max: 64, UserUsed: 0, UserMax: 14));
    }

    private static SubmissionGridColumnsQuery Query() => new(SampleData.TENANT_ID, FormId, "default");

    private void GivenForm(long tenantId) =>
        _formsRepository
            .SingleOrDefaultAsync(Arg.Any<ISingleResultSpecification<Form>>(), Arg.Any<CancellationToken>())
            .Returns(Form.Create(new FormCreateArgs(TenantId: tenantId, Name: "Grid form")));
}
