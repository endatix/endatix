using System.Text.RegularExpressions;

namespace Endatix.Api.Tests.Infrastructure;

/// <summary>
/// Guards against leaking exception text into RFC7807 <c>detail</c> via Result factories.
/// </summary>
public sealed class ResultFactoryMustNotInterpolateExceptionMessageTests
{
    private static readonly Regex FactoryCall = new(
        @"Result(?:<[^>]+>)?\s*\.\s*(Error|Invalid)\s*\(",
        RegexOptions.Compiled);

    private static readonly Regex ExceptionMessage = new(
        @"(?<!Error)Exception\s*\??\s*\.\s*Message|(?<!\w)ex\.Message|(?<!\w)exception\.Message",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    [Fact]
    public void OssSrc_ResultFactories_DoNotPassExceptionMessage()
    {
        // Arrange
        string srcRoot = FindOssSrcRoot();
        List<string> leaks = [];

        // Act
        foreach (string file in Directory.EnumerateFiles(srcRoot, "*.cs", SearchOption.AllDirectories))
        {
            string text = File.ReadAllText(file);
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
