using Endatix.Core.Abstractions.Data;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.UseCases.Themes.Delete;
using NSubstitute.ExceptionExtensions;

namespace Endatix.Core.Tests.UseCases.Themes.Delete;

public class DeleteThemeHandlerTests
{
    private readonly IRepository<Theme> _themesRepository;
    private readonly IRepository<Form> _formsRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly DeleteThemeHandler _handler;

    public DeleteThemeHandlerTests()
    {
        _themesRepository = Substitute.For<IRepository<Theme>>();
        _formsRepository = Substitute.For<IRepository<Form>>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _handler = new DeleteThemeHandler(_themesRepository, _formsRepository, _unitOfWork);
    }

    [Fact]
    public async Task Handle_ThemeNotFound_ReturnsNotFoundResult()
    {
        // Arrange
        var request = new DeleteThemeCommand(1);
        _themesRepository.GetByIdAsync(request.ThemeId, Arg.Any<CancellationToken>())
                     .Returns((Theme?)null);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.NotFound);
        result.Errors.Should().Contain(e => e.Contains("not found"));
    }

    [Fact]
    public async Task Handle_ThemeWithForms_UpdatesFormsAndDeletesTheme()
    {
        // Arrange
        var theme = new Theme(SampleData.TENANT_ID, "Test Theme") { Id = 1 };
        var forms = new List<Form>
        {
            new Form(SampleData.TENANT_ID, "Test Form 1") { Id = 1 },
            new Form(SampleData.TENANT_ID, "Test Form 2") { Id = 2 },
            new Form(SampleData.TENANT_ID, "Test Form 3") { Id = 3 }
        };

        forms.ForEach(f => f.SetTheme(theme));

        var request = new DeleteThemeCommand(1);

        _themesRepository.GetByIdAsync(request.ThemeId, Arg.Any<CancellationToken>())
                     .Returns(theme);

        _formsRepository.ListAsync(
            Arg.Any<FormSpecifications.ByThemeId>(),
            Arg.Any<CancellationToken>())
            .Returns(forms);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Ok);

        // Forms should have their theme nullified
        foreach (var form in forms)
        {
            form.ThemeId.Should().BeNull();
        }

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _themesRepository.Received(1).DeleteAsync(theme, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).CommitTransactionAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ValidRequest_DeletesTheme()
    {
        // Arrange
        var theme = new Theme(SampleData.TENANT_ID, "Test Theme") { Id = 1 };
        var request = new DeleteThemeCommand(1);

        _themesRepository.GetByIdAsync(request.ThemeId, Arg.Any<CancellationToken>())
                     .Returns(theme);

        _formsRepository.ListAsync(
            Arg.Any<FormSpecifications.ByThemeId>(),
            Arg.Any<CancellationToken>())
            .Returns(new List<Form>());

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().Be(theme.Id.ToString());
        await _themesRepository.Received(1).DeleteAsync(
            Arg.Is<Theme>(t => t.Id == theme.Id),
            Arg.Any<CancellationToken>()
        );
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).CommitTransactionAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_RepositoryException_ReturnsErrorResult()
    {
        // Arrange
        var theme = new Theme(SampleData.TENANT_ID, "Test Theme") { Id = 1 };
        var request = new DeleteThemeCommand(1);

        _themesRepository.GetByIdAsync(request.ThemeId, Arg.Any<CancellationToken>())
                     .Returns(theme);

        _formsRepository.ListAsync(
            Arg.Any<FormSpecifications.ByThemeId>(),
            Arg.Any<CancellationToken>())
            .Returns(new List<Form>());

        _themesRepository.DeleteAsync(Arg.Any<Theme>(), Arg.Any<CancellationToken>())
                     .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Error);
        result.Errors.Should().Contain(e => e.Contains("Error deleting theme"));
    }
}