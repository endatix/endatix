using System.Reflection;
using Microsoft.EntityFrameworkCore.Migrations;
using Ardalis.GuardClauses;

namespace Endatix.Framework.Scripts;

/// <summary>
/// Extension methods for MigrationBuilder to read embedded SQL scripts.
/// </summary>
public static class MigrationBuilderExtensions
{
    /// <summary>
    /// Reads an embedded SQL script and returns its content for use in migrations.
    /// </summary>
    /// <param name="migrationBuilder">The migration builder instance</param>
    /// <param name="scriptPath">The path to the SQL script relative to the Scripts folder (e.g., "Functions/export_form_submissions.sql")</param>
    /// <param name="assembly">The assembly containing the embedded resource. If null, uses the calling assembly.</param>
    /// <returns>The SQL script content</returns>
    /// <exception cref="FileNotFoundException">Thrown when the script file is not found</exception>
    /// <exception cref="InvalidOperationException">Thrown when the script content is invalid</exception>
    public static string ReadEmbeddedSqlScript(this MigrationBuilder migrationBuilder, string scriptPath, Assembly? assembly = null)
    {
        Guard.Against.Null(migrationBuilder, nameof(migrationBuilder));
        
        assembly ??= Assembly.GetCallingAssembly();
        
        return ScriptReader.ReadSqlScript(scriptPath, assembly);
    }
} 