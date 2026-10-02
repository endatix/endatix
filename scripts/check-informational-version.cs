// Release check: the InformationalVersion of an assembly, read the way the runtime reads it.
// GET /api/system/version reports this value with SourceLink's +commit removed, so a release
// build must carry exactly <version> or <version>+<commit>.
//
// Usage: dotnet run scripts/check-informational-version.cs -- <assembly.dll> <version>
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

if (args.Length != 2)
{
    Console.Error.WriteLine("usage: check-informational-version.cs <assembly.dll> <version>");
    return 2;
}

var (path, expected) = (args[0], args[1]);
var actual = ReadInformationalVersion(path);

if (actual == expected || actual?.StartsWith(expected + "+", StringComparison.Ordinal) == true)
{
    Console.WriteLine($"{Path.GetFileName(path)} InformationalVersion: {actual}");
    return 0;
}

Console.Error.WriteLine($"::error::{path} InformationalVersion is {actual ?? "missing"}, expected {expected}. Is -p:Version passed to dotnet build?");
return 1;

static string? ReadInformationalVersion(string path)
{
    using var stream = File.OpenRead(path);
    using var pe = new PEReader(stream);
    var reader = pe.GetMetadataReader();

    foreach (var handle in reader.GetAssemblyDefinition().GetCustomAttributes())
    {
        var attribute = reader.GetCustomAttribute(handle);
        if (attribute.Constructor.Kind != HandleKind.MemberReference)
        {
            continue;
        }

        var constructor = reader.GetMemberReference((MemberReferenceHandle)attribute.Constructor);
        var type = reader.GetTypeReference((TypeReferenceHandle)constructor.Parent);
        if (reader.GetString(type.Name) != "AssemblyInformationalVersionAttribute")
        {
            continue;
        }

        // Blob: prolog 0x0001, then the single string argument.
        var blob = reader.GetBlobReader(attribute.Value);
        blob.ReadUInt16();
        return blob.ReadSerializedString();
    }

    return null;
}
