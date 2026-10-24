using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Domain;
using ReportingFormSchema = Endatix.Modules.Reporting.Domain.FormSchema;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Features.SubmissionViews;

namespace Endatix.Modules.Reporting.Tests.Features.SubmissionViews;

public sealed class GetSubmissionViewHandlerTests
{
    private const long TenantId = 1;
    private const long FormId = 100;
    private const long FormDefinitionId = 200;

    private readonly IRepository<Form> _formsRepository = Substitute.For<IRepository<Form>>();
    private readonly ISubmissionViewRepository _viewRepository = Substitute.For<ISubmissionViewRepository>();
    private readonly IFormSchemaRepository _formSchemaRepository = Substitute.For<IFormSchemaRepository>();

    public GetSubmissionViewHandlerTests()
    {
        _formsRepository
            .AnyAsync(Arg.Any<FormSpecifications.ByIdForTenant>(), Arg.Any<CancellationToken>())
            .Returns(true);
    }

    [Fact]
    public async Task Handle_ZeroTenant_ReturnsUnauthorizedWithoutReading()
    {
        // Arrange
        GetSubmissionViewQuery query = new(TenantId: 0, FormId);

        // Act
        var result = await CreateSut().Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.Unauthorized);
        await _viewRepository.DidNotReceiveWithAnyArgs().FindSharedAsync(default, default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Handle_FormNotInTenant_ReturnsNotFound()
    {
        // Arrange
        _formsRepository
            .AnyAsync(Arg.Any<FormSpecifications.ByIdForTenant>(), Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        var result = await CreateSut().Handle(new(TenantId, FormId), TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.NotFound);
        await _viewRepository.DidNotReceiveWithAnyArgs().FindSharedAsync(default, default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Handle_NoSavedView_ReturnsDefaultsAtRevisionZero()
    {
        // Arrange
        _viewRepository.FindSharedAsync(TenantId, FormId, Arg.Any<CancellationToken>()).Returns((SubmissionView?)null);

        // Act
        var result = await CreateSut().Handle(new(TenantId, FormId), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(SubmissionViewDto.Default);
        result.Value.Revision.Should().Be(0);
    }

    [Fact]
    public async Task Handle_SavedView_ReturnsItsRevisionWithoutColumnsTheFormNoLongerHas()
    {
        // Arrange
        var view = SubmissionView.CreateShared(new SubmissionViewCreateArgs(
            TenantId,
            FormId,
            """{"v":1,"columns":[{"key":"q1","width":120},{"key":"gone"},{"key":"createdAt"}],"pageSize":50}""",
            ModifiedBy: 7));
        _viewRepository.FindSharedAsync(TenantId, FormId, Arg.Any<CancellationToken>()).Returns(view);
        _formSchemaRepository.GetByFormIdAsync(TenantId, FormId, Arg.Any<CancellationToken>()).Returns(SchemaWithQuestionQ1());

        // Act
        var result = await CreateSut().Handle(new(TenantId, FormId), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Revision.Should().Be(view.Revision);
        result.Value.SettingsJson.Should().Be(
            """{"v":1,"columns":[{"key":"q1","width":120},{"key":"createdAt"}],"pageSize":50}""");
    }

    private GetSubmissionViewHandler CreateSut() => new(_formsRepository, _viewRepository, _formSchemaRepository);

    private static ReportingFormSchema SchemaWithQuestionQ1()
    {
        var compiled = new FormSchemaCompiler().CompilePersisted(
            """{"pages":[{"name":"p1","elements":[{"type":"text","name":"q1"}]}]}""");

        return new ReportingFormSchema(
            TenantId,
            FormId,
            FormDefinitionId,
            compiled.FlatteningMapJson,
            compiled.CodebookJson,
            compiled.LocalesJson);
    }
}
