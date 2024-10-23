namespace Endatix.Core.UseCases.Assistant.DefineForm;

public record AssistedDefinitionDto(string AssistantResponse, string? Definition, string AssistantId, string ThreadId);
