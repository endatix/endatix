using Ardalis.Specification;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Models.Themes;
using Endatix.Core.UseCases.Themes.PartialUpdate;
using NSubstitute.ExceptionExtensions;
using System.Text.Json;

namespace Endatix.Core.Tests.UseCases.Themes.PartialUpdate;

public class PartialUpdateThemeHandlerTests
{
    private readonly IThemesRepository _themesRepository;
    private readonly PartialUpdateThemeHandler _handler;

    public PartialUpdateThemeHandlerTests()
    {
        _themesRepository = Substitute.For<IThemesRepository>();
        _handler = new PartialUpdateThemeHandler(_themesRepository);
    }

    [Fact]
    public async Task Handle_ThemeNotFound_ReturnsNotFoundResult()
    {
        // Arrange
        var request = new PartialUpdateThemeCommand(1, "Updated Theme");
        _themesRepository.GetByIdAsync(request.ThemeId, Arg.Any<CancellationToken>())
                     .Returns((Theme?)null);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.NotFound);
        result.Errors.Should().Contain("Theme not found");
    }

    [Fact]
    public async Task Handle_PartialUpdateName_UpdatesOnlyName()
    {
        // Arrange
        var themeId = 1;
        var originalTheme = new Theme(SampleData.TENANT_ID, "Original Name", "Original Description") { Id = themeId };
        originalTheme.UpdateJsonData("{\"themeName\":\"Original Name\",\"colorPalette\":\"light\"}");

        var request = new PartialUpdateThemeCommand(themeId, "Updated Theme");

        _themesRepository.GetByIdAsync(request.ThemeId, Arg.Any<CancellationToken>())
                     .Returns(originalTheme);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().NotBeNull();
        result.Value.Name.Should().Be("Updated Theme");
        result.Value.Description.Should().Be("Original Description");

        // Verify JSON data was updated for the name only
        var jsonData = JsonSerializer.Deserialize<ThemeData>(result.Value.JsonData);
        jsonData.Should().NotBeNull();
        jsonData!.ThemeName.Should().Be("Updated Theme");
        jsonData!.ColorPalette.Should().Be("light");

        await _themesRepository.Received(1).UpdateAsync(
            Arg.Is<Theme>(t =>
                t.Id == themeId &&
                t.Name == "Updated Theme" &&
                t.Description == "Original Description"
            ),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task Handle_PartialUpdateDescription_UpdatesOnlyDescription()
    {
        // Arrange
        var themeId = 1;
        var originalTheme = new Theme(SampleData.TENANT_ID, "Original Name", "Original Description") { Id = themeId };
        originalTheme.UpdateJsonData("{\"themeName\":\"Original Name\",\"colorPalette\":\"light\"}");

        var request = new PartialUpdateThemeCommand(themeId, null, "Updated Description");

        _themesRepository.GetByIdAsync(request.ThemeId, Arg.Any<CancellationToken>())
                     .Returns(originalTheme);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().NotBeNull();
        result.Value.Name.Should().Be("Original Name");
        result.Value.Description.Should().Be("Updated Description");

        // Verify JSON data wasn't changed
        var jsonData = JsonSerializer.Deserialize<ThemeData>(result.Value.JsonData);
        jsonData.Should().NotBeNull();
        jsonData!.ThemeName.Should().Be("Original Name");
        jsonData!.ColorPalette.Should().Be("light");

        await _themesRepository.Received(1).UpdateAsync(
            Arg.Is<Theme>(t =>
                t.Id == themeId &&
                t.Name == "Original Name" &&
                t.Description == "Updated Description"
            ),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task Handle_PartialUpdateThemeData_UpdatesOnlyThemeData()
    {
        // Arrange
        var themeId = 1;
        var originalTheme = new Theme(SampleData.TENANT_ID, "Original Name", "Original Description") { Id = themeId };
        originalTheme.UpdateJsonData("{\"themeName\":\"Original Name\",\"colorPalette\":\"light\"}");

        var themeData = new ThemeData
        {
            ThemeName = "Original Name", // Keep name the same
            ColorPalette = "dark", // Change only color palette
            CssVariables = new Dictionary<string, string> { ["--primary-color"] = "#000000" }
        };

        var request = new PartialUpdateThemeCommand(themeId, null, null, themeData);

        _themesRepository.GetByIdAsync(request.ThemeId, Arg.Any<CancellationToken>())
                     .Returns(originalTheme);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().NotBeNull();
        result.Value.Name.Should().Be("Original Name");
        result.Value.Description.Should().Be("Original Description");

        // Verify only theme data was updated
        var jsonData = JsonSerializer.Deserialize<ThemeData>(result.Value.JsonData);
        jsonData.Should().NotBeNull();
        jsonData!.ThemeName.Should().Be("Original Name");
        jsonData!.ColorPalette.Should().Be("dark");
        jsonData!.CssVariables.Should().ContainKey("--primary-color");

        await _themesRepository.Received(1).UpdateAsync(
            Arg.Is<Theme>(t =>
                t.Id == themeId &&
                t.Name == "Original Name" &&
                t.Description == "Original Description"
            ),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task Handle_PartialUpdateAll_UpdatesAllFields()
    {
        // Arrange
        var themeId = 1;
        var originalTheme = new Theme(SampleData.TENANT_ID, "Original Name", "Original Description") { Id = themeId };
        originalTheme.UpdateJsonData("{\"themeName\":\"Original Name\",\"colorPalette\":\"light\"}");

        var themeData = new ThemeData
        {
            ThemeName = "Updated Theme",
            ColorPalette = "dark",
            CssVariables = new Dictionary<string, string> { ["--primary-color"] = "#000000" }
        };

        var request = new PartialUpdateThemeCommand(themeId, "Updated Theme", "Updated Description", themeData);

        _themesRepository.GetByIdAsync(request.ThemeId, Arg.Any<CancellationToken>())
                     .Returns(originalTheme);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().NotBeNull();
        result.Value.Name.Should().Be("Updated Theme");
        result.Value.Description.Should().Be("Updated Description");

        // Verify all theme data was updated
        var jsonData = JsonSerializer.Deserialize<ThemeData>(result.Value.JsonData);
        jsonData.Should().NotBeNull();
        jsonData!.ThemeName.Should().Be("Updated Theme");
        jsonData!.ColorPalette.Should().Be("dark");
        jsonData!.CssVariables.Should().ContainKey("--primary-color");

        await _themesRepository.Received(1).UpdateAsync(
            Arg.Is<Theme>(t =>
                t.Id == themeId &&
                t.Name == "Updated Theme" &&
                t.Description == "Updated Description"
            ),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task Handle_RepositoryException_ReturnsErrorResult()
    {
        // Arrange
        var themeId = 1;
        var originalTheme = new Theme(SampleData.TENANT_ID, "Original Name", "Original Description") { Id = themeId };
        var request = new PartialUpdateThemeCommand(themeId, "Updated Theme");

        _themesRepository.GetByIdAsync(request.ThemeId, Arg.Any<CancellationToken>())
                     .Returns(originalTheme);

        _themesRepository.UpdateAsync(Arg.Any<Theme>(), Arg.Any<CancellationToken>())
                     .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Error);
        result.Errors.Should().Contain(e => e.Contains("Error updating theme"));
    }

    [Fact]
    public async Task Handle_DuplicateThemeName_ReturnsErrorResult()
    {
        // Arrange
        var themeId = 1;
        var existingThemeId = 2;
        var themeName = "Duplicate Theme Name";

        var originalTheme = new Theme(SampleData.TENANT_ID, "Original Name", "Original Description") { Id = themeId };
        var existingTheme = new Theme(SampleData.TENANT_ID, themeName, "Another Description") { Id = existingThemeId };

        var request = new PartialUpdateThemeCommand(themeId, name: themeName);

        _themesRepository.GetByIdAsync(request.ThemeId, Arg.Any<CancellationToken>())
                     .Returns(originalTheme);

        _themesRepository.FirstOrDefaultAsync(Arg.Any<ISpecification<Theme>>(), Arg.Any<CancellationToken>())
                     .Returns(existingTheme);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Error);
        result.Errors.Should().Contain($"Another theme with the name '{themeName}' already exists");

        // Verify the theme was not updated
        await _themesRepository.DidNotReceive().UpdateAsync(Arg.Any<Theme>(), Arg.Any<CancellationToken>());
    }
}