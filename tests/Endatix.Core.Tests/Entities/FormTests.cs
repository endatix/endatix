using Endatix.Core.Entities;

namespace Endatix.Core.Tests.Entities;

public class FormTests
{
    [Fact]
    public void AddFormDefinition_WhenFirstDefinition_SetsItAsActive()
    {
        // Arrange & Act
        var form = new Form(SampleData.FORM_NAME_1);
        var formDefinition = new FormDefinition(jsonData: SampleData.FORM_DEFINITION_JSON_DATA_1);
        form.AddFormDefinition(formDefinition);

        // Assert
        form?.ActiveDefinition.Should().NotBeNull();
        form?.FormDefinitions.Should().HaveCount(1);
        form?.ActiveDefinition?.Should().Be(form?.FormDefinitions.First());
    }

    [Fact]
    public void SetActiveFormDefinition_WhenChangingActive_UpdatesActiveDefinitionCorrectly()
    {
        // Arrange
        var form = new Form(SampleData.FORM_NAME_1);
        var formDefinition1 = new FormDefinition(jsonData: SampleData.FORM_DEFINITION_JSON_DATA_1);
        var formDefinition2 = new FormDefinition(jsonData: SampleData.FORM_DEFINITION_JSON_DATA_2);
        form.AddFormDefinition(formDefinition1);
        form.AddFormDefinition(formDefinition2);

        // Act
        form.SetActiveFormDefinition(formDefinition2);

        // Assert
        form.ActiveDefinition.Should().Be(formDefinition2);
    }

    [Fact]
    public void SetActiveFormDefinition_WithNonExistingDefinition_ThrowsException()
    {
        // Arrange
        var form = new Form(SampleData.FORM_NAME_1);
        var externalForm = new Form(SampleData.FORM_NAME_2);
        var externalDefinition = new FormDefinition(jsonData: SampleData.FORM_DEFINITION_JSON_DATA_1);
        externalForm.AddFormDefinition(externalDefinition);

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(
            () => form.SetActiveFormDefinition(externalDefinition)
        );
        Assert.Contains("doesn't belong to this form", exception.Message);
    }
}
