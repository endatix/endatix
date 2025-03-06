namespace Endatix.Hosting.Builders;

/// <summary>
/// Extension methods for Endatix builders to support setup logging.
/// </summary>
internal static class EndatixBuilderExtensions
{
    /// <summary>
    /// Logs a setup information message.
    /// </summary>
    public static T LogSetupInfo<T>(this T builder, string message, params object?[] args) 
        where T : class
    {
        var parentBuilder = GetRootBuilder(builder);
        parentBuilder.SetupLogger.Information(message, args);
        return builder;
    }

    /// <summary>
    /// Logs a setup warning message.
    /// </summary>
    public static T LogSetupWarning<T>(this T builder, string message, params object?[] args) 
        where T : class
    {
        var parentBuilder = GetRootBuilder(builder);
        parentBuilder.SetupLogger.Warning(message, args);
        return builder;
    }

    /// <summary>
    /// Logs a setup error message.
    /// </summary>
    public static T LogSetupError<T>(this T builder, string message, params object?[] args) 
        where T : class
    {
        var parentBuilder = GetRootBuilder(builder);
        parentBuilder.SetupLogger.Error(message, args);
        return builder;
    }

    private static EndatixBuilder GetRootBuilder<T>(T builder) where T : class
    {
        // Use pattern matching to get the root builder
        return builder switch
        {
            EndatixApiBuilder api => api.Build(),
            EndatixPersistenceBuilder persistence => persistence.Build(),
            EndatixSecurityBuilder security => security.Build(),
            EndatixMessagingBuilder messaging => messaging.Build(),
            EndatixLoggingBuilder logging => logging.Build(),
            EndatixBuilder main => main,
            _ => throw new ArgumentException($"Unsupported builder type: {typeof(T).Name}")
        };
    }
} 