using Endatix.Core.Entities;
using Endatix.Core.Exceptions;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Domain;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Features.SubmissionViews;
using Microsoft.Extensions.Logging.Abstractions;

namespace Endatix.Modules.Reporting.Tests.Features.SubmissionViews;

public sealed class SaveSubmissionViewHandlerTests
{
    private const long TenantId = 1;
    private const long FormId = 100;
    private const long UserId = 7;
    private const string Settings = """{"v":1,"columns":[{"key":"q1","visible":false}]}""";

    private readonly IRepository<Form> _formsRepository = Substitute.For<IRepository<Form>>();
    private readonly ISubmissionViewRepository _viewRepository = Substitute.For<ISubmissionViewRepository>();
    private readonly IFormSchemaRepository _formSchemaRepository = Substitute.For<IFormSchemaRepository>();

    public SaveSubmissionViewHandlerTests()
    {
        _formsRepository
            .AnyAsync(Arg.Any<FormSpecifications.ByIdForTenant>(), Arg.Any<CancellationToken>())
            .Returns(true);
    }

    [Fact]
    public async Task Handle_ZeroTenant_ReturnsUnauthorizedWithoutSaving()
    {
        // Arrange
        var command = Command(new SubmissionViewSave(0, FormId, 0, Settings, UserId));

        // Act
        var result = await CreateSut().Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.Unauthorized);
        await _viewRepository.DidNotReceiveWithAnyArgs().SaveSharedAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Handle_FormNotInTenant_ReturnsNotFoundWithoutSaving()
    {
        // Arrange
        _formsRepository
            .AnyAsync(Arg.Any<FormSpecifications.ByIdForTenant>(), Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        var result = await CreateSut().Handle(Command(Save(revision: 0)), TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.NotFound);
        await _viewRepository.DidNotReceiveWithAnyArgs().SaveSharedAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Handle_AnotherSaveGotThereFirst_ReturnsConflict()
    {
        // Arrange
        var save = Save(revision: 1);
        _viewRepository.SaveSharedAsync(save, Arg.Any<CancellationToken>()).Returns((SubmissionView?)null);

        // Act
        var result = await CreateSut().Handle(Command(save), TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.Conflict);
        result.Errors.Should().Equal(SaveSubmissionViewHandler.ConflictMessage);
    }

    [Fact]
    public async Task Handle_SettingsTheEntityRefuses_ReturnsInvalidWithItsMessage()
    {
        // Arrange
        var save = Save(revision: 0);
        const string reason = "Settings must be valid JSON.";
        _viewRepository
            .SaveSharedAsync(save, Arg.Any<CancellationToken>())
            .Returns<SubmissionView?>(_ => throw new DomainValidationException(reason, "settingsJson"));

        // Act
        var result = await CreateSut().Handle(Command(save), TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(ResultStatus.Invalid);
        result.ValidationErrors.Should().ContainSingle().Which.ErrorMessage.Should().Be(reason);
    }

    [Fact]
    public async Task Handle_LatestRevision_ReturnsTheSavedView()
    {
        // Arrange
        var save = Save(revision: 0);
        var saved = SubmissionView.CreateShared(new SubmissionViewCreateArgs(TenantId, FormId, Settings, UserId));
        _viewRepository.SaveSharedAsync(save, Arg.Any<CancellationToken>()).Returns(saved);

        // Act
        var result = await CreateSut().Handle(Command(save), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(new SubmissionViewDto(1, Settings));
    }

    [Fact]
    public async Task Handle_LatestRevision_ReturnsTheSettingsAReadWouldReturn()
    {
        // Arrange
        const string stored = """{"v":1,"columns":[{"key":"q1","width":120},{"key":"gone"},{"key":"createdAt"}]}""";
        var save = new SubmissionViewSave(TenantId, FormId, 0, stored, UserId);
        _viewRepository.SaveSharedAsync(save, Arg.Any<CancellationToken>())
            .Returns(SubmissionView.CreateShared(new SubmissionViewCreateArgs(TenantId, FormId, stored, UserId)));
        _formSchemaRepository.GetByFormIdAsync(TenantId, FormId, Arg.Any<CancellationToken>())
            .Returns(SchemaWithQuestionQ1());

        // Act
        var result = await CreateSut().Handle(Command(save), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.SettingsJson.Should().Be("""{"v":1,"columns":[{"key":"q1","width":120},{"key":"createdAt"}]}""");
    }

    private static Endatix.Modules.Reporting.Domain.FormSchema SchemaWithQuestionQ1()
    {
        var compiled = new FormSchemaCompiler().CompilePersisted(
            """{"pages":[{"name":"p1","elements":[{"type":"text","name":"q1"}]}]}""");

        return new Endatix.Modules.Reporting.Domain.FormSchema(
            TenantId,
            FormId,
            formDefinitionRevision: 200,
            compiled.FlatteningMapJson,
            compiled.CodebookJson,
            compiled.LocalesJson);
    }

    private SaveSubmissionViewHandler CreateSut() =>
        new(
            _formsRepository,
            _viewRepository,
            new SubmissionViewReader(_formSchemaRepository),
            NullLogger<SaveSubmissionViewHandler>.Instance);

    private static SubmissionViewSave Save(long revision) => new(TenantId, FormId, revision, Settings, UserId);

    private static SaveSubmissionViewCommand Command(SubmissionViewSave save) => new(save);
}
