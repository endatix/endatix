using System.Globalization;

namespace Endatix.IntegrationTests;

/// <summary>
/// The switches of an opt-in performance run. Performance tests carry <c>Category=Performance</c>, which the CI
/// filters exclude, and skip unless <see cref="EnabledVariable"/> is <c>1</c>.
/// </summary>
/// <remarks>
/// <code>
/// ENDATIX_PERF=1 ENDATIX_PERF_SHAPE=realistic ENDATIX_PERF_ROWS=50000 \
///   dotnet test tests/Endatix.IntegrationTests -c Release --filter "Category=Performance"
/// </code>
/// Add <c>ENDATIX_TEST_DB_PROVIDER=SqlServer</c> for SQL Server. Each run writes a Markdown report to
/// <see cref="OutputDirectory"/>.
/// </remarks>
internal static class PerformanceRun
{
    public const string Category = "Performance";
    public const string EnabledVariable = "ENDATIX_PERF";
    public const string SkipReason = "Performance runs are opt-in: set ENDATIX_PERF=1.";

    public static bool Enabled => Environment.GetEnvironmentVariable(EnabledVariable) == "1";

    /// <summary><c>realistic</c> (default), <c>worst</c> or <c>mid</c>; see <see cref="GridPerformanceShape"/>.</summary>
    public static string ShapeName => Environment.GetEnvironmentVariable("ENDATIX_PERF_SHAPE") ?? "realistic";

    public static int Rows => IntegerOr("ENDATIX_PERF_ROWS", 50_000);

    /// <summary>Measured runs per scenario, after two warm-up runs.</summary>
    public static int Iterations => IntegerOr("ENDATIX_PERF_ITERATIONS", 7);

    public static string OutputDirectory =>
        Environment.GetEnvironmentVariable("ENDATIX_PERF_OUT") ?? Path.Combine(Path.GetTempPath(), "endatix-perf");

    private static int IntegerOr(string variable, int fallback) =>
        int.TryParse(Environment.GetEnvironmentVariable(variable), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;
}
