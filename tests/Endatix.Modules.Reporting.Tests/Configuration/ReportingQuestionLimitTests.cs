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

    private static ServiceProvider Configure(params (string Key, string Value)[] settings) =>
        ModuleServices(settings).BuildServiceProvider();

    private static ServiceCollection ModuleServices(params (string Key, string Value)[] settings)
    {
        var values = settings.ToDictionary(setting => $"{ReportingOptions.SECTION_NAME}:{setting.Key}",
            setting => (string?)setting.Value);
        values["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=unused";
        values["ConnectionStrings:DefaultConnection_DbProvider"] = "postgresql";
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        ReportingModule.Instance.ConfigureServices(new EndatixModuleBuilder(services, configuration));
        return services;
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
    [InlineData(0, 2)]
    [InlineData(52, 0)]
    public void Validate_ConfigureAddsInvalidOverride_RejectsEffectiveSetting(long tenantId, int limit)
    {
        var services = ModuleServices();
        services.Configure<ReportingOptions>(options => options.MaxQuestionsByTenant[tenantId] = limit);
        using var host = services.BuildServiceProvider();

        var validate = () => host.GetRequiredService<IStartupValidator>().Validate();

        validate.Should().Throw<OptionsValidationException>().WithMessage($"*MaxQuestionsByTenant:{tenantId}*");
    }

    [Fact]
    public void Validate_PostConfigureReplacesValidBoundOverride_RejectsEffectiveLimit()
    {
        var services = ModuleServices(("MaxQuestionsByTenant:52", "3"));
        services.PostConfigure<ReportingOptions>(options => options.MaxQuestionsByTenant[52] = -1);
        using var host = services.BuildServiceProvider();

        var validate = () => host.GetRequiredService<IStartupValidator>().Validate();

        validate.Should().Throw<OptionsValidationException>().WithMessage("*MaxQuestionsByTenant:52*");
    }

    [Fact]
    public void ForTenant_ReselectAcrossTenants_PreservesEachCompilerAndProfileFields()
    {
        var services = ModuleServices(("MaxQuestions", "2"), ("SubmitterProfileFields:0", "division"));
        services.PostConfigure<ReportingOptions>(options => options.MaxQuestionsByTenant[52] = 3);
        using var host = services.BuildServiceProvider();
        host.GetRequiredService<IStartupValidator>().Validate();
        var root = host.GetRequiredService<FormSchemaCompiler>();

        var tenantA = root.ForTenant(52);
        var tenantB = tenantA.ForTenant(53);
        var backToA = tenantB.ForTenant(52);

        AssertCompilerLimit(tenantB, 2);
        AssertCompilerLimit(backToA, 3);
        AssertCompilerLimit(root, 2);
        AssertCompilerLimit(tenantA, 3);
    }

    private static void AssertCompilerLimit(FormSchemaCompiler compiler, int limit)
    {
        compiler.CompilePersisted(Survey(limit)).FlatteningMap.Columns.Should().HaveCount(limit);
        var overLimit = () => compiler.CompilePersisted(Survey(limit + 1));
        overLimit.Should().Throw<SchemaCompilationLimitExceededException>().Which.Limit.Should().Be(limit);
        compiler.SubmitterProfileColumns.Select(column => column.Key).Should().Equal("submitter_division");
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
