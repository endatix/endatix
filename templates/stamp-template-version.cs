// Stamps EndatixVersion's defaultValue in template.json with the package version.
// Usage: dotnet run --file templates/stamp-template-version.cs -- <source> <dest> <version>
using System.Text.RegularExpressions;

if (args.Length != 3)
{
    await Console.Error.WriteLineAsync("usage: stamp-template-version.cs <source> <dest> <version>");
    return 2;
}

var source = args[0];
var destination = args[1];
var version = args[2];
var json = await File.ReadAllTextAsync(source);
var stamped = Regex.Replace(
    json,
    """("EndatixVersion"[\s\S]*?"defaultValue":\s*")[^"]+""",
    match => match.Groups[1].Value + version,
    RegexOptions.CultureInvariant,
    TimeSpan.FromSeconds(2));

var expected = $"\"defaultValue\": \"{version}\"";
if (!stamped.Contains(expected, StringComparison.Ordinal))
{
    await Console.Error.WriteLineAsync(
        $"Stamped template.json does not set EndatixVersion defaultValue to {version}. Keep a defaultValue string on that symbol.");
    return 1;
}

var directory = Path.GetDirectoryName(Path.GetFullPath(destination));
if (!string.IsNullOrEmpty(directory))
{
    Directory.CreateDirectory(directory);
}

await File.WriteAllTextAsync(destination, stamped);
return 0;
