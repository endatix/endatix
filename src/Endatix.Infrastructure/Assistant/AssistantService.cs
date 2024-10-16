using System;
using System.Threading.Tasks;
using Endatix.Core.Abstractions;
using Microsoft.Extensions.Options;
using OpenAI.Chat;

namespace Endatix.Infrastructure.Assistant;

public class AssistantService : IAssistantService
{
    private readonly string model = "gpt-4o";
    private readonly ChatClient client;

    public AssistantService(IOptions<AssistantOptions> assistantOptions)
    {
        client = new(model: model, apiKey: assistantOptions.Value.OpenAiApiKey);
    }

    public string DefineForm(string prompt, string? definition = null)
    {
        var completionPrompt = "Create a SurveyJS form based on the following prompt:\n" + prompt + "\nReturn only the JSON of the form definition formatted properly and do not return and explanations.";

        ChatCompletion completion = client.CompleteChat(completionPrompt);

        var result = completion.Content[0].Text;

        return result;
    }
}
