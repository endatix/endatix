using System.Reflection;
using Microsoft.Data.SqlClient;

namespace Endatix.Modules.Jobs.Tests.Shared;

/// <summary>SQL Server failures as SqlClient reports them, for code that tells them apart.</summary>
internal static class SqlServerExceptions
{
    private const string DefaultMessage = "The server refused the command.";

    /// <summary>A failure whose batch raised errors with <paramref name="numbers"/>, in that order.</summary>
    public static SqlException WithNumbers(params int[] numbers) => WithMessage(DefaultMessage, numbers);

    /// <summary>
    /// A failure whose batch raised errors with <paramref name="numbers"/>, each reporting <paramref name="message"/>.
    /// </summary>
    // SqlClient makes its exceptions only internally, from the errors a batch raised, so this calls what it calls.
    public static SqlException WithMessage(string message, params int[] numbers)
    {
        const BindingFlags Internal = BindingFlags.NonPublic | BindingFlags.Instance;
        var errors = (SqlErrorCollection)Activator.CreateInstance(typeof(SqlErrorCollection), nonPublic: true)!;
        var add = typeof(SqlErrorCollection).GetMethod("Add", Internal)!;
        foreach (var number in numbers)
        {
            add.Invoke(errors, [Error(number, message)]);
        }

        var create = typeof(SqlException).GetMethod(
            "CreateException", BindingFlags.NonPublic | BindingFlags.Static, [typeof(SqlErrorCollection), typeof(string)])!;
        return (SqlException)create.Invoke(null, [errors, "16.00.1000"])!;
    }

    /// <summary>A failure with <paramref name="number"/> that wraps <paramref name="inner"/>, as a failed connect does.</summary>
    public static SqlException WithInner(int number, Exception inner)
    {
        var errors = (SqlErrorCollection)Activator.CreateInstance(typeof(SqlErrorCollection), nonPublic: true)!;
        typeof(SqlErrorCollection).GetMethod("Add", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(errors, [Error(number, DefaultMessage)]);
        var create = typeof(SqlException).GetMethod(
            "CreateException",
            BindingFlags.NonPublic | BindingFlags.Static,
            [typeof(SqlErrorCollection), typeof(string), typeof(Guid), typeof(Exception)])!;
        return (SqlException)create.Invoke(null, [errors, "16.00.1000", Guid.Empty, inner])!;
    }

    private static SqlError Error(int number, string message)
    {
        var constructor = typeof(SqlError).GetConstructor(
            BindingFlags.NonPublic | BindingFlags.Instance,
            [typeof(int), typeof(byte), typeof(byte), typeof(string), typeof(string), typeof(string), typeof(int), typeof(Exception)])!;
        return (SqlError)constructor.Invoke([number, (byte)1, (byte)16, "server", message, "", 1, null]);
    }
}
