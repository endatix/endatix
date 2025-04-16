using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.UseCases.Themes.GetFormsByThemeId;
using NSubstitute.ExceptionExtensions;

namespace Endatix.Core.Tests.UseCases.Themes.GetFormsByThemeId;

public class GetFormsByThemeIdHandlerTests
{
    private readonly IRepository<Form> _formsRepository;
    private readonly GetFormsByThemeIdHandler _handler;

    public GetFormsByThemeIdHandlerTests()
    {
        _formsRepository = Substitute.For<IRepository<Form>>();
        _handler = new GetFormsByThemeIdHandler(_formsRepository);
    }

    [Fact]
    public async Task Handle_NoForms_ReturnsEmptyList()
    {
        // Arrange
        var request = new GetFormsByThemeIdQuery(1);
        _formsRepository.ListAsync(
            Arg.Any<FormSpecifications.ByThemeId>(),
            Arg.Any<CancellationToken>())
            .Returns(new List<Form>());

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().NotBeNull();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_FormsFound_ReturnsListOfForms()
    {
        // Arrange
        var themeId = 1;
        var theme = new Theme(SampleData.TENANT_ID, "Test Theme", "Test Description") { Id = themeId };
        var forms = new List<Form>
        {
            CreateFormWithTheme(1, "Form 1", theme),
            CreateFormWithTheme(2, "Form 2", theme),
            CreateFormWithTheme(3, "Form 3", theme)
        };

        var request = new GetFormsByThemeIdQuery(themeId);

        _formsRepository.ListAsync(
            Arg.Any<FormSpecifications.ByThemeId>(),
            Arg.Any<CancellationToken>())
            .Returns(forms);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().NotBeNull();
        result.Value.Should().HaveCount(3);
        result.Value.Should().BeEquivalentTo(forms);
    }

    [Fact]
    public async Task Handle_RepositoryException_ReturnsErrorResult()
    {
        // Arrange
        var request = new GetFormsByThemeIdQuery(1);

        _formsRepository.ListAsync(
            Arg.Any<FormSpecifications.ByThemeId>(),
            Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Error);
        result.Errors.Should().Contain(e => e.Contains("Error retrieving forms"));
    }

    // Helper method to create a Form with a theme using reflection (for testing only)
    private Form CreateFormWithTheme(long id, string name, Theme theme)
    {
        var form = new Form(SampleData.TENANT_ID, name) { Id = id };
        form.SetTheme(theme);

        return form;
    }
}