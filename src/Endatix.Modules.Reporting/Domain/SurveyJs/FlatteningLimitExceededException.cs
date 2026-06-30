namespace Endatix.Modules.Reporting.Domain.SurveyJs;

internal sealed class FlatteningLimitExceededException(string message) : InvalidOperationException(message);
