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
    // `SomethingException.Message`, but not `ErrorException`-style false positives from the
    // `IEndUserSafeError` types themselves. Identifier-based reads (`ex.Message`, `error.Message`)
    // are found via the catch clauses that introduce them - see BuildExceptionMessagePattern.
    private const string ExceptionTypeMessagePattern =
        @"(?<!Error)Exception\s*\??\s*\.\s*Message";

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

    // `catch (SomeException name)` / `catch (SomeException name) when (...)`. The name it binds is
    // the only thing that makes `name.Message` a leak, so the scanner learns it per file rather than
    // guessing at a fixed list of conventional spellings.
    private static readonly Regex CatchClause = new(
        @"catch\s*\(\s*[\w.]*Exception(?:\s*<[^>]*>)?\s+(\w+)\s*[)\w]",
        RegexOptions.Compiled);

    [Fact]
    public void OssSrc_ResultFactories_DoNotPassExceptionMessage()
    {
        // Arrange
        string srcRoot = FindOssSrcRoot();
        List<string> leaks = [];

        // Act
        foreach (string file in EnumerateSourceFiles(srcRoot))
        {
            string relativePath = Path.GetRelativePath(srcRoot, file);
            foreach (string leak in FindLeaks(File.ReadAllText(file)))
            {
                leaks.Add($"{relativePath}: {leak}");
            }
        }

        // Assert
        leaks.Should().BeEmpty(
            "Result.Error / Result.Invalid arguments must be author-written; exception text belongs in logs. Leaks:\n{0}",
            string.Join('\n', leaks));
    }

    [Theory]
    [InlineData("""catch (Exception ex) { return Result.Error(ex.Message); }""")]
    [InlineData("""catch (Exception error) { return Result.Error(error.Message); }""")]
    [InlineData("""catch (JsonException e) { return Result.Invalid(new ValidationError(e.Message)); }""")]
    [InlineData("""catch (Exception ex) when (ex is IOException) { return Result.Error(ex.Message); }""")]
    [InlineData("""catch (Exception ex) { return Result.Error($"failed (see logs): {ex.Message}"); }""")]
    [InlineData("""catch (Exception ex) { var e = new ValidationError { ErrorMessage = ex.Message }; }""")]
    [InlineData("""return Result.Error(exception.Message);""")]
    [InlineData("""return Result.Error(HttpRequestException.Message);""")]
    public void Scanner_FlagsExceptionText(string source) =>
        FindLeaks(source).Should().NotBeEmpty();

    [Theory]
    [InlineData("""catch (Exception ex) { return Result.Error(SafeError.MessageOr(ex, "Failed.")); }""")]
    [InlineData("""catch (Exception ex) { logger.LogError(ex, "boom"); return Result.Error("Failed."); }""")]
    [InlineData("""return Result.Error("Import failed (see the log for details).");""")]
    [InlineData("""// Historic note: we used to return ex.Message from Result.Error here.""")]
    [InlineData("""return Result.Invalid(new ValidationError("Use the 'ex.Message' placeholder."));""")]
    [InlineData("""catch (Exception ex) { logger.LogWarning("{Msg}", ex.Message); }""")]
    public void Scanner_AllowsSafeText(string source) =>
        FindLeaks(source).Should().BeEmpty();

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
    /// Every place in <paramref name="source"/> where caught-exception text reaches a client-visible
    /// message, either as a direct Result factory argument or through a <c>ValidationError</c> built
    /// alongside it.
    /// </summary>
    private static IEnumerable<string> FindLeaks(string source)
    {
        // Scan with literals and comments blanked out: a `)` inside a string would otherwise end the
        // argument span early and hide a later `.Message`, and `ex.Message` named in prose is not code.
        string code = BlankOutLiteralsAndComments(source);
        string exceptionMessagePattern = BuildExceptionMessagePattern(code);
        Regex exceptionMessage = new(exceptionMessagePattern, RegexOptions.IgnoreCase);

        // Handlers also build the error indirectly (`ValidationError e = new() { ErrorMessage = ex.Message };`
        // then `Result.Invalid(e)`), which the factory-call scan below cannot see.
        Regex indirect = new(
            @"(?:ErrorMessage|Detail|Title)\s*=\s*[^;]*?(?:" + exceptionMessagePattern + @")"
            + @"|new\s+ValidationError\s*\([^)]*?(?:" + exceptionMessagePattern + @")",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        foreach (Match match in indirect.Matches(code))
        {
            yield return Excerpt(source, match.Index, match.Length);
        }

        foreach (Match factory in FactoryCall.Matches(code))
        {
            int end = FindMatchingCloseParen(code, factory.Index + factory.Length - 1);
            if (end < 0)
            {
                continue;
            }

            if (exceptionMessage.IsMatch(code[factory.Index..end]))
            {
                yield return Excerpt(source, factory.Index, end - factory.Index);
            }
        }
    }

    /// <summary>
    /// Matches a read of a caught exception's <c>Message</c>: the identifiers this file's own
    /// <c>catch</c> clauses bind, plus any <c>*Exception.Message</c> spelled out by type.
    /// </summary>
    private static string BuildExceptionMessagePattern(string code)
    {
        HashSet<string> identifiers = CatchClause.Matches(code)
            .Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

        // Covered without a catch clause in view, so a helper taking an `Exception exception`
        // parameter is policed the same way a catch block is.
        identifiers.Add("exception");
        identifiers.Add("ex");

        return ExceptionTypeMessagePattern
            + @"|(?<!\w)(?:" + string.Join('|', identifiers.Select(Regex.Escape)) + @")\s*\??\s*\.\s*Message\b";
    }

    /// <summary>
    /// Replaces the contents of comments and string/char literals with spaces, keeping every other
    /// character at its original offset so reported excerpts still line up with the real source.
    /// Interpolation holes are left intact - <c>$"{ex.Message}"</c> is code, and a leak.
    /// </summary>
    private static string BlankOutLiteralsAndComments(string source)
    {
        char[] result = source.ToCharArray();
        int i = 0;

        while (i < source.Length)
        {
            if (Matches(source, i, "//"))
            {
                while (i < source.Length && source[i] is not ('\n' or '\r'))
                {
                    result[i++] = ' ';
                }
            }
            else if (Matches(source, i, "/*"))
            {
                while (i < source.Length && !Matches(source, i, "*/"))
                {
                    Blank(result, source, i++);
                }

                i = Math.Min(i + 2, source.Length);
            }
            else if (source[i] is '"' or '\'')
            {
                i = BlankLiteral(result, source, i);
            }
            else
            {
                i++;
            }
        }

        return new string(result);
    }

    /// <summary>
    /// Blanks the literal starting at <paramref name="start"/>; returns the index just past it.
    /// </summary>
    private static int BlankLiteral(char[] result, string source, int start)
    {
        // `$`/`@` prefixes sit immediately before the quote in any order (`$@"..."`, `@$"..."`).
        int prefixStart = start;
        while (prefixStart > 0 && source[prefixStart - 1] is '$' or '@')
        {
            prefixStart--;
        }

        string prefix = source[prefixStart..start];
        bool interpolated = prefix.Contains('$');
        char quote = source[start];

        int quoteRun = 0;
        while (start + quoteRun < source.Length && source[start + quoteRun] == quote)
        {
            quoteRun++;
        }

        // Raw string literal: `"""…"""`, closed by a run of at least as many quotes. No escapes inside.
        if (quote == '"' && quoteRun >= 3)
        {
            return BlankRawLiteral(result, source, start, quoteRun, interpolated);
        }

        bool verbatim = prefix.Contains('@');
        int i = start + 1;
        result[start] = ' ';

        while (i < source.Length)
        {
            char c = source[i];

            if (!verbatim && c == '\\' && i + 1 < source.Length)
            {
                result[i] = ' ';
                result[i + 1] = ' ';
                i += 2;
            }
            else if (verbatim && c == quote && i + 1 < source.Length && source[i + 1] == quote)
            {
                result[i] = ' ';
                result[i + 1] = ' ';
                i += 2;
            }
            else if (interpolated && c == '{')
            {
                i = SkipInterpolationHole(result, source, i);
            }
            else if (c == quote)
            {
                result[i] = ' ';
                return i + 1;
            }
            else if (!verbatim && c is '\n' or '\r')
            {
                // Unterminated - the compiler will complain long before this scanner does.
                return i;
            }
            else
            {
                Blank(result, source, i);
                i++;
            }
        }

        return i;
    }

    private static int BlankRawLiteral(char[] result, string source, int start, int quoteRun, bool interpolated)
    {
        for (int q = 0; q < quoteRun; q++)
        {
            result[start + q] = ' ';
        }

        int i = start + quoteRun;
        while (i < source.Length)
        {
            if (source[i] == '"')
            {
                int run = 0;
                while (i + run < source.Length && source[i + run] == '"')
                {
                    run++;
                }

                if (run >= quoteRun)
                {
                    for (int q = 0; q < run; q++)
                    {
                        result[i + q] = ' ';
                    }

                    return i + run;
                }

                for (int q = 0; q < run; q++)
                {
                    result[i + q] = ' ';
                }

                i += run;
                continue;
            }

            if (interpolated && source[i] == '{')
            {
                i = SkipInterpolationHole(result, source, i);
                continue;
            }

            Blank(result, source, i);
            i++;
        }

        return i;
    }

    /// <summary>
    /// Leaves an interpolation hole's expression in place (it is code), blanking only its braces.
    /// <c>{{</c> is an escaped brace, not a hole.
    /// </summary>
    private static int SkipInterpolationHole(char[] result, string source, int open)
    {
        if (open + 1 < source.Length && source[open + 1] == '{')
        {
            result[open] = ' ';
            result[open + 1] = ' ';
            return open + 2;
        }

        result[open] = ' ';
        int depth = 1;
        int i = open + 1;

        while (i < source.Length)
        {
            if (source[i] == '{')
            {
                depth++;
            }
            else if (source[i] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    result[i] = ' ';
                    return i + 1;
                }
            }

            i++;
        }

        return i;
    }

    private static bool Matches(string source, int index, string token) =>
        index + token.Length <= source.Length && string.CompareOrdinal(source, index, token, 0, token.Length) == 0;

    private static void Blank(char[] result, string source, int index) =>
        result[index] = source[index] is '\n' or '\r' ? source[index] : ' ';

    private static string Excerpt(string source, int start, int length) =>
        source.Substring(start, Math.Min(length, source.Length - start)).ReplaceLineEndings(" ").Trim();

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
