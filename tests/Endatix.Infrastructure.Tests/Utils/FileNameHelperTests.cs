using Endatix.Infrastructure.Utils;

namespace Endatix.Infrastructure.Tests.Utils;

public sealed class FileNameHelperTests
{
    [Theory]
    [InlineData("normal-file.txt", "normal-file.txt")]
    [InlineData("file/with/slash.txt", "file_with_slash.txt")]
    [InlineData("file name with spaces.txt", "file name with spaces.txt")]
    [InlineData("", "")]
    public void SanitizeFileName_WorksAsExpected(string input, string expected)
    {
        var result = input is null ? null : FileNameHelper.SanitizeFileName(input);
        Assert.Equal(expected, result);
    }
} 