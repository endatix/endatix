using System.Reflection;
using Endatix.Framework.Scripts;
using FluentAssertions;

namespace Endatix.Infrastructure.Tests.Exporting;

public sealed class LegacyExportSqlTestSubmissionFilterTests
{
    [Theory]
    [InlineData("Endatix.Persistence.PostgreSql", "Functions/export_form_submissions_v5.sql", "include_test_submissions boolean DEFAULT false", "s.\"IsTestSubmission\" = false")]
    [InlineData("Endatix.Persistence.PostgreSql", "Functions/export_form_submissions_nested_loops_v5.sql", "include_test_submissions boolean DEFAULT false", "s.\"IsTestSubmission\" = false")]
    [InlineData("Endatix.Persistence.SqlServer", "Procedures/export_form_submissions_v6.sql", "@include_test_submissions bit = 0", "IsTestSubmission = 0")]
    public void ReadSqlScript_LatestLegacyExportScripts_LeaveOutTestSubmissionsByDefault(
        string assemblyName,
        string scriptPath,
        string expectedParameter,
        string expectedFilter)
    {
        Assembly assembly = Assembly.Load(assemblyName);

        string script = ScriptReader.ReadSqlScript(scriptPath, assembly);

        script.Should().Contain(expectedParameter);
        script.Should().Contain(expectedFilter);
    }
}
