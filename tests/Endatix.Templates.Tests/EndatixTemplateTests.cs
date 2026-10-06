using System.Diagnostics;
using System.Text.Json;

namespace Endatix.Templates.Tests;

/// <summary>
/// The template is the setup path. These checks fail when a conditional leaves invalid JSON,
/// a flag drops the wrong files, or packing ships a default version that is not the package version.
/// </summary>
public sealed class EndatixTemplateTests
{
    [Fact]
    public async Task SqlServer_is_strict_json_without_saas()
    {
        await using var created = await CreateAsync();

        var settings = await ReadAsync(Path.Combine(created.OutputDirectory, "appsettings.Development.json"));
        using var json = JsonDocument.Parse(settings);
        json.RootElement.GetProperty("ConnectionStrings").GetProperty("DefaultConnection_DbProvider").GetString()
            .Should().Be("sqlserver");
        settings.Should().Contain("Server=localhost");
        settings.Should().NotContain("SaaSManagement");
        settings.Should().NotContain("//#if");

        var project = await ReadAsync(Path.Combine(created.OutputDirectory, "Sample.Api.csproj"));
        project.Should().Contain("Endatix.Hosting");
        project.Should().NotContain("Endatix.SaaS.Management");

        var program = await ReadAsync(Path.Combine(created.OutputDirectory, "Program.cs"));
        program.Should().Contain("ConfigureEndatix()");
        program.Should().NotContain("SaaSManagement");
    }

    [Fact]
    public async Task PostgreSql_with_saas_adds_the_module_and_stays_valid_json()
    {
        await using var created = await CreateAsync("--database", "postgresql", "--saasManagement");

        var settings = await ReadAsync(Path.Combine(created.OutputDirectory, "appsettings.Development.json"));
        using var json = JsonDocument.Parse(settings);
        json.RootElement.GetProperty("ConnectionStrings").GetProperty("DefaultConnection").GetString()
            .Should().Contain("Host=localhost");
        json.RootElement.GetProperty("Endatix").GetProperty("FeatureFlags").GetProperty("SaaSManagement").GetBoolean()
            .Should().BeTrue();

        var project = await ReadAsync(Path.Combine(created.OutputDirectory, "Sample.Api.csproj"));
        project.Should().Contain("Endatix.SaaS.Management");

        var program = await ReadAsync(Path.Combine(created.OutputDirectory, "Program.cs"));
        program.Should().Contain("SaaSManagementModule.Instance");
    }

    [Fact]
    public async Task Packed_template_defaults_EndatixVersion_to_the_package_version()
    {
        var root = RepositoryRoot();
        var output = Path.Combine(Path.GetTempPath(), "endatix-templates-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        try
        {
            const string version = "9.9.9-test";
            var pack = await RunAsync(
                root,
                "dotnet",
                "pack",
                "templates/Endatix.Templates.csproj",
                "-c",
                "Release",
                $"-p:Version={version}",
                "-o",
                output);

            pack.ExitCode.Should().Be(0, pack.Output);

            var nupkg = Path.Combine(output, $"Endatix.Templates.{version}.nupkg");
            var extract = Path.Combine(output, "extracted");
            var unzip = await RunAsync(output, "unzip", "-q", nupkg, "-d", extract);
            unzip.ExitCode.Should().Be(0, unzip.Output);

            var template = await ReadAsync(
                Path.Combine(extract, "content", "endatix-api", ".template.config", "template.json"));
            using var json = JsonDocument.Parse(template);
            json.RootElement.GetProperty("symbols").GetProperty("EndatixVersion").GetProperty("defaultValue").GetString()
                .Should().Be(version);
        }
        finally
        {
            Directory.Delete(output, recursive: true);
        }
    }

    private static async Task<CreatedTemplate> CreateAsync(params string[] extraArgs)
    {
        var root = RepositoryRoot();
        var home = Path.Combine(Path.GetTempPath(), "endatix-cli-" + Guid.NewGuid().ToString("N"));
        var output = Path.Combine(Path.GetTempPath(), "endatix-app-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(home);

        var install = await RunAsync(
            root,
            "dotnet",
            ["new", "install", "./templates/endatix-api", "--force"],
            home);
        install.ExitCode.Should().Be(0, install.Output);

        var createArgs = new List<string> { "new", "endatix", "-n", "Sample.Api", "-o", output, "--force" };
        createArgs.AddRange(extraArgs);
        var create = await RunAsync(root, "dotnet", createArgs, home);
        create.ExitCode.Should().Be(0, create.Output);

        return new CreatedTemplate(home, output);
    }

    private static async Task<ProcessResult> RunAsync(
        string workingDirectory,
        string fileName,
        IReadOnlyList<string> args,
        string? dotnetCliHome = null)
    {
        var start = new ProcessStartInfo(fileName)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var arg in args)
        {
            start.ArgumentList.Add(arg);
        }

        start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        start.Environment["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1";
        start.Environment["DOTNET_NOLOGO"] = "1";
        if (dotnetCliHome is not null)
        {
            start.Environment["DOTNET_CLI_HOME"] = dotnetCliHome;
        }

        using var process = Process.Start(start) ?? throw new InvalidOperationException($"Could not start {fileName}.");
        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return new ProcessResult(process.ExitCode, stdout + stderr);
    }

    private static Task<ProcessResult> RunAsync(string workingDirectory, string fileName, params string[] args) =>
        RunAsync(workingDirectory, fileName, args, dotnetCliHome: null);

    private static Task<string> ReadAsync(string path) =>
        File.ReadAllTextAsync(path, TestContext.Current.CancellationToken);

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Endatix.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("The repository root was not found.");
    }

    private sealed record ProcessResult(int ExitCode, string Output);

    private sealed class CreatedTemplate(string home, string directory) : IAsyncDisposable
    {
        public string OutputDirectory { get; } = directory;

        public ValueTask DisposeAsync()
        {
            System.IO.Directory.Delete(home, recursive: true);
            System.IO.Directory.Delete(OutputDirectory, recursive: true);
            return ValueTask.CompletedTask;
        }
    }
}
