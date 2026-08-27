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
    public async Task Handle_NoThemes_ReturnsEmptyPage()
    {
        // Arrange
        var request = new ListThemesQuery(1, 10);
        _themesRepository.CountAsync(
            Arg.Any<ThemeSpecifications.ListFilter>(),
            Arg.Any<CancellationToken>())
            .Returns(0);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().NotBeNull();
        result.Value.Items.Should().BeEmpty();
        result.Value.TotalRecords.Should().Be(0);

        await _themesRepository.DidNotReceive().ListAsync(
            Arg.Any<ThemeSpecifications.Paginated>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ExistingThemes_ReturnsPagedThemes()
    {
        // Arrange
        var themes = new List<Theme>
        {
            new Theme(SampleData.TENANT_ID, "Theme 1", "Description 1") { Id = 1 },
            new Theme(SampleData.TENANT_ID, "Theme 2", "Description 2") { Id = 2 },
            new Theme(SampleData.TENANT_ID, "Theme 3", "Description 3") { Id = 3 }
        };
        var request = new ListThemesQuery(1, 10);
        _themesRepository.CountAsync(
            Arg.Any<ThemeSpecifications.ListFilter>(),
            Arg.Any<CancellationToken>())
            .Returns(3);
        _themesRepository.ListAsync(
            Arg.Any<ThemeSpecifications.Paginated>(),
            Arg.Any<CancellationToken>())
            .Returns(themes);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().NotBeNull();
        result.Value.Items.Should().HaveCount(3);
        result.Value.Items.Should().BeEquivalentTo(themes);
        result.Value.TotalRecords.Should().Be(3);
        result.Value.Page.Should().Be(1);
    }

    [Fact]
    public async Task Handle_RepositoryException_ThrowsException()
    {
        // Arrange
        var request = new ListThemesQuery(1, 10);
        _themesRepository.CountAsync(
            Arg.Any<ThemeSpecifications.ListFilter>(),
            Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var act = () => _handler.Handle(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<Exception>();
    }
}
