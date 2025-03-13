using System;
using System.Collections.Generic;
using System.Linq;
using Endatix.Core.Entities;
using Endatix.Core.UseCases.FormTemplates;

namespace Endatix.Api.Endpoints.FormTemplates;

/// <summary>
/// Mapper from a form template entity to a form template API model.
/// </summary>
public static class FormTemplateMapper
{
    /// <summary>
    /// Maps a form template entity to a form template API model.
    /// </summary>
    /// <typeparam name="T">The type of the form template API model, which inherits FormTemplateModel.</typeparam>
    /// <param name="formTemplate">The form template entity.</param>
    /// <returns>The mapped form template API model.</returns>
    public static T Map<T>(FormTemplate formTemplate) where T : FormTemplateModel, new() => new T
    {
        Id = formTemplate.Id.ToString(),
        Name = formTemplate.Name,
        Description = formTemplate.Description,
        JsonData = formTemplate.JsonData,
        IsEnabled = formTemplate.IsEnabled,
        CreatedAt = formTemplate.CreatedAt,
        ModifiedAt = formTemplate.ModifiedAt
    };
}

/// <summary>
/// Extension methods for mapping form template DTOs to API models.
/// </summary>
public static class FormTemplateMapperExtensions
{
    /// <summary>
    /// Maps a collection of form template DTOs to form template API models.
    /// </summary>
    /// <param name="formTemplates">The collection of form template DTOs.</param>
    /// <returns>A collection of mapped form template API models.</returns>
    public static IEnumerable<FormTemplateModelWithoutJsonData> ToFormTemplateModelList(this IEnumerable<FormTemplateDto> formTemplates)
    {
        return formTemplates.Select(formTemplateDto => formTemplateDto.ToFormTemplateModel());
    }

    /// <summary>
    /// Maps a form template DTO to a form template API model.
    /// </summary>
    /// <param name="formTemplateDto">The form template DTO.</param>
    /// <returns>The mapped form template API model.</returns>
    public static FormTemplateModelWithoutJsonData ToFormTemplateModel(this FormTemplateDto formTemplateDto) => new FormTemplateModelWithoutJsonData()
    {
        Id = formTemplateDto.Id,
        Name = formTemplateDto.Name,
        Description = formTemplateDto.Description,
        IsEnabled = formTemplateDto.IsEnabled,
        CreatedAt = formTemplateDto.CreatedAt,
        ModifiedAt = formTemplateDto.ModifiedAt
    };
}
