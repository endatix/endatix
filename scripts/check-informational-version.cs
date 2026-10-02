// Release check: InformationalVersion, read the way the runtime reads it.
// A release build must be exactly <version> or <version>+<commit> (SourceLink).
//
// Usage: dotnet run scripts/check-informational-version.cs -- <assembly.dll> <version>
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Endatix.Scripts;

return await InformationalVersionCheck.Run(args);

// Top-level statements rule out a file-scoped namespace, hence the block (Sonar S3903).
namespace Endatix.Scripts
{
    internal static class InformationalVersionCheck
    {
        public static async Task<int> Run(string[] args)
        {
            if (args.Length != 2)
            {
                await Console.Error.WriteLineAsync("usage: check-informational-version.cs <assembly.dll> <version>");
                return 2;
            }

            var actual = Read(args[0]);
            if (Matches(actual, args[1]))
            {
                await Console.Out.WriteLineAsync($"{Path.GetFileName(args[0])} InformationalVersion: {actual}");
                return 0;
            }

            await ReportMismatch(args[0], actual, args[1]);
            return 1;
        }

        private static bool Matches(string? actual, string expected) =>
            actual == expected || actual?.StartsWith(expected + "+", StringComparison.Ordinal) == true;

        private static async Task ReportMismatch(string path, string? actual, string expected) =>
            await Console.Error.WriteLineAsync(
                $"::error::{path} InformationalVersion is {actual ?? "missing"}, expected {expected}. Is -p:Version passed to dotnet build?");

        private static string? Read(string path)
        {
            using var stream = File.OpenRead(path);
            using var pe = new PEReader(stream);
            var reader = pe.GetMetadataReader();

            foreach (var handle in reader.GetAssemblyDefinition().GetCustomAttributes())
            {
                var attribute = reader.GetCustomAttribute(handle);
                if (AttributeName(reader, attribute) == "AssemblyInformationalVersionAttribute")
                {
                    return StringArgument(reader, attribute);
                }
            }

            return null;
        }

        private static string? AttributeName(MetadataReader reader, CustomAttribute attribute)
        {
            if (attribute.Constructor.Kind != HandleKind.MemberReference)
            {
                return null;
            }

            var constructor = reader.GetMemberReference((MemberReferenceHandle)attribute.Constructor);
            if (constructor.Parent.Kind != HandleKind.TypeReference)
            {
                return null;
            }

            var type = reader.GetTypeReference((TypeReferenceHandle)constructor.Parent);
            return reader.GetString(type.Name);
        }

        private static string? StringArgument(MetadataReader reader, CustomAttribute attribute)
        {
            var blob = reader.GetBlobReader(attribute.Value);
            blob.ReadUInt16();
            return blob.ReadSerializedString();
        }
    }
}
