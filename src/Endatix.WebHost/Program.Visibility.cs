using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Exposes the implicit entry point to integration tests (WebApplicationFactory).
/// Must live in the global namespace to merge with the compiler-generated <c>Program</c> from top-level statements in <c>Program.cs</c>.
/// </summary>
[SuppressMessage("csharpsquid", "S3903:Types should be defined in named namespaces", Justification = "Partial merge with implicit global Program from top-level statements.")]
public partial class Program;
