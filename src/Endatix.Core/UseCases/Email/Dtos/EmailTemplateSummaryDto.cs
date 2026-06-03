namespace Endatix.Core.UseCases.Email.Dtos;

/// <summary>
/// DTO for a summary of an email template.
/// </summary>
/// <param name="Id">The ID of the email template.</param>
/// <param name="Name">The name of the email template.</param>
/// <param name="Subject">The subject of the email template.</param>
/// <param name="FromAddress">The from address of the email template.</param>
public record EmailTemplateSummaryDto(long Id, string Name, string Subject, string FromAddress);
