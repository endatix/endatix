using System.ClientModel;
using Microsoft.Extensions.Options;
using OpenAI.Assistants;
using Endatix.Core.Abstractions;
using Endatix.Core.UseCases.Assistant.DefineForm;

namespace Endatix.Infrastructure.Assistant;

public class AssistantService : IAssistantService
{
#pragma warning disable OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

    private const string ASSISTANT_NAME = "SurveyJS form creator";
    private const string ASSISTANT_INSTRUCTIONS = "You are an assistant that is an expert in SurveyJS and creating forms with it. "
        + "When asked to generate a SurveyJS form definition, you return the JSON of the form definition.";
    private const string NEW_DEFINITION_PROMPT_TEMPLATE = "Create a SurveyJS form based on the following prompt:\n{0}";
    private const string EXISTING_DEFINITION_PROMPT_TEMPLATE = "Following is a SurveyJS form inside triple backticks:\n```{0}```\nAdjust it based on the following prompt:\n{1}";
    private readonly string model = "gpt-4o";
    private readonly AssistantClient client;

    public AssistantService(IOptions<AssistantOptions> assistantOptions)
    {
        client = new(assistantOptions.Value.OpenAiApiKey);
    }

    public async Task<AssistedDefinitionDto> DefineFormAsync(string prompt, string? definition = null, string? assistantId = null, string? threadId = null)
    {
        ThreadRun threadRun;
        if (string.IsNullOrEmpty(assistantId) || string.IsNullOrEmpty(threadId))
        {
            AssistantCreationOptions assistantOptions = new()
            {
                Name = ASSISTANT_NAME,
                Instructions = ASSISTANT_INSTRUCTIONS,
                ResponseFormat = AssistantResponseFormat.CreateJsonObjectFormat()
            };
            OpenAI.Assistants.Assistant assistant = await client.CreateAssistantAsync(model, assistantOptions);

            var assistantPrompt = string.IsNullOrEmpty(definition) ?
                string.Format(NEW_DEFINITION_PROMPT_TEMPLATE, prompt) :
                string.Format(EXISTING_DEFINITION_PROMPT_TEMPLATE, definition, prompt);

            ThreadCreationOptions threadOptions = new()
            {
                InitialMessages = { assistantPrompt }
            };

            threadRun = await client.CreateThreadAndRunAsync(assistant.Id, threadOptions);
        }
        else
        {
            RunCreationOptions newRunOptions = new()
            {
                AdditionalMessages = { prompt }
            };
            threadRun = client.CreateRun(threadId, assistantId, newRunOptions);
        }

        WaitToComplete(threadRun);
        var newDefinition = await GetLastMessage(threadRun.ThreadId);
        return new AssistedDefinitionDto(newDefinition, threadRun.AssistantId, threadRun.ThreadId);
    }

    private void WaitToComplete(ThreadRun threadRun)
    {
        do
        {
            Thread.Sleep(TimeSpan.FromMilliseconds(500));
            threadRun = client.GetRun(threadRun.ThreadId, threadRun.Id);
        } while (!threadRun.Status.IsTerminal);
    }

    private async Task<string> GetLastMessage(string threadId)
    {
        AsyncCollectionResult<ThreadMessage> threadMessages =
            client.GetMessagesAsync(threadId, new MessageCollectionOptions() { Order = MessageCollectionOrder.Ascending });

        var lastMessageContent = string.Empty;
        await foreach (ThreadMessage message in threadMessages)
        {
            foreach (MessageContent contentItem in message.Content)
            {
                if (!string.IsNullOrEmpty(contentItem.Text))
                {
                    lastMessageContent = contentItem.Text;
                }
            }
        }

        return lastMessageContent;
    }
#pragma warning restore OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
}
