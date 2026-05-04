using Ardalis.GuardClauses;
using Endatix.Core.Authorization.Access;
using Endatix.Core.Abstractions.Authorization.PublicForm;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Authorization.PublicForm;

/// <summary>
/// Mints a form access JWT after the same public-form gate as <see cref="PublicFormAccessContext"/>.
/// </summary>
public sealed record CreateFormAccessTokenCommand : ICommand<Result<FormAccessTokenDto>>
{
    /// <summary>
    /// The ID of the form to create the access token for.
    /// </summary>
    public long FormId { get; init; }

    /// <summary>
    /// The access token to create the access token for.
    /// </summary>
    public string? AccessToken { get; init; }

    /// <summary>
    /// The type of access token to create the access token for.
    /// </summary>
    public SubmissionTokenType? AccessTokenType { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateFormAccessTokenCommand"/> class.
    /// </summary>
    /// <param name="formId">The ID of the form to create the access token for.</param>
    /// <param name="accessToken">The access token to create the access token for.</param>
    /// <param name="accessTokenType">The type of access token to create the access token for.</param>
    public CreateFormAccessTokenCommand(
        long formId,
        string? accessToken = null,
        SubmissionTokenType? accessTokenType = null)
    {
        Guard.Against.NegativeOrZero(formId);

        FormId = formId;
        AccessToken = accessToken;
        AccessTokenType = accessTokenType;
    }
}
