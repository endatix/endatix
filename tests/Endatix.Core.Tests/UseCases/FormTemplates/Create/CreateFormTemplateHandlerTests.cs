using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.FormTemplates.Create;
using static Endatix.Core.Tests.ErrorMessages;
using static Endatix.Core.Tests.ErrorType;

namespace Endatix.Core.Tests.UseCases.FormTemplates.Create;

public class CreateFormTemplateHandlerTests
{
    private readonly IRepository<FormTemplate> _repository;
    private readonly ITenantContext _tenantContext;
    private readonly CreateFormTemplateHandler _handler;

    public CreateFormTemplateHandlerTests()
    {
        _repository = Substitute.For<IRepository<FormTemplate>>();
        _tenantContext = Substitute.For<ITenantContext>();
        _handler = new CreateFormTemplateHandler(_repository, _tenantContext);
    }

    [Fact]
    public async Task Handle_ValidRequest_CreatesFormTemplate()
    {
        // Arrange
        var request = new CreateFormTemplateCommand(
            SampleData.FORM_NAME_1,
            SampleData.FORM_DESCRIPTION_1,
            SampleData.FORM_DEFINITION_JSON_DATA_1,
            true
        );

        var createdTemplate = new FormTemplate(
            SampleData.TENANT_ID,
            request.Name,
            request.Description,
            request.JsonData,
            request.IsEnabled
        );

        _repository.AddAsync(
            Arg.Any<FormTemplate>(),
            Arg.Any<CancellationToken>()
        ).Returns(createdTemplate);
        _tenantContext.TenantId.Returns(SampleData.TENANT_ID);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Created);
        result.Value.Should().NotBeNull();
        result.Value.Name.Should().Be(request.Name);
        result.Value.Description.Should().Be(request.Description);
        result.Value.JsonData.Should().Be(request.JsonData);
        result.Value.IsEnabled.Should().Be(request.IsEnabled);
        result.Value.TenantId.Should().Be(SampleData.TENANT_ID);
        
        await _repository.Received(1).AddAsync(
            Arg.Is<FormTemplate>(ft => 
                ft.Name == request.Name &&
                ft.Description == request.Description &&
                ft.JsonData == request.JsonData &&
                ft.IsEnabled == request.IsEnabled
            ),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task Handle_InvalidTenantId_ThrowsArgumentException()
    {
        // Arrange
        var request = new CreateFormTemplateCommand(
            SampleData.FORM_NAME_1,
            SampleData.FORM_DESCRIPTION_1,
            SampleData.FORM_DEFINITION_JSON_DATA_1,
            true
        );
        _tenantContext.TenantId.Returns(0);

        // Act
        Func<Task> act = () => _handler.Handle(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage(GetErrorMessage("tenantContext.TenantId", ZeroOrNegative));
    }
} 