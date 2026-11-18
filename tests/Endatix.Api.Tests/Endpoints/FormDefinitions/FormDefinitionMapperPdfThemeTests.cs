using Endatix.Api.Endpoints.FormDefinitions;
using Endatix.Core.Entities;
using Endatix.Core.UseCases.FormDefinitions;

namespace Endatix.Api.Tests.Endpoints.FormDefinitions;

public sealed class FormDefinitionMapperPdfThemeTests
{
    [Fact]
    public void Map_FormDefinition_LeavesPdfUsesFormThemeUnset()
    {
        var model = FormDefinitionMapper.Map<FormDefinitionModel>(new FormDefinition(1, jsonData: "{}"));

        model.PdfUsesFormTheme.Should().BeNull();
    }

    [Fact]
    public void Map_ActiveDefinition_ReportsPdfUsesFormTheme()
    {
        var dto = new ActiveDefinitionDto(new FormDefinition(1, jsonData: "{}")) { PdfUsesFormTheme = true };

        var model = FormDefinitionMapper.Map<FormDefinitionModel>(dto);

        model.PdfUsesFormTheme.Should().BeTrue();
    }
}
