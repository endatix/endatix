using System.Text.Json.Serialization;

namespace Endatix.Core.Authorization.Access;

/// <summary>
/// The type of token used for submission access.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SubmissionTokenType
{
    /// <summary>Short-lived signed token with explicit permissions (e.g. view, edit, export).</summary>
    AccessToken,

    /// <summary>Long-lived token stored on the submission, used for respondent editing.</summary>
    SubmissionToken,

    /// <summary>Short-lived JWT authorizing public data list search/display-value calls for a form</summary>
    FormToken
}
