using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.UseCases.FormDefinitions.PartialUpdateActive;

namespace Endatix.Core.Tests.UseCases.FormDefinitions.PartialUpdateActive;

public class PartialUpdateActiveFormDefinitionHandlerTests
{
    private readonly IFormsRepository _formRepository;
    private readonly IRepository<Submission> _submissionsRepository;
    private readonly PartialUpdateActiveFormDefinitionHandler _handler;

    public PartialUpdateActiveFormDefinitionHandlerTests()
    {
        _formRepository = Substitute.For<IFormsRepository>();
        _submissionsRepository = Substitute.For<IRepository<Submission>>();
        _handler = new PartialUpdateActiveFormDefinitionHandler(
            _formRepository,
            _submissionsRepository);
    }

    [Fact]
    public async Task Handle_FormDefinitionNotFound_ReturnsNotFoundResult()
    {
        // Arrange
        var request = new PartialUpdateActiveFormDefinitionCommand(1, null, null);
        _formRepository.SingleOrDefaultAsync(
            Arg.Any<ActiveFormDefinitionByFormIdSpec>(),
            Arg.Any<CancellationToken>()
        ).Returns(Task.FromResult<Form?>(null));

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.NotFound);
        result.Errors.Should().Contain("Active form definition not found.");
    }

    [Fact]
    public async Task Handle_ValidRequest_UpdatesFormDefinition()
    {
        // Arrange
        var form = new Form(SampleData.TENANT_ID, "Test Form") { Id = 1 };
        var formDefinition = new FormDefinition(SampleData.TENANT_ID, jsonData: SampleData.FORM_DEFINITION_JSON_DATA_1);
        form.AddFormDefinition(formDefinition);

        var request = new PartialUpdateActiveFormDefinitionCommand(1, false, SampleData.FORM_DEFINITION_JSON_DATA_2);
        _formRepository.SingleOrDefaultAsync(
            Arg.Any<ActiveFormDefinitionByFormIdSpec>(),
            Arg.Any<CancellationToken>()
        ).Returns(form);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().NotBeNull();
        result.Value.IsDraft.Should().Be(request.IsDraft!.Value);
        result.Value.JsonData.Should().Be(request.JsonData);
        await _formRepository.Received(1).UpdateAsync(form, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PartialUpdate_UpdatesOnlySpecifiedFields()
    {
        // Arrange
        var form = new Form(SampleData.TENANT_ID, "Test Form") { Id = 1 };
        var formDefinition = new FormDefinition(SampleData.TENANT_ID, jsonData: SampleData.FORM_DEFINITION_JSON_DATA_1);
        form.AddFormDefinition(formDefinition);

        var request = new PartialUpdateActiveFormDefinitionCommand(1, null, SampleData.FORM_DEFINITION_JSON_DATA_2);
        _formRepository.SingleOrDefaultAsync(
            Arg.Any<ActiveFormDefinitionByFormIdSpec>(),
            Arg.Any<CancellationToken>()
        ).Returns(form);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().NotBeNull();
        result.Value.IsDraft.Should().Be(formDefinition.IsDraft);
        result.Value.JsonData.Should().Be(request.JsonData);

        await _formRepository.Received(1).UpdateAsync(form, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithSubmissionsAndChangedJsonData_CreatesNewPublishedDefinitionAndSetsItActive()
    {
        // Arrange
        var form = new Form(SampleData.TENANT_ID, "Test Form") { Id = 1 };
        var originalDefinition = new FormDefinition(SampleData.TENANT_ID, jsonData: SampleData.FORM_DEFINITION_JSON_DATA_1)
        {
            Id = 10
        };
        form.AddFormDefinition(originalDefinition);

        var request = new PartialUpdateActiveFormDefinitionCommand(1, false, SampleData.FORM_DEFINITION_JSON_DATA_2);
        _formRepository.SingleOrDefaultAsync(
            Arg.Any<ActiveFormDefinitionByFormIdSpec>(),
            Arg.Any<CancellationToken>()
        ).Returns(form);
        _submissionsRepository.AnyAsync(
            Arg.Any<SubmissionsTotalCountByFormDefinitionIdSpec>(),
            Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.JsonData.Should().Be(SampleData.FORM_DEFINITION_JSON_DATA_2);
        result.Value.IsDraft.Should().BeFalse();
        form.FormDefinitions.Should().HaveCount(2);
        form.ActiveDefinition.Should().Be(result.Value);
        originalDefinition.JsonData.Should().Be(SampleData.FORM_DEFINITION_JSON_DATA_1);
        await _formRepository.Received(1).UpdateAsync(form, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithSubmissionsAndChangedJsonData_CreatesDraftWithoutChangingActiveDefinition()
    {
        // Arrange
        var form = new Form(SampleData.TENANT_ID, "Test Form") { Id = 1 };
        var originalDefinition = new FormDefinition(SampleData.TENANT_ID, jsonData: SampleData.FORM_DEFINITION_JSON_DATA_1)
        {
            Id = 10
        };
        form.AddFormDefinition(originalDefinition);

        var request = new PartialUpdateActiveFormDefinitionCommand(1, true, SampleData.FORM_DEFINITION_JSON_DATA_2);
        _formRepository.SingleOrDefaultAsync(
            Arg.Any<ActiveFormDefinitionByFormIdSpec>(),
            Arg.Any<CancellationToken>()
        ).Returns(form);
        _submissionsRepository.AnyAsync(
            Arg.Any<SubmissionsTotalCountByFormDefinitionIdSpec>(),
            Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.JsonData.Should().Be(SampleData.FORM_DEFINITION_JSON_DATA_2);
        result.Value.IsDraft.Should().BeTrue();
        form.FormDefinitions.Should().HaveCount(2);
        form.ActiveDefinition.Should().Be(originalDefinition);
        await _formRepository.Received(1).UpdateAsync(form, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithoutSubmissionsAndChangedJsonData_UpdatesActiveDefinitionInPlace()
    {
        // Arrange
        var form = new Form(SampleData.TENANT_ID, "Test Form") { Id = 1 };
        var formDefinition = new FormDefinition(SampleData.TENANT_ID, jsonData: SampleData.FORM_DEFINITION_JSON_DATA_1)
        {
            Id = 10
        };
        form.AddFormDefinition(formDefinition);

        var request = new PartialUpdateActiveFormDefinitionCommand(1, false, SampleData.FORM_DEFINITION_JSON_DATA_2);
        _formRepository.SingleOrDefaultAsync(
            Arg.Any<ActiveFormDefinitionByFormIdSpec>(),
            Arg.Any<CancellationToken>()
        ).Returns(form);
        _submissionsRepository.AnyAsync(
            Arg.Any<SubmissionsTotalCountByFormDefinitionIdSpec>(),
            Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Status.Should().Be(ResultStatus.Ok);
        form.FormDefinitions.Should().ContainSingle();
        form.ActiveDefinition.Should().Be(formDefinition);
        formDefinition.JsonData.Should().Be(SampleData.FORM_DEFINITION_JSON_DATA_2);
        await _formRepository.Received(1).UpdateAsync(form, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithSubmissionsAndUnchangedJsonData_UpdatesActiveDefinitionInPlace()
    {
        // Arrange
        var form = new Form(SampleData.TENANT_ID, "Test Form") { Id = 1 };
        var formDefinition = new FormDefinition(SampleData.TENANT_ID, jsonData: SampleData.FORM_DEFINITION_JSON_DATA_1)
        {
            Id = 10
        };
        form.AddFormDefinition(formDefinition);

        var request = new PartialUpdateActiveFormDefinitionCommand(1, false, SampleData.FORM_DEFINITION_JSON_DATA_1);
        _formRepository.SingleOrDefaultAsync(
            Arg.Any<ActiveFormDefinitionByFormIdSpec>(),
            Arg.Any<CancellationToken>()
        ).Returns(form);
        _submissionsRepository.AnyAsync(
            Arg.Any<SubmissionsTotalCountByFormDefinitionIdSpec>(),
            Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Status.Should().Be(ResultStatus.Ok);
        form.FormDefinitions.Should().ContainSingle();
        form.ActiveDefinition.Should().Be(formDefinition);
        formDefinition.JsonData.Should().Be(SampleData.FORM_DEFINITION_JSON_DATA_1);
        await _submissionsRepository.DidNotReceive().AnyAsync(
            Arg.Any<SubmissionsTotalCountByFormDefinitionIdSpec>(),
            Arg.Any<CancellationToken>());
        await _formRepository.Received(1).UpdateAsync(form, Arg.Any<CancellationToken>());
    }
}
