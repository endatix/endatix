using Ardalis.Specification;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases;
using Endatix.Core.UseCases.Themes.GetById;

namespace Endatix.Core.Tests.UseCases.Themes.GetById;

public class GetThemeByIdHandlerTests
{
    private readonly IRepository<Theme> _themesRepository;
    private readonly GetThemeByIdHandler _handler;

    public GetThemeByIdHandlerTests()
    {
        _themesRepository = Substitute.For<IRepository<Theme>>();
        _handler = new GetThemeByIdHandler(_themesRepository);
    }

    [Theory]
    [InlineData(2, ResultStatus.Ok)]
    [InlineData(1, ResultStatus.NotFound)]
    [InlineData(0, ResultStatus.NotFound)]
    public async Task Handle_RegisteredLookup_SelectsOnlyTheCurrentTenantsTheme(long tenantId, ResultStatus expected)
    {
        // Arrange
        var tenant = Substitute.For<ITenantContext>();
        tenant.TenantId.Returns(tenantId);
        var handler = new GetThemeByIdHandler.Registered(_themesRepository, new ContentQueryScope(tenant));
        var theme = new Theme(2, "Blue", null) { Id = 10 };
        _themesRepository.FirstOrDefaultAsync(Arg.Any<ISpecification<Theme>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<ISpecification<Theme>>().IsSatisfiedBy(theme) ? theme : null);

        // Act
        var result = await handler.Handle(new GetThemeByIdQuery(theme.Id), TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(expected);
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
