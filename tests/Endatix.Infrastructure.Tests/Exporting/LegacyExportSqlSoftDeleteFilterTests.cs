using System.Reflection;
using Endatix.Framework.Scripts;
using FluentAssertions;

namespace Endatix.Infrastructure.Tests.Exporting;

public sealed class LegacyExportSqlSoftDeleteFilterTests
{
    [Theory]
    [InlineData("Endatix.Persistence.PostgreSql", "Functions/export_form_submissions_v3.sql", "s.\"IsDeleted\" = false")]
    [InlineData("Endatix.Persistence.PostgreSql", "Functions/export_form_submissions_nested_loops_v3.sql", "s.\"IsDeleted\" = false")]
    [InlineData("Endatix.Persistence.SqlServer", "Procedures/export_form_submissions_v4.sql", "IsDeleted = 0")]
    public void ReadSqlScript_LatestLegacyExportScripts_ContainSoftDeleteFilter(
        string assemblyName,
        string scriptPath,
        string expectedFilter)
    {
        Assembly assembly = Assembly.Load(assemblyName);

        string script = ScriptReader.ReadSqlScript(scriptPath, assembly);

        script.Should().Contain(expectedFilter);
    }
}
