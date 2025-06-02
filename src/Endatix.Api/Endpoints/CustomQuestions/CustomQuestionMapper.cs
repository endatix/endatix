using Endatix.Core.Entities;

namespace Endatix.Api.Endpoints.CustomQuestions;

/// <summary>
/// Mapper from a custom question entity to a custom question API model.
/// </summary>
public static class CustomQuestionMapper
{
    /// <summary>
    /// Maps a custom question entity to a custom question API model.
    /// </summary>
    /// <typeparam name="T">The type of the custom question API model, which inherits CustomQuestionModel.</typeparam>
    /// <param name="question">The custom question entity.</param>
    /// <returns>The mapped custom question API model.</returns>
    public static T Map<T>(CustomQuestion question) where T : CustomQuestionModel, new() => new T
    {
        Id = question.Id.ToString(),
        Name = question.Name,
        Description = question.Description,
        JsonData = question.JsonData,
        CreatedAt = question.CreatedAt,
        ModifiedAt = question.ModifiedAt
    };

    /// <summary>
    /// Maps a collection of custom question entities to a collection of custom question API models.
    /// </summary>
    /// <typeparam name="T">The type of the custom question API model, which inherits CustomQuestionModel.</typeparam>
    /// <param name="questions">The collection of custom question entities.</param>
    /// <returns>A collection of mapped custom question API models.</returns>
    public static IEnumerable<T> Map<T>(IEnumerable<CustomQuestion> questions) where T : CustomQuestionModel, new() =>
        questions.Select(Map<T>).ToList();
} 