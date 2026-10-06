// Stamps EndatixVersion's defaultValue in template.json with the package version.
// Usage: dotnet run --file templates/stamp-template-version.cs -- <source> <dest> <version>
using System.Text.RegularExpressions;

if (args.Length != 3)
{
    Console.Error.WriteLine("usage: stamp-template-version.cs <source> <dest> <version>");
    return 2;
}

var source = args[0];
var destination = args[1];
var version = args[2];
var json = File.ReadAllText(source);
var stamped = Regex.Replace(
    json,
    """("EndatixVersion"[\s\S]*?"defaultValue":\s*")[^"]+""",
    match => match.Groups[1].Value + version,
    RegexOptions.CultureInvariant);

var expected = $"\"defaultValue\": \"{version}\"";
if (!stamped.Contains(expected, StringComparison.Ordinal))
{
    Console.Error.WriteLine(
        $"Stamped template.json does not set EndatixVersion defaultValue to {version}. Keep a defaultValue string on that symbol.");
    return 1;
}

var directory = Path.GetDirectoryName(Path.GetFullPath(destination));
if (!string.IsNullOrEmpty(directory))
{
    Directory.CreateDirectory(directory);
}

File.WriteAllText(destination, stamped);
return 0;
