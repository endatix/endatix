using Endatix.Modules.Reporting.Domain;

namespace Endatix.Modules.Reporting.Tests.Domain;

public class SurveyTypeExportMappingTests
{
    [Fact]
    public void Constructor_DefaultsIsDefaultToFalse()
    {
        var mapping = new SurveyTypeExportMapping(tenantId: 1, exportFormatId: 10, surveyTypeId: 100);

        mapping.IsDefault.Should().BeFalse();
    }

    [Fact]
    public void Constructor_AcceptsIsDefault()
    {
        var mapping = new SurveyTypeExportMapping(tenantId: 1, exportFormatId: 10, surveyTypeId: 100, isDefault: true);

        mapping.IsDefault.Should().BeTrue();
    }

    [Fact]
    public void MarkAsDefault_SetsIsDefaultTrue()
    {
        var mapping = new SurveyTypeExportMapping(tenantId: 1, exportFormatId: 10);

        mapping.MarkAsDefault();

        mapping.IsDefault.Should().BeTrue();
    }

    [Fact]
    public void ClearDefault_SetsIsDefaultFalse()
    {
        var mapping = new SurveyTypeExportMapping(tenantId: 1, exportFormatId: 10, isDefault: true);

        mapping.ClearDefault();

        mapping.IsDefault.Should().BeFalse();
    }
}
