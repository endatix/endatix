using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Models.Themes;
using Endatix.Core.Specifications;
using Endatix.Core.UseCases.Themes.Create;
using NSubstitute.ExceptionExtensions;
using System.Text.Json;
using static Endatix.Core.Tests.ErrorMessages;
using static Endatix.Core.Tests.ErrorType;

namespace Endatix.Core.Tests.UseCases.Themes.Create;

public class CreateThemeHandlerTests
{
    private readonly IRepository<Theme> _themesRepository;
    private readonly ITenantContext _tenantContext;
    private readonly CreateThemeHandler _handler;

    public CreateThemeHandlerTests()
    {
        _themesRepository = Substitute.For<IRepository<Theme>>();
        _tenantContext = Substitute.For<ITenantContext>();
        _handler = new CreateThemeHandler(_themesRepository, _tenantContext);
    }

    [Fact]
    public async Task Handle_ValidRequest_CreatesTheme()
    {
        // Arrange
        var themeData = new ThemeData { ThemeName = "Test Theme" };
        var request = new CreateThemeCommand("Test Theme", "Test Description", themeData);
        
        _themesRepository.FirstOrDefaultAsync(
            Arg.Any<ThemeSpecifications.ByName>(),
            Arg.Any<CancellationToken>())
            .Returns((Theme?)null);
        
        _tenantContext.TenantId.Returns(SampleData.TENANT_ID);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Created);
        result.Value.Should().NotBeNull();
        result.Value.Name.Should().Be(request.Name);
        result.Value.Description.Should().Be(request.Description);
        
        // Verify JSON data contains the theme name
        var jsonData = JsonSerializer.Deserialize<ThemeData>(result.Value.JsonData);
        jsonData.Should().NotBeNull();
        jsonData!.ThemeName.Should().Be(request.Name);

        await _themesRepository.Received(1).AddAsync(
            Arg.Is<Theme>(t => 
                t.Name == request.Name &&
                t.Description == request.Description
            ),
            Arg.Any<CancellationToken>()
        );
        await _themesRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_InvalidTenantId_ThrowsArgumentException()
    {
        // Arrange
        var request = new CreateThemeCommand("Test Theme", "Test Description");
        _tenantContext.TenantId.Returns(0);

        // Act
        var act = () => _handler.Handle(request, CancellationToken.None);

        // Assert
        var expectedMessage = GetErrorMessage("Current tenant ID", ZeroOrNegative);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage(expectedMessage);
    }

    [Fact]
    public async Task Handle_DuplicateThemeName_ReturnsErrorResult()
    {
        // Arrange
        var request = new CreateThemeCommand("Existing Theme", "Test Description");
        var existingTheme = new Theme(SampleData.TENANT_ID, "Existing Theme") { Id = 1 };
        
        _tenantContext.TenantId.Returns(SampleData.TENANT_ID);
        _themesRepository.FirstOrDefaultAsync(
            Arg.Any<ThemeSpecifications.ByName>(),
            Arg.Any<CancellationToken>())
            .Returns(existingTheme);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Error);
        result.Errors.Should().Contain(e => e.Contains("already exists"));
    }

    [Fact]
    public async Task Handle_ExceptionDuringCreation_ReturnsErrorResult()
    {
        // Arrange
        var request = new CreateThemeCommand("Test Theme", "Test Description");
        
        _tenantContext.TenantId.Returns(SampleData.TENANT_ID);
        _themesRepository.FirstOrDefaultAsync(
            Arg.Any<ThemeSpecifications.ByName>(),
            Arg.Any<CancellationToken>())
            .Returns((Theme?)null);
        
        _themesRepository.AddAsync(
            Arg.Any<Theme>(),
            Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Error);
        result.Errors.Should().Contain(e => e.Contains("Error creating theme"));
    }
} 