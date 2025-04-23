using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.UseCases.Themes.List;
using NSubstitute.ExceptionExtensions;

namespace Endatix.Core.Tests.UseCases.Themes.List;

public class ListThemesHandlerTests
{
    private readonly IRepository<Theme> _themesRepository;
    private readonly ListThemesHandler _handler;

    public ListThemesHandlerTests()
    {
        _themesRepository = Substitute.For<IRepository<Theme>>();
        _handler = new ListThemesHandler(_themesRepository);
    }

    [Fact]
    public async Task Handle_NoThemes_ReturnsEmptyList()
    {
        // Arrange
        var request = new ListThemesQuery();
        _themesRepository.ListAsync(Arg.Any<ThemeSpecifications.Paginated>(), Arg.Any<CancellationToken>())
                     .Returns(new List<Theme>());

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().NotBeNull();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ExistingThemes_ReturnsAllThemes()
    {
        // Arrange
        var themes = new List<Theme>
        {
            new Theme(SampleData.TENANT_ID, "Theme 1", "Description 1") { Id = 1 },
            new Theme(SampleData.TENANT_ID, "Theme 2", "Description 2") { Id = 2 },
            new Theme(SampleData.TENANT_ID, "Theme 3", "Description 3") { Id = 3 }
        };
        var request = new ListThemesQuery();
        _themesRepository.ListAsync(Arg.Any<ThemeSpecifications.Paginated>(), Arg.Any<CancellationToken>())
                     .Returns(themes);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().NotBeNull();
        result.Value.Should().HaveCount(3);
        result.Value.Should().BeEquivalentTo(themes);
    }

    [Fact]
    public async Task Handle_RepositoryException_ThrowsException()
    {
        // Arrange
        var request = new ListThemesQuery();
        _themesRepository.ListAsync(Arg.Any<ThemeSpecifications.Paginated>(), Arg.Any<CancellationToken>())
                     .ThrowsAsync(new Exception("Database error"));

        // Act 
        var act = async () => await _handler.Handle(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<Exception>();
    }
}