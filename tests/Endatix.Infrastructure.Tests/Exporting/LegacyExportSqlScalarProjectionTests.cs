using System.Reflection;
using Endatix.Framework.Scripts;
using FluentAssertions;

namespace Endatix.Infrastructure.Tests.Exporting;

public sealed class LegacyExportSqlScalarProjectionTests
{
    [Fact]
    public void ReadSqlScript_SqlServerExportV4_ProjectsScalarsViaOpenJson()
    {
        Assembly assembly = Assembly.Load("Endatix.Persistence.SqlServer");

        string script = ScriptReader.ReadSqlScript("Procedures/export_form_submissions_v4.sql", assembly);

        // JSON_QUERY alone drops scalars; v4 must project via OPENJSON types into JSON_MODIFY.
        script.Should().Contain("OPENJSON(r.JsonData)");
        script.Should().Contain("TRY_CONVERT(bigint");
        script.Should().Contain("TRY_CONVERT(float(53)");
        script.Should().Contain("STRING_ESCAPE(@name, 'json')");
        script.Should().NotContain("ISNULL(JSON_QUERY(r.JsonData");
    }
}
