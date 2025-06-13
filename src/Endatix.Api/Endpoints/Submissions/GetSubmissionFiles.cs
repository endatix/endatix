using FastEndpoints;
using MediatR;

namespace Endatix.Api.Endpoints.Submissions;

/// <summary>
/// Endpoint for downloading files for a submission.
/// </summary>
public class GetSubmissionFiles(IMediator mediator, IHttpClientFactory httpClientFactory) : Endpoint<GetSubmissionFilesRequest>
{
    /// <inheritdoc/>
    public override void Configure()
    {
        Get("forms/{formId}/submissions/{submissionId}/files");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Download files for a submission";
            s.Description = "Downloads use uploaded files for a given submission";
            s.Responses[200] = "The files were successfully downloaded";
            s.Responses[400] = "Invalid input data.";
            s.Responses[404] = "Form not found. Cannot download files";
        });
    }

    /// <inheritdoc/>
    public override async Task HandleAsync(GetSubmissionFilesRequest request, CancellationToken cancellationToken)
    {
        const string IMAGE_URL = "https://endatixstorageci.blob.core.windows.net/dev-user-files/s/1383020408374034432/1383030656518324224/fdcfdf37-6ff0-4958-b61f-e464084d1fb3.jpg";
        try
        {
            var httpClient = httpClientFactory.CreateClient();
            var response = await httpClient.GetAsync(IMAGE_URL, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                await SendNotFoundAsync();
                return;
            }

            HttpContext.MarkResponseStart();
            HttpContext.Response.StatusCode = 200;
            HttpContext.Response.ContentType = "image/jpeg";
            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await stream.CopyToAsync(HttpContext.Response.Body, cancellationToken);
        }
        catch (Exception)
        {
            await SendNotFoundAsync();
        }
    }
}
