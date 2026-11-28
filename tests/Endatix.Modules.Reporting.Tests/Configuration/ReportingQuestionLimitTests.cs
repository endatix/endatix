using System.Text.Json;
using Endatix.Framework.Modules;
using Endatix.Modules.Reporting.Configuration;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Endatix.Modules.Reporting.Tests.Configuration;

public class ReportingQuestionLimitTests
{
    [Fact]
    public void GetMaxQuestions_WithTenantAndHostSettings_UsesTheExactTenantFirst()
    {
        // Arrange
        using var host = Configure(("MaxQuestions", "1200"), ("MaxQuestionsByTenant:52", "1510"),
            ("MaxQuestionsByTenant:53", "900"), ("SubmitterProfileFields:0", "division"),
            ("ApplyMigrationsAtStartup", "false"));

        // Act
        var options = host.GetRequiredService<IOptions<ReportingOptions>>().Value;

        // Assert
        options.GetMaxQuestions(52).Should().Be(1510);
        options.GetMaxQuestions(53).Should().Be(900);
        options.GetMaxQuestions(54).Should().Be(1200);
        options.SubmitterProfileFields.Should().Equal("division");
        options.ApplyMigrationsAtStartup.Should().BeFalse();
    }

    private static ServiceProvider Configure(params (string Key, string Value)[] settings)
    {
        var values = settings.ToDictionary(setting => $"{ReportingOptions.SECTION_NAME}:{setting.Key}",
            setting => (string?)setting.Value);
        values["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=unused";
        values["ConnectionStrings:DefaultConnection_DbProvider"] = "postgresql";
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        ReportingModule.Instance.ConfigureServices(new EndatixModuleBuilder(services, configuration));
        return services.BuildServiceProvider();
    }

    [Theory]
    [InlineData("MaxQuestions", "0")]
    [InlineData("MaxQuestionsByTenant:52", "-1")]
    [InlineData("MaxQuestionsByTenant:52", "unlimited")]
    [InlineData("MaxQuestionsByTenant:invalid", "1510")]
    [InlineData("MaxQuestionsByTenant:0", "1510")]
    [InlineData("MaxQuestionsByTenant", "1510")]
    public void Bind_InvalidLimit_RejectsConfigurationWithSettingName(string key, string value)
    {
        using var host = Configure((key, value));

        var validate = () => host.GetRequiredService<IStartupValidator>().Validate();

        validate.Should().Throw<OptionsValidationException>().WithMessage($"*{key}*");
    }

    [Fact]
    public void GetMaxQuestions_NoReportingSettings_DefaultsTo1500()
    {
        using var host = Configure();

        var options = host.GetRequiredService<IOptions<ReportingOptions>>().Value;

        options.GetMaxQuestions(52).Should().Be(1500);
    }

    [Theory]
    [InlineData(52, 1510)]
    [InlineData(53, 1500)]
    public void CompilePersisted_AtAndAboveTenantLimit_UsesInclusiveLimit(long tenantId, int expected)
    {
        using var host = Configure(("MaxQuestionsByTenant:52", "1510"));
        var compiler = host.GetRequiredService<FormSchemaCompiler>().ForTenant(tenantId);

        var compiled = compiler.CompilePersisted(Survey(expected));
        var overLimit = () => compiler.CompilePersisted(Survey(expected + 1));

        compiled.FlatteningMap.Columns.Should().HaveCount(expected);
        overLimit.Should().Throw<SchemaCompilationLimitExceededException>()
            .Which.Limit.Should().Be(expected);
    }

    private static string Survey(int count) => JsonSerializer.Serialize(new
    {
        pages = new[] { new { elements = Enumerable.Range(1, count).Select(i => new { type = "text", name = $"q{i}" }) } }
    });
}
