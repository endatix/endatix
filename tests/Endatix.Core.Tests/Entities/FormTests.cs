using Endatix.Core.Entities;

namespace Endatix.Core.Tests.Entities;

public class FormTests
{
    [Fact]
    public void AddFormDefinition_WhenFirstDefinition_SetsItAsActive()
    {
        // Arrange & Act
        var form = new Form("Test Form");

        // Assert
        form?.ActiveDefinition.Should().NotBeNull();
        form?.FormDefinitions.Should().HaveCount(1);
        form?.ActiveDefinition?.IsActive.Should().BeTrue();
        form?.ActiveDefinition?.Should().Be(form?.FormDefinitions.First());
    }

    [Fact]
    public void SetActiveFormDefinition_WhenChangingActive_UpdatesActiveStatusCorrectly()
    {
        // Arrange
        var form = new Form("Test Form");
        form.AddFormDefinition("{\"version\":\"1\"}");
        form.AddFormDefinition("{\"version\":\"2\"}");
        var previousActive = form.ActiveDefinition;
        var newActive = form.FormDefinitions.Last();

        // Act
        form.SetActiveFormDefinition(newActive);

        // Assert
        Assert.False(previousActive?.IsActive);
        Assert.True(newActive.IsActive);
        Assert.Equal(newActive, form.ActiveDefinition);
    }

    [Fact]
    public void SetActiveFormDefinition_WithNonExistingDefinition_ThrowsException()
    {
        // Arrange
        var form = new Form("Test Form");
        var externalDefinition = new FormDefinition(false, "{\"test\":\"data\"}", false);

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(
            () => form.SetActiveFormDefinition(externalDefinition)
        );
        Assert.Contains("doesn't belong to this form", exception.Message);
    }
}
