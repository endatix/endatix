using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Tests.TestUtils;
using Endatix.Core.UseCases.FormDefinitions.Create;

namespace Endatix.Core.Tests.UseCases.FormDefinitions.Create;

public class CreateFormDefinitionHandlerTests
{
    private readonly IFormsRepository _formsRepository;
    private readonly ITenantContext _tenantContext;
    private readonly CreateFormDefinitionHandler _handler;

    public CreateFormDefinitionHandlerTests()
    {
        _formsRepository = Substitute.For<IFormsRepository>();
        _tenantContext = Substitute.For<ITenantContext>();
        _handler = new CreateFormDefinitionHandler(_formsRepository, _tenantContext);
    }

    [Fact]
    public async Task Handle_FormNotFound_ReturnsNotFoundResult()
    {
        // Arrange
        Form? notFoundForm = null;
        var request = new CreateFormDefinitionCommand(SampleData.TENANT_ID, true, SampleData.FORM_DEFINITION_JSON_DATA_1);
        _formsRepository.GetByIdAsync(
            request.FormId,
            cancellationToken: Arg.Any<CancellationToken>()
        ).Returns(notFoundForm);
        _tenantContext.TenantId.Returns(SampleData.TENANT_ID);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.NotFound);
        result.Errors.Should().Contain("Form not found.");
    }

    [Fact]
    public async Task Handle_NonDraft_ValidRequest_CreatesNew_Active_FormDefinition()
    {
        // Arrange
        var request = new CreateFormDefinitionCommand(SampleData.TENANT_ID, isDraft: false, SampleData.FORM_DEFINITION_JSON_DATA_1);
        var form = new Form(SampleData.TENANT_ID, SampleData.FORM_NAME_1)
        {
            Id = request.FormId
        };
          var formDefinition = FormDefinitionFactory.CreateForTesting(
            isDraft: false,
            jsonData: SampleData.FORM_DEFINITION_JSON_DATA_1,
            formDefinitionId: 123
        );
        form.AddFormDefinition(formDefinition);

        _formsRepository.GetByIdAsync(
            request.FormId,
            cancellationToken: Arg.Any<CancellationToken>()
        ).Returns(form);
        _tenantContext.TenantId.Returns(SampleData.TENANT_ID);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Created);
        result.Value.Should().NotBeNull();
        var createdFormDefinition = result.Value;

        form.ActiveDefinition!.JsonData.Should().Be(request.JsonData);
        form.FormDefinitions.Count.Should().Be(2);
        createdFormDefinition.IsDraft.Should().Be(request.IsDraft);
        createdFormDefinition.JsonData.Should().Be(request.JsonData);

        await _formsRepository.Received(1).UpdateAsync(form, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Draft_ValidRequest_CreatesNewFormDefinition()
    {
        // Arrange
        var request = new CreateFormDefinitionCommand(SampleData.TENANT_ID, isDraft: true, SampleData.FORM_DEFINITION_JSON_DATA_1);
        var form = new Form(SampleData.TENANT_ID, SampleData.FORM_NAME_1)
        {
            Id = request.FormId
        };
        var formDefinition = FormDefinitionFactory.CreateForTesting(
            isDraft: false,
            jsonData: SampleData.FORM_DEFINITION_JSON_DATA_1,
            formDefinitionId: 2
        );
        form.AddFormDefinition(formDefinition);

        _formsRepository.GetByIdAsync(
            request.FormId,
            cancellationToken: Arg.Any<CancellationToken>()
        ).Returns(form);
        _tenantContext.TenantId.Returns(SampleData.TENANT_ID);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Created);
        result.Value.Should().NotBeNull();
        var createdFormDefinition = result.Value;

        form.ActiveDefinition!.Id.Should().Be(2);
        form.FormDefinitions.Count.Should().Be(2);
        createdFormDefinition.TenantId.Should().Be(SampleData.TENANT_ID);
        createdFormDefinition.IsDraft.Should().Be(request.IsDraft);
        createdFormDefinition.JsonData.Should().Be(request.JsonData);

        await _formsRepository.Received(1).UpdateAsync(form, Arg.Any<CancellationToken>());
    }
}
