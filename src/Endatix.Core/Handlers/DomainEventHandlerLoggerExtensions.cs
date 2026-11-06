using Endatix.Core.Entities;
using Endatix.Core.Entities.Identity;
using Microsoft.Extensions.Logging;

namespace Endatix.Core.Handlers;

/// <summary>
/// Source-generated trace logging for the default domain event handlers.
/// </summary>
internal static partial class DomainEventHandlerLoggerExtensions
{
    [LoggerMessage(
        EventName = "SubmissionCompletedHandled",
        Level = LogLevel.Trace,
        Message = "Handling Submission Completed event for {@EventData}")]
    public static partial void LogSubmissionCompletedHandled(this ILogger logger, Submission eventData);

    [LoggerMessage(
        EventName = "UserLoggedInHandled",
        Level = LogLevel.Trace,
        Message = "Handling User LoggedIn event for {@EventData}")]
    public static partial void LogUserLoggedInHandled(this ILogger logger, User eventData);

    [LoggerMessage(
        EventName = "UserLoggedOutHandled",
        Level = LogLevel.Trace,
        Message = "Handling User LoggedOut event for {@EventData}")]
    public static partial void LogUserLoggedOutHandled(this ILogger logger, User eventData);

    [LoggerMessage(
        EventName = "UserRegisteredHandled",
        Level = LogLevel.Trace,
        Message = "Handling User Registered event for {@EventData}")]
    public static partial void LogUserRegisteredHandled(this ILogger logger, User eventData);
}
