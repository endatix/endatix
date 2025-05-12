using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.UseCases.CustomQuestions.Create;

namespace Endatix.Core.Tests.UseCases.CustomQuestions.Create;

public class CreateCustomQuestionHandlerTests
{
    private readonly IRepository<CustomQuestion> _repository;
    private readonly ITenantContext _tenantContext;
    private readonly CreateCustomQuestionHandler _handler;

    public CreateCustomQuestionHandlerTests()
    {
        _repository = Substitute.For<IRepository<CustomQuestion>>();
        _tenantContext = Substitute.For<ITenantContext>();
        _handler = new CreateCustomQuestionHandler(_repository, _tenantContext);
    }

    [Fact]
    public async Task Handle_ValidRequest_CreatesCustomQuestion()
    {
        // Arrange
        var request = new CreateCustomQuestionCommand(
            "Test Question",
            "{ \"type\": \"text\" }",
            "Test Description"
        );

        var createdQuestion = new CustomQuestion(
            SampleData.TENANT_ID,
            request.Name,
            request.JsonData,
            request.Description
        );

        _repository.AddAsync(
            Arg.Any<CustomQuestion>(),
            Arg.Any<CancellationToken>()
        ).Returns(createdQuestion);
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
        result.Value.TenantId.Should().Be(SampleData.TENANT_ID);
        
        await _repository.Received(1).AddAsync(
            Arg.Is<CustomQuestion>(q => 
                q.Name == request.Name &&
                q.Description == request.Description &&
                q.JsonData == request.JsonData &&
                q.TenantId == SampleData.TENANT_ID
            ),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task Handle_InvalidTenantId_ThrowsArgumentException()
    {
        // Arrange
        var request = new CreateCustomQuestionCommand(
            "Test Question",
            "{ \"type\": \"text\" }",
            "Test Description"
        );
        _tenantContext.TenantId.Returns(0);

        // Act
        Func<Task> act = () => _handler.Handle(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*tenantContext.TenantId*");
    }

    [Fact]
    public async Task Handle_DuplicateName_ReturnsInvalidResult()
    {
        // Arrange
        var request = new CreateCustomQuestionCommand(
            "Test Question",
            "{ \"type\": \"text\" }",
            "Test Description"
        );

        var existingQuestion = new CustomQuestion(
            SampleData.TENANT_ID,
            request.Name,
            "{ \"type\": \"number\" }",
            "Existing Description"
        );

        _repository.FirstOrDefaultAsync(
            Arg.Any<CustomQuestionSpecifications.ByName>(),
            Arg.Any<CancellationToken>()
        ).Returns(existingQuestion);
        _tenantContext.TenantId.Returns(SampleData.TENANT_ID);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Invalid);
        result.ValidationErrors.Should().ContainSingle(e => e.ErrorMessage.Contains($"A custom question with the name '{request.Name}' already exists"));
    }
} 