using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Themes.GetById;

namespace Endatix.Core.Tests.UseCases.Themes.GetById;

public class GetThemeByIdHandlerTests
{
    private readonly IThemesRepository _themesRepository;
    private readonly GetThemeByIdHandler _handler;

    public GetThemeByIdHandlerTests()
    {
        _themesRepository = Substitute.For<IThemesRepository>();
        _handler = new GetThemeByIdHandler(_themesRepository);
    }

    [Fact]
    public async Task Handle_ThemeNotFound_ReturnsNotFoundResult()
    {
        // Arrange
        var request = new GetThemeByIdQuery(1);
        _themesRepository.GetByIdAsync(request.ThemeId, Arg.Any<CancellationToken>())
                      .Returns((Theme?)null);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.NotFound);
        result.Errors.Should().Contain("Theme not found.");
    }

    [Fact]
    public async Task Handle_ValidRequest_ReturnsTheme()
    {
        // Arrange
        var theme = new Theme(SampleData.TENANT_ID, "Test Theme", "Test Description") { Id = 1 };
        var request = new GetThemeByIdQuery(1);
        _themesRepository.GetByIdAsync(request.ThemeId, Arg.Any<CancellationToken>())
                      .Returns(theme);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().NotBeNull();
        result.Value.Should().Be(theme);
        result.Value.Forms.Should().BeEmpty();
    }
} 