using Endatix.Core.Entities;
using Endatix.Core.Filters;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.UseCases.Forms.List;
using FluentAssertions;
using Moq;

namespace Endatix.Core.UseCases.Tests.Forms.List;

public class ListFormsHandlerTests
{
    private readonly Mock<IRepository<Form>> _repositoryMock;
    private readonly ListFormsHandler _handler;

    public ListFormsHandlerTests()
    {
        _repositoryMock = new Mock<IRepository<Form>>();
        _handler = new ListFormsHandler(_repositoryMock.Object);
    }

    [Fact]
    public async Task Handle_WithValidQuery_ShouldReturnSuccessResult()
    {
        // Arrange
        var forms = new List<Form>
        {
            new Form("Form1", "Description1", true, "{\"key\":\"value1\"}"),
            new Form("Form2", "Description2", false, "{\"key\":\"value2\"}")
        };

        var query = new ListFormsQuery(1, 10);
        _repositoryMock.Setup(x => x.ListAsync(It.IsAny<FormsSpec>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(forms);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().BeEquivalentTo(forms);
    }
}
