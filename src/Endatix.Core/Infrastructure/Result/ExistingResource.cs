namespace Endatix.Core.Infrastructure.Result;

/// <summary>
/// The resource a 409 ran into, named so the caller can carry on with it instead of looking it up
/// (<c>submission_exists</c> → <c>submissionId</c>). The problem body gets <paramref name="ErrorCode"/>
/// as <c>errorCode</c> and <paramref name="Id"/> under <paramref name="Field"/>.
/// </summary>
public sealed record ExistingResource(string ErrorCode, string Field, string Id);
