namespace Endatix.Core.Abstractions;

/// <summary>
/// Resolves the effective sender address for email templates.
/// </summary>
public interface IEmailTemplateFromAddressResolver
{
    /// <summary>
    /// Resolves the sender address that will be used when sending a template email.
    /// </summary>
    /// <param name="templateName">The database template name.</param>
    /// <param name="databaseFromAddress">The sender address stored on the template.</param>
    /// <returns>The effective sender address.</returns>
    string Resolve(string templateName, string databaseFromAddress);
}
