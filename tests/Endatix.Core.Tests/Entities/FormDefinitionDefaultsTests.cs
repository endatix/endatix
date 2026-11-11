using Endatix.Core.Entities;

namespace Endatix.Core.Tests.Entities;

public class FormDefinitionDefaultsTests
{
    [Fact]
    public void FormDefinition_WithoutJson_UsesFixedDefault()
    {
        var definition = new FormDefinition(SampleData.TENANT_ID);

        definition.JsonData.Should().Be(FormDefinitionDefaults.Json);
        definition.JsonData.Should().Be("{\"logoPosition\": \"right\"}");
    }

    [Fact]
    public void FormTemplate_WithoutJson_UsesFixedDefault()
    {
        var template = new FormTemplate(SampleData.TENANT_ID, SampleData.FORM_NAME_1);

        template.JsonData.Should().Be(FormDefinitionDefaults.Json);
        template.JsonData.Should().Be("{\"logoPosition\": \"right\"}");
    }
}
