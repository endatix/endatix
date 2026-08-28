using System.Text.RegularExpressions;

namespace Endatix.Api.Tests.Infrastructure;

/// <summary>
/// Guards against leaking exception text into RFC7807 <c>detail</c> via Result factories.
/// </summary>
/// <remarks>
/// The sanctioned way to emit a caught exception's message is <c>SafeError.MessageOr(ex, fallback)</c>,
/// which only returns text from a type that opted in via <c>IEndUserSafeError</c>. That call is what
/// tells a reviewer or a scanner the detail is intentional; a bare <c>ex.Message</c> never is.
/// <see cref="EndUserMessage_IsOnlyReadInsideSafeError"/> keeps that gate from being bypassed.
/// </remarks>
public sealed class ResultFactoryMustNotInterpolateExceptionMessageTests
{
    private const string ExceptionMessagePattern =
        @"(?<!Error)Exception\s*\??\s*\.\s*Message|(?<!\w)ex\.Message|(?<!\w)exception\.Message";

    // Every Result factory whose message ends up in the RFC7807 `detail`. 4xx details are echoed
    // verbatim to the client; only 5xx is scrubbed by EndatixProblemDetails.
    private static readonly Regex FactoryCall = new(
        @"Result(?:<[^>]+>)?\s*\.\s*(Error|Invalid|NotFound|Conflict|Unauthorized|Forbidden|CriticalError|Unavailable)\s*\(",
        RegexOptions.Compiled);

    // A dereference of the opt-in message, e.g. `safe.EndUserMessage`. Declaring the property or
    // implementing the interface is not a read, so definitions are excluded by file name above.
    private static readonly Regex EndUserMessageRead = new(
        @"\.\s*EndUserMessage(?!\s*[{=])",
        RegexOptions.Compiled);

    private static readonly Regex ExceptionMessage = new(
        ExceptionMessagePattern,
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Handlers usually build the error indirectly (`ValidationError e = new() { ErrorMessage = ex.Message };`
    // then `Result.Invalid(e)`), which the factory-call scan above cannot see.
    private static readonly Regex IndirectExceptionMessage = new(
        @"(?:ErrorMessage|Detail|Title)\s*=\s*[^;]*?(?:" + ExceptionMessagePattern + @")"
        + @"|new\s+ValidationError\s*\([^)]*?(?:" + ExceptionMessagePattern + @")",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    [Fact]
    public void OssSrc_ResultFactories_DoNotPassExceptionMessage()
    {
        // Arrange
        string srcRoot = FindOssSrcRoot();
        List<string> leaks = [];

        // Act
        foreach (string file in EnumerateSourceFiles(srcRoot))
        {
            string text = File.ReadAllText(file);

            foreach (Match indirect in IndirectExceptionMessage.Matches(text))
            {
                leaks.Add($"{Path.GetRelativePath(srcRoot, file)}: {indirect.Value.ReplaceLineEndings(" ").Trim()}");
            }

            foreach (Match factory in FactoryCall.Matches(text))
            {
                int start = factory.Index;
                int end = FindMatchingCloseParen(text, factory.Index + factory.Length - 1);
                if (end < 0)
                {
                    continue;
                }

                string argumentSpan = text[start..end];
                if (ExceptionMessage.IsMatch(argumentSpan))
                {
                    leaks.Add($"{Path.GetRelativePath(srcRoot, file)}: {argumentSpan.ReplaceLineEndings(" ").Trim()}");
                }
            }
        }

        // Assert
        leaks.Should().BeEmpty(
            "Result.Error / Result.Invalid arguments must be author-written; exception text belongs in logs. Leaks:\n{0}",
            string.Join('\n', leaks));
    }

    /// <summary>
    /// <c>IEndUserSafeError.EndUserMessage</c> is the one message the codebase may return verbatim, so it
    /// must be read through the single audited gate rather than dereferenced ad hoc. Keeping every read in
    /// <c>SafeError</c> means reviewing the opt-in policy is reviewing one file.
    /// </summary>
    [Fact]
    public void EndUserMessage_IsOnlyReadInsideSafeError()
    {
        // Arrange
        string srcRoot = FindOssSrcRoot();
        List<string> reads = [];

        // Act
        foreach (string file in EnumerateSourceFiles(srcRoot))
        {
            string fileName = Path.GetFileName(file);
            if (fileName is "SafeError.cs" or "IEndUserSafeError.cs"
                or "DomainRuleException.cs" or "DomainValidationException.cs")
            {
                continue;
            }

            if (EndUserMessageRead.IsMatch(File.ReadAllText(file)))
            {
                reads.Add(Path.GetRelativePath(srcRoot, file));
            }
        }

        // Assert
        reads.Should().BeEmpty(
            "EndUserMessage must be read via SafeError.MessageOr, which is the audited gate for "
            + "client-visible exception text. Direct reads:\n{0}",
            string.Join('\n', reads));
    }

    /// <summary>
    /// Hand-written sources only - build output (obj/bin) is generated and not ours to police.
    /// </summary>
    private static IEnumerable<string> EnumerateSourceFiles(string srcRoot) =>
        Directory.EnumerateFiles(srcRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

    private static string FindOssSrcRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string candidate = Path.Combine(dir.FullName, "src");
            if (Directory.Exists(Path.Combine(candidate, "Endatix.Core")))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate oss/src from the test output directory.");
    }

    private static int FindMatchingCloseParen(string text, int openParenIndex)
    {
        int depth = 0;
        for (int i = openParenIndex; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '(')
            {
                depth++;
            }
            else if (c == ')')
            {
                depth--;
                if (depth == 0)
                {
                    return i + 1;
                }
            }
        }

        return -1;
    }
}
