using System.Text.Json;
using Endatix.Modules.Reporting.Shared.SurveyJs;

namespace Endatix.Modules.Reporting.Features.FormSchema.Choices;

/// <summary>Survey-level settings a select question inherits; read once per definition.</summary>
/// <param name="StoreOthersAsComment">The survey's <c>storeOthersAsComment</c>; SurveyJS defaults it to true.</param>
internal sealed record SurveyChoiceDefaults(bool StoreOthersAsComment)
{
    public static SurveyChoiceDefaults Default { get; } = new(StoreOthersAsComment: true);

    public static SurveyChoiceDefaults Of(JsonElement definitionRoot) =>
        definitionRoot.ValueKind == JsonValueKind.Object
            ? new(definitionRoot.GetBooleanProperty(SurveyJsPropertyNames.StoreOthersAsComment, defaultValue: true))
            : Default;
}
