using System.Text.Json;
using Endatix.Modules.Reporting.Domain.SurveyJs;

namespace Endatix.Modules.Reporting.Tests.Domain.SurveyJs;

public class SurveyJsElementTypeTests
{
    [Theory]
    [InlineData("checkbox")]
    [InlineData("tagbox")]
    [InlineData("ranking")]
    [InlineData("matrix")]
    [InlineData("paneldynamic")]
    [InlineData("text")]
    [InlineData("html")]
    [InlineData("empty")]
    [InlineData("expression")]
    [InlineData("multipletext")]
    [InlineData("file")]
    [InlineData("video")]
    [InlineData("imagepicker")]
    [InlineData("slider")]
    [InlineData("buttongroup")]
    [InlineData("page")]
    public void TryResolve_KnownTypes_ReturnsRegisteredInstance(string typeName)
    {
        // Arrange
        SurveyJsElementType expected = SurveyJsElementType.AllTypes.Single(type => type.Matches(typeName));

        // Act
        SurveyJsElementType? resolved = SurveyJsElementType.TryResolve(typeName);

        // Assert
        resolved.Should().Be(expected);
    }

    [Fact]
    public void TryResolve_UnknownType_ReturnsNull()
    {
        SurveyJsElementType.TryResolve("custom-widget").Should().BeNull();
    }

    [Fact]
    public void TryResolve_NumberType_IsNotRegistered()
    {
        SurveyJsElementType.TryResolve("number").Should().BeNull();
    }

    [Fact]
    public void Page_IsContainer()
    {
        SurveyJsElementType.Page.Category.Should().Be(SurveyJsElementCategory.Container);
        SurveyJsElementType.IsContainer("page").Should().BeTrue();
    }

    [Fact]
    public void Expression_IsScalarNotNonData()
    {
        SurveyJsElementType.Expression.Category.Should().Be(SurveyJsElementCategory.Scalar);
        SurveyJsElementType.IsNonData("expression").Should().BeFalse();
    }

    [Fact]
    public void Empty_IsNonData()
    {
        SurveyJsElementType.Empty.Category.Should().Be(SurveyJsElementCategory.NonData);
        SurveyJsElementType.IsNonData("empty").Should().BeTrue();
    }

    [Fact]
    public void ResolveFlattening_ImagePickerMultiSelect_UsesCheckboxChoices()
    {
        using JsonDocument document = JsonDocument.Parse("""{ "type": "imagepicker", "multiSelect": true }""");
        SurveyJsElementType.ResolveFlattening("imagepicker", document.RootElement)
            .Should().Be(SurveyJsFlattening.CheckboxChoices);
    }

    [Fact]
    public void ResolveFlattening_ImagePickerSingleSelect_UsesSimple()
    {
        using JsonDocument document = JsonDocument.Parse("""{ "type": "imagepicker", "multiSelect": false }""");
        SurveyJsElementType.ResolveFlattening("imagepicker", document.RootElement)
            .Should().Be(SurveyJsFlattening.Simple);
    }

    [Fact]
    public void ComplexTypes_ContainsExpandedFlatteningKinds()
    {
        SurveyJsElementType.ComplexTypes
            .Select(type => type.Flattening)
            .Should().OnlyContain(flattening =>
                flattening != SurveyJsFlattening.None &&
                flattening != SurveyJsFlattening.Simple);
    }

    [Fact]
    public void IsDrivingChoiceType_OnlyCheckboxAndRadiogroup()
    {
        SurveyJsElementType.IsDrivingChoiceType("checkbox").Should().BeTrue();
        SurveyJsElementType.IsDrivingChoiceType("radiogroup").Should().BeTrue();
        SurveyJsElementType.IsDrivingChoiceType("tagbox").Should().BeFalse();
    }

    [Fact]
    public void Video_IsFileCategory()
    {
        SurveyJsElementType.Video.Category.Should().Be(SurveyJsElementCategory.File);
        SurveyJsElementType.Video.Flattening.Should().Be(SurveyJsFlattening.File);
    }

    [Fact]
    public void AggregateCollections_AreDerivedFromAllTypes()
    {
        SurveyJsElementType.AllTypes.Should().Contain(SurveyJsElementType.ScalarTypes);
        SurveyJsElementType.AllTypes.Should().Contain(SurveyJsElementType.BaseSelectTypes);
        SurveyJsElementType.AllTypes.Should().Contain(SurveyJsElementType.MatrixTypes);
        SurveyJsElementType.AllTypes.Should().Contain(SurveyJsElementType.FileTypes);
        SurveyJsElementType.AllTypes.Should().Contain(SurveyJsElementType.MultipleTextTypes);
    }
}
