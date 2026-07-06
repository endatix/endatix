namespace Endatix.Modules.Reporting.Features.FormSchema.FormSchema;

internal sealed class SchemaCompilationLimitExceededException : InvalidOperationException
{
    public SchemaCompilationLimitExceededException(
        SchemaCompilationLimitKind limitKind,
        int limit,
        string message,
        int? actual = null,
        string? context = null) : base(message)
    {
        LimitKind = limitKind;
        Limit = limit;
        Actual = actual;
        Context = context;
    }

    public SchemaCompilationLimitKind LimitKind { get; }

    public int Limit { get; }

    public int? Actual { get; }

    public string? Context { get; }
}
