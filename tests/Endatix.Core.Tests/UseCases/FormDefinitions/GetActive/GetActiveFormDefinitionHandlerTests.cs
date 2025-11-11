using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Features.ReCaptcha;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.UseCases.FormDefinitions.GetActive;

namespace Endatix.Core.Tests.UseCases.FormDefinitions.GetActive;

public class GetActiveFormDefinitionHandlerTests
{
    private readonly IFormsRepository _formsRepository;
    private readonly IRepository<CustomQuestion> _customQuestionsRepository;
    private readonly IReCaptchaPolicyService _recaptchaPolicyService;
    private readonly ICurrentUserAuthorizationService _authorizationService;
    private readonly GetActiveFormDefinitionHandler _handler;

    public GetActiveFormDefinitionHandlerTests()
    {
        _formsRepository = Substitute.For<IFormsRepository>();
        _customQuestionsRepository = Substitute.For<IRepository<CustomQuestion>>();
        _recaptchaPolicyService = Substitute.For<IReCaptchaPolicyService>();
        _authorizationService = Substitute.For<ICurrentUserAuthorizationService>();
        _handler = new GetActiveFormDefinitionHandler(_formsRepository, _customQuestionsRepository, _recaptchaPolicyService, _authorizationService);
    }

    [Fact]
    public async Task Handle_FormDefinitionNotFound_ReturnsNotFoundResult()
    {
        // Arrange
        var request = new GetActiveFormDefinitionQuery(1, null, "access.authenticated");
        _formsRepository.SingleOrDefaultAsync(Arg.Any<ActiveFormDefinitionByFormIdSpec>(), Arg.Any<CancellationToken>())
                   .Returns(Task.FromResult<Form?>(null));

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.NotFound);
        result.Errors.Should().Contain("Active form definition not found.");
    }

    [Fact]
    public async Task Handle_ValidRequest_ReturnsActiveFormDefinition()
    {
        // Arrange
        var formWithActiveDefinition = new Form(SampleData.TENANT_ID, SampleData.FORM_NAME_1, isPublic: true)
        {
            Id = 1
        };
        var activeDefinition = new FormDefinition(SampleData.TENANT_ID, jsonData: SampleData.FORM_DEFINITION_JSON_DATA_1);
        formWithActiveDefinition.AddFormDefinition(activeDefinition);

        var request = new GetActiveFormDefinitionQuery(1, null, "access.authenticated");

        _formsRepository.SingleOrDefaultAsync(
            Arg.Any<ActiveFormDefinitionByFormIdSpec>(),
            CancellationToken.None
        ).Returns(formWithActiveDefinition);

        _customQuestionsRepository.ListAsync(
            Arg.Any<CustomQuestionSpecifications.ByTenantId>(),
            Arg.Any<CancellationToken>()
        ).Returns(new List<CustomQuestion>());

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().NotBeNull();
        result.Value.Id.Should().Be(activeDefinition.Id);
        result.Value.CustomQuestions.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ValidRequestWithCustomQuestions_ReturnsActiveFormDefinitionWithCustomQuestions()
    {
        // Arrange
        var formWithActiveDefinition = new Form(SampleData.TENANT_ID, SampleData.FORM_NAME_1, isPublic: true)
        {
            Id = 1
        };
        var activeDefinition = new FormDefinition(SampleData.TENANT_ID, jsonData: SampleData.FORM_DEFINITION_JSON_DATA_1);
        formWithActiveDefinition.AddFormDefinition(activeDefinition);

        var customQuestions = new List<CustomQuestion>
        {
            new(SampleData.TENANT_ID, "Question 1", "{ \"type\": \"text\" }", "Description 1"),
            new(SampleData.TENANT_ID, "Question 2", "{ \"type\": \"number\" }", "Description 2")
        };

        var request = new GetActiveFormDefinitionQuery(1, null, "access.authenticated");

        _formsRepository.SingleOrDefaultAsync(
            Arg.Any<ActiveFormDefinitionByFormIdSpec>(),
            CancellationToken.None
        ).Returns(formWithActiveDefinition);

        _customQuestionsRepository.ListAsync(
            Arg.Any<CustomQuestionSpecifications.ByTenantId>(),
            Arg.Any<CancellationToken>()
        ).Returns(customQuestions);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().NotBeNull();
        result.Value.Id.Should().Be(activeDefinition.Id);
        result.Value.CustomQuestions.Should().HaveCount(2);
        result.Value.CustomQuestions.Should().Contain(customQuestions[0].JsonData);
        result.Value.CustomQuestions.Should().Contain(customQuestions[1].JsonData);
    }

    [Fact]
    public async Task Handle_ValidRequestWithTheme_ReturnsActiveFormDefinitionWithTheme()
    {
        // Arrange
        var formWithActiveDefinition = new Form(SampleData.TENANT_ID, SampleData.FORM_NAME_1, isPublic: true)
        {
            Id = 1
        };
        var activeDefinition = new FormDefinition(SampleData.TENANT_ID, jsonData: SampleData.FORM_DEFINITION_JSON_DATA_1);
        var theme = new Theme(SampleData.TENANT_ID, "Test Theme", "{ \"colors\": { \"primary\": \"#000000\" } }");
        formWithActiveDefinition.AddFormDefinition(activeDefinition);
        formWithActiveDefinition.SetTheme(theme);

        var request = new GetActiveFormDefinitionQuery(1, null, "access.authenticated");

        _formsRepository.SingleOrDefaultAsync(
            Arg.Any<ActiveFormDefinitionByFormIdSpec>(),
            CancellationToken.None
        ).Returns(formWithActiveDefinition);

        _customQuestionsRepository.ListAsync(
            Arg.Any<CustomQuestionSpecifications.ByTenantId>(),
            Arg.Any<CancellationToken>()
        ).Returns(new List<CustomQuestion>());

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().NotBeNull();
        result.Value.Id.Should().Be(activeDefinition.Id);
        result.Value.ThemeJsonData.Should().Be(theme.JsonData);
    }

    #region Access Control Tests

    [Fact]
    public async Task Handle_PrivateFormWithAnonymousUser_ReturnsUnauthorizedResult()
    {
        // Arrange
        var formWithActiveDefinition = new Form(SampleData.TENANT_ID, SampleData.FORM_NAME_1, isPublic: false)
        {
            Id = 1
        };
        var activeDefinition = new FormDefinition(SampleData.TENANT_ID, jsonData: SampleData.FORM_DEFINITION_JSON_DATA_1);
        formWithActiveDefinition.AddFormDefinition(activeDefinition);

        var request = new GetActiveFormDefinitionQuery(1, null, "access.authenticated");

        _formsRepository.SingleOrDefaultAsync(
            Arg.Any<ActiveFormDefinitionByFormIdSpec>(),
            CancellationToken.None
        ).Returns(formWithActiveDefinition);

        _authorizationService.ValidateAccessAsync("access.authenticated", Arg.Any<CancellationToken>())
            .Returns(Result.Unauthorized("Authentication required to access this resource."));

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Unauthorized);
    }

    [Fact]
    public async Task Handle_PrivateFormWithUserWithoutPermission_ReturnsForbiddenResult()
    {
        // Arrange
        var formWithActiveDefinition = new Form(SampleData.TENANT_ID, SampleData.FORM_NAME_1, isPublic: false)
        {
            Id = 1
        };
        var activeDefinition = new FormDefinition(SampleData.TENANT_ID, jsonData: SampleData.FORM_DEFINITION_JSON_DATA_1);
        formWithActiveDefinition.AddFormDefinition(activeDefinition);

        var request = new GetActiveFormDefinitionQuery(1, "123", "access.authenticated");

        _formsRepository.SingleOrDefaultAsync(
            Arg.Any<ActiveFormDefinitionByFormIdSpec>(),
            CancellationToken.None
        ).Returns(formWithActiveDefinition);

        _authorizationService.ValidateAccessAsync("access.authenticated", Arg.Any<CancellationToken>())
            .Returns(Result.Forbidden("Permission 'access.authenticated' required to access this resource."));

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Forbidden);
    }

    [Fact]
    public async Task Handle_PrivateFormWithUserWithPermission_ReturnsActiveFormDefinition()
    {
        // Arrange
        var formWithActiveDefinition = new Form(SampleData.TENANT_ID, SampleData.FORM_NAME_1, isPublic: false)
        {
            Id = 1
        };
        var activeDefinition = new FormDefinition(SampleData.TENANT_ID, jsonData: SampleData.FORM_DEFINITION_JSON_DATA_1);
        formWithActiveDefinition.AddFormDefinition(activeDefinition);

        var request = new GetActiveFormDefinitionQuery(1, "123", "access.authenticated");

        _formsRepository.SingleOrDefaultAsync(
            Arg.Any<ActiveFormDefinitionByFormIdSpec>(),
            CancellationToken.None
        ).Returns(formWithActiveDefinition);

        _customQuestionsRepository.ListAsync(
            Arg.Any<CustomQuestionSpecifications.ByTenantId>(),
            Arg.Any<CancellationToken>()
        ).Returns(new List<CustomQuestion>());

        _authorizationService.ValidateAccessAsync("access.authenticated", Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().NotBeNull();
        result.Value.Id.Should().Be(activeDefinition.Id);
    }

    [Fact]
    public async Task Handle_PublicForm_ReturnsActiveFormDefinitionWithoutPermissionCheck()
    {
        // Arrange
        var formWithActiveDefinition = new Form(SampleData.TENANT_ID, SampleData.FORM_NAME_1, isPublic: true)
        {
            Id = 1
        };
        var activeDefinition = new FormDefinition(SampleData.TENANT_ID, jsonData: SampleData.FORM_DEFINITION_JSON_DATA_1);
        formWithActiveDefinition.AddFormDefinition(activeDefinition);

        var request = new GetActiveFormDefinitionQuery(1, null, "access.authenticated");

        _formsRepository.SingleOrDefaultAsync(
            Arg.Any<ActiveFormDefinitionByFormIdSpec>(),
            CancellationToken.None
        ).Returns(formWithActiveDefinition);

        _customQuestionsRepository.ListAsync(
            Arg.Any<CustomQuestionSpecifications.ByTenantId>(),
            Arg.Any<CancellationToken>()
        ).Returns(new List<CustomQuestion>());

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().NotBeNull();
        result.Value.Id.Should().Be(activeDefinition.Id);

        // Verify permission check was not called for public form
        await _authorizationService.DidNotReceive().ValidateAccessAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    #endregion
}
