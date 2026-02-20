using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.UseCases.FormDefinitions.GetFields;

namespace Endatix.Core.Tests.UseCases.FormDefinitions.GetFields;

public class GetFormDefinitionFieldsHandlerTests
{
    private readonly IRepository<FormDefinition> _formDefinitionsRepository;
    private readonly GetFormDefinitionFieldsHandler _handler;

    public GetFormDefinitionFieldsHandlerTests()
    {
        _formDefinitionsRepository = Substitute.For<IRepository<FormDefinition>>();
        _handler = new GetFormDefinitionFieldsHandler(_formDefinitionsRepository);
    }

    [Fact]
    public async Task Handle_NoDefinitionsFound_ReturnsNotFoundResult()
    {
        // Arrange
        var request = new GetFormDefinitionFieldsQuery(1);
        _formDefinitionsRepository.ListAsync(Arg.Any<FormDefinitionsByFormIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(new List<FormDefinition>());

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.NotFound);
        result.Errors.Should().Contain("No form definitions found for the given form.");
    }

    [Fact]
    public async Task Handle_ValidRequest_ReturnsFields()
    {
        // Arrange
        var jsonData = """
        {
            "pages": [
                {
                    "elements": [
                        { "type": "text", "name": "firstName", "title": "First Name" },
                        { "type": "text", "name": "lastName", "title": "Last Name" }
                    ]
                }
            ]
        }
        """;

        var definition = new FormDefinition(SampleData.TENANT_ID, jsonData: jsonData) { Id = 1 };
        var request = new GetFormDefinitionFieldsQuery(1);

        _formDefinitionsRepository.ListAsync(Arg.Any<FormDefinitionsByFormIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(new List<FormDefinition> { definition });

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().NotBeNull();
        result.Value.Should().HaveCount(2);
        result.Value.Should().Contain(f => f.Name == "firstName" && f.Title == "First Name" && f.Type == "text");
        result.Value.Should().Contain(f => f.Name == "lastName" && f.Title == "Last Name" && f.Type == "text");
    }

    [Fact]
    public async Task Handle_MultipleDefinitions_MergesFields()
    {
        // Arrange
        var jsonData1 = """
        {
            "pages": [
                {
                    "elements": [
                        { "type": "text", "name": "firstName", "title": "First Name" }
                    ]
                }
            ]
        }
        """;

        var jsonData2 = """
        {
            "pages": [
                {
                    "elements": [
                        { "type": "text", "name": "lastName", "title": "Last Name" },
                        { "type": "text", "name": "email", "title": "Email Address" }
                    ]
                }
            ]
        }
        """;

        var definition1 = new FormDefinition(SampleData.TENANT_ID, jsonData: jsonData1) { Id = 1 };
        var definition2 = new FormDefinition(SampleData.TENANT_ID, jsonData: jsonData2) { Id = 2 };

        var request = new GetFormDefinitionFieldsQuery(1);

        _formDefinitionsRepository.ListAsync(Arg.Any<FormDefinitionsByFormIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(new List<FormDefinition> { definition1, definition2 });

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().HaveCount(3);
        result.Value.Should().Contain(f => f.Name == "firstName");
        result.Value.Should().Contain(f => f.Name == "lastName");
        result.Value.Should().Contain(f => f.Name == "email");
    }

    [Fact]
    public async Task Handle_MultipleDefinitionsWithSameField_NewestDefinitionWins()
    {
        // Arrange
        var jsonDataOld = """
        {
            "pages": [
                {
                    "elements": [
                        { "type": "text", "name": "fullName", "title": "Old Title" }
                    ]
                }
            ]
        }
        """;

        var jsonDataNew = """
        {
            "pages": [
                {
                    "elements": [
                        { "type": "text", "name": "fullName", "title": "New Title" }
                    ]
                }
            ]
        }
        """;

        // Handler orders by CreatedAt descending, so we return newest first
        var definitionOld = new FormDefinition(SampleData.TENANT_ID, jsonData: jsonDataOld) { Id = 1 };
        var definitionNew = new FormDefinition(SampleData.TENANT_ID, jsonData: jsonDataNew) { Id = 2 };

        var request = new GetFormDefinitionFieldsQuery(1);

        // Return in order newest first (what handler expects after ordering)
        _formDefinitionsRepository.ListAsync(Arg.Any<FormDefinitionsByFormIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(new List<FormDefinition> { definitionNew, definitionOld });

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Value.Should().HaveCount(1);
        var field = result.Value.First();
        field.Name.Should().Be("fullName");
        field.Title.Should().Be("New Title", "newest definition should win");
    }

    [Fact]
    public async Task Handle_PanelWithNestedElements_RecursesIntoPanel()
    {
        // Arrange
        var jsonData = """
        {
            "pages": [
                {
                    "elements": [
                        {
                            "type": "panel",
                            "name": "personalInfo",
                            "title": "Personal Information",
                            "elements": [
                                { "type": "text", "name": "firstName", "title": "First Name" },
                                { "type": "text", "name": "lastName", "title": "Last Name" }
                            ]
                        }
                    ]
                }
            ]
        }
        """;

        var definition = new FormDefinition(SampleData.TENANT_ID, jsonData: jsonData) { Id = 1 };
        var request = new GetFormDefinitionFieldsQuery(1);

        _formDefinitionsRepository.ListAsync(Arg.Any<FormDefinitionsByFormIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(new List<FormDefinition> { definition });

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Value.Should().HaveCount(2, "panel itself should not be included");
        result.Value.Should().Contain(f => f.Name == "firstName");
        result.Value.Should().Contain(f => f.Name == "lastName");
        result.Value.Should().NotContain(f => f.Name == "personalInfo");
    }

    [Fact]
    public async Task Handle_ElementWithoutName_SkipsElement()
    {
        // Arrange
        var jsonData = """
        {
            "pages": [
                {
                    "elements": [
                        { "type": "text", "title": "No Name Field" },
                        { "type": "text", "name": "validField", "title": "Valid Field" }
                    ]
                }
            ]
        }
        """;

        var definition = new FormDefinition(SampleData.TENANT_ID, jsonData: jsonData) { Id = 1 };
        var request = new GetFormDefinitionFieldsQuery(1);

        _formDefinitionsRepository.ListAsync(Arg.Any<FormDefinitionsByFormIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(new List<FormDefinition> { definition });

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Value.Should().HaveCount(1);
        result.Value.First().Name.Should().Be("validField");
    }

    [Fact]
    public async Task Handle_ElementWithoutType_SkipsElement()
    {
        // Arrange
        var jsonData = """
        {
            "pages": [
                {
                    "elements": [
                        { "name": "noTypeField", "title": "No Type Field" },
                        { "type": "text", "name": "validField", "title": "Valid Field" }
                    ]
                }
            ]
        }
        """;

        var definition = new FormDefinition(SampleData.TENANT_ID, jsonData: jsonData) { Id = 1 };
        var request = new GetFormDefinitionFieldsQuery(1);

        _formDefinitionsRepository.ListAsync(Arg.Any<FormDefinitionsByFormIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(new List<FormDefinition> { definition });

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Value.Should().HaveCount(1);
        result.Value.First().Name.Should().Be("validField");
    }

    [Fact]
    public async Task Handle_ElementWithoutTitle_UseNameAsTitle()
    {
        // Arrange
        var jsonData = """
        {
            "pages": [
                {
                    "elements": [
                        { "type": "text", "name": "fieldName" }
                    ]
                }
            ]
        }
        """;

        var definition = new FormDefinition(SampleData.TENANT_ID, jsonData: jsonData) { Id = 1 };
        var request = new GetFormDefinitionFieldsQuery(1);

        _formDefinitionsRepository.ListAsync(Arg.Any<FormDefinitionsByFormIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(new List<FormDefinition> { definition });

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Value.Should().HaveCount(1);
        var field = result.Value.First();
        field.Name.Should().Be("fieldName");
        field.Title.Should().Be("fieldName", "should use name as title when title is missing");
    }

    [Fact]
    public async Task Handle_DefinitionWithEmptyJsonData_SkipsDefinition()
    {
        // Arrange
        var emptyDefinition = new FormDefinition(SampleData.TENANT_ID, jsonData: "") { Id = 1 };
        var validJsonData = """
        {
            "pages": [
                {
                    "elements": [
                        { "type": "text", "name": "validField", "title": "Valid Field" }
                    ]
                }
            ]
        }
        """;
        var validDefinition = new FormDefinition(SampleData.TENANT_ID, jsonData: validJsonData) { Id = 2 };

        var request = new GetFormDefinitionFieldsQuery(1);

        _formDefinitionsRepository.ListAsync(Arg.Any<FormDefinitionsByFormIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(new List<FormDefinition> { emptyDefinition, validDefinition });

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Value.Should().HaveCount(1);
        result.Value.First().Name.Should().Be("validField");
    }

    [Fact]
    public async Task Handle_AllQuestionTypes_ReturnsAllTypes()
    {
        // Arrange
        var jsonData = """
        {
            "pages": [
                {
                    "elements": [
                        { "type": "text", "name": "textField", "title": "Text Field" },
                        { "type": "radiogroup", "name": "radioField", "title": "Radio Field" },
                        { "type": "checkbox", "name": "checkboxField", "title": "Checkbox Field" },
                        { "type": "dropdown", "name": "dropdownField", "title": "Dropdown Field" },
                        { "type": "rating", "name": "ratingField", "title": "Rating Field" },
                        { "type": "file", "name": "fileField", "title": "File Field" },
                        { "type": "matrix", "name": "matrixField", "title": "Matrix Field" }
                    ]
                }
            ]
        }
        """;

        var definition = new FormDefinition(SampleData.TENANT_ID, jsonData: jsonData) { Id = 1 };
        var request = new GetFormDefinitionFieldsQuery(1);

        _formDefinitionsRepository.ListAsync(Arg.Any<FormDefinitionsByFormIdSpec>(), Arg.Any<CancellationToken>())
            .Returns(new List<FormDefinition> { definition });

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Value.Should().HaveCount(7, "all question types should be returned");
        result.Value.Should().Contain(f => f.Type == "text");
        result.Value.Should().Contain(f => f.Type == "radiogroup");
        result.Value.Should().Contain(f => f.Type == "checkbox");
        result.Value.Should().Contain(f => f.Type == "file");
        result.Value.Should().Contain(f => f.Type == "matrix");
    }
}
