using Ardalis.GuardClauses;
using Endatix.Core.Features.Email;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Core.Entities;

/// <summary>
/// Represents an email template stored in the database.
/// </summary>
public class EmailTemplate : BaseEntity, IAggregateRoot
{
    public EmailTemplate(string name, string subject, string htmlContent, string plainTextContent, string fromAddress)
    {
        Guard.Against.NullOrWhiteSpace(name);
        Guard.Against.NullOrWhiteSpace(subject);
        Guard.Against.NullOrWhiteSpace(htmlContent);
        Guard.Against.NullOrWhiteSpace(plainTextContent);
        Guard.Against.NullOrWhiteSpace(fromAddress);

        Name = name;
        Subject = subject;
        HtmlContent = htmlContent;
        PlainTextContent = plainTextContent;
        FromAddress = fromAddress;
    }

    /// <summary>
    /// The unique name/identifier for this template.
    /// </summary>
    public string Name { get; private set; }

    /// <summary>
    /// The subject line for emails using this template.
    /// </summary>
    public string Subject { get; private set; }

    /// <summary>
    /// The HTML content of the email template.
    /// </summary>
    public string HtmlContent { get; private set; }

    /// <summary>
    /// The plain text content of the email template.
    /// </summary>
    public string PlainTextContent { get; private set; }

    /// <summary>
    /// The sender email address for emails using this template.
    /// </summary>
    public string FromAddress { get; private set; }

    /// <summary>
    /// Updates the template content.
    /// </summary>
    public void UpdateContent(string subject, string htmlContent, string plainTextContent, string fromAddress)
    {
        Guard.Against.NullOrWhiteSpace(subject);
        Guard.Against.NullOrWhiteSpace(htmlContent);
        Guard.Against.NullOrWhiteSpace(plainTextContent);
        Guard.Against.NullOrWhiteSpace(fromAddress);

        Subject = subject;
        HtmlContent = htmlContent;
        PlainTextContent = plainTextContent;
        FromAddress = fromAddress;
    }

    /// <summary>
    /// Renders the template with the provided variables.
    /// </summary>
    /// <param name="to">The recipient email address.</param>
    /// <param name="variables">The variables to substitute in the template.</param>
    /// <param name="subject">Optional subject override. If provided, overrides template subject.</param>
    /// <param name="from">Optional from address override. If provided, overrides template from address.</param>
    /// <returns>An EmailWithBody model ready to be sent.</returns>
    public EmailWithBody Render(string to, Dictionary<string, string> variables, string? subject = null, string? from = null)
    {
        Guard.Against.NullOrWhiteSpace(to);
        Guard.Against.Null(variables);

        var htmlContent = ReplaceVariables(HtmlContent, variables);
        var plainTextContent = ReplaceVariables(PlainTextContent, variables);
        
        var finalSubject = !string.IsNullOrEmpty(subject) 
            ? ReplaceVariables(subject, variables)
            : ReplaceVariables(Subject, variables);
        
        var finalFromAddress = !string.IsNullOrEmpty(from) 
            ? from
            : FromAddress;

        return new EmailWithBody
        {
            To = to,
            From = finalFromAddress,
            Subject = finalSubject,
            HtmlBody = htmlContent,
            PlainTextBody = plainTextContent
        };
    }

    private static string ReplaceVariables(string content, Dictionary<string, string> variables)
    {
        var result = content;
        foreach (var (key, value) in variables)
        {
            var placeholder = $"{{{{{key}}}}}";
            result = result.Replace(placeholder, value);
        }
        return result;
    }
} 