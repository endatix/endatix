using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Models.Themes;
using Endatix.Core.UseCases.Themes.Update;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using System.Text.Json;
using Ardalis.Specification;

namespace Endatix.Core.Tests.UseCases.Themes.Update;

public class UpdateThemeHandlerTests
{
    private readonly IRepository<Theme> _themesRepository;
    private readonly UpdateThemeHandler _handler;

    public UpdateThemeHandlerTests()
    {
        _themesRepository = Substitute.For<IRepository<Theme>>();
        _handler = new UpdateThemeHandler(_themesRepository);
    }

    [Fact]
    public async Task Handle_ThemeNotFound_ReturnsNotFoundResult()
    {
        // Arrange
        var request = new UpdateThemeCommand(1, "Updated Theme", "Updated Description", new ThemeData());
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
    public async Task Handle_ValidRequest_UpdatesTheme()
    {
        // Arrange
        var themeId = 1;
        var originalTheme = new Theme(SampleData.TENANT_ID, "Original Name", "Original Description") { Id = themeId };
        
        var themeData = new ThemeData { 
            ThemeName = "Updated Theme",
            ColorPalette = "dark",
            CssVariables = new Dictionary<string, string> { ["--primary-color"] = "#000000" }
        };
        
        var request = new UpdateThemeCommand(themeId, "Updated Theme", "Updated Description", themeData);
        
        _themesRepository.GetByIdAsync(request.ThemeId, Arg.Any<CancellationToken>())
                     .Returns(originalTheme);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().NotBeNull();
        result.Value.Name.Should().Be(request.Name);
        result.Value.Description.Should().Be(request.Description);
        
        // Verify JSON data was updated
        var jsonData = JsonSerializer.Deserialize<ThemeData>(result.Value.JsonData);
        jsonData.Should().NotBeNull();
        jsonData!.ThemeName.Should().Be(request.Name);
        jsonData!.ColorPalette.Should().Be("dark");
        jsonData!.CssVariables.Should().ContainKey("--primary-color");

        await _themesRepository.Received(1).UpdateAsync(
            Arg.Is<Theme>(t => 
                t.Id == themeId &&
                t.Name == request.Name &&
                t.Description == request.Description
            ),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task Handle_EmptyThemeData_UpdatesNameAndDescription()
    {
        // Arrange
        var themeId = 1;
        var originalTheme = new Theme(SampleData.TENANT_ID, "Original Name", "Original Description") 
        { 
            Id = themeId 
        };
        originalTheme.UpdateJsonData("{\"themeName\":\"Original Name\",\"colorPalette\":\"light\"}");
        
        var request = new UpdateThemeCommand(themeId, "Updated Theme", "Updated Description", null);
        
        _themesRepository.GetByIdAsync(request.ThemeId, Arg.Any<CancellationToken>())
                     .Returns(originalTheme);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().NotBeNull();
        result.Value.Name.Should().Be(request.Name);
        result.Value.Description.Should().Be(request.Description);
        
        // Verify name in JSON was updated but other properties remained
        var jsonData = JsonSerializer.Deserialize<ThemeData>(result.Value.JsonData);
        jsonData.Should().NotBeNull();
        jsonData!.ThemeName.Should().Be(request.Name);
        jsonData!.ColorPalette.Should().Be("light");
    }

    [Fact]
    public async Task Handle_RepositoryException_ReturnsErrorResult()
    {
        // Arrange
        var themeId = 1;
        var originalTheme = new Theme(SampleData.TENANT_ID, "Original Name", "Original Description") { Id = themeId };
        var request = new UpdateThemeCommand(themeId, "Updated Theme", "Updated Description", new ThemeData());
        
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
        
        var request = new UpdateThemeCommand(themeId, themeName, "Updated Description", new ThemeData());
        
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