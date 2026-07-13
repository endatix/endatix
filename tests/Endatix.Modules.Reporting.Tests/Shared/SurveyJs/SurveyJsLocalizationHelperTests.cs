using System.Text.Json;
using Endatix.Modules.Reporting.Shared.SurveyJs;
using FluentAssertions;

namespace Endatix.Modules.Reporting.Tests.Shared.SurveyJs;

public class SurveyJsLocalizationHelperTests
{
    [Fact]
    public void DiscoverLocales_WithLocalizedTitle_CollectsNonDefaultLocales()
    {
        const string definitionJson = """
            {
              "title": {
                "default": "Survey",
                "es": "Encuesta",
                "en-US": "US Survey"
              }
            }
            """;

        using JsonDocument definition = JsonDocument.Parse(definitionJson);

        List<string> locales = SurveyJsLocalizationHelper.DiscoverLocales(definition.RootElement);

        locales.Should().Equal("default", "en-US", "es");
    }

    [Fact]
    public void DiscoverLocales_WithQuestionMetadata_DoesNotTreatPropertyNamesAsLocales()
    {
        const string definitionJson = """
            {
              "pages": [
                {
                  "elements": [
                    {
                      "type": "text",
                      "name": "q1",
                      "title": "Question"
                    }
                  ]
                }
              ]
            }
            """;

        using JsonDocument definition = JsonDocument.Parse(definitionJson);

        List<string> locales = SurveyJsLocalizationHelper.DiscoverLocales(definition.RootElement);

        locales.Should().Equal("default");
    }

    [Fact]
    public void DiscoverLocales_WithExpressionObject_DoesNotCollectNonLocaleKeys()
    {
        const string definitionJson = """
            {
              "triggers": [
                {
                  "type": "runexpression",
                  "expression": "{q1} > 0",
                  "runIf": "{q1} notempty"
                }
              ]
            }
            """;

        using JsonDocument definition = JsonDocument.Parse(definitionJson);

        List<string> locales = SurveyJsLocalizationHelper.DiscoverLocales(definition.RootElement);

        locales.Should().Equal("default");
    }

    [Fact]
    public void DiscoverLocales_WithLocaleOnlyObjectUnderUnknownProperty_CollectsLocales()
    {
        const string definitionJson = """
            {
              "customLabels": {
                "default": "Hello",
                "fr": "Bonjour"
              }
            }
            """;

        using JsonDocument definition = JsonDocument.Parse(definitionJson);

        List<string> locales = SurveyJsLocalizationHelper.DiscoverLocales(definition.RootElement);

        locales.Should().Equal("default", "fr");
    }
}
