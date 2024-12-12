using Ardalis.Specification;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.UseCases.Submissions.ListByFormId;

namespace Endatix.Core.Tests.UseCases.Submissions.ListByFormId;

public class ListByFormIdHandlerTests
{
    private readonly IRepository<Submission> _submissionsRepository;
    private readonly IRepository<FormDefinition> _formDefinitionsRepository;
    private readonly ListByFormIdHandler _handler;

    public ListByFormIdHandlerTests()
    {
        _submissionsRepository = Substitute.For<IRepository<Submission>>();
        _formDefinitionsRepository = Substitute.For<IRepository<FormDefinition>>();
        _handler = new ListByFormIdHandler(_submissionsRepository, _formDefinitionsRepository);
    }

    [Fact]
    public async Task Handle_FormNotFound_ReturnsNotFoundResult()
    {
        // Arrange
        var request = new ListByFormIdQuery(1, 1, 10);
        _formDefinitionsRepository.AnyAsync(Arg.Any<FormDefinitionsByFormIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.NotFound);
        result.Errors.Should().Contain("Form not found.");
    }

    [Fact]
    public async Task Handle_ValidRequest_ReturnsSubmissions()
    {
        // Arrange
        var formDefinition = new FormDefinition() { Id = 1 };
        var submissions = new List<Submission>
        {
            new("{ }", 1, 2) { Id = 3 },
            new("{ }", 1, 2) { Id = 4 }
        };
        var request = new ListByFormIdQuery(1, 1, 10, new List<string>());

        _formDefinitionsRepository.AnyAsync(Arg.Any<FormDefinitionsByFormIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _submissionsRepository.ListAsync(
            Arg.Any<ISpecification<Submission>>(),
            Arg.Any<CancellationToken>()
        ).Returns(submissions);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().NotBeNull();
        result.Value.Count().Should().Be(2);
    }

    [Fact]
    public async Task Handle_ValidRequest_AppliesPagination()
    {
        // Arrange
        var formDefinition = new FormDefinition() { Id = 1 };
        var request = new ListByFormIdQuery(1, 2, 20, new List<string>());

        _formDefinitionsRepository.AnyAsync(Arg.Any<FormDefinitionsByFormIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        await _handler.Handle(request, CancellationToken.None);

        // Assert
        await _submissionsRepository.Received(1).ListAsync(
            Arg.Is<ISpecification<Submission>>(spec => 
                spec.Skip == 20 && 
                spec.Take == 20
            ),
            Arg.Any<CancellationToken>()
        );
    }
}
