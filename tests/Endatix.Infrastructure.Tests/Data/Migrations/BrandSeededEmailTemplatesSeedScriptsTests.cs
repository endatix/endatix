using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Endatix.Framework.Scripts;
using FluentAssertions;

namespace Endatix.Infrastructure.Tests.Data.Migrations;

/// <summary>
/// The BrandSeededEmailTemplates migrations read the seed scripts when they run, to learn which template rows are
/// unedited. Editing a script changes what an applied migration means, and re-running one in a new migration would
/// write "Endatix" back over the branded rows. A new default email text belongs in a new migration instead.
/// </summary>
public sealed class BrandSeededEmailTemplatesSeedScriptsTests
{
    public static TheoryData<string, string, string> PinnedScripts() => new()
    {
        { "PostgreSql", "Data/insert_email_verification_template.sql", "47585a57661b0ac094f0359df2dc8ddff5fa161594ba96918998ff27e6852e20" },
        { "PostgreSql", "Data/insert_forgot_password_template.sql", "f766b3393368a49dc0854736476ebe8ef90cb2fb1cec4f96d92d8607475cd6d0" },
        { "PostgreSql", "Data/insert_password_changed_template.sql", "c704afbad6da5bfedd50f4023d1cd0c39e7675434ebed4e78778a3ac306798c2" },
        { "PostgreSql", "Data/insert_tenant_signup_email_templates.sql", "20ba03ec898382fd0e0be58c2db6ee169561bc08b04b97c1e77049e1046147d9" },
        { "SqlServer", "Data/insert_email_verification_template.sql", "42f5f001ab7d1f0080bc71d8b5663b4886638c80d9c653cc7ec246cbbff0593e" },
        { "SqlServer", "Data/insert_forgot_password_template.sql", "317bfcbcd3105414a413559a83c9723691a7ec1979fc11f16682f9fd6a39d701" },
        { "SqlServer", "Data/insert_password_changed_template.sql", "e955c152d37bf39b9b8faa9d70375884a4e3db7a8cba092bb8897e0abd430201" },
        { "SqlServer", "Data/insert_tenant_signup_email_templates.sql", "2849ee9ac2d0bb387f9d0ab86c505732549426f753b1839c7b99a31ed4ef8726" },
    };

    [Theory]
    [MemberData(nameof(PinnedScripts))]
    public void SeedScript_ReadByTheBrandMigration_IsUnchanged(string provider, string script, string sha256)
    {
        // Arrange
        var assembly = Assembly.Load($"Endatix.Persistence.{provider}");

        // Act
        var text = ScriptReader.ReadSqlScript(script, assembly).ReplaceLineEndings("\n");

        // Assert
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)))
            .Should().Be(sha256, "BrandSeededEmailTemplates compares template rows with this script's text");
    }
}
